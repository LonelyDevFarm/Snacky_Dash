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
    /// - A swipe starts the snake; it runs at 26 cells/s (30 in the original [PlayerView._moveSpeed], slowed [user]), follows corners
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
    public sealed partial class GameSession
    {
        public const float DefaultCellsPerSecond = 26f; // the original runs 30 [PlayerView._moveSpeed]; slowed to 26 [user]
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
            private List<CrateDefinition> _crates;
            private List<int> _filled;
            public IReadOnlyList<CrateDefinition> Crates => _crates;
            /// <summary>The extra crate column: locked (empty, a wooden floor) until bought [ExtraCrateController].</summary>
            public bool IsExtra { get; }
            public int CurrentIndex { get; internal set; }
            /// <summary>Fruit already in the current crate.</summary>
            public int Filled => IsComplete ? 0 : _filled[CurrentIndex];
            public bool IsComplete => CurrentIndex >= Crates.Count;
            public CrateDefinition Current => Crates[CurrentIndex];

            /// <summary>Fruit in any crate of the stack (crates below the top can be filled by a snip).</summary>
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

            /// <summary>The crates still to fill (the current one and those under it), with the fruit already in them.</summary>
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
        public event Action<Direction> MoveBlocked;                        // swipe into a wall or the tail
        public event Action<int> DeliveryStarted;                          // column
        public event Action<int> DeliveryEnded;                            // column
        public event Action<int> TailBitten;                               // first segment index a snip removes
        public event Action<int, PillKind, int, int> SegmentSnipped;       // segment index, kind, column, crate index (before removal)
        public event Action<int> TailSnipped;                              // segments kept (all from that index on removed)
        public event Action<int, IReadOnlyList<int>> BoomerangThrown;      // column, segment indices that will fly
        public event Action<int> SegmentRemoved;                           // segment index taken out of the middle (after SegmentSnipped)
        public event Action<Vector2Int, Vector2Int> ArrowsFlipped;          // the two arrows of a pair, after turning round
        public event Action<Vector2Int> ArrowNotAllowed;                    // a gate or arrow beside the head's way that it may not enter
        public event Action<int, int> WallHealthChanged;                    // rock group, health left (after a crate completed)
        public event Action<int> WallBroken;                                // rock group gone, its road open
        public event Action<Vector2Int, int> SecretRevealed;                // cell, segment index: a secret fruit was eaten
        public event Action Won;

        private readonly BoardGraph _board;
        private readonly TiledMap _map;
        private readonly LevelDefinition _level;
        private readonly Dictionary<int, PillKind> _spawnIds;
        private readonly Dictionary<Vector2Int, PillKind> _pills = new Dictionary<Vector2Int, PillKind>();
        private readonly List<PillKind> _segments = new List<PillKind>();
        private readonly List<Vector2> _trail = new List<Vector2>(); // cell centres passed, newest first
        // [i] >= 0: from _trail[i] back to _trail[i+1] goes through a tunnel that long (along the body);
        // NotTunnel for plain road. A tunnel just entered is 0 long, so never use 0 as "no tunnel"
        // (that drew the body on the straight line between the two mouths for a frame).
        private const float NotTunnel = -1f;
        private readonly List<float> _trailJump = new List<float>();
        private readonly Dictionary<int, Vector2Int> _arrowEntered = new Dictionary<int, Vector2Int>(); // flip group -> arrow the head went in by
        private readonly List<int> _wallHealth = new List<int>();
        private readonly HashSet<Vector2Int> _secret = new HashSet<Vector2Int>();

        /// <summary>Crates still to complete before each rock group breaks (BoardGraph.WallGroups order).</summary>
        public IReadOnlyList<int> WallHealth => _wallHealth;

        /// <summary>A fruit on the board whose kind stays hidden until it is eaten.</summary>
        public bool IsSecret(Vector2Int cell) => _secret.Contains(cell);
        private float _tunnelLeft; // > 0: body still sliding into a tunnel (distance, at tunnel speed)
        private float _tunnelWait; // > 0: whole snake inside, seconds until the head comes out

        /// <summary>
        /// Lv14 footage (15 fps steps), 16-segment snake: from the head reaching the mouth the body
        /// is in after ~0.3 s (about twice the normal speed) [user: it speeds up into the tunnel],
        /// then nothing shows for ~0.4 s before the head comes out at normal speed.
        /// </summary>
        public const float TunnelSpeedMultiplier = 2f;
        public const float TunnelInsideTime = 0.4f;

        /// <summary>The head is inside a tunnel (hidden) until the whole body has gone in and come through [user].</summary>
        public bool HeadInTunnel => _tunnelLeft > 0f || _tunnelWait > 0f;
        private readonly float _speed;

        /// <summary>A buffered swipe expires after this long (SwipeRecorder.maxTimeRecordSwipe, Game.unity).</summary>
        public const float BufferedSwipeLifetime = 1f;

        /// <summary>
        /// Seconds after a stop during which a held finger cannot start a new move: with stops close
        /// together the slightest drag otherwise sends the snake on at once [play-test choice; the
        /// original has no such field, its touch threshold is just larger on a phone].
        /// </summary>
        public float HeldSwipeGraceAfterStop { get; set; } = 0.15f;

        /// <summary>Random fruit layout of levels without fixed spawn layers; set a seed for repeatable tests.</summary>
        private System.Random _random = new System.Random();
        public void SetSeed(int seed) => _random = new System.Random(seed);

        private Vector2Int _target;
        private Direction? _bufferedSwipe;
        private float _bufferedAge;
        private float _sinceStop = float.MaxValue;
        private int _deliveryColumn = -1;
        private float _deliveryTimer;
        private bool _deliverFromTail; // false: only completing crates a snip filled (snake not at the crate)
        private bool _boomerang;       // the delivery is a boomerang throw (fruit fly from the body)
        private int _boomerangLeft;    // planned fruit still to throw

        /// <summary>Lv7 video stepped at 15 fps: one boomerang fruit takes off per frame (~0.067 s), </summary>
        public const float BoomerangInterval = 0.1f; // 0.07 from the video felt too fast on the phone [user 08/10: slower by a few tenths]

        /// <summary>Wait between the last fruit of a full crate and the first of the next (about one flight, CratesView boomerangFlightDuration 0.55 s in Game.unity).</summary>
        public const float BoomerangCrateGap = 0.65f;

        public Vector2 HeadPosition { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        /// <summary>Cell the head is running into (equals <see cref="CurrentCell"/> once it arrives).</summary>
        public Vector2Int TargetCell => _target;
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

        /// <summary>The extra crate column (-1 when the level has none); locked until bought (TAP TO UNLOCK, 700 coins).</summary>
        public int ExtraColumn { get; }
        public bool ExtraLocked { get; private set; }
        public event Action<int> ExtraColumnUnlocked; // the extra column

        /// <summary>Something to share out: the snake is idle and some column still has crates waiting under its top one.</summary>
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
        /// Opens the extra column and deals the crates still to fill out again [ExtraCrateReshuffleService; the exact rule is
        /// not in the files: inferred from the user's Level 30 screenshots, which it matches]. All of them, the ones on top
        /// included, in the order they would come up (one level down across the columns, then the next), go one by one
        /// to the new column first, then the open columns in turn (Level 30: the new column yellow, orange, red; the old one
        /// yellow, red, green). Fruit already in a crate goes with it. Undo is cleared (the stacks no longer match).
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

            // Rock rows: the level health, or the row's own when one of its cells is listed.
            foreach (var group in board.WallGroups)
            {
                int health = level.wallHealth;
                foreach (var own in level.wallGroupHealths)
                    if (System.Linq.Enumerable.Contains(group, own.seedCell)) health = own.health;
                _wallHealth.Add(Mathf.Max(1, health));
            }

            CurrentCell = board.PlayerStart;
            HeadPosition = CurrentCell;
            _trail.Add(CurrentCell);
            _trailJump.Add(NotTunnel);
        }

        /// <summary>Spawns the first wave. Separate from the constructor so views can subscribe first.</summary>
        public void Begin()
        {
            if (BatchIndex < 0) SpawnNextBatch();
        }

        /// <summary>Tutorial: while set, only this swipe is taken (others do nothing); cleared once it starts a move.</summary>
        public Direction? ForcedSwipe { get; set; }

        /// <summary>Tutorial message on screen: swipes are ignored until it is tapped away.</summary>
        public bool InputBlocked { get; set; }

        private float _lockLeft;

        /// <summary>
        /// An effect is playing (the snake dropping in, fruit jumping onto the road, a crate being swapped): swipes and
        /// boosters wait for it, so a swipe can no longer cut it short [user]. Timed rather than released by the effect,
        /// so an effect that is destroyed half-way cannot leave the input locked.
        /// </summary>
        public void LockInput(float seconds) => _lockLeft = Mathf.Max(_lockLeft, seconds);

        public bool InputLocked => _lockLeft > 0f;

        /// <summary>
        /// A forced booster tutorial is up: swipes are ignored (not even buffered) while boosters still work, until
        /// the player has used the booster [user: a fast swipe on Level 4 moved the snake past the shovel tutorial].
        /// </summary>
        public bool SwipesHeld { get; set; }

        /// <param name="fingerHeld">
        /// True for a further flick of a finger that stayed down: while moving it is ignored, so a
        /// held finger only steers again once the snake has stopped [user, tested on the original],
        /// and not during <see cref="HeldSwipeGraceAfterStop"/> right after a stop [play-test choice].
        /// </param>
        public bool RequestMove(Direction direction, bool fingerHeld = false)
        {
            if (IsWon || IsStuck || IsDelivering || IsRewinding) return false; // no input until an undo has run back [user]
            if (InputBlocked) return false; // a tutorial message is up
            if (_lockLeft > 0f) return false; // an effect is still playing: not even buffered
            if (SwipesHeld) return false; // a forced booster tutorial waits for the booster
            if (ForcedSwipe.HasValue && direction != ForcedSwipe.Value) return false; // Level 1 tutorial: only the shown way [user]
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
        }

        /// <summary>World-plane position of tail segment <paramref name="index"/> (0 = right behind the head).</summary>
        public Vector2 SegmentPosition(int index) => SampleTrail((index + 1) * SegmentSpacing);

        /// <summary>Point on the head's recent path, <paramref name="distance"/> cells behind the head.</summary>
        public Vector2 PathPoint(float distance) => SampleTrail(distance);

        /// <summary>True when the point <paramref name="distance"/> cells behind the head is inside a tunnel.</summary>
        public bool PathInTunnel(float distance)
        {
            SampleTrail(distance, out bool inTunnel);
            return inTunnel;
        }

        /// <summary>Fruit spawned by waves 0..<paramref name="lastWave"/> together.</summary>
        public int FruitInWaves(int lastWave)
        {
            int total = 0;
            for (int b = 0; b <= lastWave && b < _level.batches.Count; b++)
                foreach (var pill in _level.batches[b].pills) total += pill.count;
            return total;
        }

        /// <summary>Fruit of every wave of the level.</summary>
        public int TotalFruit => FruitInWaves(_level.batches.Count - 1);

        /// <summary>Fruit eaten so far: everything spawned minus what still lies on the board.</summary>
        public int FruitEaten => BatchIndex < 0 ? 0 : FruitInWaves(BatchIndex) - _pills.Count;

        public int WaveCount => _level.batches.Count;

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
            ForgetUndo(); // like a booster: undo waits for the next swipe, it never brings snipped fruit back [user]
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

        /// <summary>
        /// Boomerang booster (BoomerangDeliveryPlanner): fruit fly from anywhere in the snake into the
        /// chosen column, crate by crate: the current crate takes its kind (segments nearest the head
        /// first) and the next crate is only planned once that one would be full [Lv7 video].
        /// Returns the planned (segment index, crate index) pairs, indices as they are now.
        /// </summary>
        public List<(int segment, int crate)> PlanBoomerang(int columnIndex)
        {
            var plan = new List<(int segment, int crate)>();
            if (IsWon || IsStuck || IsMoving || IsDelivering) return plan;
            if (columnIndex < 0 || columnIndex >= Columns.Count) return plan;
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
                if (room > 0) break; // this crate stays part filled: nothing further down is reached
            }
            return plan;
        }

        public bool CanBoomerang(int columnIndex) => PlanBoomerang(columnIndex).Count > 0;

        /// <summary>
        /// Throws the boomerang at a column (see <see cref="PlanBoomerang"/>). Stock is the caller's job.
        /// Runs like a delivery from afar [Lv7 video]: one fruit every <see cref="BoomerangInterval"/>,
        /// the current crate's kind nearest the head first; after a full crate the next kind waits
        /// <see cref="BoomerangCrateGap"/>. <see cref="BoomerangThrown"/> lists the segments that will go.
        /// </summary>
        public bool UseBoomerang(int columnIndex)
        {
            var plan = PlanBoomerang(columnIndex);
            if (plan.Count == 0) return false;
            var segments = new List<int>();
            foreach (var entry in plan) segments.Add(entry.segment);
            ForgetUndo(); // a booster was used: undo waits for the next swipe [user]
            BoomerangThrown?.Invoke(columnIndex, segments);
            _deliveryColumn = columnIndex;
            _deliverFromTail = true;
            _boomerang = true;
            _boomerangLeft = plan.Count;
            _deliveryTimer = BoomerangInterval; // the first fruit leaves at once
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
                if (SegmentInTunnel(i)) continue; // out of reach inside a tunnel
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
        /// At a bridge the deck and the road under it never touch [user]: a segment crossing the bridge
        /// on the other axis than the head is on the other level.
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

        /// <summary>A swipe that way would set the head going now (road open, not straight into its own tail).</summary>
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
                _trailJump[0] += used; // the tunnel link the head is in is always the newest
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
                if (CheckBite())
                {
                    IsMoving = false;
                    _bufferedSwipe = null;
                    return;
                }
                Enter(CurrentCell);
                if (_stopOnArrival) // came up from a shovel hole: waits there
                {
                    _stopOnArrival = false;
                    Stop();
                    return;
                }
                var arrivedHeading = Heading;
                if (IsWon || !ContinueFrom(CurrentCell))
                {
                    // Arriving at a stop shakes the closed gates and arrows around it too, every time [user].
                    if (!IsWon && _board.IsStop(CurrentCell)) SignalClosedArrows(CurrentCell, arrivedHeading, arrivedHeading.Opposite()); // not when it just ran into one
                    Stop();
                    return;
                }
                if (HeadInTunnel) return; // the tunnel trip starts next tick
                SignalClosedArrows(CurrentCell, arrivedHeading, Heading);

            }
        }

        private bool ContinueFrom(Vector2Int cell)
        {
            // Into a tunnel mouth [user]: the tunnel holds the whole snake. The head travels inside
            // (hidden) while the body slides in after it, and comes out of the other mouth once the
            // tail is in, onto that mouth's road; the body then comes out behind it, never back at
            // the first mouth.
            if (_board.TryTunnel(cell, Heading, out var exit, out var exitHeading))
            {
                CurrentCell = exit;
                HeadPosition = exit;
                _trail.Insert(0, exit);
                _swipePath.Add(exit);
                _swipeUnderground.Add(true); // from the mouth it went into to this one, inside the tube
                _trailJump.Insert(0, 0f); // grows as the head goes through (Advance)
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
        /// Where a snake running with <paramref name="heading"/> goes from <paramref name="cell"/>:
        /// straight on, or round a corner (the only way on that is not straight back). False at a
        /// stop, a dead end or a junction.
        /// </summary>
        private bool NextStep(Vector2Int cell, Direction heading, out Direction next)
        {
            next = heading;
            if (_board.IsStop(cell)) return false; // a stop square always stops, whatever arrows, gates or rocks close [user]
            if (_board.CanMove(cell, heading)) return true;
            if (_board.IsBridge(cell)) return false; // straight through only (never happens on real maps)

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
        /// The cells a swipe <paramref name="direction"/> would run over from where the head stands, up to the
        /// next stop (tutorial path highlight, TutorialPathOverlayService). Tunnels are not followed.
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

        /// <summary>Cells the head still runs over before its next stop (the current cell included).</summary>
        private HashSet<Vector2Int> PathToNextStop()
        {
            var path = new HashSet<Vector2Int> { CurrentCell };
            if (!IsMoving) return path;
            var cell = CurrentCell;
            var heading = Heading;
            if (cell != _target) { cell = _target; path.Add(cell); } // already on its way to the next cell
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
            OnStoppedForBoosters();

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

        /// <summary>
        /// The head has just left <paramref name="cell"/>. A flipping arrow pair turns round once the head
        /// has gone in by one arrow and out by the other [user]; a stop in between changes nothing.
        /// Counted on leaving the cell, so the arrow under the head never turns against it.
        /// </summary>
        /// <summary>
        /// Running past a gate or arrow that points out of its branch shakes it, to show that way is
        /// closed (ArrowGroupController.AnimateNotAllowedArrowsAround). Driving straight into one does
        /// not (only the snake bumps) [user].
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
                _pills[cell] = kind;
                if (_board.IsSecretSpawn(BatchIndex, cell)) _secret.Add(cell);
                PillSpawned?.Invoke(cell, kind);
            }
        }

        /// <summary>
        /// Levels without fixed spawn layers, as observed on the original [user, videos]:
        /// - the level has a fixed set of slots split into segments (BoardGraph.PillSlotGroups);
        /// - every wave picks again at random, even the first one, so each play differs;
        /// - segments holding priority slots (PillSpawnPoint) come first;
        /// - segments are taken whole, in random order, each from a random end; only the last one may
        ///   stop part way, the others stay empty (Level 12 footage: every run fills its whole segment);
        /// - within a segment each kind is one block and never comes back (no red-blue-red), so long
        ///   runs of one kind are common: the kinds are shuffled and laid out as one block each.
        /// Cells the head will still run over before its next stop are left out, or the snake would
        /// eat the new fruit straight away. Fruit may land under the body (it waits beside it).
        /// If the slots run out, the rest goes to the nearest free road cell.
        /// </summary>
        private void SpawnOnMapSlots(SpawnBatchDefinition batch)
        {
            var blocked = PathToNextStop();

            // Usable runs: segments cut at blocked or occupied cells.
            var runs = new List<List<Vector2Int>>();
            foreach (var segment in _board.PillSlotGroups)
            {
                var run = new List<Vector2Int>();
                foreach (var cell in segment)
                {
                    if (blocked.Contains(cell) || _pills.ContainsKey(cell))
                    {
                        if (run.Count > 0) runs.Add(run);
                        run = new List<Vector2Int>();
                        continue;
                    }
                    run.Add(cell);
                }
                if (run.Count > 0) runs.Add(run);
            }

            // Random order, each run from a random end.
            for (int i = runs.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (runs[i], runs[j]) = (runs[j], runs[i]);
            }
            foreach (var run in runs)
                if (_random.Next(2) == 1) run.Reverse();

            // Runs holding priority slots (PillSpawnPoint) go first, still in random order.
            var priority = _board.PrioritySlots;
            if (priority.Count > 0)
            {
                var first = runs.FindAll(run => run.Exists(cell => ((ICollection<Vector2Int>)priority).Contains(cell)));
                runs.RemoveAll(first.Contains);
                runs.InsertRange(0, first);
            }

            // Kinds in random order, one block each.
            var kinds = new List<PillKind>();
            var order = new List<PillCount>(batch.pills);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (var pill in order)
                for (int n = 0; n < pill.count; n++) kinds.Add(pill.kind);

            // Whole runs in turn; only the last one may stop part way.
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

            // A crate a snip filled in advance completes as soon as it reaches the top.
            if (column.IsFull(column.CurrentIndex)) return CompleteCurrent(columnIndex);
            if (!_deliverFromTail) return false;

            var kind = column.Current.kind;
            if (_boomerang)
            {
                // Boomerang: from the head side, flying out of the body (no snake at the stop).
                int thrown = _boomerangLeft > 0 ? _segments.IndexOf(kind) : -1;
                if (thrown < 0) return false;
                _boomerangLeft--;
                column.Add(column.CurrentIndex);
                SegmentSnipped?.Invoke(thrown, kind, columnIndex, column.CurrentIndex);
                _segments.RemoveAt(thrown);
                SegmentRemoved?.Invoke(thrown);
                if (column.IsFull(column.CurrentIndex))
                {
                    // Lv7 video: the next kind lifts off as the full crate's last fruit lands (one
                    // flight), not after the whole 1 s crate pause, so it arrives as the new crate
                    // comes up. Without the gap it flew into the crate still hidden underneath (bug1).
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
            WearWalls();
            if (!AllColumnsComplete()) return true;
            IsWon = true;
            Won?.Invoke();
            return false;
        }

        /// <summary>Every completed crate takes one off every rock row; at 0 it breaks and its road opens [user].</summary>
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

        /// <summary>True while tail segment <paramref name="index"/> is inside a tunnel (hidden, cannot be bitten).</summary>
        public bool SegmentInTunnel(int index)
        {
            SampleTrail((index + 1) * SegmentSpacing, out bool inTunnel);
            return inTunnel;
        }

        /// <summary>
        /// Point on the head's path; a tunnel link counts as long as the stretch of body that went in,
        /// and anything on it is inside the tunnel (placed at the mouth it went into).
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
