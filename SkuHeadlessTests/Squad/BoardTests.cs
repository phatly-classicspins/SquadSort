using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>The rules of GDD §2.2b, one behaviour per test.</summary>
    public sealed class BoardTests
    {
        private static Board Load(string text, bool outOfSpace = true) => new Board(LevelParser.Parse(text, requireFullSquads: false), outOfSpace);

        private static string Colors(Tube t) => string.Concat(t.Slots.Select(b => (b.Hidden ? "?" : string.Empty) + SquadColorCodes.ToLetter(b.Color)));

        [Test]
        public void TapMovesTheWholeFrontGroupIntoTheMatchingTube()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : R R R
tube 1 -1 up 4 : R");

            MoveResult r = board.TapTube(0);

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.Moves.Count, Is.EqualTo(3));
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 1 }));
            Assert.That(r.State, Is.EqualTo(BoardState.Won));
        }

        [Test]
        public void GroupStopsAtTheFirstDifferentColour()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : G R R
tube 1 -1 up 4 : R G G G");

            board.TapTube(0);

            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("G"));
            Assert.That(board.Tubes.Count(t => t.Cleared), Is.EqualTo(0));
            Assert.That(board.Plaza.Count(c => !c.IsFree), Is.EqualTo(2), "no tube takes red, so both reds go to the plaza");
        }

        [Test]
        public void GroupWithNoTubeGoesToThePlazaCellNearestTheMouth()
        {
            Board board = Load(@"
plaza:
#####
tube 4 -1 up 4 : G G Y Y
tube 0 -1 up 4 : B B B B");

            board.TapTube(0);

            Assert.That(board.Plaza[4].Occupant?.Color, Is.EqualTo(SquadColor.Yellow));
            Assert.That(board.Plaza[3].Occupant?.Color, Is.EqualTo(SquadColor.Yellow));
            Assert.That(board.Plaza[0].IsFree, Is.True);
        }

        [Test]
        public void OnlyWhatFitsMovesAndTheRestStays()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : B Y Y Y
tube 1 -1 up 4 : G G G Y
tube 2 -1 up 4 : B B B G");

            board.TapTube(0); // three yellows, but tube 1 has no room → plaza
            Assert.That(board.PlazaFree, Is.EqualTo(1));

            Board b2 = Load(@"
plaza:
####
tube 0 -1 up 4 : Y Y
tube 1 -1 up 4 : B B Y");
            b2.TapTube(0);
            Assert.That(Colors(b2.Tubes[1]), Is.EqualTo("BBYY"));
            Assert.That(Colors(b2.Tubes[0]), Is.EqualTo("Y"), "partial move: one yellow stays behind");
        }

        [Test]
        public void EmptyTubeTakesAGroupWhenNoTubeMatches()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : G P P
tube 1 -1 up 4 :");

            board.TapTube(0);

            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("PP"));
            Assert.That(board.PlazaFree, Is.EqualTo(4));
        }

        [Test]
        public void MatchingTubeBeatsEmptyTube()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : P
tube 1 -1 up 4 :
tube 2 -1 up 4 : P P P");

            board.TapTube(0);

            Assert.That(board.Tubes[2].Cleared, Is.True);
            Assert.That(board.Tubes[1].IsEmpty, Is.True);
        }

        [Test]
        public void TappingAPlazaBuddyBringsEveryPlazaBuddyOfThatColour()
        {
            Board board = Load(@"
plaza:
R..R
#..#
tube 1 -1 up 4 : R R");

            MoveResult r = board.TapPlaza(0);

            Assert.That(r.Moves.Count, Is.EqualTo(2));
            Assert.That(board.Tubes[0].Cleared, Is.True);
        }

        [Test]
        public void PlazaBuddyWithNoTubeIsRejected()
        {
            Board board = Load(@"
plaza:
R###
tube 1 -1 up 4 : G G G G
tube 2 -1 up 4 : R R R B");

            MoveResult r = board.TapPlaza(0);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.NoDestination));
        }

        [Test]
        public void ArrivingGroupPullsSameColourBuddiesOffThePlaza()
        {
            Board board = Load(@"
plaza:
R.R#
tube 0 -1 up 4 : G R R
tube 1 -1 up 4 :
tube 2 -1 up 4 : G G G");

            board.TapTube(0); // red → empty tube 1, then both plaza reds follow

            Assert.That(board.Tubes[1].Cleared, Is.True);
            Assert.That(board.PlazaFree, Is.EqualTo(3), "'R.R#' is three cells, all empty again");
        }

        [Test]
        public void HiddenBuddyRevealsWhenItBecomesTheFront()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : ?G ?B R");

            Assert.That(board.Tubes[0].Slots[1].Hidden, Is.True);
            MoveResult r = board.TapTube(0);

            Assert.That(r.RevealedBuddies.Count, Is.EqualTo(1));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("?GB"));
        }

        [Test]
        public void HiddenFrontIsNeverPartOfAGroup()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : ?R R");

            board.TapTube(0);

            Assert.That(board.Tubes[0].Count, Is.EqualTo(1), "only the revealed red moves");
            Assert.That(board.Tubes[0].Top.Hidden, Is.False, "the hidden one is now the front and reveals");
        }

        [Test]
        public void HouseStaysShutUntilItsColourCompletesThenOpens()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : Y Y Y
tube 1 -1 up 4 : Y
house Y 2 -1 up 4 : G G ?P");

            Assert.That(board.TapTube(2).Reject, Is.EqualTo(RejectReason.NotTappable));
            Assert.That(board.FindTubeFor(SquadColor.Green, null), Is.Not.SameAs(board.Tubes[2]));

            MoveResult r = board.TapTube(1);

            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 0 }));
            Assert.That(r.OpenedHouses, Is.EqualTo(new[] { 2 }));
            Assert.That(board.Tubes[2].Locked, Is.False);
            Assert.That(board.Tubes[2].Top.Hidden, Is.False, "house front reveals on opening");
        }

        [Test]
        public void EveryHouseOfTheColourOpensTogether()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : B B B B
tube 1 -1 up 4 :
house B 2 -1 up 4 : R
house B 3 -1 up 4 : R R R");

            // Already-complete squads are not auto-cleared at load; one tap away and back completes it.
            board.TapTube(0);
            board.TapTube(1);

            Assert.That(board.Tubes.Where(t => t.HouseColor == SquadColor.Blue).All(t => !t.Locked), Is.True);
        }

        [Test]
        public void FullPlazaStopsPlayUntilACellIsAdded()
        {
            Board board = Load(@"
plaza:
##
tube 0 -1 up 4 : B B G G
tube 1 -1 up 4 : G G Y Y");

            MoveResult r = board.TapTube(0);

            Assert.That(r.State, Is.EqualTo(BoardState.OutOfSpace));
            Assert.That(board.TapTube(1).Reject, Is.EqualTo(RejectReason.NotPlaying));

            board.AddPlazaCell(new GridPos(2, 0));

            Assert.That(board.State, Is.EqualTo(BoardState.Playing));
        }

        [Test]
        public void AFullFloorLosesEvenIfTwoContainersCouldPassAGroup()
        {
            Board board = Load(@"
plaza:
##
tube 0 -1 up 4 : B B G G
tube 1 -1 up 4 : G G B B");

            MoveResult r = board.TapTube(0);   // the greens fill the floor; tube 1's blues could join tube 0, but not across a full floor

            Assert.That(r.State, Is.EqualTo(BoardState.OutOfSpace));
        }

        [Test]
        public void ContainerToContainerNeedsAFreeFloorTile()
        {
            Board board = Load(@"
plaza:
RB
tube 0 -1 up 4 : R R
tube 1 -1 up 4 : R");

            MoveResult blocked = board.MoveTubeToTube(0, 1);

            Assert.That(blocked.Reject, Is.EqualTo(RejectReason.NoSpace));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("RR"));
            Assert.That(board.MovePlazaToTube(0, 1).Accepted, Is.True, "a floor buddy still walks into a container");
            Assert.That(board.MoveTubeToTube(0, 1).Accepted, Is.True, "with a tile free again, containers pass groups");
        }

        [Test]
        public void AFullPlazaIsNotALossWhileAPlazaBuddyCanReachATube()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : Y R
tube 1 -1 up 4 :");

            board.TapTube(1);                  // nothing to tap in an empty tube
            MoveResult r = board.TapTube(0);   // red goes to the empty tube, not the plaza

            Assert.That(r.State, Is.EqualTo(BoardState.Playing));
        }

        [Test]
        public void NoSpaceIsRejectedWithoutChangingTheBoard()
        {
            Board board = Load(@"
plaza:
G
tube 1 -1 up 4 : B B B G
tube 2 -1 up 4 : G G Y Y", outOfSpace: false);

            board.TapTube(1); // G → tube… none matches (tube 2 front is Y) → plaza is full
            MoveResult r = board.TapTube(1);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.NoSpace));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("BBBG"));
        }

        [Test]
        public void SnapshotRestoreUndoesAMove()
        {
            Board board = Load(@"
plaza:
####
tube 0 -1 up 4 : R G G
tube 1 -1 up 4 : B");
            BoardSnapshot before = board.Snapshot();

            board.TapTube(0);
            board.Restore(before);

            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("RGG"));
            Assert.That(board.PlazaFree, Is.EqualTo(4));
        }

        [Test]
        public void ShuffleIsDeterministicForASeedAndKeepsTheBuddies()
        {
            const string level = @"
plaza:
####
tube 0 -1 up 4 : R G B Y";
            Board a = Load(level);
            Board b = Load(level);

            a.Shuffle(0, new Pcg32(42));
            b.Shuffle(0, new Pcg32(42));

            Assert.That(Colors(a.Tubes[0]), Is.EqualTo(Colors(b.Tubes[0])));
            Assert.That(Colors(a.Tubes[0]).OrderBy(c => c), Is.EqualTo("BGRY".ToCharArray()));
        }

        [Test]
        public void SpareTubeAppearsOnceAndTakesGroups()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : G G G R
tube 1 -1 up 4 : R R R G
spare 2 -1 up");

            int spare = board.AddSpareTube();
            board.TapTube(0);

            Assert.That(spare, Is.EqualTo(2));
            Assert.That(board.AddSpareTube(), Is.EqualTo(-1));
            Assert.That(Colors(board.Tubes[2]), Is.EqualTo("R"));
        }

        [Test]
        public void ParserRejectsAColourThatCannotFormSquads()
        {
            Assert.Throws<FormatException>(() => LevelParser.Parse(@"
plaza:
##
tube 0 -1 up 4 : R R R"));
        }

        [Test]
        public void TubeCellsRunFromMouthToClosedEnd()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : R R R R");

            Tube t = board.Tubes[0];

            Assert.That(t.CellOf(3), Is.EqualTo(new GridPos(0, -1)));
            Assert.That(t.CellOf(0), Is.EqualTo(new GridPos(0, -4)));
        }
    }
}
