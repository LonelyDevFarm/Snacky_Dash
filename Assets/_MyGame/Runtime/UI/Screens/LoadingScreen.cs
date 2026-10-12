using System.Collections;
using SnackDash.Core;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn chờ tím khi đổi scene Home / Game (prefab <c>Prefabs/UI/Screens/Loading/LoadingScreen</c>, canvas riêng thứ tự 100; gốc: Boot.unity
    /// LoadingScreenView: nền LoadingBg màu AE6CFC, cột ô S_Icon_LoadingScreen_Slot có đôi mắt ở giữa, "LOADING" và dấu chấm chạy).
    /// Nằm dưới [Managers] nên sống qua lúc nạp scene, mờ đi khi scene mới đã lên. Prefab do <see cref="GameManager"/> giữ.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string HomeScene = "Home";
        public const string GameScene = "Game";

        [SerializeField] private CanvasGroup group;
        [Tooltip("Dấu chấm: thêm một dấu mỗi 0.3 giây, tối đa 5.")]
        [SerializeField] private TMP_Text dots;
        [SerializeField] private float minSeconds = 0.6f, fade = 0.2f;

        private static LoadingScreen _current;

        /// <summary>Đang đổi scene (scene mới nạp phía sau màn chờ).</summary>
        public static bool Busy => _current != null;

        private static LoadingScreen Spawn()
        {
            var manager = Services.Get<GameManager>();
            return Instantiate(manager.LoadingPrefab, manager.transform.root); // dưới [Managers]: không mất khi đổi scene
        }

        /// <summary>Hiện màn chờ, nạp <paramref name="scene"/> rồi mờ đi khi xong.</summary>
        public static void Load(string scene)
        {
            if (_current != null) return;
            Time.timeScale = 1f; // màn đang dừng (mở cài đặt) không được làm đứng hiệu ứng mờ
            _current = Spawn();
            _current.StartCoroutine(_current.Run(scene));
        }

        /// <summary>Màn chờ riêng (lúc mở game, trước khi Home hiện), mờ đi sau <paramref name="seconds"/> giây.</summary>
        public static IEnumerator Show(float seconds)
        {
            var screen = Spawn();
            yield return new WaitForSeconds(seconds);
            yield return Tween.Run(screen.fade, Tween.Linear, t => screen.group.alpha = 1f - t);
            Destroy(screen.gameObject);
        }

        private IEnumerator Run(string scene)
        {
            group.alpha = 0f;
            // Thời gian thật (màn có thể đang dừng vì cài đặt).
            yield return Tween.Run(fade, Tween.Linear, t => group.alpha = t, true);
            float start = Time.unscaledTime;
            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone) yield return null;
            while (Time.unscaledTime - start < minSeconds) yield return null;
            yield return Tween.Run(fade, Tween.Linear, t => group.alpha = 1f - t, true);
            _current = null;
            Destroy(gameObject);
        }

        private void Update() => dots.text = new string('.', 1 + (int)(Time.unscaledTime / 0.3f) % 5);
    }
}
