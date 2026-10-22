using System;
using System.Collections;
using SnackDash.Audio;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn thắng (prefab <c>Prefabs/UI/Popups/Victory</c>, gốc: VictoryPopup, đo trên LV2-3 và Lv11-15): nền đen hiện dần cùng VICTORY!,
    /// đống xu bật ra kèm pháo giấy và "+N", đống "combo" nhập vào đống lớn rồi số đếm lên, thanh NEW ITEM (khi có cơ chế sắp mở),
    /// nút x2 (miễn phí, nhân đôi) và CONTINUE. Có cơ chế vừa mở thì CONTINUE tắt trang thắng và bật trang UNLOCKED! ngay, rồi
    /// CONTINUE lần nữa [user].
    /// Các phần đều có sẵn trong prefab, script bật / tắt theo trận thắng. Mở qua <see cref="VictoryView"/>.
    /// </summary>
    public class VictoryPopup : BasePopup
    {
        [Header("Khung")]
        [SerializeField] private Image background;
        [SerializeField] private RectTransform title;
        [SerializeField] private UiButton continueButton;
        [Header("Xu, combo, NEW ITEM, x2")]
        [SerializeField] private RectTransform main;
        [SerializeField] private RectTransform coinItem;
        [SerializeField] private GameObject streakFlame;
        [SerializeField] private RectTransform glow;
        [SerializeField] private Image pile;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private RectTransform combo;
        [SerializeField] private TMP_Text comboAmount;
        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform barFill;
        [SerializeField] private Image featureIcon;
        [SerializeField] private UiButton doubleButton;
        [SerializeField] private Sprite smallPile, bigPile;
        [Header("UNLOCKED!")]
        [SerializeField] private RectTransform unlocked;
        [Tooltip("Dải tên và dòng PLAY NEXT LEVEL bật lên sau hình bấy nhiêu giây (video VFX_Unlock 15 fps) [user].")]
        [SerializeField] private RectTransform unlockedRibbon, unlockedHint;
        [SerializeField] private float ribbonDelay = 0.1f, hintDelay = 0.4f, partPopDuration = 0.2f;
        [SerializeField] private Image unlockedIcon;
        [SerializeField] private TMP_Text unlockedName;
        [Tooltip("Hình cơ chế (theo UnlockPopups.Mechanics) và booster (xẻng, boomerang, undo, nam châm).")]
        [SerializeField] private Sprite[] mechanicIcons = new Sprite[0];
        [SerializeField] private Sprite[] boosterIcons = new Sprite[4];
        [Header("Nhịp (đo trên LV2-3)")]
        [SerializeField] private float backgroundFade = 0.3f;
        [SerializeField] private float titlePopDuration = 0.35f;
        [SerializeField] private float continueDelay = 0.9f;
        [Header("Nhịp màn xu (gốc VictoryPopup: UIAnimation + WinScreenPopupView bản 1.15), giây tính từ lúc mở")]
        [Tooltip("Mọi phần bật ra: cỡ 0 -> 1 trong bấy nhiêu giây, OutBack (DOTween Ease 27).")]
        [SerializeField] private float popDuration = 0.3f;
        [Tooltip("VICTORY! bật ra tới cỡ này (gốc Title Content: scaleEnd 1.1).")]
        [SerializeField] private float titleScale = 1.1f;
        [SerializeField] private float titleDelay = 0.1f, coinsDelay = 0.3f, buttonsDelay = 0.8f;
        [SerializeField] private float comboAppear = 0.75f, comboScaleUp = 0.35f, comboHold = 0.45f, comboFly = 0.5f, comboMerge = 0.45f;
        [Tooltip("Tia sáng sau đống xu nảy lên rồi về khi xu combo nhập vào (gốc _comboFxPulseSeconds).")]
        [SerializeField] private float fxPulse = 0.4f;
        [SerializeField] private float barDelay = 1.5f, barDuration = 0.75f;
        [Tooltip("Tâm CONTINUE / x2 cách đáy màn hình (đơn vị canvas), đo trên video.")]
        [SerializeField] private float continueHeight = 380f;
        [SerializeField] private float buttonsSpread = 232f;

        [Header("Pháo giấy quanh xu thưởng")]
        [Tooltip("Prefab mẫu một mảnh (Prefabs/UI/Widgets/UiParticle): lấy từ kho tái dùng, không dựng bằng code.")]
        [SerializeField] private Image confettiPrefab;
        [Tooltip("Màu các mảnh pháo giấy, lần lượt.")]
        [SerializeField] private Color[] confettiColours =
        {
            new Color32(0xFF, 0x4D, 0x6D, 0xFF), new Color32(0xFF, 0xD2, 0x3F, 0xFF), new Color32(0x3E, 0xC1, 0xF3, 0xFF),
            new Color32(0x7C, 0xDB, 0x3A, 0xFF), new Color32(0xB0, 0x6B, 0xFF, 0xFF), new Color32(0xFF, 0x8A, 0x2B, 0xFF),
        };

        private UiPool _confetti;

        private Action _onContinue;
        private UnlockPopups.Entry? _unlock;
        private bool _unlockShown;
        private bool _doubled;
        private int _total;
        private VictoryReward _reward;

        protected override void OnInit()
        {
            continueButton.Clicked += OnContinue;
            doubleButton.Clicked += OnDouble;
        }

        public void Play(Action onContinue, UnlockPopups.Entry? unlockedEntry, VictoryReward? reward)
        {
            _onContinue = onContinue;
            _unlock = unlockedEntry;
            _unlockShown = !unlockedEntry.HasValue;
            StartCoroutine(Run(reward));
        }

        private IEnumerator Run(VictoryReward? reward)
        {
            var color = background.color;
            if (reward.HasValue) color.a = 1f; // màn xu đen hẳn (video)
            title.localScale = Vector3.zero;
            var buttonRect = (RectTransform)continueButton.transform;
            buttonRect.localScale = Vector3.zero;
            main.gameObject.SetActive(reward.HasValue);
            doubleButton.gameObject.SetActive(reward.HasValue);
            unlocked.gameObject.SetActive(false);
            // Đặt sẵn mọi phần trước khi màn hiện: prefab để chúng bật ở cỡ thật (có cả lửa streak) nên không làm trước thì
            // chúng hiện ra một thoáng trong lúc VICTORY! bật lên [user].
            if (reward.HasValue) PrepareReward(buttonRect, reward.Value);
            StartCoroutine(Tween.Run(backgroundFade, Tween.Linear, t => background.color = new Color(color.r, color.g, color.b, t * color.a)));
            if (reward.HasValue) StartCoroutine(RewardSequence(buttonRect, reward.Value)); // chạy song song với tiêu đề như gốc
            yield return new WaitForSeconds(titleDelay);
            yield return Tween.Run(popDuration, Pop, t => title.localScale = Vector3.one * (titleScale * t));
            if (reward.HasValue) yield break;
            yield return new WaitForSeconds(Mathf.Max(0f, buttonsDelay - titleDelay - popDuration));
            yield return Tween.Run(popDuration, Pop, t => buttonRect.localScale = Vector3.one * t);
        }

        /// <summary>DOTween Ease.OutBack (độ vượt mặc định 1.70158) như UIAnimation gốc.</summary>
        private static readonly Func<float, float> Pop = Tween.OutBack(1.70158f);

        private IEnumerator PopAfter(float delay, RectTransform part, float duration)
        {
            yield return new WaitForSeconds(delay);
            yield return Tween.Run(duration, Pop, t => part.localScale = Vector3.one * t);
        }

        private void OnContinue()
        {
            if (!_unlockShown)
            {
                // VICTORY! -> CONTINUE -> UNLOCKED! -> CONTINUE (rồi về Home như gốc) [user].
                _unlockShown = true;
                continueButton.Interactable = false; // bật lại khi UNLOCKED! đã hiện
                StartCoroutine(ShowUnlocked(_unlock.Value));
                return;
            }
            continueButton.Interactable = false;
            _onContinue?.Invoke();
        }

        // ------------------------------------------------------------------ xu, combo, NEW ITEM, x2

        /// <summary>
        /// Video Lv11-15 (10 fps) và user: đống xu bật ra kèm pháo giấy và "+20"; đống combo (+N) ở trên, rồi thanh NEW ITEM (chỉ khi
        /// có cơ chế sắp tới) và nút x2 / CONTINUE; đống combo rơi vào đống lớn và số đếm lên tổng.
        /// </summary>
        private bool _hasBar;
        private float _barFrom, _barTo;

        /// <summary>Lửa streak, đống xu, combo, thanh NEW ITEM, 2 nút: đúng trận này và thu về 0, chờ lần lượt bật ra.</summary>
        private void PrepareReward(RectTransform continueRect, VictoryReward reward)
        {
            _reward = reward;
            _total = reward.baseCoins + reward.comboCoins;
            streakFlame.SetActive(reward.onFire);
            pile.sprite = reward.comboCoins > 0 ? smallPile : bigPile; // đống nhỏ tới khi xu combo nhập vào [video]
            amount.text = Plus(reward.baseCoins);
            coinItem.localScale = Vector3.zero;
            combo.gameObject.SetActive(reward.comboCoins > 0);
            comboAmount.text = Plus(reward.comboCoins);
            combo.localScale = Vector3.zero;
            _hasBar = UnlockPopups.NewItemBar(reward.level, out _barFrom, out _barTo, out int icon);
            bar.gameObject.SetActive(_hasBar);
            if (_hasBar)
            {
                barFill.anchorMax = new Vector2(_barFrom, 1f);
                featureIcon.sprite = icon < mechanicIcons.Length ? mechanicIcons[icon] : null;
                bar.localScale = Vector3.zero;
            }
            ((RectTransform)doubleButton.transform).localScale = Vector3.zero;
            continueRect.anchoredPosition = new Vector2(buttonsSpread, continueHeight);
        }

        /// <summary>
        /// Nhịp gốc (giây từ lúc mở): đống xu + pháo giấy 0.3, thanh NEW ITEM + 2 nút 0.8, thanh chạy từ 1.5; combo hiện 0.75, giữ, bay
        /// vào đống lớn, nhập (đổi đống to, tia sáng nảy, số đếm lên tổng).
        /// </summary>
        private IEnumerator RewardSequence(RectTransform continueRect, VictoryReward reward)
        {
            float from = _barFrom, to = _barTo;
            var doubleRect = (RectTransform)doubleButton.transform;
            StartCoroutine(PopAfter(buttonsDelay, doubleRect, popDuration));
            StartCoroutine(PopAfter(buttonsDelay, continueRect, popDuration));
            if (_hasBar)
            {
                StartCoroutine(PopAfter(buttonsDelay, bar, popDuration));
                StartCoroutine(Later(barDelay, Tween.Run(barDuration, Tween.OutQuad, t => barFill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), 1f))));
            }

            yield return new WaitForSeconds(coinsDelay);
            StartCoroutine(Confetti(coinItem));
            StartCoroutine(Pulse(glow));
            StartCoroutine(Tween.Run(appearFlashSeconds, Tween.OutQuad, t => _impact = appearFlash * (1f - t)));
            StartCoroutine(Tween.Run(popDuration, Pop, t => coinItem.localScale = Vector3.one * t));
            if (!combo.gameObject.activeSelf) yield break;

            yield return new WaitForSeconds(comboAppear - coinsDelay);
            yield return Tween.Run(comboScaleUp, Pop, t => combo.localScale = Vector3.one * t);
            yield return new WaitForSeconds(comboHold);
            var start = combo.anchoredPosition;
            yield return Tween.Run(comboFly, Tween.InQuad, t =>
            {
                combo.anchoredPosition = Vector2.Lerp(start, coinItem.anchoredPosition, t);
                combo.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
            });
            combo.gameObject.SetActive(false);
            pile.sprite = bigPile;
            SoundPlayer.Play("CollectCoin");
            StartCoroutine(Tween.Run(fxPulse, Tween.Linear, t => _impact = 0.25f * Mathf.Sin(Mathf.PI * t)));
            yield return CountUp(reward.baseCoins, _total, comboMerge);
        }

        private static IEnumerator Later(float delay, IEnumerator then)
        {
            yield return new WaitForSeconds(delay);
            yield return then;
        }

        private static string Plus(int value) => "+ " + value; // gốc WinScreenPopupView.RewardFormat "+ {0}"

        /// <summary>x2 (nút quảng cáo, ở đây miễn phí): trả tổng thêm lần nữa, số đếm lên lại, x2 biến mất và CONTINUE vào giữa.</summary>
        private void OnDouble()
        {
            if (_doubled) return;
            _doubled = true;
            _reward.doubled?.Invoke();
            StartCoroutine(Double());
        }

        private IEnumerator Double()
        {
            var doubleRect = (RectTransform)doubleButton.transform;
            var continueRect = (RectTransform)continueButton.transform;
            StartCoroutine(Tween.Run(0.2f, Tween.InQuad, t => doubleRect.localScale = Vector3.one * (1f - t)));
            var start = continueRect.anchoredPosition;
            StartCoroutine(Tween.Run(0.3f, Tween.OutQuad, t => continueRect.anchoredPosition = Vector2.Lerp(start, new Vector2(0f, continueHeight), t)));
            SoundPlayer.Play("Coins");
            yield return CountUp(_total, _total * 2, 0.5f);
            doubleButton.gameObject.SetActive(false);
        }

        /// <summary>Số chạy từ <paramref name="from"/> tới <paramref name="to"/>, đống xu nảy lên.</summary>
        private IEnumerator CountUp(int from, int to, float duration)
        {
            yield return Tween.Run(duration, Tween.OutQuad, t =>
            {
                amount.text = Plus(Mathf.RoundToInt(Mathf.Lerp(from, to, t)));
                coinItem.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.PI * t));
            });
            amount.text = Plus(to);
        }

        private float _impact; // tia sáng nảy thêm khi combo nhập vào

        /// <summary>Tia sáng sau đống xu (FX_UI_Highlight_Rays_Golden gốc, tia tự quay trong <see cref="UnlockFx"/>): chỉ nảy khi combo nhập.</summary>
        private IEnumerator Pulse(RectTransform target)
        {
            _impact = 0f;
            var size = target.localScale;
            while (target != null && target.gameObject.activeInHierarchy)
            {
                target.localScale = size * (1f + _impact);
                yield return null;
            }
        }

        [Header("Pháo giấy (gốc FX_UI_Reward_Appear_2/FX_Confetti bản 1.15)")]
        [Tooltip("Nơi chứa mảnh pháo giấy: nằm cạnh đống xu, sau nó (không đè số +N) và không co theo đống xu lúc bật ra [user 22/10: pháo giấy lỗi].")]
        [SerializeField] private RectTransform confettiRoot;
        [Tooltip("Số mảnh bung một lần (gốc: burst 60).")]
        [SerializeField] private int confettiCount = 60;
        [Tooltip("Cỡ mảnh: ngang x dọc ngẫu nhiên (video lv61: sợi mảnh ~9 x 30 đơn vị).")]
        [SerializeField] private Vector2 confettiWidth = new Vector2(8f, 11f), confettiLength = new Vector2(22f, 32f);
        [Tooltip("Tốc độ đầu (đơn vị canvas / s) và lực cản: mảnh dừng ở bán kính ~ tốc độ / cản (video lv61: vòng ~220 khung đầu, ~300 lúc dừng).")]
        [SerializeField] private Vector2 confettiSpeed = new Vector2(1900f, 2300f);
        [SerializeField] private float confettiDrag = 7f, confettiGravity = 150f;
        [Tooltip("Mỗi mảnh sống ngẫu nhiên trong khoảng này rồi mờ đi (gốc startLifetime 0.5-1).")]
        [SerializeField] private Vector2 confettiLife = new Vector2(0.5f, 1f);
        [Tooltip("Quầng sáng chớp khi đống xu bật ra (gốc FX_Glow_Large 0.7 s): tia sáng phóng thêm rồi về.")]
        [SerializeField] private float appearFlash = 0.6f, appearFlashSeconds = 0.7f;

        /// <summary>Pháo giấy (hệ hạt UI trong gốc): các mảnh màu bung thành vòng rộng quanh đống xu rồi tắt dần.</summary>
        private IEnumerator Confetti(RectTransform around)
        {
            if (confettiPrefab == null || confettiColours.Length == 0) yield break;
            if (_confetti == null) _confetti = new UiPool(confettiPrefab, confettiRoot != null ? confettiRoot : around);
            var colours = confettiColours;
            int count = confettiCount;
            var pieces = new Image[count];
            var velocity = new Vector2[count];
            var spin = new float[count];
            var life = new float[count];
            for (int i = 0; i < count; i++)
            {
                pieces[i] = _confetti.Get();
                var rect = pieces[i].rectTransform;
                rect.sizeDelta = new Vector2(Random.Range(confettiWidth.x, confettiWidth.y), Random.Range(confettiLength.x, confettiLength.y));
                pieces[i].sprite = null;
                pieces[i].color = colours[i % colours.Length];
                float angle = i * Mathf.PI * 2f / count + Random.Range(-0.1f, 0.1f);
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(confettiSpeed.x, confettiSpeed.y);
                spin[i] = Random.Range(-540f, 540f);
                life[i] = Random.Range(confettiLife.x, confettiLife.y);
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            for (float t = 0f; t < confettiLife.y; t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    if (pieces[i] == null) continue;
                    velocity[i] *= Mathf.Exp(-confettiDrag * Time.deltaTime); // không về 0 khi một khung dài
                    velocity[i].y -= confettiGravity * Time.deltaTime;
                    pieces[i].rectTransform.anchoredPosition += velocity[i] * Time.deltaTime;
                    pieces[i].rectTransform.Rotate(0f, 0f, spin[i] * Time.deltaTime);
                    var c = colours[i % colours.Length];
                    pieces[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01((life[i] - t) / 0.25f)); // mờ trong 0.25 s cuối đời
                }
                yield return null;
            }
            foreach (var piece in pieces) _confetti.Release(piece);
        }

        // ------------------------------------------------------------------ UNLOCKED!

        /// <summary>UNLOCKED!: trang riêng sau màn thắng [user]. Bấm CONTINUE thì trang thắng (VICTORY!, xu, NEW ITEM, x2) tắt ngay trên
        /// nền đen và UNLOCKED! bật ra luôn — không thu nhỏ trang cũ (các phần thu về điểm neo trông như bị kéo lên) [user 19/10].</summary>
        private IEnumerator ShowUnlocked(UnlockPopups.Entry entry)
        {
            unlockedIcon.sprite = entry.booster ? boosterIcons[Mathf.Clamp(entry.icon, 0, boosterIcons.Length - 1)] : entry.icon < mechanicIcons.Length ? mechanicIcons[entry.icon] : null;
            unlockedName.text = entry.name + "!";
            unlocked.gameObject.SetActive(true);
            unlocked.localScale = Vector3.zero;
            var button = (RectTransform)continueButton.transform;
            title.localScale = Vector3.zero;
            button.localScale = Vector3.zero;
            main.gameObject.SetActive(false);
            doubleButton.gameObject.SetActive(false);
            button.anchoredPosition = new Vector2(0f, button.anchoredPosition.y);
            // Hình bật trước, dải tên theo sau, dòng PLAY NEXT LEVEL cuối cùng [video VFX_Unlock].
            if (unlockedRibbon != null) { unlockedRibbon.localScale = Vector3.zero; StartCoroutine(PopLater(unlockedRibbon, ribbonDelay)); }
            if (unlockedHint != null) { unlockedHint.localScale = Vector3.zero; StartCoroutine(PopLater(unlockedHint, hintDelay)); }
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => unlocked.localScale = Vector3.one * t);
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => button.localScale = Vector3.one * t);
            continueButton.Interactable = true;
        }

        private IEnumerator PopLater(RectTransform part, float delay)
        {
            yield return new WaitForSeconds(delay);
            yield return Tween.Run(partPopDuration, Tween.OutBack(3f), t => part.localScale = Vector3.one * t);
        }
    }
}
