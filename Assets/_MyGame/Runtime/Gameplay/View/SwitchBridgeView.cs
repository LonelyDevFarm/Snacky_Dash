using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Cầu bật (prefab Prefabs/InGame/tilescovers/visualoverhaul/TileCov_Bridge_Switching_LR): mặt cầu (Rotate_Parent) dựng lên khi
    /// công tắc On, xoay xuống dưới mặt đường khi Off, trong 0.25 s (gốc BridgeView._bridgeAxeToRotate, _tweenDuration). Hai thanh cam
    /// hai bên đứng yên. BoardView gọi theo GameSession.SwitchBridgeChanged.
    /// </summary>
    public class SwitchBridgeView : MonoBehaviour
    {
        [Tooltip("Rotate_Parent: phần quay của mặt cầu.")]
        [SerializeField] private Transform deck;
        [Tooltip("Giây dựng / hạ (gốc BridgeView._tweenDuration 0.25).")]
        [SerializeField] private float duration = 0.25f;
        [Tooltip("Mặt cầu lăn sang ngang quanh trục này (cục bộ của deck) rồi chìm dưới đường [video lv61 30 fps].")]
        [SerializeField] private Vector3 downAxis = Vector3.up;
        [Tooltip("Góc lăn khi hạ (độ); đổi dấu để lăn chiều kia.")]
        [SerializeField] private float downAngle = 180f;

        private Quaternion _up;
        private bool _ready;
        private float _angle; // 0 = dựng, downAngle = hạ
        private Coroutine _move;

        private void Ready()
        {
            if (_ready || deck == null) return;
            _up = deck.localRotation;
            _ready = true;
        }

        private void SetAngle(float angle)
        {
            _angle = angle;
            deck.localRotation = _up * Quaternion.AngleAxis(angle, downAxis);
        }

        public void Snap(bool up)
        {
            Ready();
            if (deck == null) return;
            if (_move != null) StopCoroutine(_move);
            _move = null;
            SetAngle(up ? 0f : downAngle);
        }

        public void Animate(bool up)
        {
            Ready();
            if (deck == null) return;
            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(Move(up));
        }

        private IEnumerator Move(bool up)
        {
            float from = _angle, to = up ? 0f : downAngle;
            yield return Tween.Run(duration, Tween.OutQuad, t => SetAngle(Mathf.LerpUnclamped(from, to, t)));
            _move = null;
        }
    }
}
