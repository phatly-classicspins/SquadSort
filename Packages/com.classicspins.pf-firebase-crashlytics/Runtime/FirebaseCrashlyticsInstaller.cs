using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>
    /// Replaces the framework's <c>CrashLog</c> boot node with <see cref="CrashlyticsCrashLogNode"/>. Drop the asset into
    /// <c>RootLifetimeScope ▸ SKU game installers</c> in the Master Scene. Never gated: without the SDK it still replaces the
    /// node, over <see cref="NullCrashlyticsBridge"/>, so the absence is logged instead of the asset turning into a missing script.
    /// </summary>
    [CreateAssetMenu(fileName = "FirebaseCrashlyticsInstaller", menuName = "PrototypeFramework/Crash Reporting/Firebase Crashlytics Installer")]
    public sealed class FirebaseCrashlyticsInstaller : ScriptableObject, IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<CrashlyticsCrashLogNode>(r =>
            {
                var log = r.Resolve<ILog>();
                return new CrashlyticsCrashLogNode(CreateBridge(log), new UnityCrashHooks(log), log);
            }, Lifetime.Singleton);
            builder.Register<BootNodeOverride>(
                r => BootNodeOverride.Replace(CrashlyticsModule.ReplacedNodeId, r.Resolve<CrashlyticsCrashLogNode>()),
                Lifetime.Transient);
        }

        private static ICrashlyticsBridge CreateBridge(ILog log)
        {
            // An #if edge into the gated Sdk role: Unity drops a reference to an assembly whose
            // defineConstraints are unmet (UnityCsReference CustomScriptAssembly.cs:568 →
            // EditorBuildRules.cs:244, 451-465), so this compiles with and without the SDK.
#if PF_FIREBASE_CRASHLYTICS
            return new ClassicSpins.PrototypeFramework.FirebaseCrashlytics.Sdk.CrashlyticsBridge();
#else
            return new NullCrashlyticsBridge(log);
#endif
        }
    }
}
