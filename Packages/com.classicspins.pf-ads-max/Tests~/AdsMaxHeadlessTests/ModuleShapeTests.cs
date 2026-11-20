using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// The module's shape, held where the kernel cannot hold it yet: constructor surfaces by reflection,
    /// and the four-role layout, the consent removal, the <c>#if PF_MAX</c> edge and the metadata by
    /// source scan. The <c>Sdk</c> role is never compiled here, so everything about it is read as text.
    /// </summary>
    [TestFixture]
    public sealed class ModuleShapeTests
    {
        private static string Root => TestSupport.ModuleRoot;
        private static string Runtime => Path.Combine(Root, "Runtime");

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

        // ── constructors ───────────────────────────────────────────────────────────────────────────────

        private static readonly Type[] ContainerResolved = { typeof(MaxAdsService), typeof(MaxBannerAds), typeof(MaxAdsInitNode), typeof(NullMaxBridge), typeof(MaxAdUnits), typeof(MaxRevenueReporter), typeof(MaxAdVisibility), typeof(MaxAdReadiness), typeof(MaxMediationDebuggerTool), typeof(MaxCreativeDebuggerTool), typeof(MaxAdsPrivacyOptions) };

        [TestCaseSource(nameof(ContainerResolved))]
        public void Constructor_takes_no_consent_or_boot_status_and_defaults_nothing(Type type)
        {
            var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.That(ctors, Has.Length.EqualTo(1), type.Name + " has exactly one public constructor");
            var parameters = ctors[0].GetParameters();

            Assert.Multiple(() =>
            {
                foreach (var p in parameters)
                {
                    Assert.That(p.ParameterType.Name, Is.Not.EqualTo("IConsentService"),
                        type.Name + ": personalized-ads consent is MAX's — never inject the framework's consent service");
                    Assert.That(p.ParameterType.Name, Is.Not.EqualTo("IBootStatus"),
                        type.Name + ": can-show keys on the bridge's own initialized state, never boot status");
                    Assert.That(p.HasDefaultValue || p.IsOptional, Is.False,
                        type.Name + "." + p.Name + " is defaulted — VContainer resolves every parameter regardless");
                }
            });
        }

        [Test]
        public void The_init_node_is_constructed_from_bridge_units_config_visibility_and_readiness_state_and_log_only()
        {
            // IGameConfig is covered by the node's Requires (ConfigAvailable) — the race rule.
            var types = typeof(MaxAdsInitNode).GetConstructors()[0].GetParameters().Select(p => p.ParameterType.Name);
            Assert.That(types, Is.EqualTo(new[] { "IMaxBridge", "MaxAdUnits", "IGameConfig", "MaxAdVisibility", "MaxAdReadiness", "ILog" }));
            var bridge = new FakeMaxBridge();
            var config = new DictConfig();
            var units = new MaxAdUnits(bridge, config, new RecordingLog(), null, new FakeDelay().Func);
            Assert.That(new MaxAdsInitNode(bridge, units, config, new MaxAdVisibility(new RecordingPublisher(), new RecordingLog()),
                    new MaxAdReadiness(bridge, units, config, new RecordingReadinessPublisher(), new RecordingLog()), new RecordingLog()).Requires,
                Does.Contain(ClassicSpins.PrototypeFramework.Domain.BootCaps.ConfigAvailable));
        }

        [Test]
        public void Only_the_readiness_state_constructs_AdReadinessChanged_or_names_its_publisher()
        {
            // AD-MOD-11: one producer of the fact; the publisher reaches only it (and the installer that builds it).
            var sources = Directory.GetFiles(Runtime, "*.cs", SearchOption.AllDirectories)
                .ToDictionary(Rel, f => StripComments(File.ReadAllText(f)));
            const string state = "Runtime/Core/MaxAdReadiness.cs";
            const string installer = "Runtime/MaxAdsInstaller.cs";
            Assert.That(sources.Keys, Does.Contain(state));
            Assert.That(sources[state], Does.Contain("new AdReadinessChanged("));

            Assert.Multiple(() =>
            {
                Assert.That(sources.Where(s => s.Key != state && s.Value.Contains("new AdReadinessChanged("))
                    .Select(s => s.Key), Is.Empty, "only the readiness state constructs the fact");
                Assert.That(sources.Where(s => s.Key != state && s.Key != installer && s.Value.Contains("IAdReadinessChangedPublisher"))
                    .Select(s => s.Key), Is.Empty, "only the readiness state (and the installer that builds it) names the publisher");
                Assert.That(sources.Where(s => s.Value.Contains("IBufferedSubscriber")).Select(s => s.Key), Is.Empty,
                    "a buffered subscriber replays a default fact");
                foreach (var producer in new[] { "Runtime/Core/MaxAdsService.cs", "Runtime/Core/MaxAdsInitNode.cs" })
                    Assert.That(sources[producer], Does.Contain("MaxAdReadiness"), producer);
            });
        }

        [Test]
        public void The_installer_registers_one_readiness_state_singleton_for_the_service_and_the_init_node()
        {
            var src = StripComments(InstallerSource);
            Assert.Multiple(() =>
            {
                Assert.That(Regex.Matches(src, @"Register<MaxAdReadiness>\(").Count, Is.EqualTo(1), "registered once");
                Assert.That(src, Does.Match(@"Register<MaxAdReadiness>\(\s*resolver\s*=>\s*new MaxAdReadiness\([^;]*?Lifetime\.Singleton\)\s*;"),
                    "a Singleton factory");
                Assert.That(Regex.Matches(src, @"resolver\.Resolve<MaxAdReadiness>\(\)").Count, Is.EqualTo(2),
                    "resolved for the service and for the init node");
                Assert.That(Regex.Matches(src, @"Resolve<IAdReadinessChangedPublisher>\(\)").Count, Is.EqualTo(1),
                    "the publisher reaches only the readiness state's factory");
            });
            Assert.That(src, Does.Match(@"new MaxAdsService\([^;]*resolver\.Resolve<MaxAdReadiness>\(\)[^;]*;"), "the service takes the state");
            Assert.That(src, Does.Match(@"new MaxAdsInitNode\([^;]*resolver\.Resolve<MaxAdReadiness>\(\)[^;]*;"), "the init node takes the state");
        }

        // ── developer tools (Story 10.10, AD-MOD-13) ───────────────────────────────────────────────────

        [Test]
        public void Each_dev_tool_is_constructed_from_bridge_config_and_log_only()
        {
            foreach (var type in new[] { typeof(MaxMediationDebuggerTool), typeof(MaxCreativeDebuggerTool) })
            {
                var types = type.GetConstructors()[0].GetParameters().Select(p => p.ParameterType.Name);
                Assert.That(types, Is.EqualTo(new[] { "IMaxBridge", "IGameConfig", "ILog" }), type.Name);
                Assert.That(type.IsSealed, Is.True, type.Name);
                Assert.That(typeof(ClassicSpins.PrototypeFramework.Application.IDevTool).IsAssignableFrom(type), Is.True, type.Name);
            }
        }

        [Test]
        public void The_installer_registers_two_distinct_dev_tool_types_as_IDevTool_ungated_and_never_as_an_instance()
        {
            var src = StripComments(InstallerSource);
            var registration = new Regex(
                @"Register<(\w+)>\(\s*resolver\s*=>\s*new\s+\1\([^;]*?Lifetime\.Singleton\)\s*\.As<IDevTool>\(\)\s*;");
            var registered = registration.Matches(src).Select(m => m.Groups[1].Value).ToArray();
            var ungated = StripComments(UngatedInstallerSource());

            Assert.Multiple(() =>
            {
                Assert.That(registered, Is.EquivalentTo(new[] { nameof(MaxMediationDebuggerTool), nameof(MaxCreativeDebuggerTool) }),
                    "one Singleton factory per tool type, each .As<IDevTool>()");
                Assert.That(Regex.Matches(src, @"\bIDevTool\b").Count, Is.EqualTo(2), "no other IDevTool registration");
                Assert.That(Regex.Matches(ungated, @"\.As<IDevTool>\(\)").Count, Is.EqualTo(2), "both registered outside any #if");
                Assert.That(src, Does.Not.Contain("RegisterInstance"), "DW-35: never RegisterInstance");
                Assert.That(src, Does.Not.Contain("IsInitialized"), "nothing reads the SDK state at registration");
                Assert.That(src, Does.Not.Contain("TryReadInt"), "nothing reads the switch at registration");
                Assert.That(src, Does.Not.Contain("SwitchKey"), "nothing reads the switch at registration");
            });
        }

        // ── privacy options (Story 10.11, AD-MOD-9) ────────────────────────────────────────────────────

        [Test]
        public void The_privacy_options_are_constructed_from_bridge_visibility_state_and_log_only()
        {
            var type = typeof(MaxAdsPrivacyOptions);
            var types = type.GetConstructors()[0].GetParameters().Select(p => p.ParameterType.Name);
            Assert.That(types, Is.EqualTo(new[] { "IMaxBridge", "MaxAdVisibility", "ILog" }));
            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(ClassicSpins.PrototypeFramework.Application.IAdsPrivacyOptions).IsAssignableFrom(type), Is.True);
        }

        [Test]
        public void The_installer_registers_the_privacy_options_once_as_IAdsPrivacyOptions_ungated_and_never_as_an_instance()
        {
            var src = StripComments(InstallerSource);
            var ungated = StripComments(UngatedInstallerSource());
            var registration = new Regex(
                @"Register<MaxAdsPrivacyOptions>\(\s*resolver\s*=>\s*new\s+MaxAdsPrivacyOptions\([^;]*?Lifetime\.Singleton\)\s*\.As<IAdsPrivacyOptions>\(\)\s*;");

            Assert.Multiple(() =>
            {
                Assert.That(registration.Matches(src).Count, Is.EqualTo(1), "one Singleton factory .As<IAdsPrivacyOptions>()");
                Assert.That(Regex.Matches(src, @"\bIAdsPrivacyOptions\b").Count, Is.EqualTo(1), "no other IAdsPrivacyOptions registration");
                Assert.That(registration.Matches(ungated).Count, Is.EqualTo(1), "registered outside any #if");
                Assert.That(src, Does.Match(@"new MaxAdsPrivacyOptions\(\s*resolver\.Resolve<IMaxBridge>\(\),\s*resolver\.Resolve<MaxAdVisibility>\(\),\s*resolver\.Resolve<ILog>\(\)\)"),
                    "the same MaxAdVisibility singleton the service and the init node use");
                Assert.That(src, Does.Not.Contain("RegisterInstance"), "DW-35: never RegisterInstance");
            });
        }

        /// <summary>The installer's lines outside every <c>#if</c> block.</summary>
        private static string UngatedInstallerSource()
        {
            var depth = 0;
            var ungated = new System.Text.StringBuilder();
            foreach (var raw in InstallerSource.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#if", StringComparison.Ordinal)) { depth++; continue; }
                if (line.StartsWith("#endif", StringComparison.Ordinal)) { depth--; continue; }
                if (depth == 0) ungated.Append(line).Append('\n');
            }
            return ungated.ToString();
        }

        [Test]
        public void Only_the_visibility_state_constructs_the_fact_or_names_its_publisher()
        {
            // AD-MOD-12: only MaxAdVisibility constructs AdVisibilityChanged; every producer holds a slot.
            var sources = Directory.GetFiles(Runtime, "*.cs", SearchOption.AllDirectories)
                .ToDictionary(Rel, f => StripComments(File.ReadAllText(f)));
            const string state = "Runtime/Core/MaxAdVisibility.cs";
            const string installer = "Runtime/MaxAdsInstaller.cs";
            Assert.That(sources.Keys, Does.Contain(state));

            Assert.Multiple(() =>
            {
                Assert.That(sources.Where(s => s.Key != state && s.Value.Contains("new AdVisibilityChanged("))
                    .Select(s => s.Key), Is.Empty, "only the state constructs the fact");
                Assert.That(sources.Where(s => s.Key != state && s.Key != installer && s.Value.Contains("IAdVisibilityChangedPublisher"))
                    .Select(s => s.Key), Is.Empty, "only the state (and the installer that builds it) names the publisher");
                foreach (var producer in new[] { "Runtime/Core/MaxAdsService.cs", "Runtime/Core/MaxAdsInitNode.cs", "Runtime/Core/MaxAdsPrivacyOptions.cs" })
                    Assert.That(sources[producer], Does.Contain("MaxAdVisibility"), producer);
            });
        }

        [Test]
        public void The_installer_registers_one_visibility_state_singleton_for_all_three_producers()
        {
            var src = StripComments(InstallerSource);
            Assert.Multiple(() =>
            {
                Assert.That(Regex.Matches(src, @"Register<MaxAdVisibility>\(").Count, Is.EqualTo(1), "registered once");
                Assert.That(src, Does.Match(@"Register<MaxAdVisibility>\(\s*resolver\s*=>\s*new MaxAdVisibility\([^;]*?Lifetime\.Singleton\)\s*;"),
                    "a Singleton factory");
                Assert.That(Regex.Matches(src, @"resolver\.Resolve<MaxAdVisibility>\(\)").Count, Is.EqualTo(3),
                    "resolved for the service, the init node and the privacy options");
                Assert.That(Regex.Matches(src, @"Resolve<IAdVisibilityChangedPublisher>\(\)").Count, Is.EqualTo(1),
                    "the publisher reaches only the state's factory");
            });
            Assert.That(src, Does.Match(@"new MaxAdsService\([^;]*resolver\.Resolve<MaxAdVisibility>\(\)[^;]*;"), "the service takes the state");
            Assert.That(src, Does.Match(@"new MaxAdsInitNode\([^;]*resolver\.Resolve<MaxAdVisibility>\(\)[^;]*;"), "the init node takes the state");
            Assert.That(src, Does.Match(@"new MaxAdsPrivacyOptions\([^;]*resolver\.Resolve<MaxAdVisibility>\(\)[^;]*;"), "the privacy options take the state");
        }

        [Test]
        public void The_bridge_carries_primitives_only()
        {
            // Action<string>: the CMP completion callback (null ⇒ success, else MAX's error as text).
            var allowed = new List<Type> { typeof(string), typeof(bool), typeof(string[]), typeof(void), typeof(Action<string>) };

            // MaxRevenue is the one struct allowed across: a readonly value type whose every public member is
            // a get-only string, bool or double — so it, too, carries no MAX type.
            var revenue = typeof(MaxRevenue);
            var revenueFields = new[] { typeof(string), typeof(bool), typeof(double) };
            Assert.That(revenue.IsValueType, Is.True);
            Assert.That(revenue.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute"), Is.True,
                "MaxRevenue is a readonly struct");
            Assert.That(revenue.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => !revenueFields.Contains(p.PropertyType) || p.CanWrite).Select(p => p.Name),
                Is.Empty, "every MaxRevenue property is a get-only string, bool or double");
            Assert.That(revenue.GetFields(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
            allowed.Add(revenue);

            var offenders = new List<string>();
            foreach (var m in typeof(IMaxBridge).GetMethods())
            {
                if (m.IsSpecialName) continue;
                if (m.ReturnType != typeof(Cysharp.Threading.Tasks.UniTask<bool>) && !allowed.Contains(m.ReturnType)) offenders.Add(m.Name);
                offenders.AddRange(m.GetParameters().Where(p => !allowed.Contains(p.ParameterType)).Select(p => m.Name + "(" + p.Name + ")"));
            }
            foreach (var e in typeof(IMaxBridge).GetEvents())
                offenders.AddRange(e.EventHandlerType.GetGenericArguments().Where(t => !allowed.Contains(t)).Select(t => e.Name));
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void Core_reads_no_Unity_define_and_takes_time_only_as_the_injected_delay()
        {
            var banned = new[]
            {
                new Regex(@"#if\s+UNITY_"), new Regex(@"UniTask\.Delay\b"), new Regex(@"\bTime\."),
                new Regex(@"Split\(\s*','"), new Regex(@"Split\(\s*"",""\s*\)"),
            };
            var offenders = Directory.GetFiles(Path.Combine(Runtime, "Core"), "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => banned.Where(b => b.IsMatch(StripComments(File.ReadAllText(f)))).Select(b => Rel(f) + " ~ " + b))
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void The_installer_maps_the_platform_define_and_passes_a_realtime_delay()
        {
            var src = StripComments(InstallerSource);
            Assert.Multiple(() =>
            {
                Assert.That(src, Does.Match(@"#if\s+UNITY_ANDROID\s+return\s+MaxAdUnits\.Android;"));
                Assert.That(src, Does.Match(@"#elif\s+UNITY_IOS\s+return\s+MaxAdUnits\.Ios;"));
                Assert.That(src, Does.Contain("DelayType.Realtime"));
                Assert.That(src, Does.Contain("new MaxAdUnits("));
            });
        }

        [Test]
        public void The_bridge_has_no_consent_member()
        {
            var members = typeof(IMaxBridge).GetMembers().Select(m => m.Name).ToArray();
            Assert.That(members.Where(m => m.IndexOf("Consent", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty);
        }

        /// <summary>Story 10.11 (AD-MOD-9): re-showing MAX's form is not a way to write consent. The bridge
        /// exposes no setter at all, no consent-setting method, and the Sdk role calls none of MAX's.</summary>
        [Test]
        public void The_bridge_has_no_consent_setter()
        {
            var setters = new[] { "SetHasUserConsent", "SetDoNotSell", "SetIsAgeRestrictedUser" };
            var bridge = typeof(IMaxBridge);
            var sdk = StripComments(File.ReadAllText(Path.Combine(Runtime, "Sdk", "MaxBridge.cs")));

            Assert.Multiple(() =>
            {
                Assert.That(bridge.GetProperties().Where(p => p.CanWrite).Select(p => p.Name), Is.Empty, "no settable property");
                Assert.That(bridge.GetMethods().Where(m => !m.IsSpecialName)
                        .Select(m => m.Name)
                        .Where(n => setters.Any(s => n.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                                    || n.IndexOf("Consent", StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.Empty, "no consent-setting method");
                Assert.That(setters.Where(s => sdk.Contains(s)), Is.Empty, "Sdk/MaxBridge.cs calls none of MAX's consent setters");
                Assert.That(sdk, Does.Contain("ShowCmpForExistingUser"), "the scan reads the real bridge, not an empty file");
            });
        }

        // ── source scans ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Only_Sdk_MaxBridge_names_a_MAX_type()
        {
            var max = new Regex(@"\bMaxSdk(Base|Callbacks)?\b");
            var naming = ModuleSources().Where(f => max.IsMatch(File.ReadAllText(f))).Select(Rel).ToArray();
            Assert.That(naming, Is.EqualTo(new[] { "Runtime/Sdk/MaxBridge.cs" }));
        }

        [Test]
        public void No_source_mentions_the_consent_pass_through()
        {
            var offenders = ModuleSources()
                .Where(f => { var s = File.ReadAllText(f); return s.Contains("SetHasUserConsent") || s.Contains("IConsentService"); })
                .Select(Rel).ToArray();
            Assert.That(offenders, Is.Empty);
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
        public void The_gated_marker_is_declared_in_the_Sdk_role_only()
        {
            var marker = new Regex(@"\bclass\s+PfModuleMarker_" + Regex.Escape(Define) + @"\b");
            var declaring = ModuleSources().Where(f => marker.IsMatch(StripComments(File.ReadAllText(f)))).Select(Rel).ToArray();
            Assert.That(declaring, Has.Length.EqualTo(1));
            Assert.That(declaring[0], Does.StartWith("Runtime/Sdk/"));
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
                .Concat(new[] { Path.Combine(Runtime, "NullMaxBridge.cs") })
                .SelectMany(f => banned.Where(b => b.IsMatch(StripComments(File.ReadAllText(f)))).Select(b => Rel(f) + " ~ " + b))
                .ToArray();
            Assert.That(offenders, Is.Empty);
        }

        private static string InstallerSource => File.ReadAllText(Path.Combine(Runtime, "MaxAdsInstaller.cs"));

        [Test]
        public void The_if_PF_MAX_edge_cites_the_UnityCsReference_behaviour_it_relies_on()
        {
            var src = InstallerSource;
            Assert.That(src, Does.Contain("#if " + Define));
            Assert.That(src, Does.Contain("CustomScriptAssembly.cs:568"));
            Assert.That(src, Does.Contain("EditorBuildRules.cs:244, 451-465"));
        }

        [Test]
        public void The_installer_registers_the_service_and_the_override_unconditionally()
        {
            var lines = InstallerSource.Split('\n');
            var depth = 0;
            string gated = "", ungated = "", ifBranch = "", elseBranch = "";
            var inElse = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("#if", StringComparison.Ordinal)) { depth++; inElse = false; continue; }
                if (line.StartsWith("#else", StringComparison.Ordinal)) { inElse = true; continue; }
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
                Assert.That(ungated, Does.Contain(".As<IAdsService>()"), "IAdsService is registered outside any #if");
                Assert.That(ungated, Does.Contain("new MaxAdsService("));
                Assert.That(ungated, Does.Contain("Register<IMaxBridge>"));
                Assert.That(ungated, Does.Contain("BootNodeOverride.Replace("));
                Assert.That(gated, Does.Not.Contain("MaxAdsService"));
                Assert.That(gated, Does.Not.Contain("BootNodeOverride"));
                Assert.That(ifBranch, Does.Contain("MaxBridge"));
                Assert.That(elseBranch, Does.Contain("new NullMaxBridge("));
                Assert.That(StripComments(InstallerSource), Does.Not.Contain("As<IBootNode>"),
                    "as an IBootNode it would be a second AdsInit");
            });
        }

        // ── the Editor role (Story 9.4): the build preflight, fact gathering only ──────────────────────
        // Never compiled here (it names UnityEditor and CS.PF.Editor), so it is held by source scan.

        private static string EditorRole => Path.Combine(Root, "Editor");

        private static IEnumerable<string> EditorSources()
            => Directory.GetFiles(EditorRole, "*.cs", SearchOption.AllDirectories);

        [Test]
        public void The_Editor_role_asmdef_is_ungated_editor_only_and_arms_the_define_like_the_Sdk_role()
        {
            var editor = Json(Directory.GetFiles(EditorRole, "*.asmdef").Single());
            var sdk = Json(Directory.GetFiles(Path.Combine(Runtime, "Sdk"), "*.asmdef").Single());
            Assert.Multiple(() =>
            {
                Assert.That((string)editor["name"], Is.EqualTo("ClassicSpins.PrototypeFramework.Ads.Max.Editor"));
                Assert.That(((JArray)editor["includePlatforms"]).Select(t => (string)t), Is.EqualTo(new[] { "Editor" }));
                Assert.That((bool)editor["autoReferenced"], Is.False);
                Assert.That((JArray)editor["defineConstraints"], Is.Empty, "ungated: it must report the SDK's ABSENCE");
                Assert.That(((JArray)editor["references"]).Select(t => (string)t), Is.EquivalentTo(new[]
                {
                    "ClassicSpins.PrototypeFramework.Ads.Max.Core", "ClassicSpins.PrototypeFramework.Editor",
                    "MaxSdk.Scripts.IntegrationManager.Editor",
                }));
                Assert.That(JToken.DeepEquals(editor["versionDefines"], sdk["versionDefines"]), Is.True,
                    "the Editor role arms " + Define + " with the Sdk role's exact versionDefines entry");
            });
        }

        [Test]
        public void The_MAX_settings_type_is_named_only_inside_if_the_define()
        {
            var settings = new Regex(@"\bAppLovinSettings\b");
            var outside = new List<string>();
            var inside = 0;
            foreach (var file in EditorSources())
            {
                var depth = 0;
                var gated = new Stack<bool>();
                var lines = StripComments(File.ReadAllText(file)).Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("#if", StringComparison.Ordinal))
                    {
                        var isDefine = Regex.IsMatch(line, @"^#if\s+" + Regex.Escape(Define) + @"\s*$");
                        gated.Push(isDefine);
                        if (isDefine) depth++;
                        continue;
                    }
                    if (line.StartsWith("#else", StringComparison.Ordinal) || line.StartsWith("#elif", StringComparison.Ordinal))
                    {
                        if (gated.Count > 0 && gated.Peek()) { depth--; gated.Pop(); gated.Push(false); }
                        continue;
                    }
                    if (line.StartsWith("#endif", StringComparison.Ordinal))
                    {
                        if (gated.Count > 0 && gated.Pop()) depth--;
                        continue;
                    }
                    if (!settings.IsMatch(line)) continue;
                    if (depth > 0) inside++;
                    else outside.Add(Rel(file) + ":" + (i + 1));
                }
            }
            Assert.That(outside, Is.Empty, "the settings type exists only when " + Define + " is armed");
            Assert.That(inside, Is.GreaterThan(0), "the scan must not pass vacuously: the SDK key is read under #if " + Define);
        }

        [Test]
        public void The_Android_resolver_is_reached_by_reflection_only()
        {
            var offenders = EditorSources()
                .Where(f =>
                {
                    var s = StripComments(File.ReadAllText(f));
                    return Regex.IsMatch(s, @"\busing\s+GooglePlayServices\b") || Regex.IsMatch(s, @"\bPlayServicesResolver\s*\.");
                })
                .Select(Rel).ToArray();
            Assert.That(offenders, Is.Empty, "a hard reference would stop the Editor role compiling where the resolver is absent");
            Assert.That(EditorSources().Any(f => File.ReadAllText(f).Contains("\"GooglePlayServices.PlayServicesResolver\"")
                                                 && File.ReadAllText(f).Contains("\"ResolveSync\"")), Is.True);
        }

        // The build-callback policy (AD-MOD-6's closed list, amended 2026-11-02) lives in the framework's
        // `greps.sh source` buildhook rule alone — the per-module copy that stood here is dropped.

        [Test]
        public void MaxAdsPreflight_implements_IModulePreflight()
        {
            var declaring = EditorSources()
                .Where(f => Regex.IsMatch(StripComments(File.ReadAllText(f)), @"\bclass\s+MaxAdsPreflight\s*:\s*IModulePreflight\b"))
                .Select(Rel).ToArray();
            Assert.That(declaring, Is.EqualTo(new[] { "Editor/MaxAdsPreflight.cs" }));
        }

        [Test]
        public void MaxAdsPreflight_writes_nothing()
        {
            var writes = new[]
            {
                "File.Write", "File.Create", "File.Copy", "File.Move", "File.Delete", "Directory.Create", "CreateAsset",
                "SaveAssets", "SetDirty", "PlayerSettings.", "SetScriptingDefineSymbols", "EditorPrefs.Set", ".Instance",
            };
            var hits = EditorSources()
                .SelectMany(f => writes.Where(w => StripComments(File.ReadAllText(f)).Contains(w)).Select(w => Rel(f) + " ~ " + w))
                .ToArray();
            Assert.That(hits, Is.Empty, "fact gathering only — and no create-on-miss settings singleton");
        }

        // ── metadata ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Installer_type_resolves_to_a_ScriptableObject_installer_in_the_runtime_role()
        {
            var type = (string)PfModule["installer"]["type"];
            var dot = type.LastIndexOf('.');
            var ns = type.Substring(0, dot);
            var cls = type.Substring(dot + 1);

            var runtimeAsmdef = Directory.GetFiles(Runtime, "*.asmdef").Single();
            Assert.That((string)Json(runtimeAsmdef)["name"], Is.EqualTo(ns), "installer.type lives in the runtime role's assembly");

            var declaring = Directory.GetFiles(Runtime, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var s = StripComments(File.ReadAllText(f));
                    return Regex.IsMatch(s, @"namespace\s+" + Regex.Escape(ns) + @"\s*\{")
                           && Regex.IsMatch(s, @"\bclass\s+" + Regex.Escape(cls) + @"\s*:\s*ScriptableObject\s*,\s*IInstaller\b");
                })
                .ToArray();
            Assert.That(declaring, Has.Length.EqualTo(1), "exactly one runtime-role source declares " + type);
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
            Assert.That((string)pkg["description"], Does.Not.Contain("PLACEHOLDER"));
        }

        /// <summary>
        /// The first framework release carrying every port this module fills: 0.8.1 brought
        /// <c>IDevTool</c> (Story 10.10), which the module registers for its debuggers, and 0.8.2 brought
        /// <c>IAdsPrivacyOptions</c> (Story 10.11), whose Null the module replaces. The floor may sit below
        /// it only while the framework itself predates it (the story's feature commit, before its bump).
        /// </summary>
        private static readonly Version FirstReleaseWithEveryPort = new Version(0, 8, 2);

        [Test]
        public void FrameworkRange_floors_at_the_first_release_with_every_port_and_caps_at_the_next_minor()
        {
            var framework = Version.Parse((string)FrameworkPackage["version"]);
            var range = (string)PfModule["frameworkRange"];
            var match = Regex.Match(range, @"^>=(\d+\.\d+\.\d+) <(\d+\.\d+\.\d+)$");
            Assert.That(match.Success, Is.True, "frameworkRange is '>=floor <cap': " + range);

            var floor = Version.Parse(match.Groups[1].Value);
            var cap = Version.Parse(match.Groups[2].Value);
            var required = framework < FirstReleaseWithEveryPort ? framework : FirstReleaseWithEveryPort;

            Assert.Multiple(() =>
            {
                Assert.That(floor.Major, Is.EqualTo(framework.Major), "the floor is compatible with the framework's major");
                Assert.That(floor, Is.LessThanOrEqualTo(framework), "the floor admits the framework this module pins");
                Assert.That(floor, Is.GreaterThanOrEqualTo(required),
                    "a framework below " + FirstReleaseWithEveryPort + " has no IAdsPrivacyOptions for this module to replace");
                Assert.That(cap, Is.EqualTo(new Version(framework.Major, framework.Minor + 1, 0)), "the cap is the next minor");
            });
        }

        [Test]
        public void Ports_declares_exactly_the_additive_IDevTool_and_the_replacements_IAdsPrivacyOptions_and_IBannerAds()
        {
            var ports = (JArray)PfModule["ports"];
            Assert.That(ports, Is.Not.Null, "pf-module.json declares ports[]");
            Assert.That(ports, Has.Count.EqualTo(3));
            var expected = new[]
            {
                ("ClassicSpins.PrototypeFramework.Application.IDevTool", "additive"),
                ("ClassicSpins.PrototypeFramework.Application.IAdsPrivacyOptions", "replacement"),
                ("ClassicSpins.PrototypeFramework.Application.IBannerAds", "replacement"),
            };
            Assert.Multiple(() =>
            {
                for (var i = 0; i < expected.Length; i++)
                {
                    var entry = (JObject)ports[i];
                    Assert.That(entry.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "interface", "mode" }),
                        "replacesBootNode belongs to port only");
                    Assert.That((string)entry["interface"], Is.EqualTo(expected[i].Item1));
                    Assert.That((string)entry["mode"], Is.EqualTo(expected[i].Item2));
                    Assert.That((string)PfModule["port"]["interface"], Is.Not.EqualTo((string)entry["interface"]));
                }
            });
        }

        /// <summary>
        /// The <c>source</c> a consumer resolves must tag this package's own version, so a version bump
        /// cannot leave the pin naming a release that is not this one (FR62 — 0.0.0 shipped with a
        /// placeholder URL and an <c>ads-max/v0.1.0</c> tag that disagreed with it).
        /// </summary>
        [Test]
        public void The_source_pin_tags_this_package_s_own_version()
        {
            var version = (string)Json(Path.Combine(Root, "package.json"))["version"];
            var source = (JObject)PfModule["source"];
            var url = (string)source["url"];

            Assert.Multiple(() =>
            {
                Assert.That((string)source["kind"], Is.EqualTo("git"));
                Assert.That(version, Is.Not.EqualTo("0.0.0"), "package.json version 0.0.0 is an unreleased placeholder");
                Assert.That(url, Does.Not.Contain("…"), "source.url is a placeholder: " + url);
                Assert.That(url, Does.Contain("?path=/Packages/com.classicspins.pf-ads-max#"), "source.url names the module folder: " + url);
                Assert.That(url, Does.EndWith("#ads-max/v" + version),
                    "source.url tag must be ads-max/v<package.json version> — url " + url + ", package.json version " + version);
            });
        }

        [Test]
        public void The_unit_id_step_also_names_the_rewarded_flag()
        {
            var steps = ((JArray)PfModule["manualSteps"]).Select(s => (string)s).ToArray();
            var unitStep = steps.Single(s => s.Contains("ads.max.<placement>.unitId"));
            Assert.That(unitStep, Does.Contain("ads.<placement>.rewarded = 1"));
        }
    }
}
