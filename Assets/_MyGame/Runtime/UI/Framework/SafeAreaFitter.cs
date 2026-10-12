using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Đặt khung này vừa Screen.safeArea (như _safe_area_fitter của bản gốc): trên máy có tai thỏ, mọi thứ bên trong nằm thấp hơn.
    /// Gắn lên object "SafeArea" (neo kéo giãn) của Home và HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private void Awake()
        {
            var rect = (RectTransform)transform;
            var area = Screen.safeArea;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        }
    }
}
