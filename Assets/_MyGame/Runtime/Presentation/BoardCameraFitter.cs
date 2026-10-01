using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Fits the orthographic board camera to the level, using the values serialized on the original
    /// LevelCameraFitter in Game.unity: bounds expanded by (3, 4) cells, camera pulled back 13 units
    /// along its view axis, fit area = canvas (1080x1920 reference, match height) inset by 10 x 287.78
    /// and shifted up by 30. The fitting maths itself is reconstructed (the original body is stripped).
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

        public void Fit(Bounds board)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            var bounds = board;
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
            float fitHeightFraction = (referenceHeight - fitAreaHeightInset) / referenceHeight;

            float sizeForHeight = (maxY - minY) * 0.5f / fitHeightFraction;
            float sizeForWidth = (maxX - minX) * 0.5f / (fitWidthFraction * _camera.aspect);
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // Board centre should land on the fit-area centre, which sits fitAreaOffsetY above screen centre.
            float offsetWorld = fitAreaOffsetY / referenceHeight * 2f * _camera.orthographicSize;
            transform.position = bounds.center - forward * distance - up * offsetWorld;
        }
    }
}
