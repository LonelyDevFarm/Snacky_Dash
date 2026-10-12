using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Ô phần thưởng trong thẻ gói (prefab <c>Prefabs/UI/Screens/Shop/ShopRewardCell</c>, gốc: RewardContainer_Bundle_Offers): hộp be, hình ở trên
    /// (một hình thì to, nhiều hình xếp lưới 2 cột), dải nâu với số.
    /// </summary>
    public class ShopRewardCell : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [Tooltip("Hình mẫu (chép cho mỗi hình).")]
        [SerializeField] private Image iconTemplate;
        [SerializeField] private float bigIcon = 100f, smallIcon = 60f, iconGap = 5f;

        /// <summary>Bề ngang lưới hình (ô 60 x 60, 2 cột, cách 5).</summary>
        public static float GridWidth(int icons) => icons >= 2 ? 125f : 60f;

        public void Show(Sprite[] icons, string text)
        {
            label.text = text;
            float size = icons.Length == 1 ? bigIcon : smallIcon;
            int columns = Mathf.Min(2, icons.Length), rows = (icons.Length + 1) / 2;
            for (int i = 0; i < icons.Length; i++)
            {
                var image = i == 0 ? iconTemplate : Instantiate(iconTemplate, iconTemplate.transform.parent);
                image.sprite = icons[i];
                image.rectTransform.sizeDelta = new Vector2(size, size);
                image.rectTransform.anchoredPosition = new Vector2(((i % 2) - (columns - 1) * 0.5f) * (size + iconGap), ((rows - 1) * 0.5f - i / 2) * (size + iconGap));
            }
        }
    }
}
