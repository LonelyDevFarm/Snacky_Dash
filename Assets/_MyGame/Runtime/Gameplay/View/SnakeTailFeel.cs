using System;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Thân rắn mềm như video gốc (Lv43 giây 38-40, Lv17 giây 86): góc rẽ của đường luôn bo tròn, chạy hay đứng yên
    /// (đốt lệch khỏi đường thẳng, hộp không xoay), nên thân uốn như sợi dây, đốt vẫn liền nhau; dừng không đổi hình [user 15/10]. Trên vòm cầu đốt
    /// xếp sát theo độ dài thật của vòm (SnakeView). Chỉ đổi hình, luật chơi không đổi. SnakeView gọi.
    /// </summary>
    public class SnakeTailFeel : MonoBehaviour
    {
        private const int Samples = 5; // số điểm lấy trung bình khi bo góc

        [Tooltip("Bật / tắt cả hiệu ứng (tắt: thân bám cứng vào đường, gãy vuông ở góc).")]
        [SerializeField] private bool feelEnabled = true;

        [Header("Bo góc")]
        [Tooltip("Độ bo góc (ô), chạy hay đứng yên như nhau. Tăng: góc cong rộng, mềm hơn. Giảm: góc gắt hơn. 0: không bo.")]
        [SerializeField, Range(0f, 2f)] private float cornerRadius = 0.8f;

        public bool Enabled => feelEnabled;

        /// <summary>Độ bo góc hiện tại (ô); 0 khi tắt hiệu ứng.</summary>
        public float Radius => feelEnabled ? cornerRadius : 0f;

        /// <summary>
        /// Điểm trên đường đã bo: trung bình các điểm quanh <paramref name="distance"/> trong ±radius (không vượt qua đầu).
        /// Gặp chỗ nhảy (miệng hầm) thì dùng điểm gốc để không kéo đốt ngang bàn.
        /// </summary>
        public Vector2 Smooth(Func<float, Vector2> sample, float distance, float radius)
        {
            var exact = sample(distance);
            if (radius <= 0f) return exact;
            float from = Mathf.Max(0f, distance - radius), to = distance + radius;
            float step = (to - from) / (Samples - 1);
            var sum = Vector2.zero;
            var previous = sample(from);
            for (int k = 0; k < Samples; k++)
            {
                var point = k == 0 ? previous : sample(from + step * k);
                if ((point - previous).sqrMagnitude > step * step * 4f + 0.0001f) return exact;
                sum += point;
                previous = point;
            }
            return sum / Samples;
        }
    }
}
