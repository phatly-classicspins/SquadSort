using System;
using System.Linq;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    public sealed class BoardWalkRouteTests
    {
        private static Board Load(string text) => new Board(LevelParser.Parse(text, requireFullSquads: false));

        [Test]
        public void AdjacentTubesMustExitThroughMouthsAndUseWaitingSlots()
        {
            var board = Load("plaza:\n####\n\ntube 0 -1 up 4 : R\ntube 1 -1 up 4 : B");
            var route = BoardWalkRoute.Build(board, BuddyLocation.InTube(0, 0), BuddyLocation.InTube(1, 1));
            Assert.That(route.First(), Is.EqualTo(new GridPos(0, -4)));
            Assert.That(route.Last(), Is.EqualTo(new GridPos(1, -3)));
            Assert.That(route, Does.Contain(new GridPos(0, 0)));
            Assert.That(route, Does.Contain(new GridPos(1, 0)));
            Assert.That(route.Count, Is.EqualTo(9));
            AssertOrthogonal(route.ToArray());
        }

        [Test]
        public void WaitingRouteTurnsAroundMissingCellsInsteadOfCuttingAcrossTheHole()
        {
            var board = Load("plaza:\n#.#\n###\n\ntube 0 -1 up 4 : R");
            var route = BoardWalkRoute.Build(board, BuddyLocation.OnPlaza(0), BuddyLocation.OnPlaza(1));
            Assert.That(route, Is.EqualTo(new[] {new GridPos(0,0),new GridPos(0,1),new GridPos(1,1),new GridPos(2,1),new GridPos(2,0)}));
            AssertOrthogonal(route.ToArray());
        }

        [Test]
        public void SameTubeShuffleStaysInsideItsLane()
        {
            var board = Load("plaza:\n#\n\ntube 0 -1 up 4 : R B G");
            var route = BoardWalkRoute.Build(board, BuddyLocation.InTube(0, 0), BuddyLocation.InTube(0, 2));
            Assert.That(route, Is.EqualTo(new[] {new GridPos(0,-4),new GridPos(0,-3),new GridPos(0,-2)}));
        }

        [Test]
        public void RouteStillExistsAfterDestinationSquadHasCleared()
        {
            var board = Load("plaza:\n####\n\ntube 0 -1 up 4 : R R R\ntube 1 -1 up 4 : R");
            var result = board.TapTube(0);
            Assert.That(result.ClearedTubes, Does.Contain(1));
            foreach (var move in result.Moves)
            {
                var route = BoardWalkRoute.Build(board, move.From, move.To);
                Assert.That(route, Does.Contain(new GridPos(0,0)));
                Assert.That(route, Does.Contain(new GridPos(1,0)));
                AssertOrthogonal(route.ToArray());
            }
        }

        [Test]
        public void DisconnectedWaitingCellsAreReportedInsteadOfInventingADiagonal()
        {
            var board = Load("plaza:\n#.#\n\ntube 0 -1 up 4 : R");
            Assert.Throws<InvalidOperationException>(() => BoardWalkRoute.Build(board, BuddyLocation.OnPlaza(0), BuddyLocation.OnPlaza(1)));
        }

        private static void AssertOrthogonal(GridPos[] path)
        {
            for (int i = 1; i < path.Length; i++) Assert.That(path[i-1].ManhattanTo(path[i]), Is.EqualTo(1));
        }
    }
}
