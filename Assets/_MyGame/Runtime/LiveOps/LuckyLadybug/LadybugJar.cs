using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Lọ bắt bọ trên HUD (prefab Prefabs/UI/Screens/Game/LadybugJar, trong HUD ở chỗ đầu rắn của thanh tiến độ): chỉ hiện ở màn có bọ
    /// và khi đó đầu rắn ẩn đi, 2 trạng thái — rỗng / có 1 con
    /// (mỗi màn tối đa 1 con). Lớp từ dưới lên: kính, khung gỗ, bọ (đứng ở tâm sàn), nan gỗ phía trước, vệt sáng kính.
    /// LadybugView gọi <see cref="Fill"/> khi bọ bay tới: bọ hiện, lọ nảy (clip LB_Pop).
    /// </summary>
    public class LadybugJar : MonoBehaviour
    {
        [Tooltip("Con bọ trong lọ (tắt sẵn).")]
        [SerializeField] private GameObject bug;
        [Tooltip("Phần nảy khi bọ vào (cả lọ): component Animation với clip LB_Pop, sửa trong cửa sổ Animation.")]
        [SerializeField] private RectTransform body;
        [Tooltip("Đầu rắn tím bên trái thanh tiến độ (ProgressBar/Snacky): lọ đứng thay chỗ nó ở màn có bọ [user].")]
        [SerializeField] private GameObject replaces;

        /// <summary>Chỗ bọ đáp (điểm bay tới).</summary>
        public RectTransform Target => (RectTransform)bug.transform;

        public void Show(bool visible)
        {
            gameObject.SetActive(visible);
            if (replaces != null) replaces.SetActive(!visible);
            bug.SetActive(false);
        }

        public void Fill()
        {
            bug.SetActive(true);
            if (isActiveAndEnabled) ClipPlayer.Play(body);
        }
    }
}
