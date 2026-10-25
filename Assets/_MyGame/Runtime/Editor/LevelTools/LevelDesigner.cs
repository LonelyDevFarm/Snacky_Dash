using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Cách 4, tự thiết kế bản đồ [user 19/10: cắt ghép bản đồ cũ thì màn nào cũng rộng, đường nối gượng, vật chặn không có ý đồ].
    /// Bản đồ gốc là các khối cỏ chữ nhật, đường chạy quanh. Dựng như vậy:
    /// 1. Lưới đường: các đường dọc / ngang cách nhau 3-5 ô (khối cỏ 2-4 ô), bề ngang bàn 15-18 ô như màn gốc.
    /// 2. Khay đặt trước, như màn gốc (đo 350 màn): khay đứng ngay trên ô giao; hoặc trong lòng một khối cỏ rộng ≥ 5 × 5
    ///    (gộp 2 × 2 ô lưới), hoặc trên mép trên bàn (chừa 2 hàng cỏ). 1 / 2 / 3 cột khay theo tỉ lệ màn gốc 30 / 48 / 21%.
    /// 3. Gộp khối: bỏ dần đoạn đường cho tới số ô đường theo bậc màn; không bỏ đoạn nào làm có ô cụt hay vòng treo (mọi nút
    ///    còn ≥ 2 nhánh, bỏ bất kỳ một nút nào bàn vẫn liền).
    /// 4. Điểm xuất phát ở một ngã rẽ xa khay. Lát mã ô: đường theo hướng nối, viền cỏ theo bảng học từ màn gốc (LevelParts).
    /// Cơ chế đặt sau bằng LevelBuilder.AddMechanics như cách 3.
    /// </summary>
    public static class LevelDesigner
    {
        // Mã ô đường theo hướng nối (bit 0 lên, 1 phải, 2 xuống, 3 trái) — đo trên 350 bản đồ gốc, khớp 100%.
        private static readonly Dictionary<int, int> RoadTiles = new Dictionary<int, int>
        {
            { 5, 352 }, { 10, 350 }, { 3, 337 }, { 6, 335 }, { 12, 331 }, { 9, 333 },
            { 7, 353 }, { 11, 354 }, { 13, 356 }, { 14, 355 }, { 15, 347 },
        };
        private const int EndPointTile = 387; // ô khay đứng (ngay trên ô giao)
        private const int NonPillSpawnTile = 276; // lớp spawn: ô không rải trái (như màn gốc đánh dấu)
        private const string TemplateLevel = "Assets/_MyGame/Data/Levels/Definitions"; // lấy tileset từ một màn gốc

        /// <summary>Số ô đường theo bậc. Gọn hơn màn gốc (thường đa số 80-160) [user 19/10: màn nào cũng rộng]: khó nhờ cơ chế, không
        /// nhờ đường dài (bàn 140-180 ô thì đường thắng quá 350 lượt).</summary>
        private static int TargetRoads(int difficulty, System.Random random) =>
            difficulty >= 4 ? random.Next(115, 151) : difficulty >= 2 ? random.Next(105, 141) : random.Next(85, 121);

        private sealed class Lattice
        {
            public int[] X, Y;
            public readonly HashSet<(int i, int j, bool h)> Edges = new HashSet<(int, int, bool)>(); // h: (i,j)-(i+1,j); dọc: (i,j)-(i,j+1)
            public readonly HashSet<(int, int, bool)> Fixed = new HashSet<(int, int, bool)>();

            public IEnumerable<(int, int, bool)> At(int i, int j)
            {
                if (Edges.Contains((i, j, true))) yield return (i, j, true);
                if (Edges.Contains((i - 1, j, true))) yield return (i - 1, j, true);
                if (Edges.Contains((i, j, false))) yield return (i, j, false);
                if (Edges.Contains((i, j - 1, false))) yield return (i, j - 1, false);
            }

            public int Degree(int i, int j) => At(i, j).Count();

            public (int, int) Other((int i, int j, bool h) e, (int i, int j) from) =>
                e.h ? (from.i == e.i ? (e.i + 1, e.j) : (e.i, e.j)) : (from.j == e.j ? (e.i, e.j + 1) : (e.i, e.j));

            public List<(int, int)> Nodes()
            {
                var nodes = new List<(int, int)>();
                for (int i = 0; i < X.Length; i++)
                    for (int j = 0; j < Y.Length; j++)
                        if (Degree(i, j) > 0) nodes.Add((i, j));
                return nodes;
            }

            public int RoadCells() =>
                Nodes().Count + Edges.Sum(e => e.h ? X[e.i + 1] - X[e.i] - 1 : Y[e.j + 1] - Y[e.j] - 1);

            /// <summary>Liền một khối khi bỏ nút <paramref name="skip"/> (null: không bỏ).</summary>
            public bool Connected((int, int)? skip)
            {
                var nodes = Nodes().Where(n => !skip.HasValue || !n.Equals(skip.Value)).ToList();
                if (nodes.Count == 0) return false;
                var seen = new HashSet<(int, int)> { nodes[0] };
                var queue = new Queue<(int, int)>();
                queue.Enqueue(nodes[0]);
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    foreach (var e in At(n.Item1, n.Item2))
                    {
                        var m = Other(e, n);
                        if (skip.HasValue && m.Equals(skip.Value)) continue;
                        if (seen.Add(m)) queue.Enqueue(m);
                    }
                }
                return seen.Count == nodes.Count;
            }

            /// <summary>Không ô cụt (nút còn đường thì ≥ 2 nhánh), liền, không nút nào bỏ đi làm bàn tách (không vòng treo).</summary>
            public bool Valid()
            {
                var nodes = Nodes();
                if (nodes.Any(n => Degree(n.Item1, n.Item2) == 1)) return false;
                if (!Connected(null)) return false;
                return nodes.All(n => Connected(n));
            }
        }

        /// <summary>Màn tự thiết kế (chưa lưu, chưa có cơ chế) và bản đồ JSON; null kèm lý do khi không dựng được.</summary>
        /// <param name="paired">Khay đôi (sáng tạo, màn sau 366): điểm giao cuối là một cặp khay trên mép trên, chừa đủ ngang cho hai chồng.</param>
        public static LevelDefinition Design(int difficulty, TileDefinitionDatabase database, System.Random random, int mixWindow, out string json, out string why, bool paired = false)
        {
            json = null;
            var lattice = new Lattice();
            // 1. Lưới: đường dọc ở X, ngang ở Y; bề ngang bàn = X cuối + 2 (15-18), cao = Y cuối + 2, +1 nếu khay ở mép trên.
            lattice.X = Lines(random, 13, 16);
            lattice.Y = Lines(random, 19, 25);
            if (lattice.X == null || lattice.Y == null) { why = "không chia được lưới"; return null; }
            int nx = lattice.X.Length, ny = lattice.Y.Length;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (i + 1 < nx) lattice.Edges.Add((i, j, true));
                    if (j + 1 < ny) lattice.Edges.Add((i, j, false));
                }

            // 2. Khay.
            int roll = random.Next(100), columns = roll < 30 ? 1 : roll < 79 ? 2 : 3;
            bool top = random.Next(2) == 0;
            var stops = new List<Vector2Int>();
            var blocks = new List<RectInt>(); // ô lưới đã gộp cho khay
            if (paired)
            {
                // Khay đôi: 0-1 khay thường trong khối cỏ, rồi cặp trên mép trên (điểm giao cuối, nên cột bạn là cột cuối, không có điểm giao).
                if (random.Next(2) == 0) BlockCrate(lattice, stops, blocks, random);
                if (!PairTopCrate(lattice, stops, random)) { why = "không có chỗ đặt khay đôi"; return null; }
            }
            else
                for (int c = 0; c < columns; c++)
                {
                    bool placed = top ? TopCrate(lattice, stops, random) : BlockCrate(lattice, stops, blocks, random);
                    if (!placed && !(top ? BlockCrate(lattice, stops, blocks, random) : TopCrate(lattice, stops, random))) break;
                }
            if (stops.Count == 0) { why = "không có chỗ đặt khay"; return null; }

            // 3. Gộp khối tới số ô đường theo bậc.
            // Dừng khi đủ nhỏ, hoặc khi số vòng độc lập (đoạn - nút + 1) xuống tới mức màn gốc (6-11 vòng): gộp tiếp thì chỉ còn
            // vài đường dài, ít ngã rẽ.
            int target = TargetRoads(difficulty, random), minLoops = difficulty >= 4 ? 8 : difficulty >= 2 ? 7 : 6;
            foreach (var edge in lattice.Edges.Where(e => !lattice.Fixed.Contains(e)).OrderBy(_ => random.Next()).ToList())
            {
                if (lattice.RoadCells() <= target || lattice.Edges.Count - lattice.Nodes().Count + 1 <= minLoops) break;
                lattice.Edges.Remove(edge);
                if (!lattice.Valid()) lattice.Edges.Add(edge);
            }
            if (!lattice.Valid()) { why = "lưới không hợp lệ"; return null; }

            // 4. Ô đường và hướng nối.
            int width = lattice.X[nx - 1] + 2, height = lattice.Y[ny - 1] + (stops.Any(s => s.y == lattice.Y[ny - 1]) ? 3 : 2); // khay mép trên: chừa 2 hàng (màn gốc)
            var roads = new Dictionary<Vector2Int, int>();
            void Link(Vector2Int a, Vector2Int b)
            {
                var d = b - a;
                int bit = d.y > 0 ? 0 : d.x > 0 ? 1 : d.y < 0 ? 2 : 3;
                roads[a] = (roads.TryGetValue(a, out int m) ? m : 0) | (1 << bit);
                roads[b] = (roads.TryGetValue(b, out int n) ? n : 0) | (1 << ((bit + 2) % 4));
            }
            foreach (var e in lattice.Edges)
            {
                var from = new Vector2Int(lattice.X[e.i], lattice.Y[e.j]);
                var to = e.h ? new Vector2Int(lattice.X[e.i + 1], lattice.Y[e.j]) : new Vector2Int(lattice.X[e.i], lattice.Y[e.j + 1]);
                var step = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
                for (var c = from; c != to; c += step) Link(c, c + step);
            }
            if (stops.Any(s => !roads.TryGetValue(s, out int m) || (m & 10) != 10)) { why = "ô giao khay không nằm trên đường ngang"; return null; }

            // Điểm xuất phát: ngã rẽ xa khay nhất trong nửa số ngã rẽ xa nhất (ngẫu nhiên).
            var junctions = roads.Where(p => CountBits(p.Value) >= 3 && !stops.Contains(p.Key)).Select(p => p.Key).ToList();
            if (junctions.Count == 0) { why = "không có ngã rẽ"; return null; }
            var far = junctions.OrderByDescending(j => stops.Min(s => Math.Abs(s.x - j.x) + Math.Abs(s.y - j.y))).Take(Math.Max(1, junctions.Count / 2)).ToList();
            var start = far[random.Next(far.Count)];

            // 5. Lát mã ô.
            var template = UnityEditor.AssetDatabase.FindAssets("t:LevelDefinition", new[] { TemplateLevel })
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .First(l => l != null && l.map != null);
            var source = JsonUtility.FromJson<LevelParts.TiledJson.Map>(template.map.text);
            int firstGid = source.tilesets[0].firstgid;
            int Index(Vector2Int c) => (height - 1 - c.y) * width + c.x;
            var map = new long[width * height];
            var spawn = new long[width * height];
            var ldf = new long[width * height];
            // Khay đôi: không ô end point, không lối vào; cỏ bao quanh nền hai khay (Hole_Pair_VO kín miệng) [user 25/10: lối vào giữa xấu hơn].
            // BoardGraph đặt cặp ngay trên ô giao khi ô giao không có end point kề bên.
            var anchors = new HashSet<Vector2Int>(stops.Where((s, i) => !paired || i < stops.Count - 1).Select(s => s + Vector2Int.up));
            foreach (var pair in roads)
            {
                int stopIndex = stops.IndexOf(pair.Key);
                int local = stopIndex >= 0 ? LevelParts.DeliverTiles[stopIndex] : RoadTiles[pair.Value];
                map[Index(pair.Key)] = local + firstGid;
            }
            foreach (var a in anchors) map[Index(a)] = EndPointTile + firstGid;
            var grass = LevelParts.GrassTable(database);
            bool RoadLikeAt(Vector2Int c) => roads.ContainsKey(c) || anchors.Contains(c);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (RoadLikeAt(cell)) continue;
                    int mask = 0;
                    for (int k = 0; k < 8; k++) if (RoadLikeAt(cell + LevelParts.Around[k])) mask |= 1 << k;
                    map[Index(cell)] = grass.TryGetValue(mask, out int best) && best >= 0 ? best + firstGid : 0;
                }
            spawn[Index(start)] = LevelParts.StartTile + firstGid;
            // Khay đôi: đường ngay dưới cặp khay không rải trái (trái cao, đè lên chân khay).
            if (paired)
            {
                var pairStop = stops[stops.Count - 1];
                for (int dx = -PairHalfWidth; dx <= PairHalfWidth; dx++)
                {
                    var cell = pairStop + new Vector2Int(dx, 0);
                    if (roads.ContainsKey(cell) && cell != start) spawn[Index(cell)] = NonPillSpawnTile + firstGid;
                }
            }

            var output = new LevelParts.TiledJson.Map
            {
                width = width, height = height, tilewidth = source.tilewidth, tileheight = source.tileheight, tilesets = source.tilesets,
                layers = new[]
                {
                    new LevelParts.TiledJson.Layer { name = "map", data = map, width = width, height = height, id = 1 },
                    new LevelParts.TiledJson.Layer { name = "ldf", data = ldf, width = width, height = height, id = 2 },
                    new LevelParts.TiledJson.Layer { name = "spawn", data = spawn, width = width, height = height, id = 3 },
                },
            };
            json = JsonUtility.ToJson(output);

            // 6. Dữ liệu màn: khay và đợt trái như cách 3, cỡ lấy theo hai màn gốc cùng bậc.
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.difficulty = difficulty;
            level.wallHealth = 3;
            level.risingBlockDeliveriesToLower = 3;
            for (int c = 0; c < stops.Count; c++) level.columns.Add(new CrateColumnDefinition());
            if (paired) level.columns.Add(new CrateColumnDefinition { pairedWithPrevious = true });
            var list = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelList>(LevelGenerator.LevelListPath);
            var sameTier = list.levels.Take(350).Where(l => l != null && (l.difficulty >= 4) == (difficulty >= 4) && (l.difficulty >= 2) == (difficulty >= 2)).ToList();
            LevelParts.FillFruit(level, sameTier.OrderBy(_ => random.Next()).Take(2).ToList(), random, mixWindow);
            if (paired) EvenPair(level, random, mixWindow);
            why = null;
            return level;
        }

        /// <summary>Nửa bề ngang cặp khay tính từ ô giao (khay 2.8 lệch 2 ô, cộng mép hố), ô.</summary>
        private const int PairHalfWidth = 4;

        /// <summary>
        /// Khay đôi trên mép trên: ô giao giữa một đoạn đường ngang trên cùng; đường ngang dưới cả cặp giữ liền, thẳng (khay đứng trên chỗ
        /// đường đứt / rẽ xuống thì nền khay lòi ra mép cỏ) [user 25/10], hai đầu đường còn cách mép cặp ít nhất một ô.
        /// </summary>
        private static bool PairTopCrate(Lattice lattice, List<Vector2Int> stops, System.Random random)
        {
            int j = lattice.Y.Length - 1, last = lattice.X.Length - 1;
            var options = Enumerable.Range(0, last)
                .Where(i => lattice.X[i + 1] - lattice.X[i] >= 4)
                .Where(i => { int x = (lattice.X[i] + lattice.X[i + 1]) / 2; return x - PairHalfWidth - 1 >= lattice.X[0] && x + PairHalfWidth + 1 <= lattice.X[last]; })
                .ToList();
            if (options.Count == 0) return false;
            int pick = options[random.Next(options.Count)];
            int stop = (lattice.X[pick] + lattice.X[pick + 1]) / 2;
            stops.Add(new Vector2Int(stop, lattice.Y[j]));
            for (int i = 0; i < last; i++)
                if (lattice.X[i + 1] > stop - PairHalfWidth - 1 && lattice.X[i] < stop + PairHalfWidth + 1) lattice.Fixed.Add((i, j, true));
            return true;
        }

        /// <summary>Hai cột của cặp cùng số tầng, mỗi tầng 9 trái và hai khay khác màu (GameSession.BuildPairs); chia lại đợt trái.</summary>
        private static void EvenPair(LevelDefinition level, System.Random random, int mixWindow)
        {
            var lead = level.columns[level.columns.Count - 2];
            var partner = level.columns[level.columns.Count - 1];
            var crates = lead.crates.Concat(partner.crates).Select(c => new CrateDefinition { kind = c.kind, requiredCount = 9 }).ToList();
            var kinds = crates.Select(c => c.kind).Distinct().ToList();
            if (crates.Count % 2 == 1) crates.Add(new CrateDefinition { kind = kinds[random.Next(kinds.Count)], requiredCount = 9 });
            lead.crates.Clear();
            partner.crates.Clear();
            // Mỗi tầng lấy hai khay khác màu khi còn được.
            while (crates.Count > 0)
            {
                var a = crates[0];
                crates.RemoveAt(0);
                int other = crates.FindIndex(c => c.kind != a.kind);
                var b = crates[other < 0 ? 0 : other];
                crates.RemoveAt(other < 0 ? 0 : other);
                lead.crates.Add(a);
                partner.crates.Add(b);
            }
            LevelParts.Regroup(level, Mathf.Clamp(level.batches.Count, LevelParts.MinWaves, LevelParts.MaxWaves), random, mixWindow);
        }

        /// <summary>Vị trí các đường song song từ 1, cách nhau 3-5 ô (khối cỏ 2-4 ô, hay gặp 3-4), đường cuối trong [min, max].</summary>
        private static int[] Lines(System.Random random, int min, int max)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var lines = new List<int> { 1 };
                while (lines[lines.Count - 1] < min) lines.Add(lines[lines.Count - 1] + new[] { 3, 4, 4, 5 }[random.Next(4)]);
                if (lines[lines.Count - 1] <= max) return lines.ToArray();
            }
            return null;
        }

        /// <summary>Khay trên mép trên: ô giao ở giữa một đoạn đường ngang trên cùng, khay đứng phía trên.</summary>
        private static bool TopCrate(Lattice lattice, List<Vector2Int> stops, System.Random random)
        {
            int j = lattice.Y.Length - 1;
            var options = Enumerable.Range(0, lattice.X.Length - 1)
                .Where(i => lattice.X[i + 1] - lattice.X[i] >= 4 && !stops.Any(s => s.y == lattice.Y[j] && s.x > lattice.X[i] && s.x < lattice.X[i + 1]))
                .ToList();
            if (options.Count == 0) return false;
            int pick = options[random.Next(options.Count)];
            stops.Add(new Vector2Int((lattice.X[pick] + lattice.X[pick + 1]) / 2, lattice.Y[j]));
            lattice.Fixed.Add((pick, j, true));
            return true;
        }

        /// <summary>Khay trong khối cỏ: gộp 2 × 2 ô lưới (lòng ≥ 5 × 5), ô giao ở nút giữa cạnh dưới; giữ nguyên viền khối.</summary>
        private static bool BlockCrate(Lattice lattice, List<Vector2Int> stops, List<RectInt> blocks, System.Random random)
        {
            var options = new List<(int i, int j)>();
            for (int i = 0; i + 2 < lattice.X.Length; i++)
                for (int j = 0; j + 2 < lattice.Y.Length; j++)
                {
                    if (lattice.X[i + 2] - lattice.X[i] - 1 < 5 || lattice.Y[j + 2] - lattice.Y[j] - 1 < 5) continue;
                    var rect = new RectInt(i, j, 2, 2);
                    if (blocks.Any(b => b.Overlaps(new RectInt(i - 1, j - 1, 4, 4)))) continue;
                    options.Add((i, j));
                }
            if (options.Count == 0) return false;
            var (pi, pj) = options[random.Next(options.Count)];
            blocks.Add(new RectInt(pi, pj, 2, 2));
            // Bỏ 4 nhánh của nút giữa khối, giữ 8 đoạn viền.
            lattice.Edges.Remove((pi, pj + 1, true));
            lattice.Edges.Remove((pi + 1, pj + 1, true));
            lattice.Edges.Remove((pi + 1, pj, false));
            lattice.Edges.Remove((pi + 1, pj + 1, false));
            foreach (var e in new[] { (pi, pj, true), (pi + 1, pj, true), (pi, pj + 2, true), (pi + 1, pj + 2, true), (pi, pj, false), (pi, pj + 1, false), (pi + 2, pj, false), (pi + 2, pj + 1, false) })
                lattice.Fixed.Add(e);
            stops.Add(new Vector2Int(lattice.X[pi + 1], lattice.Y[pj]));
            return true;
        }

        private static int CountBits(int m) { int n = 0; for (; m != 0; m &= m - 1) n++; return n; }
    }
}
