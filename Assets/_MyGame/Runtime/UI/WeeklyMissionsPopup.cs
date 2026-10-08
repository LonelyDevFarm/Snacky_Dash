using System.Collections;
using System.Collections.Generic;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// WEEKLY ADVENTURE, laid out from the original Resources/liveops/wm prefabs (WeeklyMissionsPopup, Mission,
    /// MissionPremium, WeeklyMissionDayItem, the milestone markers) and played as in videos sk1 / sk1.1.
    /// The layout is the prefab Prefabs/UI/WeeklyMissionsPopup (made from <see cref="BuildLayout"/>, then edited by
    /// hand [user]); its "Templates" hold one mission row and one day tab that are copied for each mission and day.
    /// This class finds the parts by name and plays them: the opening of Weekly Missions_Opening.anim, the rows of
    /// the chosen day (claimable first, then in progress, claimed last) appearing one after another, claiming
    /// (the rewards grow where they were, then the hat flies to the bar, coins and hearts to Home's counters, the
    /// rows slide into their new order) and the chest screen of a milestone (<see cref="WeeklyChestReward"/>).
    /// </summary>
    public class WeeklyMissionsPopup : MonoBehaviour
    {
        // Popup Parent of the prefab, in its own space (origin bottom left).
        private const float Width = 881.13f, Height = 1468f;
        /// <summary>Mask of the prefab: from 138 under the card to 1320 (anchors y -0.0941 .. 0.8992).</summary>
        private const float MaskBottom = -138.2f;
        /// <summary>Where the markers sit along the bar's fill: equal steps from the hat (0.028) to the grand prize [user: even, sk1.1].</summary>
        private static readonly float[] MarkerAt = { 0.271f, 0.514f, 0.757f, 1f };

        private UiSkin _skin;
        private WeeklyMissions _missions;
        private int _day;
        private RectTransform _root, _card, _header, _headerText, _mask, _bottom, _rows, _tabs, _fill, _markers, _hatIcon, _rowTemplate, _tabTemplate;
        private TextMeshProUGUI _timer, _hats;
        private Image _cover;
        private float _coverAlpha;
        private bool _busy;
        private readonly Dictionary<int, RectTransform> _rowOf = new Dictionary<int, RectTransform>();

        public static WeeklyMissionsPopup Open(RectTransform canvas, UiSkin skin, GameObject prefab = null)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, canvas).transform;
                root.name = "WeeklyMissionsPopup";
            }
            else
            {
                root = Stretch(canvas, "WeeklyMissionsPopup");
                BuildLayout(root, skin);
            }
            var popup = root.GetComponent<WeeklyMissionsPopup>();
            if (popup == null) popup = root.gameObject.AddComponent<WeeklyMissionsPopup>();
            popup._skin = skin;
            popup._missions = WeeklyMissions.Shared;
            popup._day = popup._missions.Day;
            popup.Bind(root);
            return popup;
        }

        private Sprite S(string name) => _skin.Get(name);
        private Material M(string preset) => _skin.EventMaterial(preset);

        /// <summary>A rect placed by its centre in the Popup Parent's space (origin bottom left).</summary>
        private static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
            => Rect(parent, name, Vector2.zero, Vector2.zero, new Vector2(x, y), new Vector2(w, h));

        // ================================================================== layout (saved as the prefab)

        /// <summary>The whole popup in its resting state, plus the row and tab templates (inactive).</summary>
        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            var popup = root.GetComponent<WeeklyMissionsPopup>();
            if (popup == null) popup = root.gameObject.AddComponent<WeeklyMissionsPopup>();
            popup._skin = skin;
            popup.Layout(root);
        }

        private void Layout(RectTransform root)
        {
            Image(Stretch(root, "Overlay"), null, new Color(0f, 0f, 0f, 0.957f), raycast: true); // Weekly Missions_Opening: 0.957
            var card = Rect(root, "Popup Parent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(Width, Height));

            // Mask / Popup Bottom: everything under the header, which slides down out of it when the popup opens.
            var mask = Rect(card, "Mask", new Vector2(0f, -0.0941f), new Vector2(1f, 0.8992f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));
            mask.gameObject.AddComponent<RectMask2D>();
            var bottom = Stretch(mask, "Popup Bottom");
            var content = Rect(bottom, "Content", Vector2.zero, Vector2.zero, new Vector2(0f, -MaskBottom), new Vector2(Width, Height), Vector2.zero);

            var paper = Image(At(content, "Background", Width * 0.5f, 656.3f, 841f, 1327f), S("Tex_WeeklyMissions_Background 1_0"), Color.white, sliced: true);
            paper.pixelsPerUnitMultiplier = 1.1f;
            Image(At(content, "shadow", Width * 0.5f, 1303f, 806f, 36f), _skin.bottomGradient, new Color(0.419f, 0.251f, 0.066f, 0.75f)).rectTransform.localScale = new Vector3(1f, -1f, 1f);
            BarLayout(content);
            At(content, "Mission Area", Width * 0.5f, 547.8f, 807f, 983.8f);
            Rect(content, "Days Section", Vector2.zero, Vector2.zero, new Vector2(Width * 0.5f, 6.6f), new Vector2(Width, 143.8f), new Vector2(0.5f, 1f)); // grows down when opening
            TimerLayout(content);

            var header = At(card, "Header", Width * 0.5f, 1390.6f, Width, 144.3f);
            Title(header, _skin);
            var close = Rect(header, "Cancel Button", Vector2.one, Vector2.one, new Vector2(-98.3f, -67.2f), new Vector2(138.3f, 126.2f));
            RoundButton(close, "S_Icon_Cancel", new Vector2(0.19f, 0.108f), new Vector2(0.81f, 0.726f), new Vector2(0.215f, 0.187f), new Vector2(0.785f, 0.813f), 6.97f);
            var info = Rect(header, "Info Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18.7f, -4.1f), new Vector2(138.3f, 126.2f), new Vector2(0f, 1f));
            RoundButton(info, "S_Icon_Info", new Vector2(0.214f, 0.153f), new Vector2(0.786f, 0.723f), new Vector2(0.257f, 0.239f), new Vector2(0.743f, 0.773f), 6.09f);

            var templates = Stretch(root, "Templates");
            RowTemplate(templates);
            TabTemplate(templates);
            templates.gameObject.SetActive(false);
            CentreOnLetters(root);
        }

        /// <summary>The WEEKLY ADVENTURE title on its header plate (also on the chest and rules screens).</summary>
        public static void Title(RectTransform header, UiSkin skin)
        {
            Image(Stretch(header, "Header Image"), skin.Get("Tex_WeeklyMissions_Headaer_0"), Color.white);
            var words = Rect(header, "Header Text Parent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(534.4f, 252.9f));
            Word(words, skin, "weekly", 33.7f, 124.5f, 4.4f, 109.4f, "LuckiestGuy-Regular weekly orange", Color.white);
            Word(words, skin, "adventure", -34.2f, 82.4f, 2.5f, 72f, "LuckiestGuy-Regular weekly blue", new Color(0.761f, 0.898f, 1f));
        }

        /// <summary>The red ribbon under the title ("Rewards", "Rules"), as the Timer of those screens.</summary>
        public static void Ribbon(RectTransform header, UiSkin skin, string text)
        {
            var ribbon = Rect(header, "Timer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(200f, 50f), new Vector2(0.5f, 1f));
            Image(Rect(ribbon, "Timer Background", Vector2.zero, Vector2.one, new Vector2(0f, -5.65f), new Vector2(0f, 11.3f)), skin.Get("S_Background_Timer_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.23f;
            Text(Stretch(ribbon, "Text (TMP)"), text.ToUpperInvariant(), skin.font, skin.EventMaterial("LuckiestGuy-Regular Red Cartoon 02"), 42.6f, Color.white);
        }

        /// <summary>A title word: the purple back and the coloured face 4 above it (as the prefab).</summary>
        private static void Word(RectTransform parent, UiSkin skin, string word, float y, float backSize, float faceY, float faceSize, string facePreset, Color faceColor)
        {
            var back = Rect(parent, "Text (TMP) - " + word, Vector2.zero, Vector2.one, new Vector2(0f, y), new Vector2(-145.4f, -70.8f));
            Text(back, word.ToUpperInvariant(), skin.font, skin.EventMaterial("LuckiestGuy-Regular SDF 2 - purple cartoon - weekly mission"), backSize, new Color(0.529f, 0.165f, 0.765f));
            var face = Text(Rect(back, "Face", Vector2.zero, Vector2.one, new Vector2(0f, faceY), Vector2.zero), word.ToUpperInvariant(), skin.font, skin.EventMaterial(facePreset), faceSize, faceColor);
            if (word == "weekly")
            {
                // The prefab's vertex gradient: light yellow on top, orange below.
                face.enableVertexGradient = true;
                Color top = new Color(1f, 0.854f, 0.368f), bottom = new Color(1f, 0.737f, 0.082f);
                face.colorGradient = new VertexGradient(top, top, bottom, bottom);
            }
        }

        /// <summary>The round header buttons: a brown shadow and the icon.</summary>
        private void RoundButton(RectTransform button, string icon, Vector2 shadowMin, Vector2 shadowMax, Vector2 iconMin, Vector2 iconMax, float shadowPpu)
        {
            var shadow = Image(Rect(button, "shadow", shadowMin, shadowMax, Vector2.zero, Vector2.zero), S("Shadow_Round"), new Color(0.774f, 0.521f, 0.26f), sliced: true, raycast: true);
            shadow.pixelsPerUnitMultiplier = shadowPpu;
            Image(Rect(button, "icon", iconMin, iconMax, Vector2.zero, Vector2.zero), S(icon), Color.white);
        }

        private void TimerLayout(RectTransform content)
        {
            var timer = At(content, "Timer", Width * 0.5f, 1287.3f, 210f, 50f); // 12 under the plate: at 1299 its top hid under the header [user]
            Image(Stretch(timer, "Timer Background"), S("S_Background_Timer_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.23f;
            var icon = Rect(timer, "Timer Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(23f, 2.8f), new Vector2(53.4f, 53.4f));
            Image(icon, S("S_Icon_Timer_01_0"), Color.white);
            Image(Rect(icon, "Timer Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.53f), new Vector2(19.8f, 17.9f), new Vector2(1f, 1f)), S("S_Icon_Timer_03_0"), Color.white);
            Image(Rect(icon, "Timer Center", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(15.1f, 15.1f)), S("S_Icon_Timer_02_0"), Color.white);
            Text(Rect(timer, "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-50f, 0f)), "7D 0H", _skin.font, M("LuckiestGuy-Regular Red Cartoon 02"), 36f, Color.white);
        }

        private void BarLayout(RectTransform content)
        {
            var area = At(content, "Progression Bar Area", 440.5f, 1205.8f, 803.6f, 233.8f);
            Image(Rect(area, "Decoration", new Vector2(0f, 0.084f), new Vector2(1f, 0.916f), Vector2.zero, Vector2.zero), S("Tex_WeeklyMissions_Background_Progression"), new Color(0.82f, 0.663f, 0.38f, 0.588f));
            var slider = Rect(area, "Slider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-4.6f, -44f), new Vector2(685.6f, 57.4f));
            Image(Rect(slider, "Background", Vector2.zero, Vector2.one, new Vector2(0f, -2.87f), new Vector2(-13.3f, -21.4f)), S("generic_round_128"), new Color(0.655f, 0.49f, 0.294f), sliced: true).pixelsPerUnitMultiplier = 3.69f;
            var fillArea = Rect(slider, "Fill Area", new Vector2(0.0097f, 0.1328f), new Vector2(0.9903f, 0.8672f), Vector2.zero, Vector2.zero);
            Image(Rect(fillArea, "Fill", Vector2.zero, new Vector2(0.2f, 1f), Vector2.zero, Vector2.zero), S("S_ProgressionBar_Fill_Yellow"), Color.white, sliced: true).pixelsPerUnitMultiplier = 3.5f;
            Stretch(fillArea, "MilestoneMarkersContainer");
            Image(Rect(slider, "Target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(372.9f, 39.7f), new Vector2(66.7f, 66.7f)), S("S_Weekly_Target_0"), Color.white)
                .rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18.1f);
            var hats = Rect(slider, "CollectedHats", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(25.3f, 3.5f), new Vector2(102.5f, 105.9f));
            Image(Stretch(hats, "Hat Icon"), S("S_Weekly_SafariHat_0"), Color.white);
            var count = Rect(hats, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(35.5f, -13.1f), new Vector2(71f, 46f));
            count.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Text(count, "0", _skin.font, M("LuckiestGuy-Regular Brown Cartoon"), 45.95f, Color.white);
        }

        /// <summary>
        /// The Mission prefab: every part of the three looks (in progress, claimable, claimed), the code shows the
        /// right ones; MissionPremium (the purchase) is the same row in gold, swapped at run time.
        /// </summary>
        private void RowTemplate(RectTransform templates)
        {
            var row = Rect(templates, "Mission", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98.16f), new Vector2(803.75f, 196.32f));
            var brown = new Color(0.514f, 0.345f, 0.122f);
            var paper = Image(Stretch(row, "S_BG_InProgress"), S("S_Weekly_Background_InProfgress_0"), new Color(1f, 1f, 1f, 0.87f), sliced: true, raycast: true);
            paper.pixelsPerUnitMultiplier = 0.415f;
            Image(Rect(row, "Separator Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 2f), new Vector2(-30f, 4f)), null, new Color(0.62f, 0.45f, 0.24f, 0.35f));
            Image(Stretch(row, "ClaimBackground"), S("S_Weekly_Background_Claimable_Green_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.4f;
            Image(Rect(row, "Mission Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(81.4f, 0f), new Vector2(108f, 114.3f)), S("S_Icon_Login_0"), Color.white).preserveAspect = true;

            // Claimed Missions Layout (scale 1.08 about the row's centre): "Claim" and the mission under it.
            Text(Rect(row, "Claim", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56.4f, 21.2f), new Vector2(353f, 64f)), "CLAIM", _skin.font, _skin.greenCartoonMaterial, 63.8f, Color.white);
            Fit(Text(Rect(row, "Claim Mission", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-56.4f, -31.1f), new Vector2(353f, 49f)), "LOGIN 1 DAY!", _skin.font, _skin.plainMaterial, 37f, new Color(0.216f, 0.549f, 0.02f)), 37f);

            var layout = Rect(row, "Default Missions Layout", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-58f, 10.2f), new Vector2(415.7f, 163.6f));
            var title = Text(Rect(layout, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(380f, 60.8f)), "LOGIN 1 DAY!", _skin.font, _skin.plainMaterial, 36f, brown);
            title.enableWordWrapping = true;
            Fit(title, 36f);
            var bar = Rect(layout, "Progression Bar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(352.7f, 37.3f));
            Image(bar, S("generic_round_128"), new Color(0.847f, 0.725f, 0.49f), sliced: true).pixelsPerUnitMultiplier = 5.4f;
            Image(Rect(bar, "FILL HERE", Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero), S("generic_round_128"), new Color(0.537f, 0.161f, 0.965f), sliced: true).pixelsPerUnitMultiplier = 5.4f;
            Text(Rect(bar, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1f), new Vector2(200f, 36f)), "0/1", _skin.font, M("LuckiestGuy-Regular Purple Cartoon"), 36f, Color.white);

            var claimed = Stretch(row, "Claimed Area");
            var claimedText = Text(Rect(claimed, "Claimed Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-55f, -4.1f), new Vector2(355.3f, 103.4f)), "LOGIN 1 DAY!", _skin.font, _skin.plainMaterial, 36f, brown);
            claimedText.enableWordWrapping = true;
            Fit(claimedText, 36f);
            var circle = Rect(claimed, "check mark", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64.7f, -2.1f), new Vector2(63.8f, 59.6f));
            TickBox(circle, new Vector2(6.6f, 9f));

            // Mission Rewards Area: the hat with its tokens and one more reward beside it (one item sits in the middle).
            var rewards = Rect(row, "Mission Rewards Area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(281.9f, 3f), new Vector2(200f, 153.6f));
            for (int i = 0; i < 2; i++)
            {
                var item = Rect(rewards, "Reward Item " + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -50f : 50f, 0f), new Vector2(100f, 100f));
                Image(Stretch(item, "Icon"), S("S_Weekly_SafariHat_0"), Color.white).preserveAspect = true;
                Text(Rect(item, "Amount", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 13f), new Vector2(0f, 46f)), "20", _skin.font, M("LuckiestGuy-Regular Brown Cartoon"), 40f, Color.white);
            }
            Image(Rect(row, "Lock", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(7f, -30f), new Vector2(94.9f, 80.9f), new Vector2(0.5f, 0.906f)), S("S_Lock_Icon_0"), Color.white).preserveAspect = true;
        }

        /// <summary>WeeklyMissionDayItem: both backgrounds (dark, light when chosen), DAY or the lock, the number, the red dot.</summary>
        private void TabTemplate(RectTransform templates)
        {
            var tab = Rect(templates, "DayItem", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 12f), new Vector2(101.5f, 117.9f), new Vector2(0.5f, 1f));
            Image(Stretch(tab, "Dark Item Background"), S("S_Weekly_Background_Day_Dark_0"), Color.white, sliced: true, raycast: true);
            Image(Stretch(tab, "White Item Background"), S("S_Weekly_Background_Day_Light"), Color.white, sliced: true, raycast: true);
            Text(Rect(tab, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 29.2f), new Vector2(87f, 28.7f)), "DAY", _skin.font, _skin.homeScreenLevelsMaterial, 26.35f, Color.white);
            Image(Rect(tab, "lock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(27f, 27f)), S("S_Icon_Lock_0"), new Color(0.604f, 0.451f, 0.263f));
            Text(Rect(tab, "DayNumber", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -13.1f), new Vector2(56.5f, 53.5f)), "1", _skin.font, _skin.homeScreenLevelsMaterial, 53.45f, Color.white);
            Image(Rect(tab, "Notification", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(37.9f, 67.2f), new Vector2(42.6f, 42.6f)), S("S_Icon_Notification_0"), Color.white);
        }

        /// <summary>
        /// The original check mark: the tan box (Shadow up 4.58, darker edge) and the green tick with its Outline (1, 1) and two
        /// Shadows (dark green 6.37 down: its thickness; black 35 % 3.73 down). The tick sprite came out of the build cropped
        /// (113 x 91 of 128), so it is drawn at its true share of the prefab's 78.3 rect instead of stretched over it [user].
        /// </summary>
        private void TickBox(RectTransform box, Vector2 tickCentre)
        {
            var boxImage = Image(box, S("generic_round_128"), new Color(0.827f, 0.678f, 0.388f), sliced: true);
            boxImage.pixelsPerUnitMultiplier = 8.75f;
            Effect<Shadow>(boxImage, new Color(0.786f, 0.598f, 0.373f), new Vector2(0f, 4.58f));
            var tick = Image(Rect(box, "check mark green", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), tickCentre, new Vector2(69.1f, 55.6f)), S("PictoIcon_Check_2"), new Color(0.31f, 0.851f, 0f));
            Effect<Outline>(tick, new Color(0.124f, 0.516f, 0f), new Vector2(1f, 1f));
            Effect<Shadow>(tick, new Color(0.125f, 0.518f, 0f), new Vector2(0f, -6.37f));
            Effect<Shadow>(tick, new Color(0f, 0f, 0f, 0.349f), new Vector2(0f, -3.73f));
        }

        /// <summary>A row template saved before the tick had its edge (08/10): give it the box shadow, outline, shadows and true size.</summary>
        private void UpgradeTick()
        {
            var box = _rowTemplate.Find("Claimed Area/check mark");
            var tick = _rowTemplate.Find("Claimed Area/check mark/check mark green");
            if (box == null || tick == null || tick.GetComponent<Shadow>() != null) return;
            Effect<Shadow>(box.GetComponent<Image>(), new Color(0.786f, 0.598f, 0.373f), new Vector2(0f, 4.58f));
            var image = tick.GetComponent<Image>();
            Effect<Outline>(image, new Color(0.124f, 0.516f, 0f), new Vector2(1f, 1f));
            Effect<Shadow>(image, new Color(0.125f, 0.518f, 0f), new Vector2(0f, -6.37f));
            Effect<Shadow>(image, new Color(0f, 0f, 0f, 0.349f), new Vector2(0f, -3.73f));
            ((RectTransform)tick).sizeDelta = new Vector2(69.1f, 55.6f);
        }

        private static void Effect<T>(Graphic target, Color color, Vector2 distance) where T : Shadow
        {
            var effect = target.gameObject.AddComponent<T>();
            effect.effectColor = color;
            effect.effectDistance = distance;
            effect.useGraphicAlpha = true;
        }

        private static void Fit(TextMeshProUGUI text, float size)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.6f;
            text.fontSizeMax = size;
        }

        // ================================================================== behaviour (found by name)

        private void Bind(RectTransform root)
        {
            _root = root;
            _cover = root.Find("Overlay").GetComponent<Image>();
            _coverAlpha = _cover.color.a;
            _card = (RectTransform)root.Find("Popup Parent");
            _header = (RectTransform)_card.Find("Header");
            _headerText = (RectTransform)_header.Find("Header Text Parent");
            _mask = (RectTransform)_card.Find("Mask");
            _bottom = (RectTransform)_mask.Find("Popup Bottom");
            var content = _bottom.Find("Content");
            _rows = (RectTransform)content.Find("Mission Area");
            _tabs = (RectTransform)content.Find("Days Section");
            _timer = content.Find("Timer/Text (TMP)").GetComponent<TextMeshProUGUI>();
            _clockHand = content.Find("Timer/Timer Icon/Timer Arrow");
            var slider = content.Find("Progression Bar Area/Slider");
            _fill = (RectTransform)slider.Find("Fill Area/Fill");
            _markers = (RectTransform)slider.Find("Fill Area/MilestoneMarkersContainer");
            _hatIcon = (RectTransform)slider.Find("CollectedHats");
            _hats = _hatIcon.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            _rowTemplate = (RectTransform)root.Find("Templates/Mission");
            _tabTemplate = (RectTransform)root.Find("Templates/DayItem");
            UpgradeTick();

            AddClick(_header.Find("Cancel Button"), Close);
            AddClick(_header.Find("Info Button"), () => WeeklyMissionsRules.Open(_root, _skin, _rulesPrefab));
            ShowPoints(_missions.Points);
            BuildMarkers();
            BuildTabs();
            BuildRows(null, 0.55f);
            CentreOnLetters(root);
            Pressable(root);
            _missions.Changed += Refresh;
            StartCoroutine(PopIn());
        }

        private GameObject _rulesPrefab;

        /// <summary>The rules screen's prefab (Prefabs/UI/WeeklyMissionsRules), given by Home.</summary>
        public WeeklyMissionsPopup WithRules(GameObject rulesPrefab) { _rulesPrefab = rulesPrefab; return this; }

        private static void AddClick(Transform target, System.Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        private void OnDestroy() { if (_missions != null) _missions.Changed -= Refresh; }

        /// <summary>Hermite keys with flat tangents (all the slopes of Weekly Missions_Opening are 0): smoothstep between keys.</summary>
        private static float Keys(float t, params float[] timeValue)
        {
            if (t <= timeValue[0]) return timeValue[1];
            for (int i = 2; i < timeValue.Length; i += 2)
                if (t <= timeValue[i])
                {
                    float u = (t - timeValue[i - 2]) / (timeValue[i] - timeValue[i - 2]);
                    return Mathf.Lerp(timeValue[i - 1], timeValue[i + 1], u * u * (3f - 2f * u));
                }
            return timeValue[timeValue.Length - 1];
        }

        /// <summary>
        /// Weekly Missions_Opening.anim (0.83 s): the cover darkens (0.15 s), the header plate widens from nothing
        /// (0.08-0.28 s) while its words pop (1.22 x 1.31 at 0.13 s), the body slides down out from under the header
        /// (Popup Bottom 1441 to 0, 0.22-0.53 s) and the mask bounces (1.075 high at 0.5 s), then the day tabs
        /// grow down (0.53-0.7 s).
        /// </summary>
        private IEnumerator PopIn()
        {
            _busy = true;
            var bottomRest = _bottom.anchoredPosition;
            var tabsScale = _tabs.localScale;
            System.Action<float> step = p =>
            {
                float t = p * 0.8333f;
                _cover.color = new Color(0f, 0f, 0f, Keys(t, 0f, 0f, 0.15f, _coverAlpha));
                _header.localScale = new Vector3(Keys(t, 0f, 0f, 0.0833f, 0f, 0.2833f, 1f), Keys(t, 0f, 0f, 0.0833f, 1f), 1f);
                _headerText.localScale = new Vector3(Keys(t, 0.0333f, 1f, 0.1333f, 1.225f, 0.3667f, 1f), Keys(t, 0.0333f, 1f, 0.1333f, 1.3083f, 0.3667f, 1f), 1f);
                _bottom.anchoredPosition = bottomRest + new Vector2(0f, Keys(t, 0.2167f, 1441f, 0.5333f, 0f));
                _mask.localScale = new Vector3(1f, Keys(t, 0.4333f, 1f, 0.5f, 1.075f, 0.7f, 1f), 1f);
                _tabs.localScale = new Vector3(tabsScale.x, tabsScale.y * Keys(t, 0.5333f, 0f, 0.7f, 1f), 1f);
            };
            step(0f); // hidden from the first frame (a nested tween only starts on the next one)
            yield return Tween.Run(0.8333f, Tween.Linear, step, true);
            _busy = false;
        }

        private float _tick;
        private Transform _clockHand;

        /// <summary>The timer's hand ticks a quarter turn clockwise every 0.7 s, each step taking about 0.1 s (sk1 17-21 s at 10 fps).</summary>
        private void TickClock()
        {
            if (_clockHand == null) return;
            const float step = 1f, turn = 0.2f; // the video measured 0.7 s, too quick on the phone [user]
            float t = Time.unscaledTime;
            int steps = Mathf.FloorToInt(t / step);
            float k = Mathf.Clamp01((t - steps * step) / turn);
            _clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(k)));
        }

        private void Update()
        {
            TickClock();
            if (_missions == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = _missions.Left;
            _timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes}M" : $"{left.Minutes}M {left.Seconds}S";
        }

        /// <summary>Where <paramref name="points"/> tokens reach along the fill (0 to 1): straight between markers.</summary>
        public static float FillAt(int points)
        {
            var milestones = WeeklyMissionsData.Milestones;
            float from = 0f; int fromPoints = 0;
            for (int i = 0; i < milestones.Length; i++)
            {
                if (points < milestones[i].points)
                    return Mathf.Lerp(from, MarkerAt[i], (points - fromPoints) / (float)(milestones[i].points - fromPoints));
                from = MarkerAt[i]; fromPoints = milestones[i].points;
            }
            return 1f;
        }

        private void ShowPoints(int points)
        {
            _hats.text = points.ToString();
            _fill.anchorMax = new Vector2(FillAt(points), 1f);
        }

        private void Refresh()
        {
            if (_busy) return;
            if (_tip != null) Destroy(_tip.gameObject);
            ShowPoints(_missions.Points);
            BuildMarkers();
            BuildTabs();
            BuildRows(null, -1f);
        }

        // ------------------------------------------------------------------ markers and their bubbles

        private void BuildMarkers()
        {
            for (int i = _markers.childCount - 1; i >= 0; i--) Destroy(_markers.GetChild(i).gameObject);
            var milestones = WeeklyMissionsData.Milestones;
            for (int i = 0; i < milestones.Length; i++)
            {
                bool grand = i == milestones.Length - 1;
                var marker = Rect(_markers, grand ? "GrandMilestone" : "MilestoneMarker " + i, new Vector2(MarkerAt[i], 0.5f), new Vector2(MarkerAt[i], 0.5f),
                    grand ? new Vector2(-12f, 0.3f) : Vector2.zero, new Vector2(100f, 100f));
                if (i < _missions.MilestonesReached && !grand) { CheckMark(marker); continue; }
                int index = i;
                var hit = Image(Rect(marker, "Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(139.7f, 144.2f)), null, Color.clear, raycast: true);
                hit.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowTip(index, marker, grand));
                float size = grand ? 129f : 100f;
                Image(Rect(marker, "chest image", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10.4f), new Vector2(size, size)), ChestSprite(i), Color.white);
                if (grand)
                {
                    var label = Rect(marker, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54.1f, -9.5f), new Vector2(114.1f, 57.7f));
                    label.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    var text = Text(label, "GRAND\nPRIZE", _skin.font, M("LuckiestGuy-Regular Red Cartoon 02"), 31.15f, Color.white);
                    text.lineSpacing = -30f;
                    if (i < _missions.MilestonesReached) CheckMark(marker);
                }
            }
            Pressable(_markers);
        }

        private RectTransform _tip;
        private Coroutine _tipTimer;

        /// <summary>
        /// Tapping a marker (Clicked Area of the marker prefabs, sk1.1 33 s): the tokens it needs on a brown pill under it
        /// and a speech bubble above with its rewards in a row; the grand prize's bubble opens to the left.
        /// </summary>
        private void ShowTip(int milestone, RectTransform marker, bool grand)
        {
            if (_tip != null) Destroy(_tip.gameObject);
            if (_tipTimer != null) StopCoroutine(_tipTimer);
            _tip = Rect(marker, "Clicked Area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));

            string need = WeeklyMissionsData.Milestones[milestone].points.ToString();
            float pillWidth = 52f + need.Length * 17f;
            var pill = Rect(_tip, "required hats area", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, -46.4f), new Vector2(pillWidth, 24.4f));
            Image(pill, S("generic_round_128"), new Color(0.655f, 0.49f, 0.294f), sliced: true).pixelsPerUnitMultiplier = 6.44f;
            Image(Rect(pill, "safari hat", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(1.3f, -0.45f), new Vector2(47.8f, 43.8f)), S("S_Weekly_SafariHat_0"), Color.white).preserveAspect = true;
            Text(Rect(pill, "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-24f, 6f)), need, _skin.font, _skin.homeScreenLevelsMaterial, 28f, new Color(1f, 0.953f, 0.827f));

            var rewards = Extras(WeeklyMissionsData.Milestones[milestone].reward, "x");
            // sk1.1 33-34 s (purple and grand): about 105 a reward plus a margin, 131 high, its foot 47 over the chest's centre;
            // the icon in the upper part and its amount under it, both inside the bubble.
            float width = rewards.Count * 105f + 30f;
            var bubble = Rect(_tip, "SpeechBubble2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(grand ? 40f : 6f, 47f), new Vector2(width, 131f), new Vector2(grand ? 1f : 0.5f, 0f));
            Image(Stretch(bubble, "background"), S("SpeechBubble_MainOutline"), new Color(0.651f, 0.329f, 0.012f), sliced: true).pixelsPerUnitMultiplier = 2.35f;
            // The inner sprite came out of the build cropped (122 x 109 of the 128 outline, 3 px in, 16 px up), so drawn on the
            // same rect it hid the outline: inset it by the cropped pixels at the slice scale (about 1.3 a side, 3 at the bottom) to show the thin brown edge [user: thinner].
            Image(Rect(bubble, "backgroundInner", Vector2.zero, Vector2.one, new Vector2(0f, 0.85f), new Vector2(-2.6f, -4.3f)), S("SpeechBubble_MainInner"), Color.white, sliced: true).pixelsPerUnitMultiplier = 2.35f;
            var pointer = Rect(bubble, "Pointer", new Vector2(grand ? 1f : 0.5f, 0f), new Vector2(grand ? 1f : 0.5f, 0f), new Vector2(grand ? -37.9f : 0f, 1.4f), new Vector2(16.2f, 16.2f));
            Image(pointer, S("SpeechBubble_PointerOutline"), new Color(0.651f, 0.333f, 0.008f));
            Image(Rect(pointer, "PointerInner", new Vector2(0f, 0.267f), Vector2.one, Vector2.zero, Vector2.zero), S("SpeechBubble_PointerInner"), Color.white); // cropped too: its upper 47 of 64 px
            for (int r = 0; r < rewards.Count; r++)
            {
                var view = Rect(bubble, "RewardView", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(15f + 52.5f + r * 105f, -65.5f), new Vector2(105f, 131f));
                Image(Rect(view, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -47f), new Vector2(84f, 84f)), rewards[r].icon, Color.white).preserveAspect = true;
                Text(Rect(view, "Amount", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(105f, 40f)), rewards[r].amount.ToUpperInvariant(), _skin.font, M("LuckiestGuy-Regular Brown Cartoon"), 36f, Color.white);
            }
            CentreOnLetters(_tip);
            _tip.SetParent(transform, true); // over the title, as on the phone
            var tip = _tip;
            tip.localScale = Vector3.zero;
            StartCoroutine(Tween.Run(0.2f, Tween.OutBack(1.7f), t => { if (tip != null) tip.localScale = Vector3.one * t; }, true));
            _tipTimer = StartCoroutine(HideTip(tip));
        }

        private IEnumerator HideTip(RectTransform tip)
        {
            yield return new WaitForSecondsRealtime(2.5f);
            if (tip == null) yield break;
            yield return Tween.Run(0.15f, Tween.Linear, t => { if (tip != null) tip.localScale = Vector3.one * (1f - t); }, true);
            if (tip != null) Destroy(tip.gameObject);
        }

        /// <summary>Bronze, blue, purple, then the red and gold grand prize (S_Weekly_Chests_M_0..3) [user: the first is bronze].</summary>
        public Sprite ChestSprite(int milestone) => S("S_Weekly_Chests_M_" + Mathf.Clamp(milestone, 0, 3));

        /// <summary>A reached marker: the beige circle with the green tick (check mark of the markers).</summary>
        private void CheckMark(RectTransform marker)
        {
            var circle = Rect(marker, "check mark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14.7f, -2.1f), new Vector2(63.8f, 59.6f));
            TickBox(circle, new Vector2(-4.9f, 17.8f));
        }

        // ------------------------------------------------------------------ days and missions

        private void BuildTabs(bool grow = false)
        {
            for (int i = _tabs.childCount - 1; i >= 0; i--) Destroy(_tabs.GetChild(i).gameObject);
            const float step = 107f;
            var rest = _tabTemplate.anchoredPosition;
            for (int day = 1; day <= WeeklyMissions.WeekDays; day++)
            {
                bool selected = day == _day, open = _missions.DayOpen(day);
                var tab = (RectTransform)Instantiate(_tabTemplate, _tabs);
                tab.name = "WeeklyMissionDayItem " + day;
                tab.anchoredPosition = rest + new Vector2((day - 4) * step, 0f);
                if (selected)
                {
                    // The chosen tab is bigger and hangs lower; a newly chosen one grows into it (about 0.13 s, sk1.1 14.8 s).
                    var from = _tabTemplate.localScale;
                    var to = from * 1.18f;
                    tab.localScale = grow ? from : to;
                    if (grow) StartCoroutine(Tween.Run(0.13f, Tween.OutQuad, t => { if (tab != null) tab.localScale = Vector3.Lerp(from, to, t); }, true));
                }
                tab.Find("Dark Item Background").gameObject.SetActive(!selected);
                tab.Find("White Item Background").gameObject.SetActive(selected);
                tab.Find("Text (TMP)").gameObject.SetActive(open);
                var lockIcon = tab.Find("lock").GetComponent<Image>();
                lockIcon.gameObject.SetActive(!open);
                if (selected) lockIcon.color = Color.white;
                var number = tab.Find("DayNumber").GetComponent<TextMeshProUGUI>();
                number.text = day.ToString();
                if (!selected) number.color = new Color(1f, 1f, 1f, open ? 0.85f : 0.6f);
                tab.Find("Notification").gameObject.SetActive(!selected && _missions.DayHasClaimable(day)); // no dot on the chosen day
                int chosen = day;
                AddClick(tab.Find(selected ? "White Item Background" : "Dark Item Background"), () =>
                {
                    if (_busy || chosen == _day) return;
                    _day = chosen;
                    BuildTabs(true);
                    BuildRows(null, 0f);
                });
            }
            Pressable(_tabs);
        }

        private const float RowStagger = 0.04f; // sk1.1 14.8 s at 30 fps: the rows change top to bottom, about one every frame or two

        /// <summary>Missions of the day: claimable first, then in progress, claimed last; on a locked day in progress first.</summary>
        private List<int> Order()
        {
            // On a day not open yet the done missions cannot be claimed: they go under the ones in progress (sk1 26 s, sk1.1 day 5) [user].
            bool open = _missions.DayOpen(_day);
            var order = new List<int>();
            int count = WeeklyMissionsData.Days[_day - 1].Length;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < count; i++)
                {
                    int state = _missions.Claimable(_day, i) ? (open ? 0 : 1) : _missions.Claimed(_day, i) ? 2 : (open ? 1 : 0);
                    if (state == pass) order.Add(i);
                }
            return order;
        }

        private float RowY(int slot) => _rowTemplate.anchoredPosition.y - slot * _rowTemplate.rect.height;

        /// <summary>
        /// The rows of the chosen day. Without <paramref name="from"/> they come in one after another from the top and the
        /// ticks of claimed ones pop after them (opening: once the body is down, <paramref name="delay"/>; a negative delay
        /// just shows them, after a claim settles); with it (a claim) each row slides from where it was to its new place.
        /// </summary>
        private void BuildRows(Dictionary<int, float> from, float delay)
        {
            // Changing day: the old rows stay until the new ones take their places one by one (sk1.1 14.8 s).
            bool stagger = from == null && delay >= 0f;
            var old = new List<GameObject>();
            for (int i = 0; i < _rows.childCount; i++) old.Add(_rows.GetChild(i).gameObject);
            if (!stagger) foreach (var row in old) Destroy(row);
            _rowOf.Clear();
            var order = Order();
            for (int slot = 0; slot < order.Count; slot++)
            {
                var row = Row(order[slot], slot);
                _rowOf[order[slot]] = row;
                if (from != null && from.TryGetValue(order[slot], out float oldY)) StartCoroutine(Slide(row, oldY, RowY(slot)));
                else if (stagger) StartCoroutine(Appear(row, delay + slot * RowStagger, slot < old.Count ? old[slot] : null));
            }
            if (stagger) for (int i = order.Count; i < old.Count; i++) Destroy(old[i]);
            CentreOnLetters(_rows);
            Pressable(_rows);
        }

        private static IEnumerator Slide(RectTransform row, float fromY, float toY)
        {
            var x = row.anchoredPosition.x;
            yield return Tween.Run(0.15f, Tween.OutQuad, t => { if (row != null) row.anchoredPosition = new Vector2(x, Mathf.Lerp(fromY, toY, t)); }, true);
        }

        /// <summary>A row takes the place of the old one with a little pop; a claimed row's tick pops a moment later (sk1.1 3.4 s, 14.0 s, 14.8 s).</summary>
        private static IEnumerator Appear(RectTransform row, float delay, GameObject replaces)
        {
            var group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var tick = row.Find("Claimed Area/check mark/check mark green");
            if (tick != null && !tick.gameObject.activeInHierarchy) tick = null;
            var tickScale = tick != null ? tick.localScale : Vector3.one;
            if (tick != null) tick.localScale = Vector3.zero;
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            if (replaces != null) Destroy(replaces);
            if (row == null) yield break;
            group.alpha = 1f;
            var scale = row.localScale;
            yield return Tween.Run(0.12f, Tween.OutBack(2f), t => { if (row != null) row.localScale = scale * Mathf.LerpUnclamped(0.96f, 1f, t); }, true);
            if (tick == null) yield break;
            yield return Tween.Run(0.15f, Tween.OutBack(2f), t => { if (tick != null) tick.localScale = tickScale * t; }, true);
        }

        private RectTransform Row(int index, int slot)
        {
            var mission = WeeklyMissions.MissionAt(_day, index);
            bool premium = mission.goal == MissionGoal.CompletePurchase;
            bool claimable = _missions.Claimable(_day, index), claimed = _missions.Claimed(_day, index), open = _missions.DayOpen(_day);
            var row = (RectTransform)Instantiate(_rowTemplate, _rows);
            row.name = "Mission " + index;
            row.anchoredPosition = new Vector2(_rowTemplate.anchoredPosition.x, RowY(slot));
            Color brown = premium ? new Color(0.675f, 0.424f, 0.141f) : new Color(0.514f, 0.345f, 0.122f);
            string text = mission.Text.ToUpperInvariant();

            if (premium)
            {
                // MissionPremium: the gold row.
                var paper = row.Find("S_BG_InProgress").GetComponent<Image>();
                paper.sprite = S("S_Weekly_Background_InProfgress_Gold_V2_0");
                paper.color = Color.white;
                row.Find("ClaimBackground").GetComponent<Image>().sprite = S("S_Weekly_Background_Claimable_Gold_V2_0");
                row.Find("Claim").GetComponent<TextMeshProUGUI>().fontSharedMaterial = M("LuckiestGuy-Regular weekly Brown");
                row.Find("Claim Mission").GetComponent<TextMeshProUGUI>().color = brown;
                row.Find("Default Missions Layout/Title").GetComponent<TextMeshProUGUI>().color = brown;
                row.Find("Default Missions Layout/Progression Bar").GetComponent<Image>().color = new Color(0.761f, 0.596f, 0.278f);
                row.Find("Claimed Area/Claimed Text").GetComponent<TextMeshProUGUI>().color = brown;
            }

            row.Find("ClaimBackground").gameObject.SetActive(claimable);
            if (claimable) UiShiny.Row(row.Find("ClaimBackground").GetComponent<Image>()); // the light that sweeps over it (UIShiny of the Mission prefab)
            row.Find("Claim").gameObject.SetActive(claimable);
            row.Find("Claim Mission").gameObject.SetActive(claimable);
            row.Find("Claim Mission").GetComponent<TextMeshProUGUI>().text = text;
            row.Find("Mission Icon").gameObject.SetActive(!claimed);
            row.Find("Mission Icon").GetComponent<Image>().sprite = S(Icons[mission.goal]);
            row.Find("Default Missions Layout").gameObject.SetActive(!claimable && !claimed);
            row.Find("Default Missions Layout/Title").GetComponent<TextMeshProUGUI>().text = text;
            int progress = _missions.Progress(_day, index);
            var fill = (RectTransform)row.Find("Default Missions Layout/Progression Bar/FILL HERE");
            fill.anchorMax = new Vector2(mission.amount > 0 ? Mathf.Clamp01(progress / (float)mission.amount) : 0f, 1f);
            fill.gameObject.SetActive(progress > 0);
            row.Find("Default Missions Layout/Progression Bar/Text (TMP)").GetComponent<TextMeshProUGUI>().text = $"{progress}/{mission.amount}";
            row.Find("Claimed Area").gameObject.SetActive(claimed);
            row.Find("Claimed Area/Claimed Text").GetComponent<TextMeshProUGUI>().text = text;
            row.Find("Lock").gameObject.SetActive(!open);

            var rewards = (RectTransform)row.Find("Mission Rewards Area");
            if (claimable) rewards.SetAsLastSibling(); // over the green claim background
            var items = new List<(Sprite icon, string amount)> { (S("S_Weekly_SafariHat_0"), mission.reward.points.ToString()) };
            items.AddRange(Extras(mission.reward, "x"));
            var first = (RectTransform)rewards.Find("Reward Item 0");
            var second = (RectTransform)rewards.Find("Reward Item 1");
            if (items.Count == 1) first.anchoredPosition = (first.anchoredPosition + second.anchoredPosition) * 0.5f; // one item sits in the middle
            for (int i = 0; i < 2; i++)
            {
                var item = i == 0 ? first : second;
                item.gameObject.SetActive(i < items.Count);
                if (i >= items.Count) continue;
                item.Find("Icon").GetComponent<Image>().sprite = items[i].icon;
                item.Find("Amount").GetComponent<TextMeshProUGUI>().text = items[i].amount;
            }
            if (claimed) rewards.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            if (claimable) AddClick(row, () => OnClaim(index, row));
            return row;
        }

        private static readonly Dictionary<MissionGoal, string> Icons = new Dictionary<MissionGoal, string>
        {
            { MissionGoal.CollectCoins, "S_Icon_EarnCoins_0" }, { MissionGoal.SpendCoins, "S_Icon_SpendCoins_0" }, { MissionGoal.SpendAnyBooster, "S_Icon_UseAnyBooster_0" },
            { MissionGoal.SpendBoomerang, "S_Icon_Booster_FruitCanon" }, { MissionGoal.SpendShovel, "S_Icon_Booster_Teleporter" }, { MissionGoal.CompleteAnyLevel, "S_Icon_CompleteLevels_0" },
            { MissionGoal.CompleteHardLevel, "S_Icon_CompleteHardLevels_0" }, { MissionGoal.CompleteSuperHardLevel, "S_Icon_CompleteSuperHardLevels_0" },
            { MissionGoal.CompleteLevelInStreak, "S_Icon_CompleteLevelStreak_0" }, { MissionGoal.WatchRewardedVideo, "S_Icon_WatchAds_0" },
            { MissionGoal.CompletePurchase, "S_Icon_MakeAPurchase_0" }, { MissionGoal.LoginDay, "S_Icon_Login_0" }, { MissionGoal.SpendUndo, "S_Icon_Booster_Undo" },
            { MissionGoal.SpendMagnet, "S_Icon_Booster_Magnet" },
        };

        /// <summary>Coins, boosters ("x1") and infinite lives ("15M", "2H") of a reward, with their icons.</summary>
        public List<(Sprite icon, string amount)> Extras(MissionReward reward, string times)
        {
            var list = new List<(Sprite, string)>();
            if (reward.coins > 0) list.Add((S("Coin_Icon_02_0"), reward.coins.ToString()));
            string[] boosterIcons = { "S_Icon_Booster_Teleporter", "S_Icon_Booster_FruitCanon", "S_Icon_Booster_Undo", "S_Icon_Booster_Magnet" };
            var boosters = reward.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) list.Add((S(boosterIcons[b]), times + boosters[b]));
            if (reward.infiniteMinutes > 0) list.Add((S("S_Icon_Heart_Infinit_Lives"), reward.infiniteMinutes % 60 == 0 ? reward.infiniteMinutes / 60 + "H" : reward.infiniteMinutes + "M"));
            return list;
        }

        // ------------------------------------------------------------------ claiming

        private void OnClaim(int index, RectTransform row)
        {
            if (_busy) return;
            if (!_missions.DayOpen(_day)) { StartCoroutine(Notice(row, $"UNLOCK AT DAY {_day}!")); return; }
            _busy = true;
            int before = _missions.Points;
            var reward = WeeklyMissions.MissionAt(_day, index).reward;

            // What flies: copies of the row's reward items where they are now.
            var flying = new List<(RectTransform item, string kind, string label)>();
            var rewards = row.Find("Mission Rewards Area");
            var extras = Extras(reward, "x");
            for (int i = 0; i < 2; i++)
            {
                var item = (RectTransform)rewards.Find("Reward Item " + i);
                if (!item.gameObject.activeSelf) continue;
                string kind = i == 0 ? "hat" : reward.coins > 0 ? "coins" : reward.infiniteMinutes > 0 && extras.Count == 1 ? "lives" : "booster";
                string label = i == 0 ? "+" + reward.points : kind == "coins" ? "+" + reward.coins : extras[0].amount.ToUpperInvariant();
                var copy = (RectTransform)Instantiate(item, _root);
                copy.position = item.position;
                copy.localScale = Vector3.one * item.lossyScale.x / _root.lossyScale.x;
                copy.Find("Amount").GetComponent<TextMeshProUGUI>().text = label;
                flying.Add((copy, kind, label));
            }

            var oldY = new Dictionary<int, float>();
            foreach (var pair in _rowOf) oldY[pair.Key] = pair.Value.anchoredPosition.y;
            var reached = _missions.Claim(_day, index);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();
            BuildRows(oldY, 0f); // the rows slide into their new order at once (sk1.1 22.7 s)
            BuildTabs();
            StartCoroutine(ClaimFlow(flying, before, before + reward.points, reached));
        }

        /// <summary>
        /// "Unlock at Day N!" (sk1 26.8-27.3 s): white letters with a dark edge born low on the tapped row rise straight
        /// up past its top in 0.6 s, fading at the end; each tap sends another one [user].
        /// </summary>
        private IEnumerator Notice(RectTransform row, string message)
        {
            var label = Text(Rect(_root, "Unlock Notice", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 60f)), message, _skin.font, M("LuckiestGuy-Regular Brown Cartoon"), 40f, Color.white);
            label.alignment = TextAlignmentOptions.Midline;
            // Born low on the tapped row, a little big, and rising nearly straight up past its top edge (sk1 26.8-27.3 s) [user].
            float h = row.rect.height;
            var start = row.TransformPoint(new Vector3(-56f, -0.3f * h, 0f));
            var end = row.TransformPoint(new Vector3(-44f, 0.95f * h, 0f)); // a little higher than past the top edge [user]
            label.rectTransform.position = start;
            var scale = label.rectTransform.localScale;
            yield return Tween.Run(0.6f, Tween.Linear, t =>
            {
                if (label == null) return;
                label.rectTransform.position = Vector3.Lerp(start, end, Tween.OutQuad(t));
                label.rectTransform.localScale = scale * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t / 0.18f));
                label.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }, true);
            if (label != null) Destroy(label.gameObject);
        }

        /// <summary>A part of Home behind the popup (its coin and life counters), or null.</summary>
        private Transform HomePart(string path) => _root.parent != null ? _root.parent.Find(path) : null;

        /// <summary>
        /// sk1.1 22.7-24.0 s: the rewards grow where they were with their "+N" (to 1.5 x in 0.12 s) and rise a little
        /// for half a second; then the hat flies to the bar (which counts up and fills), the coins burst into single
        /// coins that fly to Home's coin counter, the heart to its lives counter; boosters rise and fade. Then the chests.
        /// </summary>
        private IEnumerator ClaimFlow(List<(RectTransform item, string kind, string label)> flying, int from, int to, List<int> reached)
        {
            var starts = new Vector3[flying.Count];
            var scales = new Vector3[flying.Count];
            for (int i = 0; i < flying.Count; i++) { starts[i] = flying[i].item.localPosition; scales[i] = flying[i].item.localScale; }
            yield return Tween.Run(0.12f, Tween.OutBack(2f), t =>
            {
                for (int i = 0; i < flying.Count; i++) flying[i].item.localScale = scales[i] * Mathf.LerpUnclamped(1f, 1.5f, t);
            }, true);
            yield return Tween.Run(0.5f, Tween.OutQuad, t =>
            {
                for (int i = 0; i < flying.Count; i++) flying[i].item.localPosition = starts[i] + Vector3.up * 25f * t;
            }, true);

            int pending = 0;
            foreach (var (item, kind, _) in flying)
            {
                pending++;
                switch (kind)
                {
                    case "hat": StartCoroutine(FlyTo(item, _hatIcon, () => { pending--; StartCoroutine(CountHats(from, to)); })); break;
                    case "coins": StartCoroutine(CoinBurst(item, HomePart("SafeArea/HomeTopView/CurrenciesTopView/Currency-Coins/Icon"), () => pending--)); break;
                    case "lives": StartCoroutine(FlyTo(item, HomePart("SafeArea/HomeTopView/CurrenciesTopView/Currency-Lives/Icon"), () => pending--)); break;
                    default: StartCoroutine(FlyTo(item, null, () => pending--)); break;
                }
            }
            while (pending > 0) yield return null;
            yield return Tween.Run(0.4f, Tween.Linear, _ => { }, true); // the bar finishes counting

            foreach (int milestone in reached)
            {
                bool done = false;
                var marker = _markers.childCount > milestone ? (RectTransform)_markers.GetChild(milestone) : _hatIcon;
                WeeklyChestReward.Open(_root, _skin, this, milestone, marker.position, () => done = true);
                while (!done) yield return null;
            }
            _busy = false;
            Refresh();
        }

        /// <summary>Flight of the hat and hearts, and of each coin [user: 0.35 s was a little fast].</summary>
        private const float FlightTime = 0.55f, CoinFlightTime = 0.5f;

        /// <summary>An arc to <paramref name="target"/> shrinking into it (or up and away when there is none).</summary>
        private IEnumerator FlyTo(RectTransform item, Transform target, System.Action arrived)
        {
            Vector3 start = item.position, end = target != null ? target.position : start + Vector3.up * 300f * _root.lossyScale.y;
            var scale = item.localScale;
            var group = item.gameObject.AddComponent<CanvasGroup>();
            yield return Tween.Run(FlightTime, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                item.position = Vector3.Lerp(start, end, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 80f * _root.lossyScale.y;
                item.localScale = scale * Mathf.Lerp(1f, 0.5f, e);
                if (target == null) group.alpha = 1f - e;
            }, true);
            Destroy(item.gameObject);
            if (target != null) StartCoroutine(Tween.Punch(0.08f, s => { if (target != null) target.localScale = Vector3.one * (1f + 0.15f * s); }));
            arrived();
        }

        /// <summary>The coin pile breaks into single coins that fly one after another to the counter (sk1.1 23.4 s).</summary>
        private IEnumerator CoinBurst(RectTransform pile, Transform target, System.Action arrived)
        {
            const int count = 6;
            Vector3 centre = pile.position;
            float unit = _root.lossyScale.y;
            Destroy(pile.gameObject);
            int left = count;
            for (int i = 0; i < count; i++)
            {
                var coin = Rect(_root, "Flying Coin", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                Image(coin, S("S_Icon_Coin_Single_0"), Color.white).preserveAspect = true;
                coin.position = centre;
                var spread = centre + (Vector3)(Random.insideUnitCircle * 70f * unit);
                StartCoroutine(Coin(coin, spread, target, i * 0.05f, () => left--));
            }
            while (left > 0) yield return null;
            if (target != null) StartCoroutine(Tween.Punch(0.08f, s => { if (target != null) target.localScale = Vector3.one * (1f + 0.15f * s); }));
            arrived();
        }

        private IEnumerator Coin(RectTransform coin, Vector3 spread, Transform target, float delay, System.Action done)
        {
            Vector3 start = coin.position;
            yield return Tween.Run(0.16f, Tween.OutQuad, t => coin.position = Vector3.Lerp(start, spread, t), true);
            for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
            Vector3 end = target != null ? target.position : spread + Vector3.up * 400f * _root.lossyScale.y;
            yield return Tween.Run(CoinFlightTime, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                coin.position = Vector3.Lerp(spread, end, e);
                coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, e);
            }, true);
            Destroy(coin.gameObject);
            done();
        }

        private IEnumerator CountHats(int from, int to)
        {
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(0.4f, Tween.OutQuad, t => ShowPoints(Mathf.RoundToInt(Mathf.Lerp(from, to, t))), true);
        }

        private void Close()
        {
            if (_busy) return;
            Destroy(gameObject);
        }
    }
}
