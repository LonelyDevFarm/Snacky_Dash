using System.Collections.Generic;
using System.Linq;
using SnackDash.Editor.Restore;
using SnackDash.LevelSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Cửa sổ SnackDash/Level Tools/Level Generator: chọn cách sinh màn mới từ màn có sẵn (LevelGenerator), xem các màn đã
    /// sinh (nguồn gốc, số lượt người chơi thử cần), thêm / bỏ chúng khỏi danh sách màn (sau màn gốc cuối), chơi thử, xoá.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        private LevelGenerator.Options _options = new LevelGenerator.Options();
        private Vector2 _scroll;
        private List<LevelDefinition> _generated = new List<LevelDefinition>();

        [MenuItem("SnackDash/Level Tools/Level Generator")]
        public static void Open() => GetWindow<LevelGeneratorWindow>("Level Generator");

        private void OnEnable() => _generated = LevelGenerator.All();

        // Màn sinh / xoá ngoài cửa sổ (code, Project) thì danh sách cũ trỏ vào asset đã mất: nạp lại.
        private void OnProjectChange()
        {
            _generated = LevelGenerator.All();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sinh màn mới từ màn có sẵn", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Mỗi màn sinh ra phải qua kiểm bàn (không chỗ cụt) và người chơi thử SmartPlayer phải thắng được mà không dùng booster nào; chỉ màn đạt mới được lưu (Data/Levels/Generated). Màn gốc không bị sửa.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                _options.fromLevel = EditorGUILayout.IntField("Màn gốc từ", _options.fromLevel);
                _options.toLevel = EditorGUILayout.IntField("tới", _options.toLevel);
            }
            _options.count = Mathf.Max(1, EditorGUILayout.IntField("Số màn muốn tạo", _options.count));
            _options.seed = EditorGUILayout.IntField(new GUIContent("Hạt ngẫu nhiên", "Cùng hạt + cùng lựa chọn = cùng kết quả."), _options.seed);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cách tạo", EditorStyles.boldLabel);
            _options.method = (LevelGenerator.Method)EditorGUILayout.Popup(new GUIContent("Cách", "Xáo / lật: biến đổi một màn gốc. Cắt ghép: nửa trái một màn nối nửa phải màn khác ở chỗ đường khớp nhau, khay và trái tạo mới. Tự thiết kế: dựng bản đồ khối cỏ mới, có ý đồ chính (khối nâng / cổng / tường)."),
                (int)_options.method, new[] { "Xáo dữ liệu / lật bàn (một màn gốc)", "Cắt ghép hai màn", "Tự thiết kế bản đồ" });
            if (_options.method != LevelGenerator.Method.Remix)
                _options.mechanics = EditorGUILayout.Toggle(new GUIContent("Đặt cơ chế", "Tắt: chỉ dựng bàn đường để xem hình."), _options.mechanics);
            bool remix = _options.method == LevelGenerator.Method.Remix;
            using (new EditorGUI.DisabledScope(!remix))
            {
                _options.mirror = (LevelGenerator.Mirror)EditorGUILayout.Popup("Lật bàn trái ↔ phải", (int)_options.mirror, new[] { "Không", "Luôn", "Ngẫu nhiên" });
                _options.permuteKinds = EditorGUILayout.Toggle("Đổi màu trái", _options.permuteKinds);
                _options.shuffleCrates = EditorGUILayout.Toggle("Chia lại khay giữa các cột", _options.shuffleCrates);
                _options.regroupWaves = EditorGUILayout.Toggle(new GUIContent("Chia lại các đợt trái", "Chỉ màn rải trái ngẫu nhiên (bản đồ không có lớp spawn1)."), _options.regroupWaves);
            }
            using (new EditorGUI.DisabledScope(remix && !_options.regroupWaves))
                _options.mixWindow = EditorGUILayout.IntSlider(new GUIContent("Độ trộn đợt (khó dần)", "Nhỏ: trái ra gần đúng thứ tự khay cần. Lớn: lẫn lộn hơn."), _options.mixWindow, 1, 60);
            _options.difficulty = EditorGUILayout.IntPopup("Độ khó", _options.difficulty, new[] { "Giữ như màn gốc", "Thường", "Khó", "Siêu khó" }, new[] { -1, 0, 2, 4 });
            _options.games = EditorGUILayout.IntSlider("Số ván chơi thử", _options.games, 1, 5);

            if (GUILayout.Button("Sinh màn", GUILayout.Height(28)))
            {
                try
                {
                    LevelGenerator.Generate(_options, (text, progress) => EditorUtility.DisplayCancelableProgressBar("Level Generator", text, progress));
                }
                finally { EditorUtility.ClearProgressBar(); }
                _options.seed++; // lần bấm sau ra màn khác
                _generated = LevelGenerator.All();
            }

            EditorGUILayout.Space();
            var list = AssetDatabase.LoadAssetAtPath<LevelList>(LevelDataImporter.LevelListPath);
            EditorGUILayout.LabelField($"Màn đã sinh: {_generated.Count}", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Thêm tất cả vào danh sách màn")) { AddToList(list, _generated); }
                if (GUILayout.Button("Bỏ tất cả khỏi danh sách")) { RemoveFromList(list, _generated); }
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var level in _generated.ToList())
            {
                if (level == null) continue;
                int number = list != null ? list.levels.IndexOf(level) + 1 : 0;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(level, typeof(LevelDefinition), false);
                        GUILayout.Label(number > 0 ? $"Màn {number}" : "chưa vào danh sách", GUILayout.Width(120));
                        if (number == 0 && GUILayout.Button("Thêm", GUILayout.Width(50))) AddToList(list, new[] { level });
                        if (number > 0 && GUILayout.Button("Chơi thử", GUILayout.Width(70))) PlayTest(number);
                        if (GUILayout.Button("Xoá", GUILayout.Width(40)) && EditorUtility.DisplayDialog("Xoá màn", $"Xoá {level.name} và bản đồ của nó?", "Xoá", "Thôi"))
                        {
                            RemoveFromList(list, new[] { level });
                            var map = AssetDatabase.GetAssetPath(level.map);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(level));
                            if (!string.IsNullOrEmpty(map)) AssetDatabase.DeleteAsset(map);
                            _generated = LevelGenerator.All();
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.LabelField(level.origin, EditorStyles.wordWrappedMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void AddToList(LevelList list, IEnumerable<LevelDefinition> levels)
        {
            foreach (var level in levels) if (level != null && !list.levels.Contains(level)) list.levels.Add(level);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        private static void RemoveFromList(LevelList list, IEnumerable<LevelDefinition> levels)
        {
            foreach (var level in levels) list.levels.Remove(level);
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Vào thẳng màn <paramref name="number"/> trong scene Game (đổi màn đã lưu, như menu Test/Set Level).</summary>
        private static void PlayTest(int number)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerPrefs.SetInt("SnackDash.LevelIndex", number - 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene("Assets/_MyGame/Scenes/Game.unity");
            EditorApplication.isPlaying = true;
        }
    }
}
