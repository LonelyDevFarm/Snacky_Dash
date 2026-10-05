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

        // Bridge cells -> true when the deck runs left-right (BridgeLR), false for up-down (BridgeUD).
        private readonly Dictionary<Vector2Int, bool> _bridges = new Dictionary<Vector2Int, bool>();

        /// <summary>
        /// Bridge cells (value: deck runs left-right). On a bridge the two roads do not meet: the snake
        /// only goes straight on, never stops or turns, and the deck and the road under it never touch
        /// [user, tested on the original].
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, bool> Bridges => _bridges;

        public bool IsBridge(Vector2Int cell) => _bridges.ContainsKey(cell);

        // Fixed one-way gates (TileCov_Arrow_Fixed, ldf layer over a straight road) -> the way through.
        private readonly Dictionary<Vector2Int, Direction> _oneWay = new Dictionary<Vector2Int, Direction>();

        /// <summary>
        /// Fixed arrow gates: passable only in the arrow's direction, a wall the other way [user].
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, Direction> OneWayGates => _oneWay;

        // Tunnel mouths (ldf layer): the side their road comes from, and the other mouth of the pair.
        private readonly Dictionary<Vector2Int, Direction> _tunnelSide = new Dictionary<Vector2Int, Direction>();
        private readonly Dictionary<Vector2Int, Vector2Int> _tunnelOther = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>
        /// Tunnel mouths: a road split in two [user]. The snake runs into one mouth from its road,
        /// comes out of the other mouth of the pair (same tunnelIndex) onto that mouth's road and runs on.
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, Direction> TunnelMouths => _tunnelSide;

        /// <summary>
        /// True when a snake heading <paramref name="heading"/> on mouth <paramref name="cell"/> goes into
        /// the tunnel; it comes out on <paramref name="exit"/> heading <paramref name="exitHeading"/>.
        /// </summary>
        public bool TryTunnel(Vector2Int cell, Direction heading, out Vector2Int exit, out Direction exitHeading)
        {
            exit = cell;
            exitHeading = heading;
            if (!_tunnelSide.TryGetValue(cell, out var side) || heading != side.Opposite()) return false;
            if (!_tunnelOther.TryGetValue(cell, out exit)) return false;
            exitHeading = _tunnelSide[exit];
            return true;
        }

        // Destructible walls (rocks): rows of wall cells on the road, closed until their group breaks.
        private readonly List<List<Vector2Int>> _wallGroups = new List<List<Vector2Int>>();
        private readonly HashSet<Vector2Int> _closedWalls = new HashSet<Vector2Int>();

        /// <summary>Rock rows (destructible_walls_H/V ends, MIDDLE inside), each a connected group of cells.</summary>
        public IReadOnlyList<IReadOnlyList<Vector2Int>> WallGroups => _wallGroups;

        public bool IsClosedWall(Vector2Int cell) => _closedWalls.Contains(cell);

        /// <summary>The group's rocks are gone: the road under them opens.</summary>
        public void OpenWall(int group)
        {
            foreach (var cell in _wallGroups[group]) _closedWalls.Remove(cell);
        }

        // Secret fruit [user: a fruit whose kind shows once eaten]: ldfN cells marked SecretCollectible
        // make the fruit wave N puts there secret.
        private readonly Dictionary<int, HashSet<Vector2Int>> _secretCells = new Dictionary<int, HashSet<Vector2Int>>();

        /// <summary>True when the fruit wave <paramref name="wave"/> (0-based) spawns on <paramref name="cell"/> is secret.</summary>
        public bool IsSecretSpawn(int wave, Vector2Int cell) =>
            _secretCells.TryGetValue(wave, out var cells) && cells.Contains(cell);

        // Flipping arrows (TileCov_Arrow): gates whose way turns round, in pairs.
        private readonly Dictionary<Vector2Int, int> _flipGroup = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _flipPartner = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>
        /// Flipping arrows, paired like tunnel mouths [user]: a gate like the fixed ones, but once the
        /// head has gone in through one arrow of a pair and out through the other, both turn round.
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, int> FlippingArrows => _flipGroup;

        public bool TryGetFlipPartner(Vector2Int arrow, out Vector2Int partner) => _flipPartner.TryGetValue(arrow, out partner);

        /// <summary>Turns a flipping arrow and its partner round.</summary>
        public void FlipPair(Vector2Int arrow)
        {
            if (_oneWay.TryGetValue(arrow, out var way)) _oneWay[arrow] = way.Opposite();
            if (_flipPartner.TryGetValue(arrow, out var partner) && _oneWay.TryGetValue(partner, out var other))
                _oneWay[partner] = other.Opposite();
        }

        /// <summary>Half the length of the bridge deck along its road (TileCov_Bridge_LR mesh: 4.3 cells).</summary>
        public const float BridgeDeckHalfLength = 2.15f;

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

                // Bridges sit on the ldf layer above a crossing ("path_Cross" Stop) of the map layer.
                if (map.GetLocalTileId("ldf", position) is int ldf && ldf >= 0 && database.TryGet(ldf, out var ldfDef)
                    && (ldfDef.tileType == TileType.BridgeLR || ldfDef.tileType == TileType.BridgeUD))
                    _bridges[position] = ldfDef.tileType == TileType.BridgeLR;
                if (map.GetLocalTileId("ldf", position) is int gate && gate >= 0 && database.TryGet(gate, out var gateDef))
                {
                    if (FixedArrowDirection(gateDef.tileType) is Direction way) _oneWay[position] = way;
                    if (FlippingArrowDirection(gateDef.tileType) is Direction flipWay)
                    {
                        _oneWay[position] = flipWay;
                        _flipGroup[position] = gateDef.arrowGroupId;
                    }
                }
            }

            // Rocks: connected wall cells of the ldf layer form one group.
            var walls = new HashSet<Vector2Int>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Vector2Int(x, y);
                if (map.GetLocalTileId("ldf", position) is int id && id >= 0 && database.TryGet(id, out var definition)
                    && definition.tileType == TileType.Wall) walls.Add(position);
            }
            var grouped = new HashSet<Vector2Int>();
            foreach (var start in walls)
            {
                if (!grouped.Add(start)) continue;
                var group = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    group.Add(cell);
                    foreach (var direction in DirectionExtensions.All)
                    {
                        var next = cell + direction.ToCell();
                        if (walls.Contains(next) && grouped.Add(next)) queue.Enqueue(next);
                    }
                }
                _wallGroups.Add(group);
                foreach (var cell in group) _closedWalls.Add(cell);
            }

            // Secret fruit marks: layers ldf1, ldf2... go with spawn1, spawn2...
            for (int wave = 0; map.HasLayer("ldf" + (wave + 1)); wave++)
            {
                var cells = new HashSet<Vector2Int>();
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (map.GetLocalTileId("ldf" + (wave + 1), position) is int id && id >= 0 && database.TryGet(id, out var definition)
                        && definition.tileType == TileType.SecretCollectible) cells.Add(position);
                }
                _secretCells[wave] = cells;
            }

            // Flipping arrows come in pairs sharing arrowGroupId.
            foreach (var a in _flipGroup)
            foreach (var b in _flipGroup)
                if (a.Key != b.Key && a.Value == b.Value) _flipPartner[a.Key] = b.Key;

            // Tunnel mouths, paired by tunnelIndex. tunnelDirection is the side the road joins
            // (0 up, 1 down, 2 left, 3 right: path_Tunnel_Down_A has its road below).
            var mouthsByIndex = new Dictionary<int, List<Vector2Int>>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Vector2Int(x, y);
                if (!(map.GetLocalTileId("ldf", position) is int id) || id < 0 || !database.TryGet(id, out var definition)
                    || definition.tileType != TileType.Tunnel) continue;
                _tunnelSide[position] = TunnelSide(definition.tunnelDirection);
                _cells[position] = new Cell { type = TileType.Tunnel, crateColumn = -1 };
                if (!mouthsByIndex.TryGetValue(definition.tunnelIndex, out var list)) mouthsByIndex[definition.tunnelIndex] = list = new List<Vector2Int>();
                list.Add(position);
            }
            foreach (var pair in mouthsByIndex.Values)
                if (pair.Count == 2)
                {
                    _tunnelOther[pair[0]] = pair[1];
                    _tunnelOther[pair[1]] = pair[0];
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

            // No fruit on or under a bridge [user]: Level 5 marks exactly these cells non-spawn (2 cells
            // each way along the deck, 1 each way along the road under it); other levels leave it implied.
            foreach (var bridge in _bridges)
            {
                var along = bridge.Value ? Vector2Int.right : Vector2Int.up;
                var across = bridge.Value ? Vector2Int.up : Vector2Int.right;
                for (int i = -2; i <= 2; i++) excluded.Add(bridge.Key + along * i);
                excluded.Add(bridge.Key + across);
                excluded.Add(bridge.Key - across);
            }

            // No fruit on any object standing on the road (bridges, gates, later rocks, pipes...): they
            // all sit on the "ldf" layer [user]. Nor right next to one: gate doors reach over the
            // neighbouring cells (Level 10 play-test).
            foreach (var cell in _cells.Keys)
            {
                if (!HasRoadObject(map, cell)) continue;
                excluded.Add(cell);
                foreach (var direction in DirectionExtensions.All) excluded.Add(cell + direction.ToCell());
            }

            // PillSpawnPoint (277) is in the PrioritySpawnPoint category: those cells are filled first,
            // they do not restrict the slots (Level 6 has only 2 of them for waves of ~30 fruit).
            // The no-fruit rules win over them too: some levels (24, 27, 30) put spawn points on or
            // next to an arrow or a bridge.
            spawnPoints.ExceptWith(excluded);
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

        /// <summary>True when an object of the "ldf" (mechanics) layer stands on <paramref name="cell"/>.</summary>
        public static bool HasRoadObject(TiledMap map, Vector2Int cell) =>
            map.HasLayer("ldf") && map.GetLocalTileId("ldf", cell) >= 0;

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

        /// <summary>Stop squares and crate stops; the crossing under a bridge is not a stop (no way to turn there). Tunnel mouths never are.</summary>
        public bool IsStop(Vector2Int cell) =>
            _cells.TryGetValue(cell, out var c) && (c.type == TileType.Stop || IsCrateStop(c.type)) && !IsBridge(cell);

        /// <summary>Crate column delivered from this cell, or -1.</summary>
        public int CrateColumnAt(Vector2Int cell) => _cells.TryGetValue(cell, out var c) ? c.crateColumn : -1;

        public IReadOnlyDictionary<int, Vector2Int> CrateStops => _crateStops;

        /// <summary>Delivery end-point cell (where the crate stands) per crate column.</summary>
        public IReadOnlyDictionary<int, Vector2Int> CrateAnchors => _crateAnchors;

        public bool CanMove(Vector2Int from, Direction direction)
        {
            var to = from + direction.ToCell();
            return _cells.TryGetValue(from, out var a) && _cells.TryGetValue(to, out var b)
                   && Allows(a.type, direction) && Allows(b.type, direction.Opposite())
                   && PassesGate(from, direction) && PassesGate(to, direction)
                   && MouthOpens(from, direction) && MouthOpens(to, direction.Opposite())
                   && !_closedWalls.Contains(from) && !_closedWalls.Contains(to);
        }

        /// <summary>Like <see cref="CanMove"/> but ignoring arrow gates: is there road that way at all?</summary>
        public bool HasRoad(Vector2Int from, Direction direction)
        {
            var to = from + direction.ToCell();
            return _cells.TryGetValue(from, out var a) && _cells.TryGetValue(to, out var b)
                   && Allows(a.type, direction) && Allows(b.type, direction.Opposite())
                   && MouthOpens(from, direction) && MouthOpens(to, direction.Opposite());
        }

        public bool IsCrateStopCell(Vector2Int cell) => _cells.TryGetValue(cell, out var c) && IsCrateStop(c.type);

        /// <summary>A tunnel mouth only joins the road on its own side (the way in is TryTunnel).</summary>
        private bool MouthOpens(Vector2Int cell, Direction towards) =>
            !_tunnelSide.TryGetValue(cell, out var side) || side == towards;

        private static Direction? FlippingArrowDirection(TileType type)
        {
            switch (type)
            {
                case TileType.FlippingArrowLeft: return Direction.Left;
                case TileType.FlippingArrowRight: return Direction.Right;
                case TileType.FlippingArrowDown: return Direction.Down;
                case TileType.FlippingArrowUp: return Direction.Up;
                default: return null;
            }
        }

        private static Direction TunnelSide(int tunnelDirection)
        {
            switch (tunnelDirection)
            {
                case 1: return Direction.Down;
                case 2: return Direction.Left;
                case 3: return Direction.Right;
                default: return Direction.Up;
            }
        }

        private bool PassesGate(Vector2Int cell, Direction direction) =>
            !_oneWay.TryGetValue(cell, out var way) || way == direction;

        private static Direction? FixedArrowDirection(TileType type)
        {
            switch (type)
            {
                case TileType.FixedArrowLeft: return Direction.Left;
                case TileType.FixedArrowRight: return Direction.Right;
                case TileType.FixedArrowDown: return Direction.Down;
                case TileType.FixedArrowUp: return Direction.Up;
                default: return null;
            }
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
