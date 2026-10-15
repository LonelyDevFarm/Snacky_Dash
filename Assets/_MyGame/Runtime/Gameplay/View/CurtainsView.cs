using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Rèm che ô và bộ đếm của rèm (gốc CurtainManager, số trong CurtainConfig_VO; video Lv43); nằm cạnh BoardView trên bàn,
    /// GameBootstrap gọi Bind. Mảnh rèm là prefab dựng lại vì bản trích có hệ hạt Unity 6 làm Unity 2022 treo.
    /// </summary>
    public class CurtainsView : MonoBehaviour
    {
        [SerializeField] private Transform root;
        [Header("Mảnh rèm (TilesCovers/Curtain)")]
        [SerializeField] private GameObject innerPrefab;
        [SerializeField] private GameObject sidePrefab, cornerPrefab, innerCornerPrefab;
        [Tooltip("Góc xoay (độ) mảnh cạnh khi ô trống ở trên; mỗi phía kế theo chiều kim đồng hồ thêm -90.")]
        [SerializeField] private float sideAngleUp = 180f;
        [Tooltip("Góc xoay (độ) mảnh góc ngoài khi ô trống ở trên và phải.")]
        [SerializeField] private float cornerAngleUpRight = 0f;
        [Tooltip("Góc xoay (độ) mảnh góc trong khi ô chéo trên-phải trống.")]
        [SerializeField] private float innerCornerAngleUpRight = 0f;
        [Tooltip("Gốc: CoverViewScale / CoverViewPositionOffset.")]
        [SerializeField] private Vector3 coverScale = new Vector3(0.65f, 0.65f, 0.15f);
        [SerializeField] private Vector3 coverOffset = new Vector3(0f, 0f, -0.05f);

        [Header("Bộ đếm")]
        [SerializeField] private CurtainCounterView counterPrefab;
        [Tooltip("Gốc: CounterViewPositionOffset.")]
        [SerializeField] private Vector3 counterOffset = new Vector3(0f, 0f, -0.5f);
        [SerializeField] private FruitGraphicSet fruits;

        [Header("Mở rèm (ms của CurtainConfig_VO)")]
        [SerializeField] private float revealStartDelay = 0.15f;
        [SerializeField] private float groupStartDelay = 0.1f;
        [SerializeField] private float flashTime = 0.2f;
        [SerializeField] private float shutterTime = 0.4f;
        [SerializeField] private float completeDelay = 0.2f;
        [Tooltip("FX bung giữa rèm khi mở (gốc FX_Snake_Curtain_Reveal làm Unity 2022 treo, để trống).")]
        [SerializeField] private GameObject revealFxPrefab;
        [Tooltip("FX_CurtainSmoke: khói khi mảnh rèm biến mất.")]
        [SerializeField] private ParticleSystem smokePrefab;
        [Tooltip("Hạt khói mỗi ô (gốc 100; rèm tới 170 ô nên bớt cho nhẹ).")]
        [SerializeField] private int smokePerCell = 2;
        [Tooltip("Độ cao khói (âm = về phía camera; gốc FX_CurtainDestroy z -1.65).")]
        [SerializeField] private float smokeHeight = -1.5f;

        private class Group
        {
            public Transform root;
            public readonly List<GameObject> covers = new List<GameObject>();
            public readonly List<MeshRenderer> meshes = new List<MeshRenderer>(); // Visual_Container/Mesh mỗi mảnh, tìm một lần
            public CurtainCounterView counter;
            public float minY, maxY;
            public readonly List<Vector2Int> cells = new List<Vector2Int>();
        }

        private readonly List<Group> _groups = new List<Group>();
        private MaterialPropertyBlock _block;
        private BoardView _board;
        private static readonly int Shutter = Shader.PropertyToID("_Shutter");
        private static readonly int MinY = Shader.PropertyToID("_MinY");
        private static readonly int MaxY = Shader.PropertyToID("_MaxY");
        private static readonly int ParentWorldPos = Shader.PropertyToID("_ParentWorldPos");
        private static readonly int FlashStrength = Shader.PropertyToID("_FlashStrength");

        /// <summary>Từ lúc rèm mở tới lúc đường bên dưới hiện hết; CratesView đợi chừng này mới hiện crate dưới rèm.</summary>
        public float RevealTime => revealStartDelay + Mathf.Max(flashTime, shutterTime) + completeDelay;

        public void Bind(GameSession session)
        {
            _block = new MaterialPropertyBlock();
            var board = GetComponent<BoardView>();
            _board = board;
            var parent = root != null ? root : transform;
            var cells = session.Board.CurtainCells;
            foreach (var curtain in session.Curtains)
            {
                var group = new Group();
                var centre = ((Vector2)curtain.CounterMin + curtain.CounterMax) * 0.5f;
                group.root = new GameObject($"Curtain {curtain.Group}").transform;
                group.root.SetParent(parent, false);
                group.root.localPosition = new Vector3(centre.x, centre.y, 0f);
                group.minY = float.MaxValue;
                group.maxY = float.MinValue;
                foreach (var pair in cells)
                {
                    if (pair.Value != curtain.Group) continue;
                    if (board != null) board.SetMechanicVisible(pair.Key, false); // đá, mũi tên... chỉ hiện khi rèm mở
                    group.cells.Add(pair.Key);
                    var cover = SpawnCover(pair.Key, curtain.Group, cells, group.root);
                    if (cover != null)
                    {
                        group.covers.Add(cover);
                        var mesh = cover.transform.Find("Visual_Container/Mesh");
                        if (mesh != null && mesh.TryGetComponent<MeshRenderer>(out var renderer)) group.meshes.Add(renderer);
                    }
                    group.minY = Mathf.Min(group.minY, pair.Key.y - 0.5f);
                    group.maxY = Mathf.Max(group.maxY, pair.Key.y + 0.5f);
                }
                if (counterPrefab != null)
                {
                    group.counter = Instantiate(counterPrefab, group.root);
                    group.counter.transform.localPosition = counterOffset;
                    var entry = fruits != null ? fruits.Get(curtain.Kind) : null;
                    group.counter.Init(entry == null ? null : entry.fieldSprite != null ? entry.fieldSprite : entry.sprite, curtain.Remaining);
                }
                _groups.Add(group);
                SetShutter(group, 0f);
            }
            session.CurtainCountChanged += (index, remaining) =>
            {
                if (index < _groups.Count && _groups[index].counter != null) _groups[index].counter.SetCount(remaining);
            };
            session.CurtainOpened += index => { if (index < _groups.Count) StartCoroutine(Reveal(_groups[index], index)); };
            // Undo làm BoardView hiện lại đá: ô còn dưới rèm thì ẩn lại.
            session.Rewound += () =>
            {
                for (int i = 0; i < _groups.Count; i++)
                    if (!session.Curtains[i].Open && _board != null) foreach (var cell in _groups[i].cells) _board.SetMechanicVisible(cell, false);
            };
        }

        private GameObject SpawnCover(Vector2Int cell, int group, IReadOnlyDictionary<Vector2Int, int> cells, Transform parent)
        {
            bool Has(int dx, int dy) => cells.TryGetValue(cell + new Vector2Int(dx, dy), out var g) && g == group;
            bool up = Has(0, 1), right = Has(1, 0), down = Has(0, -1), left = Has(-1, 0);
            GameObject prefab = innerPrefab;
            float angle = 0f;
            int sides = (up ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0);
            if (sides == 4)
            {
                // Mảnh giữa, hoặc góc trong nếu thiếu một ô chéo (gốc CurtainCoverResolver.TryResolveInnerCorner).
                if (!Has(1, 1)) { prefab = innerCornerPrefab; angle = innerCornerAngleUpRight; }
                else if (!Has(1, -1)) { prefab = innerCornerPrefab; angle = innerCornerAngleUpRight - 90f; }
                else if (!Has(-1, -1)) { prefab = innerCornerPrefab; angle = innerCornerAngleUpRight - 180f; }
                else if (!Has(-1, 1)) { prefab = innerCornerPrefab; angle = innerCornerAngleUpRight - 270f; }
            }
            else if (sides == 3)
            {
                prefab = sidePrefab;
                angle = sideAngleUp - (!up ? 0f : !right ? 90f : !down ? 180f : 270f);
            }
            else if (sides == 2 && up != down)
            {
                prefab = cornerPrefab;
                angle = cornerAngleUpRight - (!up && !right ? 0f : !right && !down ? 90f : !down && !left ? 180f : 270f);
            }
            else if (sides < 4) prefab = sidePrefab; // dải rộng một ô: hiếm, dùng mảnh cạnh
            if (prefab == null) prefab = innerPrefab;
            if (prefab == null) return null;

            var cover = Instantiate(prefab, parent);
            cover.name = $"{prefab.name} ({cell.x},{cell.y})";
            cover.transform.position = parent.parent.TransformPoint(BoardView.CellToWorld(cell) + coverOffset);
            cover.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            cover.transform.localScale = coverScale;
            return cover;
        }

        private IEnumerator Reveal(Group group, int index)
        {
            yield return new WaitForSeconds(groupStartDelay * index);
            if (group.counter != null) StartCoroutine(group.counter.Reveal());
            if (revealFxPrefab != null)
            {
                var fx = Instantiate(revealFxPrefab, group.root);
                fx.transform.localPosition = counterOffset;
                foreach (var system in fx.GetComponentsInChildren<ParticleSystem>(true)) system.Play(false);
                Destroy(fx, 3f);
            }
            yield return new WaitForSeconds(revealStartDelay);
            float time = 0f, end = Mathf.Max(flashTime, shutterTime);
            while (time < end)
            {
                time += Time.deltaTime;
                SetFlash(group, Mathf.Clamp01(time / flashTime));
                SetShutter(group, Mathf.Clamp01(time / shutterTime));
                yield return null;
            }
            foreach (var cover in group.covers)
            {
                if (cover == null) continue;
                var visual = cover.transform.Find("Visual_Container");
                if (visual != null) visual.gameObject.SetActive(false);
                Destroy(cover, 1.5f);
            }
            if (_board != null) foreach (var cell in group.cells) _board.SetMechanicVisible(cell, true);
            Smoke(group);
            yield return new WaitForSeconds(completeDelay);
        }

        /// <summary>Một hệ hạt cho cả rèm thay vì mỗi mảnh một FX như gốc, cho nhẹ máy.</summary>
        private void Smoke(Group group)
        {
            if (smokePrefab == null || smokePerCell <= 0) return;
            var smoke = Instantiate(smokePrefab, group.root);
            var emit = new ParticleSystem.EmitParams();
            var board = group.root.parent;
            foreach (var cell in group.cells)
                for (int i = 0; i < smokePerCell; i++)
                {
                    emit.position = board.TransformPoint(BoardView.CellToWorld(cell) + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), smokeHeight));
                    smoke.Emit(emit, 1);
                }
            Destroy(smoke.gameObject, smoke.main.startLifetime.constantMax + 0.5f);
        }

        private void SetShutter(Group group, float progress)
        {
            var pos = group.root.position;
            foreach (var renderer in group.meshes)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(Shutter, progress);
                _block.SetFloat(MinY, group.minY - pos.y);
                _block.SetFloat(MaxY, group.maxY - pos.y);
                _block.SetVector(ParentWorldPos, pos);
                renderer.SetPropertyBlock(_block);
            }
        }

        private void SetFlash(Group group, float strength)
        {
            foreach (var renderer in group.meshes)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_block);
                _block.SetFloat(FlashStrength, strength);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
