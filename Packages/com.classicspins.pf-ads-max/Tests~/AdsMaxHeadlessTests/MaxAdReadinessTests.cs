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
    /// Story 10.7 (AD-MOD-11, FR47) — the readiness push, against one bridge, one <see cref="MaxAdUnits"/>,
    /// one <see cref="MaxAdReadiness"/>, the <see cref="MaxAdsService"/> and the <see cref="MaxAdsInitNode"/>
    /// over one recording publisher. The format-default unit is <c>ads.max.rewarded.unitId = "d"</c>, and
    /// <c>ads.a.rewarded = ads.b.rewarded = 1</c>. Every row of the story's I/O matrix but the Fake row (that
    /// one runs under the Unity runner, through <c>AdsInstaller</c>).
    /// </summary>
    [TestFixture]
    public sealed class MaxAdReadinessTests
    {
        private static readonly AdPlacement A = new AdPlacement("a");
        private static readonly AdPlacement B = new AdPlacement("b");
        private const string D = "d";

        private FakeMaxBridge _bridge;
        private DictConfig _config;
        private RecordingLog _log;
        private FakeDelay _delay;
        private RecordingReadinessPublisher _published;
        private MaxAdUnits _units;
        private MaxAdVisibility _visibility;
        private MaxAdReadiness _readiness;
        private MaxAdsService _service;
        private MaxAdsInitNode _node;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = false, Ready = false };
            _config = new DictConfig().Set("ads.max.rewarded.unitId", D).Set("ads.a.rewarded", 1).Set("ads.b.rewarded", 1);
            _log = new RecordingLog();
            _delay = new FakeDelay();
            _published = new RecordingReadinessPublisher();
            Build(_published);
        }

        private void Build(IAdReadinessChangedPublisher publisher)
        {
            _units = new MaxAdUnits(_bridge, _config, _log, null, _delay.Func);
            _visibility = new MaxAdVisibility(new RecordingPublisher(), _log);
            _readiness = new MaxAdReadiness(_bridge, _units, _config, publisher, _log);
            _service = new MaxAdsService(_bridge, _units, _visibility, _readiness, new RecordingAnalytics(), _config, _log, _delay.Func);
            _node = new MaxAdsInitNode(_bridge, _units, _config, _visibility, _readiness, _log);
        }

        private Task<bool> IsReady(AdPlacement placement) => _service.IsReadyAsync(placement).AsTask();

        private Task<BootNodeResult> RunInit(bool succeeds)
        {
            _bridge.InitBehaviour = () =>
            {
                _bridge.Initialized = succeeds;
                return UniTask.FromResult(succeeds);
            };
            return _node.RunAsync(null, CancellationToken.None).AsTask();
        }

        /// <summary>Initialized, with <c>a</c> and <c>b</c> both bound to <c>d</c> (baselines false) and nothing published.</summary>
        private void BothBoundInitialized()
        {
            _bridge.Initialized = true;
            _service.Preload(A);
            _service.Preload(B);
            Assert.That(_published.Published, Is.Empty, "a baseline is never published");
        }

        private void AssertWarn(string fragment)
            => Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max") && w.Contains(fragment)), Is.True, string.Join(" | ", _log.Warns));

        // ── matrix: pre-init query ─────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_placement_queried_before_init_is_settled_at_init_completion_then_pushed_on_load()
        {
            Assert.That(await IsReady(A), Is.False);
            Assert.That(_published.Published, Is.Empty, "the pull never publishes");
            Assert.That(_units.IsRequested(D), Is.True, "d is requested before init");
            Assert.That(_bridge.LoadCalls, Is.Empty, "recorded, not loaded, before init");
            Assert.That(_bridge.IsReadyCalls, Is.Empty, "no bridge ready read before init");

            Assert.That(await RunInit(succeeds: true), Is.EqualTo(BootNodeResult.Succeeded));
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }), "init completion publishes the settled value");
            Assert.That(_bridge.LoadCalls, Does.Contain((D, true)), "d is loaded at auto-load");

            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false), ("a", true) }));
        }

        [Test]
        public async Task A_placement_shown_before_init_is_refused_bound_and_settled_at_init_completion()
        {
            Assert.That(await _service.ShowAsync(A, CancellationToken.None).AsTask(), Is.EqualTo(AdResult.Failed));
            Assert.That(_units.IsRequested(D), Is.True, "d is requested before init");
            Assert.That(_published.Published, Is.Empty);

            await RunInit(succeeds: true);
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }));
            Assert.That(_bridge.LoadCalls, Does.Contain((D, true)));
        }

        // ── matrix: failed init ────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_failed_init_publishes_false_once_and_a_rerun_publishes_nothing()
        {
            _service.Preload(A);

            Assert.That(await RunInit(succeeds: false), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }));

            await _node.RunAsync(null, CancellationToken.None).AsTask();
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }), "the settle sits inside the init-once memo");
        }

        [Test]
        public async Task Init_completion_is_forced_for_every_tracked_placement_whatever_its_last_known_value()
        {
            _bridge.Ready = true;
            _service.Preload(A);                 // pre-init: baseline false
            var none = new AdPlacement("none");  // interstitial, and no interstitial unit anywhere
            _service.Preload(none);

            await RunInit(succeeds: true);

            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", true), ("none", false) }),
                "every placement receives its settled value, a unit-less one false");
        }

        // ── matrix: shared unit fan-out ────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_transition_of_a_shared_unit_reaches_every_placement_bound_to_it_in_first_bind_order()
        {
            BothBoundInitialized();

            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", true), ("b", true) }), "load");
            _published.Published.Clear();

            var show = _service.ShowAsync(A, CancellationToken.None).AsTask();
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false), ("b", false) }), "show-start: the unit is on screen");
            _published.Published.Clear();

            _bridge.RaiseHidden(D);
            await show;
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", true), ("b", true) }), "show-end, still loaded");
            _published.Published.Clear();

            _bridge.Ready = false;
            _bridge.RaiseLoadFailed(D, "no fill");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false), ("b", false) }), "load-fail");
        }

        // ── matrix: no change, no publish ──────────────────────────────────────────────────────────────

        [Test]
        public void An_event_that_leaves_the_value_unchanged_publishes_nothing()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();

            _bridge.RaiseLoaded(D);

            Assert.That(_published.Published, Is.Empty);
        }

        // ── matrix: baseline never published ──────────────────────────────────────────────────────────

        [Test]
        public async Task Binding_to_an_already_loaded_unit_publishes_nothing_and_the_pull_answers_true()
        {
            _bridge.Initialized = true;
            _bridge.Ready = true;
            _units.Request(D, true);

            _service.Preload(A);

            Assert.That(_published.Published, Is.Empty, "the baseline (true) is never published");
            Assert.That(await IsReady(A), Is.True);
            Assert.That(_published.Published, Is.Empty);
        }

        // ── matrix: rebind on config change ────────────────────────────────────────────────────────────

        [Test]
        public void A_config_change_rebinds_only_the_placement_whose_unit_moved()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();
            _bridge.LoadCalls.Clear();

            _config.Set("ads.max.a.unitId", "x");
            _config.RaiseChanged();

            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }), "x is unrequested: evaluated before its request");
            Assert.That(_units.IsRequested("x"), Is.True, "x is requested");
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("x", true) }));
            _published.Published.Clear();

            _bridge.RaiseLoaded("x");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", true) }), "a later load of x reaches a");
            _published.Published.Clear();

            _bridge.Ready = false;
            _bridge.RaiseLoadFailed(D, "no fill");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("b", false) }), "an event on d no longer reaches a");
        }

        [Test]
        public async Task A_pull_that_resolves_a_different_unit_rebinds_silently()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();

            _config.Set("ads.max.a.unitId", "x"); // no OnChanged: only the pull sees it
            Assert.That(await IsReady(A), Is.False, "x is unrequested at the pull");
            Assert.That(_published.Published, Is.Empty, "the pull never publishes");
            Assert.That(_units.IsRequested("x"), Is.True);

            _bridge.Ready = false;
            _bridge.RaiseLoadFailed(D, "no fill");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("b", false) }), "a is now bound to x");

            _bridge.Ready = true;
            _bridge.RaiseLoaded("x");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("b", false), ("a", true) }),
                "the pull recorded false for a, so the load of x is pushed");
        }

        [Test]
        public async Task A_pull_that_reads_false_under_a_true_last_known_value_is_pushed_true_on_the_next_load()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();

            _bridge.Ready = false; // the ad expired with no callback
            Assert.That(await IsReady(A), Is.False);
            Assert.That(_published.Published, Is.Empty, "the pull never publishes");

            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", true) }),
                "a's pull recorded false; b was never told otherwise, so only a is pushed");
        }

        [Test]
        public void A_config_change_that_leaves_a_placement_with_no_unit_publishes_false_for_it_only()
        {
            _config.Set("ads.max.b.unitId", D);
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();

            _config.Set("ads.max.rewarded.unitId", null); // a has no unit on any tier; b keeps its own key
            _config.RaiseChanged();
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false) }));
            _published.Published.Clear();

            _bridge.Ready = false;
            _bridge.RaiseLoadFailed(D, "no fill");
            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("b", false) }), "a is no longer bound to d");
        }

        // ── matrix: pull while showing ─────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_unit_being_shown_reads_not_ready_to_the_pull()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);

            var show = _service.ShowAsync(A, CancellationToken.None).AsTask();
            Assert.That(await IsReady(B), Is.False, "the showing unit is not ready");

            _bridge.RaiseHidden(D);
            await show;
            Assert.That(await IsReady(B), Is.True);
        }

        [Test]
        public async Task A_show_whose_bridge_throws_still_unmarks_the_unit()
        {
            BothBoundInitialized();
            _bridge.Ready = true;
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();
            _bridge.ThrowOnShow = true;

            Assert.That(await _service.ShowAsync(A, CancellationToken.None).AsTask(), Is.EqualTo(AdResult.Failed));

            Assert.That(_published.Pairs, Is.EqualTo(new[] { ("a", false), ("b", false), ("a", true), ("b", true) }));
            Assert.That(await IsReady(B), Is.True, "unmarked in the outermost finally");
        }

        // ── matrix: refused show ───────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_refused_show_publishes_nothing_and_marks_no_unit_showing()
        {
            _bridge.Initialized = true;
            _bridge.Ready = true;
            _service.Preload(B);
            _bridge.RaiseLoaded(D);
            _published.Published.Clear();
            _bridge.IsReadyCalls.Clear();

            var consent = _visibility.Open(MaxAdSurface.ConsentFlow);
            Assert.That(await _service.ShowAsync(A, CancellationToken.None).AsTask(), Is.EqualTo(AdResult.Failed));

            Assert.That(_published.Published, Is.Empty, "nothing is published");
            Assert.That(_bridge.IsReadyCalls, Is.Empty, "the refusal precedes the bind: no bridge read");
            Assert.That(_readiness.Ready(D), Is.True, "no unit is marked showing");
            consent.Close();
        }

        // ── matrix: publisher throws ───────────────────────────────────────────────────────────────────

        [Test]
        public void A_throwing_publisher_is_a_warning_and_the_value_is_still_recorded()
        {
            var publisher = new ThrowingReadinessPublisher();
            Build(publisher);
            _bridge.Initialized = true;
            _service.Preload(A);
            _bridge.Ready = true;

            Assert.DoesNotThrow(() => _bridge.RaiseLoaded(D));
            Assert.That(publisher.Attempts, Is.EqualTo(1));
            AssertWarn("publish threw");

            _bridge.RaiseLoaded(D);
            Assert.That(publisher.Attempts, Is.EqualTo(1), "a later identical value publishes nothing");
        }

        // ── ready(unit) ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Ready_asks_the_bridge_only_once_initialized_and_requested_with_the_requested_format()
        {
            _bridge.Ready = true;
            Assert.That(_readiness.Ready(D), Is.False, "not initialized");
            _bridge.Initialized = true;
            Assert.That(_readiness.Ready(D), Is.False, "not requested");
            Assert.That(_readiness.Ready(null), Is.False);
            Assert.That(_bridge.IsReadyCalls, Is.Empty, "short-circuited before the bridge");

            _units.Request(D, true);
            Assert.That(_readiness.Ready(D), Is.True);
            Assert.That(_bridge.IsReadyCalls, Is.EqualTo(new[] { (D, true) }), "the format MaxAdUnits recorded");
        }

        [Test]
        public void A_throwing_bridge_reads_not_ready_with_a_warning()
        {
            _bridge.Initialized = true;
            _units.Request(D, true);
            _bridge.ThrowOnIsReady = true;

            Assert.That(_readiness.Ready(D), Is.False);
            AssertWarn("threw");
        }

        // ── plumbing ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_config_whose_change_stream_throws_is_a_warning_and_binding_still_works()
        {
            var config = new ChangeStreamThrowsConfig(_config);
            var units = new MaxAdUnits(_bridge, config, _log, null, _delay.Func);
            var readiness = new MaxAdReadiness(_bridge, units, config, _published, _log);

            Assert.That(readiness.Bind(A), Is.EqualTo(D));
            AssertWarn("config changes");
            Assert.That(units.IsRequested(D), Is.True);
        }

        [Test]
        public void After_dispose_a_load_publishes_nothing()
        {
            BothBoundInitialized();
            _readiness.Dispose();
            _bridge.Ready = true;

            _bridge.RaiseLoaded(D);
            _config.RaiseChanged();

            Assert.That(_published.Published, Is.Empty);
        }

        [Test]
        public void The_constructor_guards_every_argument()
        {
            var pub = new RecordingReadinessPublisher();
            Assert.Throws<ArgumentNullException>(() => new MaxAdReadiness(null, _units, _config, pub, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdReadiness(_bridge, null, _config, pub, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdReadiness(_bridge, _units, null, pub, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdReadiness(_bridge, _units, _config, null, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdReadiness(_bridge, _units, _config, pub, null));
        }

        /// <summary>Reads through to an inner config; only its change stream throws.</summary>
        private sealed class ChangeStreamThrowsConfig : IGameConfig
        {
            private readonly IGameConfig _inner;
            public ChangeStreamThrowsConfig(IGameConfig inner) { _inner = inner; }
            public T Get<T>(ConfigKey<T> key) => _inner.Get(key);
            public UniTask RefreshRemoteAsync(CancellationToken ct) => UniTask.CompletedTask;
            public IObservable<Unit> OnChanged => throw new InvalidOperationException("change stream exploded");
        }
    }
}
