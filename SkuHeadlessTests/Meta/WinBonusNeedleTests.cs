using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-076 (CR-013 H4, Money Sort bonus wheel): the win bonus needle and the multipliers it reads.</summary>
    public class WinBonusNeedleTests
    {
        private static readonly int[] Segments = { 2, 3, 5, 3, 2 };

        private sealed class StartAt : IRandom
        {
            private readonly float _f;
            public StartAt(float f) { _f = f; }
            public long Seed => 0;
            public int NextInt(int minInc, int maxExc) => minInc;
            public float NextFloat() => _f;
            public void Shuffle<T>(IList<T> list) { }
        }

        private static WinBonusNeedle Needle(float start = 0f) => new WinBonusNeedle(Segments, 84f, 0.8f, new StartAt(start));

        [Test]
        public void It_starts_where_the_random_stream_says()
        {
            Assert.That(Needle(0f).Angle, Is.EqualTo(84f).Within(1e-3), "phase 0 = the far left");
            Assert.That(Needle(0f).Segment, Is.EqualTo(0));
            Assert.That(Needle(0.5f).Angle, Is.EqualTo(-84f).Within(1e-3), "half a swing = the far right");
            Assert.That(Needle(0.5f).Segment, Is.EqualTo(4));
            Assert.That(Needle(0.25f).Multiplier, Is.EqualTo(5), "a quarter swing = straight up, on ×5");
        }

        [Test]
        public void It_swings_by_the_dt_it_is_given_in_fixed_steps()
        {
            var a = Needle();
            var b = Needle();
            for (int i = 0; i < 60; i++) a.Advance(1f / 60f);
            b.Advance(1f);   // one long frame moves it as far as sixty short ones
            Assert.That(a.Angle, Is.EqualTo(b.Angle).Within(1e-3));
            Assert.That(a.Angle, Is.Not.EqualTo(84f).Within(1f), "it moved");
            var c = Needle();
            c.Advance(1f / 0.8f);   // one full swing brings it back
            Assert.That(c.Angle, Is.EqualTo(84f).Within(0.5f));
        }

        [Test]
        public void Stopping_holds_the_pick_until_resumed()
        {
            var n = Needle(0.25f);
            Assert.That(n.Stop(), Is.EqualTo(5));
            n.Advance(0.3f);
            Assert.That(n.Multiplier, Is.EqualTo(5), "a stopped needle does not move");
            Assert.That(n.BonusFor(40), Is.EqualTo(160), "×5 on 40 adds 160 on top of the 40 already paid");
            n.Resume();
            n.Advance(0.3f);
            Assert.That(n.Multiplier, Is.Not.EqualTo(5));
        }

        [Test]
        public void Every_segment_can_be_hit()
        {
            var n = Needle();
            var seen = new HashSet<int>();
            for (int i = 0; i < 120; i++) { n.Advance(1f / 60f); seen.Add(n.Segment); }
            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void The_multipliers_come_from_config_and_a_bad_value_never_takes_the_bonus_away()
        {
            Assert.That(new EconomyTuning().WinBonusSegments(), Is.EqualTo(Segments));
            Assert.That(new EconomyTuning { WinBonusMultipliers = " 2, 4 ,x,0,10" }.WinBonusSegments(), Is.EqualTo(new[] { 2, 4, 10 }));
            Assert.That(new EconomyTuning { WinBonusMultipliers = "lots", WinAdMultiplier = 3 }.WinBonusSegments(), Is.EqualTo(new[] { 3 }));
            Assert.That(new EconomyTuning { WinBonusMultipliers = null }.WinBonusSegments(), Is.EqualTo(new[] { 2 }));
        }
    }
}
