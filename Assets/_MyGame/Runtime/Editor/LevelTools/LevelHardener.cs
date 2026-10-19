using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Làm khó màn đã sinh, giữ nguyên bản đồ [user 19/10]: thêm từng loại trái (đổi một khay sang loại mới) khi máy còn thắng, tới đủ
    /// loại; vẫn thắng thì thêm từng vật chặn theo đủ luật đặt (LevelBuilder.PlaceOne); vật chặn làm máy thua thì gỡ, đặt chỗ / hướng
    /// khác; thử hết chỗ mà vẫn thua thì sang loại vật chặn khác. Mỗi bước phải qua cả SmartPlayer (thắng không booster, ≤
    /// MaxSolutionMoves lượt) lẫn LevelGenerator.Validate (như test Level_IsWinnable). Giới hạn theo bậc để màn thường không khó bằng
    /// màn siêu khó: <see cref="MaxKinds"/>, <see cref="MaxExtraBlockers"/>.
    /// </summary>
    public static class LevelHardener
    {
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";
        private const int PlacementsPerBlocker = 4; // số chỗ / hướng thử cho một vật chặn trước khi đổi loại

        private static int MaxKinds(LevelDefinition level) => level.difficulty >= 2 ? LevelRemixer.UsableKinds.Length : 5;
        private static int MaxExtraBlockers(LevelDefinition level) => level.difficulty >= 4 ? 99 : level.difficulty >= 2 ? 4 : 2;

        /// <summary>Kết quả một màn: số loại trái / vật chặn thêm được, độ khó trước → sau (số thế cờ SmartPlayer phải thử).</summary>
        public struct Result
        {
            public int addedKinds, addedBlockers, nodesBefore, nodesAfter, movesAfter;
            public List<string> blockers;
        }

        /// <summary>Làm khó <paramref name="level"/> rồi lưu đè bản đồ và dữ liệu màn; false khi màn hiện tại máy đã không thắng.</summary>
        public static bool Harden(LevelDefinition level, int seed, out Result result)
        {
            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            var random = new System.Random(seed);
            string json = level.map.text;
            result = new Result { blockers = new List<string>() };
            if (!Wins(level, json, database, out _, out result.nodesBefore)) return false;
            result.nodesAfter = result.nodesBefore;

            // 1. Loại trái.
            int waves = Mathf.Max(1, level.batches.Count);
            while (Kinds(level).Count < MaxKinds(level))
            {
                var backup = Snapshot(level);
                if (!AddKind(level, random) ) break;
                LevelStitcher.Regroup(level, waves, random, 12);
                if (Wins(level, json, database, out result.movesAfter, out int nodes)) { result.addedKinds++; result.nodesAfter = nodes; continue; }
                Restore(level, backup);
                break;
            }

            // 2. Vật chặn.
            var exhausted = new HashSet<string>();
            while (result.addedBlockers < MaxExtraBlockers(level))
            {
                var kinds = LevelBuilder.Blockers.Where(b => !exhausted.Contains(b)).ToList();
                if (kinds.Count == 0) break;
                string name = kinds[random.Next(kinds.Count)];
                bool added = false;
                for (int i = 0; i < PlacementsPerBlocker && !added; i++)
                {
                    string candidate = json;
                    var backup = Snapshot(level);
                    var placed = new List<string>();
                    if (!LevelBuilder.PlaceOne(ref candidate, level, database, random, name, placed)) continue; // không còn chỗ hợp lệ
                    if (Wins(level, candidate, database, out result.movesAfter, out int nodes))
                    {
                        json = candidate;
                        result.nodesAfter = nodes;
                        result.addedBlockers++;
                        result.blockers.Add(name);
                        added = true;
                    }
                    else Restore(level, backup); // máy thua: gỡ, thử chỗ / hướng khác
                }
                if (!added) exhausted.Add(name);
            }

            // 3. Lưu đè.
            File.WriteAllText(AssetDatabase.GetAssetPath(level.map), json);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(level.map));
            Wins(level, json, database, out result.movesAfter, out result.nodesAfter);
            level.origin += $"; làm khó: +{result.addedKinds} loại trái, +{result.addedBlockers} vật chặn ({string.Join(", ", result.blockers)}), {Kinds(level).Count} loại trái; thắng không booster: {result.movesAfter} lượt vuốt, tìm {result.nodesAfter} thế cờ";
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool Wins(LevelDefinition level, string json, TileDefinitionDatabase database, out int moves, out int nodes) =>
            LevelGenerator.SolveWithoutBoosters(level, json, database, out moves, out nodes) && moves <= LevelGenerator.MaxSolutionMoves
            && LevelGenerator.Validate(level, json, database, 3, out _, out _, out _);

        private static HashSet<PillKind> Kinds(LevelDefinition level) =>
            new HashSet<PillKind>(level.columns.SelectMany(c => c.crates).Select(k => k.kind));

        /// <summary>Đổi một khay của loại đang nhiều khay nhất sang một loại chưa có (giữ tổng trái).</summary>
        private static bool AddKind(LevelDefinition level, System.Random random)
        {
            var used = Kinds(level);
            var fresh = LevelRemixer.UsableKinds.Where(k => !used.Contains(k)).ToList();
            if (fresh.Count == 0) return false;
            var common = level.columns.SelectMany(c => c.crates).GroupBy(k => k.kind).OrderByDescending(g => g.Count()).First();
            if (common.Count() < 2) return false; // loại nào cũng một khay: đổi thì mất loại cũ
            var targets = new List<(CrateColumnDefinition column, int index)>();
            foreach (var column in level.columns)
                for (int i = 0; i < column.crates.Count; i++)
                    if (column.crates[i].kind == common.Key) targets.Add((column, i));
            var (col, idx) = targets[random.Next(targets.Count)];
            var crate = col.crates[idx];
            crate.kind = fresh[random.Next(fresh.Count)];
            col.crates[idx] = crate;
            return true;
        }

        private static string Snapshot(LevelDefinition level) => JsonUtility.ToJson(level);

        private static void Restore(LevelDefinition level, string snapshot)
        {
            var map = level.map;
            string name = level.name;
            JsonUtility.FromJsonOverwrite(snapshot, level);
            level.map = map;
            level.name = name;
        }
    }
}
