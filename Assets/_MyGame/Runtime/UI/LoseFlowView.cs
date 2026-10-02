using System;
using System.Collections;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Lose flow as recorded on the original (video Man_Lose): biting the tail shows "NO WAY OUT!"
    /// with SNIP TAIL (pay to continue) and GIVE UP; giving up shows GAME OVER / TRY AGAIN, which
    /// restarts the level. Built in code from the original sprites, font materials and RectTransform
    /// values of LoseInBoardPopup / LoseScreenFailedPopup: instancing those decompiled prefabs
    /// crashed the editor (gizmo build on their UI particle / legacy animation parts).
    /// Snip costs come from SnipOffer (0 coins for now).
    /// </summary>
    public class LoseFlowView : MonoBehaviour
    {
        [SerializeField] private UiSkin skin = new UiSkin();
        [SerializeField] private SnipOffer snipOffer = new SnipOffer();
        [SerializeField] private float popupAppearDuration = 0.35f;

        [Header("NO WAY OUT (AppearDissappear feedback of LoseInBoardPopup)")]
        [SerializeField] private float titleScaleDuration = 0.2f;
        [SerializeField] private float titleHoldDuration = 0.75f;
        [SerializeField] private float bodyFadeDuration = 0.2f;
        [Tooltip("Board zoom-out behind the buttons; ~0.33 s measured on Man_Lose (30 fps), easing not known.")]
        [SerializeField] private float boardRefitDuration = 0.35f;

        // Bottom_Container height and Top_Container_Version_B (130 + 15 offset) of LoseInBoardPopup.
        private const float BottomContainerHeight = 500f;
        private const float TopContainerHeight = 145f;

        private GameSession _session;
        private Wallet _wallet;
        private Action _restart;
        private BoardCameraFitter _cameraFitter;
        private GameObject _open;

        public void Bind(GameSession session, Wallet wallet, Action restart, BoardCameraFitter cameraFitter)
        {
            _session = session;
            _wallet = wallet;
            _restart = restart;
            _cameraFitter = cameraFitter;
            session.TailBitten += snipCount => ShowNoWayOut();
        }

        private void ShowNoWayOut() => StartCoroutine(NoWayOut());

        /// <summary>
        /// FailText AppearDissappear feedback: title scales in 0.2 s, holds 0.75 s, scales out 0.2 s;
        /// then the body (gradients + buttons) fades in while the board is refitted above the buttons.
        /// </summary>
        private IEnumerator NoWayOut()
        {
            var root = Open("NoWayOut");

            // FailText (stretch, -56 x -1408) / Title_Text: auto-size 15..146, wrapped, spacing -2.7.
            // On a tall phone the canvas is narrower than 1080, so the title wraps to two lines.
            var failText = Rect(root, "FailText", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-56, -1408));
            var title = Rect(failText, "Title_Text", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 54.66f), new Vector2(-36.46f, 333.84f));
            TitleText(Stretch(title, "Back"), skin.titleBackMaterial);
            TitleText(Stretch(title, "Front"), skin.titleFrontMaterial);
            title.localScale = Vector3.zero;
            yield return Tween.Run(titleScaleDuration, Tween.OutBack(3f), t => title.localScale = Vector3.one * t);
            yield return new WaitForSeconds(titleHoldDuration);
            yield return Tween.Run(titleScaleDuration, Tween.InQuad, t => title.localScale = Vector3.one * (1f - t));
            Destroy(failText.gameObject);

            var body = Stretch(root, "Body");
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;

            // Body/Gradient: dark gradients at top and bottom.
            Image(Rect(body, "Overlay_Top", new Vector2(0, 1), Vector2.one, new Vector2(0, 40), new Vector2(60, 550), new Vector2(0.5f, 1)), skin.topGradient, Color.black);
            Image(Rect(body, "Overlay_Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, -40), new Vector2(60, 700), new Vector2(0.5f, 0)), skin.bottomGradient, Color.black);

            // Body/Content/Bottom_Container (500 high, bottom of the screen).
            var bottom = Rect(body, "Bottom_Container", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BottomContainerHeight), new Vector2(0.5f, 0));

            var snip = BigButton(bottom, "SNIP TAIL!", snipOffer.CurrentCost);
            snip.onClick.AddListener(() =>
            {
                if (!snipOffer.TryUse(_wallet, _session)) return;
                Close();
                if (_cameraFitter != null) _cameraFitter.FitDefault(boardRefitDuration);
            });

            var giveUp = SmallButton(bottom, "GIVE UP");
            giveUp.onClick.AddListener(ShowGameOver);

            if (_cameraFitter != null) _cameraFitter.FitBetween(BottomContainerHeight, 1920f - TopContainerHeight, boardRefitDuration);
            yield return Tween.Run(bodyFadeDuration, Tween.Linear, t => group.alpha = t);
            group.interactable = true;
        }

        private void TitleText(RectTransform rect, Material material)
        {
            var text = Text(rect, "NO WAY OUT!", skin.font, material, 146, Color.white);
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMin = 15;
            text.fontSizeMax = 146;
            text.characterSpacing = -2.7f;
        }

        private void ShowGameOver()
        {
            var root = Open("GameOver");
            Image(Stretch(root, "background overlay"), null, Hex("092334DB"), raycast: true);

            var card = Rect(root, "Popup Parent", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -101), new Vector2(0, 896));
            Image(Rect(card, "Background Main Color", Vector2.zero, Vector2.one, new Vector2(-3, 86), new Vector2(-105, -11)), skin.bannerFrame, Hex("D2E3FFFF"), sliced: true);
            Image(Rect(card, "SP_BG_Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-12, 58), new Vector2(600, 178)), skin.titleBanner, Color.white);
            Text(Rect(card, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-12, 51), new Vector2(848, 114)), "GAME OVER", skin.font, skin.blueUnderlayMaterial, 75.6f, Color.white);

            var content = Rect(card, "Popup Content", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 131), new Vector2(874, 509));
            var contentBackground = Rect(content, "Popup Content Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-80, 0));
            Image(Rect(contentBackground, "Color", new Vector2(0.03f, 0), new Vector2(0.97f, 1), new Vector2(0, -6), new Vector2(0, 12)), skin.bannerFrame, Color.white, sliced: true);
            var heartArea = Rect(content, "Content", Vector2.zero, Vector2.one, new Vector2(-3, 0), new Vector2(-129, 0));
            Image(Rect(heartArea, "SP_Heart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 19), new Vector2(488, 488)), skin.brokenHeart, Color.white);
            Text(Rect(content, "Sub text", new Vector2(0.11f, 0.10f), new Vector2(0.89f, 0.30f), new Vector2(1, -67), new Vector2(23, 0)), "LEVEL FAILED!", skin.font, skin.plainMaterial, 44.6f, Hex("446890FF"));

            var buttons = Rect(card, "Buttons Area", Vector2.zero, new Vector2(1, 0.36f), Vector2.zero, Vector2.zero);
            var tryAgainRect = Rect(buttons, "Try Again Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 155));
            var tryAgainImage = Image(tryAgainRect, skin.greenButton, Color.white, raycast: true);
            Text(Rect(tryAgainRect, "Text", new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.74f), new Vector2(4, 4), new Vector2(-9, 17)), "TRY AGAIN", skin.font, skin.greenCartoonMaterial, 58.7f, Hex("FDF8CAFF"));
            var tryAgain = MakeButton(tryAgainRect, tryAgainImage);
            tryAgain.onClick.AddListener(() => { Close(); _restart?.Invoke(); });

            StartCoroutine(Pop(card));
        }

        /// <summary>View_Button_Revive_Currency: 4 sliced layers, label + price.</summary>
        private Button BigButton(RectTransform parent, string label, int cost)
        {
            var rect = Rect(parent, "View_Button_Snip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 210));
            Image(Rect(rect, "Base", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16, 208)), skin.bigButton[0], Hex("399F00FF"), sliced: true);
            var front = Image(Rect(rect, "Front", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 12), new Vector2(-20, 180)), skin.bigButton[1], Hex("4FCD04FF"), sliced: true, raycast: true);
            Image(Rect(rect, "Highlight", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(1, -61), new Vector2(-34, 34)), skin.bigButton[2], Hex("78F024FF"), sliced: true);
            Image(Rect(rect, "Outline", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16, 208)), skin.bigButton[3], Hex("2D8700FF"), sliced: true);
            var texts = Rect(rect, "Tex_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-120, 210));
            Text(Rect(texts, "Text", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 43), new Vector2(-19, 66)), label, skin.font, skin.greenOutlineMaterial, 66, Color.white);
            Text(Rect(texts, "Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -23), new Vector2(300, 66)), cost.ToString(), skin.font, skin.greenOutlineMaterial, 66, Color.white);
            return MakeButton(rect, front);
        }

        /// <summary>View_Button_GiveUp: top-left of the bottom container, 0.5 scale, zero width so the
        /// 480-wide visual is centred on the pivot and half of it runs off the left edge (as on video).</summary>
        private Button SmallButton(RectTransform parent, string label)
        {
            var rect = Rect(parent, "View_Button_GiveUp", new Vector2(0, 1), new Vector2(0, 1), new Vector2(102.7f, -29.8f), new Vector2(0, 210), new Vector2(0, 1));
            rect.localScale = Vector3.one * 0.5f;
            Image(Rect(rect, "Underlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1, -8), new Vector2(466, 161)), skin.buttonUnderlay, Hex("00000040"));
            var visual = Rect(rect, "Visual_Container", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -3), new Vector2(480, 210));
            Image(Rect(visual, "Base", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(447, 147)), skin.smallButton[0], Hex("C20200FF"));
            var front = Image(Rect(visual, "Front", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(447, 129)), skin.smallButton[1], Hex("E8413EFF"), raycast: true);
            Image(Rect(visual, "Highlight", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(440, 32)), skin.smallButton[2], Hex("FF6262FF"));
            Image(Rect(visual, "Outline", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(449, 150)), skin.smallButton[3], Hex("BE0700FF"));
            Text(Rect(visual, "Text", Vector2.zero, Vector2.one, new Vector2(13, -2), new Vector2(-63, -107)), label, skin.font, skin.darkRedMaterial, 66, Color.white);
            return MakeButton(rect, front);
        }

        private static Button MakeButton(RectTransform rect, Graphic target)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            return button;
        }

        /// <summary>Appear like the original boxes/popups (OutBack 3); the exact popup feedback is stripped.</summary>
        private IEnumerator Pop(RectTransform rect)
        {
            var ease = Tween.OutBack(3f);
            yield return Tween.Run(popupAppearDuration, ease, t => { if (rect != null) rect.localScale = Vector3.one * t; });
        }

        private RectTransform Open(string name)
        {
            Close();
            var canvas = UiBuilder.Canvas(transform, name, 10);
            _open = canvas.gameObject;
            return canvas;
        }

        private void Close()
        {
            if (_open != null) Destroy(_open);
            _open = null;
        }
    }
}
