using System.Collections.Generic;

namespace Game.Domain
{
    public enum BoardState : byte
    {
        Playing,

        /// <summary>The plaza filled up; play resumes only after a cell is added (GDD §2.4).</summary>
        OutOfSpace,

        Won,
    }

    public enum RejectReason : byte
    {
        None,

        /// <summary>The target is not a revealed buddy in an active tube or on the plaza.</summary>
        NotTappable,

        /// <summary>No tube can take the group and the plaza is full.</summary>
        NoSpace,

        /// <summary>A plaza buddy has no tube to go to.</summary>
        NoDestination,

        /// <summary>The board is not in <see cref="BoardState.Playing"/>.</summary>
        NotPlaying,
    }

    public readonly struct BuddyMove
    {
        public readonly int BuddyId;
        public readonly BuddyLocation From;
        public readonly BuddyLocation To;

        public BuddyMove(int buddyId, BuddyLocation from, BuddyLocation to)
        {
            BuddyId = buddyId;
            From = from;
            To = to;
        }
    }

    /// <summary>
    /// Everything one action changed, in order, so a View can animate it without re-deriving rules:
    /// moves first, then squads cleared, houses opened, buddies revealed.
    /// </summary>
    public sealed class MoveResult
    {
        private static readonly IReadOnlyList<BuddyMove> NoMoves = new BuddyMove[0];
        private static readonly IReadOnlyList<int> NoInts = new int[0];

        internal MoveResult(RejectReason reject, BoardState state)
        {
            Reject = reject;
            State = state;
            Moves = NoMoves;
            ClearedTubes = NoInts;
            OpenedHouses = NoInts;
            RevealedBuddies = NoInts;
        }

        internal MoveResult(List<BuddyMove> moves, List<int> cleared, List<int> opened, List<int> revealed, BoardState state)
        {
            Reject = RejectReason.None;
            Moves = moves;
            ClearedTubes = cleared;
            OpenedHouses = opened;
            RevealedBuddies = revealed;
            State = state;
        }

        public bool Accepted => Reject == RejectReason.None;
        public RejectReason Reject { get; }
        public IReadOnlyList<BuddyMove> Moves { get; }

        /// <summary>Tubes whose squad completed and left the board.</summary>
        public IReadOnlyList<int> ClearedTubes { get; }

        /// <summary>House tubes that opened because a squad of their colour completed.</summary>
        public IReadOnlyList<int> OpenedHouses { get; }

        /// <summary>Hidden buddies that became visible.</summary>
        public IReadOnlyList<int> RevealedBuddies { get; }

        public BoardState State { get; }
    }
}
