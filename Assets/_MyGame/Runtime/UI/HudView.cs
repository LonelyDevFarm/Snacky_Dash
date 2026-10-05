using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// In-game top bar, built in code from Panel_InGame_HUD in the original Game.unity (_top /
    /// Panel_InGame_HUD / ProgressBar with its level plaque, slider, snake head and fruit icons).
    /// Like the original's _safe_area_fitter it sits inside Screen.safeArea, which is why it shows
    /// lower on notched phones in the footage.
    /// The bar fills with the fruit eaten; a marker sits at the end of every wave but the last and
    /// turns into a check once that wave is eaten [user; LevelCompletionBarUI wavePendingPrefab /
    /// waveCompletePrefab, delayMilestoneCompleteSpawnMs 150].
    /// Not built yet: combo coin counter (coin economy), the check's sparkle particles.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] private UiSkin skin = new UiSkin();
        [Tooltip("Layout prefab (Prefabs/UI/HUD); built in code when empty. Milestone markers are added at runtime.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Fill speed of the progress bar, in bar lengths per second (smoothing; not measured).")]
        [SerializeField] private float fillSpeed = 1.5f;
        [SerializeField] private float markerCompleteDelay = 0.15f;

        private readonly List<(float fraction, int fruit, GameObject pending)> _markers = new List<(float, int, GameObject)>();
        private GameSession _session;
        private RectTransform _fill, _completedHolder;
        private float _shown;
        private float _total;

        public void Bind(GameSession session, int levelNumber)
        {
            _session = session;
            _total = Mathf.Max(1, session.TotalFruit);

            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, transform).transform;
                root.name = "HUD";
            }
            else
            {
                root = UiBuilder.Canvas(transform, "HUD", 5);
                BuildHud(root);
            }

            var safe = (RectTransform)root.Find("SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            const string bar = "SafeArea/_top/Panel_InGame_HUD/ProgressBar/";
            root.Find(bar + "LvlHolder_Img/LvlNumber_Txt").GetComponent<TMPro.TMP_Text>().text = "LEVEL " + levelNumber;
            _fill = (RectTransform)root.Find(bar + "Slider/FillArea/Fill");
            _completedHolder = (RectTransform)root.Find(bar + "Slider/CompletedMilestoneHolder");
            var pendingHolder = (RectTransform)root.Find(bar + "Slider/PendingMilestoneHolder");
            for (int wave = 0; wave < _session.WaveCount - 1; wave++)
            {
                int fruit = _session.FruitInWaves(wave);
                float fraction = fruit / _total;
                _markers.Add((fraction, fruit, PendingMarker(pendingHolder, fraction)));
            }
        }

        /// <summary>Layout of the top bar (saved as Prefabs/UI/HUD by SnackDash/Setup/Create UI Prefabs).</summary>
        public void BuildHud(RectTransform root)
        {
            var safe = Stretch(root, "SafeArea");
            var top = Rect(safe, "_top", new Vector2(0, 1), Vector2.one, new Vector2(0, -10), new Vector2(0, 128), new Vector2(0.5f, 1));
            var panel = Rect(top, "Panel_InGame_HUD", new Vector2(0, 1), Vector2.one, new Vector2(0, -5), new Vector2(0, 120), new Vector2(0.5f, 1));
            // Shorter bar with the settings button on its right, as on Level 27 of the original
            // (Man_Lose video); the older layout ran the bar across and kept settings in the bottom row.
            var bar = Rect(panel, "ProgressBar", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(-43.6f, -7.24f), new Vector2(-302.8f, 70.07f));
            var settings = Rect(panel, "SettingsButton", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-83.3f, -7.24f), new Vector2(105, 105));
            Image(settings, skin.settingsBackground, Color.white, raycast: true);
            Image(Rect(settings, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(65, 65)), skin.settingsIcon, Color.white);
            Image(bar, skin.hudBar, Color.white);

            var holder = Rect(bar, "LvlHolder_Img", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 2.32f), new Vector2(271.48f, 68.61f));
            var holderImage = Image(holder, skin.hudLevelHolder, Color.white, sliced: true);
            holderImage.pixelsPerUnitMultiplier = 3.51f;
            Text(Rect(holder, "LvlNumber_Txt", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -27), new Vector2(239.33f, 0)),
                "LEVEL 1", skin.font, skin.brownLevelMaterial, 35, Hex("974A0AFF"));

            var slider = Rect(bar, "Slider", Vector2.zero, Vector2.one, new Vector2(0, 5), new Vector2(-50, -37.59f));
            Image(Rect(slider, "Bg", Vector2.zero, Vector2.one, new Vector2(-0.17f, 0), new Vector2(-0.73f, -0.21f)), skin.hudProgressBackground, Color.white);
            Stretch(slider, "PendingMilestoneHolder");
            var fillArea = Rect(slider, "FillArea", Vector2.zero, Vector2.one, new Vector2(0, 0.625f), new Vector2(-10, -1.25f));
            Image(Rect(fillArea, "Fill", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero), skin.hudProgressFill, Color.white);
            Stretch(slider, "CompletedMilestoneHolder");

            Image(Rect(bar, "Snacky", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-5.4f, 6.87f), new Vector2(140, 87.14f)), skin.hudSnakeHead, Color.white).preserveAspect = true;
            Image(Rect(bar, "Fruits", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0.11f), new Vector2(82.71f, 81.12f)), skin.hudFruits, Color.white).preserveAspect = true;
        }

        /// <summary>WavePending_HUD: a thin sliced strip across the bar with a dot.</summary>
        private GameObject PendingMarker(RectTransform holder, float fraction)
        {
            var rect = Rect(holder, "WavePending_HUD", new Vector2(fraction, 0), new Vector2(fraction, 1), new Vector2(0, -0.04f), new Vector2(19, 5.63f));
            Image(rect, skin.milestonePendingBar, Color.white, sliced: true);
            Image(Rect(rect, "Circle_Img", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19, 17.13f)), skin.milestonePendingDot, Color.white);
            return rect.gameObject;
        }

        /// <summary>WaveComplete_HUD: the filled strip with a check mark, popping in.</summary>
        private IEnumerator CompleteMarker(float fraction, GameObject pending)
        {
            yield return new WaitForSeconds(markerCompleteDelay);
            if (pending != null) Destroy(pending);
            var rect = Rect(_completedHolder, "WaveComplete_HUD", new Vector2(fraction, 0), new Vector2(fraction, 1), new Vector2(0, 0.64f), new Vector2(19, 4.48f));
            Image(rect, skin.milestoneDoneBar, Color.white, sliced: true);
            var check = Rect(rect, "CheckIcon_Img", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(19, 19));
            Image(check, skin.checkMark, Color.white);
            yield return Tween.Run(0.25f, Tween.OutBack(3f), t => { if (check != null) check.localScale = Vector3.one * t; });
        }

        private void Update()
        {
            if (_session == null || _fill == null) return;
            int eaten = _session.FruitEaten;
            _shown = Mathf.MoveTowards(_shown, eaten / _total, fillSpeed * Time.deltaTime);
            _fill.anchorMax = new Vector2(_shown, 1f);

            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker.pending == null || eaten < marker.fruit) continue;
                StartCoroutine(CompleteMarker(marker.fraction, marker.pending));
                _markers[i] = (marker.fraction, marker.fruit, null);
            }
        }
    }
}
