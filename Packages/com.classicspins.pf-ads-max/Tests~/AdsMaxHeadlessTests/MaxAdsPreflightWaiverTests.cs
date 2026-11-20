using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using NUnit.Framework;
using R = ClassicSpins.PrototypeFramework.Ads.Max.Core.MaxAdsPreflightRules;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary><c>ads.max.allowMissingUnitIds</c>: a missing unit id becomes a warning, never a build failure.</summary>
    [TestFixture]
    public sealed class MaxAdsPreflightWaiverTests
    {
        private static MaxAdsPreflightFacts Facts(Dictionary<string, object> config, MaxAdsPreflightTarget target = MaxAdsPreflightTarget.Ios) => new MaxAdsPreflightFacts
        {
            Target = target,
            SdkPackageVersion = "8.6.5",
            DefineArmed = true,
            InstallerSlotted = true,
            SdkKey = "key",
            ConsentFlowEnabled = true,
            ConsentPrivacyPolicyUrl = "https://example.invalid/privacy",
            UsedPlacements = new[] { "rewarded_revive", "interstitial_level_end" },
            BundledConfig = config,
        };

        [Test]
        public void Without_the_flag_a_missing_unit_id_fails_the_build()
        {
            var facts = Facts(new Dictionary<string, object> { ["ads.rewarded_revive.rewarded"] = 1 });
            Assert.That(R.Evaluate(facts).Select(p => p.Code), Is.EqualTo(new[] { R.UnitId, R.UnitId }));
            Assert.That(R.Warnings(facts), Is.Empty);
        }

        [Test]
        public void With_the_flag_it_is_a_warning_per_missing_placement_only()
        {
            var facts = Facts(new Dictionary<string, object>
            {
                ["ads.rewarded_revive.rewarded"] = 1,
                ["ads.max.ios.rewarded.unitId"] = "rv-unit",
                [R.AllowMissingUnitIdsKey] = 1,
            });
            Assert.That(R.Evaluate(facts), Is.Empty);
            var warnings = R.Warnings(facts);
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("interstitial_level_end").And.Contain(R.AllowMissingUnitIdsKey));
        }

        [Test]
        public void The_flag_waives_nothing_else()
        {
            var facts = Facts(new Dictionary<string, object> { [R.AllowMissingUnitIdsKey] = 1 });
            facts.SdkKey = "";
            Assert.That(R.Evaluate(facts).Select(p => p.Code), Is.EqualTo(new[] { R.SdkKeyEmpty }));
        }

        [Test]
        public void A_zero_or_non_int_flag_is_off()
        {
            Assert.That(R.Evaluate(Facts(new Dictionary<string, object> { [R.AllowMissingUnitIdsKey] = 0 })), Has.Count.EqualTo(2));
            Assert.That(R.Evaluate(Facts(new Dictionary<string, object> { [R.AllowMissingUnitIdsKey] = "1" })), Has.Count.EqualTo(2));
        }
    }

    /// <summary>A banner placement (<c>ads.&lt;p&gt;.banner = 1</c>) is checked against the keys the banner runtime reads.</summary>
    [TestFixture]
    public sealed class MaxAdsPreflightBannerTests
    {
        private const string Placement = "banner_gameplay";

        private static MaxAdsPreflightFacts Facts(Dictionary<string, object> config) => new MaxAdsPreflightFacts
        {
            Target = MaxAdsPreflightTarget.Android,
            SdkPackageVersion = "8.6.5",
            DefineArmed = true,
            InstallerSlotted = true,
            SdkKey = "key",
            ConsentFlowEnabled = true,
            ConsentPrivacyPolicyUrl = "https://example.invalid/privacy",
            UsedPlacements = new[] { Placement },
            BundledConfig = config,
        };

        private static IEnumerable<string> UnitIdProblems(Dictionary<string, object> config)
            => R.Evaluate(Facts(config)).Where(p => p.Code == R.UnitId).Select(p => p.Message);

        [Test]
        public void The_banner_keys_are_the_runtime_keys()
            => Assert.That(R.UnitIdKeys(MaxAdsPreflightTarget.Android, Placement,
                    new Dictionary<string, object> { [MaxBannerAds.BannerKey(Placement)] = 1 }),
                Is.EqualTo(MaxBannerAds.KeysFor(MaxAdUnits.Android, Placement)));

        [Test]
        public void A_banner_format_default_satisfies_a_banner_placement()
            => Assert.That(UnitIdProblems(new Dictionary<string, object>
            {
                [MaxBannerAds.BannerKey(Placement)] = 1,
                ["ads.max.banner.unitId"] = "banner-unit",
            }), Is.Empty);

        [Test]
        public void An_interstitial_default_never_satisfies_a_banner_placement()
            => Assert.That(UnitIdProblems(new Dictionary<string, object>
            {
                [MaxBannerAds.BannerKey(Placement)] = 1,
                ["ads.max.interstitial.unitId"] = "inter-unit",
            }).ToList(), Has.Count.EqualTo(1));

        [Test]
        public void An_unmarked_placement_is_told_about_the_banner_marker()
            => Assert.That(UnitIdProblems(new Dictionary<string, object>()).Single(),
                Does.Contain(MaxBannerAds.BannerKey(Placement)));
    }
}
