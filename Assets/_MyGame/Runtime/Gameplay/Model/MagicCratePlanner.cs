using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Luật lấy trái của booster Khay thần kỳ (phần sáng tạo, bản gốc không có): khay được chọn lấp đủ ô trống bằng trái thật
    /// cùng màu, lấy trên bàn trước (trái xa đầu rắn nhất trước), thiếu thì lấy ở các đợt sau (đợt gần nhất trước), cuối cùng
    /// trên đuôi (đốt gần đầu nhất trước) [user 20/10]. Tổng trái mỗi màu vẫn bằng số ô khay nên màn không bao giờ kẹt.
    /// Thuần C#: GameSession đưa trạng thái vào, nhận danh sách rồi tự thực hiện.
    /// </summary>
    public static class MagicCratePlanner
    {
        public enum Source { Board, Ahead, Tail }

        public struct Pick
        {
            public Source source;
            /// <summary>Board: ô của trái trên bàn.</summary>
            public Vector2Int cell;
            /// <summary>Ahead: chỉ số đợt chưa rải.</summary>
            public int wave;
            /// <summary>Tail: chỉ số đốt (0 = ngay sau đầu).</summary>
            public int segment;
        }

        /// <param name="kind">Loại trái khay cần.</param>
        /// <param name="need">Số ô còn trống của khay.</param>
        /// <param name="head">Vị trí đầu rắn (ô).</param>
        /// <param name="pills">Trái đang trên bàn.</param>
        /// <param name="hidden">Ô trái bí ẩn chưa lộ: không lấy.</param>
        /// <param name="aheadCounts">Số trái loại này còn lấy được ở từng đợt chưa rải, bắt đầu từ đợt <paramref name="firstAheadWave"/>.</param>
        /// <param name="segments">Thân rắn, 0 = ngay sau đầu.</param>
        public static List<Pick> Plan(PillKind kind, int need, Vector2 head,
            IReadOnlyDictionary<Vector2Int, PillKind> pills, ICollection<Vector2Int> hidden,
            IReadOnlyList<int> aheadCounts, int firstAheadWave, IReadOnlyList<PillKind> segments)
        {
            var picks = new List<Pick>();
            if (need <= 0) return picks;

            var board = new List<Vector2Int>();
            foreach (var pill in pills)
                if (pill.Value == kind && !hidden.Contains(pill.Key)) board.Add(pill.Key);
            board.Sort((a, b) =>
            {
                int far = (b - head).sqrMagnitude.CompareTo((a - head).sqrMagnitude);
                if (far != 0) return far;
                return a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x); // bằng nhau: thứ tự cố định để test lặp lại được
            });
            for (int i = 0; i < board.Count && picks.Count < need; i++)
                picks.Add(new Pick { source = Source.Board, cell = board[i] });

            for (int w = 0; w < aheadCounts.Count && picks.Count < need; w++)
                for (int n = 0; n < aheadCounts[w] && picks.Count < need; n++)
                    picks.Add(new Pick { source = Source.Ahead, wave = firstAheadWave + w });

            for (int i = 0; i < segments.Count && picks.Count < need; i++)
                if (segments[i] == kind) picks.Add(new Pick { source = Source.Tail, segment = i });

            return picks;
        }
    }
}
