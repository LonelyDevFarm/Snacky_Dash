using System;
using System.Collections;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Lose flow as recorded on the original (video Man_Lose): biting the tail shows "NO WAY OUT!"
    /// with SNIP TAIL (pay to continue) and GIVE UP; giving up offers one more snip (LosePopup),
    /// whose cancel button shows GAME OVER / TRY AGAIN, which restarts the level.
    /// Each popup's layout comes from a prefab in Prefabs/UI (made once by SnackDash/Setup/Create UI
    /// Prefabs from the Build* methods here, then free to edit by hand); without one the layout is
    /// built in code. The Show* methods find the parts by name and add texts, buttons and motion.
    /// Layout values come from the original LoseInBoardPopup / LosePopup / LoseScreenFailedPopup
    /// RectTransforms and phone screenshots. Snip costs come from SnipOffer (0 coins for now).
    /// </summary>
    public class LoseFlowView : MonoBehaviour
    {
        [Serializable]
        public class Prefabs
        {
            public GameObject noWayOut;
            public GameObject secondChance;
            public GameObject gameOver;
        }

        [SerializeField] private UiSkin skin = new UiSkin();
        [SerializeField] private Prefabs prefabs = new Prefabs();
        [SerializeField] private SnipOffer snipOffer = new SnipOffer();
        [SerializeField] private float popupAppearDuration = 0.35f;

        [Header("NO WAY OUT (AppearDissappear feedback of LoseInBoardPopup)")]
        [SerializeField] private float titleScaleDuration = 0.2f;
        [SerializeField] private float titleHoldDuration = 0.75f;
        [SerializeField] private float bodyFadeDuration = 0.2f;
        [Tooltip("Board zoom-out behind the buttons; ~0.33 s measured on Man_Lose (30 fps), easing not known.")]
        [SerializeField] private float boardRefitDuration = 0.35f;
        [Tooltip("Second-chance popup: half a fade cycle of the tail part to cut (~2 s per cycle on Man_Lose).")]
        [SerializeField] private float secondChanceBlinkHalf = 1f;

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

        // ---------------------------------------------------------------- NO WAY OUT

        private void ShowNoWayOut() => StartCoroutine(NoWayOut());

        /// <summary>
        /// FailText AppearDissappear feedback: title scales in 0.2 s, holds 0.75 s, scales out 0.2 s;
        /// then the body (gradients + buttons) fades in while the board is refitted above the buttons.
        /// </summary>
        private IEnumerator NoWayOut()
        {
            var root = Open(prefabs.noWayOut, "NoWayOut", BuildNoWayOut);
            var failText = root.Find("FailText");
            var title = (RectTransform)root.Find("FailText/Title_Text");
            var group = root.Find("Body").GetComponent<CanvasGroup>();
            SetGroup(group, 0f, false);

            SetText(root, "Body/Bottom_Container/View_Button_Snip/Tex_Container/Value", snipOffer.CurrentCost.ToString());
            OnClick(root, "Body/Bottom_Container/View_Button_Snip", Snip);
            OnClick(root, "Body/Bottom_Container/View_Button_GiveUp", ShowSecondChance);

            failText.gameObject.SetActive(true);
            title.localScale = Vector3.zero;
            yield return Tween.Run(titleScaleDuration, Tween.OutBack(3f), t => { if (title != null) title.localScale = Vector3.one * t; });
            yield return new WaitForSeconds(titleHoldDuration);
            yield return Tween.Run(titleScaleDuration, Tween.InQuad, t => { if (title != null) title.localScale = Vector3.one * (1f - t); });
            if (failText == null) yield break;
            failText.gameObject.SetActive(false);

            if (_cameraFitter != null) _cameraFitter.FitBetween(BottomContainerHeight, 1920f - TopContainerHeight, boardRefitDuration);
            yield return Tween.Run(bodyFadeDuration, Tween.Linear, t => { if (group != null) group.alpha = t; });
            if (group != null) SetGroup(group, 1f, true);
        }

        /// <summary>Layout of the NO WAY OUT screen (FailText title + Body with the two buttons).</summary>
        public void BuildNoWayOut(RectTransform root)
        {
            // FailText (stretch, -56 x -1408) / Title_Text: auto-size 15..146, wrapped, spacing -2.7.
            // On a tall phone the canvas is narrower than 1080, so the title wraps to two lines.
            var failText = Rect(root, "FailText", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-56, -1408));
            var title = Rect(failText, "Title_Text", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 54.66f), new Vector2(-36.46f, 333.84f));
            TitleText(Stretch(title, "Back"), skin.titleBackMaterial);
            TitleText(Stretch(title, "Front"), skin.titleFrontMaterial);

            var body = Stretch(root, "Body");
            body.gameObject.AddComponent<CanvasGroup>();
            // Body/Gradient: dark gradients at top and bottom.
            Image(Rect(body, "Overlay_Top", new Vector2(0, 1), Vector2.one, new Vector2(0, 40), new Vector2(60, 550), new Vector2(0.5f, 1)), skin.topGradient, Color.black);
            Image(Rect(body, "Overlay_Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0, -40), new Vector2(60, 700), new Vector2(0.5f, 0)), skin.bottomGradient, Color.black);
            // Body/Content/Bottom_Container (500 high, bottom of the screen).
            var bottom = Rect(body, "Bottom_Container", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BottomContainerHeight), new Vector2(0.5f, 0));
            BigButton(bottom, "SNIP TAIL!");
            SmallButton(bottom, "GIVE UP");
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

        private void Snip()
        {
            if (!snipOffer.TryUse(_wallet, _session)) return;
            Close();
            if (_cameraFitter != null) _cameraFitter.FitDefault(boardRefitDuration);
        }

        // ---------------------------------------------------------------- Second chance

        /// <summary>
        /// LosePopup (after GIVE UP, video Man_Lose): one more chance to snip. PLAY ON pays the snip
        /// price and plays on; the cancel button goes to GAME OVER. Holding the dimmed background
        /// hides the popup to see the board (TMP_See_The_Board).
        /// </summary>
        private void ShowSecondChance()
        {
            var root = Open(prefabs.secondChance, "LosePopup", BuildSecondChance);
            var card = (RectTransform)root.Find("Popup Parent");

            var price = (RectTransform)root.Find("Popup Parent/PlayOn Revive Button/Coin Text (TMP)");
            var priceText = price.GetComponent<TMP_Text>();
            priceText.text = snipOffer.CurrentCost.ToString();
            price.sizeDelta = new Vector2(priceText.preferredWidth, price.sizeDelta.y); // the original fits the rect to the text
            OnClick(root, "Popup Parent/PlayOn Revive Button", Snip);
            OnClick(root, "Popup Parent/Cancel Button", ShowGameOver);

            root.Find("background overlay").GetComponent<HoldToHide>().Target = card.GetComponent<CanvasGroup>();
            var fruits = root.Find("Popup Parent/Popup Content Common/Content/SP_Fruits");
            if (fruits != null) StartCoroutine(Blink(fruits.GetComponent<Graphic>()));
            StartCoroutine(Pop(card));
        }

        /// <summary>Layout of the second-chance popup (LosePopup without the ad button and the offer).</summary>
        public void BuildSecondChance(RectTransform root)
        {
            var overlay = Image(Stretch(root, "background overlay"), null, Hex("092334DB"), raycast: true);
            overlay.gameObject.AddComponent<HoldToHide>();

            var card = Rect(root, "Popup Parent", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 79.4f), new Vector2(0, 895.66f));
            card.gameObject.AddComponent<CanvasGroup>();
            Image(Rect(card, "Background Main Color", Vector2.zero, Vector2.one, new Vector2(0, 115), new Vector2(-100, -70)), skin.bannerFrame, Hex("D2E3FFFF"), sliced: true);
            Image(Rect(card, "SP_BG_Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-12, 58), new Vector2(600, 178)), skin.titleBanner, Color.white);
            Text(Rect(card, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-16, 60), new Vector2(575, 108)), "NO WAY OUT!", skin.font, skin.blueUnderlayMaterial, 72, Color.white);

            var content = Rect(card, "Popup Content Common", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 171), new Vector2(828, 509.45f));
            var contentBackground = Rect(content, "Popup Content Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-80, 0));
            Image(Rect(contentBackground, "Color", new Vector2(0.0285f, 0), new Vector2(0.9715f, 1), new Vector2(0, -21.08f), new Vector2(0, -18.93f)), skin.bannerFrame, Color.white, sliced: true);
            var art = Rect(content, "Content", Vector2.zero, Vector2.one, new Vector2(-2.84f, 0), new Vector2(-129.29f, 0));
            SnakeIllustration(art);
            var sub = Text(Rect(content, "Sub text", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26.52f), new Vector2(686, 53.05f)),
                "<color=#FF4945>SHORTEN THE TAIL</color> TO KEEP PLAYING!", skin.font, skin.plainMaterial, 42, Hex("446890FF"));
            sub.richText = true;

            // Phone screenshot of the original: the button sits inside the card, ~37 units under the
            // white box and ~31 above the card's bottom, drawn ~0.85x (its layout group scales it).
            var playOnRect = Rect(card, "PlayOn Revive Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -205), new Vector2(350, 155));
            playOnRect.localScale = Vector3.one * 0.85f;
            var playOnImage = Image(playOnRect, skin.greenButton, Color.white, raycast: true);
            // Centred on the button face (the thick bottom rim puts the face ~6 units above centre):
            // the label above, the coin and price below, the coin + price pair centred as one.
            Text(Rect(playOnRect, "Text (TMP)", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 30), new Vector2(0, 60)), "PLAY ON", skin.font, skin.greenCartoonMaterial, 52.87f, Hex("FDF8CAFF"));
            var price = Rect(playOnRect, "Coin Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(25.55f, -22), Vector2.zero);
            Text(price, "0", skin.font, skin.greenCartoonMaterial, 52, Hex("FDF8CAFF"));
            Image(Rect(price, "Generic Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(-25.55f, 3.48f), new Vector2(51.1f, 58.97f)), skin.coinIcon, Color.white).preserveAspect = true;
            MakeButton(playOnRect, playOnImage);

            var cancelRect = Rect(card, "Cancel Button", Vector2.one, Vector2.one, new Vector2(-95.1f, 69), new Vector2(190.19f, 200));
            var cancelImage = Image(Rect(cancelRect, "cancel icon", new Vector2(0.32f, 0.218f), new Vector2(0.838f, 0.8f), new Vector2(13.6f, -1.73f), Vector2.zero), skin.cancelButton, Color.white, raycast: true);
            MakeButton(cancelRect, cancelImage);

            Text(Rect(root, "TMP_See_The_Board", Vector2.zero, new Vector2(1, 0), new Vector2(0, 120.12f), new Vector2(0, 240.23f)),
                "HOLD TO SEE THE BOARD", skin.font, skin.blueUnderlayMaterial, 46.9f, Color.white);
        }

        /// <summary>
        /// The snake (head and the first tiles, SP_Snake) with the rest of its tail (SP_Fruits) carrying
        /// on its top row, as in the footage. Both sprites come from one atlas page at the same pixel
        /// scale; the original prefab draws them at different scales (0.77 vs 1.0) because its runtime
        /// layout is not recoverable, so both are drawn at one tile scale and fitted into the box.
        /// </summary>
        private void SnakeIllustration(RectTransform area)
        {
            var snake = skin.snakeIllustration;
            var fruits = skin.fruitsIllustration;
            if (snake == null || fruits == null) return;

            // Pixel layout: the snake from (0,0); its top row is the upper quarter (4 tiles tall).
            // The fruit strip starts at the snake's right edge; its sprite pivot marks the bottom of
            // its tile row, which sits on the bottom of the snake's top row. Its tiles are drawn as
            // wide as the snake's (3 tiles across SP_Snake, 4 across SP_Fruits), raised 20 px.
            float tileMatch = (snake.rect.width / 3f) / (fruits.rect.width / 4f);
            Vector2 snakeSize = snake.rect.size, fruitSize = fruits.rect.size * tileMatch;
            var fruitMin = new Vector2(snakeSize.x, snakeSize.y * 3f / 4f - fruits.pivot.y * tileMatch + 20f);
            // Fit on the visible tiles: the whole width and the snake's height (the strip's sprite has
            // transparent room above its fruit, which must not shrink the picture).
            var max = new Vector2(fruitMin.x + fruitSize.x, snakeSize.y);

            const float captionSpace = 90f; // keep the bottom caption clear
            const float margin = 40f;
            var box = new Vector2(area.rect.width - margin, area.rect.height - captionSpace - margin);
            float scale = Mathf.Min(box.x / max.x, box.y / max.y);
            // Centred in the room above the caption, a little low so the fruit stems stay inside the box.
            var origin = -max * 0.5f * scale + new Vector2(0, captionSpace * 0.5f - margin * 0.5f);

            void Place(string name, Sprite sprite, Vector2 pixelMin, Vector2 pixelSize)
            {
                var rect = Rect(area, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    origin + (pixelMin + pixelSize * 0.5f) * scale, pixelSize * scale);
                Image(rect, sprite, Color.white);
            }
            Place("SP_Snake", snake, Vector2.zero, snakeSize);
            Place("SP_Fruits", fruits, fruitMin, fruitSize);
        }

        /// <summary>
        /// The tail part a snip would cut fades out and back in, over and over (Man_Lose at 3 fps:
        /// about 2 s per cycle): fully out, then back in [user].
        /// </summary>
        private IEnumerator Blink(Graphic graphic)
        {
            while (graphic != null)
            {
                yield return Tween.Run(secondChanceBlinkHalf, Tween.InOutSine, t => { if (graphic != null) graphic.color = new Color(1f, 1f, 1f, 1f - t); });
                yield return Tween.Run(secondChanceBlinkHalf, Tween.InOutSine, t => { if (graphic != null) graphic.color = new Color(1f, 1f, 1f, t); });
            }
        }

        // ---------------------------------------------------------------- Game over

        private void ShowGameOver()
        {
            var root = Open(prefabs.gameOver, "GameOver", BuildGameOver);
            OnClick(root, "Popup Parent/Try Again Button", () => { Close(); _restart?.Invoke(); });
            StartCoroutine(Pop((RectTransform)root.Find("Popup Parent")));
        }

        /// <summary>Layout of GAME OVER (LoseScreenFailedPopup).</summary>
        public void BuildGameOver(RectTransform root)
        {
            Image(Stretch(root, "background overlay"), null, Hex("092334DB"), raycast: true);

            var card = Rect(root, "Popup Parent", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -101), new Vector2(0, 896));
            Image(Rect(card, "Background Main Color", Vector2.zero, Vector2.one, new Vector2(-3, 86), new Vector2(-105, -11)), skin.bannerFrame, Hex("D2E3FFFF"), sliced: true);
            Image(Rect(card, "SP_BG_Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-12, 58), new Vector2(600, 178)), skin.titleBanner, Color.white);
            Text(Rect(card, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-12, 51), new Vector2(848, 114)), "GAME OVER", skin.font, skin.blueUnderlayMaterial, 75.6f, Color.white);

            var content = Rect(card, "Popup Content", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 131), new Vector2(874, 509));
            var contentBackground = Rect(content, "Popup Content Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-80, 0));
            Image(Rect(contentBackground, "Color", new Vector2(0.03f, 0), new Vector2(0.97f, 1), new Vector2(0, -6), new Vector2(0, 12)), skin.bannerFrame, Color.white, sliced: true);
            var heartArea = Rect(content, "Content", Vector2.zero, Vector2.one, new Vector2(-3, 0), new Vector2(-129, 0));
            // Phone screenshot: the heart draws ~370 units tall, wholly above "LEVEL FAILED!" (the
            // prefab's 488 overlaps the caption here).
            Image(Rect(heartArea, "SP_Heart", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(370, 370)), skin.brokenHeart, Color.white).preserveAspect = true;
            Text(Rect(content, "Sub text", new Vector2(0.11f, 0.10f), new Vector2(0.89f, 0.30f), new Vector2(1, -67), new Vector2(23, 0)), "LEVEL FAILED!", skin.font, skin.plainMaterial, 44.6f, Hex("446890FF"));

            // Phone screenshot: TRY AGAIN sits inside the card, ~46 units above its bottom (474 x 135 drawn).
            var tryAgainRect = Rect(card, "Try Again Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -243), new Vector2(474, 135));
            var tryAgainImage = Image(tryAgainRect, skin.greenButton, Color.white, raycast: true);
            // Centred on the button face (~6 units above the sprite centre, see PLAY ON).
            Text(Rect(tryAgainRect, "Text", Vector2.zero, Vector2.one, new Vector2(0, 6), Vector2.zero), "TRY AGAIN", skin.font, skin.greenCartoonMaterial, 58.7f, Hex("FDF8CAFF"));
            MakeButton(tryAgainRect, tryAgainImage);
        }

        // ---------------------------------------------------------------- Shared parts

        /// <summary>View_Button_Revive_Currency: 4 sliced layers, label + price ("Value").</summary>
        private void BigButton(RectTransform parent, string label)
        {
            var rect = Rect(parent, "View_Button_Snip", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 210));
            Image(Rect(rect, "Base", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16, 208)), skin.bigButton[0], Hex("399F00FF"), sliced: true);
            var front = Image(Rect(rect, "Front", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 12), new Vector2(-20, 180)), skin.bigButton[1], Hex("4FCD04FF"), sliced: true, raycast: true);
            Image(Rect(rect, "Highlight", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(1, -61), new Vector2(-34, 34)), skin.bigButton[2], Hex("78F024FF"), sliced: true);
            Image(Rect(rect, "Outline", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16, 208)), skin.bigButton[3], Hex("2D8700FF"), sliced: true);
            var texts = Rect(rect, "Tex_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-120, 210));
            Text(Rect(texts, "Text", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 43), new Vector2(-19, 66)), label, skin.font, skin.greenOutlineMaterial, 66, Color.white);
            Text(Rect(texts, "Value", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -23), new Vector2(300, 66)), "0", skin.font, skin.greenOutlineMaterial, 66, Color.white);
            MakeButton(rect, front);
        }

        /// <summary>View_Button_GiveUp: top-left of the bottom container, 0.5 scale, zero width so the
        /// 480-wide visual is centred on the pivot and half of it runs off the left edge (as on video).</summary>
        private void SmallButton(RectTransform parent, string label)
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
            MakeButton(rect, front);
        }

        private static void MakeButton(RectTransform rect, Graphic target)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
        }

        private static void OnClick(Transform root, string path, Action action)
        {
            var button = root.Find(path)?.GetComponent<Button>();
            if (button == null) { Debug.LogWarning($"[UI] No button at {root.name}/{path}"); return; }
            button.onClick.AddListener(() => action());
        }

        private static void SetText(Transform root, string path, string text)
        {
            var label = root.Find(path)?.GetComponent<TMP_Text>();
            if (label != null) label.text = text;
        }

        private static void SetGroup(CanvasGroup group, float alpha, bool interactive)
        {
            group.alpha = alpha;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        /// <summary>Appear like the original boxes/popups (OutBack 3); the exact popup feedback is stripped.</summary>
        private IEnumerator Pop(RectTransform rect)
        {
            var ease = Tween.OutBack(3f);
            yield return Tween.Run(popupAppearDuration, ease, t => { if (rect != null) rect.localScale = Vector3.one * t; });
        }

        /// <summary>The popup from its prefab, or built in code when there is none.</summary>
        private RectTransform Open(GameObject prefab, string name, Action<RectTransform> build)
        {
            Close();
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, transform).transform;
                root.name = name;
            }
            else
            {
                root = UiBuilder.Canvas(transform, name, 10);
                build(root);
            }
            _open = root.gameObject;
            return root;
        }

        private void Close()
        {
            if (_open != null) Destroy(_open);
            _open = null;
        }
    }
}
