using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Chạy lại từ đầu clip của component Animation (legacy) gắn trên một object, ví dụ LB_Pop (nảy) hay LB_ChestShake (rung) của
    /// Lucky Ladybug. Clip làm và chỉnh trong cửa sổ Animation của Unity; code chỉ bấm chạy. Trả về độ dài clip để chờ (0 nếu không có).
    /// </summary>
    public static class ClipPlayer
    {
        public static float Play(Component target)
        {
            var animation = target != null ? target.GetComponent<Animation>() : null;
            if (animation == null || animation.clip == null) return 0f;
            animation.Stop();
            animation.Play();
            return animation.clip.length;
        }
    }
}
