using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Walkable road network of a level, read from the "map" layer. Two cells are connected when both
    /// are walkable and each allows the shared direction: straight roads only allow their own axis,
    /// every other walkable tile (corners, crossings, stops) opens towards any walkable neighbour.
    /// </summary>
    public sealed class BoardGraph
    {
        private struct Cell
        {
            public TileType type;
            public int crateColumn;
        }

        private readonly Dictionary<Vector2Int, Cell> _cells = new Dictionary<Vector2Int, Cell>();
        private readonly Dictionary<int, Vector2Int> _crateStops = new Dictionary<int, Vector2Int>();
        private readonly Dictionary<int, Vector2Int> _crateAnchors = new Dictionary<int, Vector2Int>();

        public Vector2Int PlayerStart { get; }

        public BoardGraph(TiledMap map, TileDefinitionDatabase database)
        {
            var endPoints = new List<Vector2Int>();
            PlayerStart = new Vector2Int(-1, -1);

            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Vector2Int(x, y);
                if (map.GetLocalTileId("spawn", position) is int start && start >= 0
                    && database.TryGet(start, out var startDef) && startDef.tileType == TileType.PlayerStartPosition)
                    PlayerStart = position;

                int id = map.GetLocalTileId("map", position);
                if (id < 0 || !database.TryGet(id, out var definition)) continue;
                if (definition.category == TileCategory.CrateEndPoint) endPoints.Add(position);
                if (!IsWalkableType(definition.tileType)) continue;

                int column = IsCrateStop(definition.tileType) ? definition.deliverPointIndex : -1;
                _cells[position] = new Cell { type = definition.tileType, crateColumn = column };
                if (column >= 0) _crateStops[column] = position;
            }

            // The crate stands on the delivery end point next to its stop cell.
            foreach (var stop in _crateStops)
            foreach (var end in endPoints)
                if ((end - stop.Value).sqrMagnitude == 1) _crateAnchors[stop.Key] = end;

            PillSlotGroups = BuildPillSlotGroups(map, database);
        }

        /// <summary>
        /// Cells fruit may spawn on in levels without fixed spawn1..N layers (RespawnScheduler
        /// "map pill" slots): every road except stops, NoPill roads, NonPillSpawnPlace cells and the
        /// start; PillSpawnPoint cells are priority slots (see <see cref="PrioritySlots"/>).
        /// Split into segments (FloodFillGroupBuilder groups): straight runs of slots joined by road,
        /// broken at excluded cells, junctions and corners, each listed from one end to the other.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Vector2Int>> PillSlotGroups { get; }

        /// <summary>PillSpawnPoint cells (category PrioritySpawnPoint): their segments are filled first.</summary>
        public IReadOnlyCollection<Vector2Int> PrioritySlots { get; private set; } = new HashSet<Vector2Int>();

        private List<IReadOnlyList<Vector2Int>> BuildPillSlotGroups(TiledMap map, TileDefinitionDatabase database)
        {
            var spawnPoints = new HashSet<Vector2Int>();
            var excluded = new HashSet<Vector2Int> { PlayerStart };
            foreach (var cell in _cells.Keys)
            {
                if (map.GetLocalTileId("spawn", cell) is int id && id >= 0 && database.TryGet(id, out var definition))
                {
                    if (definition.tileType == TileType.PillSpawnPoint) spawnPoints.Add(cell);
                    if (definition.tileType == TileType.NonPillSpawnPlace) excluded.Add(cell);
                }
            }

            // PillSpawnPoint (277) is in the PrioritySpawnPoint category: those cells are filled first,
            // they do not restrict the slots (Level 6 has only 2 of them for waves of ~30 fruit).
            PrioritySlots = spawnPoints;
            var slots = new HashSet<Vector2Int>(spawnPoints);
            foreach (var pair in _cells)
                if (!excluded.Contains(pair.Key) && IsPillRoad(pair.Value.type)) slots.Add(pair.Key);

            // Junctions inside the slot set would branch a segment: leave them out.
            var junctions = new List<Vector2Int>();
            foreach (var cell in slots)
                if (SlotNeighbours(cell, slots).Count >= 3) junctions.Add(cell);
            foreach (var cell in junctions) slots.Remove(cell);

            var ordered = new List<Vector2Int>(slots);
            ordered.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x)); // stable order
            var segments = new List<IReadOnlyList<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();
            // Open runs from an end first, then whatever is left (closed loops) from any cell.
            foreach (var start in ordered)
                if (!visited.Contains(start) && SlotNeighbours(start, slots).Count <= 1)
                    segments.Add(Walk(start, slots, visited));
            foreach (var start in ordered)
                if (!visited.Contains(start))
                    segments.Add(Walk(start, slots, visited));

            // Footage: a run of one kind never turns a corner, so segments are straight. Cut at
            // every change of direction (the corner cell ends the run it comes from).
            var straight = new List<IReadOnlyList<Vector2Int>>();
            foreach (var segment in segments)
            {
                var run = new List<Vector2Int> { segment[0] };
                for (int i = 1; i < segment.Count; i++)
                {
                    bool turns = run.Count >= 2 && segment[i] - segment[i - 1] != segment[i - 1] - segment[i - 2];
                    if (turns) { straight.Add(run); run = new List<Vector2Int>(); }
                    run.Add(segment[i]);
                }
                straight.Add(run);
            }
            return straight;
        }

        /// <summary>Follows a run of slots from <paramref name="start"/> along the road.</summary>
        private List<Vector2Int> Walk(Vector2Int start, HashSet<Vector2Int> slots, HashSet<Vector2Int> visited)
        {
            var run = new List<Vector2Int>();
            Vector2Int? cell = start;
            while (cell.HasValue && visited.Add(cell.Value))
            {
                run.Add(cell.Value);
                Vector2Int? next = null;
                foreach (var neighbour in SlotNeighbours(cell.Value, slots))
                    if (!visited.Contains(neighbour)) { next = neighbour; break; }
                cell = next;
            }
            return run;
        }

        /// <summary>Slots connected to <paramref name="cell"/> by road.</summary>
        private List<Vector2Int> SlotNeighbours(Vector2Int cell, HashSet<Vector2Int> slots)
        {
            var neighbours = new List<Vector2Int>(4);
            foreach (var direction in DirectionExtensions.All)
            {
                var next = cell + direction.ToCell();
                if (slots.Contains(next) && CanMove(cell, direction)) neighbours.Add(next);
            }
            return neighbours;
        }

        private static bool IsPillRoad(TileType type) =>
            type != TileType.Stop && !IsCrateStop(type)
            && type != TileType.RoadRightLeftNoPill && type != TileType.RoadUpDownNoPill;

        public bool IsWalkable(Vector2Int cell) => _cells.ContainsKey(cell);

        public bool IsStop(Vector2Int cell) => _cells.TryGetValue(cell, out var c) && (c.type == TileType.Stop || IsCrateStop(c.type));

        /// <summary>Crate column delivered from this cell, or -1.</summary>
        public int CrateColumnAt(Vector2Int cell) => _cells.TryGetValue(cell, out var c) ? c.crateColumn : -1;

        public IReadOnlyDictionary<int, Vector2Int> CrateStops => _crateStops;

        /// <summary>Delivery end-point cell (where the crate stands) per crate column.</summary>
        public IReadOnlyDictionary<int, Vector2Int> CrateAnchors => _crateAnchors;

        public bool CanMove(Vector2Int from, Direction direction)
        {
            var to = from + direction.ToCell();
            return _cells.TryGetValue(from, out var a) && _cells.TryGetValue(to, out var b)
                   && Allows(a.type, direction) && Allows(b.type, direction.Opposite());
        }

        public List<Direction> Exits(Vector2Int cell)
        {
            var exits = new List<Direction>(4);
            foreach (var direction in DirectionExtensions.All)
                if (CanMove(cell, direction)) exits.Add(direction);
            return exits;
        }

        private static bool Allows(TileType type, Direction direction)
        {
            switch (type)
            {
                case TileType.RoadRightLeft:
                case TileType.RoadRightLeftNoPill:
                    return direction.IsHorizontal();
                case TileType.RoadUpDown:
                case TileType.RoadUpDownNoPill:
                    return !direction.IsHorizontal();
                default:
                    return true;
            }
        }

        private static bool IsWalkableType(TileType type)
        {
            switch (type)
            {
                case TileType.CornerUpRight:
                case TileType.CornerDownRight:
                case TileType.CornerUpLeft:
                case TileType.CornerDownLeft:
                case TileType.RoadRightLeft:
                case TileType.RoadUpDown:
                case TileType.RoadRightLeftNoPill:
                case TileType.RoadUpDownNoPill:
                case TileType.CrossRoad:
                case TileType.Stop:
                    return true;
                default:
                    return IsCrateStop(type);
            }
        }

        private static bool IsCrateStop(TileType type) =>
            type == TileType.StopCrate || (type >= TileType.StopCrate0 && type <= TileType.StopCrate2)
                                       || (type >= TileType.StopCrate3 && type <= TileType.StopCrate6);
    }
}
