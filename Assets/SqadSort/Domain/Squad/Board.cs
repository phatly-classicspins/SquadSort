using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Domain
{
    /// <summary>
    /// The rules core of one level (GDD §2.2b / §2.3). The player picks a group, then where it goes
    /// (<see cref="MoveTubeToTube"/>, <see cref="MoveTubeToPlaza"/>, <see cref="MovePlazaToTube"/>);
    /// <see cref="TapTube"/> / <see cref="TapPlaza"/> pick the destination themselves (the solver's moves). Pure and deterministic: randomness only enters through
    /// <see cref="Shuffle"/>'s <see cref="IRandom"/>, and nothing here reads time.
    /// </summary>
    public sealed class Board
    {
        /// <summary>Buddies per squad — a tube of this many same-colour buddies completes.</summary>
        public const int SquadSize = 4;

        private List<Tube> _tubes;
        private List<PlazaCell> _plaza;
        private readonly GridPos? _spareMouth;
        private readonly TubeDirection _spareDirection;
        private readonly bool _outOfSpaceOnFullPlaza;

        public Board(LevelDefinition level, bool outOfSpaceOnFullPlaza = true)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            _outOfSpaceOnFullPlaza = outOfSpaceOnFullPlaza;
            _spareMouth = level.SpareMouth;
            _spareDirection = level.SpareDirection;
            Difficulty = level.Difficulty;

            int nextId = 0;
            _tubes = new List<Tube>(level.Tubes.Count + 1);
            foreach (TubeSpec spec in level.Tubes)
            {
                var tube = new Tube(_tubes.Count, spec.Mouth, spec.Direction, spec.Capacity, spec.HouseColor, isSpare: false);
                foreach (BuddySpec b in spec.Contents) tube.Push(new Buddy(nextId++, b.Color, b.Hidden));
                _tubes.Add(tube);
            }

            _plaza = new List<PlazaCell>(level.PlazaCells.Count);
            foreach (GridPos pos in level.PlazaCells) _plaza.Add(new PlazaCell(_plaza.Count, pos));
            foreach (PlazaBuddySpec p in level.PlazaBuddies)
            {
                PlazaCell cell = _plaza.Find(c => c.Pos.Equals(p.Cell));
                if (cell == null) throw new ArgumentException($"Plaza buddy at {p.Cell} is not on a plaza cell.");
                cell.Occupant = new Buddy(nextId++, p.Buddy.Color, hidden: false);
            }

            RevealTops(null);
            State = BoardState.Playing;
        }

        public LevelDifficulty Difficulty { get; }
        public BoardState State { get; private set; }
        public IReadOnlyList<Tube> Tubes => _tubes;
        public IReadOnlyList<PlazaCell> Plaza => _plaza;
        public bool CanAddSpareTube => _spareMouth.HasValue && !_tubes.Exists(t => t.IsSpare);
        public int PlazaFree => _plaza.FindAll(c => c.IsFree).Count;

        // ------------------------------------------------------------------ actions

        /// <summary>Tap a buddy in a tube: its whole front group goes to the best destination (GDD §2.3).</summary>
        public MoveResult TapTube(int tubeIndex)
        {
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (tubeIndex < 0 || tubeIndex >= _tubes.Count) return new MoveResult(RejectReason.NotTappable, State);

            Tube source = _tubes[tubeIndex];
            int run = source.IsActive ? source.TopRunLength() : 0;
            if (run == 0) return new MoveResult(RejectReason.NotTappable, State);

            SquadColor color = source.Top.Color;
            var moves = new List<BuddyMove>();
            Tube dest = FindTubeFor(color, source);

            if (dest != null)
            {
                int n = Math.Min(run, dest.Free);
                for (int i = 0; i < n; i++) MoveTubeToTube(source, dest, moves);
                GatherFromPlaza(dest, color, moves);
                return Finish(moves, dest);
            }

            int free = PlazaFree;
            if (free == 0) return new MoveResult(RejectReason.NoSpace, State);
            int count = Math.Min(run, free);
            for (int i = 0; i < count; i++)
            {
                int slot = source.Count - 1;
                Buddy b = source.Pop();
                PlazaCell cell = NearestFreeCell(source.Mouth);
                cell.Occupant = b;
                moves.Add(new BuddyMove(b.Id, BuddyLocation.InTube(source.Index, slot), BuddyLocation.OnPlaza(cell.Index)));
            }

            return Finish(moves, null);
        }

        // ------------------------------------------------------------------ select, then drop

        /// <summary>A tube can be picked up from: it is open and its front buddy is revealed.</summary>
        public bool CanPickTube(int tubeIndex)
            => State == BoardState.Playing && tubeIndex >= 0 && tubeIndex < _tubes.Count && _tubes[tubeIndex].IsActive && _tubes[tubeIndex].TopRunLength() > 0;

        /// <summary>A plaza cell can be picked up from: someone stands on it.</summary>
        public bool CanPickPlaza(int cellIndex)
            => State == BoardState.Playing && cellIndex >= 0 && cellIndex < _plaza.Count && !_plaza[cellIndex].IsFree;

        /// <summary>
        /// The front group of tube <paramref name="from"/> walks into tube <paramref name="to"/> as far as
        /// it has room, then plaza buddies of the colour follow (GDD §2.2b-10). The destination must be
        /// open and empty, or show the same colour at its front.
        /// </summary>
        public MoveResult MoveTubeToTube(int from, int to)
        {
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (!CanPickTube(from)) return new MoveResult(RejectReason.NotTappable, State);
            Tube source = _tubes[from];
            SquadColor color = source.Top.Color;
            if (to < 0 || to >= _tubes.Count || to == from || !Accepts(_tubes[to], color))
                return new MoveResult(RejectReason.WrongDestination, State);

            Tube dest = _tubes[to];
            var moves = new List<BuddyMove>();
            int n = Math.Min(source.TopRunLength(), dest.Free);
            for (int i = 0; i < n; i++) MoveTubeToTube(source, dest, moves);
            GatherFromPlaza(dest, color, moves);
            return Finish(moves, dest);
        }

        /// <summary>
        /// The front group of tube <paramref name="from"/> steps out onto the plaza: the first onto
        /// <paramref name="cellIndex"/> if it is free, the rest onto the free cells nearest it.
        /// </summary>
        public MoveResult MoveTubeToPlaza(int from, int cellIndex)
        {
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (!CanPickTube(from)) return new MoveResult(RejectReason.NotTappable, State);
            if (cellIndex < 0 || cellIndex >= _plaza.Count) return new MoveResult(RejectReason.WrongDestination, State);
            int free = PlazaFree;
            if (free == 0) return new MoveResult(RejectReason.NoSpace, State);

            Tube source = _tubes[from];
            GridPos target = _plaza[cellIndex].Pos;
            var moves = new List<BuddyMove>();
            int count = Math.Min(source.TopRunLength(), free);
            for (int i = 0; i < count; i++)
            {
                int slot = source.Count - 1;
                Buddy b = source.Pop();
                PlazaCell cell = NearestFreeCell(target);
                cell.Occupant = b;
                moves.Add(new BuddyMove(b.Id, BuddyLocation.InTube(source.Index, slot), BuddyLocation.OnPlaza(cell.Index)));
            }

            return Finish(moves, null);
        }

        /// <summary>
        /// The plaza buddy on <paramref name="cellIndex"/> walks into tube <paramref name="to"/>; plaza
        /// buddies of its colour follow while there is room.
        /// </summary>
        public MoveResult MovePlazaToTube(int cellIndex, int to)
        {
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (!CanPickPlaza(cellIndex)) return new MoveResult(RejectReason.NotTappable, State);
            PlazaCell start = _plaza[cellIndex];
            SquadColor color = start.Occupant.Color;
            if (to < 0 || to >= _tubes.Count || !Accepts(_tubes[to], color))
                return new MoveResult(RejectReason.WrongDestination, State);

            Tube dest = _tubes[to];
            Buddy first = start.Occupant;
            start.Occupant = null;
            dest.Push(first);
            var moves = new List<BuddyMove> { new BuddyMove(first.Id, BuddyLocation.OnPlaza(start.Index), BuddyLocation.InTube(dest.Index, dest.Count - 1)) };
            GatherFromPlaza(dest, color, moves);
            return Finish(moves, dest);
        }

        /// <summary>An open tube with room that is empty or shows <paramref name="color"/> at its front.</summary>
        public bool Accepts(Tube tube, SquadColor color)
            => tube != null && tube.IsActive && tube.Free > 0 && (tube.IsEmpty || (!tube.Top.Hidden && tube.Top.Color == color));

        /// <summary>Tap a buddy on the plaza: every plaza buddy of its colour goes to the best tube (GDD §2.2b-4).</summary>
        public MoveResult TapPlaza(int cellIndex)
        {
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (cellIndex < 0 || cellIndex >= _plaza.Count || _plaza[cellIndex].IsFree)
                return new MoveResult(RejectReason.NotTappable, State);

            SquadColor color = _plaza[cellIndex].Occupant.Color;
            Tube dest = FindTubeFor(color, null);
            if (dest == null) return new MoveResult(RejectReason.NoDestination, State);

            var moves = new List<BuddyMove>();
            GatherFromPlaza(dest, color, moves);
            return Finish(moves, dest);
        }

        /// <summary>Shuffle booster: reorder one active tube at random (GDD §3).</summary>
        public MoveResult Shuffle(int tubeIndex, IRandom random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (State != BoardState.Playing) return new MoveResult(RejectReason.NotPlaying, State);
            if (tubeIndex < 0 || tubeIndex >= _tubes.Count) return new MoveResult(RejectReason.NotTappable, State);
            Tube tube = _tubes[tubeIndex];
            if (!tube.IsActive || tube.Count < 2) return new MoveResult(RejectReason.NotTappable, State);

            List<Buddy> slots = tube.MutableSlots;
            var before = new Dictionary<int, int>(slots.Count);
            for (int i = 0; i < slots.Count; i++) before[slots[i].Id] = i;
            random.Shuffle(slots);

            var moves = new List<BuddyMove>();
            for (int i = 0; i < slots.Count; i++)
            {
                int old = before[slots[i].Id];
                if (old != i) moves.Add(new BuddyMove(slots[i].Id, BuddyLocation.InTube(tube.Index, old), BuddyLocation.InTube(tube.Index, i)));
            }

            return Finish(moves, tube);
        }

        /// <summary>Add Container booster: one empty tube at the level's spare position. Returns its index, or -1.</summary>
        public int AddSpareTube(int capacity = SquadSize)
        {
            if (!CanAddSpareTube || State == BoardState.Won) return -1;
            var tube = new Tube(_tubes.Count, _spareMouth.Value, _spareDirection, capacity, houseColor: null, isSpare: true);
            _tubes.Add(tube);
            if (State == BoardState.OutOfSpace) State = BoardState.Playing;   // an empty tube is somewhere to go again
            return tube.Index;
        }

        /// <summary>Add Space (the Out of Space continue): a new plaza cell. Resumes play.</summary>
        public int AddPlazaCell(GridPos pos)
        {
            if (_plaza.Exists(c => c.Pos.Equals(pos))) throw new ArgumentException($"Plaza already has a cell at {pos}.");
            var cell = new PlazaCell(_plaza.Count, pos);
            _plaza.Add(cell);
            if (State == BoardState.OutOfSpace) State = BoardState.Playing;
            return cell.Index;
        }

        /// <summary>
        /// Where Add Space (the Out of Space continue) puts its cell: a free grid cell next to the plaza that no tube — nor the
        /// level's spare tube — uses; the lowest row first, then the one nearest the plaza's middle. Null when none is left.
        /// </summary>
        public GridPos? NextPlazaCell()
        {
            var used = new HashSet<GridPos>();
            foreach (PlazaCell c in _plaza) used.Add(c.Pos);
            foreach (Tube t in _tubes)
                for (int s = 0; s < t.Capacity; s++) used.Add(t.CellOf(s));
            if (_spareMouth.HasValue && CanAddSpareTube)
                for (int s = 0; s < SquadSize; s++) used.Add(_spareMouth.Value.Step(_spareDirection, s));

            double midX = 0;
            foreach (PlazaCell c in _plaza) midX += c.Pos.X;
            midX = _plaza.Count > 0 ? midX / _plaza.Count : 0;

            GridPos? best = null;
            foreach (PlazaCell c in _plaza)
            foreach (TubeDirection d in new[] { TubeDirection.Down, TubeDirection.Left, TubeDirection.Right, TubeDirection.Up })
            {
                GridPos n = c.Pos.Step(d, 1);
                if (used.Contains(n)) continue;
                if (best == null || n.Y > best.Value.Y
                    || (n.Y == best.Value.Y && Math.Abs(n.X - midX) < Math.Abs(best.Value.X - midX)))
                    best = n;
            }

            return best;
        }

        // ------------------------------------------------------------------ undo

        public BoardSnapshot Snapshot() => new BoardSnapshot(_tubes.ConvertAll(t => t.CloneWithBuddies()), _plaza.ConvertAll(c => c.CloneWithBuddy()), State);

        public void Restore(BoardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _tubes = snapshot.Tubes.ConvertAll(t => t.CloneWithBuddies());
            _plaza = snapshot.Plaza.ConvertAll(c => c.CloneWithBuddy());
            State = snapshot.State;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>Where a buddy stands now, or null if its squad has left the board.</summary>
        public BuddyLocation? Locate(int buddyId)
        {
            foreach (Tube t in _tubes)
                for (int i = 0; i < t.Count; i++)
                    if (t.Slots[i].Id == buddyId) return BuddyLocation.InTube(t.Index, i);
            foreach (PlazaCell c in _plaza)
                if (c.Occupant != null && c.Occupant.Id == buddyId) return BuddyLocation.OnPlaza(c.Index);
            return null;
        }

        public int BuddiesRemaining()
        {
            int n = 0;
            foreach (Tube t in _tubes) n += t.Count;
            foreach (PlazaCell c in _plaza) if (!c.IsFree) n++;
            return n;
        }

        /// <summary>
        /// The tube a group of <paramref name="color"/> would go to: an active tube whose front matches and
        /// has room (longest matching run first), else an empty active tube; never <paramref name="exclude"/>.
        /// </summary>
        public Tube FindTubeFor(SquadColor color, Tube exclude)
        {
            Tube best = null;
            int bestRun = -1;
            foreach (Tube t in _tubes)
            {
                if (t == exclude || !t.IsActive || t.Free == 0 || t.IsEmpty) continue;
                if (t.Top.Hidden || t.Top.Color != color) continue;
                int run = t.TopRunLength();
                if (run > bestRun)
                {
                    best = t;
                    bestRun = run;
                }
            }

            if (best != null) return best;
            foreach (Tube t in _tubes)
                if (t != exclude && t.IsActive && t.IsEmpty)
                    return t;
            return null;
        }

        // ------------------------------------------------------------------ internals

        private void MoveTubeToTube(Tube from, Tube to, List<BuddyMove> moves)
        {
            int fromSlot = from.Count - 1;
            Buddy b = from.Pop();
            to.Push(b);
            moves.Add(new BuddyMove(b.Id, BuddyLocation.InTube(from.Index, fromSlot), BuddyLocation.InTube(to.Index, to.Count - 1)));
        }

        /// <summary>Plaza buddies of the colour follow into the tube, nearest the mouth first (GDD §2.2b-10).</summary>
        private void GatherFromPlaza(Tube dest, SquadColor color, List<BuddyMove> moves)
        {
            while (dest.Free > 0)
            {
                PlazaCell nearest = null;
                int best = int.MaxValue;
                foreach (PlazaCell c in _plaza)
                {
                    if (c.IsFree || c.Occupant.Color != color) continue;
                    int d = c.Pos.ManhattanTo(dest.Mouth);
                    if (d < best)
                    {
                        best = d;
                        nearest = c;
                    }
                }

                if (nearest == null) return;
                Buddy b = nearest.Occupant;
                nearest.Occupant = null;
                dest.Push(b);
                moves.Add(new BuddyMove(b.Id, BuddyLocation.OnPlaza(nearest.Index), BuddyLocation.InTube(dest.Index, dest.Count - 1)));
            }
        }

        private PlazaCell NearestFreeCell(GridPos from)
        {
            PlazaCell nearest = null;
            int best = int.MaxValue;
            foreach (PlazaCell c in _plaza)
            {
                if (!c.IsFree) continue;
                int d = c.Pos.ManhattanTo(from);
                if (d < best)
                {
                    best = d;
                    nearest = c;
                }
            }

            return nearest;
        }

        private MoveResult Finish(List<BuddyMove> moves, Tube touched)
        {
            var cleared = new List<int>();
            var opened = new List<int>();
            var revealed = new List<int>();

            // A completion can open a house whose contents complete at once — loop until stable.
            var check = new Queue<Tube>();
            if (touched != null) check.Enqueue(touched);
            while (check.Count > 0)
            {
                Tube t = check.Dequeue();
                if (!IsCompleteSquad(t)) continue;

                SquadColor color = t.Top.Color;
                t.ClearSlots();
                t.Cleared = true;
                cleared.Add(t.Index);

                foreach (Tube house in _tubes)
                {
                    if (!house.Locked || house.HouseColor != color) continue;
                    house.Locked = false;
                    opened.Add(house.Index);
                    check.Enqueue(house);
                }
            }

            RevealTops(revealed);

            if (BuddiesRemaining() == 0) State = BoardState.Won;
            else if (_outOfSpaceOnFullPlaza && PlazaFree == 0) State = BoardState.OutOfSpace;

            return new MoveResult(moves, cleared, opened, revealed, State);
        }

        private static bool IsCompleteSquad(Tube t)
        {
            if (!t.IsActive || t.Count != t.Capacity || t.Count < SquadSize) return false;
            SquadColor color = t.Slots[0].Color;
            foreach (Buddy b in t.Slots)
                if (b.Hidden || b.Color != color) return false;
            return true;
        }

        /// <summary>The front buddy of every open tube is visible (GDD §2.2b-11).</summary>
        private void RevealTops(List<int> revealed)
        {
            foreach (Tube t in _tubes)
            {
                if (!t.IsActive || t.IsEmpty || !t.Top.Hidden) continue;
                t.Top.Hidden = false;
                revealed?.Add(t.Top.Id);
            }
        }
    }

    /// <summary>A deep copy of the board for Undo.</summary>
    public sealed class BoardSnapshot
    {
        internal BoardSnapshot(List<Tube> tubes, List<PlazaCell> plaza, BoardState state)
        {
            Tubes = tubes;
            Plaza = plaza;
            State = state;
        }

        internal List<Tube> Tubes { get; }
        internal List<PlazaCell> Plaza { get; }
        internal BoardState State { get; }
    }
}
