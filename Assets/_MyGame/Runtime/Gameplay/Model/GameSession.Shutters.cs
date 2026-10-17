using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Khay đóng / mở (TOGGLE CRATE, từ Level 71; gốc ShutterCrateController; video lv70): cột có cờ closedAtStart bắt đầu đóng.
    /// Khay đóng không nhận trái (rắn tới thì khay rung từ chối). Khay đi theo cặp (cột i với cột i + nửa số cột): giao xong vào một
    /// khay thì khay đó đóng lại và khay cặp của nó mở ra [user]; khay cặp hết việc thì khay vừa giao giữ mở (gốc
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
            // Khay cặp không có việc (cột trống) thì khay đóng của cặp đó mở luôn, không thì đóng mãi.
            for (int i = 0; i < Columns.Count && HasShutters; i++)
                if (_closed[i] && Partner(i) >= 0 && !HasWork(Partner(i))) _closed[i] = false;
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

        /// <summary>
        /// Khay đi theo cặp [user, video lv70: trên trái ↔ dưới trái, trên phải ↔ dưới phải ở màn 71]: cột i với cột i + nửa số cột.
        /// Số cột lẻ thì không có cặp: -1.
        /// </summary>
        private int Partner(int column)
        {
            int count = Columns.Count;
            if (count < 2 || count % 2 != 0) return -1;
            return (column + count / 2) % count;
        }

        /// <summary>Một lượt giao (rắn hay boomerang) vào <paramref name="column"/> vừa xong: khay đó đóng, khay cặp của nó mở.</summary>
        private void ToggleAfterDelivery(int column)
        {
            int delivered = _deliveredThisRun;
            _deliveredThisRun = 0;
            if (!HasShutters || delivered == 0 || column < 0) return;
            int partner = Partner(column);
            if (partner >= 0)
            {
                if (!HasWork(partner)) return; // khay cặp hết việc: khay này giữ mở
                if (_closed[partner]) { _closed[partner] = false; CrateShutterChanged?.Invoke(partner, false); }
            }
            else if (!OpenBestClosed(column)) return; // không có cặp: mở khay đóng có nhiều trái nhất; không còn khay nào thì giữ mở
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
