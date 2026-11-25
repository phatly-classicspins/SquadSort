using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>The ways out of a full plaza: Add Space (one more cell) and Add Container (the spare tube).</summary>
    public sealed class RescueTests
    {
        private static Board Load(string text) => new Board(LevelParser.Parse(text, requireFullSquads: false));

        [Test]
        public void Add_space_takes_a_free_cell_below_the_plaza_first()
        {
            Board board = Load(@"
plaza:
###
tube 1 -1 up 4 : R");

            GridPos? cell = board.NextPlazaCell();

            Assert.That(cell, Is.EqualTo(new GridPos(1, 1)), "row below the plaza, middle column");
        }

        [Test]
        public void Add_space_never_takes_a_tube_or_the_spare_tube_cell()
        {
            Board board = Load(@"
plaza:
###
tube 1 1 down 4 : R
spare 0 1 down");

            GridPos? cell = board.NextPlazaCell();

            Assert.That(cell, Is.EqualTo(new GridPos(2, 1)));
        }

        [Test]
        public void Adding_the_cell_resumes_a_full_plaza()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : B R");
            board.TapTube(0);   // the red has no tube: it steps onto the only floor tile, the floor is full and blue cannot follow
            Assume.That(board.State, Is.EqualTo(BoardState.OutOfSpace));

            board.AddPlazaCell(board.NextPlazaCell().Value);

            Assert.That(board.State, Is.EqualTo(BoardState.Playing));
        }

        [Test]
        public void The_spare_tube_resumes_a_full_plaza()
        {
            Board board = Load(@"
plaza:
#
tube 0 -1 up 4 : B R
spare 1 0 right");
            board.TapTube(0);
            Assume.That(board.State, Is.EqualTo(BoardState.OutOfSpace));

            Assert.That(board.AddSpareTube(), Is.GreaterThanOrEqualTo(0));
            Assert.That(board.State, Is.EqualTo(BoardState.Playing));
        }
    }
}
