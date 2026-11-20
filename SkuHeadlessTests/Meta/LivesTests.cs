using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>CR-012 E (features/lives.md, D-039): 5 lives, one back every 20 minutes, taken at the start of an attempt and
    /// given back on a win; a clock that goes back restarts the count.</summary>
    public class LivesTests
    {
        TestStore store; EconomyTuning tuning; LevelProgressService svc;
        DateTimeOffset t0 = new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, new TestWallet(), tuning, new TestFacts());
        }

        DateTimeOffset At(double minutes) => t0.AddMinutes(minutes);

        [Test]
        public void A_new_player_has_five_and_no_timer()
        {
            Assert.That(svc.Lives(t0), Is.EqualTo(5));
            Assert.That(svc.NextLifeIn(t0), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void Leaving_an_attempt_costs_one_and_the_refill_brings_it_back()
        {
            Assert.That(svc.TrySpendLife(t0), Is.True);
            Assert.That(svc.Lives(t0), Is.EqualTo(4));
            Assert.That(svc.NextLifeIn(t0), Is.EqualTo(TimeSpan.FromMinutes(20)));
            Assert.That(svc.Lives(At(20)), Is.EqualTo(5));
            Assert.That(store.Progress.LivesRefillFromUtc, Is.EqualTo(0), "full again: the count stops");
        }

        [Test]
        public void A_life_comes_back_every_twenty_minutes_until_full()
        {
            for (int i = 0; i < 5; i++) svc.TrySpendLife(t0);
            Assert.That(svc.Lives(t0), Is.EqualTo(0));
            Assert.That(svc.Lives(At(19)), Is.EqualTo(0));
            Assert.That(svc.Lives(At(20)), Is.EqualTo(1));
            Assert.That(svc.NextLifeIn(At(25)), Is.EqualTo(TimeSpan.FromMinutes(15)));
            Assert.That(svc.Lives(At(61)), Is.EqualTo(3), "several periods at once");
            Assert.That(svc.Lives(At(500)), Is.EqualTo(5));
            Assert.That(svc.NextLifeIn(At(500)), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void Spending_while_already_counting_keeps_the_running_timer()
        {
            svc.TrySpendLife(t0);
            svc.TrySpendLife(At(10));
            Assert.That(svc.NextLifeIn(At(10)), Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [Test]
        public void With_no_life_an_attempt_cannot_start_until_an_ad_gives_one()
        {
            for (int i = 0; i < 5; i++) svc.TrySpendLife(t0);
            Assert.That(svc.TrySpendLife(t0), Is.False);
            Assert.That(svc.GrantLifeFromAd(t0), Is.True);
            Assert.That(svc.TrySpendLife(t0), Is.True);
            Assert.That(svc.Lives(t0), Is.EqualTo(0));
        }

        [Test]
        public void A_clock_set_back_restarts_the_count_without_adding()
        {
            svc.TrySpendLife(t0);
            Assert.That(svc.Lives(At(-60)), Is.EqualTo(4));
            Assert.That(svc.NextLifeIn(At(-60)), Is.EqualTo(TimeSpan.FromMinutes(20)), "the mark moved to the new now");
        }

        [Test]
        public void A_failed_save_takes_nothing()
        {
            store.Fail = true;
            Assert.That(svc.TrySpendLife(t0), Is.False);
            store.Fail = false;
            Assert.That(svc.Lives(t0), Is.EqualTo(5));
            Assert.That(store.Progress.LivesRefillFromUtc, Is.EqualTo(0));
        }
    }
}
