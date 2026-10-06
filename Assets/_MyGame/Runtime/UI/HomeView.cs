using System.Collections;
using SnackDash.Audio;
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
    /// lives and the settings button on top, event widgets left and right, the green "Level N" button with
    /// its streak bar, and the bottom bar (Shop / HOME / lock). Only Level and settings do something for
    /// now; coins, lives, widgets and tabs are for show [user] and are built one per method so each can get
    /// its feature later.
    /// </summary>
    public class HomeView : MonoBehaviour
    {
        [SerializeField] private UiSkin skin = new UiSkin();
        [SerializeField] private LevelList levels;
        [SerializeField] private SnackDash.Audio.SoundLibrary sounds;
        [Header("App start")]
        [SerializeField] private float splashSeconds = 1.5f;
        [SerializeField] private float startLoadingSeconds = 1.2f;
        [Header("Layout")]
        [Tooltip("Event widgets look about 10 % bigger in the video than their 188 rect.")]
        [SerializeField] private float widgetScale = 1.1f;

        private static bool _started; // splash and loading only once per run of the app
        private RectTransform _canvas;

        private void Start()
        {
            LoadingScreen.Skin = skin;
            _canvas = Canvas(transform, "Home", 10);
            Background(_canvas); // first: everything else draws over it
            var safe = Stretch(_canvas, "SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            TopBar(safe);
            Widgets(safe);
            PlayButton(safe, CurrentLevelNumber());
            BottomBar(_canvas);
            CentreOnLetters(_canvas);
            Pressable(_canvas);
            SoundPlayer.Use(sounds);
            SoundPlayer.Music("Music_Lobby");

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
            yield return LoadingScreen.Show(cover, skin, startLoadingSeconds);
            Destroy(cover.gameObject);
        }

        // ------------------------------------------------------------------ parts

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
            Counter(currencies, "Currency-Coins", 0f, "S_Coin_Icon", "317", null);
            Counter(currencies, "Currency-Lives", 1f, "S_Heart_Icon_0", "FULL", "5");

            var settings = Rect(content, "Settings", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(110f, 110f));
            Image(settings, skin.Get("S_Settings_Icon_0"), Color.white, raycast: true).preserveAspect = true;
            settings.gameObject.AddComponent<Button>().onClick.AddListener(() => SettingsPopup.Open(_canvas, skin, null));
        }

        private void Counter(RectTransform parent, string name, float side, string icon, string value, string amount)
        {
            var counter = Rect(parent, name, new Vector2(side, 0f), new Vector2(side, 1f), Vector2.zero, new Vector2(240f, 0f), new Vector2(side, 0.5f));
            Image(Rect(counter, "BG", Vector2.zero, Vector2.one, new Vector2(24f, 4f), new Vector2(-50f, -36f)), skin.Get("S_Counter_Bg_0"), Color.white, sliced: true);
            var iconRect = Rect(counter, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(85f, 85f));
            Image(iconRect, skin.Get(icon), Color.white).preserveAspect = true;
            if (amount != null) Text(Stretch(iconRect, "Amount"), amount, skin.font, skin.plainMaterial, 46f, Color.white);
            else Image(Rect(iconRect, "Plus Icon", Vector2.zero, Vector2.zero, new Vector2(70f, 15f), new Vector2(35f, 35f)), skin.Get("S_Plus_Icon_0"), Color.white);
            Text(Rect(counter, "Value Text", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(37f, 5f), new Vector2(120f, -38f)), value, skin.font, skin.plainMaterial, 40f, Hex("6B4A2BFF")); // plain brown (the Progress Bar preset has a blue outline)
        }

        /// <summary>LeftBadgesLayout / RIghtBadgesLayout: event widgets, about 215 apart (video).</summary>
        private void Widgets(RectTransform root)
        {
            var left = Rect(root, "LeftBadgesLayout", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(115f, 545f), Vector2.zero);
            Widget(left, 0f, "WeeklyMissionsWidget", "S_Chest_Icon_Widget_0", "CLAIM", true, 1);
            Widget(left, -205f, "LavaQuestWidget", "S_Lava Quest_Widget_0", "LEVEL 35", false, 0, locked: true);
            var right = Rect(root, "RIghtBadgesLayout", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-115f, 550f), Vector2.zero);
            Widget(right, 0f, "SeasonPassWidget", "S_Icon_GoldenTicket_0", "CLAIM", true, 1);
            Widget(right, -205f, "NoAdsWidget", "S_Icon_NoAds", "NO ADS", false, 0);
            Widget(right, -410f, "Widget_Offer", "S_Chest_Icon_Widget_0", "1D 16H", false, 0, clock: true);
        }

        /// <summary>Icons sit lower than the scene values, resting on the bar (video).</summary>
        private const float IconDrop = 18f;

        private void Widget(RectTransform parent, float y, string name, string icon, string label, bool claim, int notification, bool locked = false, bool clock = false)
        {
            var widget = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(188f, 188f));
            widget.localScale = Vector3.one * widgetScale;
            // Visual: the dark card (S_Bg_Widget_0, 155 x 153.5 round a 100 x 100 rect at +15) and the bow corners.
            var visual = Rect(widget, "Visual", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15.2f - IconDrop), new Vector2(100f, 100f));
            Image(Rect(visual, "S_Bg", Vector2.zero, Vector2.one, new Vector2(0f, -22.3f), new Vector2(54.9f, 53.5f)), skin.Get("S_Bg_Widget_0"), Color.white);
            Image(Rect(visual, "S_Bow_Corner_R", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72.4f, -56.9f), new Vector2(29.5f, 29.5f)), skin.Get("S_Bow_Widget_Corner_0"), Color.white);
            Image(Rect(visual, "S_Bow_Corner_L", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-67.2f, -61.5f), new Vector2(29.5f, 29.5f)), skin.Get("S_Bow_Widget_Corner_0"), Color.white);
            Image(Rect(widget, "S_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 15f - IconDrop), new Vector2(156f, 156f)), skin.Get(icon), Color.white).preserveAspect = true;
            var bar = Rect(widget, "TextBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(178f, 58f));
            Image(Rect(bar, "Shadow", Vector2.zero, Vector2.one, new Vector2(1.5f, -4f), Vector2.zero), skin.Get("S_Bow_Widget (2)_0"), new Color(0f, 0f, 0f, 0.17f), sliced: true);
            if (claim)
            {
                // CLAIM: a green box with small corners and a gold rim (video), not the purple bow.
                Image(Rect(bar, "Rim", Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-6f, -6f)), skin.bluePlate, Hex("F2A31BFF"), sliced: true).pixelsPerUnitMultiplier = 3f; // a white rounded box tinted gold (tinting the green sprite only gave olive)
                Image(Rect(bar, "TextBarVisual", Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-18f, -16f)), skin.Get("S_Btn_Green_UI_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 11f;
            }
            else Image(Stretch(bar, "TextBarVisual"), skin.Get("S_Bow_Widget (2)_0"), Color.white, sliced: true);
            // Small white letters with a thin outline in the bar's dark colour (video), not filling the bar.
            var text = Text(Rect(bar, "Text", Vector2.zero, Vector2.one, new Vector2(locked || clock ? 14f : 0f, 1f), new Vector2(locked || clock ? -52f : -34f, -14f)), label, skin.font, skin.plainMaterial, 28f, Color.white);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 28f;
            text.characterSpacing = 2f;
            text.fontMaterial.DisableKeyword("UNDERLAY_ON");
            text.fontMaterial.EnableKeyword("OUTLINE_ON");
            text.outlineWidth = 0.22f;
            text.outlineColor = claim ? Hex("1E6B0AFF") : Hex("4A2486FF");
            if (locked || clock)
                Image(Rect(bar, locked ? "Lock" : "Clock", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 2f), new Vector2(44f, 44f)), locked ? skin.lockIcon : skin.Get("S_Clock_Icon_Widget_0"), Color.white).preserveAspect = true;
            if (notification > 0)
            {
                var dot = Rect(widget, "Dot", Vector2.one, Vector2.one, new Vector2(-30f, -14f), new Vector2(45f, 45f));
                Image(dot, skin.Get("S_Notification_Icon_Widget_0"), Color.white);
                Text(Stretch(dot, "TMP_Notifications"), notification.ToString(), skin.font, skin.plainMaterial, 30f, Color.white);
            }
        }

        /// <summary>
        /// Button_Play_Level (0, -535, 498 x 195): View_Button_Play_Normal's beige underlay and four green
        /// layers (S_Button_Sheet_0..3), "Level N", and the CoreStreak bar on top with the flame and x2 coin.
        /// </summary>
        private void PlayButton(RectTransform root, int level)
        {
            var play = Rect(root, "Button_Play_Level", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -535f), new Vector2(497.7f, 195.1f));
            var view = Rect(play, "View_Button_Play_Normal", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(500f, 200f), new Vector2(0.5f, 0f));
            var underlay = Rect(view, "Underlay_Base", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(484.2f, 164.1f));
            Image(underlay, skin.Get("S_Button_Sheet_3"), new Color(0.831f, 0.702f, 0.576f));
            Image(Rect(underlay, "Underlay_Front", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 5f), new Vector2(484.2f, 154f)), skin.Get("S_Button_Sheet_3"), new Color(0.945f, 0.89f, 0.737f));
            Image(Rect(view, "Base", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(463.9f, 170.7f)), skin.Get("S_Button_Sheet_3"), new Color(0.224f, 0.624f, 0f));
            var front = Image(Rect(view, "Front", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 9.7f), new Vector2(459.7f, 148f)), skin.Get("S_Button_Sheet_2"), new Color(0.31f, 0.804f, 0.016f), raycast: true);
            Image(Rect(view, "Highlight", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50.3f), new Vector2(448.1f, 28f)), skin.Get("S_Button_Sheet_1"), new Color(0.471f, 0.94f, 0.14f));
            Image(Rect(view, "Outline", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(463.9f, 170.7f)), skin.Get("S_Button_Sheet_0"), new Color(0.176f, 0.529f, 0f));
            Text(Rect(view, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(388.7f, 90f)), "Level " + level, skin.font, skin.greenOutlineMaterial, 80f, Color.white);
            var button = play.gameObject.AddComponent<Button>();
            button.targetGraphic = front;
            button.onClick.AddListener(() => LoadingScreen.Load(LoadingScreen.GameScene, skin));

            // CoreStreak: five empty steps and the flame with the x2 coin (the streak itself comes with coins).
            var streak = Rect(play, "CoreStreak", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 13.2f), new Vector2(336f, 62f));
            Image(Rect(streak, "Shadow", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-13.7f, 2.4f), new Vector2(315.3f, 36.4f)), skin.Get("S_Streakbar_Shadow_0"), new Color(0f, 0f, 0f, 0.35f));
            var bar = Rect(streak, "Progress_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(311.9f, 50f));
            Image(bar, skin.Get("S_StreakBar_Thin_0"), Color.white, sliced: true);
            for (int i = 0; i < 5; i++)
                Image(Rect(bar, "Filler", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f + i * 56f, 0f), new Vector2(46f, 40f)),
                    skin.Get(i == 0 ? "S_Bg_CoreStreak_Progress_Unfilled_Corner_0" : "S_Bg_CoreStreak_Progress_Unfilled_0"), Color.white);
            var icon = Rect(bar, "Streak_Icon", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(45f, 16f), new Vector2(150f, 150f));
            Image(Rect(icon, "Flame_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3.2f, -2.9f), new Vector2(105.8f, 115.4f)), skin.Get("S_Flame_CoreStreak_0"), Color.white);
            var coin = Rect(icon, "Coin_Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3.2f, -11.2f), new Vector2(83.7f, 83.7f));
            Image(coin, skin.Get("S_Coin_CoreStreak_0"), Color.white);
            Text(Stretch(coin, "MultiplierTag"), "x<size=120%>2", skin.font, skin.plainMaterial, 32f, Hex("8B2B00FF")).richText = true;
        }

        /// <summary>Bottom HUD (190 high): Shop, HOME (the raised, active tab), the locked tab.</summary>
        private void BottomBar(RectTransform root)
        {
            var hud = Rect(root, "Bottom HUD", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 190f), new Vector2(0.5f, 0f));
            Image(hud, null, Hex("5B2BB5FF"));
            Image(Rect(hud, "Top line", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 8f), new Vector2(0.5f, 1f)), null, Hex("F2C54BFF"));
            var marker = Rect(hud, "Marker", new Vector2(1f / 3f, 0f), new Vector2(2f / 3f, 1f), Vector2.zero, Vector2.zero);
            Image(marker, skin.Get("S_Active_Tab_3_0"), Color.white);
            Tab(hud, 1f / 6f, "Shop", "S_Shop_Icon_0", 120f, null);
            Tab(hud, 0.5f, "Home", "S_Home_Icon_0", 175f, "HOME");
            Tab(hud, 5f / 6f, "Lock", null, 120f, null, skin.lockIcon); // the gold lock (two sprites are named S_Lock_Icon_0)
        }

        private void Tab(RectTransform hud, float x, string name, string icon, float size, string label, Sprite sprite = null)
        {
            var tab = Rect(hud, name, new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0f, label != null ? 40f : 0f), new Vector2(size, size));
            Image(tab, sprite != null ? sprite : skin.Get(icon), Color.white).preserveAspect = true;
            if (label != null) Text(Rect(hud, "Label", new Vector2(x, 0.5f), new Vector2(x, 0.5f), new Vector2(0f, -55f), new Vector2(300f, 50f)), label, skin.font, skin.plainMaterial, 43.7f, Color.white);
        }
    }
}
