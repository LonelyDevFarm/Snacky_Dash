#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.Restore
{
    /// <summary>
    /// Converts original LevelDataSO assets (YAML) into LevelDefinition assets, and the original
    /// GraphicSet_VO into a FruitGraphicSet. Only the fields the game uses are read:
    /// manualColumns, respawnSequence, _timeLimitSeconds, speedPlayer, the rock healths, the curtains
    /// (curtainCells, curtainGroups) and risingBlockDeliveriesToLower; the cohort order can be patched (Swaps).
    /// Source: the v1.15 export (the version on the user's phone), level order from its default
    /// cohort ab-vo-co; v1.15 shuffles levels from 15 on and uses newer variants (ftue).
    /// </summary>
    public static class LevelDataImporter
    {
        private const string ExportRoot = "G:/Game_2D/APK/Export_1.15_ShadersYaml/ExportedProject/Assets/";
        private const string SourceFolder = ExportRoot + "MonoBehaviour/";
        private const string MapSource = ExportRoot + "Resources/levelmaps/";
        private const string CohortSource = ExportRoot + "Resources/cohorts/ab-vo-co.asset";
        /// <summary>How many levels of the cohort are imported (more as mechanics get built).</summary>
        public const int LevelCount = 60;
        private const string GraphicSetSource = "G:/Game_2D/APK/ExportedProject/Assets/Resources/graphicssets/GraphicSet_VO.asset";
        private const string JsonFolder = "Assets/_MyGame/Data/Levels/Maps/";
        private const string OutputFolder = "Assets/_MyGame/Data/Levels/Definitions/";
        public const string LevelListPath = "Assets/_MyGame/Data/Levels/LevelList.asset";
        private const string GraphicSetOutput = "Assets/_MyGame/Data/FruitGraphicSet.asset";

        // Levels whose mechanic is not built yet: kept in the list (numbering) but skipped in play.
        private static readonly string[] SkippedForNow = { };

        /// <summary>
        /// Chỗ thứ tự gốc sai, đổi hai màn (số màn đếm từ 1). 43 có khối nâng và thẻ NEW ITEM của nó trước khi thanh mở khoá
        /// tới (khối nâng mở sau 50) [user, video Lv43]; 93 (5156_v2) chỉ có rèm.
        /// </summary>
        private static readonly (int a, int b)[] Swaps = { (43, 93) };

        private static string[] _levels;

        /// <summary>The first <see cref="LevelCount"/> levels of the cohort, in play order (LevelDataSO names).</summary>
        public static string[] Levels => _levels ?? (_levels = ReadCohort());

        private static string[] ReadCohort()
        {
            var names = new Dictionary<string, string>();
            foreach (var meta in Directory.GetFiles(SourceFolder, "*.asset.meta"))
            {
                var guidLine = File.ReadLines(meta).FirstOrDefault(l => l.StartsWith("guid: "));
                if (guidLine != null) names[guidLine.Substring(6).Trim()] = Path.GetFileName(meta).Replace(".asset.meta", "");
            }
            var order = new List<string>();
            bool inLevels = false;
            foreach (var line in File.ReadLines(CohortSource))
            {
                if (line.StartsWith("  levels:")) { inLevels = true; continue; }
                if (!inLevels) continue;
                if (!line.StartsWith("  - ")) break;
                int g = line.IndexOf("guid: ");
                if (g >= 0 && names.TryGetValue(line.Substring(g + 6, 32), out var name)) order.Add(name);
            }
            foreach (var (a, b) in Swaps)
                if (a <= order.Count && b <= order.Count) (order[a - 1], order[b - 1]) = (order[b - 1], order[a - 1]);
            return order.Take(LevelCount).ToArray();
        }

        /// <summary>Definitions/LevelNN_mapname.asset: sorted by play order, original map name kept [user].</summary>
        public static string AssetPath(string name) =>
            OutputFolder + $"Level{System.Array.IndexOf(Levels, name) + 1:00}_{name}.asset";

        [MenuItem("SnackDash/Restore/Import Level Data")]
        public static void ImportLevels()
        {
            Directory.CreateDirectory(OutputFolder);
            _levels = null; // re-read the cohort
            // Definitions from another version or order go (they are regenerated from the source data).
            var wanted = new HashSet<string>(Levels.Select(AssetPath));
            foreach (var old in Directory.GetFiles(OutputFolder, "*.asset").Select(p => p.Replace('\\', '/')))
                if (!wanted.Contains(old)) AssetDatabase.DeleteAsset(old);
            foreach (var name in Levels)
            {
                var lines = File.ReadAllLines(SourceFolder + name + ".asset");
                var path = AssetPath(name);
                var json = JsonFolder + name + ".json";
                // Always the v1.15 map: some names are the same in v1.14.1 with a different board (14, 15, 20...).
                if (!File.Exists(json) || File.ReadAllText(json) != File.ReadAllText(MapSource + name + ".json"))
                {
                    File.Copy(MapSource + name + ".json", json, true);
                    AssetDatabase.ImportAsset(json);
                }
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                    AssetDatabase.CreateAsset(level, path);
                }

                level.map = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonFolder + name + ".json");
                level.columns = ParseColumns(Block(lines, "  manualColumns:"));
                level.batches = ParseBatches(Block(lines, "  respawnSequence:"));
                level.timeLimitSeconds = Float(TopLevel(lines, "_timeLimitSeconds"));
                level.speedOverride = Float(TopLevel(lines, "<speedPlayer>k__BackingField"));
                level.wallHealth = Int(TopLevel(lines, "destructibleWallHealth"));
                level.difficulty = Int(TopLevel(lines, "<difficulty>k__BackingField"));
                level.wallGroupHealths = ParseWallGroups(Block(lines, "  destructibleWallGroupHealths:"));
                level.curtainCells = ParseCurtainCells(Block(lines, "  curtainCells:"));
                level.curtainGroups = ParseCurtainGroups(Block(lines, "  curtainGroups:"));
                level.risingBlockDeliveriesToLower = Int(TopLevel(lines, "risingBlockDeliveriesToLower"));
                EditorUtility.SetDirty(level);

                Debug.Log($"[Restore] {name}: {level.columns.Count} columns, " +
                          $"{level.columns.Sum(c => c.crates.Count)} crates, {level.batches.Count} waves, " +
                          $"timer {level.timeLimitSeconds}s, map {(level.map != null ? "ok" : "MISSING")}", level);
            }

            // Play order for GameBootstrap / LevelProgress.
            var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<LevelList>();
                AssetDatabase.CreateAsset(list, LevelListPath);
            }
            list.levels = Levels.Select(name => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetPath(name))).ToList();
            list.skipped = SkippedForNow.Select(name => AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetPath(name))).ToList();
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("SnackDash/Restore/Import Fruit Graphic Set")]
        public static void ImportGraphicSet()
        {
            var entries = new List<FruitGraphicSet.Entry>();
            FruitGraphicSet.Entry current = null;
            foreach (var raw in File.ReadLines(GraphicSetSource))
            {
                var line = raw.Trim();
                if (line.StartsWith("- PillKind:"))
                {
                    current = new FruitGraphicSet.Entry { kind = (PillKind)Int(Value(line)) };
                    entries.Add(current);
                }
                else if (current != null && line.StartsWith("Sprite:")) current.sprite = LoadRef<Sprite>(line);
                else if (current != null && line.StartsWith("SpriteNoOutline:")) current.spriteNoOutline = LoadRef<Sprite>(line);
                else if (current != null && line.StartsWith("MaterialCrate:")) current.crateMaterial = LoadRef<Material>(line);
                else if (current != null && line.StartsWith("OutlineColor:")) current.outlineColor = ParseColor(line);
            }

            var set = AssetDatabase.LoadAssetAtPath<FruitGraphicSet>(GraphicSetOutput);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FruitGraphicSet>();
                AssetDatabase.CreateAsset(set, GraphicSetOutput);
            }
            set.SetEntries(entries);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Restore] Fruit graphic set: {entries.Count} kinds, " +
                      $"{entries.Count(e => e.sprite != null)} sprites, {entries.Count(e => e.crateMaterial != null)} crate materials.", set);
        }

        private const string MainScene = "G:/Game_2D/APK/ExportedProject/Assets/Scenes/Main.unity";
        private const string OriginalAudioFolder = "G:/Game_2D/APK/ExportedProject/Assets/AudioClip/";
        private const string AudioFolder = "Assets/_MyGame/Source/Audio/"; // nhạc nền (tên có "Music") vào Music/, còn lại SFX/
        private const string SoundLibraryOutput = "Assets/_MyGame/Data/SoundLibrary.asset";
        private const string SoundManagerScriptGuid = "2e83bdd43bac007489ebc82f32c29983";

        /// <summary>
        /// Reads the original sound table ("sounds:" list of the sound manager in Main.unity). The audio
        /// copied earlier got new GUIDs, so clips are matched by file name; missing clips are copied over.
        /// </summary>
        [MenuItem("SnackDash/Restore/Import Sound Library")]
        public static void ImportSoundLibrary()
        {
            var nameByOriginalGuid = Directory.GetFiles(OriginalAudioFolder, "*.meta")
                .ToDictionary(meta => File.ReadLines(meta).First(l => l.StartsWith("guid: ")).Substring(6).Trim(),
                              meta => Path.GetFileName(meta.Substring(0, meta.Length - 5)));

            var lines = File.ReadAllLines(MainScene);
            int script = System.Array.FindIndex(lines, l => l.Contains("guid: " + SoundManagerScriptGuid));
            var sounds = new List<SoundLibraryEntry>();
            for (int i = script + 1; i < lines.Length && !lines[i].StartsWith("--- "); i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("- name:")) sounds.Add(new SoundLibraryEntry { name = Value(line) });
                else if (sounds.Count == 0) continue;
                else if (line.StartsWith("clip:") && line.Contains("guid: ")) sounds[sounds.Count - 1].clipGuid = line.Substring(line.IndexOf("guid: ") + 6, 32);
                else if (line.StartsWith("volume:")) sounds[sounds.Count - 1].volume = Float(Value(line));
                else if (line.StartsWith("pitch:")) sounds[sounds.Count - 1].pitch = Float(Value(line));
                else if (line.StartsWith("looping:")) sounds[sounds.Count - 1].looping = Int(Value(line)) != 0;
            }

            var result = new List<SnackDash.Audio.SoundLibrary.Sound>();
            var missing = new List<string>();
            foreach (var entry in sounds)
            {
                AudioClip clip = null;
                if (entry.clipGuid != null && nameByOriginalGuid.TryGetValue(entry.clipGuid, out var file))
                {
                    var path = AudioFolder + (file.Contains("Music") ? "Music/" : "SFX/") + file;
                    if (!File.Exists(path))
                    {
                        File.Copy(OriginalAudioFolder + file, path);
                        File.Copy(OriginalAudioFolder + file + ".meta", path + ".meta");
                        AssetDatabase.ImportAsset(path);
                    }
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                if (clip == null) missing.Add(entry.name);
                result.Add(new SnackDash.Audio.SoundLibrary.Sound { name = entry.name, clip = clip, volume = entry.volume, pitch = entry.pitch, looping = entry.looping });
            }

            var library = AssetDatabase.LoadAssetAtPath<SnackDash.Audio.SoundLibrary>(SoundLibraryOutput);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SnackDash.Audio.SoundLibrary>();
                AssetDatabase.CreateAsset(library, SoundLibraryOutput);
            }
            library.SetSounds(result);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Restore] Sound library: {result.Count} sounds, {missing.Count} without clip: {string.Join(", ", missing)}", library);
        }

        private class SoundLibraryEntry
        {
            public string name;
            public string clipGuid;
            public float volume = 1f;
            public float pitch = 1f;
            public bool looping;
        }

        private static List<CrateColumnDefinition> ParseColumns(List<string> block)
        {
            var columns = new List<CrateColumnDefinition>();
            CrateColumnDefinition column = null;
            foreach (var raw in block)
            {
                var line = raw.Trim();
                if (raw.StartsWith("  - crates:")) { column = new CrateColumnDefinition(); columns.Add(column); }
                else if (column == null) continue;
                else if (line.StartsWith("- pillKind:")) column.crates.Add(new CrateDefinition { kind = (PillKind)Int(Value(line)) });
                else if (line.StartsWith("requiredCount:") && column.crates.Count > 0)
                {
                    var crate = column.crates[column.crates.Count - 1];
                    crate.requiredCount = Int(Value(line));
                    column.crates[column.crates.Count - 1] = crate;
                }
                else if (line.StartsWith("isClosedAtStart:")) column.closedAtStart = Int(Value(line)) != 0;
                else if (line.StartsWith("isExtra:")) column.isExtra = Int(Value(line)) != 0;
            }
            return columns;
        }

        private static List<SpawnBatchDefinition> ParseBatches(List<string> block)
        {
            var batches = new List<SpawnBatchDefinition>();
            SpawnBatchDefinition batch = null;
            foreach (var raw in block)
            {
                var line = raw.Trim();
                if (line.StartsWith("- colors:")) { batch = new SpawnBatchDefinition(); batches.Add(batch); }
                else if (batch == null) continue;
                else if (line.StartsWith("- pillKind:")) batch.pills.Add(new PillCount { kind = (PillKind)Int(Value(line)) });
                else if (line.StartsWith("count:") && batch.pills.Count > 0)
                {
                    var pill = batch.pills[batch.pills.Count - 1];
                    pill.count = Int(Value(line));
                    batch.pills[batch.pills.Count - 1] = pill;
                }
            }
            return batches;
        }

        /// <summary>Lines nested under a top-level (2-space) key, until the next top-level key.</summary>
        /// <summary>destructibleWallGroupHealths entries: "- groupId", "seedCell: {x, y}", "cellCount", "health".</summary>
        private static List<WallGroupHealth> ParseWallGroups(List<string> block)
        {
            var groups = new List<WallGroupHealth>();
            var group = new WallGroupHealth();
            foreach (var line in block)
            {
                var t = line.Trim();
                if (t.StartsWith("seedCell:"))
                {
                    var parts = t.Substring(t.IndexOf('{') + 1).TrimEnd('}').Split(',');
                    group.seedCell = new Vector2Int(Int(Value(parts[0])), Int(Value(parts[1])));
                }
                else if (t.StartsWith("health:"))
                {
                    group.health = Int(Value(t));
                    groups.Add(group);
                    group = new WallGroupHealth();
                }
            }
            return groups;
        }

        /// <summary>curtainCells entries: "- cell: {x, y}", "groupId".</summary>
        private static List<CurtainCell> ParseCurtainCells(List<string> block)
        {
            var cells = new List<CurtainCell>();
            foreach (var line in block)
            {
                var t = line.Trim();
                if (t.StartsWith("- cell:")) cells.Add(new CurtainCell { cell = Cell(t) });
                else if (t.StartsWith("groupId:") && cells.Count > 0)
                {
                    var c = cells[cells.Count - 1];
                    c.groupId = Int(Value(t));
                    cells[cells.Count - 1] = c;
                }
            }
            return cells;
        }

        /// <summary>curtainGroups entries: "- groupId", seedCell, cellCount, counterPlacementMin/MaxCell, requiredKind, requiredCount.</summary>
        private static List<CurtainGroupDefinition> ParseCurtainGroups(List<string> block)
        {
            var groups = new List<CurtainGroupDefinition>();
            var group = new CurtainGroupDefinition();
            foreach (var line in block)
            {
                var t = line.Trim();
                if (t.StartsWith("- groupId:")) group = new CurtainGroupDefinition { groupId = Int(Value(t)) };
                else if (t.StartsWith("counterPlacementMinCell:")) group.counterMin = Cell(t);
                else if (t.StartsWith("counterPlacementMaxCell:")) group.counterMax = Cell(t);
                else if (t.StartsWith("requiredKind:")) group.kind = (PillKind)Int(Value(t));
                else if (t.StartsWith("requiredCount:"))
                {
                    group.count = Int(Value(t));
                    groups.Add(group);
                }
            }
            return groups;
        }

        /// <summary>"key: {x: 1, y: 2}" -> (1, 2).</summary>
        private static Vector2Int Cell(string line)
        {
            var parts = line.Substring(line.IndexOf('{') + 1).TrimEnd('}').Split(',');
            return new Vector2Int(Int(Value(parts[0])), Int(Value(parts[1])));
        }

        private static List<string> Block(string[] lines, string header)
        {
            var result = new List<string>();
            int start = System.Array.FindIndex(lines, l => l == header);
            if (start < 0) return result;
            for (int i = start + 1; i < lines.Length; i++)
            {
                var l = lines[i];
                bool topLevelKey = l.Length > 2 && l.StartsWith("  ") && l[2] != ' ' && l[2] != '-';
                if (topLevelKey) break;
                result.Add(l);
            }
            return result;
        }

        private static string TopLevel(string[] lines, string key)
        {
            var line = lines.FirstOrDefault(l => l.StartsWith("  " + key + ":"));
            return line != null ? Value(line) : "0";
        }

        private static T LoadRef<T>(string line) where T : Object
        {
            int g = line.IndexOf("guid: ");
            if (g < 0) return null;
            var guid = line.Substring(g + 6, 32);
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault();
        }

        private static Color ParseColor(string line)
        {
            var body = line.Substring(line.IndexOf('{') + 1).TrimEnd('}');
            var parts = body.Split(',').Select(p => p.Split(':')).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            float Component(string key) => Float(parts[key]);
            return new Color(Component("r"), Component("g"), Component("b"), Component("a"));
        }

        private static string Value(string line)
        {
            int colon = line.IndexOf(':');
            return colon < 0 ? string.Empty : line.Substring(colon + 1).Trim();
        }

        private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);
        private static float Float(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
#endif
