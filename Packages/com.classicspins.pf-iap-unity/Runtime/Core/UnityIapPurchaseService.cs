using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Core
{
    /// <summary>The platform, reduced to what the service distinguishes. The installer maps it from
    /// <c>UNITY_ANDROID</c> / <c>UNITY_IOS</c>; Core reads no Unity define.</summary>
    public enum UnityIapPlatform
    {
        /// <summary>Neither Android nor iOS (the Editor on a desktop target, a desktop player).</summary>
        Other = 0,
        Android = 1,
        Ios = 2,
    }

    /// <summary>
    /// <see cref="IPurchaseService"/> backed by Unity IAP 5, through <see cref="IUnityIapBridge"/>. The
    /// framework's <c>FakePurchaseService</c> is the specification: result never throw, a disabled product
    /// never sells, grants only through <see cref="GrantInterpreter"/> attributed <see cref="GrantSource.Iap"/>,
    /// the <c>PurchaseSucceeded</c> / <c>PurchaseFailed</c> / <c>PurchaseRestored</c> funnel tracked here, and a
    /// restore that replays entitlement grants only.
    /// </summary>
    /// <remarks>
    /// <para><b>Grants follow the store, not the button.</b> Unity IAP delivers every paid order through one
    /// pending callback — a purchase just made, an Ask-to-Buy approved later, an order an earlier session left
    /// unconfirmed. Each is fulfilled the same way: grant, confirm, track <c>PurchaseSucceeded</c>. A
    /// <see cref="BuyAsync"/> waiting on that product resolves <see cref="PurchaseResult.Success"/> from it.</para>
    /// <para><b>Once per transaction.</b> A transaction id granted in this process is never granted again: a
    /// redelivery (the store re-raising it, or the <c>PendingOrders</c> pass of a purchase fetch) is only
    /// re-confirmed. The grant and the confirmation run in one synchronous step, so an order is unconfirmed
    /// across a restart only when the confirmation itself fails (store disconnected) — the one window where
    /// the next session can grant it again. A durable ledger would close it and needs a persisted model; see
    /// the README.</para>
    /// <para><b>Grant failure leaves the order unconfirmed</b>, so the store delivers it again rather than the
    /// player paying for nothing. Grants earlier in the payload have committed (the interpreter's known
    /// limit, as for the fake).</para>
    /// <para><b>Restore</b> re-applies the entitlement legs of every store-owned
    /// <see cref="ProductKind.NonConsumable"/> catalog product, exactly as the fake replays its owned
    /// non-consumables. On iOS it first runs Apple's restore-transactions. Separately, the purchase fetch that
    /// follows a successful init re-applies <i>permanent</i> entitlements the player owns at the store but no
    /// longer holds (a reinstall), silently and without analytics — never a time-limited one, which would
    /// extend on every launch.</para>
    /// </remarks>
    public sealed class UnityIapPurchaseService : IPurchaseService
    {
        /// <summary>How long init waits for Unity Gaming Services before it connects the store anyway.</summary>
        public static readonly TimeSpan ServicesInitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>How long init waits for the store to connect.</summary>
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

        /// <summary>How long init waits for the store to answer the product fetch.</summary>
        public static readonly TimeSpan ProductFetchTimeout = TimeSpan.FromSeconds(30);

        /// <summary>How long a restore waits for the store to answer the purchase fetch.</summary>
        public static readonly TimeSpan PurchaseFetchTimeout = TimeSpan.FromSeconds(30);

        private const string Tag = "[pf-iap-unity] ";

        private readonly IUnityIapBridge _bridge;
        private readonly ICatalogSource _catalogSource;
        private readonly GrantInterpreter _interpreter;
        private readonly IEntitlementService _entitlements;
        private readonly IAnalytics _analytics;
        private readonly ILog _log;
        private readonly UnityIapPlatform _platform;
        private readonly Func<TimeSpan, CancellationToken, UniTask> _delay;

        private List<ProductInfo> _catalog;
        private bool _servicesReady;
        private bool _connected;
        private bool _priced;
        private UniTaskCompletionSource _initInFlight;

        private readonly HashSet<string> _grantedTransactions = new HashSet<string>(StringComparer.Ordinal);

        private string _buyingProductId;
        private UniTaskCompletionSource<PurchaseResult> _buying;

        private UniTaskCompletionSource<string> _productsWaiter;
        private UniTaskCompletionSource<string[]> _restoreWaiter;
        private bool _restoring;

        public UnityIapPurchaseService(
            IUnityIapBridge bridge,
            ICatalogSource catalogSource,
            GrantInterpreter interpreter,
            IEntitlementService entitlements,
            IAnalytics analytics,
            ILog log,
            UnityIapPlatform platform,
            Func<TimeSpan, CancellationToken, UniTask> delay)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _catalogSource = catalogSource ?? throw new ArgumentNullException(nameof(catalogSource));
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _platform = platform;
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));

            _bridge.ProductsFetched += OnProductsFetched;
            _bridge.ProductsFetchFailed += OnProductsFetchFailed;
            _bridge.PurchasePending += OnPurchasePending;
            _bridge.PurchaseFailed += OnPurchaseFailed;
            _bridge.PurchaseDeferred += OnPurchaseDeferred;
            _bridge.PurchaseConfirmed += OnPurchaseConfirmed;
            _bridge.PurchasesFetched += OnPurchasesFetched;
            _bridge.PurchasesFetchFailed += OnPurchasesFetchFailed;
        }

        /// <summary>Whether the store connected and priced the catalog.</summary>
        public bool IsInitialized => _connected && _priced;

        public IReadOnlyList<ProductInfo> Catalog
        {
            get
            {
                EnsureLoaded();
                return _catalog;
            }
        }

        // ── init ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Initialize Unity Gaming Services, connect, price the catalog, then fetch purchases. Runs once
        /// per process once it has succeeded; concurrent callers share the one run; a failed run (thrown —
        /// <c>IapInit</c> degrades) may be retried by a later call.</summary>
        /// <remarks>The UGS step is <b>best-effort and bounded on its own</b>: a UGS error or a UGS that does not
        /// answer in <see cref="ServicesInitTimeout"/> is logged and the store connects anyway, so a UGS fault
        /// never skips the store init or eats the store's <see cref="ConnectTimeout"/>. Only the store's own
        /// connect and product fetch fail the run.</remarks>
        public async UniTask InitializeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (IsInitialized) return;

            // A completion source, not a preserved task: it supports any number of concurrent awaiters.
            var run = _initInFlight;
            if (run == null)
            {
                run = new UniTaskCompletionSource();
                _initInFlight = run;
                RunInitAsync(run).Forget();
            }
            await run.Task.AttachExternalCancellation(ct);
        }

        private async UniTaskVoid RunInitAsync(UniTaskCompletionSource run)
        {
            try
            {
                EnsureLoaded();
                WarnKindMismatches();

                if (!_connected && !_servicesReady)
                {
                    var servicesError = await BoundedAsync(_bridge.InitializeServicesAsync, ServicesInitTimeout);
                    if (servicesError == null)
                        _servicesReady = true;
                    else
                        _log.Warn(Tag + "Unity Gaming Services did not initialize (" + servicesError + ") — connecting "
                                  + "the store anyway. Check the project is linked to a Unity Cloud project "
                                  + "(Edit ▸ Project Settings ▸ Services).");
                }

                if (!_connected)
                {
                    var error = await BoundedAsync(_bridge.ConnectAsync, ConnectTimeout);
                    if (error != null)
                        throw new InvalidOperationException("the store did not connect: " + error);
                    _connected = true;
                }

                var ids = new List<string>();
                var nonConsumable = new List<bool>();
                foreach (var p in _catalog)
                {
                    if (p.Disabled || string.IsNullOrEmpty(p.Id)) continue;
                    ids.Add(p.Id);
                    nonConsumable.Add(p.Kind == ProductKind.NonConsumable);
                }

                if (ids.Count > 0)
                {
                    var waiter = new UniTaskCompletionSource<string>();
                    _productsWaiter = waiter;
                    using (var timeout = new CancellationTokenSource())
                    {
                        StartTimeout(ProductFetchTimeout, timeout.Token, () => waiter.TrySetResult(
                            "the store did not answer the product fetch in " + ProductFetchTimeout.TotalSeconds + " s"));
                        _bridge.FetchProducts(ids.ToArray(), nonConsumable.ToArray());
                        var fetchError = await waiter.Task;
                        timeout.Cancel();
                        if (fetchError != null) throw new InvalidOperationException(fetchError);
                    }
                }
                else
                {
                    _log.Warn(Tag + "the catalog has no sellable product — nothing to fetch.");
                }

                _priced = true;
                _log.Info(Tag + "store connected — " + ids.Count + " products priced.");

                // Re-delivers orders an earlier session left unconfirmed, and lets a reinstall regain its
                // permanent entitlements.
                _bridge.FetchPurchases();
            }
            catch (Exception ex)
            {
                _productsWaiter = null;
                _initInFlight = null; // a later InitializeAsync retries
                run.TrySetException(ex);
                return;
            }

            _productsWaiter = null;
            _initInFlight = null;
            run.TrySetResult();
        }

        /// <summary>Run a bridge step that resolves <c>null</c> or an error text, giving up with a timeout error
        /// after <paramref name="timeout"/> of the injected (realtime) delay. Never throws: a throw from the step
        /// is its error.</summary>
        private async UniTask<string> BoundedAsync(Func<UniTask<string>> step, TimeSpan timeout)
        {
            var answer = new UniTaskCompletionSource<string>();
            using (var timer = new CancellationTokenSource())
            {
                StartTimeout(timeout, timer.Token, () => answer.TrySetResult("no answer in " + timeout.TotalSeconds + " s"));
                RunStepAsync().Forget();
                var error = await answer.Task;
                timer.Cancel();
                return error;
            }

            async UniTaskVoid RunStepAsync()
            {
                string error;
                try { error = await step(); }
                catch (Exception ex) { error = ex.Message; }
                answer.TrySetResult(error);
            }
        }

        /// <summary>Run <paramref name="onTimeout"/> after <paramref name="after"/> of the injected (realtime)
        /// delay, unless <paramref name="token"/> is cancelled first.</summary>
        private void StartTimeout(TimeSpan after, CancellationToken token, Action onTimeout)
        {
            TimeoutAsync().Forget();

            async UniTaskVoid TimeoutAsync()
            {
                var cancelled = await _delay(after, token).SuppressCancellationThrow();
                if (cancelled || token.IsCancellationRequested) return;
                onTimeout();
            }
        }

        private void OnProductsFetched(UnityIapStoreProduct[] products)
        {
            EnsureLoaded();
            var priced = 0;
            if (products != null)
                foreach (var sp in products)
                {
                    var i = IndexOf(sp.Id);
                    if (i < 0 || _catalog[i].Disabled) continue;
                    _catalog[i] = _catalog[i] with { LocalizedPrice = sp.LocalizedPrice, Available = sp.Available };
                    priced++;
                }

            if (priced > 0) _priced = _connected;
            _productsWaiter?.TrySetResult(null);
        }

        private void OnProductsFetchFailed(string reason)
        {
            _log.Warn(Tag + "product fetch failed: " + reason);
            _productsWaiter?.TrySetResult("the store refused the product fetch: " + reason);
        }

        // ── buy ──────────────────────────────────────────────────────────────────────────────────────

        public async UniTask<PurchaseResult> BuyAsync(ProductInfo product, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            UniTaskCompletionSource<PurchaseResult> pending;
            try
            {
                if (product == null)
                    return Refuse(null, "BuyAsync called with a null product");
                if (product.Disabled)
                    return Refuse(product, "product '" + product.Id + "' is disabled");
                if (!IsInitialized)
                    return Refuse(product, "the store is not initialized (IapInit has not succeeded)");

                var i = IndexOf(product.Id);
                if (i < 0)
                    return Refuse(product, "product '" + product.Id + "' is not in the catalog");
                if (!_catalog[i].Available)
                    return Refuse(product, "the store reports product '" + product.Id + "' unavailable");
                if (_buying != null)
                    return Refuse(product, "a purchase of '" + _buyingProductId + "' is already in progress");

                pending = new UniTaskCompletionSource<PurchaseResult>();
                _buying = pending;
                _buyingProductId = product.Id;
                _bridge.Purchase(product.Id);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ClearBuying();
                return Refuse(product, "the store errored (" + ex.Message + ")");
            }

            try
            {
                // A cancelled caller stops waiting; the store flow does not stop — if the player pays, the order
                // still arrives through the pending callback and is granted then.
                return await pending.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (ReferenceEquals(_buying, pending)) ClearBuying();
            }
        }

        private PurchaseResult Refuse(ProductInfo product, string why)
        {
            _log.Warn(Tag + "purchase refused — " + why + ".");
            _analytics.Track(new PurchaseFailed(product?.Id));
            return PurchaseResult.Failed;
        }

        private void ClearBuying()
        {
            _buying = null;
            _buyingProductId = null;
        }

        private void ResolveBuying(string productId, PurchaseResult result)
        {
            if (_buying == null || !string.Equals(_buyingProductId, productId, StringComparison.Ordinal)) return;
            var buying = _buying;
            ClearBuying();
            buying.TrySetResult(result);
        }

        private void OnPurchasePending(string transactionId, string productId)
        {
            if (string.IsNullOrEmpty(transactionId) || string.IsNullOrEmpty(productId))
            {
                _log.Warn(Tag + "a pending order without a transaction or product id was ignored (left unconfirmed).");
                return;
            }

            if (_grantedTransactions.Contains(transactionId))
            {
                // Already granted in this process: only the store's acknowledgement is missing.
                _bridge.ConfirmPurchase(transactionId);
                ResolveBuying(productId, PurchaseResult.Success);
                return;
            }

            EnsureLoaded();
            var i = IndexOf(productId);
            if (i < 0)
            {
                _log.Warn(Tag + "paid order " + transactionId + " is for '" + productId + "', which is not in the catalog — "
                          + "left unconfirmed so the store delivers it again once the catalog sells it.");
                _analytics.Track(new PurchaseFailed(productId));
                ResolveBuying(productId, PurchaseResult.Failed);
                return;
            }

            var product = _catalog[i];
            try
            {
                _interpreter.Apply(product.Grants, GrantSource.Iap);
            }
            catch (Exception ex)
            {
                _log.Warn(Tag + "granting order " + transactionId + " ('" + productId + "') errored — left unconfirmed so "
                          + "the store delivers it again: " + ex.Message);
                _analytics.Track(new PurchaseFailed(productId));
                ResolveBuying(productId, PurchaseResult.Failed);
                return;
            }

            _grantedTransactions.Add(transactionId);
            _bridge.ConfirmPurchase(transactionId);
            _analytics.Track(new PurchaseSucceeded(productId));
            ResolveBuying(productId, PurchaseResult.Success);
        }

        private void OnPurchaseFailed(string productId, string reason, string details)
        {
            var result = UnityIapResultMapping.FromFailureReason(reason);
            if (result == PurchaseResult.Failed)
            {
                _log.Warn(Tag + "purchase of '" + productId + "' failed: " + reason + " — " + details);
                _analytics.Track(new PurchaseFailed(productId));
            }
            // Cancelled / Pending are non-events for the funnel (the fake's rule).
            ResolveBuying(productId, result);
        }

        private void OnPurchaseDeferred(string productId)
        {
            _log.Info(Tag + "purchase of '" + productId + "' is deferred (awaiting approval); it is granted when the store delivers it.");
            ResolveBuying(productId, PurchaseResult.Deferred);
        }

        private void OnPurchaseConfirmed(string transactionId, bool acknowledged, string reason)
        {
            // A failed acknowledgement reverses nothing and the grant stands. The order stays pending at the
            // store and is redelivered — re-confirmed, never re-granted, while this process lives.
            if (!acknowledged)
                _log.Warn(Tag + "the store did not acknowledge order " + transactionId + " (" + reason + "); it will be re-confirmed when redelivered.");
        }

        // ── restore ──────────────────────────────────────────────────────────────────────────────────

        public async UniTask<RestoreResult> RestoreAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsInitialized)
            {
                _log.Warn(Tag + "restore refused — the store is not initialized.");
                return RestoreResult.Failed;
            }
            if (_restoring)
            {
                _log.Warn(Tag + "restore refused — a restore is already running.");
                return RestoreResult.Failed;
            }

            _restoring = true;
            try
            {
                if (_platform == UnityIapPlatform.Ios)
                {
                    var apple = new UniTaskCompletionSource<string>();
                    _bridge.RestoreTransactions((ok, error) => apple.TrySetResult(ok ? null : (error ?? "unknown error")));
                    var appleError = await apple.Task.AttachExternalCancellation(ct);
                    if (appleError != null)
                    {
                        _log.Warn(Tag + "Apple restore failed: " + appleError);
                        return RestoreResult.Failed;
                    }
                }

                var waiter = new UniTaskCompletionSource<string[]>();
                _restoreWaiter = waiter;
                string[] owned;
                using (var timeout = new CancellationTokenSource())
                {
                    StartTimeout(PurchaseFetchTimeout, timeout.Token, () => waiter.TrySetResult(null));
                    _bridge.FetchPurchases();
                    owned = await waiter.Task.AttachExternalCancellation(ct);
                    timeout.Cancel();
                }

                if (owned == null)
                {
                    _log.Warn(Tag + "restore failed — the store did not return this player's purchases.");
                    return RestoreResult.Failed;
                }

                var restored = 0;
                foreach (var product in OwnedNonConsumables(owned))
                {
                    _interpreter.Apply(EntitlementGrantsOf(product, permanentOnly: false), GrantSource.Iap);
                    restored++;
                }

                _analytics.Track(new PurchaseRestored(restored));
                return restored > 0 ? RestoreResult.Success : RestoreResult.Nothing;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warn(Tag + "restore errored — Failed (never a throw): " + ex.Message);
                return RestoreResult.Failed;
            }
            finally
            {
                _restoreWaiter = null;
                _restoring = false;
            }
        }

        private void OnPurchasesFetched(string[] ownedNonConsumableIds)
        {
            var owned = ownedNonConsumableIds ?? new string[0];
            if (_restoreWaiter != null)
            {
                _restoreWaiter.TrySetResult(owned);
                return;
            }

            // Not a restore: the fetch after init. Regain permanent entitlements the store says are owned but
            // this install does not hold — silently, never a time-grant, never tracked as a restore.
            foreach (var product in OwnedNonConsumables(owned))
            {
                var missing = new List<GrantSpec>();
                foreach (var g in EntitlementGrantsOf(product, permanentOnly: true))
                    if (!_entitlements.Has(new EntitlementKey(g.Id))) missing.Add(g);
                if (missing.Count == 0) continue;
                try
                {
                    _interpreter.Apply(missing, GrantSource.Iap);
                    _log.Info(Tag + "re-applied " + missing.Count + " owned entitlement(s) of '" + product.Id + "'.");
                }
                catch (Exception ex)
                {
                    _log.Warn(Tag + "re-applying the entitlements of '" + product.Id + "' errored: " + ex.Message);
                }
            }
        }

        private void OnPurchasesFetchFailed(string reason)
        {
            _log.Warn(Tag + "purchase fetch failed: " + reason);
            _restoreWaiter?.TrySetResult(null);
        }

        // ── helpers ──────────────────────────────────────────────────────────────────────────────────

        private void EnsureLoaded()
        {
            if (_catalog != null) return;
            var loaded = _catalogSource.LoadCatalog();
            _catalog = loaded != null ? new List<ProductInfo>(loaded) : new List<ProductInfo>();
        }

        private int IndexOf(string productId)
        {
            if (string.IsNullOrEmpty(productId) || _catalog == null) return -1;
            for (var i = 0; i < _catalog.Count; i++)
                if (string.Equals(_catalog[i].Id, productId, StringComparison.Ordinal)) return i;
            return -1;
        }

        private IEnumerable<ProductInfo> OwnedNonConsumables(string[] ownedIds)
        {
            EnsureLoaded();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ownedIds)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                var i = IndexOf(id);
                if (i < 0 || _catalog[i].Kind != ProductKind.NonConsumable) continue;
                if (EntitlementGrantsOf(_catalog[i], permanentOnly: false).Count == 0) continue;
                yield return _catalog[i];
            }
        }

        private static IReadOnlyList<GrantSpec> EntitlementGrantsOf(ProductInfo product, bool permanentOnly)
        {
            var list = new List<GrantSpec>();
            if (product.Grants != null)
                foreach (var g in product.Grants)
                    if (g != null && g.Kind == GrantKinds.Entitlement && !string.IsNullOrEmpty(g.Id) && (!permanentOnly || g.Days == 0))
                        list.Add(g);
            return list;
        }

        /// <summary>A product that grants a permanent entitlement but is sold as consumable is never returned
        /// by the store, so it can never be restored — almost always a missing <c>"type": "NonConsumable"</c>.</summary>
        private void WarnKindMismatches()
        {
            foreach (var p in _catalog)
                if (!p.Disabled && p.Kind == ProductKind.Consumable && EntitlementGrantsOf(p, permanentOnly: true).Count > 0)
                    _log.Warn(Tag + "product '" + p.Id + "' grants a permanent entitlement but is Consumable, so the store "
                              + "never returns it and it cannot be restored — set \"type\": \"NonConsumable\" in products.json.");
        }
    }
}
