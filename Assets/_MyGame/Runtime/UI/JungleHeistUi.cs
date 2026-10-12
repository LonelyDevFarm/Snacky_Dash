using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Parts shared by the Jungle Heist screens (original Resources/Popup_Lava_Quest_*): the "JUNGLE / HEIST" title on its
    /// wooden banner with ropes, the two leaves over it, the brown popup frame with its inlay, the green button and the
    /// red timer with its clock.
    /// </summary>
    public static class JungleHeistUi
    {
        public static RectTransform Centre(Transform parent, string name, float x, float y, float w, float h)
            => Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));

        /// <summary>Header: S_Bg_Offer_Banner_Lava_Quest on two ropes, "JUNGLE" pale blue and "HEIST" orange, each a back and a face, tilted 3 degrees.</summary>
        public static RectTransform Title(RectTransform parent, UiSkin skin, Vector2 at, Vector2 size)
        {
            var header = Centre(parent, "Header", at.x, at.y, size.x, size.y);
            Image(header, skin.Get("S_Bg_Offer_Banner_Lava_Quest_0"), Color.white);
            var right = Rect(header, "Rope_Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-82.7f, 151.5f), new Vector2(72f, 146.5f));
            Image(right, skin.Get("S_Title_Accessory_LavaQuest (2)_0"), Color.white);
            var left = Rect(header, "Rope_Left", Vector2.zero, new Vector2(0f, 1f), new Vector2(88.2f, 145.7f), new Vector2(72f, 158.1f));
            left.localScale = new Vector3(-1f, 1f, 1f);
            Image(left, skin.Get("S_Title_Accessory_LavaQuest (2)_0"), Color.white);
            Word(header, skin, "Text (TMP) - Jungle", "JUNGLE", new Vector2(2.1f, 51.6f), new Vector2(-228.8f, 37f), 3.9f, "LuckiestGuy-Regular LavaQuest blue", 100f, new Color(0.761f, 0.898f, 1f), new Color(0.866f, 0.973f, 1f));
            Word(header, skin, "Text (TMP) - Heist", "HEIST", new Vector2(14.8f, -26.3f), new Vector2(-248.2f, -14.5f), 3.4f, "LuckiestGuy-Regular LavaQuest orange", 95f, Color.white, Color.white);
            return header;
        }

        private static void Word(RectTransform header, UiSkin skin, string name, string word, Vector2 at, Vector2 size, float lift, string preset, float fontSize, Color back, Color face)
        {
            var rect = Rect(header, name, Vector2.zero, Vector2.one, at, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, 3f);
            Text(rect, word, skin.font, skin.EventMaterial(preset), fontSize, back);
            Text(Rect(rect, "Face", Vector2.zero, Vector2.one, new Vector2(0f, lift), Vector2.zero), word, skin.font, skin.EventMaterial(preset), fontSize * 0.92f, face);
        }

        /// <summary>Title_Accessory: the big leaves either side of the title (S_Title_Leaf_LavaQuest, the left one mirrored).</summary>
        public static void Leaves(RectTransform parent, UiSkin skin, float y, float scale = 1f)
        {
            var accessory = Rect(parent, "Title_Accessory", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, y), new Vector2(46.6f, 226f));
            accessory.localScale = Vector3.one * scale;
            Image(Rect(accessory, "Leaf_Right", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(135f, -74.3f), new Vector2(315f, 330f)), skin.Get("S_Title_Leaf_LavaQuest (2)_0"), Color.white);
            var left = Rect(accessory, "Leaf_Left", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-135f, -70f), new Vector2(315f, 330f));
            left.localScale = new Vector3(-1f, 1f, 1f);
            Image(left, skin.Get("S_Title_Leaf_LavaQuest (2)_0"), Color.white);
        }

        /// <summary>
        /// The black full-screen popups (Popup_Lava_Quest_Rewards / _PurchaseOverlay): black 0.98 on the root, Content a
        /// VerticalLayoutGroup (padding 262 / 377, rows stretched) whose first row is the title (asks 36.7): the banner (0.9)
        /// with its leaves. The purple "Active!" tag behind the banner is left out. Returns Content for the other rows.
        /// </summary>
        public static RectTransform BlackRows(RectTransform root, UiSkin skin)
        {
            Image(root, null, new Color(0f, 0f, 0f, 0.98f), raycast: true);
            var content = Stretch(root, "Content");
            var rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.padding = new RectOffset(0, 0, 262, 377);
            rows.childControlWidth = rows.childControlHeight = true;
            rows.childForceExpandWidth = rows.childForceExpandHeight = true;
            var title = Stretch(content, "Title");
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36.7f;
            var header = Centre(title, "Header", 0f, -6f, 407.9f, 262.4f);
            header.localScale = Vector3.one * 0.9f;
            Leaves(header, skin, 74.1f);
            ((RectTransform)header.Find("Title_Accessory")).sizeDelta = new Vector2(472.6f, 226f);
            var holder = Rect(header, "Header_Holder", new Vector2(0f, 0.898f), new Vector2(1f, 0.9964f), new Vector2(1.75f, 10.72f), new Vector2(395.6f, 110.9f));
            holder.localScale = Vector3.one * 1.1f;
            Title(holder, skin, new Vector2(0f, -38f), new Vector2(639f, 193.8f)).localScale = Vector3.one * 1.0714f;
            return content;
        }

        /// <summary>The grand prize chest (S_GrandPrize_LavaQuest) with "Grand Prize 10 000" on its sign.</summary>
        public static RectTransform GrandPrize(RectTransform parent, UiSkin skin, Vector2 size, Vector2 textAt, Vector2 textSize, float fontSize)
        {
            var icons = Centre(parent, "Icons", 0f, 0f, size.x, size.y);
            Image(icons, skin.Get("S_GrandPrize_LavaQuest_0"), Color.white).preserveAspect = true;
            var text = Text(Centre(icons, "Text (TMP)", textAt.x, textAt.y, textSize.x, textSize.y), "Grand Prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular LavaQuest Counter"), fontSize, Color.white);
            text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 5.8f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            return icons;
        }

        /// <summary>The brown popup (834 x 1300): frame, a mask for the picture, the inlay, the leaves, the title and the close button.</summary>
        public static RectTransform Frame(RectTransform root, UiSkin skin, out RectTransform mask)
        {
            Image(Stretch(root, "Overlay"), null, new Color(0f, 0f, 0f, 0.957f), raycast: true);
            var popup = Centre(root, "Popup_Container", 0f, 0f, 834f, 1300f);
            Leaves(popup, skin, 93f);
            var background = Stretch(popup, "Background");
            Image(background, skin.Get("S_Pop-up_Lava_Quest_Frame_Brown _0"), Color.white, sliced: true);
            mask = Rect(background, "Image_Mask", Vector2.zero, Vector2.one, new Vector2(0f, 11.5f), new Vector2(-70f, -93f));
            Image(mask, null, Color.black);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Title(popup, skin, new Vector2(-0.66f, 630.3f), new Vector2(639f, 193.8f));
            var close = Rect(popup, "Close_Button", Vector2.one, Vector2.one, new Vector2(-20.3f, -24.4f), new Vector2(95.4f, 95.4f));
            Image(close, skin.Get("S_Button_Close_Brown_0"), Color.white, raycast: true);
            var shadow = close.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.165f);
            shadow.effectDistance = new Vector2(0f, -5f);
            return popup;
        }

        /// <summary>Inlay over the picture (added after the picture so it frames it).</summary>
        public static void Inlay(RectTransform popup, UiSkin skin)
        {
            var background = popup.Find("Background");
            Image(Rect(background, "Inlay", Vector2.zero, Vector2.one, new Vector2(0f, 12f), new Vector2(-56f, -80f)), skin.Get("S_Pop-up_Frame_Inlay_Brown_0"), new Color(1f, 0.843f, 0.54f), sliced: true);
        }

        /// <summary>PlayButton: the dark under-plate and the green button (S_Button_Sheet layers) with its label, at (0, -474).</summary>
        public static RectTransform GreenButton(RectTransform root, UiSkin skin, string label)
        {
            var play = Centre(root, "PlayButton", 0f, -24f, 100f, 100f);
            var under = Centre(play, "View_Button_Play_Under", 0f, -477.5f, 414.4f, 131.8f);
            Image(under, skin.Get("S_Button_Sheet_3"), new Color(0.14f, 0.07f, 0f, 0.863f));
            var view = Centre(play, "View_Button_Play_Base", 0f, -474.3f, 500f, 200f);
            Image(Centre(view, "Base", 0f, 3.18f, 402.9f, 128.7f), skin.Get("S_Button_Sheet_3"), new Color(0.224f, 0.624f, 0f));
            Image(Centre(view, "Front", 0f, 10.5f, 399.2f, 111.6f), skin.Get("S_Button_Sheet_2"), new Color(0.31f, 0.804f, 0.016f), raycast: true);
            Image(Centre(view, "Highlight", 0f, -34.75f, 389.1f, 21.1f), skin.Get("S_Button_Sheet_1"), new Color(0.471f, 0.94f, 0.14f));
            Image(Centre(view, "Outline", 0f, 3.18f, 402.9f, 128.7f), skin.Get("S_Button_Sheet_0"), new Color(0.176f, 0.529f, 0f));
            Text(Centre(view, "Text_Continu (TMP)", 0f, 7.2f, 355.3f, 89.8f), label, skin.font, skin.EventMaterial("LuckiestGuy-Regular Green Dark Cartoon 3"), 63f, Color.white);
            return view;
        }

        /// <summary>The red timer banner with the clock on its left (Timer_Banner / Timer_Clock).</summary>
        public static TextMeshProUGUI Timer(RectTransform parent, UiSkin skin, Vector2 at, float scale, out Transform hand)
        {
            var banner = Centre(parent, "Timer_Banner", at.x, at.y, 200f, 50f);
            banner.localScale = Vector3.one * scale;
            Image(Stretch(banner, "TimerBackground"), skin.Get("S_TimerBackground"), new Color(1f, 0.286f, 0.271f), sliced: true).pixelsPerUnitMultiplier = 3.78f;
            var text = Text(Rect(banner, "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-32f, 0f)), "1D 00H", skin.font, skin.EventMaterial("LuckiestGuy-Regular Red Cartoon 02"), 36f, Color.white);
            var clock = Rect(banner, "Timer_Clock", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-2f, 2f), new Vector2(71.8f, 59.8f));
            clock.localScale = Vector3.one * 0.85f;
            Image(clock, skin.Get("S_Bg_Clock_0"), Color.white);
            var arrow = Rect(clock, "TimerArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.53f), new Vector2(19.8f, 17.9f), new Vector2(1f, 1f));
            Image(arrow, skin.Get("S_Icon_Timer_03_0"), Color.white);
            Image(Centre(clock, "TimerCenter", 0f, 0f, 15.1f, 15.1f), skin.Get("S_Icon_Timer_02_0"), Color.white);
            hand = arrow;
            return text;
        }

        /// <summary>
        /// View_LavaQuest_Avatar: a player's picture (TXT_Avatars_Icons_0..9, 81 x 79.6) in its frame (S_Profile_Frame, 100,
        /// sliced; the player's own S_Profile_Frame_Player).
        /// </summary>
        public static RectTransform Avatar(Transform parent, UiSkin skin, string name, Vector2 at, float scale, int icon, bool player)
        {
            var avatar = Centre(parent, name, at.x, at.y, 100f, 100f);
            avatar.localScale = Vector3.one * scale;
            Image(Centre(avatar, "Avatar", 0.28f, 0.34f, 81f, 79.6f), skin.Get("TXT_Avatars_Icons_" + Mathf.Abs(icon % 10)), Color.white);
            Image(Stretch(avatar, "Frame"), skin.Get(player ? "S_Profile_Frame_Player_0" : "S_Profile_Frame_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 3f;
            return avatar;
        }

        /// <summary>"1D 04H", "5H 12M", "12M 30S".</summary>
        public static string Clock(System.TimeSpan left) =>
            left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes:00}M" : $"{left.Minutes}M {left.Seconds:00}S";

        /// <summary>The clock hand ticks a quarter turn each second (as on the other timers).</summary>
        public static void Tick(Transform hand)
        {
            if (hand == null) return;
            float time = Time.unscaledTime;
            int steps = Mathf.FloorToInt(time);
            hand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + SnackDash.Presentation.Tween.OutQuad(Mathf.Clamp01((time - steps) / 0.2f))));
        }
    }
}
