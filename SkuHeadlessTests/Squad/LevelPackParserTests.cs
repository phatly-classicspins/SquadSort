using System;
using System.Linq;
using Game.Domain;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Squad
{
    /// <summary>How the JSON level pack maps onto the board: grid, tube direction, pet order, hidden, houses, spare.</summary>
    public sealed class LevelPackParserTests
    {
        private const string Pack = @"{
  ""colors"": [ { ""index"": 0, ""name"": ""Red"", ""hex"": ""#e53836"" } ],
  ""levels"": [ {
    ""name"": ""Level001"", ""levelId"": ""c0001"", ""difficulty"": 1, ""levelDuration"": 0, ""version"": 3,
    ""waitingCells"": [ [-2, 0], [-1, 0], [0, 0], [1, 0], [-2, 1], [-1, 1], [0, 1], [1, 1] ],
    ""holes"": [],
    ""containers"": [
      { ""side"": 0, ""cell"": 0, ""x"": -1, ""y"": -1, ""offset"": 0, ""offsetSide"": 0, ""pets"": [3, 6, 3], ""hidden"": [1, 0, 0],
        ""keyColors"": [], ""keyColorIndex"": -1, ""keyPetIndex"": -1, ""features"": [] },
      { ""side"": 1, ""cell"": 0, ""x"": 0, ""y"": 2, ""offset"": 0, ""offsetSide"": 0, ""pets"": [6, 6, 6, 3],  ""hidden"": [0, 0, 0, 0],
        ""keyColors"": [-1], ""keyColorIndex"": -1, ""keyPetIndex"": -1,
        ""features"": [ { ""type"": ""CurtainFeatureData"", ""UnlockColorIndex"": 8 } ] },
      { ""side"": 1, ""cell"": 0, ""x"": 1, ""y"": 2, ""offset"": 0, ""offsetSide"": 0, ""pets"": [3], ""hidden"": [0],
        ""keyColors"": [], ""keyColorIndex"": -1, ""keyPetIndex"": -1, ""features"": [ { ""type"": ""BarrierFeatureData"" } ] }
    ] } ]
}";

        private static LevelDefinition Level => LevelPackParser.Parse(Pack).Levels.Single();

        [Test]
        public void The_plaza_is_shifted_to_start_at_the_origin()
            => Assert.That(Level.PlazaCells.Min(p => p.X) == 0 && Level.PlazaCells.Min(p => p.Y) == 0, Is.True);

        [Test]
        public void Side_0_sits_above_the_plaza_and_side_1_below()
        {
            Assert.That(Level.Tubes[0].Mouth, Is.EqualTo(new GridPos(1, -1)));
            Assert.That(Level.Tubes[0].Direction, Is.EqualTo(TubeDirection.Up));
            Assert.That(Level.Tubes[1].Mouth, Is.EqualTo(new GridPos(2, 2)));
            Assert.That(Level.Tubes[1].Direction, Is.EqualTo(TubeDirection.Down));
        }

        [Test]
        public void Pets_run_closed_end_to_mouth_with_their_hidden_flags_and_mapped_colours()
        {
            var c = Level.Tubes[0].Contents;
            Assert.That(c.Select(b => b.Color), Is.EqualTo(new[] { SquadColor.Purple, SquadColor.Gray, SquadColor.Purple }));
            Assert.That(c.Select(b => b.Hidden), Is.EqualTo(new[] { true, false, false }));
            Assert.That(Level.Tubes.All(t => t.Capacity == Board.SquadSize), Is.True);
        }

        [Test]
        public void A_curtain_is_a_house_of_its_unlock_colour_and_a_barrier_plays_as_a_plain_tube()
        {
            Assert.That(Level.Tubes[1].HouseColor, Is.EqualTo(SquadColor.Yellow));
            Assert.That(Level.Tubes[2].HouseColor, Is.Null);
            Assert.That(LevelPackParser.Parse(Pack).IgnoredBarriers, Is.EqualTo(1));
        }

        [Test]
        public void Difficulty_and_name_come_through()
        {
            Assert.That(Level.Difficulty, Is.EqualTo(LevelDifficulty.Hard));
            Assert.That(Level.Name, Is.EqualTo("Level001"));
        }

        [Test]
        public void The_spare_tube_gets_a_free_slot_beside_the_plaza()
        {
            Assert.That(Level.SpareMouth, Is.Not.Null);
            var board = new Board(Level);
            Tube spare = board.Tubes[board.AddSpareTube()];
            var used = Level.PlazaCells.Concat(board.Tubes.Where(t => t != spare).SelectMany(t => Enumerable.Range(0, t.Capacity).Select(t.CellOf))).ToHashSet();
            Assert.That(Enumerable.Range(0, spare.Capacity).Select(spare.CellOf).Any(used.Contains), Is.False);
        }

        [Test]
        public void Every_pack_colour_index_maps_to_a_distinct_game_colour()
            => Assert.That(Enumerable.Range(0, 13).Select(LevelPackParser.ColorOf).Distinct().Count(), Is.EqualTo(13));

        [Test]
        public void An_incomplete_squad_is_rejected()
            => Assert.Throws<FormatException>(() => LevelPackParser.Parse(Pack.Replace("[6, 6, 6, 3]", "[6, 6, 3]")));
    }
}
