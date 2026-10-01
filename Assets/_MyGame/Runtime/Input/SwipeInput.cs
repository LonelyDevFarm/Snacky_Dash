using System;
using System.Collections.Generic;
using SnackDash.Gameplay;
using UnityEngine;

namespace SnackDash.Input
{
    /// <summary>
    /// Swipe detection matching the original setup (Game.unity): LeanTouch swipeThreshold 18 px at
    /// referenceDpi 200, tapThreshold 0.2 s, and a LeanFingerFlick in "Multiple" mode - a finger that
    /// stays down can flick again and again; each time it travels the threshold within the last
    /// 0.2 s a new direction fires. Direction snaps to the dominant axis (SwipeController
    /// _maxSnapAngle = 45). Arrow keys / WASD work too, for testing in the Editor.
    /// Uses the legacy Input Manager (the project's active input handling).
    /// </summary>
    public class SwipeInput : MonoBehaviour
    {
        [SerializeField] private float swipeThresholdPixels = 18f;
        [SerializeField] private float referenceDpi = 200f;
        [SerializeField] private float flickWindowSeconds = 0.2f;

        public event Action<Direction> Swiped;

        private readonly List<(float time, Vector2 position)> _samples = new List<(float, Vector2)>();
        private bool _pressed;

        private void Update()
        {
            ReadKeyboard();
            ReadPointer();
        }

        private void ReadPointer()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _pressed = true;
                _samples.Clear();
            }
            if (UnityEngine.Input.GetMouseButtonUp(0)) _pressed = false;
            if (!_pressed) return;

            float now = Time.unscaledTime;
            Vector2 position = UnityEngine.Input.mousePosition;
            _samples.Add((now, position));
            while (_samples.Count > 1 && now - _samples[0].time > flickWindowSeconds) _samples.RemoveAt(0);

            var delta = position - _samples[0].position;
            float scale = Screen.dpi > 0f ? Screen.dpi / referenceDpi : 1f;
            if (delta.magnitude < swipeThresholdPixels * scale) return;

            Swiped?.Invoke(Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0f ? Direction.Right : Direction.Left)
                : (delta.y > 0f ? Direction.Up : Direction.Down));

            // Re-arm: the next flick has to be a fresh movement from here.
            _samples.Clear();
            _samples.Add((now, position));
        }

        private void ReadKeyboard()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W)) Swiped?.Invoke(Direction.Up);
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S)) Swiped?.Invoke(Direction.Down);
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A)) Swiped?.Invoke(Direction.Left);
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D)) Swiped?.Invoke(Direction.Right);
        }
    }
}
