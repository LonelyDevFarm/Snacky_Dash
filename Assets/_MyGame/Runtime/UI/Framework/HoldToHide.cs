using UnityEngine;
using UnityEngine.EventSystems;

namespace SnackDash.UI
{
    /// <summary>
    /// "Hold to see the board" (LosePopup gốc: _holdDuration 0.35, _scaleOnHold TMP_See_The_Board, _scale 1.3): giữ tay trên nền mờ
    /// thì dòng chữ to dần, đủ <see cref="holdDuration"/> giây mới ẩn popup và nền để xem bàn; thả tay thì hiện lại. Chạm nhanh không làm gì.
    /// </summary>
    public class HoldToHide : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public CanvasGroup Target;
        [Tooltip("Giây phải giữ trước khi ẩn.")]
        [SerializeField] private float holdDuration = 0.35f;
        [Tooltip("Dòng chữ HOLD TO SEE THE BOARD, to dần tới scale khi đang giữ.")]
        [SerializeField] private RectTransform scaleOnHold;
        [SerializeField] private float scale = 1.3f;

        private CanvasGroup _self;
        private float _held = -1f; // < 0: không giữ

        private void Awake()
        {
            _self = gameObject.GetComponent<CanvasGroup>();
            if (_self == null) _self = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData eventData) => _held = 0f;

        public void OnPointerUp(PointerEventData eventData)
        {
            _held = -1f;
            if (scaleOnHold != null) scaleOnHold.localScale = Vector3.one;
            Show(true);
        }

        private void Update()
        {
            if (_held < 0f || _held >= holdDuration) return;
            _held += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_held / holdDuration);
            if (scaleOnHold != null) scaleOnHold.localScale = Vector3.one * Mathf.Lerp(1f, scale, t);
            if (t >= 1f) Show(false);
        }

        private void Show(bool visible)
        {
            float alpha = visible ? 1f : 0f;
            if (Target != null) { Target.alpha = alpha; Target.blocksRaycasts = visible; }
            _self.alpha = alpha; // nền vẫn nhận tay khi trong suốt
        }
    }
}
