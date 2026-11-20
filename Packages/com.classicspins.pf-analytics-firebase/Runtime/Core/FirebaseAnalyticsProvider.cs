using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Core
{
    /// <summary>Main-thread state machine. Holds at most 128 snapshots; failure is latched for this lifetime.</summary>
    public sealed class FirebaseAnalyticsProvider : IAnalyticsProvider, IAnalyticsCommandTarget
    {
        public const int PendingCapacity = 128;
        private const string Prefix = "[pf-analytics-firebase] ";
        private readonly IFirebaseAnalyticsBridge _bridge;
        private readonly IGameConfig _config;
        private readonly IConsentService _consent;
        private readonly ILog _log;
        private readonly Action<Action> _mainThread;
        private readonly Queue<Func<bool>> _pending = new Queue<Func<bool>>();
        private readonly HashSet<string> _eventWarnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _propertyWarnings = new HashSet<string>(StringComparer.Ordinal);
        private bool _starting, _started, _failed, _granted, _adSignals;

        public FirebaseAnalyticsProvider(IFirebaseAnalyticsBridge bridge, IGameConfig config,
            IConsentService consent, ILog log, Action<Action> mainThread)
        {
            _bridge = bridge;
            _config = config;
            _consent = consent;
            _log = log;
            _mainThread = mainThread;
        }

        public string ProviderId => "analytics-firebase";

        public void Send(GameEvent evt)
        {
            Guard(() =>
            {
                if (evt == null || !ObserveConsent()) return;
                // Snapshot now: a custom event may wrap caller-owned mutable collections.
                string name;
                KeyValuePair<string, object>[] snapshot;
                try
                {
                    var writer = new SnapshotWriter(this);
                    if (evt is AdRevenuePaid paid)
                    {
                        name = "ad_impression";
                        writer.Name = name;
                        writer.Write("ad_platform", paid.Mediator == "applovin_max" ? "AppLovin" : paid.Mediator ?? "");
                        writer.Write("ad_source", paid.Network ?? "");
                        writer.Write("ad_format", paid.AdFormat ?? "");
                        writer.Write("ad_unit_name", paid.AdUnitId ?? "");
                        writer.Write("value", paid.Revenue);
                        writer.Write("currency", paid.Currency ?? "");
                    }
                    else
                    {
                        name = evt.WireName();
                        writer.Name = name;
                        evt.WriteParams(writer);
                    }
                    snapshot = writer.Values.ToArray();
                }
                catch (Exception ex)
                {
                    Warn("event serialization failed; event dropped: " + ex.Message);
                    return;
                }
                Accept(() => _bridge.LogEvent(name, snapshot));
            });
        }

        public void SetUserProperty(UserPropKey key, string value)
        {
            Guard(() =>
            {
                if (!ObserveConsent()) return;
                if (!UserPropKeyValidator.IsValid(key))
                {
                    Warn("invalid property name '" + key.Value + "'; refused.");
                    return;
                }
                var name = key.Value;
                var snapshot = Limit(value, 36, name, _propertyWarnings, "property");
                Accept(() => _bridge.SetUserProperty(name, snapshot));
            });
        }

        public void OnAnalyticsConsent(bool granted)
        {
            Guard(() => ApplyConsent(granted));
        }

        public bool Execute(string command, IReadOnlyList<KeyValuePair<string, string>> args)
        {
            try
            {
                if (command == "privacy.forget_me")
                {
                    if ((args != null && args.Count != 0) || !_started) return false;
                    _pending.Clear();
                    // Clear the explicit user identifier as well as local analytics data.
                    var cleared = Run(() => _bridge.SetUserId(null));
                    var reset = Run(() => _bridge.ResetAnalyticsData());
                    return cleared && reset;
                }
                if (command != "set_user_id" || args == null || args.Count != 1
                    || args[0].Key != "id" || string.IsNullOrWhiteSpace(args[0].Value)
                    || args[0].Value.Length > 256) return false;
                if (!ObserveConsent()) return false;
                var id = args[0].Value;
                return Accept(() => _bridge.SetUserId(id));
            }
            catch (Exception ex)
            {
                if (_starting) Fail(ex.Message);
                else Warn("command refused: " + ex.Message);
                return false;
            }
        }

        private bool ObserveConsent()
        {
            var granted = CurrentConsent();
            if (granted != _granted) ApplyConsent(granted);
            return granted;
        }

        private bool CurrentConsent()
            => _consent != null && _consent.IsResolved && _consent.IsGranted(ConsentPurpose.Analytics);

        private void ApplyConsent(bool granted)
        {
            _granted = granted;
            if (!granted) _pending.Clear();
            // A grant notification never starts Firebase. Regrant reuses the originally configured ad signals.
            if (_started)
            {
                if (!granted)
                {
                    DisableCollection();
                }
                else if (!_failed)
                {
                    if (!_bridge.SetConsent(true, _adSignals) || !_bridge.SetCollectionEnabled(true))
                        Fail("consent regrant configuration refused");
                }
            }
        }

        private bool Accept(Func<bool> work)
        {
            if (_failed) { Warn("work refused: initialization failure is latched; restart required."); return false; }
            if (_started) return Run(work);
            if (_pending.Count >= PendingCapacity)
            {
                Warn("pending queue overflow (128); newest work refused.");
                return false;
            }
            _pending.Enqueue(work);
            if (!_starting)
            {
                _starting = true;
                // Object preserves both absence and the actual bundled/remote value type.
                _adSignals = !AdSignalsDenied(_config.Get(new ConfigKey<object>("analytics.firebase.adSignals")));
                try
                {
                    _bridge.Start((ok, error) =>
                    {
                        try { _mainThread(() =>
                        {
                            try { CompleteStart(ok, error); }
                            catch (Exception ex) { Fail("completion failed: " + ex.Message); }
                        }); }
                        catch (Exception ex) { Fail("completion dispatch failed: " + ex.Message); }
                    });
                }
                catch (Exception ex) { Fail("start threw: " + ex.Message); }
            }
            return !_failed;
        }

        private void CompleteStart(bool ok, string error)
        {
            // Ignore a duplicate or late callback after a failure; no implicit restart.
            if (!_starting || _failed) return;
            _starting = false;
            if (!ok) { Fail(error ?? "dependency check refused"); return; }
            _started = true;
            // Withdrawal wins even when CheckAndFixDependenciesAsync finishes afterwards.
            if (!_granted || !CurrentConsent())
            {
                _granted = false;
                _pending.Clear();
                DisableCollection();
                return;
            }
            if (!_bridge.SetConsent(true, _adSignals) || !_bridge.SetCollectionEnabled(true))
            {
                Fail("consent/collection configuration refused");
                return;
            }
            while (_pending.Count > 0 && _granted)
            {
                if (!ObserveConsent()) break;
                Run(_pending.Dequeue());
            }
        }

        private static bool AdSignalsDenied(object value)
        {
            if (value is string text) return text == "0";
            // Config supports int/long/float/double; accept the other numeric CLR primitives too.
            if (value is byte || value is sbyte || value is short || value is ushort || value is int
                || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) == 0d;
            return false;
        }

        private void DisableCollection()
        {
            try
            {
                if (!_bridge.SetCollectionEnabled(false)) Warn("withdrawal collection-off refused by SDK.");
            }
            catch (Exception ex) { Warn("withdrawal collection-off failed: " + ex.Message); }
        }

        private bool Run(Func<bool> work)
        {
            try
            {
                var applied = work();
                if (!applied) Warn("SDK work refused; dropped without retry.");
                return applied;
            }
            catch (Exception ex) { Warn("SDK work failed; dropped: " + ex.Message); return false; }
        }

        private void Fail(string error)
        {
            _starting = false;
            _failed = true;
            _pending.Clear();
            try { if (_started) _bridge.SetCollectionEnabled(false); } catch (Exception) { }
            Warn("initialization failed; held work dropped, no retry this lifetime: " + error);
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                if (_starting) Fail(ex.Message);
                else Warn("provider operation refused: " + ex.Message);
            }
        }

        private void Warn(string text) { try { _log?.Warn(Prefix + text); } catch (Exception) { } }

        private string Limit(string value, int max, string name, HashSet<string> warned, string kind)
        {
            if (value == null || value.Length <= max) return value;
            if (warned.Add(name ?? "")) Warn(kind + " '" + name + "' string value truncated to " + max + " characters.");
            // Do not leave half a UTF-16 surrogate pair in the SDK string.
            var length = max;
            if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length])) length--;
            return value.Substring(0, length);
        }

        private sealed class SnapshotWriter : IEventParamWriter
        {
            private readonly FirebaseAnalyticsProvider _owner;
            internal readonly List<KeyValuePair<string, object>> Values = new List<KeyValuePair<string, object>>();
            internal string Name;
            internal SnapshotWriter(FirebaseAnalyticsProvider owner) { _owner = owner; }
            public void Write(string key, string value)
            {
                if (value != null) Values.Add(new KeyValuePair<string, object>(key,
                    _owner.Limit(value, 100, Name, _owner._eventWarnings, "event")));
            }
            public void Write(string key, long value) => Values.Add(new KeyValuePair<string, object>(key, value));
            public void Write(string key, double value) => Values.Add(new KeyValuePair<string, object>(key, value));
            public void Write(string key, bool value) => Write(key, WireValues.AsLong(value));
        }
    }
}
