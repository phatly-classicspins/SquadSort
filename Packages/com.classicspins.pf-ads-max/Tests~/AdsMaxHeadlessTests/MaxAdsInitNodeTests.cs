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
    /// <see cref="MaxAdsInitNode"/>: the replaced node's shape, the first-run outcomes, the visibility
    /// window around init, the init-once memo — and, on the real <see cref="BootFlow"/>, that a consent
    /// flow the player never finishes leaves <c>AdsReady</c> unresolved without any timeout firing.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdsInitNodeTests
    {
        private FakeMaxBridge _bridge;
        private RecordingPublisher _visibility;
        private RecordingLog _log;
        private DictConfig _config;
        private FakeDelay _delay;
        private MaxAdUnits _units;
        private MaxAdsInitNode _node;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge();
            _visibility = new RecordingPublisher();
            _log = new RecordingLog();
            _config = new DictConfig();
            _delay = new FakeDelay();
            _units = new MaxAdUnits(_bridge, _config, _log, MaxAdUnits.Android, _delay.Func);
            _node = new MaxAdsInitNode(_bridge, _units, _config, new MaxAdVisibility(_visibility, _log),
                new MaxAdReadiness(_bridge, _units, _config, new NullAdReadinessChangedPublisher(), _log), _log);
        }

        private MaxAdsInitNode NullBridgeNode()
        {
            var bridge = new NullMaxBridge(_log);
            var units = new MaxAdUnits(bridge, _config, _log, null, _delay.Func);
            return new MaxAdsInitNode(bridge, units, _config, new MaxAdVisibility(_visibility, _log),
                new MaxAdReadiness(bridge, units, _config, new NullAdReadinessChangedPublisher(), _log), _log);
        }

        /// <summary>An init that succeeds the way the real bridge does: initialized, then true.</summary>
        private void InitSucceeds() => _bridge.InitBehaviour = () => { _bridge.Initialized = true; return UniTask.FromResult(true); };

        private Task<BootNodeResult> Run(CancellationToken ct = default) => _node.RunAsync(null, ct).AsTask();

        // ── shape ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Keeps_the_replaced_nodes_Id_Provides_and_optionality_with_no_timeouts()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_node.Id, Is.EqualTo("AdsInit"));
                Assert.That(_node.Provides, Is.EqualTo(new[] { BootCaps.AdsReady }));
                Assert.That(_node.Required, Is.False, "a dead ads SDK never blocks boot");
                Assert.That(_node.Requires, Is.EquivalentTo(new[] { BootCaps.ConfigAvailable, BootCaps.ConsentResolved }));
                Assert.That(_node.SoftTimeout, Is.EqualTo(TimeSpan.Zero));
                Assert.That(_node.HardTimeout, Is.EqualTo(TimeSpan.Zero), "no timeout over the interactive consent step");
                Assert.That(_node.Predicate, Is.Null);
                Assert.That(_node.ExclusiveGroup, Is.Empty);
            });
        }

        [Test]
        public void Replace_accepts_the_node_for_AdsInit()
        {
            var ov = BootNodeOverride.Replace("AdsInit", _node);
            Assert.That(ov.TargetId, Is.EqualTo(_node.Id));
            Assert.That(ov.Replacement, Is.SameAs(_node));
        }

        [Test]
        public void Reconcile_accepts_the_node_in_place_of_the_framework_AdsInit()
        {
            // The framework's Replace guard (DW-41) runs in Reconcile, not in the factory: it holds the
            // replacement to the framework node's Id and exact Provides. Shaped like AdsInitNode.
            var framework = new StubBootNode { Id = "AdsInit", Provides = new[] { BootCaps.AdsReady } };

            var result = BootGraph.Reconcile(
                new IBootNode[] { framework }, new[] { BootNodeOverride.Replace("AdsInit", _node) });

            Assert.That(result, Is.EqualTo(new IBootNode[] { _node }));
        }

        // ── first run ──────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Init_true_is_Succeeded_with_the_visibility_pair_around_the_init()
        {
            var run = Run();
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true }), "visible before the init await");
            Assert.That(run.IsCompleted, Is.False);

            _bridge.PendingInit.TrySetResult(true);

            Assert.That(await run, Is.EqualTo(BootNodeResult.Succeeded));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_bridge.InitCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Init_false_is_Degraded_and_warns()
        {
            _bridge.InitBehaviour = () => UniTask.FromResult(false);

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max")), Is.True);
        }

        [Test]
        public async Task Init_throwing_is_Degraded_warns_and_still_publishes_false()
        {
            _bridge.InitBehaviour = () => throw new InvalidOperationException("sdk exploded");

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max") && w.Contains("sdk exploded")), Is.True,
                string.Join(" | ", _log.Warns));
        }

        [Test]
        public async Task Init_faulting_asynchronously_is_Degraded()
        {
            _bridge.InitBehaviour = () => UniTask.FromException<bool>(new InvalidOperationException("late fault"));

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task The_null_bridge_degrades_the_node_and_never_throws()
        {
            var node = NullBridgeNode();

            Assert.That(await node.RunAsync(null, CancellationToken.None).AsTask(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max")), Is.True);
        }

        // ── rerun / memo ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Two_runs_init_once_and_the_second_returns_the_memo_without_publishing()
        {
            _bridge.InitBehaviour = () => UniTask.FromResult(true);

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Succeeded));
            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Succeeded));

            Assert.That(_bridge.InitCount, Is.EqualTo(1), "SDK init — and MAX's consent flow — run at most once");
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }), "the rerun publishes nothing");
        }

        [Test]
        public async Task A_memoized_failure_stays_Degraded_on_rerun()
        {
            _bridge.InitBehaviour = () => UniTask.FromResult(false);

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_bridge.InitCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Cancelling_the_first_await_rethrows_and_leaves_the_memo_intact()
        {
            using var cts = new CancellationTokenSource();
            var first = Run(cts.Token);
            cts.Cancel();

            Assert.That(async () => await first, Throws.InstanceOf<OperationCanceledException>());

            var second = Run();
            Assert.That(second.IsCompleted, Is.False, "the second run awaits the same, still-running init");
            _bridge.PendingInit.TrySetResult(true);

            Assert.That(await second, Is.EqualTo(BootNodeResult.Succeeded));
            Assert.That(_bridge.InitCount, Is.EqualTo(1));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void An_already_cancelled_token_throws_before_init_starts()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(async () => await Run(cts.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(_bridge.InitCount, Is.Zero);
            Assert.That(_visibility.Published, Is.Empty);
        }

        // ── v2: dev config before init ─────────────────────────────────────────────────────────────────

        [Test]
        public async Task Dev_config_is_applied_before_InitializeAsync()
        {
            _config.Set(MaxAdUnits.VerboseLoggingKey, 1).Set(MaxAdUnits.TestDeviceIdsKey, " a, ,b ");
            InitSucceeds();

            Assert.That(await Run(), Is.EqualTo(BootNodeResult.Succeeded));

            Assert.That(_bridge.VerboseLoggingCalls, Is.EqualTo(new[] { true }));
            Assert.That(_bridge.TestDeviceIdCalls.Single(), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(_bridge.Calls.Take(3), Is.EqualTo(new[] { "SetVerboseLogging", "SetTestDeviceAdvertisingIds", "InitializeAsync" }));
        }

        [Test]
        public async Task Absent_dev_keys_make_no_bridge_call()
        {
            InitSucceeds();
            await Run();
            Assert.That(_bridge.VerboseLoggingCalls, Is.Empty);
            Assert.That(_bridge.TestDeviceIdCalls, Is.Empty);
        }

        [Test]
        public async Task Verbose_logging_0_turns_it_off_and_an_all_blank_id_list_makes_no_call()
        {
            _config.Set(MaxAdUnits.VerboseLoggingKey, 0).Set(MaxAdUnits.TestDeviceIdsKey, " , ");
            InitSucceeds();
            await Run();
            Assert.That(_bridge.VerboseLoggingCalls, Is.EqualTo(new[] { false }));
            Assert.That(_bridge.TestDeviceIdCalls, Is.Empty);
        }

        [Test]
        public async Task A_rerun_applies_dev_config_once()
        {
            _config.Set(MaxAdUnits.VerboseLoggingKey, 1);
            InitSucceeds();
            await Run();
            await Run();
            Assert.That(_bridge.VerboseLoggingCalls, Has.Count.EqualTo(1));
        }

        // ── v2: auto-load after init ───────────────────────────────────────────────────────────────────

        [Test]
        public async Task Init_ok_auto_loads_each_format_default_once_after_init()
        {
            _config.Set("ads.max.android.rewarded.unitId", "R").Set("ads.max.interstitial.unitId", "I");
            var run = Run();
            Assert.That(_bridge.LoadCalls, Is.Empty, "nothing loads before init resolves");

            _bridge.Initialized = true;
            _bridge.PendingInit.TrySetResult(true);
            Assert.That(await run, Is.EqualTo(BootNodeResult.Succeeded));
            await Run();

            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("R", true), ("I", false) }), "once each, across the rerun");
            Assert.That(_bridge.Calls.IndexOf("Load"), Is.GreaterThan(_bridge.Calls.IndexOf("InitializeAsync")));
        }

        [Test]
        public async Task Init_ok_also_loads_what_was_requested_before_init()
        {
            _config.Set("ads.max.p.unitId", "U");
            _units.Request("U", true);
            Assert.That(_bridge.LoadCalls, Is.Empty);

            InitSucceeds();
            await Run();

            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", true) }));
        }

        [Test]
        public async Task Init_false_loads_nothing()
        {
            _config.Set("ads.max.rewarded.unitId", "R");
            _bridge.InitBehaviour = () => UniTask.FromResult(false);
            await Run();
            Assert.That(_bridge.LoadCalls, Is.Empty);
        }

        [Test]
        public async Task Init_throwing_loads_nothing()
        {
            _config.Set("ads.max.rewarded.unitId", "R");
            _bridge.InitBehaviour = () => throw new InvalidOperationException("sdk exploded");
            await Run();
            Assert.That(_bridge.LoadCalls, Is.Empty);
        }

        // ── on the real BootFlow ───────────────────────────────────────────────────────────────────────

        private static StubBootNode Config() => new StubBootNode
            { Id = "ConfigCached", Required = true, Provides = new[] { BootCaps.ConfigAvailable } };

        private static StubBootNode Consent() => new StubBootNode
            { Id = "Consent", Provides = new[] { BootCaps.ConsentResolved } };

        [Test]
        public async Task A_consent_flow_that_never_completes_leaves_AdsReady_unresolved_and_blocks_nothing_else()
        {
            var independent = new StubBootNode { Id = "FirstScene", Required = true, Requires = new[] { BootCaps.ConfigAvailable } };
            var flow = new BootFlow(new IBootNode[] { Config(), Consent(), _node, independent }, maxConcurrency: 4);

            var run = flow.RunAsync(CancellationToken.None).AsTask();
            await TestSupport.SpinUntil(() => independent.Completed && _bridge.InitCount == 1);
            await Task.Delay(150); // long enough for any timeout that (wrongly) existed to have a go

            Assert.That(independent.Completed, Is.True, "nodes that do not need AdsReady finish");
            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Absent),
                "AdsReady is unresolved — not Failed, not Degraded by a timeout");
            Assert.That(run.IsCompleted, Is.False, "the player is still reading MAX's prompt");
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true }));

            _bridge.PendingInit.TrySetResult(true);
            await run;

            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Ok));
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public async Task RerunFrom_ConsentResolved_reuses_the_memo_and_re_emits_AdsReady()
        {
            _bridge.InitBehaviour = () => UniTask.FromResult(true);
            var consent = Consent();
            var flow = new BootFlow(new IBootNode[] { Config(), consent, _node }, maxConcurrency: 4);

            await flow.RunAsync(CancellationToken.None).AsTask();
            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Ok));

            await flow.RerunFromAsync(BootCaps.ConsentResolved, CancellationToken.None).AsTask();

            Assert.That(consent.RunCount, Is.EqualTo(2), "the consent provider reran");
            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Ok), "AdsReady re-emitted");
            Assert.That(_bridge.InitCount, Is.EqualTo(1), "one bridge init across the rerun");
            Assert.That(_visibility.Fullscreen, Is.EqualTo(new[] { true, false }), "the rerun publishes nothing");
        }

        [Test]
        public async Task A_SKU_gate_holds_the_replaced_AdsInit_until_its_game_is_ready_and_a_rerun_inits_once()
        {
            // Story 10.4: the module replaces AdsInit; the SKU gates it (no wrapper class). The gate
            // folds over the module's replacement, so MAX's init waits for the SKU, not the framework.
            InitSucceeds();
            var ftueDone = new BootCap("FtueDone");
            var frameworkAdsInit = new StubBootNode
            {
                Id = "AdsInit",
                Requires = new[] { BootCaps.ConfigAvailable, BootCaps.ConsentResolved },
                Provides = new[] { BootCaps.AdsReady },
            };
            var ftue = new StubBootNode { Id = "Ftue", Provides = new[] { ftueDone } };
            var gameReady = false;

            var nodes = BootGraph.Reconcile(
                new IBootNode[] { Config(), Consent(), ftue, frameworkAdsInit },
                new[]
                {
                    BootNodeOverride.Replace("AdsInit", _node),
                    BootNodeOverride.Gate("AdsInit", _ => gameReady, ftueDone),
                },
                _log);
            var flow = new BootFlow(nodes, maxConcurrency: 4);

            await flow.RunAsync(CancellationToken.None).AsTask();

            Assert.That(_bridge.InitCount, Is.EqualTo(0), "the gate holds MAX's init through boot");
            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Skipped),
                "a gated skip still emits AdsReady");

            gameReady = true;
            await flow.RerunFromAsync(BootCaps.AdsReady, CancellationToken.None).AsTask();

            Assert.That(_bridge.InitCount, Is.EqualTo(1), "the rerun reopens the gate and inits exactly once");
            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Ok));
        }

        [Test]
        public async Task A_null_bridge_node_in_a_real_flow_still_emits_AdsReady_Degraded()
        {
            var node = NullBridgeNode();
            var flow = new BootFlow(new IBootNode[] { Config(), Consent(), node }, maxConcurrency: 4);

            await flow.RunAsync(CancellationToken.None).AsTask();

            Assert.That(flow.Status.StatusOf(BootCaps.AdsReady), Is.EqualTo(CapStatus.Degraded));
        }
    }
}
