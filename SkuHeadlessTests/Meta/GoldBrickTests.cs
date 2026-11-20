using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-070 Gold Brick Package! (GDD Block Sort 7.3, Money Sort GarageGift): from level 10, priority 3 on a level complete
    /// screen and 2 at app start; a 10-minute window opens with the popup, one popup per window; claimed ⇒ 200 coins + 1 Hand and a
    /// 4-hour cooldown from now; not claimed ⇒ the cooldown runs from the window's end.</summary>
    public class GoldBrickTests
    {
        TestStore store; TestWallet wallet; TestFacts facts; EconomyTuning tuning; LevelProgressService progress; OfferSchedule offers;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); facts = new TestFacts(); tuning = new EconomyTuning();
            progress = new LevelProgressService(store, wallet, tuning, facts);
            store.Progress.StarterOfferFromUtc = 1;   // the Starter Pack out of the way
            offers = new OfferSchedule(store, tuning);
        }

        LevelCompleteOffer Win(int level, DateTimeOffset at) => offers.AtLevelComplete(level, false, false, at);

        [Test]
        public void The_defaults_are_the_GDD_numbers()
        {
            Assert.That((tuning.GoldBrickUnlockLevel, tuning.GoldBrickCoins, tuning.GoldBrickActiveMinutes, tuning.GoldBrickCooldownHours),
                Is.EqualTo((10, 200, 10, 4)));
        }

        [Test]
        public void It_unlocks_at_level_10_and_opens_a_10_minute_window()
        {
            Assert.That(Win(9, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(Win(10, Now), Is.EqualTo(LevelCompleteOffer.GoldBrick));
            Assert.That(offers.GoldBrickLeft(Now.AddMinutes(4)), Is.EqualTo(TimeSpan.FromMinutes(6)));
            Assert.That(Win(11, Now.AddMinutes(4)), Is.EqualTo(LevelCompleteOffer.None));   // one popup per window
        }

        [Test]
        public void Claiming_grants_200_coins_and_a_hand_and_restarts_the_cooldown()
        {
            Win(10, Now);
            Assert.That(offers.ClaimGoldBrick(progress, Now.AddMinutes(2)), Is.True);
            Assert.That((progress.Coins, store.Progress.BoosterUndo, offers.GoldBrickLeft(Now.AddMinutes(2))), Is.EqualTo((200L, 1, TimeSpan.Zero)));
            Assert.That(facts.Log, Is.EqualTo(new[] { "earn coins 200 garage_reward", "earn ap_booster_undo 1 garage_reward" }));
            Assert.That(Win(11, Now.AddMinutes(2).AddHours(4).AddSeconds(-1)), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(Win(11, Now.AddMinutes(2).AddHours(4)), Is.EqualTo(LevelCompleteOffer.GoldBrick));
            Assert.That(offers.ClaimGoldBrick(progress, Now.AddHours(10)), Is.False);   // window over
        }

        [Test]
        public void Not_claimed_the_cooldown_runs_from_the_window_end()
        {
            Win(10, Now);
            Assert.That(Win(11, Now.AddMinutes(10).AddHours(4).AddSeconds(-1)), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(Win(11, Now.AddMinutes(10).AddHours(4)), Is.EqualTo(LevelCompleteOffer.GoldBrick));
        }

        [Test]
        public void Higher_priority_offers_hold_it_back()
        {
            store.Progress.StarterOfferFromUtc = 0;
            Assert.That(Win(12, Now), Is.EqualTo(LevelCompleteOffer.Starter));
            Assert.That(store.Progress.GoldBrickActiveEndsUtc, Is.EqualTo(0));   // the window waits for its own popup
            Assert.That(Win(13, Now), Is.EqualTo(LevelCompleteOffer.GoldBrick));
        }

        [Test]
        public void At_app_start_starter_first_then_the_gift()
        {
            Assert.That(offers.AtSessionStart(progress, 9, Now), Is.EqualTo(LevelCompleteOffer.None));
            Assert.That(offers.AtSessionStart(progress, 10, Now), Is.EqualTo(LevelCompleteOffer.GoldBrick));
            store.Progress.StarterOfferFromUtc = Now.ToUnixTimeSeconds();
            Assert.That(offers.AtSessionStart(progress, 10, Now), Is.EqualTo(LevelCompleteOffer.Starter));
        }

        [Test]
        public void An_ad_started_inside_the_window_pays_even_if_the_window_ran_out_meanwhile()
        {
            // QA RV-07: Get Reward pressed at 9:55; the window ends (and is reported expired) while the ad plays; it completes at 10:03
            Win(10, Now);
            Assert.That(offers.TakeExpiredGoldBrick(Now.AddMinutes(10), out _), Is.True);
            Assert.That(offers.ClaimGoldBrick(progress, Now.AddMinutes(10).AddSeconds(3), adStartedInWindow: true), Is.True);
            Assert.That((progress.Coins, store.Progress.BoosterUndo), Is.EqualTo((200L, 1)));
            Assert.That(Win(11, Now.AddMinutes(10).AddSeconds(3).AddHours(4).AddSeconds(-1)), Is.EqualTo(LevelCompleteOffer.None));   // cooldown from the claim
            Assert.That(offers.ClaimGoldBrick(progress, Now.AddMinutes(11)), Is.False);   // without the ad started in time: nothing
        }

        [Test]
        public void A_failed_save_grants_nothing()
        {
            Win(10, Now);
            store.Fail = true;
            Assert.That(offers.ClaimGoldBrick(progress, Now), Is.False);
            Assert.That((progress.Coins, store.Progress.BoosterUndo), Is.EqualTo((0L, 0)));
        }
    }
}
