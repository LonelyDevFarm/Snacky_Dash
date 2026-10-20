using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Tạo một lớp canvas riêng (ComboView, ExtraCrateView) có thứ tự vẽ riêng. Hàm dựng ảnh / chữ / nút bằng code đã gỡ 20/10
    /// [UNITY_WORK_RULES luật 14]: thứ nhìn thấy là prefab; thứ sinh lặp lại lấy từ prefab mẫu qua <see cref="UiPool"/>.
    /// </summary>
    public static class UiBuilder
    {
        /// <summary>Canvas màn hình ở độ phân giải chuẩn của bản gốc (1080x1920, khớp chiều cao).</summary>
        public static RectTransform Canvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            return (RectTransform)go.transform;
        }
    }
}
