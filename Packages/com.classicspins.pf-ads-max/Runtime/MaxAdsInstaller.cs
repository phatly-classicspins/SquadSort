using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;

namespace ClassicSpins.PrototypeFramework.Ads.Max
{
    /// <summary>
    /// The module's entry point: a <see cref="ScriptableObject"/> <see cref="IInstaller"/> the SKU drops
    /// into the game-installer slot on <c>RootLifetimeScope</c> (Master Scene). That slot is a
    /// <c>List&lt;ScriptableObject&gt;</c> filtered at runtime by <c>is IInstaller</c>, so the framework
    /// needs no compile-time reference to this package.
    /// </summary>
    /// <remarks>
    /// <para><b>Never define-gated — absence refuses.</b> This assembly always compiles, and
    /// <see cref="Install"/> always registers <see cref="MaxAdsService"/> as <see cref="IAdsService"/>.
    /// Only the bridge behind it depends on <c>PF_MAX</c>: the real <c>MaxBridge</c> when the MAX package
    /// resolved, <see cref="NullMaxBridge"/> otherwise. The framework's auto-rewarding fake is therefore
    /// never left as the resolved implementation.</para>
    /// <para><b>Why registering later is enough.</b> SKU installers run after every framework installer,
    /// and the later VContainer registration wins a single resolve. The framework's fake stays registered
    /// and is never constructed.</para>
    /// <para><b>The boot node replaces, never adds.</b> <see cref="MaxAdsInitNode"/> is registered as
    /// itself and handed to <see cref="BootNodeOverride.Replace"/>; registering it as
    /// <see cref="IBootNode"/> too would put a second <c>AdsInit</c> in the graph.</para>
    /// <para><b>One visibility state.</b> <see cref="MaxAdVisibility"/> is registered once, as a singleton,
    /// and the same instance goes to its three producers — <see cref="MaxAdsService"/>,
    /// <see cref="MaxAdsInitNode"/> and <see cref="MaxAdsPrivacyOptions"/>: each opens only its own slot, and
    /// the state alone publishes <c>AdVisibilityChanged</c> (AD-MOD-12).</para>
    /// <para><b>One readiness state.</b> <see cref="MaxAdReadiness"/> is registered once, as a singleton,
    /// and the same instance goes to <see cref="MaxAdsService"/> (bind, pull, show bracketing) and
    /// <see cref="MaxAdsInitNode"/> (the settled publish at init completion): it alone constructs
    /// <c>AdReadinessChanged</c> and is the only module type handed the framework's readiness publisher
    /// (AD-MOD-11).</para>
    /// <para><b>Developer tools.</b> MAX's Mediation and Creative Debuggers are registered as
    /// <see cref="IDevTool"/> — <see cref="MaxMediationDebuggerTool"/> and <see cref="MaxCreativeDebuggerTool"/>,
    /// one distinct type each (DW-35), outside any <c>#if</c>. Each is available only once the SDK has
    /// initialized and while <c>ads.max.devTools</c> is not <c>0</c>, both read at call time. Which builds
    /// expose them is the SKU's decision.</para>
    /// <para><b>Privacy options.</b> <see cref="MaxAdsPrivacyOptions"/> is registered as the framework's
    /// <see cref="IAdsPrivacyOptions"/>, replacing <c>AdsInstaller</c>'s Null (declared <c>replacement</c>
    /// in <c>ports[]</c>), outside any <c>#if</c>. It re-shows MAX's own consent form once the SDK has
    /// initialized and reports a supported CMP, holds its own <c>PrivacyOptions</c> visibility slot while
    /// the form is up, and writes nothing back — personalized-ads consent stays MAX's (AD-MOD-9). The SKU
    /// consumes the port behind its own interface and never re-registers it.</para>
    /// <para><b>Factory registrations throughout</b> keep constructor selection explicit and immune to
    /// IL2CPP stripping.</para>
    /// <para><b>Platform and time are handed to Core here.</b> <see cref="MaxAdUnits"/> takes the platform
    /// token this assembly maps from <c>UNITY_ANDROID</c> / <c>UNITY_IOS</c> (none elsewhere — the Editor on a
    /// desktop target skips the platform tiers), and Core's retry and display timeout take a realtime
    /// <c>UniTask.Delay</c>: the game may pause <c>timeScale</c> while an ad is up.</para>
    /// <para>Creating the asset is a live-editor step (human zone, rule #12):
    /// <c>Assets ▸ Create ▸ PrototypeFramework ▸ Ads ▸ AppLovin MAX Installer</c>.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "MaxAdsInstaller",
        menuName = "PrototypeFramework/Ads/AppLovin MAX Installer")]
    public sealed class MaxAdsInstaller : ScriptableObject, IInstaller
    {
        /// <summary>The framework boot node this module replaces (<c>pf-module.json</c> <c>port.replacesBootNode</c>).</summary>
        public const string ReplacedBootNodeId = "AdsInit";

        public void Install(IContainerBuilder builder)
        {
            builder.Register<IMaxBridge>(resolver => CreateBridge(resolver.Resolve<ILog>()), Lifetime.Singleton);

            // The one owner of unit resolution, the requested set and load retry — shared by the service and
            // the init node.
            builder.Register<MaxAdUnits>(resolver => new MaxAdUnits(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<ILog>(),
                    PlatformToken(),
                    RealtimeDelay),
                Lifetime.Singleton);

            // The module's one visibility state (AD-MOD-12): the only constructor of AdVisibilityChanged. One
            // singleton, handed to both producers, so the fact is derived from every open surface's slot.
            builder.Register<MaxAdVisibility>(resolver => new MaxAdVisibility(
                    resolver.Resolve<IAdVisibilityChangedPublisher>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton);

            // The module's one readiness state (AD-MOD-11): the only constructor of AdReadinessChanged. One
            // singleton, handed to the service and the init node, so the pull and the push read one ready(unit).
            builder.Register<MaxAdReadiness>(resolver => new MaxAdReadiness(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<MaxAdUnits>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<IAdReadinessChangedPublisher>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton);

            // The one producer of AdRevenuePaid: it subscribes to the bridge's revenue event in its constructor.
            builder.Register<MaxRevenueReporter>(resolver => new MaxRevenueReporter(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<IAnalytics>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton);

            // Implementation type MaxAdsService, contract IAdsService (the framework's own AdsInstaller shape).
            // VContainer refuses two Singleton registrations of one service that share an implementation
            // type, so the implementation type must be the concrete class, never the interface.
            //
            // The revenue reporter is resolved here, as the service is built: VContainer singletons are lazy,
            // and MAX raises revenue only after a show, which only this service issues — so the reporter is
            // listening before the first paid impression can arrive, without a build callback that would
            // construct IAnalytics at container build.
            builder.Register(resolver =>
                {
                    resolver.Resolve<MaxRevenueReporter>();
                    return new MaxAdsService(
                        resolver.Resolve<IMaxBridge>(),
                        resolver.Resolve<MaxAdUnits>(),
                        resolver.Resolve<MaxAdVisibility>(),
                        resolver.Resolve<MaxAdReadiness>(),
                        resolver.Resolve<IAnalytics>(),
                        resolver.Resolve<IGameConfig>(),
                        resolver.Resolve<ILog>(),
                        RealtimeDelay);
                },
                Lifetime.Singleton)
                .As<IAdsService>();

            // IBannerAds — the MAX banner; wins over the framework's FakeBannerAds the same way.
            builder.Register(resolver => new MaxBannerAds(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<MaxAdUnits>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<IAdGate>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton)
                .As<IBannerAds>();

            builder.Register<MaxAdsInitNode>(resolver => new MaxAdsInitNode(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<MaxAdUnits>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<MaxAdVisibility>(),
                    resolver.Resolve<MaxAdReadiness>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton);

            // Transient, not Singleton: VContainer's collection provider throws "Conflict implementation type"
            // when an EARLIER Singleton registration of the same implementation type exists, and every
            // BootNodeOverride — a SKU's RegisterInstance included — has the one implementation type
            // BootNodeOverride. A Transient entry never trips that check for the registrations after it.
            // BootRunInstaller resolves the override collection once, and the node inside it is the
            // singleton above, so nothing is duplicated.
            builder.Register<BootNodeOverride>(
                resolver => BootNodeOverride.Replace(ReplacedBootNodeId, resolver.Resolve<MaxAdsInitNode>()),
                Lifetime.Transient);

            // Developer tools (AD-MOD-13), declared additive in pf-module.json ports[]. One distinct
            // implementation type per tool: VContainer's collection provider throws "Conflict implementation
            // type" on two Singleton entries sharing one (DW-35 is still open), so never one shared type and
            // never RegisterInstance. Nothing is read here: each tool reads IsInitialized and the
            // ads.max.devTools switch at call time, because config arrives only with ConfigAvailable.
            builder.Register<MaxMediationDebuggerTool>(resolver => new MaxMediationDebuggerTool(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton)
                .As<IDevTool>();

            builder.Register<MaxCreativeDebuggerTool>(resolver => new MaxCreativeDebuggerTool(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<IGameConfig>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton)
                .As<IDevTool>();

            // "Change privacy settings" (AD-MOD-9, FR56), declared replacement in pf-module.json ports[]: it
            // replaces the framework AdsInstaller's NullAdsPrivacyOptions (the later registration wins a single
            // resolve). It holds its own PrivacyOptions slot in the SAME visibility singleton the service and
            // the init node use, so an ad is refused while MAX's form is up. Ungated: with MAX absent the null
            // bridge reads uninitialized, so every call refuses with a pf-ads-max warning.
            builder.Register<MaxAdsPrivacyOptions>(resolver => new MaxAdsPrivacyOptions(
                    resolver.Resolve<IMaxBridge>(),
                    resolver.Resolve<MaxAdVisibility>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton)
                .As<IAdsPrivacyOptions>();
        }

        /// <summary>A timeScale-independent delay: retries and the display timeout keep counting while the
        /// game is paused under an ad.</summary>
        private static UniTask RealtimeDelay(TimeSpan delay, CancellationToken ct)
            => UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, ct);

        /// <summary>The platform token <see cref="MaxAdUnits"/> keys its platform tiers on; null off-mobile.</summary>
        private static string PlatformToken()
        {
#if UNITY_ANDROID
            return MaxAdUnits.Android;
#elif UNITY_IOS
            return MaxAdUnits.Ios;
#else
            return null;
#endif
        }

        private static IMaxBridge CreateBridge(ILog log)
        {
            // This `#if PF_MAX` edge names a type from the PF_MAX-constrained Sdk assembly, which this
            // UNGATED assembly references unconditionally. That is legal only because Unity drops a
            // reference to an assembly whose defineConstraints are unsatisfied instead of failing the
            // compile — undocumented behaviour, verified in UnityCsReference:
            // CustomScriptAssembly.cs:568 → EditorBuildRules.cs:244, 451-465.
            // PF_MAX is armed here by this assembly's own versionDefines entry on com.applovin.mediation.ads.
#if PF_MAX
            return new ClassicSpins.PrototypeFramework.Ads.Max.Sdk.MaxBridge();
#else
            return new NullMaxBridge(log);
#endif
        }
    }
}
