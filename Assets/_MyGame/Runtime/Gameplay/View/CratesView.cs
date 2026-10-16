using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Chồng crate (khay) trên mỗi điểm giao: trái bay từ rắn vào khay, khay đầy bay đi, khay sau dâng lên.
    /// Nằm trong scene Game, GameBootstrap gọi Bind; vị trí đo từ video Lv2.
    /// </summary>
    public class CratesView : MonoBehaviour
    {
        public const float CrateHeightAboveEndPoint = 1.6f;
        private const float StackDepth = 1.1f;
        private const int VisibleCratesPerColumn = 3;
        private const float TopCrateLift = -StackDepth * (VisibleCratesPerColumn - 1);

        /// <summary>Khung một crate trên màn (rộng 2.8 ô, đo video), dùng để canh camera.</summary>
        public static readonly Vector3 CrateSize = new Vector3(2.8f, 2f, 0f);

        [SerializeField] private GameObject crate9Prefab;
        [Tooltip("Hole_VO: hố chồng khay đứng trong; lộ sàn gỗ khi cột hết khay.")]
        [SerializeField] private GameObject holePrefab;
        [SerializeField] private GameObject pillPrefab;
        [SerializeField] private FruitGraphicSet fruits;
        [Tooltip("Bàn có ô điểm giao (TileCov_Crate_Delivery) cần bật sáng.")]
        [SerializeField] private BoardView board;
        [Tooltip("Trái giao bay ra từ đầu rắn.")]
        [SerializeField] private SnakeView snake;

        [Header("Feel")]
        [Tooltip("Giây bay vào khay (chốt khi chơi thử so với bản gốc).")]
        [SerializeField] private float flightDuration = 0.475f;
        [SerializeField] private float flightArcHeight = 1.5f;
        [Tooltip("Giây bay của trái boomerang: nhấc lên rồi lượn vào khay (video Lv7).")]
        [SerializeField] private float boomerangFlightDuration = 0.5f;
        [SerializeField] private float boomerangArcHeight = 3f;
        [SerializeField] private GameObject vortexPrefab;
        [Header("Extra crate (ExtraCrateVfxConfig)")]
        [Tooltip("Particle_Extra_Crate: bung trên khay khi mở cột thêm.")]
        [SerializeField] private GameObject extraUnlockVfx;
        [Tooltip("ExtraCrate_Trail_FX: bay từ giữa màn tới khay trước khi mở.")]
        [SerializeField] private GameObject extraTrailPrefab;
        [SerializeField] private float extraTrailDuration = 1f;
        [Tooltip("Trái chạm khay nảy cỡ thêm bấy nhiêu rồi về (gốc FruitsPunchScaleOnLand 0.4, 0.4, -0.5; hạ xuống vì trái mình lấp kín ô, 0.4 thì tràn ra ngoài khay [user]); bay thì không to lên.")]
        [SerializeField] private Vector3 landingPunch = new Vector3(0.15f, 0.15f, -0.2f);
        [Tooltip("Giây nảy khi chạm khay (gốc FruitsPunchDurationOnLand).")]
        [SerializeField] private float landingDuration = 0.2f;
        [Header("Khay rung khi nhận trái (gốc Box_Crate_9_VO: MMF_PositionShake trên BoxVisual_VO)")]
        [Tooltip("Giây rung (gốc Duration 0.1).")]
        [SerializeField] private float receiveShakeDuration = 0.1f;
        [Tooltip("Xa nhất bấy nhiêu (gốc ShakeRange 0.06). Tăng: rung mạnh hơn.")]
        [SerializeField] private float receiveShakeRange = 0.06f;
        [Tooltip("Nhanh bấy nhiêu (gốc ShakeSpeed 10).")]
        [SerializeField] private float receiveShakeSpeed = 10f;
        [Tooltip("Hướng rung (gốc ShakeMainDirection 1, 1, 0), mỗi lần lệch ngẫu nhiên thêm tới 0.25 (gốc DirectionalNoise).")]
        [SerializeField] private Vector3 receiveShakeDirection = new Vector3(1f, 1f, 0f);
        [Tooltip("Giây khay đầy đứng yên trước khi bay đi (video).")]
        [SerializeField] private float fullCrateHold = 0.33f;
        [SerializeField] private float appearDuration = 0.35f;
        [SerializeField] private float exitDuration = 0.3f;
        [SerializeField] private float easeBack = 3f;

        /// <summary>Báo khi khay đầy bắt đầu hiệu ứng hoàn thành (sau lúc đứng yên).</summary>
        public event System.Action<int> CompletionEffectStarted;

        private readonly Dictionary<int, List<GameObject>> _columns = new Dictionary<int, List<GameObject>>();
        private readonly Dictionary<int, GameObject> _holes = new Dictionary<int, GameObject>();
        [Tooltip("Khay dưới rèm hiện khi rèm mở xong.")]
        [SerializeField] private CurtainsView curtains;
        private readonly Dictionary<int, (GameObject idle, GameObject highlight)> _stopStates = new Dictionary<int, (GameObject, GameObject)>();
        private GameSession _session;
        private int _snipOrder;
        // Khay đã đầy nhưng còn trên màn, và trái đang bay: boomerang vẫn đổ vào khay sau trong lúc đó.
        private readonly Dictionary<int, int> _pendingSwaps = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _columnFreeAt = new Dictionary<int, float>(); // lúc khay kế được phép hoàn thành
        private float _lastEffectLag;

        private float NormalCompletionWait => (_boomerang ? boomerangFlightDuration : flightDuration) + fullCrateHold;

        /// <summary>Khay đầy cuối bị trễ bao lâu (khay một cột xếp hàng sau boomerang); màn thắng chờ thêm chừng đó.</summary>
        public float LastCompletionLag => _lastEffectLag;
        private readonly Dictionary<(int column, int crate), int> _inFlight = new Dictionary<(int, int), int>();
        private bool _boomerang; // trái SegmentSnipped hiện tại là do boomerang ném
        private readonly Dictionary<Transform, Coroutine> _shakes = new Dictionary<Transform, Coroutine>();

        /// <summary>Tâm khay trên cùng của cột.</summary>
        public static Vector3 CratePosition(Vector2Int endPoint) =>
            BoardView.CellToWorld(endPoint) + new Vector3(0f, CrateHeightAboveEndPoint, TopCrateLift);

        public void Bind(GameSession session)
        {
            _session = session;
            // Mọi cột, kể cả chỗ khay thêm (gốc mua bằng xu, ở đây để trống).
            if (holePrefab != null)
                foreach (var anchor in session.Board.CrateAnchors)
                {
                    var hole = Instantiate(holePrefab, transform);
                    hole.name = $"Hole c{anchor.Key}";
                    hole.transform.localPosition = BoardView.CellToWorld(anchor.Value) + new Vector3(0f, CrateHeightAboveEndPoint, 0f);
                    _holes[anchor.Key] = hole;
                }
            for (int column = 0; column < session.Columns.Count; column++) Rebuild(column, false);
            session.SegmentDelivered += OnDelivered;
            session.ExtraColumnUnlocked += OnExtraUnlocked;
            session.SegmentSnipped += OnSnipped;
            session.TailSnipped += keep => { _snipOrder = 0; _boomerang = false; };
            session.BoomerangThrown += (column, thrown) => { _snipOrder = 0; _boomerang = true; };
            session.TailBitten += count => _boomerang = false; // sắp cắt đuôi: bay thường
            session.DeliveryStarted += column => _boomerang = false;
            session.CrateCompleted += OnCrateCompleted;
            session.PillMagnetized += OnMagnetized;
            session.CurtainOpened += index => StartCoroutine(ShowUncovered(curtains != null ? curtains.RevealTime : 0.6f));
            // Undo: dựng lại chồng khay theo session.
            session.Rewound += () =>
            {
                _pendingSwaps.Clear();
                _inFlight.Clear();
                for (int column = 0; column < session.Columns.Count; column++) Rebuild(column, false);
            };

            // Điểm giao sáng khi rắn mang loại trái của khay hiện tại, đủ hay chưa đủ (gốc highlightOnlyIfEnoughToFill = 0).
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

        /// <summary>
        /// Trái từ miệng rắn vào khay đang nhận. Nhắm theo chỗ trong chồng (hỏi lại mỗi frame) và tính là đang bay: khay đầy
        /// chưa kịp đi mà trái kế đã ra thì vẫn bay vào khay mới, khay mới dựng lại không hiện sẵn trái đó [user, video Bug].
        /// </summary>
        private void OnDelivered(int segmentIndex, PillKind kind, int columnIndex)
        {
            var start = snake != null ? snake.HeadWorldPosition : ColumnWorldPosition(columnIndex);
            FlyToCrate(start, kind, columnIndex, _session.Columns[columnIndex].CurrentIndex, 0f, false);
        }

        /// <summary>Trái của đuôi bị cắt bay lần lượt vào khay cùng loại, kể cả khay dưới chồng (video Man_Lose).</summary>
        private void OnSnipped(int segmentIndex, PillKind kind, int columnIndex, int crateIndex)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            var start = snake != null ? snake.SegmentWorldPosition(segmentIndex) : transform.position;
            // Boomerang đã ra từng trái theo GameSession.BoomerangInterval: không giãn thêm.
            float delay = _boomerang ? 0f : _snipOrder++ * GameSession.DeliveryInterval;
            FlyToCrate(start, kind, columnIndex, crateIndex, delay, _boomerang);
        }

        /// <summary>Vị trí khay của cột (world), vd cho bong bóng TAP TO UNLOCK.</summary>
        public Vector3 ColumnWorldPosition(int column) =>
            _session.Board.CrateAnchors.TryGetValue(column, out var anchor) ? transform.TransformPoint(CratePosition(anchor)) : transform.position;

        /// <summary>Vệt FX bay từ dưới giữa màn tới khay thêm, to dần rồi nhỏ lại (gốc ExtraCrateVfxConfig).</summary>
        public IEnumerator PlayExtraTrail(int column)
        {
            var camera = Camera.main;
            var end = ColumnWorldPosition(column);
            if (extraTrailPrefab == null || camera == null) yield break;
            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.4f, 0f));
            var plane = new Plane(-camera.transform.forward, end);
            var start = plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : end;
            var trail = Instantiate(extraTrailPrefab, start, Quaternion.identity, transform);
            var scale = trail.transform.localScale;
            yield return Tween.Run(extraTrailDuration, Tween.Linear, t =>
            {
                if (trail == null) return;
                trail.transform.position = Vector3.Lerp(start, end, Tween.InOutSine(t));
                float size = t < 0.35f ? Mathf.Lerp(0.5f, 1f, t / 0.35f) : Mathf.Lerp(1f, 0.35f, (t - 0.35f) / 0.65f);
                trail.transform.localScale = scale * size;
            });
            if (trail != null) Destroy(trail, 1f); // chờ hạt tan hết
        }

        /// <summary>Mở cột thêm: FX bung, mọi cột dựng lại vì khay được chia lại.</summary>
        private void OnExtraUnlocked(int extraColumn)
        {
            if (extraUnlockVfx != null)
            {
                var vfx = Instantiate(extraUnlockVfx, ColumnWorldPosition(extraColumn) + new Vector3(0f, 0f, -0.5f), Quaternion.identity, transform);
                Destroy(vfx, 3f);
            }
            for (int column = 0; column < _session.Columns.Count; column++) Rebuild(column, column == extraColumn);
        }

        /// <summary>Booster nam châm: trái bay từ ô vào khay, kiểu bay boomerang.</summary>
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
                // Khay sâu hơn chồng đang hiện: bay vào khay dưới cùng rồi biến, hiện lại khi khay dâng lên.
                var bottom = crates[crates.Count - 1].transform;
                StartCoroutine(Fly(kind, () => bottom, start, delay, vanish: true, boomerang: boomerang));
            }
        }

        private int InFlight((int, int) key) => _inFlight.TryGetValue(key, out int n) ? n : 0;

        /// <summary>Chỗ <paramref name="slot"/> trong khay theo chồng đang hiện; null nếu khay chưa hiện.</summary>
        private Transform SlotFor(int column, int crateIndex, int slot)
        {
            if (!_columns.TryGetValue(column, out var crates)) return null;
            int shownTop = _session.Columns[column].CurrentIndex - (_pendingSwaps.TryGetValue(column, out int p) ? p : 0);
            int depth = crateIndex - shownTop;
            if (depth < 0 || depth >= crates.Count || crates[depth] == null) return null;
            var points = crates[depth].transform.Find("AllDestinationPoints/DestinationPoints");
            return points != null && slot >= 0 && slot < points.childCount ? points.GetChild(slot) : null;
        }

        /// <summary>Hỏi lại <paramref name="target"/> mỗi frame vì chồng khay có thể đổi khi trái đang bay.</summary>
        private IEnumerator Fly(PillKind kind, System.Func<Transform> target, Vector3 start, float delay, bool vanish = false, bool boomerang = false, System.Action landed = null)
        {
            // Trái hiện ngay tại chỗ (thân đuôi cắt biến mất, trái ở lại) rồi chờ lượt [video Man_Lose, user].
            var fruit = Instantiate(pillPrefab, transform);
            PillVisual.SetKind(fruit, kind);
            PillVisual.HideShadow(fruit);
            fruit.transform.position = start;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var slot = target();
            if (slot == null) { Destroy(fruit); landed?.Invoke(); yield break; }
            GameObject wind = boomerang && vortexPrefab != null ? Instantiate(vortexPrefab, fruit.transform) : null;
            yield return Tween.Run(boomerang ? boomerangFlightDuration : flightDuration, Tween.Linear, t =>
            {
                if (fruit == null) return;
                slot = target() ?? slot;
                if (slot == null) return;
                Vector3 position;
                if (boomerang)
                {
                    // Nhấc lên trước (cao nhất ở 1/4 đường), rồi lượn chậm vào.
                    position = Vector3.Lerp(start, slot.position, Tween.InOutSine(t));
                    position.z -= Mathf.Sin(Mathf.PI * Mathf.Sqrt(t)) * boomerangArcHeight;
                }
                else
                {
                    position = Vector3.Lerp(start, slot.position, Tween.OutQuad(t));
                    position.z -= 4f * t * (1f - t) * flightArcHeight;
                }
                // Cỡ trên sân dần về cỡ chỗ trong khay, không to lên [user].
                fruit.transform.localScale = Vector3.one * Mathf.Lerp(1f, slot.lossyScale.x / transform.lossyScale.x, t);
                fruit.transform.position = position;
            });
            if (wind != null) Destroy(wind);
            slot = target();
            landed?.Invoke();
            if (slot == null || vanish) { Destroy(fruit); yield break; }
            fruit.transform.SetParent(slot, false);
            fruit.transform.localPosition = Vector3.zero;
            fruit.transform.localScale = Vector3.one;
            Shake(slot.parent != null && slot.parent.parent != null ? slot.parent.parent.parent : null);

            // Chạm khay nảy nhẹ một cái rồi về (gốc DOPunchScale FruitsPunchScaleOnLand trong 0.2 s).
            yield return Tween.Punch(landingDuration * 0.5f, t =>
            {
                if (fruit != null) fruit.transform.localScale = Vector3.one + landingPunch * t;
            });
        }

        /// <summary>Thân khay (BoxVisual_VO) rung nhẹ một cái khi trái đáp; trái trong khay đứng yên như gốc. Đang rung thì rung lại từ đầu.</summary>
        private void Shake(Transform crate)
        {
            var visual = crate != null ? crate.Find("BoxVisual_VO") : null;
            if (visual == null || receiveShakeDuration <= 0f) return;
            if (_shakes.TryGetValue(visual, out var running) && running != null) StopCoroutine(running);
            _shakes[visual] = StartCoroutine(ShakeRoutine(visual));
        }

        private IEnumerator ShakeRoutine(Transform visual)
        {
            var noise = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f));
            var direction = receiveShakeDirection + noise;
            // Lên rồi tắt dần (gốc AttenuationCurve 0 → 1 → 0), về đúng chỗ cũ (ResetTargetValuesAfterShake).
            yield return Tween.Run(receiveShakeDuration, Tween.Linear, t =>
            {
                if (visual == null) return;
                float fade = 1f - Mathf.Abs(t * 2f - 1f);
                visual.localPosition = direction * (Mathf.Sin(t * receiveShakeDuration * receiveShakeSpeed * Mathf.PI * 2f) * receiveShakeRange * fade);
            });
            if (visual != null) visual.localPosition = Vector3.zero;
            _shakes.Remove(visual);
        }

        private void OnCrateCompleted(int columnIndex, int crateIndex)
        {
            _pendingSwaps[columnIndex] = (_pendingSwaps.TryGetValue(columnIndex, out int p) ? p : 0) + 1;
            StartCoroutine(Complete(columnIndex));
        }

        /// <summary>Khay đầy đứng yên rồi bay đi, khay sau dâng lên. Khay cùng cột xong lần lượt, kể cả khi boomerang
        /// lấp khay sau trước khi khay trước đi [user].</summary>
        private IEnumerator Complete(int columnIndex)
        {
            float ready = Time.time + NormalCompletionWait;
            float start = Mathf.Max(ready, _columnFreeAt.TryGetValue(columnIndex, out float free) ? free : 0f);
            _columnFreeAt[columnIndex] = start + exitDuration + appearDuration + fullCrateHold;
            _lastEffectLag = Mathf.Max(0f, start - ready);
            yield return new WaitForSeconds(start - Time.time);
            CompletionEffectStarted?.Invoke(columnIndex);
            GameObject finished = null;
            if (_columns.TryGetValue(columnIndex, out var crates) && crates.Count > 0)
            {
                finished = crates[0];
                crates.RemoveAt(0);
            }
            _pendingSwaps[columnIndex]--; // cùng lúc gỡ khay để trái đang bay nhắm khay kế
            // Khay đầy đi trước rồi chồng mới dâng; làm cùng lúc thì khay sau đè lên khay đầy [user].
            if (finished != null) yield return Exit(finished);
            Rebuild(columnIndex, true);
        }

        private IEnumerator Exit(GameObject crate)
        {
            CameraShake.CrateExit(); // gốc: Box_Crate_9_VO BoxFeedbackView
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
            // Tính từ khay đang hiện, không phải của session: khay đầy chờ đi vẫn nằm trên, bỏ qua thì mất khay [user].
            int top = column.CurrentIndex - (_pendingSwaps.TryGetValue(columnIndex, out int pending) ? pending : 0);
            // Chồng đứng trên đất: hết khay mới thì chỗ trống ở trên, khay còn lại đứng yên chứ không dâng lơ lửng [mentor].
            int remaining = column.Crates.Count - top;
            int missing = Mathf.Max(0, VisibleCratesPerColumn - remaining);
            int missingBefore = Mathf.Max(0, VisibleCratesPerColumn - (remaining + 1)); // tính cả khay vừa đi

            for (int k = 0; k < VisibleCratesPerColumn; k++)
            {
                int crateIndex = top + k;
                if (crateIndex >= column.Crates.Count) break;
                var definition = column.Crates[crateIndex];
                var crate = Instantiate(crate9Prefab, transform); // khay nào cũng 9 chỗ (GameSession.CrateCapacity)
                crate.name = $"Crate c{columnIndex} #{crateIndex} {definition.kind} x{definition.requiredCount}";
                Paint(crate, definition.kind);
                crates.Add(crate);
                ShowFilled(crate, definition.kind, column.FilledIn(crateIndex) - InFlight((columnIndex, crateIndex)));

                var target = basePosition + new Vector3(0f, 0f, StackDepth * (k + missing));
                crate.transform.localPosition = target;
                if (!animate) continue;

                // Chồng dâng một bậc, khay mới hiện ở dưới; hết khay mới thì giữ nguyên chỗ (from == target).
                var from = basePosition + new Vector3(0f, 0f, StackDepth * (k + 1 + missingBefore));
                bool entering = missing == 0 && k == VisibleCratesPerColumn - 1;
                if (from == target && !entering) continue;
                // Không vượt quá rồi lùi (OutBack): khay dâng vượt / phồng lên thì đè trái đang bay vào [user].
                StartCoroutine(Tween.Run(appearDuration, Tween.OutCubic, t =>
                {
                    if (crate == null) return;
                    crate.transform.localPosition = Vector3.LerpUnclamped(from, target, t);
                    if (entering) crate.transform.localScale = Vector3.one * t;
                }));
            }
            // Khay dưới rèm ẩn tới khi rèm mở (gốc CrateManager.IsColumnCurtainCovered).
            bool covered = _session.IsColumnCovered(columnIndex);
            foreach (var crate in crates) crate.SetActive(!covered);
            if (_holes.TryGetValue(columnIndex, out var hole)) hole.SetActive(!covered);
        }

        /// <summary>Rèm mở: khay bên dưới hiện ra sau khi rèm đi hết (CurtainsView.RevealTime).</summary>
        private IEnumerator ShowUncovered(float delay)
        {
            yield return new WaitForSeconds(delay);
            for (int column = 0; column < _session.Columns.Count; column++)
                if (_columns.TryGetValue(column, out var crates) && crates.Count > 0 && !crates[0].activeSelf && !_session.IsColumnCovered(column))
                {
                    Rebuild(column, false);
                    foreach (var crate in _columns[column])
                    {
                        var target = crate.transform.localScale;
                        crate.transform.localScale = Vector3.zero;
                        StartCoroutine(Tween.Run(appearDuration, Tween.OutBack(easeBack), t => { if (crate != null) crate.transform.localScale = target * t; }));
                    }
                }
        }

        /// <summary>Trái đã có sẵn trong khay (do cắt đuôi đổ vào khi khay chưa lên trên cùng).</summary>
        private void ShowFilled(GameObject crate, PillKind kind, int count)
        {
            var points = crate.transform.Find("AllDestinationPoints/DestinationPoints");
            if (points == null) return;
            for (int slot = 0; slot < count && slot < points.childCount; slot++)
            {
                var fruit = Instantiate(pillPrefab, points.GetChild(slot), false);
                PillVisual.SetKind(fruit, kind);
                PillVisual.HideShadow(fruit);
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
                if (materials.Length > 1) materials[1] = entry.crateMaterial; // gốc: _materialIndexToColor = 1
                renderer.sharedMaterials = materials;
            }
            // Viền khay theo loại trái (gốc: Crate._outline, GetCrateOutlineColor); prefab để sẵn màu xanh navy.
            var outline = crate.transform.Find("BoxVisual_VO/Outline");
            if (outline != null && entry != null && outline.TryGetComponent<SpriteRenderer>(out var sprite))
                sprite.color = entry.outlineColor;
            // Khay trống trong video không có icon; gốc tắt bằng code.
            var icon = crate.transform.Find("BoxVisual_VO/BoxFloor_VO/Mesh_Crate/Fruit_Icon");
            if (icon != null) icon.gameObject.SetActive(false);
        }
    }
}
