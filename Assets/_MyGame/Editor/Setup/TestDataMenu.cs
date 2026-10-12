using UnityEditor;
using UnityEngine;

namespace SnackDash.Editor.Setup
{
    /// <summary>
    /// Saved data for testing (menu SnackDash/Test). Everything the game remembers lives in PlayerPrefs:
    /// level progress, coins, lives, streak, booster stock, settings, and the one-time tutorials and gifts.
    /// </summary>
    public static class TestDataMenu
    {
        /// <summary>Like a fresh install: Level 1, 1800 coins, 5 lives, every tutorial and gift shown again.</summary>
        [MenuItem("SnackDash/Test/Reset All Data")]
        public static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("Reset All Data", "Xóa toàn bộ dữ liệu đã lưu (level, coin, tim, booster, hướng dẫn, cài đặt)?", "Reset", "Hủy")) return;
            SnackDash.Bootstrap.SaveData.ResetAll();
        }

        /// <summary>Only the one-time things (booster intro cards, forced first use, unlock gifts); level and coins stay.</summary>
        [MenuItem("SnackDash/Test/Reset Tutorials (keep level)")]
        public static void ResetTutorials() => SnackDash.Bootstrap.SaveData.ResetTutorials();

        /// <summary>
        /// Sets the saved level (Home and the next game start there). The Game scene's Test Level only plays a level once
        /// and leaves the saved progress alone [user: back on Home it was still Level 6].
        /// </summary>
        [MenuItem("SnackDash/Test/Set Level...")]
        public static void SetLevel() => EditorWindow.GetWindow<SetLevelWindow>(true, "Set Level", true);

        private class SetLevelWindow : EditorWindow
        {
            private int _level;

            private void OnEnable()
            {
                _level = PlayerPrefs.GetInt("SnackDash.LevelIndex", 0) + 1;
                minSize = maxSize = new Vector2(260f, 70f);
            }

            private void OnGUI()
            {
                _level = Mathf.Max(1, EditorGUILayout.IntField("Level", _level));
                if (GUILayout.Button("Set"))
                {
                    PlayerPrefs.SetInt("SnackDash.LevelIndex", _level - 1);
                    PlayerPrefs.Save();
                    Debug.Log($"[Test] Saved level is now {_level} (events open from it: weekly 15, season pass 25, lava 35).");
                    Close();
                }
            }
        }
    }
}
