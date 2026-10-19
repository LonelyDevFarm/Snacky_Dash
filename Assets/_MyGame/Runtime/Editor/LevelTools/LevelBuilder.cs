using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Cách 3 làm lại [user 19/10, màn 365 cũ là ngõ cụt]: bàn ghép chỉ giữ đường (lớp map, điểm giao khay, hầm), bỏ hết cơ chế ở
    /// lớp phủ; bàn đường phải thông như 350 bản đồ gốc; rồi đặt lại từng cơ chế theo quy luật đo trên màn gốc và kiểm lại bàn sau
    /// mỗi cái — cái nào làm bàn có chỗ cụt thì bỏ. Quy luật (Docs/KE_HOACH_LEVEL_GENERATOR.md):
    /// - Màn gốc không có ô cụt (ô chỉ một lối đều là miệng hầm); mọi đường nằm trên vòng.
    /// - Tính theo trạng thái (ô + hướng đang đi, rắn không quay đầu): trạng thái tới được mà không quay về được điểm xuất phát
    ///   p90 là 2.1% ở màn gốc; màn 365 cũ là 14%.
    /// - Cầu (359) ở ngã tư; cổng một chiều (299 / 302 / 305 / 308) ở ô đường thẳng sát ngã rẽ, chỉ vào ngã rẽ (69% cổng gốc);
    ///   tường (đá 286, nắp 283 dọc / 291 ngang, máu ở một nắp) và khối nâng (285) lấp kín một đoạn có hai đầu là ngã rẽ.
    /// - Mọi vật chặn rắn: sau khi đặt không có trạng thái "vào không ra", và rắn tới sát vật chặn còn vòng quay lại ≥ 14 ô
    ///   (vòng ngắn nhất qua cổng ở màn gốc: p10 = 14) [user 19/10: không đặt bừa tạo đường cụt].
    /// </summary>
    public static class LevelBuilder
    {
        public const int BridgeTile = 359, RockTile = 286, WallCapUpDown = 283, WallCapRightLeft = 291, PlotTile = 285;
        private static readonly Dictionary<Direction, int> GateTiles = new Dictionary<Direction, int>
        {
            { Direction.Up, 308 }, { Direction.Down, 299 }, { Direction.Left, 302 }, { Direction.Right, 305 },
        };
        private static readonly int[] TunnelTiles = { 278, 279, 280, 281, 360, 361, 362, 363, 378, 379, 380, 381, 382, 384, 385, 386 };

        /// <summary>
        /// Trạng thái "vào không ra" tối đa cho phép: 0 như phần lớn màn gốc (p50 = 0). Ngưỡng cũ 2.1% (p90) để lọt túi cụt do cổng /
        /// tường ở màn 351, 353 [user 19/10].
        /// </summary>
        public const float MaxTrapRate = 0f;

        /// <summary>Vòng ngắn nhất qua một cổng một chiều (rắn đi qua cổng rồi quay lại được đúng cổng đó): màn gốc p10 = 14 ô;
        /// ngắn hơn thì rắn dài vòng lại sẽ cắn đuôi.</summary>
        public const int MinGateCycle = 14;

        // ------------------------------------------------------------------ kiểm bàn

        /// <summary>Ô đi được chỉ có một lối mà không phải miệng hầm (màn gốc: 0).</summary>
        public static int DeadEnds(BoardGraph board, int width, int height)
        {
            int dead = 0;
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!board.IsWalkable(cell) || board.TunnelMouths.ContainsKey(cell)) continue;
                    if (DirectionExtensions.All.Count(d => board.HasRoad(cell, d)) == 1) dead++;
                }
            return dead;
        }

        /// <summary>
        /// Phần trạng thái (ô + hướng, không quay đầu, theo cổng một chiều và tường đang đóng) tới được từ điểm xuất phát mà không
        /// quay về được điểm xuất phát; <paramref name="reach"/>: số trạng thái tới được.
        /// </summary>
        public static float TrapRate(BoardGraph board, int width, int height, out int reach)
        {
            int Id(Vector2Int c, Direction d) => (c.y * width + c.x) * 4 + (int)d;
            int n = width * height * 4;
            var succ = new List<int>[n];
            var pred = new List<int>[n];
            for (int i = 0; i < n; i++) { succ[i] = new List<int>(2); pred[i] = new List<int>(2); }
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!board.IsWalkable(cell)) continue;
                    foreach (var heading in DirectionExtensions.All)
                        foreach (var d in DirectionExtensions.All)
                        {
                            if (d == heading.Opposite() || !board.CanMove(cell, d)) continue;
                            var next = cell + d.ToCell();
                            var nextHeading = d;
                            if (board.TunnelMouths.ContainsKey(next) && board.TryTunnel(next, d, out var exit, out var exitHeading)) { next = exit; nextHeading = exitHeading; }
                            if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height || !board.IsWalkable(next)) continue;
                            int from = Id(cell, heading), to = Id(next, nextHeading);
                            succ[from].Add(to);
                            pred[to].Add(from);
                        }
                }
            var starts = DirectionExtensions.All.Select(d => Id(board.PlayerStart, d)).ToList();
            var forward = Flood(starts, succ, n);
            var backward = Flood(starts, pred, n);
            reach = 0;
            int trapped = 0;
            for (int i = 0; i < n; i++)
                if (forward[i]) { reach++; if (!backward[i]) trapped++; }
            return reach == 0 ? 1f : trapped / (float)reach;
        }

        /// <summary>Ô cổng một chiều và ô đường sát tường đang đóng / khối nâng đang nhô.</summary>
        private static HashSet<Vector2Int> BlockerCells(BoardGraph board)
        {
            var cells = new HashSet<Vector2Int>(board.OneWayGates.Keys);
            var blocked = board.WallGroups.SelectMany(g => g).Where(board.IsClosedWall).Concat(board.RisingBlocks.Where(board.IsRisen));
            foreach (var b in blocked)
                foreach (var d in DirectionExtensions.All)
                {
                    var next = b + d.ToCell();
                    if (board.HasRoad(b, d) && board.IsWalkable(next) && !board.IsClosedWall(next) && !board.IsRisen(next)) cells.Add(next);
                }
            return cells;
        }

        /// <summary>Số ô của vòng ngắn nhất (không quay đầu) từ ô <paramref name="cell"/> đang đi hướng <paramref name="heading"/>
        /// quay về lại ô đó; int.MaxValue khi không quay về được.</summary>
        public static int ShortestCycle(BoardGraph board, int width, int height, Vector2Int cell, Direction heading)
        {
            var dist = new Dictionary<(Vector2Int, Direction), int>();
            var queue = new Queue<(Vector2Int cell, Direction heading)>();
            void Push(Vector2Int from, Direction h, int d)
            {
                foreach (var dir in DirectionExtensions.All)
                {
                    if (dir == h.Opposite() || !board.CanMove(from, dir)) continue;
                    var next = from + dir.ToCell();
                    var nextHeading = dir;
                    if (board.TunnelMouths.ContainsKey(next) && board.TryTunnel(next, dir, out var exit, out var exitHeading)) { next = exit; nextHeading = exitHeading; }
                    if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height || !board.IsWalkable(next)) continue;
                    if (dist.ContainsKey((next, nextHeading))) continue;
                    dist[(next, nextHeading)] = d + 1;
                    queue.Enqueue((next, nextHeading));
                }
            }
            Push(cell, heading, 0);
            while (queue.Count > 0)
            {
                var (c, h) = queue.Dequeue();
                int d = dist[(c, h)];
                if (c == cell) return d;
                Push(c, h, d);
            }
            return int.MaxValue;
        }

        private static bool[] Flood(List<int> starts, List<int>[] edges, int n)
        {
            var seen = new bool[n];
            var queue = new Queue<int>();
            foreach (int s in starts) { seen[s] = true; queue.Enqueue(s); }
            while (queue.Count > 0)
                foreach (int v in edges[queue.Dequeue()])
                    if (!seen[v]) { seen[v] = true; queue.Enqueue(v); }
            return seen;
        }

        /// <summary>
        /// Bàn đạt quy luật màn gốc: không ô cụt; tường đóng và khối nâng đều nhô lên mà phần "vào không ra" vẫn ≤ 2.1%; điểm giao
        /// khay và ô rải trái đi tới và quay về được (LevelStitcher.Connected).
        /// </summary>
        public static bool Check(string json, LevelDefinition level, TileDefinitionDatabase database, out string why)
        {
            var map = TiledMap.Parse(json);
            var board = new BoardGraph(map, database);
            if (DeadEnds(board, map.Width, map.Height) > 0) { why = "có ô cụt"; return false; }
            foreach (var plot in board.RisingBlocks) board.SetRisen(plot, true); // xấu nhất: mọi khối nâng đều đang nhô
            if (TrapRate(board, map.Width, map.Height, out _) > MaxTrapRate) { why = "có chỗ vào rồi không ra được"; return false; }
            // Mọi vật chặn rắn (cổng một chiều, tường đóng, khối nâng nhô): rắn tới sát nó phải còn vòng quay lại đủ dài [user 19/10].
            foreach (var cell in BlockerCells(board))
                foreach (var heading in DirectionExtensions.All)
                {
                    var from = cell - heading.ToCell();
                    if (!board.IsWalkable(cell) || !board.IsWalkable(from) || !board.CanMove(from, heading)) continue; // không vào ô này theo hướng đó được
                    if (ShortestCycle(board, map.Width, map.Height, cell, heading) < MinGateCycle) { why = "vòng quanh vật chặn quá ngắn"; return false; }
                }
            int hanging = SmallestHangingLoop(board, map.Width, map.Height);
            if (hanging < MinHangingLoop) { why = $"vòng treo {hanging} ô (nối bàn qua một ô)"; return false; }
            var closed = ClosedCells(board);
            if (board.CrateStops.Values.Distinct().Any(c => CrateRoutes(board, c, closed) < MinCrateRoutes)) { why = "khay còn dưới 2 lối riêng khi vật chặn đóng"; return false; }
            if (!LevelStitcher.Connected(map, level, database)) { why = "khay / ô rải trái không đi tới và quay về được"; return false; }
            why = null;
            return true;
        }


        // ------------------------------------------------------------------ lối khay, độ dài vòng (đo trên 347 màn gốc, LevelAnalysis)

        /// <summary>Ô cách khay từ bước này trở ra coi là "ra xa khay" khi đếm lối.</summary>
        public const int FarCells = 10;

        /// <summary>Số lối riêng tối thiểu của mỗi khay khi mọi tường đóng, mọi khối nâng nhô: màn gốc 342/347 màn ≥ 2.</summary>
        public const int MinCrateRoutes = 2;

        /// <summary>Luồng lớn nhất (mỗi ô dùng một lần, ô cầu hai lần) từ <paramref name="crate"/> tới các ô cách nó ≥ <see cref="FarCells"/>,
        /// bỏ các ô <paramref name="closed"/>; cổng coi như đường hai chiều, hầm nối hai miệng.</summary>
        public static int CrateRoutes(BoardGraph board, Vector2Int crate, HashSet<Vector2Int> closed)
        {
            var dist = RoadDistances(board, crate);
            // Tách mỗi ô thành vào (2i) / ra (2i+1); cạnh vào->ra là sức chứa của ô.
            var index = new Dictionary<Vector2Int, int>();
            foreach (var cell in dist.Keys) if (!closed.Contains(cell)) index[cell] = index.Count;
            if (!index.ContainsKey(crate)) return 0;
            int sink = index.Count * 2;
            var cap = new Dictionary<(int, int), int>();
            var adj = new Dictionary<int, List<int>>();
            void Edge(int a, int b, int c)
            {
                cap[(a, b)] = (cap.TryGetValue((a, b), out int old) ? old : 0) + c;
                if (!cap.ContainsKey((b, a))) cap[(b, a)] = 0;
                if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<int>();
                if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<int>();
                la.Add(b);
                lb.Add(a);
            }
            foreach (var pair in index)
            {
                int i = pair.Value;
                Edge(2 * i, 2 * i + 1, pair.Key == crate ? 99 : board.IsBridge(pair.Key) ? 2 : 1);
                foreach (var d in DirectionExtensions.All)
                    if (board.HasRoad(pair.Key, d) && index.TryGetValue(pair.Key + d.ToCell(), out int j)) Edge(2 * i + 1, 2 * j, 1);
                if (board.TryGetTunnelOther(pair.Key, out var other) && index.TryGetValue(other, out int k)) Edge(2 * i + 1, 2 * k, 1);
                if (dist[pair.Key] >= FarCells) Edge(2 * i + 1, sink, 99);
            }
            int source = 2 * index[crate], flow = 0;
            while (flow < 8)
            {
                var parent = new Dictionary<int, int> { { source, source } };
                var queue = new Queue<int>();
                queue.Enqueue(source);
                while (queue.Count > 0 && !parent.ContainsKey(sink))
                {
                    int u = queue.Dequeue();
                    if (!adj.TryGetValue(u, out var next)) continue;
                    foreach (int v in next)
                        if (!parent.ContainsKey(v) && cap[(u, v)] > 0) { parent[v] = u; queue.Enqueue(v); }
                }
                if (!parent.ContainsKey(sink)) break;
                for (int v = sink; v != source; v = parent[v]) { cap[(parent[v], v)]--; cap[(v, parent[v])]++; }
                flow++;
            }
            return flow;
        }


        private static Dictionary<Vector2Int, int> RoadDistances(BoardGraph board, Vector2Int source)
        {
            var dist = new Dictionary<Vector2Int, int> { { source, 0 } };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(source);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    if (!board.HasRoad(cell, d)) continue;
                    var next = cell + d.ToCell();
                    if (!board.IsWalkable(next) || dist.ContainsKey(next)) continue;
                    dist[next] = dist[cell] + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        /// <summary>
        /// Vòng treo nhỏ nhất được phép: phần bàn có vòng mà chỉ nối với phần còn lại qua một ô; rắn dài hơn vòng đi vào là cắn đuôi
        /// lúc quay ra [user 19/10, góc dưới phải màn 365]. Màn gốc: 10/347 màn có, nhỏ nhất 32 ô.
        /// </summary>
        public const int MinHangingLoop = 32;

        /// <summary>Số ô của vòng treo nhỏ nhất (bỏ một ô thì bàn tách ra phần nhỏ có vòng); int.MaxValue khi không có.</summary>
        public static int SmallestHangingLoop(BoardGraph board, int width, int height)
        {
            var cells = Enumerable.Range(0, width * height).Select(i => new Vector2Int(i % width, i / width)).Where(board.IsWalkable).ToList();
            List<Vector2Int> Next(Vector2Int c)
            {
                var list = new List<Vector2Int>();
                foreach (var d in DirectionExtensions.All)
                    if (board.HasRoad(c, d) && board.IsWalkable(c + d.ToCell())) list.Add(c + d.ToCell());
                if (board.TryGetTunnelOther(c, out var other)) list.Add(other);
                return list;
            }
            int smallest = int.MaxValue;
            foreach (var cut in cells)
            {
                var seen = new HashSet<Vector2Int> { cut };
                var parts = new List<List<Vector2Int>>();
                foreach (var start in Next(cut))
                {
                    if (!seen.Add(start)) continue;
                    var part = new List<Vector2Int> { start };
                    for (int i = 0; i < part.Count; i++)
                        foreach (var n in Next(part[i]))
                            if (seen.Add(n)) part.Add(n);
                    parts.Add(part);
                }
                if (parts.Count < 2) continue;
                foreach (var part in parts.OrderBy(p => p.Count).Take(parts.Count - 1))
                {
                    var set = new HashSet<Vector2Int>(part);
                    int edges = part.Sum(c => Next(c).Count(set.Contains)) / 2;
                    if (edges >= part.Count) smallest = Math.Min(smallest, part.Count); // có vòng
                }
            }
            return smallest;
        }

        /// <summary>Ô tường đang đóng và ô khối nâng (coi như đang nhô).</summary>
        private static HashSet<Vector2Int> ClosedCells(BoardGraph board) =>
            new HashSet<Vector2Int>(board.WallGroups.SelectMany(g => g).Where(board.IsClosedWall).Concat(board.RisingBlocks));

        /// <summary>
        /// Độ dài vòng ngắn nhất (theo luật đi: cổng, tường đóng, khối nâng nhô; không quay đầu) trung bình qua các ô khay và qua
        /// các ngã rẽ. Vật chặn có tác dụng thì làm một trong hai dài ra (màn gốc: đóng hết tường / khối nâng thì vòng ngã rẽ
        /// dài thêm ~4 ô).
        /// </summary>
        public static (float crate, float junction, int crateRoutes) Pressure(string json, TileDefinitionDatabase database)
        {
            var (crates, junctions, routes) = Loops(json, database);
            float Avg(IEnumerable<int> loops) { var list = loops.Where(v => v < 10000).ToList(); return list.Count == 0 ? 0 : (float)list.Average(); }
            return (Avg(crates.Values), Avg(junctions.Values), routes);
        }

        /// <summary>
        /// Vòng ngắn nhất theo từng hướng tới ô khay / ngã rẽ (rắn tới ô đó theo hướng đó thì phải đi bao xa mới quay lại được) và
        /// tổng số lối riêng của các khay, khi vật chặn đóng hết. Tính theo hướng tới vì cổng / tường chỉ chặn một ngả rẽ: rắn tới
        /// từ hướng khác vẫn có vòng ngắn như cũ.
        /// </summary>
        public static (Dictionary<(Vector2Int, Direction), int> crates, Dictionary<(Vector2Int, Direction), int> junctions, int crateRoutes) Loops(string json, TileDefinitionDatabase database)
        {
            var map = TiledMap.Parse(json);
            var board = new BoardGraph(map, database);
            foreach (var plot in board.RisingBlocks) board.SetRisen(plot, true);
            Dictionary<(Vector2Int, Direction), int> States(IEnumerable<Vector2Int> cells)
            {
                var loops = new Dictionary<(Vector2Int, Direction), int>();
                foreach (var cell in cells)
                    foreach (var h in DirectionExtensions.All)
                        if (board.CanMove(cell - h.ToCell(), h)) loops[(cell, h)] = ShortestCycle(board, map.Width, map.Height, cell, h);
                return loops;
            }
            var crates = board.CrateStops.Values.Distinct().ToList();
            var closed = ClosedCells(board);
            return (States(crates), States(board.StopCells.Where(c => !board.IsCrateStopCell(c) && IsJunction(board, c))),
                    crates.Sum(c => CrateRoutes(board, c, closed)));
        }

        /// <summary>
        /// Vật chặn vừa đặt có tác dụng [user 19/10: không đặt cho có]: một khay có vòng dài thêm ≥ <see cref="MinLoopGain"/> ô, hoặc
        /// ít nhất 2 ngã rẽ có vòng dài thêm ≥ 2 × <see cref="MinLoopGain"/>, hoặc khay mất một lối (vẫn ≥ 2). Đo từng ô, không lấy trung
        /// bình (một cổng chỉ đổi 1-2 ngã rẽ, trung bình cả bàn thì gần 0).
        /// </summary>
        private static bool Useful((Dictionary<(Vector2Int, Direction), int> crates, Dictionary<(Vector2Int, Direction), int> junctions, int crateRoutes) before,
                                   (Dictionary<(Vector2Int, Direction), int> crates, Dictionary<(Vector2Int, Direction), int> junctions, int crateRoutes) after)
        {
            int Gain(Dictionary<(Vector2Int, Direction), int> a, Dictionary<(Vector2Int, Direction), int> b, (Vector2Int, Direction) c) =>
                a.TryGetValue(c, out int x) && b.TryGetValue(c, out int y) && x < 10000 ? Math.Min(y, 200) - x : 0;
            if (after.crateRoutes < before.crateRoutes) return true;
            if (before.crates.Keys.Any(c => Gain(before.crates, after.crates, c) >= MinLoopGain)) return true;
            return before.junctions.Keys.Count(c => Gain(before.junctions, after.junctions, c) >= 2 * MinLoopGain) >= 2;
        }
        // ------------------------------------------------------------------ bàn đường

        /// <summary>Bỏ mọi cơ chế ở lớp phủ (ldf, ldf_below, ldf_above), giữ hầm (một phần hình đường).</summary>
        public static void StripMechanics(Dictionary<string, long[]> layers, int firstGid)
        {
            foreach (var name in new[] { "ldf", "ldf_below", "ldf_above" })
            {
                if (!layers.TryGetValue(name, out var data)) continue;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == 0) continue;
                    int local = TiledMap.ToLocalTileId(data[i], firstGid);
                    if (name == "ldf" && Array.IndexOf(TunnelTiles, local) >= 0) continue;
                    data[i] = 0;
                }
            }
        }

        // ------------------------------------------------------------------ đặt cơ chế

        /// <summary>Đoạn đường giữa hai ngã rẽ: các ô 2 lối liên tiếp (không phải ngã rẽ, miệng hầm, ô dừng hay điểm xuất phát).</summary>
        private static List<List<Vector2Int>> Corridors(BoardGraph board, int width, int height)
        {
            bool Inner(Vector2Int c) => board.IsWalkable(c) && !board.TunnelMouths.ContainsKey(c) && !board.IsStop(c) && !board.IsCrateStopCell(c)
                && c != board.PlayerStart && DirectionExtensions.All.Count(d => board.HasRoad(c, d)) == 2;
            var seen = new HashSet<Vector2Int>();
            List<Vector2Int> Walk(Vector2Int from, Direction first)
            {
                var side = new List<Vector2Int>();
                var prev = from;
                var cur = from + first.ToCell();
                while (Inner(cur) && seen.Add(cur))
                {
                    side.Add(cur);
                    var step = DirectionExtensions.All.First(d => board.HasRoad(cur, d) && cur + d.ToCell() != prev);
                    prev = cur;
                    cur += step.ToCell();
                }
                return side;
            }
            var corridors = new List<List<Vector2Int>>();
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var start = new Vector2Int(x, y);
                    if (!Inner(start) || !seen.Add(start)) continue;
                    // đi về hai phía tới khi gặp ô không còn là ô giữa đoạn; nối lại theo thứ tự trên đường
                    var exits = DirectionExtensions.All.Where(d => board.HasRoad(start, d)).ToList();
                    var before = Walk(start, exits[0]);
                    var after = Walk(start, exits[1]);
                    before.Reverse();
                    before.Add(start);
                    before.AddRange(after);
                    corridors.Add(before);
                }
            return corridors;
        }

        /// <summary>
        /// Điểm khó của từng cơ chế [user 19/10]: cực khó (khối nâng, rèm) 3, khó (cổng một chiều, tường đá, mũi tên) 2, hơi khó (cầu
        /// công tắc, ống công tắc, khay đóng / mở) 1. Cầu và hầm làm màn dễ đi nên không tính vào ngân sách.
        /// </summary>
        public const int RisingCost = 3, WallCost = 2, GateCost = 2, ToggleCrateCost = 1;

        /// <summary>Ngân sách điểm khó theo bậc màn (difficulty 0 thường, 2 khó, 4 siêu khó).</summary>
        private static int Budget(LevelDefinition level, System.Random random) =>
            level.difficulty >= 4 ? random.Next(6, 9) : level.difficulty >= 2 ? random.Next(4, 7) : random.Next(2, 5);

        /// <summary>Vật chặn có tác dụng khi vòng qua khay hoặc vòng ở ngã rẽ dài thêm ít nhất chừng này ô, hay khay mất một lối
        /// (vẫn ≥ 2) [user 19/10: không đặt cho có].</summary>
        public const float MinLoopGain = 2f;

        /// <summary>Khối nâng nằm ở vòng ngoài: cách khay ít nhất chừng này bước (màn gốc: cụm không cắt lối khay cách khay ~15 ô;
        /// màn 76: khối nâng chỉ ở các đoạn vòng ngoài).</summary>
        public const int RisingMinCrateDistance = 5;

        /// <summary>
        /// Đặt cơ chế cho bàn đường đã thông, mỗi cái kiểm lại bàn; trả về danh sách cơ chế đã đặt. Theo 347 màn gốc [đo 19/10]:
        /// cầu ở ngã tư (chỗ vòng dài cắt qua chính nó); rồi tiêu ngân sách độ khó (<see cref="Budget"/>) bằng các vật chặn, cái nào
        /// không làm vòng dài ra / không chặn lối khay thì không đặt; khối nâng 2-3 cụm ở các đoạn vòng ngoài.
        /// </summary>
        /// <summary>
        /// Ý đồ chính của màn: cơ chế đặt trước tiên, quyết định cách chơi [user 19/10: vật chặn phải có mục đích]:
        /// khối nâng = các lối vòng ngoài chỉ dùng được một lần (màn 76); cổng một chiều = ép đi vòng; tường = một vùng đóng lúc đầu,
        /// phá xong bàn rộng ra.
        /// </summary>
        public static readonly string[] Themes = { "khối nâng", "cổng một chiều", "tường" };

        public static List<string> AddMechanics(ref string json, LevelDefinition level, TileDefinitionDatabase database, System.Random random, out int unspent) =>
            AddMechanics(ref json, level, database, random, out unspent, out _);

        /// <param name="theme">Ý đồ chính đã đặt được (<see cref="Themes"/>); null khi không đặt được cái nào.</param>
        public static List<string> AddMechanics(ref string json, LevelDefinition level, TileDefinitionDatabase database, System.Random random, out int unspent, out string theme)
        {
            var placed = new List<string>();
            // Cầu: 1-4 ở ngã tư, không sát nhau (xem Bridges): rắn dài đi vòng số 8 cắt qua thân mình ở đó mà không chết (bỏ cầu: 32/111 màn gốc không thắng).
            Try(ref json, level, database, placed, "cầu", (map, ldf, lvl) => Bridges(map, ldf, database, random));
            int budget = Budget(level, random);
            var normal = level.columns.Where(c => !c.isExtra).ToList();
            var exhausted = new HashSet<string>(); // loại đã thử nhiều chỗ mà không chỗ nào đạt
            const int Attempts = 5;
            var themes = Themes.Where(t => (t == "khối nâng" ? RisingCost : t == "tường" ? WallCost : GateCost) <= budget).ToList();
            string wanted = themes.Count > 0 ? themes[random.Next(themes.Count)] : null;
            theme = null;
            for (int guard = 0; budget > 0 && guard < 12; guard++)
            {
                var choices = new List<(string name, int cost)> { ("cổng một chiều", GateCost), ("tường", WallCost) };
                if (!placed.Contains("khối nâng")) choices.Add(("khối nâng", RisingCost));
                if (!placed.Contains("khay đóng / mở") && normal.Count >= 2) choices.Add(("khay đóng / mở", ToggleCrateCost));
                choices.RemoveAll(c => c.cost > budget || exhausted.Contains(c.name));
                if (choices.Count == 0) break;
                // Lượt đầu: ý đồ chính; không đặt được thì màn không có ý đồ (bên gọi bỏ bàn).
                if (guard == 0 && wanted != null && !choices.Any(c => c.name == wanted)) break;
                var (name, cost) = guard == 0 && wanted != null ? choices.First(c => c.name == wanted) : choices[random.Next(choices.Count)];
                bool ok = false;
                switch (name)
                {
                    case "cổng một chiều":
                        for (int i = 0; i < Attempts && !ok; i++) ok = Try(ref json, level, database, placed, name, (map, ldf, lvl) => Gate(map, ldf, database, random), true);
                        break;
                    case "tường":
                        for (int i = 0; i < Attempts && !ok; i++) ok = Try(ref json, level, database, placed, name, (map, ldf, lvl) => Wall(map, ldf, lvl, database, random), true);
                        break;
                    case "khối nâng":
                        // 2-3 cụm như màn gốc (màn 76: 4 cụm trên ba đoạn vòng ngoài); đủ 2 cụm mới tính.
                        int groups = 0, want = random.Next(2, 4);
                        for (int i = 0; i < want * 3 && groups < want; i++)
                            if (Try(ref json, level, database, placed, name, (map, ldf, lvl) => Rising(map, ldf, lvl, database, random), false)) groups++;
                        ok = groups >= 2;
                        break;
                    default:
                        normal[random.Next(normal.Count)].closedAtStart = true;
                        placed.Add(name);
                        ok = true;
                        break;
                }
                if (ok) budget -= cost;
                else exhausted.Add(name);
                if (guard == 0) { if (!ok) break; theme = name; }
            }
            unspent = budget;
            if (TiledMap.Parse(json).LayerNames().Contains("ldf") && new BoardGraph(TiledMap.Parse(json), database).TunnelMouths.Count > 0) placed.Add("hầm");
            return placed;
        }

        /// <summary>Đếm vì sao cơ chế bị loại ("cơ chế: lý do" → số lần), để chỉnh luật khi một loại không bao giờ được đặt.</summary>
        public static readonly Dictionary<string, int> Rejections = new Dictionary<string, int>();

        private static void Reject(string what, string why)
        {
            string key = $"{what}: {why}";
            Rejections[key] = Rejections.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        private delegate bool Placer(TiledMap map, long[] ldf, LevelDefinition level);

        /// <summary>Thử đặt một cơ chế trên bản sao; giữ lại khi bàn vẫn đạt <see cref="Check"/> và, với <paramref name="needEffect"/>,
        /// khi nó có tác dụng (vòng dài thêm ≥ <see cref="MinLoopGain"/> hay khay mất một lối).</summary>
        private static bool Try(ref string json, LevelDefinition level, TileDefinitionDatabase database, List<string> placed, string what, Placer place, bool needEffect = false)
        {
            var before = needEffect ? Loops(json, database) : default;
            var file = JsonUtility.FromJson<LevelStitcher.TiledJson.Map>(json);
            var ldfLayer = file.layers.FirstOrDefault(l => l.name == "ldf");
            if (ldfLayer == null)
            {
                ldfLayer = new LevelStitcher.TiledJson.Layer { name = "ldf", data = new long[file.width * file.height], width = file.width, height = file.height, id = file.layers.Length + 1 };
                file.layers = file.layers.Concat(new[] { ldfLayer }).ToArray();
            }
            var backupData = (long[])ldfLayer.data.Clone();
            var backupWalls = level.wallGroupHealths.ToList();
            int backupLower = level.risingBlockDeliveriesToLower;
            if (!place(TiledMap.Parse(json), ldfLayer.data, level)) { ldfLayer.data = backupData; Reject(what, "không có chỗ hợp lệ"); return false; }
            string candidate = JsonUtility.ToJson(file);
            if (Check(candidate, level, database, out string why))
            {
                bool useful = !needEffect || Useful(before, Loops(candidate, database));
                if (useful) { json = candidate; if (!placed.Contains(what)) placed.Add(what); return true; }
                Reject(what, "không có tác dụng");
            }
            else Reject(what, why);
            level.wallGroupHealths.Clear();
            level.wallGroupHealths.AddRange(backupWalls);
            level.risingBlockDeliveriesToLower = backupLower;
            return false;
        }

        private static int Index(TiledMap map, Vector2Int c) => (map.Height - 1 - c.y) * map.Width + c.x;

        private static bool Free(TiledMap map, long[] ldf, Vector2Int c) => ldf[Index(map, c)] == 0;

        private static bool Bridges(TiledMap map, long[] ldf, TileDefinitionDatabase database, System.Random random)
        {
            var board = new BoardGraph(map, database);
            var crossings = new List<Vector2Int>();
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (board.IsWalkable(c) && Free(map, ldf, c) && c != board.PlayerStart && !board.IsCrateStopCell(c)
                        && DirectionExtensions.All.All(d => board.HasRoad(c, d))) crossings.Add(c);
                }
            if (crossings.Count == 0) return false;
            // Số cầu như màn gốc (màn có cầu: 1 cầu 68, 2 cầu 105, 3 cầu 61, 4 cầu 21 màn); hai cầu không cùng hàng / cột cách ≤ 3 ô
            // (màn gốc 9% màn; màn 359 sinh ra có 3 cầu liền nhau trên một cột) [user 19/10].
            int roll = random.Next(255), want = roll < 68 ? 1 : roll < 173 ? 2 : roll < 234 ? 3 : 4;
            var chosen = new List<Vector2Int>();
            foreach (var c in crossings.OrderBy(_ => random.Next()))
            {
                if (chosen.Count >= want) break;
                if (chosen.Any(o => (o.x == c.x && Math.Abs(o.y - c.y) <= 3) || (o.y == c.y && Math.Abs(o.x - c.x) <= 3))) continue;
                chosen.Add(c);
                ldf[Index(map, c)] = BridgeTile + map.FirstGid;
            }
            return true;
        }

        private static bool Gate(TiledMap map, long[] ldf, TileDefinitionDatabase database, System.Random random)
        {
            // Như màn gốc (69% cổng): cổng ở ô đường thẳng sát ngã rẽ, chỉ vào ngã rẽ — rắn từ đoạn đường đi ra ngã rẽ được, từ ngã
            // rẽ không rẽ vào đoạn đó được; không bao giờ ở giữa một đoạn một làn [user 19/10].
            var board = new BoardGraph(map, database);
            var options = new List<(Vector2Int cell, Direction dir)>();
            foreach (var corridor in Corridors(board, map.Width, map.Height))
                foreach (var end in new[] { corridor[0], corridor[corridor.Count - 1] })
                    foreach (var d in DirectionExtensions.All)
                    {
                        var junction = end + d.ToCell();
                        if (!board.HasRoad(end, d) || corridor.Contains(junction) || !IsJunction(board, junction) || !Free(map, ldf, end)) continue;
                        if (!board.HasRoad(end, d.Opposite())) continue; // ô cổng phải thẳng theo hướng cổng
                        options.Add((end, d));
                    }
            if (options.Count == 0) return false;
            var (cell, dir) = options[random.Next(options.Count)];
            ldf[Index(map, cell)] = GateTiles[dir] + map.FirstGid;
            return true;
        }

        private static bool IsJunction(BoardGraph board, Vector2Int cell) =>
            board.IsWalkable(cell) && !board.TunnelMouths.ContainsKey(cell) && DirectionExtensions.All.Count(d => board.HasRoad(cell, d)) >= 3;

        /// <summary>Hai ô ngay ngoài hai đầu đoạn đường.</summary>
        private static List<Vector2Int> Ends(BoardGraph board, List<Vector2Int> corridor)
        {
            var ends = new List<Vector2Int>();
            foreach (var end in new[] { corridor[0], corridor[corridor.Count - 1] })
                foreach (var d in DirectionExtensions.All)
                {
                    var next = end + d.ToCell();
                    if (board.HasRoad(end, d) && !corridor.Contains(next)) ends.Add(next);
                }
            return ends;
        }

        /// <summary>
        /// Đoạn đường cho tường / khối nâng, như màn gốc: hai đầu đều là ngã rẽ (khối nâng được thêm miệng hầm), dài
        /// <paramref name="min"/>-<paramref name="max"/> ô (tường gốc 4-13+, khối nâng 2-12), chưa có gì; tường cần hai đầu là
        /// đường thẳng để đặt nắp.
        /// </summary>
        private static List<Vector2Int> PickCorridor(TiledMap map, long[] ldf, BoardGraph board, System.Random random, bool wall, int min, int max)
        {
            bool Straight(Vector2Int c) => board.HasRoad(c, Direction.Up) && board.HasRoad(c, Direction.Down) || board.HasRoad(c, Direction.Left) && board.HasRoad(c, Direction.Right);
            bool GoodEnd(Vector2Int c) => IsJunction(board, c) || (!wall && board.TunnelMouths.ContainsKey(c));
            var options = Corridors(board, map.Width, map.Height)
                .Where(c => c.Count >= min && c.Count <= max && c.All(cell => Free(map, ldf, cell))
                    && Ends(board, c) is var ends && ends.Count == 2 && ends.All(GoodEnd)
                    && (!wall || Straight(c[0]) && Straight(c[c.Count - 1]))
                    && !LaneBlocked(map, ldf, board, c))
                .ToList();
            return options.Count == 0 ? null : options[random.Next(options.Count)];
        }

        /// <summary>
        /// Làn của đoạn đường (đi tiếp thẳng qua cầu, tới ngã rẽ / điểm dừng / miệng hầm) đã có vật chặn: đặt thêm cái thứ hai là thừa,
        /// đóng một cái là làn đã đóng [user 19/10: màn 363 có 2 cụm khối nâng trên một đường, cách nhau bởi cầu].
        /// </summary>
        private static bool LaneBlocked(TiledMap map, long[] ldf, BoardGraph board, List<Vector2Int> corridor)
        {
            bool Blocker(Vector2Int c)
            {
                if (Free(map, ldf, c)) return false;
                int local = TiledMap.ToLocalTileId(ldf[Index(map, c)], map.FirstGid);
                return local == PlotTile || local == RockTile || local == WallCapUpDown || local == WallCapRightLeft || GateTiles.ContainsValue(local);
            }
            foreach (var (end, inside) in new[] { (corridor[0], corridor.Count > 1 ? corridor[1] : corridor[0]), (corridor[corridor.Count - 1], corridor.Count > 1 ? corridor[corridor.Count - 2] : corridor[0]) })
                foreach (var d in DirectionExtensions.All)
                {
                    var prev = end;
                    var cur = end + d.ToCell();
                    if (!board.HasRoad(end, d) || cur == inside || corridor.Contains(cur)) continue;
                    for (int guard = 0; guard < 200 && board.IsWalkable(cur); guard++)
                    {
                        if (Blocker(cur)) return true;
                        Vector2Int next;
                        if (board.IsBridge(cur)) next = cur + (cur - prev); // qua cầu đi thẳng
                        else
                        {
                            var exits = DirectionExtensions.All.Where(x => board.HasRoad(cur, x) && cur + x.ToCell() != prev).ToList();
                            if (exits.Count != 1 || board.IsStop(cur) || board.IsCrateStopCell(cur) || board.TunnelMouths.ContainsKey(cur)) break; // hết làn
                            next = cur + exits[0].ToCell();
                        }
                        prev = cur;
                        cur = next;
                    }
                }
            return false;
        }

        private static bool Wall(TiledMap map, long[] ldf, LevelDefinition level, TileDefinitionDatabase database, System.Random random)
        {
            var board = new BoardGraph(map, database);
            var corridor = PickCorridor(map, ldf, board, random, true, 4, 13);
            if (corridor == null) return false;
            foreach (var c in corridor) ldf[Index(map, c)] = RockTile + map.FirstGid;
            foreach (var end in new[] { corridor[0], corridor[corridor.Count - 1] })
                ldf[Index(map, end)] = (board.HasRoad(end, Direction.Up) ? WallCapUpDown : WallCapRightLeft) + map.FirstGid;
            level.wallGroupHealths.Add(new WallGroupHealth { seedCell = corridor[0], health = random.Next(2, 6) });
            return true;
        }

        private static bool Rising(TiledMap map, long[] ldf, LevelDefinition level, TileDefinitionDatabase database, System.Random random)
        {
            var board = new BoardGraph(map, database);
            // Vòng ngoài: đoạn cách mọi khay ≥ RisingMinCrateDistance bước.
            var near = new HashSet<Vector2Int>();
            foreach (var crate in board.CrateStops.Values)
                foreach (var pair in RoadDistances(board, crate))
                    if (pair.Value < RisingMinCrateDistance) near.Add(pair.Key);
            List<Vector2Int> corridor = null;
            for (int i = 0; i < 8 && corridor == null; i++)
            {
                corridor = PickCorridor(map, ldf, board, random, false, 2, 12);
                if (corridor != null && corridor.Any(near.Contains)) corridor = null;
            }
            if (corridor == null) return false;
            foreach (var c in corridor) ldf[Index(map, c)] = PlotTile + map.FirstGid;
            level.risingBlockDeliveriesToLower = 3;
            return true;
        }
    }
}
