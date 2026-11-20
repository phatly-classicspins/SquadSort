using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The MAX replacement for the framework's <c>AdsInit</c> boot node, installed through
    /// <c>BootNodeOverride.Replace("AdsInit", …)</c>. SDK init must be awaited and can fail
    /// meaningfully, so it is tier 2 by the three-tier init law — but it replaces the framework node
    /// rather than adding one (a module adds no boot node of its own).
    /// </summary>
    /// <remarks>
    /// <para><b><see cref="Id"/> and <see cref="Provides"/> are identical to the node replaced</b>, and
    /// <see cref="Required"/> is false: a dead ads SDK never blocks boot and never raises an error
    /// screen. Until the kernel's <c>Replace</c> guard enforces this, the module's own headless tests
    /// hold the shape.</para>
    /// <para><b>Consent is MAX's (AD-MOD-9).</b> MAX's Terms and Privacy Policy flow runs inside
    /// <see cref="IMaxBridge.InitializeAsync"/>. This node reads no consent purpose, passes nothing to
    /// the SDK and writes nothing back. <see cref="Requires"/> still asserts <c>ConsentResolved</c> so
    /// MAX's prompt follows the SKU's own agree gate rather than racing it.</para>
    /// <para><b>No timeout over the interactive step.</b> Both timeouts are zero: the init await may be
    /// a player reading a privacy prompt, and a hard timeout would cancel them mid-read. The node awaits
    /// no other SDK work (loads are fire-and-forget), so there is nothing non-interactive to bound.</para>
    /// <para><b>Visibility window (AD-MOD-12).</b> MAX signals only on completion, so the node opens its
    /// <see cref="MaxAdSurface.ConsentFlow"/> slot in the module's one <see cref="MaxAdVisibility"/> before
    /// the init await and closes it in its <c>finally</c> — a superset of the time the flow is actually on
    /// screen. The node never publishes <see cref="AdVisibilityChanged"/> itself: the state derives it, so
    /// the window closing while a show is still up releases nothing. The slot opens unconditionally — SDK
    /// init is never refused, since the init is memoized and a refusal would disable ads for the
    /// process.</para>
    /// <para><b>Rerun-idempotent.</b> SDK init (and therefore the consent flow) runs at most once per
    /// process: the first run starts it and memoizes its outcome in a completion source; every later run — a second
    /// <c>RunAsync</c>, a <c>RerunFromAsync(ConsentResolved)</c> — awaits the same memo, re-emits
    /// <c>AdsReady</c> with the memoized outcome and publishes nothing. Cancelling a run's token cancels
    /// only that run's await (the <see cref="OperationCanceledException"/> is rethrown); the memo keeps
    /// running. Memoized in memory, never in user data.</para>
    /// <para><b>Dev config before init, auto-load after it.</b> Before <see cref="IMaxBridge.InitializeAsync"/>
    /// the node applies <c>ads.max.verboseLogging</c> (int, non-zero ⇒ on) and <c>ads.max.testDeviceIds</c>
    /// (comma-separated, trimmed, empties dropped) — each only when present, so an absent key never calls the
    /// bridge. After a successful init it starts <see cref="MaxAdUnits.StartAutoLoad"/>: the format defaults
    /// and every unit requested before init begin loading. A failed init loads nothing. Both run inside the
    /// init-once memo, so a rerun repeats neither.</para>
    /// <para><b>Readiness settles at init completion (AD-MOD-11).</b> In the init-once <c>finally</c>, after
    /// the slot closes and before the memo resolves, the node tells the module's one
    /// <see cref="MaxAdReadiness"/> that init completed: every bound placement — including one bound before
    /// init — receives its settled <c>AdReadinessChanged</c>, <c>false</c> after a failed init. It sits inside
    /// the memo, so a rerun never publishes it again.</para>
    /// </remarks>
    public sealed class MaxAdsInitNode : IBootNode
    {
        private const string Module = "[pf-ads-max] ";

        private readonly IReadOnlyList<BootCap> _requires = new[]
        {
            BootCaps.ConfigAvailable,
            BootCaps.ConsentResolved,
        };
        private readonly IReadOnlyList<BootCap> _provides = new[] { BootCaps.AdsReady };

        private readonly IMaxBridge _max;
        private readonly MaxAdUnits _units;
        private readonly IGameConfig _config;
        private readonly MaxAdVisibility _visibility;
        private readonly MaxAdReadiness _readiness;
        private readonly ILog _log;

        private readonly object _gate = new object();
        private UniTaskCompletionSource<bool> _memo;

        // IGameConfig is read only inside RunAsync, whose Requires asserts ConfigAvailable (the race rule).
        public MaxAdsInitNode(
            IMaxBridge max,
            MaxAdUnits units,
            IGameConfig config,
            MaxAdVisibility visibility,
            MaxAdReadiness readiness,
            ILog log)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Id => "AdsInit";
        public IReadOnlyList<BootCap> Requires => _requires;
        public IReadOnlyList<BootCap> Provides => _provides;
        public bool Required => false;
        public TimeSpan SoftTimeout => TimeSpan.Zero;
        public TimeSpan HardTimeout => TimeSpan.Zero;
        public Func<BootContext, bool> Predicate => null;
        public string ExclusiveGroup => "";
        public float Weight => 1f;

        public async UniTask<BootNodeResult> RunAsync(BootContext ctx, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            UniTaskCompletionSource<bool> memo;
            var start = false;
            lock (_gate)
            {
                if (_memo == null)
                {
                    // A completion source, not a Preserve()d async-method task: Preserve() memoizes the
                    // RESULT, but until completion it forwards to a promise that accepts exactly one
                    // continuation — so a first run whose await was cancelled would leave a second awaiter
                    // throwing "already continuation registered". A completion source takes any number.
                    _memo = new UniTaskCompletionSource<bool>();
                    start = true;
                }
                memo = _memo;
            }
            if (start) InitOnceAsync(memo).Forget();

            bool ok;
            try
            {
                ok = await memo.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                throw; // this run's token only — the memo is untouched and keeps running
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "AdsInit failed — AdsReady degraded (still emitted): " + ex.Message);
                return BootNodeResult.Degraded;
            }

            return ok ? BootNodeResult.Succeeded : BootNodeResult.Degraded;
        }

        private async UniTaskVoid InitOnceAsync(UniTaskCompletionSource<bool> memo)
        {
            var ok = false;
            var slot = _visibility.Open(MaxAdSurface.ConsentFlow);
            try
            {
                ApplyDevConfig();
                ok = await _max.InitializeAsync();
                if (ok)
                {
                    _log.Info(Module + "AppLovin MAX initialized.");
                    StartAutoLoad();
                }
                else _log.Warn(Module + "AppLovin MAX did not initialize — AdsReady degraded (still emitted).");
            }
            catch (Exception ex)
            {
                ok = false;
                _log.Warn(Module + "AppLovin MAX init threw — AdsReady degraded (still emitted): " + ex.Message);
            }
            finally
            {
                // Close the slot first, so every awaiter resumes after the window has closed. Then settle
                // readiness — the one forced publish (AD-MOD-11) — before any awaiter resumes.
                slot.Close();
                try { _readiness.InitCompleted(); }
                catch (Exception ex) { _log.Warn(Module + "readiness settle threw: " + ex.Message); }
                memo.TrySetResult(ok);
            }
        }

        /// <summary>The dev knobs, applied once before SDK init; an absent key makes no bridge call and a
        /// failing one is a warning, never a failed init.</summary>
        private void ApplyDevConfig()
        {
            try
            {
                int verbose;
                if (MaxAdUnits.TryReadInt(_config, MaxAdUnits.VerboseLoggingKey, out verbose))
                {
                    _max.SetVerboseLogging(verbose != 0);
                    _log.Info(Module + "verbose logging " + (verbose != 0 ? "on" : "off") + " (" + MaxAdUnits.VerboseLoggingKey + ").");
                }
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "applying " + MaxAdUnits.VerboseLoggingKey + " threw: " + ex.Message);
            }

            try
            {
                var ids = MaxAdUnits.SplitList(_config.Get(new ConfigKey<string>(MaxAdUnits.TestDeviceIdsKey)));
                if (ids.Length > 0)
                {
                    _max.SetTestDeviceAdvertisingIds(ids);
                    _log.Info(Module + ids.Length + " test device id(s) registered (" + MaxAdUnits.TestDeviceIdsKey + ").");
                }
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "applying " + MaxAdUnits.TestDeviceIdsKey + " threw: " + ex.Message);
            }
        }

        private void StartAutoLoad()
        {
            try { _units.StartAutoLoad(); }
            catch (Exception ex) { _log.Warn(Module + "auto-load threw: " + ex.Message); }
        }
    }
}
