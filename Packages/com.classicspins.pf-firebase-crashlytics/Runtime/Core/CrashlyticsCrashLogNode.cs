using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>
    /// Runs in place of the framework's <c>CrashLog</c> node (<see cref="BootNodeOverride.Replace"/>). Same <see cref="Id"/>,
    /// <see cref="Requires"/>, <see cref="Provides"/>, <see cref="Required"/> and no timeouts, so the graph, the boot report
    /// and every node that waits on <see cref="BootCaps.LoggingReady"/> see no difference.
    /// </summary>
    /// <remarks>
    /// <para><b>Keeps the framework's hooks.</b> <see cref="ICrashHooks"/> installs the same unhandled-exception surfaces the
    /// original node hooks, so replacing it loses nothing.</para>
    /// <para><b>Never holds boot.</b> Firebase checks its native dependencies asynchronously; the start is fired and the node
    /// succeeds at once. Crashlytics is not something boot has to await or can fail on, so a Crashlytics problem (no SDK, a
    /// missing config, a throw from the bridge) is logged and boot goes on.</para>
    /// <para><b>Not consent-gated.</b> Crash reports are kept for stability (D-114); analytics stays behind the consent fan-out.</para>
    /// </remarks>
    public sealed class CrashlyticsCrashLogNode : IBootNode
    {
        private static readonly IReadOnlyList<BootCap> None = Array.Empty<BootCap>();
        private static readonly IReadOnlyList<BootCap> LoggingReady = new[] { BootCaps.LoggingReady };

        private readonly ICrashlyticsBridge _bridge;
        private readonly ICrashHooks _hooks;
        private readonly ILog _log;
        private bool _ran;

        public CrashlyticsCrashLogNode(ICrashlyticsBridge bridge, ICrashHooks hooks, ILog log)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Id => CrashlyticsModule.ReplacedNodeId;
        public IReadOnlyList<BootCap> Requires => None;
        public IReadOnlyList<BootCap> Provides => LoggingReady;
        public bool Required => true;

        // Crash logging is never timed out (same as the framework node).
        public TimeSpan SoftTimeout => TimeSpan.Zero;
        public TimeSpan HardTimeout => TimeSpan.Zero;

        public Func<BootContext, bool> Predicate => null;
        public string ExclusiveGroup => "";
        public float Weight => 1f;

        public UniTask<BootNodeResult> RunAsync(BootContext ctx, CancellationToken ct)
        {
            if (_ran) return UniTask.FromResult(BootNodeResult.Succeeded);
            _ran = true;

            _hooks.Install();
            try
            {
                _bridge.Start();
            }
            catch (Exception e)
            {
                _log.Error($"[{CrashlyticsModule.ModuleId}] Crashlytics start threw; boot continues without it: {e}");
            }
            return UniTask.FromResult(BootNodeResult.Succeeded);
        }
    }
}
