using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase
{
    [CreateAssetMenu(fileName = "FirebaseAnalyticsInstaller", menuName = "PrototypeFramework/Analytics/Firebase Analytics Installer")]
    public sealed class FirebaseAnalyticsInstaller : ScriptableObject, IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<IFirebaseAnalyticsBridge>(r => CreateBridge(r.Resolve<ILog>()), Lifetime.Singleton);
            // A single registration exposes both interfaces: command and consent always share state.
            builder.Register(r => new FirebaseAnalyticsProvider(r.Resolve<IFirebaseAnalyticsBridge>(),
                    r.Resolve<IGameConfig>(), r.Resolve<IConsentService>(), r.Resolve<ILog>(), Dispatch), Lifetime.Singleton)
                .As<IAnalyticsProvider>().As<IAnalyticsCommandTarget>().AsSelf();
        }
        private static void Dispatch(Action action)
        {
            if (PlayerLoopHelper.IsMainThread) action();
            else UniTask.Post(action, PlayerLoopTiming.Update);
        }
        private static IFirebaseAnalyticsBridge CreateBridge(ILog log)
        {
            // Unity drops the constrained Sdk reference when absent: UnityCsReference
            // CustomScriptAssembly.cs:568 -> EditorBuildRules.cs:244,451-465.
#if PF_FIREBASE
            return new ClassicSpins.PrototypeFramework.Analytics.Firebase.Sdk.FirebaseAnalyticsBridge();
#else
            return new NullFirebaseAnalyticsBridge(log);
#endif
        }
    }
}
