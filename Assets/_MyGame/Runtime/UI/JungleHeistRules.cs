using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using static SnackDash.UI.JungleHeistUi;

namespace SnackDash.UI
{
    /// <summary>
    /// The rules of JUNGLE HEIST (original Resources/Popup_Lava_Quest_Ftue): the title between its leaves, then four steps:
    /// the crowd of players with yours in front ("Start with 100 players!") and its arrow, the fruit with a tick
    /// ("Win levels!") and its arrow, the grand prize ("Win your prize share!") and the warning at the foot (LocData jungle_heist_ftue_step_*),
    /// then "Tap to continue!". Shown after joining the first time and from the i button. Prefab Prefabs/UI/JungleHeistRules.
    /// </summary>
    public class JungleHeistRules : MonoBehaviour
    {
        private const string SeenKey = "jh_rules_seen";
        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        // Player_Avatars_Container of Step01: position, scale, picture (the player last, in front).
        internal static readonly (float x, float y, float scale, int icon)[] Crowd =
        {
            (93.5f, 105.95f, 1.3f, 3), (-73.5f, 117.95f, 1.2f, 2), (-160.5f, 44.95f, 1.4f, 1), (186.5f, 61.95f, 1.25f, 0), (-20.5f, 53.95f, 1.25f, 9),
            (85.5f, 24.95f, 1.25f, 8), (-126.5f, 5.95f, 1.25f, 7), (-294.5f, -42.05f, 1.25f, 6), (294.5f, -20.05f, 1.25f, 5), (-215.5f, -64.05f, 1.4f, 4),
            (210.8f, -49.65f, 1.4f, 3), (-146.9f, -102.95f, 1.5f, 2), (144.8f, -102.95f, 1.5f, 1),
        };

        private Image _cover;
        private float _coverAlpha;
        private RectTransform[] _arrows;
        private Vector2[] _arrowHome, _arrowWay;
        private TextMeshProUGUI _tap;
        private bool _ready, _closing;
        private System.Action _closed;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, System.Action closed = null)
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "Popup_Lava_Quest_Ftue";
            }
            else
            {
                root = Stretch(parent, "Popup_Lava_Quest_Ftue");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistRules>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistRules>();
            view._closed = closed;
            view.Bind(root);
        }

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistRules>() == null) root.gameObject.AddComponent<JungleHeistRules>();
            Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0.988f), raycast: true);

            var title = Rect(root, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 292.7f));
            Leaves(title, skin, -180f, 0.9f);
            var header = Centre(title, "Header_Holder", 1.75f, -67.5f, 800f, 136.7f);
            header.localScale = Vector3.one * 1.1f;
            Title(header, skin, new Vector2(0f, -89f), new Vector2(639f, 193.8f));

            var step1 = Step(root, "Step01", -144.4f, 259f, 599.2f, 405.8f);
            var frame = Centre(step1, "Frames_Icon", 22.6f, 64f, 605.9f, 456.9f);
            var crowd = Centre(frame, "Player_Avatars_Container", 1.5f * 0.8f, 22.05f * 0.8f, 714f, 355.9f);
            crowd.localScale = Vector3.one * 0.8f;
            for (int i = 0; i < Crowd.Length; i++)
                Avatar(crowd, skin, "View_LavaQuest_Avatar_" + (13 - i), new Vector2(Crowd[i].x, Crowd[i].y), Crowd[i].scale, Crowd[i].icon, false);
            Avatar(crowd, skin, "View_LavaQuest_Avatar_Player", new Vector2(-1.5f, -90.35f), 2.3f, 5, true);
            Line(step1, skin, "Start with <color=#F5BF46>100</color> players!", new Vector2(9.6f, -145f), new Vector2(580f, 115.8f), 43.6f);
            Arrow(root, skin, "Arrow01", step1.anchoredPosition + new Vector2(415f, -87.9f) * 0.9f, -24f);

            var step2 = Step(root, "Step02", 199.6f, -216f, 614.9f, 314.3f);
            var fruits = Centre(step2, "Fruits_Icon", 50f, 145.7f, 231.7f, 231.7f);
            Image(fruits, skin.Get("S_FruitsIcon_0"), Color.white).preserveAspect = true;
            Image(Centre(fruits, "CheckMarkIcon", 73f, -72f, 100f, 100f), skin.checkMark, Color.white).preserveAspect = true;
            Line(step2, skin, "Win <color=#F5BF46>levels!</color>", new Vector2(50.7f, -12.7f), new Vector2(513.5f, 85.1f), 43.6f);
            Arrow(root, skin, "Arrow02", step2.anchoredPosition + new Vector2(56f, -192.8f) * 0.9f, -110.2f);

            var step3 = Step(root, "Step03", -147f, -442f, 547.2f, 383.7f);
            var prize = Centre(step3, "Icons", 0f, 124.65f, 513.7f, 436.8f);
            Image(prize, skin.Get("S_GrandPrize_LavaQuest_0"), Color.white).preserveAspect = true;
            var prizeText = Text(Centre(prize, "Text (TMP)", 99.9f, -66.5f, 151.3f, 63.5f), "Grand Prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter"), 24.9f, Color.white);
            prizeText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5.8f);
            Line(step3, skin, "Win your <color=#F5BF46>prize</color> share!", new Vector2(16f, -142.8f), new Vector2(562.4f, 98.1f), 43.6f);

            var warning = Text(Rect(root, "Step04", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 203.65f), new Vector2(-81f, 191.7f)),
                "Level lost - challenge failed! Win 7 levels in a row to complete it!", skin.font, skin.plainMaterial, 39.4f, Color.white);
            warning.enableWordWrapping = true;
            Text(Rect(root, "TapToContinue", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 85.6f), new Vector2(703.9f, 171.2f)), "Tap to continue!", skin.font, skin.plainMaterial, 44.7f, new Color(0.553f, 0.569f, 0.588f));
            CentreOnLetters(root);
        }

        private static RectTransform Step(RectTransform root, string name, float x, float y, float w, float h)
        {
            var step = Centre(root, name, x, y, w, h);
            step.localScale = Vector3.one * 0.9f;
            return step;
        }

        private static void Line(RectTransform step, UiSkin skin, string text, Vector2 at, Vector2 size, float fontSize)
        {
            var line = Text(Centre(step, "Text", at.x, at.y, size.x, size.y), text, skin.font, skin.plainMaterial, fontSize, Color.white);
            line.richText = true;
            line.enableWordWrapping = true;
        }

        private static void Arrow(RectTransform root, UiSkin skin, string name, Vector2 at, float angle)
        {
            var arrow = Centre(root, name, at.x, at.y, 140f * 0.9f, 140f * 0.9f);
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image(arrow, skin.Get("image 1585_0"), Color.white).preserveAspect = true;
        }

        private void Bind(RectTransform root)
        {
            _cover = root.Find("Background").GetComponent<Image>();
            _coverAlpha = _cover.color.a;
            var button = _cover.GetComponent<Button>();
            if (button == null) button = _cover.gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnTap);
            _tap = root.Find("TapToContinue").GetComponent<TextMeshProUGUI>();
            _arrows = new[] { (RectTransform)root.Find("Arrow01"), (RectTransform)root.Find("Arrow02") };
            _arrowHome = new Vector2[_arrows.Length];
            _arrowWay = new Vector2[_arrows.Length];
            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrowHome[i] = _arrows[i].anchoredPosition;
                _arrowWay[i] = _arrows[i].localRotation * Vector3.right;
            }
            StartCoroutine(Reveal(root));
        }

        /// <summary>As the other events' rules (no clip in the original): darken, title, then each step pops in turn with its arrow, the warning and "Tap to continue!".</summary>
        private IEnumerator Reveal(RectTransform root)
        {
            var parts = new (Transform part, float at)[]
            {
                (root.Find("Title"), 0.1f), (root.Find("Step01"), 0.4f), (_arrows[0], 0.5f), (root.Find("Step02"), 0.8f), (_arrows[1], 0.9f), (root.Find("Step03"), 1.2f), (root.Find("Step04"), 1.5f),
            };
            var scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) { scales[i] = parts[i].part.localScale; parts[i].part.localScale = Vector3.zero; }
            float tapAlpha = _tap.alpha;
            _tap.alpha = 0f;
            for (float t = 0f; t < 1.9f && !_ready; t += Time.unscaledDeltaTime)
            {
                _cover.color = new Color(0f, 0f, 0f, _coverAlpha * Mathf.Clamp01(t / 0.2f));
                for (int i = 0; i < parts.Length; i++)
                    parts[i].part.localScale = scales[i] * Tween.OutBack(1.7f)(Mathf.Clamp01((t - parts[i].at) / 0.25f));
                _tap.alpha = Mathf.Clamp01((t - 1.6f) / 0.2f) * tapAlpha;
                yield return null;
            }
            _cover.color = new Color(0f, 0f, 0f, _coverAlpha);
            for (int i = 0; i < parts.Length; i++) parts[i].part.localScale = scales[i];
            _tap.alpha = tapAlpha;
            _ready = true;
        }

        private void Update()
        {
            if (_arrows == null || !_ready) return;
            float time = Time.unscaledTime;
            for (int i = 0; i < _arrows.Length; i++)
                _arrows[i].anchoredPosition = _arrowHome[i] + _arrowWay[i] * (8f * Mathf.Sin(time * 5f + i));
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
            _closed?.Invoke();
        }
    }
}
