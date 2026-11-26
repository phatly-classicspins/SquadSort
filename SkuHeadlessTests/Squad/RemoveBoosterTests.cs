using System.Linq;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>The Remove booster (Squad Sort GDD §1.5): swap other colours out, fill to a squad, clear.</summary>
    public sealed class RemoveBoosterTests
    {
        private static Board Load(string text) => new Board(LevelParser.Parse(text, requireFullSquads: false));

        private static string Colors(Tube t) => string.Concat(t.Slots.Select(b => (b.Hidden ? "?" : string.Empty) + SquadColorCodes.ToLetter(b.Color)));

        private static string Floor(Board board) =>
            string.Concat(board.Plaza.Select(c => c.IsFree ? "." : SquadColorCodes.ToLetter(c.Occupant.Color).ToString()));

        [Test]
        public void A_front_buddy_fills_its_own_container_by_swap_then_fill_and_the_squad_clears()
        {
            Board board = Load(@"
plaza:
R#
tube 0 -1 up 4 : B R R
tube 1 -1 up 4 : R G");
            int front = board.Tubes[0].Top.Id;

            MoveResult r = board.Remove(front);

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 0 }), "the target fills with four reds and clears");
            Assert.That(Floor(board), Is.EqualTo("B."), "the blue took the floor red's place");
            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("G"), "the red under green was pulled in; the green closes the gap");
            Assert.That(r.Moves.Count, Is.EqualTo(4), "blue out, floor red in, the tube red in, green settles");
        }

        [Test]
        public void A_floor_buddy_picks_the_container_that_already_shows_the_most_of_its_colour()
        {
            Board board = Load(@"
plaza:
R#
tube 0 -1 up 4 : R G
tube 1 -1 up 4 : G R R
tube 2 -1 up 4 :");
            int floorRed = board.Plaza[0].Occupant.Id;

            MoveResult r = board.Remove(floorRed);

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 1 }));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("G"));
            Assert.That(Floor(board), Is.EqualTo("G."), "tube 1's green swapped out onto the floor red's tile");
        }

        [Test]
        public void A_floor_buddy_whose_colour_no_container_shows_is_refused()
        {
            Board board = Load(@"
plaza:
RRRR
tube 0 -1 up 4 : G
tube 1 -1 up 4 :");

            MoveResult r = board.Remove(board.Plaza[0].Occupant.Id);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.NoDestination));
            Assert.That(Floor(board), Is.EqualTo("RRRR"));
        }

        [Test]
        public void Buddies_locked_in_a_house_are_never_taken_so_too_few_is_refused()
        {
            Board board = Load(@"
plaza:
##
tube 0 -1 up 4 : R R R
house B 1 -1 up 4 : R");

            MoveResult r = board.Remove(board.Tubes[0].Top.Id);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.NotTappable));
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("RRR"));
        }

        [Test]
        public void A_hidden_buddy_of_the_colour_is_taken_and_revealed_one_of_another_colour_stays_hidden()
        {
            Board board = Load(@"
plaza:
##
tube 0 -1 up 4 : ?G R R R
tube 1 -1 up 4 : ?R Y");

            MoveResult r = board.Remove(board.Tubes[0].Top.Id);

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 0 }));
            Assert.That(Colors(board.Tubes[1]), Is.EqualTo("?GY"), "the hidden green took the hidden red's place, still hidden");
        }

        [Test]
        public void Only_the_front_group_can_be_tapped()
        {
            Board board = Load(@"
plaza:
##
tube 0 -1 up 4 : R G
tube 1 -1 up 4 : R R R");

            MoveResult r = board.Remove(board.Tubes[0].Slots[0].Id);

            Assert.That(r.Reject, Is.EqualTo(RejectReason.NotTappable));
        }

        [Test]
        public void Remove_works_on_a_board_that_ran_out_of_space()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : R B R
tube 1 -1 up 4 : Y Y R R");
            board.TapTube(0);   // tube 1 is full: the front red takes the only tile, and no floor buddy fits a container
            Assume.That(board.State, Is.EqualTo(BoardState.OutOfSpace));

            MoveResult r = board.Remove(board.Plaza[0].Occupant.Id);

            Assert.That(r.Accepted, Is.True);
            Assert.That(r.ClearedTubes, Is.EqualTo(new[] { 1 }), "tube 1 shows the most reds: its yellows swap out");
            Assert.That(Colors(board.Tubes[0]), Is.EqualTo("YB"), "a yellow took the bottom red's place");
            Assert.That(Floor(board), Is.EqualTo("Y"));
        }
    }
}
