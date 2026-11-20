using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>
    /// <see cref="UnityIapPurchaseService"/> against a scripted bridge and the real grant interpreter. The
    /// framework's <c>FakePurchaseService</c> is the specification it is held to: results never throws, a
    /// disabled product never sells, grants only through the interpreter, the funnel tracked at this layer,
    /// and restore replays entitlement grants only. On top: one grant per store transaction.
    /// </summary>
    [TestFixture]
    public sealed class UnityIapPurchaseServiceTests
    {
        private FakeUnityIapBridge _bridge;
        private RecordingLog _log;
        private RecordingAnalytics _analytics;
        private Economy _economy;
        private FakeDelay _delay;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeUnityIapBridge();
            _log = new RecordingLog();
            _analytics = new RecordingAnalytics();
            _economy = new Economy(_log);
            _delay = new FakeDelay();
        }

        private UnityIapPurchaseService Service(UnityIapPlatform platform = UnityIapPlatform.Android, params ProductInfo[] catalog)
            => new UnityIapPurchaseService(_bridge, new ListCatalog(catalog.Length > 0 ? catalog : Catalog.Default()),
                _economy.Interpreter, _economy.Entitlements, _analytics, _log, platform, _delay.Func);

        private async Task<UnityIapPurchaseService> Initialized(UnityIapPlatform platform = UnityIapPlatform.Android, params ProductInfo[] catalog)
        {
            var service = Service(platform, catalog);
            await service.InitializeAsync(CancellationToken.None).AsTask();
            return service;
        }

        private static ProductInfo Find(IPurchaseService s, string id) => s.Catalog.Single(p => p.Id == id);

        // ── init ───────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Init_connects_fetches_every_enabled_product_with_its_store_type_and_prices_the_catalog()
        {
            _bridge.Unavailable.Add(Catalog.Starter);
            var s = await Initialized();

            Assert.Multiple(() =>
            {
                Assert.That(s.IsInitialized, Is.True);
                Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
                Assert.That(_bridge.FetchProductsIds.Single(), Is.EqualTo(new[] { Catalog.Gold, Catalog.RemoveAds, Catalog.Starter }),
                    "the disabled product is never registered");
                Assert.That(_bridge.FetchProductsNonConsumable.Single(), Is.EqualTo(new[] { false, true, true }));
                Assert.That(Find(s, Catalog.Gold).LocalizedPrice, Is.EqualTo("$1.99"));
                Assert.That(Find(s, Catalog.Gold).Available, Is.True);
                Assert.That(Find(s, Catalog.Starter).Available, Is.False, "the store's stock answer");
                Assert.That(Find(s, Catalog.Off).LocalizedPrice, Is.Null, "a disabled product stays priceless");
                Assert.That(Find(s, Catalog.Off).Available, Is.False);
                Assert.That(_bridge.FetchPurchasesCalls, Is.EqualTo(1), "purchases are fetched after init");
            });
        }

        [Test]
        public void Before_init_the_catalog_is_the_baked_entries_with_loading_prices()
        {
            var s = Service();
            Assert.That(s.Catalog.Select(p => p.Id), Is.EqualTo(Catalog.Default().Select(p => p.Id)));
            Assert.That(s.Catalog.All(p => p.LocalizedPrice == null && !p.Available), Is.True);
            Assert.That(_bridge.ConnectCalls, Is.Zero, "reading the catalog never touches the store");
        }

        [Test]
        public void A_connect_error_throws_so_IapInit_degrades_and_a_later_init_retries()
        {
            _bridge.ConnectError = "no network";
            var s = Service();
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(),
                Throws.InvalidOperationException.With.Message.Contains("no network"));
            Assert.That(s.IsInitialized, Is.False);

            _bridge.ConnectError = null;
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(), Throws.Nothing);
            Assert.That(s.IsInitialized, Is.True);
            Assert.That(_bridge.ConnectCalls, Is.EqualTo(2));
        }

        // ── UGS: its own step, best-effort, never skips the store ───────────────────────────────────────

        [Test]
        public async Task Init_initializes_UGS_as_its_own_step_before_connecting_the_store()
        {
            await Initialized();
            Assert.That(_bridge.Steps, Is.EqualTo(new[] { "services", "connect" }));
        }

        [Test]
        public async Task A_UGS_error_is_warned_and_the_store_still_connects_and_prices()
        {
            _bridge.ServicesError = "ServicesInitializationException: project not linked";
            var s = await Initialized();

            Assert.That(s.IsInitialized, Is.True);
            Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
            Assert.That(_bridge.FetchProductsIds, Has.Count.EqualTo(1));
            Assert.That(_log.Warns, Has.Some.Contains("Unity Gaming Services did not initialize").And.Contains("project not linked"));
        }

        [Test]
        public async Task A_UGS_throw_is_warned_and_the_store_still_connects()
        {
            _bridge.ThrowOnServices = true;
            var s = await Initialized();

            Assert.That(s.IsInitialized, Is.True);
            Assert.That(_log.Warns, Has.Some.Contains("UGS exploded"));
        }

        [Test]
        public async Task A_UGS_that_never_answers_times_out_on_its_own_delay_and_the_store_still_connects()
        {
            _bridge.HoldServices = true;
            var s = Service();
            var init = s.InitializeAsync(CancellationToken.None).AsTask();

            Assert.That(_bridge.ConnectCalls, Is.Zero, "the store waits for the UGS step");
            Assert.That(_delay.Calls.Single().Delay, Is.EqualTo(UnityIapPurchaseService.ServicesInitTimeout));
            _delay.ElapsePending();
            await init;

            Assert.That(s.IsInitialized, Is.True);
            Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
            Assert.That(_log.Warns, Has.Some.Contains("Unity Gaming Services did not initialize"));
            Assert.That(_delay.Pending, Is.Zero, "no timeout is left running");
        }

        [Test]
        public void A_store_retry_after_UGS_succeeded_never_reinitializes_UGS()
        {
            _bridge.ConnectError = "no network";
            var s = Service();
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(), Throws.InvalidOperationException);

            _bridge.ConnectError = null;
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(), Throws.Nothing);
            Assert.That(_bridge.ServicesCalls, Is.EqualTo(1));
            Assert.That(_bridge.ConnectCalls, Is.EqualTo(2));
        }

        [Test]
        public void A_store_retry_after_UGS_failed_tries_UGS_again()
        {
            _bridge.ServicesError = "offline";
            _bridge.ConnectError = "offline";
            var s = Service();
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(), Throws.InvalidOperationException);

            _bridge.ServicesError = null;
            _bridge.ConnectError = null;
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(), Throws.Nothing);
            Assert.That(_bridge.ServicesCalls, Is.EqualTo(2));
        }

        [Test]
        public void A_product_fetch_failure_throws_with_the_store_reason()
        {
            _bridge.ProductsFetchError = "ProductUnavailable";
            var s = Service();
            Assert.That(async () => await s.InitializeAsync(CancellationToken.None).AsTask(),
                Throws.InvalidOperationException.With.Message.Contains("ProductUnavailable"));
            Assert.That(s.IsInitialized, Is.False);
        }

        [Test]
        public async Task A_store_that_never_answers_the_product_fetch_times_out_on_the_injected_delay()
        {
            _bridge.AutoAnswerProducts = false;
            var s = Service();
            var init = s.InitializeAsync(CancellationToken.None).AsTask();

            Assert.That(init.IsCompleted, Is.False);
            Assert.That(_delay.Calls.Any(c => c.Delay == UnityIapPurchaseService.ProductFetchTimeout), Is.True);
            _delay.ElapsePending();

            Assert.That(async () => await init, Throws.InvalidOperationException.With.Message.Contains("did not answer"));
            Assert.That(s.IsInitialized, Is.False);
        }

        [Test]
        public async Task A_store_that_never_connects_times_out_on_the_injected_delay()
        {
            _bridge.HoldConnect = true;
            var s = Service();
            var init = s.InitializeAsync(CancellationToken.None).AsTask();
            Assert.That(_delay.Calls.Single(c => c.IsPending).Delay, Is.EqualTo(UnityIapPurchaseService.ConnectTimeout));

            _delay.ElapsePending();

            Assert.That(async () => await init, Throws.InvalidOperationException.With.Message.Contains("did not connect"));
            await Task.Yield();
        }

        [Test]
        public async Task Init_runs_once_per_process_once_it_succeeded_and_cancels_its_timeouts()
        {
            var s = await Initialized();
            await s.InitializeAsync(CancellationToken.None).AsTask();
            await s.InitializeAsync(CancellationToken.None).AsTask();

            Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
            Assert.That(_bridge.FetchProductsIds, Has.Count.EqualTo(1));
            Assert.That(_delay.Pending, Is.Zero, "no timeout is left running");
        }

        [Test]
        public async Task Concurrent_inits_share_one_run()
        {
            _bridge.HoldConnect = true;
            var s = Service();
            var a = s.InitializeAsync(CancellationToken.None).AsTask();
            var b = s.InitializeAsync(CancellationToken.None).AsTask();
            _bridge.PendingConnect.TrySetResult(null);
            await Task.WhenAll(a, b);

            Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
            Assert.That(s.IsInitialized, Is.True);
        }

        [Test]
        public void A_cancelled_init_rethrows_the_cancellation()
        {
            var s = Service();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.That(async () => await s.InitializeAsync(cts.Token).AsTask(), Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public async Task A_consumable_that_grants_a_permanent_entitlement_is_warned_at_init()
        {
            var bad = new ProductInfo("com.x.remove_ads_untyped", new[] { new GrantSpec(GrantKinds.Entitlement, "remove_ads") });
            await Initialized(UnityIapPlatform.Android, bad, Catalog.GoldPack());
            Assert.That(_log.Warns.Count(w => w.Contains("com.x.remove_ads_untyped") && w.Contains("NonConsumable")), Is.EqualTo(1));
            Assert.That(_log.Warns.Any(w => w.Contains(Catalog.Gold)), Is.False);
        }

        // ── buy ────────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_paid_purchase_grants_once_confirms_tracks_Succeeded_and_resolves_Success()
        {
            var s = await Initialized();
            var buy = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            Assert.That(_bridge.Purchases, Is.EqualTo(new[] { Catalog.Gold }));
            Assert.That(buy.IsCompleted, Is.False, "waits for the store");

            _bridge.RaisePending("tx-1", Catalog.Gold);

            Assert.That(await buy, Is.EqualTo(PurchaseResult.Success));
            Assert.Multiple(() =>
            {
                Assert.That(_economy.Coins, Is.EqualTo(1000));
                Assert.That(_bridge.Confirms, Is.EqualTo(new[] { "tx-1" }));
                Assert.That(_analytics.Of<PurchaseSucceeded>().Select(e => e.ProductId), Is.EqualTo(new[] { Catalog.Gold }));
                Assert.That(_analytics.Of<PurchaseFailed>(), Is.Empty);
            });
        }

        [Test]
        public async Task The_same_transaction_delivered_again_is_reconfirmed_never_regranted()
        {
            var s = await Initialized();
            var buy = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            _bridge.RaisePending("tx-1", Catalog.Gold);
            await buy;

            // The store's own callback and the fetch's PendingOrders pass may both deliver it; a failed
            // acknowledgement redelivers it too.
            _bridge.RaiseConfirmed("tx-1", false, "StoreNotConnected");
            _bridge.RaisePending("tx-1", Catalog.Gold);
            _bridge.RaisePending("tx-1", Catalog.Gold);

            Assert.Multiple(() =>
            {
                Assert.That(_economy.Coins, Is.EqualTo(1000), "granted exactly once");
                Assert.That(_bridge.Confirms, Is.EqualTo(new[] { "tx-1", "tx-1", "tx-1" }), "re-confirmed each time");
                Assert.That(_analytics.Of<PurchaseSucceeded>().Count(), Is.EqualTo(1), "tracked once");
            });
        }

        [Test]
        public async Task Two_transactions_of_one_consumable_grant_twice()
        {
            var s = await Initialized();
            var first = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            _bridge.RaisePending("tx-1", Catalog.Gold);
            await first;
            var second = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            _bridge.RaisePending("tx-2", Catalog.Gold);

            Assert.That(await second, Is.EqualTo(PurchaseResult.Success));
            Assert.That(_economy.Coins, Is.EqualTo(2000));
        }

        [Test]
        public async Task An_order_left_unconfirmed_by_an_earlier_session_is_granted_and_confirmed_after_init()
        {
            _bridge.PendingAtStore.Add(("tx-old", Catalog.RemoveAds));
            await Initialized();

            Assert.Multiple(() =>
            {
                Assert.That(_economy.Has("remove_ads"), Is.True);
                Assert.That(_bridge.Confirms, Is.EqualTo(new[] { "tx-old" }));
                Assert.That(_analytics.Of<PurchaseSucceeded>().Single().ProductId, Is.EqualTo(Catalog.RemoveAds));
            });
        }

        [Test]
        public async Task A_starter_pack_grants_every_leg_once()
        {
            var s = await Initialized();
            var buy = s.BuyAsync(Find(s, Catalog.Starter), CancellationToken.None).AsTask();
            _bridge.RaisePending("tx-s", Catalog.Starter);

            Assert.That(await buy, Is.EqualTo(PurchaseResult.Success));
            Assert.That(_economy.Has("starter_pack"), Is.True);
            Assert.That(_economy.Coins, Is.EqualTo(500));
        }

        [TestCase("UserCancelled", PurchaseResult.Cancelled, false)]
        [TestCase("ExistingPurchasePending", PurchaseResult.Pending, false)]
        [TestCase("PaymentDeclined", PurchaseResult.Failed, true)]
        [TestCase("ProductUnavailable", PurchaseResult.Failed, true)]
        [TestCase("SomethingNew", PurchaseResult.Failed, true)]
        public async Task A_store_failure_maps_to_the_pinned_result_and_only_Failed_is_tracked(string reason, PurchaseResult expected, bool tracked)
        {
            var s = await Initialized();
            var buy = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            _bridge.RaiseFailed(Catalog.Gold, reason);

            Assert.That(await buy, Is.EqualTo(expected));
            Assert.That(_analytics.Of<PurchaseFailed>().Count(), Is.EqualTo(tracked ? 1 : 0));
            Assert.That(_economy.Coins, Is.Zero);
            Assert.That(_bridge.Confirms, Is.Empty);
        }

        [Test]
        public async Task A_deferred_purchase_resolves_Deferred_and_is_granted_when_approval_arrives()
        {
            var s = await Initialized();
            var buy = s.BuyAsync(Find(s, Catalog.RemoveAds), CancellationToken.None).AsTask();
            _bridge.RaiseDeferred(Catalog.RemoveAds);

            Assert.That(await buy, Is.EqualTo(PurchaseResult.Deferred));
            Assert.That(_economy.Has("remove_ads"), Is.False);
            Assert.That(_analytics.Events, Is.Empty, "a deferral is a non-event for the funnel");

            _bridge.RaisePending("tx-approved", Catalog.RemoveAds);
            Assert.That(_economy.Has("remove_ads"), Is.True);
            Assert.That(_bridge.Confirms, Is.EqualTo(new[] { "tx-approved" }));
            Assert.That(_analytics.Of<PurchaseSucceeded>().Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task A_disabled_product_never_sells()
        {
            var s = await Initialized();
            Assert.That(await s.BuyAsync(Find(s, Catalog.Off), CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));
            Assert.That(_bridge.Purchases, Is.Empty);
            Assert.That(_analytics.Of<PurchaseFailed>().Single().ProductId, Is.EqualTo(Catalog.Off));
        }

        [Test]
        public async Task A_null_product_fails_without_a_store_call()
        {
            var s = await Initialized();
            Assert.That(await s.BuyAsync(null, CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));
            Assert.That(_bridge.Purchases, Is.Empty);
        }

        [Test]
        public async Task Buying_before_init_fails_without_a_store_call()
        {
            var s = Service();
            Assert.That(await s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));
            Assert.That(_bridge.Purchases, Is.Empty);
            Assert.That(_log.Warns.Any(w => w.Contains("pf-iap-unity") && w.Contains("not initialized")), Is.True);
        }

        [Test]
        public async Task A_product_the_store_reports_unavailable_fails_without_a_store_call()
        {
            _bridge.Unavailable.Add(Catalog.Gold);
            var s = await Initialized();
            Assert.That(await s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));
            Assert.That(_bridge.Purchases, Is.Empty);
        }

        [Test]
        public async Task A_second_buy_while_one_is_in_flight_fails_and_leaves_the_first_alone()
        {
            var s = await Initialized();
            var first = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            Assert.That(await s.BuyAsync(Find(s, Catalog.RemoveAds), CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));

            _bridge.RaisePending("tx-1", Catalog.Gold);
            Assert.That(await first, Is.EqualTo(PurchaseResult.Success));
            Assert.That(_bridge.Purchases, Is.EqualTo(new[] { Catalog.Gold }));
        }

        [Test]
        public async Task A_store_throw_is_Failed_never_a_throw_and_frees_the_slot()
        {
            var s = await Initialized();
            _bridge.ThrowOnPurchase = true;
            Assert.That(await s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask(), Is.EqualTo(PurchaseResult.Failed));

            _bridge.ThrowOnPurchase = false;
            var again = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            _bridge.RaisePending("tx-1", Catalog.Gold);
            Assert.That(await again, Is.EqualTo(PurchaseResult.Success));
        }

        [Test]
        public async Task A_cancelled_buyer_rethrows_but_a_later_payment_is_still_granted()
        {
            var s = await Initialized();
            using (var cts = new CancellationTokenSource())
            {
                var buy = s.BuyAsync(Find(s, Catalog.Gold), cts.Token).AsTask();
                cts.Cancel();
                Assert.That(async () => await buy, Throws.InstanceOf<OperationCanceledException>());
            }

            _bridge.RaisePending("tx-late", Catalog.Gold);
            Assert.That(_economy.Coins, Is.EqualTo(1000));
            Assert.That(_bridge.Confirms, Is.EqualTo(new[] { "tx-late" }));

            var next = s.BuyAsync(Find(s, Catalog.Gold), CancellationToken.None).AsTask();
            Assert.That(_bridge.Purchases, Has.Count.EqualTo(2), "the slot was freed");
            _bridge.RaisePending("tx-next", Catalog.Gold);
            Assert.That(await next, Is.EqualTo(PurchaseResult.Success));
        }

        [Test]
        public async Task An_order_for_a_product_missing_from_the_catalog_is_left_unconfirmed()
        {
            await Initialized();
            _bridge.RaisePending("tx-x", "com.brainbucks.sushi.sort.unknown");

            Assert.That(_bridge.Confirms, Is.Empty, "redelivered once the catalog sells it");
            Assert.That(_analytics.Of<PurchaseFailed>().Single().ProductId, Is.EqualTo("com.brainbucks.sushi.sort.unknown"));
            Assert.That(_log.Warns.Any(w => w.Contains("tx-x")), Is.True);
        }

        [Test]
        public async Task A_grant_that_throws_leaves_the_order_unconfirmed_and_resolves_Failed()
        {
            var bridge = _bridge;
            var throwing = new ThrowingEntitlementGate();
            var interpreter = new GrantInterpreter(_economy.Wallet, throwing, _economy.Inventory, _log);
            var s = new UnityIapPurchaseService(bridge, new ListCatalog(Catalog.Default()), interpreter, throwing,
                _analytics, _log, UnityIapPlatform.Android, _delay.Func);
            await s.InitializeAsync(CancellationToken.None).AsTask();

            var buy = s.BuyAsync(Find(s, Catalog.RemoveAds), CancellationToken.None).AsTask();
            bridge.RaisePending("tx-1", Catalog.RemoveAds);

            Assert.That(await buy, Is.EqualTo(PurchaseResult.Failed));
            Assert.That(bridge.Confirms, Is.Empty);
            Assert.That(_analytics.Of<PurchaseFailed>().Count(), Is.EqualTo(1));
            Assert.That(_analytics.Of<PurchaseSucceeded>(), Is.Empty);
        }

        [Test]
        public async Task A_pending_order_without_ids_is_ignored()
        {
            await Initialized();
            _bridge.RaisePending(null, Catalog.Gold);
            _bridge.RaisePending("tx", null);
            Assert.That(_economy.Coins, Is.Zero);
            Assert.That(_bridge.Confirms, Is.Empty);
        }

        // ── restore ────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task Restore_on_iOS_runs_Apple_restore_then_reapplies_the_entitlement_legs_of_owned_non_consumables()
        {
            var s = await Initialized(UnityIapPlatform.Ios, Catalog.GoldPack(), Catalog.RemoveAdsProduct(), Catalog.StarterPack());
            _bridge.OwnedNonConsumables.AddRange(new[] { Catalog.RemoveAds, Catalog.Starter, Catalog.Gold, "com.not.in.catalog" });

            var result = await s.RestoreAsync(CancellationToken.None).AsTask();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(RestoreResult.Success));
                Assert.That(_bridge.RestoreTransactionsCalls, Is.EqualTo(1));
                Assert.That(_economy.Has("remove_ads"), Is.True);
                Assert.That(_economy.Has("starter_pack"), Is.True);
                Assert.That(_economy.Coins, Is.Zero, "the starter pack's coin leg is never restored, nor a consumable");
                Assert.That(_analytics.Of<PurchaseRestored>().Single().Count, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task Restore_off_iOS_skips_Apple_restore()
        {
            var s = await Initialized(UnityIapPlatform.Android);
            _bridge.OwnedNonConsumables.Add(Catalog.RemoveAds);
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Success));
            Assert.That(_bridge.RestoreTransactionsCalls, Is.Zero);
        }

        [Test]
        public async Task Restore_with_nothing_owned_is_Nothing_and_still_tracked()
        {
            var s = await Initialized(UnityIapPlatform.Ios);
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Nothing));
            Assert.That(_analytics.Of<PurchaseRestored>().Single().Count, Is.Zero);
        }

        [Test]
        public async Task Restore_fails_when_Apple_restore_fails()
        {
            var s = await Initialized(UnityIapPlatform.Ios);
            _bridge.AppleRestoreAnswer = (false, "user not signed in");
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Failed));
            Assert.That(_bridge.FetchPurchasesCalls, Is.EqualTo(1), "only the post-init fetch");
        }

        [Test]
        public async Task Restore_fails_when_the_purchase_fetch_fails()
        {
            var s = await Initialized();
            _bridge.PurchasesFetchError = "Unknown";
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Failed));
        }

        [Test]
        public async Task Restore_fails_when_the_store_never_answers()
        {
            var s = await Initialized();
            _bridge.AutoAnswerPurchases = false;
            var restore = s.RestoreAsync(CancellationToken.None).AsTask();
            Assert.That(restore.IsCompleted, Is.False);
            _delay.ElapsePending();
            Assert.That(await restore, Is.EqualTo(RestoreResult.Failed));
        }

        [Test]
        public async Task Restore_before_init_or_twice_at_once_fails()
        {
            Assert.That(await Service().RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Failed));

            var s = await Initialized();
            _bridge.AutoAnswerPurchases = false;
            var first = s.RestoreAsync(CancellationToken.None).AsTask();
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Failed));
            _bridge.RaisePurchasesFetched(Catalog.RemoveAds);
            Assert.That(await first, Is.EqualTo(RestoreResult.Success));
        }

        [Test]
        public async Task An_explicit_restore_replays_time_grants_like_the_fake()
        {
            var s = await Initialized(UnityIapPlatform.Android, Catalog.VipWeek());
            _bridge.OwnedNonConsumables.Add(Catalog.Vip);
            Assert.That(await s.RestoreAsync(CancellationToken.None).AsTask(), Is.EqualTo(RestoreResult.Success));
            Assert.That(_economy.Has("vip"), Is.True);
        }

        // ── the post-init purchase fetch (reinstall) ───────────────────────────────────────────────────

        [Test]
        public async Task After_init_a_store_owned_permanent_entitlement_this_install_lacks_is_regained_silently()
        {
            _bridge.OwnedNonConsumables.AddRange(new[] { Catalog.RemoveAds, Catalog.Starter });
            await Initialized();

            Assert.Multiple(() =>
            {
                Assert.That(_economy.Has("remove_ads"), Is.True);
                Assert.That(_economy.Has("starter_pack"), Is.True);
                Assert.That(_economy.Coins, Is.Zero, "never a coin leg");
                Assert.That(_analytics.Events, Is.Empty, "not a purchase, not a restore");
            });
        }

        [Test]
        public async Task After_init_a_held_entitlement_or_a_time_grant_is_not_reapplied()
        {
            _economy.Entitlements.Grant(new EntitlementKey("remove_ads"), GrantSource.Iap);
            _bridge.OwnedNonConsumables.AddRange(new[] { Catalog.RemoveAds, Catalog.Vip });
            await Initialized(UnityIapPlatform.Android, Catalog.RemoveAdsProduct(), Catalog.VipWeek());

            Assert.That(_economy.Has("vip"), Is.False, "a time-grant would extend on every launch");
            Assert.That(_log.Infos.Any(i => i.Contains("re-applied")), Is.False, "remove_ads was already held");
        }

        private sealed class ThrowingEntitlementGate : IEntitlementService
        {
            public bool Has(EntitlementKey key) => false;
            public void Grant(EntitlementKey key, GrantSource source, TimeSpan? duration = null) => throw new InvalidOperationException("gate down");
            public void Revoke(EntitlementKey key, GrantSource source) { }
        }
    }
}
