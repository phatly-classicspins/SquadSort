using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    public class RestartBoosterTests
    {
        private TestStore store;
        private LevelProgressService service;
        [SetUp] public void SetUp()
        {
            store = new TestStore();
            service = new LevelProgressService(store, new TestWallet(), new EconomyTuning(), new TestFacts());
        }
        [Test] public void Requires_two_losses_and_an_unlocked_add_container()
        {
            service.RecordLoss(16);
            service.RecordLoss(16);
            Assert.That(service.CanOfferRestartBooster(16), Is.False);
            service.UnlockBoostersFor(16);
            Assert.That(service.CanOfferRestartBooster(16), Is.True);
            Assert.That(service.CanOfferRestartBooster(17), Is.False);
        }
        [Test] public void Does_not_offer_after_the_first_loss()
        {
            service.UnlockBoostersFor(16);
            service.RecordLoss(16);
            Assert.That(service.TryMarkRestartBoosterOffered(16), Is.False);
        }
        [Test] public void Marking_persists_once_and_survives_clone_restore()
        {
            service.UnlockBoostersFor(16);
            service.RecordLoss(16); service.RecordLoss(16);
            Assert.That(service.TryMarkRestartBoosterOffered(16), Is.True);
            store.Progress.CopyFrom(store.Progress.Clone());
            Assert.That(service.TryMarkRestartBoosterOffered(16), Is.False);
        }
        [Test] public void Failed_save_does_not_consume_the_offer()
        {
            service.UnlockBoostersFor(16);
            service.RecordLoss(16); service.RecordLoss(16);
            store.Fail = true;
            Assert.That(service.TryMarkRestartBoosterOffered(16), Is.False);
            Assert.That(service.CanOfferRestartBooster(16), Is.True);
        }
        [Test] public void Undo_unlock_alone_does_not_allow_an_add_container_offer()
        {
            service.UnlockBoostersFor(5);
            service.RecordLoss(5); service.RecordLoss(5);
            Assert.That(service.IsUnlocked(Booster.Undo), Is.True);
            Assert.That(service.CanOfferRestartBooster(5), Is.False);
        }
        [Test] public void Reward_is_exactly_one_magnet_regardless_of_normal_ad_amount()
        {
            service = new LevelProgressService(store, new TestWallet(), new EconomyTuning { AddContainerAdAmount = 9 }, new TestFacts());
            Assert.That(service.GrantRestartBoosterFromAd(), Is.True);
            Assert.That(service.Boosters(Booster.AddContainer), Is.EqualTo(1));
            Assert.That(service.Boosters(Booster.Undo), Is.Zero);
            Assert.That(service.Boosters(Booster.Shuffle), Is.Zero);
        }
        [Test] public void Failed_reward_save_does_not_grant_a_magnet()
        {
            store.Fail = true;
            Assert.That(service.GrantRestartBoosterFromAd(), Is.False);
            Assert.That(service.Boosters(Booster.AddContainer), Is.Zero);
        }    }
}