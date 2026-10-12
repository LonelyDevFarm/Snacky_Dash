using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một nút booster trên thanh dưới (gốc: BoosterButtonX): ô lục giác, hình booster, ô số lượng đỏ hoặc dấu +, nhãn FREE; chưa mở thì
    /// hình bóng be dưới ổ khoá và "LVL. N"; mũi tên tím nhún khi hướng dẫn dùng lần đầu. Nằm trong prefab <c>Prefabs/UI/Screens/Game/BoosterBar</c>.
    /// </summary>
    public class BoosterButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite iconSprite;
        [Tooltip("Hình bóng be khi chưa mở.")]
        [SerializeField] private Sprite lockedSprite;
        [Tooltip("Hình xám khi tạm không dùng được (Undo); để trống nếu không có.")]
        [SerializeField] private Sprite disabledSprite;
        [SerializeField] private RectTransform countPanel;
        [SerializeField] private TMP_Text count;
        [SerializeField] private GameObject plus;
        [SerializeField] private GameObject freeLabel;
        [SerializeField] private GameObject lockedPanel;
        [SerializeField] private TMP_Text lockedLabel;
        [SerializeField] private RectTransform arrow;
        [Tooltip("Bề ngang ô số: 1 chữ số / 2 chữ số.")]
        [SerializeField] private float countWidth = 50f, wideCountWidth = 74f;

        private CanvasGroup _group;

        public event Action Clicked;

        public Sprite Icon => iconSprite;
        public RectTransform Arrow => arrow;

        private void Awake() => button.onClick.AddListener(() => Clicked?.Invoke());

        /// <summary>Đã mở thì bấm được; chưa mở thì ổ khoá với "LVL. <paramref name="unlockLevel"/>".</summary>
        public void Setup(bool unlocked, int unlockLevel)
        {
            icon.sprite = unlocked || lockedSprite == null ? iconSprite : lockedSprite;
            lockedPanel.SetActive(!unlocked);
            lockedLabel.text = "LVL. " + unlockLevel;
            button.targetGraphic.raycastTarget = unlocked;
            button.enabled = unlocked;
            countPanel.gameObject.SetActive(false);
            plus.SetActive(false);
            freeLabel.SetActive(false);
        }

        /// <summary>FREE ở màn mở khoá; còn hàng thì ô số; hết thì dấu +. <paramref name="usable"/> = false: hình xám và mờ (Undo) [user].</summary>
        public void Show(bool free, int stock, bool usable = true)
        {
            freeLabel.SetActive(free);
            countPanel.gameObject.SetActive(!free && stock > 0);
            plus.SetActive(!free && stock <= 0);
            count.text = stock.ToString();
            countPanel.sizeDelta = new Vector2(stock >= 10 ? wideCountWidth : countWidth, countPanel.sizeDelta.y);
            if (disabledSprite == null) return;
            icon.sprite = usable ? iconSprite : disabledSprite;
            if (_group == null && !TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = usable ? 1f : 0.45f;
        }
    }
}
