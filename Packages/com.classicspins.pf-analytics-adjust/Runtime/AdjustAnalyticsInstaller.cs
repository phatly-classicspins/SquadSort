using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust
{
    [CreateAssetMenu(fileName = "AdjustAnalyticsInstaller", menuName = "PrototypeFramework/Analytics/Adjust Analytics Installer")]
    public sealed class AdjustAnalyticsInstaller : ScriptableObject, IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IAdjustAnalyticsBridge>(r => CreateBridge(r.Resolve<ILog>()), Lifetime.Singleton);
            builder.Register(r => new AdjustAnalyticsProvider(r.Resolve<IAdjustAnalyticsBridge>(),
                    r.Resolve<IGameConfig>(), r.Resolve<IConsentService>(), r.Resolve<ILog>()), Lifetime.Singleton)
                .As<IAnalyticsProvider>().As<IAnalyticsCommandTarget>().AsSelf();
        }
        private static IAdjustAnalyticsBridge CreateBridge(ILog log)
        {
            // Unity drops absent constrained references: UnityCsReference CustomScriptAssembly.cs:568
            // -> EditorBuildRules.cs:244,451-465. Installer and Null remain ungated.
#if PF_ADJUST
            return new ClassicSpins.PrototypeFramework.Analytics.Adjust.Sdk.AdjustAnalyticsBridge();
#else
            return new NullAdjustAnalyticsBridge(log);
#endif
        }
    }
}
