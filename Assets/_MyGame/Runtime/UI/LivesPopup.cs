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
    /// The lives popups, laid out from the original prefabs:
    /// <list type="bullet">
    /// <item>LIVES (AdsLifeRefillPopup, video "home"): the big heart with the number of lives. When lives are
    /// full it only says FULL. When they are not, it also shows "Time to next life:" and two buttons:
    /// Get 1 (the video button, which gives one life straight away here) and Refill all (900 coins, back to 5)
    /// [user]. The original's coin pack offer under the popup is left out.</item>
    /// <item>ConfirmQuitLevelPopup: "YOU WILL LOSE 1 LIFE!", the row of hearts, the regen countdown,
    /// CONTINUE leaves the level and X goes back [user].</item>
    /// </list>
    /// Both pop in: the dark cover fades in and the card grows from 90 % with a small overshoot.
    /// </summary>
    public class LivesPopup : MonoBehaviour
    {
        private UiSkin _skin;
        private TextMeshProUGUI _number, _countdown;
        private Image _infinity;
        private RectTransform _buttons;
        private Image[] _hearts;
        private TextMeshProUGUI _timeLabel;

        /// <summary>The LIVES popup (tapping the hearts on Home, or Level with no lives left).</summary>
        public static LivesPopup Open(RectTransform canvas, UiSkin skin)
        {
            var popup = Frame(canvas, skin, "AdsLifeRefillPopup", "LIVES", 247f, new Color(0f, 0f, 0f, 0.95f), out var content, out var body); // no cover of its own in the prefab: black; the video dims Home to about 20 % (the project blends in linear, hence 0.95)
            popup._skin = skin;
            var heart = Rect(body, "Heart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-11f, 29f), new Vector2(361f, 338.3f));
            Image(heart, skin.Get("S_Icon_Heart_Large_0"), Color.white);
            popup._number = Text(Rect(heart, "Lives Text", Vector2.zero, Vector2.one, new Vector2(0f, -10.5f), new Vector2(0f, -20.9f)), "5", skin.font, skin.plainMaterial, 165f, Color.white);
            popup._infinity = Image(Rect(heart, "Image Infinity", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16.3f), new Vector2(172.3f, 107.6f)), skin.Get("S_Icon_Infinite_0"), Color.white);
            popup._countdown = Text(Rect(body, "CountdownText", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 45f), new Vector2(-100f, 75f)), "FULL", skin.font, skin.plainMaterial, 41f, Hex("446890FF"));
            popup._countdown.richText = true;

            // Bottom: Get 1 (orange) and Refill all (green), only while lives are not full. The original row spreads its
            // 128 spare width evenly (expand, spacing 0): centres -175.5 and 205.5, 32 from each side; top padding -15.
            popup._buttons = Rect(content, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 185.9f), new Vector2(-30f, 217.5f));
            var ads = Rect(popup._buttons, "WatchAdsButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-175.5f, 7.5f), new Vector2(347.4f, 150f));
            Image(ads, skin.Get("S_Button_Orange"), Color.white, sliced: true, raycast: true);
            Image(Rect(ads, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-95f, 8f), new Vector2(90f, 90f)), skin.Get("S_Play_ads_Icon_0"), Color.white).preserveAspect = true;
            Text(Rect(ads, "Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(45f, 8f), new Vector2(210f, 70f)), "Get 1", skin.font, skin.greenOutlineMaterial, 50f, Color.white);
            ads.gameObject.AddComponent<Button>().onClick.AddListener(() => PlayerEconomy.Shared.AddLife());

            var refill = Rect(popup._buttons, "RefillByCoinsButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(205.5f, 7.5f), new Vector2(287.1f, 150f));
            Image(refill, skin.Get("S_Button_Green"), Color.white, sliced: true, raycast: true);
            Text(Rect(refill, "Text", Vector2.zero, Vector2.one, new Vector2(0f, 22.9f), new Vector2(-26.3f, -100.9f)), "Refill all", skin.font, skin.greenOutlineMaterial, 45f, Color.white);
            var cost = Rect(refill, "CostPanel", Vector2.zero, new Vector2(1f, 0f), new Vector2(-3.1f, 45.1f), new Vector2(-76.2f, 65f));
            Image(Rect(cost, "Icon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(53.3f, -32.5f), new Vector2(106.6f, 65f)), skin.Get("S_Icon_Coin_Single_0"), Color.white).preserveAspect = true;
            Text(Rect(cost, "Cost", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(177.4f, -32.5f), new Vector2(141.5f, 65f)), PlayerEconomy.RefillPrice.ToString(), skin.font, skin.greenOutlineMaterial, 55f, Color.white);
            refill.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (!PlayerEconomy.Shared.TryRefill()) ShopView.Open(canvas, skin, 0f, () => { }); // short of coins: the Shop, its X comes back here [user]
            });

            popup.Finish();
            return popup;
        }

        /// <summary>BACK TO HOME in a level: CONTINUE runs <paramref name="leave"/> (which costs the life), X closes.</summary>
        public static LivesPopup ConfirmLeave(RectTransform canvas, UiSkin skin, Action leave)
        {
            var popup = Frame(canvas, skin, "ConfirmQuitLevelPopup", "ARE YOU SURE?", 0f, new Color(0.035f, 0.137f, 0.204f, 0.88f), out var content, out var body);
            popup._skin = skin;
            var top = Text(Rect(body, "Text Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -80.2f), new Vector2(-96.2f, 117.1f)), "YOU WILL LOSE 1 LIFE!", skin.font, skin.plainMaterial, 60f, new Color(1f, 0.286f, 0.271f));
            top.enableAutoSizing = true;
            top.fontSizeMin = 30f;
            top.fontSizeMax = 60f;
            // LivesPanel: one slot per life, the grey empty heart under the red full one.
            var panel = Rect(body, "LivesPanel", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -40f), new Vector2(-50f, 130f));
            popup._hearts = new Image[PlayerEconomy.MaxLives];
            for (int i = 0; i < popup._hearts.Length; i++)
            {
                var live = Rect(panel, i == 0 ? "Live" : $"Live ({i})", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (popup._hearts.Length - 1) * 0.5f) * 135f, 0f), new Vector2(125f, 125f));
                Image(Stretch(live, "Empty"), skin.Get("S_Icon_Heart_bg_0"), Color.white).preserveAspect = true;
                popup._hearts[i] = Image(Stretch(live, "Full"), skin.Get("S_Icon_Heart_0"), Color.white);
                popup._hearts[i].preserveAspect = true;
            }
            popup._timeLabel = Text(Rect(body, "Text Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(-132.2f, 42.6f), new Vector2(-264.5f, 85.1f)), "Time to next life:", skin.font, skin.plainMaterial, 41f, Hex("446890FF"));
            popup._countdown = Text(Rect(body, "Text CountDown", Vector2.zero, new Vector2(1f, 0f), new Vector2(250.8f, 42.6f), new Vector2(-501.6f, 85.1f)), "0s", skin.font, skin.plainMaterial, 50f, Hex("F59D00FF"));

            var bottom = Rect(content, "Bottom", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 185.9f), new Vector2(-183.7f, 217.5f));
            var cont = Rect(bottom, "ContinueButton", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(510.1f, 208.3f));
            Image(cont, skin.Get("S_Green_btn_0"), Color.white, raycast: true);
            Text(Rect(cont, "Text", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-68.3f, -45.1f)), "CONTINUE", skin.font, skin.greenOutlineMaterial, 55f, Color.white);
            cont.gameObject.AddComponent<Button>().onClick.AddListener(() => leave());

            popup.Finish();
            return popup;
        }

        /// <summary>The shared frame: cover, Content 792 x 1078, the tilted blue title, X, the white Body.</summary>
        private static LivesPopup Frame(RectTransform canvas, UiSkin skin, string name, string title, float y, Color coverColor, out RectTransform content, out RectTransform body)
        {
            var root = Stretch(canvas, name);
            var cover = Image(root, skin.Get("gdpr_rounded_square"), coverColor, sliced: true, raycast: true);
            var popup = root.gameObject.AddComponent<LivesPopup>();
            content = Rect(root, "Content", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(792.2f, 1078.7f));
            Image(Rect(content, "BG", Vector2.zero, Vector2.one, new Vector2(0f, -16.2f), new Vector2(0f, -186.6f)), skin.Get("S_BG_Pop-up_Frame_0"), Color.white, sliced: true);
            var header = Rect(content, "Header", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -131.4f), new Vector2(0f, 262.9f));
            var titleRect = Rect(header, "Title", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16f, -4.4f), new Vector2(645.6f, 215.5f));
            Image(titleRect, skin.Get("S_BG_Title (1)_0"), Color.white);
            var text = Rect(titleRect, "Text", Vector2.zero, Vector2.one, new Vector2(0f, 1.6f), new Vector2(-53f, -92.4f));
            text.localRotation = Quaternion.Euler(0f, 0f, 3f); // the prefab tilts the words with the banner
            Text(text, title, skin.font, skin.blueUnderlayMaterial, 72f, Color.white);
            var close = Rect(header, "CloseButton", Vector2.one, Vector2.one, new Vector2(-17.7f, -125.2f), new Vector2(117.6f, 117.1f));
            Image(close, skin.Get("S_Close_Btn_0"), Color.white, raycast: true);
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(root.gameObject));
            body = Rect(content, "Body", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 22f), new Vector2(-30f, 533.5f));
            Image(Stretch(body, "BG"), skin.Get("S_BG_Pop-up_0"), Color.white, sliced: true);
            popup._cover = cover;
            popup._content = content;
            return popup;
        }

        private Image _cover;
        private RectTransform _content;

        private void Finish()
        {
            CentreOnLetters(transform);
            Pressable(transform);
            PlayerEconomy.Shared.Changed += Refresh;
            Refresh();
            StartCoroutine(PopIn());
        }

        private IEnumerator PopIn()
        {
            var group = _content.gameObject.AddComponent<CanvasGroup>();
            float coverAlpha = _cover.color.a;
            // Real time: BACK TO HOME opens it over the paused level (timeScale 0), where it stayed invisible [user].
            yield return Tween.Run(0.25f, Tween.Linear, t =>
            {
                var c = _cover.color;
                c.a = coverAlpha * t;
                _cover.color = c;
                group.alpha = Mathf.Clamp01(t * 2f);
                _content.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, Tween.OutBack(1.7f)(t));
            }, true);
        }

        private void OnDestroy() => PlayerEconomy.Shared.Changed -= Refresh;

        private float _tick;

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            var economy = PlayerEconomy.Shared;
            int lives = economy.Lives;
            bool infinite = economy.InfiniteLives, full = !infinite && lives >= PlayerEconomy.MaxLives;
            if (_number != null)
            {
                _number.text = lives.ToString();
                _number.enabled = !infinite;
                _infinity.enabled = infinite;
                _buttons.gameObject.SetActive(!full && !infinite);
                _countdown.text = full ? "FULL" : $"Time to next life: <color=#F59D00>{Clock(infinite ? economy.InfiniteLeft : economy.NextLifeIn)}</color>";
            }
            if (_hearts != null)
            {
                for (int i = 0; i < _hearts.Length; i++) _hearts[i].enabled = infinite || i < lives;
                // Full lives: no life is coming back, so the countdown line stays hidden.
                _timeLabel.enabled = _countdown.enabled = !full;
                _countdown.text = Clock(infinite ? economy.InfiniteLeft : economy.NextLifeIn);
            }
        }

        /// <summary>"12m 05s", "45s", "1h 20m" (the prefab's sample is "0s").</summary>
        public static string Clock(TimeSpan time)
        {
            if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes:00}m";
            if (time.TotalMinutes >= 1) return $"{time.Minutes}m {time.Seconds:00}s";
            return $"{Mathf.Max(0, time.Seconds)}s";
        }
    }
}
