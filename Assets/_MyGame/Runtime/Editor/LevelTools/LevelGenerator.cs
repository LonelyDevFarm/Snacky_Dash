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

        /// <summary>Remix: cách 1-2 (một màn gốc, xáo dữ liệu / lật). Stitch: cách 3 (ghép nửa trái màn A với nửa phải màn B).</summary>
        public enum Method { Remix, Stitch }

        [Serializable]
        public class Options
        {
            public Method method = Method.Remix;
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
            List<LevelStitcher.Seam> seams = null;
            if (options.method == Method.Stitch)
            {
                progress?.Invoke("Tìm chỗ ghép giữa các bản đồ...", 0f);
                seams = LevelStitcher.FindSeams(sources.Select(s => s.level).ToList(), database, StitchMinWidth, StitchMaxWidth);
                // Bỏ cặp màn gốc đã ghép ở màn sinh trước (tên GenNNN_StitchAxB): khỏi ra nhiều bàn gần giống nhau.
                var used = new HashSet<string>(All().Select(l => l.name.Substring(l.name.IndexOf('_') + 1)));
                seams = seams.Where(s => !used.Contains($"Stitch{sources.First(x => x.level == s.a).number}x{sources.First(x => x.level == s.b).number}")).ToList();
                if (seams.Count == 0) { Debug.LogWarning("[LevelGenerator] Không có chỗ ghép nào trong khoảng màn đã chọn."); return saved; }
            }
            int NumberOf(LevelDefinition level) => sources.First(s => s.level == level).number;
            int made = 0;
            for (int n = 0; n < options.count; n++)
            {
                for (int attempt = 0; attempt < options.triesPerLevel; attempt++)
                {
                    if (progress != null && progress($"Màn {n + 1}/{options.count}, lần thử {attempt + 1}", (n + attempt / (float)options.triesPerLevel) / options.count))
                        return saved;
                    int seed = random.Next(1, int.MaxValue);
                    LevelDefinition candidate;
                    string json, how, sourceName;
                    (int number, LevelDefinition level) source = default;
                    if (seams != null)
                    {
                        var seam = seams[random.Next(seams.Count)];
                        // Bước 1: bàn đường thông (chỉ đường, hầm, khay) — phải đạt quy luật màn gốc trước khi có cơ chế nào.
                        candidate = LevelStitcher.Stitch(seam, database, new System.Random(seed), options.mixWindow, out json, out string why, roadsOnly: true);
                        if (candidate == null || !AnchorsComplete(json, database) || !LevelBuilder.Check(json, candidate, database, out why))
                        {
                            if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate);
                            continue;
                        }
                        // Bước 2: đặt cơ chế theo quy luật, mỗi cái kiểm lại bàn.
                        if (options.difficulty >= 0) candidate.difficulty = options.difficulty; // ngân sách độ khó theo bậc
                        var mechanics = LevelBuilder.AddMechanics(ref json, candidate, database, new System.Random(seed), out int unspent);
                        if (unspent > 1) { UnityEngine.Object.DestroyImmediate(candidate); continue; } // bàn này không đủ chỗ cho độ khó của bậc
                        how = $"ghép đường cột 0-{seam.xa - 1} màn {NumberOf(seam.a)} với cột {seam.xb}+ màn {NumberOf(seam.b)}; cơ chế: {(mechanics.Count > 0 ? string.Join(", ", mechanics) : "không")}; khay và đợt trái mới (trộn {options.mixWindow})";
                        sourceName = $"Stitch{NumberOf(seam.a)}x{NumberOf(seam.b)}";
                        source = (NumberOf(seam.a), seam.a);
                    }
                    else
                    {
                        source = sources[random.Next(sources.Count)];
                        candidate = Remix(source.level, options, seed, out json, out how);
                        sourceName = source.level.name;
                    }
                    if (candidate == null) continue;
                    if (!LevelBuilder.Check(json, candidate, database, out _)
                        || !Validate(candidate, json, database, options.games, out _, out _, out _)
                        || !SolveWithoutBoosters(candidate, json, database, out int moves, out int nodes)
                        || moves > MaxSolutionMoves)
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                        continue;
                    }
                    candidate.origin = (seams != null ? "" : $"Từ màn {source.number} ({source.level.name}); ") + $"{how}; hạt {seed}; thắng không booster: {moves} lượt vuốt, tìm {nodes} thế cờ";
                    saved.Add(Save(candidate, json, sourceName));
                    made++;
                    if (seams != null) seams.RemoveAll(s => s.a == source.level && sourceName == $"Stitch{NumberOf(s.a)}x{NumberOf(s.b)}"); // cặp này đã dùng
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

        /// <summary>Bề ngang bàn ghép: p10-p90 của 350 bản đồ gốc là 15-18 ô [đo 19/10].</summary>
        public const int StitchMinWidth = 15, StitchMaxWidth = 18;

        /// <summary>Mọi điểm giao khay có ô đặt khay kề bên (không thì khay không có chỗ đứng).</summary>
        private static bool AnchorsComplete(string json, TileDefinitionDatabase database)
        {
            var board = new BoardGraph(TiledMap.Parse(json), database);
            return board.CrateStops.Count > 0 && board.CrateStops.Keys.All(board.CrateAnchors.ContainsKey);
        }

        /// <summary>Giới hạn tìm của SmartPlayer khi kiểm màn sinh ra (329/345 màn gốc thắng trong giới hạn này, đo 19/10).</summary>
        public const int SolveMaxNodes = 6000, SolveMaxMoves = 600;

        /// <summary>Đường thắng dài nhất được nhận: màn gốc p50 ~190, p90 ~300 lượt; dài hơn thì màn lê thê [đo 19/10].</summary>
        public const int MaxSolutionMoves = 350;

        /// <summary>
        /// SmartPlayer tìm được đường thắng không dùng booster nào (không Snip, không hoàn tác) [user 19/10]; <paramref name="moves"/>
        /// là độ dài đường thắng, <paramref name="nodes"/> số thế cờ phải thử (càng nhiều càng khó).
        /// </summary>
        public static bool SolveWithoutBoosters(LevelDefinition level, string json, TileDefinitionDatabase database, out int moves, out int nodes)
        {
            Func<GameSession> newGame = () =>
            {
                var map = TiledMap.Parse(json);
                var session = new GameSession(new BoardGraph(map, database), map, level);
                session.SetSeed(1);
                session.Begin();
                if (session.ExtraColumn >= 0) session.UnlockExtraColumn(); // như test: khay phụ mua bằng xu, không phải booster
                return session;
            };
            var player = new SmartPlayer(newGame) { MaxNodes = SolveMaxNodes };
            bool won;
            try { won = player.Play(SolveMaxMoves); }
            catch (Exception e) { Debug.LogWarning($"[LevelGenerator] {level.name}: {e.Message}"); won = false; }
            moves = player.Moves;
            nodes = player.Nodes;
            return won;
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
                    // Cùng luật với test Level_IsWinnable: rắn không bao giờ dừng trên cầu hay miệng hầm.
                    bool stoppedOnBridge = false;
                    player.Moved += () => stoppedOnBridge |= session.Board.IsBridge(session.CurrentCell) || session.Board.TunnelMouths.ContainsKey(session.CurrentCell);
                    bool won = player.Play(session, game);
                    if (stoppedOnBridge) return false; // như test Level_IsWinnable: ván nào dừng trên cầu cũng hỏng
                    if (won) { moves = player.Moves; snips = player.Snips; return true; }
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
