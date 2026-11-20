using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>
    /// The bridge when <c>com.google.firebase.crashlytics</c> is not installed: fails its start-up and says so once with the
    /// module id. Never a silent success, never a throw.
    /// </summary>
    public sealed class NullCrashlyticsBridge : ICrashlyticsBridge
    {
        private readonly ILog _log;

        public NullCrashlyticsBridge(ILog log) => _log = log;

        public CrashlyticsBridgeState State { get; private set; } = CrashlyticsBridgeState.NotStarted;

        public void Start()
        {
            if (State != CrashlyticsBridgeState.NotStarted) return;
            State = CrashlyticsBridgeState.Failed;
            _log?.Warn($"[{CrashlyticsModule.ModuleId}] refused Start: com.google.firebase.crashlytics is not installed (PF_FIREBASE_CRASHLYTICS unset); crashes are not reported.");
        }
    }
}
