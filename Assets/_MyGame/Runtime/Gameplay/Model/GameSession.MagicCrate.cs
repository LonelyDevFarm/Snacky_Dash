using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Booster Khay thần kỳ (phần sáng tạo, bản gốc không có): chọn một cột, khay trên cùng được lấp đủ bằng trái thật theo
    /// <see cref="MagicCratePlanner"/> rồi đi như giao thường [user 20/10]. Trái trên bàn bay đi như nam châm hút
    /// (<see cref="PillMagnetized"/>), trái của đợt sau hiện ở chỗ của nó rồi bay (<see cref="FruitTakenAhead"/>) và đợt đó rải
    /// ít đi, trái trên đuôi bay như boomerang. Số lượng booster do người gọi lo.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Trái của đợt chưa rải bị lấy: ô nó hiện lên, loại, cột, khay.</summary>
        public event Action<Vector2Int, PillKind, int, int> FruitTakenAhead;

        /// <summary>Khay thần kỳ bắt đầu: cột, số trái sẽ bay (đến trước các event từng trái, để view xếp lượt bay).</summary>
        public event Action<int, int> MagicCrateUsed;

        /// <summary>Giây giữa hai trái bay vào khay (như boomerang).</summary>
        public const float MagicCrateInterval = BoomerangInterval;

        // Đợt chưa rải -> số trái mỗi loại đã bị lấy (đợt đó rải ít đi đúng số này). Giữ mãi: undo trả đợt về thì rải lại vẫn thiếu đúng chừng ấy.
        private readonly Dictionary<int, Dictionary<PillKind, int>> _takenAhead = new Dictionary<int, Dictionary<PillKind, int>>();
        // Màn có layer spawn: ô của đợt đó không rải nữa.
        private readonly Dictionary<int, HashSet<Vector2Int>> _skippedSpawn = new Dictionary<int, HashSet<Vector2Int>>();
        // Cột vừa dùng booster: khay đóng mở đổi cửa sau khi khay đầy đi, như một lần giao.
        private int _toggleAfterSettle = -1;

        /// <summary>Cột chọn được: khay trên cùng còn chỗ, cửa mở, không dưới rèm, không phải cột thêm còn khoá.</summary>
        private bool MagicColumn(int column) =>
            column >= 0 && column < Columns.Count && !Columns[column].IsComplete && !Columns[column].IsFull(Columns[column].CurrentIndex)
            && !IsCrateClosed(column) && !IsColumnCovered(column) && !(Columns[column].IsExtra && ExtraLocked);

        /// <summary>Trái sẽ lấy cho cột này (rỗng: không dùng được).</summary>
        public List<MagicCratePlanner.Pick> PlanMagicCrate(int column)
        {
            if (!IsIdle || !MagicColumn(column)) return new List<MagicCratePlanner.Pick>();
            var stack = Columns[column];
            var kind = stack.Current.kind;
            var ahead = new List<int>();
            for (int wave = BatchIndex + 1; wave < _level.batches.Count; wave++) ahead.Add(AheadAvailable(wave, kind));
            return MagicCratePlanner.Plan(kind, stack.Current.requiredCount - stack.Filled, HeadPosition, _pills, _secret, ahead, BatchIndex + 1, _segments);
        }

        public bool CanMagicCrate(int column) => PlanMagicCrate(column).Count > 0;

        public bool UseMagicCrate(int column)
        {
            var plan = PlanMagicCrate(column);
            if (plan.Count == 0) return false;
            var stack = Columns[column];
            int crate = stack.CurrentIndex;
            var kind = stack.Current.kind;
            ForgetUndo();
            MagicCrateUsed?.Invoke(column, plan.Count);

            var tail = new List<int>();
            foreach (var pick in plan)
            {
                if (pick.source == MagicCratePlanner.Source.Tail) { tail.Add(pick.segment); continue; }
                stack.Add(crate);
                CountCurtainFruit(kind); // rèm đếm trái đã ăn: trái bị lấy cũng tính, không thì rèm có thể không bao giờ mở
                if (pick.source == MagicCratePlanner.Source.Board)
                {
                    _pills.Remove(pick.cell);
                    PillMagnetized?.Invoke(pick.cell, kind, column, crate);
                }
                else
                {
                    var cell = TakeAhead(pick.wave, kind); // gọi riêng: trong ?.Invoke(...) nó bị bỏ qua khi không ai nghe event
                    FruitTakenAhead?.Invoke(cell, kind, column, crate);
                }
            }
            // Đuôi: xoá từ đốt xa đầu nhất để chỉ số gửi cho view vẫn đúng.
            for (int i = tail.Count - 1; i >= 0; i--)
            {
                int segment = tail[i];
                stack.Add(crate);
                SegmentSnipped?.Invoke(segment, kind, column, crate);
                _segments.RemoveAt(segment);
                SegmentRemoved?.Invoke(segment);
            }

            _deliveredThisRun += plan.Count;
            _toggleAfterSettle = column;
            if (_pills.Count == 0) SpawnNextBatch();
            EnsureShutterProgress();
            StartSettlingFullCrates(plan.Count * MagicCrateInterval);
            return true;
        }

        /// <summary>Khay đầy do booster vừa đi xong: đổi cửa khay đóng mở như sau một lần giao.</summary>
        private void ToggleAfterMagicCrate()
        {
            if (_toggleAfterSettle < 0) return;
            int column = _toggleAfterSettle;
            _toggleAfterSettle = -1;
            ToggleAfterDelivery(column);
        }

        private int Taken(int wave, PillKind kind) =>
            _takenAhead.TryGetValue(wave, out var kinds) && kinds.TryGetValue(kind, out int count) ? count : 0;

        /// <summary>Trái đã bị lấy ở các đợt từ <paramref name="firstWave"/> trở đi (mọi loại, hoặc một loại).</summary>
        private int TakenFrom(int firstWave, PillKind? kind = null)
        {
            int total = 0;
            foreach (var wave in _takenAhead)
                if (wave.Key >= firstWave)
                    foreach (var taken in wave.Value)
                        if (kind == null || taken.Key == kind.Value) total += taken.Value;
            return total;
        }

        /// <summary>Số trái loại này của đợt chưa rải còn lấy được.</summary>
        private int AheadAvailable(int wave, PillKind kind)
        {
            if (_map.HasLayer(SpawnLayer(wave))) return SpawnCells(wave, kind).Count;
            int count = 0;
            foreach (var pill in _level.batches[wave].pills) if (pill.kind == kind) count += pill.count;
            return Mathf.Max(0, count - Taken(wave, kind));
        }

        private static string SpawnLayer(int wave) => "spawn" + (wave + 1);

        /// <summary>Ô của loại này trong layer spawn của đợt, trừ ô đã bỏ và ô trái bí ẩn.</summary>
        private List<Vector2Int> SpawnCells(int wave, PillKind kind)
        {
            var cells = new List<Vector2Int>();
            string layer = SpawnLayer(wave);
            _skippedSpawn.TryGetValue(wave, out var skipped);
            for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!_spawnIds.TryGetValue(_map.GetLocalTileId(layer, cell), out var at) || at != kind) continue;
                if ((skipped != null && skipped.Contains(cell)) || _board.IsSecretSpawn(wave, cell)) continue;
                cells.Add(cell);
            }
            return cells;
        }

        private bool SkippedSpawn(int wave, Vector2Int cell) => _skippedSpawn.TryGetValue(wave, out var skipped) && skipped.Contains(cell);

        /// <summary>
        /// Lấy một trái của đợt chưa rải; trả về ô nó hiện lên: đúng ô của nó trong layer spawn (đợt đó rải thì ô trống), màn rải
        /// ngẫu nhiên thì một ô trái trống xa đầu nhất (vị trí vốn ngẫu nhiên nên người chơi không thấy lệch) [user 20/10].
        /// </summary>
        private Vector2Int TakeAhead(int wave, PillKind kind)
        {
            if (!_takenAhead.TryGetValue(wave, out var kinds)) _takenAhead[wave] = kinds = new Dictionary<PillKind, int>();
            kinds[kind] = Taken(wave, kind) + 1;

            if (_map.HasLayer(SpawnLayer(wave)))
            {
                var cell = Farthest(SpawnCells(wave, kind));
                if (!_skippedSpawn.TryGetValue(wave, out var skipped)) _skippedSpawn[wave] = skipped = new HashSet<Vector2Int>();
                skipped.Add(cell);
                return cell;
            }
            var occupied = new HashSet<Vector2Int> { CurrentCell };
            for (int i = 0; i < _segments.Count; i++) occupied.Add(Vector2Int.RoundToInt(SegmentPosition(i)));
            var free = new List<Vector2Int>();
            foreach (var group in _board.PillSlotGroups)
                foreach (var cell in group)
                    if (!_pills.ContainsKey(cell) && !occupied.Contains(cell) && !_board.IsCovered(cell) && !NoFruitOnBlock(cell)) free.Add(cell);
            return free.Count > 0 ? Farthest(free) : CurrentCell;
        }

        private Vector2Int Farthest(List<Vector2Int> cells)
        {
            var best = cells[0];
            foreach (var cell in cells)
                if ((cell - HeadPosition).sqrMagnitude > (best - HeadPosition).sqrMagnitude) best = cell;
            return best;
        }

        /// <summary>Đợt rải ngẫu nhiên, bớt trái đã bị lấy.</summary>
        private SpawnBatchDefinition WithoutTaken(int wave)
        {
            var authored = _level.batches[wave];
            if (!_takenAhead.TryGetValue(wave, out var taken)) return authored;
            var toTake = new Dictionary<PillKind, int>(taken); // một loại có thể nằm ở nhiều dòng của đợt
            var left = new SpawnBatchDefinition();
            foreach (var pill in authored.pills)
            {
                toTake.TryGetValue(pill.kind, out int take);
                take = Mathf.Min(take, pill.count);
                if (take > 0) toTake[pill.kind] -= take;
                left.pills.Add(new PillCount { kind = pill.kind, count = pill.count - take });
            }
            return left;
        }
    }
}
