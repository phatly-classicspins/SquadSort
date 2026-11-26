using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    public sealed class LevelSessionTests
    {
        private static LevelSession Session(string text, int undo = 3, int shuffle = 3, int add = 3)
            => new LevelSession(
                1,
                LevelParser.Parse(text, requireFullSquads: false),
                new Pcg32(7),
                new Dictionary<Booster, int> { [Booster.Undo] = undo, [Booster.Shuffle] = shuffle, [Booster.Remove] = add });

        private const string TwoTubes = @"
plaza:
####
tube 0 -1 up 4 : R G G
tube 1 -1 up 4 : B Y
spare 2 -1 up";

        [Test]
        public void UndoRestoresTheBoardAndSpendsACharge()
        {
            LevelSession s = Session(TwoTubes);
            s.TapTube(0);

            Assert.That(s.Undo(), Is.True);
            Assert.That(s.Board.Tubes[0].Count, Is.EqualTo(3));
            Assert.That(s.Charges(Booster.Undo), Is.EqualTo(2));
            Assert.That(s.MovesMade, Is.EqualTo(0));
        }

        [Test]
        public void UndoWithNothingToUndoCostsNothing()
        {
            LevelSession s = Session(TwoTubes);

            Assert.That(s.Undo(), Is.False);
            Assert.That(s.Charges(Booster.Undo), Is.EqualTo(3));
        }

        [Test]
        public void UndoWithNoChargesIsRefused()
        {
            LevelSession s = Session(TwoTubes, undo: 0);
            s.TapTube(0);

            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.Undo(), Is.False);
        }

        [Test]
        public void RejectedTapIsNotRecorded()
        {
            LevelSession s = Session(TwoTubes);
            s.TapTube(5);

            Assert.That(s.MovesMade, Is.EqualTo(0));
            Assert.That(s.CanUndo, Is.False);
        }

        [Test]
        public void ShuffleSpendsAChargeOnlyWhenItHappens()
        {
            LevelSession s = Session(TwoTubes);

            Assert.That(s.Shuffle(9).Accepted, Is.False);
            Assert.That(s.Charges(Booster.Shuffle), Is.EqualTo(3));
            Assert.That(s.Shuffle(0).Accepted, Is.True);
            Assert.That(s.Charges(Booster.Shuffle), Is.EqualTo(2));
        }

        [Test]
        public void TheSpareTubeComesOncePerLevelAndSpendsNoBooster()
        {
            LevelSession s = Session(TwoTubes);

            Assert.That(s.AddSpareTube(), Is.EqualTo(2));
            Assert.That(s.AddSpareTube(), Is.EqualTo(-1));
            Assert.That(s.Charges(Booster.Remove), Is.EqualTo(3));
        }

        [Test]
        public void RemoveSpendsAChargeOnlyWhenItHappensAndUndoTakesItBack()
        {
            LevelSession s = Session(@"
plaza:
GG##
tube 0 -1 up 4 : R G
tube 1 -1 up 4 : G R R
tube 2 -1 up 4 : R", add: 1);

            Assert.That(s.Remove(s.Board.Tubes[0].Slots[0].Id).Accepted, Is.False, "a buried buddy cannot be tapped");
            Assert.That(s.Charges(Booster.Remove), Is.EqualTo(1));
            MoveResult r = s.Remove(s.Board.Tubes[1].Top.Id);
            Assert.That(r.Accepted, Is.True);
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 1 }));
            Assert.That(s.Charges(Booster.Remove), Is.EqualTo(0));
            Assert.That(s.Remove(s.Board.Tubes[0].Top.Id), Is.Null, "no charge left");
            Assert.That(s.Undo(), Is.True);
            Assert.That(s.Board.Tubes[1].Cleared, Is.False);
        }
    }
}
