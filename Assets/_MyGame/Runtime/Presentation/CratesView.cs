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
        [Tooltip("Hole_VO (CrateVisualConfig_Box_VO.HoleColumnPrefab): the pit the stack stands in; its wooden floor shows once the column is empty.")]
        [SerializeField] private GameObject holePrefab;
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
        [Tooltip("Lv7 video (15 fps steps): a boomerang fruit is lifted off the road first, then glides into the crate in ~0.5 s, wrapped in the Vortex2 FX wind; ~6 are in the air at once.")]
        [SerializeField] private float boomerangFlightDuration = 0.5f;
        [SerializeField] private float boomerangArcHeight = 3f;
        [SerializeField] private GameObject vortexPrefab;
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
        // Completions whose crate is still on screen (the swap comes after the hold), per column, and
        // fruit still flying into a crate: a boomerang keeps streaming into the next crate meanwhile.
        private readonly Dictionary<int, int> _pendingSwaps = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _columnFreeAt = new Dictionary<int, float>(); // next completion may start
        private float _lastEffectLag;

        private float NormalCompletionWait => (_boomerang ? boomerangFlightDuration : flightDuration) + fullCrateHold;

        /// <summary>
        /// How much later than usual the last scheduled crate completion plays (crates of a column
        /// queue up after a boomerang); the victory screen waits that much longer.
        /// </summary>
        public float LastCompletionLag => _lastEffectLag;
        private readonly Dictionary<(int column, int crate), int> _inFlight = new Dictionary<(int, int), int>();
        private bool _boomerang; // the SegmentSnipped flights now come from a boomerang throw

        /// <summary>Centre of the current (top) crate of a column.</summary>
        public static Vector3 CratePosition(Vector2Int endPoint) =>
            BoardView.CellToWorld(endPoint) + new Vector3(0f, CrateHeightAboveEndPoint, TopCrateLift);

        public void Bind(GameSession session)
        {
            _session = session;
            // Every column, extra-crate slots included (bought with coins in the original, empty here).
            if (holePrefab != null)
                foreach (var anchor in session.Board.CrateAnchors)
                {
                    var hole = Instantiate(holePrefab, transform);
                    hole.name = $"Hole c{anchor.Key}";
                    hole.transform.localPosition = BoardView.CellToWorld(anchor.Value) + new Vector3(0f, CrateHeightAboveEndPoint, 0f);
                }
            for (int column = 0; column < session.Columns.Count; column++) Rebuild(column, false);
            session.SegmentDelivered += OnDelivered;
            session.SegmentSnipped += OnSnipped;
            session.TailSnipped += keep => { _snipOrder = 0; _boomerang = false; };
            session.BoomerangThrown += (column, thrown) => { _snipOrder = 0; _boomerang = true; };
            session.TailBitten += count => _boomerang = false; // a snip follows: plain flights
            session.DeliveryStarted += column => _boomerang = false;
            session.CrateCompleted += OnCrateCompleted;
            session.PillMagnetized += OnMagnetized;
            // Undo booster: the stacks show what the session holds again.
            session.Rewound += () =>
            {
                _pendingSwaps.Clear();
                _inFlight.Clear();
                for (int column = 0; column < session.Columns.Count; column++) Rebuild(column, false);
            };

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
            { var target = points.GetChild(slot); StartCoroutine(Fly(kind, () => target, snake != null ? snake.HeadWorldPosition : points.position, 0f)); }
        }

        /// <summary>
        /// Video Man_Lose: after a snip the cut fruit streams out of the tail into the crates of its
        /// kind, also crates further down the stack. Staggered like a delivery (inferred spacing).
        /// </summary>
        private void OnSnipped(int segmentIndex, PillKind kind, int columnIndex, int crateIndex)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            var start = snake != null ? snake.SegmentWorldPosition(segmentIndex) : transform.position;
            // Boomerang fruit come one per GameSession.BoomerangInterval tick already: no extra stagger.
            float delay = _boomerang ? 0f : _snipOrder++ * GameSession.DeliveryInterval;
            FlyToCrate(start, kind, columnIndex, crateIndex, delay, _boomerang);
        }

        /// <summary>Magnet booster: the fruit flies from its cell into the crate it was given (the boomerang's flight).</summary>
        private void OnMagnetized(Vector2Int cell, PillKind kind, int columnIndex, int crateIndex) =>
            FlyToCrate(BoardView.CellToWorld(cell), kind, columnIndex, crateIndex, 0f, true);

        private void FlyToCrate(Vector3 start, PillKind kind, int columnIndex, int crateIndex, float delay, bool boomerang)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            int slot = _session.Columns[columnIndex].FilledIn(crateIndex) - 1;
            if (SlotFor(columnIndex, crateIndex, slot) != null)
            {
                var key = (columnIndex, crateIndex);
                _inFlight[key] = InFlight(key) + 1;
                StartCoroutine(Fly(kind, () => SlotFor(columnIndex, crateIndex, slot), start, delay, boomerang: boomerang,
                    landed: () => _inFlight[key] = InFlight(key) - 1));
            }
            else
            {
                // Deeper than the visible stack: into the bottom crate, shown once it comes up.
                var bottom = crates[crates.Count - 1].transform;
                StartCoroutine(Fly(kind, () => bottom, start, delay, vanish: true, boomerang: boomerang));
            }
        }

        private int InFlight((int, int) key) => _inFlight.TryGetValue(key, out int n) ? n : 0;

        /// <summary>Destination point <paramref name="slot"/> of a crate as the stack shows it now, or null when it is not shown.</summary>
        private Transform SlotFor(int column, int crateIndex, int slot)
        {
            if (!_columns.TryGetValue(column, out var crates)) return null;
            int shownTop = _session.Columns[column].CurrentIndex - (_pendingSwaps.TryGetValue(column, out int p) ? p : 0);
            int depth = crateIndex - shownTop;
            if (depth < 0 || depth >= crates.Count || crates[depth] == null) return null;
            var points = crates[depth].transform.Find("AllDestinationPoints/DestinationPoints");
            return points != null && slot >= 0 && slot < points.childCount ? points.GetChild(slot) : null;
        }

        /// <summary>
        /// Flies a fruit to <paramref name="target"/>, asked again every frame: the stack may swap
        /// crates under a boomerang stream, which then lands in the crate where it now stands.
        /// </summary>
        private IEnumerator Fly(PillKind kind, System.Func<Transform> target, Vector3 start, float delay, bool vanish = false, bool boomerang = false, System.Action landed = null)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var slot = target();
            if (slot == null) { landed?.Invoke(); yield break; }
            var fruit = Instantiate(pillPrefab, transform);
            PillVisual.SetKind(fruit, kind);
            GameObject wind = boomerang && vortexPrefab != null ? Instantiate(vortexPrefab, fruit.transform) : null;
            yield return Tween.Run(boomerang ? boomerangFlightDuration : flightDuration, Tween.Linear, t =>
            {
                if (fruit == null) return;
                slot = target() ?? slot;
                if (slot == null) return;
                Vector3 position;
                if (boomerang)
                {
                    // Lifted first (height peaks a quarter of the way), then a slow glide in.
                    position = Vector3.Lerp(start, slot.position, Tween.InOutSine(t));
                    position.z -= Mathf.Sin(Mathf.PI * Mathf.Sqrt(t)) * boomerangArcHeight;
                }
                else
                {
                    position = Vector3.Lerp(start, slot.position, Tween.OutQuad(t));
                    position.z -= 4f * t * (1f - t) * flightArcHeight;
                }
                fruit.transform.position = position;
            });
            if (wind != null) Destroy(wind);
            slot = target();
            landed?.Invoke();
            if (slot == null || vanish) { Destroy(fruit); yield break; }
            fruit.transform.SetParent(slot, false);
            fruit.transform.localPosition = Vector3.zero;

            var settle = Tween.OutBack(easeBack);
            yield return Tween.Run(landingDuration, settle, t =>
            {
                if (fruit != null) fruit.transform.localScale = Vector3.one * Mathf.LerpUnclamped(landingScale, 1f, t);
            });
        }

        private void OnCrateCompleted(int columnIndex, int crateIndex)
        {
            _pendingSwaps[columnIndex] = (_pendingSwaps.TryGetValue(columnIndex, out int p) ? p : 0) + 1;
            StartCoroutine(Complete(columnIndex));
        }

        /// <summary>
        /// Footage: the last fruit lands, the full crate holds still, then the flash/smoke plays while
        /// the next crate takes its place underneath. The session's CrateCompletePause covers all of it.
        /// Crates of one column finish one after another: a boomerang can fill the next crate before
        /// the previous one has gone, and that crate still comes up and holds before it goes [user].
        /// </summary>
        private IEnumerator Complete(int columnIndex)
        {
            float ready = Time.time + NormalCompletionWait;
            float start = Mathf.Max(ready, _columnFreeAt.TryGetValue(columnIndex, out float free) ? free : 0f);
            _columnFreeAt[columnIndex] = start + appearDuration + fullCrateHold;
            _lastEffectLag = Mathf.Max(0f, start - ready);
            yield return new WaitForSeconds(start - Time.time);
            CompletionEffectStarted?.Invoke(columnIndex);
            if (_columns.TryGetValue(columnIndex, out var crates) && crates.Count > 0)
            {
                var finished = crates[0];
                crates.RemoveAt(0);
                StartCoroutine(Exit(finished));
            }
            _pendingSwaps[columnIndex]--;
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
                ShowFilled(crate, definition.kind, column.FilledIn(crateIndex) - InFlight((columnIndex, crateIndex)));

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
