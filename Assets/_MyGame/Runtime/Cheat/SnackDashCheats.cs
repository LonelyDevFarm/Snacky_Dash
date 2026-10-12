using System;
using CheatSystem;
using SnackDash.Bootstrap;
using SnackDash.Economy;
using SnackDash.Input;
using SnackDash.LevelSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SnackDash.Cheat
{
    /// <summary>
    /// Snack Dash's commands for the shared cheat panel (Assets/CheatSystem), registered once at start:
    /// Game (win / lose / restart, go to a level), Items (coins, lives, boosters, streak), Events (weekly missions,
    /// Snacky Pass, Jungle Heist), Other (clock, saved data). The in-level commands only show in the Game scene.
    /// While the panel is open the swipes are off, so scrolling it does not move the snake.
    /// Only in the Editor and Development Builds (CheatManager.Enabled).
    /// </summary>
    public static class SnackDashCheats
    {
        private const string GameTab = "Game", ItemsTab = "Items", EventsTab = "Events", OtherTab = "Other";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (!CheatManager.Enabled) return;
            CheatManager.Opened += () => SetSwipes(false);
            CheatManager.Closed += () => SetSwipes(true);
            RegisterGame();
            RegisterItems();
            RegisterEvents();
            RegisterOther();
        }

        private static PlayerEconomy Eco => PlayerEconomy.Shared;
        private static GameBootstrap Bootstrap => Object.FindObjectOfType<GameBootstrap>();
        private static bool InLevel => Bootstrap != null && Bootstrap.Session != null;

        // ------------------------------------------------------------------ Game

        private static void RegisterGame()
        {
            CheatManager.Label(GameTab, () => InLevel
                ? $"Playing Level {Bootstrap.LevelNumber}  (saved: {LevelProgress.Index + 1})"
                : $"Saved level: {LevelProgress.Index + 1} / {LevelCount}");
            CheatManager.Button(GameTab, "WIN now", () =>
            {
                if (!Bootstrap.Session.CheatWin()) Debug.LogWarning("[Cheat] Cannot win now: already won or stuck.");
            }).When(() => InLevel);
            CheatManager.Button(GameTab, "LOSE now (NO WAY OUT)", () =>
            {
                if (!Bootstrap.Session.CheatBite()) Debug.LogWarning("[Cheat] Cannot lose now: the snake must stand still with a tail.");
            }).When(() => InLevel);
            CheatManager.Button(GameTab, "Restart level", () => Bootstrap.RestartLevel()).When(() => InLevel);
            CheatManager.Input(GameTab, "Go to level", "GO", text =>
            {
                if (int.TryParse(text, out int level)) GoToLevel(level);
            }, () => (CurrentLevel + 1).ToString()).Numeric();
            CheatManager.Buttons(GameTab, "Level", ("< Prev", () => GoToLevel(CurrentLevel - 1)), ("Next >", () => GoToLevel(CurrentLevel + 1)));
            CheatManager.Button(GameTab, "Reload scene", ReloadScene);
        }

        /// <summary>The level being played, or the saved one on Home.</summary>
        private static int CurrentLevel => InLevel ? Bootstrap.LevelNumber : LevelProgress.Index + 1;

        private static LevelList Levels
        {
            get
            {
                var lists = Resources.FindObjectsOfTypeAll<LevelList>();
                return lists.Length > 0 ? lists[0] : null;
            }
        }

        private static int LevelCount => Levels != null ? Levels.levels.Count : 0;

        /// <summary>Saves <paramref name="level"/> (1-based, kept inside the list) and loads the scene again to start there.</summary>
        private static void GoToLevel(int level)
        {
            int count = LevelCount;
            level = Mathf.Clamp(level, 1, count > 0 ? count : int.MaxValue);
            LevelProgress.SetIndex(level - 1);
            LevelProgress.TestLevelDone = true; // the Game scene's Test Level does not take over
            Eco.LevelInProgress = false; // leaving by cheat costs no life
            ReloadScene();
        }

        private static void ReloadScene()
        {
            if (InLevel) Eco.LevelInProgress = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------------ Items

        private static void RegisterItems()
        {
            CheatManager.Label(ItemsTab, () =>
                $"Coins {Eco.Coins}   Lives {Eco.Lives}/{Eco.LifeCap}" + (Eco.InfiniteLives ? $"   INF {Eco.InfiniteLeft:hh\\:mm\\:ss}" : "") +
                $"\nShovel {Eco.Boosters(PlayerEconomy.BoosterKeys[0])}  Boomerang {Eco.Boosters(PlayerEconomy.BoosterKeys[1])}  Undo {Eco.Boosters(PlayerEconomy.BoosterKeys[2])}  Magnet {Eco.Boosters(PlayerEconomy.BoosterKeys[3])}");
            CheatManager.Buttons(ItemsTab, "Coins",
                ("+1K", () => Eco.SetCoins(Eco.Coins + 1000)),
                ("+10K", () => Eco.SetCoins(Eco.Coins + 10000)),
                ("+100K", () => Eco.SetCoins(Eco.Coins + 100000)),
                ("0", () => Eco.SetCoins(0)));
            CheatManager.Input(ItemsTab, "Set coins", "SET", text =>
            {
                if (int.TryParse(text, out int coins)) Eco.SetCoins(coins);
            }, () => Eco.Coins.ToString()).Numeric();
            CheatManager.Buttons(ItemsTab, "Lives",
                ("Full", () => { while (Eco.Lives < Eco.LifeCap) Eco.AddLife(); }),
                ("-1", () => Eco.LoseLife()),
                ("0", () => { while (Eco.Lives > 0 && !Eco.InfiniteLives) Eco.LoseLife(); }),
                ("INF 1h", () => Eco.AddInfiniteLives(TimeSpan.FromHours(1))));
            CheatManager.Buttons(ItemsTab, "Boosters +10",
                ("Shovel", () => Eco.AddBoosters(PlayerEconomy.BoosterKeys[0], 10)),
                ("Boomer.", () => Eco.AddBoosters(PlayerEconomy.BoosterKeys[1], 10)),
                ("Undo", () => Eco.AddBoosters(PlayerEconomy.BoosterKeys[2], 10)),
                ("Magnet", () => Eco.AddBoosters(PlayerEconomy.BoosterKeys[3], 10)));
            CheatManager.Buttons(ItemsTab, "All boosters",
                ("99", () => SetAllBoosters(99)),
                ("0", () => SetAllBoosters(0)));
            CheatManager.Toggle(ItemsTab, "Win streak on fire (x2)", () => Eco.StreakOnFire, on => Eco.SetStreak(on));
        }

        private static void SetAllBoosters(int count)
        {
            foreach (var key in PlayerEconomy.BoosterKeys) Eco.SetBoosters(key, count, countUse: false);
        }

        // ------------------------------------------------------------------ Events

        private static void RegisterEvents()
        {
            CheatManager.Label(EventsTab, () => WeeklyMissions.Shared.Started
                ? $"Weekly: day {WeeklyMissions.Shared.Day}/{WeeklyMissions.WeekDays}, {WeeklyMissions.Shared.Points} points, ends in {Format(WeeklyMissions.Shared.Left)}"
                : $"Weekly: not started (opens at Level {WeeklyMissions.UnlockLevel})");
            CheatManager.Buttons(EventsTab, "Weekly",
                ("New week", () => { PlayerPrefs.DeleteKey("wm_start"); WeeklyMissions.Shared.StartIfUnlocked(WeeklyMissions.UnlockLevel); }),
                ("Finish today", () => WeeklyMissions.Shared.CheatFinishDay(WeeklyMissions.Shared.Day)));

            CheatManager.Label(EventsTab, () => SeasonPass.Shared.Running
                ? $"Snacky Pass: stage {SeasonPass.Shared.Stage}/{SeasonPass.Stages}, {SeasonPass.Shared.Tickets} tickets{(SeasonPass.Shared.Premium ? ", premium" : "")}, ends in {Format(SeasonPass.Shared.Left)}"
                : $"Snacky Pass: not running (opens at Level {SeasonPass.UnlockLevel})");
            CheatManager.Buttons(EventsTab, "Snacky Pass",
                ("New season", () => { PlayerPrefs.DeleteKey("sp_start"); SeasonPass.Shared.StartIfUnlocked(SeasonPass.UnlockLevel); }),
                ("+5 tickets", () => SeasonPass.Shared.Record(MissionGoal.CompleteAnyLevel, 5)),
                ("Premium", () => SeasonPass.Shared.Activate()));

            CheatManager.Label(EventsTab, () => JungleHeist.Shared.Started
                ? $"Jungle Heist: {JungleHeist.Shared.Current}, {JungleHeist.Shared.Step}/{JungleHeist.Steps} wins, ends in {Format(JungleHeist.Shared.Left)}"
                : $"Jungle Heist: not started (opens at Level {JungleHeist.UnlockLevel})");
            CheatManager.Buttons(EventsTab, "Jungle Heist",
                ("New cycle", () => { PlayerPrefs.DeleteKey("jh_cycle"); JungleHeist.Shared.StartIfUnlocked(JungleHeist.UnlockLevel); }),
                ("Join", () => JungleHeist.Shared.Join()),
                ("+1 win", () => JungleHeist.Shared.Record(MissionGoal.CompleteAnyLevel, 1)),
                ("Fail", () => JungleHeist.Shared.Record(MissionGoal.CompleteLevelInStreak, 0)));

            CheatManager.Label(EventsTab, () => "Home shows the change after Reload scene.");
            CheatManager.Button(EventsTab, "Reload scene", ReloadScene);
        }

        private static string Format(TimeSpan time) => time.TotalHours >= 24 ? $"{(int)time.TotalDays}d {time.Hours}h" : $"{(int)time.TotalHours}h {time.Minutes:00}m";

        // ------------------------------------------------------------------ Other

        private static void RegisterOther()
        {
            CheatManager.Label(OtherTab, () => GameClock.Offset == TimeSpan.Zero
                ? "Clock: real time"
                : $"Clock: +{Format(GameClock.Offset)} ({GameClock.UtcNow.ToLocalTime():dd/MM HH:mm})");
            CheatManager.Buttons(OtherTab, "Clock",
                ("+30m", () => GameClock.Offset += TimeSpan.FromMinutes(30)),
                ("+1h", () => GameClock.Offset += TimeSpan.FromHours(1)),
                ("+1 day", () => GameClock.Offset += TimeSpan.FromDays(1)),
                ("Real", () => GameClock.Offset = TimeSpan.Zero));
            CheatManager.Button(OtherTab, "Reset tutorials (keep level)", SnackDash.Core.SaveManager.ResetTutorials);
            CheatManager.Button(OtherTab, "Reset ALL data", () =>
            {
                SnackDash.Core.SaveManager.ResetAll();
                GameClock.Offset = TimeSpan.Zero;
                SceneManager.LoadScene(0);
            }).Confirmed();
        }

        // ------------------------------------------------------------------ input

        private static void SetSwipes(bool on)
        {
            foreach (var swipes in Object.FindObjectsOfType<SwipeInput>()) swipes.enabled = on;
        }
    }
}
