using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
{
    /// <summary>Text-shape assertions for the uncompiled installer/SDK/Editor, never a claim of SDK compilation.</summary>
    [TestFixture]
    public sealed class ModuleShapeTests
    {
        private static string Read(string path) => File.ReadAllText(Path.Combine(TestPaths.Root, path));
        private static JObject Json(string path) => JObject.Parse(Read(path));
        private static string Base => "ClassicSpins.PrototypeFramework.Analytics.Firebase";
        private static IEnumerable<string> Imported()
            => Directory.GetFileSystemEntries(TestPaths.Root, "*", SearchOption.AllDirectories).Where(p => !Path.GetRelativePath(TestPaths.Root, p).Split(Path.DirectorySeparatorChar).Any(s => s.EndsWith("~") || s.StartsWith(".")));
        private static string WithoutComments(string source) => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//.*", "");
        [Test] public void Metadata_pins_only_framework_and_manual_sdk_requirements_with_additive_ports_and_no_boot_node()
        {
            var package = Json("package.json"); var module = Json("pf-module.json");
            Assert.That((string)package["name"], Is.EqualTo(FirebaseAnalyticsBuildRules.ModuleId));
            Assert.That((string)package["version"], Is.EqualTo("0.1.0"));
            Assert.That((JObject)package["dependencies"], Has.Count.EqualTo(1));
            Assert.That((string)package["dependencies"]["com.classicspins.prototype-framework"], Is.EqualTo("0.9.0"));
            Assert.That((string)module["frameworkRange"], Is.EqualTo(">=0.9.0 <0.10.0"));
            Assert.That((string)module["port"]["interface"], Is.EqualTo("ClassicSpins.PrototypeFramework.Application.IAnalyticsProvider"));
            Assert.That((string)module["port"]["mode"], Is.EqualTo("additive"));
            Assert.That(module["port"]["replacesBootNode"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((string)module["ports"][0]["interface"], Is.EqualTo("ClassicSpins.PrototypeFramework.Application.IAnalyticsCommandTarget"));
            Assert.That((string)module["ports"][0]["mode"], Is.EqualTo("additive"));
            Assert.That((JArray)module["ports"], Has.Count.EqualTo(1));
            Assert.That((string)module["requires"]["dependencies"]["com.google.external-dependency-manager"], Is.EqualTo("1.2.190"));
            Assert.That((string)module["requires"]["scopedRegistries"][0]["url"], Is.EqualTo("https://package.openupm.com"));
            var manual = (JArray)module["requires"]["manualPackages"];
            Assert.That(manual.Select(p => (string)p["package"]), Is.EquivalentTo(new[] { "com.google.firebase.app", "com.google.firebase.analytics" }));
            Assert.That(manual.All(p => (string)p["version"] == "14.1.0"), Is.True);
            Assert.That(module["requires"]["manualAssemblies"], Is.Null);
        }
        [Test] public void Catalog_matches_module_offer_and_source_ref_matches_version_without_claiming_tag_exists()
        {
            var module = Json("pf-module.json");
            var catalog = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.Framework, "Editor/Modules/modules.catalog.json")));
            var entry = ((JArray)catalog["modules"]).Single(e => (string)e["id"] == FirebaseAnalyticsBuildRules.ModuleId);
            foreach (var key in new[] { "id", "displayName", "summary" }) Assert.That((string)entry[key], Is.EqualTo((string)module[key]));
            Assert.That((string)module["source"]["url"], Does.EndWith("#analytics-firebase/v" + (string)Json("package.json")["version"]));
        }
        [Test] public void Four_roles_gate_only_Sdk_and_arm_identical_version_defines_on_branching_roles()
        {
            var paths = new[] { "Runtime/Core/", "Runtime/Sdk/", "Runtime/", "Editor/" };
            var suffixes = new[] { ".Core", ".Sdk", "", ".Editor" };
            var asmdefs = Imported().Where(p => p.EndsWith(".asmdef")).ToArray(); Assert.That(asmdefs, Has.Length.EqualTo(4));
            JToken versionDefines = null;
            for (int i = 0; i < paths.Length; i++)
            {
                var asmdef = Json(paths[i] + Base + suffixes[i] + ".asmdef");
                Assert.That((string)asmdef["name"], Is.EqualTo(Base + suffixes[i]));
                Assert.That((string)asmdef["rootNamespace"], Is.EqualTo((string)asmdef["name"]));
                Assert.That(((JArray)asmdef["defineConstraints"]).Select(t => (string)t), Is.EqualTo(i == 1 ? new[] { "PF_FIREBASE" } : Array.Empty<string>()));
                if (i == 0) { Assert.That((bool)asmdef["noEngineReferences"], Is.True); continue; }
                if (versionDefines == null) versionDefines = asmdef["versionDefines"];
                Assert.That(JToken.DeepEquals(asmdef["versionDefines"], versionDefines), Is.True);
                Assert.That((string)asmdef["versionDefines"][0]["name"], Is.EqualTo("com.google.firebase.analytics"));
                Assert.That((string)asmdef["versionDefines"][0]["expression"], Is.EqualTo("14.1.0"));
                Assert.That((string)asmdef["versionDefines"][0]["define"], Is.EqualTo("PF_FIREBASE"));
            }
            var sdk = Json("Runtime/Sdk/" + Base + ".Sdk.asmdef");
            Assert.That((bool)sdk["overrideReferences"], Is.True);
            Assert.That(((JArray)sdk["precompiledReferences"]).Select(t => (string)t), Is.EquivalentTo(new[] { "Firebase.App.dll", "Firebase.Analytics.dll", "Firebase.Platform.dll" }));
            Assert.That(Read("Runtime/Sdk/PfModuleMarker_PF_FIREBASE.cs"), Does.Contain("internal static class PfModuleMarker_PF_FIREBASE"));
        }
        [Test] public void Installer_registers_one_singleton_factory_as_both_ports_without_boot_node_or_optional_constructors()
        {
            var source = WithoutComments(Read("Runtime/FirebaseAnalyticsInstaller.cs"));
            Assert.That(Regex.Matches(source, "new FirebaseAnalyticsProvider\\("), Has.Count.EqualTo(1));
            Assert.That(Regex.IsMatch(source, @"builder\.Register\(r => new FirebaseAnalyticsProvider\([\s\S]*?Dispatch\), Lifetime\.Singleton\)\s*\.As<IAnalyticsProvider>\(\)\.As<IAnalyticsCommandTarget>\(\)\.AsSelf\(\)"), Is.True);
            Assert.That(source, Does.Contain("#if PF_FIREBASE").And.Contain("new NullFirebaseAnalyticsBridge(log)"));
            Assert.That(source, Does.Not.Contain("BootNode").And.Not.Contain("RegisterInstance"));
            foreach (var type in new[] { typeof(FirebaseAnalyticsProvider), typeof(NullFirebaseAnalyticsBridge) })
            {
                Assert.That(type.GetConstructors(), Has.Length.EqualTo(1));
                Assert.That(type.GetConstructors()[0].GetParameters().Any(p => p.HasDefaultValue || p.IsOptional), Is.False);
            }
        }
        [Test] public void Only_Sdk_names_runtime_vendor_types_and_Core_and_Null_remain_engine_free()
        {
            foreach (var path in Imported().Where(p => p.EndsWith(".cs")))
            {
                var rel = Path.GetRelativePath(TestPaths.Root, path).Replace('\\', '/');
                var source = WithoutComments(File.ReadAllText(path));
                if (!rel.StartsWith("Runtime/Sdk/")) Assert.That(source, Does.Not.Contain("global::Firebase.").And.Not.Contain("using Firebase."), rel);
                if (rel.StartsWith("Runtime/Core/") || rel.EndsWith("NullFirebaseAnalyticsBridge.cs"))
                    Assert.That(source, Does.Not.Contain("UnityEngine").And.Not.Contain("UnityEditor").And.Not.Contain("System.Text.Json").And.Not.Contain("#if UNITY_"), rel);
                var dir = Path.GetDirectoryName(path);
                while (Directory.GetFiles(dir, "*.asmdef").Length == 0) dir = Path.GetDirectoryName(dir);
                var asm = JObject.Parse(File.ReadAllText(Directory.GetFiles(dir, "*.asmdef").Single()));
                var ns = Regex.Match(source, @"namespace\s+([\w.]+)").Groups[1].Value;
                Assert.That(ns, Is.EqualTo((string)asm["name"]), rel);
            }
        }
        [Test] public void Every_imported_file_and_folder_has_a_stable_unique_meta()
        {
            var all = Imported().Where(p => !p.EndsWith(".meta")).ToArray();
            Assert.That(all.Where(p => !File.Exists(p + ".meta")), Is.Empty);
            var guids = all.Select(p => Regex.Match(File.ReadAllText(p + ".meta"), @"guid: ([a-f0-9]{32})").Groups[1].Value).ToArray();
            Assert.That(guids.All(g => g.Length == 32), Is.True); Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length));
        }
        [Test] public void Editor_only_gathers_facts_and_permitted_postbuild_transforms_have_explicit_order()
        {
            var preflight = Read("Editor/FirebaseAnalyticsPreflight.cs"); var post = Read("Editor/FirebaseAnalyticsPostBuild.cs");
            Assert.That(preflight, Does.Contain("IModulePreflight").And.Contain("FirebaseAnalyticsBuildRules.Evaluate").And.Contain("PlayerSettings.Android.minSdkVersion"));
            Assert.That(post, Does.Contain("IPostprocessBuildWithReport").And.Contain("IPostGenerateGradleAndroidProject").And.Contain("callbackOrder => 10000"));
            Assert.That(post, Does.Contain("FirebaseAnalyticsExport.Android(path)").And.Contain("FirebaseAnalyticsExport.Ios(report.summary.outputPath)").And.Contain("BuildFailedException"));
            var sdk = Read("Runtime/Sdk/FirebaseAnalyticsBridge.cs");
            Assert.That(sdk, Does.Contain("CheckAndFixDependenciesAsync").And.Contain("UniTask.SwitchToMainThread"));
            Assert.That(Read("Runtime/FirebaseAnalyticsInstaller.cs"), Does.Contain("PlayerLoopHelper.IsMainThread").And.Contain("UniTask.Post"));
        }
    }
}
