using UnityEngine;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseRc
{
    /// <summary>
    /// Replaces the framework's fake <see cref="IRemoteConfigSource"/> with Firebase Remote Config (later
    /// registration wins: SKU installers run after the framework's). Drop the asset into
    /// <c>RootLifetimeScope ▸ SKU game installers</c> in the Master Scene. Never gated: without the SDK it still
    /// replaces the fake, over <see cref="NullFirebaseRcBridge"/>, so the absence is logged.
    /// </summary>
    [CreateAssetMenu(fileName = "FirebaseRemoteConfigInstaller", menuName = "PrototypeFramework/Config/Firebase Remote Config Installer")]
    public sealed class FirebaseRemoteConfigInstaller : ScriptableObject, IInstaller
    {
        [Tooltip("Firebase throttles fetches to this interval (it caches the last values in between). 0 while testing console edits; 60 or more in a release.")]
        [SerializeField] private int _minimumFetchIntervalMinutes = 60;

        public void Install(IContainerBuilder builder)
        {
            var settings = new FirebaseRcSettings(_minimumFetchIntervalMinutes);
            builder.Register<IRemoteConfigSource>(r =>
            {
                var log = r.Resolve<ILog>();
                return new FirebaseRemoteConfigSource(CreateBridge(log), settings, r.Resolve<ConfigDefaults>(), log);
            }, Lifetime.Singleton);
        }

        private static IFirebaseRcBridge CreateBridge(ILog log)
        {
            // An #if edge into the gated Sdk role: Unity drops a reference to an assembly whose
            // defineConstraints are unmet (UnityCsReference CustomScriptAssembly.cs:568 →
            // EditorBuildRules.cs:244, 451-465), so this compiles with and without the SDK.
#if PF_FIREBASE_RC
            return new ClassicSpins.PrototypeFramework.FirebaseRc.Sdk.FirebaseRcBridge();
#else
            return new NullFirebaseRcBridge(log);
#endif
        }
    }
}
