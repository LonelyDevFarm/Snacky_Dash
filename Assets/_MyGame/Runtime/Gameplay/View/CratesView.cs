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

        /// <summary>Khay đôi (sáng tạo): khe giữa hai chồng đứng hai bên điểm giao, ô; rộng cho xích các tầng không dồn đè nhau [user 25/10].</summary>
        public const float PairGap = 2f;

        /// <summary>Khay đôi lùi lên khỏi đường chừng này ô (không có đoạn đường nối, sát đường trông chật) [user 25/10].</summary>
        public const float PairBack = 0.5f;

        /// <summary>Tâm cụm khay của một điểm giao (khay đôi lùi lên <see cref="PairBack"/>), dùng để canh camera.</summary>
        public static Vector3 StopCratesCenter(Vector2Int endPoint, bool paired) => CratePosition(endPoint) + new Vector3(0f, paired ? PairBack : 0f, 0f);

        /// <summary>Khung khay trên một điểm giao: khay đôi rộng hai khay cộng khe.</summary>
        public static Vector3 StopCratesSize(bool paired) => paired ? new Vector3(CrateSize.x * 2f + PairGap, CrateSize.y, 0f) : CrateSize;

        [SerializeField] private GameObject crate9Prefab;
        [Tooltip("Hole_VO: hố chồng khay đứng trong; lộ sàn gỗ khi cột hết khay.")]
        [SerializeField] private GameObject holePrefab;
        [SerializeField] private GameObject pillPrefab;
        [SerializeField] private FruitGraphicSet fruits;
        [Tooltip("Bàn có ô điểm giao (TileCov_Crate_Delivery) cần bật sáng.")]
        [SerializeField] private BoardView board;
        [Tooltip("Trái giao bay ra từ đầu rắn.")]
        [SerializeField] private SnakeView snake;
        [Header("Khay đóng / mở (TOGGLE CRATE, từ Level 71)")]
        [Tooltip("ShutterCrateVisual: cửa gắn lên khay trên cùng của mỗi cột khi màn có khay đóng / mở.")]
        [SerializeField] private ShutterCrateView shutterPrefab;
        [Tooltip("Rắn tới khay đang đóng: khay nảy cỡ thêm bấy nhiêu trong bấy nhiêu giây (gốc ClosedCrateRejectPunchDuration 0.25).")]
        [SerializeField] private Vector3 rejectPunch = new Vector3(0.12f, 0.12f, 0f);
        [SerializeField] private float rejectDuration = 0.25f;
        private readonly Dictionary<int, ShutterCrateView> _shutters = new Dictionary<int, ShutterCrateView>();

        [Header("Khay đôi (sáng tạo, màn sau 366)")]
        [Tooltip("PairRope: xích căng nối hai khay trên cùng (dài sẵn đúng khe PairGap, hai đầu chui dưới thành khay); mắt xích trong Left / Right tô theo màu dây của trái hai khay (FruitGraphicSet.ropeColor).")]
        [SerializeField] private GameObject pairRopePrefab;
        [Tooltip("Hole_Pair_VO: biến thể Hole_VO kín miệng cho mỗi chồng khay đôi (không đoạn đường nối, cỏ bao quanh): bỏ phần đường của Mesh_Path_CrateDelivery, tắt bóng miệng (Mesh_Path_Shadow_CrateHole, _1, _2).")]
        [SerializeField] private GameObject pairHolePrefab;
        [Tooltip("Xích + khoen đưa ra trước mặt thùng chừng này (đơn vị thế giới) để khoen đè lên mép thùng, che viền thùng ở chỗ nối.")]
        [SerializeField] private float pairRopeLift = 0.6f;
        [Tooltip("Xích + khoen nâng lên chừng này (đơn vị khay) cho nằm giữa thành khay trên màn.")]
        [SerializeField] private float pairRopeRaise = 0.4f;

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
        [Tooltip("Khay thần kỳ (sáng tạo): trái của đợt chưa rải hiện lên ở ô của nó trong chừng ấy giây rồi mới bay vào khay.")]
        [SerializeField] private float aheadAppearDuration = 0.2f;
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
        private int _magicLeft, _magicOrder; // Khay thần kỳ: số trái còn chờ xếp lượt bay, lượt kế
        private readonly Dictionary<Transform, Coroutine> _shakes = new Dictionary<Transform, Coroutine>();
        private readonly List<(PairChainView chain, int partner, int tier)> _chains = new List<(PairChainView, int, int)>(); // xích khay đôi đang hiện

        /// <summary>Tâm khay trên cùng của cột.</summary>
        public static Vector3 CratePosition(Vector2Int endPoint) =>
            BoardView.CellToWorld(endPoint) + new Vector3(0f, CrateHeightAboveEndPoint, TopCrateLift);

        public void Bind(GameSession session)
        {
            _session = session;
            // Mọi cột, kể cả chỗ khay thêm (gốc mua bằng xu, ở đây để trống).
            if (holePrefab != null)
                for (int column = 0; column < session.Columns.Count; column++)
                {
                    // Khay đôi: mỗi chồng một hố như khay thường, đứng hai bên điểm giao của cột đầu [user 23/10].
                    if (!session.Board.CrateAnchors.TryGetValue(session.PairLead(column), out var anchor)) continue;
                    var hole = Instantiate(session.PartnerOf(column) >= 0 && pairHolePrefab != null ? pairHolePrefab : holePrefab, transform);
                    hole.name = $"Hole c{column}";
                    hole.transform.localPosition = BoardView.CellToWorld(anchor) + new Vector3(PairOffset(column), CrateHeightAboveEndPoint + (session.PartnerOf(column) >= 0 ? PairBack : 0f), 0f);
                    _holes[column] = hole;
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
            // Khay thần kỳ: trái bay lần lượt kiểu boomerang, khay đầy chờ trái cuối như boomerang.
            session.MagicCrateUsed += (column, count) => { _magicLeft = count; _magicOrder = 0; _boomerang = true; };
            session.FruitTakenAhead += (cell, kind, column, crate) =>
                FlyToCrate(BoardView.CellToWorld(cell), kind, column, crate, NextMagicDelay(), true, aheadAppearDuration);
            session.CurtainOpened += index => StartCoroutine(ShowUncovered(curtains != null ? curtains.RevealTime : 0.6f));
            // Cửa đóng và cửa mở chạy cùng lúc, sau khi trái cuối bay vào xong [video lv70; user: không lần lượt].
            session.CrateShutterChanged += (column, closed) => StartCoroutine(SetShutter(column, closed, flightDuration + landingDuration));
            // Khay đóng bị rắn tới thì nảy từ chối.
            session.ClosedCrateRejected += column =>
            {
                if (!_columns.TryGetValue(column, out var crates) || crates.Count == 0 || crates[0] == null) return;
                var visual = crates[0].transform.Find("BoxVisual_VO");
                if (visual != null) StartCoroutine(Tween.Punch(rejectDuration * 0.5f, t => { if (visual != null) visual.localScale = Vector3.one + rejectPunch * t; }));
            };
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
            float delay = _magicLeft > 0 ? NextMagicDelay() : _boomerang ? 0f : _snipOrder++ * GameSession.DeliveryInterval;
            FlyToCrate(start, kind, columnIndex, crateIndex, delay, _boomerang);
        }

        /// <summary>Lượt bay của trái Khay thần kỳ kế tiếp (0 khi không phải trái của nó, vd nam châm).</summary>
        private float NextMagicDelay()
        {
            if (_magicLeft <= 0) return 0f;
            _magicLeft--;
            return _magicOrder++ * GameSession.MagicCrateInterval;
        }

        /// <summary>Vị trí khay của cột (world), vd cho bong bóng TAP TO UNLOCK.</summary>
        public Vector3 ColumnWorldPosition(int column) =>
            TryBasePosition(column, out var top) ? transform.TransformPoint(top) : transform.position;

        /// <summary>Tâm khay trên cùng của cột; khay đôi đứng hai bên điểm giao của cột đầu (cột đầu bên trái).</summary>
        private bool TryBasePosition(int column, out Vector3 position)
        {
            int lead = _session.PairLead(column);
            if (!_session.Board.CrateAnchors.TryGetValue(lead, out var anchor)) { position = default; return false; }
            position = StopCratesCenter(anchor, _session.PartnerOf(column) >= 0) + new Vector3(PairOffset(column), 0f, 0f);
            return true;
        }

        /// <summary>Khay đôi: cột đầu lệch trái, cột sau lệch phải khỏi điểm giao; cột thường 0.</summary>
        private float PairOffset(int column) =>
            _session.PartnerOf(column) < 0 ? 0f : (column == _session.PairLead(column) ? -0.5f : 0.5f) * (CrateSize.x + PairGap);

        /// <summary>
        /// Khay đôi: mỗi tầng đang hiện một xích căng từ khay cột đầu sang khay cùng tầng cột sau, nửa trái màu trái khay trái, nửa phải
        /// màu khay phải [user 23/10; tầng dưới cũng có xích, cùng chỗ như tầng trên: user 25/10]. Dây là con của khay cột đầu nên dâng /
        /// biến mất cùng tầng; dựng lại kèm chồng.
        /// </summary>
        private void AttachRopes(int columnIndex, List<GameObject> crates, int top)
        {
            int partner = _session.PartnerOf(columnIndex);
            if (pairRopePrefab == null || partner < 0 || _session.PairLead(columnIndex) != columnIndex) return;
            var partnerCrates = _session.Columns[partner].Crates;
            var view = Camera.main;
            for (int k = 0; k < crates.Count && top + k < partnerCrates.Count; k++)
            {
                var rope = Instantiate(pairRopePrefab, crates[k].transform);
                rope.name = "PairRope";
                rope.transform.localPosition = new Vector3((CrateSize.x + PairGap) * 0.5f, pairRopeRaise, 0f);
                // Ra trước mặt thùng theo hướng nhìn của camera (camera nghiêng): đứng yên trên màn mà vẽ đè lên mép thùng.
                if (view != null) rope.transform.position -= view.transform.forward * pairRopeLift;
                Tint(rope.transform.Find("Left"), _session.Columns[columnIndex].Crates[top + k].kind);
                Tint(rope.transform.Find("Right"), partnerCrates[top + k].kind);
                // Khoen đi theo thân thùng khi thùng rung (Shake), xích đung đưa theo.
                var chain = rope.GetComponent<PairChainView>();
                if (chain == null) continue;
                int tier = k;
                chain.Bind(crates[k].transform.Find("BoxVisual_VO"), () => PartnerCrate(partner, tier) is GameObject other ? other.transform.Find("BoxVisual_VO") : null);
                _chains.RemoveAll(entry => entry.chain == null);
                _chains.Add((chain, partner, tier));
            }
        }

        // Cột sau dựng lại sau cột đầu, nên khay của nó tra lúc cần chứ không giữ từ lúc gắn xích.
        private GameObject PartnerCrate(int partner, int tier) =>
            _columns.TryGetValue(partner, out var crates) && tier < crates.Count && crates[tier] != null ? crates[tier] : null;

        /// <summary>Thùng có xích khay đôi nhận trái: xích bị hất theo phía thùng đó.</summary>
        private void KickChain(Transform crate)
        {
            foreach (var (chain, partner, tier) in _chains)
            {
                if (chain == null) continue;
                if (chain.transform.parent == crate) chain.Kick(false);
                else if (PartnerCrate(partner, tier) is GameObject other && other.transform == crate) chain.Kick(true);
            }
        }

        private void Tint(Transform half, PillKind kind)
        {
            var entry = fruits != null ? fruits.Get(kind) : null;
            if (half == null || entry == null) return;
            // Khoen màu thành thùng (liền với thùng), mắt xích màu dây.
            foreach (var part in half.GetComponentsInChildren<SpriteRenderer>()) part.color = part.name == "Anchor" ? entry.crateRimColor : entry.ropeColor;
        }

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

        /// <summary>Booster nam châm (và trái trên bàn của Khay thần kỳ): trái bay từ ô vào khay, kiểu bay boomerang.</summary>
        private void OnMagnetized(Vector2Int cell, PillKind kind, int columnIndex, int crateIndex) =>
            FlyToCrate(BoardView.CellToWorld(cell), kind, columnIndex, crateIndex, NextMagicDelay(), true);

        private void FlyToCrate(Vector3 start, PillKind kind, int columnIndex, int crateIndex, float delay, bool boomerang, float appear = 0f)
        {
            if (!_columns.TryGetValue(columnIndex, out var crates) || crates.Count == 0) return;
            int slot = _session.Columns[columnIndex].FilledIn(crateIndex) - 1;
            if (SlotFor(columnIndex, crateIndex, slot) != null)
            {
                var key = (columnIndex, crateIndex);
                _inFlight[key] = InFlight(key) + 1;
                StartCoroutine(Fly(kind, () => SlotFor(columnIndex, crateIndex, slot), start, delay, boomerang: boomerang,
                    landed: () => _inFlight[key] = InFlight(key) - 1, appear: appear));
            }
            else
            {
                // Khay sâu hơn chồng đang hiện: bay vào khay dưới cùng rồi biến, hiện lại khi khay dâng lên.
                var bottom = crates[crates.Count - 1].transform;
                StartCoroutine(Fly(kind, () => bottom, start, delay, vanish: true, boomerang: boomerang, appear: appear));
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
        private IEnumerator Fly(PillKind kind, System.Func<Transform> target, Vector3 start, float delay, bool vanish = false, bool boomerang = false,
            System.Action landed = null, float appear = 0f)
        {
            // Trái hiện ngay tại chỗ (thân đuôi cắt biến mất, trái ở lại) rồi chờ lượt [video Man_Lose, user].
            var fruit = Instantiate(pillPrefab, transform);
            PillVisual.SetKind(fruit, kind);
            PillVisual.HideShadow(fruit);
            fruit.transform.position = start;
            // Trái chưa có trên bàn (đợt sau, Khay thần kỳ): nở ra tại chỗ trước, phần còn lại của lượt chờ như thường.
            if (appear > 0f)
                yield return Tween.Run(appear, Tween.OutBack(easeBack), t => { if (fruit != null) fruit.transform.localScale = Vector3.one * t; });
            delay -= appear;
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
            KickChain(crate);
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
            // Khay đôi: xích đứt, mắt xích văng ra khi cặp khay bay đi (xích là con của khay cột đầu).
            var chain = finished != null ? finished.GetComponentInChildren<PairChainView>() : null;
            if (chain != null) chain.Break(transform);
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

            if (!TryBasePosition(columnIndex, out var basePosition)) return;
            var column = _session.Columns[columnIndex];
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
            if (crates.Count > 0) AttachShutter(columnIndex, crates[0], column.Crates[top].kind);
            AttachRopes(columnIndex, crates, top);
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

        /// <summary>Cửa lên khay trên cùng, màu theo loại trái, đúng trạng thái session hiện tại.</summary>
        private void AttachShutter(int column, GameObject crate, PillKind kind)
        {
            _shutters.Remove(column);
            if (shutterPrefab == null || !_session.HasShutters) return;
            var view = Instantiate(shutterPrefab, crate.transform, false);
            var entry = fruits != null ? fruits.Get(kind) : null;
            view.SetColor(entry?.shutterMaterial, entry?.shutterPlaneMaterial);
            view.HideWhenClosed(crate.transform.Find("AllDestinationPoints"));
            view.Snap(_session.IsCrateClosed(column));
            _shutters[column] = view;
        }

        private IEnumerator SetShutter(int column, bool closed, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            // Khay có thể đã đổi (khay đầy đi, khay sau lên) trong lúc chờ: lấy cửa hiện tại; cửa mới đã đúng trạng thái thì đứng yên.
            if (_shutters.TryGetValue(column, out var view) && view != null && _session.IsCrateClosed(column) == closed) view.Animate(closed);
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
