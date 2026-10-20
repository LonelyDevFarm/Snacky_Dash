using System.Collections;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Cửa khay đóng / mở (prefab Prefabs/InGame/Crates/ShutterCrateVisual, dựng theo ShutterCrateVisual gốc 1.15): hai cánh
    /// trái / phải, mỗi cánh là mesh cửa trượt ra mép khay và tấm phẳng co lại. Số đo lấy từ AN_ShutterCrateOpening (0.333 s) và
    /// AN_ShutterCrateClosing (0.25 s); gốc chạy bằng Animator, ở đây tween cho gọn. CratesView gắn lên khay trên cùng của cột.
    /// </summary>
    public class ShutterCrateView : MonoBehaviour
    {
        [System.Serializable]
        private struct Panel
        {
            public Transform door;  // Mesh_ShutterCrate
            public Transform plane; // Quad
            public Renderer doorRenderer, planeRenderer;
        }

        [SerializeField] private Panel[] panels = new Panel[2];
        [Header("Tư thế cánh (cục bộ trong cánh; gốc AN_ShutterCrateOpening / Closing)")]
        [SerializeField] private Vector3 doorClosed = new Vector3(-0.107f, 0.12f, -0.028f);
        [SerializeField] private Vector3 doorOpen = new Vector3(-1.26f, 0.12f, -0.03f);
        [SerializeField] private Vector3 planeClosed = new Vector3(-0.754477f, 0.12f, -0.048f);
        [SerializeField] private Vector3 planeOpen = new Vector3(-1.3021f, 0.12f, -0.048f);
        [SerializeField] private Vector3 planeClosedScale = new Vector3(1.1037184f, 2.37f, 1f);
        [SerializeField] private Vector3 planeOpenScale = new Vector3(0f, 2.37f, 1f);
        [SerializeField] private float openDuration = 0.33333334f, closeDuration = 0.25f;
        [Tooltip("Cửa khép quá mức này (0 mở .. 1 đóng) thì ẩn trái trong khay, vì trái nằm cao hơn cửa và sẽ hiện đè lên [user].")]
        [Range(0f, 1f)]
        [SerializeField] private float hideFruitAt = 0.5f;

        private float _closed; // 0 mở .. 1 đóng
        private GameObject _fruits;

        /// <summary>Chỗ chứa trái của khay (AllDestinationPoints) để ẩn khi cửa đóng.</summary>
        public void HideWhenClosed(Transform fruits) => _fruits = fruits != null ? fruits.gameObject : null;
        private Coroutine _move;

        /// <summary>Màu cửa và tấm phẳng theo loại trái của khay (gốc SetColor).</summary>
        public void SetColor(Material door, Material plane)
        {
            foreach (var panel in panels)
            {
                if (door != null && panel.doorRenderer != null) panel.doorRenderer.sharedMaterial = door;
                if (plane != null && panel.planeRenderer != null) panel.planeRenderer.sharedMaterial = plane;
            }
        }

        public void Snap(bool closed)
        {
            if (_move != null) StopCoroutine(_move);
            _move = null;
            Apply(closed ? 1f : 0f);
        }

        public void Animate(bool closed)
        {
            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(Move(closed));
        }

        private IEnumerator Move(bool closed)
        {
            float from = _closed, to = closed ? 1f : 0f;
            float duration = (closed ? closeDuration : openDuration) * Mathf.Abs(to - from);
            yield return Tween.Run(duration, Tween.Linear, t => Apply(Mathf.Lerp(from, to, t)));
            _move = null;
        }

        private void Apply(float closed)
        {
            _closed = closed;
            if (_fruits != null) _fruits.SetActive(closed < hideFruitAt);
            foreach (var panel in panels)
            {
                if (panel.door != null) panel.door.localPosition = Vector3.Lerp(doorOpen, doorClosed, closed);
                if (panel.plane == null) continue;
                panel.plane.localPosition = Vector3.Lerp(planeOpen, planeClosed, closed);
                panel.plane.localScale = Vector3.Lerp(planeOpenScale, planeClosedScale, closed);
            }
        }
    }
}
