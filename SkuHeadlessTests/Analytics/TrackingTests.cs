using System;
using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using SqadSort.SkuHeadlessTests.Meta;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Analytics
{
    sealed class EventSink : IAnalytics
    {
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public void Track(GameEvent evt) => Events.Add(evt);
        public void SetUserProperty(UserPropKey key, string value) { }
        public List<TrackedEvent> Tracked => Events.OfType<TrackedEvent>().ToList();
        public List<string> Names => Tracked.Select(e => e.Name).ToList();
        public TrackedEvent Last(string name) => Tracked.Last(e => e.Name == name);
    }

    /// <summary>D-061: Money Sort's tracking plan as CardSlot sends it — names, parameters, and when each event goes out.</summary>
    public class GameTrackingTests
    {
        EventSink sink; TestStore store; TestWallet wallet; GameTracking t;

        [SetUp]
        public void SetUp()
        {
            sink = new EventSink(); store = new TestStore(); wallet = new TestWallet();
            store.Progress.FirstLoadingTracked = true;   // most tests are not about the first launch
            t = new GameTracking(sink, wallet, store, isTest: false);
        }

        [Test]
        public void Looped_attempt_reports_display_and_content_levels()
        {
            t.LevelStarted(41, 1, 21);
            Assert.That(sink.Last("level_started")["level_id"], Is.EqualTo(41));
            Assert.That(sink.Last("level_started")["content_level_id"], Is.EqualTo(21));
        }

        [Test]
        public void A_won_attempt_is_started_then_completed_with_its_taps_and_reward()
        {
            wallet.Grant(SqadSortResources.Coin, 120, GrantSource.Reward);
            t.LevelStarted(4, launch: 2);
            t.Tapped(); t.Tapped(); t.Tapped();
            t.LevelCompleted(4, 5, reward: 40);

            Assert.That(sink.Names, Is.EqualTo(new[] { "level_started", "screen_view", "level_completed", "level_up" }));
            var started = sink.Last("level_started");
            Assert.That(started.Params.Select(p => p.Key), Is.EqualTo(new[] { "level_id", "level_name", "level_type", "level_launch", "balance" }));
            Assert.That((started["level_id"], started["level_name"], started["level_type"], started["level_launch"], started["balance"]),
                Is.EqualTo((4, "null", "main", 2, 120L)));
            Assert.That(sink.Last("screen_view")["screen_name"], Is.EqualTo("action_phase"));
            var done = sink.Last("level_completed");
            Assert.That((done["level_counter"], done["reward"]), Is.EqualTo((3, "{\"coins\":40}")));
            var up = sink.Last("level_up");
            Assert.That((up["level"], up["character"]), Is.EqualTo((5, "main_level")));
            Assert.That(store.Progress.TrackedAttemptLevel, Is.EqualTo(0), "a finished attempt is no longer mirrored");
        }

        [Test]
        public void Failed_ends_the_attempt_once_and_abandoned_after_it_is_nothing()
        {
            t.LevelStarted(9, 1);
            t.Tapped();
            t.LevelFailed();
            t.LevelAbandoned();   // the screen unloading afterwards
            t.LevelFailed();
            Assert.That(sink.Names.Count(n => n.StartsWith("level_") && n != "level_started"), Is.EqualTo(1));
            Assert.That(sink.Last("level_failed")["level_counter"], Is.EqualTo(1));
        }

        [Test]
        public void A_restart_abandons_the_open_attempt_before_the_new_one_starts()
        {
            t.LevelStarted(9, 1);
            t.Tapped(); t.Tapped();
            t.LevelStarted(9, 2);
            Assert.That(sink.Names, Is.EqualTo(new[] { "level_started", "screen_view", "level_abandoned", "level_started", "screen_view" }));
            var abandoned = sink.Last("level_abandoned");
            Assert.That((abandoned["level_launch"], abandoned["level_counter"]), Is.EqualTo((1, 2)));
            Assert.That(sink.Last("level_started")["level_launch"], Is.EqualTo(2));
        }

        [Test]
        public void Taps_outside_an_attempt_are_not_counted()
        {
            t.Tapped();
            t.LevelStarted(1, 1);
            t.LevelAbandoned();
            Assert.That(sink.Last("level_abandoned")["level_counter"], Is.EqualTo(0));
        }

        [Test]
        public void An_attempt_the_app_died_in_is_abandoned_at_the_next_launch()
        {
            t.LevelStarted(12, 3);
            t.Tapped(); t.Tapped();
            t.AppBackgrounded(4, 0);   // mirrors the taps so far
            Assert.That((store.Progress.TrackedAttemptLevel, store.Progress.TrackedAttemptLaunch, store.Progress.TrackedAttemptCounter), Is.EqualTo((12, 3, 2)));

            // the process dies; a new one reads the same save
            var next = new EventSink();
            var t2 = new GameTracking(next, wallet, store, isTest: false);
            t2.SessionStarted();
            t2.SessionStarted();
            Assert.That(next.Names, Is.EqualTo(new[] { "level_abandoned" }));
            var a = next.Last("level_abandoned");
            Assert.That((a["level_id"], a["level_launch"], a["level_counter"]), Is.EqualTo((12, 3, 2)));
            Assert.That(store.Progress.TrackedAttemptLevel, Is.EqualTo(0), "reported once");
        }

        // ── D-097 user properties (Money Sort InitializeUserProperties) ──

        static readonly DateTime Nov9 = new DateTime(2026, 11, 9, 15, 30, 0, DateTimeKind.Utc);

        GameTracking At(DateTime utcNow, EventSink into = null) =>
            new GameTracking(into ?? sink, wallet, store, false, () => utcNow, () => "id-1");

        static Dictionary<string, object> PropsOf(UserPropertiesRecorded u) => u.Properties.ToDictionary(p => p.Key, p => p.Value);

        [Test]
        public void The_first_session_makes_the_user_id_and_install_day_and_sends_them_first()
        {
            var tr = At(Nov9);
            tr.LevelStarted(3, 1);   // the session starts with the first attempt
            Assert.That(sink.Events.First(), Is.InstanceOf<UserPropertiesRecorded>(), "before any plan event");
            var u = sink.Events.OfType<UserPropertiesRecorded>().Single();
            Assert.That(u.UserId, Is.EqualTo("id-1"));
            Assert.That(PropsOf(u), Is.EqualTo(new Dictionary<string, object> { ["user_id"] = "id-1", ["retained_days"] = 0, ["media_source"] = "unknown" }));
            Assert.That((store.Progress.AnalyticsUserId, store.Progress.AnalyticsInstallDayUtc), Is.EqualTo(("id-1", 20261109)));
            tr.SessionStarted();
            Assert.That(sink.Events.OfType<UserPropertiesRecorded>().Count(), Is.EqualTo(1), "once a session");
        }

        [Test]
        public void A_later_session_keeps_the_id_and_counts_whole_UTC_days()
        {
            store.Progress.AnalyticsUserId = "kept"; store.Progress.AnalyticsInstallDayUtc = 20261028;
            At(Nov9).SessionStarted();
            var u = sink.Events.OfType<UserPropertiesRecorded>().Single();
            Assert.That((u.UserId, PropsOf(u)["retained_days"]), Is.EqualTo(("kept", 12)));
            Assert.That(store.Saves, Is.EqualTo(0), "nothing new to save");
        }

        [Test]
        public void A_clock_set_back_reads_day_0_and_a_broken_day_starts_again()
        {
            store.Progress.AnalyticsUserId = "kept"; store.Progress.AnalyticsInstallDayUtc = 20261120;
            At(Nov9).SessionStarted();
            Assert.That(PropsOf(sink.Events.OfType<UserPropertiesRecorded>().Single())["retained_days"], Is.EqualTo(0));

            var next = new EventSink();
            store.Progress.AnalyticsInstallDayUtc = 20261340;   // month 13: not a day
            At(Nov9, next).SessionStarted();
            Assert.That(PropsOf(next.Events.OfType<UserPropertiesRecorded>().Single())["retained_days"], Is.EqualTo(0));
            Assert.That(store.Progress.AnalyticsInstallDayUtc, Is.EqualTo(20261109));
        }

        [Test]
        public void A_save_error_still_sends_the_user_properties()
        {
            store.Fail = true;
            Assert.DoesNotThrow(() => At(Nov9).SessionStarted());
            Assert.That(sink.Events.OfType<UserPropertiesRecorded>().Single().UserId, Is.EqualTo("id-1"));
        }

        [Test]
        public void The_first_launch_reports_its_loading_flow_once_per_install()
        {
            store.Progress.FirstLoadingTracked = false;
            t.SessionStarted();
            new GameTracking(sink, wallet, store, false).SessionStarted();
            Assert.That(sink.Tracked.Select(e => $"{e.Name} {e["meta_name"]}"), Is.EqualTo(new[]
            {
                "meta_started start", "meta_completed init_adjust", "meta_completed init_firebase", "meta_completed finish",
            }));
            Assert.That(sink.Tracked.All(e => (string)e["meta_id"] == "first_loading" && (string)e["meta_type"] == "flow"));
        }

        [Test]
        public void A_save_failure_never_reaches_the_caller()
        {
            store.Fail = true;
            Assert.DoesNotThrow(() => { t.LevelStarted(2, 1); t.AppBackgrounded(5, 0); t.LevelCompleted(2, 3, 40); });
            Assert.That(sink.Names, Does.Contain("level_completed"));
        }

        [Test]
        public void Rewarded_ads_carry_placement_balance_and_reward()
        {
            wallet.Grant(SqadSortResources.Coin, 300, GrantSource.Reward);
            t.LevelStarted(6, 1);
            t.AdStarted("RV", TrackingNames.Revive);
            t.AdCompleted("RV", TrackingNames.Revive, "{\"revive\":1}");
            t.AdFailed("RV", TrackingNames.RvSlotLose);
            var start = sink.Last("IAA_start");
            Assert.That(start.Params.Select(p => p.Key), Is.EqualTo(new[] { "iaa_type", "level_id", "placement", "real_currency_name", "value", "balance" }));
            Assert.That((start["iaa_type"], start["level_id"], start["placement"], start["real_currency_name"], start["value"], start["balance"]),
                Is.EqualTo(("RV", 6, "ep_revive", "USD", 0d, 300L)));
            Assert.That(sink.Last("IAA_completed")["reward"], Is.EqualTo("{\"revive\":1}"));
            Assert.That(sink.Last("IAA_completed")["isTest"], Is.Null, "a release build sends no isTest");
            Assert.That((sink.Last("IAA_failed")["placement"], sink.Last("IAA_failed")["receiptStatus"]), Is.EqualTo(("ep_rv_slot", "not_completed")));
        }

        [Test]
        public void Test_builds_mark_completed_ads()
        {
            var test = new GameTracking(sink, wallet, store, isTest: true);
            test.AdCompleted("inter", TrackingNames.LevelComplete, null);
            Assert.That(sink.Last("IAA_completed")["isTest"], Is.EqualTo(true));
            Assert.That(sink.Last("IAA_completed")["reward"], Is.Null, "an interstitial has no reward");
        }

        [Test]
        public void Background_is_sent_once_per_trip_with_the_screen_underneath()
        {
            t.LevelStarted(3, 1);
            t.AppBackgrounded(4, 600);
            t.AppBackgrounded(4, 600);   // the quit after the pause
            t.AppResumed();
            t.ScreenViewed(TrackingNames.Home);
            t.AppBackgrounded(5, 0);
            var trips = sink.Tracked.Where(e => e.Name == "app_backgrounded").ToList();
            Assert.That(trips.Select(e => e["screen_name"]), Is.EqualTo(new[] { "action_phase", "home" }));
            Assert.That((trips[0]["level_id"], trips[0]["current_lives"], trips[0]["next_life_seconds"], trips[0]["levels_played_session"]),
                Is.EqualTo((3, 4, 600L, 1)));
        }

        [Test]
        public void Out_of_lives_and_its_buttons()
        {
            t.OutOfLivesShown(TrackingNames.TapPlay, 754, 0, false, true);
            t.UiTapped(TrackingNames.WatchAd, TrackingNames.OutOfLivesPopup);
            var shown = sink.Last("out_of_lives_shown");
            Assert.That((shown["trigger"], shown["next_life_seconds"], shown["refill_price"], shown["can_afford"], shown["rv_available"]),
                Is.EqualTo(("tap_play", 754L, 0L, false, true)));
            var ui = sink.Last("ui_interacted");
            Assert.That((ui["ui_action"], ui["ui_name"], ui["ui_location"], ui["ui_type"]), Is.EqualTo(("tap", "watch_ad", "out_of_lives_popup", "button")));
        }

        [Test]
        public void Currency_events_report_the_balance_of_what_moved()
        {
            store.Progress.BoosterUndo = 4;
            t.LevelStarted(8, 1);
            t.Earned(TrackingNames.Booster(Booster.Undo), 3, TrackingNames.ActionPhase);
            t.Earned(TrackingNames.Coins, 0, "nothing");   // nothing moved: no event
            var e = sink.Last("earn_virtual_currency");
            Assert.That((e["virtual_currency_type"], e["virtual_currency_name"], e["value"], e["placement"], e["level_id"], e["balance"]),
                Is.EqualTo(("soft", "ap_booster_undo", 3L, "action_phase", 8, 4L)));
            Assert.That(sink.Names.Count(n => n == "earn_virtual_currency"), Is.EqualTo(1));
        }

        [Test]
        public void First_clears_still_feed_Adjust()
        {
            t.FirstClear(10);
            Assert.That(sink.Events.OfType<LevelWon>().Single().Level, Is.EqualTo(10));
        }

        [Test]
        public void The_console_shows_the_name_and_parameters()
        {
            t.LevelStarted(2, 1);
            Assert.That(sink.Last("level_started").ToString(),
                Is.EqualTo("level_started { level_id = 2, level_name = null, level_type = main, level_launch = 1, balance = 0 }"));
        }
    }

    /// <summary>D-061: what LevelProgressService reports once a change is saved — and nothing when it was rolled back.</summary>
    public class ProgressFactsTests
    {
        TestStore store; TestWallet wallet; TestFacts facts; EconomyTuning tuning; LevelProgressService svc;
        static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); facts = new TestFacts(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, wallet, tuning, facts);
        }

        [Test]
        public void Coins_in_and_out_carry_the_plan_placements()
        {
            svc.EnsureStartCoins();
            svc.EnsureStartCoins();                       // once per install
            svc.CompleteLevel(1);
            svc.GrantWinBonus(tuning.WinReward);
            svc.TrySpendCoins(50, TrackingNames.Revive);
            Assert.That(facts.Log, Is.EqualTo(new[]
            {
                $"earn coins {tuning.StartCoins} install_game",
                $"earn coins {tuning.WinReward} level_up",
                $"earn coins {tuning.WinReward * (tuning.WinAdMultiplier - 1)} ap_free_coins",
                "spend coins 50 ep_revive",
            }));
        }

        [Test]
        public void Boosters_unlock_get_bought_granted_and_used()
        {
            wallet.Grant(SqadSortResources.Coin, 1000, GrantSource.Reward);
            svc.UnlockBoostersFor(tuning.UnlockLevel(Booster.Undo));
            svc.TryBuyBooster(Booster.Undo);
            svc.GrantBoosterFromAd(Booster.Undo);
            svc.TryUseBooster(Booster.Undo);
            Assert.That(facts.Log, Is.EqualTo(new[]
            {
                "unlock ap_booster_undo",
                $"earn ap_booster_undo {tuning.BoosterUnlockGift} unlock_gift",
                $"spend coins {tuning.Price(Booster.Undo)} ap_booster_undo",
                $"earn ap_booster_undo {tuning.BuyAmount(Booster.Undo)} action_phase",
                $"earn ap_booster_undo {tuning.AdAmount(Booster.Undo)} action_phase",
                "spend ap_booster_undo 1 action_phase",
            }));
        }

        [Test]
        public void A_life_from_an_ad_and_an_element_unlock()
        {
            store.Progress.Lives = 0; store.Progress.LivesRefillFromUtc = Now.ToUnixTimeSeconds();
            svc.GrantLifeFromAd(Now);
            svc.UnlockElementsFor(tuning.HiddenLevel);
            Assert.That(facts.Log, Is.EqualTo(new[] { "earn lives 1 out_of_lives_rv", "unlock element_hidden" }));
        }

        [Test]
        public void A_life_from_an_ad_when_full_reports_nothing_received()
        {
            svc.GrantLifeFromAd(Now);
            Assert.That(facts.Log, Is.EqualTo(new[] { "earn lives 0 out_of_lives_rv" }), "the tracker drops a zero amount");
        }

        [Test]
        public void A_rolled_back_change_reports_nothing()
        {
            wallet.Grant(SqadSortResources.Coin, 1000, GrantSource.Reward);
            store.Fail = true;
            svc.EnsureStartCoins();
            svc.CompleteLevel(1);
            svc.TrySpendCoins(50, TrackingNames.Revive);
            svc.UnlockBoostersFor(20);
            svc.TryBuyBooster(Booster.Shuffle);
            svc.UnlockElementsFor(20);
            Assert.That(facts.Log, Is.Empty);
        }

        [Test]
        public void Wired_to_the_real_tracker_a_purchase_reports_balances_after_it()
        {
            var sink = new EventSink();
            var tracking = new GameTracking(sink, wallet, store, false);
            var live = new LevelProgressService(store, wallet, tuning, tracking);
            wallet.Grant(SqadSortResources.Coin, 500, GrantSource.Reward);
            live.TryBuyBooster(Booster.Remove);
            var spend = sink.Last("spend_virtual_currency");
            var earn = sink.Last("earn_virtual_currency");
            Assert.That((spend["virtual_currency_name"], spend["value"], spend["balance"]), Is.EqualTo(("coins", (long)tuning.Price(Booster.Remove), 500L - tuning.Price(Booster.Remove))));
            Assert.That((earn["virtual_currency_name"], earn["balance"]), Is.EqualTo(("ap_booster_remove", (long)tuning.BuyAmount(Booster.Remove))));
        }
    }
}
