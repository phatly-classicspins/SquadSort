using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using ClassicSpins.PrototypeFramework.Editor.Modules;

namespace ClassicSpins.PrototypeFramework.FirebaseCrashlytics.Editor
{
    /// <summary>
    /// Gathers the build facts for <see cref="CrashlyticsPreflightRules"/>; the framework's one
    /// <c>ModulePreflightBuildHook</c> runs it and fails the build on any problem.
    /// </summary>
    public sealed class CrashlyticsModulePreflight : IModulePreflight
    {
        public string ModuleId => CrashlyticsModule.ModuleId;

        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            var facts = new CrashlyticsBuildFacts
            {
                MobileTarget = target == BuildTarget.Android || target == BuildTarget.iOS,
#if PF_FIREBASE_CRASHLYTICS
                SdkCompiled = true,
#else
                SdkCompiled = false,
#endif
                InstallerAssets = AssetDatabase.FindAssets("t:" + nameof(FirebaseCrashlyticsInstaller)).Length,
            };
            return CrashlyticsPreflightRules.Check(facts).Select(m => new ModulePreflightProblem(m));
        }
    }
}
