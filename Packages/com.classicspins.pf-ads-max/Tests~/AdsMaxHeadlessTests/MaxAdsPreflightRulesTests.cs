using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R = ClassicSpins.PrototypeFramework.Ads.Max.Core.MaxAdsPreflightRules;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// Story 9.4 — the decision half of the build preflight, row by row: facts in, named problem codes out.
    /// The editor role only gathers these facts; every rule that can stop a build is proven here.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdsPreflightRulesTests
    {
        private const string Placement = "match_interstitial";
        private const string SdkRecord = "com.applovin:applovin-sdk:13.0.0";

        /// <summary>A fully wired Android project: package, define, slot, key, consent, unit id, resolve.</summary>
        private static MaxAdsPreflightFacts Wired(MaxAdsPreflightTarget target = MaxAdsPreflightTarget.Android) => new MaxAdsPreflightFacts
        {
            Target = target,
            SdkPackageVersion = "8.6.5",
            DefineArmed = true,
            InstallerSlotted = true,
            SdkKey = "key",
            ConsentFlowEnabled = true,
            ConsentPrivacyPolicyUrl = "https://example.invalid/privacy",
            UsedPlacements = new[] { Placement },
            BundledConfig = new Dictionary<string, object> { [R.UnitIdKey(Placement)] = "unit-123" },
            ResolverPresent = true,
            ResolvedAndroidPackages = new[] { SdkRecord, "com.applovin.mediation:facebook-adapter:6.22.0.0" },
            MainTemplateGradle = "dependencies {\n**DEPS**}\n",
            ResolvedPackageIds = new[] { R.SdkPackage, "com.applovin.mediation.adapters.facebook.android", "com.applovin.mediation.adapters.facebook.ios" },
        };

        private static string[] Codes(MaxAdsPreflightFacts facts) => R.Evaluate(facts).Select(p => p.Code).ToArray();

        // ── silence ─────────────────────────────────────────────────────────────────────────────────────

        [TestCase(MaxAdsPreflightTarget.Android)]
        [TestCase(MaxAdsPreflightTarget.Ios)]
        [TestCase(MaxAdsPreflightTarget.Other)]
        public void Not_in_play_is_silent_SKU_0(MaxAdsPreflightTarget target)
        {
            // SKU #0: package absent, define unarmed, nothing slotted — and every other fact at its worst.
            var facts = new MaxAdsPreflightFacts { Target = target, UsedPlacements = new[] { Placement } };
            Assert.That(R.Evaluate(facts), Is.Empty);
        }

        [Test]
        public void Null_facts_are_silent() => Assert.That(R.Evaluate(null), Is.Empty);

        [TestCase(MaxAdsPreflightTarget.Android)]
        [TestCase(MaxAdsPreflightTarget.Ios)]
        [TestCase(MaxAdsPreflightTarget.Other)]
        public void A_fully_wired_project_is_silent(MaxAdsPreflightTarget target)
            => Assert.That(Codes(Wired(target)), Is.Empty);

        // ── wiring rows: every target ───────────────────────────────────────────────────────────────────

        [TestCase(MaxAdsPreflightTarget.Android)]
        [TestCase(MaxAdsPreflightTarget.Other)]
        public void Armed_without_package(MaxAdsPreflightTarget target)
        {
            var facts = new MaxAdsPreflightFacts { Target = target, DefineArmed = true };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.DefineWithoutPackage }));
        }

        [TestCase("8.6.5")]
        [TestCase("7.3.1")]
        public void Package_without_define(string version)
        {
            var facts = Wired(MaxAdsPreflightTarget.Other);
            facts.SdkPackageVersion = version;
            facts.DefineArmed = false;
            facts.InstallerSlotted = false;
            var problems = R.Evaluate(facts);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { R.PackageWithoutDefine }));
            Assert.That(problems[0].Message, Does.Contain(version));
            if (version.StartsWith("7", StringComparison.Ordinal))
                Assert.That(problems[0].Message, Does.Contain("8.0.0"), "a pre-UPM version is told why it does not arm");
        }

        [Test]
        public void Slotted_unarmed_without_package()
        {
            var facts = new MaxAdsPreflightFacts { Target = MaxAdsPreflightTarget.Android, InstallerSlotted = true };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.InstallerWithoutDefine }));
        }

        [Test]
        public void Slotted_unarmed_with_package_reports_both_wiring_rows()
        {
            var facts = Wired(MaxAdsPreflightTarget.Other);
            facts.DefineArmed = false;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.PackageWithoutDefine, R.InstallerWithoutDefine }));
        }

        // ── SDK configuration: mobile ───────────────────────────────────────────────────────────────────

        [TestCase("")]
        [TestCase("   ")]
        public void Sdk_key_empty(string key)
        {
            var facts = Wired();
            facts.SdkKey = key;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.SdkKeyEmpty }));
        }

        [Test]
        public void Sdk_key_unknowable_is_skipped()
        {
            var facts = Wired();
            facts.SdkKey = null;
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Sdk_key_is_skipped_when_unarmed_even_if_known_empty()
        {
            var facts = Wired(MaxAdsPreflightTarget.Ios);
            facts.DefineArmed = false;
            facts.InstallerSlotted = false;
            facts.SdkKey = "";
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.PackageWithoutDefine }));
        }

        [Test]
        public void Consent_flow_disabled()
        {
            var facts = Wired();
            facts.ConsentFlowEnabled = false;
            facts.ConsentPrivacyPolicyUrl = null;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.ConsentFlowDisabled }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Consent_flow_on_with_an_empty_privacy_url(string url)
        {
            var facts = Wired();
            facts.ConsentPrivacyPolicyUrl = url;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.ConsentPrivacyUrl }));
        }

        [Test]
        public void Unit_id_missing_from_bundled_config()
        {
            var facts = Wired();
            facts.BundledConfig = new Dictionary<string, object> { ["ads.max.other.unitId"] = "x" };
            var problems = R.Evaluate(facts);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { R.UnitId }));
            Assert.That(problems[0].Message, Does.Contain("ads.max." + Placement + ".unitId"));
        }

        [Test]
        public void Unit_id_one_problem_per_placement_and_null_config_means_all_missing()
        {
            var facts = Wired();
            facts.UsedPlacements = new[] { "a", "b", "a" };
            facts.BundledConfig = null;
            var problems = R.Evaluate(facts);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { R.UnitId, R.UnitId }));
            Assert.That(problems[0].Message, Does.Contain("ads.max.a.unitId"));
            Assert.That(problems[1].Message, Does.Contain("ads.max.b.unitId"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(42)]
        public void Unit_id_that_is_not_a_non_empty_string_is_missing(object value)
        {
            var facts = Wired();
            facts.BundledConfig = new Dictionary<string, object> { [R.UnitIdKey(Placement)] = value };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.UnitId }));
        }

        [Test]
        public void Unit_id_null_value_is_missing()
        {
            var facts = Wired();
            facts.BundledConfig = new Dictionary<string, object> { [R.UnitIdKey(Placement)] = null };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.UnitId }));
        }

        [Test]
        public void No_placement_manifest_means_no_unit_id_row()
        {
            var facts = Wired();
            facts.BundledConfig = null;
            facts.UsedPlacements = null;
            Assert.That(Codes(facts), Is.Empty);
            facts.UsedPlacements = new string[0];
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void The_unit_id_key_is_the_one_the_service_reads()
            => Assert.That(R.UnitIdKey(Placement),
                Is.EqualTo(MaxAdsService.UnitIdKey(new ClassicSpins.PrototypeFramework.Domain.AdPlacement(Placement))));

        // ── unit-id: the runtime's tiered lookup (v2) ──────────────────────────────────────────────────

        [TestCase(MaxAdsPreflightTarget.Android, "ads.max.android.match_interstitial.unitId")]
        [TestCase(MaxAdsPreflightTarget.Ios, "ads.max.ios.match_interstitial.unitId")]
        [TestCase(MaxAdsPreflightTarget.Android, "ads.max.android.interstitial.unitId")]
        [TestCase(MaxAdsPreflightTarget.Ios, "ads.max.interstitial.unitId")]
        public void Unit_id_passes_when_any_tier_for_the_build_target_is_bundled(MaxAdsPreflightTarget target, string key)
        {
            var facts = Wired(target);
            facts.BundledConfig = new Dictionary<string, object> { [key] = "unit" };
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Another_platforms_tier_does_not_count()
        {
            var facts = Wired(MaxAdsPreflightTarget.Ios);
            facts.BundledConfig = new Dictionary<string, object>
            {
                ["ads.max.android." + Placement + ".unitId"] = "unit", ["ads.max.android.interstitial.unitId"] = "unit",
            };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.UnitId }));
        }

        [Test]
        public void The_format_default_tier_follows_the_bundled_rewarded_flag()
        {
            var facts = Wired();
            facts.BundledConfig = new Dictionary<string, object>
            {
                ["ads." + Placement + ".rewarded"] = 1, ["ads.max.interstitial.unitId"] = "unit",
            };
            var problems = R.Evaluate(facts);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { R.UnitId }), "a rewarded placement needs a rewarded unit");
            Assert.That(problems[0].Message, Does.Contain("ads.max.rewarded.unitId"));

            facts.BundledConfig = new Dictionary<string, object>
            {
                ["ads." + Placement + ".rewarded"] = 1, ["ads.max.rewarded.unitId"] = "unit",
            };
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void A_bundled_long_rewarded_flag_reads_as_interstitial_matching_the_runtime()
        {
            // GameConfigCore's ConfigKey<int> reads with `v is T`, so a bundled 1L (long) is invisible to the
            // runtime's int read and the placement stays interstitial — the preflight must agree, not pass it
            // as rewarded.
            var facts = Wired();
            facts.BundledConfig = new Dictionary<string, object>
            {
                ["ads." + Placement + ".rewarded"] = 1L, ["ads.max.interstitial.unitId"] = "unit",
            };
            Assert.That(Codes(facts), Is.Empty, "1L reads as interstitial, and the interstitial default is bundled");

            facts.BundledConfig = new Dictionary<string, object>
            {
                ["ads." + Placement + ".rewarded"] = 1L, ["ads.max.rewarded.unitId"] = "unit",
            };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.UnitId }),
                "1L must NOT read as rewarded: only the interstitial default is bundled, so this must fail");
        }

        [Test]
        public void The_tiers_are_the_ones_the_runtime_tries()
            => Assert.That(R.UnitIdKeys(MaxAdsPreflightTarget.Android, Placement, null),
                Is.EqualTo(MaxAdUnits.UnitIdKeys(MaxAdUnits.Android, Placement, false)));

        // ── warnings: logged, never a build failure ───────────────────────────────────────────────────────

        [TestCase(MaxAdsPreflightTarget.Android)]
        [TestCase(MaxAdsPreflightTarget.Ios)]
        public void Slotted_for_mobile_without_the_placement_manifest_warns(MaxAdsPreflightTarget target)
        {
            var facts = Wired(target);
            facts.UsedPlacements = null;
            var warnings = R.Warnings(facts);
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("[pf-ads-max]").And.Contain("game.adplacements.json"));
            Assert.That(Codes(facts), Is.Empty, "a warning, not a problem");
        }

        [TestCase(MaxAdsPreflightTarget.Android)]
        [TestCase(MaxAdsPreflightTarget.Ios)]
        public void Slotted_for_mobile_with_an_empty_manifest_also_warns(MaxAdsPreflightTarget target)
        {
            // Zero placements leaves the unit-id rule checking nothing, exactly like no manifest at all.
            var facts = Wired(target);
            facts.UsedPlacements = new string[0];
            var warnings = R.Warnings(facts);
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("[pf-ads-max]").And.Contain("game.adplacements.json"));
            Assert.That(Codes(facts), Is.Empty, "a warning, not a problem");
        }

        [Test]
        public void No_warning_with_a_manifest_off_mobile_or_unslotted()
        {
            Assert.That(R.Warnings(Wired()), Is.Empty);

            var other = Wired(MaxAdsPreflightTarget.Other);
            other.UsedPlacements = null;
            Assert.That(R.Warnings(other), Is.Empty);

            var unslotted = Wired();
            unslotted.UsedPlacements = null;
            unslotted.InstallerSlotted = false;
            Assert.That(R.Warnings(unslotted), Is.Empty);

            Assert.That(R.Warnings(null), Is.Empty);
        }

        // ── Android dependency resolution ───────────────────────────────────────────────────────────────

        [Test]
        public void Edm4u_missing()
        {
            var facts = Wired();
            facts.ResolverPresent = false;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.Edm4uMissing }));
        }

        [Test]
        public void Android_unresolved_when_there_is_no_record()
        {
            var facts = Wired();
            facts.ResolvedAndroidPackages = null;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.AndroidUnresolved }), "no record ⇒ no adapter count either");
        }

        [Test]
        public void Android_unresolved_when_the_record_lacks_the_sdk()
        {
            var facts = Wired();
            facts.ResolvedAndroidPackages = new[] { "com.applovin.mediation:facebook-adapter:6.22.0.0" };
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.AndroidUnresolved }));
        }

        [Test]
        public void Gradle_template_without_the_deps_token()
        {
            var facts = Wired();
            facts.MainTemplateGradle = "dependencies {\n}\n";
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.GradleDepsTokenMissing }));
        }

        [Test]
        public void No_custom_gradle_template_is_fine()
        {
            var facts = Wired();
            facts.MainTemplateGradle = null;
            Assert.That(Codes(facts), Is.Empty);
        }

        [Test]
        public void Adapter_count_mismatch()
        {
            var facts = Wired();
            facts.ResolvedPackageIds = facts.ResolvedPackageIds.Concat(new[] { "com.applovin.mediation.adapters.google.android" }).ToArray();
            var problems = R.Evaluate(facts);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { R.AdapterCount }));
            Assert.That(problems[0].Message, Does.Contain("2").And.Contain("1"));
        }

        [Test]
        public void Adapter_count_with_no_adapters_on_either_side_is_fine()
        {
            var facts = Wired();
            facts.ResolvedPackageIds = new[] { R.SdkPackage };
            facts.ResolvedAndroidPackages = new[] { SdkRecord };
            Assert.That(Codes(facts), Is.Empty);
        }

        // ── target scoping ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Every fact broken at once, with the wiring intact.</summary>
        private static MaxAdsPreflightFacts Broken(MaxAdsPreflightTarget target)
        {
            var facts = Wired(target);
            facts.SdkKey = "";
            facts.ConsentFlowEnabled = false;
            facts.BundledConfig = null;
            facts.ResolverPresent = true;
            facts.ResolvedAndroidPackages = new[] { "com.applovin.mediation:facebook-adapter:6.22.0.0", "com.applovin.mediation:google-adapter:1.0.0.0" };
            facts.MainTemplateGradle = "no token";
            return facts;
        }

        [Test]
        public void Android_runs_every_row()
            => Assert.That(Codes(Broken(MaxAdsPreflightTarget.Android)), Is.EqualTo(new[]
            {
                R.SdkKeyEmpty, R.ConsentFlowDisabled, R.UnitId, R.AndroidUnresolved, R.GradleDepsTokenMissing, R.AdapterCount,
            }));

        [Test]
        public void Ios_skips_the_Android_only_rows()
            => Assert.That(Codes(Broken(MaxAdsPreflightTarget.Ios)), Is.EqualTo(new[] { R.SdkKeyEmpty, R.ConsentFlowDisabled, R.UnitId }));

        [Test]
        public void Other_targets_skip_the_mobile_rows_too()
            => Assert.That(Codes(Broken(MaxAdsPreflightTarget.Other)), Is.Empty);

        [Test]
        public void Other_targets_still_get_the_wiring_rows()
        {
            var facts = Broken(MaxAdsPreflightTarget.Other);
            facts.DefineArmed = false;
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.PackageWithoutDefine, R.InstallerWithoutDefine }));
        }

        [Test]
        public void Every_problem_has_a_message()
            => Assert.That(R.Evaluate(Broken(MaxAdsPreflightTarget.Android)).Select(p => p.Message), Has.All.Not.Empty);

        // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ParseResolvedAndroidPackages_reads_the_resolution_record()
        {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<dependencies>\n  <packages>\n" +
                               "    <package> com.applovin:applovin-sdk:13.0.0 </package>\n" +
                               "    <package>com.applovin.mediation:facebook-adapter:6.22.0.0</package>\n" +
                               "  </packages>\n  <files>\n    <file>Assets/Plugins/Android/x.aar</file>\n  </files>\n" +
                               "  <settings><setting name=\"a\" value=\"b\" /></settings>\n</dependencies>\n";
            Assert.That(R.ParseResolvedAndroidPackages(xml), Is.EqualTo(new[]
            {
                "com.applovin:applovin-sdk:13.0.0", "com.applovin.mediation:facebook-adapter:6.22.0.0",
            }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("<dependencies><packages>")]
        [TestCase("not xml")]
        [TestCase("<other><packages><package>x</package></packages></other>")]
        public void ParseResolvedAndroidPackages_is_null_on_missing_or_malformed(string xml)
            => Assert.That(R.ParseResolvedAndroidPackages(xml), Is.Null);

        [Test]
        public void ParseResolvedAndroidPackages_of_an_empty_record_is_empty_not_null()
            => Assert.That(R.ParseResolvedAndroidPackages("<dependencies />"), Is.Empty);

        [Test]
        public void CountManifestAndroidAdapters_counts_only_distinct_android_adapter_packages()
            => Assert.That(R.CountManifestAndroidAdapters(new[]
            {
                "com.applovin.mediation.adapters.facebook.android", "com.applovin.mediation.adapters.facebook.android",
                "com.applovin.mediation.adapters.facebook.ios", "com.applovin.mediation.adapters.google.android",
                "com.applovin.mediation.ads", "com.applovin.mediation.adapters..android", null,
            }), Is.EqualTo(2));

        [Test]
        public void CountResolvedAdapters_counts_only_adapter_artifacts()
            => Assert.That(R.CountResolvedAdapters(new[]
            {
                "com.applovin.mediation:facebook-adapter:[6.22.0.0]", "com.applovin.mediation:google-adapter:1.0",
                "com.applovin:applovin-sdk:13.0.0", "com.google.android.gms:play-services-ads:23.0.0",
                "com.applovin.mediation:facebook-adapter:[6.22.0.0]", null,
            }), Is.EqualTo(2));

        [Test]
        public void CountResolvedAdapters_counts_two_versions_of_one_adapter_once()
        {
            Assert.That(R.CountResolvedAdapters(new[]
            {
                "com.applovin.mediation:facebook-adapter:6.22.0.0", "com.applovin.mediation:facebook-adapter:6.23.0.0",
            }), Is.EqualTo(1));

            var facts = Wired();
            facts.ResolvedAndroidPackages = new[]
            {
                SdkRecord, "com.applovin.mediation:facebook-adapter:6.22.0.0", "com.applovin.mediation:facebook-adapter:[6.23.0.0]",
            };
            Assert.That(Codes(facts), Is.Empty, "one installed adapter, one resolved artifact — no false adapter-count");
        }

        // ── consent settings file (JsonUtility output) ──────────────────────────────────────────────────

        private const string InternalSettingsJson =
            "{\n" +
            "    \"consentFlowEnabled\": true,\n" +
            "    \"consentFlowPrivacyPolicyUrl\": \"https:\\/\\/example.invalid\\/privacy?a=\\\"b\\\"\",\n" +
            "    \"consentFlowTermsOfServiceUrl\": \"\",\n" +
            "    \"shouldShowTermsAndPrivacyPolicyAlertInGDPR\": false,\n" +
            "    \"debugUserGeography\": 0,\n" +
            "    \"userTrackingUsageDescriptionEn\": \"This uses device info\",\n" +
            "    \"userTrackingUsageLocalizationEnabled\": false\n" +
            "}";

        private static MaxAdsPreflightFacts Consent(string json)
        {
            var facts = new MaxAdsPreflightFacts { ConsentFlowEnabled = true, ConsentPrivacyPolicyUrl = "stale" };
            R.ParseConsentSettings(json, facts);
            return facts;
        }

        [Test]
        public void ParseConsentSettings_reads_a_real_shaped_JsonUtility_file()
        {
            var facts = Consent(InternalSettingsJson);
            Assert.That(facts.ConsentFlowEnabled, Is.True);
            Assert.That(facts.ConsentPrivacyPolicyUrl, Is.EqualTo("https://example.invalid/privacy?a=\"b\""));
        }

        [Test]
        public void ParseConsentSettings_flow_off_is_off()
        {
            var facts = Consent(InternalSettingsJson.Replace("\"consentFlowEnabled\": true", "\"consentFlowEnabled\": false"));
            Assert.That(facts.ConsentFlowEnabled, Is.False);
        }

        [Test]
        public void ParseConsentSettings_missing_keys_read_as_disabled_and_empty()
        {
            var facts = Consent("{\n    \"debugUserGeography\": 0\n}");
            Assert.That(facts.ConsentFlowEnabled, Is.False);
            Assert.That(facts.ConsentPrivacyPolicyUrl, Is.Null);
        }

        [TestCase("{ \"consentFlowEnabled\": \"true\", \"consentFlowPrivacyPolicyUrl\": 42 }")]
        [TestCase("{ \"consentFlowEnabled\": 1 }")]
        public void ParseConsentSettings_wrong_types_read_as_disabled(string json)
        {
            var facts = Consent(json);
            Assert.That(facts.ConsentFlowEnabled, Is.False);
            Assert.That(facts.ConsentPrivacyPolicyUrl, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("\"consentFlowEnabled\": true")]
        [TestCase("{ \"consentFlowEnabled\": true, ")]
        public void ParseConsentSettings_missing_or_malformed_reads_as_disabled(string json)
        {
            var facts = Consent(json);
            Assert.That(facts.ConsentFlowEnabled, Is.False);
            Assert.That(facts.ConsentPrivacyPolicyUrl, Is.Null);
        }

        [Test]
        public void ParseConsentSettings_then_Evaluate_reports_the_disabled_flow()
        {
            var facts = Wired();
            R.ParseConsentSettings(null, facts);
            Assert.That(Codes(facts), Is.EqualTo(new[] { R.ConsentFlowDisabled }));
        }

        // ── build target mapping ────────────────────────────────────────────────────────────────────────

        [TestCase("Android", MaxAdsPreflightTarget.Android)]
        [TestCase("iOS", MaxAdsPreflightTarget.Ios)]
        [TestCase("StandaloneOSX", MaxAdsPreflightTarget.Other)]
        [TestCase("WebGL", MaxAdsPreflightTarget.Other)]
        [TestCase("android", MaxAdsPreflightTarget.Other)]
        [TestCase("", MaxAdsPreflightTarget.Other)]
        [TestCase(null, MaxAdsPreflightTarget.Other)]
        public void TargetOf_maps_build_target_names(string name, MaxAdsPreflightTarget expected)
            => Assert.That(R.TargetOf(name), Is.EqualTo(expected));

        [Test]
        public void Counting_null_is_zero()
        {
            Assert.That(R.CountManifestAndroidAdapters(null), Is.EqualTo(0));
            Assert.That(R.CountResolvedAdapters(null), Is.EqualTo(0));
        }

        // ── the consts are the metadata ─────────────────────────────────────────────────────────────────

        private static JObject PfModule => JObject.Parse(File.ReadAllText(Path.Combine(TestSupport.ModuleRoot, "pf-module.json")));

        [Test]
        public void The_consts_equal_pf_module_json()
        {
            var m = PfModule;
            Assert.Multiple(() =>
            {
                Assert.That(R.ModuleId, Is.EqualTo((string)m["id"]));
                Assert.That(R.InstallerType, Is.EqualTo((string)m["installer"]["type"]));
                Assert.That(R.Define, Is.EqualTo((string)m["define"]));
                Assert.That(((JObject)m["requires"]["dependencies"]).Properties().Select(p => p.Name), Does.Contain(R.SdkPackage));
            });
        }

        [Test]
        public void The_settings_menu_is_the_one_the_checklist_names()
        {
            var steps = ((JArray)PfModule["manualSteps"]).Select(s => (string)s).ToArray();
            var menu = R.SettingsMenu.Split('/');
            Assert.That(steps.Any(s => s.Contains(menu[0]) && s.Contains(menu[1])), Is.True, string.Join("\n", steps));
        }
    }
}
