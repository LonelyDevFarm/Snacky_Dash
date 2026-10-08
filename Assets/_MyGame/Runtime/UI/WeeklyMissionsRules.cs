using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The rules of WEEKLY ADVENTURE (original WeeklyMissionsPopupFtue): the title with its "Rules" ribbon and three
    /// steps with their pictures and LocData lines: missions earn tokens, tokens win adventure chests, collect the
    /// grand prize. Shown the first time the popup opens and from its i button. The layout is the prefab
    /// Prefabs/UI/WeeklyMissionsRules [user]. The original plays it from code (no clip), so the timing is read
    /// from video sk1.1 (5.5-7.0 s): the screen goes near black and the title widens in, then the snake with its
    /// hat pops in with its arrow, the bar to the chest with the second arrow and "Tap to continue!", and last
    /// the grand prize in its glow; the arrows then nudge the way they point and the glow breathes.
    /// </summary>
    public class WeeklyMissionsRules : MonoBehaviour
    {
        private const string SeenKey = "wm_rules_seen";

        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        private Image _cover;
        private float _coverAlpha;
        private RectTransform _header, _glow;
        private RectTransform[] _arrows;
        private Vector2[] _arrowHome, _arrowWay;
        private TextMeshProUGUI _tap;
        private bool _ready, _closing;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab = null)
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "WeeklyMissionsPopupFtue";
            }
            else
            {
                root = Stretch(parent, "WeeklyMissionsPopupFtue");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<WeeklyMissionsRules>();
            if (view == null) view = root.gameObject.AddComponent<WeeklyMissionsRules>();
            view.Bind(root);
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<WeeklyMissionsRules>() == null) root.gameObject.AddComponent<WeeklyMissionsRules>();
            Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0.97f), raycast: true); // near black as on the phone (linear blending) [user]

            var title = Rect(root, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 292.7f));
            var header = Rect(title, "Header", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(881.13f, 144.3f));
            header.localScale = Vector3.one * 0.933f;
            WeeklyMissionsPopup.Title(header, skin);
            WeeklyMissionsPopup.Ribbon(header, skin, "Rules");

            var step1 = Rect(root, "Step01", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-182f, 369.5f), new Vector2(456.9f, 405.8f));
            Image(Rect(step1, "Shooter RIght", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46.5f, 0f), new Vector2(210.2f, 208.7f)), skin.Get("S_Snake_Head_Illustration_0"), Color.white).preserveAspect = true;
            Line(step1, skin, "Complete missions to earn <color=#FED025>tokens!</color>", new Vector2(58.2f, -184.6f), new Vector2(548.5f, 134f));
            Arrow(root, skin, "Arrow01", step1.anchoredPosition + new Vector2(363f, -58f), -24f);

            var step2 = Rect(root, "Step02", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(106.6f, -12f), new Vector2(614.9f, 314.3f));
            Image(Rect(step2, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(5.1f, 7.8f), new Vector2(791.1f, 290.5f)), skin.Get("S_Progress_Illustration_0"), Color.white).preserveAspect = true;
            Line(step2, skin, "Progress to win <color=#FED025>adventure chests!</color>", new Vector2(-29.8f, -115.1f), new Vector2(674.5f, 132.4f));
            Arrow(root, skin, "Arrow02", step2.anchoredPosition + new Vector2(59f, -314f), -110.2f);

            var step3 = Rect(root, "Step03", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-182f, -396.2f), new Vector2(456.9f, 383.7f));
            Image(Rect(step3, "Light01", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2f, 0f), new Vector2(385.8f, 383.8f)), skin.Get("Glow_Circle01"), new Color(1f, 0.728f, 0f));
            Image(Rect(step3, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24.7f, 11.3f), new Vector2(386.7f, 361.2f)), skin.Get("S_Chest_Illustration_0"), Color.white).preserveAspect = true;
            Line(step3, skin, "Collect amazing <color=#FED025>grand prize!</color>", new Vector2(39.5f, -243f), new Vector2(524.2f, 120.5f));

            Text(Rect(root, "TapToContinue", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 137f), new Vector2(703.9f, 171.2f)), "Tap to continue!", skin.font, skin.plainMaterial, 44.7f, new Color(0.553f, 0.569f, 0.588f, 0.753f));
            CentreOnLetters(root);
        }

        private static void Line(RectTransform step, UiSkin skin, string text, Vector2 position, Vector2 size)
        {
            var line = Text(Rect(step, "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size), text, skin.font, skin.plainMaterial, 38.7f, Color.white);
            line.richText = true;
            line.enableWordWrapping = true;
        }

        /// <summary>The arrow from one step to the next (on the root, so it pops on its own after its step).</summary>
        private static void Arrow(RectTransform root, UiSkin skin, string name, Vector2 position, float angle)
        {
            var arrow = Rect(root, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(140f, 140f));
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image(arrow, skin.Get("image 1585_0"), Color.white).preserveAspect = true;
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root)
        {
            _cover = root.Find("Background").GetComponent<Image>();
            _coverAlpha = _cover.color.a;
            var button = _cover.GetComponent<Button>();
            if (button == null) button = _cover.gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnTap);
            _header = (RectTransform)root.Find("Title/Header");
            _glow = (RectTransform)root.Find("Step03/Light01");
            _tap = root.Find("TapToContinue").GetComponent<TextMeshProUGUI>();
            _arrows = new[] { (RectTransform)root.Find("Arrow01"), (RectTransform)root.Find("Arrow02") };
            _arrowHome = new Vector2[_arrows.Length];
            _arrowWay = new Vector2[_arrows.Length];
            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrowHome[i] = _arrows[i].anchoredPosition;
                _arrowWay[i] = _arrows[i].localRotation * Vector3.right; // the sprite points right before its rotation
            }
            StartCoroutine(Reveal(root));
        }

        /// <summary>sk1.1: darken 0.2 s; title widens 0.1-0.25 s; snake 0.4 s, its arrow 0.5 s; bar 0.8 s, arrow and Tap 1.0 s; chest 1.2 s.</summary>
        private IEnumerator Reveal(RectTransform root)
        {
            var parts = new (Transform part, float at)[]
            {
                (root.Find("Step01"), 0.4f), (_arrows[0], 0.5f), (root.Find("Step02"), 0.8f), (_arrows[1], 1.0f), (root.Find("Step03"), 1.2f),
            };
            var scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) { scales[i] = parts[i].part.localScale; parts[i].part.localScale = Vector3.zero; }
            var headerScale = _header.localScale;
            float tapAlpha = _tap.alpha;
            _tap.alpha = 0f;
            for (float t = 0f; t < 1.5f && !_ready; t += Time.unscaledDeltaTime)
            {
                _cover.color = new Color(0f, 0f, 0f, _coverAlpha * Mathf.Clamp01(t / 0.2f));
                float h = Mathf.Clamp01((t - 0.1f) / 0.15f);
                _header.localScale = new Vector3(headerScale.x * Tween.OutBack(1.7f)(h), headerScale.y * Mathf.Min(1f, t / 0.1f), 1f);
                for (int i = 0; i < parts.Length; i++)
                    parts[i].part.localScale = scales[i] * Tween.OutBack(1.7f)(Mathf.Clamp01((t - parts[i].at) / 0.25f));
                _tap.alpha = Mathf.Clamp01((t - 1.0f) / 0.2f) * tapAlpha;
                yield return null;
            }
            // Everything in place (also when a tap came early).
            _cover.color = new Color(0f, 0f, 0f, _coverAlpha);
            _header.localScale = headerScale;
            for (int i = 0; i < parts.Length; i++) parts[i].part.localScale = scales[i];
            _tap.alpha = tapAlpha;
            _ready = true;
        }

        private void Update()
        {
            if (_arrows == null) return;
            float time = Time.unscaledTime;
            if (_ready)
                for (int i = 0; i < _arrows.Length; i++)
                    _arrows[i].anchoredPosition = _arrowHome[i] + _arrowWay[i] * (8f * Mathf.Sin(time * 5f + i));
            _glow.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(time * 3f));
        }

        private void OnTap()
        {
            if (_closing) return;
            if (!_ready) { _ready = true; return; }
            _closing = true;
            StartCoroutine(Close());
        }

        private IEnumerator Close()
        {
            var group = gameObject.AddComponent<CanvasGroup>();
            yield return Tween.Run(0.2f, Tween.Linear, t => group.alpha = 1f - t, true);
            Destroy(gameObject);
        }
    }
}
