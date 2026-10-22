using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Đặt khung này vừa Screen.safeArea (như _safe_area_fitter của bản gốc): trên máy có tai thỏ, mọi thứ bên trong nằm thấp hơn.
    /// Gắn lên object "SafeArea" (neo kéo giãn) của Home và HUD. Tính lại khi cỡ màn / vùng an toàn đổi (xoay, máy gập, Editor đổi
    /// cửa sổ Game / Simulator), neo kẹp trong 0..1 để khung không bao giờ ra ngoài màn.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect _area;
        private Vector2Int _screen;

        private void Awake() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _area || Screen.width != _screen.x || Screen.height != _screen.y) Apply();
        }

        private void Apply()
        {
            _area = Screen.safeArea;
            _screen = new Vector2Int(Screen.width, Screen.height);
            var n = Normalized();
            var rect = (RectTransform)transform;
            rect.anchorMin = n.min;
            rect.anchorMax = n.max;
        }

        /// <summary>
        /// Vùng an toàn theo tỉ lệ màn (0..1, đã kẹp) — dùng chung cho mọi chỗ cần (HUD, thanh booster, Shop, camera bàn) để không lệch nhau.
        /// </summary>
        public static Rect Normalized()
        {
            var area = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return new Rect(0f, 0f, 1f, 1f);
            float xMin = Mathf.Clamp01(area.xMin / Screen.width), yMin = Mathf.Clamp01(area.yMin / Screen.height);
            float xMax = Mathf.Clamp01(area.xMax / Screen.width), yMax = Mathf.Clamp01(area.yMax / Screen.height);
            return Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }
    }
}
