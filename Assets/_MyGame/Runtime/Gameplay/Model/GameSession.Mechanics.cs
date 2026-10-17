using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Phần GameSession lo rèm và khối nâng (từ Level 41; gốc: CurtainManager, RisingBlockController; video Lv43) và cầu bật
    /// (từ Level 61; gốc: SwitchController, BridgeController; video lv61).
    /// Rèm mở ở lần dừng kế tiếp khi ăn đủ trái cùng loại; khối nâng dựng lên sau khi cả rắn qua, hạ sau vài khay xong;
    /// đầu rắn qua vạch công tắc thì mọi cầu bật đổi dựng / hạ. CurtainsView và BoardView nghe các event ở đây.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Một tấm rèm: loại trái mở nó và số còn phải ăn.</summary>
        public sealed class Curtain
        {
            public int Group { get; }
            public PillKind Kind { get; }
            public int Remaining { get; internal set; }
            /// <summary>Đếm đã về 0: mở khi rắn dừng lần tới.</summary>
            public bool Pending { get; internal set; }
            public bool Open { get; internal set; }
            /// <summary>Bộ đếm đặt giữa hộp ô này.</summary>
            public Vector2Int CounterMin { get; }
            public Vector2Int CounterMax { get; }

            internal Curtain(int group, PillKind kind, int count, Vector2Int min, Vector2Int max)
            {
                Group = group;
                Kind = kind;
                Remaining = count;
                CounterMin = min;
                CounterMax = max;
            }
        }

        private readonly List<Curtain> _curtains = new List<Curtain>();
        private readonly Dictionary<Vector2Int, int> _blockPassed = new Dictionary<Vector2Int, int>(); // khối đang hạ -> 1 khi đầu đã đi qua
        private readonly Dictionary<Vector2Int, int> _blockDeliveries = new Dictionary<Vector2Int, int>(); // khối đang nâng -> số khay xong từ lúc nâng
        private int _deliveriesToLower = 3;

        public IReadOnlyList<Curtain> Curtains => _curtains;

        public event Action<int, int> CurtainCountChanged; // chỉ số rèm, số trái còn phải ăn
        public event Action<int> CurtainOpened;            // chỉ số rèm: đường (và khay) dưới nó hiện ra
        public event Action<Vector2Int, bool> RisingBlockChanged; // ô, đang nâng

        private void SetUpMechanics(LevelDefinition level)
        {
            var cells = new List<(Vector2Int, int)>();
            foreach (var c in level.curtainCells) cells.Add((c.cell, c.groupId));
            _board.SetCurtainGroups(cells);
            var groups = new HashSet<int>(_board.CurtainCells.Values);
            foreach (var group in groups)
            {
                var definition = level.curtainGroups.Find(g => g.groupId == group);
                bool known = level.curtainGroups.Exists(g => g.groupId == group);
                var min = known ? definition.counterMin : Vector2Int.zero;
                var max = known ? definition.counterMax : Vector2Int.zero;
                if (!known) // dữ liệu màn không có nhóm này: đặt bộ đếm giữa các ô rèm
                {
                    bool first = true;
                    foreach (var pair in _board.CurtainCells)
                    {
                        if (pair.Value != group) continue;
                        min = first ? pair.Key : Vector2Int.Min(min, pair.Key);
                        max = first ? pair.Key : Vector2Int.Max(max, pair.Key);
                        first = false;
                    }
                }
                _curtains.Add(new Curtain(group, known ? definition.kind : PillKind.Strawberry, known ? Mathf.Max(1, definition.count) : 10, min, max));
            }
            _deliveriesToLower = level.risingBlockDeliveriesToLower > 0 ? level.risingBlockDeliveriesToLower : 3;
        }

        private void CountCurtainFruit(PillKind kind)
        {
            for (int i = 0; i < _curtains.Count; i++)
            {
                var curtain = _curtains[i];
                if (curtain.Open || curtain.Pending || curtain.Kind != kind) continue;
                curtain.Remaining--;
                if (curtain.Remaining <= 0) { curtain.Remaining = 0; curtain.Pending = true; }
                CurtainCountChanged?.Invoke(i, curtain.Remaining);
            }
        }

        /// <summary>Rắn dừng: rèm đã đếm xong thì mở (gốc: CurtainManager.FlushPendingReveals).</summary>
        private void OpenPendingCurtains()
        {
            for (int i = 0; i < _curtains.Count; i++)
            {
                var curtain = _curtains[i];
                if (!curtain.Pending) continue;
                curtain.Pending = false;
                curtain.Open = true;
                _board.OpenCurtain(curtain.Group);
                CurtainOpened?.Invoke(i);
            }
        }

        /// <summary>
        /// Ô rắn tới được khi còn rèm đóng; trái chỉ rải vào đây, nếu không màn bị kẹt (Level 41) (gốc: PillSpawnEligibility).
        /// Null khi không còn rèm đóng. Đá đang đóng cũng chặn (Level 48: đá dưới cầu ngăn vùng dưới); cổng, khối nâng coi như mở; đường hầm nối hai miệng.
        /// </summary>
        private HashSet<Vector2Int> ReachableWhileCurtained()
        {
            if (!_curtains.Exists(c => !c.Open)) return null;
            var reach = new HashSet<Vector2Int> { CurrentCell };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(CurrentCell);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = cell + direction.ToCell();
                    if (_board.HasRoad(cell, direction) && !_board.IsCovered(next) && !_board.WallBlocks(cell, direction)
                        && !_board.WallBlocks(next, direction) && reach.Add(next)) queue.Enqueue(next);
                }
                if (_board.TryGetTunnelOther(cell, out var other) && reach.Add(other)) queue.Enqueue(other);
            }
            return reach;
        }

        /// <summary>True khi khay của <paramref name="column"/> còn nằm dưới rèm chưa mở.</summary>
        public bool IsColumnCovered(int column) =>
            (_board.CrateAnchors.TryGetValue(column, out var anchor) && _board.IsCovered(anchor))
            || (_board.CrateStops.TryGetValue(column, out var stop) && _board.IsCovered(stop));

        private void PassRisingBlock(Vector2Int cell)
        {
            if (_blockDeliveries.ContainsKey(cell)) return; // đang nâng (không xảy ra: đường đã đóng)
            foreach (var block in _board.RisingBlocks)
                if (block == cell) { _blockPassed[cell] = 1; return; }
        }

        /// <summary>Khối rắn đã đi qua sẽ nâng khi không còn phần nào của rắn trên nó (gốc: RisingBlockController.OnTailUpdated).</summary>
        private void RaiseLeftBlocks()
        {
            if (_blockPassed.Count == 0) return;
            List<Vector2Int> rise = null;
            foreach (var cell in _blockPassed.Keys)
                if (!SnakeOn(cell)) (rise ?? (rise = new List<Vector2Int>())).Add(cell);
            if (rise == null) return;
            foreach (var cell in rise)
            {
                _blockPassed.Remove(cell);
                _blockDeliveries[cell] = 0;
                _board.SetRisen(cell, true);
                RisingBlockChanged?.Invoke(cell, true);
            }
        }

        /// <summary>Một khay xong: khối đang nâng đếm, đủ <see cref="LevelDefinition.risingBlockDeliveriesToLower"/> thì hạ.</summary>
        private void LowerRisenBlocks()
        {
            if (_blockDeliveries.Count == 0) return;
            foreach (var cell in new List<Vector2Int>(_blockDeliveries.Keys))
            {
                int done = _blockDeliveries[cell] + 1;
                if (done < _deliveriesToLower) { _blockDeliveries[cell] = done; continue; }
                _blockDeliveries.Remove(cell);
                _board.SetRisen(cell, false);
                RisingBlockChanged?.Invoke(cell, false);
            }
        }

        /// <summary>Không trái trên khối đang nâng hay sắp nâng; khối đã hạ nhận trái như đường thường [user; gốc: PillSpawnEligibility].</summary>
        private bool NoFruitOnBlock(Vector2Int cell) => _board.IsRisen(cell) || _blockPassed.ContainsKey(cell);

        // ------------------------------------------------------------------ cầu bật

        private bool _switchOn = true; // gốc SwitchController._initialState 0 = On: cầu dựng
        private readonly HashSet<Vector2Int> _bridgeWaiting = new HashSet<Vector2Int>(); // cầu phải đổi nhưng rắn còn nằm trên

        /// <summary>Công tắc đang On: cầu bật dựng (cầu thường); Off: cầu hạ (ngã tư).</summary>
        public bool SwitchOn => _switchOn;

        public event Action<bool> SwitchToggled;                // công tắc đổi (cần gạt, vạch)
        public event Action<Vector2Int, bool> SwitchBridgeChanged; // ô cầu, đang dựng

        /// <summary>Đầu rắn vào vạch công tắc: đổi công tắc, mọi cầu bật đổi theo khi rắn không nằm trên nó.</summary>
        private void PassSwitch(Vector2Int cell)
        {
            if (!_board.IsSwitch(cell)) return;
            _switchOn = !_switchOn;
            SwitchToggled?.Invoke(_switchOn);
            foreach (var bridge in _board.SwitchBridges.Keys) _bridgeWaiting.Add(bridge);
            ApplyWaitingBridges();
        }

        /// <summary>Cầu chờ rắn đi khỏi rồi mới dựng / hạ (gốc BridgeController.CanOpenNow / CanCloseNow: ô phải trống).</summary>
        private void ApplyWaitingBridges()
        {
            if (_bridgeWaiting.Count == 0) return;
            foreach (var cell in new List<Vector2Int>(_bridgeWaiting))
            {
                if (_board.IsBridgeUp(cell) == _switchOn) { _bridgeWaiting.Remove(cell); continue; }
                if (SnakeOn(cell)) continue;
                _bridgeWaiting.Remove(cell);
                _board.SetBridgeUp(cell, _switchOn);
                SwitchBridgeChanged?.Invoke(cell, _switchOn);
            }
        }

        /// <summary>Đầu (kể cả ô đang chạy tới) hoặc đốt thân nào chạm <paramref name="cell"/>.</summary>
        private bool SnakeOn(Vector2Int cell)
        {
            if (CurrentCell == cell || (IsMoving && _target == cell)) return true;
            if ((HeadPosition - (Vector2)cell).sqrMagnitude < 0.98f) return true;
            for (int i = 0; i < _segments.Count; i++)
                if ((SegmentPosition(i) - (Vector2)cell).sqrMagnitude < 0.98f) return true;
            return false;
        }

        // ------------------------------------------------------------------ hoàn tác

        private sealed class MechanicsState
        {
            public int[] remaining;
            public bool[] pending;
            public Dictionary<Vector2Int, int> passed, deliveries;
            public bool switchOn;
            public HashSet<Vector2Int> bridgesUp, bridgeWaiting;
        }

        private MechanicsState SaveMechanics()
        {
            var state = new MechanicsState
            {
                remaining = new int[_curtains.Count],
                pending = new bool[_curtains.Count],
                passed = new Dictionary<Vector2Int, int>(_blockPassed),
                deliveries = new Dictionary<Vector2Int, int>(_blockDeliveries),
                switchOn = _switchOn,
                bridgesUp = new HashSet<Vector2Int>(),
                bridgeWaiting = new HashSet<Vector2Int>(_bridgeWaiting),
            };
            foreach (var cell in _board.SwitchBridges.Keys) if (_board.IsBridgeUp(cell)) state.bridgesUp.Add(cell);
            for (int i = 0; i < _curtains.Count; i++)
            {
                state.remaining[i] = _curtains[i].Remaining;
                state.pending[i] = _curtains[i].Pending;
            }
            return state;
        }

        /// <summary>
        /// Hoàn tác: trái trả lại bàn được trừ khỏi bộ đếm rèm, rèm đã mở vẫn mở (gốc: CurtainGroupRuntime.TryUndoFruitContribution).
        /// Khối nâng về trạng thái trước lần vuốt.
        /// </summary>
        private void RestoreMechanics(MechanicsState state, IReadOnlyCollection<Vector2Int> risenBefore)
        {
            for (int i = 0; i < _curtains.Count; i++)
            {
                var curtain = _curtains[i];
                if (curtain.Open) continue;
                curtain.Remaining = state.remaining[i];
                curtain.Pending = state.pending[i];
                CurtainCountChanged?.Invoke(i, curtain.Remaining);
            }
            _blockPassed.Clear();
            foreach (var pair in state.passed) _blockPassed[pair.Key] = pair.Value;
            _blockDeliveries.Clear();
            foreach (var pair in state.deliveries) _blockDeliveries[pair.Key] = pair.Value;
            foreach (var cell in _board.RisingBlocks)
            {
                bool risen = ((ICollection<Vector2Int>)risenBefore).Contains(cell);
                if (_board.IsRisen(cell) == risen) continue;
                _board.SetRisen(cell, risen);
                RisingBlockChanged?.Invoke(cell, risen);
            }
            // Cầu bật và công tắc về như trước lần vuốt (gốc: memento của SwitchController / BridgeController).
            if (_switchOn != state.switchOn)
            {
                _switchOn = state.switchOn;
                SwitchToggled?.Invoke(_switchOn);
            }
            foreach (var cell in _board.SwitchBridges.Keys)
            {
                bool up = state.bridgesUp.Contains(cell);
                if (_board.IsBridgeUp(cell) == up) continue;
                _board.SetBridgeUp(cell, up);
                SwitchBridgeChanged?.Invoke(cell, up);
            }
            _bridgeWaiting.Clear();
            _bridgeWaiting.UnionWith(state.bridgeWaiting);
        }
    }
}
