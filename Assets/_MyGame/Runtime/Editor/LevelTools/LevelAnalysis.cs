using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SnackDash.Editor.Restore;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Đo cách màn gốc được thiết kế bằng SmartPlayer [user 19/10], để bộ sinh bản đồ học theo (Docs/KE_HOACH_LEVEL_GENERATOR.md):
    /// 1. Chỗ chết: mỗi nước SmartPlayer thử mà cắn đuôi — ô chết, độ dài rắn, vòng ngắn nhất ở đó, vật gần đó (cổng, tường, khối
    ///    nâng, hầm, cầu, khay, ngã rẽ). Temp/analysis_deaths.tsv (gộp theo màn).
    /// 2. Tác dụng của vật chặn: bỏ từng loại (cổng, tường, khối nâng, cầu, mũi tên lật) rồi giải lại; dễ hẳn đi = có tác dụng.
    ///    Temp/analysis_ablation.tsv.
    /// 3. Chỗ đặt: từng vật chặn cách khay / điểm xuất phát bao xa, có nằm trên đường ngắn nhất từ xuất phát tới khay không.
    ///    Temp/analysis_layout.tsv.
    /// Chạy mỗi khung một màn (EditorApplication.update) để Editor không đứng lâu. Bỏ màn hướng dẫn 3 (bắt buộc thua), 4, 7 [user].
    /// </summary>
    public static class LevelAnalysis
    {
        public static readonly int[] Skipped = { 3, 4, 7 };
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";
        private static readonly Dictionary<string, int[]> Blockers = new Dictionary<string, int[]>
        {
            { "cổng", new[] { 299, 302, 305, 308 } },
            { "tường", new[] { 283, 286, 291 } },
            { "khối nâng", new[] { 285 } },
            { "cầu", new[] { LevelBuilder.BridgeTile } },
            { "mũi tên lật", new[] { 298, 300, 301, 303, 304, 306, 307, 309 } },
        };

        private static List<int> _queue;
        private static TileDefinitionDatabase _database;
        private static LevelList _list;
        public static int Done { get; private set; }
        public static int Total => _queue?.Count ?? 0;
        public static bool Running { get; private set; }

        [MenuItem("SnackDash/Level Tools/Phân tích màn gốc (SmartPlayer)")]
        public static void Start() => Start(1, 350);

        public static void Start(int from, int to)
        {
            _database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            _list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelDataImporter.LevelListPath);
            _queue = Enumerable.Range(from, to - from + 1).Where(n => !Skipped.Contains(n) && n <= _list.levels.Count).ToList();
            Done = 0;
            File.WriteAllText("Temp/analysis_deaths.tsv", "level\ttier\twon\tmoves\tnodes\tdeaths\tavgLength\tavgCycle\tshortLoop%\t" + string.Join("\t", Features.Select(f => "near_" + f)) + "\n");
            File.WriteAllText("Temp/analysis_ablation.tsv", "level\ttier\tremoved\tcount\tbaseWon\tbaseMoves\tbaseNodes\twon\tmoves\tnodes\n");
            File.WriteAllText("Temp/analysis_layout.tsv", "level\ttier\tblocker\tcell\tdistCrate\tdistStart\tonStartToCrate\n");
            Running = true;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        private static readonly string[] Features = { "cổng", "tường", "khối nâng", "hầm", "cầu", "khay", "ngã rẽ" };

        private static void Step()
        {
            if (Done >= _queue.Count)
            {
                EditorApplication.update -= Step;
                Running = false;
                File.AppendAllText("Temp/analysis_deaths.tsv", "DONE\n");
                return;
            }
            int number = _queue[Done++];
            try { Analyse(number); }
            catch (Exception e) { File.AppendAllText("Temp/analysis_deaths.tsv", $"{number}\tERR\t{e.GetType().Name}: {e.Message}\n"); }
            GC.Collect();
        }

        private static string Tier(LevelDefinition level) => level.difficulty >= 4 ? "siêu khó" : level.difficulty >= 2 ? "khó" : "thường";

        private static void Analyse(int number)
        {
            var level = _list.levels[number - 1];
            string tier = Tier(level);
            string json = level.map.text;

            // 1. chỗ chết
            int deaths = 0, shortLoops = 0;
            long lengthSum = 0, cycleSum = 0;
            var near = Features.ToDictionary(f => f, f => 0);
            var player = new SmartPlayer(() => NewGame(json, level)) { MaxNodes = 6000 };
            player.Died += (before, after) =>
            {
                deaths++;
                var board = after.Board;
                var map = TiledMap.Parse(json);
                int length = after.Segments.Count;
                int cycle = LevelBuilder.ShortestCycle(board, map.Width, map.Height, before.CurrentCell, after.Heading);
                lengthSum += length;
                cycleSum += Math.Min(cycle, 200);
                if (cycle < length) shortLoops++;
                foreach (var f in FeaturesNear(board, after.CurrentCell, 2)) near[f]++;
            };
            bool won = player.Play(600);
            int moves = player.Moves, nodes = player.Nodes;
            File.AppendAllText("Temp/analysis_deaths.tsv", string.Join("\t", new object[]
            {
                number, tier, won ? 1 : 0, moves, nodes, deaths,
                deaths > 0 ? (lengthSum / (double)deaths).ToString("0.0", CultureInfo.InvariantCulture) : "0",
                deaths > 0 ? (cycleSum / (double)deaths).ToString("0.0", CultureInfo.InvariantCulture) : "0",
                deaths > 0 ? (shortLoops * 100 / deaths).ToString(CultureInfo.InvariantCulture) : "0",
            }.Concat(Features.Select(f => (object)near[f]))) + "\n");

            // 2. bỏ từng loại vật chặn
            foreach (var blocker in Blockers)
            {
                string stripped = Strip(json, blocker.Value, out int removed);
                if (removed == 0) continue;
                var lighter = new SmartPlayer(() => NewGame(stripped, level)) { MaxNodes = 6000 };
                bool lighterWon = lighter.Play(600);
                File.AppendAllText("Temp/analysis_ablation.tsv", $"{number}\t{tier}\t{blocker.Key}\t{removed}\t{(won ? 1 : 0)}\t{moves}\t{nodes}\t{(lighterWon ? 1 : 0)}\t{lighter.Moves}\t{lighter.Nodes}\n");
            }

            // 3. chỗ đặt vật chặn
            var plainMap = TiledMap.Parse(json);
            var plain = new BoardGraph(plainMap, _database);
            var fromStart = Distances(plain, plain.PlayerStart);
            var crates = plain.CrateStops.Values.ToList();
            var fromCrates = Distances(plain, crates.ToArray());
            int startToCrate = crates.Select(c => fromStart.TryGetValue(c, out int d) ? d : int.MaxValue).DefaultIfEmpty(int.MaxValue).Min();
            var sb = new StringBuilder();
            foreach (var cell in BlockerTiles(plainMap))
            {
                int dc = fromCrates.TryGetValue(cell.cell, out int a) ? a : -1;
                int ds = fromStart.TryGetValue(cell.cell, out int b) ? b : -1;
                bool onPath = dc >= 0 && ds >= 0 && dc + ds == startToCrate;
                sb.Append($"{number}\t{tier}\t{cell.kind}\t{cell.cell.x},{cell.cell.y}\t{dc}\t{ds}\t{(onPath ? 1 : 0)}\n");
            }
            File.AppendAllText("Temp/analysis_layout.tsv", sb.ToString());
        }

        private static GameSession NewGame(string json, LevelDefinition level)
        {
            var map = TiledMap.Parse(json);
            var session = new GameSession(new BoardGraph(map, _database), map, level);
            session.SetSeed(1);
            session.Begin();
            if (session.ExtraColumn >= 0) session.UnlockExtraColumn();
            return session;
        }

        /// <summary>Vật trong bán kính <paramref name="radius"/> ô quanh <paramref name="cell"/> (mỗi loại tính một lần).</summary>
        private static IEnumerable<string> FeaturesNear(BoardGraph board, Vector2Int cell, int radius)
        {
            var found = new HashSet<string>();
            for (int dx = -radius; dx <= radius; dx++)
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var c = cell + new Vector2Int(dx, dy);
                    if (board.OneWayGates.ContainsKey(c)) found.Add("cổng");
                    if (board.IsClosedWall(c)) found.Add("tường");
                    if (board.RisingBlocks.Contains(c)) found.Add("khối nâng");
                    if (board.TunnelMouths.ContainsKey(c)) found.Add("hầm");
                    if (board.IsBridge(c)) found.Add("cầu");
                    if (board.IsCrateStopCell(c)) found.Add("khay");
                    if (board.IsWalkable(c) && DirectionExtensions.All.Count(d => board.HasRoad(c, d)) >= 3) found.Add("ngã rẽ");
                }
            return found;
        }

        /// <summary>Bản đồ bỏ các ô <paramref name="tiles"/> ở lớp phủ.</summary>
        private static string Strip(string json, int[] tiles, out int removed)
        {
            var file = JsonUtility.FromJson<LevelParts.TiledJson.Map>(json);
            int firstGid = file.tilesets[0].firstgid;
            removed = 0;
            foreach (var layer in file.layers)
            {
                if (layer.name != "ldf" && layer.name != "ldf_below") continue;
                for (int i = 0; i < layer.data.Length; i++)
                    if (layer.data[i] != 0 && Array.IndexOf(tiles, TiledMap.ToLocalTileId(layer.data[i], firstGid)) >= 0) { layer.data[i] = 0; removed++; }
            }
            return JsonUtility.ToJson(file);
        }

        private static IEnumerable<(string kind, Vector2Int cell)> BlockerTiles(TiledMap map)
        {
            foreach (var layer in new[] { "ldf", "ldf_below" })
            {
                if (!map.HasLayer(layer)) continue;
                for (int x = 0; x < map.Width; x++)
                    for (int y = 0; y < map.Height; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        int local = map.GetLocalTileId(layer, cell);
                        foreach (var b in Blockers)
                            if (Array.IndexOf(b.Value, local) >= 0) yield return (b.Key, cell);
                    }
            }
        }

        // ------------------------------------------------------------------ 4. số lối tới khay

        /// <summary>
        /// Số lối riêng (không chung ô nào) từ mỗi ô khay ra xa (ô cách khay ≥ <see cref="LevelBuilder.FarCells"/> bước) [user 19/10: "bình thường
        /// có 2 lối giao hàng, khối nâng che 1 lối"]. Mỗi cụm khối nâng / hàng đá: đóng riêng cụm đó thì tổng số lối các khay giảm
        /// bao nhiêu. Temp/analysis_routes.tsv; nhanh (không giải) nên chạy liền một lần.
        /// </summary>
        public static string Routes(int from, int to)
        {
            _database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            _list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelDataImporter.LevelListPath);
            var sb = new StringBuilder("level\ttier\tcrates\troutesOpen\troutesAllClosed\tkind\tgroup\tcells\tdrop\tdistCrate\tcrateLoopOpen\tcrateLoopShut\tjunctionLoopOpen\tjunctionLoopShut\tjunctionsNoLoop\n");
            for (int number = from; number <= to && number <= _list.levels.Count; number++)
            {
                if (Skipped.Contains(number)) continue;
                var level = _list.levels[number - 1];
                var board = new BoardGraph(TiledMap.Parse(level.map.text), _database);
                var crates = board.CrateStops.Values.Distinct().ToList();
                if (crates.Count == 0) continue;
                var groups = new List<(string kind, List<Vector2Int> cells)>();
                foreach (var g in Groups(board.RisingBlocks)) groups.Add(("khối nâng", g));
                foreach (var g in board.WallGroups) groups.Add(("tường", g.ToList()));
                var none = new HashSet<Vector2Int>();
                var all = new HashSet<Vector2Int>(groups.SelectMany(g => g.cells));
                int open = crates.Sum(c => LevelBuilder.CrateRoutes(board, c, none));
                int closed = crates.Sum(c => LevelBuilder.CrateRoutes(board, c, all));
                string head = $"{number}\t{Tier(level)}\t{crates.Count}\t{open}\t{closed}";
                if (groups.Count == 0) { sb.Append(head).Append("\t-\t-\t0\t0\t-1\n"); continue; }
                var fromCrates = Distances(board, crates.ToArray());
                for (int i = 0; i < groups.Count; i++)
                {
                    var shut = new HashSet<Vector2Int>(groups[i].cells);
                    int drop = open - crates.Sum(c => LevelBuilder.CrateRoutes(board, c, shut));
                    int dist = groups[i].cells.Select(c => fromCrates.TryGetValue(c, out int d) ? d : 999).Min();
                    var (crateOpen, crateShut, loopOpen, loopShut, lost) = Loops(level, groups, i);
                    sb.Append($"{head}\t{groups[i].kind}\t{i}\t{groups[i].cells.Count}\t{drop}\t{dist}\t{crateOpen}\t{crateShut}\t{loopOpen}\t{loopShut}\t{lost}\n");
                }
            }
            File.WriteAllText("Temp/analysis_routes.tsv", sb.ToString());
            return "Temp/analysis_routes.tsv";
        }



        /// <summary>
        /// Vòng ngắn nhất (theo luật đi, có cổng, không quay đầu) qua các ô khay và qua các ngã rẽ, khi mọi vật chặn mở và khi chỉ cụm
        /// <paramref name="shut"/> đóng (khối nâng: đang nâng; tường: còn đá). Trả TB vòng khay, TB vòng ngã rẽ (bỏ ô hết vòng),
        /// và số ngã rẽ mất hết vòng khi đóng.
        /// </summary>
        private static (int crateOpen, int crateShut, int loopOpen, int loopShut, int lost) Loops(LevelDefinition level, List<(string kind, List<Vector2Int> cells)> groups, int shut)
        {
            var map = TiledMap.Parse(level.map.text);
            BoardGraph Board(bool close)
            {
                var b = new BoardGraph(map, _database);
                for (int w = 0; w < b.WallGroups.Count; w++)
                    if (!(close && groups[shut].kind == "tường" && b.WallGroups[w].Contains(groups[shut].cells[0]))) b.OpenWall(w);
                if (close && groups[shut].kind == "khối nâng") foreach (var c in groups[shut].cells) b.SetRisen(c, true);
                return b;
            }
            var open = Board(false);
            var closed = Board(true);
            var crates = open.CrateStops.Values.Distinct().ToList();
            var junctions = open.StopCells.Where(c => !open.IsCrateStopCell(c) && DirectionExtensions.All.Count(d => open.HasRoad(c, d)) >= 3).ToList();
            int Loop(BoardGraph b, Vector2Int cell) => DirectionExtensions.All.Select(h => LevelBuilder.ShortestCycle(b, map.Width, map.Height, cell, h)).Min();
            int Avg(IEnumerable<int> values) { var list = values.Where(v => v < 10000).ToList(); return list.Count == 0 ? -1 : (int)list.Average(); }
            int lost = junctions.Count(c => Loop(open, c) < 10000 && Loop(closed, c) >= 10000);
            return (Avg(crates.Select(c => Loop(open, c))), Avg(crates.Select(c => Loop(closed, c))),
                    Avg(junctions.Select(c => Loop(open, c))), Avg(junctions.Select(c => Loop(closed, c))), lost);
        }

        /// <summary>Các cụm ô liền nhau.</summary>
        private static List<List<Vector2Int>> Groups(IEnumerable<Vector2Int> cells)
        {
            var left = new HashSet<Vector2Int>(cells);
            var groups = new List<List<Vector2Int>>();
            while (left.Count > 0)
            {
                var first = left.First();
                left.Remove(first);
                var group = new List<Vector2Int> { first };
                for (int i = 0; i < group.Count; i++)
                    foreach (var d in DirectionExtensions.All)
                        if (left.Remove(group[i] + d.ToCell())) group.Add(group[i] + d.ToCell());
                groups.Add(group);
            }
            return groups;
        }

        /// <summary>Số ô đường (bỏ qua vật chặn) từ các ô <paramref name="sources"/> tới mọi ô.</summary>
        private static Dictionary<Vector2Int, int> Distances(BoardGraph board, params Vector2Int[] sources)
        {
            var dist = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            foreach (var s in sources) { dist[s] = 0; queue.Enqueue(s); }
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
    }
}
