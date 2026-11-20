using System;
using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using Game.Gen;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-062 (CR-013 H2, features/lives.md v1.1): Out of Lives refills with 900 coins → 5 lives, or one ad → 1 life — each
    /// one transaction, reported once saved.</summary>
    public class LivesRefillTests
    {
        TestStore store; TestWallet wallet; TestFacts facts; EconomyTuning tuning; LevelProgressService svc;
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); wallet = new TestWallet(); facts = new TestFacts(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, wallet, tuning, facts);
            store.Progress.Lives = 0; store.Progress.LivesRefillFromUtc = Now.ToUnixTimeSeconds();
        }

        [Test]
        public void The_GDD_numbers_are_the_defaults()
        {
            Assert.That((tuning.LivesRefillCoinCost, tuning.LivesRefillCoinAmount, tuning.LivesRefillAdAmount), Is.EqualTo((900, 5, 1)));
            Assert.That(svc.LivesRefillCoinCost, Is.EqualTo(900));
        }

        [Test]
        public void Coins_buy_five_lives_and_stop_the_clock()
        {
            wallet.Grant(SqadSortResources.Coin, 1240, GrantSource.Reward);
            Assert.That(svc.CanAffordLivesRefill, Is.True);
            Assert.That(svc.TryRefillLivesWithCoins(Now), Is.True);
            Assert.That((svc.Coins, svc.Lives(Now), store.Progress.LivesRefillFromUtc), Is.EqualTo((340L, 5, 0L)));
            Assert.That(svc.NextLifeIn(Now), Is.EqualTo(TimeSpan.Zero));
            Assert.That(facts.Log, Is.EqualTo(new[] { "spend coins 900 out_of_lives_refill", "earn lives 5 out_of_lives_coins" }));
        }

        [Test]
        public void Short_on_coins_nothing_changes()
        {
            wallet.Grant(SqadSortResources.Coin, 899, GrantSource.Reward);
            Assert.That(svc.CanAffordLivesRefill, Is.False);
            Assert.That(svc.TryRefillLivesWithCoins(Now), Is.False);
            Assert.That((svc.Coins, svc.Lives(Now)), Is.EqualTo((899L, 0)));
            Assert.That(facts.Log, Is.Empty);
        }

        [Test]
        public void A_failed_save_gives_the_coins_back_and_no_lives()
        {
            wallet.Grant(SqadSortResources.Coin, 1000, GrantSource.Reward);
            store.Fail = true;
            Assert.That(svc.TryRefillLivesWithCoins(Now), Is.False);
            store.Fail = false;
            Assert.That((svc.Coins, svc.Lives(Now)), Is.EqualTo((1000L, 0)));
            Assert.That(store.Progress.LivesRefillFromUtc, Is.EqualTo(Now.ToUnixTimeSeconds()), "the clock keeps counting");
            Assert.That(facts.Log, Is.Empty);
        }

        [Test]
        public void The_refill_never_goes_above_the_maximum_and_reports_what_was_received()
        {
            store.Progress.Lives = 3;
            wallet.Grant(SqadSortResources.Coin, 900, GrantSource.Reward);
            svc.TryRefillLivesWithCoins(Now);
            Assert.That(svc.Lives(Now), Is.EqualTo(5));
            Assert.That(facts.Log.Last(), Is.EqualTo("earn lives 2 out_of_lives_coins"));
        }

        [Test]
        public void Remote_amounts_are_used()
        {
            var t = new EconomyTuning { LivesRefillCoinCost = 500, LivesRefillCoinAmount = 3, LivesRefillAdAmount = 2 }.Sanitize();
            var s = new LevelProgressService(store, wallet, t, facts);
            wallet.Grant(SqadSortResources.Coin, 500, GrantSource.Reward);
            s.TryRefillLivesWithCoins(Now);
            Assert.That((s.Coins, s.Lives(Now)), Is.EqualTo((0L, 3)));
            s.GrantLifeFromAd(Now);
            Assert.That(s.Lives(Now), Is.EqualTo(5));
            Assert.That(facts.Log.Last(), Is.EqualTo("earn lives 2 out_of_lives_rv"));
        }

        [Test]
        public void The_three_keys_come_from_remote_config_and_bad_values_are_pulled_back()
        {
            var cfg = InMemoryConfig.Create(EconomyConfig.Defaults().Values, new FakeRemoteConfigSource(
                new RemoteConfigPayload("refill", 1, new Dictionary<string, string>
                {
                    [GameConfigKeys.LivesRefillCoinCost.Value] = "0",
                    [GameConfigKeys.LivesRefillCoinAmount.Value] = "-2",
                    [GameConfigKeys.LivesRefillAdAmount.Value] = "3",
                })));
            cfg.RefreshRemoteAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            var t = EconomyConfig.Read(cfg);
            Assert.That((t.LivesRefillCoinCost, t.LivesRefillCoinAmount, t.LivesRefillAdAmount), Is.EqualTo((1, 1, 3)));
        }
    }
}
