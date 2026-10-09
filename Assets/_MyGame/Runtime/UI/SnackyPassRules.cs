using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The rules of SNACKY PASS (original seasonpass2/SeasonPassPopupFtue, video sk2 13.5-17 s): the Snacky Pass ticket in
    /// its orange glow, then four steps one after another: the fruit with its tick ("Complete levels!") and its arrow, the
    /// ticket bar with "8/10" ("Collect tickets!") and its arrow, the chest ("Win amazing rewards!"), the golden ticket in
    /// its glow ("Activate Snacky Pass to unlock exclusive rewards!") and "Tap to continue!". Shown the first time the
    /// pass opens and from its i button. The layout is the prefab Prefabs/UI/SnackyPassRules [user].
    /// </summary>
    public class SnackyPassRules : MonoBehaviour
    {
        private const string SeenKey = "sp_rules_seen";

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
                root.name = "SeasonPassPopupFtue";
            }
            else
            {
                root = Stretch(parent, "SeasonPassPopupFtue");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<SnackyPassRules>();
            if (view == null) view = root.gameObject.AddComponent<SnackyPassRules>();
            view.Bind(root);
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<SnackyPassRules>() == null) root.gameObject.AddComponent<SnackyPassRules>();
            Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0.988f), raycast: true);

            var title = Rect(root, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 292.7f));
            var header = Rect(title, "Header", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -67.5f), new Vector2(807f, 136.6f));
            header.localScale = Vector3.one * 0.933f;
            Header(header, skin);

            var step1 = Step(root, "Step01", -182f, 369.5f, 456.9f, 405.8f);
            var fruits = Rect(step1, "Fruits_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 59f), new Vector2(231.7f, 231.7f));
            Image(fruits, skin.Get("S_FruitsIcon_0"), Color.white).preserveAspect = true;
            Image(Rect(fruits, "CheckMarkIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(73f, -72f), new Vector2(100f, 100f)), skin.checkMark, Color.white).preserveAspect = true;
            Line(step1, skin, "Complete <color=#FFBC16>levels!</color>", new Vector2(-23f, -124f), new Vector2(548.5f, 134f));
            Arrow(root, skin, "Arrow01", step1.anchoredPosition + new Vector2(284f, 6f), -24f);

            var step2 = Step(root, "Step02", 106.6f, -12f, 614.9f, 314.3f);
            var bar = Rect(step2, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(50.7f, 145.7f), new Vector2(85.1f, 216.1f));
            Image(bar, skin.Get("S_IconProgressBar_0"), Color.white);
            var bubble = Rect(bar, "SpeechBubble", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(116.6f, 5.2f), new Vector2(206.4f, 91.5f));
            Image(bubble, skin.Get("S_Icon_SpeechBubble_Purple_0"), Color.white);
            foreach (var (name, y, colour) in new[] { ("Small_Ticket_Shadow", 9.7f, new Color(0f, 0f, 0f, 0.525f)), ("Small_Ticket", 12.7f, Color.white) })
            {
                var ticket = Rect(bubble, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56.2f, y), new Vector2(69.9f, 43.7f));
                ticket.localRotation = Quaternion.Euler(0f, 0f, 15.4f);
                Image(ticket, skin.Get("S_Icon_GoldenTicket_0"), colour).preserveAspect = true;
            }
            Text(Rect(step2, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(202.6f, 156.3f), new Vector2(98f, 40.6f)), "8/10", skin.font, skin.EventMaterial("LuckiestGuy-Regular SDF 2 - purple cartoon 2"), 42f, Color.white);
            Line(step2, skin, "Collect <color=#FFBC16>tickets!</color>", new Vector2(50.7f, -43.7f), new Vector2(513.5f, 162.7f));
            Arrow(root, skin, "Arrow02", step2.anchoredPosition + new Vector2(16f, -195f), -110.2f);

            var step3 = Step(root, "Step03", -182f, -396.2f, 456.9f, 383.7f);
            Image(Rect(step3, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24.7f, 165.3f), new Vector2(386.7f, 361.2f)), skin.Get("S_Chest_Illustration_0"), Color.white).preserveAspect = true;
            Line(step3, skin, "Win <color=#FFBC16>amazing rewards!</color>", new Vector2(16f, -85f), new Vector2(472.1f, 120.5f));

            var step4 = Step(root, "Step04", -6.6f, -661.6f, 900.6f, 225.2f);
            Image(Rect(step4, "Light01", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-265.2f, 3f), new Vector2(225f, 201f)), skin.Get("Glow_Circle01"), new Color(1f, 0.728f, 0f));
            var shadow = Rect(step4, "Icons_Shdw", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-265.2f, -7f), new Vector2(201.7f, 188.4f));
            shadow.localRotation = Quaternion.Euler(0f, 0f, 15.95f);
            Image(shadow, skin.Get("S_Icon_GoldenTicket_0"), new Color(0f, 0f, 0f, 0.443f)).preserveAspect = true;
            Image(Rect(shadow, "Icons", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4.2f, 14.7f), new Vector2(201.7f, 188.4f)), skin.Get("S_Icon_GoldenTicket_0"), Color.white).preserveAspect = true;
            Line(step4, skin, "Activate <color=#FFBC16>Snacky Pass</color> to unlock exclusive <color=#FFBC16>rewards!</color>", new Vector2(141.8f, 19.9f), new Vector2(530.6f, 185.4f));

            Text(Rect(root, "TapToContinue", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 137f), new Vector2(703.9f, 171.2f)), "Tap to continue!", skin.font, skin.plainMaterial, 44.7f, new Color(0.553f, 0.569f, 0.588f));
            CentreOnLetters(root);
        }

        /// <summary>The Snacky Pass ticket title in its orange glow (Header_SeasonPass; also on the chest screen).</summary>
        public static void Header(RectTransform header, UiSkin skin)
        {
            var light = Rect(header, "Light01 (1)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-15f, -28f), new Vector2(472.3f, 264.2f));
            light.localScale = Vector3.one * 1.071f;
            Image(light, skin.Get("Glow_Circle01"), new Color(1f, 0.505f, 0f));
            var ticket = Rect(header, "Header_SeasonPass", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(407.9f, 211.2f));
            ticket.localScale = Vector3.one * 1.071f;
            Image(ticket, skin.Get("S_Img_Ticket_0"), Color.white);
            var words = Rect(ticket, "MainTitle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3f, -20f), new Vector2(400f, 161.4f));
            words.localScale = Vector3.one * 0.88f;
            SnackyPassView.TitleWords(words, skin, 0f, 6f, 10.1f);
        }

        private static RectTransform Step(RectTransform root, string name, float x, float y, float w, float h)
            => Rect(root, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));

        private static void Line(RectTransform step, UiSkin skin, string text, Vector2 position, Vector2 size)
        {
            var line = Text(Rect(step, "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size), text, skin.font, skin.plainMaterial, 38.7f, Color.white);
            line.richText = true;
            line.enableWordWrapping = true;
        }

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
            _glow = (RectTransform)root.Find("Step04/Light01");
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

        /// <summary>sk2: darken; the title widens in; the fruit and its arrow, the bar and its arrow, the chest, the ticket with "Tap to continue!".</summary>
        private IEnumerator Reveal(RectTransform root)
        {
            var parts = new (Transform part, float at)[]
            {
                (root.Find("Step01"), 0.4f), (_arrows[0], 0.5f), (root.Find("Step02"), 0.8f), (_arrows[1], 0.9f), (root.Find("Step03"), 1.2f), (root.Find("Step04"), 1.6f),
            };
            var scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) { scales[i] = parts[i].part.localScale; parts[i].part.localScale = Vector3.zero; }
            var headerScale = _header.localScale;
            float tapAlpha = _tap.alpha;
            _tap.alpha = 0f;
            for (float t = 0f; t < 1.9f && !_ready; t += Time.unscaledDeltaTime)
            {
                _cover.color = new Color(0f, 0f, 0f, _coverAlpha * Mathf.Clamp01(t / 0.2f));
                float h = Mathf.Clamp01((t - 0.1f) / 0.15f);
                _header.localScale = new Vector3(headerScale.x * Tween.OutBack(1.7f)(h), headerScale.y * Mathf.Min(1f, t / 0.1f), 1f);
                for (int i = 0; i < parts.Length; i++)
                    parts[i].part.localScale = scales[i] * Tween.OutBack(1.7f)(Mathf.Clamp01((t - parts[i].at) / 0.25f));
                _tap.alpha = Mathf.Clamp01((t - 1.6f) / 0.2f) * tapAlpha;
                yield return null;
            }
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
            _glow.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 3f));
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
