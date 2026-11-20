using System;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The shared body of the module's developer tools (<see cref="IDevTool"/>, AD-MOD-13): the
    /// <see cref="SwitchKey"/> kill switch, availability, the guarded <see cref="Open"/> and its logging.
    /// Each concrete tool only names its id and the one bridge call it makes.
    /// </summary>
    /// <remarks>
    /// <para><b>Available</b> = the bridge reports the SDK initialized ∧ <see cref="SwitchKey"/> is not
    /// <c>0</c> (absent ⇒ on). Both are read on <b>every</b> <see cref="IsAvailable"/> / <see cref="Open"/>
    /// call, never in a constructor or the installer: config arrives only with <c>ConfigAvailable</c>, and
    /// init completes long after the container is built.</para>
    /// <para><b>Fails off, never throws.</b> A config read that throws means <b>off</b> (unlike
    /// <c>ads.max.revenueLog</c>, which fails on: losing money is worse than a missing record, but a broken
    /// config layer is no reason to put SDK UI on screen). A throwing bridge call is caught. Every refusal
    /// and fault is one warning naming <c>pf-ads-max</c>, the tool and the reason.</para>
    /// <para><b>No visibility slot.</b> The debuggers are developer-only; they are never refused during an
    /// ad and never publish <c>AdVisibilityChanged</c>.</para>
    /// </remarks>
    public abstract class MaxDevTool : IDevTool
    {
        private const string Module = "[pf-ads-max] ";

        /// <summary>Dev-tools switch (int; absent ⇒ on, <c>0</c> ⇒ off), read at call time.</summary>
        public const string SwitchKey = "ads.max.devTools";

        /// <summary>The id of <see cref="MaxMediationDebuggerTool"/>.</summary>
        public const string MediationDebuggerId = "ads-max.mediation-debugger";

        /// <summary>The id of <see cref="MaxCreativeDebuggerTool"/>.</summary>
        public const string CreativeDebuggerId = "ads-max.creative-debugger";

        private readonly IMaxBridge _max;
        private readonly IGameConfig _config;
        private readonly ILog _log;

        protected MaxDevTool(string id, IMaxBridge max, IGameConfig config, ILog log)
        {
            Id = id;
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Id { get; }

        /// <summary>Initialized ∧ switch not <c>0</c>, read now. A faulting read is logged and reads false.</summary>
        public bool IsAvailable
        {
            get
            {
                string why;
                bool fault;
                var available = Available(out why, out fault);
                if (fault) _log.Warn(Module + Id + " is unavailable: " + why + ".");
                return available;
            }
        }

        /// <summary>Open the tool when available: one bridge call, then true. Otherwise false with one
        /// warning naming the reason. Never throws.</summary>
        public bool Open()
        {
            string why;
            bool fault;
            if (!Available(out why, out fault))
            {
                _log.Warn(Module + Id + " refused: " + why + ".");
                return false;
            }

            try
            {
                Show(_max);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warn(Module + Id + " failed to open: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>The tool's one bridge call.</summary>
        protected abstract void Show(IMaxBridge max);

        private bool Available(out string why, out bool fault)
        {
            fault = false;
            try
            {
                int value;
                if (MaxAdUnits.TryReadInt(_config, SwitchKey, out value) && value == 0)
                {
                    why = "'" + SwitchKey + "' is 0 (developer tools switched off)";
                    return false;
                }
            }
            catch (Exception ex)
            {
                fault = true;
                why = "reading '" + SwitchKey + "' threw (" + ex.GetType().Name + ": " + ex.Message
                      + "), so developer tools read as off";
                return false;
            }

            try
            {
                if (!_max.IsInitialized)
                {
                    why = "the AppLovin MAX SDK is not initialized";
                    return false;
                }
            }
            catch (Exception ex)
            {
                fault = true;
                why = "reading the SDK's initialized state threw (" + ex.GetType().Name + ": " + ex.Message + ")";
                return false;
            }

            why = null;
            return true;
        }
    }

    /// <summary>Opens MAX's Mediation Debugger (<see cref="MaxDevTool.MediationDebuggerId"/>).</summary>
    public sealed class MaxMediationDebuggerTool : MaxDevTool
    {
        // No defaulted parameters: VContainer resolves every parameter and ignores C# default values.
        public MaxMediationDebuggerTool(IMaxBridge max, IGameConfig config, ILog log)
            : base(MediationDebuggerId, max, config, log)
        {
        }

        protected override void Show(IMaxBridge max) => max.ShowMediationDebugger();
    }

    /// <summary>Opens MAX's Creative Debugger (<see cref="MaxDevTool.CreativeDebuggerId"/>).</summary>
    public sealed class MaxCreativeDebuggerTool : MaxDevTool
    {
        // No defaulted parameters: VContainer resolves every parameter and ignores C# default values.
        public MaxCreativeDebuggerTool(IMaxBridge max, IGameConfig config, ILog log)
            : base(CreativeDebuggerId, max, config, log)
        {
        }

        protected override void Show(IMaxBridge max) => max.ShowCreativeDebugger();
    }
}
