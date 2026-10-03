using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Bootstrap
{
    /// <summary>
    /// Which level the player is on, saved in PlayerPrefs. Skipped levels (LevelList.skipped) are
    /// passed over. After the last imported level play wraps to the first (the original goes on to
    /// more levels; only 13 are imported so far).
    /// </summary>
    public static class LevelProgress
    {
        private const string Key = "SnackDash.LevelIndex";

        public static int Index
        {
            get => PlayerPrefs.GetInt(Key, 0);
            private set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        /// <summary>Set once a level picked in the Inspector for testing has been won, so the next one follows.</summary>
        public static bool TestLevelDone { get; set; }

        public static LevelDefinition Current(LevelList list)
        {
            if (list == null || list.levels.Count == 0) return null;
            int index = list.Playable(Mathf.Clamp(Index, 0, list.levels.Count - 1));
            return index < 0 ? null : list.levels[index];
        }

        /// <summary>Moves on to the playable level after <paramref name="won"/>.</summary>
        public static void Advance(LevelList list, LevelDefinition won)
        {
            if (list == null || list.levels.Count == 0) return;
            int index = list.levels.IndexOf(won);
            int next = list.Playable(((index < 0 ? Index : index) + 1) % list.levels.Count);
            if (next >= 0) Index = next;
        }
    }
}
