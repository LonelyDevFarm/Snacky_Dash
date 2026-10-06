using System;
using System.Collections;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Win screen, built in code from the original VictoryPopup's RectTransforms, sprites and font
    /// materials (instancing the decompiled popup prefabs crashes the editor). Timing measured on
    /// LV2-3 (10 fps): ~1.3 s after the last crate's flash the black background fades in with the
    /// title, the CONTINUE button pops in ~0.9 s later. Coin reward, combo and "new item" bar are left
    /// out until the coin economy exists.
    /// </summary>
    public class VictoryView : MonoBehaviour
    {
        [SerializeField] private UiSkin skin = new UiSkin();
        [Tooltip("Layout prefab (Prefabs/UI/Victory); built in code when empty.")]
        [SerializeField] private GameObject prefab;

        [Header("Timing (measured on LV2-3)")]
        [SerializeField] private float appearDelay = 1.3f;
        [SerializeField] private float backgroundFade = 0.3f;
        [SerializeField] private float titlePopDuration = 0.35f;
        [SerializeField] private float continueDelay = 0.9f;
        [Tooltip("CONTINUE centre above the screen bottom (canvas units), measured on the video.")]
        [SerializeField] private float continueHeight = 380f;

        /// <param name="extraDelay">Added to appearDelay when the last crate finishes later than usual.</param>
        public void Show(Action onContinue, float extraDelay = 0f, UnlockPopups.Entry? unlocked = null) => StartCoroutine(Run(onContinue, extraDelay, unlocked));

        private IEnumerator Run(Action onContinue, float extraDelay, UnlockPopups.Entry? unlocked)
        {
            yield return new WaitForSeconds(appearDelay + extraDelay);
            RectTransform root;
            if (prefab != null)
            {
                root = (RectTransform)Instantiate(prefab, transform).transform;
                root.name = "Victory";
            }
            else
            {
                root = UiBuilder.Canvas(transform, "Victory", 20);
                BuildVictory(root);
            }

            var background = root.Find("Background").GetComponent<Image>();
            var backgroundColor = background.color;
            var title = (RectTransform)root.Find("Header/Title");
            var button = root.Find("View_Button_Continue").GetComponent<Button>();
            var buttonRect = (RectTransform)button.transform;
            title.localScale = Vector3.zero;
            buttonRect.localScale = Vector3.zero;
            UiBuilder.Pressable(button.transform);
            bool unlockShown = !unlocked.HasValue;
            button.onClick.AddListener(() =>
            {
                if (!unlockShown)
                {
                    // VICTORY! -> CONTINUE -> UNLOCKED! -> CONTINUE (then Home in the original) [user].
                    unlockShown = true;
                    button.interactable = false; // back on once UNLOCKED! is up
                    StartCoroutine(ShowUnlocked(root, title, buttonRect, unlocked.Value));
                    return;
                }
                button.interactable = false;
                onContinue?.Invoke();
            });

            StartCoroutine(Tween.Run(backgroundFade, Tween.Linear, t => background.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, t * backgroundColor.a)));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => title.localScale = Vector3.one * t);
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => buttonRect.localScale = Vector3.one * t);
        }

        /// <summary>UNLOCKED!: the next level brings a mechanic; its picture takes the title's place [user].</summary>
        private IEnumerator ShowUnlocked(RectTransform root, RectTransform title, RectTransform button, UnlockPopups.Entry entry)
        {
            var feature = BuildUnlocked(root, entry);
            feature.localScale = Vector3.zero;
            yield return Tween.Run(titlePopDuration, Tween.Linear, t =>
            {
                title.localScale = Vector3.one * (1f - t);
                button.localScale = Vector3.one * (1f - t);
            });
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => feature.localScale = Vector3.one * t);
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => button.localScale = Vector3.one * t);
            button.GetComponent<Button>().interactable = true;
        }

        /// <summary>
        /// "Unlocked!" (VictoryPopup Unlocked_Txt, 90), the mechanic's picture on a glow and its name on the
        /// green ribbon (S_Ribbon_Green 599 x 161), placed as on the user's screenshot (title 21 % down,
        /// picture 49 %, ribbon 61 %). The x2 video button is left out (no ads).
        /// </summary>
        private RectTransform BuildUnlocked(RectTransform root, UnlockPopups.Entry entry)
        {
            var feature = Rect(root, "Feautre Unlocked", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text(Rect(feature, "Unlocked_Txt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 555f), new Vector2(700f, 160f)),
                "UNLOCKED!", skin.font, skin.redCartoonMaterial, 110f, Color.white);
            Image(Rect(feature, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(560f, 560f)), skin.glow, new Color(1f, 0.78f, 0.25f, 0.6f));
            Image(Rect(feature, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(470f, 360f)), UnlockPopups.Icon(skin, entry), Color.white).preserveAspect = true;
            var ribbon = Rect(feature, "Ribbon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -212f), new Vector2(599f, 161f));
            Image(ribbon, skin.ribbonGreen, Color.white);
            var name = Text(Rect(ribbon, "LDFeature_Txt (TMP)", new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.75f), Vector2.zero, Vector2.zero), entry.name + "!", skin.font, skin.greenOutlineMaterial, 55f, Color.white);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = 55f;
            return feature;
        }

        /// <summary>Layout of the win screen (saved as Prefabs/UI/Victory by SnackDash/Setup/Create UI Prefabs).</summary>
        public void BuildVictory(RectTransform root)
        {
            // Background: black at alpha FD over everything (fades in from transparent).
            Image(Stretch(root, "Background"), null, new Color(0f, 0f, 0f, 0.992f), raycast: true);

            // -----Header (top, 406 down, 600 x 330) / TitleCurvy_Txt with its inner shadow and outline layers.
            var header = Rect(root, "Header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -406), new Vector2(600, 330));
            var title = Rect(header, "Title", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(96.47f, 232.45f));
            Text(Stretch(title, "TitleCurvy_Txt"), "VICTORY!", skin.font, skin.redCartoonMaterial, 145, Hex("840000FF"));
            Text(Rect(title, "Title_InnerShdw_Txt", Vector2.zero, Vector2.one, new Vector2(0, -9.1f), Vector2.zero), "VICTORY!", skin.font, skin.redCartoonInnerMaterial, 145, Hex("840101FF"));
            Text(Rect(title, "Title_CurvyOutline_Txt", Vector2.zero, Vector2.one, new Vector2(0, -10.3f), Vector2.zero), "VICTORY!", skin.font, skin.redCartoonMaterial, 145, Hex("FEFEFEFF"));
            ContinueButton(root);
        }

        /// <summary>View_Button_Continue: 410 x 210, four sliced green layers and a "continue" label.</summary>
        private Button ContinueButton(RectTransform root)
        {
            var rect = Rect(root, "View_Button_Continue", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, continueHeight), new Vector2(410, 210));
            var visual = Rect(rect, "Visual_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 210));
            Image(Rect(visual, "Base", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), skin.bigButton[0], Hex("399F00FF"), sliced: true);
            var front = Image(Rect(visual, "Front", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 8.19f), new Vector2(-20.27f, 124.99f)), skin.bigButton[1], Hex("4FCD04FF"), sliced: true, raycast: true);
            Image(Rect(visual, "Highlight", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.97f, -42.49f), new Vector2(-33.86f, 23.62f)), skin.bigButton[2], Hex("78F024FF"), sliced: true);
            Image(Rect(visual, "Outline", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), skin.bigButton[3], Hex("2D8700FF"), sliced: true);
            var texts = Rect(visual, "Tex_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-119.5f, 210));
            Text(Rect(texts, "Text (TMP)", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 66)), "CONTINUE", skin.font, skin.greenOutlineMaterial, 65.6f, Color.white);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = front;
            button.transition = Selectable.Transition.None;
            return button;
        }
    }
}
