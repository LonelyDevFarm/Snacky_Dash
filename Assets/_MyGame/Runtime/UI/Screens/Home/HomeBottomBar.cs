using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh dưới của Home (Bottom HUD, cao 190): Shop, HOME, tab khoá (gốc: HomeBottomView, HomeButtonView, MainNavigationTransitionController).
    /// Icon neo đáy ở giữa thanh nên tab đang chọn nhô lên khỏi mép; tab không chọn tụt xuống 50 và ẩn tên. Đổi tab: ô nền sáng (Marker)
    /// trượt sang, icon / tên lên xuống trong 0.25 s; trang Shop trượt từ trái vào còn Home trượt sang phải trong 0.22 s (OutCubic).
    /// Trang Shop nằm giữa đỉnh màn và thanh này, không có nút X.
    /// </summary>
    public class HomeBottomBar : MonoBehaviour
    {
        [System.Serializable]
        private struct Tab
        {
            public Button button;
            [Tooltip("Icon của tab (neo đáy, đặt ở giữa thanh).")]
            public RectTransform icon;
            [Tooltip("Tên dưới icon, chỉ hiện ở tab đang chọn (để trống: tab không có tên).")]
            public TMP_Text label;
        }

        [SerializeField] private RectTransform marker;
        [Tooltip("Thứ tự: Shop, Home, khoá (tab khoá không bấm được).")]
        [SerializeField] private Tab[] tabs = new Tab[3];
        [Tooltip("Các phần của trang Home trượt đi khi mở Shop (BG, SafeArea).")]
        [SerializeField] private RectTransform[] homePanel;
        [Tooltip("Tab không chọn tụt xuống bấy nhiêu (gốc HomeButtonView._hiddenContentY).")]
        [SerializeField] private float hiddenContentY = -50f;
        [Tooltip("Tên nằm dưới đáy icon bấy nhiêu (gốc: Label y -55).")]
        [SerializeField] private float labelY = -55f;
        [Tooltip("Giây Marker trượt và icon lên xuống (gốc HomeBottomView._moveDuration).")]
        [SerializeField] private float moveDuration = 0.25f;
        [Tooltip("Giây trang Home / Shop trượt ngang (gốc MainNavigationTransitionController._duration, Ease OutCubic).")]
        [SerializeField] private float swipeDuration = 0.22f;

        private const int ShopTab = 0, HomeTab = 1;
        private int _selected = HomeTab;
        private ShopView _shop;
        private Coroutine _move, _swipe;

        private void Awake()
        {
            tabs[ShopTab].button.onClick.AddListener(() => ShowShop(true));
            tabs[HomeTab].button.onClick.AddListener(() => ShowShop(false));
            Place(1f, HomeTab, HomeTab);
        }

        /// <summary>Mở trang Shop (dưới thanh này) hoặc về Home.</summary>
        public void ShowShop(bool show)
        {
            int target = show ? ShopTab : HomeTab;
            if (target == _selected) return;
            int from = _selected;
            _selected = target;
            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(Tween.Run(moveDuration, Tween.OutCubic, t => Place(t, from, target)));
            if (show && _shop == null)
            {
                _shop = Instantiate(Core.Services.Get<Core.UIManager>().Prefab<ShopView>(), transform.parent);
                _shop.ShowAsTab(((RectTransform)transform).rect.height);
                _shop.transform.SetSiblingIndex(transform.GetSiblingIndex()); // dưới thanh tab
            }
            if (_swipe != null) StopCoroutine(_swipe);
            _swipe = StartCoroutine(Swipe(show));
        }

        /// <summary>Marker và icon / tên ở giữa đường từ tab <paramref name="from"/> sang <paramref name="to"/> (t 0..1).</summary>
        private void Place(float t, int from, int to)
        {
            float slots = tabs.Length;
            float x = Mathf.Lerp(from, to, t);
            marker.anchorMin = new Vector2(x / slots, 0f);
            marker.anchorMax = new Vector2((x + 1f) / slots, 1f);
            for (int i = 0; i < tabs.Length; i++)
            {
                float shown = i == to ? t : i == from ? 1f - t : 0f; // 1 = đang chọn
                float y = Mathf.Lerp(hiddenContentY, 0f, shown);
                tabs[i].icon.anchoredPosition = new Vector2(tabs[i].icon.anchoredPosition.x, y);
                if (tabs[i].label == null) continue;
                tabs[i].label.rectTransform.anchoredPosition = new Vector2(tabs[i].label.rectTransform.anchoredPosition.x, labelY + y);
                tabs[i].label.alpha = shown;
            }
        }

        /// <summary>Shop trượt từ trái vào, Home sang phải (hoặc ngược lại khi về Home, rồi đóng Shop).</summary>
        private IEnumerator Swipe(bool toShop)
        {
            float width = ((RectTransform)transform.parent).rect.width;
            var shop = _shop != null ? (RectTransform)_shop.transform : null;
            float start = homePanel.Length > 0 ? homePanel[0].anchoredPosition.x / width : (toShop ? 0f : 1f); // 0 = Home, 1 = Shop
            float end = toShop ? 1f : 0f;
            yield return Tween.Run(swipeDuration * Mathf.Abs(end - start), Tween.OutCubic, t =>
            {
                float p = Mathf.Lerp(start, end, t);
                foreach (var part in homePanel) part.anchoredPosition = new Vector2(p * width, part.anchoredPosition.y);
                if (shop != null) shop.anchoredPosition = new Vector2((p - 1f) * width, shop.anchoredPosition.y);
            });
            if (!toShop && _shop != null) { _shop.Close(); _shop = null; }
            _swipe = null;
        }
    }
}
