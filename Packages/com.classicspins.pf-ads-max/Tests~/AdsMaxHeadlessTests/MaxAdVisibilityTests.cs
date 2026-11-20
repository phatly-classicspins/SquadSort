using System;
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
    /// Story 10.6 / AD-MOD-12: the one visibility state. The state-level rows (derivation, publish on
    /// change only, idempotent close, the exclusive open, a throwing publisher) and the producer overlap
    /// matrix — one shared state, a <see cref="MaxAdsService"/> and a <see cref="MaxAdsInitNode"/> over one
    /// fake bridge that is initialized and whose init stays pending until the test releases it.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdVisibilityTests
    {
        private static readonly AdPlacement P = new AdPlacement("p");
        private const string Unit = "u";
        private const string UnitQ = "v";

        private FakeMaxBridge _bridge;
        private RecordingPublisher _publisher;
        private RecordingAnalytics _analytics;
        private DictConfig _config;
        private RecordingLog _log;
        private FakeDelay _delay;
        private MaxAdVisibility _state;
        private MaxAdsService _service;
        private MaxAdsInitNode _node;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true, Ready = true };
            _publisher = new RecordingPublisher();
            _analytics = new RecordingAnalytics();
            _config = new DictConfig().Set("ads.max.p.unitId", Unit).Set("ads.max.q.unitId", UnitQ);
            _log = new RecordingLog();
            _delay = new FakeDelay();
            _state = new MaxAdVisibility(_publisher, _log);
            var units = new MaxAdUnits(_bridge, _config, _log, null, _delay.Func);
            var readiness = new MaxAdReadiness(_bridge, units, _config, new NullAdReadinessChangedPublisher(), _log);
            _service = new MaxAdsService(_bridge, units, _state, readiness, _analytics, _config, _log, _delay.Func);
            _node = new MaxAdsInitNode(_bridge, units, _config, _state, readiness, _log);

            // Both units requested, as a SKU's early Preload does; forget those loads.
            _service.Preload(P);
            _service.Preload(new AdPlacement("q"));
            ResetBridgeCounters();
        }

        private void ResetBridgeCounters()
        {
            _bridge.Calls.Clear();
            _bridge.LoadCalls.Clear();
            _bridge.IsInitializedReads = 0;
        }

        /// <summary>Starts the init node; MAX's init (and so the consent window) stays pending.</summary>
        private Task<BootNodeResult> StartInit()
        {
            var run = _node.RunAsync(null, CancellationToken.None).AsTask();
            Assert.That(_bridge.InitCount, Is.EqualTo(1), "SDK init started");
            Assert.That(run.IsCompleted, Is.False, "the consent window is open until init completes");
            return run;
        }

        private async Task CompleteInit(Task<BootNodeResult> run)
        {
            _bridge.PendingInit.TrySetResult(true);
            Assert.That(await run, Is.EqualTo(BootNodeResult.Succeeded));
        }

        /// <summary>Every row's end: no slot left open, and the last fact published is "not visible".</summary>
        private void AssertAllClosed()
        {
            Assert.That(_state.IsFullscreenOpen, Is.False, "no slot is left open");
            Assert.That(_publisher.Fullscreen.Last(), Is.False, "the last published value is false");
            Assert.That(_publisher.Published.All(v => !v.BannerVisible && v.BannerHeightPx == 0), Is.True,
                "there is no banner slot");
        }

        private void AssertWarnNamesModule(string fragment)
            => Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max") && w.Contains(fragment)), Is.True,
                string.Join(" | ", _log.Warns));

        private void AssertRefusedWithoutTouchingTheBridge(AdResult result, int subscriptionsBefore)
        {
            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(_bridge.IsInitializedReads, Is.Zero, "refused before any bridge read");
            Assert.That(_bridge.Calls, Is.Empty, "no bridge call");
            Assert.That(_bridge.Subscriptions, Is.EqualTo(subscriptionsBefore), "no subscription");
            AssertWarnNamesModule("another fullscreen ad surface is open");
        }

        // ── state level ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_state_starts_not_visible_and_publishes_nothing_until_a_change()
        {
            Assert.That(_state.IsFullscreenOpen, Is.False);
            Assert.That(_publisher.Published, Is.Empty);
        }

        [Test]
        public void The_constructor_null_guards_its_dependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new MaxAdVisibility(null, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdVisibility(_publisher, null));
        }

        [Test]
        public void An_unknown_surface_is_an_argument_error()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _state.Open((MaxAdSurface)7));
            Assert.That(_state.IsFullscreenOpen, Is.False);
        }

        [TestCase(MaxAdSurface.Show)]
        [TestCase(MaxAdSurface.ConsentFlow)]
        [TestCase(MaxAdSurface.PrivacyOptions)]
        public void Every_surface_is_fullscreen_and_derives_the_fact(MaxAdSurface surface)
        {
            var slot = _state.Open(surface);
            Assert.That(slot.Surface, Is.EqualTo(surface));
            Assert.That(slot.IsOpen, Is.True);
            Assert.That(_state.IsFullscreenOpen, Is.True);
            Assert.That(_publisher.Published, Is.EqualTo(new[] { new AdVisibilityChanged(true, false, 0) }));

            slot.Close();

            Assert.That(slot.IsOpen, Is.False);
            Assert.That(_publisher.Published, Is.EqualTo(new[]
            {
                new AdVisibilityChanged(true, false, 0), new AdVisibilityChanged(false, false, 0),
            }));
            AssertAllClosed();
        }

        [Test]
        public void The_fact_is_published_only_when_the_derived_value_changes()
        {
            var show = _state.Open(MaxAdSurface.Show);
            var consent = _state.Open(MaxAdSurface.ConsentFlow);
            var privacy = _state.Open(MaxAdSurface.PrivacyOptions);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "a second and third open change nothing");

            show.Close();
            consent.Close();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "closing while another slot is open publishes nothing");

            privacy.Close();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }), "the last close publishes false");

            _state.Open(MaxAdSurface.Show).Close();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false, true, false }));
            AssertAllClosed();
        }

        [Test]
        public void A_second_close_of_one_handle_never_closes_another_slot_of_the_same_surface()
        {
            var first = _state.Open(MaxAdSurface.ConsentFlow);
            var second = _state.Open(MaxAdSurface.ConsentFlow);

            first.Close();
            first.Close();

            Assert.That(first.IsOpen, Is.False);
            Assert.That(second.IsOpen, Is.True);
            Assert.That(_state.IsFullscreenOpen, Is.True, "the other handle's slot is still open");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            second.Close();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public void A_second_close_of_one_handle_never_closes_another_producers_slot()
        {
            var show = _state.Open(MaxAdSurface.Show);
            var consent = _state.Open(MaxAdSurface.ConsentFlow);

            show.Close();
            show.Close();

            Assert.That(consent.IsOpen, Is.True);
            Assert.That(_state.IsFullscreenOpen, Is.True);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            consent.Close();
            AssertAllClosed();
        }

        [Test]
        public void An_exclusive_open_with_nothing_open_opens_and_publishes()
        {
            MaxAdVisibility.Slot slot;
            Assert.That(_state.TryOpenExclusive(MaxAdSurface.Show, out slot), Is.True);
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.Surface, Is.EqualTo(MaxAdSurface.Show));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            slot.Close();
            AssertAllClosed();
        }

        [TestCase(MaxAdSurface.Show)]
        [TestCase(MaxAdSurface.ConsentFlow)]
        [TestCase(MaxAdSurface.PrivacyOptions)]
        public void An_exclusive_open_is_refused_while_any_fullscreen_slot_is_open(MaxAdSurface occupying)
        {
            var open = _state.Open(occupying);

            MaxAdVisibility.Slot privacy;
            Assert.That(_state.TryOpenExclusive(MaxAdSurface.PrivacyOptions, out privacy), Is.False);
            Assert.That(privacy, Is.Null);
            MaxAdVisibility.Slot show;
            Assert.That(_state.TryOpenExclusive(MaxAdSurface.Show, out show), Is.False);
            Assert.That(show, Is.Null);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "a refusal publishes nothing");

            open.Close();
            Assert.That(_state.TryOpenExclusive(MaxAdSurface.PrivacyOptions, out privacy), Is.True, "free again once closed");
            privacy.Close();
            AssertAllClosed();
        }

        [Test]
        public void The_unconditional_open_is_never_refused()
        {
            MaxAdVisibility.Slot show;
            Assert.That(_state.TryOpenExclusive(MaxAdSurface.Show, out show), Is.True);

            var consent = _state.Open(MaxAdSurface.ConsentFlow);

            Assert.That(consent.IsOpen, Is.True, "SDK init is never refused");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));
            show.Close();
            consent.Close();
            AssertAllClosed();
        }

        [Test]
        public void A_publisher_that_throws_on_true_is_a_warning_and_false_still_follows()
        {
            var publisher = new ThrowingPublisher();
            var state = new MaxAdVisibility(publisher, _log);

            MaxAdVisibility.Slot slot = null;
            Assert.DoesNotThrow(() => slot = state.Open(MaxAdSurface.Show));
            Assert.That(state.IsFullscreenOpen, Is.True, "the open is recorded");
            Assert.That(publisher.Attempts, Is.EqualTo(1));
            AssertWarnNamesModule("publish threw");

            Assert.DoesNotThrow(() => slot.Close());
            Assert.That(publisher.Fullscreen, Is.EqualTo(new[] { false }), "the failed true is still followed by false");
            Assert.That(state.IsFullscreenOpen, Is.False);
        }

        [Test]
        public void A_publisher_that_always_throws_never_throws_through_the_state()
        {
            var publisher = new ThrowingPublisher { ThrowOnHidden = true };
            var state = new MaxAdVisibility(publisher, _log);

            Assert.DoesNotThrow(() => state.Open(MaxAdSurface.ConsentFlow).Close());
            Assert.That(publisher.Attempts, Is.EqualTo(2));
            Assert.That(_log.Warns.Count(w => w.Contains("pf-ads-max") && w.Contains("publish threw")), Is.EqualTo(2));
            Assert.That(state.IsFullscreenOpen, Is.False);

            Assert.DoesNotThrow(() => state.Open(MaxAdSurface.ConsentFlow).Close());
            Assert.That(publisher.Attempts, Is.EqualTo(4), "the recorded value tracks the slots, not the failed publish");
        }

        [Test]
        public async Task A_show_whose_publisher_throws_still_shows_and_still_publishes_false()
        {
            var publisher = new ThrowingPublisher();
            var units = new MaxAdUnits(_bridge, _config, _log, null, _delay.Func);
            var service = new MaxAdsService(_bridge, units, new MaxAdVisibility(publisher, _log),
                new MaxAdReadiness(_bridge, units, _config, new NullAdReadinessChangedPublisher(), _log),
                _analytics, _config, _log, _delay.Func);
            service.Preload(P);

            var show = service.ShowAsync(P, CancellationToken.None).AsTask();
            Assert.That(_bridge.ShowCalls, Has.Count.EqualTo(1), "a throwing publisher is a warning, not a failed show");

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(publisher.Fullscreen, Is.EqualTo(new[] { false }));
            AssertWarnNamesModule("publish threw");
        }

        // ── producer overlap matrix ────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Show_then_consent_with_the_show_closing_first_publishes_false_only_after_init()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            var run = StartInit();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "the consent window is still open");
            Assert.That(_state.IsFullscreenOpen, Is.True);

            await CompleteInit(run);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public async Task Show_then_consent_with_the_consent_closing_first_publishes_false_only_after_hidden()
        {
            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            var run = StartInit();

            await CompleteInit(run);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "the show is still up");
            Assert.That(show.IsCompleted, Is.False);

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public async Task A_show_requested_while_the_consent_flow_is_open_is_refused_before_any_bridge_call()
        {
            var run = StartInit();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));
            ResetBridgeCounters();
            var subscriptions = _bridge.Subscriptions;

            var result = await _service.ShowAsync(P, CancellationToken.None).AsTask();

            AssertRefusedWithoutTouchingTheBridge(result, subscriptions);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "nothing new is published");
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")), "auto-logged like every refusal");

            await CompleteInit(run);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public async Task A_surface_opened_after_the_pre_check_is_caught_by_the_exclusive_open()
        {
            // The pre-check passes; a slot opens mid-check (re-entrancy), so only the exclusive open refuses.
            MaxAdVisibility.Slot consent = null;
            _bridge.DuringIsReady = () => consent = _state.Open(MaxAdSurface.ConsentFlow);
            var subscriptions = _bridge.Subscriptions;

            var result = await _service.ShowAsync(P, CancellationToken.None).AsTask();

            Assert.That(result, Is.EqualTo(AdResult.Failed));
            Assert.That(consent, Is.Not.Null, "the slot opened after the pre-check");
            Assert.That(_bridge.ShowCalls, Is.Empty, "no show starts");
            Assert.That(_bridge.Subscriptions, Is.EqualTo(subscriptions), "no subscription");
            Assert.That(_bridge.LiveSubscribers, Is.Zero);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "only the slot's own true");
            AssertWarnNamesModule("another fullscreen ad surface is open");
            Assert.That(_analytics.Tracked.Single(), Is.EqualTo(new AdFailed("p")));

            consent.Close();
            AssertAllClosed();
        }

        [TestCase("p")]
        [TestCase("q")]
        public async Task A_second_show_while_a_show_is_up_is_refused_and_the_first_resolves_normally(string second)
        {
            var first = _service.ShowAsync(P, CancellationToken.None).AsTask();
            ResetBridgeCounters();
            var subscriptions = _bridge.Subscriptions;

            var result = await _service.ShowAsync(new AdPlacement(second), CancellationToken.None).AsTask();

            AssertRefusedWithoutTouchingTheBridge(result, subscriptions);
            Assert.That(_bridge.ShowCalls, Has.Count.EqualTo(1), "no second fullscreen show starts");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));
            Assert.That(first.IsCompleted, Is.False);

            _bridge.RaiseHidden(Unit);
            Assert.That(await first, Is.EqualTo(AdResult.Shown));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_analytics.Tracked, Is.EqualTo(new GameEvent[] { new AdFailed(second), new AdShown("p") }));
            AssertAllClosed();
        }

        public enum ShowFailure { DisplayFailed, DisplayTimeout, BridgeShowThrows }

        [TestCase(ShowFailure.DisplayFailed)]
        [TestCase(ShowFailure.DisplayTimeout)]
        [TestCase(ShowFailure.BridgeShowThrows)]
        public async Task A_failed_show_closes_its_slot_but_the_consent_window_keeps_the_fact_true(ShowFailure failure)
        {
            Task<BootNodeResult> run = null;
            if (failure == ShowFailure.BridgeShowThrows)
            {
                // The only overlap a throwing Show can have: consent opens while the show is mid-call.
                _bridge.ThrowOnShow = true;
                _bridge.BeforeShowReturns = () => run = StartInit();
            }

            var show = _service.ShowAsync(P, CancellationToken.None).AsTask();
            if (failure != ShowFailure.BridgeShowThrows) run = StartInit();
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            if (failure == ShowFailure.DisplayFailed) _bridge.RaiseDisplayFailed(Unit, "no fill");
            if (failure == ShowFailure.DisplayTimeout)
            {
                Assert.That(_delay.Calls.Last().Delay, Is.EqualTo(TimeSpan.FromSeconds(MaxAdUnits.DefaultDisplayTimeoutSeconds)));
                _delay.ElapseLast();
            }

            Assert.That(await show, Is.EqualTo(AdResult.Failed));
            Assert.That(_bridge.LiveSubscribers, Is.Zero);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "the consent window is still open");
            Assert.That(_state.IsFullscreenOpen, Is.True);
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max")), Is.True);

            await CompleteInit(run);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public async Task A_token_cancelled_before_the_show_is_refused_and_opens_no_slot()
        {
            var run = StartInit();
            ResetBridgeCounters();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(await _service.ShowAsync(P, cts.Token).AsTask(), Is.EqualTo(AdResult.Failed));
            Assert.That(_bridge.Calls, Is.Empty);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            await CompleteInit(run);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }

        [Test]
        public async Task A_token_cancelled_while_the_show_is_up_keeps_the_slot_open_until_hidden()
        {
            using var cts = new CancellationTokenSource();
            var show = _service.ShowAsync(P, cts.Token).AsTask();
            var run = StartInit();

            cts.Cancel();
            await CompleteInit(run);

            Assert.That(show.IsCompleted, Is.False, "the token is ignored once the ad is up");
            Assert.That(_state.IsFullscreenOpen, Is.True);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            _bridge.RaiseHidden(Unit);
            Assert.That(await show, Is.EqualTo(AdResult.Shown));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
            AssertAllClosed();
        }
    }
}
