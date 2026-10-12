using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Fits the orthographic board camera to the level, using the values serialized on the original
    /// LevelCameraFitter in Game.unity: bounds expanded by (3, 4) cells, camera pulled back 13 units
    /// along its view axis, fit area = canvas (1080x1920 reference, match height) inset by 10 x 287.78
    /// and shifted up by 30. The fitting maths itself is reconstructed (the original body is stripped).
    /// While the lose buttons are shown the board is refitted between them and the HUD (FitBetween).
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

        /// <summary>
        /// Tween the board into the area between <paramref name="bottom"/> and <paramref name="top"/>
        /// canvas units, counted inside the safe area (see Compute).
        /// </summary>
        public void FitBetween(float bottom, float top, float duration)
        {
            Compute(top - bottom, (bottom + top) * 0.5f - referenceHeight * 0.5f, out var size, out var position);
            TweenTo(size, position, duration);
        }

        /// <summary>Tween back to the normal play framing.</summary>
        public void FitDefault(float duration)
        {
            Compute(referenceHeight - fitAreaHeightInset, fitAreaOffsetY, out var size, out var position);
            TweenTo(size, position, duration);
        }

        private void Compute(float areaHeight, float areaOffsetY, out float size, out Vector3 position)
        {
            if (_camera == null) _camera = GetComponent<Camera>();

            // The HUD and the booster bar sit inside Screen.safeArea (like the original's
            // _safe_area_fitter), so on a notched phone they move in from the top and bottom: keep the
            // board inside the safe area too, or the bar overlaps it (Simulator, iPhone).
            var safe = Screen.safeArea;
            float insetTop = Screen.height > 0 ? (Screen.height - safe.yMax) / Screen.height * referenceHeight : 0f;
            float insetBottom = Screen.height > 0 ? safe.yMin / Screen.height * referenceHeight : 0f;
            areaHeight -= insetTop + insetBottom;
            areaOffsetY += (insetBottom - insetTop) * 0.5f;
            var bounds = _board;
            bounds.Expand(new Vector3(expandBoundsX, expandBoundsY, 0f));

            // Extent of the bounds in camera space (the camera is tilted, so project the corners).
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

            // Board centre should land on the fit-area centre, which sits areaOffsetY above screen centre.
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
