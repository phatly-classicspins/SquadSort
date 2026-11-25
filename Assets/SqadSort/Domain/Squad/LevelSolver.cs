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
            public readonly HashSet<int> TriedFloorMoves = new HashSet<int>();   // colour × destination: one per pair
            public int Next;
        }

        /// <summary>Depth-first, on an explicit stack — a long level's path is deeper than the thread's call stack.
        /// The moves are the two-tap control's (Squad Sort GDD §1.2): a container's front group into every container that
        /// accepts it, a floor colour group into every container that accepts it (one try per colour and container — every cell
        /// of a colour does the same), then a container's front group out onto the floor (where it lands changes no rule).</summary>
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
                int cells = board.Plaza.Count;
                int tubeToTube = tubes * tubes, floorToTube = cells * tubes;
                int moves = tubeToTube + floorToTube + tubes;
                bool advanced = false;

                while (here.Next < moves && !advanced)
                {
                    int m = here.Next++;
                    bool accepted;
                    if (m < tubeToTube)
                    {
                        int from = m / tubes, to = m % tubes;
                        if (from == to || !board.CanPickTube(from) || !board.Accepts(board.Tubes[to], board.Tubes[from].Top.Color)) continue;
                        accepted = board.MoveTubeToTube(from, to).Accepted;
                    }
                    else if (m < tubeToTube + floorToTube)
                    {
                        int cell = (m - tubeToTube) / tubes, to = (m - tubeToTube) % tubes;
                        Buddy b = board.Plaza[cell].Occupant;
                        if (b == null || !board.Accepts(board.Tubes[to], b.Color) || !here.TriedFloorMoves.Add((int)b.Color * tubes + to)) continue;
                        accepted = board.MovePlazaToTube(cell, to).Accepted;
                    }
                    else
                    {
                        int from = m - tubeToTube - floorToTube;
                        if (!board.CanPickTube(from) || board.PlazaFree == 0) continue;
                        int free = 0;
                        while (!board.Plaza[free].IsFree) free++;
                        accepted = board.MoveTubeToPlaza(from, free).Accepted;
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
