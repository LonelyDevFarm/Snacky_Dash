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
    /// Each swipe says whether it is a further flick of a finger that stayed down: the game only
    /// buffers the first flick of a touch (see GameSession.RequestMove).
    /// Uses the legacy Input Manager (the project's active input handling).
    /// </summary>
    public class SwipeInput : MonoBehaviour
    {
        [SerializeField] private float swipeThresholdPixels = 18f;
        [SerializeField] private float referenceDpi = 200f;
        [SerializeField] private float flickWindowSeconds = 0.2f;

        /// <summary>Direction, and true when the finger has not been lifted since its previous flick.</summary>
        public event Action<Direction, bool> Swiped;

        private readonly List<(float time, Vector2 position)> _samples = new List<(float, Vector2)>();
        private bool _pressed;
        private int _flicksThisPress;

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
                _flicksThisPress = 0;
                _samples.Clear();
            }
            if (UnityEngine.Input.GetMouseButtonUp(0) || !UnityEngine.Input.GetMouseButton(0)) _pressed = false;
            if (!_pressed) return;

            float now = Time.unscaledTime;
            Vector2 position = UnityEngine.Input.mousePosition;
            _samples.Add((now, position));
            while (_samples.Count > 1 && now - _samples[0].time > flickWindowSeconds) _samples.RemoveAt(0);

            var delta = position - _samples[0].position;
            // LeanTouch scales by dpi / 200; on a ~96 dpi desktop that halves the threshold to ~9 px
            // and the mouse flicks again on the slightest move, so never go below the reference.
            float scale = Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / referenceDpi) : 1f;
            if (delta.magnitude < swipeThresholdPixels * scale) return;

            var direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                ? (delta.x > 0f ? Direction.Right : Direction.Left)
                : (delta.y > 0f ? Direction.Up : Direction.Down);
            Swiped?.Invoke(direction, _flicksThisPress++ > 0);

            // Re-arm: the next flick has to be a fresh movement from here.
            _samples.Clear();
            _samples.Add((now, position));
        }

        private void ReadKeyboard()
        {
            // Every key press counts as a fresh touch.
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W)) Swiped?.Invoke(Direction.Up, false);
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S)) Swiped?.Invoke(Direction.Down, false);
            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A)) Swiped?.Invoke(Direction.Left, false);
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D)) Swiped?.Invoke(Direction.Right, false);
        }
    }
}
