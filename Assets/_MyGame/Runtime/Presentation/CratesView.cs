using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Crate columns above their delivery end points. Placement is measured from Level 2 footage
    /// (the original computes it at runtime): the column is a stack of crates standing on the ground,
    /// rising towards the camera (-Z) 1.1 units per crate, with the current crate on top. Through the
    /// tilted camera each crate below shows as a ~12 px coloured band; the original shows two of them.
    ///
    /// Feel: delivered fruit leaves the snake's head, arcs into its slot and settles there with a
    /// bounce (measured on footage, flight slowed slightly after play-testing);
    /// a full crate exits (BoxFeedbackView: InBack 3, 0.3 s) with its confetti while the next one
    /// rises into place (OutBack 3, 0.35 s).
    /// </summary>
    public class CratesView : MonoBehaviour
    {
        public const float CrateHeightAboveEndPoint = 1.6f;
        private const float StackDepth = 1.1f;
        private const int VisibleCratesPerColumn = 3;
        private const float TopCrateLift = -StackDepth * (VisibleCratesPerColumn - 1);

        /// <summary>Footprint of a crate on screen (measured: 2.8 cells wide), used for camera framing.</summary>
        public static readonly Vector3 CrateSize = new Vector3(2.8f, 2f, 0f);

        [SerializeField] private GameObject crate9Prefab;
        [SerializeField] private GameObject pillPrefab;
        [SerializeField] private FruitGraphicSet fruits;
        [Tooltip("Board whose crate-stop covers (TileCov_Crate_Delivery) get their idle/highlight state.")]
        [SerializeField] private BoardView board;
        [Tooltip("Delivered fruit starts its flight from the snake head.")]
        [SerializeField] private SnakeView snake;

        [Header("Feel")]
        [Tooltip("Footage (30 fps) shows ~4 frames; 0.475 s chosen by play-testing against the original on a phone.")]
        [SerializeField] private float flightDuration = 0.475f;
        [SerializeField] private float flightArcHeight = 1.5f;
        [Tooltip("Footage: the fruit lands large and settles into its slot over ~6 frames.")]
        [SerializeField] private float landingScale = 1.6f;
        [SerializeField] private float landingDuration = 0.2f;
        [Tooltip("Footage: a full crate sits still ~0.33 s (after its last fruit lands) before the completion flash.")]
        [SerializeField] private float fullCrateHold = 0.33f;
        [SerializeField] private float appearDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.3f;
        [SerializeField] private float easeBack = 3f;

        /// <summary>Raised when a full crate starts its completion flash (after the hold).</summary>
        public event System.Action<int> CompletionEffectStarted;

        private readonly Dictionary<int, List<GameObject>> _columns = new Dictionary<int, List<GameObject>>();
        private readonly Dictionary<int, (GameObject idle, GameObject highlight)> _stopStates = new Dictionary<int, (GameObject, GameObject)>();
        private GameSession _session;
        private int _snipOrder;

        /// <summary>Centre of the current (top) crate of a column.</summary>
        public static Vector3 CratePosition(Vector2Int endPoint) =>
            BoardView.CellToWorld(endPoint) + new Vector3(0f, CrateHeightAboveEndPoint, TopCrateLift);

        public void Bind(GameSession session)
        {
            _session = session;
            for (int column = 0; column < session.Columns.Count; column++) Rebuild(column, false);
            session.SegmentDelivered += OnDelivered;
            session.SegmentSnipped += OnSnipped;
            session.TailSnipped += keep => _snipOrder = 0;
            session.CrateCompleted += OnCrateCompleted;

            // Original CrateDeliverPoint: idleRoot / highlightRoot, highlighted whenever the snake
            // carries the current crate's kind (CrateManager.highlightOnlyIfEnoughToFill = 0).
            foreach (var stop in session.Board.CrateStops)
            {
                var cover = board != null ? board.GetCover(stop.Value) : null;
                if (cover == null) continue;
                _stopStates[stop.Key] = (cover.transform.Find("Torus")?.gameObject, cover.transform.Find("Torus_Highlight")?.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (_session == null) return;
            foreach (var state in _stopStates)
            {
                bool highlighted = _session.CanDeliverTo(state.Key);
                if (state.Value.idle != null) state.Value.idle.SetActive(!highlighted);
                if (state.Value.highlight != null) state.Value.highlight.SetActive(highlighted);
            }
        }

        private void OnDelivered(int segmentIndex, PillKind kind, int columnIndex)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            var points = crates[0].transform.Find("AllDestinationPoints/DestinationPoints");
            int slot = _session.Columns[columnIndex].Filled - 1;
            if (points == null || slot < 0 || slot >= points.childCount) return;
            StartCoroutine(Fly(kind, points.GetChild(slot), snake != null ? snake.HeadWorldPosition : points.position, 0f));
        }

        /// <summary>
        /// Video Man_Lose: after a snip the cut fruit streams out of the tail into the crates of its
        /// kind, also crates further down the stack. Staggered like a delivery (inferred spacing).
        /// </summary>
        private void OnSnipped(int segmentIndex, PillKind kind, int columnIndex, int crateIndex)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            var start = snake != null ? snake.SegmentWorldPosition(segmentIndex) : transform.position;
            float delay = _snipOrder++ * GameSession.DeliveryInterval;
            int depth = crateIndex - _session.Columns[columnIndex].CurrentIndex;
            int slot = _session.Columns[columnIndex].FilledIn(crateIndex) - 1;
            var points = depth < crates.Count ? crates[depth].transform.Find("AllDestinationPoints/DestinationPoints") : null;
            if (points != null && slot >= 0 && slot < points.childCount)
                StartCoroutine(Fly(kind, points.GetChild(slot), start, delay));
            else // deeper than the visible stack: into the bottom crate, shown once it comes up
                StartCoroutine(Fly(kind, crates[crates.Count - 1].transform, start, delay, vanish: true));
        }

        private IEnumerator Fly(PillKind kind, Transform slot, Vector3 start, float delay, bool vanish = false)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (slot == null) yield break;
            var fruit = Instantiate(pillPrefab, transform);
            PillVisual.SetKind(fruit, kind);
            yield return Tween.Run(flightDuration, Tween.Linear, t =>
            {
                if (fruit == null || slot == null) return;
                var position = Vector3.Lerp(start, slot.position, Tween.OutQuad(t));
                position.z -= 4f * t * (1f - t) * flightArcHeight;
                fruit.transform.position = position;
            });
            if (slot == null || vanish) { Destroy(fruit); yield break; }
            fruit.transform.SetParent(slot, false);
            fruit.transform.localPosition = Vector3.zero;

            var settle = Tween.OutBack(easeBack);
            yield return Tween.Run(landingDuration, settle, t =>
            {
                if (fruit != null) fruit.transform.localScale = Vector3.one * Mathf.LerpUnclamped(landingScale, 1f, t);
            });
        }

        private void OnCrateCompleted(int columnIndex, int crateIndex) => StartCoroutine(Complete(columnIndex));

        /// <summary>
        /// Footage: the last fruit lands, the full crate holds still, then the flash/smoke plays while
        /// the next crate takes its place underneath. The session's CrateCompletePause covers all of it.
        /// </summary>
        private IEnumerator Complete(int columnIndex)
        {
            yield return new WaitForSeconds(flightDuration + fullCrateHold);
            CompletionEffectStarted?.Invoke(columnIndex);
            if (_columns.TryGetValue(columnIndex, out var crates) && crates.Count > 0)
            {
                var finished = crates[0];
                crates.RemoveAt(0);
                StartCoroutine(Exit(finished));
            }
            Rebuild(columnIndex, true);
        }

        private IEnumerator Exit(GameObject crate)
        {
            var confetti = crate.transform.Find("Confetti_Crates");
            if (confetti != null)
            {
                confetti.SetParent(transform, true);
                confetti.gameObject.SetActive(true);
                Destroy(confetti.gameObject, 3f);
            }
            var ease = Tween.InBack(easeBack);
            yield return Tween.Run(exitDuration, ease, t => { if (crate != null) crate.transform.localScale = Vector3.one * (1f - t); });
            Destroy(crate);
        }

        private void Rebuild(int columnIndex, bool animate)
        {
            if (_columns.TryGetValue(columnIndex, out var old))
                foreach (var go in old) Destroy(go);
            var crates = new List<GameObject>();
            _columns[columnIndex] = crates;

            if (!_session.Board.CrateAnchors.TryGetValue(columnIndex, out var anchor)) return;
            var column = _session.Columns[columnIndex];
            var basePosition = CratePosition(anchor);

            for (int k = 0; k < VisibleCratesPerColumn; k++)
            {
                int crateIndex = column.CurrentIndex + k;
                if (crateIndex >= column.Crates.Count) break;
                var definition = column.Crates[crateIndex];
                var crate = Instantiate(crate9Prefab, transform); // every crate holds 9 (see GameSession.CrateCapacity)
                crate.name = $"Crate c{columnIndex} #{crateIndex} {definition.kind} x{definition.requiredCount}";
                Paint(crate, definition.kind);
                crates.Add(crate);
                ShowFilled(crate, definition.kind, column.FilledIn(crateIndex));

                var target = basePosition + new Vector3(0f, 0f, StackDepth * k);
                crate.transform.localPosition = target;
                if (!animate) continue;

                // The stack rises one level; the crate entering at the bottom pops in.
                var from = target + new Vector3(0f, 0f, StackDepth);
                bool entering = k == VisibleCratesPerColumn - 1;
                var ease = Tween.OutBack(easeBack);
                StartCoroutine(Tween.Run(appearDuration, ease, t =>
                {
                    if (crate == null) return;
                    crate.transform.localPosition = Vector3.LerpUnclamped(from, target, t);
                    if (entering) crate.transform.localScale = Vector3.one * t;
                }));
            }
        }

        /// <summary>Fruit already in a crate (put there by a snip before it reached the top).</summary>
        private void ShowFilled(GameObject crate, PillKind kind, int count)
        {
            var points = crate.transform.Find("AllDestinationPoints/DestinationPoints");
            if (points == null) return;
            for (int slot = 0; slot < count && slot < points.childCount; slot++)
            {
                var fruit = Instantiate(pillPrefab, points.GetChild(slot), false);
                PillVisual.SetKind(fruit, kind);
                fruit.transform.localPosition = Vector3.zero;
            }
        }

        private void Paint(GameObject crate, PillKind kind)
        {
            var entry = fruits != null ? fruits.Get(kind) : null;
            var mesh = crate.transform.Find("BoxVisual_VO/BoxFloor_VO/Mesh_Crate");
            if (mesh != null && entry?.crateMaterial != null)
            {
                var renderer = mesh.GetComponent<Renderer>();
                var materials = renderer.sharedMaterials;
                if (materials.Length > 1) materials[1] = entry.crateMaterial; // original _materialIndexToColor = 1
                renderer.sharedMaterials = materials;
            }
            // The empty crate in the footage shows no icon; the original toggles it from code.
            var icon = crate.transform.Find("BoxVisual_VO/BoxFloor_VO/Mesh_Crate/Fruit_Icon");
            if (icon != null) icon.gameObject.SetActive(false);
        }
    }
}
