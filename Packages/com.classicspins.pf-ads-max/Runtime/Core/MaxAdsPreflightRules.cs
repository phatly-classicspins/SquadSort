using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>The build target, reduced to what the rules distinguish.</summary>
    public enum MaxAdsPreflightTarget
    {
        /// <summary>Neither Android nor iOS: MAX serves no ads there, so only the wiring rows apply.</summary>
        Other = 0,
        Android = 1,
        Ios = 2,
    }

    /// <summary>
    /// Everything the preflight rules decide from, gathered by the module's editor role. Plain settable
    /// properties so a test states a scenario in one initializer.
    /// </summary>
    public sealed class MaxAdsPreflightFacts
    {
        public MaxAdsPreflightTarget Target { get; set; }

        /// <summary>The resolved version of <see cref="MaxAdsPreflightRules.SdkPackage"/>; null when unresolved.</summary>
        public string SdkPackageVersion { get; set; }

        /// <summary>Whether this module's define is armed in the module's own assemblies.</summary>
        public bool DefineArmed { get; set; }

        /// <summary>Whether the module's installer is slotted into an enabled build scene.</summary>
        public bool InstallerSlotted { get; set; }

        /// <summary>The configured SDK key; null when it is unknowable (the define is unarmed, so the settings
        /// type cannot be named), empty when known to be empty.</summary>
        public string SdkKey { get; set; }

        /// <summary>The Integration Manager's consent-flow switch (absent settings ⇒ false).</summary>
        public bool ConsentFlowEnabled { get; set; }

        /// <summary>The consent flow's privacy-policy URL.</summary>
        public string ConsentPrivacyPolicyUrl { get; set; }

        /// <summary>Every ad placement the game uses (the codegen placement manifest); null when the project has
        /// no such manifest (which <see cref="MaxAdsPreflightRules.Warnings"/> reports).</summary>
        public IReadOnlyList<string> UsedPlacements { get; set; }

        /// <summary>The merged bundled config defaults the slotted installers register; null when none. The
        /// <c>unit-id</c> rule passes a placement when any tier of the runtime's unit lookup is non-empty here.</summary>
        public IReadOnlyDictionary<string, object> BundledConfig { get; set; }

        /// <summary>Whether the Android dependency resolver's type is loaded.</summary>
        public bool ResolverPresent { get; set; }

        /// <summary>The resolver's record of resolved Android packages (<c>group:artifact:version</c>);
        /// null when there is no record (or it is malformed).</summary>
        public IReadOnlyList<string> ResolvedAndroidPackages { get; set; }

        /// <summary>The custom main Gradle template's text; null when the project has none.</summary>
        public string MainTemplateGradle { get; set; }

        /// <summary>Every resolved UPM package id.</summary>
        public IReadOnlyList<string> ResolvedPackageIds { get; set; }
    }

    /// <summary>One named problem: a stable <see cref="Code"/> and a message a human can act on.</summary>
    public sealed class MaxAdsPreflightProblem
    {
        public MaxAdsPreflightProblem(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }

        public override string ToString() => Code + " — " + Message;
    }

    /// <summary>
    /// The decision half of the module's build preflight: facts in, named problems out. Engine-free, so
    /// every row is proven under plain <c>dotnet test</c>; the editor role only gathers the facts.
    /// </summary>
    /// <remarks>
    /// <para><b>Silent when the module is not in play.</b> SDK package unresolved, define unarmed and no
    /// installer slotted ⇒ no problem at all.</para>
    /// <para><b>Scoping.</b> The wiring rows (define / package / slot) apply to every target. MAX serves ads
    /// only on Android and iOS, so the SDK-configuration rows apply only there. The resolver resolves iOS
    /// pods after the build, so only Android has a pre-build resolved state: the resolver, Gradle-template
    /// and adapter rows are Android-only.</para>
    /// </remarks>
    public static class MaxAdsPreflightRules
    {
        public const string ModuleId = "com.classicspins.pf-ads-max";
        public const string SdkPackage = "com.applovin.mediation.ads";
        public const string InstallerType = "ClassicSpins.PrototypeFramework.Ads.Max.MaxAdsInstaller";
        public const string SettingsMenu = "AppLovin/Integration Manager";
        public const string Define = "PF_MAX";
        public const string PlacementManifest = "game.adplacements.json";

        public const string AdapterPackagePrefix = "com.applovin.mediation.adapters.";
        public const string AdapterPackageAndroidSuffix = ".android";
        public const string SdkArtifactPrefix = "com.applovin:applovin-sdk:";
        public const string GradleDepsToken = "**DEPS**";

        public const string DefineWithoutPackage = "define-without-package";
        public const string PackageWithoutDefine = "package-without-define";
        public const string InstallerWithoutDefine = "installer-without-define";
        public const string SdkKeyEmpty = "sdk-key-empty";
        public const string ConsentFlowDisabled = "consent-flow-disabled";
        public const string ConsentPrivacyUrl = "consent-privacy-url";
        public const string UnitId = "unit-id";
        public const string Edm4uMissing = "edm4u-missing";
        public const string AndroidUnresolved = "android-unresolved";
        public const string GradleDepsTokenMissing = "gradle-deps-token";
        public const string AdapterCount = "adapter-count";

        /// <summary>Bundled config flag (non-zero ⇒ on): a placement with no unit id is a build <i>warning</i>, not a
        /// failure. For a SKU that ships before every ad unit exists — at runtime such a placement is simply never
        /// ready (interstitial skipped, rewarded refused, banner not shown). Bundled only; a remote value is ignored.</summary>
        public const string AllowMissingUnitIdsKey = "ads.max.allowMissingUnitIds";

        private static readonly Regex ResolvedAdapter = new Regex(@"^(com\.applovin\.mediation:[^:]+-adapter):.*$", RegexOptions.CultureInvariant);
        private static readonly Regex ConsentEnabledKey = new Regex(@"""consentFlowEnabled""\s*:\s*(true|false)\b", RegexOptions.CultureInvariant);
        private static readonly Regex ConsentUrlKey = new Regex(@"""consentFlowPrivacyPolicyUrl""\s*:\s*""((?:[^""\\]|\\.)*)""", RegexOptions.CultureInvariant);

        /// <summary>Every problem the facts show, in matrix order; empty when there is none.</summary>
        public static IReadOnlyList<MaxAdsPreflightProblem> Evaluate(MaxAdsPreflightFacts facts)
        {
            var problems = new List<MaxAdsPreflightProblem>();
            if (facts == null) return problems;

            var present = !string.IsNullOrWhiteSpace(facts.SdkPackageVersion);
            var armed = facts.DefineArmed;
            if (!present && !armed && !facts.InstallerSlotted) return problems;

            // ── wiring: every target ────────────────────────────────────────────────────────────────
            if (armed && !present)
                problems.Add(new MaxAdsPreflightProblem(DefineWithoutPackage,
                    Define + " is armed but " + SdkPackage + " is not resolved: the build would compile against an SDK "
                    + "it does not ship. Install the MAX package through UPM, or remove whatever arms " + Define + "."));

            if (present && !armed)
                problems.Add(new MaxAdsPreflightProblem(PackageWithoutDefine,
                    SdkPackage + " " + facts.SdkPackageVersion + " is resolved but " + Define + " is not armed"
                    + (IsBelowUpmFloor(facts.SdkPackageVersion) ? " (it arms only from 8.0.0, the first UPM distribution)" : "")
                    + ": the build would ship the refusing null bridge, so every ad is refused."));

            if (facts.InstallerSlotted && !armed)
                problems.Add(new MaxAdsPreflightProblem(InstallerWithoutDefine,
                    "MaxAdsInstaller is slotted into a build scene but " + Define + " is not armed: the build would "
                    + "ship the refusing null bridge, so every ad is refused. Install " + SdkPackage + " (8.0.0 or later)."));

            if (!present) return problems;
            var mobile = facts.Target == MaxAdsPreflightTarget.Android || facts.Target == MaxAdsPreflightTarget.Ios;
            if (!mobile) return problems;

            // ── SDK configuration: Android and iOS ──────────────────────────────────────────────────
            if (armed && facts.SdkKey != null && string.IsNullOrWhiteSpace(facts.SdkKey))
                problems.Add(new MaxAdsPreflightProblem(SdkKeyEmpty,
                    "the MAX SDK key is empty: SDK init fails at runtime and ads never become ready. Paste it in "
                    + SettingsMenu + "."));

            if (!facts.ConsentFlowEnabled)
                problems.Add(new MaxAdsPreflightProblem(ConsentFlowDisabled,
                    "MAX's consent flow (Terms and Privacy Policy) is disabled: players in regions that require "
                    + "consent are never asked and are served non-personalized ads. Enable it in " + SettingsMenu + "."));
            else if (string.IsNullOrWhiteSpace(facts.ConsentPrivacyPolicyUrl))
                problems.Add(new MaxAdsPreflightProblem(ConsentPrivacyUrl,
                    "MAX's consent flow is enabled with an empty privacy-policy URL. Fill it in " + SettingsMenu + "."));

            if (!IsNonZero(facts.BundledConfig, AllowMissingUnitIdsKey))
                foreach (var message in MissingUnitIds(facts))
                    problems.Add(new MaxAdsPreflightProblem(UnitId, message));

            if (facts.Target != MaxAdsPreflightTarget.Android) return problems;

            // ── Android dependency resolution: Android only ─────────────────────────────────────────
            if (!facts.ResolverPresent)
                problems.Add(new MaxAdsPreflightProblem(Edm4uMissing,
                    "the External Dependency Manager (com.google.external-dependency-manager) is not loaded: MAX's "
                    + "native Android libraries and every adapter would be left out of the APK."));
            else if (facts.ResolvedAndroidPackages == null
                     || !facts.ResolvedAndroidPackages.Any(p => p != null && p.StartsWith(SdkArtifactPrefix, StringComparison.Ordinal)))
                problems.Add(new MaxAdsPreflightProblem(AndroidUnresolved,
                    "Android dependencies are not resolved (no " + SdkArtifactPrefix + "* in the resolver's resolution file): "
                    + "the APK would build green without MAX's native libraries. Run Assets ▸ External Dependency "
                    + "Manager ▸ Android Resolver ▸ Force Resolve."));

            if (facts.MainTemplateGradle != null && facts.MainTemplateGradle.IndexOf(GradleDepsToken, StringComparison.Ordinal) < 0)
                problems.Add(new MaxAdsPreflightProblem(GradleDepsTokenMissing,
                    "Assets/Plugins/Android/mainTemplate.gradle has no " + GradleDepsToken + " token: the resolver "
                    + "cannot inject the native dependencies, so they would be missing from the APK."));

            if (facts.ResolvedAndroidPackages != null)
            {
                var declared = CountManifestAndroidAdapters(facts.ResolvedPackageIds);
                var resolved = CountResolvedAdapters(facts.ResolvedAndroidPackages);
                if (declared != resolved)
                    problems.Add(new MaxAdsPreflightProblem(AdapterCount,
                        declared + " Android adapter package(s) are installed but " + resolved + " adapter artifact(s) "
                        + "are resolved: the mediation waterfall would not match the networks you ticked. Resolve "
                        + "Android dependencies again."));
            }

            return problems;
        }

        /// <summary>The placement-tier config key of a placement's MAX unit id (<c>ads.max.&lt;placement&gt;.unitId</c>).</summary>
        public static string UnitIdKey(string placement) => MaxAdsService.UnitIdKey(new AdPlacement(placement));

        /// <summary>The platform token <see cref="MaxAdUnits"/> uses for a build target; null for Other.</summary>
        public static string PlatformOf(MaxAdsPreflightTarget target)
            => target == MaxAdsPreflightTarget.Android ? MaxAdUnits.Android
                : target == MaxAdsPreflightTarget.Ios ? MaxAdUnits.Ios
                : null;

        /// <summary>
        /// Every key the runtime would try for <paramref name="placement"/> on <paramref name="target"/>, in the
        /// runtime's lookup order — the format read from the same bundled config: <c>ads.&lt;placement&gt;.banner</c>
        /// non-zero ⇒ banner (<see cref="MaxBannerAds.KeysFor"/>, never an interstitial or rewarded default);
        /// else <c>ads.&lt;placement&gt;.rewarded</c> non-zero ⇒ rewarded; else interstitial.
        /// </summary>
        public static IReadOnlyList<string> UnitIdKeys(MaxAdsPreflightTarget target, string placement, IReadOnlyDictionary<string, object> bundledConfig)
            => IsNonZero(bundledConfig, MaxBannerAds.BannerKey(placement))
                ? MaxBannerAds.KeysFor(PlatformOf(target), placement)
                : MaxAdUnits.UnitIdKeys(PlatformOf(target), placement, IsNonZero(bundledConfig, MaxAdUnits.RewardedKey(placement)));

        /// <summary>
        /// Advisory lines the editor role logs as warnings — never build failures. Today one: the installer is
        /// slotted for Android/iOS but the project has no <c>game.adplacements.json</c>, or that manifest lists
        /// zero placements, so the <c>unit-id</c> rule has no placement to check either way.
        /// </summary>
        public static IReadOnlyList<string> Warnings(MaxAdsPreflightFacts facts)
        {
            var warnings = new List<string>();
            if (facts == null) return warnings;
            var mobile = facts.Target == MaxAdsPreflightTarget.Android || facts.Target == MaxAdsPreflightTarget.Ios;
            if (mobile && !string.IsNullOrWhiteSpace(facts.SdkPackageVersion) && IsNonZero(facts.BundledConfig, AllowMissingUnitIdsKey))
                foreach (var message in MissingUnitIds(facts))
                    warnings.Add("[pf-ads-max] " + ModuleId + ": " + UnitId + " (waived by " + AllowMissingUnitIdsKey + ") — " + message);
            if (facts.InstallerSlotted && mobile && (facts.UsedPlacements == null || facts.UsedPlacements.Count == 0))
                warnings.Add("[pf-ads-max] " + ModuleId + ": MaxAdsInstaller is slotted for " + facts.Target
                             + " but there is no " + PlacementManifest + " codegen manifest, or it lists no placements, "
                             + "so the unit-id preflight checks no placement. Add the manifest (one entry per ad "
                             + "placement) so a missing ad unit fails the build instead of every show at runtime.");
            return warnings;
        }

        /// <summary>One message per used placement whose unit lookup is empty in the bundled config.</summary>
        private static IEnumerable<string> MissingUnitIds(MaxAdsPreflightFacts facts)
        {
            if (facts.UsedPlacements == null) yield break;
            foreach (var placement in facts.UsedPlacements.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal))
            {
                var keys = UnitIdKeys(facts.Target, placement, facts.BundledConfig);
                if (!keys.Any(k => HasNonEmptyString(facts.BundledConfig, k)))
                    yield return "no bundled config value for '" + UnitIdKey(placement) + "' or any fallback ('"
                                 + string.Join("', '", keys.Where(k => k != UnitIdKey(placement))) + "'): every show of placement '"
                                 + placement + "' would fail. Add one to the bundled config defaults (a remote-only value can fail to arrive)."
                                 + (IsNonZero(facts.BundledConfig, MaxBannerAds.BannerKey(placement)) ? ""
                                     : " A banner placement also needs '" + MaxBannerAds.BannerKey(placement) + "' = 1 so its banner keys are checked.");
            }
        }

        /// <summary>
        /// The <c>&lt;dependencies&gt;&lt;packages&gt;&lt;package&gt;</c> entries of the resolver's
        /// resolution record, trimmed; null when <paramref name="xml"/> is missing, malformed or not a
        /// resolution record.
        /// </summary>
        public static IReadOnlyList<string> ParseResolvedAndroidPackages(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (XmlException) { return null; }

            var root = doc.Root;
            if (root == null || root.Name.LocalName != "dependencies") return null;
            var list = new List<string>();
            foreach (var packages in root.Elements().Where(e => e.Name.LocalName == "packages"))
                foreach (var package in packages.Elements().Where(e => e.Name.LocalName == "package"))
                {
                    var value = package.Value == null ? "" : package.Value.Trim();
                    if (value.Length > 0) list.Add(value);
                }
            return list;
        }

        /// <summary>How many distinct <c>com.applovin.mediation.adapters.*.android</c> packages are installed.</summary>
        public static int CountManifestAndroidAdapters(IEnumerable<string> packageIds)
        {
            if (packageIds == null) return 0;
            return packageIds
                .Where(id => id != null
                             && id.StartsWith(AdapterPackagePrefix, StringComparison.Ordinal)
                             && id.EndsWith(AdapterPackageAndroidSuffix, StringComparison.Ordinal)
                             && id.Length > AdapterPackagePrefix.Length + AdapterPackageAndroidSuffix.Length)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        /// <summary>How many distinct <c>com.applovin.mediation:*-adapter</c> artifacts are resolved —
        /// distinct by <c>group:artifact</c>, so two versions of one adapter count once.</summary>
        public static int CountResolvedAdapters(IEnumerable<string> resolvedKeys)
        {
            if (resolvedKeys == null) return 0;
            return resolvedKeys
                .Where(k => k != null)
                .Select(k => ResolvedAdapter.Match(k.Trim()))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        /// <summary>
        /// Fill <see cref="MaxAdsPreflightFacts.ConsentFlowEnabled"/> and
        /// <see cref="MaxAdsPreflightFacts.ConsentPrivacyPolicyUrl"/> from the Integration Manager's internal
        /// settings file (JsonUtility output: <c>"consentFlowEnabled": true</c>,
        /// <c>"consentFlowPrivacyPolicyUrl": "…"</c>). A missing, malformed or wrongly typed value reads as
        /// disabled / empty — never as configured.
        /// </summary>
        public static void ParseConsentSettings(string json, MaxAdsPreflightFacts facts)
        {
            if (facts == null) return;
            facts.ConsentFlowEnabled = false;
            facts.ConsentPrivacyPolicyUrl = null;
            if (string.IsNullOrWhiteSpace(json)) return;
            var text = json.Trim();
            if (!text.StartsWith("{", StringComparison.Ordinal) || !text.EndsWith("}", StringComparison.Ordinal)) return;

            var enabled = ConsentEnabledKey.Match(text);
            facts.ConsentFlowEnabled = enabled.Success && enabled.Groups[1].Value == "true";

            var url = ConsentUrlKey.Match(text);
            if (!url.Success) return;
            try { facts.ConsentPrivacyPolicyUrl = Regex.Unescape(url.Groups[1].Value); }
            catch (ArgumentException) { facts.ConsentPrivacyPolicyUrl = url.Groups[1].Value; }
        }

        /// <summary>The rules' target for a <c>BuildTarget</c> name: <c>"Android"</c>, <c>"iOS"</c>, else Other.</summary>
        public static MaxAdsPreflightTarget TargetOf(string buildTargetName)
        {
            if (string.Equals(buildTargetName, "Android", StringComparison.Ordinal)) return MaxAdsPreflightTarget.Android;
            if (string.Equals(buildTargetName, "iOS", StringComparison.Ordinal)) return MaxAdsPreflightTarget.Ios;
            return MaxAdsPreflightTarget.Other;
        }

        private static bool HasNonEmptyString(IReadOnlyDictionary<string, object> config, string key)
        {
            object value;
            return config != null && config.TryGetValue(key, out value) && value is string s && !string.IsNullOrWhiteSpace(s);
        }

        /// <summary>
        /// Mirrors the runtime's own read of <c>ads.&lt;placement&gt;.rewarded</c>
        /// (<c>ConfigKey&lt;int&gt;</c>, and <c>GameConfigCore</c> decides presence with <c>v is T</c>): only a
        /// bundled <see cref="int"/> counts, and only when non-zero. A bundled <c>1L</c> (long) or <c>1.0</c>
        /// (double) is invisible to the runtime's <c>int</c> read, so it must be invisible here too —
        /// otherwise the preflight would pass a placement the runtime reads as interstitial.
        /// </summary>
        private static bool IsNonZero(IReadOnlyDictionary<string, object> config, string key)
        {
            object value;
            return config != null && config.TryGetValue(key, out value) && value is int i && i != 0;
        }

        private static bool IsBelowUpmFloor(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            var head = version.Split('.')[0];
            int major;
            return int.TryParse(head, out major) && major < 8;
        }
    }
}
