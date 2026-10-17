using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Luật biến một màn có sẵn thành màn mới (Level Generator gọi; không đụng asset gốc, làm trên bản sao).
    /// Cách 1, xáo dữ liệu: đổi màu trái, chia lại khay giữa các cột, chia lại các đợt trái (chỉ màn rải ngẫu nhiên).
    /// Cách 2, lật bàn trái ↔ phải: đổi ô có hướng theo bảng cặp ô (đọc từ TileDefinitionDatabase) và toạ độ đá / rèm.
    /// </summary>
    public static class LevelRemixer
    {
        /// <summary>Loại trái có đủ hình (icon trên sân, mô hình, khay); cà tím không màn nào dùng, thiếu icon.</summary>
        public static readonly PillKind[] UsableKinds =
            { PillKind.Strawberry, PillKind.Blueberry, PillKind.Banana, PillKind.Pear, PillKind.Apple, PillKind.Carrot };

        // ------------------------------------------------------------------ Cách 1: xáo dữ liệu

        /// <summary>Đổi mỗi loại trái của màn sang một loại khác (một-một) ở đợt, khay, rèm. Lớp spawn của bản đồ tự khớp vì id
        /// dấu rải gán theo thứ tự loại xuất hiện trong các đợt (SpawnIdMap).</summary>
        public static void PermuteKinds(LevelDefinition level, System.Random random)
        {
            var used = KindsIn(level);
            var pool = UsableKinds.OrderBy(k => random.Next()).ToList();
            var map = new Dictionary<PillKind, PillKind>();
            foreach (var kind in used) { map[kind] = pool[0]; pool.RemoveAt(0); }
            foreach (var batch in level.batches)
                for (int i = 0; i < batch.pills.Count; i++) { var p = batch.pills[i]; p.kind = map[p.kind]; batch.pills[i] = p; }
            foreach (var column in level.columns)
                for (int i = 0; i < column.crates.Count; i++) { var c = column.crates[i]; c.kind = Map(map, c.kind); column.crates[i] = c; }
            for (int i = 0; i < level.curtainGroups.Count; i++) { var g = level.curtainGroups[i]; g.kind = Map(map, g.kind); level.curtainGroups[i] = g; }
        }

        /// <summary>Xáo mọi khay giữa các cột thường; mỗi cột giữ số khay và trạng thái đóng lúc đầu, khay giữ cỡ của nó.</summary>
        public static void ShuffleCrates(LevelDefinition level, System.Random random)
        {
            var columns = level.columns.Where(c => !c.isExtra).ToList();
            var all = columns.SelectMany(c => c.crates).OrderBy(c => random.Next()).ToList();
            int next = 0;
            foreach (var column in columns)
                for (int i = 0; i < column.crates.Count; i++) column.crates[i] = all[next++];
        }

        /// <summary>
        /// Chia lại trái vào các đợt (giữ số trái mỗi đợt, vì đó là sức chứa của bàn): trái xếp theo thứ tự khay cần (lần lượt
        /// khay trên cùng mỗi cột, rồi khay kế) rồi xáo trong cửa sổ <paramref name="mixWindow"/> trái, nên lớn thì khó hơn.
        /// Chỉ cho màn rải ngẫu nhiên (bản đồ không có lớp spawn1); trả false khi không làm được.
        /// </summary>
        public static bool RegroupWaves(LevelDefinition level, TiledMap map, System.Random random, int mixWindow)
        {
            if (map.HasLayer("spawn1") || level.batches.Count == 0) return false;
            var order = new List<PillKind>();
            int depth = level.columns.Max(c => c.crates.Count);
            for (int d = 0; d < depth; d++)
                foreach (var column in level.columns)
                    if (d < column.crates.Count)
                        for (int n = 0; n < Mathf.Max(1, column.crates[d].requiredCount); n++) order.Add(column.crates[d].kind);
            var sizes = level.batches.Select(b => b.pills.Sum(p => p.count)).ToList();
            if (order.Count != sizes.Sum()) return false; // số trái không khớp số khay: giữ nguyên
            for (int i = 0; i < order.Count; i++)
            {
                int j = Mathf.Min(order.Count - 1, i + random.Next(Mathf.Max(1, mixWindow)));
                var t = order[i]; order[i] = order[j]; order[j] = t;
            }
            int at = 0;
            for (int w = 0; w < sizes.Count; w++)
            {
                var counts = new List<PillCount>();
                for (int n = 0; n < sizes[w]; n++, at++)
                {
                    int index = counts.FindIndex(p => p.kind == order[at]);
                    if (index < 0) counts.Add(new PillCount { kind = order[at], count = 1 });
                    else { var p = counts[index]; p.count++; counts[index] = p; }
                }
                level.batches[w].pills = counts;
            }
            return true;
        }

        private static PillKind Map(Dictionary<PillKind, PillKind> map, PillKind kind) => map.TryGetValue(kind, out var to) ? to : kind;

        private static List<PillKind> KindsIn(LevelDefinition level)
        {
            var kinds = new List<PillKind>();
            void Add(PillKind k) { if (!kinds.Contains(k)) kinds.Add(k); }
            foreach (var b in level.batches) foreach (var p in b.pills) Add(p.kind);
            foreach (var c in level.columns) foreach (var crate in c.crates) Add(crate.kind);
            foreach (var g in level.curtainGroups) Add(g.kind);
            return kinds;
        }

        // ------------------------------------------------------------------ Cách 2: lật bàn trái ↔ phải

        /// <summary>
        /// Cặp ô đối xứng trái ↔ phải (id cục bộ của tileset, theo tên và loại ô trong TileDefinitionDatabase): miệng hầm
        /// Left ↔ Right (4 màu), mũi tên Left ↔ Right, ống rẽ (UP / DOWN đổi nhánh, LEFT_U_Down ↔ RIGHT_Up_Down, Up_L ↔ Up_R),
        /// viền cỏ, góc cỏ trong / ngoài, đầu đảo, ngã ba CrossR ↔ CrossL. Ô khác (lên / xuống, thẳng, cầu, khay...) giữ nguyên.
        /// </summary>
        private static readonly int[,] MirrorPairs =
        {
            { 278, 280 }, { 361, 362 }, { 385, 386 }, { 379, 381 },
            { 301, 304 }, { 302, 305 }, { 303, 306 },
            { 311, 312 }, { 317, 318 }, { 314, 316 }, { 320, 321 },
            { 323, 324 }, { 326, 328 }, { 327, 329 }, { 330, 334 }, { 331, 335 }, { 332, 336 }, { 333, 337 }, { 339, 340 },
            { 353, 356 },
        };

        private static Dictionary<int, int> _mirror;

        private static int MirrorTile(int localId)
        {
            if (_mirror == null)
            {
                _mirror = new Dictionary<int, int>();
                for (int i = 0; i < MirrorPairs.GetLength(0); i++)
                {
                    _mirror[MirrorPairs[i, 0]] = MirrorPairs[i, 1];
                    _mirror[MirrorPairs[i, 1]] = MirrorPairs[i, 0];
                }
            }
            return _mirror.TryGetValue(localId, out int to) ? to : localId;
        }

        private static readonly Regex DataArray = new Regex(@"""data""\s*:\s*\[([^\]]*)\]");

        /// <summary>Bản đồ Tiled lật trái ↔ phải: mọi lớp có "data" đảo từng hàng và đổi ô theo cặp; bit lật của Tiled giữ nguyên.</summary>
        public static string MirrorMap(string json)
        {
            var map = TiledMap.Parse(json);
            int width = map.Width, firstGid = map.FirstGid;
            return DataArray.Replace(json, match =>
            {
                var values = match.Groups[1].Value.Split(',').Select(s => long.Parse(s.Trim())).ToArray();
                if (values.Length % width != 0) return match.Value;
                var result = new long[values.Length];
                for (int row = 0; row < values.Length / width; row++)
                    for (int x = 0; x < width; x++)
                    {
                        long raw = values[row * width + x];
                        uint flags = (uint)raw & TiledMap.FlipMask;
                        int local = TiledMap.ToLocalTileId(raw, firstGid);
                        long mirrored = local < 0 ? raw : (MirrorTile(local) + firstGid) | flags;
                        result[row * width + (width - 1 - x)] = mirrored;
                    }
                var sb = new StringBuilder(match.Value.Length);
                sb.Append("\"data\":[");
                for (int i = 0; i < result.Length; i++) { if (i > 0) sb.Append(','); sb.Append(result[i]); }
                return sb.Append(']').ToString();
            });
        }

        /// <summary>Toạ độ trong dữ liệu màn (ô rèm, hộp bộ đếm rèm, ô gốc của hàng đá) lật theo cùng trục.</summary>
        public static void MirrorData(LevelDefinition level, int width)
        {
            Vector2Int Flip(Vector2Int c) => new Vector2Int(width - 1 - c.x, c.y);
            for (int i = 0; i < level.curtainCells.Count; i++) { var c = level.curtainCells[i]; c.cell = Flip(c.cell); level.curtainCells[i] = c; }
            for (int i = 0; i < level.wallGroupHealths.Count; i++) { var w = level.wallGroupHealths[i]; w.seedCell = Flip(w.seedCell); level.wallGroupHealths[i] = w; }
            for (int i = 0; i < level.curtainGroups.Count; i++)
            {
                var g = level.curtainGroups[i];
                var min = Flip(g.counterMax); var max = Flip(g.counterMin);
                g.counterMin = new Vector2Int(min.x, g.counterMin.y);
                g.counterMax = new Vector2Int(max.x, g.counterMax.y);
                level.curtainGroups[i] = g;
            }
        }
    }
}
