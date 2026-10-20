using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Phần dùng chung của bộ sinh màn (cách 4, LevelDesigner; cách 1-3 đã bỏ [user 20/10: chỉ dùng cách 4]): ô xuất phát, ô giao
    /// khay, bảng viền cỏ học từ 350 bản đồ gốc, kiểm khay / ô rải trái đi tới và quay về được, tạo khay và chia đợt trái như màn gốc.
    /// </summary>
    public static class LevelParts
    {
        internal const int StartTile = 287;
        internal static readonly int[] DeliverTiles = { 295, 296, 297, 394, 395, 396, 397 }; // điểm giao khay 0..6

        /// <summary>Loại trái có đủ hình (icon trên sân, mô hình, khay); cà tím không màn nào dùng, thiếu icon.</summary>
        public static readonly PillKind[] UsableKinds =
            { PillKind.Strawberry, PillKind.Blueberry, PillKind.Banana, PillKind.Pear, PillKind.Apple, PillKind.Carrot };

        /// <summary>
        /// Điểm xuất phát với mọi điểm giao khay và mọi ô rải trái đi được cả đi lẫn về: theo cổng một chiều; đá, rèm coi như sẽ
        /// mở; mũi tên lật đi được hai chiều; ống rẽ / cầu bật chỉ đổi được khi bàn còn vạch công tắc. Cắt bàn có thể làm mất vạch
        /// công tắc của ống, hay tạo bẫy một chiều (vào được, không ra được).
        /// </summary>
        internal static bool Connected(TiledMap map, LevelDefinition level, TileDefinitionDatabase database)
        {
            BoardGraph Open(bool switched)
            {
                var board = new BoardGraph(map, database);
                board.SetCurtainGroups(level.curtainCells.Select(c => (c.cell, c.groupId)));
                foreach (int group in board.CurtainCells.Values.Distinct().ToList()) board.OpenCurtain(group);
                for (int g = 0; g < board.WallGroups.Count; g++) board.OpenWall(g);
                if (switched)
                {
                    foreach (var turnout in board.Turnouts) board.SetTurnoutB(turnout, true);
                    foreach (var bridge in board.SwitchBridges.Keys.ToList()) board.SetBridgeUp(bridge, false);
                }
                return board;
            }
            var normal = Open(false);
            if (!normal.IsWalkable(normal.PlayerStart)) return false;
            var other = normal.Switches.Count > 0 ? Open(true) : null;
            bool Step(Vector2Int from, Direction d) =>
                normal.CanMove(from, d) || (other != null && other.CanMove(from, d))
                || ((normal.FlippingArrows.ContainsKey(from) || normal.FlippingArrows.ContainsKey(from + d.ToCell())) && normal.HasRoad(from, d));
            IEnumerable<Vector2Int> Next(Vector2Int cell)
            {
                if (normal.TryGetTunnelOther(cell, out var exit)) yield return exit;
                foreach (var d in DirectionExtensions.All) if (Step(cell, d)) yield return cell + d.ToCell();
            }
            var forward = Reach(normal.PlayerStart, Next);
            var cells = forward.ToList();
            var edges = cells.ToDictionary(c => c, c => Next(c).ToList());
            var backward = new HashSet<Vector2Int> { normal.PlayerStart };
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (var cell in cells)
                    if (!backward.Contains(cell) && edges[cell].Any(backward.Contains)) { backward.Add(cell); grew = true; }
            }
            var needed = normal.CrateStops.Values.Concat(normal.PillSlotGroups.SelectMany(g => g));
            return needed.All(c => forward.Contains(c) && backward.Contains(c));
        }

        private static HashSet<Vector2Int> Reach(Vector2Int start, Func<Vector2Int, IEnumerable<Vector2Int>> next)
        {
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>(seen);
            while (queue.Count > 0)
                foreach (var n in next(queue.Dequeue()))
                    if (seen.Add(n)) queue.Enqueue(n);
            return seen;
        }

        // ------------------------------------------------------------------ viền cỏ

        internal static readonly Vector2Int[] Around =
        {
            new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 0),
            new Vector2Int(1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        };
        private static Dictionary<int, int> _grassByMask;

        internal static Dictionary<int, int> GrassTable(TileDefinitionDatabase database)
        {
            if (_grassByMask != null) return _grassByMask;
            var votes = new Dictionary<int, Dictionary<int, int>>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets/_MyGame/Data/Levels/Definitions" }))
            {
                var level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                var map = TiledMap.Parse(level.map.text);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        int local = map.GetLocalTileId("map", cell);
                        if (RoadLike(local, database) || IsEndPoint(local, database)) continue;
                        int mask = 0;
                        for (int k = 0; k < 8; k++) if (RoadLike(map.GetLocalTileId("map", cell + Around[k]), database)) mask |= 1 << k;
                        if (!votes.TryGetValue(mask, out var tally)) votes[mask] = tally = new Dictionary<int, int>();
                        tally[local] = tally.TryGetValue(local, out int n) ? n + 1 : 1;
                    }
            }
            _grassByMask = votes.ToDictionary(v => v.Key, v => v.Value.OrderByDescending(t => t.Value).First().Key);
            return _grassByMask;
        }

        /// <summary>Ô có đường (kể cả ô đặt khay, vì quanh khay cỏ vẽ như quanh đường).</summary>
        internal static bool RoadLike(int local, TileDefinitionDatabase database) =>
            local >= 0 && database.TryGet(local, out var d)
            && (d.category == TileCategory.CrateEndPoint || (d.tileType != TileType.Empty && d.tileType != TileType.None));

        private static bool IsEndPoint(int local, TileDefinitionDatabase database) =>
            local >= 0 && database.TryGet(local, out var d) && d.category == TileCategory.CrateEndPoint;

        // ------------------------------------------------------------------ khay và trái mới

        /// <summary>
        /// Khay: mỗi cột thường số khay bằng trung bình hai màn gốc, mỗi khay 9 trái (đôi khi 18), 3-5 loại trái. Đợt: cỡ đợt
        /// trung bình hai màn gốc (sức chứa bàn của chúng), rồi chia đợt như màn gốc (Regroup).
        /// </summary>
        internal static void FillFruit(LevelDefinition level, IList<LevelDefinition> sources, System.Random random, int mixWindow)
        {
            double depth = sources.SelectMany(s => s.columns.Where(c => !c.isExtra)).Select(c => (double)c.crates.Count).DefaultIfEmpty(4).Average();
            // Số loại trái theo bậc [user 20/10: từ 5 loại trở lên]: thường 5, khó 5-6, siêu khó 6 (đủ 6 loại có hình).
            int kindCount = level.difficulty >= 4 ? 6 : level.difficulty >= 2 ? random.Next(5, 7) : 5;
            var kinds = UsableKinds.OrderBy(k => random.Next()).Take(kindCount).ToList();
            int crateCount = 0;
            foreach (var column in level.columns.Where(c => !c.isExtra))
            {
                int n = Mathf.Clamp((int)Math.Round(depth + random.Next(-1, 2)), 2, 8);
                for (int i = 0; i < n; i++, crateCount++)
                    column.crates.Add(new CrateDefinition { kind = kinds[crateCount < kinds.Count ? crateCount : random.Next(kinds.Count)], requiredCount = random.Next(7) == 0 ? 18 : 9 });
            }
            // Đủ khay cho mọi loại đã chọn (một cột 4 khay thì 6 loại không có chỗ).
            var regular = level.columns.Where(c => !c.isExtra).ToList();
            for (int i = 0; crateCount < kinds.Count && regular.Count > 0; i++, crateCount++)
                regular[i % regular.Count].crates.Add(new CrateDefinition { kind = kinds[crateCount], requiredCount = 9 });
            int total = level.columns.Sum(c => c.crates.Sum(k => k.requiredCount));
            double waveSize = sources.SelectMany(s => s.batches).Select(b => (double)b.pills.Sum(p => p.count)).DefaultIfEmpty(27).Average();
            int size = Mathf.Clamp((int)Math.Round(waveSize), 9, 60);
            int waves = Mathf.Clamp(Mathf.CeilToInt(total / (float)size), MinWaves, MaxWaves); // [user 20/10: 3-5 đợt rải thêm sau đợt đầu]
            GrowFruit(level, waves, size, random, mixWindow); // đủ đợt mà mỗi đợt vẫn cỡ màn gốc: thêm khay [user 20/10]
            for (int i = 0; i < level.curtainGroups.Count; i++)
            {
                var g = level.curtainGroups[i];
                g.kind = kinds[random.Next(kinds.Count)];
                int ofKind = level.columns.Sum(c => c.crates.Where(k => k.kind == g.kind).Sum(k => k.requiredCount));
                g.count = Mathf.Clamp(g.count, 1, Mathf.Max(1, ofKind / 2));
                level.curtainGroups[i] = g;
            }
        }

        /// <summary>Số đợt trái của màn sinh ra, tính cả đợt có sẵn lúc vào màn: 4-6, tức 3-5 đợt rải thêm [user 20/10: 3-5 đợt không tính
        /// đợt đầu].</summary>
        public const int MinWaves = 4, MaxWaves = 6;

        /// <summary>
        /// Thêm khay 9 trái (loại đã có trong màn) vào cột thường ít khay nhất cho tới khi tổng trái gần đủ <paramref name="waves"/> ×
        /// <paramref name="waveSize"/>, rồi chia lại thành <paramref name="waves"/> đợt [user 20/10: tăng số đợt mà giữ cỡ mỗi đợt, tổng trái
        /// tăng]. Không bớt trái. Trả về số khay đã thêm.
        /// </summary>
        public static int GrowFruit(LevelDefinition level, int waves, int waveSize, System.Random random, int mixWindow = 12)
        {
            var kinds = level.columns.SelectMany(c => c.crates).Select(k => k.kind).Distinct().ToList();
            var regular = level.columns.Where(c => !c.isExtra).ToList();
            int added = 0, target = waves * waveSize;
            while (regular.Count > 0 && kinds.Count > 0 && level.columns.Sum(c => c.crates.Sum(k => k.requiredCount)) + 4 < target)
            {
                var column = regular.OrderBy(c => c.crates.Count).First();
                column.crates.Add(new CrateDefinition { kind = kinds[random.Next(kinds.Count)], requiredCount = 9 });
                added++;
            }
            Regroup(level, waves, random, mixWindow);
            return added;
        }

        /// <summary>Số loại trái tối thiểu của màn sinh ra [user 20/10: từ 5 loại trở lên].</summary>
        public const int MinKinds = 5;

        /// <summary>
        /// Nâng trái của màn đã sinh cho đúng <see cref="MinKinds"/> loại và <see cref="MinWaves"/>-<see cref="MaxWaves"/> đợt, giữ
        /// bản đồ, số khay và tổng trái: thiếu loại thì đổi một khay của loại đang nhiều khay nhất sang loại chưa có; số đợt ngoài
        /// khoảng thì kéo về mép gần nhất rồi chia lại đợt. Trả về false khi không đổi gì.
        /// </summary>
        public static bool EnsureFruit(LevelDefinition level, System.Random random, int mixWindow = 12)
        {
            bool changed = false;
            var used = new HashSet<PillKind>(level.columns.SelectMany(c => c.crates).Select(k => k.kind));
            while (used.Count < MinKinds)
            {
                var fresh = UsableKinds.Where(k => !used.Contains(k)).ToList();
                var common = level.columns.SelectMany(c => c.crates).GroupBy(k => k.kind).OrderByDescending(g => g.Count()).First();
                if (fresh.Count == 0 || common.Count() < 2) break; // loại nào cũng một khay: đổi thì mất loại cũ
                var targets = level.columns.SelectMany(c => Enumerable.Range(0, c.crates.Count).Where(i => c.crates[i].kind == common.Key).Select(i => (c, i))).ToList();
                var (column, index) = targets[random.Next(targets.Count)];
                var crate = column.crates[index];
                crate.kind = fresh[random.Next(fresh.Count)];
                column.crates[index] = crate;
                used.Add(crate.kind);
                changed = true;
            }
            int waves = Mathf.Clamp(level.batches.Count, MinWaves, MaxWaves);
            if (changed || waves != level.batches.Count)
            {
                Regroup(level, waves, random, mixWindow);
                changed = true;
            }
            return changed;
        }

        /// <summary>Phần mỗi đợt dành cho khay đang mở; còn lại cho khay chưa mở (màn gốc: ~50% trái đợt đầu thuộc khay chưa mở).</summary>
        public const float OpenShare = 0.5f;

        /// <summary>
        /// Chia trái của các khay thành <paramref name="waves"/> đợt đều nhau như màn gốc [user 20/10; đo 337 màn gốc rải ngẫu nhiên]:
        /// giả lập người chơi giao ngay khi được; mỗi đợt ~<see cref="OpenShare"/> cho khay đang mở — hai khay đang mở cùng màu thì chỉ
        /// đủ một khay (+0-2 trái), chia cho cả hai là thiếu — phần còn lại cho khay chưa mở theo số trái mỗi loại còn cần (đợt đầu đủ mọi
        /// loại: 90% màn gốc); đợt cuối rải hết phần còn lại. Tổng trái mỗi loại đúng bằng các khay cần.
        /// </summary>
        public static void Regroup(LevelDefinition level, int waves, System.Random random, int mixWindow = 12)
        {
            var columns = level.columns.Where(c => !c.isExtra && c.crates.Count > 0).ToList();
            var remaining = new Dictionary<PillKind, int>();
            foreach (var crate in level.columns.SelectMany(c => c.crates))
                remaining[crate.kind] = (remaining.TryGetValue(crate.kind, out int r) ? r : 0) + crate.requiredCount;
            int total = remaining.Values.Sum();
            var index = new int[columns.Count];
            var fill = new int[columns.Count];
            var pool = new Dictionary<PillKind, int>(); // trái đã rải mà chưa giao (người chơi đang mang / nằm trên bàn)
            int Pool(PillKind k) => pool.TryGetValue(k, out int v) ? v : 0;
            level.batches.Clear();
            for (int w = 0; w < waves; w++)
            {
                int size = total / waves + (w < total % waves ? 1 : 0);
                var wave = new Dictionary<PillKind, int>();
                int room = size;
                void Give(PillKind k, int n)
                {
                    n = Mathf.Min(n, room, remaining.TryGetValue(k, out int left) ? left : 0);
                    if (n <= 0) return;
                    wave[k] = (wave.TryGetValue(k, out int had) ? had : 0) + n;
                    remaining[k] -= n;
                    room -= n;
                }
                if (w == waves - 1)
                    foreach (var k in remaining.Keys.ToList()) { room = int.MaxValue; Give(k, remaining[k]); } // đợt cuối: hết phần còn lại
                else
                {
                    // 1. Khay đang mở (một cột một khay): thiếu bao nhiêu thì cho; cùng màu nhiều khay thì chỉ đủ một khay.
                    int openRoom = Mathf.RoundToInt(size * OpenShare);
                    var open = new Dictionary<PillKind, List<int>>();
                    for (int i = 0; i < columns.Count; i++)
                        if (index[i] < columns[i].crates.Count)
                        {
                            var crate = columns[i].crates[index[i]];
                            if (!open.TryGetValue(crate.kind, out var needs)) open[crate.kind] = needs = new List<int>();
                            needs.Add(crate.requiredCount - fill[i]);
                        }
                    foreach (var pair in open.OrderBy(_ => random.Next()))
                    {
                        int need = pair.Value.Count >= 2 ? pair.Value.Max() + random.Next(0, 3) : pair.Value.Sum();
                        int give = Mathf.Min(Mathf.Max(0, need - Pool(pair.Key)), openRoom);
                        int before = room;
                        Give(pair.Key, give);
                        openRoom -= before - room;
                    }
                    // 2. Khay chưa mở: phần còn lại chia theo số trái mỗi loại còn cần; đợt đầu loại nào cũng có.
                    var kinds = remaining.Where(p => p.Value > 0).Select(p => p.Key).OrderBy(_ => random.Next()).ToList();
                    if (w == 0) foreach (var k in kinds) if (!wave.ContainsKey(k)) Give(k, Mathf.Min(3, Mathf.Max(1, room / Mathf.Max(1, kinds.Count))));
                    for (int guard = 0; room > 0 && guard < 50; guard++)
                    {
                        int left = remaining.Values.Sum();
                        if (left == 0) break;
                        foreach (var k in kinds)
                        {
                            if (room <= 0) break;
                            int share = Mathf.Max(1, Mathf.RoundToInt(room * remaining[k] / (float)left));
                            Give(k, Mathf.Min(share, random.Next(1, share + 2)));
                        }
                    }
                }
                level.batches.Add(new SpawnBatchDefinition { pills = wave.Where(p => p.Value > 0).Select(p => new PillCount { kind = p.Key, count = p.Value }).ToList() });
                // Người chơi giao ngay khi được: cập nhật khay và trái đang mang.
                foreach (var p in wave) pool[p.Key] = Pool(p.Key) + p.Value;
                for (bool progress = true; progress;)
                {
                    progress = false;
                    for (int i = 0; i < columns.Count; i++)
                    {
                        if (index[i] >= columns[i].crates.Count) continue;
                        var crate = columns[i].crates[index[i]];
                        int take = Mathf.Min(Pool(crate.kind), crate.requiredCount - fill[i]);
                        if (take <= 0) continue;
                        pool[crate.kind] -= take;
                        fill[i] += take;
                        progress = true;
                        if (fill[i] >= crate.requiredCount) { index[i]++; fill[i] = 0; }
                    }
                }
            }
        }

        /// <summary>Khung JSON Tiled tối thiểu mà TiledMap đọc (width, height, tilesets, layers name / data).</summary>
        internal static class TiledJson
        {
            [Serializable] public class Map { public int width, height, tilewidth, tileheight; public Tileset[] tilesets; public Layer[] layers; public string orientation = "orthogonal", type = "map"; }
            [Serializable] public class Tileset { public int firstgid; public string source; }
            [Serializable] public class Layer { public long[] data; public int width, height, id, x, y; public string name, type = "tilelayer"; public float opacity = 1f; public bool visible = true; }
        }
    }
}
