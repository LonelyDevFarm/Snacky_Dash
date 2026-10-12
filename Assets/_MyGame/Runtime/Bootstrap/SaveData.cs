using UnityEngine;

namespace SnackDash.Bootstrap
{
    /// <summary>
    /// Clearing saved data for testing, shared by the menu SnackDash/Test and the cheat panel. Everything the game
    /// remembers lives in PlayerPrefs: level progress, coins, lives, streak, booster stock, settings, events, and the
    /// one-time tutorials and gifts.
    /// </summary>
    public static class SaveData
    {
        /// <summary>Like a fresh install: Level 1, 1800 coins, 5 lives, every tutorial and gift shown again.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Test] All saved data cleared: next start is a fresh install.");
        }

        /// <summary>Only the one-time things (booster intro cards, forced first use, unlock gifts); level and coins stay.</summary>
        public static void ResetTutorials()
        {
            foreach (var booster in new[] { "shovel", "boomerang", "undo", "magnet" })
            {
                PlayerPrefs.DeleteKey("booster_" + booster + "_tutorial");
                PlayerPrefs.DeleteKey("booster_" + booster + "_gift");
                PlayerPrefs.DeleteKey("booster_intro_" + booster.ToUpperInvariant());
            }
            PlayerPrefs.DeleteKey("home_streak_shown");
            PlayerPrefs.Save();
            Debug.Log("[Test] Tutorials and unlock gifts will show again; level, coins and lives kept.");
        }
    }
}
