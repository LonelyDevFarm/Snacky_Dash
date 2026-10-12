using System;
using SnackDash.Audio;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The settings screen, shared by Home and Game (original SettingsPopup, laid out as the video
    /// shows it): orange zigzag header with SETTINGS and an X, Music / Haptics / Sound switches (green
    /// with the Enabled icon, grey with the Disabled one), the language row (English only), HELP &amp;
    /// SUPPORT, BACK TO HOME in a level, TERMS &amp; PRIVACY. Help, Terms and the language arrows are for
    /// show [user]. BACK TO HOME asks "ARE YOU SURE?" with "YOU WILL LOSE 1 LIFE!"
    /// (LivesPopup.ConfirmLeave).
    /// </summary>
    public static class SettingsPopup
    {
        /// <param name="backToHome">Null on Home; in a level, what BACK TO HOME does once confirmed.</param>
        /// <param name="onClose">Called when the X closes it.</param>
        public static RectTransform Open(RectTransform canvas, UiSkin skin, Action backToHome, Action onClose = null)
        {
            var root = Stretch(canvas, "SettingsPopup");
            Image(root, null, Hex("2B87DCFF"), raycast: true); // the blue page (phone screenshot)

            // Header: 310 high, ZigZag_Tile in orange, SETTINGS, the X on the right.
            var header = Rect(root, "Header", new Vector2(0f, 1f), Vector2.one, Vector2.zero, new Vector2(0f, 310f), new Vector2(0.5f, 1f));
            Image(header, null, Hex("FEB207FF")); // yellow under the orange zigzag (phone screenshot: FEB207 / FD9B14)
            var pattern = Image(Stretch(header, "Pattern"), skin.Get("ZigZag_Tile"), new Color(0.988f, 0.612f, 0.043f)); // the prefab's tint
            pattern.type = UnityEngine.UI.Image.Type.Tiled;
            // Divider/Line: a thin yellow line under the header (video).
            Image(Rect(header, "Line", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 6f), new Vector2(0.5f, 0f)), null, Hex("FFD312FF"));
            var shadow = Rect(header, "Shadow", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -12f), new Vector2(0f, 12f), new Vector2(0.5f, 1f));
            Image(shadow, skin.Get("LineShadow"), new Color(0f, 0f, 0f, 0.59f));
            var title = Text(Rect(header, "Text (TMP) - Header", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(600f, 100f)), "SETTINGS", skin.font, skin.homeScreenLevelsMaterial, 80f, Color.white);
            // Tapping SETTINGS twice opens the cheat panel, on Home and in a level (Editor / Development Build only) [user].
            title.raycastTarget = true;
            title.gameObject.AddComponent<CheatSystem.CheatButton>().Taps = 2;
            var cancel = Rect(header, "Cancel Button", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-82f, -20f), new Vector2(100f, 100f));
            Image(cancel, null, Color.clear, raycast: true);
            Image(Rect(cancel, "cancel icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(73f, 73f)), skin.Get("Button_Cancel"), new Color(0.827f, 0.38f, 0.016f));
            cancel.gameObject.AddComponent<Button>().onClick.AddListener(() => { UnityEngine.Object.Destroy(root.gameObject); onClose?.Invoke(); });

            // Switches: a blue box 800 x 260, three columns (the original also has Alerts, off in this version).
            var toggles = Rect(root, "Container_Toggles", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(800f, 260f), new Vector2(0.5f, 1f));
            Image(toggles, skin.Get("Popup_Background_Blue"), Color.white, sliced: true);
            Toggle(toggles, skin, -250f, "MUSIC", "Music", () => GameSettings.Music, v => GameSettings.Music = v);
            Toggle(toggles, skin, 0f, "HAPTICS", "Haptics", () => GameSettings.Haptics, v => GameSettings.Haptics = v);
            Toggle(toggles, skin, 250f, "SOUND", "Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);

            var language = Rect(root, "Container_Language", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -695f), new Vector2(800f, 112f), new Vector2(0.5f, 1f));
            Image(language, skin.Get("Popup_Background_Blue"), Color.white, sliced: true);
            Text(Rect(language, "Text (TMP) - language", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 76f)), "ENGLISH", skin.font, skin.plainMaterial, 52f, Color.white);
            Image(Rect(language, "arrow_R", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(64f, 64f)), skin.Get("Icon_Arrow"), Color.white).preserveAspect = true;
            Image(Rect(language, "arrow_L", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f)), skin.Get("Icon_Arrow_2"), Color.white).preserveAspect = true;

            // Buttons: 800 x 180 each.
            BigButton(root, skin, -960f, "SupportButton", "Button_Green_256", "S_Icon_Support", Color.white, "HELP & SUPPORT", new Color(0.024f, 0.463f, 0.051f), null);
            if (backToHome != null)
                BigButton(root, skin, -1165f, "MainMenuButton", "Button_Blue_256", "Pictogram_Home", new Color(0f, 0.29f, 0.576f), "BACK TO HOME", new Color(0f, 0.294f, 0.584f),
                    () => LivesPopup.ConfirmLeave(canvas, skin, backToHome));

            var terms = Text(Rect(root, "Terms&Privacy (TMP)", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 135f), new Vector2(860f, 68f)), "TERMS & PRIVACY", skin.font, skin.plainMaterial, 55f, new Color(0.97f, 0.97f, 0.97f));
            terms.raycastTarget = false;
            CentreOnLetters(root);
            Pressable(root);

            return root;
        }

        private static void Toggle(RectTransform box, UiSkin skin, float x, string title, string icon, Func<bool> get, Action<bool> set)
        {
            var column = Rect(box, "Container_Button_" + icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(170f, 260f));
            Text(Rect(column, "Title (TMP)", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(200f, 90f)), title, skin.font, skin.plainMaterial, 48f, Color.white);
            var button = Rect(column, "ToggleButton_" + icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(128f, 128f));
            Image(Rect(button, "Shadow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(150f, 110f)), skin.Get("Shadow_Round"), new Color(0f, 0f, 0f, 0.35f));
            var state = Image(Stretch(button, "State"), null, Color.white, raycast: true);
            var image = Image(Rect(button, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(80f, 80f)), null, Color.white);
            image.preserveAspect = true;
            void Show()
            {
                bool on = get();
                state.sprite = skin.Get(on ? "S_Button_Round_Green_Underlay" : "S_Button_Round_Gray_Underlay");
                image.sprite = skin.Get("S_Icon_" + icon + (on ? "_Enabled" : "_Disabled"));
            }
            Show();
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => { set(!get()); Show(); });
        }

        private static void BigButton(RectTransform root, UiSkin skin, float y, string name, string sprite, string icon, Color iconColor, string text, Color textColor, Action onClick)
        {
            var rect = Rect(root, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(800f, 180f), new Vector2(0.5f, 1f));
            Image(Rect(rect, "shadow", Vector2.zero, Vector2.one, new Vector2(0f, -18f), new Vector2(14.4f, 0f)), skin.Get("Shadow_Round_sharp"), new Color(0.122f, 0.13f, 0.264f, 0.43f), sliced: true);
            // A thin dark rim round the face gives the button its edge (phone screenshot): the same shape, darker, 3 px out.
            Image(Rect(rect, "outline", Vector2.zero, Vector2.one, new Vector2(0f, -2f), new Vector2(7f, 9f)), skin.Get(sprite), new Color(textColor.r * 0.55f, textColor.g * 0.55f, textColor.b * 0.55f + 0.08f), sliced: true);
            var face = Image(Stretch(rect, "button color"), skin.Get(sprite), Color.white, sliced: true, raycast: true);
            Image(Rect(rect, "Generic Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-270f, 0f), new Vector2(136f, 136f)), skin.Get(icon), iconColor).preserveAspect = true;
            Text(Rect(rect, "Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(74f, 0f), new Vector2(560f, 88f)), text, skin.font, skin.plainMaterial, 62f, textColor);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
    }
}
