using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-067 (features/shop.md): the GDD 6.1 catalog, what is on sale, granting a pack as one transaction, the Starter Pack
    /// window and unlimited lives.</summary>
    public class ShopTests
    {
        TestStore store; TestWallet wallet; TestFacts facts; EconomyTuning tuning; LevelProgressService svc;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); facts = new TestFacts(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, wallet, tuning, facts);
        }

        static ShopPack P(string id) => ShopCatalog.Find(id);

        [Test]
        public void The_catalog_is_GDD_6_1_in_store_order()
        {
            Assert.That(ShopCatalog.All.Select(p => p.Id), Is.EqualTo(new[] { "remove_ads", "starter_pack", "pack_small", "pack_medium", "pack_ultra",
                "coins_1K", "coins_5K", "coins_10K", "coins_25K", "coins_50K", "coins_100K" }));
            Assert.That(P("pack_ultra"), Is.EqualTo(new ShopPack("pack_ultra", ShopSection.Special, 25000, 10, 6, false, false, 24.99m, ShopTag.None)));
            Assert.That((P("remove_ads").StoreId, ShopCatalog.FallbackPrice(P("remove_ads"))), Is.EqualTo(("squad.sort.remove_ads", "$5.99")));
            Assert.That(ShopCatalog.All.Where(p => p.OneTime).Select(p => p.Id), Is.EqualTo(new[] { "remove_ads", "starter_pack" }));
            Assert.That((P("remove_ads").Tag, P("starter_pack").Tag, P("coins_100K").Tag), Is.EqualTo((ShopTag.Hot, ShopTag.Off, ShopTag.BestValue)));
            Assert.That((tuning.StarterPackUnlockLevel, tuning.StarterPackDurationHours, tuning.RemoveAdsUnlockLevel), Is.EqualTo((12, 72, 15)));
        }

        [Test]
        public void Before_the_starter_window_the_store_has_everything_but_the_starter()
        {
            Assert.That(svc.ShopPacks(Now, false).Select(p => p.Id), Does.Not.Contain("starter_pack"));
            Assert.That(svc.ShopPacks(Now, false).Count, Is.EqualTo(10));
            Assert.That(svc.ShopPacks(Now, true).Select(p => p.Id), Does.Not.Contain("remove_ads"));
        }

        [Test]
        public void A_pack_grants_each_booster_and_unlimited_lives_in_one_save_and_reports_its_coins()
        {
            // QA IAP-01: the coins are the store's grant (products.json), applied by the IAP module — not here
            store.Progress.BoosterUndo = 1;
            Assert.That(svc.GrantPurchase(P("pack_medium"), Now, TrackingNames.HomeShop), Is.True);
            var p = store.Progress;
            Assert.That((svc.Coins, p.BoosterUndo, p.BoosterShuffle, p.BoosterAddContainer), Is.EqualTo((0L, 6, 5, 5)));
            Assert.That(svc.UnlimitedLivesLeft(Now), Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(facts.Log, Is.EqualTo(new[] { "earn coins 10000 home_shop", "earn ap_booster_undo 5 home_shop",
                "earn ap_booster_shuffle 5 home_shop", "earn ap_booster_add_container 5 home_shop" }));
        }

        [Test]
        public void Unlimited_lives_add_up_and_cost_no_life()
        {
            svc.GrantPurchase(P("pack_small"), Now, TrackingNames.HomeShop);
            svc.GrantPurchase(P("pack_ultra"), Now.AddMinutes(30), TrackingNames.HomeShop);
            Assert.That(svc.UnlimitedLivesLeft(Now), Is.EqualTo(TimeSpan.FromHours(7)));   // 1h + 6h
            store.Progress.Lives = 0; store.Progress.LivesRefillFromUtc = Now.ToUnixTimeSeconds();
            Assert.That(svc.Lives(Now), Is.EqualTo(tuning.LivesMax));
            Assert.That(svc.TrySpendLife(Now), Is.True);
            Assert.That(store.Progress.Lives, Is.EqualTo(0));
            var after = Now.AddHours(8);
            Assert.That((svc.UnlimitedLivesLeft(after), svc.Lives(after)), Is.EqualTo((TimeSpan.Zero, tuning.LivesMax)));   // refilled meanwhile
        }

        [Test]
        public void Coins_only_packs_add_nothing_beyond_the_store_grant()
        {
            Assert.That(svc.GrantPurchase(P("coins_1K"), Now, TrackingNames.HomeShop), Is.True);
            Assert.That((svc.Coins, store.Progress.BoosterUndo, svc.UnlimitedLivesLeft(Now)), Is.EqualTo((0L, 0, TimeSpan.Zero)));
            Assert.That(facts.Log, Is.EqualTo(new[] { "earn coins 1000 home_shop" }));
            Assert.That(svc.ShopPacks(Now, false).Select(p => p.Id), Does.Contain("coins_1K"));   // consumable: still on sale
        }

        [Test]
        public void Remove_ads_grants_no_booster()   // D-140: only what the popup shows (its 1,500 coins: the store's grant)
        {
            Assert.That(svc.GrantPurchase(P("remove_ads"), Now, TrackingNames.HomeShop), Is.True);
            var p = store.Progress;
            Assert.That((svc.Coins, p.BoosterUndo, p.BoosterShuffle, p.BoosterAddContainer, svc.UnlimitedLivesLeft(Now)),
                Is.EqualTo((0L, 0, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void A_failed_save_grants_nothing()
        {
            store.Fail = true;
            Assert.That(svc.GrantPurchase(P("pack_ultra"), Now, TrackingNames.HomeShop), Is.False);
            Assert.That((svc.Coins, store.Progress.BoosterUndo, svc.UnlimitedLivesLeft(Now)), Is.EqualTo((0L, 0, TimeSpan.Zero)));
            Assert.That(facts.Log, Is.Empty);
        }

        [Test]
        public void The_starter_window_opens_on_the_level_12_complete_screen_for_72_hours()
        {
            store.Progress.GoldBrickNextAvailableUtc = long.MaxValue / 2;   // the Gold Brick Package! out of the way (D-070)
            var offers = new OfferSchedule(store, tuning);
            Assert.That(offers.AtLevelComplete(11, false, false, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(offers.AtLevelComplete(12, false, false, Now), Is.EqualTo(LevelCompleteOffer.Starter));
            Assert.That(offers.AtLevelComplete(13, false, false, Now.AddHours(1)), Is.EqualTo(LevelCompleteOffer.None));   // once
            Assert.That(svc.StarterOfferLeft(Now.AddHours(1)), Is.EqualTo(TimeSpan.FromHours(71)));
            Assert.That(offers.StarterAtSessionStart(svc, Now.AddHours(1)), Is.True);   // then at each app start
            Assert.That(svc.ShopPacks(Now, false).Select(p => p.Id), Does.Contain("starter_pack"));
            Assert.That(svc.ShopPacks(Now.AddHours(72), false).Select(p => p.Id), Does.Not.Contain("starter_pack"));
            Assert.That(offers.StarterAtSessionStart(svc, Now.AddHours(72)), Is.False);
        }

        [Test]
        public void A_one_time_pack_is_gone_once_bought()
        {
            store.Progress.GoldBrickNextAvailableUtc = long.MaxValue / 2;   // the Gold Brick Package! out of the way (D-070)
            var offers = new OfferSchedule(store, tuning);
            offers.AtLevelComplete(20, false, false, Now);
            Assert.That(svc.GrantPurchase(P("starter_pack"), Now, TrackingNames.StarterPopup), Is.True);
            Assert.That(svc.StarterOfferLeft(Now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(svc.GrantPurchase(P("remove_ads"), Now, TrackingNames.HomeShop), Is.True);
            Assert.That(svc.ShopPacks(Now, false).Select(p => p.Id), Is.EqualTo(new[] { "pack_small", "pack_medium", "pack_ultra",
                "coins_1K", "coins_5K", "coins_10K", "coins_25K", "coins_50K", "coins_100K" }));
            Assert.That(offers.AtLevelComplete(30, false, false, Now.AddDays(10)), Is.EqualTo(LevelCompleteOffer.None));
        }
    }
}
