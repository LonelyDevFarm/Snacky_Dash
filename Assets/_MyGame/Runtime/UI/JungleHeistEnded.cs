using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;
using static SnackDash.UI.JungleHeistUi;

namespace SnackDash.UI
{
    /// <summary>
    /// The end of a Jungle Heist run (original Popup_Lava_Quest_Ended_Won / _Lose): the brown frame with the treasure
    /// boat (won) or the lose picture, "You won / failed the challenge. Next Jungle Heist will start in:" and the red timer,
    /// and CONTINUE. The share itself is shown and paid before, by JungleHeistRewards (CONTINUE still pays it if not).
    /// One prefab for both, Prefabs/UI/JungleHeistEnded [user]; the won / lost parts are switched on by name.
    /// </summary>
    public class JungleHeistEnded : MonoBehaviour
    {
        private RectTransform _popup;
        private Image _overlay;
        private float _overlayAlpha;
        private TextMeshProUGUI _timer;
        private Transform _hand;
        private bool _closing;
        private float _tick;

        public static void Open(RectTransform parent, UiSkin skin, GameObject prefab, bool won, Action closed)
        {
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, parent).transform;
                root.name = "Popup_Lava_Quest_Ended";
            }
            else
            {
                root = Stretch(parent, "Popup_Lava_Quest_Ended");
                BuildLayout(root, skin);
            }
            var view = root.GetComponent<JungleHeistEnded>();
            if (view == null) view = root.gameObject.AddComponent<JungleHeistEnded>();
            view.Bind(root, won, closed);
        }

        public static void BuildLayout(RectTransform root, UiSkin skin)
        {
            if (root.GetComponent<JungleHeistEnded>() == null) root.gameObject.AddComponent<JungleHeistEnded>();
            var popup = Frame(root, skin, out var mask);

            // Won: the start picture, square (AspectRatioFitter height-led) on the mask's height + 438.6, its right edge
            // 322 past the mask's (Offer_Visual), with the sign; "Grand prize" over it (scale 1.45) and the brown fade below.
            var won = Stretch(mask, "Won");
            var visual = Centre(won, "Offer_Visual", -118.8f, 219.28f, 1645.6f, 1645.6f);
            Image(visual, skin.Get("Tex_Jungle_Heist_Start_Illustration_V2_0"), Color.white).preserveAspect = true;
            var sign = Centre(visual, "Sign", 311f, -248f, 389.3f, 266.8f);
            sign.localRotation = Quaternion.Euler(0f, 0f, -14.33f);
            Image(sign, skin.Get("S_Sign_Bg_0"), Color.white);
            var grant = Text(Centre(won, "Grant_Text", 177.2f, -30.2f, 209.4f, 106.3f), "Grand prize\n<size=140%>10 000</size>", skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 30f, Color.white);
            grant.rectTransform.localScale = Vector3.one * 1.45f;
            grant.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8.06f);
            Image(Centre(won, "BackgroundGreen", 0f, -128.8f, 765.5f, 987.6f), skin.Get("S_Gradient_Overlay_LavaQuest"), new Color(0.165f, 0.114f, 0.031f), sliced: true);

            // Lost: Tex_Jungle Heist_Lose_screen and its dark fades.
            var lost = Stretch(mask, "Lost");
            Image(Stretch(lost, "Offer_Visual"), skin.Get("Tex_Jungle Heist_Lose_screen"), Color.white).preserveAspect = false;
            Image(Centre(lost, "BotGreenGradian", 0f, -301.75f, 803.6f, 603.5f), skin.Get("S_LinearGradiant_0"), new Color(0.023f, 0.102f, 0.011f));
            var top = Centre(mask, "TopGreenGradian", 0f, 464.8f, 782.6f, 319.2f);
            top.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Image(top, skin.Get("S_LinearGradiant_0"), new Color(0.039f, 0.216f, 0.2f));
            Inlay(popup, skin);

            // Text_Info (0, -35, 191.8 high): Hint_Text 87.1 under its bottom, the timer banner at about -316 (over the dark fade).
            var hint = Text(Centre(root, "Hint_Text", 0f, -35f - 95.9f - 87.1f, 666f, 174.3f),
                "<size=45>You <color=#FED025>won</color> the challenge!</size>\nNext <color=#FED025>Jungle Heist</color> will start in:", skin.font, skin.EventMaterial("LuckiestGuy-Regular Brown Cartoon"), 37f, Color.white);
            hint.richText = true;
            hint.enableWordWrapping = true;
            Timer(root, skin, new Vector2(9.6f, -316f), 1.06f, out _);
            GreenButton(root, skin, "Continue");
            CentreOnLetters(root);
        }

        private void Bind(RectTransform root, bool won, Action closed)
        {
            _overlay = root.Find("Overlay").GetComponent<Image>();
            _overlayAlpha = _overlay.color.a;
            _popup = (RectTransform)root.Find("Popup_Container");
            var mask = _popup.Find("Background/Image_Mask");
            mask.Find("Won").gameObject.SetActive(won);
            mask.Find("Lost").gameObject.SetActive(!won);
            var hint = root.Find("Hint_Text").GetComponent<TextMeshProUGUI>();
            hint.text = won
                ? "<size=45>You <color=#FED025>won</color> the challenge!</size>\nNext <color=#FED025>Jungle Heist</color> will start in:"
                : "<size=45>You <color=#FED025>failed</color> the challenge.</size>\nNext <color=#FED025>Jungle Heist</color> will start in:";
            _timer = root.Find("Timer_Banner/Text (TMP)").GetComponent<TextMeshProUGUI>();
            _hand = root.Find("Timer_Banner/Timer_Clock/TimerArrow");
            Action done = () =>
            {
                if (won && JungleHeist.Shared.ClaimPrize()) SnackDash.Audio.SoundPlayer.Play("CollectCoin");
                closed?.Invoke();
            };
            AddClick(_popup.Find("Close_Button"), () => StartCoroutine(Close(done)));
            AddClick(root.Find("PlayButton/View_Button_Play_Base/Front"), () => StartCoroutine(Close(done)));
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

        private IEnumerator PopIn()
        {
            var scale = _popup.localScale;
            Action<float> step = t =>
            {
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * Mathf.Clamp01(t * 2f));
                _popup.localScale = scale * Tween.OutBack(1.6f)(t);
            };
            step(0f);
            yield return Tween.Run(0.35f, Tween.Linear, step, true);
        }

        private IEnumerator Close(Action then)
        {
            if (_closing) yield break;
            _closing = true;
            var scale = _popup.localScale;
            yield return Tween.Run(0.22f, Tween.Linear, t =>
            {
                _popup.localScale = scale * (1f - Tween.InBack(1.5f)(t));
                _overlay.color = new Color(0f, 0f, 0f, _overlayAlpha * (1f - t));
            }, true);
            Destroy(gameObject);
            then?.Invoke();
        }

        private void Update()
        {
            Tick(_hand);
            if (_timer == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            _timer.text = Clock(JungleHeist.Shared.Left);
        }
    }
}
