using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Các ngôi sao con nhấp nháy (lớn lên, nhỏ lại và xoay) quanh một thứ nhận được, ví dụ thẻ Snacky Pass có CLAIM (sk2). Gắn lên
    /// cha của các ngôi sao; mỗi sao lệch nhịp một chút. Bật / tắt cả nhóm bằng SetActive.
    /// </summary>
    public class Sparkles : MonoBehaviour
    {
        [SerializeField] private float speed = 2.4f;
        [Tooltip("Độ / giây.")]
        [SerializeField] private float spin = 40f;
        [Tooltip("Lệch nhịp giữa 2 sao liền nhau.")]
        [SerializeField] private float phaseStep = 1.3f;

        private void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < transform.childCount; i++)
            {
                float phase = i * phaseStep;
                var star = transform.GetChild(i);
                star.localScale = Vector3.one * Mathf.Max(0f, Mathf.Sin(time * speed + phase));
                star.localRotation = Quaternion.Euler(0f, 0f, time * spin + phase * 30f);
            }
        }
    }
}
