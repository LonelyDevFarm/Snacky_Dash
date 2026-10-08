using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Scene switches between Home and Game behind the original purple loading screen (Boot.unity
    /// LoadingScreenView: LoadingBg tinted AE6CFC, a column of S_Icon_LoadingScreen_Slot with the eyes in it,
    /// "Loading" and running dots). The overlay lives across the scene load and fades once the new scene
    /// is up.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string HomeScene = "Home";
        public const string GameScene = "Game";

        private const float MinSeconds = 0.6f, Fade = 0.2f;

        private static LoadingScreen _current;

        /// <summary>The skin the last scene's views handed over (for callers without one, e.g. the bootstrap).</summary>
        public static UiSkin Skin { get; set; }

        /// <summary>A scene switch is under way (the new scene came in behind this loading screen).</summary>
        public static bool Busy => _current != null;

        public static void Load(string scene) => Load(scene, Skin);

        /// <summary>Shows the loading screen, loads <paramref name="scene"/> and fades out when it is ready.</summary>
        public static void Load(string scene, UiSkin skin)
        {
            if (_current != null) return;
            Time.timeScale = 1f; // a paused level (settings open) must not freeze the fades
            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);
            _current = go.AddComponent<LoadingScreen>();
            _current.StartCoroutine(_current.Run(scene, skin));
        }

        /// <summary>The loading screen on its own (game start, before Home shows), gone after <paramref name="seconds"/>.</summary>
        public static IEnumerator Show(RectTransform canvas, UiSkin skin, float seconds)
        {
            var root = Build(canvas, skin);
            yield return new WaitForSeconds(seconds);
            var group = root.GetComponent<CanvasGroup>();
            yield return Tween.Run(Fade, Tween.Linear, t => group.alpha = 1f - t);
            Destroy(root.gameObject);
        }

        private IEnumerator Run(string scene, UiSkin skin)
        {
            var canvas = Canvas(transform, "Loading", 100);
            var root = Build(canvas, skin);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            // Fades run on real time (the level may have been paused by the settings).
            yield return Tween.Run(Fade, Tween.Linear, t => group.alpha = t, true);
            float start = Time.unscaledTime;
            var load = SceneManager.LoadSceneAsync(scene);
            while (!load.isDone) yield return null;
            while (Time.unscaledTime - start < MinSeconds) yield return null;
            yield return Tween.Run(Fade, Tween.Linear, t => group.alpha = 1f - t, true);
            _current = null;
            Destroy(gameObject);
        }

        private static RectTransform Build(RectTransform canvas, UiSkin skin)
        {
            var root = Stretch(canvas, "LoadingScreenView");
            root.gameObject.AddComponent<CanvasGroup>();
            Image(root, skin.Get("LoadingBg"), Hex("AE6CFCFF"), raycast: true);

            // A column of slots with the eyes in it, as in the video (eyes a little above the middle).
            var column = Rect(root, "CenterContainer", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), Vector2.zero);
            const float Step = 330f;
            for (int i = -4; i <= 4; i++)
            {
                var slot = Rect(column, i == 0 ? "Eyes" : "Slot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -i * Step), new Vector2(556.4f, i == 0 ? 321.2f : 311.2f));
                if (i != 0) slot.localScale = Vector3.one * 0.95f;
                Image(slot, skin.Get(i == 0 ? "S_Icon_LoadingScreen_Eyes" : "S_Icon_LoadingScreen_Slot"), Color.white).preserveAspect = true;
            }

            var label = Text(Rect(root, "Text", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 375f), new Vector2(0f, 138f)), "LOADING", skin.font, skin.plainMaterial, 72f, Hex("F3E8FFFF"));
            var dots = Text(Rect(root, "Dots", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 325f), new Vector2(0f, 99f)), "", skin.font, skin.plainMaterial, 36f, Hex("F3E8FFFF"));
            root.gameObject.AddComponent<LoadingDots>().label = dots;
            label.fontMaterial.EnableKeyword("OUTLINE_ON");
            label.outlineWidth = 0.15f;
            label.outlineColor = Hex("7A3FC4FF");
            return root;
        }

        /// <summary>"Dots": one more dot every 0.3 s, up to five.</summary>
        private class LoadingDots : MonoBehaviour
        {
            public TMP_Text label;
            private void Update() { if (label != null) label.text = new string('.', 1 + (int)(Time.unscaledTime / 0.3f) % 5); }
        }
    }
}
