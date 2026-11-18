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
            int depth = Search(board, visited, maxStates, 0);
            bool exhausted = depth < 0 && visited.Count >= maxStates;
            return new Solution(depth >= 0, depth, visited.Count, exhausted);
        }

        /// <returns>Moves to win from here, or -1.</returns>
        private static int Search(Board board, HashSet<string> visited, int maxStates, int depth)
        {
            if (board.State == BoardState.Won) return depth;
            if (board.State != BoardState.Playing) return -1;
            if (visited.Count >= maxStates || !visited.Add(Key(board))) return -1;

            BoardSnapshot here = board.Snapshot();

            for (int t = 0; t < board.Tubes.Count; t++)
            {
                if (board.TapTube(t).Accepted)
                {
                    int found = Search(board, visited, maxStates, depth + 1);
                    if (found >= 0) return found;
                    board.Restore(here);
                }
            }

            var triedColours = new HashSet<SquadColor>();
            for (int c = 0; c < board.Plaza.Count; c++)
            {
                Buddy b = board.Plaza[c].Occupant;
                if (b == null || !triedColours.Add(b.Color)) continue; // every cell of a colour does the same
                if (board.TapPlaza(c).Accepted)
                {
                    int found = Search(board, visited, maxStates, depth + 1);
                    if (found >= 0) return found;
                    board.Restore(here);
                }
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
