using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Khay đóng / mở (TOGGLE CRATE, từ Level 71; gốc ShutterCrateController; video lv70): cột có cờ closedAtStart bắt đầu đóng.
    /// Khay đóng không nhận trái (rắn tới thì khay rung từ chối). Giao xong vào một khay thì khay đó đóng lại và một khay đang đóng
    /// mở ra (khay có loại trái đang có nhiều nhất trên bàn và thân rắn; gốc TryPromoteClosedColumnByPriority); không còn khay đóng
    /// nào có việc thì khay vừa giao giữ mở (gốc TryLockSoleRemainingWorkColumn). Trái đã giao không hoàn tác nên cửa cũng không.
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

        /// <summary>Một lượt giao (rắn hay boomerang) vào <paramref name="column"/> vừa xong.</summary>
        private void ToggleAfterDelivery(int column)
        {
            int delivered = _deliveredThisRun;
            _deliveredThisRun = 0;
            if (!HasShutters || delivered == 0 || column < 0) return;
            if (!OpenBestClosed(column)) return; // khay cuối còn việc: giữ mở
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
