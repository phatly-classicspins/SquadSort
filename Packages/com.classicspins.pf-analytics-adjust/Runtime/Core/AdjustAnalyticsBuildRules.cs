using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.Core
{
    public sealed class AdjustPreflightFacts
    {
        public bool InPlay { get; set; }
        public bool SdkAvailable { get; set; }
        public IReadOnlyDictionary<string, object> BundledDefaults { get; set; }
        public readonly List<string> Notes = new List<string>();
    }
    /// <summary>Pure, read-only build decisions. A remote-only app token cannot satisfy a shipped build.</summary>
    public static class AdjustAnalyticsBuildRules
    {
        public const string ModuleId = "com.classicspins.pf-analytics-adjust";
        public const string InstallerType = "ClassicSpins.PrototypeFramework.Analytics.Adjust.AdjustAnalyticsInstaller";
        public static bool IsAppTokenValid(object value)
            => value is string text && text.Length == 12 && !string.IsNullOrWhiteSpace(text);
        public static bool TryEnvironment(object value, out string environment)
        {
            environment = value == null ? "sandbox" : value as string;
            return environment == "sandbox" || environment == "production";
        }
        public static IReadOnlyList<string> Evaluate(AdjustPreflightFacts facts)
        {
            var problems = new List<string>();
            if (facts == null || !facts.InPlay) return problems;
            if (!facts.SdkAvailable) problems.Add("PF_ADJUST SDK unavailable; install com.adjust.sdk 5.8.2 and EDM4U 1.2.190 through Wizard Install and resolve packages.");
            object token = null;
            facts.BundledDefaults?.TryGetValue("analytics.adjust.appToken", out token);
            if (!IsAppTokenValid(token))
                problems.Add("missing/invalid analytics.adjust.appToken in bundled ConfigDefaults; a nonblank 12-character string is required (remote-only configuration cannot satisfy preflight).");
            object environment = null;
            facts.BundledDefaults?.TryGetValue("analytics.adjust.environment", out environment);
            if (!TryEnvironment(environment, out _))
                problems.Add("invalid analytics.adjust.environment in bundled ConfigDefaults; omit it for sandbox or use exactly sandbox/production.");
            foreach (var note in facts.Notes) problems.Add("bundled-default probe: " + note);
            return problems;
        }
    }
}
