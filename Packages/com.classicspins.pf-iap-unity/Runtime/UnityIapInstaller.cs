using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity
{
    /// <summary>
    /// The module's entry point: a <see cref="ScriptableObject"/> <see cref="IInstaller"/> the SKU drops into the
    /// game-installer slot on <c>RootLifetimeScope</c> (Master Scene). That slot is a
    /// <c>List&lt;ScriptableObject&gt;</c> filtered at runtime by <c>is IInstaller</c>, so the framework needs no
    /// compile-time reference to this package.
    /// </summary>
    /// <remarks>
    /// <para><b>Never define-gated — absence refuses.</b> This assembly always compiles, and
    /// <see cref="Install"/> always registers <see cref="UnityIapPurchaseService"/> as <see cref="IPurchaseService"/>.
    /// Only the bridge behind it depends on <c>PF_UNITY_IAP</c>: the real <c>UnityIapBridge</c> when Unity IAP
    /// resolved, <see cref="NullUnityIapBridge"/> otherwise. The framework's free-granting fake is therefore
    /// never left as the resolved implementation.</para>
    /// <para><b>Why registering later is enough.</b> SKU installers run after every framework installer, and the
    /// later VContainer registration wins a single resolve. The framework's <c>FakePurchaseService</c> stays
    /// registered and is never constructed. The framework's <c>ICatalogSource</c> (baked <c>products.json</c>)
    /// and <c>GrantInterpreter</c> are reused as they are.</para>
    /// <para><b>The boot node replaces, never adds.</b> <see cref="UnityIapInitNode"/> is registered as itself
    /// and handed to <see cref="BootNodeOverride.Replace"/>; registering it as <see cref="IBootNode"/> too would
    /// put a second <c>IapInit</c> in the graph.</para>
    /// <para><b>Platform and time are handed to Core here.</b> The platform token is mapped from
    /// <c>UNITY_ANDROID</c> / <c>UNITY_IOS</c> (restore runs Apple's restore-transactions on iOS only), and the
    /// store timeouts take a realtime <c>UniTask.Delay</c>.</para>
    /// <para>Creating the asset is a live-editor step (human zone, rule #12):
    /// <c>Assets ▸ Create ▸ PrototypeFramework ▸ IAP ▸ Unity IAP Installer</c>, or
    /// <c>Framework/Modules/Create Installer Assets</c>.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "UnityIapInstaller",
        menuName = "PrototypeFramework/IAP/Unity IAP Installer")]
    public sealed class UnityIapInstaller : ScriptableObject, IInstaller
    {
        /// <summary>The framework boot node this module replaces (<c>pf-module.json</c> <c>port.replacesBootNode</c>).</summary>
        public const string ReplacedBootNodeId = "IapInit";

        public void Install(IContainerBuilder builder)
        {
            builder.Register<IUnityIapBridge>(resolver => CreateBridge(resolver.Resolve<ILog>()), Lifetime.Singleton);

            // Implementation type UnityIapPurchaseService, contract IPurchaseService (+ itself, for the init node).
            builder.Register(resolver => new UnityIapPurchaseService(
                    resolver.Resolve<IUnityIapBridge>(),
                    resolver.Resolve<ICatalogSource>(),
                    resolver.Resolve<GrantInterpreter>(),
                    resolver.Resolve<IEntitlementService>(),
                    resolver.Resolve<IAnalytics>(),
                    resolver.Resolve<ILog>(),
                    PlatformToken(),
                    RealtimeDelay),
                Lifetime.Singleton)
                .As<IPurchaseService>()
                .AsSelf();

            builder.Register<UnityIapInitNode>(resolver => new UnityIapInitNode(
                    resolver.Resolve<UnityIapPurchaseService>(),
                    resolver.Resolve<ILog>()),
                Lifetime.Singleton);

            // Transient, not Singleton: VContainer's collection provider throws "Conflict implementation type"
            // when an EARLIER Singleton registration of the same implementation type exists, and every
            // BootNodeOverride has the one implementation type BootNodeOverride. The node inside it is the
            // singleton above, so nothing is duplicated.
            builder.Register<BootNodeOverride>(
                resolver => BootNodeOverride.Replace(ReplacedBootNodeId, resolver.Resolve<UnityIapInitNode>()),
                Lifetime.Transient);
        }

        /// <summary>A timeScale-independent delay: the store timeouts keep counting while the game is paused.</summary>
        private static UniTask RealtimeDelay(TimeSpan delay, CancellationToken ct)
            => UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, ct);

        /// <summary>The platform token the service keys restore on.</summary>
        private static UnityIapPlatform PlatformToken()
        {
#if UNITY_ANDROID
            return UnityIapPlatform.Android;
#elif UNITY_IOS
            return UnityIapPlatform.Ios;
#else
            return UnityIapPlatform.Other;
#endif
        }

        private static IUnityIapBridge CreateBridge(ILog log)
        {
            // This `#if PF_UNITY_IAP` edge names a type from the PF_UNITY_IAP-constrained Sdk assembly, which this
            // UNGATED assembly references unconditionally. That is legal only because Unity drops a reference to
            // an assembly whose defineConstraints are unsatisfied instead of failing the compile — undocumented
            // behaviour, verified in UnityCsReference: CustomScriptAssembly.cs:568 → EditorBuildRules.cs:244, 451-465.
            // PF_UNITY_IAP is armed here by this assembly's own versionDefines entry on com.unity.purchasing.
#if PF_UNITY_IAP
            return new ClassicSpins.PrototypeFramework.Iap.Unity.Sdk.UnityIapBridge();
#else
            return new NullUnityIapBridge(log);
#endif
        }
    }
}
