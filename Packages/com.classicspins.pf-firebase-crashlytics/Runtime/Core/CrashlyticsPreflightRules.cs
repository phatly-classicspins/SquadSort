using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics
{
    /// <summary>The facts the Editor role gathers before a build.</summary>
    public sealed class CrashlyticsBuildFacts
    {
        public bool MobileTarget;      // Android or iOS: the only targets Crashlytics reports from
        public bool SdkCompiled;       // PF_FIREBASE_CRASHLYTICS is armed (com.google.firebase.crashlytics resolved)
        public int InstallerAssets;    // FirebaseCrashlyticsInstaller assets in the project
    }

    /// <summary>The preflight decisions, engine-free so every row is a unit test. Empty == fine. The Firebase config files
    /// (plist / google-services.json) are pf-firebase-rc's preflight; every Firebase module reads the same ones.</summary>
    public static class CrashlyticsPreflightRules
    {
        public static IReadOnlyList<string> Check(CrashlyticsBuildFacts f)
        {
            var problems = new List<string>();
            if (f == null || !f.MobileTarget) return problems;

            if (!f.SdkCompiled)
                problems.Add("com.google.firebase.crashlytics is not installed (PF_FIREBASE_CRASHLYTICS unset): crashes would not be reported. Put the .tgz in GooglePackages/ and add it to Packages/manifest.json.");
            if (f.InstallerAssets == 0)
                problems.Add("No FirebaseCrashlyticsInstaller asset exists: the framework's CrashLog node still runs and nothing reaches Crashlytics. Create one and drop it into RootLifetimeScope ▸ SKU game installers.");
            else if (f.InstallerAssets > 1)
                problems.Add($"{f.InstallerAssets} FirebaseCrashlyticsInstaller assets exist; keep exactly one.");
            return problems;
        }
    }
}
