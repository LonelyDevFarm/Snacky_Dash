using System.Collections;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Khối nâng (prefab Prefabs/InGame/ldfs/visualoverhaul/RisingBlock): nhô lên / chìm xuống mặt đường, hạ xuống thì bung lá.
    /// Ô góc dùng mảnh góc. BoardView gọi theo sự kiện của GameSession.
    /// </summary>
    public class RisingBlockView : MonoBehaviour
    {
        [SerializeField] private float duration = 0.15f;
        [Tooltip("MeshesRoot.localPosition khi mở (chìm) / khi đóng (nhô lên).")]
        [SerializeField] private Vector3 open = new Vector3(0f, -0.01235f, 0f), closed = Vector3.zero;
        [SerializeField] private Transform meshesRoot;
        [SerializeField] private GameObject straightMesh, cornerMesh;
        [Tooltip("LeafExplosion _Small: bung lá khi khối hạ.")]
        [SerializeField] private ParticleSystem lowerParticles;
        [Tooltip("Góc xoay mảnh góc (độ) cho UpRight, DownRight, DownLeft, UpLeft; chỉnh theo hình.")]
        [SerializeField] private float[] cornerRotation = { 0f, -90f, 180f, 90f };

        private Coroutine _move;

        /// <summary>Chọn mảnh thẳng / mảnh góc theo đường bên dưới; bắt đầu ở trạng thái mở.</summary>
        public void Init(TileType road)
        {
            int corner = CornerIndex(road);
            if (straightMesh != null) straightMesh.SetActive(corner < 0);
            if (cornerMesh != null) cornerMesh.SetActive(corner >= 0);
            if (corner >= 0 && corner < cornerRotation.Length) transform.localRotation = Quaternion.Euler(0f, 0f, cornerRotation[corner]);
            Snap(false);
        }

        public void Snap(bool risen)
        {
            if (_move != null) StopCoroutine(_move);
            _move = null;
            if (meshesRoot != null) meshesRoot.localPosition = risen ? closed : open;
        }

        public void Animate(bool risen)
        {
            if (meshesRoot == null) return;
            if (_move != null) StopCoroutine(_move);
            _move = StartCoroutine(Move(risen));
            if (!risen && lowerParticles != null) lowerParticles.Play(true);
        }

        private IEnumerator Move(bool risen)
        {
            var from = meshesRoot.localPosition;
            var to = risen ? closed : open;
            yield return Tween.Run(duration, Tween.OutQuad, t => meshesRoot.localPosition = Vector3.LerpUnclamped(from, to, t));
            _move = null;
        }

        private static int CornerIndex(TileType road)
        {
            switch (road)
            {
                case TileType.CornerUpRight: return 0;
                case TileType.CornerDownRight: return 1;
                case TileType.CornerDownLeft: return 2;
                case TileType.CornerUpLeft: return 3;
                default: return -1;
            }
        }
    }
}
