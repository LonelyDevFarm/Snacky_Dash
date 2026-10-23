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
    /// Màn mở rương (gốc: ChestRewardPopupView + clip Weekly Missions_ Rewards_Screen_Opening, video sk1 36-40 s): màn tối dưới tiêu
    /// đề, đường chấm vẽ từ mốc xuống vòng đích và ở lại (Weekly), tia sáng nở, rương bật ra giữa màn, thu lại, lắc, phồng lên rồi tắt;
    /// phần thưởng bật ra thành hàng (170 mỗi món, cách 50) và TAP TO CLAIM; chạm thì chúng bay đi và màn đóng.
    /// Phần thưởng đã được cộng từ trước. Hai prefab: <see cref="WeeklyChestPopup"/> (tiêu đề
    /// WEEKLY ADVENTURE / REWARDS, có đường chấm) và <see cref="SnackyChestPopup"/> (clip SeasonPass: sớm hơn 0.4167 s, tiêu đề bật).
    /// </summary>
    public abstract class ChestRewardPopup : BasePopup
    {
        [SerializeField] private Image cover;
        [SerializeField] private Button tapArea;
        [SerializeField] private CanvasGroup header;
        [Tooltip("Đường chấm và vòng đích (để trống nếu không có).")]
        [SerializeField] private Image path;
        [Tooltip("Lớp trắng mờ phủ đường chấm (gốc SP_Path (1) #FFFFFF38), vẽ chậm hơn một chút.")]
        [SerializeField] private Image pathHighlight;
        [SerializeField] private Image target;
        [SerializeField] private Image rays;
        [SerializeField] private Image chest;
        [Tooltip("Mẫu một phần thưởng (Icon + Amount), tắt sẵn.")]
        [SerializeField] private RectTransform rewardTemplate;
        [SerializeField] private TMP_Text tapText;
        [Tooltip("Gốc Background #000000FA.")]
        [SerializeField] private float coverAlpha = 0.98f;
        [Tooltip("Khoảng cách giữa tâm 2 phần thưởng; hàng thứ hai cách 260.")]
        [SerializeField] private float rewardStep = 220f;
        [Header("Nhịp (gốc: clip Weekly Missions_ Rewards_Screen_Opening; Snacky Pass: SeasonPass_RewardsScreen_Opening)")]
        [Tooltip("Mọi mốc của rương / tia sáng sớm hơn clip Weekly bấy nhiêu giây (Snacky Pass 0.4167).")]
        [SerializeField] private float lead = 0f;
        [Tooltip("Tia sáng sau rương to tới cỡ này (gốc Back Rays 02: Weekly 7, Snacky Pass 10); ảnh rays ở cỡ 1 = cỡ này.")]
        [SerializeField] private float raysPeak = 7f;
        [Tooltip("Tia sáng hiện dần trong bấy nhiêu giây (Weekly 0 = bật ngay, Snacky Pass 0.333).")]
        [SerializeField] private float raysFadeIn = 0f;
        [Tooltip("Tiêu đề bật 0 -> 1.1 -> 1 trong 0.3 s (Snacky Pass); không thì hiện sẵn (Weekly).")]
        [SerializeField] private bool headerPop;
        [Tooltip("So le giữa các phần thưởng khi bật ra (video sk1: món thứ hai hiện sau).")]
        [SerializeField] private float rewardStagger = 0.15f;

        // Khoá hình của clip Weekly (giây, giá trị); Snacky Pass dùng cùng dáng, sớm hơn `lead`.
        private static readonly AnimationCurve ChestSize = Linear((0.7333f, 100f), (0.8667f, 500f));
        private static readonly AnimationCurve ChestY = Linear((0.6667f, -155.23f), (0.8667f, -127f));
        private static readonly AnimationCurve ChestScale = Linear((0f, 1f), (0.8667f, 0.8f), (1f, 0.7f), (1.1167f, 0.7f), (1.75f, 0.8f), (1.8833f, 0.9f));
        private static readonly AnimationCurve ChestParentScale = Linear((1f, 1f), (1.8833f, 1.2f));
        private static readonly AnimationCurve TargetScale = Linear((0.2833f, 0f), (0.3f, 1f), (0.4f, 1.2f), (0.5f, 1f));
        private static readonly AnimationCurve PathFill = Linear((0.0667f, 0.052f), (0.15f, 0.523f), (0.2167f, 1f));
        private static readonly AnimationCurve PathHighlightFill = Linear((0.0667f, 0.013f), (0.15f, 0.342f), (0.2667f, 1f));
        private static readonly AnimationCurve HeaderScale = Linear((0f, 0f), (0.2f, 1.1f), (0.3f, 1f));
        private static readonly AnimationCurve RewardOpen = Linear((0f, 0f), (0.25f, 1.3f), (0.4167f, 1f)); // AN_Reward_Open
        private const float RaysOn = 0.6667f, ChestOn = 0.7333f, ShakeFrom = 1.1333f, ShakeTo = 1.75f, Reveal = 1.8833f;

        private static AnimationCurve Linear(params (float time, float value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach (var (time, value) in keys) curve.AddKey(new Keyframe(time, value));
            for (int i = 0; i < curve.length; i++) MakeLinear(curve, i);
            return curve;
        }

        /// <summary>Tiếp tuyến thẳng giữa các khoá (như nội suy tuyến tính).</summary>
        private static void MakeLinear(AnimationCurve curve, int i)
        {
            var key = curve[i];
            if (i > 0) key.inTangent = (key.value - curve[i - 1].value) / (key.time - curve[i - 1].time);
            if (i < curve.length - 1) key.outTangent = (curve[i + 1].value - key.value) / (curve[i + 1].time - key.time);
            curve.MoveKey(i, key);
        }

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
            var chestParent = chestRect.parent;
            var raysRect = rays.rectTransform;
            var raysColor = rays.color;
            var raysGroup = rays.GetComponent<CanvasGroup>(); // quầng + tia + bụi sáng con của rays mờ theo cùng
            var headerRect = (RectTransform)header.transform;
            tapText.gameObject.SetActive(false);
            SetAlpha(cover, 0f);
            header.alpha = 1f;
            headerRect.localScale = headerPop ? Vector3.zero : Vector3.one;
            SetAlpha(rays, 0f);
            if (raysGroup != null) raysGroup.alpha = 0f;
            if (path != null) path.fillAmount = 0f;
            if (pathHighlight != null) pathHighlight.fillAmount = 0f;
            if (target != null) { target.gameObject.SetActive(true); target.rectTransform.localScale = Vector3.zero; }
            chest.gameObject.SetActive(false);
            chestRect.localRotation = Quaternion.identity;
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

            // Theo clip gốc: màn tối, đường chấm vẽ từ mốc rương xuống và ở lại, vòng đích bật; tia sáng nở, rương bật ra giữa màn
            // (100 -> 500), thu lại, lắc +-3 độ, phồng lên; rồi rương tắt, phần thưởng bật ra và TAP TO CLAIM.
            bool chestShown = false, haptic = false;
            float reveal = Reveal - lead;
            for (float time = 0f; time < reveal; time += Time.unscaledDeltaTime)
            {
                float t = time + lead; // giờ theo clip Weekly
                SetAlpha(cover, coverAlpha * Mathf.Clamp01(time / 0.3f));
                if (headerPop) headerRect.localScale = Vector3.one * HeaderScale.Evaluate(time);
                if (path != null) path.fillAmount = time < 0.0667f ? 0f : PathFill.Evaluate(time);
                if (pathHighlight != null) pathHighlight.fillAmount = time < 0.0667f ? 0f : PathHighlightFill.Evaluate(time);
                if (target != null) target.rectTransform.localScale = Vector3.one * TargetScale.Evaluate(time);
                if (t >= RaysOn)
                {
                    float grow = Mathf.Clamp01((t - RaysOn) / (ChestOn - RaysOn));
                    float size = t < ChestOn ? Mathf.Lerp(0.3f, 3.8f, grow) : Mathf.Lerp(3.8f, raysPeak, Mathf.Clamp01((t - ChestOn) / (0.8667f - ChestOn)));
                    raysRect.localScale = Vector3.one * (size / raysPeak);
                    float shown = raysFadeIn > 0f ? Mathf.Clamp01((t - RaysOn) / raysFadeIn) : 1f;
                    SetAlpha(rays, raysColor.a * shown);
                    if (raysGroup != null) raysGroup.alpha = shown;
                }
                if (t >= ChestOn)
                {
                    if (!chestShown) { chestShown = true; chest.gameObject.SetActive(true); SetAlpha(chest, 1f); }
                    chestRect.localPosition = new Vector3(0f, ChestY.Evaluate(t), 0f);
                    chestRect.localScale = Vector3.one * (ChestSize.Evaluate(t) / 500f * ChestScale.Evaluate(t) * ChestParentScale.Evaluate(t));
                    // Lắc: khoá +-3 độ đổi chiều mỗi ~0.1 s từ 1.13 tới 1.75 s.
                    float shake = t > ShakeFrom && t < ShakeTo ? 3f * Mathf.Sign(Mathf.Sin((t - ShakeFrom) / 0.1f * Mathf.PI + Mathf.PI * 0.5f)) : 0f;
                    chestRect.localRotation = Quaternion.Euler(0f, 0f, shake);
                    if (!haptic && t > ShakeFrom) { haptic = true; SnackDash.Audio.Haptics.Light(); }
                }
                yield return null;
            }
            chest.gameObject.SetActive(false);
            if (target != null) target.gameObject.SetActive(false);
            SnackDash.Audio.Haptics.Medium();
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            float open = RewardOpen.keys[RewardOpen.length - 1].time;
            for (float time = 0f; time < open + rewardStagger * items.Length; time += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < items.Length; i++)
                    items[i].localScale = Vector3.one * RewardOpen.Evaluate(Mathf.Clamp(time - i * rewardStagger, 0f, open));
                yield return null;
            }
            for (int i = 0; i < items.Length; i++) items[i].localScale = Vector3.one;

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
                if (raysGroup != null) raysGroup.alpha = 1f - t;
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
