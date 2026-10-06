using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// What each early level brings in, and the card popups that introduce it [IngredientUnlockConfig_FTUE_AB
    /// (v1.15, matches this project's level order), LocData ldf_*_name / ldf_*_desc, booster banners; user].
    /// A mechanic unlocks when the level before it is won ("UNLOCKED!" on the win screen) and its card
    /// ("NEW ITEM!", LdfTutorialPopup) shows when its level starts; a booster's card ("NEW BOOSTER!",
    /// BoosterTutorialPopup) shows when its level starts.
    /// </summary>
    public static class UnlockPopups
    {
        public struct Entry
        {
            public int level;      // the level it first appears on
            public bool booster;
            public int icon;       // skin.mechanicIcons index, or the booster (0 shovel, 1 boomerang, 2 undo, 3 magnet)
            public string name, description;
        }

        private const string Highlight = "#8929F6";

        public static readonly Entry[] All =
        {
            new Entry { level = 4, booster = true, icon = 0, name = "SHOVEL", description = $"<color={Highlight}>DIG A TUNNEL</color> TO ESCAPE!\n<color={Highlight}>CLICK A CIRCLE</color> TO SELECT YOUR EXIT POINT" },
            new Entry { level = 5, icon = 0, name = "BRIDGE", description = $"<color={Highlight}>CROSS THE ROAD</color> WITHOUT HITTING YOURSELF!" },
            new Entry { level = 7, booster = true, icon = 1, name = "BOOMERANG", description = $"<color={Highlight}>CLICK ARROW</color> TO DELIVER YOUR FRUITS!" },
            new Entry { level = 9, icon = 1, name = "GATE", description = $"YOU CAN PASS THEM ONLY THROUGH <color={Highlight}>ONE WAY!</color>" },
            new Entry { level = 11, booster = true, icon = 2, name = "UNDO", description = $"<color={Highlight}>REWINDS</color> YOUR LAST MOVE" },
            new Entry { level = 14, icon = 2, name = "TUNNEL", description = $"<color={Highlight}>DIVE</color> IN ONE SIDE, <color={Highlight}>LEAVE</color> FROM THE OTHER!" },
            new Entry { level = 16, booster = true, icon = 3, name = "MAGNET", description = $"<color={Highlight}>ATTRACT</color> NEARBY FRUITS TO THE CRATES FOR A FEW SWIPES!" },
            new Entry { level = 21, icon = 3, name = "WALL", description = $"<color={Highlight}>COMPLETE ORDERS</color> AND DESTROY THE BLOCKING WALLS!" },
            new Entry { level = 26, icon = 4, name = "MYSTERY FRUIT", description = $"COLLECT THE <color={Highlight}>MYSTERY FRUITS</color> TO REVEAL WHAT'S INSIDE!" },
            new Entry { level = 31, icon = 5, name = "ARROWS", description = $"MAKES THE ROAD ONE WAY, <color={Highlight}>CHANGES THE DIRECTION</color> WHEN YOU PASS!" },
        };

        private const int RayCount = 10;

        public static bool TryGet(int level, bool booster, out Entry entry)
        {
            foreach (var e in All)
                if (e.level == level && e.booster == booster) { entry = e; return true; }
            entry = default;
            return false;
        }

        public static Sprite Icon(UiSkin skin, Entry entry)
        {
            if (entry.booster)
                switch (entry.icon)
                {
                    case 0: return skin.shovelIcon;
                    case 1: return skin.boomerangIcon;
                    case 2: return skin.undoIcon;
                    default: return skin.magnetIcon;
                }
            return skin.mechanicIcons != null && entry.icon < skin.mechanicIcons.Length ? skin.mechanicIcons[entry.icon] : null;
        }

        /// <summary>
        /// LdfTutorialPopup / BoosterTutorialPopup laid out as the video shows them (canvas 1080 x 1920):
        /// title 12 % down, the pinned card 42 %, its ribbon 60 %, the speech bubble 70 %, "Tap to continue!"
        /// at -809. Returns the root (full screen, its Background takes the tap); "TapToContinue" starts hidden.
        /// </summary>
        public static RectTransform BuildCard(RectTransform canvas, UiSkin skin, Entry entry, float backgroundAlpha)
        {
            var root = Stretch(canvas, entry.booster ? "BoosterTutorialPopup" : "LdfTutorialPopup");
            Image(Stretch(root, "Background"), null, new Color(0.04f, 0.01f, 0.09f, backgroundAlpha), raycast: true); // a purple-black, as the video

            // Back Rays 02: purple rays turning slowly behind the card (lightray3; a particle system in the original).
            var rays = Rect(root, "Back Rays 02", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), Vector2.zero);
            rays.gameObject.AddComponent<CanvasGroup>();
            for (int i = 0; i < RayCount; i++)
            {
                var ray = Rect(rays, "ray", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 300f), new Vector2(0f, 0.5f));
                ray.localRotation = Quaternion.Euler(0f, 0f, i * 360f / RayCount);
                var image = ray.gameObject.AddComponent<RawImage>();
                image.texture = skin.fxRay;
                image.color = new Color(0.62f, 0.25f, 1f, 0.32f);
                image.raycastTarget = false;
            }
            // Chest Pop FX: a purple flash where the card pops out.
            var flash = Rect(root, "Chest Pop FX", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(420f, 420f));
            var flashImage = flash.gameObject.AddComponent<RawImage>();
            flashImage.texture = skin.fxGlow;
            flashImage.color = new Color(0.75f, 0.45f, 1f, 1f);
            flashImage.raycastTarget = false;

            var title = Text(Rect(root, "Header Text", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 730f), new Vector2(1000f, 240f)),
                entry.booster ? "NEW BOOSTER!" : "NEW ITEM!", skin.font, skin.plainMaterial, 105f, Color.white);
            // "Text (TMP) new item outline" (8D00B8) as a soft purple glow round white letters.
            title.fontMaterial.EnableKeyword("OUTLINE_ON");
            title.outlineWidth = 0.12f;
            title.outlineColor = new Color(0.45f, 0.2f, 0.85f);
            title.fontMaterial.EnableKeyword("UNDERLAY_ON");
            title.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.553f, 0f, 0.722f, 0.75f));
            title.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            title.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);

            // Card: a tinted card behind, tilted, the framed one on top with its pin (S_OnboardingScreen_*).
            var card = Rect(root, "Feature", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(620f, 560f));
            var back = Stretch(card, "Card Background (1)");
            Image(back, skin.cardFrameWhite, new Color(0.749f, 0.728f, 0.818f));
            back.localRotation = Quaternion.Euler(0f, 0f, 4f);
            back.anchoredPosition = new Vector2(-24f, 18f);
            Image(Stretch(card, "Card Background"), entry.booster ? skin.cardFrameGold : skin.cardFrameWhite, Color.white);
            var icon = Rect(card, "Card Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(500f, 400f));
            Image(icon, Icon(skin, entry), Color.white).preserveAspect = true;
            Image(Rect(card, "pin", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(58f, 126f)), skin.cardPin, Color.white).preserveAspect = true;

            var ribbon = Rect(root, "TutorialPopupRibbon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -188f), new Vector2(740f, 254f));
            Image(ribbon, skin.cardRibbon, Color.white).preserveAspect = true;
            var name = Text(Rect(ribbon, "Text (TMP) name", new Vector2(0.07f, 0.38f), new Vector2(0.93f, 0.62f), Vector2.zero, Vector2.zero),
                entry.name + "!", skin.font, skin.plainMaterial, 84.55f, Color.white);
            name.enableAutoSizing = true;
            name.fontSizeMin = 30f;
            name.fontSizeMax = 84.55f;

            var bubble = Rect(root, "Speech Bubble", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -388f), new Vector2(760f, 150f));
            Image(bubble, skin.speechBubble, Color.white, sliced: true).pixelsPerUnitMultiplier = 2f;
            Image(Rect(bubble, "triangle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-211f, 10f), new Vector2(42f, 40f)), skin.speechTriangle, Color.white);
            var description = Text(Rect(bubble, "Text (TMP) (1)", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-50f, -30f)),
                entry.description, skin.font, skin.plainMaterial, 48f, new Color(0.267f, 0.408f, 0.565f));
            description.enableWordWrapping = true;
            description.enableAutoSizing = true;
            description.fontSizeMin = 24f;
            description.fontSizeMax = 48f;
            description.richText = true;

            // sparkles / stars: little stars twinkling round the card.
            var sparkles = Rect(root, "sparkles", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), Vector2.zero);
            Vector2[] spots = { new Vector2(-330f, 230f), new Vector2(320f, 260f), new Vector2(-360f, -120f), new Vector2(350f, -60f), new Vector2(-150f, 330f), new Vector2(180f, -300f) };
            foreach (var spot in spots)
            {
                var star = Rect(sparkles, "star", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), spot, new Vector2(70f, 70f));
                var image = star.gameObject.AddComponent<RawImage>();
                image.texture = skin.fxSparkle;
                image.color = new Color(1f, 0.92f, 1f, 0.9f);
                image.raycastTarget = false;
            }

            var tap = Rect(root, "TapToContinue", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -809f), new Vector2(712f, 154f));
            var tapText = Text(tap, "TAP TO CONTINUE!", skin.font, skin.plainMaterial, 50f, Color.white);
            tapText.fontMaterial.EnableKeyword("OUTLINE_ON");
            tapText.outlineWidth = 0.25f;
            tapText.outlineColor = new Color32(40, 30, 60, 255);
            tap.gameObject.SetActive(false);
            return root;
        }

        /// <summary>
        /// How the card comes in (video, 5 fps): the screen darkens, NEW ITEM! flies in from the top right,
        /// a purple flash, the card pops out tilted and straightens, the rays turn up behind it, then the name
        /// on the ribbon and the speech bubble. About 1.6 s in all.
        /// </summary>
        public static IEnumerator Animate(RectTransform root, MonoBehaviour host)
        {
            var background = root.Find("Background").GetComponent<Image>();
            float alpha = background.color.a;
            var title = (RectTransform)root.Find("Header Text");
            var card = (RectTransform)root.Find("Feature");
            var ribbon = (RectTransform)root.Find("TutorialPopupRibbon");
            var name = (RectTransform)ribbon.Find("Text (TMP) name");
            var bubble = (RectTransform)root.Find("Speech Bubble");
            var rays = root.Find("Back Rays 02").GetComponent<CanvasGroup>();
            var flash = (RectTransform)root.Find("Chest Pop FX");
            var flashImage = flash.GetComponent<RawImage>();
            var sparkles = root.Find("sparkles");

            title.localScale = card.localScale = ribbon.localScale = bubble.localScale = flash.localScale = name.localScale = Vector3.zero;
            rays.alpha = 0f;
            sparkles.gameObject.SetActive(false);
            host.StartCoroutine(Spin((RectTransform)rays.transform));

            var color = background.color;
            yield return Tween.Run(0.2f, Tween.Linear, t => background.color = new Color(color.r, color.g, color.b, alpha * t));
            var titleEnd = title.anchoredPosition;
            var titleStart = titleEnd + new Vector2(260f, 80f);
            yield return Tween.Run(0.3f, Tween.OutBack(2f), t =>
            {
                title.anchoredPosition = Vector2.LerpUnclamped(titleStart, titleEnd, t);
                title.localScale = Vector3.one * t;
                title.localRotation = Quaternion.Euler(0f, 0f, 12f * (1f - t));
            });
            host.StartCoroutine(Tween.Run(0.4f, Tween.OutQuad, t =>
            {
                if (flash == null) return;
                flash.localScale = Vector3.one * (0.2f + 2f * t);
                flashImage.color = new Color(flashImage.color.r, flashImage.color.g, flashImage.color.b, 1f - t);
            }));
            yield return new WaitForSeconds(0.1f);
            yield return Tween.Run(0.35f, Tween.OutBack(2.5f), t =>
            {
                card.localScale = ribbon.localScale = Vector3.one * t;
                card.localRotation = Quaternion.Euler(0f, 0f, -15f * (1f - t));
            });
            sparkles.gameObject.SetActive(true);
            host.StartCoroutine(Twinkle(sparkles));
            host.StartCoroutine(Tween.Run(0.4f, Tween.Linear, t => { if (rays != null) rays.alpha = t; }));
            yield return new WaitForSeconds(0.3f);
            yield return Tween.Run(0.25f, Tween.OutBack(3f), t => name.localScale = Vector3.one * t);
            yield return Tween.Run(0.25f, Tween.OutBack(2f), t => bubble.localScale = Vector3.one * t);
        }

        private static IEnumerator Spin(RectTransform rays)
        {
            while (rays != null)
            {
                rays.Rotate(0f, 0f, -18f * Time.deltaTime);
                yield return null;
            }
        }

        private static IEnumerator Twinkle(Transform sparkles)
        {
            float time = 0f;
            while (sparkles != null)
            {
                time += Time.deltaTime;
                for (int i = 0; i < sparkles.childCount; i++)
                    sparkles.GetChild(i).localScale = Vector3.one * Mathf.Abs(Mathf.Sin(time * 2.2f + i * 1.3f));
                yield return null;
            }
        }
    }
}
