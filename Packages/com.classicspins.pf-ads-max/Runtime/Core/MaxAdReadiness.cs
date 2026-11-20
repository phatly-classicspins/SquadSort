using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The module's one readiness state (AD-MOD-11, FR47): which unit each placement is bound to, and the
    /// one <c>ready(unit)</c> function that answers both the pull (<c>IsReadyAsync</c>, through
    /// <see cref="Query"/>) and the push (<see cref="AdReadinessChanged"/>). The only constructor of that
    /// fact in the module. Registered once, as a singleton, and handed to <see cref="MaxAdsService"/> and
    /// <see cref="MaxAdsInitNode"/>.
    /// </summary>
    /// <remarks>
    /// <para><b><c>ready(unit)</c></b> = the SDK is initialized ∧ the unit was requested ∧ the unit is not
    /// being shown ∧ the bridge reports a loaded ad of the format <see cref="MaxAdUnits"/> recorded for it —
    /// evaluated in that order and short-circuited, so the bridge is never asked before init or about a unit
    /// nobody requested. A bridge that throws reads not-ready, with a warning.</para>
    /// <para><b>Binding is independent of SDK state.</b> <see cref="Bind(AdPlacement)"/> / <see cref="Query"/>
    /// bind a placement to the unit it resolves to (possibly none) whether or not the SDK is initialized,
    /// and request an unrequested unit through <see cref="MaxAdUnits.Request"/> (before init that request is
    /// recorded and loaded at auto-load). Every value a bind reads is evaluated <b>before</b> the request it
    /// makes, so the first query of an unrequested unit answers not-ready and the load that request starts
    /// pushes the change.</para>
    /// <para><b>Per-placement last-known value.</b> A new binding records its baseline and never publishes
    /// it. Every trigger — load, load-fail, show-start, show-end — computes <c>ready(unit)</c> once and
    /// publishes it, in first-bind order, for every placement bound to that unit whose last-known value
    /// differs. A config change re-resolves every binding and publishes a changed value. A pull never
    /// publishes: it re-resolves its own binding (a different unit rebinds silently) and records the value
    /// it returns as that placement's last-known value, so a caller that pulled <c>false</c> — after a silent
    /// rebind to an unrequested unit, or an ad that expired with no callback — is pushed <c>true</c> when
    /// the unit next loads.
    /// <b>Init completion is the one forced publish:</b> every tracked placement receives its settled value
    /// (false after a failed init), whatever its last-known value.</para>
    /// <para><b>Publishes run outside the lock.</b> A throwing publisher is a warning, and the last-known
    /// value is recorded anyway. Callbacks arrive on the main thread (AD-MOD-2); the lock guards the fields
    /// and never marshals. The config change subscription is taken lazily, on the first bind (the race rule:
    /// config is not read at construction); a config whose <c>OnChanged</c> throws is a warning, and its
    /// placements are then re-resolved only by a later pull.</para>
    /// </remarks>
    public sealed class MaxAdReadiness : IDisposable
    {
        private const string Module = "[pf-ads-max] ";

        private readonly IMaxBridge _max;
        private readonly MaxAdUnits _units;
        private readonly IGameConfig _config;
        private readonly IAdReadinessChangedPublisher _publisher;
        private readonly ILog _log;

        private readonly object _gate = new object();
        private readonly List<Binding> _bindings = new List<Binding>();
        private readonly Dictionary<string, int> _showing = new Dictionary<string, int>(StringComparer.Ordinal);
        private bool _configSubscribeAttempted;
        private IDisposable _configSubscription;
        private bool _isDisposed;

        public MaxAdReadiness(
            IMaxBridge max,
            MaxAdUnits units,
            IGameConfig config,
            IAdReadinessChangedPublisher publisher,
            ILog log)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _max.OnLoaded += Loaded;
            _max.OnLoadFailed += LoadFailed;
        }

        // ── ready(unit) ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>The one readiness function: initialized ∧ requested ∧ not showing ∧ the bridge's ready
        /// read with the requested format. Null or empty <paramref name="unitId"/> ⇒ false. Never throws.</summary>
        public bool Ready(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return false;
            if (!SafeIsInitialized()) return false;
            bool rewarded;
            if (!_units.TryGetRequestedFormat(unitId, out rewarded)) return false;
            lock (_gate)
            {
                if (IsShowingLocked(unitId)) return false;
            }
            try
            {
                return _max.IsReady(unitId, rewarded);
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness read of unit '" + unitId + "' threw — read as not ready: " + ex.Message);
                return false;
            }
        }

        // ── binding ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The pull: bind <paramref name="placement"/> and return <c>ready(unit)</c> evaluated before
        /// any request this call makes. Never publishes. An unresolvable placement reads false. A config read
        /// that throws propagates (the service turns it into a warning and <c>false</c>).</summary>
        public bool Query(AdPlacement placement)
        {
            bool ready;
            bool wasRequested;
            BindCore(placement, true, out ready, out wasRequested);
            return ready;
        }

        /// <summary>Bind <paramref name="placement"/> to its resolved unit and request that unit when nobody has
        /// yet. Returns the unit id, or null when none resolves. A config read that throws propagates.</summary>
        public string Bind(AdPlacement placement)
        {
            bool wasRequested;
            return Bind(placement, out wasRequested);
        }

        /// <summary><see cref="Bind(AdPlacement)"/>, also telling whether the unit was already requested before
        /// this call (false for no unit).</summary>
        public string Bind(AdPlacement placement, out bool wasRequested)
        {
            bool ready;
            return BindCore(placement, false, out ready, out wasRequested);
        }

        private string BindCore(AdPlacement placement, bool query, out bool ready, out bool wasRequested)
        {
            var unitId = _units.Resolve(placement);
            var rewarded = unitId != null && _units.IsRewarded(placement);
            wasRequested = _units.IsRequested(unitId);

            bool isNew;
            lock (_gate) isNew = FindLocked(placement) == null;

            // Evaluated before the request below: an unrequested unit reads false with no bridge call.
            ready = query || isNew ? Ready(unitId) : false;

            var subscribe = false;
            lock (_gate)
            {
                var binding = FindLocked(placement);
                if (binding == null)
                {
                    _bindings.Add(new Binding(placement, unitId, ready)); // the baseline — never published
                    if (!_configSubscribeAttempted)
                    {
                        _configSubscribeAttempted = true;
                        subscribe = true;
                    }
                }
                else
                {
                    // A bind that resolves a different unit rebinds silently (no publish).
                    binding.UnitId = unitId;
                    // The pull's answer is what its caller now knows: record it (still publishing nothing), so
                    // the next trigger that differs from it — e.g. the load of a unit the pull read as not
                    // ready — is pushed.
                    if (query) binding.LastKnown = ready;
                }
            }

            if (subscribe) SubscribeToConfig();
            if (unitId != null && !wasRequested) _units.Request(unitId, rewarded);
            return unitId;
        }

        // ── triggers ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Show-start: mark <paramref name="unitId"/> showing (a counter per unit) and publish the
        /// transition. Never throws.</summary>
        public void ShowStarted(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            try
            {
                lock (_gate)
                {
                    int count;
                    _showing.TryGetValue(unitId, out count);
                    _showing[unitId] = count + 1;
                }
                Transition(unitId);
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness show-start of unit '" + unitId + "' threw: " + ex.Message);
            }
        }

        /// <summary>Show-end: unmark <paramref name="unitId"/> (a no-op when it is not marked) and publish the
        /// transition. Never throws.</summary>
        public void ShowEnded(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            try
            {
                lock (_gate)
                {
                    int count;
                    if (!_showing.TryGetValue(unitId, out count)) return;
                    if (count <= 1) _showing.Remove(unitId);
                    else _showing[unitId] = count - 1;
                }
                Transition(unitId);
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness show-end of unit '" + unitId + "' threw: " + ex.Message);
            }
        }

        /// <summary>SDK init has completed (successfully or not): publish the settled value for every tracked
        /// placement, whatever its last-known value — the one forced publish. Never throws.</summary>
        public void InitCompleted()
        {
            try
            {
                var units = new List<string>();
                lock (_gate)
                {
                    if (_isDisposed) return;
                    foreach (var binding in _bindings)
                        if (binding.UnitId != null && !units.Contains(binding.UnitId)) units.Add(binding.UnitId);
                }

                var values = new Dictionary<string, bool>(StringComparer.Ordinal);
                foreach (var unitId in units) values[unitId] = Ready(unitId);

                var facts = new List<AdReadinessChanged>();
                lock (_gate)
                {
                    foreach (var binding in _bindings)
                    {
                        bool value;
                        if (binding.UnitId == null) value = false;
                        else if (!values.TryGetValue(binding.UnitId, out value)) continue; // bound meanwhile: its baseline is current
                        binding.LastKnown = value;
                        facts.Add(new AdReadinessChanged(binding.Placement, value));
                    }
                }
                Publish(facts);
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness settle at init completion threw: " + ex.Message);
            }
        }

        private void Loaded(string adUnitId) => SafeTransition(adUnitId, "load");

        private void LoadFailed(string adUnitId, string message) => SafeTransition(adUnitId, "load failure");

        private void SafeTransition(string unitId, string trigger)
        {
            try { Transition(unitId); }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness after the " + trigger + " of unit '" + unitId + "' threw: " + ex.Message);
            }
        }

        /// <summary>Compute <c>ready(unit)</c> once and publish it for every placement bound to the unit whose
        /// last-known value differs, in first-bind order.</summary>
        private void Transition(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            lock (_gate)
            {
                if (_isDisposed || !AnyBoundLocked(unitId)) return; // no bridge read for a unit nobody watches
            }

            var value = Ready(unitId);
            var facts = new List<AdReadinessChanged>();
            lock (_gate)
            {
                foreach (var binding in _bindings)
                {
                    if (!string.Equals(binding.UnitId, unitId, StringComparison.Ordinal) || binding.LastKnown == value) continue;
                    binding.LastKnown = value;
                    facts.Add(new AdReadinessChanged(binding.Placement, value));
                }
            }
            Publish(facts);
        }

        /// <summary>A config change: re-resolve every binding, evaluate each unit once (before any request),
        /// publish what changed, then request any newly bound unit nobody had requested.</summary>
        private void ConfigChanged()
        {
            var placements = new List<AdPlacement>();
            lock (_gate)
            {
                if (_isDisposed) return;
                foreach (var binding in _bindings) placements.Add(binding.Placement);
            }

            var plans = new List<Rebinding>();
            foreach (var placement in placements)
            {
                try
                {
                    var unitId = _units.Resolve(placement);
                    plans.Add(new Rebinding
                    {
                        Placement = placement,
                        UnitId = unitId,
                        Rewarded = unitId != null && _units.IsRewarded(placement),
                        WasRequested = _units.IsRequested(unitId),
                    });
                }
                catch (Exception ex)
                {
                    _log.Warn(Module + "re-resolving placement '" + placement.Value + "' after a config change threw — binding kept: " + ex.Message);
                }
            }

            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var plan in plans)
                if (plan.UnitId != null && !values.ContainsKey(plan.UnitId)) values[plan.UnitId] = Ready(plan.UnitId);

            var facts = new List<AdReadinessChanged>();
            lock (_gate)
            {
                foreach (var plan in plans)
                {
                    var binding = FindLocked(plan.Placement);
                    if (binding == null) continue;
                    binding.UnitId = plan.UnitId;
                    var value = plan.UnitId != null && values[plan.UnitId];
                    if (binding.LastKnown == value) continue;
                    binding.LastKnown = value;
                    facts.Add(new AdReadinessChanged(binding.Placement, value));
                }
            }
            Publish(facts);

            var requested = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plan in plans)
            {
                if (plan.UnitId == null || plan.WasRequested || !requested.Add(plan.UnitId)) continue;
                _units.Request(plan.UnitId, plan.Rewarded);
            }
        }

        // ── plumbing ────────────────────────────────────────────────────────────────────────────────────

        private void Publish(List<AdReadinessChanged> facts)
        {
            foreach (var fact in facts)
            {
                try
                {
                    _publisher.Publish(fact);
                }
                catch (Exception ex)
                {
                    _log.Warn(Module + "AdReadinessChanged('" + fact.Placement.Value + "', " + (fact.Ready ? "true" : "false")
                              + ") publish threw: " + ex.Message);
                }
            }
        }

        private void SubscribeToConfig()
        {
            IDisposable subscription;
            try
            {
                subscription = _config.OnChanged.Subscribe(new ConfigObserver(this));
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "could not subscribe to config changes — a placement is re-resolved only when it is "
                          + "queried again: " + ex.Message);
                return;
            }

            var disposeNow = false;
            lock (_gate)
            {
                if (_isDisposed) disposeNow = true;
                else _configSubscription = subscription;
            }
            if (disposeNow) subscription?.Dispose();
        }

        private bool SafeIsInitialized()
        {
            try { return _max.IsInitialized; }
            catch (Exception ex)
            {
                _log.Warn(Module + "IsInitialized threw — read as not ready: " + ex.Message);
                return false;
            }
        }

        private Binding FindLocked(AdPlacement placement)
        {
            foreach (var binding in _bindings)
                if (string.Equals(binding.Placement.Value, placement.Value, StringComparison.Ordinal)) return binding;
            return null;
        }

        private bool AnyBoundLocked(string unitId)
        {
            foreach (var binding in _bindings)
                if (string.Equals(binding.UnitId, unitId, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool IsShowingLocked(string unitId)
        {
            int count;
            return _showing.TryGetValue(unitId, out count) && count > 0;
        }

        /// <summary>Stop listening to the bridge and to config changes. Publishes nothing afterwards.</summary>
        public void Dispose()
        {
            IDisposable subscription;
            lock (_gate)
            {
                if (_isDisposed) return;
                _isDisposed = true;
                subscription = _configSubscription;
                _configSubscription = null;
            }
            _max.OnLoaded -= Loaded;
            _max.OnLoadFailed -= LoadFailed;
            try { subscription?.Dispose(); }
            catch (Exception ex) { _log.Warn(Module + "disposing the config subscription threw: " + ex.Message); }
        }

        private sealed class Binding
        {
            public Binding(AdPlacement placement, string unitId, bool lastKnown)
            {
                Placement = placement;
                UnitId = unitId;
                LastKnown = lastKnown;
            }

            public readonly AdPlacement Placement;
            public string UnitId;
            public bool LastKnown;
        }

        private sealed class Rebinding
        {
            public AdPlacement Placement;
            public string UnitId;
            public bool Rewarded;
            public bool WasRequested;
        }

        private sealed class ConfigObserver : IObserver<Unit>
        {
            private readonly MaxAdReadiness _owner;

            public ConfigObserver(MaxAdReadiness owner) { _owner = owner; }

            public void OnNext(Unit value)
            {
                try { _owner.ConfigChanged(); }
                catch (Exception ex) { _owner._log.Warn(Module + "readiness rebind after a config change threw: " + ex.Message); }
            }

            public void OnError(Exception error) { }

            public void OnCompleted() { }
        }
    }
}
