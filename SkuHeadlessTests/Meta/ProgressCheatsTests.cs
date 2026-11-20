using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-048: the tester menu's save changes — persisted like every other change, rolled back when the save fails (G18).</summary>
    public class ProgressCheatsTests
    {
        TestStore store; TestWallet wallet; EconomyTuning tuning; ProgressCheats cheats; LevelProgressService svc;
        readonly DateTimeOffset t0 = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); tuning = new EconomyTuning();
            cheats = new ProgressCheats(store, wallet, tuning);
            svc = new LevelProgressService(store, wallet, tuning, new TestFacts());
        }

        [Test]
        public void Go_to_level_sets_current_and_cleared_and_clamps()
        {
            Assert.That(cheats.SetLevel(12, 30), Is.EqualTo(12));
            Assert.That(svc.CurrentLevel, Is.EqualTo(12));
            Assert.That(svc.HighestCleared, Is.EqualTo(11));
            Assert.That(cheats.SetLevel(99, 30), Is.EqualTo(30));
            Assert.That(cheats.SetLevel(0, 30), Is.EqualTo(1));
            Assert.That(svc.HighestCleared, Is.EqualTo(0), "going back lowers progress too: a tester wants that level fresh");
        }

        [Test]
        public void Coins_and_boosters_are_added_and_saved()
        {
            Assert.That(cheats.AddCoins(500), Is.True);
            Assert.That(svc.Coins, Is.EqualTo(500));
            Assert.That(cheats.AddBooster(Booster.Shuffle, 5), Is.True);
            Assert.That(cheats.AddBooster(Booster.Undo, -3), Is.True);
            Assert.That(svc.Boosters(Booster.Shuffle), Is.EqualTo(5));
            Assert.That(svc.Boosters(Booster.Undo), Is.EqualTo(0), "never below zero");
            Assert.That(store.Saves, Is.EqualTo(3));
        }

        [Test]
        public void A_failed_save_rolls_back()
        {
            store.Fail = true;
            Assert.That(cheats.AddCoins(500), Is.False);
            Assert.That(svc.Coins, Is.EqualTo(0));
            Assert.That(cheats.AddBooster(Booster.AddContainer, 5), Is.False);
            Assert.That(svc.Boosters(Booster.AddContainer), Is.EqualTo(0));
            Assert.That(cheats.SetLevel(10, 30), Is.EqualTo(10));
            Assert.That(svc.CurrentLevel, Is.EqualTo(1));
        }

        [Test]
        public void Lives_can_be_emptied_and_refilled()
        {
            Assert.That(cheats.SetLives(0, t0), Is.True);
            Assert.That(svc.Lives(t0), Is.EqualTo(0));
            Assert.That(svc.NextLifeIn(t0), Is.EqualTo(TimeSpan.FromMinutes(tuning.LivesRefillMinutes)));
            Assert.That(cheats.SetLives(99, t0), Is.True);
            Assert.That(svc.Lives(t0), Is.EqualTo(tuning.LivesMax));
            Assert.That(store.Progress.LivesRefillFromUtc, Is.EqualTo(0));
        }

        [Test]
        public void Unlock_all_unlocks_without_gifts()
        {
            Assert.That(cheats.UnlockAll(), Is.True);
            foreach (Booster id in Enum.GetValues(typeof(Booster))) Assert.That(svc.IsUnlocked(id), Is.True);
            foreach (ElementId id in Enum.GetValues(typeof(ElementId))) Assert.That(svc.IsUnlocked(id), Is.True);
            Assert.That(svc.Boosters(Booster.Undo), Is.EqualTo(0));
            Assert.That(svc.UnlockBoostersFor(30), Is.Empty, "nothing left to unlock, so no popup comes later");
        }

        [Test]
        public void Reset_tutorial_and_reset_all()
        {
            svc.MarkFtueDone("ftue.l1.done");
            Assert.That(cheats.ResetTutorial(), Is.True);
            Assert.That(svc.IsFtueDone("ftue.l1.done"), Is.False);

            cheats.SetLevel(9, 30);
            cheats.AddCoins(700);
            Assert.That(cheats.ResetAll(), Is.True);
            Assert.That(svc.CurrentLevel, Is.EqualTo(1));
            Assert.That(svc.Coins, Is.EqualTo(0));
            Assert.That(store.Progress.StartCoinsGranted, Is.False, "the start coins come again, like a fresh install");
        }
    }
}
