using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.HeadlessTests
{
    /// <summary>Metadata and uncompiled role shape checks; these never claim SDK-enabled compilation.</summary>
    public sealed class ModuleShapeTests
    {
        private const string Base = "ClassicSpins.PrototypeFramework.Analytics.Adjust";
        private static string Read(string path) => File.ReadAllText(Path.Combine(TestPaths.Root, path));
        private static JObject Json(string path) => JObject.Parse(Read(path));
        private static string[] Imported() => Directory.GetFileSystemEntries(TestPaths.Root, "*", SearchOption.AllDirectories)
            .Where(p => !Path.GetRelativePath(TestPaths.Root, p).Split(Path.DirectorySeparatorChar).Any(s => s.EndsWith("~") || s.StartsWith("."))).ToArray();
        [Test] public void Metadata_pins_framework_only_and_registry_requirements_with_additive_ports_no_boot_node()
        {
            var p = Json("package.json"); var m = Json("pf-module.json");
            Assert.That((string)p["name"], Is.EqualTo(AdjustAnalyticsBuildRules.ModuleId)); Assert.That((string)p["version"], Is.EqualTo("0.1.0"));
            Assert.That((JObject)p["dependencies"], Has.Count.EqualTo(1)); Assert.That((string)p["dependencies"]["com.classicspins.prototype-framework"], Is.EqualTo("0.9.0"));
            Assert.That((string)m["frameworkRange"], Is.EqualTo(">=0.9.0 <0.10.0")); Assert.That((string)m["schema"], Is.EqualTo("pf-module/1"));
            Assert.That((string)m["port"]["interface"], Is.EqualTo("ClassicSpins.PrototypeFramework.Application.IAnalyticsProvider"));
            Assert.That((string)m["port"]["mode"], Is.EqualTo("additive")); Assert.That(m["port"]["replacesBootNode"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((JArray)m["ports"], Has.Count.EqualTo(1)); Assert.That((string)m["ports"][0]["interface"], Is.EqualTo("ClassicSpins.PrototypeFramework.Application.IAnalyticsCommandTarget"));
            Assert.That((string)m["ports"][0]["mode"], Is.EqualTo("additive"));
            Assert.That((string)m["requires"]["dependencies"]["com.adjust.sdk"], Is.EqualTo("5.8.2"));
            Assert.That((string)m["requires"]["dependencies"]["com.google.external-dependency-manager"], Is.EqualTo("1.2.190"));
            Assert.That((JObject)m["requires"]["dependencies"], Has.Count.EqualTo(2));
            Assert.That(((JArray)m["requires"]["scopedRegistries"][0]["scopes"]).Select(x => (string)x), Is.EquivalentTo(new[] { "com.adjust", "com.google.external-dependency-manager" }));
            Assert.That((string)m["requires"]["scopedRegistries"][0]["url"], Is.EqualTo("https://package.openupm.com"));
            Assert.That(m["requires"]["manualAssemblies"], Is.Null); Assert.That(m["requires"]["manualPackages"], Is.Null);
        }
        [Test] public void Catalog_and_source_match_module_version_without_claiming_a_tag_exists()
        {
            var m = Json("pf-module.json"); var catalog = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.Framework, "Editor/Modules/modules.catalog.json")));
            var entry = ((JArray)catalog["modules"]).Single(e => (string)e["id"] == AdjustAnalyticsBuildRules.ModuleId);
            foreach (var key in new[] { "id", "displayName", "summary" }) Assert.That((string)entry[key], Is.EqualTo((string)m[key]));
            Assert.That((string)m["source"]["url"], Does.EndWith("#analytics-adjust/v" + (string)Json("package.json")["version"]));
        }
        [Test] public void Exactly_four_roles_with_only_Sdk_gated_and_matching_version_defines()
        {
            Assert.That(Imported().Count(p => p.EndsWith(".asmdef")), Is.EqualTo(4));
            var paths = new[] { "Runtime/Core/", "Runtime/Sdk/", "Runtime/", "Editor/" }; var suffixes = new[] { ".Core", ".Sdk", "", ".Editor" };
            for (int i = 0; i < paths.Length; i++)
            {
                var a = Json(paths[i] + Base + suffixes[i] + ".asmdef");
                Assert.That((string)a["name"], Is.EqualTo(Base + suffixes[i])); Assert.That((string)a["rootNamespace"], Is.EqualTo((string)a["name"]));
                Assert.That(((JArray)a["defineConstraints"]).Select(x => (string)x), Is.EqualTo(i == 1 ? new[] { "PF_ADJUST" } : Array.Empty<string>()));
                if (i == 0) { Assert.That((bool)a["noEngineReferences"], Is.True); continue; }
                Assert.That((string)a["versionDefines"][0]["name"], Is.EqualTo("com.adjust.sdk")); Assert.That((string)a["versionDefines"][0]["expression"], Is.EqualTo("5.8.2")); Assert.That((string)a["versionDefines"][0]["define"], Is.EqualTo("PF_ADJUST"));
            }
            var sdk = Json("Runtime/Sdk/" + Base + ".Sdk.asmdef"); Assert.That(((JArray)sdk["references"]).Select(x => (string)x), Does.Contain("AdjustSdk.Scripts"));
            Assert.That((bool)sdk["overrideReferences"], Is.False); Assert.That((JArray)sdk["precompiledReferences"], Is.Empty);
            Assert.That(Read("Runtime/Sdk/PfModuleMarker_PF_ADJUST.cs"), Does.Contain("internal static class PfModuleMarker_PF_ADJUST"));
        }
        [Test] public void Imported_files_have_unique_stable_metas_and_namespaces_equal_owning_assemblies()
        {
            var all = Imported().Where(p => !p.EndsWith(".meta")).ToArray(); Assert.That(all.Where(p => !File.Exists(p + ".meta")), Is.Empty);
            var guids = all.Select(p => Regex.Match(File.ReadAllText(p + ".meta"), @"guid: ([a-f0-9]{32})").Groups[1].Value).ToArray();
            Assert.That(guids.All(g => g.Length == 32), Is.True); Assert.That(guids.Distinct().Count(), Is.EqualTo(guids.Length));
            foreach (var path in all.Where(p => p.EndsWith(".cs")))
            {
                var source = Regex.Replace(File.ReadAllText(path), @"//[^\r\n]*", "");
                var rel = Path.GetRelativePath(TestPaths.Root, path).Replace('\\', '/');
                if (!rel.StartsWith("Runtime/Sdk/")) Assert.That(source, Does.Not.Contain("using AdjustSdk").And.Not.Contain("new AdjustConfig").And.Not.Contain("new AdjustEvent").And.Not.Contain("new AdjustAdRevenue"), rel);
                if (rel.StartsWith("Runtime/Core/") || rel.EndsWith("NullAdjustAnalyticsBridge.cs")) Assert.That(source, Does.Not.Contain("UnityEngine").And.Not.Contain("UnityEditor"), rel);
                var folder = Path.GetDirectoryName(path); while (Directory.GetFiles(folder, "*.asmdef").Length == 0) folder = Path.GetDirectoryName(folder);
                var asm = JObject.Parse(File.ReadAllText(Directory.GetFiles(folder, "*.asmdef").Single()));
                Assert.That(Regex.Match(source, @"namespace\s+([\w.]+)").Groups[1].Value, Is.EqualTo((string)asm["name"]), rel);
            }
        }
        [Test] public void Installer_and_editor_follow_existing_seams_and_sdk_shell_uses_pinned_API_shapes()
        {
            var installer = Read("Runtime/AdjustAnalyticsInstaller.cs");
            Assert.That(installer, Does.Contain(".As<IAnalyticsProvider>().As<IAnalyticsCommandTarget>().AsSelf()"));
            Assert.That(installer, Does.Contain("#if PF_ADJUST").And.Contain("new NullAdjustAnalyticsBridge(log)"));
            Assert.That(installer, Does.Not.Contain("BootNode").And.Not.Contain("RegisterInstance"));
            var preflight = Read("Editor/AdjustAnalyticsPreflight.cs"); Assert.That(preflight, Does.Contain("ModuleBuildFacts.SlottedInstallers").And.Contain("ModuleBuildFacts.BundledConfigDefaults").And.Contain("facts.Notes").And.Contain("AdjustAnalyticsBuildRules.Evaluate"));
            var shell = Read("Runtime/Sdk/AdjustAnalyticsBridge.cs");
            Assert.That(shell, Does.Contain("AddGranularOption(\"google_dma\", \"ad_personalization\"").And.Contain("AddGranularOption(\"google_dma\", \"ad_user_data\""));
            Assert.That(shell, Does.Not.Contain("\"eea\"")); Assert.That(shell, Does.Contain("VendorAdjust.AddGlobalCallbackParameter").And.Contain("VendorAdjust.AddGlobalPartnerParameter").And.Contain("VendorAdjust.GdprForgetMe"));
            foreach (var t in new[] { typeof(AdjustAnalyticsProvider), typeof(NullAdjustAnalyticsBridge) }) Assert.That(t.GetConstructors().Single().GetParameters().Any(p => p.IsOptional || p.HasDefaultValue), Is.False);
        }
    }
}
