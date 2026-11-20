using System.Linq;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>Select-then-drop control: the player names the group and where it goes.</summary>
    public sealed class SelectDropTests
    {
        private static Board Load(string text) => new Board(LevelParser.Parse(text, requireFullSquads: false));

        private static string Colors(Tube t) => string.Concat(t.Slots.Select(b => (b.Hidden ? "?" : string.Empty) + SquadColorCodes.ToLetter(b.Color)));

        private const string ThreeTubes = @"
plaza:
#####
tube 0 -1 up 4 : G R R
tube 1 -1 up 4 : B R
tube 2 -1 up 4 : ";

        [Test]
        public void GroupGoesToTheChosenTubeNotTheBestOne()
        {
            Board board = Load(ThreeTubes);

            MoveResult r = board.MoveTubeToTube(0, 2); // tube 1 also shows red, but the player chose the empty one

            Assert.That(r.Accepted, Is.True);
            Assert.That(Colors(board.Tubes[2]), Is.EqualTo("RR"));
            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("BR"));
        }

        [Test]
        public void ATubeShowingAnotherColourRefuses()
        {
            Board board = Load(@"
plaza:
#####
tube 0 -1 up 4 : R
tube 1 -1 up 4 : B");

            MoveResult r = board.MoveTubeToTube(0, 1);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.WrongDestination));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("R"));
        }

        [Test]
        public void DroppingOntoItsOwnTubeRefuses()
            => Assert.That(Load(ThreeTubes).MoveTubeToTube(0, 0).Reject, Is.EqualTo(RejectReason.WrongDestination));

        [Test]
        public void OnlyAsManyAsFitWalkIn()
        {
            Board board = Load(@"
plaza:
#####
tube 0 -1 up 4 : R R R
tube 1 -1 up 4 : G G R");

            board.MoveTubeToTube(0, 1);

            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("GGRR"));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("RR"));
        }

        [Test]
        public void GroupStepsOutOntoTheTappedPlazaCellFirst()
        {
            Board board = Load(ThreeTubes);

            MoveResult r = board.MoveTubeToPlaza(0, 4);

            Assert.That(r.Accepted, Is.True);
            Assert.That(board.Plaza[4].Occupant, Is.Not.Null);
            Assert.That(board.Plaza[3].Occupant, Is.Not.Null, "the second one stands next to the first");
            Assert.That(board.PlazaFree, Is.EqualTo(3));
        }

        [Test]
        public void PlazaBuddyGoesIntoTheChosenTubeAndItsColourFollows()
        {
            Board board = Load(@"
plaza:
R#R##
tube 0 -1 up 4 : B
tube 1 -1 up 4 : R");

            MoveResult r = board.MovePlazaToTube(2, 1);

            Assert.That(r.Accepted, Is.True);
            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("RRR"));
            Assert.That(board.PlazaFree, Is.EqualTo(5));
        }

        [Test]
        public void TheSelectedPlazaBuddyWalksInBeforeCloserOnes()
        {
            Board board = Load(@"
plaza:
R###R
tube 0 -1 up 2 : R");

            MoveResult r = board.MovePlazaToTube(4, 0); // the far one at x=4

            Assert.That(r.Moves[0].From, Is.EqualTo(BuddyLocation.OnPlaza(4)));
            Assert.That(board.Plaza[0].Occupant, Is.Not.Null, "no room left for the nearer one");
        }

        [Test]
        public void ALockedHouseRefuses()
        {
            Board board = Load(@"
plaza:
#####
tube 0 -1 up 4 : R
house B 1 -1 up 4 : ");

            Assert.That(board.MoveTubeToTube(0, 1).Reject, Is.EqualTo(RejectReason.WrongDestination));
        }

        [Test]
        public void AHiddenFrontCannotBePickedUp()
        {
            Board board = Load(@"
plaza:
#####
house B 0 -1 up 4 : R
tube 1 -1 up 4 : ");

            Assert.That(board.CanPickTube(0), Is.False);
            Assert.That(board.CanPickTube(1), Is.False, "nothing to pick in an empty tube");
        }
    }
}
