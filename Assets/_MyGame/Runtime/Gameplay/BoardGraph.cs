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
        }

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
