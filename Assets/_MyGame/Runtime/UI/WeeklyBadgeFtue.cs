using System;
using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// "Weekly Adventure unlocked!" on Home the first time the player is back from Level 15 (original
    /// WeeklyMissionBadgeFtue, video sk1.3 0-10 s): Home dims (black 0.647), the widget stands out over it with
    /// a speech bubble beside it and the tutorial hand pointing at it; only the widget can be tapped, which opens
    /// the popup (and its rules, the first time). The layout is the prefab Prefabs/UI/WeeklyBadgeFtue [user];
    /// its Content is moved onto the widget, and a copy of the widget is shown inside it.
    /// </summary>
    public class WeeklyBadgeFtue : MonoBehaviour
    {
        private const string SeenKey = "wm_badge_ftue_seen";

        public static bool Seen => PlayerPrefs.GetInt(SeenKey, 0) != 0;

        private RectTransform _hand;
        private Vector2 _handRest, _handWay;
        private Vector3 _handScale;

        public static void Open(RectTransform canvas, UiSkin skin, RectTransform widget, GameObject prefab, Action open)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, canvas).transform;
                root.name = "WeeklyMissionBadgeFtue";
            }
            else
            {
                root = Stretch(canvas, "WeeklyMissionBadgeFtue");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<WeeklyBadgeFtue>();
            if (view == null) view = root.gameObject.AddComponent<WeeklyBadgeFtue>();
            view.Bind(root, widget, open);
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<WeeklyBadgeFtue>() == null) root.gameObject.AddComponent<WeeklyBadgeFtue>();
            Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0.647f), raycast: true);
            var content = Rect(root, "Content", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            Rect(content, "Widget_Unlocked", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(221.1f, 227.8f)); // the widget's copy goes here

            // Speech Bubble (303, 57): the white bubble with its point on the left, two lines.
            var bubble = Rect(content, "Speech Bubble", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(303f, 57f), new Vector2(400f, 120f));
            Image(Stretch(bubble, "background"), skin.Get("S_Background_SpeechBubble_01"), Color.white, sliced: true).pixelsPerUnitMultiplier = 2.35f;
            var text = Text(Rect(bubble, "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-40f, -20f)),
                "WEEKLY ADVENTURE\n<color=#9358D6>UNLOCKED!</color>", skin.font, skin.plainMaterial, 40f, new Color(0.369f, 0.498f, 0.678f));
            text.richText = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = 40f;

            // Tap (158, -121): the hand, 0.7, turned 30 degrees, its finger on the widget.
            var tap = Rect(content, "Tap", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(158f, -121f), Vector2.zero);
            var holder = Rect(tap, "Hand Holder", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            holder.localScale = Vector3.one * 0.7f;
            var hand = Rect(holder, "Hand", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256f, 256f));
            hand.localRotation = Quaternion.Euler(0f, 0f, 30f);
            Image(hand, skin.Get("S_Hand_Tutorial"), Color.white).preserveAspect = true;
            CentreOnLetters(root);
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root, RectTransform widget, Action open)
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            var content = (RectTransform)root.Find("Content");
            content.position = widget.position;

            // A copy of the widget over the dark cover; tapping it opens the event.
            var slot = (RectTransform)content.Find("Widget_Unlocked");
            var copy = (RectTransform)Instantiate(widget, slot);
            copy.name = "Widget";
            copy.anchorMin = copy.anchorMax = new Vector2(0.5f, 0.5f);
            copy.anchoredPosition = Vector2.zero;
            copy.localScale = widget.lossyScale.x / slot.lossyScale.x * Vector3.one;
            foreach (var button in copy.GetComponentsInChildren<Button>(true)) Destroy(button);
            var hit = Image(Stretch(copy, "Tap Area"), null, Color.clear, raycast: true);
            hit.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Destroy(root.gameObject);
                open();
            });
            Pressable(copy);

            _hand = (RectTransform)content.Find("Tap/Hand Holder");
            _handRest = _hand.anchoredPosition;
            _handScale = _hand.localScale;
            _handWay = (content.Find("Tap") as RectTransform).anchoredPosition.normalized; // from the widget out to the hand
            StartCoroutine(Show(root));
        }

        /// <summary>The cover fades in, the bubble and the hand pop.</summary>
        private IEnumerator Show(RectTransform root)
        {
            var cover = root.Find("Background").GetComponent<Image>();
            float alpha = cover.color.a;
            var bubble = root.Find("Content/Speech Bubble");
            var tap = root.Find("Content/Tap");
            Vector3 bubbleScale = bubble.localScale, tapScale = tap.localScale;
            Action<float> step = t =>
            {
                cover.color = new Color(0f, 0f, 0f, alpha * Mathf.Clamp01(t * 2f));
                bubble.localScale = bubbleScale * Tween.OutBack(1.7f)(Mathf.Clamp01((t - 0.3f) / 0.7f));
                tap.localScale = tapScale * Tween.OutBack(1.7f)(Mathf.Clamp01((t - 0.5f) / 0.5f));
            };
            step(0f);
            yield return Tween.Run(0.5f, Tween.Linear, step, true);
        }

        /// <summary>The hand taps: it goes in toward the widget and back, about once a second (sk1.3).</summary>
        private void Update()
        {
            if (_hand == null) return;
            float push = Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 0.9f));
            _hand.anchoredPosition = _handRest - _handWay * 18f * push;
            _hand.localScale = _handScale * (1f - 0.06f * push);
        }
    }
}
