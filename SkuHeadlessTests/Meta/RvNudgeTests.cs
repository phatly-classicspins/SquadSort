using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-138, GDD 1.3 "Low slot warning": the red blink and the RV Slot bubble's repeat rules (defaults: 2 free, 30 s
    /// cooldown, 2 per attempt, 1 after 3 ignored attempts).</summary>
    public class RvNudgeTests
    {
        static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;

        static (RvNudge nudge, TestStore store) New()
        {
            var store = new TestStore();
            var n = new RvNudge(store, new EconomyTuning());
            n.StartAttempt();
            return (n, store);
        }

        [Test]
        public void Blink_runs_at_two_free_or_fewer()
        {
            var (n, _) = New();
            Assert.That(n.Warn(3), Is.False);
            Assert.That(n.Warn(2), Is.True);
            Assert.That(n.Warn(0), Is.True);
        }

        [Test]
        public void Bubble_shows_once_per_drop()
        {
            var (n, _) = New();
            Assert.That(n.Poll(4, true, false, T0), Is.False);
            Assert.That(n.Poll(2, true, false, T0), Is.True, "the drop to 2");
            Assert.That(n.Poll(1, true, false, T0.AddSeconds(60)), Is.False, "still low: no second bubble");
        }

        [Test]
        public void A_new_drop_needs_the_cooldown()
        {
            var (n, _) = New();
            Assert.That(n.Poll(2, true, false, T0), Is.True);
            n.Poll(3, true, false, T0.AddSeconds(5));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(10)), Is.False, "inside 30 s: this drop passes");
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(40)), Is.False, "the passed drop does not come back while low");
            n.Poll(5, true, false, T0.AddSeconds(41));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(42)), Is.True, "above, then down again after 30 s");
        }

        [Test]
        public void At_most_two_per_attempt_and_a_restart_is_a_new_attempt()
        {
            var (n, _) = New();
            Assert.That(n.Poll(2, true, false, T0), Is.True);
            n.Poll(3, true, false, T0.AddSeconds(31));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(31)), Is.True);
            n.Poll(3, true, false, T0.AddSeconds(70));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(70)), Is.False, "third in one attempt");
            n.RvOpened();
            n.StartAttempt();
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(110)), Is.True, "a restart counts again");
        }

        [Test]
        public void A_blocked_drop_waits_and_shows_when_free_to()
        {
            var (n, _) = New();
            Assert.That(n.Poll(2, true, true, T0), Is.False, "a booster or chips in flight");
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(1)), Is.True, "still low when it ends");
        }

        [Test]
        public void A_blocked_drop_that_recovers_shows_nothing()
        {
            var (n, _) = New();
            n.Poll(2, true, true, T0);
            Assert.That(n.Poll(3, true, false, T0.AddSeconds(1)), Is.False);
        }

        [Test]
        public void No_bubble_without_an_rv_slot_left()
        {
            var (n, _) = New();
            Assert.That(n.Poll(2, false, false, T0), Is.False);
        }

        [Test]
        public void Three_ignored_attempts_leave_one_bubble_until_an_rv_slot_opens()
        {
            var (n, store) = New();
            for (int a = 0; a < 3; a++)
            {
                Assert.That(n.Poll(2, true, false, T0.AddSeconds(100 * a)), Is.True);
                n.Poll(3, true, false, T0.AddSeconds(100 * a + 1));
                n.StartAttempt();
            }
            Assert.That(store.Progress.RvNudgeIgnoreStreak, Is.EqualTo(3));
            Assert.That(n.MaxThisAttempt, Is.EqualTo(1));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(400)), Is.True);
            n.Poll(3, true, false, T0.AddSeconds(440));
            Assert.That(n.Poll(2, true, false, T0.AddSeconds(440)), Is.False, "only 1 now");
            n.RvOpened();
            Assert.That(store.Progress.RvNudgeIgnoreStreak, Is.Zero);
            Assert.That(n.MaxThisAttempt, Is.EqualTo(2));
        }
    }
}
