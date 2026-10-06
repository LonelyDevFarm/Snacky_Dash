using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Spawns the tile cover prefab of every cell. The board lies on the XY plane, one unit per cell,
    /// cell (x, y) centred at world (x, y, 0); covers are rotated around Z by the database rotation,
    /// matching the original Tilemap-based layout.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        // Layers whose tiles carry covers. "map" holds ground/path, "ldf" holds mechanics.
        private static readonly string[] CoverLayers = { "map", "ldf" };

        [SerializeField] private TileDefinitionDatabase database;
        [SerializeField] private Transform coverRoot;

        [Tooltip("Original: TilesCovers/VisualOverhaul/FullScreenBackground, loaded by GroundBGController.")]
        [SerializeField] private GameObject backgroundPrefab;

        // Original Game.unity "Ground_BG" transform, which carries GroundBGController; the background
        // prefab is spawned under it with its own local transform, giving a 160x160 plane at z = +1.3.
        private static readonly Quaternion GroundRotation = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Vector3 GroundScale = new Vector3(8f, 1f, 8f);
        private const float GroundDepth = 0.1f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _mapCovers = new Dictionary<Vector2Int, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _ldfCovers = new Dictionary<Vector2Int, GameObject>();

        [Header("Fixed arrow gates (ArrowView on TileCov_Arrow_Fixed)")]
        [SerializeField] private float gateRotateAngle = 60f;
        [SerializeField] private float gateAnimationDuration = 0.15f;
        [SerializeField] private float gateNotAllowedDuration = 0.2f;
        [SerializeField] private Vector3 gateNotAllowedShake = new Vector3(0f, 0.0013f, 0.0015f);

        private class Gate
        {
            public Vector2Int cell;
            public Direction way;
            public Transform left, right, shakeOrigin;
            public Quaternion leftRest, rightRest;
            public Vector3 shakeRest;
            public float open;  // 0 closed .. 1 open
            public float shake; // time left of the not-allowed shake
        }

        private readonly List<Gate> _gates = new List<Gate>();
        private GameSession _session;

        public Bounds BoardBounds { get; private set; }

        public static Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x, cell.y, 0f);

        public void Build(TiledMap map)
        {
            Clear();
            var root = coverRoot != null ? coverRoot : transform;
            var unknownIds = new HashSet<int>();

            foreach (var layer in CoverLayers)
            {
                if (!map.HasLayer(layer)) continue;
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int id = map.GetLocalTileId(layer, cell);
                    if (id < 0) continue;
                    if (!database.TryGet(id, out var definition))
                    {
                        unknownIds.Add(id);
                        continue;
                    }
                    if (definition.coverPrefab == null) continue;

                    var cover = Instantiate(definition.coverPrefab, root);
                    cover.name = $"{definition.name} ({x},{y})";
                    cover.transform.localPosition = CellToWorld(cell);
                    cover.transform.localRotation = Quaternion.Euler(0f, 0f, definition.coverZRotation);
                    _spawned.Add(cover);
                    if (layer == "map") _mapCovers[cell] = cover;
                    else _ldfCovers[cell] = cover;
                }
            }

            BoardBounds = ComputePlayableBounds(map);

            if (backgroundPrefab != null)
            {
                var ground = new GameObject("GroundBG").transform;
                ground.SetParent(root, false);
                ground.localPosition = new Vector3(BoardBounds.center.x, BoardBounds.center.y, GroundDepth);
                ground.localRotation = GroundRotation;
                ground.localScale = GroundScale;
                Instantiate(backgroundPrefab, ground, false);
                _spawned.Add(ground.gameObject);
            }

            // Local id 342 (raw GID 343) is plain grass with no entry in the original database either.
            unknownIds.Remove(342);
            if (unknownIds.Count > 0)
                Debug.LogWarning($"[BoardView] Tile ids without database entry: {string.Join(", ", unknownIds)}", this);
        }

        /// <summary>
        /// Bounds spanning the centres of the non-grass cells (paths, delivery points, mechanics).
        /// Calibrated against Level 2 footage (ortho size ~16.3): the original frames centre-to-centre
        /// path bounds (12 for 13 columns) plus the fitter's 3-cell expansion, not the full map.
        /// </summary>
        private Bounds ComputePlayableBounds(TiledMap map)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int id = map.GetLocalTileId("map", new Vector2Int(x, y));
                // Road corners are category Grass but carry a corner TileType, so only pure grass is skipped.
                if (id < 0 || !database.TryGet(id, out var definition)) continue;
                if (definition.category == TileCategory.Grass && definition.tileType == TileType.Empty) continue;
                min = Vector2.Min(min, new Vector2(x, y));
                max = Vector2.Max(max, new Vector2(x, y));
            }
            if (min.x > max.x)
                return new Bounds(new Vector3((map.Width - 1) * 0.5f, (map.Height - 1) * 0.5f, 0f), new Vector3(map.Width, map.Height, 0f));
            var center = (min + max) * 0.5f;
            var size = max - min;
            return new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
        }

        /// <summary>Cover spawned for a cell of the "map" layer, or null.</summary>
        public GameObject GetCover(Vector2Int cell) => _mapCovers.TryGetValue(cell, out var cover) ? cover : null;

        /// <summary>
        /// ArrowSwitchController / ArrowView: a fixed gate swings its two doors open while the head
        /// is about to enter it or the snake is inside it, and shuts again once the tail has left;
        /// it shakes when the snake runs past it while it is closed to it.
        /// </summary>
        public void BindGates(GameSession session)
        {
            _session = session;
            _gates.Clear();
            foreach (var pair in session.Board.OneWayGates)
            {
                if (!_ldfCovers.TryGetValue(pair.Key, out var cover)) continue;
                var shakeOrigin = cover.transform.Find("Gates/Gates");
                var left = shakeOrigin != null ? shakeOrigin.Find("LDF_Gates left") : null;
                var right = shakeOrigin != null ? shakeOrigin.Find("LDF_Gates Right") : null;
                if (left == null || right == null) continue;
                _gates.Add(new Gate
                {
                    cell = pair.Key, way = pair.Value, left = left, right = right, shakeOrigin = shakeOrigin,
                    leftRest = left.localRotation, rightRest = right.localRotation, shakeRest = shakeOrigin.localPosition,
                });
            }
            BindWalls(session);

            // Flipping arrows (TileCov_Arrow): the arrow turns half round when its pair flips.
            _flips.Clear();
            foreach (var arrow in session.Board.FlippingArrows.Keys)
            {
                if (!_ldfCovers.TryGetValue(arrow, out var cover)) continue;
                var spinner = cover.transform.Find("Switching Arrow/Arrow");
                var shakeOrigin = cover.transform.Find("Switching Arrow");
                if (spinner == null || shakeOrigin == null) continue;
                _flips[arrow] = new FlipArrow { arrow = spinner, rest = spinner.localRotation, shakeOrigin = shakeOrigin, shakeRest = shakeOrigin.localPosition,
                    initial = session.Board.OneWayGates[arrow] };
            }
            // Undo booster: arrows point and rocks stand as the session has them again.
            session.Rewound += () =>
            {
                foreach (var flip in _flips)
                {
                    int turned = session.Board.OneWayGates[flip.Key] != flip.Value.initial ? 1 : 0;
                    flip.Value.turns = turned;
                    flip.Value.shown = turned;
                }
                for (int group = 0; group < _wallCovers.Count; group++)
                {
                    if (session.WallHealth[group] <= 0) continue;
                    foreach (var cover in _wallCovers[group])
                    {
                        if (cover == null) continue;
                        cover.SetActive(true);
                        cover.transform.localScale = Vector3.one;
                        var text = cover.GetComponentInChildren<TMPro.TMP_Text>(true);
                        if (text != null) text.gameObject.SetActive(true);
                    }
                    SetWallText(group, session.WallHealth[group]);
                }
            };
            session.ArrowsFlipped += (a, b) =>
            {
                foreach (var cell in new[] { a, b })
                    if (_flips.TryGetValue(cell, out var flip)) flip.turns++;
            };

            // Shakes when the snake runs past a closed one, not when it is driven into it [user].
            session.ArrowNotAllowed += cell =>
            {
                foreach (var gate in _gates)
                    if (gate.cell == cell) gate.shake = gateNotAllowedDuration;
                if (_flips.TryGetValue(cell, out var flip)) flip.shake = gateNotAllowedDuration;
            };
        }

        [Header("Rocks (DestructibleWall ends, RoadBlock inside)")]
        [Tooltip("The rocks react when the crate's completion plays (CratesView: flight + hold), not when it is counted.")]
        [SerializeField] private float wallHitDelay = 0.8f;
        [SerializeField] private float wallTextOutline = 0.3f;
        [SerializeField] private Vector3 wallTextOffset = new Vector3(0f, -0.05f, 0f);

        private readonly List<List<GameObject>> _wallCovers = new List<List<GameObject>>();
        private readonly List<List<Vector2Int>> _wallCoverCells = new List<List<Vector2Int>>(); // cell of each cover

        [Tooltip("A rock row breaks from both ends inwards, one cell per step [user].")]
        [SerializeField] private float wallBreakStep = 0.067f; // VFX_da stepped at 15 fps: about one cell per frame

        /// <summary>
        /// Rock rows: inside cells (RoadBlock) show one of their three rock meshes, the ends
        /// (DestructibleWall) show the crates left; every completed crate hits the row
        /// (FX_DestructibleWall_Hit) and at 0 it bursts (FX_DestructibleWall) and goes [user].
        /// </summary>
        private void BindWalls(GameSession session)
        {
            _wallCovers.Clear();
            _wallCoverCells.Clear();
            for (int group = 0; group < session.Board.WallGroups.Count; group++)
            {
                var covers = new List<GameObject>();
                var coverCells = new List<Vector2Int>();
                foreach (var cell in session.Board.WallGroups[group])
                {
                    if (!_ldfCovers.TryGetValue(cell, out var cover)) continue;
                    covers.Add(cover);
                    coverCells.Add(cell);
                    int variant = Mathf.Abs(cell.x * 7 + cell.y * 13) % 3 + 1;
                    var rock = cover.transform.Find("MS_LDF_DestructibleRock_" + variant);
                    if (rock != null) rock.gameObject.SetActive(true);
                }
                _wallCovers.Add(covers);
                _wallCoverCells.Add(coverCells);
                foreach (var cover in covers)
                {
                    var text = cover.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (text == null) continue;
                    // White with a black outline, centred on the rock [user]: the rebuilt font sits
                    // higher than the original GladlyAccept, so it comes down a little.
                    text.color = Color.white;
                    text.outlineWidth = wallTextOutline;
                    text.outlineColor = Color.black;
                    text.transform.localPosition += wallTextOffset;
                }
                SetWallText(group, session.WallHealth[group]);
            }
            session.WallHealthChanged += (group, health) => StartCoroutine(HitWall(group, health));
        }

        private void SetWallText(int group, int health)
        {
            foreach (var cover in _wallCovers[group])
            {
                var text = cover != null ? cover.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                if (text != null) text.text = health.ToString();
            }
        }

        private IEnumerator HitWall(int group, int health)
        {
            yield return new WaitForSeconds(wallHitDelay);
            if (health > 0)
            {
                SetWallText(group, health);
                foreach (var cover in _wallCovers[group]) Replay(cover, "FX_DestructibleWall_Hit");
                yield break;
            }
            // VFX_da video: the numbered ends flash (FX_DestructibleWall), then the rocks go one cell a
            // frame from those ends along the row, each leaving a dust puff that fades.
            var covers = _wallCovers[group];
            var cells = _wallCoverCells[group];
            var steps = StepsFromEnds(cells);
            GameObject burst = null;
            foreach (var cover in covers)
            {
                var fx = cover != null ? cover.transform.Find("FX_DestructibleWall") : null;
                if (fx != null) { burst = fx.gameObject; break; }
            }
            for (int i = 0; i < covers.Count; i++)
            {
                bool end = covers[i] != null && covers[i].transform.Find("FX_DestructibleWall") != null;
                StartCoroutine(BreakRock(covers[i], burst, steps[i] * wallBreakStep, end));
            }
        }

        /// <summary>
        /// One rock: an end gets the whole burst (flash, smoke, rocks, sparkles); a rock inside only the
        /// dust (FX_Smoke, FX_Small_Rocks) and the rock chunks. The rock itself is gone at once.
        /// </summary>
        private IEnumerator BreakRock(GameObject cover, GameObject burstTemplate, float delay, bool end)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (cover == null) yield break;
            var text = cover.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text != null) text.gameObject.SetActive(false);
            if (burstTemplate != null)
            {
                // Placed like the template sits in its cap (local copy), then left behind when the rock goes.
                var burst = Instantiate(burstTemplate, cover.transform, false);
                burst.transform.SetParent(cover.transform.parent, true);
                if (!end)
                    foreach (Transform part in burst.transform)
                        part.gameObject.SetActive(part.name == "FX_Smoke" || part.name == "FX_Small_Rocks");
                burst.SetActive(true);
                // The prefab leaves FX_DestructibleWall off with playOnAwake off (a feedback fired it).
                if (burst.TryGetComponent<ParticleSystem>(out var particles)) particles.Play(true);
                Destroy(burst, 3f);
            }
            // VFX_da: the rock is simply gone as its burst goes off (shrinking read as sinking) [user].
            cover.SetActive(false);
        }

        /// <summary>Distance of each cell from the nearer end of its row (ends: cells with one neighbour in the row).</summary>
        private static List<int> StepsFromEnds(List<Vector2Int> cells)
        {
            var set = new HashSet<Vector2Int>(cells);
            var distance = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            foreach (var cell in cells)
            {
                int neighbours = 0;
                foreach (var d in DirectionExtensions.All) if (set.Contains(cell + d.ToCell())) neighbours++;
                if (neighbours <= 1) { distance[cell] = 0; queue.Enqueue(cell); }
            }
            if (queue.Count == 0 && cells.Count > 0) { distance[cells[0]] = 0; queue.Enqueue(cells[0]); }
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in DirectionExtensions.All)
                {
                    var next = cell + d.ToCell();
                    if (set.Contains(next) && !distance.ContainsKey(next)) { distance[next] = distance[cell] + 1; queue.Enqueue(next); }
                }
            }
            var steps = new List<int>();
            foreach (var cell in cells) steps.Add(distance.TryGetValue(cell, out int s) ? s : 0);
            return steps;
        }

        /// <summary>Restarts a particle child (kept off in the prefab); detached ones outlive the rock.</summary>
        private static void Replay(GameObject cover, string fx, bool detach = false)
        {
            var effect = cover != null ? cover.transform.Find(fx) : null;
            if (effect == null) return;
            effect.gameObject.SetActive(false);
            effect.gameObject.SetActive(true);
            if (!detach) return;
            effect.SetParent(cover.transform.parent, true);
            Destroy(effect.gameObject, 3f);
        }

        private class FlipArrow
        {
            public Transform arrow, shakeOrigin;
            public Quaternion rest;
            public Vector3 shakeRest;
            public Direction initial; // the way it pointed at the start
            public int turns;     // half turns asked for
            public float shown;   // half turns shown so far (eases towards turns)
            public float shake;
        }

        private readonly Dictionary<Vector2Int, FlipArrow> _flips = new Dictionary<Vector2Int, FlipArrow>();

        [Tooltip("TileCov_Arrow ArrowView: half turn in _animationDuration, shake _notAllowedShakeAmount.")]
        [SerializeField] private float flipDuration = 0.15f;
        [SerializeField] private Vector3 flipNotAllowedShake = new Vector3(0.1f, 0.1f, 0f);

        private void UpdateFlips()
        {
            float step = flipDuration > 0f ? Time.deltaTime / flipDuration : 1f;
            foreach (var flip in _flips.Values)
            {
                flip.shown = Mathf.MoveTowards(flip.shown, flip.turns, step);
                float whole = Mathf.Floor(flip.shown), part = flip.shown - whole;
                flip.arrow.localRotation = flip.rest * Quaternion.Euler(0f, 0f, 180f * (whole + Tween.OutSine(part)));

                var offset = Vector3.zero;
                if (flip.shake > 0f)
                {
                    flip.shake = Mathf.Max(0f, flip.shake - Time.deltaTime);
                    float t = 1f - flip.shake / gateNotAllowedDuration;
                    offset = flipNotAllowedShake * (Mathf.Sin(t * Mathf.PI * 8f) * (1f - t));
                }
                flip.shakeOrigin.localPosition = flip.shakeRest + offset;
            }
        }

        private void Update()
        {
            if (_session == null) return;
            UpdateFlips();
            float step = gateAnimationDuration > 0f ? Time.deltaTime / gateAnimationDuration : 1f;
            foreach (var gate in _gates)
            {
                bool open = SnakeAtGate(gate.cell);
                gate.open = Mathf.MoveTowards(gate.open, open ? 1f : 0f, step);
                float angle = gateRotateAngle * Tween.OutSine(gate.open);
                // Doors hinge on their bolts (local Y) and swing the way the arrow points; the right
                // door is the mirrored copy (scale x -1), so it turns the other way.
                gate.left.localRotation = gate.leftRest * Quaternion.Euler(0f, angle, 0f);
                gate.right.localRotation = gate.rightRest * Quaternion.Euler(0f, -angle, 0f);

                var offset = Vector3.zero;
                if (gate.shake > 0f)
                {
                    gate.shake = Mathf.Max(0f, gate.shake - Time.deltaTime);
                    float t = 1f - gate.shake / gateNotAllowedDuration;
                    offset = gateNotAllowedShake * (Mathf.Sin(t * Mathf.PI * 8f) * (1f - t));
                }
                gate.shakeOrigin.localPosition = gate.shakeRest + offset;
            }
        }

        /// <summary>Head heading into the gate's cell next (the "tip" cell), or any part of the snake in it.</summary>
        private bool SnakeAtGate(Vector2Int cell)
        {
            if (_session.IsMoving && _session.TargetCell == cell) return true;
            if ((_session.HeadPosition - (Vector2)cell).sqrMagnitude < 0.81f) return true;
            for (int i = 0; i < _session.Segments.Count; i++)
                if ((_session.SegmentPosition(i) - (Vector2)cell).sqrMagnitude < 0.81f) return true;
            return false;
        }

        public void Clear()
        {
            _mapCovers.Clear();
            _ldfCovers.Clear();
            _gates.Clear();
            _flips.Clear();
            _wallCovers.Clear();
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
        }
    }
}
