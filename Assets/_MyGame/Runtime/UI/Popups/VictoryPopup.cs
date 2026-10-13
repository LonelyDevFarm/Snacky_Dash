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
    /// nút x2 (miễn phí, nhân đôi) và CONTINUE. Có cơ chế vừa mở thì CONTINUE chuyển sang trang UNLOCKED! rồi CONTINUE lần nữa [user].
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
        [SerializeField] private Image unlockedIcon;
        [SerializeField] private TMP_Text unlockedName;
        [Tooltip("Hình cơ chế (theo UnlockPopups.Mechanics) và booster (xẻng, boomerang, undo, nam châm).")]
        [SerializeField] private Sprite[] mechanicIcons = new Sprite[0];
        [SerializeField] private Sprite[] boosterIcons = new Sprite[4];
        [Header("Nhịp (đo trên LV2-3)")]
        [SerializeField] private float backgroundFade = 0.3f;
        [SerializeField] private float titlePopDuration = 0.35f;
        [SerializeField] private float continueDelay = 0.9f;
        [Tooltip("Tâm CONTINUE / x2 cách đáy màn hình (đơn vị canvas), đo trên video.")]
        [SerializeField] private float continueHeight = 380f;
        [SerializeField] private float buttonsSpread = 232f;

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
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => title.localScale = Vector3.one * t);
            if (reward.HasValue)
            {
                yield return RewardSequence(buttonRect, reward.Value);
                yield break;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => buttonRect.localScale = Vector3.one * t);
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
            amount.text = "+" + reward.baseCoins;
            coinItem.localScale = Vector3.zero;
            combo.gameObject.SetActive(reward.comboCoins > 0);
            comboAmount.text = "+" + reward.comboCoins;
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

        private IEnumerator RewardSequence(RectTransform continueRect, VictoryReward reward)
        {
            bool hasBar = _hasBar;
            float from = _barFrom, to = _barTo;
            var doubleRect = (RectTransform)doubleButton.transform;

            yield return Tween.Run(0.3f, Tween.OutBack(2.5f), t => coinItem.localScale = Vector3.one * t);
            StartCoroutine(Confetti(coinItem));
            StartCoroutine(Pulse(glow));
            yield return new WaitForSeconds(0.45f);
            if (combo.gameObject.activeSelf) yield return Tween.Run(0.25f, Tween.OutBack(3f), t => combo.localScale = Vector3.one * t);
            yield return new WaitForSeconds(0.2f);
            StartCoroutine(Tween.Run(0.25f, Tween.OutBack(3f), t => doubleRect.localScale = continueRect.localScale = Vector3.one * t));
            if (hasBar)
            {
                yield return Tween.Run(0.25f, Tween.OutBack(3f), t => bar.localScale = Vector3.one * t);
                StartCoroutine(Tween.Run(0.6f, Tween.OutQuad, t => barFill.anchorMax = new Vector2(Mathf.Lerp(from, to, t), 1f)));
            }
            yield return new WaitForSeconds(0.4f);

            // Đống combo rơi vào đống lớn, rồi số đếm lên tổng.
            if (combo.gameObject.activeSelf)
            {
                var start = combo.anchoredPosition;
                yield return Tween.Run(0.35f, Tween.InQuad, t =>
                {
                    combo.anchoredPosition = Vector2.Lerp(start, coinItem.anchoredPosition, t);
                    combo.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, t);
                });
                combo.gameObject.SetActive(false);
                pile.sprite = bigPile;
                SoundPlayer.Play("CollectCoin");
                yield return CountUp(reward.baseCoins, _total);
            }
        }

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
            yield return CountUp(_total, _total * 2);
            doubleButton.gameObject.SetActive(false);
        }

        /// <summary>Số chạy từ <paramref name="from"/> tới <paramref name="to"/>, đống xu nảy lên.</summary>
        private IEnumerator CountUp(int from, int to)
        {
            yield return Tween.Run(0.5f, Tween.OutQuad, t =>
            {
                amount.text = "+" + Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                coinItem.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.PI * t));
            });
            amount.text = "+" + to;
        }

        private static IEnumerator Pulse(RectTransform target)
        {
            float time = 0f;
            while (target != null)
            {
                time += Time.deltaTime;
                target.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(time * 3f));
                target.Rotate(0f, 0f, 20f * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Pháo giấy (hệ hạt UI trong gốc): các mảnh màu bay ra thành vòng rồi rơi.</summary>
        private static IEnumerator Confetti(RectTransform around)
        {
            Color[] colours = { UiBuilder.Hex("FF4D6DFF"), UiBuilder.Hex("FFD23FFF"), UiBuilder.Hex("3EC1F3FF"), UiBuilder.Hex("7CDB3AFF"), UiBuilder.Hex("B06BFFFF"), UiBuilder.Hex("FF8A2BFF") };
            const int Count = 46;
            var pieces = new Image[Count];
            var velocity = new Vector2[Count];
            var spin = new float[Count];
            for (int i = 0; i < Count; i++)
            {
                var rect = UiBuilder.Rect(around, "confetti", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Random.Range(14f, 22f), Random.Range(30f, 44f)));
                pieces[i] = UiBuilder.Image(rect, null, colours[i % colours.Length]);
                float angle = i * Mathf.PI * 2f / Count + Random.Range(-0.1f, 0.1f);
                velocity[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(700f, 1050f);
                spin[i] = Random.Range(-540f, 540f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (pieces[i] == null) continue;
                    velocity[i] *= 1f - 3.2f * Time.deltaTime;
                    velocity[i].y -= 600f * Time.deltaTime;
                    pieces[i].rectTransform.anchoredPosition += velocity[i] * Time.deltaTime;
                    pieces[i].rectTransform.Rotate(0f, 0f, spin[i] * Time.deltaTime);
                    var c = colours[i % colours.Length];
                    pieces[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01(2f - t * 1.3f));
                }
                yield return null;
            }
            foreach (var piece in pieces) if (piece != null) Destroy(piece.gameObject);
        }

        // ------------------------------------------------------------------ UNLOCKED!

        /// <summary>UNLOCKED!: trang riêng sau màn thắng [user]: VICTORY!, xu, NEW ITEM và x2 thu lại trên nền đen, rồi hình cơ chế và CONTINUE ở giữa.</summary>
        private IEnumerator ShowUnlocked(UnlockPopups.Entry entry)
        {
            unlockedIcon.sprite = entry.booster ? boosterIcons[Mathf.Clamp(entry.icon, 0, boosterIcons.Length - 1)] : entry.icon < mechanicIcons.Length ? mechanicIcons[entry.icon] : null;
            unlockedName.text = entry.name + "!";
            unlocked.gameObject.SetActive(true);
            unlocked.localScale = Vector3.zero;
            var button = (RectTransform)continueButton.transform;
            var doubleRect = (RectTransform)doubleButton.transform;
            yield return Tween.Run(titlePopDuration, Tween.Linear, t =>
            {
                title.localScale = Vector3.one * (1f - t);
                button.localScale = Vector3.one * (1f - t);
                main.localScale = Vector3.one * (1f - t);
                doubleRect.localScale = Vector3.one * (1f - t);
            });
            main.gameObject.SetActive(false);
            doubleButton.gameObject.SetActive(false);
            button.anchoredPosition = new Vector2(0f, button.anchoredPosition.y);
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => unlocked.localScale = Vector3.one * t);
            yield return new WaitForSeconds(Mathf.Max(0f, continueDelay - titlePopDuration));
            yield return Tween.Run(titlePopDuration, Tween.OutBack(3f), t => button.localScale = Vector3.one * t);
            continueButton.Interactable = true;
        }
    }
}
