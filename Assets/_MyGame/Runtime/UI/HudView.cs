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
        public UiSkin Skin => skin;
        [Tooltip("Layout prefab (Prefabs/UI/HUD); built in code when empty. Milestone markers are added at runtime.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Fill speed of the progress bar, in bar lengths per second (smoothing; not measured).")]
        [SerializeField] private float fillSpeed = 1.5f;
        [SerializeField] private float markerCompleteDelay = 0.15f;
        [Tooltip("The settings popup has BACK TO HOME from this level on [user: from Level 6].")]
        [SerializeField] private int backToHomeFromLevel = 6;
        [Tooltip("How long a combo's +N stays on the coin tag before it hides.")]
        [SerializeField] private float coinTagHold = 1.2f;
        [Header("Win streak on fire (View_StreakActive_HUD)")]
        [Tooltip("S_Flame_CoreStreak_0, S_Level_Number_BG_0_0 and Mat_UI_Streak_Glow.")]
        [SerializeField] private Sprite streakFlame;
        [SerializeField] private Sprite streakGlowSprite;
        [SerializeField] private Material streakGlow;
        [Tooltip("TX_FX_Sparkle, S_FX_SmallGlow_01_0 and MT_FX_UI_Particle_Additive: its sparks.")]
        [SerializeField] private Sprite streakSpark;
        [SerializeField] private Sprite streakEmber;
        [SerializeField] private Material streakParticles;

        private readonly List<(float fraction, int fruit, GameObject pending)> _markers = new List<(float, int, GameObject)>();
        private GameSession _session;
        private RectTransform _fill, _completedHolder;
        private float _shown;
        private float _total;

        private RectTransform _settingsCanvas;

        private void OpenSettings(int levelNumber)
        {
            if (_settingsCanvas == null) _settingsCanvas = UiBuilder.Canvas(transform, "Settings", 50);
            if (_settingsCanvas.childCount > 0) return;
            Time.timeScale = 0f;
            System.Action home = levelNumber >= backToHomeFromLevel ? () => { SnackDash.Economy.PlayerEconomy.Shared.Lose(); LoadingScreen.Load(LoadingScreen.HomeScene, skin); } : (System.Action)null; // leaving costs a life [user]
            SettingsPopup.Open(_settingsCanvas, skin, home, () => Time.timeScale = 1f);
        }

        /// <summary>
        /// View_StreakActive_HUD (original Game.unity): while the streak is on fire a flame (S_Flame_CoreStreak_0, its foot cut off by
        /// a mask) burns behind the snake head at the bar's left end, over a flickering orange glow (Mat_UI_Streak_Glow) [user].
        /// </summary>
        private void StreakOnFire(RectTransform progressBar)
        {
            if (streakFlame == null || progressBar.Find("View_StreakActive_HUD") != null) return;
            var view = UiBuilder.Rect(progressBar, "View_StreakActive_HUD", Vector2.zero, Vector2.zero, new Vector2(-5f, 56.6f), new Vector2(140f, 84f));
            view.localScale = new Vector3(0.69f, 0.886f, 0.797f);
            var snake = progressBar.Find("Snacky");
            if (snake != null) view.SetSiblingIndex(snake.GetSiblingIndex()); // just under the head
            var fx = UiBuilder.Rect(view, "Streak_FX_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 126f));
            var glow = UiBuilder.Image(UiBuilder.Rect(fx, "StreakGlow_HUD", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26.26f), new Vector2(150.4f, 97.2f)), streakGlowSprite, new Color(1f, 0.359f, 0f, 0.6f), sliced: true);
            glow.pixelsPerUnitMultiplier = 5.2f;
            glow.material = streakGlow;
            UiParticles.HudSparks(fx, streakSpark, streakEmber, streakParticles);
            var mask = UiBuilder.Rect(view, "Mask", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15.3f), new Vector2(161.4f, 139.2f));
            mask.gameObject.AddComponent<RectMask2D>();
            UiBuilder.Image(UiBuilder.Rect(mask, "StreakIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18.7f), new Vector2(147.3f, 147.3f)), streakFlame, Color.white);
        }

        /// <summary>Where combo coins fly to; shown only while a combo's coins land (ShowComboCoins).</summary>
        public RectTransform CoinTag { get; private set; }
        private TMPro.TMP_Text _coinTagText;

        private Coroutine _coinTagShow;

        /// <summary>
        /// One combo's coins [user]: the tag pops up with "+N" for that combo alone, stays a moment and goes again;
        /// the level total is only added up on the win screen.
        /// </summary>
        public void ShowComboCoins(int coins)
        {
            if (CoinTag == null || coins <= 0) return;
            if (_coinTagShow != null) StopCoroutine(_coinTagShow);
            _coinTagShow = StartCoroutine(CoinTagRoutine(coins));
        }

        private IEnumerator CoinTagRoutine(int coins)
        {
            if (_coinTagText != null) _coinTagText.text = "+" + coins;
            if (!CoinTag.gameObject.activeSelf)
            {
                CoinTag.gameObject.SetActive(true);
                yield return Tween.Run(0.25f, Tween.OutBack(3f), t => CoinTag.localScale = Vector3.one * t);
            }
            else yield return Tween.Punch(0.12f, t => CoinTag.localScale = Vector3.one * (1f + 0.25f * t));
            yield return new WaitForSeconds(coinTagHold);
            yield return Tween.Run(0.2f, Tween.InBack(2f), t => CoinTag.localScale = Vector3.one * (1f - t));
            CoinTag.gameObject.SetActive(false);
            CoinTag.localScale = Vector3.one;
            _coinTagShow = null;
        }

        private RectTransform BuildCoinTag(RectTransform bar)
        {
            var tag = Rect(bar, "ComboHolder", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, -16f), new Vector2(111.45f, 48.43f));
            Image(tag, skin.Get("S_CoinBackground_Gold_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 5.31f; // the original's: small round caps
            // LvlNumber_Txt: the original box is 0 high (TMP still draws it); here as high as the tag so auto-size works,
            // moved onto the middle of the orange part right of the coin (measured on a screenshot) [user: "lệch"].
            var label = Text(Rect(tag, "LvlNumber_Txt", Vector2.zero, Vector2.one, new Vector2(-4f, 5f), new Vector2(-50.97f, 0f)), "+0", skin.font, skin.plainMaterial, 28.6f, Color.white);
            label.alignment = TMPro.TextAlignmentOptions.Midline;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 28.6f;
            Image(Rect(tag, "Coin_Img", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-67.1f, 1.66f), new Vector2(59.3f, 57.4f)), skin.coinIcon, Color.white).preserveAspect = true;
            return tag;
        }

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

            // ComboHolder (original Panel_InGame_HUD: S_CoinBackground_Gold_0 111 x 48, coin left, "+N"), under the
            // right end of the bar as in the video. Made here when the HUD prefab has none.
            var progressBar = (RectTransform)root.Find("SafeArea/_top/Panel_InGame_HUD/ProgressBar");
            CoinTag = progressBar != null ? progressBar.Find("ComboHolder") as RectTransform : null;
            if (CoinTag == null && progressBar != null) CoinTag = BuildCoinTag(progressBar);
            if (progressBar != null && SnackDash.Economy.PlayerEconomy.Shared.StreakOnFire && SnackDash.Economy.PlayerEconomy.StreakShown(levelNumber)) StreakOnFire(progressBar);
            if (CoinTag != null)
            {
                _coinTagText = CoinTag.GetComponentInChildren<TMPro.TMP_Text>();
                CoinTag.gameObject.SetActive(false); // it pops up when a combo's coins arrive [user]
            }

            // Settings: pauses the level; BACK TO HOME from Level 6 on (earlier levels have none) [user].
            LoadingScreen.Skin = skin;
            var settingsButton = root.Find("SafeArea/_top/Panel_InGame_HUD/SettingsButton");
            if (settingsButton != null)
            {
                var image = settingsButton.GetComponent<UnityEngine.UI.Image>();
                if (image != null) image.raycastTarget = true;
                settingsButton.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => OpenSettings(levelNumber));
                UiBuilder.Pressable(settingsButton);
            }
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
