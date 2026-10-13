using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Canh camera trực giao (Main Camera trong Game.unity) cho vừa màn; số lấy từ LevelCameraFitter gốc, phép tính tự dựng lại.
    /// GameBootstrap gọi Fit; LoseFlowView và BoosterBarView gọi FitBetween / FitDefault.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BoardCameraFitter : MonoBehaviour
    {
        [SerializeField] private float expandBoundsX = 3f;
        [SerializeField] private float expandBoundsY = 4f;
        [SerializeField] private float distance = 13f;

        [Header("Fit area (canvas units, reference 1080x1920, match height)")]
        [SerializeField] private float referenceHeight = 1920f;
        [SerializeField] private float fitAreaWidthInset = 10f;
        [SerializeField] private float fitAreaHeightInset = 287.78442f;
        [SerializeField] private float fitAreaOffsetY = 30.041992f;

        private Camera _camera;
        private Bounds _board;
        private Coroutine _zoom;

        public void Fit(Bounds board)
        {
            _board = board;
            Compute(referenceHeight - fitAreaHeightInset, fitAreaOffsetY, out var size, out var position);
            Apply(size, position);
        }

        /// <summary>Đưa bàn vào khoảng giữa <paramref name="bottom"/> và <paramref name="top"/> (đơn vị canvas, tính trong safe area).</summary>
        public void FitBetween(float bottom, float top, float duration)
        {
            Compute(top - bottom, (bottom + top) * 0.5f - referenceHeight * 0.5f, out var size, out var position);
            TweenTo(size, position, duration);
        }

        public void FitDefault(float duration)
        {
            Compute(referenceHeight - fitAreaHeightInset, fitAreaOffsetY, out var size, out var position);
            TweenTo(size, position, duration);
        }

        private void Compute(float areaHeight, float areaOffsetY, out float size, out Vector3 position)
        {
            if (_camera == null) _camera = GetComponent<Camera>();

            // HUD và thanh booster nằm trong safe area; giữ bàn trong đó luôn, không thì máy tai thỏ bị thanh che bàn.
            var safe = Screen.safeArea;
            float insetTop = Screen.height > 0 ? (Screen.height - safe.yMax) / Screen.height * referenceHeight : 0f;
            float insetBottom = Screen.height > 0 ? safe.yMin / Screen.height * referenceHeight : 0f;
            areaHeight -= insetTop + insetBottom;
            areaOffsetY += (insetBottom - insetTop) * 0.5f;
            var bounds = _board;
            bounds.Expand(new Vector3(expandBoundsX, expandBoundsY, 0f));

            // Camera nghiêng nên phải chiếu 8 góc vào không gian camera để lấy khung.
            var rotation = transform.rotation;
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up, forward = rotation * Vector3.forward;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                float cx = Vector3.Dot(corner - bounds.center, right);
                float cy = Vector3.Dot(corner - bounds.center, up);
                minX = Mathf.Min(minX, cx); maxX = Mathf.Max(maxX, cx);
                minY = Mathf.Min(minY, cy); maxY = Mathf.Max(maxY, cy);
            }

            float canvasWidth = referenceHeight * _camera.aspect;
            float fitWidthFraction = (canvasWidth - fitAreaWidthInset) / canvasWidth;
            float fitHeightFraction = areaHeight / referenceHeight;

            float sizeForHeight = (maxY - minY) * 0.5f / fitHeightFraction;
            float sizeForWidth = (maxX - minX) * 0.5f / (fitWidthFraction * _camera.aspect);
            size = Mathf.Max(sizeForHeight, sizeForWidth);

            // Tâm bàn nằm ở tâm vùng canh, cao hơn tâm màn hình areaOffsetY.
            float offsetWorld = areaOffsetY / referenceHeight * 2f * size;
            position = bounds.center - forward * distance - up * offsetWorld;
        }

        private void Apply(float size, Vector3 position)
        {
            _camera.orthographic = true;
            _camera.orthographicSize = size;
            transform.position = position;
        }

        private void TweenTo(float size, Vector3 position, float duration)
        {
            if (_zoom != null) StopCoroutine(_zoom);
            _zoom = StartCoroutine(Zoom(size, position, duration));
        }

        private IEnumerator Zoom(float size, Vector3 position, float duration)
        {
            float fromSize = _camera.orthographicSize;
            var fromPosition = transform.position;
            yield return Tween.Run(duration, Tween.OutQuad, t =>
                Apply(Mathf.LerpUnclamped(fromSize, size, t), Vector3.LerpUnclamped(fromPosition, position, t)));
            _zoom = null;
        }
    }
}
