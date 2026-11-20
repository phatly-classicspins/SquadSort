using System;
using System.Collections.Generic;
using System.Globalization;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.Core
{
    /// <summary>Main-thread, synchronous lazy startup; failures are latched and no work is retried or queued.</summary>
    public sealed class AdjustAnalyticsProvider : IAnalyticsProvider, IAnalyticsCommandTarget
    {
        // Adjust v5.8.2 AdjustAdRevenue accepts a source string; Adjust's AppLovin guide specifies this value.
        public const string AppLovinRevenueSource = "applovin_max_sdk";
        private readonly IAdjustAnalyticsBridge _bridge;
        private readonly IGameConfig _config;
        private readonly IConsentService _consent;
        private readonly ILog _log;
        private readonly HashSet<string> _missingTokens = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _properties = new HashSet<string>(StringComparer.Ordinal);
        private bool _starting, _started, _failed, _granted, _enabled, _changingEnabled, _terminal;

        public AdjustAnalyticsProvider(IAdjustAnalyticsBridge bridge, IGameConfig config, IConsentService consent, ILog log)
        { _bridge = bridge; _config = config; _consent = consent; _log = log; }

        public string ProviderId => "analytics-adjust";

        public void Send(GameEvent evt)
        {
            Guard(() =>
            {
                if (evt == null || !ObserveConsent()) return;
                if (evt is AdRevenuePaid paid)
                {
                    if (paid.Mediator != "applovin_max") { Warn("unknown revenue mediator '" + paid.Mediator + "'; dropped."); return; }
                    if (double.IsNaN(paid.Revenue) || double.IsInfinity(paid.Revenue) || paid.Revenue < 0 || !CurrencyValid(paid.Currency))
                    { Warn("invalid ad revenue amount/currency; dropped."); return; }
                    Accept(() => _bridge.TrackRevenue(AppLovinRevenueSource, paid.Revenue, paid.Currency,
                        paid.Network, paid.AdUnitId, paid.Placement));
                    return;
                }
                // Read the mapping on every call, including after a remote-config refresh.
                var name = evt.WireName();
                var token = _config.Get(new ConfigKey<string>("analytics.adjust.token." + name));
                if (string.IsNullOrWhiteSpace(token))
                {
                    if (_missingTokens.Add(name ?? "")) Warn("missing token for wire name '" + name + "'; event dropped.");
                    return;
                }
                var writer = new CallbackWriter();
                evt.WriteParams(writer); // Complete serialization before starting the SDK or sending anything.
                Accept(() => _bridge.TrackToken(token, writer.Values));
            });
        }

        public void SetUserProperty(UserPropKey key, string value)
        {
            Guard(() =>
            {
                if (!ObserveConsent()) return;
                if (_properties.Add(key.Value ?? "")) Warn("user property '" + key.Value + "' unsupported; ignored.");
            });
        }

        public void OnAnalyticsConsent(bool granted) => Guard(() => ApplyConsent(granted));

        public bool Execute(string command, IReadOnlyList<KeyValuePair<string, string>> args)
        {
            try
            {
                if (command == "privacy.forget_me")
                {
                    // Privacy never calls ObserveConsent: it must not re-enable a disabled SDK.
                    if ((args != null && args.Count != 0) || !_started || _starting || _changingEnabled
                        || !_enabled || !ConsentStillGranted() || !_enabled)
                        return Refuse("privacy.forget_me requires an active granted SDK and no arguments; erasure while denied/disabled is unsupported");
                    if (!Run(() => _bridge.ForgetMe())) return false;
                    _terminal = true; // Local acceptance only, never proof of server deletion or another instance's state.
                    BestEffortDisable();
                    return true;
                }
                if (command == "set_user_id") return Refuse("set_user_id is unsupported");
                string first, second;
                if (command == "track_token")
                {
                    if (!Arguments(args, "token", null, out first, out second)) return Refuse("track_token requires exactly token");
                    if (!ObserveConsent()) return false;
                    return Accept(() => _bridge.TrackToken(first, Array.Empty<KeyValuePair<string, string>>()));
                }
                if (command == "add_callback_param" || command == "add_partner_param")
                {
                    if (!Arguments(args, "key", "value", out first, out second)) return Refuse(command + " requires exactly key and value");
                    if (!ObserveConsent()) return false;
                    return Accept(() => command == "add_callback_param" ? _bridge.AddCallbackParameter(first, second) : _bridge.AddPartnerParameter(first, second));
                }
                return Refuse("unknown command '" + command + "'");
            }
            catch (Exception ex) { Warn("command failed; dropped without retry: " + ex.Message); return false; }
        }

        private bool CurrentConsent() => _consent != null && _consent.IsResolved && _consent.IsGranted(ConsentPurpose.Analytics);
        private bool ObserveConsent()
        {
            // Refuse nested ordinary observations before they can undo a withdrawal recorded
            // by the outer initialization/Enable callback.
            if (_starting || _changingEnabled) return Refuse("reentrant initialization/collection transition; work dropped");
            if (_failed) return Refuse("initialization failure is latched; restart required");
            if (_terminal) return Refuse("forget accepted in this provider instance; ordinary work is terminally refused");
            var granted = CurrentConsent();
            if (granted != _granted) ApplyConsent(granted);
            var allowed = ConsentStillGranted();
            if (!allowed) Warn("Analytics consent unresolved/denied or provider terminal/failed; work refused.");
            return allowed;
        }
        private bool ConsentStillGranted()
        {
            var current = CurrentConsent();
            if (!current) ApplyConsent(false);
            // Read flags after the external consent getter: it can reenter the callback.
            return current && _granted && !_failed && !_terminal;
        }
        private void ApplyConsent(bool granted)
        {
            if (_granted == granted) return;
            _granted = granted;
            // A callback during an SDK transition only changes the desired state. The outer
            // transition settles it once, so an Enable callback cannot race a nested Disable.
            if (_changingEnabled || !_started) return;
            if (!granted) ChangeEnabled(false);
            else if (!_failed && !_terminal) ChangeEnabled(true);
        }
        private bool ChangeEnabled(bool enabled)
        {
            if (_changingEnabled) return false;
            _changingEnabled = true;
            try
            {
                if (!Run(() => _bridge.SetEnabled(enabled))) return Fail("collection transition refused");
                _enabled = enabled;
                if (enabled && !ConsentStillGranted())
                {
                    _enabled = false;
                    if (!Run(() => _bridge.SetEnabled(false))) return Fail("withdrawal transition refused");
                    return false;
                }
                return !_failed && !_terminal;
            }
            catch (Exception ex) { return Fail("collection transition failed: " + ex.Message); }
            finally { _changingEnabled = false; }
        }
        private bool Accept(Func<bool> work)
        {
            if (_failed) return Refuse("initialization failure is latched; restart required");
            if (_terminal) return Refuse("forget accepted in this provider instance; ordinary work is terminally refused");
            if (_starting || _changingEnabled) return Refuse("reentrant initialization/collection transition; work dropped");
            if (!ConsentStillGranted()) return Refuse("consent withdrawn before work");
            if (!_started)
            {
                _starting = true;
                try
                {
                    var appToken = _config.Get(new ConfigKey<string>("analytics.adjust.appToken"));
                    var configuredEnvironment = _config.Get(new ConfigKey<object>("analytics.adjust.environment"));
                    if (!AdjustAnalyticsBuildRules.IsAppTokenValid(appToken)
                        || !AdjustAnalyticsBuildRules.TryEnvironment(configuredEnvironment, out var environment))
                        return Fail("invalid analytics.adjust.appToken (12 characters required) or analytics.adjust.environment");
                    var signals = !AdSignalsDenied(_config.Get(new ConfigKey<object>("analytics.adjust.adSignals")));
                    if (!ConsentStillGranted()) return Refuse("consent withdrawn before startup");
                    if (!_bridge.ConfigureAdSignals(signals)) return Fail("DMA configuration refused");
                    if (!ConsentStillGranted()) return Refuse("consent withdrawn before InitSdk");
                    if (!_bridge.Start(appToken, environment)) return Fail("SDK start refused");
                    _started = true;
                    if (!ConsentStillGranted())
                    {
                        ChangeEnabled(false);
                        return Refuse("consent withdrawn during startup");
                    }
                    // Native enabled state may have survived an earlier process/provider withdrawal.
                    if (!ChangeEnabled(true)) return false;
                }
                catch (Exception ex) { return Fail(ex.Message); }
                finally { _starting = false; }
            }
            // Serialization, config getters and Enable can all reenter consent. This is the
            // final state/consent gate immediately before every ordinary bridge call.
            if (!_enabled || !ConsentStillGranted() || _failed || _terminal || !_enabled)
                return Refuse("ordinary work final consent/state gate");
            return Run(work);
        }
        private bool Fail(string error)
        {
            _failed = true;
            BestEffortDisable();
            Warn("initialization failed; no retry this lifetime: " + error);
            return false;
        }
        private void BestEffortDisable()
        {
            var wasChanging = _changingEnabled;
            _changingEnabled = true;
            _enabled = false;
            try { if (!_bridge.SetEnabled(false)) Warn("best-effort disable refused"); }
            catch (Exception ex) { Warn("best-effort disable failed: " + ex.Message); }
            finally { _changingEnabled = wasChanging; }
        }
        private bool Run(Func<bool> work)
        {
            try { return work() || Refuse("SDK work refused; dropped without retry"); }
            catch (Exception ex) { Warn("SDK work failed; dropped without retry: " + ex.Message); return false; }
        }
        private void Guard(Action action)
        { try { action(); } catch (Exception ex) { Warn("provider operation failed; dropped without retry: " + ex.Message); } }
        private bool Refuse(string message) { Warn(message + "; refused."); return false; }
        private void Warn(string message) { try { _log?.Warn("[pf-analytics-adjust] " + message); } catch (Exception) { } }
        private static bool CurrencyValid(string currency)
        {
            if (currency == null || currency.Length != 3) return false;
            foreach (var c in currency) if (c < 'A' || c > 'Z') return false;
            return true;
        }
        private static bool AdSignalsDenied(object value)
        {
            if (value is string text) return text == "0";
            return (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint
                || value is long || value is ulong || value is float || value is double || value is decimal)
                && Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0d;
        }
        private static bool Arguments(IReadOnlyList<KeyValuePair<string, string>> args, string firstKey, string secondKey, out string first, out string second)
        {
            first = null; second = null;
            if (args == null || args.Count != (secondKey == null ? 1 : 2)) return false;
            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg.Value)) return false;
                if (arg.Key == firstKey && first == null) first = arg.Value;
                else if (secondKey != null && arg.Key == secondKey && second == null) second = arg.Value;
                else return false;
            }
            return first != null && (secondKey == null || second != null);
        }
        private sealed class CallbackWriter : IEventParamWriter
        {
            internal readonly List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
            public void Write(string key, string value) { if (value != null) Values.Add(new KeyValuePair<string, string>(key, value)); }
            public void Write(string key, long value) => Write(key, WireValues.ToText(value));
            public void Write(string key, double value) => Write(key, WireValues.ToText(value));
            public void Write(string key, bool value) => Write(key, WireValues.ToText(value));
        }
    }
}
