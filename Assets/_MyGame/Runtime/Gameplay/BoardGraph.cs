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
        /// "map pill" respawn): the PillSpawnPoint cells of the spawn layer when the level has any,
        /// otherwise every road except stops, NoPill roads, NonPillSpawnPlace cells and the start.
        /// Grouped by flood fill like FloodFillGroupBuilder; each group is in path order from one end.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Vector2Int>> PillSlotGroups { get; }

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

            var slots = new HashSet<Vector2Int>(spawnPoints);
            if (slots.Count == 0)
                foreach (var pair in _cells)
                    if (!excluded.Contains(pair.Key) && IsPillRoad(pair.Value.type)) slots.Add(pair.Key);

            var ordered = new List<Vector2Int>(slots);
            ordered.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x)); // top-left first
            var groups = new List<IReadOnlyList<Vector2Int>>();
            var assigned = new HashSet<Vector2Int>();
            foreach (var start in ordered)
            {
                if (assigned.Contains(start)) continue;
                var region = Flood(start, slots);
                // Seed at a run end (fewest neighbours in the group) so the order follows the road.
                var seed = region[0];
                foreach (var cell in region)
                    if (SlotNeighbours(cell, slots) < SlotNeighbours(seed, slots)) seed = cell;
                var group = Flood(seed, slots);
                assigned.UnionWith(group);
                groups.Add(group);
            }
            return groups;
        }

        private static List<Vector2Int> Flood(Vector2Int seed, HashSet<Vector2Int> slots)
        {
            var result = new List<Vector2Int>();
            var seen = new HashSet<Vector2Int> { seed };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                result.Add(cell);
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = cell + direction.ToCell();
                    if (slots.Contains(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return result;
        }

        private static int SlotNeighbours(Vector2Int cell, HashSet<Vector2Int> slots)
        {
            int count = 0;
            foreach (var direction in DirectionExtensions.All)
                if (slots.Contains(cell + direction.ToCell())) count++;
            return count;
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
