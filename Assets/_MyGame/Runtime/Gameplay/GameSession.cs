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
    ///   automatically and stops on Stop / crate-stop cells or when the road ends [Player, video].
    /// - Swipes made while moving are buffered; only the latest one is kept and applied on the next
    ///   stop [tested on the original].
    /// - While fruit is being delivered every swipe is ignored [tested on the original]; a swipe
    ///   buffered before reaching the crate is dropped too [inference].
    /// - With a tail the snake cannot reverse into the segment right behind its head; a head-only
    ///   snake can turn back [tested on the original; MovementGatekeeper validators].
    /// - Entering a pill cell eats it; the fruit joins the END of the tail [video LV2-3].
    /// - Tail segments trail the head path one cell apart [measured on video].
    /// - When the current wave is fully eaten, the next one spawns [RespawnScheduler, video].
    /// - Standing on a crate stop delivers matching fruit from the tail end, one every 0.048 s
    ///   [CrateManager.timeBetweenPillsToCrate]; crates of a column fill in order [CrateColumn].
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

        public sealed class CrateColumn
        {
            public readonly IReadOnlyList<CrateDefinition> Crates;
            public int CurrentIndex { get; internal set; }
            public int Filled { get; internal set; }
            public bool IsComplete => CurrentIndex >= Crates.Count;
            public CrateDefinition Current => Crates[CurrentIndex];

            internal CrateColumn(IReadOnlyList<CrateDefinition> crates) => Crates = crates;
        }

        public event Action<Vector2Int, PillKind> PillSpawned;
        public event Action<Vector2Int, PillKind, int> PillEaten;           // cell, kind, new segment index
        public event Action<int, PillKind, int> SegmentDelivered;           // removed segment index, kind, column
        public event Action<int, int> CrateCompleted;                      // column, crate index
        public event Action<Direction> MoveStarted;
        public event Action<Vector2Int> Stopped;
        public event Action<int> DeliveryStarted;                          // column
        public event Action<int> DeliveryEnded;                            // column
        public event Action Won;

        private readonly BoardGraph _board;
        private readonly TiledMap _map;
        private readonly LevelDefinition _level;
        private readonly Dictionary<int, PillKind> _spawnIds;
        private readonly Dictionary<Vector2Int, PillKind> _pills = new Dictionary<Vector2Int, PillKind>();
        private readonly List<PillKind> _segments = new List<PillKind>();
        private readonly List<Vector2> _trail = new List<Vector2>(); // cell centres passed, newest first
        private readonly float _speed;

        private Vector2Int _target;
        private Direction? _bufferedSwipe;
        private int _deliveryColumn = -1;
        private float _deliveryTimer;

        public Vector2 HeadPosition { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public Direction Heading { get; private set; } = Direction.Up;
        public bool IsMoving { get; private set; }
        public bool IsDelivering => _deliveryColumn >= 0;
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

        public bool RequestMove(Direction direction)
        {
            if (IsWon || IsDelivering) return false;
            if (IsMoving)
            {
                _bufferedSwipe = direction;
                return false;
            }
            if (!_board.CanMove(CurrentCell, direction) || BlockedByTail(direction)) return false;

            Heading = direction;
            _target = CurrentCell + direction.ToCell();
            IsMoving = true;
            MoveStarted?.Invoke(direction);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (IsWon) return;
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
            Stopped?.Invoke(CurrentCell);

            int column = _board.CrateColumnAt(CurrentCell);
            if (CanDeliverTo(column))
            {
                _deliveryColumn = column;
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
            for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!_spawnIds.TryGetValue(_map.GetLocalTileId(layer, cell), out var kind)) continue;
                _pills[cell] = kind;
                PillSpawned?.Invoke(cell, kind);
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
                    _deliveryColumn = -1;
                    DeliveryEnded?.Invoke(column);
                    return;
                }
            }
        }

        private bool DeliverOne(int columnIndex)
        {
            var column = Columns[columnIndex];
            if (column.IsComplete) return false;
            var kind = column.Current.kind;
            int index = _segments.LastIndexOf(kind);
            if (index < 0) return false;

            _segments.RemoveAt(index);
            column.Filled++;
            SegmentDelivered?.Invoke(index, kind, columnIndex);

            if (column.Filled >= column.Current.requiredCount)
            {
                int completed = column.CurrentIndex;
                column.CurrentIndex++;
                column.Filled = 0;
                _deliveryTimer = -CrateCompletePause; // the snake stays locked during the pause
                CrateCompleted?.Invoke(columnIndex, completed);
                if (AllColumnsComplete())
                {
                    IsWon = true;
                    Won?.Invoke();
                    return false;
                }
            }
            return true;
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
