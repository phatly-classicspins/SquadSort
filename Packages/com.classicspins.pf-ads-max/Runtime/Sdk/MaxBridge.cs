using System;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Sdk
{
    /// <summary>
    /// The thin shell over the AppLovin MAX Unity plugin (UPM <c>com.applovin.mediation.ads</c> ≥ 8.0.0,
    /// built against 8.6.5; the placement-tagged show, verbose logging, test device ids and the
    /// loaded/displayed callbacks verified against 8.6.6). This is the only source file in the module that names a MAX type, and it is
    /// deliberately thin enough to review by eye: nothing in it can run under the headless gate, and the
    /// in-editor MAX stub proves nothing about a device.
    /// </summary>
    /// <remarks>
    /// <para><b>Compiled only when <c>PF_MAX</c> is armed.</b> The assembly carries
    /// <c>defineConstraints: [PF_MAX]</c> and arms the define itself through a <c>versionDefines</c> entry
    /// on the MAX package — never a Player Settings symbol. With the SDK absent the assembly drops and the
    /// installer backs the service with the refusing null bridge instead.</para>
    /// <para><b>Init hosts MAX's consent flow.</b> <c>InitializeSdk</c> runs MAX's Terms and Privacy
    /// Policy flow (Google UMP where required, the iOS ATT prompt when configured) before it reports
    /// completion on <c>OnSdkInitializedEvent</c>. Nothing here bounds that wait.</para>
    /// <para><b>Nothing subscribes before init.</b> MAX marshals callbacks through an executor it creates
    /// during init, so the ad callbacks are wired only after a successful init, and exactly once.</para>
    /// <para><b>Callbacks arrive on the main thread.</b> <see cref="InitializeAsync"/> sets
    /// <c>MaxSdkBase.InvokeEventsOnUnityMainThread = true</c> before <c>InitializeSdk</c>, so every event
    /// this bridge raises — revenue included — is raised on Unity's main thread. Core never marshals; it
    /// only warns if that promise is broken (device-only, unverified here).</para>
    /// <para><b>Revenue.</b> <c>OnAdRevenuePaidEvent</c> for interstitial and rewarded is forwarded as a
    /// primitives-only <see cref="MaxRevenue"/> (raw format label, which callback fired, network, the
    /// placement echo, revenue — <c>-1</c> when the SDK sent no info — and raw precision).</para>
    /// <para><b>Idempotent.</b> A second <see cref="InitializeAsync"/> returns the first call's task; the
    /// completion source supports any number of awaiters.</para>
    /// </remarks>
    public sealed class MaxBridge : IMaxBridge
    {
        private UniTaskCompletionSource<bool> _init;
        private bool _initialized;
        private bool _callbacksWired;

        public bool IsInitialized => _initialized;

        public event Action<string> OnLoaded;
        public event Action OnInitialized;
        private readonly System.Collections.Generic.HashSet<string> _banners = new System.Collections.Generic.HashSet<string>();

        public void ShowBanner(string adUnitId)
        {
            if (_banners.Add(adUnitId))
            {
                // Created once per unit; MAX loads and auto-refreshes it from here on (as Money Sort).
                global::MaxSdk.CreateBanner(adUnitId,
                    new global::MaxSdkBase.AdViewConfiguration(global::MaxSdkBase.AdViewPosition.BottomCenter));
            }
            global::MaxSdk.ShowBanner(adUnitId);
        }

        public void HideBanner(string adUnitId)
        {
            if (_banners.Contains(adUnitId)) global::MaxSdk.HideBanner(adUnitId);
        }
        public event Action<string> OnDisplayed;
        public event Action<string> OnHidden;
        public event Action<string> OnRewarded;
        public event Action<string, string> OnDisplayFailed;
        public event Action<string, string> OnLoadFailed;
        public event Action<MaxRevenue> OnRevenuePaid;

        public UniTask<bool> InitializeAsync()
        {
            if (_init != null) return _init.Task;
            _init = new UniTaskCompletionSource<bool>();

            // Before InitializeSdk: every SDK event (revenue included) is then invoked on Unity's main thread.
            global::MaxSdkBase.InvokeEventsOnUnityMainThread = true;

            if (global::MaxSdk.IsInitialized())
            {
                Complete(true);
                return _init.Task;
            }

            global::MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            try
            {
                global::MaxSdk.InitializeSdk();
            }
            catch (Exception ex)
            {
                // A fault, not a throw out of this call: the init node turns it into Degraded and logs it.
                global::MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
                _init.TrySetException(ex);
            }
            return _init.Task;
        }

        // Both must precede InitializeSdk: the SDK reads them at init.
        public void SetVerboseLogging(bool enabled) => global::MaxSdk.SetVerboseLogging(enabled);

        public void SetTestDeviceAdvertisingIds(string[] advertisingIds)
            => global::MaxSdk.SetTestDeviceAdvertisingIdentifiers(advertisingIds ?? new string[0]);

        public bool IsReady(string adUnitId, bool rewarded)
            => rewarded ? global::MaxSdk.IsRewardedAdReady(adUnitId) : global::MaxSdk.IsInterstitialReady(adUnitId);

        public void Load(string adUnitId, bool rewarded)
        {
            if (rewarded) global::MaxSdk.LoadRewardedAd(adUnitId);
            else global::MaxSdk.LoadInterstitial(adUnitId);
        }

        public void Show(string adUnitId, bool rewarded, string placement)
        {
            var tag = string.IsNullOrEmpty(placement) ? null : placement;
            if (rewarded) global::MaxSdk.ShowRewardedAd(adUnitId, tag);
            else global::MaxSdk.ShowInterstitial(adUnitId, tag);
        }

        // Developer tools; the Core dev tools call these only once the SDK has initialized.
        public void ShowMediationDebugger() => global::MaxSdk.ShowMediationDebugger();

        public void ShowCreativeDebugger() => global::MaxSdk.ShowCreativeDebugger();

        // Privacy options; the Core MaxAdsPrivacyOptions calls these only after init, and serializes the show
        // (MAX keeps one static completion callback). MAX 8.6.5: MaxCmpService.HasSupportedCmp (:73),
        // ShowCmpForExistingUser(Action<MaxCmpError>) (:51), MaxSdkBase.CmpService (:666).
        public bool HasSupportedCmp => global::MaxSdk.CmpService.HasSupportedCmp;

        public void ShowCmpForExistingUser(Action<string> onCompleted)
            => global::MaxSdk.CmpService.ShowCmpForExistingUser(
                error => onCompleted?.Invoke(error == null ? null : error.Code + ": " + error.Message));

        private void OnSdkInitialized(global::MaxSdkBase.SdkConfiguration configuration)
        {
            global::MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            Complete(configuration != null && configuration.IsSuccessfullyInitialized);
        }

        private void Complete(bool ok)
        {
            if (ok)
            {
                WireCallbacks();
                _initialized = true;
            }
            _init.TrySetResult(ok);
            if (ok) OnInitialized?.Invoke();
        }

        private void WireCallbacks()
        {
            if (_callbacksWired) return;
            _callbacksWired = true;

            global::MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += Loaded;
            global::MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += Displayed;
            global::MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += Hidden;
            global::MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += DisplayFailed;
            global::MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += LoadFailed;

            global::MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += Loaded;
            global::MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += Displayed;
            global::MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += Hidden;
            global::MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += DisplayFailed;
            global::MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += LoadFailed;
            global::MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += Rewarded;

            global::MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += InterstitialRevenuePaid;
            global::MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += RewardedRevenuePaid;
            global::MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += InterstitialRevenuePaid;   // non-rewarded; AdFormat says BANNER
        }

        private void Loaded(string adUnitId, global::MaxSdkBase.AdInfo info)
            => OnLoaded?.Invoke(adUnitId);

        private void Displayed(string adUnitId, global::MaxSdkBase.AdInfo info)
            => OnDisplayed?.Invoke(adUnitId);

        private void Hidden(string adUnitId, global::MaxSdkBase.AdInfo info)
            => OnHidden?.Invoke(adUnitId);

        private void Rewarded(string adUnitId, global::MaxSdkBase.Reward reward, global::MaxSdkBase.AdInfo info)
            => OnRewarded?.Invoke(adUnitId);

        private void DisplayFailed(string adUnitId, global::MaxSdkBase.ErrorInfo error, global::MaxSdkBase.AdInfo info)
            => OnDisplayFailed?.Invoke(adUnitId, error != null ? error.Message : "unknown display error");

        private void LoadFailed(string adUnitId, global::MaxSdkBase.ErrorInfo error)
            => OnLoadFailed?.Invoke(adUnitId, error != null ? error.Message : "unknown load error");

        private void InterstitialRevenuePaid(string adUnitId, global::MaxSdkBase.AdInfo info)
            => RevenuePaid(adUnitId, info, false);

        private void RewardedRevenuePaid(string adUnitId, global::MaxSdkBase.AdInfo info)
            => RevenuePaid(adUnitId, info, true);

        private void RevenuePaid(string adUnitId, global::MaxSdkBase.AdInfo info, bool rewarded)
            => OnRevenuePaid?.Invoke(info == null
                ? new MaxRevenue(adUnitId, null, rewarded, null, null, -1d, null)
                : new MaxRevenue(
                    string.IsNullOrEmpty(info.AdUnitIdentifier) ? adUnitId : info.AdUnitIdentifier,
                    info.AdFormat, rewarded, info.NetworkName, info.Placement, info.Revenue, info.RevenuePrecision));
    }
}
