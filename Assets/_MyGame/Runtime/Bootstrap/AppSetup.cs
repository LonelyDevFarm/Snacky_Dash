using UnityEngine;

namespace SnackDash.Bootstrap
{
    /// <summary>
    /// App-wide settings applied before the first scene: 60 fps (mobile builds default to 30, which makes
    /// the snake judder), the screen stays awake while playing, portrait only.
    /// </summary>
    public static class AppSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
#if !UNITY_EDITOR // stopping Play in the editor is not closing the app
            SnackDash.Economy.PlayerEconomy.Shared.ChargeUncleanExit(); // the app was closed during a level: that life is lost [user]
#endif
            // Weekly missions listen to the wallet from the first moment (wins, coins, boosters).
            var missions = SnackDash.Economy.WeeklyMissions.Shared;
            missions.StartIfUnlocked(LevelProgress.Index + 1);
            missions.Login();
            // Snacky Pass counts the tickets of every win too.
            SnackDash.Economy.SeasonPass.Shared.StartIfUnlocked(LevelProgress.Index + 1);
            // Jungle Heist steps on with each win and fails on a loss.
            SnackDash.Economy.JungleHeist.Shared.StartIfUnlocked(LevelProgress.Index + 1);
        }
    }
}
