using System;
using System.Collections.Generic;

namespace Game.Domain
{
    /// <summary>Presentation route over the authored waiting grid. Occupancy and move legality remain Board's rules.</summary>
    public static class BoardWalkRoute
    {
        private static readonly TubeDirection[] Directions =
            { TubeDirection.Up, TubeDirection.Right, TubeDirection.Down, TubeDirection.Left };

        /// <summary>Includes both endpoints. Tubes connect to the plaza only through their mouth, never a side wall.</summary>
        public static IReadOnlyList<GridPos> Build(Board board, BuddyLocation from, BuddyLocation to)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            var route = new List<GridPos>();
            if (from.Kind == LocationKind.Tube && to.Kind == LocationKind.Tube && from.Index == to.Index)
            {
                Tube tube = board.Tubes[from.Index];
                int step = from.Slot < to.Slot ? 1 : -1;
                for (int slot = from.Slot; ; slot += step)
                {
                    route.Add(tube.CellOf(slot));
                    if (slot == to.Slot) return route;
                }
            }

            GridPos start;
            if (from.Kind == LocationKind.Tube)
            {
                Tube tube = board.Tubes[from.Index];
                for (int slot = from.Slot; slot < tube.Capacity; slot++) route.Add(tube.CellOf(slot));
                start = tube.Mouth.Step(tube.Direction, -1);
            }
            else start = board.Plaza[from.Index].Pos;

            GridPos end = to.Kind == LocationKind.Tube
                ? board.Tubes[to.Index].Mouth.Step(board.Tubes[to.Index].Direction, -1)
                : board.Plaza[to.Index].Pos;
            var cells = new HashSet<GridPos>();
            foreach (PlazaCell cell in board.Plaza) cells.Add(cell.Pos);
            var queue = new Queue<GridPos>();
            var previous = new Dictionary<GridPos, GridPos>();
            if (!cells.Contains(start) || !cells.Contains(end))
                throw new InvalidOperationException($"Tube port is not on the waiting grid: {start} -> {end}.");
            previous.Add(start, start);
            queue.Enqueue(start);
            while (queue.Count > 0 && !previous.ContainsKey(end))
            {
                GridPos cell = queue.Dequeue();
                foreach (TubeDirection direction in Directions)
                {
                    GridPos next = cell.Step(direction, 1);
                    if (!cells.Contains(next) || previous.ContainsKey(next)) continue;
                    previous.Add(next, cell);
                    queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(end))
                throw new InvalidOperationException($"Waiting grid is disconnected: {start} -> {end}.");
            var plazaPath = new List<GridPos>();
            for (GridPos at = end; ; at = previous[at])
            {
                plazaPath.Add(at);
                if (at.Equals(start)) break;
            }
            for (int i = plazaPath.Count - 1; i >= 0; i--) route.Add(plazaPath[i]);
            if (to.Kind == LocationKind.Tube)
            {
                Tube tube = board.Tubes[to.Index];
                for (int slot = tube.Capacity - 1; slot >= to.Slot; slot--) route.Add(tube.CellOf(slot));
            }
            return route;
        }
    }
}
