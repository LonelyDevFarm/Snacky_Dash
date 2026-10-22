using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Core;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// SNACKY PASS (prefab <c>Prefabs/UI/Popups/SnackyPass/SnackyPassView</c>, gốc: seasonpass/BP_MainUI_View, video sk2 / sk2.1): hình minh hoạ, vé
    /// "Snacky Pass" với ACTIVATE (ACTIVE! khi đã bật), đồng hồ đỏ, nút i và X, 20 chặng bên dưới (mỗi chặng một prefab
    /// <see cref="SnackyPassRow"/>). Mở ra ở cuối danh sách rồi trượt lên chặng hiện tại (sk2 12.3-13.4 s), lần đầu hiện luật chơi.
    /// Chạm thẻ nhận được: phần thưởng bay lên, thẻ nhạt đi và dấu tích bật ra; rương mở màn riêng. Thiết kế rộng 1080 (như gốc)
    /// và co theo bề ngang màn hình. Mở bằng <c>UIManager.Show&lt;SnackyPassView&gt;()</c>.
    /// </summary>
    public class SnackyPassView : BasePopup
    {
        private const float Width = 1080f;

        [Header("Khung")]
        [Tooltip("Thiết kế rộng 1080, co theo bề ngang màn hình.")]
        [SerializeField] private RectTransform fit;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private UiButton cancelButton;
        [SerializeField] private UiButton infoButton;
        [Tooltip("Nút ACTIVATE (phần màu nhận chạm) và cả cụm của nó (ẩn khi pass đã bật).")]
        [SerializeField] private UiButton activateButton;
        [SerializeField] private GameObject activate;
        [SerializeField] private RectTransform active;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Transform clockHand;

        [Header("Chặng")]
        [SerializeField] private SnackyPassRow rowPrefab;
        [Tooltip("Chặng 1 cao hơn các chặng khác.")]
        [SerializeField] private float firstRow = 350f;
        [SerializeField] private float rowHeight = 285f;

        [Header("Mẫu")]
        [Tooltip("Số bay lên cùng phần thưởng vừa nhận (mẫu, tắt sẵn).")]
        [SerializeField] private TMP_Text amountTemplate;
        [Tooltip("Thông báo \"Snacky Pass is active!\" (mẫu, tắt sẵn).")]
        [SerializeField] private RectTransform toastTemplate;
        [SerializeField] private string toastTitle = "Snacky Pass is active!";
        [SerializeField, TextArea] private string toastLine = "Maximum amount of lives has increased to 8!\nPremium rewards are unlocked!";

        [Header("Hình phần thưởng")]
        [SerializeField] private Sprite livesPerkSprite;
        [Tooltip("Rương 1..n (S_Icon_Chests2_0..).")]
        [SerializeField] private Sprite[] chestSprites = new Sprite[0];
        [SerializeField] private Sprite coinSprite;
        [SerializeField] private Sprite infiniteLivesSprite;
        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        [SerializeField] private Sprite[] boosterSprites = new Sprite[4];

        private SeasonPass _pass;
        private bool _busy;
        private float _tick;
        private readonly List<SnackyPassRow> _rows = new List<SnackyPassRow>();

        protected override void OnInit()
        {
            _pass = SeasonPass.Shared;
            cancelButton.Clicked += () => { if (!_busy) Close(); };
            infoButton.Clicked += () => Services.Get<UIManager>().Show<SnackyPassRules>();
            activateButton.Clicked += OpenOffer;
            UiShiny.Widget(activateButton.GetComponent<Image>());
            _pass.Changed += Refresh;
        }

        protected override void OnShow()
        {
            FitWidth();
            BuildRows();
            ShowBadge();
            StartCoroutine(OpenFlow());
        }

        private void OnDestroy() { if (_pass != null) _pass.Changed -= Refresh; }

        /// <summary>
        /// Đầu trang: thiết kế 1080 co theo bề ngang màn hình, cao bằng màn hình. Danh sách chặng thì giữ cỡ thật như gốc (video sk2:
        /// một chặng cao đúng 285, ô 227) — bù lại phép co bằng <see cref="_listScale"/>, hàng giãn theo bề ngang, hai làn chia theo tỉ lệ.
        /// </summary>
        private void FitWidth()
        {
            Canvas.ForceUpdateCanvases();
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f) return;
            float k = size.x / Width;
            fit.localScale = new Vector3(k, k, 1f);
            fit.sizeDelta = new Vector2(Width, size.y / k);
            _listScale = 1f / k;
        }

        private float _listScale = 1f;

        private void ShowBadge()
        {
            activate.SetActive(!_pass.Premium);
            active.gameObject.SetActive(_pass.Premium);
        }

        private void Refresh()
        {
            if (_busy) return;
            ShowBadge();
            for (int i = 0; i < _rows.Count; i++) ShowRow(_rows[i], i + 1);
        }

        private float RowTop(int stage) => stage == 1 ? 0f : firstRow + (stage - 2) * rowHeight;

        private void BuildRows()
        {
            var content = scroll.content;
            content.localScale = new Vector3(_listScale, _listScale, 1f);
            content.sizeDelta = new Vector2(Width / _listScale, firstRow + (SeasonPass.Stages - 1) * rowHeight);
            for (int stage = 1; stage <= SeasonPass.Stages; stage++)
            {
                var row = Instantiate(rowPrefab, content);
                row.name = "Stage " + stage;
                var rect = (RectTransform)row.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(0f, -RowTop(stage));
                rect.sizeDelta = new Vector2(0f, stage == 1 ? firstRow : rowHeight);
                int s = stage;
                row.CardClicked += premium => OnCard(s, premium);
                ShowRow(row, stage);
                _rows.Add(row);
            }
            _rows[0].MakeFirst(-(firstRow - rowHeight) * 0.5f + 10f);
        }

        private void ShowRow(SnackyPassRow row, int stage)
        {
            int open = _pass.Stage;
            bool unlocked = stage <= open, next = stage == open + 1 && open < SeasonPass.Stages;
            int state = unlocked ? 0 : next ? 1 : 2;
            foreach (bool premium in new[] { false, true })
            {
                var reward = SeasonPass.RewardAt(stage, premium);
                var (icon, label) = Look(reward);
                row.ShowLane(premium, state, icon, label, reward.chest > 0 || reward.livesPerk,
                    _pass.Claimed(stage, premium), _pass.Claimable(stage, premium), !_pass.Premium);
            }
            var (have, need) = _pass.Progress;
            row.ShowStage(stage, next, have, need);
        }

        /// <summary>Hình và chữ của phần thưởng: "25", "X1", "15M", "1H"; rương và gói 8 tim không có chữ.</summary>
        private (Sprite icon, string label) Look(PassReward reward)
        {
            if (reward.livesPerk) return (livesPerkSprite, "");
            if (reward.chest > 0) return (chestSprites[Mathf.Clamp(reward.chest - 1, 0, chestSprites.Length - 1)], "");
            var r = reward.reward;
            if (r.coins > 0) return (coinSprite, r.coins.ToString());
            if (r.infiniteMinutes > 0) return (infiniteLivesSprite, r.infiniteMinutes % 60 == 0 ? r.infiniteMinutes / 60 + "H" : r.infiniteMinutes + "M");
            var boosters = r.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) return (boosterSprites[b], "X" + boosters[b]);
            return (null, "");
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            int steps = Mathf.FloorToInt(time);
            clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(Mathf.Clamp01((time - steps) / 0.2f))));
            if (_pass == null || (_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = _pass.Left;
            timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes:00}M" : $"{left.Minutes}M {left.Seconds:00}S";
        }

        // ------------------------------------------------------------------ mở

        /// <summary>sk2 12.3-13.4 s: hiện ở cuối danh sách (chặng 20) rồi trượt lên chặng hiện tại trong khoảng 1 giây, chậm dần cuối; lần đầu hiện luật chơi.</summary>
        private IEnumerator OpenFlow()
        {
            _busy = true;
            scroll.enabled = false;
            var content = scroll.content;
            // Vị trí danh sách tính trong khung cuộn: đơn vị chặng x _listScale.
            float bottom = Mathf.Max(0f, content.sizeDelta.y * _listScale - scroll.viewport.rect.height);
            int focus = Mathf.Max(1, FirstClaimable());
            float target = Mathf.Clamp((RowTop(focus) - (focus == 1 ? 0f : rowHeight * 0.3f)) * _listScale, 0f, bottom);
            Action<float> place = y => content.anchoredPosition = new Vector2(0f, y);
            place(bottom);
            group.alpha = 0f;
            yield return Tween.Run(0.2f, Tween.Linear, t => group.alpha = t, true);
            yield return Tween.Run(1.0f, Tween.InOutCubic, t => place(Mathf.Lerp(bottom, target, t)), true);
            scroll.enabled = true;
            scroll.velocity = Vector2.zero;
            _busy = false;
            if (!SnackyPassRules.Seen) Services.Get<UIManager>().Show<SnackyPassRules>();
        }

        /// <summary>Chặng đầu tiên có thứ nhận được, không thì chặng hiện tại.</summary>
        private int FirstClaimable()
        {
            for (int stage = 1; stage <= SeasonPass.Stages; stage++)
                if (_pass.Claimable(stage, false) || _pass.Claimable(stage, true)) return stage;
            return _pass.Stage;
        }

        // ------------------------------------------------------------------ nhận thưởng

        private void OnCard(int stage, bool premium)
        {
            if (_busy) return;
            if (premium && !_pass.Premium) { OpenOffer(); return; }
            if (!_pass.Claimable(stage, premium)) { Shake(_rows[stage - 1].LaneOf(premium).card); return; }
            StartCoroutine(ClaimFlow(stage, premium));
        }

        private void Shake(Transform card) => StartCoroutine(Tween.Run(0.3f, Tween.Linear, t => { if (card != null) card.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 5f * (1f - t)); }, true));

        /// <summary>
        /// sk2 26-28 s: CLAIM biến mất, bản sao phần thưởng bật lên khỏi thẻ và bay đi cùng số, thẻ nhạt đi và dấu tích xanh bật ra.
        /// Rương thì mở màn riêng trước.
        /// </summary>
        private IEnumerator ClaimFlow(int stage, bool premium)
        {
            _busy = true;
            var lane = _rows[stage - 1].LaneOf(premium);
            var reward = SeasonPass.RewardAt(stage, premium);
            lane.claim.SetActive(false);
            lane.glow.SetActive(false);
            lane.sparkles.SetActive(false);
            _pass.Claim(stage, premium);
            SnackDash.Audio.SoundPlayer.Play("CollectCoin");
            SnackDash.Audio.Haptics.Light();
            var (sprite, label) = Look(reward);

            if (reward.chest > 0)
            {
                bool done = false;
                var chestPopup = Services.Get<UIManager>().Show<SnackyChestPopup>();
                if (chestPopup != null) chestPopup.Play(sprite, ChestContents(reward.reward), lane.card.position, () => done = true);
                else done = true;
                while (!done) yield return null;
            }
            else
            {
                // Bản sao hình bay lên và mờ dần, to hơn một chút, kèm số.
                var icon = lane.icon.rectTransform;
                var copy = (RectTransform)Instantiate(icon, transform);
                copy.position = icon.position;
                copy.localScale = Vector3.one * icon.lossyScale.x / transform.lossyScale.x;
                if (label.Length > 0)
                {
                    var words = Instantiate(amountTemplate, copy);
                    words.gameObject.SetActive(true);
                    words.text = (reward.reward.coins > 0 ? "+" : "") + label;
                }
                var fade = copy.gameObject.AddComponent<CanvasGroup>();
                Vector3 start = copy.position, scale = copy.localScale;
                float unit = transform.lossyScale.y;
                StartCoroutine(Tween.Run(0.9f, Tween.Linear, t =>
                {
                    if (copy == null) return;
                    copy.localScale = scale * (t < 0.15f ? Mathf.Lerp(1f, 1.35f, Tween.OutQuad(t / 0.15f)) : 1.35f);
                    copy.position = start + Vector3.up * 170f * unit * Tween.OutQuad(t);
                    fade.alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                    if (t >= 1f) Destroy(copy.gameObject);
                }, true));
                yield return Tween.Run(0.25f, Tween.Linear, _ => { }, true);
            }

            // Thẻ nhạt đi, dấu tích bật ra.
            lane.claimed.SetActive(true);
            yield return Tween.Run(0.3f, Tween.OutBack(2f), t => lane.tick.localScale = Vector3.one * t, true);
            _busy = false;
            Refresh();
        }

        private List<(Sprite icon, string amount)> ChestContents(MissionReward r)
        {
            var list = new List<(Sprite, string)>();
            if (r.coins > 0) list.Add((coinSprite, r.coins.ToString()));
            var boosters = r.Boosters;
            for (int b = 0; b < boosters.Length; b++) if (boosters[b] > 0) list.Add((boosterSprites[b], "x" + boosters[b]));
            if (r.infiniteMinutes > 0) list.Add((infiniteLivesSprite, r.infiniteMinutes % 60 == 0 ? r.infiniteMinutes / 60 + "H" : r.infiniteMinutes + "M"));
            return list;
        }

        // ------------------------------------------------------------------ bật pass

        private void OpenOffer()
        {
            if (_busy || _pass.Premium) return;
            var offer = Services.Get<UIManager>().Show<SnackyPassOffer>();
            if (offer != null) offer.Activated += OnActivated;
        }

        /// <summary>Pass đã bật: ACTIVE! trên vé, ổ khoá biến mất, thẻ premium sáng lên (cuối sk2.1).</summary>
        private void OnActivated()
        {
            _pass.Activate();
            ShowBadge();
            StartCoroutine(Toast());
            StartCoroutine(Tween.Punch(0.12f, t => active.localScale = Vector3.one * (1f + 0.25f * t)));
            foreach (var row in _rows)
            {
                var lockIcon = row.LaneOf(true).lockIcon;
                if (lockIcon != null && lockIcon.gameObject.activeSelf)
                    StartCoroutine(Tween.Run(0.3f, Tween.InBack(1.7f), t => { if (lockIcon != null) lockIcon.localScale = Vector3.one * (1f - t); }, true));
            }
            Refresh();
        }

        /// <summary>Thông báo 2 dòng giữa màn, nhích lên và mờ dần (lời của popup mua hàng; ở đây không mua gì).</summary>
        private IEnumerator Toast()
        {
            var box = Instantiate(toastTemplate, transform);
            box.gameObject.SetActive(true);
            var texts = box.GetComponentsInChildren<TMP_Text>();
            texts[0].text = toastTitle;
            texts[1].text = toastLine;
            var fade = box.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            yield return Tween.Run(2.4f, Tween.Linear, t =>
            {
                if (box == null) return;
                box.localScale = Vector3.one * (t < 0.12f ? Tween.OutBack(2f)(t / 0.12f) : 1f);
                box.anchoredPosition = new Vector2(0f, 60f * t);
                fade.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
            }, true);
            if (box != null) Destroy(box.gameObject);
        }
    }
}
