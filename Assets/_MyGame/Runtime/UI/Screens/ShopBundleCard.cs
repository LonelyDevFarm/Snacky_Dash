using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thẻ gói trong mục Bundles của Shop (prefab <c>Prefabs/UI/Screens/Shop/ShopBundleCard</c>, gốc: BundleShopItem_Bundle_Offer, 846 x 410):
    /// ô xu với tia sáng quay, các ô phần thưởng (booster, tim vô hạn), tên gói và nút giá xanh.
    /// </summary>
    public class ShopBundleCard : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amount;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Button buyButton;
        [Tooltip("Tia sáng quay sau ô xu (RaysAnimation).")]
        [SerializeField] private RectTransform rays;
        [SerializeField] private float raysSpeed = -20f;
        [SerializeField] private ShopRewardCell cellPrefab;
        [Header("Hàng trên (235 cao, lề 26 / 25 / 26)")]
        [SerializeField] private float left = 26f, right = 25f, top = 26f, layer = 235f, mainWidth = 300f, cellGap = 8f, rightGap = 10f;

        public event Action<RectTransform> Bought;

        private void Awake() => buyButton.onClick.AddListener(() => Bought?.Invoke((RectTransform)buyButton.transform));

        public void Show(Sprite packIcon, string coins, string name, List<(Sprite[] icons, string label)> cells)
        {
            icon.sprite = packIcon;
            amount.text = coins;
            title.text = name;
            // Mỗi ô rộng bằng lưới hình của nó cộng phần chia đều chỗ còn lại.
            float width = ((RectTransform)transform).rect.width;
            float x0 = left + mainWidth, area = width - right - rightGap - x0, gaps = (cells.Count - 1) * cellGap;
            float preferred = 0f;
            foreach (var cell in cells) preferred += ShopRewardCell.GridWidth(cell.icons.Length);
            float share = cells.Count > 0 ? (area - gaps - preferred) / cells.Count : 0f;
            float x = x0;
            foreach (var cell in cells)
            {
                float w = ShopRewardCell.GridWidth(cell.icons.Length) + share;
                var view = Instantiate(cellPrefab, transform);
                view.transform.SetSiblingIndex(2); // sau nền và ô xu, trước hàng dưới
                var rect = (RectTransform)view.transform;
                rect.anchoredPosition = new Vector2(x, -(top + 3f));
                rect.sizeDelta = new Vector2(w, layer - top - 3f);
                view.Show(cell.icons, cell.label);
                x += w + cellGap;
            }
        }

        private void Update() => rays.Rotate(0f, 0f, raysSpeed * Time.unscaledDeltaTime);
    }
}
