using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// The original's camera shakes (DOTween DOShakePosition with fade-out, values from the original):
    /// <list type="bullet">
    /// <item>Hit Camera Shake when the snake bites its tail [PlayerView.HandleTailHit: delay 0.1, 0.3 s, strength 0.4,
    /// vibrato 20, randomness 90].</item>
    /// <item>Exit Camera Shake when a full crate leaves [Box_Crate_9_VO BoxFeedbackView: 0.2 s, strength 0.09, vibrato 22,
    /// randomness 90].</item>
    /// </list>
    /// Like DOTween: vibrato x duration steps, each a jump at an angle within ±randomness of the reverse of the last,
    /// shrinking to zero. The offset sits on top of wherever the camera fitter puts the camera.
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
                    float size = strength * (steps - i) / steps; // fade out
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
            // The camera without the shake: last frame's offset taken off, unless something (the fitter) has placed the
            // camera since, in which case where it put it is the base.
            var position = transform.position;
            var basePosition = (position - _expected).sqrMagnitude < 1e-8f ? position - _applied : position;
            transform.position = basePosition + _offset;
            _applied = _offset;
            _expected = transform.position;
        }
    }
}
