using System.Collections.Generic;

namespace Game.Domain
{
    /// <summary>
    /// A tube: a stack whose bottom (slot 0) is the closed end. Buddies settle toward the closed end, so
    /// the "front" group is always the top of the stack (GDD §2.2b-1/2).
    /// </summary>
    public sealed class Tube
    {
        private readonly List<Buddy> _slots;

        internal Tube(int index, GridPos mouth, TubeDirection direction, int capacity, SquadColor? houseColor, bool isSpare)
        {
            Index = index;
            Mouth = mouth;
            Direction = direction;
            Capacity = capacity;
            HouseColor = houseColor;
            Locked = houseColor.HasValue;
            IsSpare = isSpare;
            _slots = new List<Buddy>(capacity);
        }

        public int Index { get; }
        public GridPos Mouth { get; }
        public TubeDirection Direction { get; }
        public int Capacity { get; }

        /// <summary>The colour of the house this tube started in, or null for a plain tube.</summary>
        public SquadColor? HouseColor { get; }

        /// <summary>True while the house is shut: contents unseen, untappable, and nothing can enter.</summary>
        public bool Locked { get; internal set; }

        /// <summary>True once its squad completed and the tube left the board.</summary>
        public bool Cleared { get; internal set; }

        /// <summary>True for a tube added by the Add Container booster.</summary>
        public bool IsSpare { get; }

        public int Count => _slots.Count;
        public int Free => Capacity - _slots.Count;
        public bool IsEmpty => _slots.Count == 0;
        public bool IsActive => !Cleared && !Locked;

        /// <summary>Slot 0 is the closed end.</summary>
        public IReadOnlyList<Buddy> Slots => _slots;

        public Buddy Top => _slots.Count == 0 ? null : _slots[_slots.Count - 1];

        /// <summary>The grid cell of a slot: slot Capacity-1 sits at the mouth, slot 0 at the closed end.</summary>
        public GridPos CellOf(int slot) => Mouth.Step(Direction, Capacity - 1 - slot);

        /// <summary>Length of the same-colour, revealed run at the top. 0 when empty or the top is hidden.</summary>
        public int TopRunLength()
        {
            Buddy top = Top;
            if (top == null || top.Hidden) return 0;
            int n = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                Buddy b = _slots[i];
                if (b.Hidden || b.Color != top.Color) break;
                n++;
            }

            return n;
        }

        internal void Push(Buddy buddy) => _slots.Add(buddy);

        internal Buddy Pop()
        {
            Buddy b = _slots[_slots.Count - 1];
            _slots.RemoveAt(_slots.Count - 1);
            return b;
        }

        internal void ClearSlots() => _slots.Clear();

        internal List<Buddy> MutableSlots => _slots;

        internal Tube CloneWithBuddies()
        {
            var t = new Tube(Index, Mouth, Direction, Capacity, HouseColor, IsSpare) { Locked = Locked, Cleared = Cleared };
            foreach (Buddy b in _slots) t._slots.Add(b.Clone());
            return t;
        }
    }

    /// <summary>A plaza cell. At most one buddy stands on it.</summary>
    public sealed class PlazaCell
    {
        internal PlazaCell(int index, GridPos pos)
        {
            Index = index;
            Pos = pos;
        }

        public int Index { get; }
        public GridPos Pos { get; }
        public Buddy Occupant { get; internal set; }
        public bool IsFree => Occupant == null;

        internal PlazaCell CloneWithBuddy() => new PlazaCell(Index, Pos) { Occupant = Occupant?.Clone() };
    }
}
