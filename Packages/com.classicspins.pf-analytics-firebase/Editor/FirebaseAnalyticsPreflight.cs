using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using ClassicSpins.PrototypeFramework.Editor.Modules;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Editor
{
    internal sealed class FirebaseAnalyticsPreflight : IModulePreflight
    {
        public string ModuleId => FirebaseAnalyticsBuildRules.ModuleId;
        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            var facts = new FirebasePreflightFacts
            {
                Target = target == BuildTarget.Android ? FirebaseBuildTarget.Android : target == BuildTarget.iOS ? FirebaseBuildTarget.Ios : FirebaseBuildTarget.Other,
                DefineArmed = DefineArmed,
                InPlay = IsInPlay,
                AndroidMinSdk = (int)PlayerSettings.Android.minSdkVersion,
            };
            if (!facts.InPlay) return Array.Empty<ModulePreflightProblem>();
            var assetFiles = Directory.Exists("Assets") ? Directory.GetFiles("Assets", "*", SearchOption.AllDirectories) : Array.Empty<string>();
            facts.ServiceFilePresent = assetFiles.Any(p => FirebaseAnalyticsBuildRules.IsServiceFile(p, facts.Target));
            foreach (var path in assetFiles.Where(p => FirebaseAnalyticsBuildRules.IsNativeFileCandidate(p, facts.Target)))
            {
                var content = File.ReadAllText(path);
                if (FirebaseAnalyticsBuildRules.IsCollectionConfiguration(path, content, facts.Target))
                    facts.NativeFiles.Add(new KeyValuePair<string, string>(path, content));
            }
            return FirebaseAnalyticsBuildRules.Evaluate(facts).Select(p => new ModulePreflightProblem(p)).ToArray();
        }
        internal static bool IsInPlay => DefineArmed
            || (PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>()).Any(p => p != null && (p.name == "com.google.firebase.app" || p.name == "com.google.firebase.analytics"))
            || ModuleBuildFacts.SlottedInstallers().Any(i => i != null && i.GetType().FullName == FirebaseAnalyticsBuildRules.InstallerType);
        private static bool DefineArmed
        {
            get
            {
#if PF_FIREBASE
                return true;
#else
                return false;
#endif
            }
        }
    }
}
