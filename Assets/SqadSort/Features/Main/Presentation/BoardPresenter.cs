using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Presentation;
using Game.Application;
using Game.Domain;
using Game.Views;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Drives the <see cref="BoardView"/> from a <see cref="LevelSession"/>: lays the level out, runs the
    /// one-tap control of the reference game (tap a group and the board picks where it goes — GDD §2.3), and replays each <see cref="MoveResult"/> as animations. It
    /// owns every "what happens" decision — who is awake, the order buddies walk in, when a squad
    /// celebrates — so the View stays a dumb drawer (rule #8). A plain class, never a MonoBehaviour.
    /// </summary>
    public sealed class BoardPresenter : IDisposable
    {
        private readonly BoardView _view;
        private readonly IWorldViewport _viewport;
        private readonly HashSet<int> _shown = new HashSet<int>();
        private readonly List<int> _pickedIds = new List<int>();
        private LevelSession _session;
        private bool _ended;
        private int _walking;
        private int _replayVersion;

        // Shuffle booster armed: the next tube tap shuffles that tube instead of moving a group.
        private bool _shufflePicking;

        public BoardPresenter(BoardView view, IWorldViewport viewport)
        {
            _view = view ? view : throw new ArgumentNullException(nameof(view));
            _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            _view.BuddyTapped += OnBuddyTapped;
            _view.TubeTapped += OnTubeTapped;
            _view.CellTapped += OnCellTapped;
        }

        /// <summary>
        /// Raised once when the level stops accepting taps — won, or out of space. The argument is the
        /// final state and the seconds until the board's last animation settles.
        /// </summary>
        public event Action<BoardState, float> LevelEnded;

        /// <summary>Raised after the board changed (a move, a shuffle, a rebuild) — the HUD re-reads charges.</summary>
        public event Action Changed;

        /// <summary>Raised when an armed Shuffle ends — used on a tube, or cancelled.</summary>
        public event Action ShufflePickEnded;

        public bool IsShufflePicking => _shufflePicking;

        /// <summary>FTUE (CardSlot features/ftue.md v2): while set, only this tube takes a tap and the plaza takes none.
        /// -1 = free play. The screen decides which tube; the presenter only gates.</summary>
        public int GuidedTube { get; set; } = -1;

        /// <summary>Arm the Shuffle booster: the next tube the player taps is shuffled. Drops any held group.</summary>
        public void BeginShufflePick()
        {
            if (_walking > 0) return;
            Drop();
            _shufflePicking = true;
        }

        /// <summary>Disarm the Shuffle booster without using it.</summary>
        public void CancelShufflePick()
        {
            if (!_shufflePicking) return;
            _shufflePicking = false;
            ShufflePickEnded?.Invoke();
        }

        public void Show(LevelSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _ended = false;
            _walking = 0;
            _replayVersion++;
            _pickedIds.Clear();
            _shufflePicking = false;
            _shown.Clear();
            _view.Clear();

            Board board = session.Board;
            foreach (PlazaCell cell in board.Plaza) _view.AddPlazaCell(ToGrid(cell.Pos));

            foreach (Tube tube in board.Tubes)
            {
                _view.AddTube(tube.Index, ToGrid(tube.Mouth), Step(tube.Direction), tube.Capacity);
                if (tube.Locked) _view.SetTubeHouse(tube.Index, (int)tube.HouseColor.Value);
                else ShowTubeBuddies(tube);
            }

            foreach (PlazaCell cell in board.Plaza)
                if (!cell.IsFree)
                    ShowBuddy(cell.Occupant, cell.Pos);

            Layout();
            RefreshAwake();
            Changed?.Invoke();
        }

        /// <summary>Re-fit the board to the current viewport (an aspect change).</summary>
        public void Layout()
        {
            Rect safe = _viewport.SafeRect;
            float hw = _viewport.HalfWidth;
            float hh = _viewport.HalfHeight;
            _view.SetGroundRect(new Rect(safe.center.x - hw, safe.center.y - hh, hw * 2f, hh * 2f));
            _view.FitInto(safe);
        }

        public void Dispose()
        {
            _replayVersion++;
            _view.BuddyTapped -= OnBuddyTapped;
            _view.TubeTapped -= OnTubeTapped;
            _view.CellTapped -= OnCellTapped;
        }

        // ------------------------------------------------------------------ input

        private void OnBuddyTapped(int buddyId)
        {
            if (_session == null || _ended) return;
            BuddyLocation? at = _session.Board.Locate(buddyId);
            if (!at.HasValue) return;
            if (at.Value.Kind == LocationKind.Tube) OnTube(at.Value.Index);
            else OnPlaza(at.Value.Index);
        }

        private void OnTubeTapped(int tube)
        {
            if (_session == null || _ended) return;
            OnTube(tube);
        }

        private void OnCellTapped(Vector2Int cell)
        {
            if (_session == null || _ended) return;
            IReadOnlyList<PlazaCell> plaza = _session.Board.Plaza;
            for (int i = 0; i < plaza.Count; i++)
                if (plaza[i].Pos.X == cell.x && plaza[i].Pos.Y == cell.y)
                {
                    OnPlaza(i);
                    return;
                }
        }

        /// <summary>A tube was tapped: its front group goes to the best destination — a tube of its colour, an empty tube,
        /// else the plaza (<see cref="Board.TapTube"/>). An armed Shuffle shuffles that tube instead.</summary>
        private void OnTube(int tube)
        {
            if (_walking > 0) return;
            if (_shufflePicking)
            {
                MoveResult shuffled = _session.Shuffle(tube);
                if (shuffled == null || !shuffled.Accepted) return; // not a tube that can shuffle — stay armed
                _shufflePicking = false;
                ShufflePickEnded?.Invoke();
                Apply(shuffled);
                return;
            }

            if (GuidedTube >= 0 && tube != GuidedTube) return; // FTUE: only the pointed tube
            Board board = _session.Board;
            if (!board.CanPickTube(tube)) return; // empty, a house, or a hidden front: nothing to send
            var group = FrontGroup(board.Tubes[tube]);
            MoveResult result = _session.TapTube(tube);
            if (result.Accepted) Apply(result);
            else Refuse(result.Reject, group);
        }

        /// <summary>A plaza buddy was tapped: every plaza buddy of its colour goes to the best tube (<see cref="Board.TapPlaza"/>).</summary>
        private void OnPlaza(int cell)
        {
            if (_walking > 0) return;
            if (_shufflePicking)
            {
                CancelShufflePick(); // tapping off the tubes disarms the booster
                return;
            }

            if (GuidedTube >= 0) return; // FTUE: only the pointed tube
            Board board = _session.Board;
            if (board.Plaza[cell].IsFree) return;
            var group = new List<int> { board.Plaza[cell].Occupant.Id };
            MoveResult result = _session.TapPlaza(cell);
            if (result.Accepted) Apply(result);
            else Refuse(result.Reject, group);
        }

        private static List<int> FrontGroup(Tube t)
        {
            var ids = new List<int>();
            int run = t.TopRunLength();
            for (int slot = t.Count - run; slot < t.Count; slot++) ids.Add(t.Slots[slot].Id);
            return ids;
        }

        /// <summary>Clears a held selection. One-tap play never holds one; kept so arming Shuffle stays safe.</summary>
        private void Drop()
        {
            foreach (int id in _pickedIds) _view.SetBuddySelected(id, false);
            _pickedIds.Clear();
        }

        /// <summary>The tap had nowhere to go: the plaza flashes when it is full (GDD §2.2b-8), otherwise the group winces.</summary>
        private void Refuse(RejectReason reason, List<int> group)
        {
            if (group.Count == 0) return;
            if (reason == RejectReason.NoSpace) _view.PlayRejected(group[0]);
            else
                foreach (int id in group)
                    _view.WinceBuddy(id);
        }

        // ------------------------------------------------------------------ replay

        private void Apply(MoveResult result)
        {
            Board board = _session.Board;

            float settled = 0f;
            int version = _replayVersion;
            _walking = result.Moves.Count;
            for (int i = 0; i < result.Moves.Count; i++)
            {
                BuddyMove move = result.Moves[i];
                IReadOnlyList<GridPos> path = BoardWalkRoute.Build(board, move.From, move.To);
                var route = new Vector2Int[path.Count];
                for (int point = 0; point < path.Count; point++) route[point] = ToGrid(path[point]);
                float delay = i * MotionTokens.Stagger;
                float duration = _view.MoveBuddy(move.BuddyId, route, delay, () =>
                {
                    if (version == _replayVersion) _walking--;
                });
                settled = Mathf.Max(settled, delay + duration);
            }

            // A buddy the board no longer holds left with a completed squad.
            var gone = new List<int>();
            foreach (int id in _shown)
                if (!board.Locate(id).HasValue)
                    gone.Add(id);
            for (int i = 0; i < gone.Count; i++)
            {
                _shown.Remove(gone[i]);
                _view.CelebrateAndRemoveBuddy(gone[i], settled + i * MotionTokens.Stagger);
            }

            float cleared = settled;
            if (result.ClearedTubes.Count > 0)
            {
                cleared = settled + gone.Count * MotionTokens.Stagger + MotionTokens.CelebrateHop + MotionTokens.CelebratePop;
                foreach (int tube in result.ClearedTubes) _view.RemoveTube(tube, cleared);
                cleared += MotionTokens.TubeFade;
            }

            foreach (int tube in result.OpenedHouses)
            {
                _view.SetTubeOpen(tube);
                ShowTubeBuddies(board.Tubes[tube]);
            }

            foreach (int id in result.RevealedBuddies)
            {
                BuddyLocation? at = board.Locate(id);
                if (at.HasValue && at.Value.Kind == LocationKind.Tube)
                    _view.RevealBuddy(id, (int)board.Tubes[at.Value.Index].Slots[at.Value.Slot].Color);
            }

            RefreshAwake();
            Changed?.Invoke();

            if (result.State != BoardState.Playing)
            {
                _ended = true;
                LevelEnded?.Invoke(result.State, cleared);
            }
        }

        /// <summary>
        /// Awake = tappable: the front group of every open tube, and everyone on the plaza (GDD §2.2).
        /// </summary>
        private void RefreshAwake()
        {
            Board board = _session.Board;
            foreach (Tube tube in board.Tubes)
            {
                if (!tube.IsActive) continue;
                int run = tube.TopRunLength();
                for (int slot = 0; slot < tube.Count; slot++)
                    _view.SetBuddyAwake(tube.Slots[slot].Id, slot >= tube.Count - run);
            }

            foreach (PlazaCell cell in board.Plaza)
                if (!cell.IsFree)
                    _view.SetBuddyAwake(cell.Occupant.Id, true);
        }

        private void ShowTubeBuddies(Tube tube)
        {
            for (int slot = 0; slot < tube.Count; slot++) ShowBuddy(tube.Slots[slot], tube.CellOf(slot));
        }

        private void ShowBuddy(Buddy buddy, GridPos pos)
        {
            _view.AddBuddy(buddy.Id, ToGrid(pos), (int)buddy.Color, buddy.Hidden, awake: false);
            _shown.Add(buddy.Id);
        }

        // ------------------------------------------------------------------ grid mapping

        private static GridPos CellOf(Board board, BuddyLocation at)
            => at.Kind == LocationKind.Tube ? board.Tubes[at.Index].CellOf(at.Slot) : board.Plaza[at.Index].Pos;

        private static Vector2Int ToGrid(GridPos pos) => new Vector2Int(pos.X, pos.Y);

        /// <summary>One grid step from a tube's mouth toward its closed end (grid Y grows downward).</summary>
        private static Vector2Int Step(TubeDirection direction)
        {
            switch (direction)
            {
                case TubeDirection.Up: return new Vector2Int(0, -1);
                case TubeDirection.Down: return new Vector2Int(0, 1);
                case TubeDirection.Left: return new Vector2Int(-1, 0);
                default: return new Vector2Int(1, 0);
            }
        }
    }
}
