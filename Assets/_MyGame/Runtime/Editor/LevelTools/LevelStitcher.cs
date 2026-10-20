using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Cách 3, cắt ghép bàn: nửa trái màn A (cột 0..xa-1) nối nửa phải màn B (cột xb..hết), cùng chiều cao. Chỗ nối phải khớp
    /// đường: ở mỗi hàng, đường A chạy sang phải đúng khi đường B chạy sang trái, như trong cả hai bản đồ gốc (hình ô đường giữ
    /// đúng), và không có hai đường sát nhau mà không nối. Đánh số lại điểm giao khay, màu hầm, nhóm mũi tên lật; giữ một điểm
    /// xuất phát; viền cỏ dải nối lát lại bằng bảng học từ 350 bản đồ gốc (khớp 98.7%). Khay và đợt trái tạo mới, rải ngẫu nhiên.
    /// </summary>
    public static class LevelStitcher
    {
        public sealed class Seam
        {
            public LevelDefinition a, b;
            public int xa, xb;
            public int Width;
        }

        internal const int StartTile = 287, TailTile = 403;
        internal static readonly int[] DeliverTiles = { 295, 296, 297, 394, 395, 396, 397 }; // điểm giao khay 0..6
        private static readonly int[,] TunnelTiles = { { 279, 281, 278, 280 }, { 363, 360, 361, 362 }, { 382, 384, 385, 386 }, { 378, 380, 379, 381 } }; // [màu A-D, lên/xuống/trái/phải]
        private static readonly int[,] FlipArrowTiles = { { 300, 298 }, { 303, 301 }, { 306, 304 }, { 309, 307 } }; // [xuống/trái/phải/lên, nhóm 0/1]

        // ------------------------------------------------------------------ tìm chỗ nối

        /// <summary>Mọi chỗ nối hợp lệ giữa các màn (mỗi bên ít nhất 4 cột, bề ngang ghép trong [minWidth, maxWidth]).</summary>
        public static List<Seam> FindSeams(IList<LevelDefinition> levels, TileDefinitionDatabase database, int minWidth, int maxWidth)
        {
            var maps = levels.Select(l => TiledMap.Parse(l.map.text)).ToList();
            var boards = maps.Select(m => new BoardGraph(m, database)).ToList();
            var byKey = new Dictionary<string, List<(int level, int x)>>();
            for (int i = 0; i < maps.Count; i++)
                for (int x = 4; x <= maps[i].Width - 4; x++)
                {
                    string key = BoundaryKey(maps[i], boards[i], x);
                    if (key == null) continue;
                    if (!byKey.TryGetValue(key, out var bucket)) byKey[key] = bucket = new List<(int, int)>();
                    bucket.Add((i, x));
                }
            var parts = maps.Select(m => Objects(m)).ToList();
            var seams = new List<Seam>();
            foreach (var bucket in byKey.Values)
                foreach (var left in bucket)
                    foreach (var right in bucket)
                    {
                        if (left.level == right.level) continue;
                        int width = left.x + maps[right.level].Width - right.x;
                        if (width < minWidth || width > maxWidth) continue;
                        // Biết trước sẽ hỏng: cắt đôi cặp hầm / mũi tên lật, mất cả hai điểm xuất phát, không còn khay.
                        var l = parts[left.level]; var r = parts[right.level];
                        bool KeepLeft(int x) => x < left.x;
                        bool KeepRight(int x) => x >= right.x;
                        if (SplitsPair(l.pairs, KeepLeft) || SplitsPair(r.pairs, KeepRight)) continue;
                        if (!l.starts.Any(KeepLeft) && !r.starts.Any(KeepRight)) continue;
                        if (!l.stops.Any(KeepLeft) && !r.stops.Any(KeepRight)) continue;
                        if (!Compatible(maps[left.level], boards[left.level], left.x, maps[right.level], boards[right.level], right.x, database)) continue;
                        seams.Add(new Seam { a = levels[left.level], b = levels[right.level], xa = left.x, xb = right.x, Width = width });
                    }
            return seams;
        }

        /// <summary>Cột của các vật theo cặp (khoá: hầm màu / mũi tên nhóm), điểm xuất phát, điểm giao khay của một bản đồ.</summary>
        private static (List<(int x, string key)> pairs, List<int> starts, List<int> stops) Objects(TiledMap map)
        {
            var pairs = new List<(int, string)>(); var starts = new List<int>(); var stops = new List<int>();
            foreach (var layer in map.LayerNames())
            {
                if (IsWaveLayer(layer)) continue;
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        int local = map.GetLocalTileId(layer, new Vector2Int(x, y));
                        if (local < 0) continue;
                        if (local == StartTile) starts.Add(x);
                        if (Array.IndexOf(DeliverTiles, local) >= 0) stops.Add(x);
                        for (int r = 0; r < 4; r++)
                            for (int c = 0; c < 4; c++)
                                if (TunnelTiles[r, c] == local) pairs.Add((x, "tunnel" + r));
                        for (int r = 0; r < 4; r++)
                            for (int c = 0; c < 2; c++)
                                if (FlipArrowTiles[r, c] == local) pairs.Add((x, "arrow" + c));
                    }
            }
            return (pairs, starts, stops);
        }

        private static bool SplitsPair(List<(int x, string key)> pairs, Func<int, bool> kept) =>
            pairs.GroupBy(p => p.key).Any(g => { int n = g.Count(p => kept(p.x)); return n != 0 && n != g.Count(); });

        /// <summary>Chiều cao + các hàng có đường nối qua ranh giới giữa cột x-1 và x; null khi không hàng nào nối.</summary>
        private static string BoundaryKey(TiledMap map, BoardGraph board, int x)
        {
            var rows = new List<int>();
            for (int y = 0; y < map.Height; y++)
                if (board.HasRoad(new Vector2Int(x - 1, y), Direction.Right)) rows.Add(y);
            return rows.Count == 0 ? null : map.Height + "|" + string.Join(",", rows);
        }

        private static bool Compatible(TiledMap a, BoardGraph boardA, int xa, TiledMap b, BoardGraph boardB, int xb, TileDefinitionDatabase database)
        {
            for (int y = 0; y < a.Height; y++)
            {
                var cellA = new Vector2Int(xa - 1, y);
                var cellB = new Vector2Int(xb, y);
                bool walkA = boardA.IsWalkable(cellA), walkB = boardB.IsWalkable(cellB);
                bool joined = walkA && walkB && OpensSideways(a, cellA, database) && OpensSideways(b, cellB, database);
                bool wasA = boardA.HasRoad(cellA, Direction.Right), wasB = boardB.HasRoad(cellB + Vector2Int.left, Direction.Right);
                if (joined != wasA || joined != wasB) return false;
                if (!joined && walkA && walkB) return false; // hai đường sát nhau không nối: trông như lỗi
            }
            return true;
        }

        private static bool OpensSideways(TiledMap map, Vector2Int cell, TileDefinitionDatabase database)
        {
            int id = map.GetLocalTileId("map", cell);
            if (id < 0 || !database.TryGet(id, out var definition)) return false;
            return definition.tileType != TileType.RoadUpDown && definition.tileType != TileType.RoadUpDownNoPill;
        }

        // ------------------------------------------------------------------ ghép

        /// <summary>Màn ghép (chưa lưu) và bản đồ JSON; null kèm lý do khi không ghép được.</summary>
        public static LevelDefinition Stitch(Seam seam, TileDefinitionDatabase database, System.Random random, int mixWindow, out string json, out string why, bool roadsOnly = false)
        {
            json = null;
            var a = JsonUtility.FromJson<TiledJson.Map>(seam.a.map.text);
            var b = JsonUtility.FromJson<TiledJson.Map>(seam.b.map.text);
            int height = a.height, width = seam.Width, firstGid = a.tilesets[0].firstgid;
            int Shift(int x) => x < seam.xa ? x : x - seam.xa + seam.xb; // cột trên bàn ghép -> cột trong bản đồ nguồn
            bool FromA(int x) => x < seam.xa;

            var names = a.layers.Select(l => l.name).Concat(b.layers.Select(l => l.name)).Distinct()
                .Where(n => !IsWaveLayer(n)).ToList();
            var layers = new Dictionary<string, long[]>();
            foreach (var name in names)
            {
                var la = a.layers.FirstOrDefault(l => l.name == name);
                var lb = b.layers.FirstOrDefault(l => l.name == name);
                var data = new long[width * height];
                for (int row = 0; row < height; row++)
                    for (int x = 0; x < width; x++)
                    {
                        var source = FromA(x) ? la : lb;
                        if (source?.data != null) data[row * width + x] = source.data[row * source.width + Shift(x)];
                    }
                layers[name] = data;
            }
            int Local(long gid) => TiledMap.ToLocalTileId(gid, firstGid);
            long Gid(int local) => local + firstGid;
            // Chỉ ghép đường (LevelBuilder đặt cơ chế lại sau): bỏ mọi cơ chế ở lớp phủ, giữ hầm.
            if (roadsOnly) LevelBuilder.StripMechanics(layers, firstGid);

            // Một điểm xuất phát: giữ của nửa có nó (ưu tiên A).
            if (layers.TryGetValue("spawn", out var spawn))
            {
                bool startInA = Enumerable.Range(0, spawn.Length).Any(i => FromA(i % width) && Local(spawn[i]) == StartTile);
                bool startInB = Enumerable.Range(0, spawn.Length).Any(i => !FromA(i % width) && Local(spawn[i]) == StartTile);
                if (!startInA && !startInB) { why = "không còn điểm xuất phát"; return null; }
                for (int i = 0; i < spawn.Length; i++)
                    if ((Local(spawn[i]) == StartTile || Local(spawn[i]) == TailTile) && FromA(i % width) != startInA) spawn[i] = 0;
            }

            // Điểm giao khay: nửa B đánh số tiếp sau nửa A.
            if (!Renumber(layers["map"], width, firstGid, DeliverTiles, FromA, out var deliverMap, out why)) return null;
            // Màu hầm: mỗi nửa mỗi màu đủ 2 miệng; nửa B đổi sang màu trống nếu trùng.
            if (!RecolorPairs(layers, width, firstGid, TunnelTiles, FromA, "hầm", out why)) return null;
            if (!RecolorPairs(layers, width, firstGid, FlipArrowTiles, FromA, "mũi tên lật", out why)) return null;

            LayGrass(layers["map"], width, height, seam.xa, firstGid, database);

            // Dữ liệu màn mới.
            var level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.difficulty = Mathf.Max(seam.a.difficulty, seam.b.difficulty);
            level.wallHealth = seam.a.wallHealth > 0 ? seam.a.wallHealth : seam.b.wallHealth;
            level.risingBlockDeliveriesToLower = seam.a.risingBlockDeliveriesToLower;
            Vector2Int ToNew(Vector2Int c, bool fromA) => fromA ? c : new Vector2Int(c.x - seam.xb + seam.xa, c.y);
            bool Kept(Vector2Int c, bool fromA) => fromA ? c.x < seam.xa : c.x >= seam.xb;
            foreach (var (source, fromA) in roadsOnly ? new (LevelDefinition, bool)[0] : new[] { (seam.a, true), (seam.b, false) })
            {
                foreach (var w in source.wallGroupHealths)
                    if (Kept(w.seedCell, fromA)) level.wallGroupHealths.Add(new WallGroupHealth { seedCell = ToNew(w.seedCell, fromA), health = w.health });
                int offset = fromA ? 0 : 1000;
                foreach (var c in source.curtainCells)
                    if (Kept(c.cell, fromA)) level.curtainCells.Add(new CurtainCell { cell = ToNew(c.cell, fromA), groupId = c.groupId + offset });
                foreach (var g in source.curtainGroups)
                {
                    if (!level.curtainCells.Any(c => c.groupId == g.groupId + offset)) continue;
                    var min = ToNew(g.counterMin, fromA); var max = ToNew(g.counterMax, fromA);
                    int lo = fromA ? 0 : seam.xa, hi = fromA ? seam.xa - 1 : width - 1;
                    min.x = Mathf.Clamp(min.x, lo, hi); max.x = Mathf.Clamp(max.x, lo, hi);
                    level.curtainGroups.Add(new CurtainGroupDefinition { groupId = g.groupId + offset, counterMin = min, counterMax = max, kind = g.kind, count = g.count });
                }
            }
            // Cột khay theo số điểm giao mới, giữ cờ khay phụ / đóng lúc đầu của cột gốc.
            int columns = deliverMap.Count;
            for (int i = 0; i < columns; i++)
            {
                var (fromA, oldIndex) = deliverMap[i];
                var source = fromA ? seam.a : seam.b;
                var old = oldIndex < source.columns.Count ? source.columns[oldIndex] : null;
                level.columns.Add(new CrateColumnDefinition { closedAtStart = old != null && old.closedAtStart, isExtra = old != null && old.isExtra });
            }
            if (level.columns.All(c => c.isExtra)) { why = "không có khay thường"; return null; }
            FillFruit(level, new[] { seam.a, seam.b }, random, mixWindow);

            var output = new TiledJson.Map
            {
                width = width, height = height, tilewidth = a.tilewidth, tileheight = a.tileheight,
                tilesets = a.tilesets,
                layers = names.Select((n, i) => new TiledJson.Layer { name = n, data = layers[n], width = width, height = height, id = i + 1 }).ToArray(),
            };
            json = JsonUtility.ToJson(output);
            if (!Connected(TiledMap.Parse(json), level, database))
            {
                UnityEngine.Object.DestroyImmediate(level);
                why = "đường bị đứt";
                return null;
            }
            why = null;
            return level;
        }

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

        private static bool IsWaveLayer(string name) =>
            (name.StartsWith("spawn") || name.StartsWith("ldf")) && name.Length > 3 && char.IsDigit(name[name.Length - 1]);

        /// <summary>Điểm giao khay: giữ số của A, nửa B lấy số trống kế tiếp; trả về số mới -> (từ A?, số cũ).</summary>
        private static bool Renumber(long[] data, int width, int firstGid, int[] tiles, Func<int, bool> fromA,
            out List<(bool fromA, int old)> mapping, out string why)
        {
            mapping = new List<(bool, int)>();
            var oldToNew = new Dictionary<(bool, int), int>();
            foreach (bool pass in new[] { true, false })
                for (int i = 0; i < data.Length; i++)
                {
                    if (fromA(i % width) != pass) continue;
                    int index = Array.IndexOf(tiles, TiledMap.ToLocalTileId(data[i], firstGid));
                    if (index < 0) continue;
                    if (!oldToNew.TryGetValue((pass, index), out int fresh))
                    {
                        fresh = mapping.Count;
                        if (fresh >= tiles.Length) { why = "quá 7 khay"; return false; }
                        oldToNew[(pass, index)] = fresh;
                        mapping.Add((pass, index));
                    }
                    data[i] = tiles[fresh] + firstGid;
                }
            why = mapping.Count > 0 ? null : "không còn khay";
            return why == null;
        }

        /// <summary>Vật đi theo cặp (hầm, mũi tên lật) theo [hướng / loại, nhóm]: mỗi nửa mỗi nhóm 0 hoặc 2 cái; nửa B đổi nhóm nếu trùng.</summary>
        private static bool RecolorPairs(Dictionary<string, long[]> layers, int width, int firstGid, int[,] tiles, Func<int, bool> fromA, string what, out string why)
        {
            bool byRow = tiles.GetLength(1) == 4; // hầm: [màu, hướng] (nhóm = hàng); mũi tên: [hướng, nhóm] (nhóm = cột)
            int groups = byRow ? tiles.GetLength(0) : tiles.GetLength(1);
            int GroupOf(int local, out int other)
            {
                for (int r = 0; r < tiles.GetLength(0); r++)
                    for (int c = 0; c < tiles.GetLength(1); c++)
                        if (tiles[r, c] == local) { other = byRow ? c : r; return byRow ? r : c; }
                other = -1; return -1;
            }
            int Tile(int group, int other) => byRow ? tiles[group, other] : tiles[other, group];
            var count = new int[2, groups];
            foreach (var data in layers.Values)
                for (int i = 0; i < data.Length; i++)
                {
                    int g = GroupOf(TiledMap.ToLocalTileId(data[i], firstGid), out _);
                    if (g >= 0) count[fromA(i % width) ? 0 : 1, g]++;
                }
            var remap = new int[groups];
            var used = new bool[groups];
            for (int g = 0; g < groups; g++)
            {
                if (count[0, g] % 2 != 0 || count[1, g] % 2 != 0 || count[0, g] > 2 || count[1, g] > 2) { why = what + " bị cắt mất một đầu"; return false; }
                if (count[0, g] > 0) used[g] = true;
            }
            for (int g = 0; g < groups; g++)
            {
                remap[g] = g;
                if (count[1, g] == 0) continue;
                if (used[g])
                {
                    int free = Array.IndexOf(used, false);
                    if (free < 0) { why = "hết nhóm " + what; return false; }
                    remap[g] = free;
                }
                used[remap[g]] = true;
            }
            foreach (var data in layers.Values)
                for (int i = 0; i < data.Length; i++)
                {
                    if (fromA(i % width)) continue;
                    int g = GroupOf(TiledMap.ToLocalTileId(data[i], firstGid), out int other);
                    if (g >= 0 && remap[g] != g) data[i] = Tile(remap[g], other) + firstGid | (data[i] & TiledMap.FlipMask);
                }
            why = null;
            return true;
        }

        // ------------------------------------------------------------------ viền cỏ

        internal static readonly Vector2Int[] Around =
        {
            new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 0),
            new Vector2Int(1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        };
        private static Dictionary<int, int> _grassByMask;

        /// <summary>Ô cỏ (không phải đường, không phải ô đặt khay) ở dải 4 cột quanh chỗ nối lấy ô viền hay gặp nhất với cùng 8 ô xung quanh.</summary>
        private static void LayGrass(long[] map, int width, int height, int seamX, int firstGid, TileDefinitionDatabase database)
        {
            var table = GrassTable(database);
            int LocalAt(int x, int y) => x < 0 || y < 0 || x >= width || y >= height ? -1 : TiledMap.ToLocalTileId(map[(height - 1 - y) * width + x], firstGid);
            for (int y = 0; y < height; y++)
                for (int x = Mathf.Max(0, seamX - 2); x <= Mathf.Min(width - 1, seamX + 1); x++)
                {
                    int local = LocalAt(x, y);
                    if (RoadLike(local, database) || IsEndPoint(local, database)) continue;
                    int mask = 0;
                    for (int k = 0; k < 8; k++) if (RoadLike(LocalAt(x + Around[k].x, y + Around[k].y), database)) mask |= 1 << k;
                    if (table.TryGetValue(mask, out int best)) map[(height - 1 - y) * width + x] = best < 0 ? 0 : best + firstGid;
                }
        }

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
        /// trung bình hai màn gốc (sức chứa bàn của chúng), rồi chia trái theo thứ tự khay cần như LevelRemixer.RegroupWaves.
        /// </summary>
        internal static void FillFruit(LevelDefinition level, IList<LevelDefinition> sources, System.Random random, int mixWindow)
        {
            double depth = sources.SelectMany(s => s.columns.Where(c => !c.isExtra)).Select(c => (double)c.crates.Count).DefaultIfEmpty(4).Average();
            // Số loại trái theo bậc [user 20/10: từ 5 loại trở lên]: thường 5, khó 5-6, siêu khó 6 (đủ 6 loại có hình).
            int kindCount = level.difficulty >= 4 ? 6 : level.difficulty >= 2 ? random.Next(5, 7) : 5;
            var kinds = LevelRemixer.UsableKinds.OrderBy(k => random.Next()).Take(kindCount).ToList();
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
                var fresh = LevelRemixer.UsableKinds.Where(k => !used.Contains(k)).ToList();
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
