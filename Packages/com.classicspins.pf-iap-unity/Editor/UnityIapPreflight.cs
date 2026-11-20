using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Editor.Modules;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Editor
{
    /// <summary>
    /// The module's build preflight: gathers the facts <see cref="UnityIapPreflightRules"/> decides from and
    /// maps each named problem to a fix action. Discovered by the framework's one build preprocessor; this
    /// module ships no build hook of its own.
    /// </summary>
    /// <remarks>
    /// Fact gathering only — every decision is in the engine-free rules, proven headless, and nothing here
    /// writes a scene, prefab, asset, setting or define. Cheap facts first: when the package, the define and
    /// the installer slot all say the module is not in play, the check returns at once (SKU #0).
    /// </remarks>
    internal sealed class UnityIapPreflight : IModulePreflight
    {
        public string ModuleId => UnityIapPreflightRules.ModuleId;

        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            var sdk = (PackageInfo.GetAllRegisteredPackages() ?? new PackageInfo[0])
                .FirstOrDefault(p => p != null && p.name == UnityIapPreflightRules.SdkPackage);
            var slotted = ModuleBuildFacts.SlottedInstallers()
                .Where(i => i != null && i.GetType().FullName == UnityIapPreflightRules.InstallerType)
                .ToArray();

            var facts = new UnityIapPreflightFacts
            {
                Target = UnityIapPreflightRules.TargetOf(target.ToString()),
                SdkPackageVersion = sdk != null ? sdk.version : null,
                DefineArmed = DefineArmed,
                InstallerSlotted = slotted.Length > 0,
            };
            if (facts.SdkPackageVersion == null && !facts.DefineArmed && !facts.InstallerSlotted)
                return new ModulePreflightProblem[0];

            facts.CatalogPresent = AssetDatabase.FindAssets("products t:TextAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(path => path != null && path.EndsWith("/" + UnityIapPreflightRules.CatalogPath, System.StringComparison.Ordinal));
            facts.CloudProjectId = CloudProjectSettings.projectId ?? "";

            // Advisory only: logged, never a build failure.
            foreach (var warning in UnityIapPreflightRules.Warnings(facts)) Debug.LogWarning(warning);

            return UnityIapPreflightRules.Evaluate(facts).Select(p => ToProblem(p, slotted)).ToArray();
        }

        private static bool DefineArmed
        {
            get
            {
#if PF_UNITY_IAP
                return true;
#else
                return false;
#endif
            }
        }

        private static ModulePreflightProblem ToProblem(UnityIapPreflightProblem p, ScriptableObject[] slotted)
        {
            var message = p.Code + " — " + p.Message;
            switch (p.Code)
            {
                case UnityIapPreflightRules.DefineWithoutPackage:
                case UnityIapPreflightRules.PackageWithoutDefine:
                    return new ModulePreflightProblem(message, "Open Package Manager",
                        () => UnityEditor.PackageManager.UI.Window.Open(UnityIapPreflightRules.SdkPackage));
                case UnityIapPreflightRules.InstallerWithoutDefine:
                    return new ModulePreflightProblem(message, "Ping installer", () =>
                    {
                        if (slotted.Length == 0) return;
                        Selection.activeObject = slotted[0];
                        EditorGUIUtility.PingObject(slotted[0]);
                    });
                default:
                    // catalog-missing: the catalog is the SKU's own data file — no single place to open.
                    return new ModulePreflightProblem(message);
            }
        }
    }
}
