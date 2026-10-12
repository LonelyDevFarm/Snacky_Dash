using System;
using SnackDash.Core;
using SnackDash.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Mua booster khi hết (prefab <c>Prefabs/UI/Popups/BoosterBuyPopup</c>, gốc: Boosters_Pop-up_Purchase / BoosterBuyUI): ô xu góc trên,
    /// tiêu đề nghiêng với tên booster, hình lớn với "x3", mô tả và Buy 1800 [user: 1800 xu cho 3, mọi booster]. Nút xem video của gốc
    /// ẩn (như khi không có quảng cáo) nên Buy đứng giữa. Thiếu xu thì mở Shop đè lên, X của Shop quay lại đây [user].
    /// Mở bằng <c>UIManager.Show&lt;BoosterBuyPopup&gt;()</c> rồi <see cref="Setup"/>.
    /// </summary>
    public class BoosterBuyPopup : BasePopup
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Image icon;
        [Tooltip("\"x3\" cạnh hình (để trống nếu không có).")]
        [SerializeField] private TMP_Text count;
        [SerializeField] private TMP_Text price;
        [SerializeField] private UiButton buyButton;
        [Tooltip("Ô xu góc trên (để trống nếu không có).")]
        [SerializeField] private TMP_Text coins;

        private int _cost;
        private Action _paid;

        protected override void OnInit()
        {
            buyButton.Clicked += () =>
            {
                if (!PlayerEconomy.Shared.TrySpend(_cost)) { Services.Get<UIManager>().Show<ShopView>(); return; }
                Close(_paid);
            };
            if (coins != null) PlayerEconomy.Shared.Changed += ShowCoins;
        }

        protected override void OnShow() => ShowCoins();

        private void OnDestroy() { if (coins != null) PlayerEconomy.Shared.Changed -= ShowCoins; }

        private void ShowCoins() { if (coins != null) coins.text = PlayerEconomy.Shared.Coins.ToString(); }

        /// <summary>Booster hết hàng: mua <see cref="PlayerEconomy.BoosterPack"/> cái với <see cref="PlayerEconomy.BoosterPrice"/> xu.</summary>
        public void Setup(string name, string text, Sprite picture, string stockKey, Action bought)
        {
            title.text = name;
            description.text = text.Replace("#8929F6", "#F59D00");
            icon.sprite = picture;
            count.text = "<size=85%>x</size>" + PlayerEconomy.BoosterPack;
            Pay(PlayerEconomy.BoosterPrice, () => { PlayerEconomy.Shared.AddBoosters(stockKey, PlayerEconomy.BoosterPack); bought?.Invoke(); });
        }

        /// <summary>Giá và việc làm khi trả xong.</summary>
        protected void Pay(int cost, Action paid)
        {
            _cost = cost;
            _paid = paid;
            price.text = cost.ToString();
        }
    }
}
