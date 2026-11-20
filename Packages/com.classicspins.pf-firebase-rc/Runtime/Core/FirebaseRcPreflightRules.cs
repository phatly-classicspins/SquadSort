using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.FirebaseRc
{
    /// <summary>The facts the Editor role gathers before a build.</summary>
    public sealed class FirebaseRcBuildFacts
    {
        public bool IosTarget, AndroidTarget;
        public bool SdkCompiled;              // PF_FIREBASE_RC armed (com.google.firebase.remote-config resolved)
        public int InstallerAssets;           // FirebaseRemoteConfigInstaller assets in the project
        public bool IosConfigFound;           // a GoogleService-Info.plist under Assets/
        public string IosConfigBundleId;      // its BUNDLE_ID
        public string IosBundleId;            // Player Settings
        public bool AndroidConfigFound;       // a google-services.json under Assets/
        public bool AndroidConfigHasPackage;  // it lists the Player Settings package
        public string AndroidPackage;
    }

    /// <summary>The preflight decisions, engine-free so every row is a unit test. Empty == fine.</summary>
    public static class FirebaseRcPreflightRules
    {
        public static IReadOnlyList<string> Check(FirebaseRcBuildFacts f)
        {
            var problems = new List<string>();
            if (f == null || !(f.IosTarget || f.AndroidTarget)) return problems;

            if (!f.SdkCompiled)
                problems.Add("com.google.firebase.remote-config is not installed (PF_FIREBASE_RC unset): every fetch would be refused.");
            if (f.InstallerAssets == 0)
                problems.Add("No FirebaseRemoteConfigInstaller asset exists: the framework's fake source is still in use. Create one and drop it into RootLifetimeScope ▸ SKU game installers.");
            else if (f.InstallerAssets > 1)
                problems.Add($"{f.InstallerAssets} FirebaseRemoteConfigInstaller assets exist; keep exactly one.");

            if (f.IosTarget)
            {
                if (!f.IosConfigFound)
                    problems.Add("GoogleService-Info.plist is missing under Assets/: download it from the Firebase console (iOS app).");
                else if (f.IosConfigBundleId != f.IosBundleId)
                    problems.Add($"GoogleService-Info.plist is for '{f.IosConfigBundleId}' but the iOS bundle id is '{f.IosBundleId}'.");
            }
            if (f.AndroidTarget)
            {
                if (!f.AndroidConfigFound)
                    problems.Add("google-services.json is missing under Assets/: add an Android app to the Firebase project and download it.");
                else if (!f.AndroidConfigHasPackage)
                    problems.Add($"google-services.json has no client for the Android package '{f.AndroidPackage}'.");
            }
            return problems;
        }
    }
}
