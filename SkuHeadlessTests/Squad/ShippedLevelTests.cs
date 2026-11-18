using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>Every shipped level parses, lays out without overlaps, and is winnable without boosters.</summary>
    public sealed class ShippedLevelTests
    {
        private static IEnumerable<string> LevelFiles()
        {
            string dir = RepoLayout.Path("Assets", "SqadSort", "Content", "Levels");
            return Directory.GetFiles(dir, "level_*.txt").OrderBy(f => f).Select(Path.GetFileName);
        }

        private static LevelDefinition Load(string file)
            => LevelParser.Parse(File.ReadAllText(RepoLayout.Path("Assets", "SqadSort", "Content", "Levels", file)));

        [Test]
        public void ThereAreLevels() => Assert.That(LevelFiles().Count(), Is.GreaterThanOrEqualTo(8));

        [TestCaseSource(nameof(LevelFiles))]
        public void LevelLaysOutWithoutOverlaps(string file)
        {
            LevelDefinition level = Load(file);
            var used = new HashSet<GridPos>(level.PlazaCells);
            var board = new Board(level);

            foreach (Tube t in board.Tubes)
            {
                for (int s = 0; s < t.Capacity; s++)
                    Assert.That(used.Add(t.CellOf(s)), Is.True, $"{file}: tube {t.Index} slot {s} at {t.CellOf(s)} overlaps");
                Assert.That(level.PlazaCells.Any(p => p.ManhattanTo(t.Mouth) == 1), Is.True, $"{file}: tube {t.Index} mouth {t.Mouth} does not touch the plaza");
            }

            if (level.SpareMouth.HasValue)
            {
                int spare = board.AddSpareTube();
                Tube s = board.Tubes[spare];
                for (int i = 0; i < s.Capacity; i++)
                    Assert.That(used.Add(s.CellOf(i)), Is.True, $"{file}: spare tube overlaps at {s.CellOf(i)}");
            }
        }

        [TestCaseSource(nameof(LevelFiles))]
        public void LevelIsSolvableWithoutBoosters(string file)
        {
            LevelSolver.Solution s = LevelSolver.Solve(Load(file));

            Assert.That(s.Exhausted, Is.False, $"{file}: search budget ran out after {s.StatesVisited} states");
            Assert.That(s.Solvable, Is.True, $"{file}: no solution ({s.StatesVisited} states)");
            TestContext.Out.WriteLine($"{file}: solved in {s.Moves} taps, {s.StatesVisited} states");
        }
    }
}
