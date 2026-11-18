using System;

namespace Game.Domain
{
    /// <summary>The colour of a buddy, a squad, or a house. Ten colours, as in the GDD §4.2a.</summary>
    public enum SquadColor : byte
    {
        Red,
        Orange,
        Yellow,
        Green,
        DarkGreen,
        Cyan,
        Blue,
        Purple,
        Pink,
        Brown,
    }

    /// <summary>One-letter codes used by the level text format.</summary>
    public static class SquadColorCodes
    {
        private const string Letters = "ROYGDCBPKN";

        public static bool TryParse(char letter, out SquadColor color)
        {
            int i = Letters.IndexOf(char.ToUpperInvariant(letter));
            color = i < 0 ? default : (SquadColor)i;
            return i >= 0;
        }

        public static char ToLetter(SquadColor color) => Letters[(int)color];
    }

    /// <summary>The direction from a tube's mouth (the end touching the plaza) toward its closed end.</summary>
    public enum TubeDirection : byte
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>A cell on the level grid. <c>Y</c> grows downward, matching the rows of the level text.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Step(TubeDirection dir, int count)
        {
            switch (dir)
            {
                case TubeDirection.Up: return new GridPos(X, Y - count);
                case TubeDirection.Down: return new GridPos(X, Y + count);
                case TubeDirection.Left: return new GridPos(X - count, Y);
                default: return new GridPos(X + count, Y);
            }
        }

        public int ManhattanTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>One character. <see cref="Id"/> is stable for the whole level so a View can follow it.</summary>
    public sealed class Buddy
    {
        public Buddy(int id, SquadColor color, bool hidden)
        {
            Id = id;
            Color = color;
            Hidden = hidden;
        }

        public int Id { get; }
        public SquadColor Color { get; }

        /// <summary>A hidden buddy shows "?" until it becomes the front of an unlocked tube (GDD §2.2b-11).</summary>
        public bool Hidden { get; internal set; }

        internal Buddy Clone() => new Buddy(Id, Color, Hidden);
    }

    public enum LocationKind : byte
    {
        Tube,
        Plaza,
    }

    /// <summary>Where a buddy stands: a tube slot (0 = closed end) or a plaza cell index.</summary>
    public readonly struct BuddyLocation : IEquatable<BuddyLocation>
    {
        public readonly LocationKind Kind;
        public readonly int Index;
        public readonly int Slot;

        private BuddyLocation(LocationKind kind, int index, int slot)
        {
            Kind = kind;
            Index = index;
            Slot = slot;
        }

        public static BuddyLocation InTube(int tube, int slot) => new BuddyLocation(LocationKind.Tube, tube, slot);

        public static BuddyLocation OnPlaza(int cell) => new BuddyLocation(LocationKind.Plaza, cell, 0);

        public bool Equals(BuddyLocation other) => Kind == other.Kind && Index == other.Index && Slot == other.Slot;

        public override bool Equals(object obj) => obj is BuddyLocation other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 7919) ^ (Index * 31) ^ Slot;

        public override string ToString() => Kind == LocationKind.Tube ? $"tube{Index}[{Slot}]" : $"plaza{Index}";
    }
}
