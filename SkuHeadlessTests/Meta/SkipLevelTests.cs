using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using SqadSort.SkuHeadlessTests.Analytics;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-064 (features/skip-level.md): Skip Level from the 3rd loss in a row on a level, by ad or coins, 3 a day; it counts as
    /// a win and still pays the win coins (Phat: as the Asana task says).</summary>
    public class SkipLevelTests
    {
        TestStore store; TestWallet wallet; TestFacts facts; EconomyTuning tuning; LevelProgressService svc;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        const int Level = 21;   // past the unlock level and not an intro level (5 / 11 / 15 boosters, 8 / 20 elements)

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); facts = new TestFacts(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, wallet, tuning, facts);
            store.Progress.CurrentLevel = Level; store.Progress.HighestCleared = Level - 1;
        }

        void Lose(int level, int times) { for (int i = 0; i < times; i++) svc.RecordLoss(level); }

        [Test]
        public void The_third_loss_in_a_row_offers_skip()
        {
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.False, "1st loss");
            Lose(Level, 1);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.False, "2nd loss");
            Lose(Level, 1);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.True, "3rd loss");
            Lose(Level, 1);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.True, "4th loss");
        }

        [Test]
        public void A_loss_elsewhere_a_win_or_a_skip_restarts_the_streak()
        {
            Lose(Level, 2);
            Lose(Level + 1, 1);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.False, "another level broke the streak");

            Lose(Level, 2);
            svc.CompleteLevel(Level);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.False, "a win restarts it");

            Lose(Level, 2);
            svc.SkipLevel(Level, 40, Now);
            Assert.That(svc.CanOfferSkip(Level, false, Now), Is.False, "a skip restarts it");
        }

        [TestCase(1)]   // tutorial level
        [TestCase(3)]   // below skip_level_unlock_level (5)
        [TestCase(5)]   // Undo is introduced
        [TestCase(8)]   // Hidden is introduced
        [TestCase(20)]  // House is introduced
        public void Not_on_tutorial_locked_or_intro_levels(int level)
        {
            Lose(level, 2);
            Assert.That(svc.CanOfferSkip(level, tutorialLevel: level == 1, Now), Is.False);
        }

        [Test]
        public void Three_skips_a_day_and_a_new_day_gives_them_back()
        {
            for (int i = 0; i < 3; i++)
            {
                Lose(Level + i, 2);
                Assert.That(svc.SkipsLeft(Now), Is.EqualTo(3 - i));
                Assert.That(svc.CanOfferSkip(Level + i, false, Now), Is.True);
                svc.SkipLevel(Level + i, 40, Now);
            }
            Lose(Level + 3, 2);
            Assert.That(svc.SkipsLeft(Now), Is.EqualTo(0));
            Assert.That(svc.CanOfferSkip(Level + 3, false, Now), Is.False, "today's skips are used");
            Assert.That(svc.CanOfferSkip(Level + 3, false, Now.AddDays(1)), Is.True, "tomorrow");
            Assert.That(svc.SkipsLeft(Now.AddDays(1)), Is.EqualTo(3));
        }

        [Test]
        public void A_skip_counts_as_the_win_and_still_pays_the_win_coins()
        {
            var win = svc.SkipLevel(Level, 40, Now);
            Assert.That((win.Saved, win.NextLevel, win.Reward), Is.EqualTo((true, Level + 1, tuning.WinReward)));
            Assert.That((store.Progress.CurrentLevel, store.Progress.HighestCleared), Is.EqualTo((Level + 1, Level)));
            Assert.That(svc.Coins, Is.EqualTo(tuning.WinReward));
        }

        [Test]
        public void Coins_pay_for_the_skip_in_one_transaction()
        {
            wallet.Grant(SqadSortResources.Coin, 1240, GrantSource.Reward);
            Assert.That(svc.CanAffordSkip, Is.True);
            Assert.That(svc.TrySkipWithCoins(Level, 40, Now, out var win), Is.True);
            Assert.That(win.Saved, Is.True);
            Assert.That(svc.Coins, Is.EqualTo(1240L - 900 + tuning.WinReward));
            Assert.That(facts.Log, Does.Contain("spend coins 900 ep_skip_level"));
            Assert.That(facts.Log, Does.Contain($"earn coins {tuning.WinReward} level_up"));
        }

        [Test]
        public void Short_on_coins_or_a_failed_save_changes_nothing()
        {
            wallet.Grant(SqadSortResources.Coin, 899, GrantSource.Reward);
            Assert.That(svc.CanAffordSkip, Is.False);
            Assert.That(svc.TrySkipWithCoins(Level, 40, Now, out _), Is.False);
            Assert.That((svc.Coins, store.Progress.CurrentLevel), Is.EqualTo((899L, Level)));

            wallet.Grant(SqadSortResources.Coin, 1, GrantSource.Reward);
            store.Fail = true;
            Assert.That(svc.TrySkipWithCoins(Level, 40, Now, out _), Is.False);
            store.Fail = false;
            Assert.That((svc.Coins, store.Progress.CurrentLevel, svc.SkipsLeft(Now)), Is.EqualTo((900L, Level, 3)));
            Assert.That(facts.Log, Is.Empty);
        }

        [Test]
        public void The_four_keys_are_remote_config_with_the_agreed_defaults()
        {
            var v = EconomyConfig.Defaults().Values;
            Assert.That((v["skip_level_unlock_level"], v["skip_level_fail_count"], v["skip_level_max_per_day"], v["skip_level_coin_cost"]),
                Is.EqualTo((5, 3, 3, 900)));
        }

        [Test]
        public void Tracking_sends_level_skipped_with_the_level_the_player_goes_on_to()
        {
            var sink = new EventSink();
            var t = new GameTracking(sink, wallet, store, false);
            t.LevelStarted(Level, 3);
            t.Tapped();
            t.LevelSkipped(Level, Level + 1);
            var e = sink.Last("level_skipped");
            Assert.That((e["level_id"], e["level_launch"], e["actual_level"], e["level_counter"]), Is.EqualTo((Level, 3, Level + 1, (object)null)));
            Assert.That(sink.Names.Last(), Is.EqualTo("level_up"));
            Assert.That(sink.Names, Does.Not.Contain("level_completed"));
            t.LevelAbandoned();
            Assert.That(sink.Names, Does.Not.Contain("level_abandoned"), "the skip ended the attempt");
        }
        [Test]
        public void Retry_skip_waits_for_the_third_recorded_loss()
        {
            Lose(Level, 2);
            Assert.That(svc.CanOfferSkipAfterRestart(Level, false, Now), Is.False);
            Lose(Level, 1);
            Assert.That(svc.CanOfferSkipAfterRestart(Level, false, Now), Is.True);
            Assert.That(svc.CanOfferSkipAfterRestart(Level, true, Now), Is.False);
            Assert.That(svc.CanOfferSkipAfterRestart(Level + 1, false, Now), Is.False);
        }    }
}
