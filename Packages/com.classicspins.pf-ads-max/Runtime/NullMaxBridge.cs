using System;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;

namespace ClassicSpins.PrototypeFramework.Ads.Max
{
    /// <summary>
    /// The refusing bridge the installer registers when <c>PF_MAX</c> is not armed — the MAX package is
    /// absent, or older than the 8.0.0 UPM floor. Every member refuses and logs a warning naming
    /// <c>pf-ads-max</c>; nothing throws.
    /// </summary>
    /// <remarks>
    /// <para><b>Why it exists at all.</b> If the installer were define-gated, removing the SDK would turn
    /// the installer asset into a missing script, the installer would never run, and the game would fall
    /// back to the framework's fake ads service — which auto-rewards — without a log line. With this
    /// bridge behind <see cref="MaxAdsService"/>, absence is loud: every show resolves
    /// <see cref="AdResult.Failed"/>, the <c>AdsInit</c> replacement degrades, and the log names the
    /// module.</para>
    /// <para>Engine-free on purpose, so the module's headless test project compiles this one file
    /// alongside <c>Runtime/Core/**</c>.</para>
    /// </remarks>
    public sealed class NullMaxBridge : IMaxBridge
    {
        private const string Refusal =
            "[pf-ads-max] AppLovin MAX is not installed (PF_MAX is not armed: com.applovin.mediation.ads >= 8.0.0 "
            + "is not resolved), so ads are disabled — ";

        private readonly ILog _log;

        public NullMaxBridge(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool IsInitialized => false;

        public UniTask<bool> InitializeAsync()
        {
            _log.Warn(Refusal + "InitializeAsync refused. Install the module's requirements via Framework/Setup Wizard ▸ Modules.");
            return UniTask.FromResult(false);
        }

        public void SetVerboseLogging(bool enabled)
            => _log.Warn(Refusal + "SetVerboseLogging(" + (enabled ? "true" : "false") + ") refused.");

        public void SetTestDeviceAdvertisingIds(string[] advertisingIds)
            => _log.Warn(Refusal + "SetTestDeviceAdvertisingIds refused.");

        public bool IsReady(string adUnitId, bool rewarded)
        {
            _log.Warn(Refusal + "IsReady('" + adUnitId + "') refused.");
            return false;
        }

        public void Load(string adUnitId, bool rewarded)
            => _log.Warn(Refusal + "Load('" + adUnitId + "') refused.");

        public void Show(string adUnitId, bool rewarded, string placement)
            => _log.Warn(Refusal + "Show('" + adUnitId + "', '" + placement + "') refused.");

        public void ShowMediationDebugger()
            => _log.Warn(Refusal + "ShowMediationDebugger refused.");

        public void ShowCreativeDebugger()
            => _log.Warn(Refusal + "ShowCreativeDebugger refused.");

        // Like IsInitialized: a state read, so no log.
        public bool HasSupportedCmp => false;

        public void ShowCmpForExistingUser(Action<string> onCompleted)
        {
            const string message = "ShowCmpForExistingUser refused.";
            _log.Warn(Refusal + message);
            // Complete anyway, so nothing awaiting the callback can hang.
            onCompleted?.Invoke("pf-ads-max: AppLovin MAX is not installed — " + message);
        }

        // Never raised: nothing can load or show. Explicit accessors keep the unused-event warning away.
        public event Action<string> OnLoaded { add { } remove { } }
        public event Action<string> OnDisplayed { add { } remove { } }
        public event Action<string> OnHidden { add { } remove { } }
        public event Action<string> OnRewarded { add { } remove { } }
        public event Action<string, string> OnDisplayFailed { add { } remove { } }
        public event Action<string, string> OnLoadFailed { add { } remove { } }
        public event Action<MaxRevenue> OnRevenuePaid { add { } remove { } }
        public event Action OnInitialized { add { } remove { } }

        public void ShowBanner(string adUnitId)
            => _log.Warn(Refusal + "ShowBanner('" + adUnitId + "') refused.");

        public void HideBanner(string adUnitId) { }
    }
}
