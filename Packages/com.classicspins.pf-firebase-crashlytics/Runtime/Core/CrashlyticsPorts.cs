namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>Where the SDK is in its start-up.</summary>
    public enum CrashlyticsBridgeState { NotStarted, Starting, Ready, Failed }

    /// <summary>
    /// The thin shell over the Firebase Crashlytics SDK, so every decision stays in this engine-free assembly under
    /// <c>dotnet test</c>. The real one (<c>CrashlyticsBridge</c>) lives in the gated <c>Sdk</c> role; without the SDK the
    /// installer hands out <c>NullCrashlyticsBridge</c>, which refuses the start and says so. Neither throws on purpose.
    /// </summary>
    public interface ICrashlyticsBridge
    {
        CrashlyticsBridgeState State { get; }

        /// <summary>Begin the SDK's start-up (Firebase checks its native dependencies asynchronously) and, once it is up,
        /// report uncaught C# exceptions as fatal. Returns at once; safe to call again.</summary>
        void Start();
    }

    /// <summary>The engine's unhandled-exception surfaces the framework's own <c>CrashLog</c> node hooks. Kept behind an
    /// interface so the replacement node keeps that behaviour without this assembly touching the engine.</summary>
    public interface ICrashHooks
    {
        /// <summary>Hook the surfaces. Called once.</summary>
        void Install();
    }

    /// <summary>Constants shared by every role.</summary>
    public static class CrashlyticsModule
    {
        public const string ModuleId = "com.classicspins.pf-firebase-crashlytics";

        /// <summary>The framework node this module replaces; the replacement must carry the same id.</summary>
        public const string ReplacedNodeId = "CrashLog";
    }
}
