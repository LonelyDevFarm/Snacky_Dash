using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnackDash.Editor.Restore;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Sinh màn mới từ màn có sẵn (cửa sổ LevelGeneratorWindow gọi): chép màn gốc, áp các cách của LevelRemixer, rồi cho
    /// LevelAutoPlayer chơi thử vài ván; chỉ màn thắng được mới lưu vào Data/Levels/Generated (định nghĩa + bản đồ JSON).
    /// Không sửa màn gốc. Màn sinh ra ghi nguồn gốc trong LevelDefinition.origin để tạo lại được.
    /// </summary>
    public static class LevelGenerator
    {
        public const string Folder = "Assets/_MyGame/Data/Levels/Generated/";
        public const string MapFolder = Folder + "Maps/";
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";
        private const string BoomerangTutorial = "11019_v4_nt_VO"; // khay ngoài tầm, cần boomerang: không làm nguồn

        public enum Mirror { Never, Always, Random }

        [Serializable]
        public class Options
        {
            public int fromLevel = 1, toLevel = 350;
            public int count = 5;
            public int seed = 1;
            public Mirror mirror = Mirror.Random;
            public bool permuteKinds = true, shuffleCrates = true, regroupWaves = true;
            [Tooltip("Số trái được xáo lẫn nhau khi chia lại đợt: nhỏ thì trái ra gần đúng thứ tự khay cần (dễ), lớn thì khó.")]
            public int mixWindow = 12;
            [Tooltip("-1 giữ độ khó màn gốc; 0 thường, 2 khó, 4 siêu khó.")]
            public int difficulty = -1;
            public int games = 3;
            public int triesPerLevel = 12;
        }

        /// <summary>Sinh <see cref="Options.count"/> màn; trả về các màn đã lưu. <paramref name="progress"/> trả true để dừng.</summary>
        public static List<LevelDefinition> Generate(Options options, Func<string, float, bool> progress = null)
        {
            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelDataImporter.LevelListPath);
            var sources = new List<(int number, LevelDefinition level)>();
            for (int i = Mathf.Max(1, options.fromLevel); i <= Mathf.Min(options.toLevel, list.levels.Count); i++)
            {
                var level = list.levels[i - 1];
                if (level == null || list.skipped.Contains(level) || IsGenerated(level) || level.name.EndsWith(BoomerangTutorial)) continue;
                sources.Add((i, level));
            }
            var saved = new List<LevelDefinition>();
            if (sources.Count == 0) return saved;
            var random = new System.Random(options.seed);
            int made = 0;
            for (int n = 0; n < options.count; n++)
            {
                for (int attempt = 0; attempt < options.triesPerLevel; attempt++)
                {
                    if (progress != null && progress($"Màn {n + 1}/{options.count}, lần thử {attempt + 1}", (n + attempt / (float)options.triesPerLevel) / options.count))
                        return saved;
                    var source = sources[random.Next(sources.Count)];
                    int seed = random.Next(1, int.MaxValue);
                    var candidate = Remix(source.level, options, seed, out string json, out string how);
                    if (candidate == null) continue;
                    if (!Validate(candidate, json, database, options.games, out int game, out int moves, out int snips))
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                        continue;
                    }
                    candidate.origin = $"Từ màn {source.number} ({source.level.name}); {how}; hạt {seed}; người chơi thử thắng ván {game}: {moves} lượt vuốt, {snips} lần cắn đuôi";
                    saved.Add(Save(candidate, json, source.level.name));
                    made++;
                    break;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelGenerator] {made}/{options.count} màn mới trong {Folder}");
            return saved;
        }

        /// <summary>Bản sao màn gốc đã biến đổi (chưa lưu) và bản đồ JSON của nó; null khi không cách nào áp được.</summary>
        public static LevelDefinition Remix(LevelDefinition source, Options options, int seed, out string json, out string how)
        {
            var random = new System.Random(seed);
            var level = UnityEngine.Object.Instantiate(source);
            json = source.map.text;
            var steps = new List<string>();
            bool mirror = options.mirror == Mirror.Always || (options.mirror == Mirror.Random && random.Next(2) == 0);
            if (mirror)
            {
                json = LevelRemixer.MirrorMap(json);
                LevelRemixer.MirrorData(level, TiledMap.Parse(json).Width);
                steps.Add("lật trái-phải");
            }
            if (options.permuteKinds) { LevelRemixer.PermuteKinds(level, random); steps.Add("đổi màu trái"); }
            if (options.shuffleCrates) { LevelRemixer.ShuffleCrates(level, random); steps.Add("chia lại khay"); }
            if (options.regroupWaves && LevelRemixer.RegroupWaves(level, TiledMap.Parse(json), random, options.mixWindow))
                steps.Add($"chia lại đợt (trộn {options.mixWindow})");
            if (options.difficulty >= 0) level.difficulty = options.difficulty;
            how = string.Join(", ", steps);
            if (steps.Count == 0) { UnityEngine.Object.DestroyImmediate(level); return null; }
            return level;
        }

        /// <summary>LevelAutoPlayer chơi tối đa <paramref name="games"/> ván (hạt 1..games, như test Level_IsWinnable); thắng một ván là đạt.</summary>
        public static bool Validate(LevelDefinition level, string json, TileDefinitionDatabase database, int games, out int game, out int moves, out int snips)
        {
            moves = snips = 0;
            for (game = 1; game <= games; game++)
            {
                try
                {
                    var map = TiledMap.Parse(json);
                    var session = new GameSession(new BoardGraph(map, database), map, level);
                    session.SetSeed(game);
                    session.Begin();
                    if (session.ExtraColumn >= 0) session.UnlockExtraColumn(); // như test: khay phụ mua ngay từ đầu
                    var player = new LevelAutoPlayer();
                    if (player.Play(session, game)) { moves = player.Moves; snips = player.Snips; return true; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LevelGenerator] {level.name}: {e.Message}");
                    return false;
                }
            }
            return false;
        }

        private static LevelDefinition Save(LevelDefinition level, string json, string sourceName)
        {
            Directory.CreateDirectory(MapFolder);
            int number = NextNumber();
            string name = $"Gen{number:000}_{sourceName}";
            string mapPath = MapFolder + name + ".json";
            File.WriteAllText(mapPath, json);
            AssetDatabase.ImportAsset(mapPath);
            level.map = AssetDatabase.LoadAssetAtPath<TextAsset>(mapPath);
            level.name = name;
            AssetDatabase.CreateAsset(level, Folder + name + ".asset");
            return level;
        }

        private static int NextNumber()
        {
            int max = 0;
            if (Directory.Exists(Folder))
                foreach (var file in Directory.GetFiles(Folder, "Gen*.asset"))
                {
                    var stem = Path.GetFileNameWithoutExtension(file);
                    int underscore = stem.IndexOf('_');
                    if (underscore > 3 && int.TryParse(stem.Substring(3, underscore - 3), out int n)) max = Mathf.Max(max, n);
                }
            return max + 1;
        }

        public static bool IsGenerated(LevelDefinition level) =>
            level != null && AssetDatabase.GetAssetPath(level).StartsWith(Folder);

        /// <summary>Mọi màn đã sinh, theo số thứ tự.</summary>
        public static List<LevelDefinition> All()
        {
            if (!Directory.Exists(Folder)) return new List<LevelDefinition>();
            return Directory.GetFiles(Folder, "Gen*.asset").OrderBy(f => f)
                .Select(f => AssetDatabase.LoadAssetAtPath<LevelDefinition>(f.Replace('\\', '/'))).Where(l => l != null).ToList();
        }
    }
}
