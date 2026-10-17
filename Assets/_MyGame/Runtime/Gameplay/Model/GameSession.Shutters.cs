using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Khay đóng / mở (TOGGLE CRATE, từ Level 71; gốc ShutterCrateController; video lv70): cột có cờ closedAtStart bắt đầu đóng.
    /// Khay đóng không nhận trái (rắn tới thì khay rung từ chối). Giao xong vào một khay thì khay đó đóng lại và khay đóng xa nhất
    /// theo đường mở ra cùng lúc (khay đi theo cặp [user]); không còn khay đóng nào còn việc thì khay vừa giao giữ mở (gốc
    /// TryLockSoleRemainingWorkColumn). Trái đã giao không hoàn tác nên cửa cũng không.
    /// </summary>
    public sealed partial class GameSession
    {
        private bool[] _closed;
        private int _deliveredThisRun; // trái giao trong lượt giao hiện tại

        /// <summary>Màn có khay đóng / mở.</summary>
        public bool HasShutters { get; private set; }

        public event Action<int, bool> CrateShutterChanged; // cột, đang đóng
        public event Action<int> ClosedCrateRejected;       // rắn tới khay đang đóng

        public bool IsCrateClosed(int column) => _closed != null && column >= 0 && column < _closed.Length && _closed[column];

        private void SetUpShutters(LevelDefinition level)
        {
            _closed = new bool[Columns.Count];
            for (int i = 0; i < Columns.Count && i < level.columns.Count; i++)
                if (level.columns[i].closedAtStart && !Columns[i].IsExtra) _closed[i] = HasShutters = true;
            if (HasShutters && !HasOpenWork()) OpenBestClosed(-1);
        }

        /// <summary>Cột còn việc, không dưới rèm, không phải cột thêm còn khoá.</summary>
        private bool HasWork(int column) =>
            !Columns[column].IsComplete && !IsColumnCovered(column) && !(Columns[column].IsExtra && ExtraLocked);

        private bool HasOpenWork()
        {
            for (int i = 0; i < Columns.Count; i++)
                if (!_closed[i] && HasWork(i)) return true;
            return false;
        }

        private int[,] _stopDistance; // số ô đi đường giữa điểm giao hai cột (-1: không tới được)

        /// <summary>
        /// Khay đi theo cặp [user, video lv70]: giao xong vào một khay thì khay đóng còn việc ở <b>xa nhất theo đường</b> mở ra
        /// (bằng nhau thì cột đứng trước). Ở màn 71 ra đúng cặp trong video (trên trái ↔ dưới trái 20 ô, không phải trên phải 6 ô);
        /// cột i ↔ i + nửa số cột sai ở màn 150 (hai khay cùng cặp đều đóng từ đầu). -1 khi không còn khay đóng nào còn việc.
        /// </summary>
        private int Partner(int column)
        {
            if (_stopDistance == null) BuildStopDistances();
            int best = -1, bestDistance = int.MinValue;
            for (int i = 0; i < Columns.Count; i++)
            {
                if (i == column || !_closed[i] || !HasWork(i)) continue;
                int distance = _stopDistance[column, i];
                if (distance > bestDistance) { best = i; bestDistance = distance; }
            }
            return best;
        }

        private void BuildStopDistances()
        {
            int count = Columns.Count;
            _stopDistance = new int[count, count];
            for (int a = 0; a < count; a++)
            {
                for (int b = 0; b < count; b++) _stopDistance[a, b] = -1;
                if (!_board.CrateStops.TryGetValue(a, out var start)) continue;
                var distance = new Dictionary<Vector2Int, int> { [start] = 0 };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var direction in DirectionExtensions.All)
                    {
                        var next = cell + direction.ToCell();
                        if (distance.ContainsKey(next) || !_board.HasRoad(cell, direction)) continue;
                        distance[next] = distance[cell] + 1;
                        queue.Enqueue(next);
                    }
                }
                for (int b = 0; b < count; b++)
                    if (_board.CrateStops.TryGetValue(b, out var stop) && distance.TryGetValue(stop, out int d)) _stopDistance[a, b] = d;
            }
        }

        /// <summary>Một lượt giao (rắn hay boomerang) vào <paramref name="column"/> vừa xong: khay đó đóng, khay cặp của nó mở cùng lúc.</summary>
        private void ToggleAfterDelivery(int column)
        {
            int delivered = _deliveredThisRun;
            _deliveredThisRun = 0;
            // Khay phụ (mua bằng coin) không phải khay đóng / mở: giao vào nó không đổi cửa nào, và nó không bao giờ bị đóng
            // (nếu không, khay thường còn việc có thể đóng hết trong khi chỉ khay phụ mở, màn 104 / 119 / 124).
            if (!HasShutters || delivered == 0 || column < 0 || Columns[column].IsExtra) return;
            int partner = Partner(column);
            if (partner < 0) return; // không còn khay đóng nào còn việc: khay này giữ mở (gốc TryLockSoleRemainingWorkColumn)
            _closed[partner] = false;
            CrateShutterChanged?.Invoke(partner, false);
            if (Columns[column].IsComplete || _closed[column]) return;
            _closed[column] = true;
            CrateShutterChanged?.Invoke(column, true);
        }

        /// <summary>Mở khay đóng có loại trái đang có nhiều nhất (bằng nhau thì cột đứng trước); false khi không có khay đóng nào còn việc.</summary>
        private bool OpenBestClosed(int except)
        {
            int best = -1, bestCount = -1;
            for (int i = 0; i < Columns.Count; i++)
            {
                if (i == except || !_closed[i] || !HasWork(i)) continue;
                int count = Available(Columns[i].Current.kind);
                if (count > bestCount) { best = i; bestCount = count; }
            }
            if (best < 0) return false;
            _closed[best] = false;
            CrateShutterChanged?.Invoke(best, false);
            return true;
        }

        /// <summary>
        /// Không khay mở nào còn trái để nhận (loại của nó không có trên bàn lẫn thân rắn, cũng chưa đầy) mà một khay đóng có:
        /// mở khay đó để màn không kẹt (gốc EnsureWorkColumnReachable). Gọi khi rắn dừng và sau khi cắt đuôi.
        /// </summary>
        private void EnsureShutterProgress()
        {
            if (!HasShutters) return;
            // Không còn khay thường nào mở mà còn việc (Snip làm đầy khay mở cuối, màn 104): mở một khay đóng, như lúc vào màn.
            bool openRegular = false;
            for (int i = 0; i < Columns.Count && !openRegular; i++)
                openRegular = !_closed[i] && !Columns[i].IsExtra && HasWork(i);
            if (!openRegular && OpenBestClosed(-1)) return;
            bool closedCanUse = false;
            for (int i = 0; i < Columns.Count; i++)
            {
                if (!HasWork(i)) continue;
                bool usable = Columns[i].IsFull(Columns[i].CurrentIndex) || Available(Columns[i].Current.kind) > 0;
                if (!usable) continue;
                if (!_closed[i]) return; // còn khay mở làm được
                closedCanUse = true;
            }
            if (closedCanUse) OpenBestClosed(-1);
        }

        /// <summary>Số trái loại này trên bàn và trên thân rắn.</summary>
        private int Available(PillKind kind)
        {
            int count = 0;
            foreach (var pill in _pills.Values) if (pill == kind) count++;
            foreach (var segment in _segments) if (segment == kind) count++;
            return count;
        }
    }
}
