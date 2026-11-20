using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Core
{
    /// <summary>
    /// Unity IAP 5's <c>PurchaseFailureReason</c> (by name — the enum never crosses the bridge) mapped onto
    /// the framework's pinned <see cref="PurchaseResult"/>. Engine-free, so every row is proven headless.
    /// </summary>
    /// <remarks>
    /// <c>UserCancelled</c> is the player's own choice ⇒ <see cref="PurchaseResult.Cancelled"/>;
    /// <c>ExistingPurchasePending</c> means the store still holds an earlier, unfinished order for it — the
    /// outcome is not known yet ⇒ <see cref="PurchaseResult.Pending"/> (that order is fulfilled when the
    /// store delivers it). Every other reason, a null and an unknown name (a newer IAP adding a value) ⇒
    /// <see cref="PurchaseResult.Failed"/>.
    /// </remarks>
    public static class UnityIapResultMapping
    {
        public const string UserCancelled = "UserCancelled";
        public const string ExistingPurchasePending = "ExistingPurchasePending";

        public static PurchaseResult FromFailureReason(string reason)
        {
            switch (reason)
            {
                case UserCancelled: return PurchaseResult.Cancelled;
                case ExistingPurchasePending: return PurchaseResult.Pending;
                default: return PurchaseResult.Failed;
            }
        }
    }
}
