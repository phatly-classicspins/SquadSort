using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Editor.Modules;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Editor
{
    /// <summary>
    /// The module's build preflight: gathers the facts <see cref="MaxAdsPreflightRules"/> decides from and
    /// maps each named problem to a fix action. Discovered by the framework's one build preprocessor; this
    /// module ships no build hook of its own.
    /// </summary>
    /// <remarks>
    /// <para><b>Fact gathering only.</b> Every decision is in the engine-free rules, proven headless. Nothing
    /// here writes a scene, prefab, asset, setting or define; only a fix action a human clicks may change
    /// the project (the Android resolve).</para>
    /// <para><b>Cheap facts first.</b> Package version, the define and the installer slot are read first;
    /// when none of them puts the module in play the check returns at once (SKU #0).</para>
    /// <para><b>No create-on-miss.</b> MAX's settings singletons create and save an asset or file when they
    /// are first touched, so neither is touched: the settings asset is found through the AssetDatabase (under
    /// <c>#if PF_MAX</c>, the only place its type can be named), and the internal settings are read as a
    /// JSON file. The Android resolver is reached by reflection only — its assembly shape differs between
    /// distributions, and a hard reference would stop this role compiling where it is absent.</para>
    /// </remarks>
    internal sealed class MaxAdsPreflight : IModulePreflight
    {
        private const string ResolverTypeName = "GooglePlayServices.PlayServicesResolver";
        private const string ResolveMethodName = "ResolveSync";
        private const string ResolverPackage = "com.google.external-dependency-manager";
        private const string ResolutionRecordPath = "ProjectSettings/AndroidResolverDependencies.xml";
        private const string InternalSettingsPath = "ProjectSettings/AppLovinInternalSettings.json";
        private const string MainTemplatePath = "Assets/Plugins/Android/mainTemplate.gradle";

        public string ModuleId => MaxAdsPreflightRules.ModuleId;

        public IEnumerable<ModulePreflightProblem> Check(BuildTarget target)
        {
            var packages = (PackageInfo.GetAllRegisteredPackages() ?? new PackageInfo[0])
                .Where(p => p != null && !string.IsNullOrEmpty(p.name))
                .ToArray();
            var sdk = packages.FirstOrDefault(p => p.name == MaxAdsPreflightRules.SdkPackage);
            var installers = ModuleBuildFacts.SlottedInstallers();
            var slotted = installers.Where(i => i != null && i.GetType().FullName == MaxAdsPreflightRules.InstallerType).ToArray();

            var facts = new MaxAdsPreflightFacts
            {
                Target = MaxAdsPreflightRules.TargetOf(target.ToString()),
                SdkPackageVersion = sdk != null ? sdk.version : null,
                DefineArmed = DefineArmed,
                InstallerSlotted = slotted.Length > 0,
            };
            if (facts.SdkPackageVersion == null && !facts.DefineArmed && !facts.InstallerSlotted)
                return new ModulePreflightProblem[0];

            var root = ProjectRoot();
            facts.SdkKey = ReadSdkKey();
            MaxAdsPreflightRules.ParseConsentSettings(ReadIfExists(root, InternalSettingsPath), facts);
            facts.UsedPlacements = ModuleBuildFacts.CodegenManifestValues(MaxAdsPreflightRules.PlacementManifest);
            var notes = new List<string>();
            if (facts.UsedPlacements != null && facts.UsedPlacements.Count > 0)
                facts.BundledConfig = ModuleBuildFacts.BundledConfigDefaults(installers, notes);
            facts.ResolverPresent = FindType(ResolverTypeName) != null;
            facts.ResolvedAndroidPackages = MaxAdsPreflightRules.ParseResolvedAndroidPackages(ReadIfExists(root, ResolutionRecordPath));
            facts.MainTemplateGradle = ReadIfExists(root, MainTemplatePath);
            facts.ResolvedPackageIds = packages.Select(p => p.name).ToArray();

            // Advisory only: logged, never a build failure.
            foreach (var warning in MaxAdsPreflightRules.Warnings(facts)) Debug.LogWarning(warning);

            return MaxAdsPreflightRules.Evaluate(facts).Select(p => ToProblem(p, slotted, notes)).ToArray();
        }

        private static bool DefineArmed
        {
            get
            {
#if PF_MAX
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>The configured SDK key; null when the settings type cannot be named (define unarmed).</summary>
        private static string ReadSdkKey()
        {
#if PF_MAX
            // The serialized field of the asset found, never the SdkKey getter: that routes through MAX's
            // create-on-miss Instance, which may be a different asset (or create one).
            foreach (var guid in AssetDatabase.FindAssets("t:AppLovinSettings"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<AppLovinSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings == null) continue;
                using (var serialized = new SerializedObject(settings))
                {
                    var key = serialized.FindProperty("sdkKey");
                    return key != null && key.propertyType == SerializedPropertyType.String ? key.stringValue ?? "" : "";
                }
            }
            return "";
#else
            return null;
#endif
        }

        private static ModulePreflightProblem ToProblem(MaxAdsPreflightProblem p, ScriptableObject[] slotted, IList<string> probeNotes)
        {
            var message = p.Code + " — " + p.Message;
            // A unit id may read as missing only because the bundled config could not be probed — say so.
            if (p.Code == MaxAdsPreflightRules.UnitId && probeNotes != null && probeNotes.Count > 0)
                message += " (bundled-config probe: " + string.Join("; ", probeNotes) + ")";
            switch (p.Code)
            {
                case MaxAdsPreflightRules.DefineWithoutPackage:
                case MaxAdsPreflightRules.PackageWithoutDefine:
                    return new ModulePreflightProblem(message, "Open Package Manager",
                        () => UnityEditor.PackageManager.UI.Window.Open(MaxAdsPreflightRules.SdkPackage));
                case MaxAdsPreflightRules.Edm4uMissing:
                    return new ModulePreflightProblem(message, "Open Package Manager",
                        () => UnityEditor.PackageManager.UI.Window.Open(ResolverPackage));
                case MaxAdsPreflightRules.InstallerWithoutDefine:
                    return new ModulePreflightProblem(message, "Ping installer", () =>
                    {
                        if (slotted.Length == 0) return;
                        Selection.activeObject = slotted[0];
                        EditorGUIUtility.PingObject(slotted[0]);
                    });
                case MaxAdsPreflightRules.SdkKeyEmpty:
                case MaxAdsPreflightRules.ConsentFlowDisabled:
                case MaxAdsPreflightRules.ConsentPrivacyUrl:
                    return new ModulePreflightProblem(message, "Open Integration Manager",
                        () => EditorApplication.ExecuteMenuItem(MaxAdsPreflightRules.SettingsMenu));
                case MaxAdsPreflightRules.AndroidUnresolved:
                case MaxAdsPreflightRules.AdapterCount:
                    return new ModulePreflightProblem(message, "Resolve Android dependencies", ResolveAndroid);
                case MaxAdsPreflightRules.GradleDepsTokenMissing:
                    return new ModulePreflightProblem(message, "Open mainTemplate.gradle", () =>
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(MainTemplatePath);
                        if (asset != null) AssetDatabase.OpenAsset(asset);
                        else EditorUtility.RevealInFinder(MainTemplatePath);
                    });
                default:
                    // unit-id: the value belongs in the SKU's bundled config — no single place to open.
                    return new ModulePreflightProblem(message);
            }
        }

        /// <summary>The resolver's synchronous forced resolve, by reflection only.</summary>
        private static void ResolveAndroid()
        {
            var type = FindType(ResolverTypeName);
            var resolve = type?.GetMethod(ResolveMethodName, BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(bool) }, null);
            if (resolve == null)
            {
                Debug.LogWarning("[" + MaxAdsPreflightRules.ModuleId + "] " + ResolverTypeName + "." + ResolveMethodName
                                 + "(bool) not found — install " + ResolverPackage + ".");
                return;
            }
            resolve.Invoke(null, new object[] { true });
        }

        private static string ProjectRoot() => Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;

        private static string ReadIfExists(string root, string relative)
        {
            if (string.IsNullOrEmpty(root)) return null;
            var path = Path.Combine(root, relative);
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception) { return null; }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); }
                catch (Exception) { continue; }
                if (type != null) return type;
            }
            return null;
        }
    }
}
