using System;
using System.Collections.Generic;

namespace Game.Domain
{
    /// <summary>A buddy as written in a level: its colour and whether it starts hidden.</summary>
    public readonly struct BuddySpec
    {
        public readonly SquadColor Color;
        public readonly bool Hidden;

        public BuddySpec(SquadColor color, bool hidden)
        {
            Color = color;
            Hidden = hidden;
        }
    }

    /// <summary>A tube as written in a level. <see cref="Contents"/> runs closed end → mouth.</summary>
    public sealed class TubeSpec
    {
        public TubeSpec(GridPos mouth, TubeDirection direction, int capacity, IReadOnlyList<BuddySpec> contents, SquadColor? houseColor)
        {
            Mouth = mouth;
            Direction = direction;
            Capacity = capacity;
            Contents = contents;
            HouseColor = houseColor;
        }

        /// <summary>The tube cell touching the plaza.</summary>
        public GridPos Mouth { get; }

        /// <summary>From the mouth toward the closed end.</summary>
        public TubeDirection Direction { get; }

        public int Capacity { get; }
        public IReadOnlyList<BuddySpec> Contents { get; }

        /// <summary>Non-null ⇒ the tube starts locked inside a house of this colour (GDD §2.2b-12).</summary>
        public SquadColor? HouseColor { get; }
    }

    public readonly struct PlazaBuddySpec
    {
        public readonly GridPos Cell;
        public readonly BuddySpec Buddy;

        public PlazaBuddySpec(GridPos cell, BuddySpec buddy)
        {
            Cell = cell;
            Buddy = buddy;
        }
    }

    /// <summary>
    /// A level, as data. Built by <see cref="LevelParser"/> from the level text, or directly by tests.
    /// </summary>
    public sealed class LevelDefinition
    {
        public LevelDefinition(
            string name,
            LevelDifficulty difficulty,
            IReadOnlyList<GridPos> plazaCells,
            IReadOnlyList<PlazaBuddySpec> plazaBuddies,
            IReadOnlyList<TubeSpec> tubes,
            GridPos? spareMouth,
            TubeDirection spareDirection)
        {
            Name = name ?? string.Empty;
            Difficulty = difficulty;
            PlazaCells = plazaCells ?? throw new ArgumentNullException(nameof(plazaCells));
            PlazaBuddies = plazaBuddies ?? Array.Empty<PlazaBuddySpec>();
            Tubes = tubes ?? throw new ArgumentNullException(nameof(tubes));
            SpareMouth = spareMouth;
            SpareDirection = spareDirection;
        }

        public string Name { get; }
        public LevelDifficulty Difficulty { get; }
        public IReadOnlyList<GridPos> PlazaCells { get; }
        public IReadOnlyList<PlazaBuddySpec> PlazaBuddies { get; }
        public IReadOnlyList<TubeSpec> Tubes { get; }

        /// <summary>Where the Add Container booster puts its tube; null ⇒ the booster is unavailable here.</summary>
        public GridPos? SpareMouth { get; }

        public TubeDirection SpareDirection { get; }
    }

    public enum LevelDifficulty : byte
    {
        Normal,
        Hard,
        SuperHard,
    }
}
