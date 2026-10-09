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
    /// SNACKY PASS, laid out from the original Resources/liveops/seasonpass/BP_MainUI_View and played as in videos sk2 /
    /// sk2.1: the illustration with the snake on its coins, the "Snacky Pass" ticket with ACTIVATE (ACTIVE! once on), the
    /// red timer, the i and X buttons, and the 20 stages under it, each a row with the free reward on the purple lane
    /// and the premium one on the yellow lane, its number on a purple tag. Open stages are bright, the next one is lighter
    /// with the ticket bar ("0/1"), later ones darker; premium cards carry a lock until the pass is active.
    /// The view opens at the bottom of the list and scrolls up to the current stage (sk2 12.3-13.4 s); the rules show
    /// the first time. A claimable card wears the green CLAIM tag, glows and twinkles; tapping it gives the reward (it
    /// pops up, the card goes pale with a green tick; a chest opens on its own screen).
    /// The layout is the prefab Prefabs/UI/SnackyPassView (from <see cref="BuildLayout"/>, editable by hand [user]); its
    /// Templates/Row is copied for each stage. Everything is designed 1080 wide (the original's width) and scaled to fit.
    /// </summary>
    public class SnackyPassView : MonoBehaviour
    {
        public const float Width = 1080f, FreeWidth = 495.5f, FirstRow = 350f, RowHeight = 285f, HeaderBottom = 487.2f;

        // Lane colours from the video (top, bottom): open, next (in progress), later (locked).
        private static readonly Color[] FreeOpen = { new Color32(150, 32, 250, 255), new Color32(140, 26, 236, 255) };
        private static readonly Color[] FreeNext = { new Color32(209, 100, 255, 255), new Color32(254, 138, 255, 255) };
        private static readonly Color[] FreeLocked = { new Color32(114, 10, 210, 255), new Color32(105, 18, 183, 255) };
        private static readonly Color[] PaidOpen = { new Color32(255, 243, 8, 255), new Color32(255, 213, 7, 255) };
        private static readonly Color[] PaidNext = { new Color32(255, 255, 255, 255), new Color32(255, 246, 145, 255) };
        private static readonly Color[] PaidLocked = { new Color32(255, 202, 12, 255), new Color32(255, 164, 18, 255) };

        private UiSkin _skin;
        private SeasonPass _pass;
        private RectTransform _root, _fit, _content, _viewport, _rowTemplate, _activate, _active;
        private ScrollRect _scroll;
        private TextMeshProUGUI _timer;
        private Transform _clockHand;
        private CanvasGroup _group;
        private GameObject _rulesPrefab, _offerPrefab;
        private bool _busy;
        private float _tick;
        private readonly List<RectTransform> _rows = new List<RectTransform>();
        private readonly List<(RectTransform star, float phase)> _stars = new List<(RectTransform, float)>();

        public static SnackyPassView Open(RectTransform canvas, UiSkin skin, GameObject prefab, GameObject rulesPrefab, GameObject offerPrefab)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, canvas).transform;
                root.name = "SnackyPassView";
            }
            else
            {
                root = Stretch(canvas, "SnackyPassView");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<SnackyPassView>();
            if (view == null) view = root.gameObject.AddComponent<SnackyPassView>();
            view._skin = skin;
            view._pass = SeasonPass.Shared;
            view._rulesPrefab = rulesPrefab;
            view._offerPrefab = offerPrefab;
            view.Bind(root);
            return view;
        }

        private Sprite S(string name) => _skin.Get(name);
        private Material M(string preset) => _skin.EventMaterial(preset);

        /// <summary>A rect hung from the top of its parent: x from the left edge, y down from the top.</summary>
        private static RectTransform Top(Transform parent, string name, float x, float y, float w, float h)
            => Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));

        private static RectTransform Centre(Transform parent, string name, float x, float y, float w, float h)
            => Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));

        // ================================================================== layout (saved as the prefab)

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            var view = root.GetComponent<SnackyPassView>();
            if (view == null) view = root.gameObject.AddComponent<SnackyPassView>();
            view._skin = skin;
            view.Layout(root);
        }

        private void Layout(RectTransform root)
        {
            // Fit: the original's 1080 wide screen, scaled to the width at run time.
            var fit = Rect(root, "Fit", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, 1920f));
            Image(Stretch(fit, "BG"), null, new Color(0.686f, 0.384f, 0.922f), raycast: true);

            // Illustration (888 x 475 at the top) and its coins, the snake on them.
            var illustration = Rect(fit, "Illustration", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -237.7f), new Vector2(888f, 475.4f));
            Image(Rect(illustration, "SP_BG", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-898.1f, -36.15f), new Vector2(1619.65f, 674.6f), new Vector2(0f, 0.5f)), S("S_SeasonPass_BackgroundImg_0"), Color.white);
            Image(Rect(fit, "Snake_Img", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-126.1f, -247.4f), new Vector2(671.4f, 335.7f)), S("S_SeasonPass_SnakeCoins_0"), Color.white).preserveAspect = true;

            // Scroll View: everything under the header (487 from the top).
            var scroll = Rect(fit, "Scroll View", Vector2.zero, Vector2.one, new Vector2(0f, -HeaderBottom * 0.5f), new Vector2(0f, -HeaderBottom));
            var viewport = Stretch(scroll, "Viewport");
            Image(viewport, null, Color.clear, raycast: true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, FirstRow + 19f * RowHeight), new Vector2(0.5f, 1f));
            var rect = scroll.gameObject.AddComponent<ScrollRect>();
            rect.horizontal = false;
            rect.viewport = viewport;
            rect.content = content;
            rect.movementType = ScrollRect.MovementType.Elastic;
            rect.scrollSensitivity = 30f;

            // SP_Frame: the golden line along the header's foot and its shadow.
            var frame = Rect(fit, "SP_Frame", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HeaderBottom + 6f), new Vector2(0f, 20f));
            Image(Rect(frame, "SP_Shadow", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -14f), new Vector2(0f, 26f)), S("SP_Shadow"), new Color(0.315f, 0.034f, 0.034f, 0.35f));
            Image(Stretch(frame, "SP_Frame"), S("S_GoldenLine"), Color.white);

            BadgeAndTimer(fit);
            RoundButton(Rect(fit, "Cancel Button", Vector2.one, Vector2.one, new Vector2(-100f, -120f), new Vector2(150f, 150f)), "S_Icon_Cancel");
            RoundButton(Rect(fit, "Info Button", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -120f), new Vector2(150f, 150f)), "S_Icon_Info");

            var templates = Stretch(root, "Templates");
            RowTemplate(templates);
            templates.gameObject.SetActive(false);
            CentreOnLetters(root);
        }

        private void RoundButton(RectTransform button, string icon)
        {
            var shadow = Image(Centre(button, "shadow", 0f, -14f, 100f, 90f), S("Shadow_Round"), new Color(0f, 0f, 0f, 0.737f), sliced: true, raycast: true);
            shadow.pixelsPerUnitMultiplier = 6.09f;
            Image(Centre(button, "icon", 0f, 0f, 90f, 90f), S(icon), Color.white);
        }

        /// <summary>Badge_and_Timer (411 down): the red timer on the free half, the Snacky Pass ticket with ACTIVATE on the premium half.</summary>
        private void BadgeAndTimer(RectTransform fit)
        {
            var timer = Top(fit, "Timer", FreeWidth * 0.5f, 411f, FreeWidth, 346f);
            var timer2 = Centre(timer, "Timer2", 0f, -31f, 212.3f, 50f);
            timer2.localScale = new Vector3(1.2f, 1.2f, 1f);
            Image(Rect(timer2, "Timer Background", Vector2.zero, Vector2.one, new Vector2(0f, -1f), new Vector2(0f, -2f)), S("S_TimerBackground"), new Color(0.929f, 0.251f, 0.235f), sliced: true).pixelsPerUnitMultiplier = 2.62f;
            var icon = Rect(timer2, "Timer Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(23.06f, 0f), new Vector2(53.4f, 53.4f));
            Image(icon, S("S_Icon_Timer_01_0"), Color.white);
            Image(Rect(icon, "Timer Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.53f), new Vector2(19.8f, 17.9f), new Vector2(1f, 1f)), S("S_Icon_Timer_03_0"), Color.white);
            Image(Centre(icon, "Timer Center", 0f, 0f, 15.1f, 15.1f), S("S_Icon_Timer_02_0"), Color.white);
            Text(Rect(timer2, "Text (TMP)", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(118.6f, -27f), new Vector2(121.3f, 50f)), "7D 00H", _skin.font, M("LuckiestGuy-Regular Red Cartoon 02"), 36f, Color.white);

            var badge = Top(fit, "Badge", Width - 584.55f * 0.5f, 411f, 584.55f, 346f);
            Image(Centre(badge, "Badge_Shdw", 2.13f, 22.6f, 427f, 252.7f), S("S_Img_TicketShdw_0"), new Color(0f, 0f, 0f, 0.659f));
            var ticket = Centre(badge, "Badge", 5.11f, 29f, 412.4f, 237.3f);
            Image(ticket, S("S_Img_Ticket_0"), Color.white);

            var activate = Rect(ticket, "Activate Button", Vector2.zero, Vector2.zero, new Vector2(206.19f, 7f), new Vector2(334.2f, 195.6f));
            Image(Rect(activate, "button Shadow", Vector2.zero, Vector2.one, new Vector2(0f, -20.3f), new Vector2(-54.5f, -67.8f)), S("S_Button_Shadow_0"), new Color(0.191f, 0f, 0f, 0.776f));
            Image(Rect(activate, "button color", Vector2.zero, Vector2.one, new Vector2(0f, -12.16f), new Vector2(-62f, -71.2f)), S("S_Button_WMGreen_0"), Color.white, raycast: true);
            Text(Rect(activate, "Text (TMP) (1)", Vector2.zero, Vector2.one, new Vector2(0.88f, 3.8f), new Vector2(-102.3f, -148.2f)), "Activate", _skin.font, M("LuckiestGuy-Regular Green Cartoon"), 47.3f, Color.white);
            var active = Centre(ticket, "GreenButtonActivated", 0f, -98.46f, 275.2f, 73.7f);
            Image(active, S("S_Button_WMPurple_0"), Color.white);
            Text(Centre(active, "Text (TMP)", 0f, -3.1f, 230.7f, 46.1f), "ACTIVE!", _skin.font, M("LuckiestGuy-Regular Purple Cartoon 03"), 46.1f, Color.white);
            active.gameObject.SetActive(false);

            var title = Centre(badge, "MainTitle", 5.11f, 16f, 344.6f, 161.4f);
            title.localScale = new Vector3(0.9f, 0.9f, 1f);
            TitleWords(title, _skin, -2.21f, 12f, 9.2f);
        }

        /// <summary>"Snacky Pass" in two lines: the brown back and the white face a little above it (Snacky_Txt / Snacky_Txt (1)).</summary>
        public static void TitleWords(RectTransform parent, UiSkin skin, float x, float y, float lift)
        {
            foreach (var (name, dy, preset, colour) in new[] { ("Snacky_Txt", 0f, "LuckiestGuy-Regular Brown", new Color(0.639f, 0.208f, 0f)), ("Snacky_Txt (1)", lift, "LuckiestGuy-Regular Brown Title2", Color.white) })
            {
                var text = Text(Centre(parent, name, x, y + dy, 398.8f, 160f), "Snacky\nPass", skin.font, skin.EventMaterial(preset), 90f, colour);
                text.lineSpacing = -28f; // two tight lines, SNACKY over PASS (video)
            }
        }

        /// <summary>One stage (BP_MilestoneUI, 1080 x 285): the two lanes and their cards, the lines, the stage tag, the ticket bar.</summary>
        private void RowTemplate(RectTransform templates)
        {
            var row = Rect(templates, "Row", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, RowHeight), new Vector2(0.5f, 1f));
            Lane(row, "Free_Lane", 0f, FreeWidth, "S_PurpleButtonPass_0", false);
            Lane(row, "Premium_Lane", FreeWidth, Width - FreeWidth, "S_GoldenButtonPass_0", true);

            // SP_Frame_Top: the golden line along the row's top edge, a warm shadow under it.
            var top = Rect(row, "SP_Frame_Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -10f), new Vector2(0f, 20f));
            Image(Rect(top, "SP_Shadow", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -12f), new Vector2(0f, 24f)), S("SP_Shadow"), new Color(0.463f, 0.082f, 0f, 0.45f));
            Image(Stretch(top, "SP_Frame"), S("S_GoldenLine"), Color.white);

            // Milestone_Level: the purple tag with the stage number, on the lanes' border.
            var tag = Rect(row, "Milestone_Level", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-46.4f, -29.76f), new Vector2(115.8f, 59.5f));
            Image(tag, S("S_Icon_LevelNumber_Purple_0"), Color.white);
            Text(Centre(tag, "Text (TMP)", 0.08f, -2.1f, 98f, 40.6f), "1", _skin.font, M("LuckiestGuy-Regular SDF 2 - purple cartoon 2"), 42f, Color.white);

            // LoadingBar: the ticket bar on the free lane's edge, with its "0/1" bubble.
            var bar = Rect(row, "LoadingBar", new Vector2(0.4506f, 1f), new Vector2(0.4506f, 1f), new Vector2(-0.7f, -131.5f), new Vector2(266f, 307f));
            var back = Centre(bar, "BackgroundShadow", 1.07f, -18.8f, 73.3f, 163.3f);
            Image(back, S("ProgressBar_Back_0"), Color.white);
            var well = Rect(back, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-26f, -26f));
            Image(well, _skin.bluePlate, new Color(0.655f, 0.49f, 0.294f), sliced: true).pixelsPerUnitMultiplier = 4f;
            var fill = Rect(well, "Fill", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            Image(fill, _skin.bluePlate, new Color(1f, 0.78f, 0.08f), sliced: true).pixelsPerUnitMultiplier = 4f;
            var bubble = Centre(bar, "SpeechBubble", 86f, 60f, 140f, 62f);
            Image(bubble, S("S_Icon_SpeechBubble_Purple_0"), Color.white);
            var ticket = Centre(bubble, "Small_Ticket", -38f, 6f, 48f, 30f);
            ticket.localRotation = Quaternion.Euler(0f, 0f, 15.4f);
            Image(ticket, S("S_Icon_GoldenTicket_0"), Color.white).preserveAspect = true;
            Text(Centre(bubble, "Text (TMP)", 22f, 4f, 80f, 40f), "0/1", _skin.font, M("LuckiestGuy-Regular SDF 2 - purple cartoon 2"), 32f, Color.white);
            bar.gameObject.SetActive(false);
        }

        /// <summary>A lane: its colour, the glow behind a claimable card, the card (frame, icon, amount), CLAIM, the tick and the lock.</summary>
        private void Lane(RectTransform row, string name, float x, float width, string frame, bool premium)
        {
            var lane = Rect(row, name, Vector2.zero, new Vector2(0f, 1f), new Vector2(x + width * 0.5f, 0f), new Vector2(width, 0f));
            UiGradient.Add(Image(lane, null, Color.white), Color.white, Color.white);
            var glow = Centre(lane, "SP_VFX", premium ? 0f : 0f, 0f, 266f, 307f);
            Image(Centre(glow, "SP_Glow_1", 0f, 0f, premium ? 489.5f : 431.6f, premium ? 307f : 253.5f), S("S_Glow"), premium ? new Color(1f, 0.996f, 0.137f, 0.467f) : new Color(1f, 0.557f, 0.906f, 0.27f));
            Image(Centre(glow, "SP_Glow_2", 0f, 0f, premium ? 303.9f : 371.5f, premium ? 231.8f : 293.7f), S("S_Glow"), premium ? new Color(1f, 0.996f, 0.137f, 1f) : new Color(0.827f, 0.592f, 1f, 1f));
            glow.gameObject.SetActive(false);

            // Visual_Reward_Pass (235 x 235): the octagonal card of the lane's colour with its white inlay.
            var card = Centre(lane, "Visual_Reward_Pass", 0f, -6f, 235f, 235f);
            Image(Rect(card, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-15f, -8f)), S(frame), Color.white, sliced: true, raycast: true).pixelsPerUnitMultiplier = 2f;
            var icon = Centre(card, "Icon", 0f, 12f, 150f, 150f);
            Image(icon, null, Color.white).preserveAspect = true;
            var amount = Text(Rect(card, "Amount", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 52f), new Vector2(-40f, 60f)), "X1", _skin.font, M("LuckiestGuy-Regular SDF 2 - purple cartoon 2"), 44f, Color.white);
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 26f;
            amount.fontSizeMax = 44f;
            var claimed = Stretch(card, "Claimed");
            Image(Rect(claimed, "Fade", Vector2.zero, Vector2.one, new Vector2(0f, 6f), new Vector2(-62f, -82f)), _skin.bluePlate, new Color(1f, 1f, 1f, 0.55f), sliced: true).pixelsPerUnitMultiplier = 3f;
            Image(Centre(claimed, "GreenCheck_Img", 88f, -72f, 82f, 82f), _skin.checkMark, Color.white).preserveAspect = true;
            claimed.gameObject.SetActive(false);

            // Green Button (0, 84.8): the CLAIM tag over the card.
            var claim = Centre(card, "Green Button", 0f, 84.8f, 180.75f, 80.2f);
            Image(Rect(claim, "buttonShdw", Vector2.zero, Vector2.one, new Vector2(0f, 1.6f), new Vector2(-18.5f, -17f)), S("S_Button_WMGreen_0"), new Color(0f, 0f, 0f, 0.475f));
            Image(Rect(claim, "button color", Vector2.zero, Vector2.one, new Vector2(0f, 4.35f), new Vector2(-18.5f, -17f)), S("S_Button_WMGreen_0"), Color.white, raycast: true);
            Text(Centre(claim, "Text (TMP)", 0f, 9.7f, 134f, 36f), "Claim", _skin.font, M("LuckiestGuy-Regular Green Cartoon"), 35.95f, Color.white);
            claim.gameObject.SetActive(false);

            if (premium)
            {
                // The lock at the card's bottom right (Orange Button / Generic Icon).
                Image(Centre(lane, "Lock", 123f - 1.75f, -73f, 96f, 96f), S("S_Icon_Locked"), Color.white).preserveAspect = true;
            }
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root)
        {
            _root = root;
            _fit = (RectTransform)root.Find("Fit");
            _group = root.GetComponent<CanvasGroup>();
            if (_group == null) _group = root.gameObject.AddComponent<CanvasGroup>();
            _scroll = _fit.Find("Scroll View").GetComponent<ScrollRect>();
            _viewport = _scroll.viewport;
            _content = _scroll.content;
            _rowTemplate = (RectTransform)root.Find("Templates/Row");
            _timer = _fit.Find("Timer/Timer2/Text (TMP)").GetComponent<TextMeshProUGUI>();
            _clockHand = _fit.Find("Timer/Timer2/Timer Icon/Timer Arrow");
            _activate = (RectTransform)_fit.Find("Badge/Badge/Activate Button");
            _active = (RectTransform)_fit.Find("Badge/Badge/GreenButtonActivated");

            AddClick(_fit.Find("Cancel Button/shadow"), Close);
            AddClick(_fit.Find("Info Button/shadow"), () => SnackyPassRules.Open(_root, _skin, _rulesPrefab));
            AddClick(_activate.Find("button color"), OpenOffer);
            UiShiny.Widget(_activate.Find("button color").GetComponent<Image>());
            FitWidth();
            BuildRows();
            ShowBadge();
            CentreOnLetters(root);
            Pressable(root);
            _pass.Changed += Refresh;
            StartCoroutine(OpenFlow());
        }

        private void OnDestroy() { if (_pass != null) _pass.Changed -= Refresh; }

        private static void AddClick(Transform target, System.Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        /// <summary>The 1080 wide design scaled to the screen's width, as tall as the screen.</summary>
        private void FitWidth()
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            var size = _root.rect.size;
            if (size.x <= 0f) return;
            float k = size.x / Width;
            _fit.localScale = new Vector3(k, k, 1f);
            _fit.sizeDelta = new Vector2(Width, size.y / k);
        }

        private void ShowBadge()
        {
            bool premium = _pass.Premium;
            _activate.gameObject.SetActive(!premium);
            _active.gameObject.SetActive(premium);
        }

        private void Refresh()
        {
            if (_busy) return;
            ShowBadge();
            for (int i = 0; i < _rows.Count; i++) BindRow(_rows[i], i + 1);
        }

        private static float RowTop(int stage) => stage == 1 ? 0f : FirstRow + (stage - 2) * RowHeight;

        private void BuildRows()
        {
            foreach (var row in _rows) Destroy(row.gameObject);
            _rows.Clear();
            _content.sizeDelta = new Vector2(Width, FirstRow + (SeasonPass.Stages - 1) * RowHeight);
            for (int stage = 1; stage <= SeasonPass.Stages; stage++)
            {
                var row = (RectTransform)Instantiate(_rowTemplate, _content);
                row.name = "Stage " + stage;
                row.gameObject.SetActive(true);
                row.anchoredPosition = new Vector2(0f, -RowTop(stage));
                row.sizeDelta = new Vector2(Width, stage == 1 ? FirstRow : RowHeight);
                _rows.Add(row);
                int s = stage;
                AddClick(row.Find("Free_Lane/Visual_Reward_Pass/Frame"), () => OnCard(s, false));
                AddClick(row.Find("Free_Lane/Visual_Reward_Pass/Green Button/button color"), () => OnCard(s, false));
                AddClick(row.Find("Premium_Lane/Visual_Reward_Pass/Frame"), () => OnCard(s, true));
                AddClick(row.Find("Premium_Lane/Visual_Reward_Pass/Green Button/button color"), () => OnCard(s, true));
                BindRow(row, stage);
            }
            // Stage 1's row is 65 taller at the top: its content sits in its lower 285 like every other row.
            var first = _rows[0];
            foreach (var lane in new[] { "Free_Lane", "Premium_Lane" })
            {
                var card = (RectTransform)first.Find(lane + "/Visual_Reward_Pass");
                card.anchoredPosition += new Vector2(0f, -(FirstRow - RowHeight) * 0.5f + 10f);
                var glow = (RectTransform)first.Find(lane + "/SP_VFX");
                glow.anchoredPosition = card.anchoredPosition;
                var lockIcon = first.Find(lane + "/Lock") as RectTransform;
                if (lockIcon != null) lockIcon.anchoredPosition += new Vector2(0f, -(FirstRow - RowHeight) * 0.5f + 10f);
            }
        }

        private void BindRow(RectTransform row, int stage)
        {
            int open = _pass.Stage;
            bool unlocked = stage <= open, next = stage == open + 1 && open < SeasonPass.Stages;
            Lane(row.Find("Free_Lane"), stage, false, unlocked, unlocked ? FreeOpen : next ? FreeNext : FreeLocked);
            Lane(row.Find("Premium_Lane"), stage, true, unlocked, unlocked ? PaidOpen : next ? PaidNext : PaidLocked);
            row.Find("Milestone_Level/Text (TMP)").GetComponent<TextMeshProUGUI>().text = stage.ToString();

            var bar = row.Find("LoadingBar");
            bar.gameObject.SetActive(next);
            if (next)
            {
                var (have, need) = _pass.Progress;
                var fill = (RectTransform)bar.Find("BackgroundShadow/Background/Fill");
                fill.anchorMax = new Vector2(1f, need > 0 ? Mathf.Clamp01((float)have / need) : 0f);
                fill.gameObject.SetActive(have > 0);
                bar.Find("SpeechBubble/Text (TMP)").GetComponent<TextMeshProUGUI>().text = have + "/" + need;
            }
        }

        private void Lane(Transform lane, int stage, bool premium, bool unlocked, Color[] colours)
        {
            lane.GetComponent<UiGradient>().Set(colours[0], colours[1]);
            var reward = SeasonPass.RewardAt(stage, premium);
            var (icon, label) = Look(reward);
            var card = lane.Find("Visual_Reward_Pass");
            var image = card.Find("Icon").GetComponent<Image>();
            image.sprite = icon;
            var iconRect = image.rectTransform;
            bool big = reward.chest > 0 || reward.livesPerk;
            iconRect.sizeDelta = Vector2.one * (big ? 175f : 150f);
            iconRect.anchoredPosition = new Vector2(0f, big ? 2f : 12f);
            card.Find("Amount").GetComponent<TextMeshProUGUI>().text = label;
            card.Find("Amount").gameObject.SetActive(label.Length > 0);

            bool claimed = _pass.Claimed(stage, premium);
            bool claimable = _pass.Claimable(stage, premium);
            card.Find("Claimed").gameObject.SetActive(claimed);
            var claim = card.Find("Green Button");
            claim.gameObject.SetActive(claimable);
            lane.Find("SP_VFX").gameObject.SetActive(claimable);
            if (claimable) UiShiny.Row(claim.Find("button color").GetComponent<Image>());
            var lockIcon = lane.Find("Lock");
            if (lockIcon != null) lockIcon.gameObject.SetActive(!_pass.Premium);
            Sparkles(card, claimable);
        }

        /// <summary>The picture and words of a reward: "25", "X1", "15M", "1H"; chests and the 8 lives perk have none.</summary>
        private (Sprite icon, string label) Look(PassReward reward)
        {
            if (reward.livesPerk) return (S("S_Icon_PremiumReward"), "");
            if (reward.chest > 0) return (S("S_Icon_Chests2_" + (reward.chest - 1)), "");
            var r = reward.reward;
            if (r.coins > 0) return (S("Coin_Icon_02_0"), r.coins.ToString());
            if (r.infiniteMinutes > 0) return (S("S_Icon_Heart_Infinit_Lives"), r.infiniteMinutes % 60 == 0 ? r.infiniteMinutes / 60 + "H" : r.infiniteMinutes + "M");
            string[] icons = { "S_Icon_Booster_Teleporter", "S_Icon_Booster_FruitCanon", "S_Icon_Booster_Undo", "S_Icon_Booster_Magnet" };
            var boosters = r.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) return (S(icons[b]), "X" + boosters[b]);
            return (null, "");
        }

        /// <summary>Little stars twinkling round a claimable card (sk2: sparkles on the stage 1 card).</summary>
        private void Sparkles(Transform card, bool on)
        {
            var holder = card.Find("Sparkles");
            if (!on) { if (holder != null) holder.gameObject.SetActive(false); return; }
            if (holder == null)
            {
                holder = Stretch(card, "Sparkles");
                holder.SetAsFirstSibling();
                Vector2[] spots = { new Vector2(-135f, 45f), new Vector2(-120f, -55f), new Vector2(130f, 30f), new Vector2(-30f, -128f), new Vector2(118f, -96f) };
                for (int i = 0; i < spots.Length; i++)
                {
                    var star = Centre(holder, "Star", spots[i].x, spots[i].y, 34f, 34f);
                    Image(star, S("S_FX_Sparkle_02_0"), Color.white).preserveAspect = true;
                    _stars.Add((star, i * 1.3f));
                }
            }
            holder.gameObject.SetActive(true);
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            foreach (var (star, phase) in _stars)
            {
                if (star == null || !star.gameObject.activeInHierarchy) continue;
                float k = Mathf.Max(0f, Mathf.Sin(time * 2.4f + phase));
                star.localScale = Vector3.one * k;
                star.localRotation = Quaternion.Euler(0f, 0f, time * 40f + phase * 30f);
            }
            if (_clockHand != null)
            {
                int steps = Mathf.FloorToInt(time);
                _clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(Mathf.Clamp01((time - steps) / 0.2f))));
            }
            if (_pass == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = _pass.Left;
            _timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes:00}M" : $"{left.Minutes}M {left.Seconds:00}S";
        }

        // ------------------------------------------------------------------ opening

        /// <summary>
        /// sk2 12.3-13.4 s: the view comes in at the bottom of the list (stage 20) and glides up to the current stage
        /// in about a second, slowing at the end; the rules follow the first time.
        /// </summary>
        private IEnumerator OpenFlow()
        {
            _busy = true;
            _scroll.enabled = false;
            float viewHeight = _viewport.rect.height;
            float bottom = Mathf.Max(0f, _content.sizeDelta.y - viewHeight);
            int focus = Mathf.Max(1, FirstClaimable());
            float target = Mathf.Clamp(RowTop(focus) - (focus == 1 ? 0f : RowHeight * 0.3f), 0f, bottom);
            System.Action<float> place = y => _content.anchoredPosition = new Vector2(0f, y);
            place(bottom);
            _group.alpha = 0f;
            yield return Tween.Run(0.2f, Tween.Linear, t => _group.alpha = t, true);
            yield return Tween.Run(1.0f, Tween.InOutCubic, t => place(Mathf.Lerp(bottom, target, t)), true);
            _scroll.enabled = true;
            _scroll.velocity = Vector2.zero;
            _busy = false;
            if (!SnackyPassRules.Seen) SnackyPassRules.Open(_root, _skin, _rulesPrefab);
        }

        /// <summary>The first stage with something to claim, else the current one.</summary>
        private int FirstClaimable()
        {
            for (int stage = 1; stage <= SeasonPass.Stages; stage++)
                if (_pass.Claimable(stage, false) || _pass.Claimable(stage, true)) return stage;
            return _pass.Stage;
        }

        // ------------------------------------------------------------------ claiming

        private void OnCard(int stage, bool premium)
        {
            if (_busy) return;
            if (premium && !_pass.Premium) { OpenOffer(); return; }
            if (!_pass.Claimable(stage, premium)) { Shake(_rows[stage - 1].Find((premium ? "Premium_Lane" : "Free_Lane") + "/Visual_Reward_Pass")); return; }
            StartCoroutine(ClaimFlow(stage, premium));
        }

        private void Shake(Transform card) => StartCoroutine(Tween.Run(0.3f, Tween.Linear, t => { if (card != null) card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 5f * (1f - t)); }, true));

        /// <summary>
        /// sk2 26-28 s: the CLAIM tag goes, the reward's copy pops up out of the card and floats off with its amount, the card
        /// turns pale and the green tick pops on it. A chest opens on its own screen first.
        /// </summary>
        private IEnumerator ClaimFlow(int stage, bool premium)
        {
            _busy = true;
            var lane = _rows[stage - 1].Find(premium ? "Premium_Lane" : "Free_Lane");
            var card = (RectTransform)lane.Find("Visual_Reward_Pass");
            var reward = SeasonPass.RewardAt(stage, premium);
            card.Find("Green Button").gameObject.SetActive(false);
            lane.Find("SP_VFX").gameObject.SetActive(false);
            Sparkles(card, false);
            _pass.Claim(stage, premium);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();

            if (reward.chest > 0)
            {
                bool done = false;
                var (icon, _) = Look(reward);
                WeeklyChestReward.OpenChest(_root, _skin, header => SnackyPassRules.Header(header, _skin), icon, ChestContents(reward.reward), card.position, () => done = true);
                while (!done) yield return null;
            }
            else
            {
                // The icon's copy rises and fades, a little bigger, with its amount.
                var icon = (RectTransform)card.Find("Icon");
                var copy = (RectTransform)Instantiate(icon, _root);
                copy.position = icon.position;
                copy.localScale = Vector3.one * icon.lossyScale.x / _root.lossyScale.x;
                var (_, label) = Look(reward);
                if (label.Length > 0)
                {
                    var words = Text(Rect(copy, "Amount", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(240f, 70f)), (reward.reward.coins > 0 ? "+" : "") + label, _skin.font, M("LuckiestGuy-Regular SDF 2 - purple cartoon 2"), 56f, Color.white);
                    words.alignment = TextAlignmentOptions.Midline;
                }
                var group = copy.gameObject.AddComponent<CanvasGroup>();
                Vector3 start = copy.position, scale = copy.localScale;
                float unit = _root.lossyScale.y;
                StartCoroutine(Tween.Run(0.9f, Tween.Linear, t =>
                {
                    if (copy == null) return;
                    copy.localScale = scale * (t < 0.15f ? Mathf.Lerp(1f, 1.35f, Tween.OutQuad(t / 0.15f)) : 1.35f);
                    copy.position = start + Vector3.up * 170f * unit * Tween.OutQuad(t);
                    group.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                }, true));
                yield return Tween.Run(0.25f, Tween.Linear, _ => { }, true);
            }

            // The card goes pale, the tick pops.
            var claimed = card.Find("Claimed");
            claimed.gameObject.SetActive(true);
            var tick = claimed.Find("GreenCheck_Img");
            yield return Tween.Run(0.3f, Tween.OutBack(2f), t => tick.localScale = Vector3.one * t, true);
            _busy = false;
            Refresh();
        }

        private List<(Sprite icon, string amount)> ChestContents(MissionReward r)
        {
            var list = new List<(Sprite, string)>();
            if (r.coins > 0) list.Add((S("Coin_Icon_02_0"), r.coins.ToString()));
            string[] icons = { "S_Icon_Booster_Teleporter", "S_Icon_Booster_FruitCanon", "S_Icon_Booster_Undo", "S_Icon_Booster_Magnet" };
            var boosters = r.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) list.Add((S(icons[b]), "x" + boosters[b]));
            if (r.infiniteMinutes > 0) list.Add((S("S_Icon_Heart_Infinit_Lives"), r.infiniteMinutes % 60 == 0 ? r.infiniteMinutes / 60 + "H" : r.infiniteMinutes + "M"));
            return list;
        }

        // ------------------------------------------------------------------ the pass

        private void OpenOffer()
        {
            if (_busy || _pass.Premium) return;
            SnackyPassOffer.Open(_root, _skin, _offerPrefab, OnActivated);
        }

        /// <summary>The pass is on: ACTIVE! on the ticket, the locks go and the premium cards light up (sk2.1 end).</summary>
        private void OnActivated()
        {
            _pass.Activate();
            ShowBadge();
            StartCoroutine(Toast("Snacky Pass is active!", "Maximum amount of lives has increased to 8!\nPremium rewards are unlocked!")); // LocData season_pass_active_lives / active_rewards
            StartCoroutine(Tween.Punch(0.12f, t => _active.localScale = Vector3.one * (1f + 0.25f * t)));
            foreach (var row in _rows)
            {
                var lockIcon = row.Find("Premium_Lane/Lock");
                if (lockIcon != null && lockIcon.gameObject.activeSelf)
                {
                    var l = lockIcon;
                    StartCoroutine(Tween.Run(0.3f, Tween.InBack(1.7f), t => { if (l != null) l.localScale = Vector3.one * (1f - t); }, true));
                }
            }
            Refresh();
        }

        /// <summary>A two-line message in the middle that rises a little and fades (the purchase popup's words; nothing is bought here).</summary>
        private IEnumerator Toast(string title, string line)
        {
            var box = Rect(_root, "Toast", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 260f));
            var back = Image(Stretch(box, "Back"), _skin.bluePlate, new Color(0.24f, 0.07f, 0.45f, 0.92f), sliced: true);
            back.pixelsPerUnitMultiplier = 2f;
            Text(Rect(box, "Title", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -60f), new Vector2(-40f, 70f)), title, _skin.font, M("LuckiestGuy-Regular Brown Cartoon"), 54f, new Color(1f, 0.737f, 0.086f)).alignment = TextAlignmentOptions.Midline;
            var words = Text(Rect(box, "Line", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 80f), new Vector2(-60f, 120f)), line, _skin.font, _skin.plainMaterial, 36f, Color.white);
            words.enableWordWrapping = true;
            words.alignment = TextAlignmentOptions.Midline;
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            yield return Tween.Run(2.4f, Tween.Linear, t =>
            {
                if (box == null) return;
                box.localScale = Vector3.one * (t < 0.12f ? Tween.OutBack(2f)(t / 0.12f) : 1f);
                box.anchoredPosition = new Vector2(0f, 60f * t);
                group.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
            }, true);
            if (box != null) Destroy(box.gameObject);
        }

        private void Close()
        {
            if (_busy) return;
            _busy = true;
            StartCoroutine(Tween.Run(0.2f, Tween.Linear, t => { _group.alpha = 1f - t; if (t >= 1f) Destroy(gameObject); }, true));
        }
    }
}
