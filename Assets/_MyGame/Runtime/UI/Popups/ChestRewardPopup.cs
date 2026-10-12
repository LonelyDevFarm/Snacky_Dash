using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn mở rương (gốc: ChestRewardPopupView, video sk1 33-41 s): màn tối dưới tiêu đề, đường chấm hiện ra (Weekly), rương rời chỗ
    /// của nó trượt xuống giữa và to dần, rung rồi bung tia sáng, phần thưởng bật ra thành hàng (170 mỗi món, cách 50) và TAP TO
    /// CLAIM; chạm thì chúng bay đi và màn đóng. Phần thưởng đã được cộng từ trước. Hai prefab: <see cref="WeeklyChestPopup"/> (tiêu đề
    /// WEEKLY ADVENTURE / REWARDS, có đường chấm) và <see cref="SnackyChestPopup"/> (vé Snacky Pass, rương to lên tại chỗ).
    /// </summary>
    public abstract class ChestRewardPopup : BasePopup
    {
        [SerializeField] private Image cover;
        [SerializeField] private Button tapArea;
        [SerializeField] private CanvasGroup header;
        [Tooltip("Đường chấm và vòng đích (để trống nếu không có).")]
        [SerializeField] private Image path;
        [SerializeField] private Image target;
        [SerializeField] private Image rays;
        [SerializeField] private Image chest;
        [Tooltip("Mẫu một phần thưởng (Icon + Amount), tắt sẵn.")]
        [SerializeField] private RectTransform rewardTemplate;
        [SerializeField] private TMP_Text tapText;
        [SerializeField] private float coverAlpha = 0.97f;
        [Tooltip("Khoảng cách giữa tâm 2 phần thưởng; hàng thứ hai cách 260.")]
        [SerializeField] private float rewardStep = 220f;

        private bool _tapped;

        protected override void OnInit() => tapArea.onClick.AddListener(() => _tapped = true);

        /// <summary>Mở <paramref name="chestSprite"/> từ vị trí <paramref name="from"/> (thế giới), hiện <paramref name="rewards"/>, đóng rồi gọi <paramref name="done"/>.</summary>
        public void Play(Sprite chestSprite, List<(Sprite icon, string amount)> rewards, Vector3 from, Action done)
        {
            chest.sprite = chestSprite;
            StartCoroutine(Run(rewards, from, done));
        }

        private IEnumerator Run(List<(Sprite icon, string amount)> rewards, Vector3 from, Action done)
        {
            var chestRect = chest.rectTransform;
            var raysRect = rays.rectTransform;
            var raysColor = rays.color;
            tapText.gameObject.SetActive(false);
            SetAlpha(cover, 0f);
            header.alpha = 0f;
            SetAlpha(rays, 0f);
            if (path != null) path.fillAmount = 0f;
            if (target != null) SetAlpha(target, 0f);
            chestRect.position = from;
            chestRect.localScale = Vector3.one * 0.2f;
            float pathAlpha = path != null ? path.color.a : 0f;

            // Tối dần, tiêu đề và đường chấm.
            yield return Tween.Run(0.3f, Tween.Linear, t =>
            {
                SetAlpha(cover, coverAlpha * t);
                header.alpha = t;
                if (target != null) SetAlpha(target, t);
                if (path != null) path.fillAmount = t;
            }, true);

            // Xuống giữa theo đường cong, to dần.
            Vector3 start = chestRect.localPosition, end = new Vector3(0f, -127f, 0f);
            Vector3 control = new Vector3(start.x - 260f, (start.y + end.y) * 0.5f + 120f, 0f);
            yield return Tween.Run(0.9f, Tween.InOutCubic, t =>
            {
                float u = 1f - t;
                chestRect.localPosition = u * u * start + 2f * u * t * control + t * t * end;
                chestRect.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, t);
                if (path != null) SetAlpha(path, pathAlpha * (1f - t));
                if (target != null) SetAlpha(target, 1f - t);
            }, true);

            // Rung, rồi bung tia sáng.
            SnackDash.Audio.Haptics.Light();
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                chestRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 8f) * 6f * (1f - t));
                chestRect.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * Mathf.PI * 4f));
            }, true);
            SnackDash.Audio.Haptics.Medium();
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            var items = new RectTransform[rewards.Count];
            for (int i = 0; i < items.Length; i++)
            {
                // Một hàng tối đa 4; nhiều hơn thì hàng 3 (rương Snacky Pass có tới 6 món).
                int perRow = items.Length <= 4 ? items.Length : 3, rowIndex = i / perRow, inRow = Mathf.Min(perRow, items.Length - rowIndex * perRow);
                items[i] = Instantiate(rewardTemplate, transform);
                items[i].gameObject.SetActive(true);
                items[i].anchoredPosition = new Vector2((i % perRow - (inRow - 1) * 0.5f) * rewardStep, items.Length <= 4 ? 0f : 130f - rowIndex * 260f);
                items[i].GetComponentInChildren<Image>().sprite = rewards[i].icon;
                items[i].GetComponentInChildren<TMP_Text>().text = rewards[i].amount.ToUpperInvariant();
                items[i].localScale = Vector3.zero;
            }
            yield return Tween.Run(0.45f, Tween.Linear, t =>
            {
                chestRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.4f, t);
                SetAlpha(chest, 1f - t);
                SetAlpha(rays, raysColor.a * Mathf.Sin(t * Mathf.PI * 0.5f));
                raysRect.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Tween.OutQuad(t));
                for (int i = 0; i < items.Length; i++)
                    items[i].localScale = Vector3.one * Tween.OutBack(1.7f)(Mathf.Clamp01((t - i * 0.15f) / 0.6f));
            }, true);

            tapText.gameObject.SetActive(true);
            _tapped = false;
            float spin = 0f;
            while (!_tapped)
            {
                spin += Time.unscaledDeltaTime * 20f;
                raysRect.localRotation = Quaternion.Euler(0f, 0f, spin);
                tapText.alpha = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                yield return null;
            }

            // Bay đi, màn sáng lại.
            tapText.gameObject.SetActive(false);
            var starts = new Vector3[items.Length];
            for (int i = 0; i < items.Length; i++) starts[i] = items[i].localPosition;
            yield return Tween.Run(0.4f, Tween.Linear, t =>
            {
                float e = Tween.InOutCubic(t);
                for (int i = 0; i < items.Length; i++)
                {
                    items[i].localPosition = Vector3.Lerp(starts[i], starts[i] + Vector3.up * 700f, e);
                    items[i].localScale = Vector3.one * (1f - e);
                }
                SetAlpha(cover, coverAlpha * (1f - t));
                header.alpha = 1f - t;
                SetAlpha(rays, raysColor.a * (1f - t));
            }, true);
            Close(done);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
