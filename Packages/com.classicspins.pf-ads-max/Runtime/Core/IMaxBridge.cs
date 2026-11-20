using System;
using Cysharp.Threading.Tasks;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The SDK seam — the only surface through which anything in this module reaches the AppLovin MAX
    /// plugin. Everything with a decision in it (the show state machine, the publish pair, result
    /// mapping, the init-once memo, unit resolution, auto-load and load retry) lives above this
    /// interface, in <see cref="MaxAdsService"/>, <see cref="MaxAdsInitNode"/> and
    /// <see cref="MaxAdUnits"/>, so it runs under a plain <c>dotnet test</c> against a fake bridge with
    /// no SDK, no engine and no device.
    /// </summary>
    /// <remarks>
    /// <para><b>Primitives only.</b> Ad unit ids, placements and error messages are strings, the format is
    /// a <c>bool</c>, test device ids a <c>string[]</c>, a paid impression the <see cref="MaxRevenue"/>
    /// struct of strings, a <c>bool</c> and a <c>double</c>. No MAX type crosses this boundary, so the
    /// engine-free <c>Core</c> assembly takes no compile-time dependency on the plugin (AD-MOD-1).</para>
    /// <para><b>No consent setter, by design (AD-MOD-9).</b> Personalized-ads consent is MAX's: its
    /// Terms and Privacy Policy flow runs <i>inside</i> <see cref="InitializeAsync"/>, and
    /// <see cref="ShowCmpForExistingUser"/> only re-shows MAX's own form. The module never passes a
    /// consent flag in and never writes MAX's outcome back.</para>
    /// <para><b>Fullscreen only.</b> Interstitial and rewarded units; no banner surface.</para>
    /// </remarks>
    public interface IMaxBridge
    {
        /// <summary>True once the SDK has initialized successfully. Every can-show decision keys on
        /// this — never on the boot status of <c>AdsReady</c>.</summary>
        bool IsInitialized { get; }

        /// <summary>Initialize the SDK. Hosts MAX's own consent flow, which may put SDK-native UI on
        /// screen and wait on the player for as long as they read — so nothing may bound this await
        /// with a timeout. Resolves <c>true</c> on a successful init, <c>false</c> otherwise.
        /// Idempotent: a second call returns the first call's outcome.</summary>
        UniTask<bool> InitializeAsync();

        /// <summary>Turn the SDK's verbose logging on or off. Must be called before
        /// <see cref="InitializeAsync"/> to cover init.</summary>
        void SetVerboseLogging(bool enabled);

        /// <summary>Register advertising ids that are served test ads. Must be called before
        /// <see cref="InitializeAsync"/> — the SDK reads them only at init.</summary>
        void SetTestDeviceAdvertisingIds(string[] advertisingIds);

        /// <summary>Whether <paramref name="adUnitId"/> has a loaded ad of the given format.</summary>
        bool IsReady(string adUnitId, bool rewarded);

        /// <summary>Start (or restart) a load. Fire-and-forget; the outcome arrives on
        /// <see cref="OnLoaded"/> or <see cref="OnLoadFailed"/>.</summary>
        void Load(string adUnitId, bool rewarded);

        /// <summary>Show the unit, tagged with the game's <paramref name="placement"/> name (MAX
        /// reporting). The outcome arrives on the events below.</summary>
        void Show(string adUnitId, bool rewarded, string placement);

        /// <summary>Open the SDK's Mediation Debugger. Call after init; the module's dev tools
        /// (<see cref="MaxMediationDebuggerTool"/>) guard that.</summary>
        void ShowMediationDebugger();

        /// <summary>Open the SDK's Creative Debugger. Call after init; the module's dev tools
        /// (<see cref="MaxCreativeDebuggerTool"/>) guard that.</summary>
        void ShowCreativeDebugger();

        /// <summary>Whether the SDK found a consent platform it can re-prompt (MAX's CMP service; Google
        /// UMP where it applies). Read after init — <see cref="MaxAdsPrivacyOptions"/> guards that. The
        /// in-editor stub answers false.</summary>
        bool HasSupportedCmp { get; }

        /// <summary>Re-show the SDK's consent form to an existing user ("change privacy settings"). Call
        /// after init, only while <see cref="HasSupportedCmp"/> is true. MAX resets the current consent
        /// before it shows the form. <paramref name="onCompleted"/> receives <c>null</c> on success or a
        /// non-empty error message. MAX keeps <b>one</b> static completion callback, so a second call
        /// overwrites the first one's — the caller serializes (<see cref="MaxAdsPrivacyOptions"/> does). The
        /// in-editor stub may complete synchronously, inside this call.</summary>
        void ShowCmpForExistingUser(Action<string> onCompleted);

        /// <summary>A load attempt succeeded. Carries the ad unit id.</summary>
        event Action<string> OnLoaded;

        /// <summary>A load attempt failed. Carries the ad unit id and the SDK's message.</summary>
        event Action<string, string> OnLoadFailed;

        /// <summary>The fullscreen unit is on screen. Carries the ad unit id.</summary>
        event Action<string> OnDisplayed;

        /// <summary>The fullscreen unit was dismissed. Carries the ad unit id.</summary>
        event Action<string> OnHidden;

        /// <summary>A rewarded unit granted its reward. Carries the ad unit id. Its order relative to
        /// <see cref="OnHidden"/> is native-side behaviour MAX does not document — never rely on it.</summary>
        event Action<string> OnRewarded;

        /// <summary>A loaded ad failed to display. Carries the ad unit id and the SDK's message.</summary>
        event Action<string, string> OnDisplayFailed;

        /// <summary>The SDK reported a paid impression on an interstitial or rewarded unit, as a
        /// primitives-only <see cref="MaxRevenue"/>. Raised on the main thread: the bridge tells the SDK to
        /// invoke its events on Unity's main thread before init. Core never marshals threads itself.</summary>
        event Action<MaxRevenue> OnRevenuePaid;

        /// <summary>Show the banner for <paramref name="adUnitId"/>: create it (bottom centre) on the first
        /// call, then show it. MAX loads and refreshes a banner by itself; its revenue arrives on
        /// <see cref="OnRevenuePaid"/> like every other format.</summary>
        void ShowBanner(string adUnitId);

        /// <summary>Hide the banner for <paramref name="adUnitId"/> (it stays created for the next show).</summary>
        void HideBanner(string adUnitId);

        /// <summary>Raised once, after the first successful <see cref="InitializeAsync"/>, so a banner
        /// requested before init can be applied.</summary>
        event Action OnInitialized;
    }
}
