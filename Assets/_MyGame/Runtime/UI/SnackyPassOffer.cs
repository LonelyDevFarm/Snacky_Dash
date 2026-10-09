using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The Snacky Pass offer (original SeasonPassOfferPopup, video sk2 22.6-25 s): the golden frame with the Snacky Pass
    /// ticket on top, the timer, the illustration, what the pass gives (the 8 lives, 3000 coins, x10 of each booster,
    /// 6 hours of infinite lives), "Buy and finish all stages for rewards!" and the price button. There are no
    /// purchases in this game: the button reads "0 đ" and turns the pass on [user]. It pops in and shrinks away.
    /// The layout is the prefab Prefabs/UI/SnackyPassOffer [user].
    /// </summary>
    public class SnackyPassOffer : MonoBehaviour
    {
        private RectTransform _container;
        private Image _overlay;
        private float _overlayAlpha;
        private TextMeshProUGUI _timer;
        private Transform _clockHand;
        private bool _closing;
        private float _tick;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, Action activated)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "SeasonPassOfferPopup";
            }
            else
            {
                root = Stretch(parent, "SeasonPassOfferPopup");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<SnackyPassOffer>();
            if (view == null) view = root.gameObject.AddComponent<SnackyPassOffer>();
            view.Bind(root, activated);
        }

        // ================================================================== layout (saved as the prefab)

        private static RectTransform Centre(Transform parent, string name, float x, float y, float w, float h)
            => Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<SnackyPassOffer>() == null) root.gameObject.AddComponent<SnackyPassOffer>();
            Image(Stretch(root, "Overlay"), null, new Color(0f, 0f, 0f, 0.957f), raycast: true);
            var popup = Centre(root, "Popup_Container", 0f, 0f, 834f, 1300f);
            popup.localScale = Vector3.one * 0.92f; // the frame fits the 887 wide phone with a little room

            Image(Centre(popup, "BackgroundReward", -13.3f, 683.9f, 579.1f, 579.1f), skin.Get("S_TitleRewardBackground_0"), Color.white).preserveAspect = true;
            var frame = Rect(popup, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-50.6f, 0f));
            Image(frame, skin.Get("S_Pop-up_Frame_GoldenBorder_0"), Color.white, sliced: true);
            var mask = Rect(frame, "Image_Mask", Vector2.zero, Vector2.one, new Vector2(0f, 11.5f), new Vector2(-70f, -93f));
            Image(mask, null, Color.black);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image(Centre(mask, "BackgroundGreen", 1.35f, -413.8f, 753.8f, 417.7f), null, new Color(0f, 0.498f, 0.322f));
            var visual = Rect(mask, "Offer_Visual", Vector2.zero, Vector2.one, new Vector2(18f, 176f), new Vector2(140.65f, -388.3f));
            visual.localScale = Vector3.one * 1.1f;
            Image(visual, skin.Get("BackgroundImage_0"), Color.white);
            Image(Centre(mask, "BotGreenGradian", 0f, -249f, 753.8f, 124.8f), skin.Get("S_LinearGradiant_0"), new Color(0f, 0.498f, 0.322f));
            var top = Centre(mask, "TopGreenGradian", -2.36f, 464.8f, 749f, 319.2f);
            top.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image(top, skin.Get("S_LinearGradiant_0"), new Color(0.039f, 0.204f, 0f));
            Image(Centre(mask, "BotGreenGradian (1)", 0f, -371.8f, 753.8f, 463.4f), skin.Get("S_LinearGradiant_0"), new Color(0f, 0f, 0f, 0.6f));
            Image(Rect(frame, "Inlay", Vector2.zero, Vector2.one, new Vector2(0f, 12f), new Vector2(-56f, -80f)), skin.Get("S_Pop-up_Frame_InlayWhite_0"), new Color(0.906f, 0.631f, 0.125f), sliced: true);

            // Timer under the ticket, its clock on the left.
            var timer = Rect(popup, "Timer_Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(9.6f, -144.3f), new Vector2(190f, 50f), new Vector2(0.5f, 1f));
            timer.localScale = Vector3.one * 1.06f;
            timer.localRotation = Quaternion.Euler(0f, 0f, 1.3f);
            Image(Stretch(timer, "Timer Background"), skin.Get("S_Background_Timer_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.23f;
            Text(Rect(timer, "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-28f, 0f)), "7D 00H", skin.font, skin.EventMaterial("LuckiestGuy-Regular Red Cartoon 02"), 36f, Color.white);
            var clock = Rect(popup, "Timer_Clock", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(336f, 478.9f), new Vector2(67.9f, 59.8f));
            clock.localScale = Vector3.one * 0.949f;
            Image(clock, skin.Get("S_Bg_Clock_0"), Color.white);
            Image(Rect(clock, "Timer Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.43f, 0.53f), new Vector2(19.8f, 17.9f), new Vector2(1f, 1f)), skin.Get("S_Icon_Timer_03_0"), Color.white);
            Image(Centre(clock, "Timer Center", 0f, 0f, 15.1f, 15.1f), skin.Get("S_Icon_Timer_02_0"), Color.white);

            // Header: the Snacky Pass ticket.
            var header = Centre(popup, "Header", 0f, 608f, 407.9f, 211.2f);
            Image(header, skin.Get("S_Img_Ticket_0"), Color.white);
            var title = Rect(header, "MainTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -129f), new Vector2(400f, 161.4f));
            title.localScale = Vector3.one * 0.9f;
            SnackyPassView.TitleWords(title, skin, -2.2f, 14f, 11.2f);

            var close = Rect(popup, "Close_Button", Vector2.one, Vector2.one, new Vector2(-47.3f, -25.8f), new Vector2(86f, 86f));
            close.localScale = Vector3.one * 1.136f;
            Image(close, skin.Get("S_Button_CloseYellow_0"), Color.white, raycast: true);
            var closeShadow = close.gameObject.AddComponent<Shadow>();
            closeShadow.effectColor = new Color(0f, 0f, 0f, 0.165f);
            closeShadow.effectDistance = new Vector2(0f, -5f);

            // Content: what the pass gives, the line and the price.
            var content = Rect(popup, "Content_Container", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 331f), new Vector2(675f, 525f));
            var back = Stretch(content, "Background");
            var box = Rect(back, "Container", Vector2.zero, Vector2.one, new Vector2(0f, 100f), new Vector2(-10f, -245.2f));
            Image(box, skin.Get("S_Bg_Offer_Top_0"), Color.white, sliced: true);
            var main = Rect(box, "MainReward", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(140f, 0f), new Vector2(255f, 255f));
            Image(Stretch(main, "Background"), skin.Get("S_Bg_Offer_Container"), new Color(1f, 1f, 1f, 0.773f), sliced: true);
            Image(Rect(main, "Icon", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20f, -20f)), skin.Get("S_Icon_PremiumReward"), Color.white).preserveAspect = true; // without the NO ADS mark: no ads here [user]
            var grid = Rect(box, "RootContainer", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(465f, 0f), new Vector2(375f, 255f));
            string[] icons = { "Coin_Icon_02_0", "S_Icon_Booster_Teleporter", "S_Icon_Booster_Undo", "S_Icon_Booster_Magnet", "S_Icon_Booster_FruitCanon", "S_Icon_Heart_Infinit_Lives" };
            string[] amounts = { "3000", "x10", "x10", "x10", "x10", "6H" };
            for (int i = 0; i < icons.Length; i++)
            {
                var element = Rect(grid, i == 0 ? "Element" : $"Element ({i})", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f + (i % 3) * 125f, -64f - (i / 3) * 127f), new Vector2(119f, 121f));
                Image(Stretch(element, "Background"), skin.Get("S_Bg_Offer_Container"), Color.white, sliced: true);
                var view = Rect(element, "RewardView", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-32f, -32f));
                var icon = Image(Rect(view, "Icon", Vector2.zero, Vector2.one, new Vector2(0f, 8f), Vector2.zero), skin.Get(icons[i]), Color.white);
                icon.preserveAspect = true;
                Text(Rect(icon.rectTransform, "Amount", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -10f), new Vector2(30f, 44f)), amounts[i], skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 33.9f, Color.white);
            }
            Image(Rect(back, "Background_Purple", Vector2.zero, Vector2.one, new Vector2(0f, -148.2f), new Vector2(-10f, -306.5f)), skin.Get("S_Bg_Offer_BottomV2"), Color.white, sliced: true);

            var button = Rect(content, "Button", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 95f), new Vector2(340f, 120f));
            Image(Centre(button, "Front", 0f, -2.16f, 332f, 124.3f), skin.Get("S_Button_Green_0"), Color.white, sliced: true, raycast: true);
            Text(Centre(button, "Price (TMP)", 0f, 4f, 300f, 91.6f), "0 đ", skin.font, skin.EventMaterial("LuckiestGuy-Regular Green Cartoon"), 64f, Color.white);
            Image(Rect(content, "Frame", Vector2.zero, Vector2.one, new Vector2(0f, -6.44f), new Vector2(0f, -12.9f)), skin.Get("S_Bg_Offer_Frame_0"), Color.white, sliced: true).pixelsPerUnitMultiplier = 1.9f;
            var line = Text(Rect(content, "Description", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -81f), new Vector2(-60f, 50f)), "Buy and finish all stages for rewards!", skin.font, skin.plainMaterial, 32.35f, Color.white);
            line.enableAutoSizing = true;
            line.fontSizeMin = 20f;
            line.fontSizeMax = 32.35f;
            CentreOnLetters(root);
        }

        // ================================================================== behaviour

        private void Bind(RectTransform root, Action activated)
        {
            _overlay = root.Find("Overlay").GetComponent<Image>();
            _overlayAlpha = _overlay.color.a;
            _container = (RectTransform)root.Find("Popup_Container");
            _timer = _container.Find("Timer_Banner/Text (TMP)").GetComponent<TextMeshProUGUI>();
            _clockHand = _container.Find("Timer_Clock/Timer Arrow");
            AddClick(_container.Find("Close_Button"), () => StartCoroutine(Close(null)));
            AddClick(_container.Find("Content_Container/Button/Front"), () => StartCoroutine(Close(activated)));
            UiShiny.Widget(_container.Find("Content_Container/Button/Front").GetComponent<Image>());
            Pressable(root);
            StartCoroutine(PopIn());
        }

        private static void AddClick(Transform target, Action action)
        {
            if (target == null) return;
            var button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
        }

        /// <summary>sk2 22.6-23 s: the cover darkens and the popup grows in with a little overshoot.</summary>
        private IEnumerator PopIn()
        {
            var scale = _container.localScale;
            System.Action<float> step = t =>
            {
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * Mathf.Clamp01(t * 2f));
                _container.localScale = scale * Tween.OutBack(1.6f)(t);
            };
            step(0f);
            yield return Tween.Run(0.35f, Tween.Linear, step, true);
        }

        /// <summary>sk2 25 s: it shrinks away to the middle as the cover lifts.</summary>
        private IEnumerator Close(Action then)
        {
            if (_closing) yield break;
            _closing = true;
            if (then != null) { SnackDash.Audio.SoundPlayer.Play("CollectCoin"); SnackDash.Audio.Haptics.Medium(); }
            var scale = _container.localScale;
            yield return Tween.Run(0.22f, Tween.Linear, t =>
            {
                _container.localScale = scale * (1f - Tween.InBack(1.5f)(t));
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * (1f - t));
            }, true);
            Destroy(gameObject);
            then?.Invoke();
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (_clockHand != null)
            {
                int steps = Mathf.FloorToInt(time);
                _clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(Mathf.Clamp01((time - steps) / 0.2f))));
            }
            if (_timer == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = SeasonPass.Shared.Left;
            _timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : $"{left.Hours}H {left.Minutes:00}M";
        }
    }
}
