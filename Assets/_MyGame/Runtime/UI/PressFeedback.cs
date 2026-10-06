using SnackDash.Audio;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SnackDash.UI
{
    /// <summary>
    /// The original buttons' Feedbacks (Pointer Down / Pointer Up, scale 0.9 then back): the button dips
    /// while held, springs back on release, with the Click sound and a light haptic.
    /// </summary>
    public class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private const float Pressed = 0.92f, Speed = 18f;
        private Vector3 _rest;
        private float _target = 1f, _scale = 1f;

        public void OnPointerDown(PointerEventData eventData)
        {
            // The size at the press: buttons that pop in from 0 (CONTINUE) must not dip from their start size.
            if (Mathf.Approximately(_scale, 1f)) _rest = transform.localScale;
            _target = Pressed;
            SoundPlayer.Play("Click");
            Haptics.Light();
        }

        public void OnPointerUp(PointerEventData eventData) => _target = 1f;

        private void Update()
        {
            if (Mathf.Approximately(_scale, _target)) return;
            _scale = Mathf.MoveTowards(_scale, _target, Speed * Time.unscaledDeltaTime);
            if (_rest == Vector3.zero) return;
            transform.localScale = _rest * _scale;
        }
    }
}
