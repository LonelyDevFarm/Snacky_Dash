using System;
using Random = UnityEngine.Random;
using System.Collections;
using SnackDash.Audio;
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
    /// <summary>What the win paid (already in the wallet) and what the screen shows.</summary>
    public struct VictoryReward
    {
        public int baseCoins, comboCoins;
        public bool onFire;     // win streak on fire: the pile burns, the coins are doubled
        public int level;       // the level won (NEW ITEM bar)
        public Action doubled;  // x2: pays the total once more
    }

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
        public void Show(Action onContinue, float extraDelay = 0f, UnlockPopups.Entry? unlocked = null, VictoryReward? reward = null) => StartCoroutine(Run(onContinue, extraDelay, unlocked, reward));

        private IEnumerator Run(Action onContinue, float extraDelay, UnlockPopups.Entry? unlocked, VictoryReward? reward)
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
            if (reward.HasValue) backgroundColor.a = 1f; // the coin screen is fully black (video)
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
            if (reward.HasValue)
            {
                yield return RewardSequence(root, buttonRect, reward.Value);
                yield break;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => buttonRect.localScale = Vector3.one * t);
        }

        // ------------------------------------------------------------------ coins, combo, NEW ITEM, x2

        /// <summary>
        /// Video Lv11-15 (10 fps) and the user: under VICTORY! the coin pile pops with confetti and "+20"; the
        /// "combo" pile (+N) comes above it, then the NEW ITEM bar (only when a mechanic is on its way) and
        /// the x2 / CONTINUE buttons; the combo pile drops into the big one and the number counts up to the
        /// total. x2 (the ad button, here free) doubles the total: the number counts up again, x2 goes and
        /// CONTINUE moves to the middle. Layout from VictoryPopup, placed as in the video.
        /// </summary>
        private IEnumerator RewardSequence(RectTransform root, RectTransform continueButton, VictoryReward reward)
        {
            int total = reward.baseCoins + reward.comboCoins;
            var top = Rect(root, "-----Main", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

            // RewardItem_Coins: the big pile (Coin_Icon_03), its glow, "+N"; the streak flame behind when on fire.
            var item = Rect(top, "RewardItem_Coins", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -901f), new Vector2(300f, 300f));
            if (reward.onFire)
                Image(Rect(item, "Streakflame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(9f, 60f), new Vector2(514f, 358f)), skin.Get("S_Flame_CoreStreak_0"), Color.white).preserveAspect = true;
            var glow = Rect(item, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f));
            var glowImage = glow.gameObject.AddComponent<RawImage>();
            glowImage.texture = skin.fxGlow;
            glowImage.color = new Color(1f, 0.78f, 0.2f, 0.75f);
            glowImage.raycastTarget = false;
            // A small stack (Coin_Icon_02) until the combo coins join it, then the big heap (Coin_Icon_03) [video].
            var pile = Image(Rect(item, "icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(270f, 270f)), skin.Get(reward.comboCoins > 0 ? "Coin_Icon_02_0" : "Coin_Icon_03_0"), Color.white);
            pile.preserveAspect = true;
            var amount = Text(Rect(item, "Reward Text (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, -116f), new Vector2(300f, 140f)), "+" + reward.baseCoins, skin.font, skin.redCartoonMaterial, 100f, Color.white);
            item.localScale = Vector3.zero;

            // ComboReward: "combo", the small pile (Coin_Icon_02), "+N".
            RectTransform combo = null;
            if (reward.comboCoins > 0)
            {
                combo = Rect(top, "ComboReward", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -642f), new Vector2(520f, 240f));
                Text(Rect(combo, "Combo_Label_Txt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 116f), new Vector2(300f, 64f)), "COMBO", skin.font, skin.redCartoonMaterial, 60f, Color.white);
                Image(Rect(combo, "ComboCoinIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f)), skin.Get("Coin_Icon_02_0"), Color.white).preserveAspect = true;
                Text(Rect(combo, "ComboAmount_Txt", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(240f, 110f)), "+" + reward.comboCoins, skin.font, skin.redCartoonMaterial, 90f, Color.white);
                combo.localScale = Vector3.zero;
            }

            // LDFeature ProgressBar: only while a mechanic is on its way.
            RectTransform bar = null;
            RectTransform fill = null;
            bool hasBar = UnlockPopups.NewItemBar(reward.level, out float from, out float to, out int icon);
            if (hasBar)
            {
                bar = Rect(top, "LDFeature_ProgressionBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -1284f), new Vector2(690f, 145f));
                var slider = Rect(bar, "Slider", new Vector2(0f, 0.17f), new Vector2(1f, 0.83f), new Vector2(-4.6f, -7.3f), new Vector2(-84.1f, -24.3f));
                Image(Rect(slider, "Background", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-13.3f, 0f)), skin.Get("ProgressBar_BG"), Color.white, sliced: true).gameObject.AddComponent<SlicedCapFit>();
                var area = Rect(slider, "Fill Area", new Vector2(0.021f, 0.12f), new Vector2(0.979f, 0.84f), new Vector2(0.9f, -1.8f), new Vector2(-1.8f, 10f));
                fill = Rect(area, "Fill", Vector2.zero, new Vector2(from, 1f), Vector2.zero, Vector2.zero);
                Image(fill, skin.Get("ProgressBar_ProgressPart"), Color.white, sliced: true).gameObject.AddComponent<SlicedCapFit>();
                var header = Rect(slider, "Header", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 15f), new Vector2(278.4f, 47.5f));
                Image(header, skin.Get("ProgressBar_Top"), Color.white, sliced: true).gameObject.AddComponent<SlicedCapFit>();
                Text(Stretch(header, "New Item_Txt"), "NEW ITEM", skin.font, skin.plainMaterial, 32f, new Color(0.541f, 0.145f, 0f));
                var sprite = skin.mechanicIcons != null && icon < skin.mechanicIcons.Length ? skin.mechanicIcons[icon] : null;
                Image(Rect(bar, "FeatureIcon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(272f, -7f), new Vector2(130f, 130f)), sprite, Color.white).preserveAspect = true;
                bar.localScale = Vector3.zero;
            }

            // Buttons: x2 on the left, CONTINUE on the right (video).
            var doubleButton = DoubleButton(root);
            doubleButton.localScale = Vector3.zero;
            continueButton.anchoredPosition = new Vector2(232f, continueHeight);
            CentreOnLetters(top);

            yield return Tween.Run(0.3f, Tween.OutBack(2.5f), t => item.localScale = Vector3.one * t);
            StartCoroutine(Confetti(item));
            StartCoroutine(Pulse(glow));
            yield return new WaitForSeconds(0.45f);
            if (combo != null) yield return Tween.Run(0.25f, Tween.OutBack(3f), t => combo.localScale = Vector3.one * t);
            yield return new WaitForSeconds(0.2f);
            StartCoroutine(Tween.Run(0.25f, Tween.OutBack(3f), t => doubleButton.localScale = continueButton.localScale = Vector3.one * t));
            if (bar != null)
            {
                yield return Tween.Run(0.25f, Tween.OutBack(3f), t => bar.localScale = Vector3.one * t);
                StartCoroutine(Tween.Run(0.6f, Tween.OutQuad, t => fill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), 1f)));
            }
            yield return new WaitForSeconds(0.4f);

            // The combo pile drops into the big one, then the number counts up to the total.
            if (combo != null)
            {
                var start = combo.anchoredPosition;
                yield return Tween.Run(0.35f, Tween.InQuad, t =>
                {
                    combo.anchoredPosition = Vector2.Lerp(start, item.anchoredPosition, t);
                    combo.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                });
                Destroy(combo.gameObject);
                pile.sprite = skin.Get("Coin_Icon_03_0");
                SoundPlayer.Play("CollectCoin");
                yield return CountUp(amount, item, reward.baseCoins, total);
            }

            var button = doubleButton.GetComponent<Button>();
            bool doubled = false;
            button.onClick.AddListener(() =>
            {
                if (doubled) return;
                doubled = true;
                reward.doubled?.Invoke();
                StartCoroutine(Double(amount, item, total, doubleButton, continueButton));
            });
        }

        private IEnumerator Double(TMPro.TMP_Text amount, RectTransform item, int total, RectTransform doubleButton, RectTransform continueButton)
        {
            StartCoroutine(Tween.Run(0.2f, Tween.InQuad, t => { if (doubleButton != null) doubleButton.localScale = Vector3.one * (1f - t); }));
            var start = continueButton.anchoredPosition;
            StartCoroutine(Tween.Run(0.3f, Tween.OutQuad, t => continueButton.anchoredPosition = Vector2.Lerp(start, new Vector2(0f, continueHeight), t)));
            SoundPlayer.Play("Coins");
            yield return CountUp(amount, item, total, total * 2);
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
        }

        /// <summary>The amount runs from from to to, the pile bumping.</summary>
        private static IEnumerator CountUp(TMPro.TMP_Text amount, RectTransform item, int from, int to)
        {
            yield return Tween.Run(0.5f, Tween.OutQuad, t =>
            {
                amount.text = "+" + Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                item.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.PI * t));
            });
            amount.text = "+" + to;
        }

        private static IEnumerator Pulse(RectTransform glow)
        {
            float time = 0f;
            while (glow != null)
            {
                time += Time.deltaTime;
                glow.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 3f));
                glow.Rotate(0f, 0f, 20f * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>The Fireworks burst (a UI particle system in the original): coloured strips fly out in a ring and fall.</summary>
        private static IEnumerator Confetti(RectTransform around)
        {
            Color[] colours = { Hex("FF4D6DFF"), Hex("FFD23FFF"), Hex("3EC1F3FF"), Hex("7CDB3AFF"), Hex("B06BFFFF"), Hex("FF8A2BFF") };
            const int Count = 46;
            var pieces = new RectTransform[Count];
            var velocity = new Vector2[Count];
            var spin = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                pieces[i] = Rect(around, "confetti", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Random.Range(14f, 22f), Random.Range(30f, 44f)));
                Image(pieces[i], null, colours[i % colours.Length]);
                float angle = i * Mathf.PI * 2f / Count + Random.Range(-0.1f, 0.1f);
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(700f, 1050f);
                spin[i] = Random.Range(-540f, 540f);
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (pieces[i] == null) continue;
                    velocity[i] *= 1f - 3.2f * Time.deltaTime;
                    velocity[i].y -= 600f * Time.deltaTime;
                    pieces[i].anchoredPosition += velocity[i] * Time.deltaTime;
                    pieces[i].Rotate(0f, 0f, spin[i] * Time.deltaTime);
                    var c = colours[i % colours.Length];
                    pieces[i].GetComponent<Image>().color = new Color(c.r, c.g, c.b, Mathf.Clamp01(2f - t * 1.3f));
                }
                yield return null;
            }
            foreach (var piece in pieces) if (piece != null) Destroy(piece.gameObject);
        }

        /// <summary>View_Button_WatchAd: the orange button with the clapper (Icon_RV), "X2" and a coin pile.</summary>
        private RectTransform DoubleButton(RectTransform root)
        {
            var rect = Rect(root, "View_Button_WatchAd", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-235f, continueHeight), new Vector2(410, 210));
            var visual = Rect(rect, "Visual_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 210));
            Image(Rect(visual, "Base", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), skin.bigButton[0], Hex("F39600FF"), sliced: true);
            var front = Image(Rect(visual, "Front", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 8.19f), new Vector2(-20.27f, 124.99f)), skin.bigButton[1], Hex("FFBF0DFF"), sliced: true, raycast: true);
            Image(Rect(visual, "Highlight", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.97f, -42.49f), new Vector2(-33.86f, 23.62f)), skin.bigButton[2], Hex("FFE24CFF"), sliced: true);
            Image(Rect(visual, "Outline", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), skin.bigButton[3], Hex("EF9400FF"), sliced: true);
            Image(Rect(visual, "RV icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 8f), new Vector2(86f, 86f)), skin.Get("Icon_RV"), Color.white).preserveAspect = true;
            Text(Rect(visual, "Value (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2f, 8f), new Vector2(120f, 80f)), "X2", skin.font, skin.greenOutlineMaterial, 66f, Color.white);
            Image(Rect(visual, "Coins", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(108f, 8f), new Vector2(90f, 90f)), skin.Get("Coin_Icon_02_0"), Color.white).preserveAspect = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = front;
            button.transition = Selectable.Transition.None;
            Pressable(rect);
            return rect;
        }

        /// <summary>
        /// UNLOCKED!: a page of its own after the win screen [user]: VICTORY!, the coins, the NEW ITEM bar and x2 all
        /// shrink away on the same black, then the mechanic's picture and CONTINUE alone in the middle.
        /// </summary>
        private IEnumerator ShowUnlocked(RectTransform root, RectTransform title, RectTransform button, UnlockPopups.Entry entry)
        {
            var feature = BuildUnlocked(root, entry);
            feature.localScale = Vector3.zero;
            var main = root.Find("-----Main");
            var x2 = root.Find("View_Button_WatchAd");
            yield return Tween.Run(titlePopDuration, Tween.Linear, t =>
            {
                title.localScale = Vector3.one * (1f - t);
                button.localScale = Vector3.one * (1f - t);
                if (main != null) main.localScale = Vector3.one * (1f - t);
                if (x2 != null) x2.localScale = Vector3.one * (1f - t);
            });
            if (main != null) main.gameObject.SetActive(false);
            if (x2 != null) x2.gameObject.SetActive(false);
            button.anchoredPosition = new Vector2(0f, button.anchoredPosition.y);
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
