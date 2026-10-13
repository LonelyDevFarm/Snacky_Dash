using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Rung camera kiểu DOTween DOShakePosition, tự gắn lên Camera.main; SnakeView gọi Hit (rắn cắn đuôi), CratesView gọi
    /// CrateExit (crate đầy rời đi). Số lấy từ gốc: PlayerView.HandleTailHit và Box_Crate_9_VO BoxFeedbackView.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _applied, _expected;
        private Vector3 _offset;
        private Coroutine _running;

        public static void Hit() => Shake(0.1f, 0.3f, 0.4f, 20, 90f);
        public static void CrateExit() => Shake(0f, 0.2f, 0.09f, 22, 90f);

        public static void Shake(float delay, float duration, float strength, int vibrato, float randomness)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var shaker = camera.GetComponent<CameraShake>();
            if (shaker == null) shaker = camera.gameObject.AddComponent<CameraShake>();
            if (shaker._running != null) shaker.StopCoroutine(shaker._running);
            shaker._running = shaker.StartCoroutine(shaker.Run(delay, duration, strength, vibrato, randomness));
        }

        private IEnumerator Run(float delay, float duration, float strength, int vibrato, float randomness)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            int steps = Mathf.Max(2, (int)(vibrato * duration));
            float stepTime = duration / steps;
            float angle = Random.Range(0f, 360f);
            Vector3 from = _offset;
            for (int i = 0; i < steps; i++)
            {
                Vector3 to;
                if (i == steps - 1) to = Vector3.zero;
                else
                {
                    float size = strength * (steps - i) / steps; // nhỏ dần về 0
                    angle = angle - 180f + Random.Range(-randomness, randomness);
                    float rad = angle * Mathf.Deg2Rad;
                    to = (transform.right * Mathf.Cos(rad) + transform.up * Mathf.Sin(rad)) * size;
                }
                for (float t = 0f; t < stepTime; t += Time.deltaTime)
                {
                    _offset = Vector3.Lerp(from, to, Tween.OutQuad(t / stepTime));
                    yield return null;
                }
                from = _offset = to;
            }
            _offset = Vector3.zero;
            _running = null;
        }

        private void LateUpdate()
        {
            // Gỡ độ lệch frame trước; nếu BoardCameraFitter vừa đặt lại camera thì lấy chỗ đó làm gốc.
            var position = transform.position;
            var basePosition = (position - _expected).sqrMagnitude < 1e-8f ? position - _applied : position;
            transform.position = basePosition + _offset;
            _applied = _offset;
            _expected = transform.position;
        }
    }
}
