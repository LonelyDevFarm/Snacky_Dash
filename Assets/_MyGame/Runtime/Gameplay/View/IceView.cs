using System.Collections.Generic;
using SnackDash.Gameplay;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Hình của cơ chế Mặt băng (phần sáng tạo, bản gốc không có): mỗi đoạn băng là một tấm băng liền chạy dọc đường đi, từ mép điểm
    /// dừng đầu tới mép điểm dừng cuối [user 21/10]; số độ nặng ở ô giữa đoạn. Tấm lùi vào trong mặt đường, đầu cắt vuông bo góc, khúc
    /// cua bo tròn, viền sương trắng, có thành dày và bóng đổ (theo ảnh mẫu của user) — hình do shader SH_Ice vẽ, script dựng lưới và
    /// điều khiển hiệu ứng. Điểm dừng bị băng nuốt thì ẩn dấu, băng vỡ thì hiện. Gắn trên Board, cạnh BoardView.
    /// Hiệu ứng (chỉ là hình, luật giữ nguyên) [user 21/10]:
    /// - Rắn đủ nặng chạm băng: vết nứt lan từ chỗ đầu chạm; tới điểm dừng trong đoạn (lúc băng vỡ theo luật) thì nứt khắp tấm, tấm
    ///   tan dần, mảnh băng văng ra từng ô theo làn sóng từ chỗ rắn đứng (prefab Ice_Break). Dừng mà chưa vỡ thì vết nứt liền lại.
    /// - Rắn nhẹ chạm băng: lóe sáng chỗ chạm; trượt qua thì vệt sáng chạy theo đầu rắn, mỗi ô lấp lánh khi đầu đi qua (FX_Slide).
    /// - Hoàn tác trả lại băng: tấm đóng băng lại (hiện dần) kèm lấp lánh.
    /// </summary>
    public class IceView : MonoBehaviour
    {
        [Tooltip("Prefabs/InGame/Mechanics/Ice/Ice_Slab: mỗi ô băng một cái, số độ nặng (WeightText), lấp lánh khi trượt qua (FX_Slide).")]
        [SerializeField] private GameObject slabPrefab;
        [Tooltip("Prefabs/InGame/Mechanics/Ice/Ice_Break: mảnh băng văng + bụi sương khi vỡ (mỗi ô của đoạn).")]
        [SerializeField] private GameObject breakPrefab;
        [Tooltip("Vật liệu tấm băng (MAT_Ice, shader SH_Ice): bề ngang, độ bo, màu, viền sương... chỉnh ở đây.")]
        [SerializeField] private Material iceMaterial;
        [Tooltip("Băng nổi trên mặt đường bao nhiêu (về phía camera) để không nhấp nháy với mặt đường.")]
        [SerializeField] private float lift = 0.02f;
        [Tooltip("Giây hiệu ứng vỡ còn lại trước khi xoá.")]
        [SerializeField] private float breakLifetime = 2f;
        [Tooltip("Viền đen của số độ nặng, như số trên tường đá (BoardView.wallTextOutline); đặt lúc chạy vì viền chữ là material riêng.")]
        [SerializeField] private float textOutline = 0.3f;

        [Header("Hiệu ứng")]
        [Tooltip("Vết nứt lan bao nhiêu ô mỗi giây khi rắn nặng đi trên băng (trước lúc vỡ).")]
        [SerializeField] private float crackCreep = 2.5f;
        [Tooltip("Lúc vỡ: vết nứt lan khắp tấm bao nhiêu ô mỗi giây.")]
        [SerializeField] private float crackBurst = 40f;
        [Tooltip("Lúc vỡ: vết nứt phủ hết tấm chậm nhất bao nhiêu giây (đoạn dài thì lan nhanh hơn).")]
        [SerializeField] private float maxCrackTime = 0.18f;
        [Tooltip("Giây tấm băng tan hết sau khi vỡ.")]
        [SerializeField] private float vanishTime = 0.3f;
        [Tooltip("Giây chờ sau khi nứt khắp tấm rồi mới tan.")]
        [SerializeField] private float vanishDelay = 0.08f;
        [Tooltip("Mảnh vỡ của ô xa văng trễ bao nhiêu giây mỗi ô (làn sóng từ chỗ rắn đứng).")]
        [SerializeField] private float breakRipple = 0.025f;
        [Tooltip("Giây tấm băng đóng lại (hiện dần) khi hoàn tác.")]
        [SerializeField] private float refreezeTime = 0.35f;
        [Tooltip("Độ sáng / bán kính (ô) lóe sáng chỗ rắn nhẹ chạm băng.")]
        [SerializeField] private Vector2 touchGlint = new Vector2(1.1f, 0.7f);
        [Tooltip("Độ sáng / bán kính (ô) vệt sáng chạy theo đầu rắn khi trượt.")]
        [SerializeField] private Vector2 slideGlint = new Vector2(0.55f, 0.9f);
        [Tooltip("Lóe sáng tắt dần bao nhiêu mỗi giây.")]
        [SerializeField] private float glintFade = 3f;

        // Dấu điểm dừng (prefab Board/Road/Stop_Visual; vật liệu đổi theo chủ đề nên nhận theo tên).
        private const string StopMark = "Stop_Visual";
        private static readonly int HalfWidth = Shader.PropertyToID("_HalfWidth");
        private static readonly int EndInset = Shader.PropertyToID("_EndInset");
        private static readonly int Points = Shader.PropertyToID("_Points");
        private static readonly int PointCount = Shader.PropertyToID("_PointCount");
        private static readonly int CrackId = Shader.PropertyToID("_Crack");
        private static readonly int GlintId = Shader.PropertyToID("_Glint");
        private static readonly int VanishId = Shader.PropertyToID("_Vanish");
        private const int MaxPathPoints = 24; // = MAX_POINTS trong SH_Ice

        /// <summary>Trạng thái hình của một đoạn băng.</summary>
        private class Strip
        {
            public List<Vector2Int> cells;
            public List<GameObject> slabs = new List<GameObject>();
            public List<Renderer> stopMarks = new List<Renderer>();
            public MeshRenderer sheet;
            public MaterialPropertyBlock block;
            public float reachAll;                 // bán kính để vết nứt phủ hết tấm từ chỗ bắt đầu
            public Vector2 crackOrigin;
            public float crack, crackTarget, crackSpeed, crackStrength;
            public Vector2 glintAt;
            public float glint, glintRadius;
            public bool following;                 // rắn nhẹ đang trượt trên tấm: vệt sáng theo đầu
            public readonly HashSet<Vector2Int> sparkled = new HashSet<Vector2Int>();
            public float vanish, vanishTarget, vanishSpeed, vanishWait;
            public bool broken;
        }

        private readonly List<Strip> _strips = new List<Strip>();
        private GameSession _session;

        public void Bind(GameSession session)
        {
            if (slabPrefab == null) return;
            _session = session;
            var owner = new Dictionary<Vector2Int, int>();
            for (int index = 0; index < session.IceSegments.Count; index++)
            {
                var segment = session.IceSegments[index];
                var strip = new Strip { cells = segment.cells };
                for (int i = 0; i < segment.cells.Count; i++)
                {
                    owner[segment.cells[i]] = index;
                    var slab = Instantiate(slabPrefab, transform);
                    slab.name = $"Ice {index} {segment.cells[i]}";
                    slab.transform.localPosition = BoardView.CellToWorld(segment.cells[i]);
                    var text = slab.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (text != null)
                    {
                        text.gameObject.SetActive(i == segment.cells.Count / 2);
                        text.text = segment.weight.ToString();
                        text.outlineWidth = textOutline;
                        text.outlineColor = Color.black;
                    }
                    strip.slabs.Add(slab);
                }
                _strips.Add(strip);
                BuildSheet(session.Board, index, strip);
            }
            HideSwallowedStops(owner);
            session.IceTouched += OnTouched;
            session.IceSlid += index => _strips[index].following = true;
            session.IceBroken += OnBroken;
            session.IceRestored += OnRestored;
            session.Stopped += _ => OnStopped();
        }

        /// <summary>
        /// Tấm băng của một đoạn: đường đi (đầu, các khúc cua, cuối) đưa cho shader qua MaterialPropertyBlock; đầu tấm cắt vuông dừng
        /// ngay trước mép điểm dừng. Lưới là mỗi ô một mảnh vuông nới rộng bằng bề ngang tấm (shader cắt hình).
        /// </summary>
        private void BuildSheet(BoardGraph board, int index, Strip strip)
        {
            var cells = strip.cells;
            if (iceMaterial == null || cells.Count == 0) return;
            float half = iceMaterial.GetFloat(HalfWidth);
            float endBack = iceMaterial.GetFloat(EndInset) - 0.5f; // đầu tấm (cắt vuông) dừng trước mép điểm dừng EndInset
            Vector2 toA = cells.Count > 1 ? (Vector2)(cells[0] - cells[1]) : Along(board, cells[0]);
            Vector2 toB = cells.Count > 1 ? (Vector2)(cells[cells.Count - 1] - cells[cells.Count - 2]) : -toA;
            var path = new List<Vector4> { (Vector2)cells[0] - toA * endBack };
            for (int i = 1; i + 1 < cells.Count; i++)
                if (cells[i] - cells[i - 1] != cells[i + 1] - cells[i]) path.Add((Vector2)cells[i]); // khúc cua
            path.Add((Vector2)cells[cells.Count - 1] - toB * endBack);
            if (path.Count > MaxPathPoints) Debug.LogWarning($"[IceView] Đoạn băng {index} có {path.Count} điểm, shader chỉ nhận {MaxPathPoints}.", this);

            float reach = half + 0.4f; // chừa chỗ cho mép uốn lượn, thành tấm và bóng đổ
            var vertices = new List<Vector3>(cells.Count * 4);
            var triangles = new List<int>(cells.Count * 6);
            foreach (var cell in cells)
            {
                int first = vertices.Count;
                vertices.Add(new Vector3(cell.x - reach, cell.y - reach, -lift));
                vertices.Add(new Vector3(cell.x + reach, cell.y - reach, -lift));
                vertices.Add(new Vector3(cell.x + reach, cell.y + reach, -lift));
                vertices.Add(new Vector3(cell.x - reach, cell.y + reach, -lift));
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
            var mesh = new Mesh { name = $"Ice {index} sheet" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var sheet = new GameObject(mesh.name);
            sheet.transform.SetParent(transform, false);
            sheet.AddComponent<MeshFilter>().sharedMesh = mesh;
            strip.sheet = sheet.AddComponent<MeshRenderer>();
            strip.sheet.sharedMaterial = iceMaterial;
            strip.block = new MaterialPropertyBlock();
            int count = Mathf.Min(path.Count, MaxPathPoints);
            while (path.Count < MaxPathPoints) path.Add(Vector4.zero); // mảng shader cố định cỡ
            strip.block.SetVectorArray(Points, path.GetRange(0, MaxPathPoints));
            strip.block.SetFloat(PointCount, count);
            Apply(strip);
        }

        /// <summary>Đoạn chỉ một ô: lấy hướng đường đi qua ô đó.</summary>
        private static Vector2 Along(BoardGraph board, Vector2Int cell)
        {
            var exits = board.Exits(cell);
            return exits.Count > 0 ? (Vector2)exits[0].ToCell() : Vector2.right;
        }

        /// <summary>Điểm dừng bị băng nuốt: ẩn dấu (hiện lại khi băng vỡ).</summary>
        private void HideSwallowedStops(Dictionary<Vector2Int, int> owner)
        {
            if (owner.Count == 0 || !TryGetComponent<BoardView>(out var boardView)) return;
            var back = boardView.CoverRoot.worldToLocalMatrix;
            foreach (var renderer in boardView.CoverRoot.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.parent == null || !renderer.transform.parent.name.StartsWith(StopMark)) continue;
                var at = back.MultiplyPoint3x4(renderer.transform.position);
                if (!owner.TryGetValue(new Vector2Int(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.y)), out int segment)) continue;
                renderer.enabled = false;
                _strips[segment].stopMarks.Add(renderer);
            }
        }

        private void OnTouched(int index, Vector2Int cell, bool heavy)
        {
            var strip = _strips[index];
            // Chỗ chạm: mép tấm phía đầu rắn đi tới (lùi nửa ô về phía rắn).
            var from = _session.CurrentCell;
            var edge = Vector2.Lerp(from, cell, 0.5f);
            if (heavy)
            {
                strip.crackOrigin = edge;
                strip.crack = 0f;
                strip.crackTarget = strip.reachAll = ReachFrom(strip, edge);
                strip.crackSpeed = crackCreep;
                strip.crackStrength = 1f;
            }
            else
            {
                strip.glintAt = edge;
                strip.glint = touchGlint.x;
                strip.glintRadius = touchGlint.y;
                strip.following = true;
                strip.sparkled.Clear();
            }
        }

        private void OnBroken(int index)
        {
            var strip = _strips[index];
            strip.broken = true;
            strip.following = false;
            foreach (var slab in strip.slabs) slab.SetActive(false); // dấu điểm dừng hiện khi tấm tan hết (Update)
            Vector2 at = _session.CurrentCell;
            if (strip.crackStrength <= 0f) // vỡ mà không qua lúc chạm (vd. đầu đứng sẵn trên băng): nứt từ chỗ rắn đứng
            {
                strip.crackOrigin = at;
                strip.crack = 0f;
            }
            strip.crackStrength = 1f;
            strip.crackTarget = strip.reachAll = ReachFrom(strip, strip.crackOrigin);
            strip.crackSpeed = crackBurst;
            strip.vanish = 0f;
            strip.vanishTarget = 1f;
            strip.vanishSpeed = 1f / Mathf.Max(0.01f, vanishTime);
            strip.vanishWait = Mathf.Min(maxCrackTime, Mathf.Max(0f, (strip.reachAll - strip.crack) / crackBurst)) + vanishDelay;
            strip.crackSpeed = Mathf.Max(crackBurst, (strip.reachAll - strip.crack) / Mathf.Max(0.01f, maxCrackTime));
            if (breakPrefab == null) return;
            foreach (var cell in strip.cells)
                StartCoroutine(Burst(cell, strip.vanishWait * 0.5f + Vector2.Distance(at, cell) * breakRipple));
        }

        private System.Collections.IEnumerator Burst(Vector2Int cell, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var burst = Instantiate(breakPrefab, transform);
            burst.transform.localPosition = BoardView.CellToWorld(cell) + Vector3.back * 0.4f; // nổi trên mặt đường, mảnh không chìm xuống
            foreach (var particles in burst.GetComponentsInChildren<ParticleSystem>(true)) particles.Play(true);
            Destroy(burst, breakLifetime);
        }

        private void OnRestored(int index)
        {
            var strip = _strips[index];
            strip.broken = false;
            foreach (var slab in strip.slabs) slab.SetActive(true);
            foreach (var mark in strip.stopMarks) mark.enabled = false;
            if (strip.sheet != null) strip.sheet.gameObject.SetActive(true);
            strip.crack = strip.crackTarget = strip.crackStrength = 0f;
            strip.vanish = 1f;
            strip.vanishTarget = 0f;
            strip.vanishSpeed = 1f / Mathf.Max(0.01f, refreezeTime);
            strip.vanishWait = 0f;
            foreach (var slab in strip.slabs) Sparkle(slab);
        }

        /// <summary>Rắn dừng: vệt sáng thôi theo; băng nứt mà chưa vỡ thì liền lại.</summary>
        private void OnStopped()
        {
            foreach (var strip in _strips)
            {
                strip.following = false;
                if (!strip.broken && strip.crackStrength > 0f)
                {
                    strip.crackTarget = 0f;
                    strip.crackSpeed = crackCreep * 2f;
                }
            }
        }

        private void Update()
        {
            if (_session == null) return;
            float dt = Time.deltaTime;
            Vector2 head = _session.HeadPosition;
            foreach (var strip in _strips)
            {
                if (strip.sheet == null) continue;
                bool changed = false;
                if (strip.crackStrength > 0f)
                {
                    // Rắn nặng đi trên băng: vết nứt bám theo đầu rắn (không vượt quá đầu quá xa trước lúc vỡ).
                    float goal = strip.broken ? strip.crackTarget : Mathf.Min(strip.crackTarget, Vector2.Distance(head, strip.crackOrigin) + 0.8f);
                    if (!strip.broken && strip.crackTarget <= 0f) goal = 0f;
                    strip.crack = Mathf.MoveTowards(strip.crack, goal, strip.crackSpeed * dt);
                    if (!strip.broken && strip.crackTarget <= 0f && strip.crack <= 0f) strip.crackStrength = 0f;
                    changed = true;
                }
                if (strip.following)
                {
                    var cell = Vector2Int.RoundToInt(head);
                    if (strip.cells.Contains(cell))
                    {
                        strip.glintAt = head;
                        strip.glint = Mathf.Max(strip.glint, slideGlint.x);
                        strip.glintRadius = slideGlint.y;
                        if (strip.sparkled.Add(cell)) Sparkle(strip.slabs[strip.cells.IndexOf(cell)]);
                    }
                }
                if (strip.glint > 0f)
                {
                    strip.glint = Mathf.Max(0f, strip.glint - glintFade * dt);
                    changed = true;
                }
                if (strip.vanish != strip.vanishTarget)
                {
                    if (strip.vanishWait > 0f) strip.vanishWait -= dt;
                    else strip.vanish = Mathf.MoveTowards(strip.vanish, strip.vanishTarget, strip.vanishSpeed * dt);
                    if (strip.broken && strip.vanish >= 1f)
                    {
                        strip.sheet.gameObject.SetActive(false);
                        foreach (var mark in strip.stopMarks) mark.enabled = true;
                    }
                    changed = true;
                }
                if (changed) Apply(strip);
            }
        }

        private void Apply(Strip strip)
        {
            strip.block.SetVector(CrackId, new Vector4(strip.crackOrigin.x, strip.crackOrigin.y, strip.crack, strip.crackStrength));
            strip.block.SetVector(GlintId, new Vector4(strip.glintAt.x, strip.glintAt.y, strip.glint, strip.glintRadius));
            strip.block.SetFloat(VanishId, strip.vanish);
            strip.sheet.SetPropertyBlock(strip.block);
        }

        /// <summary>Bán kính để vết nứt từ điểm này phủ hết tấm.</summary>
        private static float ReachFrom(Strip strip, Vector2 from)
        {
            float far = 0f;
            foreach (var cell in strip.cells) far = Mathf.Max(far, Vector2.Distance(from, cell));
            return far + 1f;
        }

        private static void Sparkle(GameObject slab)
        {
            var fx = slab.transform.Find("FX_Slide");
            if (fx != null && fx.TryGetComponent<ParticleSystem>(out var particles)) particles.Play(true);
        }
    }
}
