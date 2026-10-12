using System;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Popup "Jungle Heist is live!" (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistJoin</c>, gốc: Popup_Lava_Quest_Join): khung gỗ, tranh
    /// thuyền kho báu với vầng sáng và tia sáng xoay, "Grand prize 10 000", lời giới thiệu và nút JOIN. Sửa hình / chữ trong prefab.
    /// Mở bằng <c>UIManager.Show&lt;JungleHeistJoin&gt;()</c>, nghe <see cref="Joined"/>; nút X đóng mà không tham gia.
    /// </summary>
    public class JungleHeistJoin : BasePopup
    {
        [Tooltip("Nút JOIN (khối JungleGreenButton).")]
        [SerializeField] private UiButton joinButton;
        [Tooltip("Tia sáng sau thuyền (xoay liên tục).")]
        [SerializeField] private RectTransform rays;
        [Tooltip("Tốc độ xoay của tia sáng (độ / giây, âm = theo chiều kim đồng hồ).")]
        [SerializeField] private float raysSpeed = -12f;

        /// <summary>Người chơi bấm JOIN (sau khi popup đóng).</summary>
        public event Action Joined;

        protected override void OnInit() => joinButton.Clicked += () => Close(() => Joined?.Invoke());

        private void Update()
        {
            if (rays != null) rays.localRotation *= Quaternion.Euler(0f, 0f, raysSpeed * Time.unscaledDeltaTime);
        }
    }
}
