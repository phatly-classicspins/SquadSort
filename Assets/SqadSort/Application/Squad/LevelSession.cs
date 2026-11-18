using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Domain;

namespace Game.Application
{
    public enum Booster : byte
    {
        Undo,
        Shuffle,
        AddContainer,
    }

    /// <summary>
    /// One play-through of one level: the <see cref="Board"/>, the undo history and the booster
    /// charges (GDD §3). Engine-free; the screen controller drives it and the Views animate what
    /// each <see cref="MoveResult"/> reports.
    /// </summary>
    public sealed class LevelSession
    {
        private readonly Stack<BoardSnapshot> _history = new Stack<BoardSnapshot>();
        private readonly Dictionary<Booster, int> _charges;
        private readonly IRandom _random;

        public LevelSession(int levelNumber, LevelDefinition level, IRandom random, IReadOnlyDictionary<Booster, int> charges)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            LevelNumber = levelNumber;
            Level = level;
            Board = new Board(level);
            _charges = new Dictionary<Booster, int>();
            foreach (Booster b in (Booster[])Enum.GetValues(typeof(Booster)))
                _charges[b] = charges != null && charges.TryGetValue(b, out int n) ? n : 0;
        }

        public int LevelNumber { get; }
        public LevelDefinition Level { get; }
        public Board Board { get; }
        public int MovesMade { get; private set; }
        public bool CanUndo => _history.Count > 0 && Charges(Booster.Undo) > 0 && Board.State != BoardState.Won;

        public int Charges(Booster booster) => _charges[booster];

        public MoveResult TapTube(int tube) => Record(() => Board.TapTube(tube));

        public MoveResult TapPlaza(int cell) => Record(() => Board.TapPlaza(cell));

        /// <summary>Undo the last accepted move. Spends a charge. Returns false if nothing was undone.</summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            Board.Restore(_history.Pop());
            _charges[Booster.Undo]--;
            MovesMade = Math.Max(0, MovesMade - 1);
            return true;
        }

        /// <summary>Shuffle one tube. Spends a charge only if the shuffle happened.</summary>
        public MoveResult Shuffle(int tube)
        {
            if (Charges(Booster.Shuffle) <= 0) return null;
            BoardSnapshot before = Board.Snapshot();
            MoveResult r = Board.Shuffle(tube, _random);
            if (!r.Accepted) return r;
            _history.Push(before);
            _charges[Booster.Shuffle]--;
            return r;
        }

        /// <summary>Add the level's spare tube. Returns its index, or -1 (no charge, already added, or no spare slot).</summary>
        public int AddContainer()
        {
            if (Charges(Booster.AddContainer) <= 0) return -1;
            int index = Board.AddSpareTube();
            if (index < 0) return -1;
            _charges[Booster.AddContainer]--;
            _history.Clear(); // undo across a new tube would leave the tube's view stranded
            return index;
        }

        /// <summary>Out of Space continue: one more plaza cell (paid for by the caller). Returns its index.</summary>
        public int AddSpace(GridPos cell)
        {
            int index = Board.AddPlazaCell(cell);
            _history.Clear();
            return index;
        }

        private MoveResult Record(Func<MoveResult> action)
        {
            BoardSnapshot before = Board.Snapshot();
            MoveResult r = action();
            if (r.Accepted)
            {
                _history.Push(before);
                MovesMade++;
            }

            return r;
        }
    }
}
