using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using static SnackDash.UI.JungleHeistUi;

namespace SnackDash.UI
{
    /// <summary>
    /// Right after JOIN (original Resources/Popup_Lava_Quest_PurchaseOverlay, payload TotalParticipants / GrandPrizeAmount /
    /// ContinueRequested): on black, the title, the grand prize chest, the panel "Finding players on your level" with the
    /// players counter, the crowd of players (Player_Avatars_Container as in the rules, yours in front) popping in one by
    /// one, then "Tap to continue!", which goes on to the river. Prefab Prefabs/UI/JungleHeistMatch [user].
    /// The counter's run and the timings are STAND-INS (the feedbacks' curves are not readable).
    /// </summary>
    public class JungleHeistMatch : MonoBehaviour
    {
        public const float CountTime = 2.2f;

        private Image _cover;
        private float _coverAlpha;
        private TextMeshProUGUI _tap, _counter;
        private bool _ready, _closing;
        private Action _continue;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, Action continued)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "Popup_Lava_Quest_PurchaseOverlay";
            }
            else
            {
                root = Stretch(parent, "Popup_Lava_Quest_PurchaseOverlay");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistMatch>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistMatch>();
            view._continue = continued;
            view.Bind(root);
        }

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistMatch>() == null) root.gameObject.AddComponent<JungleHeistMatch>();
            var content = BlackRows(root, skin);

            // Reward: the chest (480 x 491.5; STAND-IN 0.65 to stay in its 311 high row, as on the rewards popup).
            var reward = Stretch(content, "Reward");
            GrandPrize(reward, skin, new Vector2(480f, 491.5f), new Vector2(101.5f, -65.1f), new Vector2(156.8f, 63.5f), 28.4f).localScale = Vector3.one * 0.65f;

            // Players_Counter: the panel (S_Lava_Quest_Bg, 700 x 285, 0.9) with its words and the counter under them.
            var counterRow = Stretch(content, "Players_Counter");
            var panel = Centre(counterRow, "Players", 0f, 0f, 700.2f, 285.4f);
            panel.localScale = Vector3.one * 0.9f;
            Image(panel, skin.Get("S_Lava_Quest_Bg_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 0.6f;
            var inner = Rect(panel, "Content", Vector2.zero, Vector2.one, new Vector2(0f, 12f), new Vector2(-56f, -80f));
            var words = Text(Rect(inner, "Description (TMP)", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -24f), new Vector2(-44f, 48f)), "Finding players on your level", skin.font, skin.plainMaterial, 32.4f, new Color(0.455f, 0.263f, 0.082f));
            words.enableAutoSizing = true;
            words.fontSizeMin = 20f;
            words.fontSizeMax = 32.4f;
            var box = Rect(inner, "Players_Counter", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 69f), new Vector2(340f, 118f));
            Image(box, skin.Get("S_Bg_Counter_Lava_Quest_0"), Color.white, sliced: true);
            Text(Stretch(box, "Counter (TMP)"), "100/100", skin.font, skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter"), 67.95f, Color.white);

            // Players_Frames: the crowd (View_Players_Frame_Animated 0.9, the rules' Player_Avatars_Container).
            var frames = Stretch(content, "Players_Frames");
            var animated = Centre(frames, "View_Players_Frame_Animated", 0f, 0f, 888f, 360f);
            animated.localScale = Vector3.one * 0.9f;
            var crowd = Centre(animated, "Player_Avatars_Container", 1.5f, 22.05f, 714f, 355.9f);
            var spots = JungleHeistRules.Crowd;
            for (int i = 0; i < spots.Length; i++)
                Avatar(crowd, skin, "View_LavaQuest_Avatar_" + (13 - i), new Vector2(spots[i].x, spots[i].y), spots[i].scale, spots[i].icon, false);
            Avatar(crowd, skin, "View_LavaQuest_Avatar_Player", new Vector2(-1.5f, -90.35f), 2.3f, 5, true);

            Text(Rect(root, "Tap_To_Claim", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(950f, 194.3f)), "Tap to continue!", skin.font, skin.plainMaterial, 44f, new Color(0.553f, 0.569f, 0.588f));
            CentreOnLetters(root);
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root)
        {
            _cover = root.GetComponent<Image>();
            _coverAlpha = _cover.color.a;
            var button = root.GetComponent<Button>();
            if (button == null) button = root.gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnTap);
            _tap = root.Find("Tap_To_Claim").GetComponent<TextMeshProUGUI>();
            _counter = root.Find("Content/Players_Counter/Players/Content/Players_Counter/Counter (TMP)").GetComponent<TextMeshProUGUI>();
            StartCoroutine(Reveal(root));
        }

        /// <summary>The title, chest and panel pop in; the counter runs up to 100 while the players pop in one by one (yours last); then "Tap to continue!".</summary>
        private IEnumerator Reveal(RectTransform root)
        {
            var crowd = root.Find("Content/Players_Frames/View_Players_Frame_Animated/Player_Avatars_Container");
            var parts = new System.Collections.Generic.List<(Transform part, float at)>
            {
                (root.Find("Content/Title/Header"), 0.1f), (root.Find("Content/Reward/Icons"), 0.3f), (root.Find("Content/Players_Counter/Players"), 0.5f),
            };
            const float crowdFrom = 0.7f;
            float step = CountTime / Mathf.Max(1, crowd.childCount);
            for (int i = 0; i < crowd.childCount; i++) parts.Add((crowd.GetChild(i), crowdFrom + step * i));
            float end = crowdFrom + CountTime + 0.2f;
            var scales = new Vector3[parts.Count];
            for (int i = 0; i < parts.Count; i++) { scales[i] = parts[i].part.localScale; parts[i].part.localScale = Vector3.zero; }
            float tapAlpha = _tap.alpha;
            _tap.alpha = 0f;
            int total = JungleHeist.Participants, shown = -1;
            for (float t = 0f; t < end + 0.2f && !_ready; t += Time.unscaledDeltaTime)
            {
                _cover.color = new Color(0f, 0f, 0f, _coverAlpha * Mathf.Clamp01(t / 0.2f));
                for (int i = 0; i < parts.Count; i++)
                    parts[i].part.localScale = scales[i] * Tween.OutBack(1.7f)(Mathf.Clamp01((t - parts[i].at) / 0.25f));
                int found = Mathf.Clamp(Mathf.RoundToInt(total * Tween.OutQuad(Mathf.Clamp01((t - crowdFrom) / CountTime))), 1, total);
                if (found != shown) { shown = found; _counter.text = $"{found}/{total}"; }
                _tap.alpha = Mathf.Clamp01((t - end) / 0.2f) * tapAlpha;
                yield return null;
            }
            _cover.color = new Color(0f, 0f, 0f, _coverAlpha);
            for (int i = 0; i < parts.Count; i++) parts[i].part.localScale = scales[i];
            _counter.text = $"{total}/{total}";
            _tap.alpha = tapAlpha;
            _ready = true;
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
            yield return Tween.Run(0.25f, Tween.Linear, t => group.alpha = 1f - t, true);
            Destroy(gameObject);
            _continue?.Invoke();
        }
    }
}
