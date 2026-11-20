using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.HeadlessTests
{
    public sealed class AdjustAnalyticsBuildRulesTests
    {
        [Test] public void Unrelated_absence_is_inert_even_with_probe_notes()
        { var facts = new AdjustPreflightFacts(); facts.Notes.Add("unrelated probe"); Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts), Is.Empty); Assert.That(AdjustAnalyticsBuildRules.Evaluate(null), Is.Empty); }
        [Test] public void Half_installed_module_names_both_missing_bundled_token_and_SDK()
        { var problems = AdjustAnalyticsBuildRules.Evaluate(new AdjustPreflightFacts { InPlay = true }); Assert.That(problems, Has.Count.EqualTo(2)); Assert.That(problems[0], Does.Contain("PF_ADJUST").And.Contain("com.adjust.sdk 5.8.2")); Assert.That(problems[1], Does.Contain("analytics.adjust.appToken")); }
        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase(123)] [TestCase("short")] [TestCase("thirteenchars")] [TestCase("            ")]
        public void Missing_or_wrong_type_token_is_a_named_problem(object token)
        { var facts = Valid(); facts.BundledDefaults = new Dictionary<string, object> { ["analytics.adjust.appToken"] = token }; Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts), Has.Count.EqualTo(1)); }
        [Test] public void Valid_bundled_token_and_available_SDK_pass_without_token_map_or_config_mutation()
        { var facts = Valid(); Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts), Is.Empty); Assert.That(facts.BundledDefaults, Has.Count.EqualTo(1)); }
        [Test] public void Probe_notes_are_propagated_not_silently_ignored()
        { var facts = Valid(); facts.Notes.Add("Installer X could not resolve ConfigDefaults"); Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts)[0], Does.Contain("Installer X")); }
        [TestCase(null)] [TestCase("sandbox")] [TestCase("production")]
        public void Absent_or_explicit_valid_environment_passes_shared_runtime_rule(object environment)
        {
            var defaults = new Dictionary<string, object> { ["analytics.adjust.appToken"] = "abcdefghijkl" };
            if (environment != null) defaults["analytics.adjust.environment"] = environment;
            var facts = Valid(); facts.BundledDefaults = defaults;
            Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts), Is.Empty);
            Assert.That(AdjustAnalyticsBuildRules.TryEnvironment(environment, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(environment ?? "sandbox"));
        }
        [TestCase(0)] [TestCase(false)] [TestCase("")] [TestCase("Production")] [TestCase("other")]
        public void Wrong_type_or_explicit_invalid_environment_is_named_in_preflight_and_runtime_rule(object environment)
        {
            var facts = Valid(); facts.BundledDefaults = new Dictionary<string, object> { ["analytics.adjust.appToken"] = "abcdefghijkl", ["analytics.adjust.environment"] = environment };
            Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts), Has.Count.EqualTo(1));
            Assert.That(AdjustAnalyticsBuildRules.Evaluate(facts)[0], Does.Contain("analytics.adjust.environment"));
            Assert.That(AdjustAnalyticsBuildRules.TryEnvironment(environment, out _), Is.False);
        }
        private static AdjustPreflightFacts Valid() => new AdjustPreflightFacts { InPlay = true, SdkAvailable = true, BundledDefaults = new Dictionary<string, object> { ["analytics.adjust.appToken"] = "abcdefghijkl" } };
    }
}
