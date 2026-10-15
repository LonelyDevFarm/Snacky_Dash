using System;
using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Minimal coroutine tweens. Easing formulas follow DOTween, whose Ease ids are what the
    /// original serialized (3 = OutSine, 26 = InBack, 27 = OutBack, with an overshoot value).
    /// </summary>
    public static class Tween
    {
        public static float Linear(float t) => t;
        public static float OutSine(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) => 1f - (1f - t) * (1f - t) * (1f - t); // DOTween Ease 9
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static Func<float, float> OutBack(float overshoot) => t =>
        {
            t -= 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        };

        public static Func<float, float> InBack(float overshoot) => t => t * t * ((overshoot + 1f) * t - overshoot);

        /// <summary>Calls <paramref name="step"/> with eased progress 0..1 over <paramref name="duration"/> seconds.</summary>
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step) => Run(duration, ease, step, false);

        /// <param name="unscaled">Runs on real time, for UI that must move while the game is paused (timeScale 0).</param>
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step, bool unscaled)
        {
            for (float time = 0f; time < duration; time += unscaled ? Time.unscaledDeltaTime : Time.deltaTime)
            {
                if (!Step(step, ease(time / duration))) yield break;
                yield return null;
            }
            Step(step, ease(1f));
        }

        /// <summary>One step; false once what it animates has been destroyed (a popup closed mid-tween), which ends the tween.</summary>
        private static bool Step(Action<float> step, float t)
        {
            try { step(t); return true; }
            catch (MissingReferenceException) { return false; }
        }

        /// <summary>Goes to <paramref name="peak"/> and back to 1 over two halves (a "punch").</summary>
        public static IEnumerator Punch(float halfDuration, Action<float> step)
        {
            yield return Run(halfDuration, OutSine, step);
            yield return Run(halfDuration, OutSine, t => step(1f - t));
        }
    }
}
