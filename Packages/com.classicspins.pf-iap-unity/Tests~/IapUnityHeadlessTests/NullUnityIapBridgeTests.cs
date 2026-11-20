using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>
    /// The refusing bridge the installer falls back to when <c>PF_UNITY_IAP</c> is unsatisfied: every member
    /// refuses, logs the module by name and never throws — and the service behind it never grants.
    /// </summary>
    [TestFixture]
    public sealed class NullUnityIapBridgeTests
    {
        private RecordingLog _log;
        private NullUnityIapBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
            _bridge = new NullUnityIapBridge(_log);
        }

        private void AssertLoggedOnce()
        {
            Assert.That(_log.Warns, Has.Count.EqualTo(1));
            Assert.That(_log.Warns.Single(), Does.Contain("pf-iap-unity"));
        }

        [Test]
        public async Task InitializeServicesAsync_resolves_an_error_naming_the_module()
        {
            Assert.That(await _bridge.InitializeServicesAsync().AsTask(), Does.Contain("pf-iap-unity"));
            AssertLoggedOnce();
        }

        [Test]
        public async Task ConnectAsync_resolves_an_error_naming_the_module()
        {
            Assert.That(await _bridge.ConnectAsync().AsTask(), Does.Contain("pf-iap-unity"));
            AssertLoggedOnce();
        }

        [Test] public void FetchProducts_refuses() { _bridge.FetchProducts(new[] { "a" }, new[] { false }); AssertLoggedOnce(); }
        [Test] public void FetchPurchases_refuses() { _bridge.FetchPurchases(); AssertLoggedOnce(); }
        [Test] public void Purchase_refuses() { _bridge.Purchase("a"); AssertLoggedOnce(); }
        [Test] public void ConfirmPurchase_refuses() { _bridge.ConfirmPurchase("tx"); AssertLoggedOnce(); }

        [Test]
        public void RestoreTransactions_refuses_and_still_completes()
        {
            bool? ok = null;
            _bridge.RestoreTransactions((success, error) => ok = success);
            Assert.That(ok, Is.False);
            AssertLoggedOnce();
        }

        [Test]
        public async Task Behind_the_service_nothing_is_ever_bought_or_granted()
        {
            var economy = new Economy(_log);
            var analytics = new RecordingAnalytics();
            var service = new UnityIapPurchaseService(_bridge, new ListCatalog(Catalog.Default()), economy.Interpreter,
                economy.Entitlements, analytics, _log, UnityIapPlatform.Ios, new FakeDelay().Func);
            var node = new UnityIapInitNode(service, _log);

            Assert.That(await node.RunAsync(null, CancellationToken.None).AsTask(), Is.EqualTo(BootNodeResult.Degraded));
            var gold = service.Catalog.First();
            Assert.That(await service.BuyAsync(gold, CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));
            Assert.That(await service.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Failed));
            Assert.That(economy.Coins, Is.Zero);
            Assert.That(analytics.Of<ClassicSpins.PrototypeFramework.Domain.PurchaseSucceeded>(), Is.Empty);
        }
    }
}
