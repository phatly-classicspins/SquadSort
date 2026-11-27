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
    /// one-tap control of the original game on Android (tap a container and its front group finds its own way: a container
    /// that takes it, else the Center Floor; tap a floor buddy and its whole colour walks into a container), and replays each <see cref="MoveResult"/> as animations. It
    /// owns every "what happens" decision — who is awake, the order buddies walk in, when a squad
    /// celebrates — so the View stays a dumb drawer (rule #8). A plain class, never a MonoBehaviour.
    /// </summary>
    public sealed class BoardPresenter : IDisposable
    {
        private readonly BoardView _view;
        private readonly IWorldViewport _viewport;
        private readonly HashSet<int> _shown = new HashSet<int>();
        private LevelSession _session;
        private bool _ended;
        private int _walking;
        private int _replayVersion;

        // Shuffle booster armed: the next tube tap shuffles that tube instead of moving a group.
        private bool _shufflePicking;

        // Remove booster armed: the next buddy tap (a container's front, or the floor) is Remove's choice of colour.
        private bool _removePicking;

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

        /// <summary>Raised when an armed Remove ends — used on a buddy, or cancelled.</summary>
        public event Action RemovePickEnded;

        public bool IsShufflePicking => _shufflePicking;

        public bool IsRemovePicking => _removePicking;

        /// <summary>FTUE: while set, only this tube takes a tap and the plaza takes none. -1 = free play. The screen decides
        /// which tube; the presenter only gates.</summary>
        public int GuidedTube { get; set; } = -1;

        /// <summary>A result was animated: what made it, then the seconds until the buddies settle and until a cleared squad's
        /// container is gone (both 0 when nothing moves or clears).</summary>
        public event Action<MoveResult, ReplayKind, float, float> Replayed;

        /// <summary>Arm the Shuffle booster: the next tube the player taps is shuffled.</summary>
        public void BeginShufflePick()
        {
            if (_walking > 0) return;
            CancelRemovePick();
            _shufflePicking = true;
        }

        /// <summary>Arm the Remove booster (Squad Sort GDD §1.5): the next buddy tapped picks the colour.</summary>
        public void BeginRemovePick()
        {
            if (_walking > 0) return;
            CancelShufflePick();
            _removePicking = true;
        }

        /// <summary>Disarm the Remove booster without using it.</summary>
        public void CancelRemovePick()
        {
            if (!_removePicking) return;
            _removePicking = false;
            RemovePickEnded?.Invoke();
        }

        /// <summary>Replay a result made outside a tap (a booster used from the lose flow): animate it, raise the events.</summary>
        public void Play(MoveResult result)
        {
            if (result != null && result.Accepted) Apply(result, ReplayKind.Remove);
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
            _shufflePicking = false;
            _removePicking = false;
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

        /// <summary>
        /// After an Undo (Squad Sort GDD §1.5): rebuild from the restored board, then play the undone move backwards — each
        /// buddy it moved starts where the move left it (a cleared squad's container included) and walks back to where it
        /// stands now, the last mover first. Taps wait until they stand still. Returns those seconds (0 when nothing walks).
        /// </summary>
        public float Rewind(LevelSession session)
        {
            Show(session);
            MoveResult undone = session.LastUndone;
            if (undone == null) return 0f;

            Board board = session.Board;
            var start = new Dictionary<int, BuddyLocation>();
            var order = new List<int>();
            for (int i = undone.Moves.Count - 1; i >= 0; i--)
            {
                BuddyMove move = undone.Moves[i];
                if (start.ContainsKey(move.BuddyId)) continue;
                start.Add(move.BuddyId, move.To);   // its last move's end: where the undone move left it
                order.Add(move.BuddyId);
            }

            float settled = 0f;
            int version = _replayVersion;
            int walkers = 0;
            foreach (int id in order)
            {
                BuddyLocation? now = board.Locate(id);
                if (!now.HasValue || !_shown.Contains(id) || now.Value.Equals(start[id])) continue;
                IReadOnlyList<GridPos> path = BoardWalkRoute.Build(board, start[id], now.Value);
                var route = new Vector2Int[path.Count];
                for (int point = 0; point < path.Count; point++) route[point] = ToGrid(path[point]);
                _view.PlaceBuddy(id, route[0]);
                float delay = walkers * MotionTokens.Stagger;
                walkers++;
                _walking++;
                float duration = _view.MoveBuddy(id, route, delay, () =>
                {
                    if (version == _replayVersion) _walking--;
                });
                settled = Mathf.Max(settled, delay + duration);
            }

            return settled;
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

        /// <summary>A tube was tapped: its front group goes into a container that takes it, else out onto the Center Floor
        /// (<see cref="Board.TapTube"/>). An armed Shuffle shuffles that tube instead.</summary>
        private void OnTube(int tube)
        {
            if (_walking > 0) return;
            if (_shufflePicking)
            {
                MoveResult shuffled = _session.Shuffle(tube);
                if (shuffled == null || !shuffled.Accepted) return; // not a tube that can shuffle — stay armed
                _shufflePicking = false;
                ShufflePickEnded?.Invoke();
                Apply(shuffled, ReplayKind.Shuffle);
                return;
            }

            if (_removePicking)
            {
                Tube t = _session.Board.Tubes[tube];
                if (t.Top != null) RemoveOn(t.Top.Id);   // any buddy of a container means its front
                return;
            }

            if (GuidedTube >= 0 && tube != GuidedTube) return; // FTUE: only the pointed tube
            MoveResult result = _session.TapTube(tube);
            if (result.Accepted) Apply(result);
            else Refuse(result.Reject, FrontGroup(tube));
        }

        /// <summary>A Center Floor tile was tapped: every floor buddy of the colour standing there walks into a container that
        /// takes it (<see cref="Board.TapPlaza"/>).</summary>
        private void OnPlaza(int cell)
        {
            if (_walking > 0) return;
            if (_shufflePicking)
            {
                CancelShufflePick(); // tapping off the tubes disarms the booster
                return;
            }

            if (_removePicking)
            {
                Buddy on = _session.Board.Plaza[cell].Occupant;
                if (on != null) RemoveOn(on.Id);
                else CancelRemovePick(); // an empty tile disarms the booster
                return;
            }

            if (GuidedTube >= 0) return; // FTUE: only the pointed tube
            Buddy buddy = _session.Board.Plaza[cell].Occupant;
            if (buddy == null) return;   // an empty tile does nothing
            MoveResult result = _session.TapPlaza(cell);
            if (result.Accepted) Apply(result);
            else Refuse(result.Reject, FloorGroup(buddy.Color));
        }

        // The armed Remove on a buddy: the squad forms and clears; refused, the buddy winces and Remove stays armed.
        private void RemoveOn(int buddyId)
        {
            MoveResult r = _session.Remove(buddyId);
            if (r == null || !r.Accepted)
            {
                _view.WinceBuddy(buddyId);
                return;
            }

            _removePicking = false;
            RemovePickEnded?.Invoke();
            Apply(r, ReplayKind.Remove);
        }

        /// <summary>The buddies a tap on this tube moves: its front run.</summary>
        private List<int> FrontGroup(int tube)
        {
            var ids = new List<int>();
            Tube t = _session.Board.Tubes[tube];
            int run = t.IsActive ? t.TopRunLength() : 0;
            for (int slot = t.Count - run; slot < t.Count; slot++) ids.Add(t.Slots[slot].Id);
            return ids;
        }

        /// <summary>The buddies a tap on a floor buddy moves: every floor buddy of its colour.</summary>
        private List<int> FloorGroup(SquadColor color)
        {
            var ids = new List<int>();
            foreach (PlazaCell c in _session.Board.Plaza)
                if (!c.IsFree && c.Occupant.Color == color) ids.Add(c.Occupant.Id);
            return ids;
        }

        /// <summary>The tap was refused: the floor flashes when it is full, otherwise the group winces.</summary>
        private void Refuse(RejectReason reason, List<int> group)
        {
            if (reason == RejectReason.NoSpace && group.Count > 0) _view.PlayRejected(group[0]);
            else
                foreach (int id in group)
                    _view.WinceBuddy(id);
        }

        // ------------------------------------------------------------------ replay

        private void Apply(MoveResult result, ReplayKind kind = ReplayKind.Move)
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
            Replayed?.Invoke(result, kind, settled, cleared);

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

    /// <summary>What a replayed result came from: a player's move, or a booster.</summary>
    public enum ReplayKind { Move, Shuffle, Remove }
}
