using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// Story 10.10 (FR54, AD-MOD-13) — every row of the I/O matrix for the module's two
    /// <see cref="IDevTool"/>s, run once per tool: available only once initialized and while
    /// <c>ads.max.devTools</c> is not <c>0</c>, both read at call time; <see cref="IDevTool.Open"/> calls its
    /// own bridge method exactly once or refuses with one warning; nothing throws. The framework-only row
    /// (an empty collection) is <c>DevToolCollectionTests</c> under the Unity runner.
    /// </summary>
    [TestFixture]
    public sealed class MaxDevToolsTests
    {
        public enum Tool { Mediation, Creative }

        private static readonly Tool[] Tools = { Tool.Mediation, Tool.Creative };

        private FakeMaxBridge _bridge;
        private DictConfig _config;
        private RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge();
            _config = new DictConfig();
            _log = new RecordingLog();
        }

        private static IDevTool Build(Tool tool, IMaxBridge bridge, IGameConfig config, ILog log)
            => tool == Tool.Mediation
                ? (IDevTool)new MaxMediationDebuggerTool(bridge, config, log)
                : new MaxCreativeDebuggerTool(bridge, config, log);

        private IDevTool Build(Tool tool) => Build(tool, _bridge, _config, _log);

        private int OwnCalls(Tool tool) => tool == Tool.Mediation ? _bridge.MediationDebuggerCalls : _bridge.CreativeDebuggerCalls;
        private int OtherCalls(Tool tool) => tool == Tool.Mediation ? _bridge.CreativeDebuggerCalls : _bridge.MediationDebuggerCalls;
        private static string OwnCallName(Tool tool) => tool == Tool.Mediation ? "ShowMediationDebugger" : "ShowCreativeDebugger";

        private int ShowCalls => _bridge.MediationDebuggerCalls + _bridge.CreativeDebuggerCalls;

        private void AssertOneWarningNaming(string reason)
        {
            Assert.That(_log.Warns, Has.Count.EqualTo(1), string.Join(" | ", _log.Warns));
            Assert.That(_log.Warns[0], Does.Contain("pf-ads-max"));
            Assert.That(_log.Warns[0], Does.Contain(reason));
        }

        // ── identity ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Ids_and_the_switch_key_are_the_named_constants()
        {
            Assert.That(MaxDevTool.MediationDebuggerId, Is.EqualTo("ads-max.mediation-debugger"));
            Assert.That(MaxDevTool.CreativeDebuggerId, Is.EqualTo("ads-max.creative-debugger"));
            Assert.That(MaxDevTool.SwitchKey, Is.EqualTo("ads.max.devTools"));
            Assert.That(Build(Tool.Mediation).Id, Is.EqualTo(MaxDevTool.MediationDebuggerId));
            Assert.That(Build(Tool.Creative).Id, Is.EqualTo(MaxDevTool.CreativeDebuggerId));
        }

        [Test]
        public void The_two_tools_have_distinct_ids_and_distinct_types()
        {
            var tools = Tools.Select(Build).ToArray();
            Assert.That(tools.Select(t => t.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(tools.Select(t => t.GetType()).Distinct().Count(), Is.EqualTo(2),
                "one implementation type per tool — two Singletons sharing one type conflict in VContainer (DW-35)");
            Assert.That(tools.All(t => t.Id.StartsWith("ads-max.", StringComparison.Ordinal)), Is.True, "<module-slug>.<tool>");
        }

        [TestCaseSource(nameof(Tools))]
        public void Construction_reads_neither_config_nor_the_initialized_state(Tool tool)
        {
            var config = new CountingConfig(_config);
            Build(tool, _bridge, config, _log);

            Assert.That(config.Reads, Is.Zero, "config arrives only with ConfigAvailable — never read at construction");
            Assert.That(_bridge.IsInitializedReads, Is.Zero);
            Assert.That(_log.Warns, Is.Empty);
        }

        // ── before init ────────────────────────────────────────────────────────────────────────────────

        [TestCaseSource(nameof(Tools))]
        public void Before_init_it_is_unavailable_and_Open_refuses_with_one_warning_per_call(Tool tool)
        {
            var t = Build(tool);

            Assert.That(t.IsAvailable, Is.False);
            Assert.That(_log.Warns, Is.Empty, "an unavailable read is not a fault");

            Assert.That(t.Open(), Is.False);
            AssertOneWarningNaming("not initialized");
            Assert.That(_log.Warns[0], Does.Contain(t.Id));

            Assert.That(t.Open(), Is.False);
            Assert.That(_log.Warns, Has.Count.EqualTo(2), "one warning per Open");
            Assert.That(ShowCalls, Is.Zero, "no debugger call before init");
        }

        // ── switched off ───────────────────────────────────────────────────────────────────────────────

        [TestCaseSource(nameof(Tools))]
        public void Switched_off_it_is_unavailable_and_Open_never_reaches_the_bridge(Tool tool)
        {
            _bridge.Initialized = true;
            _config.Set(MaxDevTool.SwitchKey, 0);
            var t = Build(tool);

            Assert.That(t.IsAvailable, Is.False);
            Assert.That(t.Open(), Is.False);
            AssertOneWarningNaming(MaxDevTool.SwitchKey);
            Assert.That(ShowCalls, Is.Zero);
        }

        [TestCaseSource(nameof(Tools))]
        public void A_string_zero_switches_it_off_too(Tool tool)
        {
            _bridge.Initialized = true;
            _config.Set(MaxDevTool.SwitchKey, "0");
            var t = Build(tool);

            Assert.That(t.IsAvailable, Is.False);
            Assert.That(t.Open(), Is.False);
            Assert.That(ShowCalls, Is.Zero);
        }

        // ── call-time read ─────────────────────────────────────────────────────────────────────────────

        [TestCaseSource(nameof(Tools))]
        public void The_switch_and_the_initialized_state_are_read_at_call_time_absent_after_off(Tool tool)
        {
            _config.Set(MaxDevTool.SwitchKey, 0);
            var t = Build(tool);
            Assert.That(t.IsAvailable, Is.False);

            _bridge.Initialized = true;
            _config.Remove(MaxDevTool.SwitchKey);

            Assert.That(t.IsAvailable, Is.True);
            Assert.That(t.Open(), Is.True);
            Assert.That(OwnCalls(tool), Is.EqualTo(1));
        }

        [TestCaseSource(nameof(Tools))]
        public void The_switch_is_read_at_call_time_one_after_off(Tool tool)
        {
            _config.Set(MaxDevTool.SwitchKey, 0);
            var t = Build(tool);
            Assert.That(t.Open(), Is.False);

            _bridge.Initialized = true;
            _config.Set(MaxDevTool.SwitchKey, 1);

            Assert.That(t.IsAvailable, Is.True);
            Assert.That(t.Open(), Is.True);
            Assert.That(OwnCalls(tool), Is.EqualTo(1));
        }

        [TestCaseSource(nameof(Tools))]
        public void Every_IsAvailable_and_Open_reads_the_switch_again(Tool tool)
        {
            _bridge.Initialized = true;
            var config = new CountingConfig(_config);
            var t = Build(tool, _bridge, config, _log);

            _ = t.IsAvailable;
            _ = t.IsAvailable;
            t.Open();

            Assert.That(config.Reads, Is.EqualTo(3), "never cached");
        }

        // ── happy path ─────────────────────────────────────────────────────────────────────────────────

        [TestCaseSource(nameof(Tools))]
        public void Available_Open_calls_its_own_bridge_method_exactly_once_and_returns_true(Tool tool)
        {
            _bridge.Initialized = true;
            var t = Build(tool);

            Assert.That(t.IsAvailable, Is.True);
            Assert.That(t.Open(), Is.True);

            Assert.That(OwnCalls(tool), Is.EqualTo(1));
            Assert.That(OtherCalls(tool), Is.Zero, "never the other tool's debugger");
            Assert.That(_bridge.Calls, Is.EqualTo(new[] { OwnCallName(tool) }));
            Assert.That(_log.Warns, Is.Empty);
        }

        // ── faults ─────────────────────────────────────────────────────────────────────────────────────

        [TestCaseSource(nameof(Tools))]
        public void A_throwing_config_reads_as_off_with_a_warning_and_never_throws(Tool tool)
        {
            _bridge.Initialized = true;
            var t = Build(tool, _bridge, new ThrowingConfig(), _log);

            bool available = true, opened = true;
            Assert.DoesNotThrow(() => available = t.IsAvailable);
            Assert.That(available, Is.False);
            AssertOneWarningNaming(MaxDevTool.SwitchKey);

            _log.Warns.Clear();
            Assert.DoesNotThrow(() => opened = t.Open());
            Assert.That(opened, Is.False);
            AssertOneWarningNaming("threw");
            Assert.That(ShowCalls, Is.Zero, "a broken config layer never puts SDK UI on screen");
        }

        [TestCaseSource(nameof(Tools))]
        public void A_throwing_initialized_read_reads_as_unavailable_with_a_warning_and_never_throws(Tool tool)
        {
            _bridge.Initialized = true;
            _bridge.ThrowOnIsInitialized = true;
            var t = Build(tool);

            bool available = true, opened = true;
            Assert.DoesNotThrow(() => available = t.IsAvailable);
            Assert.That(available, Is.False);
            AssertOneWarningNaming("exploded");

            _log.Warns.Clear();
            Assert.DoesNotThrow(() => opened = t.Open());
            Assert.That(opened, Is.False);
            AssertOneWarningNaming("exploded");
            Assert.That(ShowCalls, Is.Zero, "a faulting SDK state never opens a debugger");
        }

        [TestCaseSource(nameof(Tools))]
        public void A_throwing_bridge_call_is_a_warning_and_false_never_a_throw(Tool tool)
        {
            _bridge.Initialized = true;
            _bridge.ThrowOnDebugger = true;
            var t = Build(tool);

            var opened = true;
            Assert.DoesNotThrow(() => opened = t.Open());
            Assert.That(opened, Is.False);
            Assert.That(OwnCalls(tool), Is.EqualTo(1));
            AssertOneWarningNaming("exploded");
        }
    }
}
