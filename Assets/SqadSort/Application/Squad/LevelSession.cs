using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Domain;

namespace Game.Application
{
    public enum Booster
    {
        Undo,
        Shuffle,
        Remove,      // Squad Sort GDD §1.5 (was Add Container; the save still names it booster_add_container / BoosterAddContainer)
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

        public MoveResult MoveTubeToTube(int from, int to) => Record(() => Board.MoveTubeToTube(from, to));

        public MoveResult MoveTubeToPlaza(int from, int cell) => Record(() => Board.MoveTubeToPlaza(from, cell));

        public MoveResult MovePlazaToTube(int cell, int to) => Record(() => Board.MovePlazaToTube(cell, to));

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

        /// <summary>Remove booster (Squad Sort GDD §1.5) on a tapped buddy: its colour fills a container and clears. Spends a
        /// charge only if it happened; Undo can take it back. Null without a charge.</summary>
        public MoveResult Remove(int buddyId)
        {
            if (Charges(Booster.Remove) <= 0) return null;
            BoardSnapshot before = Board.Snapshot();
            MoveResult r = Board.Remove(buddyId);
            if (!r.Accepted) return r;
            _history.Push(before);
            _charges[Booster.Remove]--;
            return r;
        }

        /// <summary>Remove with no tap (the lose flow): on the buddy <see cref="Board.AutoRemoveBuddy"/> picks. Null without a
        /// charge or a target.</summary>
        public MoveResult RemoveAuto()
        {
            int buddy = Board.AutoRemoveBuddy();
            return buddy < 0 ? null : Remove(buddy);
        }

        /// <summary>The level's spare tube (kept for the GDD §1.3 RV Slot; no booster spends it). Returns its index, or -1
        /// (already added, or no spare slot).</summary>
        public int AddSpareTube()
        {
            int index = Board.AddSpareTube();
            if (index < 0) return -1;
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
