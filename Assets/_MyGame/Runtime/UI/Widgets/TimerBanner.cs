using System;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Đồng hồ đếm ngược dùng chung (dải đỏ + mặt đồng hồ): hiện thời gian còn lại dạng "1D 04H" / "5H 12M" / "12M 30S",
    /// kim nhảy một phần tư vòng mỗi giây. Đổi màu / hình trong prefab; script nào cần thì gọi <see cref="Show"/>.
    /// </summary>
    public class TimerBanner : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [Tooltip("Kim đồng hồ (xoay mỗi giây). Để trống nếu không có.")]
        [SerializeField] private Transform hand;
        [Tooltip("Thời gian kim xoay một nấc (giây).")]
        [SerializeField, Min(0.01f)] private float tickTime = 0.2f;

        /// <summary>Hiện thời gian còn lại.</summary>
        public void Show(TimeSpan left) => text.text = Format(left);

        /// <summary>Hiện chữ bất kỳ thay cho thời gian (vd. "Start").</summary>
        public void ShowText(string words) => text.text = words;

        public static string Format(TimeSpan left) =>
            left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H"
            : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes:00}M"
            : $"{left.Minutes}M {left.Seconds:00}S";

        private void Update()
        {
            if (hand == null) return;
            float time = Time.unscaledTime;
            int steps = Mathf.FloorToInt(time);
            hand.localRotation = Quaternion.Euler(0f, 0f, -90f * (steps + SnackDash.Presentation.Tween.OutQuad(Mathf.Clamp01((time - steps) / tickTime))));
        }
    }
}
