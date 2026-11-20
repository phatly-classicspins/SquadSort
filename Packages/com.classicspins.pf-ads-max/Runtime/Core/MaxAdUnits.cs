using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The one owner of the module's ad units: which MAX unit a placement resolves to, which units have
    /// been requested, the auto-load of the format defaults after init, and the exponential load retry.
    /// One Core singleton shared by <see cref="MaxAdsService"/> and <see cref="MaxAdsInitNode"/>, so
    /// "requested", retry state and resolution never disagree.
    /// </summary>
    /// <remarks>
    /// <para><b>Lookup order.</b> For placement <c>P</c>, platform <c>X</c> (<c>android</c> / <c>ios</c>)
    /// and format <c>F</c> (<c>rewarded</c> when <c>ads.P.rewarded</c> is non-zero, else
    /// <c>interstitial</c>): <c>ads.max.X.P.unitId</c> → <c>ads.max.P.unitId</c> →
    /// <c>ads.max.X.F.unitId</c> → <c>ads.max.F.unitId</c>; the first non-empty value wins. With no platform
    /// (the Editor on a non-mobile target) the <c>X</c> tiers are skipped. The platform token arrives by
    /// constructor — Core never reads a Unity define.</para>
    /// <para><b>Requested set.</b> A unit is loaded only once something asked for it: the format defaults
    /// at <see cref="StartAutoLoad"/>, or a placement's unit through <see cref="Request"/> (the service's
    /// <c>Preload</c>, or the first <c>IsReadyAsync</c> / <c>ShowAsync</c>). A request before init is
    /// recorded and loaded at <see cref="StartAutoLoad"/>.</para>
    /// <para><b>Retry.</b> A load failure of a requested unit while the bridge is initialized waits
    /// <c>2^min(attempt, 6)</c> seconds (attempt counted from 1 — 2, 4, …, 64, 64) and loads again;
    /// <see cref="IMaxBridge.OnLoaded"/> resets the unit's count. At most one retry is pending per unit,
    /// and a request while it is pending does not load early. Time is the injected delay — the
    /// installer passes a timeScale-independent one, since the game may pause time while an ad is up.
    /// <see cref="Dispose"/> cancels every pending retry; a cancelled retry loads nothing and throws
    /// nothing.</para>
    /// </remarks>
    public sealed class MaxAdUnits : IDisposable
    {
        private const string Module = "[pf-ads-max] ";

        /// <summary>The platform token for an Android player (the installer maps <c>UNITY_ANDROID</c>).</summary>
        public const string Android = "android";

        /// <summary>The platform token for an iOS player (the installer maps <c>UNITY_IOS</c>).</summary>
        public const string Ios = "ios";

        public const string RewardedFormat = "rewarded";
        public const string InterstitialFormat = "interstitial";

        /// <summary>Seconds between <c>Show</c> and a display signal before the show fails (int; absent or ≤ 0 ⇒ <see cref="DefaultDisplayTimeoutSeconds"/>).</summary>
        public const string DisplayTimeoutKey = "ads.max.displayTimeoutSeconds";

        /// <summary>Analytics auto-log switch (int; absent ⇒ on, <c>0</c> ⇒ off).</summary>
        public const string AutoLogKey = "ads.max.autoLog";

        /// <summary>Dev switch: SDK verbose logging (int; non-zero ⇒ on). Absent ⇒ the bridge is not called.</summary>
        public const string VerboseLoggingKey = "ads.max.verboseLogging";

        /// <summary>Dev list: advertising ids served test ads (string, comma-separated). Absent or empty ⇒ the bridge is not called.</summary>
        public const string TestDeviceIdsKey = "ads.max.testDeviceIds";

        public const int DefaultDisplayTimeoutSeconds = 10;

        /// <summary>The retry delay stops doubling at <c>2^6</c> = 64 s.</summary>
        public const int MaxBackoffExponent = 6;

        private readonly IMaxBridge _max;
        private readonly IGameConfig _config;
        private readonly ILog _log;
        private readonly Func<TimeSpan, CancellationToken, UniTask> _delay;
        private readonly CancellationTokenSource _disposed = new CancellationTokenSource();

        private readonly object _gate = new object();
        private readonly Dictionary<string, UnitState> _units = new Dictionary<string, UnitState>(StringComparer.Ordinal);
        private bool _autoLoadStarted;
        private bool _isDisposed;

        // No defaulted parameters: VContainer resolves EVERY parameter and ignores C# default values. The
        // installer registers this type by factory, passing the platform token and a realtime delay.
        public MaxAdUnits(
            IMaxBridge max,
            IGameConfig config,
            ILog log,
            string platform,
            Func<TimeSpan, CancellationToken, UniTask> delay)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            Platform = string.IsNullOrWhiteSpace(platform) ? null : platform.Trim();

            _max.OnLoaded += Loaded;
            _max.OnLoadFailed += LoadFailed;
        }

        /// <summary>The platform token (<see cref="Android"/> / <see cref="Ios"/>), or null when none.</summary>
        public string Platform { get; }

        // ── keys ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The placement tier key: <c>ads.max.&lt;placement&gt;.unitId</c> (the pre-v2 key, unchanged).</summary>
        public static string UnitIdKey(string placement) => "ads.max." + placement + ".unitId";

        /// <summary>The framework's placement-format key (shared with the ad gate): <c>ads.&lt;placement&gt;.rewarded</c>.</summary>
        public static string RewardedKey(string placement) => "ads." + placement + ".rewarded";

        /// <summary>The format name a unit-id tier uses.</summary>
        public static string FormatOf(bool rewarded) => rewarded ? RewardedFormat : InterstitialFormat;

        /// <summary>Every unit-id key for <paramref name="placement"/>, in lookup order (see the remarks).</summary>
        public static IReadOnlyList<string> UnitIdKeys(string platform, string placement, bool rewarded)
        {
            var keys = new List<string>(4);
            var hasPlatform = !string.IsNullOrWhiteSpace(platform);
            if (hasPlatform) keys.Add(UnitIdKey(platform.Trim() + "." + placement));
            keys.Add(UnitIdKey(placement));
            keys.AddRange(FormatUnitIdKeys(platform, rewarded));
            return keys;
        }

        /// <summary>The format-default tiers only: <c>ads.max.X.F.unitId</c> → <c>ads.max.F.unitId</c>.</summary>
        public static IReadOnlyList<string> FormatUnitIdKeys(string platform, bool rewarded)
        {
            var format = FormatOf(rewarded);
            var keys = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(platform)) keys.Add(UnitIdKey(platform.Trim() + "." + format));
            keys.Add(UnitIdKey(format));
            return keys;
        }

        /// <summary>
        /// The comma-separated list in <paramref name="value"/>, each item trimmed and empties dropped
        /// (a character scan — never <c>string.Split</c>). Null or blank ⇒ an empty array.
        /// </summary>
        public static string[] SplitList(string value)
        {
            var items = new List<string>();
            if (string.IsNullOrEmpty(value)) return items.ToArray();
            var start = 0;
            for (var i = 0; i <= value.Length; i++)
            {
                if (i < value.Length && value[i] != ',') continue;
                var item = value.Substring(start, i - start).Trim();
                if (item.Length > 0) items.Add(item);
                start = i + 1;
            }
            return items.ToArray();
        }

        /// <summary>
        /// Read an int-valued key, telling absent apart from zero: false when the key is absent from every
        /// config layer or holds a value that is not a whole number. A string value counts only when
        /// <see cref="int.TryParse(string, NumberStyles, IFormatProvider, out int)"/> parses it as a plain
        /// integer (so <c>"0"</c> reads as present, not absent); any other string stays absent.
        /// </summary>
        public static bool TryReadInt(IGameConfig config, string key, out int value)
        {
            value = 0;
            if (config == null) return false;
            var raw = config.Get(new ConfigKey<object>(key));
            if (raw == null) return false;
            if (raw is int i) { value = i; return true; }
            if (raw is string s) return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            if (raw is bool) return false;
            try
            {
                var asLong = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                if (Convert.ToDouble(raw, CultureInfo.InvariantCulture) != asLong) return false;
                value = asLong > int.MaxValue ? int.MaxValue : asLong < int.MinValue ? int.MinValue : (int)asLong;
                return true;
            }
            catch (Exception) { return false; }
        }

        // ── resolution ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>Whether <paramref name="placement"/> is rewarded (<c>ads.P.rewarded</c> non-zero; absent ⇒ interstitial).</summary>
        public bool IsRewarded(AdPlacement placement)
            => _config.Get(new ConfigKey<int>(RewardedKey(placement.Value))) != 0;

        /// <summary>The unit-id keys tried for <paramref name="placement"/> on this platform, in order.</summary>
        public IReadOnlyList<string> KeysFor(AdPlacement placement)
            => UnitIdKeys(Platform, placement.Value, IsRewarded(placement));

        /// <summary>The MAX unit id <paramref name="placement"/> resolves to; null when every tier is empty, or
        /// when the placement itself is blank (never falls through to a format default for a nameless
        /// placement).</summary>
        public string Resolve(AdPlacement placement)
            => string.IsNullOrWhiteSpace(placement.Value) ? null : FirstNonEmpty(KeysFor(placement));

        private string FirstNonEmpty(IReadOnlyList<string> keys)
        {
            foreach (var key in keys)
            {
                var value = _config.Get(new ConfigKey<string>(key));
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return null;
        }

        // ── the requested set ───────────────────────────────────────────────────────────────────────────

        /// <summary>Whether something has requested <paramref name="adUnitId"/>.</summary>
        public bool IsRequested(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return false;
            lock (_gate) return _units.ContainsKey(adUnitId);
        }

        /// <summary>
        /// The format <paramref name="adUnitId"/> was last requested as (<paramref name="rewarded"/>); false —
        /// and <paramref name="rewarded"/> false — when nothing has requested it. This is the format
        /// <see cref="MaxAdReadiness"/>'s <c>ready(unit)</c> passes to the bridge.
        /// </summary>
        public bool TryGetRequestedFormat(string adUnitId, out bool rewarded)
        {
            rewarded = false;
            if (string.IsNullOrEmpty(adUnitId)) return false;
            lock (_gate)
            {
                UnitState state;
                if (!_units.TryGetValue(adUnitId, out state)) return false;
                rewarded = state.Rewarded;
                return true;
            }
        }

        /// <summary>
        /// Add <paramref name="adUnitId"/> to the requested set and load it — now when the bridge is
        /// initialized and no retry is pending for it, otherwise at <see cref="StartAutoLoad"/> (before
        /// init) or when the pending retry fires; a load issued here clears a pending
        /// <see cref="UnitState.LoadAtStart"/> so <see cref="StartAutoLoad"/> never repeats it. A later
        /// request of an already-known unit with the other format replaces the stored format (a unit id used
        /// as both formats is a misconfiguration) and logs one warning. Never throws.
        /// </summary>
        public void Request(string adUnitId, bool rewarded)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            bool flipped;
            bool issueLoad;
            lock (_gate)
            {
                if (_isDisposed) return;
                UnitState state;
                if (!_units.TryGetValue(adUnitId, out state))
                {
                    state = new UnitState(rewarded);
                    _units.Add(adUnitId, state);
                    flipped = false;
                }
                else
                {
                    flipped = state.Rewarded != rewarded;
                    state.Rewarded = rewarded;
                }
                if (state.RetryPending)
                {
                    issueLoad = false;
                }
                else if (!SafeIsInitialized())
                {
                    state.LoadAtStart = true;
                    issueLoad = false;
                }
                else
                {
                    state.LoadAtStart = false;
                    issueLoad = true;
                }
            }
            if (flipped)
                _log.Warn(Module + "unit '" + adUnitId + "' was requested as " + FormatOf(!rewarded) + ", now as "
                          + FormatOf(rewarded) + " — a unit id used as both formats is a misconfiguration.");
            if (issueLoad) SafeLoad(adUnitId, rewarded);
        }

        /// <summary>
        /// After a successful SDK init: add the format defaults (<c>ads.max.[X.]rewarded.unitId</c>,
        /// <c>ads.max.[X.]interstitial.unitId</c>) to the requested set and load them, plus every unit
        /// requested before init. Runs once; a call while the bridge is not initialized loads nothing and
        /// logs a warning (so a caller that skips the init check does not fail silently).
        /// </summary>
        public void StartAutoLoad()
        {
            var toLoad = new List<KeyValuePair<string, bool>>();
            lock (_gate)
            {
                if (_isDisposed || _autoLoadStarted) return;
                if (!SafeIsInitialized())
                {
                    _log.Warn(Module + "StartAutoLoad called while the bridge is not initialized — nothing loaded.");
                    return;
                }
                _autoLoadStarted = true;

                foreach (var rewarded in new[] { true, false })
                {
                    string unit;
                    try { unit = FirstNonEmpty(FormatUnitIdKeys(Platform, rewarded)); }
                    catch (Exception ex)
                    {
                        _log.Warn(Module + "auto-load could not read the " + FormatOf(rewarded) + " default unit: " + ex.Message);
                        continue;
                    }
                    if (unit == null || _units.ContainsKey(unit)) continue;
                    _units.Add(unit, new UnitState(rewarded) { LoadAtStart = true });
                }

                foreach (var pair in _units)
                {
                    if (!pair.Value.LoadAtStart || pair.Value.RetryPending) continue;
                    pair.Value.LoadAtStart = false;
                    toLoad.Add(new KeyValuePair<string, bool>(pair.Key, pair.Value.Rewarded));
                }
            }
            foreach (var pair in toLoad) SafeLoad(pair.Key, pair.Value);
        }

        // ── retry ───────────────────────────────────────────────────────────────────────────────────────

        private void Loaded(string adUnitId)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            lock (_gate)
            {
                UnitState state;
                if (_units.TryGetValue(adUnitId, out state)) state.Attempt = 0;
            }
        }

        private void LoadFailed(string adUnitId, string message)
        {
            if (string.IsNullOrEmpty(adUnitId)) return;
            UnitState state;
            TimeSpan wait;
            int attempt;
            lock (_gate)
            {
                if (_isDisposed) return;
                if (!_units.TryGetValue(adUnitId, out state)) return; // nobody requested it
                if (state.RetryPending) return;                       // at most one pending retry
                if (!SafeIsInitialized()) return;                     // no retry before init
                state.Attempt++;
                attempt = state.Attempt;
                state.RetryPending = true;
                wait = BackoffFor(attempt);
            }
            _log.Info(Module + "load of unit '" + adUnitId + "' failed (" + message + ") — retry " + attempt
                      + " in " + wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " s.");
            RetryAsync(adUnitId, state, wait).Forget();
        }

        /// <summary>The wait before retry <paramref name="attempt"/> (counted from 1): <c>2^min(attempt, 6)</c> seconds.</summary>
        public static TimeSpan BackoffFor(int attempt)
        {
            var exponent = Math.Max(1, Math.Min(attempt, MaxBackoffExponent));
            return TimeSpan.FromSeconds(1 << exponent);
        }

        private async UniTaskVoid RetryAsync(string adUnitId, UnitState state, TimeSpan wait)
        {
            try
            {
                await _delay(wait, _disposed.Token);
            }
            catch (OperationCanceledException)
            {
                return; // disposed: no load, no throw
            }
            catch (Exception ex)
            {
                lock (_gate) state.RetryPending = false;
                _log.Warn(Module + "retry delay for unit '" + adUnitId + "' threw: " + ex.Message);
                return;
            }

            lock (_gate)
            {
                state.RetryPending = false;
                if (_isDisposed) return;
                if (!SafeIsInitialized()) return;
            }
            SafeLoad(adUnitId, state.Rewarded);
        }

        // ── plumbing ────────────────────────────────────────────────────────────────────────────────────

        private bool SafeIsInitialized()
        {
            try { return _max.IsInitialized; }
            catch (Exception) { return false; }
        }

        private void SafeLoad(string adUnitId, bool rewarded)
        {
            try { _max.Load(adUnitId, rewarded); }
            catch (Exception ex) { _log.Warn(Module + "load of unit '" + adUnitId + "' threw: " + ex.Message); }
        }

        /// <summary>Cancel every pending retry and stop listening to the bridge.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_isDisposed) return;
                _isDisposed = true;
            }
            _max.OnLoaded -= Loaded;
            _max.OnLoadFailed -= LoadFailed;
            // Cancelled, never disposed: a retry scheduled on another thread may still read its token.
            _disposed.Cancel();
        }

        private sealed class UnitState
        {
            public UnitState(bool rewarded) { Rewarded = rewarded; }

            public bool Rewarded;
            public int Attempt;
            public bool RetryPending;
            public bool LoadAtStart;
        }
    }
}
