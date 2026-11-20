using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using SqadSort.SkuHeadlessTests.Meta;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Analytics
{
    /// <summary>D-071: the shop and offer events of Money Sort's tracking plan (ShopService.Track / TrackStoreEntered, GarageGift) —
    /// names, parameters, and the Adjust purchase token.</summary>
    public class ShopTrackingTests
    {
        EventSink sink; TestStore store; TestWallet wallet; GameTracking t;
        static ShopPack P(string id) => ShopCatalog.Find(id);

        [SetUp]
        public void SetUp()
        {
            sink = new EventSink(); store = new TestStore(); wallet = new TestWallet();
            store.Progress.FirstLoadingTracked = true;
            t = new GameTracking(sink, wallet, store, isTest: true);
            wallet.Grant(SqadSortResources.Coin, 560, GrantSource.Reward);
        }

        static object V(TrackedEvent e, string key) => e.Params.First(p => p.Key == key).Value;

        [Test]
        public void Store_entered_lists_the_store_product_ids()
        {
            t.StoreEntered(TrackingNames.ShopStore, "Shop", TrackingNames.HomeShop, new[] { P("coins_1K").StoreId, P("pack_small").StoreId });
            var e = sink.Last(TrackingNames.StoreEntered);
            Assert.That(e.Params.Select(p => p.Key), Is.EqualTo(new[] { "store_id", "store_name", "store_type", "store_items", "placement", "additional_data" }));
            Assert.That((V(e, "store_id"), V(e, "store_type"), V(e, "store_items"), V(e, "placement")),
                Is.EqualTo(("shop", "iap", "[\"squad.sort.coins_1K\",\"squad.sort.pack_small\"]", "home_shop")));
        }

        [Test]
        public void Iap_start_carries_the_usd_list_price()
        {
            t.IapStarted(P("pack_medium"), "Medium Pack", TrackingNames.HomeShop);
            var e = sink.Last(TrackingNames.IapStart);
            Assert.That((V(e, "product_id"), V(e, "product_name"), V(e, "balance"), V(e, "placement"), V(e, "real_currency_name"), V(e, "value")),
                Is.EqualTo(("squad.sort.pack_medium", "Medium Pack", 560L, "home_shop", "USD", 9.99)));
        }

        [Test]
        public void Iap_completed_reports_the_store_the_reward_items_and_a_test_purchase()
        {
            t.IapCompleted(P("remove_ads"), "Remove Ads Pack", TrackingNames.HomeShop);
            var e = sink.Last(TrackingNames.IapCompleted);
            Assert.That((V(e, "item_category"), V(e, "receiptStatus"), V(e, "isTestPurchase")), Is.EqualTo(("shop", "success", true)));
            Assert.That(V(e, "reward"), Is.EqualTo("[{\"itemID\":\"coins\",\"Name\":\"Coins\",\"Type\":\"soft\"}," +
                "{\"itemID\":\"remove_ads\",\"Name\":\"Remove Ads\",\"Type\":\"feature\"}]"));   // D-140: no boosters
            t.IapCompleted(P("pack_small"), "Small Pack", TrackingNames.HomeShop);
            Assert.That((string)V(sink.Last(TrackingNames.IapCompleted), "reward"), Does.Contain("ap_booster_undo"));
            t.IapCompleted(P("starter_pack"), "Starter Pack", TrackingNames.StarterPopup);
            Assert.That(V(sink.Last(TrackingNames.IapCompleted), "item_category"), Is.EqualTo("starter_pack_popup"));   // a popup is its own store
            Assert.That((string)V(sink.Last(TrackingNames.IapCompleted), "reward"), Does.Contain("infinite_lives"));
        }

        [Test]
        public void Iap_failed_and_restored()
        {
            t.IapFailed(P("coins_1K"), "1,000 Gold", "ep_revive", "cancelled");
            var f = sink.Last(TrackingNames.IapFailed);
            Assert.That((V(f, "item_category"), V(f, "receiptStatus")), Is.EqualTo(("ep_revive", "cancelled")));
            Assert.That(f.Params.Any(p => p.Key == "reward"), Is.False);
            t.IapRestored(P("remove_ads"), "Remove Ads Pack");
            Assert.That(V(sink.Last(TrackingNames.IapRestored), "item_category"), Is.EqualTo("restore"));
        }

        [Test]
        public void Rv_popups_shown_and_expired()
        {
            t.RvPopupShown(TrackingNames.GoldBrickRv, 0, true);
            var s = sink.Last(TrackingNames.RvPopupShown);
            Assert.That(s.Params.Select(p => p.Key), Is.EqualTo(new[] { "placement", "level_id", "fail_count", "rv_available", "balance" }));
            Assert.That((V(s, "placement"), V(s, "rv_available")), Is.EqualTo(("gold_block_reward", true)));
            t.RvPopupExpired(TrackingNames.GoldBrickRv, false);
            Assert.That(V(sink.Last(TrackingNames.RvPopupExpired), "was_shown"), Is.EqualTo(false));
        }

        [Test]
        public void A_gift_window_that_ran_out_is_reported_once()
        {
            var tuning = new EconomyTuning();
            var offers = new OfferSchedule(store, tuning);
            store.Progress.StarterOfferFromUtc = 1;
            var now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
            offers.AtLevelComplete(10, false, false, now);   // opens a 10-minute window, shown
            Assert.That(offers.TakeExpiredGoldBrick(now.AddMinutes(9), out _), Is.False);
            Assert.That(offers.TakeExpiredGoldBrick(now.AddMinutes(10), out bool shown), Is.True);
            Assert.That(shown, Is.True);
            Assert.That(offers.TakeExpiredGoldBrick(now.AddMinutes(11), out _), Is.False);
        }
    }
}
