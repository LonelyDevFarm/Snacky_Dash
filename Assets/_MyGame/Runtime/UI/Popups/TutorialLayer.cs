using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Lớp hướng dẫn trong màn (prefab <c>Prefabs/UI/Screens/Game/TutorialLayer</c>, canvas riêng thứ tự 8: trên HUD và thanh booster, dưới màn thua):
    /// nền tối (chạm để đóng lời nhắn), khung lời nhắn xanh, "TAP TO CONTINUE!", bàn tay vuốt và vệt xanh. Chỉ giữ các ô;
    /// <see cref="TutorialView"/> điều khiển.
    /// </summary>
    public class TutorialLayer : MonoBehaviour
    {
        public Button overlay;
        public RectTransform frame;
        public TMP_Text text;
        public RectTransform tap;
        [Tooltip("Cha của bàn tay: đặt lên đầu rắn.")]
        public RectTransform handRoot;
        public RectTransform hand;
        public RectTransform trail;
        [Tooltip("Lớp tối chừa một làn mềm dọc đường của cú vuốt bắt buộc (màn 1), tắt sẵn; TutorialView vẽ ảnh mặt nạ vào.")]
        public RawImage pathOverlay;
    }
}
