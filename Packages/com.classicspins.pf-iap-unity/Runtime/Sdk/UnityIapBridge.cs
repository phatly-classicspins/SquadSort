using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;
using Unity.Services.Core;
using UnityEngine.Purchasing;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Sdk
{
    /// <summary>
    /// The thin shell over Unity IAP 5's <c>StoreController</c> — the only file in the module that names a
    /// Unity IAP type. It translates calls and callbacks to primitives and keeps the pending orders behind
    /// their transaction ids; every decision is <see cref="UnityIapPurchaseService"/>'s, proven headless.
    /// </summary>
    /// <remarks>
    /// <para><b>Written against the IAP 5 API</b> (<c>UnityIAPServices.StoreController()</c>,
    /// <c>OnPurchasePending</c> → <c>ConfirmPurchase</c>); the IAP 4 listener API does not exist there. Unity
    /// Gaming Services is initialized by <see cref="InitializeServicesAsync"/>, a step of its own the service runs
    /// before <see cref="ConnectAsync"/> — a UGS failure is logged and the store still connects.</para>
    /// <para><b>Both pending paths are forwarded.</b> A fetch's <c>Orders.PendingOrders</c> is re-raised through
    /// <see cref="PurchasePending"/> as well as the store's own pending callback (Unity: pending events can be
    /// missed on some iOS versions). The same transaction may therefore arrive twice — the service grants it
    /// once.</para>
    /// <para><b>The <c>Unity</c> namespace segment.</b> This assembly's namespace ends in <c>.Iap.Unity.Sdk</c>
    /// (namespace ≡ assembly name, derived from <c>pf-iap-unity</c>), so inside it the simple name <c>Unity</c>
    /// binds to <c>ClassicSpins.PrototypeFramework.Iap.Unity</c>. Unity Gaming Services is therefore imported only
    /// by the file-level <c>using Unity.Services.Core;</c> (resolved from the global namespace); qualify any other
    /// <c>Unity.*</c> name with <c>global::</c>.</para>
    /// <para>Single-item orders only: the framework port buys one product at a time, so an order's product
    /// is its cart's first item.</para>
    /// </remarks>
    public sealed class UnityIapBridge : IUnityIapBridge
    {
        private readonly Dictionary<string, PendingOrder> _pending = new Dictionary<string, PendingOrder>(StringComparer.Ordinal);
        private StoreController _store;

        public event Action<UnityIapStoreProduct[]> ProductsFetched;
        public event Action<string> ProductsFetchFailed;
        public event Action<string, string> PurchasePending;
        public event Action<string, string, string> PurchaseFailed;
        public event Action<string> PurchaseDeferred;
        public event Action<string, bool, string> PurchaseConfirmed;
        public event Action<string[]> PurchasesFetched;
        public event Action<string> PurchasesFetchFailed;

        public async UniTask<string> InitializeServicesAsync()
        {
            try
            {
                // Already initialized by the SKU or another module: nothing to do. While another caller's
                // initialization is in flight, InitializeAsync hands back that same in-flight task.
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        }

        public async UniTask<string> ConnectAsync()
        {
            try
            {
                if (_store == null)
                {
                    _store = UnityIAPServices.StoreController();
                    _store.OnProductsFetched += OnProductsFetched;
                    _store.OnProductsFetchFailed += OnProductsFetchFailed;
                    _store.OnPurchasePending += OnPurchasePending;
                    _store.OnPurchaseFailed += OnPurchaseFailed;
                    _store.OnPurchaseDeferred += OnPurchaseDeferred;
                    _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                    _store.OnPurchasesFetched += OnPurchasesFetched;
                    _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                }

                await _store.Connect();
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        }

        public void FetchProducts(string[] productIds, bool[] nonConsumable)
        {
            var definitions = new List<ProductDefinition>(productIds.Length);
            for (var i = 0; i < productIds.Length; i++)
                definitions.Add(new ProductDefinition(productIds[i],
                    nonConsumable[i] ? ProductType.NonConsumable : ProductType.Consumable));
            _store.FetchProducts(definitions);
        }

        public void FetchPurchases() => _store.FetchPurchases();

        public void Purchase(string productId) => _store.PurchaseProduct(productId);

        public void ConfirmPurchase(string transactionId)
        {
            if (transactionId != null && _pending.TryGetValue(transactionId, out var order))
                _store.ConfirmPurchase(order);
            else
                PurchaseConfirmed?.Invoke(transactionId, false, "no pending order for this transaction in this session");
        }

        public void RestoreTransactions(Action<bool, string> onCompleted)
            => _store.RestoreTransactions((ok, error) => onCompleted?.Invoke(ok, error));

        // ── Unity IAP callbacks → primitives ───────────────────────────────────────────────────────────

        private void OnProductsFetched(List<Product> products)
        {
            var mapped = new List<UnityIapStoreProduct>(products != null ? products.Count : 0);
            if (products != null)
                foreach (var p in products)
                    if (p?.definition != null)
                        mapped.Add(new UnityIapStoreProduct(p.definition.id, p.metadata?.localizedPriceString, p.availableToPurchase));
            ProductsFetched?.Invoke(mapped.ToArray());
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
            => ProductsFetchFailed?.Invoke(failure?.FailureReason ?? "unknown");

        private void OnPurchasePending(PendingOrder order)
        {
            if (order == null) return;
            var transactionId = TransactionIdOf(order);
            _pending[transactionId] = order;
            PurchasePending?.Invoke(transactionId, ProductIdOf(order));
        }

        private void OnPurchaseFailed(FailedOrder order)
            => PurchaseFailed?.Invoke(ProductIdOf(order), order?.FailureReason.ToString(), order?.Details);

        private void OnPurchaseDeferred(DeferredOrder order) => PurchaseDeferred?.Invoke(ProductIdOf(order));

        private void OnPurchaseConfirmed(Order order)
        {
            if (order == null) return;
            var transactionId = TransactionIdOf(order);
            switch (order)
            {
                case ConfirmedOrder _:
                    _pending.Remove(transactionId);
                    PurchaseConfirmed?.Invoke(transactionId, true, null);
                    break;
                case FailedOrder failed:
                    // Keep the pending order: it is redelivered and re-confirmed.
                    PurchaseConfirmed?.Invoke(transactionId, false, failed.FailureReason.ToString());
                    break;
            }
        }

        private void OnPurchasesFetched(Orders orders)
        {
            var owned = new List<string>();
            if (orders?.ConfirmedOrders != null)
                foreach (var order in orders.ConfirmedOrders)
                {
                    var product = ProductOf(order);
                    if (product?.definition != null && product.definition.type == ProductType.NonConsumable)
                        owned.Add(product.definition.id);
                }
            PurchasesFetched?.Invoke(owned.ToArray());

            if (orders?.PendingOrders != null)
                foreach (var pending in orders.PendingOrders)
                    OnPurchasePending(pending);
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
            => PurchasesFetchFailed?.Invoke(failure != null ? failure.FailureReason + ": " + failure.Message : "unknown");

        private static Product ProductOf(Order order)
        {
            var items = order?.CartOrdered?.Items();
            return items != null && items.Count > 0 ? items[0]?.Product : null;
        }

        private static string ProductIdOf(Order order) => ProductOf(order)?.definition?.id;

        /// <summary>The store transaction id; an order without one is keyed by its object identity, so it is
        /// still confirmed by the same order and still granted at most once per delivery.</summary>
        private static string TransactionIdOf(Order order)
        {
            var id = order.Info?.TransactionID;
            return string.IsNullOrEmpty(id) ? "order#" + RuntimeHelpers.GetHashCode(order) : id;
        }
    }
}
