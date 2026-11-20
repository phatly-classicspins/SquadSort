using System;
using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;
using ClassicSpins.PrototypeFramework.Editor.Modules;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.Editor
{
    internal sealed class AdjustAnalyticsPreflight : IModulePreflight
    {
        public string ModuleId => AdjustAnalyticsBuildRules.ModuleId;
        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            var facts = new AdjustPreflightFacts { SdkAvailable = SdkAvailable };
            try
            {
                var installers = ModuleBuildFacts.SlottedInstallers();
                facts.InPlay = DefineArmed || (PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>())
                    .Any(p => p != null && p.name == "com.adjust.sdk")
                    || installers.Any(i => i != null && i.GetType().FullName == AdjustAnalyticsBuildRules.InstallerType);
                if (!facts.InPlay) return Array.Empty<ModulePreflightProblem>();
                facts.BundledDefaults = ModuleBuildFacts.BundledConfigDefaults(installers, facts.Notes);
            }
            catch (Exception ex)
            {
                return new[] { new ModulePreflightProblem("Adjust preflight could not gather build facts: " + ex.Message) };
            }
            return AdjustAnalyticsBuildRules.Evaluate(facts).Select(p => new ModulePreflightProblem(p)).ToArray();
        }
        private static bool SdkAvailable => DefineArmed && AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetType("ClassicSpins.PrototypeFramework.Analytics.Adjust.Sdk.PfModuleMarker_PF_ADJUST", false) != null);
        private static bool DefineArmed
        {
            get
            {
#if PF_ADJUST
                return true;
#else
                return false;
#endif
            }
        }
    }
}
