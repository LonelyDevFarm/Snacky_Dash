using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Mạng đường của màn, đọc từ map Tiled (lớp map, ldf, ldf_above, spawn): ô đi được, điểm dừng, cầu, cổng, đá, rèm, khối nâng.
    /// Lớp C# thuần, không gắn prefab; GameBootstrap tạo, GameSession và các View đọc.
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

        private readonly Dictionary<Vector2Int, bool> _bridges = new Dictionary<Vector2Int, bool>();

        /// <summary>
        /// Ô cầu (true = mặt cầu chạy ngang). Trên cầu rắn chỉ đi thẳng, không dừng, không rẽ;
        /// mặt cầu và đường dưới không chạm nhau [user].
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, bool> Bridges => _bridges;

        public bool IsBridge(Vector2Int cell) => _bridges.ContainsKey(cell);

        private readonly Dictionary<Vector2Int, Direction> _oneWay = new Dictionary<Vector2Int, Direction>();

        /// <summary>Cổng một chiều (TileCov_Arrow_Fixed): chỉ qua theo chiều mũi tên, chiều kia là tường [user].</summary>
        public IReadOnlyDictionary<Vector2Int, Direction> OneWayGates => _oneWay;

        // Miệng đường hầm: phía đường nối vào, và miệng còn lại của cặp.
        private readonly Dictionary<Vector2Int, Direction> _tunnelSide = new Dictionary<Vector2Int, Direction>();
        private readonly Dictionary<Vector2Int, Vector2Int> _tunnelOther = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>Miệng đường hầm: rắn chui vào một miệng, ra ở miệng kia cùng tunnelIndex rồi chạy tiếp [user].</summary>
        public IReadOnlyDictionary<Vector2Int, Direction> TunnelMouths => _tunnelSide;

        /// <summary>True khi rắn đi hướng <paramref name="heading"/> trên miệng <paramref name="cell"/> thì chui vào hầm.</summary>
        public bool TryTunnel(Vector2Int cell, Direction heading, out Vector2Int exit, out Direction exitHeading)
        {
            exit = cell;
            exitHeading = heading;
            if (!_tunnelSide.TryGetValue(cell, out var side) || heading != side.Opposite()) return false;
            if (!_tunnelOther.TryGetValue(cell, out exit)) return false;
            exitHeading = _tunnelSide[exit];
            return true;
        }

        private readonly List<List<Vector2Int>> _wallGroups = new List<List<Vector2Int>>();
        private readonly HashSet<Vector2Int> _closedWalls = new HashSet<Vector2Int>();

        /// <summary>Các hàng đá (destructible_walls_H/V), mỗi nhóm là các ô liền nhau.</summary>
        public IReadOnlyList<IReadOnlyList<Vector2Int>> WallGroups => _wallGroups;

        public bool IsClosedWall(Vector2Int cell) => _closedWalls.Contains(cell);

        /// <summary>Lớp có đá; ldf_below là đá dưới gầm cầu (Level 24, 25, 30, 35, 37, 45, 47, 48).</summary>
        public static readonly string[] WallLayers = { "ldf", "ldf_below" };

        /// <summary>Đá chặn bước đi qua ô; đá dưới gầm cầu chỉ chặn đường dưới, mặt cầu vẫn đi được.</summary>
        public bool WallBlocks(Vector2Int cell, Direction direction) =>
            _closedWalls.Contains(cell)
            && !(_bridges.TryGetValue(cell, out bool leftRight) && (direction == Direction.Left || direction == Direction.Right) == leftRight);

        public void OpenWall(int group)
        {
            foreach (var cell in _wallGroups[group]) _closedWalls.Remove(cell);
        }

        private readonly Dictionary<Vector2Int, int> _curtainGroup = new Dictionary<Vector2Int, int>();
        private readonly HashSet<Vector2Int> _covered = new HashSet<Vector2Int>();

        /// <summary>Mọi ô rèm (ldf_above) với id nhóm; id lấy từ dữ liệu màn (curtainCells).</summary>
        public IReadOnlyDictionary<Vector2Int, int> CurtainCells => _curtainGroup;

        /// <summary>Ô dưới rèm chưa mở: không vào được, không có trái.</summary>
        public bool IsCovered(Vector2Int cell) => _covered.Contains(cell);

        /// <summary>Ô dữ liệu màn không liệt kê giữ nhóm 0.</summary>
        public void SetCurtainGroups(IEnumerable<(Vector2Int cell, int group)> cells)
        {
            foreach (var (cell, group) in cells)
                if (_curtainGroup.ContainsKey(cell)) _curtainGroup[cell] = group;
        }

        public void OpenCurtain(int group)
        {
            foreach (var pair in _curtainGroup)
                if (pair.Value == group) _covered.Remove(pair.Key);
        }

        // Khối nâng (ô Plot lớp ldf): đóng đường khi đang nâng.
        private readonly List<Vector2Int> _risingBlocks = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> _risen = new HashSet<Vector2Int>();

        /// <summary>Ô khối nâng theo thứ tự trên bàn (chỉ số là id khối).</summary>
        public IReadOnlyList<Vector2Int> RisingBlocks => _risingBlocks;

        public bool IsRisen(Vector2Int cell) => _risen.Contains(cell);

        public void SetRisen(Vector2Int cell, bool risen)
        {
            if (risen) _risen.Add(cell);
            else _risen.Remove(cell);
        }

        // Trái bí ẩn [user]: ô SecretCollectible ở lớp ldfN làm trái của đợt N tại đó thành bí ẩn.
        private readonly Dictionary<int, HashSet<Vector2Int>> _secretCells = new Dictionary<int, HashSet<Vector2Int>>();

        /// <summary><paramref name="wave"/> tính từ 0.</summary>
        public bool IsSecretSpawn(int wave, Vector2Int cell) =>
            _secretCells.TryGetValue(wave, out var cells) && cells.Contains(cell);

        private readonly Dictionary<Vector2Int, int> _flipGroup = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _flipPartner = new Dictionary<Vector2Int, Vector2Int>();

        /// <summary>
        /// Mũi tên đổi chiều (TileCov_Arrow), theo cặp như đường hầm [user]: đầu vào qua một mũi tên
        /// và ra qua mũi tên kia thì cả hai quay ngược.
        /// </summary>
        public IReadOnlyDictionary<Vector2Int, int> FlippingArrows => _flipGroup;

        public bool TryGetFlipPartner(Vector2Int arrow, out Vector2Int partner) => _flipPartner.TryGetValue(arrow, out partner);

        public bool TryGetTunnelOther(Vector2Int mouth, out Vector2Int other) => _tunnelOther.TryGetValue(mouth, out other);

        public void FlipPair(Vector2Int arrow)
        {
            if (_oneWay.TryGetValue(arrow, out var way)) _oneWay[arrow] = way.Opposite();
            if (_flipPartner.TryGetValue(arrow, out var partner) && _oneWay.TryGetValue(partner, out var other))
                _oneWay[partner] = other.Opposite();
        }

        /// <summary>Booster hoàn tác cần trả lại: đá, chiều mũi tên, khối nâng.</summary>
        internal (HashSet<Vector2Int> closedWalls, Dictionary<Vector2Int, Direction> ways, HashSet<Vector2Int> risen) SaveState() =>
            (new HashSet<Vector2Int>(_closedWalls), new Dictionary<Vector2Int, Direction>(_oneWay), new HashSet<Vector2Int>(_risen));

        internal void RestoreState((HashSet<Vector2Int> closedWalls, Dictionary<Vector2Int, Direction> ways, HashSet<Vector2Int> risen) state)
        {
            _closedWalls.Clear();
            _closedWalls.UnionWith(state.closedWalls);
            foreach (var way in state.ways) _oneWay[way.Key] = way.Value;
            _risen.Clear();
            _risen.UnionWith(state.risen);
        }

        /// <summary>Nửa chiều dài mặt cầu, tính bằng ô (gốc: mesh TileCov_Bridge_LR dài 4.3 ô).</summary>
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

                // Rèm che cả cỏ lẫn đường: mọi ô rèm ldf_above đều tính.
                if (map.HasLayer("ldf_above") && map.GetLocalTileId("ldf_above", position) is int above && above >= 0
                    && database.TryGet(above, out var aboveDef) && aboveDef.category == TileCategory.Curtain)
                {
                    _curtainGroup[position] = 0;
                    _covered.Add(position);
                }

                int id = map.GetLocalTileId("map", position);
                if (id < 0 || !database.TryGet(id, out var definition)) continue;
                if (definition.category == TileCategory.CrateEndPoint) endPoints.Add(position);
                if (!IsWalkableType(definition.tileType)) continue;

                int column = IsCrateStop(definition.tileType) ? definition.deliverPointIndex : -1;
                _cells[position] = new Cell { type = definition.tileType, crateColumn = column };
                if (column >= 0) _crateStops[column] = position;

                // Cầu nằm ở lớp ldf, trên ô ngã tư (path_Cross) của lớp map.
                if (map.GetLocalTileId("ldf", position) is int ldf && ldf >= 0 && database.TryGet(ldf, out var ldfDef)
                    && (ldfDef.tileType == TileType.BridgeLR || ldfDef.tileType == TileType.BridgeUD))
                    _bridges[position] = ldfDef.tileType == TileType.BridgeLR;
                if (map.GetLocalTileId("ldf", position) is int gate && gate >= 0 && database.TryGet(gate, out var gateDef))
                {
                    if (gateDef.tileType == TileType.Plot) _risingBlocks.Add(position);
                    if (FixedArrowDirection(gateDef.tileType) is Direction way) _oneWay[position] = way;
                    if (FlippingArrowDirection(gateDef.tileType) is Direction flipWay)
                    {
                        _oneWay[position] = flipWay;
                        _flipGroup[position] = gateDef.arrowGroupId;
                    }
                }
            }

            // Đá: các ô tường liền nhau ở lớp ldf / ldf_below (đá dưới gầm cầu) thành một nhóm.
            var walls = new HashSet<Vector2Int>();
            foreach (var layer in WallLayers)
            {
                if (!map.HasLayer(layer)) continue;
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (map.GetLocalTileId(layer, position) is int id && id >= 0 && database.TryGet(id, out var definition)
                        && definition.tileType == TileType.Wall) walls.Add(position);
                }
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

            // Dấu trái bí ẩn: lớp ldf1, ldf2... đi với spawn1, spawn2...
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

            // Mũi tên đổi chiều đi theo cặp cùng arrowGroupId.
            foreach (var a in _flipGroup)
            foreach (var b in _flipGroup)
                if (a.Key != b.Key && a.Value == b.Value) _flipPartner[a.Key] = b.Key;

            // Miệng hầm ghép cặp theo tunnelIndex; tunnelDirection = phía đường nối vào
            // (0 lên, 1 xuống, 2 trái, 3 phải: path_Tunnel_Down_A có đường ở dưới).
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

            // Khay đứng trên ô end point giao hàng cạnh điểm dừng của nó.
            foreach (var stop in _crateStops)
            foreach (var end in endPoints)
                if ((end - stop.Value).sqrMagnitude == 1) _crateAnchors[stop.Key] = end;

            PillSlotGroups = BuildPillSlotGroups(map, database);
        }

        /// <summary>
        /// Ô rải trái cho màn không có lớp spawn1..N cố định (gốc: RespawnScheduler, FloodFillGroupBuilder):
        /// các đoạn đường thẳng, trừ điểm dừng, NoPill, NonPillSpawnPlace, ô xuất phát và quanh vật trên đường.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Vector2Int>> PillSlotGroups { get; }

        /// <summary>Ô PillSpawnPoint (PrioritySpawnPoint): đoạn chứa chúng được rải trước.</summary>
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

            // Không trái trên/dưới cầu [user]: 2 ô mỗi phía dọc mặt cầu, 1 ô mỗi phía đường dưới (như Level 5 đánh dấu).
            foreach (var bridge in _bridges)
            {
                var along = bridge.Value ? Vector2Int.right : Vector2Int.up;
                var across = bridge.Value ? Vector2Int.up : Vector2Int.right;
                for (int i = -2; i <= 2; i++) excluded.Add(bridge.Key + along * i);
                excluded.Add(bridge.Key + across);
                excluded.Add(bridge.Key - across);
            }

            // Không trái trên vật lớp ldf và ô kề nó (cửa cổng che ô bên cạnh, Level 10) [user].
            // Trừ khối nâng: lúc hạ vẫn nhận trái (GameSession loại khi đang nâng) [user].
            foreach (var cell in _cells.Keys)
            {
                if (!HasRoadObject(map, cell) || _risingBlocks.Contains(cell)) continue;
                excluded.Add(cell);
                foreach (var direction in DirectionExtensions.All) excluded.Add(cell + direction.ToCell());
            }

            // PillSpawnPoint chỉ là ô ưu tiên, không giới hạn ô rải (Level 6 chỉ có 2 ô cho ~30 trái).
            // Luật cấm trái vẫn thắng: Level 24, 27, 30 đặt spawn point sát mũi tên/cầu.
            spawnPoints.ExceptWith(excluded);
            PrioritySlots = spawnPoints;
            var slots = new HashSet<Vector2Int>(spawnPoints);
            foreach (var pair in _cells)
                if (!excluded.Contains(pair.Key) && IsPillRoad(pair.Value.type)) slots.Add(pair.Key);

            // Bỏ ngã ba/ngã tư để đoạn không rẽ nhánh.
            var junctions = new List<Vector2Int>();
            foreach (var cell in slots)
                if (SlotNeighbours(cell, slots).Count >= 3) junctions.Add(cell);
            foreach (var cell in junctions) slots.Remove(cell);

            var ordered = new List<Vector2Int>(slots);
            ordered.Sort((a, b) => a.y != b.y ? b.y.CompareTo(a.y) : a.x.CompareTo(b.x)); // thứ tự cố định
            var segments = new List<IReadOnlyList<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();
            // Đoạn hở đi từ một đầu trước, phần còn lại (vòng kín) đi từ ô bất kỳ.
            foreach (var start in ordered)
                if (!visited.Contains(start) && SlotNeighbours(start, slots).Count <= 1)
                    segments.Add(Walk(start, slots, visited));
            foreach (var start in ordered)
                if (!visited.Contains(start))
                    segments.Add(Walk(start, slots, visited));

            // Một dải trái cùng loại không bao giờ qua góc (video gốc): cắt đoạn ở mỗi chỗ đổi hướng.
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

        /// <summary>True khi có vật của lớp "ldf" (cơ chế) hoặc "ldf_below" (đá dưới cầu) trên <paramref name="cell"/>.</summary>
        public static bool HasRoadObject(TiledMap map, Vector2Int cell) =>
            (map.HasLayer("ldf") && map.GetLocalTileId("ldf", cell) >= 0)
            || (map.HasLayer("ldf_below") && map.GetLocalTileId("ldf_below", cell) >= 0);

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

        /// <summary>Loại đường lớp "map" (Empty khi ngoài đường).</summary>
        public TileType RoadTypeAt(Vector2Int cell) => _cells.TryGetValue(cell, out var c) ? c.type : TileType.Empty;

        public IEnumerable<Vector2Int> StopCells
        {
            get { foreach (var cell in _cells.Keys) if (IsStop(cell)) yield return cell; }
        }

        /// <summary>Ô Stop và điểm dừng khay; ngã tư dưới cầu và miệng hầm không phải điểm dừng (không rẽ được).</summary>
        public bool IsStop(Vector2Int cell) =>
            _cells.TryGetValue(cell, out var c) && (c.type == TileType.Stop || IsCrateStop(c.type)) && !IsBridge(cell);

        /// <summary>Cột khay giao từ ô này, hoặc -1.</summary>
        public int CrateColumnAt(Vector2Int cell) => _cells.TryGetValue(cell, out var c) ? c.crateColumn : -1;

        public IReadOnlyDictionary<int, Vector2Int> CrateStops => _crateStops;

        /// <summary>Ô khay đứng (end point giao hàng) theo cột khay.</summary>
        public IReadOnlyDictionary<int, Vector2Int> CrateAnchors => _crateAnchors;

        public bool CanMove(Vector2Int from, Direction direction)
        {
            var to = from + direction.ToCell();
            return _cells.TryGetValue(from, out var a) && _cells.TryGetValue(to, out var b)
                   && Allows(a.type, direction) && Allows(b.type, direction.Opposite())
                   && PassesGate(from, direction) && PassesGate(to, direction)
                   && MouthOpens(from, direction) && MouthOpens(to, direction.Opposite())
                   && !WallBlocks(from, direction) && !WallBlocks(to, direction)
                   && !_covered.Contains(from) && !_covered.Contains(to)
                   && !_risen.Contains(to);
        }

        /// <summary>Như <see cref="CanMove"/> nhưng bỏ qua cổng, đá, rèm, khối nâng: chỉ hỏi có đường không.</summary>
        public bool HasRoad(Vector2Int from, Direction direction)
        {
            var to = from + direction.ToCell();
            return _cells.TryGetValue(from, out var a) && _cells.TryGetValue(to, out var b)
                   && Allows(a.type, direction) && Allows(b.type, direction.Opposite())
                   && MouthOpens(from, direction) && MouthOpens(to, direction.Opposite());
        }

        public bool IsCrateStopCell(Vector2Int cell) => _cells.TryGetValue(cell, out var c) && IsCrateStop(c.type);

        /// <summary>Miệng hầm chỉ nối đường phía của nó (lối vào hầm đi qua TryTunnel).</summary>
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

        // Đường thẳng chỉ cho đi theo trục của nó; mọi ô đi được khác mở mọi hướng.
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
