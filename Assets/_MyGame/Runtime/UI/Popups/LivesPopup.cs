using System;
using SnackDash.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// LIVES (prefab <c>Prefabs/UI/Popups/LivesPopup</c>, gốc: AdsLifeRefillPopup, video "home"): trái tim lớn với số tim. Đầy thì chỉ ghi
    /// FULL; chưa đầy thì thêm "Time to next life:" và 2 nút: Get 1 (nút xem video, ở đây cho ngay 1 tim) và Refill all (900 xu, về
    /// 5) [user]. Thiếu xu thì mở Shop. Gói xu bên dưới của bản gốc bỏ đi. Mở bằng <c>UIManager.Show&lt;LivesPopup&gt;()</c>.
    /// </summary>
    public class LivesPopup : BasePopup
    {
        [SerializeField] private TMP_Text number;
        [SerializeField] private Image infinity;
        [SerializeField] private TMP_Text countdown;
        [Tooltip("Hàng 2 nút (ẩn khi tim đầy hoặc vô hạn).")]
        [SerializeField] private GameObject buttons;
        [SerializeField] private UiButton getOneButton;
        [SerializeField] private UiButton refillButton;
        [SerializeField] private TMP_Text refillCost;
        [SerializeField] private string fullText = "FULL";
        [Tooltip("{0} = thời gian tới tim kế tiếp.")]
        [SerializeField] private string nextText = "Time to next life: <color=#F59D00>{0}</color>";

        private float _tick;

        protected override void OnInit()
        {
            refillCost.text = PlayerEconomy.RefillPrice.ToString();
            getOneButton.Clicked += () => PlayerEconomy.Shared.AddLife();
            refillButton.Clicked += () =>
            {
                if (!PlayerEconomy.Shared.TryRefill()) SnackDash.Core.Services.Get<SnackDash.Core.UIManager>().Show<ShopView>(); // thiếu xu: Shop, nút X của nó quay lại đây [user]
            };
            PlayerEconomy.Shared.Changed += Refresh;
        }

        protected override void OnShow() => Refresh();

        private void OnDestroy() => PlayerEconomy.Shared.Changed -= Refresh;

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            var economy = PlayerEconomy.Shared;
            bool infinite = economy.InfiniteLives, full = !infinite && economy.Lives >= economy.LifeCap;
            number.text = economy.Lives.ToString();
            number.enabled = !infinite;
            infinity.enabled = infinite;
            buttons.SetActive(!full && !infinite);
            countdown.text = full ? fullText : string.Format(nextText, Clock(infinite ? economy.InfiniteLeft : economy.NextLifeIn));
        }

        /// <summary>"12m 05s", "45s", "1h 20m" (mẫu của prefab là "0s").</summary>
        public static string Clock(TimeSpan time)
        {
            if (time.TotalHours >= 1) return $"{(int)time.TotalHours}h {time.Minutes:00}m";
            if (time.TotalMinutes >= 1) return $"{time.Minutes}m {time.Seconds:00}s";
            return $"{Mathf.Max(0, time.Seconds)}s";
        }
    }
}
