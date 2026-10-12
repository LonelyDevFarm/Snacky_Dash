using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh booster dưới đáy (prefab <c>Prefabs/UI/Screens/Game/BoosterBar</c>, canvas riêng thứ tự 6): 4 nút xẻng, boomerang, undo, nam châm;
    /// băng khi chọn booster (Boosters_Pop-up_Tutorial: viền tối, tên nghiêng, mô tả, X đỏ, CONFIRM); lớp tối khi hướng dẫn dùng lần đầu.
    /// Chỉ giữ các ô; <see cref="BoosterBarView"/> điều khiển.
    /// </summary>
    public class BoosterBarLayout : MonoBehaviour
    {
        public RectTransform safeArea;
        public RectTransform bar;
        [Tooltip("Thứ tự: xẻng, boomerang, undo, nam châm.")]
        public BoosterButton[] buttons = new BoosterButton[4];
        public RectTransform aimPanel;
        public TMP_Text bannerTitle;
        public TMP_Text bannerText;
        public Button closeButton;
        public Button confirmButton;
        public RectTransform tutorialPanel;
    }
}
