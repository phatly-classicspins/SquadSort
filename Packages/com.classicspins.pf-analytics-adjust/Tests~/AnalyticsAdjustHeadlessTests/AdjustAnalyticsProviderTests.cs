using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.HeadlessTests
{
    public sealed class AdjustAnalyticsProviderTests
    {
        private TestBridge _bridge; private TestConfig _config; private TestConsent _consent; private TestLog _log;
        private AdjustAnalyticsProvider _provider;
        private static KeyValuePair<string, string>[] Args(params string[] pairs)
        { var args = new List<KeyValuePair<string, string>>(); for (int i = 0; i < pairs.Length; i += 2) args.Add(new KeyValuePair<string, string>(pairs[i], pairs[i + 1])); return args.ToArray(); }
        [SetUp] public void SetUp()
        {
            _bridge = new TestBridge(); _config = new TestConfig(); _consent = new TestConsent(); _log = new TestLog();
            _provider = new AdjustAnalyticsProvider(_bridge, _config, _consent, _log);
        }
        private bool Track() => _provider.Execute("track_token", Args("token", "abc123"));
        [Test] public void Generic_record_uses_latest_token_and_invariant_callback_values_once()
        {
            _config.Values["analytics.adjust.token.custom_fact"] = "one";
            var previous = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); _provider.Send(new CustomFact()); }
            finally { CultureInfo.CurrentCulture = previous; }
            var e = _bridge.Events.Single();
            Assert.That(e.Token, Is.EqualTo("one"));
            Assert.That(e.Params, Is.EquivalentTo(new Dictionary<string, string> { ["text"] = "x", ["count"] = "42", ["amount"] = "12.5", ["yes"] = "1", ["no"] = "0" }));
            _config.Values["analytics.adjust.token.custom_fact"] = "two"; _provider.Send(new CustomFact());
            Assert.That(_bridge.Events.Select(x => x.Token), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(_bridge.Calls.Count(x => x.StartsWith("start:")), Is.EqualTo(1));
        }
        [Test] public void Missing_tokens_warn_once_per_name_without_starting_and_can_be_added_later()
        {
            _provider.Send(new CustomFact()); _provider.Send(new CustomFact()); _provider.Send(new AdShown("p"));
            Assert.That(_bridge.Calls, Is.Empty); Assert.That(_log.Warnings.Count(x => x.Contains("missing token")), Is.EqualTo(2));
            _config.Values["analytics.adjust.token.custom_fact"] = "now"; _provider.Send(new CustomFact());
            Assert.That(_bridge.Events.Single().Token, Is.EqualTo("now"));
        }
        [Test] public void Properties_are_ignored_once_per_key_even_when_logging_throws_and_do_not_start()
        {
            _provider.SetUserProperty(new UserPropKey("one"), "a"); _provider.SetUserProperty(new UserPropKey("one"), "b");
            _provider.SetUserProperty(new UserPropKey("two"), "a");
            Assert.That(_log.Warnings, Has.Count.EqualTo(2)); Assert.That(_bridge.Calls, Is.Empty);
            _log.Throws = true; Assert.DoesNotThrow(() => _provider.SetUserProperty(default, null));
        }
        [TestCase(null, true)] [TestCase(0, false)] [TestCase(0L, false)] [TestCase(0f, false)] [TestCase(0d, false)]
        [TestCase("0", false)] [TestCase(1, true)] [TestCase("1", true)] [TestCase("false", true)]
        public void DMA_precedes_start_and_absence_defaults_to_granted_sandbox(object value, bool enabled)
        {
            if (value != null) _config.Values["analytics.adjust.adSignals"] = value;
            Assert.That(Track(), Is.True);
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { "signals:" + enabled, "start:abcdefghijkl:sandbox", "enabled:True", "event:abc123" }));
            Assert.That(_config.Reads, Does.Contain(("analytics.adjust.adSignals", typeof(object))));
        }
        [Test] public void Explicit_production_is_preserved() { _config.Values["analytics.adjust.environment"] = "production"; Track(); Assert.That(_bridge.Calls, Does.Contain("start:abcdefghijkl:production")); }
        [TestCase("")] [TestCase(" ")] [TestCase("Production")] [TestCase("invalid")] [TestCase(123)]
        public void Invalid_environment_refuses_before_start_and_latches(object env)
        { _config.Values["analytics.adjust.environment"] = env; Assert.That(Track(), Is.False); _config.Values.Remove("analytics.adjust.environment"); Assert.That(Track(), Is.False); Assert.That(_bridge.Calls.Any(x => x.StartsWith("start:")), Is.False); }
        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase("short")] [TestCase("thirteenchars")]
        public void Missing_token_refuses_before_start(string token)
        { _config.Values["analytics.adjust.appToken"] = token; Assert.That(Track(), Is.False); Assert.That(_bridge.Calls.Any(x => x.StartsWith("start:")), Is.False); }
        [TestCase(false, false)] [TestCase(true, false)]
        public void Unresolved_or_denied_has_no_native_calls(bool resolved, bool granted)
        { _consent.Resolved = resolved; _consent.Granted = granted; Assert.That(Track(), Is.False); _provider.Send(new CustomFact()); _provider.SetUserProperty(new UserPropKey("p"), "v"); Assert.That(_bridge.Calls, Is.Empty); }
        [Test] public void Consent_callback_never_starts_and_withdrawal_regrant_reuses_started_sdk()
        {
            _provider.OnAnalyticsConsent(true); Assert.That(_bridge.Calls, Is.Empty); Track();
            _consent.Granted = false; _provider.OnAnalyticsConsent(false); _provider.OnAnalyticsConsent(false); Assert.That(Track(), Is.False);
            _consent.Granted = true; _provider.OnAnalyticsConsent(true); Track();
            Assert.That(_bridge.Calls.Count(x => x == "enabled:False"), Is.EqualTo(1)); Assert.That(_bridge.Calls.Count(x => x == "enabled:True"), Is.EqualTo(2));
            Assert.That(_bridge.Calls.Count(x => x.StartsWith("start:")), Is.EqualTo(1));
        }
        [Test] public void Withdrawal_during_start_disables_and_drops_work()
        { _bridge.DuringStart = () => { _consent.Granted = false; _provider.OnAnalyticsConsent(false); }; Assert.That(Track(), Is.False); Assert.That(_bridge.Events, Is.Empty); Assert.That(_bridge.Calls.Last(), Is.EqualTo("enabled:False")); }
        [Test] public void Withdrawal_during_DMA_prevents_start()
        { _bridge.DuringSignals = () => { _consent.Granted = false; _provider.OnAnalyticsConsent(false); }; Assert.That(Track(), Is.False); Assert.That(_bridge.Calls.Any(x => x.StartsWith("start:")), Is.False); }
        [Test] public void Reentrant_start_refuses_nested_work_without_duplicating_start()
        { bool nested = true; _bridge.DuringStart = () => nested = Track(); Assert.That(Track(), Is.True); Assert.That(nested, Is.False); Assert.That(_bridge.Events, Has.Count.EqualTo(1)); Assert.That(_bridge.Calls.Count(x => x.StartsWith("start:")), Is.EqualTo(1)); }
        [TestCase("signals:True")] [TestCase("start:abcdefghijkl:sandbox")]
        public void Start_failures_latch_without_retry(string stage)
        { _bridge.ThrowOn = stage; Assert.That(Track(), Is.False); _bridge.ThrowOn = null; Assert.That(Track(), Is.False); Assert.That(_log.Warnings.Count(x => x.Contains("initialization failed")), Is.EqualTo(1)); Assert.That(_bridge.Events, Is.Empty); }
        [Test] public void Refused_start_latches() { _bridge.Refuses = true; Assert.That(Track(), Is.False); _bridge.Refuses = false; Assert.That(Track(), Is.False); Assert.That(_bridge.Events, Is.Empty); }
        [Test] public void SDK_work_failure_drops_only_that_call_without_retry()
        { _bridge.ThrowOn = "event:abc123"; Assert.That(Track(), Is.False); _bridge.ThrowOn = null; Assert.That(Track(), Is.True); Assert.That(_bridge.Events, Has.Count.EqualTo(1)); Assert.That(_bridge.Calls.Count(x => x.StartsWith("start:")), Is.EqualTo(1)); }
        [Test] public void Serialization_failure_does_not_start_or_send()
        { _config.Values["analytics.adjust.token.bad"] = "token"; Assert.DoesNotThrow(() => _provider.Send(new BadFact())); Assert.That(_bridge.Calls, Is.Empty); }
        [Test] public void Revenue_uses_one_revenue_call_with_MAX_source_and_fields_never_an_event()
        {
            _config.Values["analytics.adjust.token.ad_revenue_paid"] = "must-not-send";
            _provider.Send(new AdRevenuePaid("applovin_max", "place", "unit", "rewarded", "network", 0.25, "USD", "exact"));
            Assert.That(_bridge.Revenues.Single(), Is.EqualTo(("applovin_max_sdk", 0.25, "USD", "network", "unit", "place"))); Assert.That(_bridge.Events, Is.Empty);
        }
        [TestCase("other", 1, "USD")] [TestCase("applovin_max", -1, "USD")] [TestCase("applovin_max", double.NaN, "USD")]
        [TestCase("applovin_max", double.PositiveInfinity, "USD")] [TestCase("applovin_max", 1, "usd")] [TestCase("applovin_max", 1, null)]
        public void Invalid_revenue_is_logged_dropped_without_start(string mediator, double amount, string currency)
        { _provider.Send(new AdRevenuePaid(mediator, "p", "u", "f", "n", amount, currency, "x")); Assert.That(_bridge.Calls, Is.Empty); Assert.That(_log.Warnings, Has.Count.EqualTo(1)); }
        [Test] public void Global_commands_accept_reversed_arguments_and_use_global_bridge_apis()
        { Assert.That(_provider.Execute("add_callback_param", Args("value", "v", "key", "k")), Is.True); Assert.That(_provider.Execute("add_partner_param", Args("key", "p", "value", "q")), Is.True); Assert.That(_bridge.Calls, Does.Contain("callback:k:v").And.Contain("partner:p:q")); Assert.That(_bridge.Events, Is.Empty); }
        [TestCase("track_token", new[] { "token", "a", "extra", "b" })]
        [TestCase("track_token", new[] { "token", " " })]
        [TestCase("add_callback_param", new[] { "key", "a", "key", "b" })]
        [TestCase("add_partner_param", new[] { "key", "a", "wrong", "b" })]
        [TestCase("unknown", new[] { "key", "a" })]
        public void Malformed_duplicate_unknown_arguments_refuse_without_start(string cmd, string[] args)
        { Assert.That(_provider.Execute(cmd, Args(args)), Is.False); Assert.That(_bridge.Calls, Is.Empty); }
        [Test] public void Null_arguments_and_user_id_are_refused_without_start()
        { Assert.That(_provider.Execute("track_token", null), Is.False); Assert.That(_provider.Execute("set_user_id", Args("id", "user")), Is.False); Assert.That(_bridge.Calls, Is.Empty); }
        [Test] public void Forget_refuses_unstarted_denied_or_disabled_without_start_or_reenable()
        {
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False); Assert.That(_bridge.Calls, Is.Empty);
            Track(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            var count = _bridge.Calls.Count;
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
            Assert.That(_bridge.Calls, Has.Count.EqualTo(count)); Assert.That(_bridge.Calls, Does.Not.Contain("forget"));
            Assert.That(_bridge.State.Enabled, Is.False);
        }
        [Test] public void Accepted_active_granted_forget_is_terminal_only_in_this_instance_not_server_erasure()
        {
            Track(); Assert.That(_provider.Execute("privacy.forget_me", Args("id", "x")), Is.False);
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.True);
            var count = _bridge.Calls.Count;
            Assert.That(Track(), Is.False); _provider.Send(new CustomFact());
            _provider.OnAnalyticsConsent(false); _provider.OnAnalyticsConsent(true);
            Assert.That(Track(), Is.False); Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
            Assert.That(_bridge.Calls.Skip(count), Does.Not.Contain("enabled:True"));
            Assert.That(_bridge.Events, Has.Count.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)] public void Refused_or_thrown_forget_does_not_mark_terminal(bool throws)
        {
            Track(); if (throws) _bridge.ThrowOn = "forget"; else _bridge.RefuseOn = "forget";
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
            _bridge.ThrowOn = null; _bridge.RefuseOn = null;
            Assert.That(Track(), Is.True); Assert.That(_bridge.Events, Has.Count.EqualTo(2));
        }
        [Test] public void Fake_enabled_state_across_instances_is_explicitly_enabled_after_start_simulation_only()
        {
            Track(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            Assert.That(_bridge.State.Enabled, Is.False);
            var nextBridge = new TestBridge { State = _bridge.State };
            var next = new AdjustAnalyticsProvider(nextBridge, _config, new TestConsent(), _log);
            Assert.That(next.Execute("track_token", Args("token", "next")), Is.True);
            Assert.That(nextBridge.Calls, Is.EqualTo(new[] { "signals:True", "start:abcdefghijkl:sandbox", "enabled:True", "event:next" }));
            Assert.That(nextBridge.Events.Single().Token, Is.EqualTo("next"));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void Enable_Disable_false_or_throw_latches_refusal_and_best_effort_disables(bool regrant, bool throws)
        {
            Track();
            if (regrant) { _consent.Granted = false; _provider.OnAnalyticsConsent(false); }
            var operation = "enabled:" + regrant;
            if (throws) _bridge.ThrowOn = operation; else _bridge.RefuseOn = operation;
            _consent.Granted = regrant;
            Assert.DoesNotThrow(() => _provider.OnAnalyticsConsent(regrant));
            _bridge.ThrowOn = null; _bridge.RefuseOn = null;
            var count = _bridge.Calls.Count;
            _consent.Granted = true; _provider.OnAnalyticsConsent(true);
            Assert.That(Track(), Is.False); Assert.That(_bridge.Events, Has.Count.EqualTo(1));
            Assert.That(_bridge.Calls.Skip(count), Does.Not.Contain("enabled:True"));
            Assert.That(_bridge.Calls.Count(x => x == "enabled:False"), Is.GreaterThanOrEqualTo(2));
        }
        [Test] public void Withdrawal_inside_WriteParams_prevents_ordinary_dispatch_on_started_sdk()
        {
            Track(); _config.Values["analytics.adjust.token.reentrant"] = "nested";
            _provider.Send(new ReentrantFact(() => { _consent.Granted = false; _provider.OnAnalyticsConsent(false); }));
            Assert.That(_bridge.Events, Has.Count.EqualTo(1)); Assert.That(_bridge.State.Enabled, Is.False);
        }
        [Test] public void Withdrawal_inside_token_config_prevents_dispatch_and_start()
        {
            _config.Values["analytics.adjust.token.custom_fact"] = "mapped";
            _config.DuringGet = key => { if (key.StartsWith("analytics.adjust.token.")) { _consent.Granted = false; _provider.OnAnalyticsConsent(false); } };
            _provider.Send(new CustomFact()); Assert.That(_bridge.Calls, Is.Empty);
        }
        [Test] public void Withdrawal_inside_startup_config_prevents_start()
        {
            _config.DuringGet = key => { if (key == "analytics.adjust.adSignals") { _consent.Granted = false; _provider.OnAnalyticsConsent(false); } };
            Assert.That(Track(), Is.False); Assert.That(_bridge.Calls, Is.Empty);
        }
        [TestCase(false)] [TestCase(true)]
        public void Withdrawal_inside_Enable_settles_disabled_without_dispatch_or_callback_loop(bool regrant)
        {
            if (regrant) { Track(); _consent.Granted = false; _provider.OnAnalyticsConsent(false); }
            _bridge.DuringEnabled = enabled =>
            {
                if (enabled)
                {
                    _consent.Granted = false; _provider.OnAnalyticsConsent(false);
                    Assert.That(Track(), Is.False, "Nested ordinary work cannot undo the withdrawal during Enable.");
                }
                // Repeated callback on Disable must not recursively call Disable again.
                else _provider.OnAnalyticsConsent(false);
            };
            if (regrant) { _consent.Granted = true; _provider.OnAnalyticsConsent(true); }
            Assert.That(Track(), Is.False);
            Assert.That(_bridge.State.Enabled, Is.False);
            Assert.That(_bridge.Events, Has.Count.EqualTo(regrant ? 1 : 0));
            Assert.That(_bridge.Calls.Count(x => x == "enabled:False"), Is.EqualTo(regrant ? 2 : 1));
        }
        [Test] public void Post_start_false_token_and_revenue_calls_record_attempts_without_acceptance_or_retry()
        {
            Track(); _bridge.RefuseOn = "event:abc123";
            Assert.That(Track(), Is.False); Assert.That(_bridge.Events, Has.Count.EqualTo(1));
            Assert.That(_bridge.Calls.Count(x => x == "event:abc123"), Is.EqualTo(2));
            _bridge.RefuseOn = "revenue";
            _provider.Send(new AdRevenuePaid("applovin_max", "p", "u", "f", "n", 1, "USD", "x"));
            Assert.That(_bridge.Revenues, Is.Empty); Assert.That(_bridge.Calls.Count(x => x == "revenue"), Is.EqualTo(1));
            _bridge.RefuseOn = null; Assert.That(Track(), Is.True);
            Assert.That(_bridge.Events, Has.Count.EqualTo(2));
            Assert.That(_bridge.Calls.Count(x => x.StartsWith("start:")), Is.EqualTo(1));
        }
        [Test] public void Dependency_and_logging_defects_never_escape()
        { _config.Throws = true; _log.Throws = true; Assert.DoesNotThrow(() => _provider.Send(new CustomFact())); Assert.That(Track(), Is.False); _consent.Throws = true; Assert.DoesNotThrow(() => _provider.SetUserProperty(default, null)); Assert.DoesNotThrow(() => _provider.OnAnalyticsConsent(false)); }
        private sealed record ReentrantFact(Action Callback) : GameEvent
        {
            public override string WireName() => "reentrant";
            public override void WriteParams(IEventParamWriter w) { Callback(); w.Write("value", 1L); }
        }
        private sealed record CustomFact : GameEvent
        {
            public override string WireName() => "custom_fact";
            public override void WriteParams(IEventParamWriter w) { w.Write("text", "x"); w.Write("null", (string)null); w.Write("count", 42L); w.Write("amount", 12.5); w.Write("yes", true); w.Write("no", false); }
        }
        private sealed record BadFact : GameEvent
        { public override string WireName() => "bad"; public override void WriteParams(IEventParamWriter w) => throw new Exception("serialize"); }
    }
}
