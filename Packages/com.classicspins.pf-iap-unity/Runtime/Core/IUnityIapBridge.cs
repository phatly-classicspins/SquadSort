using System;
using Cysharp.Threading.Tasks;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Core
{
    /// <summary>One product as the store priced it — primitives only, so no Unity IAP type crosses the bridge.</summary>
    public readonly struct UnityIapStoreProduct
    {
        public UnityIapStoreProduct(string id, string localizedPrice, bool available)
        {
            Id = id;
            LocalizedPrice = localizedPrice;
            Available = available;
        }

        /// <summary>The catalog product id (Unity IAP's <c>definition.id</c>).</summary>
        public string Id { get; }

        /// <summary>The store's localized price string (<c>metadata.localizedPriceString</c>).</summary>
        public string LocalizedPrice { get; }

        /// <summary>The store's stock answer (<c>availableToPurchase</c>).</summary>
        public bool Available { get; }
    }

    /// <summary>
    /// The SDK boundary: the thin shell over Unity IAP 5's <c>StoreController</c>, implemented by
    /// <c>Sdk/UnityIapBridge</c> (the only file naming a Unity IAP type) and by the refusing
    /// <c>NullUnityIapBridge</c>. It speaks <b>primitives only</b> — every decision (result mapping,
    /// grant-once, restore, init) lives in <see cref="UnityIapPurchaseService"/>, under <c>dotnet test</c>.
    /// </summary>
    /// <remarks>
    /// Every event is raised on the thread Unity IAP raises its own (the main thread). An order is
    /// addressed by its store transaction id: the bridge keeps the pending order behind it, so
    /// <see cref="ConfirmPurchase"/> needs nothing but the id.
    /// </remarks>
    public interface IUnityIapBridge
    {
        /// <summary>Initialize Unity Gaming Services, unless something (the SKU, another module) already did.
        /// Resolves <c>null</c> on success, else the failure as text; never throws. A separate step from
        /// <see cref="ConnectAsync"/> so a UGS failure is reported on its own and never stops the store
        /// connecting.</summary>
        UniTask<string> InitializeServicesAsync();

        /// <summary>Connect the store — Unity IAP's <c>StoreController</c> only, no UGS work. Resolves
        /// <c>null</c> on success, else the failure as text; never throws.</summary>
        UniTask<string> ConnectAsync();

        /// <summary>Register and price <paramref name="productIds"/>, each with its store type
        /// (<paramref name="nonConsumable"/>, index-aligned). Answers with <see cref="ProductsFetched"/> or
        /// <see cref="ProductsFetchFailed"/>.</summary>
        void FetchProducts(string[] productIds, bool[] nonConsumable);

        /// <summary>Ask the store for this player's purchases. Answers with <see cref="PurchasesFetched"/>
        /// (and re-raises every still-pending order through <see cref="PurchasePending"/>) or
        /// <see cref="PurchasesFetchFailed"/>.</summary>
        void FetchPurchases();

        /// <summary>Start the store's purchase flow for a fetched product.</summary>
        void Purchase(string productId);

        /// <summary>Acknowledge the pending order behind <paramref name="transactionId"/> to the store.
        /// Answers with <see cref="PurchaseConfirmed"/>.</summary>
        void ConfirmPurchase(string transactionId);

        /// <summary>Apple's restore-transactions call; <paramref name="onCompleted"/> gets (success, error).
        /// Only meaningful on iOS — the service never calls it elsewhere.</summary>
        void RestoreTransactions(Action<bool, string> onCompleted);

        /// <summary>The products the store priced.</summary>
        event Action<UnityIapStoreProduct[]> ProductsFetched;

        /// <summary>The product fetch failed; the argument is the store's reason.</summary>
        event Action<string> ProductsFetchFailed;

        /// <summary>A paid order awaiting fulfillment: (transaction id, product id). Raised for a new
        /// purchase, a deferred one approved later, and an order left unconfirmed by an earlier session —
        /// possibly more than once for one transaction.</summary>
        event Action<string, string> PurchasePending;

        /// <summary>A purchase failed: (product id, Unity IAP <c>PurchaseFailureReason</c> name, details).</summary>
        event Action<string, string, string> PurchaseFailed;

        /// <summary>A purchase is waiting for out-of-band approval (Ask to Buy): (product id).</summary>
        event Action<string> PurchaseDeferred;

        /// <summary>The store answered a confirmation: (transaction id, acknowledged, failure reason name or null).</summary>
        event Action<string, bool, string> PurchaseConfirmed;

        /// <summary>The store's confirmed orders for non-consumable products: their product ids.</summary>
        event Action<string[]> PurchasesFetched;

        /// <summary>The purchase fetch failed; the argument is the store's reason.</summary>
        event Action<string> PurchasesFetchFailed;
    }
}
