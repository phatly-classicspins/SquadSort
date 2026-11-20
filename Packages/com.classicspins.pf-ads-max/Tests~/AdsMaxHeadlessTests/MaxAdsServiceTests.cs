using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// Every <see cref="MaxAdsService"/> row of the story's I/O matrix, against a scripted bridge: the
    /// show outcomes, the visibility pair, the auto-log, the refusals and the can-show gate.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdsServiceTests
    {
        private static readonly AdPlacement P = new AdPlacement("p");
        private const string Unit = "u";

        private FakeMaxBridge _bridge;
        private RecordingPublisher _visibility;
        private RecordingAnalytics _analytics;
        private DictConfig _config;
        private RecordingLog _log;
        private FakeDelay _delay;
        private MaxAdsService _service;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true, Ready = true };
            _visibility = new RecordingPublisher();
            _analytics = new RecordingAnalytics();
            _config = new DictConfig().Set("ads.max.p.unitId", Unit);
            _log = new RecordingLog();
            _delay = new FakeDelay();
            _service = Build(_bridge, platform: null);
            Requested();
        }

        private MaxAdsService Build(IMaxBridge bridge, string platform)
        {
            var units = new MaxAdUnits(bridge, _config, _log, platform, _delay.Func);
            return new MaxAdsService(bridge, units, new MaxAdVisibility(_visibility, _log),
                new MaxAdReadiness(bridge, units, _config, new NullAdReadinessChangedPublisher(), _log),
                _analytics, _config, _log, _delay.Func);
        }

        /// <summary>Put P's unit in the requested set (as a SKU's early Preload does) and forget that load.</summary>
        private void Requested()
        {
            _service.Preload(P);
            _bridge.LoadCalls.Clear();
            _bridge.Calls.Clear();
        }

        private void MakeRewarded() => _config.Set("ads.p.rewarded", 1);

        private void AssertWarnNamesModule()
            => Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max")), Is.True,
                "every refusal/failure warning names the module: " + string.Join(" | ", _log.Warns));

        // ── show outcomes ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Interstitial_hidden_resolves_Shown_publishes_the_pair_tracks_AdShown_and_reloads()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(show.IsCompleted, Is.False, "the show awaits an SDK callback");
            Assert.That(_bridge.ShowCalls, Is.EqualTo(new[] { (Unit, false) }), "absent ads.p.rewarded ⇒ interstitial");
            Assert.That(_bridge.ShowPlacements, Is.EqualTo(new[] { "p" }), "the placement is passed through to MAX");
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true }), "show-start published before the ad is up");

            _bridge.RaiseHidden(Unit);
            var result = await show;

            Assert.That(result, Is.EqualTo(AdResult.Shown));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_visibility.Published.All(v => !v.BannerVisible && v.BannerHeightPx == 0), Is.True);
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdShown("p")));
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { (Unit, false) }), "the next fill is requested");
            Assert.That(_bridge.LiveSubscribers, Is.Zero, "per-show subscriptions are released");
        }

        [Test]
        public async Task A_pending_retry_for_the_unit_is_not_pre_empted_by_the_post_show_reload()
        {
            // The post-show reload must go through MaxAdUnits.Request, not straight to the bridge: a retry
            // already pending for this unit (at most one pending) must not get an early, second Load.
            _bridge.RaiseLoadFailed(Unit, "no fill");
            Assert.That(_delay.Pending, Is.EqualTo(1), "a retry is scheduled");
            _bridge.LoadCalls.Clear();

            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseHidden(Unit);
            await show;

            Assert.That(_bridge.LoadCalls, Is.Empty, "the pending retry must not be pre-empted by an early load");
        }

        [Test]
        public async Task Rewarded_then_hidden_resolves_Rewarded_and_the_second_callback_is_a_no_op()
        {
            MakeRewarded();
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_bridge.ShowCalls, Is.EqualTo(new[] { (Unit, true) }));

            _bridge.RaiseRewarded(Unit);
            Assert.DoesNotThrow(() => _bridge.RaiseHidden(Unit));
            var result = await show;

            Assert.That(result, Is.EqualTo(AdResult.Rewarded));
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdRewarded("p")));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task Rewarded_unit_hidden_without_a_reward_is_Skipped_tracks_nothing_and_a_late_reward_is_a_no_op()
        {
            MakeRewarded();
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();

            _bridge.RaiseHidden(Unit);
            var result = await show;
            Assert.DoesNotThrow(() => _bridge.RaiseRewarded(Unit));

            Assert.That(result, Is.EqualTo(AdResult.Skipped));
            Assert.That(_analytics.Tracked, Is.Empty, "Skipped maps to no funnel event");
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task Display_failed_resolves_Failed_publishes_the_pair_tracks_AdFailed_and_warns()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();

            _bridge.RaiseDisplayFailed(Unit, "msg");
            var result = await show;

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            AssertWarnNamesModule();
            Assert.That(_log.Warns.Any(w => w.Contains("msg")), Is.True, "the SDK message is carried");
        }

        [Test]
        public async Task Another_units_callbacks_are_ignored()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();

            _bridge.RaiseHidden("other");
            _bridge.RaiseRewarded("other");
            _bridge.RaiseDisplayFailed("other", "nope");
            Assert.That(show.IsCompleted, Is.False, "a different unit's callback never completes this show");

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
        }

        [Test]
        public async Task A_token_cancelled_after_the_ad_is_up_is_ignored()
        {
            using var cts = new CancellationTokenSource();
            var show = _service.ShowAsync(P, cts.Token).AsTask();
            cts.Cancel();
            Assert.That(show.IsCompleted, Is.False, "control never returns while a fullscreen unit covers the screen");

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
        }

        // ── bridge throws ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Show_throwing_resolves_Failed_and_still_publishes_false()
        {
            _bridge.ThrowOnShow = true;

            var result = await _service.ShowAsync(P, CancellationToken.None).AsTask();

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }), "the flag was raised, so false follows");
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            Assert.That(_bridge.LiveSubscribers, Is.Zero);
            AssertWarnNamesModule();
        }

        [Test]
        public async Task IsReady_throwing_resolves_Failed_without_raising_the_flag()
        {
            _bridge.ThrowOnIsReady = true;

            var result = await _service.ShowAsync(P, CancellationToken.None).AsTask();

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_visibility.Published, Is.Empty, "the flag was never raised");
            Assert.That(_bridge.Subscriptions, Is.Zero);
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            AssertWarnNamesModule();
        }

        // ── refusals ───────────────────────────────────────────────────────────────────────────────────

        private async Task AssertRefusal(CancellationToken ct = default)
        {
            var result = await _service.ShowAsync(P, ct).AsTask();

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_visibility.Published, Is.Empty, "a refusal publishes no visibility");
            Assert.That(_bridge.Subscriptions, Is.Zero, "a refusal subscribes to nothing");
            Assert.That(_bridge.ShowCalls, Is.Empty);
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            AssertWarnNamesModule();
        }

        [Test]
        public async Task Refuses_when_the_bridge_is_not_initialized()
        {
            _bridge.Initialized = false;
            await AssertRefusal();
            Assert.That(_bridge.IsReadyCalls, Is.Empty);
            Assert.That(_bridge.LoadCalls, Is.Empty);
        }

        [Test]
        public async Task Refuses_when_the_unit_id_is_missing_and_names_the_key()
        {
            _config = new DictConfig();
            _service = Build(_bridge, platform: null);

            await AssertRefusal();
            Assert.That(_log.Warns.Any(w => w.Contains("ads.max.p.unitId")), Is.True, string.Join(" | ", _log.Warns));
        }

        [Test]
        public async Task Refuses_when_the_unit_id_is_empty()
        {
            _config.Set("ads.max.p.unitId", "");
            await AssertRefusal();
        }

        [Test]
        public async Task Refuses_when_not_ready_and_requests_a_load()
        {
            MakeRewarded();
            _bridge.Ready = false;

            await AssertRefusal();
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { (Unit, true) }));
        }

        [Test]
        public async Task Refuses_when_the_token_is_already_cancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await AssertRefusal(cts.Token);
        }

        [Test]
        public void A_cancelled_token_is_a_Failed_result_never_a_throw()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.DoesNotThrowAsync(async () => await _service.ShowAsync(P, cts.Token).AsTask());
        }

        // ── can-show ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task IsReadyAsync_is_false_and_touches_nothing_when_the_bridge_is_not_initialized()
        {
            _bridge.Initialized = false;

            Assert.That(await _service.IsReadyAsync(P).AsTask(), Is.False);
            _service.Preload(P);

            Assert.That(_bridge.IsReadyCalls, Is.Empty);
            Assert.That(_bridge.LoadCalls, Is.Empty);
        }

        [Test]
        public async Task IsReadyAsync_asks_the_bridge_with_the_configured_format_once_initialized()
        {
            // Story 10.7: ready(unit) asks the bridge with the format the unit was REQUESTED as, so the
            // placement is rewarded before its unit is first requested (SetUp's Preload requested it as an
            // interstitial; flipping the flag afterwards is the "one unit, two formats" misconfiguration).
            MakeRewarded();
            _service = Build(_bridge, platform: null);
            Requested();
            _bridge.IsReadyCalls.Clear();
            _bridge.Ready = true;

            Assert.That(await _service.IsReadyAsync(P).AsTask(), Is.True);
            Assert.That(_bridge.IsReadyCalls, Is.EqualTo(new[] { (Unit, true) }));
        }

        [Test]
        public async Task IsReadyAsync_is_false_when_the_bridge_throws()
        {
            _bridge.ThrowOnIsReady = true;
            Assert.That(await _service.IsReadyAsync(P).AsTask(), Is.False);
            AssertWarnNamesModule();
        }

        [Test]
        public void Preload_loads_the_configured_unit_and_format()
        {
            _service.Preload(P);
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { (Unit, false) }));
        }

        // ── v2: the requested set ──────────────────────────────────────────────────────────────────────

        [Test]
        public async Task IsReadyAsync_on_a_never_requested_unit_is_false_requests_a_load_and_skips_the_bridge()
        {
            _service = Build(_bridge, platform: null); // a fresh unit set: nothing requested

            Assert.That(await _service.IsReadyAsync(P).AsTask(), Is.False);
            Assert.That(_bridge.IsReadyCalls, Is.Empty, "bridge IsReady is not asked for an unrequested unit");
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { (Unit, false) }), "a load is requested");

            Assert.That(await _service.IsReadyAsync(P).AsTask(), Is.True, "requested now: the bridge answers");
            Assert.That(_bridge.IsReadyCalls, Is.EqualTo(new[] { (Unit, false) }));
            Assert.That(_bridge.LoadCalls, Has.Count.EqualTo(1), "IsReadyAsync on a requested unit loads nothing");
        }

        [Test]
        public async Task The_first_show_of_a_never_requested_unit_requests_it_and_refuses()
        {
            _service = Build(_bridge, platform: null);

            await AssertRefusal();
            Assert.That(_bridge.IsReadyCalls, Is.Empty);
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { (Unit, false) }));
            Assert.That(_log.Warns.Any(w => w.Contains("requested")), Is.True, string.Join(" | ", _log.Warns));

            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown), "the next show of the now-requested unit goes ahead");
        }

        [Test]
        public void Preload_before_init_records_the_request_without_touching_the_bridge()
        {
            _bridge.Initialized = false;
            var units = new MaxAdUnits(_bridge, _config, _log, null, _delay.Func);
            var service = new MaxAdsService(_bridge, units, new MaxAdVisibility(_visibility, _log),
                new MaxAdReadiness(_bridge, units, _config, new NullAdReadinessChangedPublisher(), _log), _analytics, _config, _log, _delay.Func);

            service.Preload(P);

            Assert.That(units.IsRequested(Unit), Is.True);
            Assert.That(_bridge.LoadCalls, Is.Empty, "loaded when auto-load starts after init");
        }

        // ── v2: unit resolution ────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_rewarded_placement_with_only_the_format_default_shows_that_unit_tagged_with_the_placement()
        {
            _config = new DictConfig().Set("ads.p.rewarded", 1).Set("ads.max.rewarded.unitId", "R");
            _service = Build(_bridge, platform: MaxAdUnits.Android);
            _service.Preload(P);

            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_bridge.ShowCalls, Is.EqualTo(new[] { ("R", true) }));
            Assert.That(_bridge.ShowPlacements, Is.EqualTo(new[] { "p" }));

            _bridge.RaiseRewarded("R");
            Assert.That(await show, Is.EqualTo(AdResult.Rewarded));
        }

        [Test]
        public async Task The_platform_placement_tier_wins_over_the_placement_tier()
        {
            _config.Set("ads.max.android.p.unitId", "A");
            _service = Build(_bridge, platform: MaxAdUnits.Android);
            _service.Preload(P);

            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_bridge.ShowCalls, Is.EqualTo(new[] { ("A", false) }));
            _bridge.RaiseHidden("A");
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
        }

        [Test]
        public async Task With_every_tier_empty_the_refusal_names_the_placement_key_and_its_fallbacks()
        {
            _config = new DictConfig();
            _service = Build(_bridge, platform: MaxAdUnits.Ios);

            await AssertRefusal();
            var warn = _log.Warns.Single(w => w.Contains("refused"));
            Assert.That(warn, Does.Contain("ads.max.p.unitId"));
            Assert.That(warn, Does.Contain("ads.max.ios.p.unitId"));
            Assert.That(warn, Does.Contain("ads.max.interstitial.unitId"));
        }

        // ── v2: display timeout (DW-36) ────────────────────────────────────────────────────────────────

        [Test]
        public async Task No_display_signal_within_the_timeout_resolves_Failed_closes_the_pair_and_ignores_late_callbacks()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_delay.Seconds, Is.EqualTo(new[] { 10.0 }), "absent ads.max.displayTimeoutSeconds ⇒ 10 s");
            Assert.That(show.IsCompleted, Is.False);

            _delay.ElapseLast();
            var result = await show;

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max") && w.Contains("no display signal")), Is.True,
                string.Join(" | ", _log.Warns));
            Assert.That(_bridge.LiveSubscribers, Is.Zero);

            Assert.DoesNotThrow(() =>
            {
                _bridge.RaiseDisplayed(Unit);
                _bridge.RaiseRewarded(Unit);
                _bridge.RaiseHidden(Unit);
            });
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }), "late callbacks publish nothing");
            Assert.That(_analytics.Tracked, Has.Count.EqualTo(1));
        }

        [TestCase(3, 3.0)]
        [TestCase(0, 10.0)]
        [TestCase(-5, 10.0)]
        public void The_display_timeout_reads_its_key_and_falls_back_to_10_s(int configured, double expected)
        {
            _config.Set(MaxAdUnits.DisplayTimeoutKey, configured);
            _service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_delay.Seconds, Is.EqualTo(new[] { expected }));
            _bridge.RaiseHidden(Unit);
        }

        [Test]
        public async Task A_long_ad_once_displayed_is_never_timed_out()
        {
            MakeRewarded();
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();

            _bridge.RaiseDisplayed(Unit);
            Assert.That(_delay.Calls.Single().IsCancelled, Is.True, "the display signal stops the clock");
            Assert.That(show.IsCompleted, Is.False);

            _bridge.RaiseRewarded(Unit);
            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Rewarded));
            Assert.That(_delay.Calls, Has.Count.EqualTo(1), "no timer after the display");
        }

        [Test]
        public async Task Display_failed_stops_the_clock()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseDisplayFailed(Unit, "msg");

            Assert.That(await show, Is.EqualTo(AdResult.Failed));
            Assert.That(_delay.Calls.Single().IsCancelled, Is.True);
        }

        [Test]
        public async Task Another_units_display_signal_does_not_stop_the_clock()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseDisplayed("other");
            Assert.That(_delay.Calls.Single().IsPending, Is.True);

            _delay.ElapseLast();
            Assert.That(await show, Is.EqualTo(AdResult.Failed));
        }

        // ── v2: the auto-log switch ────────────────────────────────────────────────────────────────────

        [Test]
        public async Task AutoLog_0_returns_the_result_and_tracks_nothing()
        {
            _config.Set(MaxAdUnits.AutoLogKey, 0);
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseHidden(Unit);

            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(_analytics.Tracked, Is.Empty);
        }

        [Test]
        public async Task AutoLog_non_zero_tracks_as_when_absent()
        {
            _config.Set(MaxAdUnits.AutoLogKey, 1);
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            _bridge.RaiseHidden(Unit);

            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdShown("p")));
        }

        // ── the null bridge behind the service ─────────────────────────────────────────────────────────

        [Test]
        public async Task Backed_by_the_null_bridge_every_show_is_Failed()
        {
            var service = Build(new NullMaxBridge(_log), platform: null);

            Assert.That(await service.ShowAsync(P, CancellationToken.None).AsTask(), Is.EqualTo(AdResult.Failed));
            Assert.That(await service.IsReadyAsync(P).AsTask(), Is.False);
            Assert.That(_visibility.Published, Is.Empty);
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));
            AssertWarnNamesModule();
        }
    }
}
