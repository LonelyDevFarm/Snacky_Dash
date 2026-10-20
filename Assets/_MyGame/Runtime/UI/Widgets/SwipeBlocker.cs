using SnackDash.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SnackDash.UI
{
    /// <summary>
    /// Gắn lên vùng UI kéo được (thanh booster lướt ngang): chạm bắt đầu ở đây, kể cả trên nút, không thành vuốt điều khiển rắn
    /// [user 20/10]. Nhận cả lúc chuẩn bị kéo vì nút không nhận sự kiện đó nên nó đi lên tới đây.
    /// </summary>
    public class SwipeBlocker : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler
    {
        public void OnPointerDown(PointerEventData eventData) => SwipeInput.IgnoreCurrentTouch();

        public void OnInitializePotentialDrag(PointerEventData eventData) => SwipeInput.IgnoreCurrentTouch();
    }
}
