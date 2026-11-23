using System.Collections.Generic;
using System.Text;

namespace Game.Domain
{
    /// <summary>
    /// Proves a level winnable without boosters (GDD §4.2: every level must be solvable before it ships).
    /// Depth-first search with a visited set; it reads hidden colours, which is fine for a proof.
    /// Out of Space counts as a dead end. Engine-free so the headless gate runs it on every level.
    /// </summary>
    public static class LevelSolver
    {
        public sealed class Solution
        {
            internal Solution(bool solvable, int moves, int statesVisited, bool exhausted)
            {
                Solvable = solvable;
                Moves = moves;
                StatesVisited = statesVisited;
                Exhausted = exhausted;
            }

            public bool Solvable { get; }

            /// <summary>Taps in the first solution found (not necessarily the shortest).</summary>
            public int Moves { get; }

            public int StatesVisited { get; }

            /// <summary>True when the search hit <c>maxStates</c> before deciding.</summary>
            public bool Exhausted { get; }
        }

        public static Solution Solve(LevelDefinition level, int maxStates = 200000)
        {
            var board = new Board(level);
            var visited = new HashSet<string>();
            int depth = Search(board, visited, maxStates);
            bool exhausted = depth < 0 && visited.Count >= maxStates;
            return new Solution(depth >= 0, depth, visited.Count, exhausted);
        }

        /// <summary>One state on the search path: where to come back to, and which move to try next.</summary>
        private sealed class Frame
        {
            public Frame(BoardSnapshot snapshot) => Snapshot = snapshot;

            public readonly BoardSnapshot Snapshot;
            public readonly HashSet<SquadColor> TriedColours = new HashSet<SquadColor>();
            public int Next;
        }

        /// <summary>Depth-first, on an explicit stack — a long level's path is deeper than the thread's call stack.
        /// Moves are tried tube by tube, then one plaza buddy per colour (every cell of a colour does the same).</summary>
        /// <returns>Moves to win, or -1.</returns>
        private static int Search(Board board, HashSet<string> visited, int maxStates)
        {
            if (board.State == BoardState.Won) return 0;
            if (board.State != BoardState.Playing || !visited.Add(Key(board))) return -1;

            var path = new Stack<Frame>();
            path.Push(new Frame(board.Snapshot()));
            while (path.Count > 0)
            {
                Frame here = path.Peek();
                board.Restore(here.Snapshot);
                int tubes = board.Tubes.Count;
                int moves = tubes + board.Plaza.Count;
                bool advanced = false;

                while (here.Next < moves && !advanced)
                {
                    int m = here.Next++;
                    bool accepted;
                    if (m < tubes)
                    {
                        accepted = board.TapTube(m).Accepted;
                    }
                    else
                    {
                        Buddy b = board.Plaza[m - tubes].Occupant;
                        if (b == null || !here.TriedColours.Add(b.Color)) continue;
                        accepted = board.TapPlaza(m - tubes).Accepted;
                    }

                    if (!accepted) continue;
                    if (board.State == BoardState.Won) return path.Count;
                    if (board.State == BoardState.Playing && visited.Count < maxStates && visited.Add(Key(board)))
                    {
                        path.Push(new Frame(board.Snapshot()));
                        advanced = true;
                    }
                    else
                    {
                        board.Restore(here.Snapshot);
                    }
                }

                if (!advanced) path.Pop();
            }

            return -1;
        }

        private static string Key(Board board)
        {
            var sb = new StringBuilder();
            foreach (Tube t in board.Tubes)
            {
                sb.Append(t.Cleared ? 'x' : t.Locked ? 'L' : '|');
                foreach (Buddy b in t.Slots)
                {
                    if (b.Hidden) sb.Append('?');
                    sb.Append(SquadColorCodes.ToLetter(b.Color));
                }
            }

            sb.Append('/');
            foreach (PlazaCell c in board.Plaza) sb.Append(c.IsFree ? '.' : SquadColorCodes.ToLetter(c.Occupant.Color));
            return sb.ToString();
        }
    }
}
