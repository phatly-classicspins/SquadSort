using System.Linq;
using NUnit.Framework;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>The preflight decision matrix, row by row; the editor role only gathers the facts.</summary>
    [TestFixture]
    public sealed class UnityIapPreflightRulesTests
    {
        private static UnityIapPreflightFacts Ready(UnityIapPreflightTarget target = UnityIapPreflightTarget.Android) => new UnityIapPreflightFacts
        {
            Target = target, SdkPackageVersion = "5.4.3", DefineArmed = true, InstallerSlotted = true,
            CatalogPresent = true, CloudProjectId = "abc",
        };

        private static string[] Codes(UnityIapPreflightFacts f) => UnityIapPreflightRules.Evaluate(f).Select(p => p.Code).ToArray();

        [Test]
        public void Silent_when_the_module_is_not_in_play() => Assert.That(Codes(new UnityIapPreflightFacts()), Is.Empty);

        [Test]
        public void Null_facts_are_silent() => Assert.That(UnityIapPreflightRules.Evaluate(null), Is.Empty);

        [Test]
        public void A_complete_install_passes_on_every_target()
        {
            foreach (var t in new[] { UnityIapPreflightTarget.Android, UnityIapPreflightTarget.Ios, UnityIapPreflightTarget.Other })
                Assert.That(Codes(Ready(t)), Is.Empty, t.ToString());
        }

        [Test]
        public void Define_without_package()
        {
            var f = Ready(); f.SdkPackageVersion = null;
            Assert.That(Codes(f), Is.EqualTo(new[] { UnityIapPreflightRules.DefineWithoutPackage }));
        }

        [Test]
        public void Package_without_define_names_the_floor_when_below_it()
        {
            var f = Ready(); f.DefineArmed = false; f.SdkPackageVersion = "4.15.1"; f.InstallerSlotted = false;
            var problems = UnityIapPreflightRules.Evaluate(f);
            Assert.That(problems.Select(p => p.Code), Is.EqualTo(new[] { UnityIapPreflightRules.PackageWithoutDefine }));
            Assert.That(problems.Single().Message, Does.Contain("5.0.0"));
        }

        [Test]
        public void An_installer_slotted_without_the_define()
        {
            var f = new UnityIapPreflightFacts { InstallerSlotted = true, Target = UnityIapPreflightTarget.Ios };
            Assert.That(Codes(f), Is.EqualTo(new[] { UnityIapPreflightRules.InstallerWithoutDefine }));
        }

        [Test]
        public void A_missing_catalog_fails_a_mobile_build_only()
        {
            var android = Ready(); android.CatalogPresent = false;
            var desktop = Ready(UnityIapPreflightTarget.Other); desktop.CatalogPresent = false;
            Assert.That(Codes(android), Is.EqualTo(new[] { UnityIapPreflightRules.CatalogMissing }));
            Assert.That(Codes(desktop), Is.Empty);
        }

        [Test]
        public void An_unlinked_cloud_project_is_a_warning_never_a_problem()
        {
            var f = Ready(UnityIapPreflightTarget.Ios); f.CloudProjectId = "";
            Assert.That(Codes(f), Is.Empty);
            Assert.That(UnityIapPreflightRules.Warnings(f).Single(), Does.Contain("pf-iap-unity"));
            f.CloudProjectId = null;
            Assert.That(UnityIapPreflightRules.Warnings(f), Is.Empty, "unknown is not unlinked");
        }

        [TestCase("Android", UnityIapPreflightTarget.Android)]
        [TestCase("iOS", UnityIapPreflightTarget.Ios)]
        [TestCase("StandaloneOSX", UnityIapPreflightTarget.Other)]
        [TestCase(null, UnityIapPreflightTarget.Other)]
        public void TargetOf(string name, UnityIapPreflightTarget expected) => Assert.That(UnityIapPreflightRules.TargetOf(name), Is.EqualTo(expected));

        [TestCase("4.15.1", true)]
        [TestCase("5.0.0", false)]
        [TestCase("5.4.3", false)]
        [TestCase("5.0.0-pre.1", false)]
        [TestCase("garbage", false)]
        [TestCase(null, false)]
        public void IsBelowFloor(string version, bool below) => Assert.That(UnityIapPreflightRules.IsBelowFloor(version), Is.EqualTo(below));
    }
}
