using System.Threading;
using System.Threading.Tasks;
using Firebase;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics.Sdk
{
    /// <summary>
    /// The one runtime type that names the Firebase Crashlytics SDK. Compiled only when <c>PF_FIREBASE_CRASHLYTICS</c> is armed
    /// by <c>com.google.firebase.crashlytics</c> (versionDefines). Crashlytics starts with the default <c>FirebaseApp</c>; the
    /// editor and desktop builds have no Crashlytics backend, so only a device report proves it works.
    /// </summary>
    public sealed class CrashlyticsBridge : ICrashlyticsBridge
    {
        private int _state = (int)CrashlyticsBridgeState.NotStarted;   // written from Firebase's continuation thread

        public CrashlyticsBridgeState State => (CrashlyticsBridgeState)Volatile.Read(ref _state);

        public void Start()
        {
            if (Interlocked.CompareExchange(ref _state, (int)CrashlyticsBridgeState.Starting, (int)CrashlyticsBridgeState.NotStarted)
                != (int)CrashlyticsBridgeState.NotStarted) return;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(t =>
            {
                bool ok = t.Status == TaskStatus.RanToCompletion && t.Result == DependencyStatus.Available;
                if (ok)
                {
                    // Creating the default app is what starts Crashlytics.
                    _ = FirebaseApp.DefaultInstance;
                    global::Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                }
                Volatile.Write(ref _state, (int)(ok ? CrashlyticsBridgeState.Ready : CrashlyticsBridgeState.Failed));
            });
        }
    }

    /// <summary>Proof for the Setup Wizard and Doctor check 16 that the gated assembly compiled.</summary>
    internal static class PfModuleMarker_PF_FIREBASE_CRASHLYTICS { }
}
