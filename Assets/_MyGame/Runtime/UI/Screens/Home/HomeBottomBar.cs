using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh dưới của Home (Bottom HUD, cao 190): Shop, HOME, tab khoá. Tab đang chọn nằm trên ô nền sáng (Marker), nhô lên 40 và có
    /// tên bên dưới (video: HOME ở Home, SHOP ở trang Shop). Trang Shop mở giữa đỉnh màn và thanh này, không có nút X.
    /// </summary>
    public class HomeBottomBar : MonoBehaviour
    {
        [System.Serializable]
        private struct Tab
        {
            public Button button;
            public RectTransform icon;
            public GameObject label;
        }

        [SerializeField] private RectTransform marker;
        [Tooltip("Thứ tự: Shop, Home (tab khoá không bấm được).")]
        [SerializeField] private Tab[] tabs = new Tab[2];
        [Tooltip("Số ô trên thanh (Shop, Home, khoá).")]
        [SerializeField] private int slots = 3;
        [SerializeField] private float raise = 40f;

        private Vector2[] _rest;
        private ShopView _shop;

        private void Awake()
        {
            _rest = new Vector2[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                _rest[i] = tabs[i].icon.anchoredPosition;
                bool shop = i == 0;
                tabs[i].button.onClick.AddListener(() => ShowShop(shop));
            }
            Select(1);
        }

        private void Select(int index)
        {
            marker.anchorMin = new Vector2((float)index / slots, 0f);
            marker.anchorMax = new Vector2((float)(index + 1) / slots, 1f);
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].icon.anchoredPosition = _rest[i] + new Vector2(0f, i == index ? raise : 0f);
                tabs[i].label.SetActive(i == index);
            }
        }

        /// <summary>Mở trang Shop (dưới thanh này) hoặc về Home.</summary>
        public void ShowShop(bool show)
        {
            if (show && _shop == null)
            {
                _shop = Instantiate(Core.Services.Get<Core.UIManager>().Prefab<ShopView>(), transform.parent);
                _shop.ShowAsTab(((RectTransform)transform).rect.height);
                _shop.transform.SetSiblingIndex(transform.GetSiblingIndex()); // dưới thanh tab
            }
            else if (!show && _shop != null) _shop.Close();
            Select(show ? 0 : 1);
        }
    }
}
