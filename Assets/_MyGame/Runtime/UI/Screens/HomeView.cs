using System.Collections;
using SnackDash.Audio;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn Home (Main.unity gốc, video "home"), nằm trên gốc prefab Prefabs/UI/Screens/Home/Home đặt sẵn trong scene Home. Lần đầu mở app: ảnh
    /// khởi động (splash) và màn chờ tím; rồi Home: hình nền, thanh trên (<see cref="HomeTopBar"/>), 3 widget sự kiện
    /// (<see cref="EventWidget"/>), nút "Level N" với thanh streak (<see cref="PlayButton"/>), thanh dưới (<see cref="HomeBottomBar"/>).
    /// Mỗi phần tự lo việc của nó; ở đây chỉ lấy level hiện tại và nối các phần với nhau.
    /// </summary>
    public class HomeView : MonoBehaviour
    {
        [SerializeField] private LevelList levels;
        [SerializeField] private SoundLibrary sounds;
        [SerializeField] private HomeTopBar topBar;
        [Tooltip("Weekly, Jungle Heist, Snacky Pass.")]
        [SerializeField] private EventWidget[] widgets;
        [SerializeField] private PlayButton playButton;
        [SerializeField] private HomeBottomBar bottomBar;
        [Header("Mở app")]
        [Tooltip("Prefab Prefabs/UI/Screens/Home/AppStart: ảnh khởi động (unity_static_splash của APK), thứ tự vẽ 90.")]
        [SerializeField] private Canvas appStartPrefab;
        [SerializeField] private float splashSeconds = 1.5f;
        [SerializeField] private float startLoadingSeconds = 1.2f;

        private static bool _started; // splash và màn chờ chỉ một lần mỗi lần chạy app
        private Canvas _appStart;

        /// <summary>Màn chờ hoặc ảnh khởi động còn che Home.</summary>
        public bool Starting => LoadingScreen.Busy || _appStart != null;

        /// <summary>Như <see cref="Starting"/>, cộng lúc xu thắng còn đang bay về.</summary>
        public bool Busy => Starting || topBar.Busy;

        public HomeTopBar TopBar => topBar;
        public PlayButton PlayButton => playButton;

        private void Start()
        {
            int level = CurrentLevelNumber();
            topBar.CoinsClicked += () => bottomBar.ShowShop(true); // ô xu và dấu + mở Shop
            foreach (var widget in widgets) widget.Setup(this, level);
            playButton.Setup(level, levels != null && level <= levels.levels.Count ? levels.levels[level - 1].difficulty : 0);
            SoundPlayer.Use(sounds);
            if (level >= Bootstrap.LevelProgress.HomeFromLevel) SoundPlayer.Music("Music_Lobby"); // trước Level 6 app vào thẳng màn chơi

            // Splash và màn chờ chỉ khi app mở ra ở Home, không phải khi từ màn chơi về.
            if (!_started && !LoadingScreen.Busy) StartCoroutine(AppStart());
            _started = true;
            topBar.ShowWinCoins(playButton.transform, () => Starting);
        }

        private int CurrentLevelNumber()
        {
            if (levels == null) return 1;
            var level = Bootstrap.LevelProgress.Current(levels);
            return Mathf.Max(1, levels.levels.IndexOf(level) + 1);
        }

        private IEnumerator AppStart()
        {
            _appStart = Instantiate(appStartPrefab);
            yield return new WaitForSeconds(splashSeconds);
            if (CurrentLevelNumber() < Bootstrap.LevelProgress.HomeFromLevel)
            {
                // Chưa có Home trước Level 6 [user]: màn chờ dẫn thẳng vào màn chơi.
                LoadingScreen.Load(LoadingScreen.GameScene);
                yield break;
            }
            _appStart.enabled = false; // ảnh tắt, màn chờ lên thay
            yield return LoadingScreen.Show(startLoadingSeconds);
            Destroy(_appStart.gameObject);
            _appStart = null;
        }
    }
}
