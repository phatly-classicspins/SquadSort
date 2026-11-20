using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>
    /// The module's shape, held where the kernel cannot hold it: constructor surfaces by reflection, and the
    /// four-role layout, the <c>#if PF_UNITY_IAP</c> edge and the metadata by source scan. The <c>Sdk</c> and
    /// <c>Editor</c> roles are never compiled here, so everything about them is read as text.
    /// </summary>
    [TestFixture]
    public sealed class ModuleShapeTests
    {
        private static string Root => TestSupport.ModuleRoot;
        private static string Runtime => Path.Combine(Root, "Runtime");
        private static string EditorRole => Path.Combine(Root, "Editor");

        private static string Rel(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');

        /// <summary>Every source Unity compiles for this module (never the <c>~</c> folders).</summary>
        private static IEnumerable<string> ModuleSources()
            => Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Rel(f).Split('/').Any(seg => seg.EndsWith("~", StringComparison.Ordinal) || seg.StartsWith(".", StringComparison.Ordinal)));

        private static JObject Json(string path) => JObject.Parse(File.ReadAllText(path));
        private static JObject PfModule => Json(Path.Combine(Root, "pf-module.json"));
        private static string Define => (string)PfModule["define"];

        private static string StripComments(string source)
            => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//.*", "");

        private static string InstallerSource => File.ReadAllText(Path.Combine(Runtime, "UnityIapInstaller.cs"));

        // ── constructors ───────────────────────────────────────────────────────────────────────────────

        private static readonly Type[] ContainerResolved = { typeof(UnityIapPurchaseService), typeof(UnityIapInitNode), typeof(NullUnityIapBridge) };

        [TestCaseSource(nameof(ContainerResolved))]
        public void Constructor_is_single_and_defaults_nothing(Type type)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(ctors, Has.Length.EqualTo(1), type.Name + " has exactly one public constructor");
            Assert.That(ctors[0].GetParameters().Where(p => p.HasDefaultValue || p.IsOptional).Select(p => p.Name), Is.Empty,
                type.Name + ": VContainer resolves every parameter regardless of a default");
        }

        [Test]
        public void The_service_takes_the_framework_catalog_interpreter_and_funnel_never_a_store_type()
        {
            var types = typeof(UnityIapPurchaseService).GetConstructors()[0].GetParameters().Select(p => p.ParameterType.Name);
            Assert.That(types, Is.EqualTo(new[]
            {
                "IUnityIapBridge", "ICatalogSource", "GrantInterpreter", "IEntitlementService", "IAnalytics", "ILog",
                "UnityIapPlatform", "Func`3",
            }));
        }

        [Test]
        public void The_bridge_carries_primitives_only()
        {
            var allowed = new List<Type>
            {
                typeof(string), typeof(bool), typeof(string[]), typeof(bool[]), typeof(void), typeof(Action<bool, string>),
                typeof(Cysharp.Threading.Tasks.UniTask<string>),
            };

            // UnityIapStoreProduct is the one struct allowed across: a readonly value type whose every public member
            // is a get-only string or bool — so it carries no Unity IAP type.
            var product = typeof(UnityIapStoreProduct);
            Assert.That(product.IsValueType, Is.True);
            Assert.That(product.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute"), Is.True);
            Assert.That(product.GetProperties().Where(p => (p.PropertyType != typeof(string) && p.PropertyType != typeof(bool)) || p.CanWrite)
                .Select(p => p.Name), Is.Empty);
            Assert.That(product.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
            allowed.Add(typeof(UnityIapStoreProduct[]));

            var offenders = new List<string>();
            foreach (var m in typeof(IUnityIapBridge).GetMethods().Where(m => !m.IsSpecialName))
            {
                if (!allowed.Contains(m.ReturnType)) offenders.Add(m.Name);
                offenders.AddRange(m.GetParameters().Where(p => !allowed.Contains(p.ParameterType)).Select(p => m.Name + "(" + p.Name + ")"));
            }
            foreach (var e in typeof(IUnityIapBridge).GetEvents())
                offenders.AddRange(e.EventHandlerType.GetGenericArguments().Where(t => !allowed.Contains(t)).Select(t => e.Name));
            Assert.That(offenders, Is.Empty);
        }

        // ── source scans ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Only_Sdk_UnityIapBridge_names_Unity_IAP_or_Unity_Gaming_Services()
        {
            var sdk = new Regex(@"\busing\s+(UnityEngine\.Purchasing|Unity\.Services)\b|\b(StoreController|UnityIAPServices|UnityServices|PendingOrder)\b");
            var naming = ModuleSources().Where(f => sdk.IsMatch(StripComments(File.ReadAllText(f)))).Select(Rel).ToArray();
            Assert.That(naming, Is.EqualTo(new[] { "Runtime/Sdk/UnityIapBridge.cs" }));
        }

        [Test]
        public void Core_reads_no_Unity_define_and_takes_time_only_as_the_injected_delay()
        {
            var banned = new[]
            {
                new Regex(@"#if\s+UNITY_"), new Regex(@"UniTask\.Delay\b"), new Regex(@"\bTime\."), new Regex(@"\bUnityEngine\b"),
                new Regex(@"Split\(\s*','"), new Regex(@"Split\(\s*"",""\s*\)"),
            };
            var offenders = Directory.GetFiles(Path.Combine(Runtime, "Core"), "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => banned.Where(b => b.IsMatch(StripComments(File.ReadAllText(f)))).Select(b => Rel(f) + " ~ " + b))
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void Core_stays_Csharp9_and_netstandard21_clean()
        {
            var banned = new[]
            {
                new Regex(@"\brecord\b"), new Regex(@"\binit\s*;\s*\}"), new Regex(@"System\.Text\.Json"),
                new Regex(@"#nullable"), new Regex(@"\b(string|object)\?"),
            };
            var offenders = Directory.GetFiles(Path.Combine(Runtime, "Core"), "*.cs", SearchOption.AllDirectories)
                .Concat(new[] { Path.Combine(Runtime, "NullUnityIapBridge.cs") })
                .SelectMany(f => banned.Where(b => b.IsMatch(StripComments(File.ReadAllText(f)))).Select(b => Rel(f) + " ~ " + b))
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void Grants_reach_the_economy_only_through_the_interpreter()
        {
            // The port's unstated contract: a module never grants directly.
            var direct = new Regex(@"\.(Grant|TryAdd|TrySpend|TryExchange)\(");
            var offenders = ModuleSources()
                .Where(f => direct.IsMatch(StripComments(File.ReadAllText(f))))
                .Select(Rel).ToArray();
            Assert.That(offenders, Is.Empty);
            Assert.That(StripComments(File.ReadAllText(Path.Combine(Runtime, "Core", "UnityIapPurchaseService.cs"))),
                Does.Contain("_interpreter.Apply(").And.Contain("GrantSource.Iap"));
        }

        [Test]
        public void Every_source_namespace_equals_its_nearest_assembly_name()
        {
            var ns = new Regex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
            Assert.Multiple(() =>
            {
                foreach (var file in ModuleSources())
                {
                    var dir = Path.GetDirectoryName(file);
                    string asmdef = null;
                    while (dir != null && asmdef == null && dir.Length >= Root.Length)
                    {
                        asmdef = Directory.GetFiles(dir, "*.asmdef").FirstOrDefault();
                        dir = Path.GetDirectoryName(dir);
                    }
                    Assert.That(asmdef, Is.Not.Null, Rel(file) + " is claimed by no asmdef");
                    if (asmdef == null) continue;
                    var name = (string)Json(asmdef)["name"];
                    var declared = ns.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToArray();
                    Assert.That(declared, Is.EqualTo(new[] { name }), Rel(file) + ": namespace ≡ assembly name");
                }
            });
        }

        [Test]
        public void Every_unity_file_and_folder_has_a_meta()
        {
            var missing = Directory.GetFileSystemEntries(Root, "*", SearchOption.AllDirectories)
                .Where(p => !Rel(p).Split('/').Any(seg => seg.EndsWith("~", StringComparison.Ordinal) || seg.StartsWith(".", StringComparison.Ordinal)))
                .Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))
                .Where(p => !File.Exists(p + ".meta"))
                .Select(Rel).ToArray();
            Assert.That(missing, Is.Empty);
        }

        // ── the four roles ─────────────────────────────────────────────────────────────────────────────

        private static JObject Asmdef(string dir) => Json(Directory.GetFiles(dir, "*.asmdef").Single());

        [Test]
        public void Only_the_Sdk_role_is_gated_and_every_bound_role_arms_the_define_identically()
        {
            var core = Asmdef(Path.Combine(Runtime, "Core"));
            var sdk = Asmdef(Path.Combine(Runtime, "Sdk"));
            var runtime = Asmdef(Runtime);
            var editor = Asmdef(EditorRole);

            Assert.Multiple(() =>
            {
                Assert.That((bool)core["noEngineReferences"], Is.True, "Core is engine-free");
                Assert.That((JArray)core["versionDefines"], Is.Empty);
                Assert.That(((JArray)sdk["defineConstraints"]).Select(t => (string)t), Is.EqualTo(new[] { Define }));
                Assert.That(((JArray)sdk["versionDefines"]).Single()["name"].ToString(), Is.EqualTo("com.unity.purchasing"));
                Assert.That(((JArray)sdk["versionDefines"]).Single()["expression"].ToString(), Is.EqualTo(UnityIapPreflightRules.SdkFloor));
                Assert.That(((JArray)sdk["versionDefines"]).Single()["define"].ToString(), Is.EqualTo(Define));
                foreach (var ungated in new[] { core, runtime, editor })
                    Assert.That((JArray)ungated["defineConstraints"], Is.Empty, (string)ungated["name"] + " is ungated");
                Assert.That(JToken.DeepEquals(runtime["versionDefines"], sdk["versionDefines"]), Is.True);
                Assert.That(JToken.DeepEquals(editor["versionDefines"], sdk["versionDefines"]), Is.True);
                Assert.That(((JArray)editor["includePlatforms"]).Select(t => (string)t), Is.EqualTo(new[] { "Editor" }));
                Assert.That(((JArray)editor["references"]).Select(t => (string)t), Is.EquivalentTo(new[]
                {
                    "ClassicSpins.PrototypeFramework.Iap.Unity.Core", "ClassicSpins.PrototypeFramework.Editor",
                }));
            });
        }

        [Test]
        public void No_role_references_a_forbidden_framework_tier_or_a_game_assembly()
        {
            var forbidden = new[] { "Presentation", "Views", "Infrastructure", "Composition" };
            var offenders = Directory.GetFiles(Root, "*.asmdef", SearchOption.AllDirectories)
                .SelectMany(f => ((JArray)Json(f)["references"]).Select(t => (string)t)
                    .Where(r => r.StartsWith("Game.", StringComparison.Ordinal)
                                || forbidden.Any(x => r == "ClassicSpins.PrototypeFramework." + x))
                    .Select(r => Rel(f) + " → " + r))
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void The_gated_marker_is_declared_in_the_Sdk_role_only()
        {
            var marker = new Regex(@"\bclass\s+PfModuleMarker_" + Regex.Escape(Define) + @"\b");
            var declaring = ModuleSources().Where(f => marker.IsMatch(StripComments(File.ReadAllText(f)))).Select(Rel).ToArray();
            Assert.That(declaring, Has.Length.EqualTo(1));
            Assert.That(declaring[0], Does.StartWith("Runtime/Sdk/"));
        }

        [Test]
        public void The_if_define_edge_cites_the_UnityCsReference_behaviour_it_relies_on()
        {
            Assert.That(InstallerSource, Does.Contain("#if " + Define));
            Assert.That(InstallerSource, Does.Contain("CustomScriptAssembly.cs:568"));
            Assert.That(InstallerSource, Does.Contain("EditorBuildRules.cs:244, 451-465"));
        }

        [Test]
        public void The_installer_registers_the_service_and_the_override_unconditionally()
        {
            var depth = 0;
            string gated = "", ungated = "", ifBranch = "", elseBranch = "";
            var inElse = false;
            foreach (var raw in InstallerSource.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#if", StringComparison.Ordinal)) { depth++; inElse = false; continue; }
                if (line.StartsWith("#elif", StringComparison.Ordinal) || line.StartsWith("#else", StringComparison.Ordinal)) { inElse = true; continue; }
                if (line.StartsWith("#endif", StringComparison.Ordinal)) { depth--; inElse = false; continue; }
                if (depth > 0)
                {
                    gated += line + "\n";
                    if (inElse) elseBranch += line + "\n"; else ifBranch += line + "\n";
                }
                else ungated += line + "\n";
            }

            Assert.Multiple(() =>
            {
                Assert.That(ungated, Does.Contain(".As<IPurchaseService>()"), "IPurchaseService is registered outside any #if");
                Assert.That(ungated, Does.Contain("new UnityIapPurchaseService("));
                Assert.That(ungated, Does.Contain("Register<IUnityIapBridge>"));
                Assert.That(ungated, Does.Contain("BootNodeOverride.Replace(ReplacedBootNodeId"));
                Assert.That(ungated, Does.Contain("Lifetime.Transient"), "the override is Transient");
                Assert.That(gated, Does.Not.Contain("UnityIapPurchaseService"));
                Assert.That(gated, Does.Not.Contain("BootNodeOverride"));
                Assert.That(ifBranch, Does.Contain("Sdk.UnityIapBridge"));
                Assert.That(elseBranch, Does.Contain("new NullUnityIapBridge("));
                Assert.That(StripComments(InstallerSource), Does.Not.Contain("As<IBootNode>"), "as an IBootNode it would be a second IapInit");
                Assert.That(StripComments(InstallerSource), Does.Not.Contain("RegisterInstance"));
                Assert.That(StripComments(InstallerSource), Does.Contain("DelayType.Realtime"));
            });
            Assert.That(UnityIapInstallerConstants.ReplacedBootNodeId(InstallerSource), Is.EqualTo((string)PfModule["port"]["replacesBootNode"]));
        }

        /// <summary>The build-callback policy (AD-MOD-6's closed list) lives in the framework's `greps.sh source`
        /// buildhook rule alone; this module keeps only its own boot-gate check.</summary>
        [Test]
        public void The_module_ships_no_boot_gate()
        {
            var gate = new Regex(@"BootNodeOverride\.Gate\(");
            var offenders = ModuleSources().Where(f => gate.IsMatch(StripComments(File.ReadAllText(f)))).Select(Rel).ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void UnityIapPreflight_implements_IModulePreflight_and_writes_nothing()
        {
            var editor = Directory.GetFiles(EditorRole, "*.cs", SearchOption.AllDirectories);
            Assert.That(editor.Where(f => Regex.IsMatch(StripComments(File.ReadAllText(f)), @"\bclass\s+UnityIapPreflight\s*:\s*IModulePreflight\b"))
                .Select(Rel), Is.EqualTo(new[] { "Editor/UnityIapPreflight.cs" }));

            var writes = new[]
            {
                "File.Write", "File.Create", "File.Copy", "File.Move", "File.Delete", "Directory.Create", "CreateAsset",
                "SaveAssets", "SetDirty", "PlayerSettings.", "SetScriptingDefineSymbols", "EditorPrefs.Set",
            };
            var hits = editor.SelectMany(f => writes.Where(w => StripComments(File.ReadAllText(f)).Contains(w)).Select(w => Rel(f) + " ~ " + w)).ToArray();
            Assert.That(hits, Is.Empty, "fact gathering only");
        }

        [Test]
        public void The_preflight_constants_match_the_metadata()
        {
            Assert.Multiple(() =>
            {
                Assert.That(UnityIapPreflightRules.ModuleId, Is.EqualTo((string)PfModule["id"]));
                Assert.That(UnityIapPreflightRules.InstallerType, Is.EqualTo((string)PfModule["installer"]["type"]));
                Assert.That(UnityIapPreflightRules.Define, Is.EqualTo(Define));
                Assert.That(((JObject)PfModule["requires"]["dependencies"]).Properties().Select(p => p.Name),
                    Is.EqualTo(new[] { UnityIapPreflightRules.SdkPackage }));
            });
        }

        // ── metadata ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_port_is_the_purchase_port_replacing_IapInit_with_no_secondary_ports()
        {
            var port = (JObject)PfModule["port"];
            Assert.Multiple(() =>
            {
                Assert.That((string)port["interface"], Is.EqualTo("ClassicSpins.PrototypeFramework.Application.IPurchaseService"));
                Assert.That((string)port["mode"], Is.EqualTo("replacement"));
                Assert.That((string)port["replacesBootNode"], Is.EqualTo(UnityIapInitNode.NodeId));
                Assert.That(PfModule["ports"], Is.Null, "the installer registers no secondary port");
                Assert.That(Regex.Matches(StripComments(InstallerSource), @"\.As<(\w+)>\(\)").Select(m => m.Groups[1].Value),
                    Is.EqualTo(new[] { "IPurchaseService" }), "every port the installer registers is declared");
            });
        }

        [Test]
        public void The_SDK_dependency_is_an_exact_pin_at_or_above_the_define_floor()
        {
            var deps = (JObject)PfModule["requires"]["dependencies"];
            var pin = (string)deps["com.unity.purchasing"];
            Assert.That(pin, Does.Match(@"^\d+\.\d+\.\d+$"), "exact version, never a range");
            Assert.That(Version.Parse(pin), Is.GreaterThanOrEqualTo(Version.Parse(UnityIapPreflightRules.SdkFloor)));
        }

        [Test]
        public void Installer_type_resolves_to_a_ScriptableObject_installer_in_the_runtime_role()
        {
            var type = (string)PfModule["installer"]["type"];
            var dot = type.LastIndexOf('.');
            var ns = type.Substring(0, dot);
            var cls = type.Substring(dot + 1);
            Assert.That((string)Asmdef(Runtime)["name"], Is.EqualTo(ns));

            var declaring = Directory.GetFiles(Runtime, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var s = StripComments(File.ReadAllText(f));
                    return Regex.IsMatch(s, @"namespace\s+" + Regex.Escape(ns) + @"\s*\{")
                           && Regex.IsMatch(s, @"\bclass\s+" + Regex.Escape(cls) + @"\s*:\s*ScriptableObject\s*,\s*IInstaller\b");
                })
                .ToArray();
            Assert.That(declaring, Has.Length.EqualTo(1));
            Assert.That(InstallerSource, Does.Contain("menuName = \"" + (string)PfModule["installer"]["menu"] + "\""));
        }

        private static JObject FrameworkPackage => Json(Path.Combine(TestSupport.FrameworkRoot, "package.json"));

        [Test]
        public void Package_json_depends_on_the_framework_only_at_its_current_version()
        {
            var pkg = Json(Path.Combine(Root, "package.json"));
            var deps = ((JObject)pkg["dependencies"]).Properties().ToDictionary(p => p.Name, p => (string)p.Value);
            Assert.That(deps.Keys, Is.EqualTo(new[] { "com.classicspins.prototype-framework" }),
                "the SDK is declared only in pf-module.json requires, never here");
            Assert.That(deps["com.classicspins.prototype-framework"], Is.EqualTo((string)FrameworkPackage["version"]));
            Assert.That((string)pkg["name"], Is.EqualTo((string)PfModule["id"]));
        }

        /// <summary>
        /// The first framework release carrying <c>ProductKind</c> (the store type the module registers each
        /// product with). It lands after 0.8.2; until the framework's own bump the floor may sit at the
        /// framework's current version (the established pf-ads-max rule), after it the floor must follow.
        /// </summary>
        private static readonly Version FirstReleaseWithProductKind = new Version(0, 8, 3);

        [Test]
        public void FrameworkRange_floors_at_the_first_release_with_ProductKind_and_caps_at_the_next_minor()
        {
            var framework = Version.Parse((string)FrameworkPackage["version"]);
            var match = Regex.Match((string)PfModule["frameworkRange"], @"^>=(\d+\.\d+\.\d+) <(\d+\.\d+\.\d+)$");
            Assert.That(match.Success, Is.True);
            var floor = Version.Parse(match.Groups[1].Value);
            var cap = Version.Parse(match.Groups[2].Value);
            var required = framework < FirstReleaseWithProductKind ? framework : FirstReleaseWithProductKind;

            Assert.Multiple(() =>
            {
                Assert.That(floor, Is.LessThanOrEqualTo(framework));
                Assert.That(floor, Is.GreaterThanOrEqualTo(required), "a framework without ProductKind cannot compile this module");
                Assert.That(cap, Is.EqualTo(new Version(framework.Major, framework.Minor + 1, 0)));
            });
        }

        [Test]
        public void The_source_pin_tags_this_package_s_own_version()
        {
            var version = (string)Json(Path.Combine(Root, "package.json"))["version"];
            var url = (string)PfModule["source"]["url"];
            Assert.That((string)PfModule["source"]["kind"], Is.EqualTo("git"));
            Assert.That(url, Does.Contain("?path=/Packages/com.classicspins.pf-iap-unity#"));
            Assert.That(url, Does.EndWith("#iap-unity/v" + version));
        }

        [Test]
        public void The_manual_steps_name_both_stores_the_product_type_and_the_installer_slot()
        {
            var steps = string.Join("\n", ((JArray)PfModule["manualSteps"]).Select(s => (string)s));
            Assert.That(steps, Does.Contain("App Store Connect").And.Contain("Google Play Console")
                .And.Contain("\"type\"").And.Contain("UnityIapInstaller.asset").And.Contain("Restore"));
        }
    }

    internal static class UnityIapInstallerConstants
    {
        public static string ReplacedBootNodeId(string installerSource)
            => Regex.Match(installerSource, @"ReplacedBootNodeId\s*=\s*""([^""]+)""").Groups[1].Value;
    }
}
