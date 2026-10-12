using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>Thẻ gói xu trong lưới 3 cột của Shop (prefab <c>Prefabs/UI/Screens/Shop/ShopCoinCard</c>, gốc: CoinPackShopItem): hình xu, số xu, nút giá.</summary>
    public class ShopCoinCard : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private Button buyButton;

        public event Action<RectTransform> Bought;

        private void Awake() => buyButton.onClick.AddListener(() => Bought?.Invoke((RectTransform)buyButton.transform));

        public void Show(Sprite packIcon, string coins)
        {
            icon.sprite = packIcon;
            amount.text = coins;
        }
    }
}
