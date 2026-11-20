using System;
using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
{
    [TestFixture]
    public sealed class FirebaseAnalyticsProviderTests
    {
        private TestBridge _bridge;
        private TestLog _log;
        private TestConfig _config;
        private TestConsent _consent;
        private FirebaseAnalyticsProvider _provider;
        [SetUp] public void Setup()
        {
            _bridge = new TestBridge(); _log = new TestLog(); _config = new TestConfig(); _consent = new TestConsent();
            _provider = Create(action => action());
        }
        private FirebaseAnalyticsProvider Create(Action<Action> dispatch) => new FirebaseAnalyticsProvider(_bridge, _config, _consent, _log, dispatch);
        private static KeyValuePair<string, string>[] Id(string value) => new[] { new KeyValuePair<string, string>("id", value) };
        private void Start() { _provider.Send(new AdShown("initial")); _bridge.Complete(); }

        private sealed record WireFact(Dictionary<string, object> Payload) : GameEvent
        {
            public override string WireName() => "sku_fact";
            public override void WriteParams(IEventParamWriter w)
            {
                foreach (var kv in Payload)
                    if (kv.Value is string text) w.Write(kv.Key, text);
                    else if (kv.Value is long integer) w.Write(kv.Key, integer);
                    else if (kv.Value is double number) w.Write(kv.Key, number);
                    else if (kv.Value is bool boolean) w.Write(kv.Key, boolean);
            }
        }
        [Test] public void Framework_and_unknown_SKU_records_keep_wire_names_types_and_invariant_bool()
        {
            var payload = new Dictionary<string, object> { ["text"] = "hello", ["integer"] = 42L, ["number"] = 1.25, ["yes"] = true, ["no"] = false };
            _provider.Send(new WireFact(payload));
            _provider.Send(new DialogOpened(4));
            Assert.That(_bridge.Events, Is.Empty);
            _bridge.Complete();
            Assert.Multiple(() =>
            {
                Assert.That(_bridge.Events[0].Name, Is.EqualTo("sku_fact"));
                Assert.That(_bridge.Events[0].Params, Is.EquivalentTo(new Dictionary<string, object> { ["text"] = "hello", ["integer"] = 42L, ["number"] = 1.25, ["yes"] = 1L, ["no"] = 0L }));
                Assert.That(_bridge.Events[0].Params["integer"], Is.TypeOf<long>());
                Assert.That(_bridge.Events[0].Params["number"], Is.TypeOf<double>());
                Assert.That(_bridge.Events[1].Name, Is.EqualTo("dialog_opened"));
                Assert.That(_bridge.Events[1].Params["depth"], Is.EqualTo(4L));
            });
        }
        [Test] public void Revenue_maps_exactly_six_fields_and_is_never_forwarded_generically()
        {
            _provider.Send(new AdRevenuePaid("applovin_max", "unused-placement", "unit", "rewarded", "network", 0.0123, "USD", "exact"));
            _bridge.Complete();
            Assert.That(_bridge.Events, Has.Count.EqualTo(1));
            Assert.That(_bridge.Events[0].Name, Is.EqualTo("ad_impression"));
            Assert.That(_bridge.Events[0].Params, Is.EquivalentTo(new Dictionary<string, object>
            {
                ["ad_platform"] = "AppLovin", ["ad_source"] = "network", ["ad_format"] = "rewarded", ["ad_unit_name"] = "unit", ["value"] = 0.0123, ["currency"] = "USD"
            }));
        }
        [Test] public void Non_MAX_mediator_is_preserved_without_inventing_a_mapping()
        {
            _provider.Send(new AdRevenuePaid("other", "", "unit", "banner", "network", 0, "USD", "undefined"));
            _bridge.Complete();
            Assert.That(_bridge.Events.Single().Params["ad_platform"], Is.EqualTo("other"));
        }
        [Test] public void Ad_funnel_events_never_synthesize_money()
        {
            _provider.Send(new AdShown("slot")); _provider.Send(new AdRewarded("slot")); _bridge.Complete();
            Assert.That(_bridge.Events.Select(e => e.Name), Is.EqualTo(new[] { "ad_shown", "ad_rewarded" }));
            Assert.That(_bridge.Events.All(e => !e.Params.ContainsKey("value")), Is.True);
        }
        [TestCase(null, true)] [TestCase("0", false)] [TestCase("1", true)]
        public void Consent_precedes_collection_and_ad_signal_absence_defaults_to_granted(string setting, bool expected)
        {
            _config.AdSignals = setting;
            _provider.OnAnalyticsConsent(true);
            Assert.That(_bridge.Starts, Is.Zero);
            _provider.Send(new AdShown("a"));
            _provider.SetUserProperty(UserProps.TotalCoins, "100");
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { "start" }));
            _bridge.Complete();
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { "start", "consent:True:" + expected, "collection:True", "event:ad_shown", "property:total_coins" }));
            Assert.That(_config.Keys, Is.EqualTo(new[] { "analytics.firebase.adSignals" }));
        }
        [Test] public void Accepted_command_can_be_first_work_without_a_fanout_callback()
        {
            Assert.That(_provider.Execute("set_user_id", Id("user")), Is.True);
            Assert.That(_bridge.Starts, Is.EqualTo(1)); Assert.That(_bridge.UserIds, Is.Empty);
            _bridge.Complete(); Assert.That(_bridge.UserIds, Is.EqualTo(new[] { "user" }));
        }
        [Test] public void Pending_snapshots_survive_caller_mutations_in_fifo_order()
        {
            var payload = new Dictionary<string, object> { ["text"] = "before" };
            var args = new List<KeyValuePair<string, string>>(Id("before-user"));
            _provider.Send(new WireFact(payload));
            Assert.That(_provider.Execute("set_user_id", args), Is.True);
            _provider.SetUserProperty(new UserPropKey("sku_property"), "before-property");
            payload["text"] = "after"; args[0] = new KeyValuePair<string, string>("id", "after-user");
            _bridge.Complete();
            Assert.That(_bridge.Events.Single().Params["text"], Is.EqualTo("before"));
            Assert.That(_bridge.UserIds.Single(), Is.EqualTo("before-user"));
            Assert.That(_bridge.Properties.Single().Value, Is.EqualTo("before-property"));
            Assert.That(_bridge.Calls.Skip(3), Is.EqualTo(new[] { "event:sku_fact", "user:before-user", "property:sku_property" }));
        }
        [Test] public void Withdrawal_drops_every_pending_kind_and_late_completion_keeps_collection_off()
        {
            _provider.Send(new AdShown("a")); _provider.SetUserProperty(UserProps.TotalCoins, "1"); _provider.Execute("set_user_id", Id("u"));
            _consent.Granted = false; _provider.OnAnalyticsConsent(false); _bridge.Complete();
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { "start", "collection:False" }));
            Assert.That(_bridge.Events, Is.Empty); Assert.That(_bridge.UserIds, Is.Empty); Assert.That(_bridge.Properties, Is.Empty);
        }
        [Test] public void Completion_rechecks_consent_even_if_fanout_has_not_observed_withdrawal()
        {
            _provider.Send(new AdShown("a")); _consent.Granted = false; _bridge.Complete();
            Assert.That(_bridge.Events, Is.Empty); Assert.That(_bridge.Calls.Last(), Is.EqualTo("collection:False"));
        }
        [Test] public void Regrant_reuses_started_sdk_and_never_replays_withdrawn_work()
        {
            Start(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            _provider.Send(new AdShown("dropped")); _consent.Granted = true; _provider.OnAnalyticsConsent(true);
            Assert.That(_bridge.Starts, Is.EqualTo(1)); Assert.That(_bridge.Events, Has.Count.EqualTo(1));
            Assert.That(_bridge.Calls.TakeLast(3), Is.EqualTo(new[] { "collection:False", "consent:True:True", "collection:True" }));
            _provider.Send(new AdShown("new")); Assert.That(_bridge.Events.Last().Params["placement"], Is.EqualTo("new"));
        }
        [Test] public void Regrant_during_pending_start_does_not_restore_old_queue()
        {
            _provider.Send(new AdShown("old")); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            _consent.Granted = true; _provider.OnAnalyticsConsent(true); _provider.Send(new AdShown("new")); _bridge.Complete();
            Assert.That(_bridge.Events.Single().Params["placement"], Is.EqualTo("new")); Assert.That(_bridge.Starts, Is.EqualTo(1));
        }
        [Test] public void Start_failure_drops_queue_latches_failure_and_logs_result()
        {
            _provider.Send(new AdShown("old")); _bridge.Complete(false);
            Assert.That(_provider.Execute("set_user_id", Id("u")), Is.False);
            _provider.Send(new AdShown("later")); _provider.OnAnalyticsConsent(true);
            _bridge.Complete(true); // duplicate/late result cannot resurrect initialization
            Assert.That(_bridge.Starts, Is.EqualTo(1)); Assert.That(_bridge.Events, Is.Empty);
            Assert.That(_log.Warnings.Any(w => w.Contains("initialization failed") && w.Contains("dependency failure")), Is.True);
        }
        [Test] public void Queue_overflow_refuses_newest_command_logs_and_stays_bounded()
        {
            for (int i = 0; i < FirebaseAnalyticsProvider.PendingCapacity; i++) _provider.Send(new AdShown(i.ToString()));
            Assert.That(_provider.Execute("set_user_id", Id("overflow")), Is.False);
            _provider.Send(new AdShown("overflow")); _bridge.Complete();
            Assert.That(_bridge.Events, Has.Count.EqualTo(FirebaseAnalyticsProvider.PendingCapacity));
            Assert.That(_bridge.UserIds, Is.Empty); Assert.That(_log.Warnings.Count(w => w.Contains("overflow")), Is.EqualTo(2));
        }
        [Test] public void Completion_is_marshaled_before_it_touches_provider_or_bridge_state()
        {
            var dispatched = new Queue<Action>(); _provider = Create(action => dispatched.Enqueue(action));
            _provider.Send(new AdShown("a")); _bridge.Complete();
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { "start" }));
            dispatched.Dequeue()(); Assert.That(_bridge.Events, Has.Count.EqualTo(1));
        }
        [Test] public void Withdrawal_after_worker_completion_before_main_thread_dispatch_wins()
        {
            var dispatched = new Queue<Action>(); _provider = Create(action => dispatched.Enqueue(action));
            _provider.Send(new AdShown("a")); _bridge.Complete(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            dispatched.Dequeue()(); Assert.That(_bridge.Events, Is.Empty); Assert.That(_bridge.Calls.Last(), Is.EqualTo("collection:False"));
        }
        [Test] public void String_limits_warn_once_per_event_or_property_name_and_keep_identifiers()
        {
            var payload = new Dictionary<string, object> { ["one"] = new string('x', 101), ["two"] = new string('y', 200) };
            _provider.Send(new WireFact(payload)); _provider.Send(new WireFact(payload));
            _provider.SetUserProperty(UserProps.TotalCoins, new string('x', 37)); _provider.SetUserProperty(UserProps.TotalCoins, new string('y', 100));
            _provider.SetUserProperty(UserProps.BestStreak, new string('z', 37)); _bridge.Complete();
            Assert.That(_bridge.Events.All(e => e.Params.Values.All(v => ((string)v).Length == 100)), Is.True);
            Assert.That(_bridge.Properties.All(p => p.Value.Length == 36), Is.True);
            Assert.That(_log.Warnings.Count(w => w.Contains("event 'sku_fact'")), Is.EqualTo(1));
            Assert.That(_log.Warnings.Count(w => w.Contains("property 'total_coins'")), Is.EqualTo(1));
            Assert.That(_log.Warnings.Count(w => w.Contains("property 'best_streak'")), Is.EqualTo(1));
        }
        [Test] public void Exact_limits_null_property_and_unicode_boundaries_are_safe()
        {
            _provider.Send(new WireFact(new Dictionary<string, object> { ["exact"] = new string('x', 100), ["unicode"] = new string('x', 99) + "😀" }));
            _provider.SetUserProperty(UserProps.TotalCoins, null); _bridge.Complete();
            Assert.That(_bridge.Events.Single().Params["exact"], Is.EqualTo(new string('x', 100)));
            Assert.That(_bridge.Events.Single().Params["unicode"], Is.EqualTo(new string('x', 99)));
            Assert.That(_bridge.Properties.Single().Value, Is.Null);
        }
        [TestCase("UPPER")] [TestCase("")] [TestCase(null)]
        public void Invalid_properties_are_refused_without_starting(string name)
        {
            _provider.SetUserProperty(new UserPropKey(name), "v"); Assert.That(_bridge.Starts, Is.Zero);
        }
        [Test] public void Privacy_refuses_before_and_during_start_without_starting_or_queueing_itself()
        {
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False); Assert.That(_bridge.Starts, Is.Zero);
            _provider.Send(new AdShown("a")); Assert.That(_provider.Execute("privacy.forget_me", Array.Empty<KeyValuePair<string, string>>()), Is.False);
            _bridge.Complete(); Assert.That(_bridge.Calls, Does.Not.Contain("reset"));
        }
        [Test] public void Privacy_is_accepted_after_withdrawal_clears_user_id_and_resets_local_data_without_restarting()
        {
            Start(); _consent.Granted = false; _provider.OnAnalyticsConsent(false);
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.True);
            Assert.That(_bridge.Calls.TakeLast(2), Is.EqualTo(new[] { "user:", "reset" }));
            Assert.That(_bridge.Starts, Is.EqualTo(1));
        }
        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        public void Empty_user_id_is_refused(string id) { Assert.That(_provider.Execute("set_user_id", Id(id)), Is.False); Assert.That(_bridge.Starts, Is.Zero); }
        [Test] public void Commands_validate_ordinal_name_exact_argument_key_count_and_user_id_length()
        {
            var invalid = new[] { Id(new string('x', 257)), new[] { new KeyValuePair<string, string>("ID", "u") }, new[] { Id("u")[0], Id("u")[0] }, Array.Empty<KeyValuePair<string, string>>() };
            foreach (var args in invalid) Assert.That(_provider.Execute("set_user_id", args), Is.False);
            Assert.That(_provider.Execute("set_user_id", null), Is.False);
            foreach (var cmd in new[] { "Set_User_Id", "unknown", "privacy.unknown", null, "" }) Assert.That(_provider.Execute(cmd, Id("u")), Is.False);
            Assert.That(_provider.Execute("privacy.forget_me", Id("extraneous")), Is.False);
            Assert.That(_bridge.Starts, Is.Zero);
            Assert.That(_provider.Execute("set_user_id", Id(new string('x', 256))), Is.True);
        }
        [TestCase(false, true)] [TestCase(true, false)]
        public void Unresolved_or_denied_consent_blocks_direct_provider_work(bool resolved, bool granted)
        {
            _consent.Resolved = resolved; _consent.Granted = granted;
            _provider.Send(new AdShown("a")); _provider.SetUserProperty(UserProps.TotalCoins, "1");
            Assert.That(_provider.Execute("set_user_id", Id("u")), Is.False); Assert.That(_bridge.Starts, Is.Zero);
        }
        [Test] public void Synchronous_start_completion_is_supported()
        {
            _bridge.CompleteImmediately = true; _provider.Send(new AdShown("a")); Assert.That(_bridge.Events, Has.Count.EqualTo(1));
        }
        [Test] public void Bridge_refusal_and_throwing_completion_drop_held_work_and_do_not_retry()
        {
            _provider.Send(new AdShown("a")); _bridge.Throws = true;
            Assert.DoesNotThrow(() => _bridge.Complete()); _bridge.Throws = false;
            _provider.Send(new AdShown("b")); _provider.OnAnalyticsConsent(true);
            Assert.That(_bridge.Events, Is.Empty); Assert.That(_bridge.Starts, Is.EqualTo(1));
            Assert.That(_log.Warnings.Any(w => w.Contains("initialization failed")), Is.True);
        }
        [Test] public void All_public_boundaries_are_no_throw_with_defective_collaborators()
        {
            _log.Throws = true; _consent.Throws = true;
            Assert.DoesNotThrow(() => _provider.Send(new AdShown("a")));
            Assert.DoesNotThrow(() => _provider.SetUserProperty(UserProps.TotalCoins, "1"));
            Assert.DoesNotThrow(() => _provider.OnAnalyticsConsent(false));
            Assert.That(_provider.Execute("set_user_id", Id("u")), Is.False);
            _consent.Throws = false; _config.Throws = true;
            Assert.DoesNotThrow(() => _provider.Send(new AdShown("a"))); Assert.That(_bridge.Starts, Is.Zero);
        }
        [Test] public void Failed_config_start_and_failed_dispatch_are_latched()
        {
            _config.Throws = true; Assert.That(_provider.Execute("set_user_id", Id("u")), Is.False);
            _config.Throws = false; _provider.Send(new AdShown("a")); Assert.That(_bridge.Starts, Is.Zero);
            _provider = Create(action => throw new Exception("dispatch")); _provider.Send(new AdShown("a"));
            Assert.DoesNotThrow(() => _bridge.Complete()); _provider.Send(new AdShown("b")); Assert.That(_bridge.Starts, Is.EqualTo(1));
        }
        [Test] public void Started_bridge_failures_return_false_and_never_escape()
        {
            Start(); _bridge.Refuses = true;
            Assert.That(_provider.Execute("set_user_id", Id("u")), Is.False);
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
            _bridge.Throws = true;
            Assert.DoesNotThrow(() => _provider.Send(new AdShown("x")));
            Assert.DoesNotThrow(() => _provider.SetUserProperty(UserProps.TotalCoins, "1"));
            Assert.DoesNotThrow(() => _provider.OnAnalyticsConsent(false));
            Assert.That(_provider.Execute("privacy.forget_me", null), Is.False);
        }
    }
}
