using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Sinh màn mới bằng cách tự thiết kế bản đồ khối cỏ (LevelDesigner; cửa sổ LevelGeneratorWindow gọi) [user 20/10: chỉ dùng cách
    /// này, bỏ xáo / lật màn gốc và cắt ghép]: dựng bàn, kiểm bàn, đặt cơ chế theo ý đồ, rồi cho LevelAutoPlayer và SmartPlayer chơi
    /// thử; chỉ màn thắng được mới lưu vào Data/Levels/Generated (định nghĩa + bản đồ JSON). Màn sinh ra ghi nguồn gốc trong
    /// LevelDefinition.origin để tạo lại được.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Danh sách màn chơi (350 màn gốc bản 1.15, màn 43 và 93 đã đổi chỗ, rồi các màn sinh ra).</summary>
        public const string LevelListPath = "Assets/_MyGame/Data/Levels/LevelList.asset";
        /// <summary>Số màn gốc đã nhập (cohort ab-vo-co bản 1.15) đứng đầu danh sách màn.</summary>
        public const int OriginalLevelCount = 350;
        public const string Folder = "Assets/_MyGame/Data/Levels/Generated/";
        public const string MapFolder = Folder + "Maps/";
        private const string DatabasePath = "Assets/_MyGame/Data/Levels/TileDefinitionDatabase.asset";

        [Serializable]
        public class Options
        {
            public int count = 5;
            public int seed = 1;
            [Tooltip("0 thường, 2 khó, 4 siêu khó.")]
            public int difficulty = 0;
            public int games = 3;
            public int triesPerLevel = 12;
            [Tooltip("Đặt cơ chế (cầu, cổng, tường, khối nâng...) lên bàn đường; tắt để chỉ xem hình đường.")]
            public bool mechanics = true;
            [Tooltip("Khay đôi (sáng tạo, màn sau 366): mỗi màn có một cặp khay chung điểm giao trên mép trên.")]
            public bool pairedCrates;
        }

        /// <summary>Sinh <see cref="Options.count"/> màn; trả về các màn đã lưu. <paramref name="progress"/> trả true để dừng.</summary>
        public static List<LevelDefinition> Generate(Options options, Func<string, float, bool> progress = null)
        {
            var database = AssetDatabase.LoadAssetAtPath<TileDefinitionDatabase>(DatabasePath);
            var saved = new List<LevelDefinition>();
            var random = new System.Random(options.seed);
            int made = 0;
            for (int n = 0; n < options.count; n++)
            {
                for (int attempt = 0; attempt < options.triesPerLevel; attempt++)
                {
                    if (progress != null && progress($"Màn {n + 1}/{options.count}, lần thử {attempt + 1}", (n + attempt / (float)options.triesPerLevel) / options.count))
                        return saved;
                    int seed = random.Next(1, int.MaxValue);
                    var candidate = LevelDesigner.Design(Mathf.Max(0, options.difficulty), database, new System.Random(seed), 12, out string json, out string why, options.pairedCrates);
                    if (candidate == null || !LevelBuilder.Check(json, candidate, database, out why))
                    {
                        if (candidate != null) UnityEngine.Object.DestroyImmediate(candidate);
                        continue;
                    }
                    var placed = new List<string>();
                    int unspent = 0;
                    string theme = null;
                    if (options.mechanics) placed = LevelBuilder.AddMechanics(ref json, candidate, database, new System.Random(seed), out unspent, out theme);
                    // Bàn không đặt được ý đồ chính hay không đủ chỗ cho ngân sách của bậc: bỏ.
                    if (unspent > 1 || (options.mechanics && theme == null)
                        || !LevelBuilder.Check(json, candidate, database, out _)
                        || !Validate(candidate, json, database, options.games, out _, out _, out _)
                        || !SolveWithoutBoosters(candidate, json, database, out int moves, out int nodes)
                        || moves > MaxSolutionMoves)
                    {
                        UnityEngine.Object.DestroyImmediate(candidate);
                        continue;
                    }
                    var size = TiledMap.Parse(json);
                    candidate.origin = $"tự thiết kế {size.Width}x{size.Height}, {candidate.columns.Count} cột khay{(options.pairedCrates ? " (có khay đôi)" : "")}; ý đồ: {theme ?? "không"}; cơ chế: {(placed.Count > 0 ? string.Join(", ", placed) : "không")}; hạt {seed}; thắng không booster: {moves} lượt vuốt, tìm {nodes} thế cờ";
                    saved.Add(Save(candidate, json, "Design"));
                    made++;
                    break;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelGenerator] {made}/{options.count} màn mới trong {Folder}");
            return saved;
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
