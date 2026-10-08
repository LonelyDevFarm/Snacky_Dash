using System.Collections;
using SnackDash.Audio;
using SnackDash.Economy;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The Home scene (original Main.unity, video "home"): on the first start of the app the launch image
    /// (splash) and the purple loading screen, then Home: the illustration (BG_Upper / BG_Down), coins,
    /// lives and the settings button on top, event widgets left and right, the "Level N" button with its
    /// streak bar, and the bottom bar (Shop / HOME / lock).
    /// The layout is the prefab Prefabs/UI/Home (made from <see cref="BuildLayout"/> by SnackDash/Setup/Create UI
    /// Prefabs, then edited by hand [user]); this class only finds its parts by name and makes them work:
    /// counters, the button's colour and text by difficulty, the widgets' locked / CLAIM state, the streak, the tabs.
    /// </summary>
    public class HomeView : MonoBehaviour
    {
        [SerializeField] private UiSkin skin = new UiSkin();
        [SerializeField] private LevelList levels;
        [SerializeField] private SnackDash.Audio.SoundLibrary sounds;
        [Tooltip("Layout prefab (Prefabs/UI/Home); built in code when empty.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Prefabs/UI/WeeklyMissionsPopup and WeeklyMissionsRules; built in code when empty.")]
        [SerializeField] private GameObject weeklyPrefab;
        [SerializeField] private GameObject weeklyRulesPrefab;
        [SerializeField] private GameObject weeklyBadgeFtuePrefab;
        [Header("App start")]
        [SerializeField] private float splashSeconds = 1.5f;
        [SerializeField] private float startLoadingSeconds = 1.2f;

        private static bool _started; // splash and loading only once per run of the app
        private RectTransform _canvas;
        private TextMeshProUGUI _coins, _livesValue, _livesAmount;
        private Image _livesHeart;
        private GameObject _livesInfinite;
        private float _tick;
        private System.Action _unsubscribe;

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f || _coins == null) return;
            _tick = 0.25f; // the regen countdown
            RefreshCounters();
        }

        private void OnDestroy()
        {
            PlayerEconomy.Shared.Changed -= RefreshCounters;
            _unsubscribe?.Invoke();
        }

        /// <summary>Coins; the hearts show the lives and FULL, the time to the next life, or ∞ with the time left.</summary>
        private void RefreshCounters()
        {
            var economy = PlayerEconomy.Shared;
            _coins.text = economy.Coins.ToString();
            bool infinite = economy.InfiniteLives;
            int lives = economy.Lives;
            _livesAmount.text = lives.ToString();
            _livesAmount.gameObject.SetActive(!infinite);
            if (_livesInfinite != null) _livesInfinite.SetActive(infinite);
            if (_livesHeart != null) _livesHeart.enabled = !infinite || _livesInfinite == null;
            _livesValue.text = infinite ? LivesPopup.Clock(economy.InfiniteLeft)
                : lives >= PlayerEconomy.MaxLives ? "FULL" : $"{(int)economy.NextLifeIn.TotalMinutes:00}:{economy.NextLifeIn.Seconds:00}";
        }

        private void Start()
        {
            LoadingScreen.Skin = skin;
            UiShiny.Shader = skin.shinyShader;
            if (prefab != null)
            {
                _canvas = (RectTransform)Instantiate(prefab, transform).transform;
                _canvas.name = "Home";
            }
            else
            {
                _canvas = Canvas(transform, "Home", 10);
                BuildLayout(_canvas);
            }
            var safe = (RectTransform)_canvas.Find("SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            int level = CurrentLevelNumber();
            BindTopBar(safe);
            BindWidgets(safe, level);
            BindPlayButton(safe, level, levels != null && level <= levels.levels.Count ? levels.levels[level - 1].difficulty : 0);
            BindBottomBar();
            CentreOnLetters(_canvas);
            Pressable(_canvas);
            SoundPlayer.Use(sounds);
            if (level >= SnackDash.Bootstrap.LevelProgress.HomeFromLevel) SoundPlayer.Music("Music_Lobby"); // before Level 6 the app goes straight to the level

            // Splash and loading only when the app opens on Home, not when a level sends the player back.
            if (!_started && !LoadingScreen.Busy) StartCoroutine(AppStart());
            _started = true;
        }

        private int CurrentLevelNumber()
        {
            if (levels == null) return 1;
            var level = SnackDash.Bootstrap.LevelProgress.Current(levels);
            return Mathf.Max(1, levels.levels.IndexOf(level) + 1);
        }

        private IEnumerator AppStart()
        {
            var cover = Canvas(transform, "AppStart", 90);
            if (skin.splash != null)
            {
                var splash = Stretch(cover, "Splash");
                var image = splash.gameObject.AddComponent<RawImage>();
                image.texture = skin.splash;
                var fit = splash.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = (float)skin.splash.width / skin.splash.height;
                yield return new WaitForSeconds(splashSeconds);
                Destroy(splash.gameObject);
            }
            if (CurrentLevelNumber() < SnackDash.Bootstrap.LevelProgress.HomeFromLevel)
            {
                // No Home before Level 6 [user]: the loading screen leads straight into the level.
                LoadingScreen.Load(LoadingScreen.GameScene, skin);
                yield break;
            }
            yield return LoadingScreen.Show(cover, skin, startLoadingSeconds);
            Destroy(cover.gameObject);
        }

        // ================================================================== layout (saved as the prefab)

        /// <summary>Everything Home shows, in its default state (green button "Level 1", widgets locked).</summary>
        public void BuildLayout(RectTransform canvas)
        {
            Background(canvas); // first: everything else draws over it
            var safe = Stretch(canvas, "SafeArea");
            TopBar(safe);
            Widgets(safe);
            PlayButton(safe);
            BottomBar(canvas);
            CentreOnLetters(canvas);
        }

        /// <summary>BG_Art: the upper and lower halves of the home illustration.</summary>
        private void Background(RectTransform root)
        {
            var bg = Stretch(root, "BG");
            Image(Rect(bg, "BG_Upper", new Vector2(0f, 0.5f), Vector2.one, Vector2.zero, Vector2.zero), skin.Get("S_Illustration_HomeScreen_Upper"), Color.white);
            Image(Rect(bg, "BG_Down", Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero), skin.Get("S_Illustration_HomeScreen_Down"), Color.white);
        }

        /// <summary>HomeTopView: coins and lives counters in the middle, the settings button on the right.</summary>
        private void TopBar(RectTransform root)
        {
            var content = Rect(root, "HomeTopView", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -20f), new Vector2(0f, 128f), new Vector2(0.5f, 1f));
            var currencies = Rect(content, "CurrenciesTopView", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-45f, 0f), new Vector2(600f, 90f));
            currencies.localScale = Vector3.one * 1.05f; // a touch bigger than the scene values (video)
            Counter(currencies, "Currency-Coins", 0f, "S_Coin_Icon", "0", null);
            Counter(currencies, "Currency-Lives", 1f, "S_Heart_Icon_0", "FULL", "5");
            var settings = Rect(content, "Settings", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(110f, 110f));
            Image(settings, skin.Get("S_Settings_Icon_0"), Color.white, raycast: true).preserveAspect = true;
        }

        private void Counter(RectTransform parent, string name, float side, string icon, string value, string amount)
        {
            var counter = Rect(parent, name, new Vector2(side, 0f), new Vector2(side, 1f), Vector2.zero, new Vector2(240f, 0f), new Vector2(side, 0.5f));
            Image(Rect(counter, "BG", Vector2.zero, Vector2.one, new Vector2(24f, 4f), new Vector2(-50f, -36f)), skin.Get("S_Counter_Bg_0"), Color.white, sliced: true, raycast: true).gameObject.AddComponent<SlicedCapFit>();
            var iconRect = Rect(counter, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(85f, 85f));
            Image(iconRect, skin.Get(icon), Color.white).preserveAspect = true;
            if (amount != null)
            {
                Text(Stretch(iconRect, "Amount"), amount, skin.font, skin.plainMaterial, 46f, Color.white);
                // Icon_Unlimited of the original Currency-Lives: the heart with the infinity sign, shown instead while lives are infinite.
                var unlimited = Image(Rect(iconRect, "Icon_Unlimited", Vector2.zero, Vector2.one, new Vector2(0f, 2.08f), Vector2.zero), skin.Get("S_Heart_Infinity_Icon_0"), Color.white);
                unlimited.preserveAspect = true;
                unlimited.gameObject.SetActive(false);
            }
            else Image(Rect(iconRect, "Plus Icon", Vector2.zero, Vector2.zero, new Vector2(70f, 15f), new Vector2(35f, 35f)), skin.Get("S_Plus_Icon_0"), Color.white);
            Text(Rect(counter, "Value Text", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(37f, 5f), new Vector2(120f, -38f)), value, skin.font, skin.plainMaterial, 40f, Hex("6B4A2BFF")); // plain brown (the Progress Bar preset has a blue outline)
        }

        /// <summary>
        /// LeftBadgesLayout / RIghtBadgesLayout: event widgets, about 215 apart (video), 10 % bigger than their
        /// 188 rect. Event widgets hold both looks: "Locked" (gold lock, "LEVEL N") and "Claim" (green CLAIM, dot).
        /// </summary>
        private void Widgets(RectTransform root)
        {
            var left = Rect(root, "LeftBadgesLayout", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(115f, 545f), Vector2.zero);
            Widget(left, 0f, "WeeklyMissionsWidget", "S_Chest_Icon_Widget_0", "LEVEL " + WeeklyMissions.UnlockLevel);
            Widget(left, -205f, "LavaQuestWidget", "S_Lava Quest_Widget_0", "LEVEL 35");
            var right = Rect(root, "RIghtBadgesLayout", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-115f, 550f), Vector2.zero);
            Widget(right, 0f, "SeasonPassWidget", "S_Icon_GoldenTicket_0", "LEVEL 25");
            // No ads in this game, so no NO ADS widget [user]; the offer moves up into its place.
            Widget(right, -205f, "Widget_Offer", "S_Chest_Icon_Widget_0", "1D 16H", clock: true);
        }

        /// <summary>Icons sit lower than the scene values, resting on the bar (video).</summary>
        private const float IconDrop = 18f;

        private void Widget(RectTransform parent, float y, string name, string icon, string lockedLabel, bool clock = false)
        {
            var widget = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(188f, 188f));
            widget.localScale = Vector3.one * 1.1f;
            // Visual: the dark card (S_Bg_Widget_0, 155 x 153.5 round a 100 x 100 rect at +15) and the bow corners.
            var visual = Rect(widget, "Visual", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15.2f - IconDrop), new Vector2(100f, 100f));
            Image(Rect(visual, "S_Bg", Vector2.zero, Vector2.one, new Vector2(0f, -22.3f), new Vector2(54.9f, 53.5f)), skin.Get("S_Bg_Widget_0"), Color.white);
            Image(Rect(visual, "S_Bow_Corner_R", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72.4f, -56.9f), new Vector2(29.5f, 29.5f)), skin.Get("S_Bow_Widget_Corner_0"), Color.white);
            Image(Rect(visual, "S_Bow_Corner_L", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-67.2f, -61.5f), new Vector2(29.5f, 29.5f)), skin.Get("S_Bow_Widget_Corner_0"), Color.white);
            Image(Rect(widget, "S_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f - IconDrop), new Vector2(156f, 156f)), skin.Get(icon), Color.white).preserveAspect = true;
            var bar = Rect(widget, "TextBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(178f, 58f));
            Image(Rect(bar, "Shadow", Vector2.zero, Vector2.one, new Vector2(1.5f, -4f), Vector2.zero), skin.Get("S_Bow_Widget (2)_0"), new Color(0f, 0f, 0f, 0.17f), sliced: true);

            // Locked / clock: the purple bow, the icon on its left, the words beside it.
            var locked = Stretch(bar, clock ? "Clock" : "Locked");
            Image(Stretch(locked, "TextBarVisual"), skin.Get("S_Bow_Widget (2)_0"), Color.white, sliced: true);
            Image(Rect(locked, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(44f, 44f)), clock ? skin.Get("S_Clock_Icon_Widget_0") : skin.lockIcon, Color.white).preserveAspect = true;
            WidgetText(locked, lockedLabel, new Vector2(14f, 1f), new Vector2(-52f, -14f));
            if (clock) return;

            // CLAIM: a green box with small corners and a gold rim (video), not the purple bow; the red dot counts.
            var claim = Stretch(bar, "Claim");
            Image(Rect(claim, "Rim", Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-6f, -6f)), skin.bluePlate, Hex("F2A31BFF"), sliced: true).pixelsPerUnitMultiplier = 3f; // a white rounded box tinted gold (tinting the green sprite only gave olive)
            Image(Rect(claim, "TextBarVisual", Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-18f, -16f)), skin.Get("S_Btn_Green_UI_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 11f;
            WidgetText(claim, "CLAIM", new Vector2(0f, 1f), new Vector2(-34f, -14f));
            var dot = Rect(widget, "Dot", Vector2.one, Vector2.one, new Vector2(-30f, -14f), new Vector2(45f, 45f));
            Image(dot, skin.Get("S_Notification_Icon_Widget_0"), Color.white);
            Text(Stretch(dot, "TMP_Notifications"), "1", skin.font, skin.plainMaterial, 30f, Color.white);
            claim.gameObject.SetActive(false);
            dot.gameObject.SetActive(false);
        }

        /// <summary>Small white letters (the outline in the bar's colour is added at run time, see <see cref="OutlineWidgetText"/>).</summary>
        private void WidgetText(RectTransform bar, string label, Vector2 position, Vector2 size)
        {
            var text = Text(Rect(bar, "Text", Vector2.zero, Vector2.one, position, size), label, skin.font, skin.plainMaterial, 28f, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 28f;
            text.characterSpacing = 2f;
        }

        // Base, Front, Highlight, Outline colours of the three original buttons.
        private static readonly Color[] Green = { Hex("399F00FF"), Hex("4FCD04FF"), Hex("78F024FF"), Hex("2D8700FF") };
        private static readonly Color[] Purple = { Hex("833EA9FF"), Hex("C150DFFF"), Hex("D977EFFF"), Hex("8240A8FF") };
        private static readonly Color[] Red = { Hex("DA0400FF"), Hex("FF4945FF"), Hex("FF7373FF"), Hex("DE0A0AFF") };
        private static readonly string[] ButtonLayers = { "Base", "Front", "Highlight", "Outline" };

        /// <summary>
        /// Button_Play_Level (0, -535, 498 x 195): the beige underlay and four layers (S_Button_Sheet_0..3) coloured
        /// by the level's difficulty like the original Btn_Nomal / Btn_Hard / Btn_SuperHard, "Level N", the
        /// difficulty under it on hard levels, and the CoreStreak bar on top (shown from Level 23).
        /// </summary>
        private void PlayButton(RectTransform root)
        {
            var play = Rect(root, "Button_Play_Level", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -535f), new Vector2(497.7f, 195.1f));
            var view = Rect(play, "View_Button_Play_Normal", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(500f, 200f), new Vector2(0.5f, 0f));
            var underlay = Rect(view, "Underlay_Base", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(484.2f, 164.1f));
            Image(underlay, skin.Get("S_Button_Sheet_3"), new Color(0.831f, 0.702f, 0.576f));
            Image(Rect(underlay, "Underlay_Front", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(484.2f, 154f)), skin.Get("S_Button_Sheet_3"), new Color(0.945f, 0.89f, 0.737f));
            Image(Rect(view, "Base", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(463.9f, 170.7f)), skin.Get("S_Button_Sheet_3"), Green[0]);
            Image(Rect(view, "Front", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 9.7f), new Vector2(459.7f, 148f)), skin.Get("S_Button_Sheet_2"), Green[1], raycast: true);
            Image(Rect(view, "Highlight", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50.3f), new Vector2(448.1f, 28f)), skin.Get("S_Button_Sheet_1"), Green[2]);
            Image(Rect(view, "Outline", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(463.9f, 170.7f)), skin.Get("S_Button_Sheet_0"), Green[3]);
            // Normal title 10 above the view's centre (80); hard levels lift it 5.5 more and shrink it (68.8), with the
            // difficulty at -30.3 under it. Each colour has its own cartoon text.
            Text(Rect(view, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(388.7f, 90f)), "Level 1", skin.font, skin.greenOutlineMaterial, 80f, Color.white);
            var difficulty = Text(Rect(view, "TMP_Super Hard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30.3f), new Vector2(388.7f, 63f)), "Hard", skin.font, skin.purpleInnerShadowMaterial, 40f, Color.white);
            difficulty.gameObject.SetActive(false);
            StreakBar(play);
        }

        /// <summary>CoreStreak: five steps (unfilled, Filler_Yellow as a horizontal fill, Filler_Orange on fire) and the x2 coin.</summary>
        private void StreakBar(RectTransform play)
        {
            var streak = Rect(play, "CoreStreak", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 13.2f), new Vector2(336f, 62f));
            Image(Rect(streak, "Shadow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-13.7f, 2.4f), new Vector2(315.3f, 36.4f)), skin.Get("S_Streakbar_Shadow_0"), new Color(0f, 0f, 0f, 0.35f));
            var bar = Rect(streak, "Progress_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(311.9f, 50f));
            Image(bar, skin.Get("S_StreakBar_Thin_0"), Color.white, sliced: true);
            for (int i = 0; i < PlayerEconomy.StreakWinsToFire; i++)
            {
                bool start = i == 0;
                var step = Rect(bar, "Step " + i, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f + i * 56f, 0f), new Vector2(46f, 40f));
                Image(step, skin.Get(start ? "S_Bg_CoreStreak_Progress_Unfilled_Corner_0" : "S_Bg_CoreStreak_Progress_Unfilled_0"), Color.white);
                var yellow = Image(Stretch(step, "Filler_Yellow"), skin.Get(start ? "S_Bg_CoreStreak_Progress_Yellow_Corner_0" : "S_Bg_CoreStreak_Progress_Yellow_0"), Color.white);
                yellow.type = UnityEngine.UI.Image.Type.Filled;
                yellow.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                var filler = Stretch(step, "Filler_Orange");
                Image(filler, skin.Get(start ? "S_Bg_CoreStreak_Progress_Yellow_Corner-1_0" : "S_Bg_CoreStreak_Progress_Orange_0"), Color.white);
                Image(Stretch(filler, "Filler_Overlay"), skin.Get("S_Gradient_Vertical"), Hex("FFD300FF"));
                filler.gameObject.SetActive(false);
            }
            var icon = Rect(bar, "Streak_Icon", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(45f, 16f), new Vector2(150f, 150f));
            Image(Rect(icon, "Flame_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3.2f, -2.9f), new Vector2(105.8f, 115.4f)), skin.Get("S_Flame_CoreStreak_0"), Color.white);
            var glow = Rect(icon, "Coin_Shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3.2f, -11.2f), new Vector2(104f, 104f));
            Image(glow, skin.Get("S_Gradient_Radial"), Hex("FE7C00FF"));
            var coin = Rect(glow, "Coin_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(83.7f, 83.7f));
            Image(coin, skin.Get("S_Coin_CoreStreak_0"), Color.white);
            Text(Stretch(coin, "MultiplierTag"), "x<size=120%>2", skin.font, skin.plainMaterial, 32f, Hex("C85B00FF")).richText = true;
        }

        /// <summary>Bottom HUD (190 high): Shop, HOME, the locked tab, the lighter marker under the active one.</summary>
        private void BottomBar(RectTransform root)
        {
            var hud = Rect(root, "Bottom HUD", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 190f), new Vector2(0.5f, 0f));
            Image(hud, null, Hex("5B2BB5FF"));
            Image(Rect(hud, "Top line", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 8f), new Vector2(0.5f, 1f)), null, Hex("F2C54BFF"));
            Image(Rect(hud, "Marker", new Vector2(1f / 3f, 0f), new Vector2(2f / 3f, 1f), Vector2.zero, Vector2.zero), skin.Get("S_Active_Tab_3_0"), Color.white);
            Tab(hud, 1f / 6f, "Shop", skin.Get("S_Shop_Icon_0"), 120f, "SHOP");
            Tab(hud, 0.5f, "Home", skin.Get("S_Home_Icon_0"), 175f, "HOME");
            Tab(hud, 5f / 6f, "Lock", skin.lockIcon, 120f, null); // the gold lock (two sprites are named S_Lock_Icon_0)
        }

        private void Tab(RectTransform hud, float x, string name, Sprite sprite, float size, string label)
        {
            var tab = Rect(hud, name, new Vector2(x, 0.5f), new Vector2(x, 0.5f), Vector2.zero, new Vector2(size, size));
            Image(tab, sprite, Color.white, raycast: label != null).preserveAspect = true;
            if (label != null) Text(Rect(hud, name + " Label", new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0f, -55f), new Vector2(300f, 50f)), label, skin.font, skin.plainMaterial, 43.7f, Color.white);
        }

        // ================================================================== behaviour (found by name)

        private static T Find<T>(Transform root, string path) where T : Component
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void OnClick(Transform target, System.Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        private void BindTopBar(RectTransform safe)
        {
            var currencies = safe.Find("HomeTopView/CurrenciesTopView");
            _coins = Find<TextMeshProUGUI>(currencies, "Currency-Coins/Value Text");
            _livesValue = Find<TextMeshProUGUI>(currencies, "Currency-Lives/Value Text");
            // "40m 02s" is wider than "FULL": shrink to fit instead of running into the heart.
            _livesValue.enableAutoSizing = true;
            _livesValue.fontSizeMax = _livesValue.fontSize;
            _livesValue.fontSizeMin = _livesValue.fontSize * 0.6f;
            _livesAmount = Find<TextMeshProUGUI>(currencies, "Currency-Lives/Icon/Amount");
            _livesHeart = Find<Image>(currencies, "Currency-Lives/Icon");
            var infinite = currencies.Find("Currency-Lives/Icon/Icon_Unlimited");
            _livesInfinite = infinite != null ? infinite.gameObject : null;
            OnClick(currencies.Find("Currency-Lives/BG"), () => { if (!PlayerEconomy.Shared.InfiniteLives) LivesPopup.Open(_canvas, skin); }); // nothing while lives are infinite [user, sk1 video end]
            OnClick(currencies.Find("Currency-Coins/BG"), () => ShowShop(true)); // the coin counter and its + open the Shop
            PlayerEconomy.Shared.Changed += RefreshCounters;
            RefreshCounters();
            OnClick(safe.Find("HomeTopView/Settings"), () => SettingsPopup.Open(_canvas, skin, null));
        }

        /// <summary>Each event opens at a level and shows its gold lock with "LEVEL N" until then (weekly 15, season pass 25, lava 35) [user].</summary>
        private void BindWidgets(RectTransform safe, int level)
        {
            var weekly = EventWidget(safe.Find("LeftBadgesLayout/WeeklyMissionsWidget"), WeeklyMissions.UnlockLevel, level);
            if (weekly != null && level >= WeeklyMissions.UnlockLevel) WeeklyWidget(weekly);
            EventWidget(safe.Find("LeftBadgesLayout/LavaQuestWidget"), 35, level);
            EventWidget(safe.Find("RIghtBadgesLayout/SeasonPassWidget"), 25, level);
            OutlineWidgetText(safe.Find("RIghtBadgesLayout/Widget_Offer/TextBar/Clock/Text"), false);
        }

        private Transform EventWidget(Transform widget, int opensAt, int level)
        {
            if (widget == null) return null;
            bool open = level >= opensAt;
            var locked = widget.Find("TextBar/Locked");
            var claim = widget.Find("TextBar/Claim");
            if (locked != null) locked.gameObject.SetActive(!open);
            if (claim != null) claim.gameObject.SetActive(open);
            var lockedText = Find<TextMeshProUGUI>(widget, "TextBar/Locked/Text");
            if (lockedText != null) lockedText.text = "LEVEL " + opensAt;
            OutlineWidgetText(widget.Find("TextBar/Locked/Text"), false);
            OutlineWidgetText(widget.Find("TextBar/Claim/Text"), true);
            var dot = widget.Find("Dot");
            if (dot != null) dot.gameObject.SetActive(open); // events themselves come later: their dot says 1 [user]
            if (open)
            {
                // An open event shines: the light sweeps over its icon and its CLAIM bar (UIShiny of Main.unity) [user].
                UiShiny.Widget(Find<Image>(widget, "S_Icon"));
                UiShiny.Widget(Find<Image>(widget, "TextBar/Claim/TextBarVisual"));
            }
            return widget;
        }

        /// <summary>A thin outline in the bar's dark colour (video); done here because a material made in the editor is not saved in the prefab.</summary>
        private static void OutlineWidgetText(Transform target, bool claim)
        {
            var text = target != null ? target.GetComponent<TextMeshProUGUI>() : null;
            if (text == null) return;
            text.fontMaterial.DisableKeyword("UNDERLAY_ON");
            text.fontMaterial.EnableKeyword("OUTLINE_ON");
            text.outlineWidth = 0.22f;
            text.outlineColor = claim ? Hex("1E6B0AFF") : Hex("4A2486FF");
        }

        /// <summary>WEEKLY ADVENTURE: opens its popup (the rules first, once); the dot counts the missions to claim (sk1).</summary>
        private void WeeklyWidget(Transform widget)
        {
            var missions = WeeklyMissions.Shared;
            missions.StartIfUnlocked(CurrentLevelNumber());
            var dot = widget.Find("Dot");
            void Badge()
            {
                if (dot == null) return;
                int count = missions.ClaimableCount;
                dot.gameObject.SetActive(count > 0);
                dot.GetComponentInChildren<TextMeshProUGUI>().text = count.ToString();
            }
            Badge();
            missions.Changed += Badge;
            _unsubscribe += () => missions.Changed -= Badge;
            var hit = Image(Stretch(widget, "Hit"), null, Color.clear, raycast: true);
            OnClick(hit.transform, OpenWeekly);
            if (!WeeklyBadgeFtue.Seen) StartCoroutine(BadgeFtue((RectTransform)widget));
        }

        /// <summary>The popup, with its rules the first time.</summary>
        private void OpenWeekly()
        {
            var popup = WeeklyMissionsPopup.Open(_canvas, skin, weeklyPrefab).WithRules(weeklyRulesPrefab);
            if (!WeeklyMissionsRules.Seen) WeeklyMissionsRules.Open((RectTransform)popup.transform, skin, weeklyRulesPrefab);
        }

        /// <summary>"Weekly Adventure unlocked!" once Home is on screen (after the splash and loading) [sk1.3].</summary>
        private IEnumerator BadgeFtue(RectTransform widget)
        {
            yield return null;
            while (LoadingScreen.Busy || transform.Find("AppStart") != null) yield return null;
            WeeklyBadgeFtue.Open(_canvas, skin, widget, weeklyBadgeFtuePrefab, OpenWeekly);
        }

        private void BindPlayButton(RectTransform safe, int level, int difficulty)
        {
            var play = safe.Find("Button_Play_Level");
            var view = play.Find("View_Button_Play_Normal");
            bool hard = difficulty >= 2, superHard = difficulty >= 4;
            var colours = superHard ? Red : hard ? Purple : Green;
            for (int i = 0; i < ButtonLayers.Length; i++)
            {
                var layer = Find<Image>(view, ButtonLayers[i]);
                if (layer != null) layer.color = colours[i];
            }
            var title = Find<TextMeshProUGUI>(view, "Text (TMP)");
            title.text = "Level " + level;
            if (hard)
            {
                title.fontSharedMaterial = superHard ? skin.redCartoonInnerMaterial : skin.purpleCartoonPlayMaterial;
                title.rectTransform.anchoredPosition += new Vector2(0f, 5.5f);
                title.fontSize *= 68.8f / 80f;
                var label = Find<TextMeshProUGUI>(view, "TMP_Super Hard");
                label.gameObject.SetActive(true);
                label.text = superHard ? "Super Hard" : "Hard";
                label.fontSharedMaterial = superHard ? skin.redInnerShadowMaterial : skin.purpleInnerShadowMaterial;
                if (superHard) label.fontSize *= 38.4f / 40f;
            }
            var button = play.gameObject.AddComponent<Button>();
            button.targetGraphic = Find<Image>(view, "Front");
            button.onClick.AddListener(() =>
            {
                if (PlayerEconomy.Shared.CanPlay) LoadingScreen.Load(LoadingScreen.GameScene, skin);
                else LivesPopup.Open(_canvas, skin); // no lives left: the refill popup [user]
            });
            var streak = play.Find("CoreStreak");
            if (streak == null) return;
            bool shown = PlayerEconomy.StreakShown(level);
            streak.gameObject.SetActive(shown);
            if (shown) BindStreak(streak);
        }

        private const string StreakShownKey = "home_streak_shown"; // wins the bar showed last time, to animate the new ones

        /// <summary>Each win fills one step in yellow; on fire all five turn orange and the x2 goes red. New steps fill one after another.</summary>
        private void BindStreak(Transform streak)
        {
            var economy = PlayerEconomy.Shared;
            var bar = streak.Find("Progress_Container");
            var yellow = new Image[PlayerEconomy.StreakWinsToFire];
            var orange = new GameObject[yellow.Length];
            for (int i = 0; i < yellow.Length; i++)
            {
                yellow[i] = Find<Image>(bar, $"Step {i}/Filler_Yellow");
                orange[i] = bar.Find($"Step {i}/Filler_Orange").gameObject;
            }
            var coin = (RectTransform)bar.Find("Streak_Icon/Coin_Shadow/Coin_Icon");
            var tag = Find<TextMeshProUGUI>(coin, "MultiplierTag");
            bool fire = economy.StreakOnFire;
            int wins = fire ? yellow.Length : economy.StreakWins;
            int shownWins = Mathf.Clamp(PlayerPrefs.GetInt(StreakShownKey, 0), 0, wins); // after a reset the bar is simply empty
            for (int i = 0; i < yellow.Length; i++) { yellow[i].fillAmount = i < shownWins ? 1f : 0f; orange[i].SetActive(false); }
            PlayerPrefs.SetInt(StreakShownKey, wins);
            PlayerPrefs.Save();
            StartCoroutine(FillStreak(yellow, orange, shownWins, wins, fire, tag, coin));
        }

        /// <summary>Fillbar_Progress: each new step fills left to right; Fillbar_Progress_Full: all turn orange with a pop.</summary>
        private IEnumerator FillStreak(Image[] yellow, GameObject[] orange, int from, int to, bool fire, TextMeshProUGUI tag, RectTransform coin)
        {
            if (to > from) yield return new WaitForSeconds(0.4f);
            while (LoadingScreen.Busy) yield return null; // after the loading screen lifts
            for (int i = from; i < to; i++)
            {
                var step = yellow[i];
                yield return Tween.Run(0.3f, Tween.OutQuad, t => step.fillAmount = t);
                StartCoroutine(Tween.Punch(0.08f, t => step.transform.parent.localScale = Vector3.one * (1f + 0.2f * t)));
            }
            if (!fire) yield break;
            foreach (var step in orange) step.SetActive(true);
            tag.color = Hex("DC0600FF");
            if (to > from) yield return Tween.Punch(0.12f, t => coin.localScale = Vector3.one * (1f + 0.3f * t));
        }

        private RectTransform _marker;
        private readonly (RectTransform icon, GameObject label, Vector2 rest)[] _tabs = new (RectTransform, GameObject, Vector2)[2];
        private ShopView _shop;

        /// <summary>The active tab sits on the lighter marker, raised 40 with its name under it (video: HOME on Home, SHOP on the Shop page).</summary>
        private void BindBottomBar()
        {
            var hud = _canvas.Find("Bottom HUD");
            _marker = (RectTransform)hud.Find("Marker");
            string[] names = { "Shop", "Home" };
            for (int i = 0; i < names.Length; i++)
            {
                var icon = (RectTransform)hud.Find(names[i]);
                _tabs[i] = (icon, hud.Find(names[i] + " Label").gameObject, icon.anchoredPosition);
                bool shop = i == 0;
                OnClick(icon, () => ShowShop(shop));
            }
            SelectTab(1);
        }

        private void SelectTab(int index)
        {
            _marker.anchorMin = new Vector2(index / 3f, 0f);
            _marker.anchorMax = new Vector2((index + 1) / 3f, 1f);
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].icon.anchoredPosition = _tabs[i].rest + new Vector2(0f, i == index ? 40f : 0f);
                _tabs[i].label.SetActive(i == index);
            }
        }

        /// <summary>The Shop page between the top of the screen and the tab bar (no X), or back to Home.</summary>
        private void ShowShop(bool show)
        {
            if (show && _shop == null)
            {
                _shop = ShopView.Open(_canvas, skin, 190f, null);
                _shop.transform.SetSiblingIndex(_canvas.Find("Bottom HUD").GetSiblingIndex()); // under the tab bar
            }
            else if (!show && _shop != null) _shop.Close();
            SelectTab(show ? 0 : 1);
        }
    }
}
