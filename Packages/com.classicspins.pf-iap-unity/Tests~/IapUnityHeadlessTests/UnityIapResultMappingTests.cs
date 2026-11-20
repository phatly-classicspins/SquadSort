using System;
using NUnit.Framework;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>Every Unity IAP 5 <c>PurchaseFailureReason</c> name onto the pinned <see cref="PurchaseResult"/>.</summary>
    [TestFixture]
    public sealed class UnityIapResultMappingTests
    {
        [TestCase("UserCancelled", PurchaseResult.Cancelled)]
        [TestCase("ExistingPurchasePending", PurchaseResult.Pending)]
        [TestCase("DuplicateTransaction", PurchaseResult.Failed)]
        [TestCase("PaymentDeclined", PurchaseResult.Failed)]
        [TestCase("ProductUnavailable", PurchaseResult.Failed)]
        [TestCase("PurchaseMissing", PurchaseResult.Failed)]
        [TestCase("PurchasingUnavailable", PurchaseResult.Failed)]
        [TestCase("SignatureInvalid", PurchaseResult.Failed)]
        [TestCase("StoreNotConnected", PurchaseResult.Failed)]
        [TestCase("Unknown", PurchaseResult.Failed)]
        [TestCase("ValidationFailure", PurchaseResult.Failed)]
        [TestCase("AReasonANewerIapAdds", PurchaseResult.Failed)]
        [TestCase(null, PurchaseResult.Failed)]
        [TestCase("usercancelled", PurchaseResult.Failed)]
        public void Maps(string reason, PurchaseResult expected)
            => Assert.That(UnityIapResultMapping.FromFailureReason(reason), Is.EqualTo(expected));

        [Test]
        public void Never_maps_to_Success_or_Deferred()
        {
            foreach (var reason in new[] { "UserCancelled", "ExistingPurchasePending", "Unknown", null, "" })
                Assert.That(UnityIapResultMapping.FromFailureReason(reason),
                    Is.Not.EqualTo(PurchaseResult.Success).And.Not.EqualTo(PurchaseResult.Deferred));
        }
    }
}
