using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Deterministic core loop of one level, independent of GameObjects.
    ///
    /// Rules (source in brackets):
    /// - A swipe starts the snake; it runs at 30 cells/s [PlayerView._moveSpeed], follows corners
    ///   automatically and stops on Stop cells (the small dark squares at junctions; blockers make
    ///   no difference) and crate stops, or when the road ends [Player, video, user].
    /// - Swipes made while moving are buffered; only the latest one is kept and applied on the next
    ///   stop [tested on the original], unless it is over 1 s old [SwipeRecorder.maxTimeRecordSwipe].
    ///   Only a fresh touch is buffered: further flicks of a finger that stayed down are ignored until
    ///   the snake stops [user, tested on the original], and for a short grace after it [play-test].
    /// - While fruit is being delivered every swipe is ignored [tested on the original]; a swipe
    ///   buffered before reaching the crate is dropped too [inference].
    /// - With a tail the snake cannot reverse into the segment right behind its head; a head-only
    ///   snake can turn back [tested on the original; MovementGatekeeper validators].
    /// - Entering a pill cell eats it; the fruit joins the END of the tail [video LV2-3].
    /// - Tail segments trail the head path one cell apart [measured on video].
    /// - When the current wave is fully eaten, the next one spawns [RespawnScheduler, video].
    /// - Standing on a crate stop delivers matching fruit from the tail end, one every 0.048 s
    ///   [CrateManager.timeBetweenPillsToCrate]; crates of a column fill in order [CrateColumn].
    /// - The head entering a cell held by its own tail (distance &lt; 0.7, tail of 5+ segments) is
    ///   "NO WAY OUT": everything stops until the player snips the tail or gives up
    ///   [TailCuttingController CollisionDistance / MinSegmentsBeforeCollision, video Man_Lose].
    /// - Snip keeps tail length / 5 (rounded down) segments counted from the head; the rest blinks
    ///   while stuck and, on continuing, flies into crates of its kind anywhere in the stacks, not
    ///   only the current crate [user, tested on the original; video Man_Lose]. Fruit counts match
    ///   the crates, so nothing is left over. The bite position plays no part.
    /// - A crate filled in advance that way is only processed (completed) once it reaches the top.
    /// - A filled crate pauses the delivery for its completion effect [observed on the original].
    /// - All crates filled = win [WinConditionChecker].
    /// </summary>
    public sealed class GameSession
    {
        public const float DefaultCellsPerSecond = 30f;
        public const float SegmentSpacing = 1f;
        public const float DeliveryInterval = 0.048f;

        /// <summary>
        /// Delivery pauses after a crate fills so its completion plays out. Measured on LV2-3 footage
        /// (30 fps, crate combo at 24-28 s): from the last fruit leaving the head to the first fruit of
        /// the next crate takes 0.8-1.2 s (full crate holds ~0.33 s, then flash/smoke, next crate).
        /// </summary>
        public const float CrateCompletePause = 1f;

        // TailCuttingController (original constants / Game.unity values).
        public const float BiteDistance = 0.7f;
        public const int MinSegmentsBeforeBite = 5;
        /// <summary>Snip keeps tail length / KeptPerSegments segments [user, observed on the original].</summary>
        public const int KeptPerSegments = 5;
        /// <summary>Fruit per crate on the current game (Box_Crate_9_VO).</summary>
        public const int CrateCapacity = 9;

        public sealed class CrateColumn
        {
            public readonly IReadOnlyList<CrateDefinition> Crates;
            private readonly int[] _filled;
            public int CurrentIndex { get; internal set; }
            /// <summary>Fruit already in the current crate.</summary>
            public int Filled => IsComplete ? 0 : _filled[CurrentIndex];
            public bool IsComplete => CurrentIndex >= Crates.Count;
            public CrateDefinition Current => Crates[CurrentIndex];

            /// <summary>Fruit in any crate of the stack (crates below the top can be filled by a snip).</summary>
            public int FilledIn(int crateIndex) => _filled[crateIndex];
            internal void Add(int crateIndex) => _filled[crateIndex]++;
            internal bool IsFull(int crateIndex) => _filled[crateIndex] >= Crates[crateIndex].requiredCount;

            internal CrateColumn(IReadOnlyList<CrateDefinition> crates)
            {
                Crates = SplitIntoCratesOf9(crates);
                _filled = new int[Crates.Count];
            }

            /// <summary>
            /// v1.14.1 data still has 18/27 crates (old Box_Crate_18/27 visuals); v1.15 keeps only the
            /// 9-fruit VO crate (CrateVisualConfig_Box_VO.CratePrefab9) and on the current game every
            /// crate holds 9 [user], so a bigger crate becomes consecutive 9-crates of its kind.
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
        public event Action<Vector2Int, PillKind, int> PillEaten;           // cell, kind, new segment index
        public event Action<int, PillKind, int> SegmentDelivered;           // removed segment index, kind, column
        public event Action<int, int> CrateCompleted;                      // column, crate index
        public event Action<Direction> MoveStarted;
        public event Action<Vector2Int> Stopped;
        public event Action<int> DeliveryStarted;                          // column
        public event Action<int> DeliveryEnded;                            // column
        public event Action<int> TailBitten;                               // first segment index a snip removes
        public event Action<int, PillKind, int, int> SegmentSnipped;       // segment index, kind, column, crate index (before removal)
        public event Action<int> TailSnipped;                              // segments kept (all from that index on removed)
        public event Action Won;

        private readonly BoardGraph _board;
        private readonly TiledMap _map;
        private readonly LevelDefinition _level;
        private readonly Dictionary<int, PillKind> _spawnIds;
        private readonly Dictionary<Vector2Int, PillKind> _pills = new Dictionary<Vector2Int, PillKind>();
        private readonly List<PillKind> _segments = new List<PillKind>();
        private readonly List<Vector2> _trail = new List<Vector2>(); // cell centres passed, newest first
        private readonly float _speed;

        /// <summary>A buffered swipe expires after this long (SwipeRecorder.maxTimeRecordSwipe, Game.unity).</summary>
        public const float BufferedSwipeLifetime = 1f;

        /// <summary>
        /// Seconds after a stop during which a held finger cannot start a new move: with stops close
        /// together the slightest drag otherwise sends the snake on at once [play-test choice; the
        /// original has no such field, its touch threshold is just larger on a phone].
        /// </summary>
        public float HeldSwipeGraceAfterStop { get; set; } = 0.15f;

        private Vector2Int _target;
        private Direction? _bufferedSwipe;
        private float _bufferedAge;
        private float _sinceStop = float.MaxValue;
        private int _deliveryColumn = -1;
        private float _deliveryTimer;
        private bool _deliverFromTail; // false: only completing crates a snip filled (snake not at the crate)

        public Vector2 HeadPosition { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public Direction Heading { get; private set; } = Direction.Up;
        public bool IsMoving { get; private set; }
        public bool IsDelivering => _deliveryColumn >= 0;
        /// <summary>The head ran into its own tail; waiting for Snip (or the player giving up).</summary>
        public bool IsStuck { get; private set; }
        /// <summary>Segments a snip removes while stuck: all from index <see cref="SnipKeep"/> on.</summary>
        public int SnipCount => IsStuck ? _segments.Count - SnipKeep : 0;
        /// <summary>Segments a snip keeps, counted from the head.</summary>
        public int SnipKeep { get; private set; }
        public bool IsWon { get; private set; }
        public int BatchIndex { get; private set; } = -1;
        public IReadOnlyList<PillKind> Segments => _segments;
        public IReadOnlyDictionary<Vector2Int, PillKind> Pills => _pills;
        public IReadOnlyList<CrateColumn> Columns { get; }
        public BoardGraph Board => _board;

        public GameSession(BoardGraph board, TiledMap map, LevelDefinition level)
        {
            _board = board;
            _map = map;
            _level = level;
            _spawnIds = SpawnIdMap.Build(level.batches);
            _speed = level.speedOverride > 0f ? level.speedOverride : DefaultCellsPerSecond;

            var columns = new List<CrateColumn>();
            foreach (var column in level.columns) columns.Add(new CrateColumn(column.crates));
            Columns = columns;

            CurrentCell = board.PlayerStart;
            HeadPosition = CurrentCell;
            _trail.Add(CurrentCell);
        }

        /// <summary>Spawns the first wave. Separate from the constructor so views can subscribe first.</summary>
        public void Begin()
        {
            if (BatchIndex < 0) SpawnNextBatch();
        }

        /// <param name="fingerHeld">
        /// True for a further flick of a finger that stayed down: while moving it is ignored, so a
        /// held finger only steers again once the snake has stopped [user, tested on the original],
        /// and not during <see cref="HeldSwipeGraceAfterStop"/> right after a stop [play-test choice].
        /// </param>
        public bool RequestMove(Direction direction, bool fingerHeld = false)
        {
            if (IsWon || IsStuck || IsDelivering) return false;
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
            if (!_board.CanMove(CurrentCell, direction) || BlockedByTail(direction)) return false;

            Heading = direction;
            _target = CurrentCell + direction.ToCell();
            IsMoving = true;
            MoveStarted?.Invoke(direction);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (IsWon || IsStuck) return;
            _bufferedAge += deltaTime;
            if (!IsMoving) _sinceStop += deltaTime;
            if (IsMoving) Advance(_speed * deltaTime);
            else if (_deliveryColumn >= 0) TickDelivery(deltaTime);
        }

        /// <summary>World-plane position of tail segment <paramref name="index"/> (0 = right behind the head).</summary>
        public Vector2 SegmentPosition(int index) => SampleTrail((index + 1) * SegmentSpacing);

        /// <summary>Point on the head's recent path, <paramref name="distance"/> cells behind the head.</summary>
        public Vector2 PathPoint(float distance) => SampleTrail(distance);

        /// <summary>True when the snake carries fruit the column's current crate accepts.</summary>
        public bool CanDeliverTo(int column)
        {
            if (column < 0 || column >= Columns.Count || Columns[column].IsComplete) return false;
            return _segments.Contains(Columns[column].Current.kind);
        }

        /// <summary>
        /// Keeps the first <see cref="SnipKeep"/> segments and sends every other one into a crate of
        /// its kind (the current crate or one further down a stack). Crates a snip fills are completed
        /// once they reach the top. Price checks (coins) are the caller's job, see SnipOffer.
        /// </summary>
        public bool Snip()
        {
            if (!IsStuck) return false;
            int keep = SnipKeep;
            int removed = _segments.Count - keep;

            // From the tail end, so the indices handed to the views stay valid while removing.
            var leftovers = new List<(Vector2Int cell, PillKind kind)>();
            for (int i = _segments.Count - 1; i >= keep; i--)
            {
                var kind = _segments[i];
                if (TryReserveCrate(kind, out int column, out int crate))
                    SegmentSnipped?.Invoke(i, kind, column, crate);
                else
                    leftovers.Add((Vector2Int.RoundToInt(SegmentPosition(i)), kind)); // data mismatch only
                _segments.RemoveAt(i);
            }

            IsStuck = false;
            SnipKeep = 0;
            TailSnipped?.Invoke(keep);

            // Should not happen (fruit counts match the crates); keep such fruit in play anyway.
            foreach (var (near, kind) in leftovers)
            {
                var cell = FreeCellNear(near);
                if (!cell.HasValue) continue;
                _pills[cell.Value] = kind;
                PillSpawned?.Invoke(cell.Value, kind);
            }

            StartSettlingFullCrates(removed * DeliveryInterval);
            return true;
        }

        /// <summary>First crate of this kind with room, columns in order, each from its current crate down.</summary>
        private bool TryReserveCrate(PillKind kind, out int column, out int crate)
        {
            for (column = 0; column < Columns.Count; column++)
            {
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

        /// <summary>A snip can fill a column's current crate: complete it like a delivery would.</summary>
        private void StartSettlingFullCrates(float delay = 0f)
        {
            for (int column = 0; column < Columns.Count; column++)
            {
                var stack = Columns[column];
                if (stack.IsComplete || !stack.IsFull(stack.CurrentIndex)) continue;
                _deliveryColumn = column;
                _deliverFromTail = false;
                _deliveryTimer = -delay; // let the snipped fruit stream in first
                return;
            }
        }

        /// <summary>Nearest walkable cell (breadth-first along the roads) without pill, head or tail.</summary>
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
                if (_board.IsWalkable(cell) && !occupied.Contains(cell) && !_pills.ContainsKey(cell)) return cell;
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = cell + direction.ToCell();
                    if (_board.IsWalkable(next) && seen.Add(next)) queue.Enqueue(next);
                }
            }
            return null;
        }

        private bool CheckBite()
        {
            if (_segments.Count < MinSegmentsBeforeBite) return false;
            for (int i = 1; i < _segments.Count; i++)
            {
                if (Vector2.Distance(SegmentPosition(i), HeadPosition) >= BiteDistance) continue;
                IsStuck = true;
                SnipKeep = _segments.Count / KeptPerSegments;
                TailBitten?.Invoke(SnipKeep);
                return true;
            }
            return false;
        }

        private bool BlockedByTail(Direction direction)
        {
            if (_segments.Count == 0) return false;
            var behindHead = Vector2Int.RoundToInt(SegmentPosition(0));
            return behindHead == CurrentCell + direction.ToCell();
        }

        private void Advance(float distance)
        {
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
                PruneTrail();
                if (CheckBite())
                {
                    IsMoving = false;
                    _bufferedSwipe = null;
                    return;
                }
                Enter(CurrentCell);
                if (IsWon || !ContinueFrom(CurrentCell))
                {
                    Stop();
                    return;
                }
            }
        }

        private bool ContinueFrom(Vector2Int cell)
        {
            if (_board.IsStop(cell)) return false;
            if (_board.CanMove(cell, Heading))
            {
                _target = cell + Heading.ToCell();
                return true;
            }

            // Corner: the only way on that is not straight back.
            Direction? turn = null;
            foreach (var exit in _board.Exits(cell))
            {
                if (exit == Heading.Opposite()) continue;
                if (turn.HasValue) return false;
                turn = exit;
            }
            if (!turn.HasValue) return false;
            Heading = turn.Value;
            _target = cell + Heading.ToCell();
            return true;
        }

        private void Stop()
        {
            IsMoving = false;
            _sinceStop = 0f;
            if (_bufferedAge > BufferedSwipeLifetime) _bufferedSwipe = null;
            Stopped?.Invoke(CurrentCell);

            int column = _board.CrateColumnAt(CurrentCell);
            if (CanDeliverTo(column))
            {
                _deliveryColumn = column;
                _deliverFromTail = true;
                _deliveryTimer = DeliveryInterval; // first fruit leaves immediately
                _bufferedSwipe = null;
                DeliveryStarted?.Invoke(column);
                return;
            }

            if (_bufferedSwipe.HasValue)
            {
                var swipe = _bufferedSwipe.Value;
                _bufferedSwipe = null;
                RequestMove(swipe);
            }
        }

        private void Enter(Vector2Int cell)
        {
            if (!_pills.TryGetValue(cell, out var kind)) return;
            _pills.Remove(cell);
            _segments.Add(kind);
            PillEaten?.Invoke(cell, kind, _segments.Count - 1);
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
                _pills[cell] = kind;
                PillSpawned?.Invoke(cell, kind);
            }
        }

        /// <summary>
        /// Levels without fixed spawn layers: the batch's fruit, kind by kind, laid along the slot
        /// groups in path order, skipping cells under the snake (RespawnScheduler blockedCells).
        /// Reconstruction from the original's names and data; the exact slot choice is unverified.
        /// When the slots run out, the rest goes to the nearest free road cell.
        /// </summary>
        private void SpawnOnMapSlots(SpawnBatchDefinition batch)
        {
            var blocked = new HashSet<Vector2Int> { CurrentCell };
            for (int i = 0; i < _segments.Count; i++) blocked.Add(Vector2Int.RoundToInt(SegmentPosition(i)));

            var free = new List<Vector2Int>();
            foreach (var group in _board.PillSlotGroups)
            foreach (var cell in group)
                if (!blocked.Contains(cell) && !_pills.ContainsKey(cell)) free.Add(cell);

            int next = 0;
            foreach (var pill in batch.pills)
            for (int n = 0; n < pill.count; n++)
            {
                Vector2Int? cell = next < free.Count ? free[next++] : FreeCellNear(CurrentCell);
                if (!cell.HasValue) return;
                _pills[cell.Value] = pill.kind;
                PillSpawned?.Invoke(cell.Value, pill.kind);
            }
        }

        private void TickDelivery(float deltaTime)
        {
            _deliveryTimer += deltaTime;
            while (_deliveryTimer >= DeliveryInterval)
            {
                _deliveryTimer -= DeliveryInterval;
                if (!DeliverOne(_deliveryColumn))
                {
                    int column = _deliveryColumn;
                    bool fromTail = _deliverFromTail;
                    _deliveryColumn = -1;
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

            // A crate a snip filled in advance completes as soon as it reaches the top.
            if (column.IsFull(column.CurrentIndex)) return CompleteCurrent(columnIndex);
            if (!_deliverFromTail) return false;

            var kind = column.Current.kind;
            int index = _segments.LastIndexOf(kind);
            if (index < 0) return false;

            _segments.RemoveAt(index);
            column.Add(column.CurrentIndex);
            SegmentDelivered?.Invoke(index, kind, columnIndex);

            if (column.IsFull(column.CurrentIndex)) return CompleteCurrent(columnIndex);
            return true;
        }

        private bool CompleteCurrent(int columnIndex)
        {
            var column = Columns[columnIndex];
            int completed = column.CurrentIndex;
            column.CurrentIndex++;
            _deliveryTimer = -CrateCompletePause; // the snake stays locked during the pause
            CrateCompleted?.Invoke(columnIndex, completed);
            if (!AllColumnsComplete()) return true;
            IsWon = true;
            Won?.Invoke();
            return false;
        }

        private bool AllColumnsComplete()
        {
            foreach (var column in Columns)
                if (!column.IsComplete) return false;
            return true;
        }

        private Vector2 SampleTrail(float distanceBehindHead)
        {
            var previous = HeadPosition;
            foreach (var point in _trail)
            {
                float step = Vector2.Distance(previous, point);
                if (distanceBehindHead <= step && step > 0f)
                    return Vector2.Lerp(previous, point, distanceBehindHead / step);
                distanceBehindHead -= step;
                previous = point;
            }
            return previous;
        }

        private void PruneTrail()
        {
            int keep = _segments.Count + 3;
            if (_trail.Count > keep) _trail.RemoveRange(keep, _trail.Count - keep);
        }
    }
}
