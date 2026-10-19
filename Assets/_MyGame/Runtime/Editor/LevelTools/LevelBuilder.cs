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
            if (!LevelStitcher.Connected(map, level, database)) { why = "khay / ô rải trái không đi tới và quay về được"; return false; }
            why = null;
            return true;
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

        /// <summary>Đặt cơ chế cho bàn đường đã thông, mỗi cái kiểm lại bàn; trả về danh sách cơ chế đã đặt.</summary>
        public static List<string> AddMechanics(ref string json, LevelDefinition level, TileDefinitionDatabase database, System.Random random)
        {
            var placed = new List<string>();
            // Cầu: ở ngã tư (màn gốc 2.3 cái / màn, phần lớn ngã tư có cầu).
            if (random.NextDouble() < 0.7) Try(ref json, level, database, placed, "cầu", (map, ldf, lvl) => Bridges(map, ldf, database, random));
            // Cổng một chiều: 1-3 cái trên đường thẳng.
            int gates = random.NextDouble() < 0.6 ? random.Next(1, 4) : 0;
            for (int i = 0; i < gates; i++) Try(ref json, level, database, placed, "cổng một chiều", (map, ldf, lvl) => Gate(map, ldf, database, random));
            // Tường: 1-2 đoạn đường, máu 2-5.
            int walls = random.NextDouble() < 0.5 ? random.Next(1, 3) : 0;
            for (int i = 0; i < walls; i++) Try(ref json, level, database, placed, "tường", (map, ldf, lvl) => Wall(map, ldf, lvl, database, random));
            // Khối nâng: một đoạn đường.
            if (random.NextDouble() < 0.4) Try(ref json, level, database, placed, "khối nâng", (map, ldf, lvl) => Rising(map, ldf, lvl, database, random));
            // Khay đóng / mở: một cột thường bắt đầu đóng (cần ít nhất 2 cột thường).
            var normal = level.columns.Where(c => !c.isExtra).ToList();
            if (normal.Count >= 2 && random.NextDouble() < 0.3)
            {
                normal[random.Next(normal.Count)].closedAtStart = true;
                placed.Add("khay đóng / mở");
            }
            if (TiledMap.Parse(json).LayerNames().Contains("ldf") && new BoardGraph(TiledMap.Parse(json), database).TunnelMouths.Count > 0) placed.Add("hầm");
            return placed;
        }

        private delegate bool Placer(TiledMap map, long[] ldf, LevelDefinition level);

        /// <summary>Thử đặt một cơ chế trên bản sao; giữ lại khi bàn vẫn đạt <see cref="Check"/>.</summary>
        private static void Try(ref string json, LevelDefinition level, TileDefinitionDatabase database, List<string> placed, string what, Placer place)
        {
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
            if (!place(TiledMap.Parse(json), ldfLayer.data, level)) { ldfLayer.data = backupData; return; }
            string candidate = JsonUtility.ToJson(file);
            if (Check(candidate, level, database, out _)) { json = candidate; placed.Add(what); return; }
            level.wallGroupHealths.Clear();
            level.wallGroupHealths.AddRange(backupWalls);
            level.risingBlockDeliveriesToLower = backupLower;
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
            int count = Mathf.Clamp(random.Next(1, crossings.Count + 1), 1, 4);
            foreach (var c in crossings.OrderBy(_ => random.Next()).Take(count)) ldf[Index(map, c)] = BridgeTile + map.FirstGid;
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
                    && (!wall || Straight(c[0]) && Straight(c[c.Count - 1])))
                .ToList();
            return options.Count == 0 ? null : options[random.Next(options.Count)];
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
            var corridor = PickCorridor(map, ldf, board, random, false, 2, 12);
            if (corridor == null) return false;
            foreach (var c in corridor) ldf[Index(map, c)] = PlotTile + map.FirstGid;
            level.risingBlockDeliveriesToLower = 3;
            return true;
        }
    }
}
