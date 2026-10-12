using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Cài đặt chung của cả game và chuyển màn hình: FPS mục tiêu, giữ màn hình sáng, chỉ màn dọc; chuyển Home ↔ màn chơi
    /// (qua màn loading). Chỉnh FPS / màn dọc ngay trên Inspector của prefab Managers.
    /// </summary>
    public class GameManager : ManagerBase
    {
        public const string HomeScene = "Home", GameScene = "Game";

        [Tooltip("Số khung hình mỗi giây (bản mobile mặc định 30 làm rắn giật).")]
        [SerializeField, Min(30)] private int targetFrameRate = 60;
        [Tooltip("Màn hình không tự tắt khi đang chơi.")]
        [SerializeField] private bool keepScreenOn = true;
        [Tooltip("Chỉ chơi màn dọc.")]
        [SerializeField] private bool portraitOnly = true;
        [Tooltip("Prefabs/UI/Screens/Loading/LoadingScreen: màn chờ khi đổi scene.")]
        [SerializeField] private SnackDash.UI.LoadingScreen loadingPrefab;

        public SnackDash.UI.LoadingScreen LoadingPrefab => loadingPrefab;

        public override void Init()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenOn) Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (portraitOnly) Screen.orientation = ScreenOrientation.Portrait;
            Services.Register(this);
        }

        /// <summary>Về Home (qua màn loading).</summary>
        public void GoHome() => SnackDash.UI.LoadingScreen.Load(HomeScene);

        /// <summary>Vào màn chơi đang tới (qua màn loading).</summary>
        public void Play() => SnackDash.UI.LoadingScreen.Load(GameScene);
    }
}
