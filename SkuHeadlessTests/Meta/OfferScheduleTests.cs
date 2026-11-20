using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-069 (GDD Block Sort 6.4 / 7.2, Money Sort TryShowLevelCompleteOffer): one offer per level complete screen, Starter
    /// Pack first; the Remove Ads popup only after an interstitial, from level 15, on its 1 → every 4 → every 8 schedule.</summary>
    public class OfferScheduleTests
    {
        TestStore store; EconomyTuning tuning; OfferSchedule offers;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); tuning = new EconomyTuning();
            store.Progress.StarterOfferFromUtc = 1;   // the Starter Pack is out of the way unless a test wants it
            store.Progress.GoldBrickNextAvailableUtc = long.MaxValue / 2;   // and the Gold Brick Package! (D-070)
            offers = new OfferSchedule(store, tuning);
        }

        LevelCompleteOffer AfterAd(int level, DateTimeOffset at) => offers.AtLevelComplete(level, true, false, at);

        void Interstitials(int n) { for (int i = 0; i < n; i++) offers.InterstitialShown(); }

        [Test]
        public void Remove_ads_needs_an_interstitial_and_level_15()
        {
            Assert.That(offers.AtLevelComplete(20, false, false, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(AfterAd(14, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(offers.AtLevelComplete(20, true, true, Now), Is.EqualTo(LevelCompleteOffer.None));   // owned
            Assert.That(offers.RemoveAdsIconVisible(false), Is.False);
            Assert.That(AfterAd(15, Now), Is.EqualTo(LevelCompleteOffer.RemoveAds));
            Assert.That(offers.RemoveAdsIconVisible(false), Is.True);
            Assert.That(offers.RemoveAdsIconVisible(true), Is.False);
        }

        [Test]
        public void Interstitials_count_only_after_the_first_popup()
        {
            Interstitials(10);
            Assert.That(store.Progress.RemoveAdsInterstitials, Is.EqualTo(0));
            AfterAd(15, Now);
            Interstitials(3);
            Assert.That(store.Progress.RemoveAdsInterstitials, Is.EqualTo(3));
        }

        [Test]
        public void Shows_2_to_4_need_4_interstitials_and_20_minutes_in_the_same_session()
        {
            AfterAd(15, Now);
            Interstitials(3);
            Assert.That(AfterAd(16, Now.AddHours(1)), Is.EqualTo(LevelCompleteOffer.None));
            Interstitials(1);
            Assert.That(AfterAd(16, Now.AddMinutes(19)), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(AfterAd(16, Now.AddMinutes(20)), Is.EqualTo(LevelCompleteOffer.RemoveAds));
            Assert.That(store.Progress.RemoveAdsInterstitials, Is.EqualTo(0));
        }

        [Test]
        public void A_new_session_skips_the_20_minutes()
        {
            AfterAd(15, Now);
            var next = new OfferSchedule(store, tuning);   // the app restarted
            for (int i = 0; i < 4; i++) next.InterstitialShown();
            Assert.That(next.AtLevelComplete(16, true, false, Now.AddMinutes(1)), Is.EqualTo(LevelCompleteOffer.RemoveAds));
        }

        [Test]
        public void From_show_5_every_8_interstitials_twice_a_day()
        {
            store.Progress.RemoveAdsPopupShown = 4;
            Interstitials(7);
            Assert.That(AfterAd(30, Now), Is.EqualTo(LevelCompleteOffer.None));
            Interstitials(1);
            Assert.That(AfterAd(30, Now), Is.EqualTo(LevelCompleteOffer.RemoveAds));
            Interstitials(8);
            Assert.That(AfterAd(31, Now.AddHours(1)), Is.EqualTo(LevelCompleteOffer.RemoveAds));
            Interstitials(8);
            Assert.That(AfterAd(32, Now.AddHours(2)), Is.EqualTo(LevelCompleteOffer.None));   // 2 in this 24 hours
            Assert.That(AfterAd(32, Now.AddHours(24)), Is.EqualTo(LevelCompleteOffer.RemoveAds));
        }

        [Test]
        public void The_starter_pack_comes_first_and_holds_remove_ads_back()
        {
            store.Progress.StarterOfferFromUtc = 0;
            Assert.That(AfterAd(15, Now), Is.EqualTo(LevelCompleteOffer.Starter));
            Assert.That(store.Progress.RemoveAdsPopupShown, Is.EqualTo(0));
            Assert.That(AfterAd(16, Now), Is.EqualTo(LevelCompleteOffer.RemoveAds));   // the next interstitial
        }

        [Test]
        public void A_failed_save_shows_nothing()
        {
            store.Fail = true;
            Assert.That(AfterAd(15, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(store.Progress.RemoveAdsPopupShown, Is.EqualTo(0));
        }
    }
}
