using System;
using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Core
{
    /// <summary>The build target, reduced to what the rules distinguish.</summary>
    public enum UnityIapPreflightTarget
    {
        /// <summary>Neither Android nor iOS: no store sells there, so only the wiring rows apply.</summary>
        Other = 0,
        Android = 1,
        Ios = 2,
    }

    /// <summary>Everything the preflight rules decide from, gathered by the module's editor role.</summary>
    public sealed class UnityIapPreflightFacts
    {
        public UnityIapPreflightTarget Target { get; set; }

        /// <summary>The resolved version of <see cref="UnityIapPreflightRules.SdkPackage"/>; null when unresolved.</summary>
        public string SdkPackageVersion { get; set; }

        /// <summary>Whether this module's define is armed in the module's own assemblies.</summary>
        public bool DefineArmed { get; set; }

        /// <summary>Whether the module's installer is slotted into an enabled build scene.</summary>
        public bool InstallerSlotted { get; set; }

        /// <summary>Whether the bundled catalog (<c>Resources/Config/products.json</c>) exists.</summary>
        public bool CatalogPresent { get; set; }

        /// <summary>The Unity Cloud project id the project is linked to; empty when unlinked, null when unknown.</summary>
        public string CloudProjectId { get; set; }
    }

    /// <summary>One named problem: a stable <see cref="Code"/> and a message a human can act on.</summary>
    public sealed class UnityIapPreflightProblem
    {
        public UnityIapPreflightProblem(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }

        public override string ToString() => Code + " — " + Message;
    }

    /// <summary>
    /// The decision half of the module's build preflight: facts in, named problems out. Engine-free, so every
    /// row is proven under plain <c>dotnet test</c>; the editor role only gathers the facts.
    /// </summary>
    /// <remarks>
    /// Silent when the module is not in play (package unresolved, define unarmed, no installer slotted). The
    /// wiring rows apply to every target; the catalog row only where a store sells (Android, iOS). The cloud
    /// project link is advisory (<see cref="Warnings"/>) — it never fails a build.
    /// </remarks>
    public static class UnityIapPreflightRules
    {
        public const string ModuleId = "com.classicspins.pf-iap-unity";
        public const string SdkPackage = "com.unity.purchasing";
        public const string SdkFloor = "5.0.0";
        public const string InstallerType = "ClassicSpins.PrototypeFramework.Iap.Unity.UnityIapInstaller";
        public const string Define = "PF_UNITY_IAP";
        public const string CatalogPath = "Resources/Config/products.json";

        public const string DefineWithoutPackage = "define-without-package";
        public const string PackageWithoutDefine = "package-without-define";
        public const string InstallerWithoutDefine = "installer-without-define";
        public const string CatalogMissing = "catalog-missing";

        /// <summary>Every problem the facts show, in matrix order; empty when there is none.</summary>
        public static IReadOnlyList<UnityIapPreflightProblem> Evaluate(UnityIapPreflightFacts facts)
        {
            var problems = new List<UnityIapPreflightProblem>();
            if (facts == null || !InPlay(facts)) return problems;

            var present = !string.IsNullOrWhiteSpace(facts.SdkPackageVersion);
            var armed = facts.DefineArmed;

            if (armed && !present)
                problems.Add(new UnityIapPreflightProblem(DefineWithoutPackage,
                    Define + " is armed but " + SdkPackage + " is not resolved: the build would compile against an SDK it "
                    + "does not ship. Install it through UPM, or remove whatever arms " + Define + "."));

            if (present && !armed)
                problems.Add(new UnityIapPreflightProblem(PackageWithoutDefine,
                    SdkPackage + " " + facts.SdkPackageVersion + " is resolved but " + Define + " is not armed"
                    + (IsBelowFloor(facts.SdkPackageVersion) ? " (it arms only from " + SdkFloor + ", the IAP 5 API this module is written against)" : "")
                    + ": the build would ship the refusing null bridge, so every purchase fails."));

            if (facts.InstallerSlotted && !armed)
                problems.Add(new UnityIapPreflightProblem(InstallerWithoutDefine,
                    "UnityIapInstaller is slotted into a build scene but " + Define + " is not armed: the build would ship "
                    + "the refusing null bridge, so every purchase fails. Install " + SdkPackage + " (" + SdkFloor + " or later)."));

            var mobile = facts.Target == UnityIapPreflightTarget.Android || facts.Target == UnityIapPreflightTarget.Ios;
            if (mobile && armed && !facts.CatalogPresent)
                problems.Add(new UnityIapPreflightProblem(CatalogMissing,
                    "no bundled catalog at " + CatalogPath + ": the store is asked for no product, so nothing can be bought."));

            return problems;
        }

        /// <summary>Advisory lines, logged and never a build failure.</summary>
        public static IReadOnlyList<string> Warnings(UnityIapPreflightFacts facts)
        {
            var warnings = new List<string>();
            if (facts == null || !InPlay(facts) || !facts.DefineArmed) return warnings;
            var mobile = facts.Target == UnityIapPreflightTarget.Android || facts.Target == UnityIapPreflightTarget.Ios;
            if (mobile && facts.CloudProjectId != null && string.IsNullOrWhiteSpace(facts.CloudProjectId))
                warnings.Add("[" + ModuleId + "] the project is not linked to a Unity Cloud project (Edit ▸ Project Settings ▸ "
                             + "Services). Unity IAP 5 initializes Unity Gaming Services first; link the project if the store "
                             + "fails to connect.");
            return warnings;
        }

        private static bool InPlay(UnityIapPreflightFacts facts)
            => !string.IsNullOrWhiteSpace(facts.SdkPackageVersion) || facts.DefineArmed || facts.InstallerSlotted;

        /// <summary>The build target name (<c>BuildTarget.ToString()</c>) reduced to a rules target.</summary>
        public static UnityIapPreflightTarget TargetOf(string buildTarget)
        {
            switch (buildTarget)
            {
                case "Android": return UnityIapPreflightTarget.Android;
                case "iOS": return UnityIapPreflightTarget.Ios;
                default: return UnityIapPreflightTarget.Other;
            }
        }

        /// <summary>Whether <paramref name="version"/> is below <see cref="SdkFloor"/>; an unparseable version is not.</summary>
        public static bool IsBelowFloor(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            var core = version.Split('-')[0];
            return Version.TryParse(core, out var v) && v < Version.Parse(SdkFloor);
        }
    }
}
