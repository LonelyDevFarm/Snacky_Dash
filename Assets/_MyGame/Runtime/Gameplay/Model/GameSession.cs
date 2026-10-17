using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Luật chơi của một màn (rắn chạy, ăn trái, giao vào crate, cắn đuôi, thắng), thuần C#, không gắn prefab.
    /// GameBootstrap tạo và gọi Tick mỗi frame; các view chỉ nghe event của lớp này.
    /// </summary>
    public sealed partial class GameSession
    {
        public const float DefaultCellsPerSecond = 26f; // ô/giây; gốc 30 (PlayerView._moveSpeed), chậm lại [user]
        public const float SegmentSpacing = 1f;
        public const float DeliveryInterval = 0.048f; // giây/trái (gốc: CrateManager.timeBetweenPillsToCrate)

        /// <summary>Dừng giao sau khi crate đầy để hiệu ứng hoàn thành chạy xong (video LV2-3: 0.8-1.2 s).</summary>
        public const float CrateCompletePause = 1f;

        // Cắn đuôi: đầu cách thân < 0.7 ô, thân từ 5 đốt (gốc: TailCuttingController).
        public const float BiteDistance = 0.7f;
        public const int MinSegmentsBeforeBite = 5;
        /// <summary>Snip giữ lại (độ dài thân / 5) đốt tính từ đầu [user].</summary>
        public const int KeptPerSegments = 5;
        /// <summary>Số trái mỗi crate ở bản game hiện tại (gốc: Box_Crate_9_VO).</summary>
        public const int CrateCapacity = 9;

        /// <summary>Một chồng crate: lấp đầy lần lượt từ trên xuống; crate bên dưới có thể được snip/nam châm lấp trước.</summary>
        public sealed class CrateColumn
        {
            private List<CrateDefinition> _crates;
            private List<int> _filled;
            public IReadOnlyList<CrateDefinition> Crates => _crates;
            /// <summary>Cột crate thêm: khóa (sàn gỗ trống) tới khi mua (gốc: ExtraCrateController).</summary>
            public bool IsExtra { get; }
            public int CurrentIndex { get; internal set; }
            public int Filled => IsComplete ? 0 : _filled[CurrentIndex];
            public bool IsComplete => CurrentIndex >= Crates.Count;
            public CrateDefinition Current => Crates[CurrentIndex];

            /// <summary>Số trái trong crate bất kỳ của chồng (crate dưới có thể đã được snip lấp).</summary>
            public int FilledIn(int crateIndex) => _filled[crateIndex];
            internal void Add(int crateIndex) => _filled[crateIndex]++;
            internal bool IsFull(int crateIndex) => _filled[crateIndex] >= Crates[crateIndex].requiredCount;
            internal int[] SaveFilled() => _filled.ToArray();
            internal void Restore(int[] filled, int current) { for (int i = 0; i < filled.Length; i++) _filled[i] = filled[i]; CurrentIndex = current; }

            internal CrateColumn(IReadOnlyList<CrateDefinition> crates, bool isExtra = false)
            {
                _crates = SplitIntoCratesOf9(crates);
                _filled = new List<int>(new int[_crates.Count]);
                IsExtra = isExtra;
            }

            /// <summary>Lấy ra các crate chưa đầy (crate hiện tại trở xuống) kèm số trái đã có, để chia lại khi mở cột thêm.</summary>
            internal List<(CrateDefinition crate, int filled)> TakeRemaining()
            {
                var remaining = new List<(CrateDefinition, int)>();
                for (int i = CurrentIndex; i < _crates.Count; i++) remaining.Add((_crates[i], _filled[i]));
                if (CurrentIndex < _crates.Count)
                {
                    _crates.RemoveRange(CurrentIndex, _crates.Count - CurrentIndex);
                    _filled.RemoveRange(CurrentIndex, _filled.Count - CurrentIndex);
                }
                return remaining;
            }

            internal void Append(CrateDefinition crate, int filled) { _crates.Add(crate); _filled.Add(filled); }

            /// <summary>
            /// Dữ liệu v1.14.1 còn crate 18/27 nhưng game hiện tại chỉ có crate 9 [user],
            /// nên crate lớn được tách thành nhiều crate 9 liên tiếp cùng loại.
            /// </summary>
            private static List<CrateDefinition> SplitIntoCratesOf9(IReadOnlyList<CrateDefinition> crates)
            {
                var result = new List<CrateDefinition>();
                foreach (var crate in crates)
                    for (int left = crate.requiredCount; left > 0; left -= CrateCapacity)
                        result.Add(new CrateDefinition { kind = crate.kind, requiredCount = Mathf.Min(left, CrateCapacity) });
                return result;
            }
        }

        public event Action<Vector2Int, PillKind> PillSpawned;
        public event Action<Vector2Int, PillKind, int> PillEaten;           // ô, loại, chỉ số đốt mới
        public event Action<int, PillKind, int> SegmentDelivered;           // chỉ số đốt bị lấy, loại, cột
        public event Action<int, int> CrateCompleted;                      // cột, chỉ số crate
        public event Action<Direction> MoveStarted;
        public event Action<Vector2Int> Stopped;
        public event Action<Direction> MoveBlocked;                        // vuốt vào tường hoặc vào thân
        public event Action<int> DeliveryStarted;                          // cột
        public event Action<int> DeliveryEnded;                            // cột
        public event Action<int> TailBitten;                               // đốt đầu tiên snip sẽ cắt
        public event Action<int, PillKind, int, int> SegmentSnipped;       // đốt, loại, cột, crate (trước khi xóa đốt)
        public event Action<int> TailSnipped;                              // số đốt giữ lại
        public event Action<int, IReadOnlyList<int>> BoomerangThrown;      // cột, các đốt sẽ bay
        public event Action<int> SegmentRemoved;                           // đốt bị rút ra giữa thân (sau SegmentSnipped)
        public event Action<Vector2Int, Vector2Int> ArrowsFlipped;          // hai mũi tên của cặp, sau khi quay
        public event Action<Vector2Int> ArrowNotAllowed;                    // cổng một chiều cạnh đường mà đầu không được vào
        public event Action<int, int> WallHealthChanged;                    // nhóm đá, máu còn lại
        public event Action<int> WallBroken;                                // nhóm đá vỡ, đường mở
        public event Action<Vector2Int, int> SecretRevealed;                // ô, đốt: vừa ăn trái bí ẩn
        public event Action Won;

        private readonly BoardGraph _board;
        private readonly TiledMap _map;
        private readonly LevelDefinition _level;
        private readonly Dictionary<int, PillKind> _spawnIds;
        private readonly Dictionary<Vector2Int, PillKind> _pills = new Dictionary<Vector2Int, PillKind>();
        private readonly List<PillKind> _segments = new List<PillKind>();
        private readonly List<Vector2> _trail = new List<Vector2>(); // tâm các ô đã qua, mới nhất trước
        // [i] >= 0: đoạn _trail[i] -> _trail[i+1] đi trong đường hầm dài chừng đó; NotTunnel = đường thường.
        // Hầm vừa vào dài 0, nên không dùng 0 làm "không hầm" (từng vẽ thân thẳng giữa hai miệng hầm 1 frame).
        private const float NotTunnel = -1f;
        private readonly List<float> _trailJump = new List<float>();
        private readonly Dictionary<int, Vector2Int> _arrowEntered = new Dictionary<int, Vector2Int>(); // nhóm mũi tên lật -> mũi tên đầu đã đi vào
        private readonly List<int> _wallHealth = new List<int>();
        private readonly HashSet<Vector2Int> _secret = new HashSet<Vector2Int>();

        /// <summary>Số crate còn phải xong trước khi mỗi nhóm đá vỡ (thứ tự BoardGraph.WallGroups).</summary>
        public IReadOnlyList<int> WallHealth => _wallHealth;

        /// <summary>Trái bí ẩn: ẩn loại tới khi bị ăn.</summary>
        public bool IsSecret(Vector2Int cell) => _secret.Contains(cell);
        private float _tunnelLeft; // > 0: thân còn đang trượt vào hầm (quãng đường, ở tốc độ hầm)
        private float _tunnelWait; // > 0: cả rắn trong hầm, số giây tới khi đầu ra

        /// <summary>Vào hầm thân trượt nhanh gấp đôi, rồi ẩn 0.4 s mới ra (video Lv14, [user]).</summary>
        public const float TunnelSpeedMultiplier = 2f;
        public const float TunnelInsideTime = 0.4f;

        /// <summary>Đầu ẩn trong hầm tới khi cả thân đã vào và đi qua [user].</summary>
        public bool HeadInTunnel => _tunnelLeft > 0f || _tunnelWait > 0f;
        private readonly float _speed;

        /// <summary>Lượt vuốt nhớ sẵn hết hạn sau 1 s (gốc: SwipeRecorder.maxTimeRecordSwipe).</summary>
        public const float BufferedSwipeLifetime = 1f;

        /// <summary>
        /// Số giây sau khi dừng mà ngón đang giữ chưa được đi tiếp: điểm dừng gần nhau thì kéo nhẹ cũng
        /// làm rắn chạy luôn [play-test; gốc không có].
        /// </summary>
        public float HeldSwipeGraceAfterStop { get; set; } = 0.15f;

        /// <summary>Rải trái ngẫu nhiên cho màn không có layer spawn cố định; test thì đặt seed.</summary>
        private System.Random _random = new System.Random();
        public void SetSeed(int seed) => _random = new System.Random(seed);

        private Vector2Int _target;
        private Direction? _bufferedSwipe;
        private float _bufferedAge;
        private float _sinceStop = float.MaxValue;
        private int _deliveryColumn = -1;
        private float _deliveryTimer;
        private bool _deliverFromTail; // false: chỉ hoàn thành crate do snip lấp (rắn không đứng ở crate)
        private bool _boomerang;       // lượt giao này là boomerang (trái bay từ thân)
        private int _boomerangLeft;    // số trái boomerang còn phải ném

        /// <summary>Giây giữa hai trái boomerang (video Lv7 ~0.067 s, trên điện thoại thấy nhanh quá [user]).</summary>
        public const float BoomerangInterval = 0.1f;

        /// <summary>Chờ giữa trái cuối của crate đầy và trái đầu crate sau, khoảng một lần bay (gốc: boomerangFlightDuration 0.55).</summary>
        public const float BoomerangCrateGap = 0.65f;

        public Vector2 HeadPosition { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        /// <summary>Ô đầu đang chạy tới (bằng <see cref="CurrentCell"/> khi đã tới).</summary>
        public Vector2Int TargetCell => _target;
        public Direction Heading { get; private set; } = Direction.Up;
        public bool IsMoving { get; private set; }
        public bool IsDelivering => _deliveryColumn >= 0;
        /// <summary>Đầu đã cắn đuôi: chờ snip hoặc người chơi bỏ cuộc.</summary>
        public bool IsStuck { get; private set; }
        /// <summary>Số đốt snip sẽ cắt: mọi đốt từ <see cref="SnipKeep"/> trở đi.</summary>
        public int SnipCount => IsStuck ? _segments.Count - SnipKeep : 0;
        /// <summary>Số đốt snip giữ lại, tính từ đầu.</summary>
        public int SnipKeep { get; private set; }
        public bool IsWon { get; private set; }
        public int BatchIndex { get; private set; } = -1;
        public IReadOnlyList<PillKind> Segments => _segments;
        public IReadOnlyDictionary<Vector2Int, PillKind> Pills => _pills;
        public IReadOnlyList<CrateColumn> Columns { get; }

        /// <summary>Cột crate thêm (-1 nếu màn không có); khóa tới khi mua (TAP TO UNLOCK, 700 xu).</summary>
        public int ExtraColumn { get; }
        public bool ExtraLocked { get; private set; }
        public event Action<int> ExtraColumnUnlocked; // cột thêm

        /// <summary>Mở được khi rắn đứng yên và còn cột có crate chờ bên dưới crate trên cùng (có cái để chia).</summary>
        public bool CanUnlockExtra
        {
            get
            {
                if (!ExtraLocked || !IsIdle) return false;
                foreach (var column in Columns) if (!column.IsExtra && !column.IsComplete && column.CurrentIndex + 1 < column.Crates.Count) return true;
                return false;
            }
        }

        /// <summary>
        /// Mở cột thêm và chia lại mọi crate chưa đầy theo tầng, lần lượt cột mới trước rồi các cột cũ (ảnh Level 30 [user]).
        /// Trái đã có đi theo crate; xóa undo vì chồng crate đã khác.
        /// </summary>
        public bool UnlockExtraColumn()
        {
            if (!CanUnlockExtra) return false;
            var open = new List<CrateColumn>();
            var waiting = new List<List<(CrateDefinition crate, int filled)>>();
            foreach (var column in Columns)
                if (!column.IsExtra && !column.IsComplete) { open.Add(column); waiting.Add(column.TakeRemaining()); }
            var pool = new List<(CrateDefinition crate, int filled)>();
            for (int depth = 0; ; depth++)
            {
                bool any = false;
                foreach (var list in waiting) if (depth < list.Count) { pool.Add(list[depth]); any = true; }
                if (!any) break;
            }
            var targets = new List<CrateColumn> { Columns[ExtraColumn] };
            targets.AddRange(open);
            for (int i = 0; i < pool.Count; i++) targets[i % targets.Count].Append(pool[i].crate, pool[i].filled);
            ExtraLocked = false;
            _beforeLastMove = null;
            ExtraColumnUnlocked?.Invoke(ExtraColumn);
            return true;
        }
        public BoardGraph Board => _board;

        public GameSession(BoardGraph board, TiledMap map, LevelDefinition level)
        {
            _board = board;
            _map = map;
            _level = level;
            _spawnIds = SpawnIdMap.Build(level.batches);
            _speed = level.speedOverride > 0f ? level.speedOverride : DefaultCellsPerSecond;

            var columns = new List<CrateColumn>();
            foreach (var column in level.columns) columns.Add(new CrateColumn(column.crates, column.isExtra));
            Columns = columns;
            ExtraColumn = columns.FindIndex(c => c.IsExtra);
            ExtraLocked = ExtraColumn >= 0;

            // Máu đá: của màn, hoặc riêng của nhóm nếu một ô của nhóm có trong danh sách.
            foreach (var group in board.WallGroups)
            {
                int health = level.wallHealth;
                foreach (var own in level.wallGroupHealths)
                    if (System.Linq.Enumerable.Contains(group, own.seedCell)) health = own.health;
                _wallHealth.Add(Mathf.Max(1, health));
            }
            SetUpMechanics(level);
            SetUpShutters(level);

            CurrentCell = board.PlayerStart;
            HeadPosition = CurrentCell;
            _trail.Add(CurrentCell);
            _trailJump.Add(NotTunnel);
        }

        /// <summary>Rải đợt trái đầu; tách khỏi constructor để view kịp đăng ký event.</summary>
        public void Begin()
        {
            if (BatchIndex < 0) SpawnNextBatch();
        }

        /// <summary>Tutorial: chỉ nhận đúng hướng vuốt này; tự xóa khi rắn bắt đầu chạy.</summary>
        public Direction? ForcedSwipe { get; set; }

        /// <summary>Đang hiện thông báo tutorial: bỏ qua vuốt tới khi chạm tắt.</summary>
        public bool InputBlocked { get; set; }

        private float _lockLeft;

        /// <summary>
        /// Khóa vuốt và booster trong lúc hiệu ứng chạy để vuốt không cắt ngang [user]. Tính theo thời gian,
        /// để hiệu ứng bị hủy giữa chừng không làm khóa mãi.
        /// </summary>
        public void LockInput(float seconds) => _lockLeft = Mathf.Max(_lockLeft, seconds);

        public bool InputLocked => _lockLeft > 0f;

        /// <summary>
        /// Tutorial booster bắt buộc: bỏ qua vuốt (không nhớ sẵn), booster vẫn dùng được, tới khi dùng booster
        /// [user: Level 4 vuốt nhanh làm rắn chạy qua tutorial xẻng].
        /// </summary>
        public bool SwipesHeld { get; set; }

        /// <summary>
        /// Vuốt khi đang chạy thì chỉ nhớ lượt mới nhất, áp dụng ở điểm dừng kế tiếp (đã thử trên bản gốc).
        /// Có thân thì không quay đầu vào đốt ngay sau đầu.
        /// </summary>
        /// <param name="fingerHeld">
        /// Ngón vẫn giữ và vuốt tiếp: bỏ qua khi rắn đang chạy và trong <see cref="HeldSwipeGraceAfterStop"/> sau khi dừng [user].
        /// </param>
        public bool RequestMove(Direction direction, bool fingerHeld = false)
        {
            if (IsWon || IsStuck || IsDelivering || IsRewinding) return false; // đang giao trái / undo chưa chạy lùi xong: bỏ qua vuốt [user]
            if (InputBlocked) return false;
            if (_lockLeft > 0f) return false; // hiệu ứng đang chạy: không nhớ sẵn luôn
            if (SwipesHeld) return false;
            if (ForcedSwipe.HasValue && direction != ForcedSwipe.Value) return false; // tutorial Level 1: chỉ hướng được chỉ [user]
            if (IsMoving)
            {
                if (!fingerHeld)
                {
                    _bufferedSwipe = direction;
                    _bufferedAge = 0f;
                }
                return false;
            }
            if (fingerHeld && _sinceStop < HeldSwipeGraceAfterStop) return false;
            if (!_board.CanMove(CurrentCell, direction) || BlockedByTail(direction))
            {
                MoveBlocked?.Invoke(direction);
                return false;
            }

            ForcedSwipe = null;
            SaveForUndo();
            Heading = direction;
            _target = CurrentCell + direction.ToCell();
            IsMoving = true;
            OnMoveStartedForBoosters();
            MoveStarted?.Invoke(direction);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_lockLeft > 0f) _lockLeft = Mathf.Max(0f, _lockLeft - deltaTime);
            if (IsWon || IsStuck) return;
            if (_rewindTime > 0f) _rewindTime = Mathf.Max(0f, _rewindTime - deltaTime);
            TickMagnet(deltaTime);
            _bufferedAge += deltaTime;
            if (!IsMoving) _sinceStop += deltaTime;
            if (IsMoving) Advance(_speed * deltaTime);
            else if (_deliveryColumn >= 0) TickDelivery(deltaTime);
            RaiseLeftBlocks();
            ApplyWaitingBridges();
        }

        /// <summary>Vị trí đốt <paramref name="index"/> (0 = ngay sau đầu); các đốt cách nhau 1 ô trên đường đầu đã đi.</summary>
        public Vector2 SegmentPosition(int index) => SampleTrail((index + 1) * SegmentSpacing);

        /// <summary>Điểm trên đường đầu vừa đi, cách đầu <paramref name="distance"/> ô.</summary>
        public Vector2 PathPoint(float distance) => SampleTrail(distance);

        public bool PathInTunnel(float distance)
        {
            SampleTrail(distance, out bool inTunnel);
            return inTunnel;
        }

        /// <summary>Tổng trái của các đợt 0..<paramref name="lastWave"/>.</summary>
        public int FruitInWaves(int lastWave)
        {
            int total = 0;
            for (int b = 0; b <= lastWave && b < _level.batches.Count; b++)
                foreach (var pill in _level.batches[b].pills) total += pill.count;
            return total;
        }

        public int TotalFruit => FruitInWaves(_level.batches.Count - 1);

        /// <summary>Đã ăn = đã rải trừ số còn trên bàn.</summary>
        public int FruitEaten => BatchIndex < 0 ? 0 : FruitInWaves(BatchIndex) - _pills.Count;

        public int WaveCount => _level.batches.Count;

        /// <summary>
        /// Trái còn phải ăn theo loại (thanh đếm dưới thanh LEVEL; gốc RemainingFruitTally): trái đang trên bàn cộng trái của các
        /// đợt chưa rải. Ăn (cả nam châm hút) thì giảm, hoàn tác thì tăng lại; giao vào khay hay Snip không đổi [ảnh lv129 của user].
        /// </summary>
        public int RemainingFruit(PillKind kind)
        {
            int count = 0;
            foreach (var pill in _pills.Values) if (pill == kind) count++;
            for (int b = BatchIndex + 1; b < _level.batches.Count; b++)
                foreach (var pill in _level.batches[b].pills) if (pill.kind == kind) count += pill.count;
            return count;
        }

        /// <summary>Loại trái có trong màn (ở bất kỳ đợt nào): thanh đếm chỉ hiện các loại này.</summary>
        public bool LevelHasFruit(PillKind kind)
        {
            foreach (var batch in _level.batches)
                foreach (var pill in batch.pills) if (pill.kind == kind && pill.count > 0) return true;
            return false;
        }

        /// <summary>Rắn có mang trái mà crate hiện tại của cột nhận.</summary>
        public bool CanDeliverTo(int column)
        {
            if (column < 0 || column >= Columns.Count || Columns[column].IsComplete || IsCrateClosed(column)) return false;
            return _segments.Contains(Columns[column].Current.kind);
        }

        /// <summary>
        /// Cắt đuôi: giữ <see cref="SnipKeep"/> đốt đầu, các đốt còn lại bay vào crate cùng loại ở bất kỳ chồng nào
        /// (video Man_Lose, [user]). Kiểm tra xu do người gọi lo (SnipOffer).
        /// </summary>
        public bool Snip()
        {
            if (!IsStuck) return false;
            ForgetUndo(); // như booster: undo không bao giờ trả lại trái đã cắt [user]
            int keep = SnipKeep;
            int removed = _segments.Count - keep;

            // Xóa từ cuối đuôi để chỉ số gửi cho view vẫn đúng trong lúc xóa.
            var leftovers = new List<(Vector2Int cell, PillKind kind)>();
            for (int i = _segments.Count - 1; i >= keep; i--)
            {
                var kind = _segments[i];
                if (TryReserveCrate(kind, out int column, out int crate))
                    SegmentSnipped?.Invoke(i, kind, column, crate);
                else
                    leftovers.Add((Vector2Int.RoundToInt(SegmentPosition(i)), kind)); // chỉ khi dữ liệu lệch
                _segments.RemoveAt(i);
            }

            IsStuck = false;
            SnipKeep = 0;
            TailSnipped?.Invoke(keep);

            // Không nên xảy ra (số trái khớp crate); nếu có thì thả trái lại lên bàn.
            foreach (var (near, kind) in leftovers)
            {
                var cell = FreeCellNear(near);
                if (!cell.HasValue) continue;
                _pills[cell.Value] = kind;
                PillSpawned?.Invoke(cell.Value, kind);
            }

            EnsureShutterProgress();
            StartSettlingFullCrates(removed * DeliveryInterval);
            return true;
        }

        /// <summary>
        /// Lên kế hoạch boomerang: lấy đốt cùng loại gần đầu nhất cho crate hiện tại; crate kế chỉ được tính
        /// khi crate trước sẽ đầy (video Lv7, gốc: BoomerangDeliveryPlanner).
        /// </summary>
        public List<(int segment, int crate)> PlanBoomerang(int columnIndex)
        {
            var plan = new List<(int segment, int crate)>();
            if (IsWon || IsStuck || IsMoving || IsDelivering) return plan;
            if (columnIndex < 0 || columnIndex >= Columns.Count || IsCrateClosed(columnIndex)) return plan; // khay đóng không nhận
            var column = Columns[columnIndex];
            var taken = new HashSet<int>();
            for (int crate = column.CurrentIndex; crate < column.Crates.Count; crate++)
            {
                int room = column.Crates[crate].requiredCount - column.FilledIn(crate);
                var kind = column.Crates[crate].kind;
                for (int i = 0; i < _segments.Count && room > 0; i++)
                {
                    if (_segments[i] != kind || taken.Contains(i)) continue;
                    taken.Add(i);
                    plan.Add((i, crate));
                    room--;
                }
                if (room > 0) break; // crate này chưa đầy thì không tới được crate dưới
            }
            return plan;
        }

        public bool CanBoomerang(int columnIndex) => PlanBoomerang(columnIndex).Count > 0;

        /// <summary>
        /// Ném boomerang vào cột theo <see cref="PlanBoomerang"/>, chạy như giao từ xa (video Lv7). Số lượng booster do người gọi lo.
        /// </summary>
        public bool UseBoomerang(int columnIndex)
        {
            var plan = PlanBoomerang(columnIndex);
            if (plan.Count == 0) return false;
            var segments = new List<int>();
            foreach (var entry in plan) segments.Add(entry.segment);
            ForgetUndo(); // đã dùng booster: undo chờ lượt vuốt mới [user]
            BoomerangThrown?.Invoke(columnIndex, segments);
            _deliveryColumn = columnIndex;
            _deliverFromTail = true;
            _boomerang = true;
            _boomerangLeft = plan.Count;
            _deliveryTimer = BoomerangInterval; // trái đầu bay ngay
            return true;
        }

        /// <summary>
        /// Crate còn chỗ đầu tiên cùng loại: duyệt cột theo thứ tự, mỗi cột từ crate hiện tại xuống. Khay đang mở trước; khay đóng
        /// chỉ khi <paramref name="allowClosed"/> (cắt đuôi không được mất trái) và không khay mở nào nhận.
        /// </summary>
        private bool TryReserveCrate(PillKind kind, out int column, out int crate, bool allowClosed = true)
        {
            for (int pass = 0; pass < (allowClosed ? 2 : 1); pass++)
                for (column = 0; column < Columns.Count; column++)
                {
                    if (IsCrateClosed(column) != (pass == 1)) continue;
                    var stack = Columns[column];
                    for (crate = stack.CurrentIndex; crate < stack.Crates.Count; crate++)
                    {
                        if (stack.Crates[crate].kind != kind || stack.IsFull(crate)) continue;
                        stack.Add(crate);
                        return true;
                    }
                }
            column = crate = -1;
            return false;
        }

        /// <summary>Snip/nam châm có thể lấp đầy crate trên cùng: hoàn thành nó như khi giao.</summary>
        private void StartSettlingFullCrates(float delay = 0f)
        {
            for (int column = 0; column < Columns.Count; column++)
            {
                var stack = Columns[column];
                if (stack.IsComplete || !stack.IsFull(stack.CurrentIndex) || IsCrateClosed(column)) continue; // khay đóng chờ mở mới xong
                _deliveryColumn = column;
                _deliverFromTail = false;
                _deliveryTimer = -delay; // chờ trái đã cắt bay vào xong
                return;
            }
        }

        /// <summary>Ô đi được gần nhất (BFS theo đường) không có trái, đầu hay thân.</summary>
        private Vector2Int? FreeCellNear(Vector2Int start)
        {
            var occupied = new HashSet<Vector2Int> { CurrentCell };
            for (int i = 0; i < _segments.Count; i++) occupied.Add(Vector2Int.RoundToInt(SegmentPosition(i)));

            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (_board.IsWalkable(cell) && !_board.IsCovered(cell) && !NoFruitOnBlock(cell) && !occupied.Contains(cell) && !_pills.ContainsKey(cell)) return cell;
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = cell + direction.ToCell();
                    if (_board.IsWalkable(next) && !_board.IsCovered(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return null;
        }

        private bool CheckBite()
        {
            if (_segments.Count < MinSegmentsBeforeBite) return false;
            for (int i = 1; i < _segments.Count; i++)
            {
                if (SegmentInTunnel(i)) continue; // đốt trong hầm thì không cắn được
                if (Vector2.Distance(SegmentPosition(i), HeadPosition) >= BiteDistance) continue;
                if (OnOtherLevelOfBridge(i)) continue;
                IsStuck = true;
                SnipKeep = _segments.Count / KeptPerSegments;
                TailBitten?.Invoke(SnipKeep);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Ở cầu, mặt cầu và đường dưới không chạm nhau [user]: đốt đi khác trục với đầu là ở tầng kia, không tính cắn.
        /// </summary>
        private bool OnOtherLevelOfBridge(int segment)
        {
            if (!_board.IsBridge(CurrentCell)) return false;
            var ahead = segment == 0 ? HeadPosition : SegmentPosition(segment - 1);
            var behind = SegmentPosition(Mathf.Min(segment + 1, _segments.Count - 1));
            var along = ahead - behind;
            bool segmentHorizontal = Mathf.Abs(along.x) >= Mathf.Abs(along.y);
            return segmentHorizontal != Heading.IsHorizontal();
        }

        /// <summary>Vuốt hướng này thì đầu chạy ngay (đường mở, không đâm vào đốt sau đầu).</summary>
        public bool CanGo(Direction direction) => _board.CanMove(CurrentCell, direction) && !BlockedByTail(direction);

        private bool BlockedByTail(Direction direction)
        {
            if (_segments.Count == 0) return false;
            var behindHead = Vector2Int.RoundToInt(SegmentPosition(0));
            return behindHead == CurrentCell + direction.ToCell();
        }

        private void Advance(float distance)
        {
            if (_tunnelLeft > 0f)
            {
                float boosted = distance * TunnelSpeedMultiplier;
                float used = Mathf.Min(boosted, _tunnelLeft);
                _tunnelLeft -= used;
                _trailJump[0] += used; // đoạn hầm đầu đang ở luôn là đoạn mới nhất
                distance = (boosted - used) / TunnelSpeedMultiplier;
                if (_tunnelLeft > 0f) return;
            }
            if (_tunnelWait > 0f)
            {
                float time = distance / _speed;
                if (time < _tunnelWait) { _tunnelWait -= time; return; }
                distance -= _tunnelWait * _speed;
                _tunnelWait = 0f;
            }
            while (distance > 0f)
            {
                var remaining = Vector2.Distance(HeadPosition, _target);
                if (distance < remaining)
                {
                    HeadPosition = Vector2.MoveTowards(HeadPosition, _target, distance);
                    return;
                }

                distance -= remaining;
                HeadPosition = _target;
                CurrentCell = _target;
                _trail.Insert(0, CurrentCell);
                _trailJump.Insert(0, NotTunnel);
                _swipePath.Add(CurrentCell);
                _swipeUnderground.Add(false);
                PruneTrail();
                if (_trail.Count > 1) LeftCell(Vector2Int.RoundToInt(_trail[1]));
                PassRisingBlock(CurrentCell);
                PassSwitch(CurrentCell);
                if (CheckBite())
                {
                    IsMoving = false;
                    _bufferedSwipe = null;
                    return;
                }
                Enter(CurrentCell);
                if (_stopOnArrival) // vừa trồi lên từ hố xẻng: đứng chờ ở đó
                {
                    _stopOnArrival = false;
                    Stop();
                    return;
                }
                var arrivedHeading = Heading;
                if (IsWon || !ContinueFrom(CurrentCell))
                {
                    // Tới điểm dừng cũng rung các cổng một chiều đóng quanh đó, mỗi lần [user]; không rung khi vừa đâm vào cổng.
                    if (!IsWon && _board.IsStop(CurrentCell)) SignalClosedArrows(CurrentCell, arrivedHeading, arrivedHeading.Opposite());
                    Stop();
                    return;
                }
                if (HeadInTunnel) return; // chuyến qua hầm bắt đầu từ tick sau
                SignalClosedArrows(CurrentCell, arrivedHeading, Heading);

            }
        }

        private bool ContinueFrom(Vector2Int cell)
        {
            // Vào miệng hầm [user]: đầu ẩn, thân trượt vào hết rồi đầu mới ra ở miệng kia; thân ra theo sau, không quay lại miệng đầu.
            if (_board.TryTunnel(cell, Heading, out var exit, out var exitHeading))
            {
                CurrentCell = exit;
                HeadPosition = exit;
                _trail.Insert(0, exit);
                _swipePath.Add(exit);
                _swipeUnderground.Add(true); // đoạn này đi trong hầm
                _trailJump.Insert(0, 0f); // dài dần khi thân đi vào (Advance)
                _tunnelLeft = (_segments.Count + 1) * SegmentSpacing;
                _tunnelWait = TunnelInsideTime;
                PruneTrail();
                Heading = exitHeading;
                _target = exit + Heading.ToCell();
                return true;
            }
            if (!NextStep(cell, Heading, out var heading)) return false;
            Heading = heading;
            _target = cell + Heading.ToCell();
            return true;
        }

        /// <summary>
        /// Rắn tự đi thẳng hoặc tự rẽ khi chỉ còn một lối (không tính quay lại).
        /// False ở điểm dừng, ngõ cụt hoặc ngã rẽ.
        /// </summary>
        private bool NextStep(Vector2Int cell, Direction heading, out Direction next)
        {
            next = heading;
            if (_board.IsStop(cell)) return false; // điểm dừng luôn dừng, dù cổng hay đá có chặn lối nào [user]
            if (_board.CanMove(cell, heading)) return true;
            if (_board.IsBridge(cell)) return false; // cầu chỉ đi thẳng (map thật không gặp)

            Direction? turn = null;
            foreach (var exit in _board.Exits(cell))
            {
                if (exit == heading.Opposite()) continue;
                if (turn.HasValue) return false;
                turn = exit;
            }
            if (!turn.HasValue) return false;
            next = turn.Value;
            return true;
        }

        /// <summary>
        /// Các ô một lượt vuốt sẽ chạy qua tới điểm dừng kế, để tutorial tô đường (TutorialPathOverlayService). Không đi theo hầm.
        /// </summary>
        public List<Vector2Int> PreviewPath(Direction direction)
        {
            var path = new List<Vector2Int> { CurrentCell };
            if (!_board.CanMove(CurrentCell, direction)) return path;
            var cell = CurrentCell + direction.ToCell();
            var heading = direction;
            path.Add(cell);
            for (int i = 0; i < 256 && NextStep(cell, heading, out heading); i++)
            {
                cell += heading.ToCell();
                path.Add(cell);
            }
            return path;
        }

        /// <summary>Các ô đầu còn chạy qua trước điểm dừng kế (gồm ô hiện tại), để không rải trái lên đó.</summary>
        private HashSet<Vector2Int> PathToNextStop()
        {
            var path = new HashSet<Vector2Int> { CurrentCell };
            if (!IsMoving) return path;
            var cell = CurrentCell;
            var heading = Heading;
            if (cell != _target) { cell = _target; path.Add(cell); } // đang chạy dở sang ô kế
            while (true)
            {
                if (_board.TryTunnel(cell, heading, out var exit, out var exitHeading))
                {
                    if (!path.Add(exit)) break;
                    cell = exit;
                    heading = exitHeading;
                }
                else if (!NextStep(cell, heading, out heading)) break;
                if (!path.Add(cell + heading.ToCell())) break;
                cell += heading.ToCell();
            }
            return path;
        }

        private void Stop()
        {
            IsMoving = false;
            _sinceStop = 0f;
            if (_bufferedAge > BufferedSwipeLifetime) _bufferedSwipe = null;
            Stopped?.Invoke(CurrentCell);
            OpenPendingCurtains();
            OnStoppedForBoosters();

            EnsureShutterProgress();
            int column = _board.CrateColumnAt(CurrentCell);
            if (CanDeliverTo(column))
            {
                _deliveryColumn = column;
                _deliverFromTail = true;
                _deliveryTimer = DeliveryInterval; // trái đầu bay ngay
                _bufferedSwipe = null; // vuốt nhớ sẵn trước khi tới crate bị bỏ
                DeliveryStarted?.Invoke(column);
                return;
            }
            // Khay đang đóng: không giao, khay rung từ chối (gốc ClosedCrateRejectPunch) khi rắn có trái của nó.
            if (IsCrateClosed(column) && !Columns[column].IsComplete && _segments.Contains(Columns[column].Current.kind))
                ClosedCrateRejected?.Invoke(column);

            if (_bufferedSwipe.HasValue)
            {
                var swipe = _bufferedSwipe.Value;
                _bufferedSwipe = null;
                RequestMove(swipe);
            }
        }

        /// <summary>
        /// Chạy ngang cổng một chiều hướng ra khỏi nhánh thì rung nó báo lối đóng (gốc: AnimateNotAllowedArrowsAround);
        /// đâm thẳng vào thì không rung, chỉ rắn khựng [user].
        /// </summary>
        private void SignalClosedArrows(Vector2Int cell, Direction arrivedHeading, Direction leaving)
        {
            foreach (var direction in DirectionExtensions.All)
            {
                if (direction == arrivedHeading.Opposite() || direction == leaving) continue;
                var beside = cell + direction.ToCell();
                if (_board.HasRoad(cell, direction) && _board.OneWayGates.TryGetValue(beside, out var way) && way != direction)
                    ArrowNotAllowed?.Invoke(beside);
            }
        }

        /// <summary>
        /// Cặp mũi tên lật quay đầu khi rắn vào bằng mũi này và ra bằng mũi kia [user]; tính lúc rời ô
        /// để mũi tên dưới đầu không quay ngược với rắn.
        /// </summary>
        private void LeftCell(Vector2Int cell)
        {
            if (!_board.FlippingArrows.TryGetValue(cell, out int group)) return;
            if (_arrowEntered.TryGetValue(group, out var entered) && entered != cell)
            {
                _arrowEntered.Remove(group);
                _board.FlipPair(cell);
                ArrowsFlipped?.Invoke(cell, entered);
            }
            else _arrowEntered[group] = cell;
        }

        private void Enter(Vector2Int cell)
        {
            if (!_pills.TryGetValue(cell, out var kind)) return;
            _pills.Remove(cell);
            _eatenSinceSwipe.Add((cell, kind));
            _segments.Add(kind);
            PillEaten?.Invoke(cell, kind, _segments.Count - 1);
            CountCurtainFruit(kind);
            if (_secret.Remove(cell)) SecretRevealed?.Invoke(cell, _segments.Count - 1);
            if (_pills.Count == 0) SpawnNextBatch();
        }

        private void SpawnNextBatch()
        {
            if (BatchIndex + 1 >= _level.batches.Count) return;
            BatchIndex++;
            string layer = "spawn" + (BatchIndex + 1);
            if (!_map.HasLayer(layer))
            {
                SpawnOnMapSlots(_level.batches[BatchIndex]);
                return;
            }
            for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!_spawnIds.TryGetValue(_map.GetLocalTileId(layer, cell), out var kind)) continue;
                if (NoFruitOnBlock(cell) && FreeCellNear(cell) is Vector2Int free) cell = free; // không rải lên khối nâng đang dựng
                _pills[cell] = kind;
                if (_board.IsSecretSpawn(BatchIndex, cell)) _secret.Add(cell);
                PillSpawned?.Invoke(cell, kind);
            }
        }

        /// <summary>
        /// Màn không có layer spawn: mỗi đợt rải ngẫu nhiên lại, lấp trọn từng đoạn ô, mỗi loại một khối liền
        /// (video Level 12, [user]); bỏ các ô đầu sắp chạy qua để rắn không ăn ngay.
        /// </summary>
        private void SpawnOnMapSlots(SpawnBatchDefinition batch)
        {
            var blocked = PathToNextStop();
            var reach = ReachableWhileCurtained();

            // Đoạn dùng được: cắt tại ô bị chặn hoặc đã có trái.
            var runs = new List<List<Vector2Int>>();
            foreach (var segment in _board.PillSlotGroups)
            {
                var run = new List<Vector2Int>();
                foreach (var cell in segment)
                {
                    if (blocked.Contains(cell) || _pills.ContainsKey(cell) || _board.IsCovered(cell) || NoFruitOnBlock(cell) || (reach != null && !reach.Contains(cell)))
                    {
                        if (run.Count > 0) runs.Add(run);
                        run = new List<Vector2Int>();
                        continue;
                    }
                    run.Add(cell);
                }
                if (run.Count > 0) runs.Add(run);
            }

            // Thứ tự đoạn ngẫu nhiên, mỗi đoạn bắt đầu từ một đầu ngẫu nhiên.
            for (int i = runs.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (runs[i], runs[j]) = (runs[j], runs[i]);
            }
            foreach (var run in runs)
                if (_random.Next(2) == 1) run.Reverse();

            // Đoạn có ô ưu tiên (gốc: PillSpawnPoint) được rải trước.
            var priority = _board.PrioritySlots;
            if (priority.Count > 0)
            {
                var first = runs.FindAll(run => run.Exists(cell => ((ICollection<Vector2Int>)priority).Contains(cell)));
                runs.RemoveAll(first.Contains);
                runs.InsertRange(0, first);
            }

            // Mỗi loại trái một khối liền, không xen kẽ (không có đỏ-xanh-đỏ).
            var kinds = new List<PillKind>();
            var order = new List<PillCount>(batch.pills);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (var pill in order)
                for (int n = 0; n < pill.count; n++) kinds.Add(pill.kind);

            // Lấp trọn từng đoạn, chỉ đoạn cuối được dở dang; hết ô thì rải ô trống gần nhất.
            int next = 0;
            for (int i = 0; i < runs.Count && next < kinds.Count; i++)
                for (int k = 0; k < runs[i].Count && next < kinds.Count; k++)
                    Place(runs[i][k], kinds[next++]);
            for (; next < kinds.Count; next++)
            {
                var cell = FreeCellNear(CurrentCell);
                if (!cell.HasValue) return;
                Place(cell.Value, kinds[next]);
            }
        }

        private void Place(Vector2Int cell, PillKind kind)
        {
            _pills[cell] = kind;
            PillSpawned?.Invoke(cell, kind);
        }

        private void TickDelivery(float deltaTime)
        {
            _deliveryTimer += deltaTime;
            float interval = _boomerang ? BoomerangInterval : DeliveryInterval;
            while (_deliveryTimer >= interval)
            {
                _deliveryTimer -= interval;
                if (!DeliverOne(_deliveryColumn))
                {
                    int column = _deliveryColumn;
                    bool fromTail = _deliverFromTail && !_boomerang;
                    _deliveryColumn = -1;
                    if (_deliverFromTail) ToggleAfterDelivery(column); // rắn hay boomerang vừa giao: khay đóng / mở
                    _boomerang = false;
                    if (fromTail) DeliveryEnded?.Invoke(column);
                    if (!IsWon) StartSettlingFullCrates();
                    return;
                }
            }
        }

        private bool DeliverOne(int columnIndex)
        {
            var column = Columns[columnIndex];
            if (column.IsComplete) return false;

            // Crate được snip lấp trước chỉ hoàn thành khi lên tới trên cùng.
            if (column.IsFull(column.CurrentIndex)) return CompleteCurrent(columnIndex);
            if (!_deliverFromTail) return false;

            var kind = column.Current.kind;
            if (_boomerang)
            {
                // Boomerang: lấy đốt gần đầu nhất, bay ra từ thân (rắn không đứng ở crate).
                int thrown = _boomerangLeft > 0 ? _segments.IndexOf(kind) : -1;
                if (thrown < 0) return false;
                _boomerangLeft--;
                column.Add(column.CurrentIndex);
                SegmentSnipped?.Invoke(thrown, kind, columnIndex, column.CurrentIndex);
                _deliveredThisRun++;
                _segments.RemoveAt(thrown);
                SegmentRemoved?.Invoke(thrown);
                if (column.IsFull(column.CurrentIndex))
                {
                    // Loại kế bay sau một lần bay, không chờ đủ 1 s, để tới đúng lúc crate mới lên (video Lv7).
                    // Thiếu khoảng chờ này trái bay vào crate còn khuất bên dưới (bug1).
                    bool more = CompleteCurrent(columnIndex);
                    _deliveryTimer = BoomerangInterval - BoomerangCrateGap;
                    return more && _boomerangLeft > 0;
                }
                return _boomerangLeft > 0;
            }

            int index = _segments.LastIndexOf(kind);
            if (index < 0) return false;

            _segments.RemoveAt(index);
            column.Add(column.CurrentIndex);
            _deliveredSinceSwipe.Add(kind);
            SegmentDelivered?.Invoke(index, kind, columnIndex);
            _deliveredThisRun++;

            if (column.IsFull(column.CurrentIndex)) return CompleteCurrent(columnIndex);
            return true;
        }

        private bool CompleteCurrent(int columnIndex)
        {
            var column = Columns[columnIndex];
            int completed = column.CurrentIndex;
            column.CurrentIndex++;
            _deliveryTimer = -CrateCompletePause; // rắn vẫn bị khóa trong lúc chờ
            CrateCompleted?.Invoke(columnIndex, completed);
            WearWalls();
            LowerRisenBlocks();
            if (!AllColumnsComplete()) return true;
            IsWon = true;
            Won?.Invoke();
            return false;
        }

        // ------------------------------------------------------------------ cheat (SnackDashCheats)

        /// <summary>Cheat: thắng ngay qua <see cref="Won"/> như thường (màn thắng, xu); crate giữ nguyên.</summary>
        public bool CheatWin()
        {
            if (IsWon || IsStuck) return false;
            IsMoving = false;
            IsWon = true;
            Won?.Invoke();
            return true;
        }

        /// <summary>Cheat: cắn đuôi tại chỗ (NO WAY OUT rồi luồng thua thường); chỉ khi rắn đứng yên và có thân.</summary>
        public bool CheatBite()
        {
            if (IsWon || IsStuck || IsMoving || IsDelivering || _segments.Count == 0) return false;
            IsStuck = true;
            SnipKeep = _segments.Count / KeptPerSegments;
            TailBitten?.Invoke(SnipKeep);
            return true;
        }

        /// <summary>Mỗi crate xong trừ 1 máu mọi nhóm đá; về 0 thì đá vỡ, đường mở [user].</summary>
        private void WearWalls()
        {
            for (int group = 0; group < _wallHealth.Count; group++)
            {
                if (_wallHealth[group] <= 0) continue;
                _wallHealth[group]--;
                WallHealthChanged?.Invoke(group, _wallHealth[group]);
                if (_wallHealth[group] > 0) continue;
                _board.OpenWall(group);
                WallBroken?.Invoke(group);
            }
        }

        private bool AllColumnsComplete()
        {
            foreach (var column in Columns)
                if (!column.IsComplete) return false;
            return true;
        }

        private Vector2 SampleTrail(float distanceBehindHead) => SampleTrail(distanceBehindHead, out _);

        /// <summary>Đốt trong hầm: bị ẩn và không cắn được.</summary>
        public bool SegmentInTunnel(int index)
        {
            SampleTrail((index + 1) * SegmentSpacing, out bool inTunnel);
            return inTunnel;
        }

        /// <summary>
        /// Điểm trên đường đầu đã đi; đoạn hầm dài bằng phần thân đã vào, điểm nằm trên đó thì ở trong hầm (đặt tại miệng vào).
        /// </summary>
        private Vector2 SampleTrail(float distanceBehindHead, out bool inTunnel)
        {
            inTunnel = false;
            var previous = HeadPosition;
            for (int i = 0; i < _trail.Count; i++)
            {
                var point = _trail[i];
                float jumpLength = i > 0 ? _trailJump[i - 1] : NotTunnel;
                bool jump = jumpLength >= 0f;
                float step = jump ? jumpLength : Vector2.Distance(previous, point);
                if (distanceBehindHead <= step && (step > 0f || jump))
                {
                    if (!jump) return Vector2.Lerp(previous, point, distanceBehindHead / step);
                    inTunnel = true;
                    return point;
                }
                distanceBehindHead -= step;
                previous = point;
            }
            return previous;
        }

        private void PruneTrail()
        {
            int keep = _segments.Count + 3;
            if (_trail.Count > keep)
            {
                _trail.RemoveRange(keep, _trail.Count - keep);
                _trailJump.RemoveRange(keep, _trailJump.Count - keep);
            }
        }
    }
}
