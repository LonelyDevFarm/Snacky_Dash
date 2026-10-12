using System;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Gói Snacky Pass (prefab <c>Prefabs/UI/Popups/SnackyPass/SnackyPassOffer</c>, gốc: SeasonPassOfferPopup, video sk2 22.6-25 s): khung vàng, vé
    /// Snacky Pass trên đầu, đồng hồ, hình minh hoạ, những gì pass cho (8 tim, 3000 xu, mỗi booster x10, 6 giờ tim vô hạn), dòng
    /// chữ và nút giá. Game này không có mua bán: nút ghi "0 đ" và bật pass [user]. Bật ra / thu lại bằng PopupAnimator.
    /// Mở bằng <c>UIManager.Show&lt;SnackyPassOffer&gt;()</c>, nghe <see cref="Activated"/>.
    /// </summary>
    public class SnackyPassOffer : BasePopup
    {
        [SerializeField] private UiButton buyButton;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Transform clockHand;
        [Header("Bảng tóm tắt: tự cộng thưởng làn premium (Data/Config/SnackyPassConfig)")]
        [SerializeField] private TMP_Text coinsAmount;
        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        [SerializeField] private TMP_Text[] boosterAmounts = new TMP_Text[4];
        [SerializeField] private TMP_Text infiniteAmount;

        /// <summary>Bấm nút giá (sau khi popup đóng).</summary>
        public event Action Activated;

        private float _tick;

        protected override void OnInit()
        {
            buyButton.Clicked += () =>
            {
                if (IsClosing) return;
                SnackDash.Audio.SoundPlayer.Play("CollectCoin");
                SnackDash.Audio.Haptics.Medium();
                Close(() => Activated?.Invoke());
            };
            UiShiny.Widget(buyButton.GetComponent<UnityEngine.UI.Image>());
            Summary();
        }

        /// <summary>Những gì pass cho = tổng thưởng làn premium (giờ đang là 3000 xu, mỗi booster x10, 6 giờ tim vô hạn).</summary>
        private void Summary()
        {
            int coins = 0, minutes = 0;
            var boosters = new int[boosterAmounts.Length];
            foreach (var stage in SeasonPassData.Premium)
            {
                coins += stage.reward.coins;
                minutes += stage.reward.infiniteMinutes;
                var each = stage.reward.Boosters;
                for (int i = 0; i < boosters.Length; i++) boosters[i] += each[i];
            }
            coinsAmount.text = coins.ToString();
            for (int i = 0; i < boosters.Length; i++) boosterAmounts[i].text = "x" + boosters[i];
            infiniteAmount.text = minutes % 60 == 0 ? minutes / 60 + "H" : minutes + "M";
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            int steps = Mathf.FloorToInt(time);
            clockHand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + Tween.OutQuad(Mathf.Clamp01((time - steps) / 0.2f))));
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            var left = SeasonPass.Shared.Left;
            timer.text = left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : $"{left.Hours}H {left.Minutes:00}M";
        }
    }
}
