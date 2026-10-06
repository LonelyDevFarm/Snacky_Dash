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
        }
    }
}
