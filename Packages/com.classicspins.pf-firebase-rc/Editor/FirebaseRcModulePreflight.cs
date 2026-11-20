using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using ClassicSpins.PrototypeFramework.Editor.Modules;

namespace ClassicSpins.PrototypeFramework.FirebaseRc.Editor
{
    /// <summary>
    /// Gathers the build facts for <see cref="FirebaseRcPreflightRules"/>; the framework's one
    /// <c>ModulePreflightBuildHook</c> runs it and fails the build on any problem.
    /// </summary>
    public sealed class FirebaseRcModulePreflight : IModulePreflight
    {
        public string ModuleId => FirebaseKeys.ModuleId;

        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            string plist = FindAsset("GoogleService-Info", ".plist");
            string json = FindAsset("google-services", ".json");
            string androidPackage = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);

            var facts = new FirebaseRcBuildFacts
            {
                IosTarget = target == BuildTarget.iOS,
                AndroidTarget = target == BuildTarget.Android,
#if PF_FIREBASE_RC
                SdkCompiled = true,
#else
                SdkCompiled = false,
#endif
                InstallerAssets = AssetDatabase.FindAssets("t:" + nameof(FirebaseRemoteConfigInstaller)).Length,
                IosConfigFound = plist != null,
                IosConfigBundleId = plist == null ? null : PlistString(File.ReadAllText(plist), "BUNDLE_ID"),
                IosBundleId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS),
                AndroidConfigFound = json != null,
                AndroidConfigHasPackage = json != null && File.ReadAllText(json).Contains("\"package_name\": \"" + androidPackage + "\""),
                AndroidPackage = androidPackage,
            };
            var problems = FirebaseRcPreflightRules.Check(facts);
            // SKU-requested local test-build workaround. Keep installer, SDK and ID checks.
            bool skip = System.Environment.GetEnvironmentVariable("CARD_SLOT_TEST_SKIP_SERVICE_SETUP") == "1";
            return problems.Where(m =>
            {
                if (!skip || !m.StartsWith("google-services.json is missing under Assets/:", System.StringComparison.Ordinal))
                    return true;
                UnityEngine.Debug.LogWarning("[CardSlot test build] Skipped Firebase preflight: " + m);
                return false;
            }).Select(m => new ModulePreflightProblem(m));
        }

        private static string FindAsset(string name, string extension) =>
            AssetDatabase.FindAssets(name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.StartsWith("Assets/") && Path.GetFileName(p) == name + extension);

        private static string PlistString(string plist, string key)
        {
            var m = Regex.Match(plist, "<key>" + Regex.Escape(key) + @"</key>\s*<string>([^<]*)</string>");
            return m.Success ? m.Groups[1].Value : null;
        }
    }
}
