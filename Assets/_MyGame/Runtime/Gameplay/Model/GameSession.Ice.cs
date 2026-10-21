using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Cơ chế Mặt băng (phần sáng tạo, bản gốc không có; chỉ màn sau 366) [user 20-21/10]: một đoạn đường nối hai điểm dừng, nuốt 1-2 điểm
    /// dừng ở giữa, ghi độ nặng. Rắn mang ít trái hơn thì không dừng ở các điểm dừng giữa: đi dọc băng thì cứ đi thẳng (cua thường thì tự
    /// rẽ) tới đầu kia; vào giữa đoạn từ nhánh bên thì rẽ trái, không được thì rẽ phải. Mang đủ thì tới điểm dừng nào trong đoạn cả đoạn
    /// vỡ, rắn dừng ở đó. Hoàn tác lượt vuốt làm vỡ băng thì băng hiện lại.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Rắn nhẹ trượt qua đoạn băng (chỉ số đoạn).</summary>
        public event Action<int> IceSlid;
        /// <summary>Đoạn băng vỡ dưới sức nặng của rắn.</summary>
        public event Action<int> IceBroken;
        /// <summary>Hoàn tác trả lại đoạn băng đã vỡ.</summary>
        public event Action<int> IceRestored;
        /// <summary>
        /// Đầu rắn sắp bước lên đoạn băng còn nguyên (đoạn, ô đầu tiên chạm, đủ nặng để phá): chỉ để vẽ hiệu ứng (vết nứt / lóe sáng),
        /// không đổi luật — băng vẫn vỡ khi tới điểm dừng trong đoạn.
        /// </summary>
        public event Action<int, Vector2Int, bool> IceTouched;

        private readonly List<IceSegment> _iceSegments = new List<IceSegment>();
        private readonly List<bool> _iceBroken = new List<bool>();
        private readonly Dictionary<Vector2Int, int> _iceStops = new Dictionary<Vector2Int, int>(); // điểm dừng bị băng nuốt → đoạn
        private readonly Dictionary<Vector2Int, int> _iceCells = new Dictionary<Vector2Int, int>(); // mọi ô băng → đoạn (chỉ cho hiệu ứng)
        private readonly HashSet<int> _touchedThisRun = new HashSet<int>();

        /// <summary>Các đoạn băng của màn (ô theo thứ tự, độ nặng).</summary>
        public IReadOnlyList<IceSegment> IceSegments => _iceSegments;

        public bool IceIntact(int segment) => !_iceBroken[segment];

        /// <summary>Điểm dừng đang bị băng còn nguyên nuốt: ô → độ nặng.</summary>
        public IReadOnlyDictionary<Vector2Int, int> Ice
        {
            get
            {
                var ice = new Dictionary<Vector2Int, int>();
                foreach (var stop in _iceStops) if (!_iceBroken[stop.Value]) ice[stop.Key] = _iceSegments[stop.Value].weight;
                return ice;
            }
        }

        // Lượt chạy hiện tại: điểm dừng băng đã trượt qua; trượt vòng về lại thì dừng ở đó, không chạy vòng mãi.
        private readonly HashSet<Vector2Int> _slidThisRun = new HashSet<Vector2Int>();
        private Vector2Int? _iceStop;

        private void SetUpIce(LevelDefinition level)
        {
            if (level.iceSegments == null) return;
            foreach (var segment in level.iceSegments)
            {
                int index = _iceSegments.Count;
                _iceSegments.Add(segment);
                _iceBroken.Add(false);
                foreach (var cell in segment.cells)
                {
                    _iceCells[cell] = index;
                    if (_board.IsStop(cell)) _iceStops[cell] = index;
                }
            }
        }

        private void ClearIceRun()
        {
            _slidThisRun.Clear();
            _iceStop = null;
            _touchedThisRun.Clear();
        }

        /// <summary>Đầu rắn vừa nhắm ô kế tiếp: là ô của đoạn băng còn nguyên thì báo chạm (một lần mỗi lượt), chỉ cho hiệu ứng.</summary>
        private void HeadingOntoIce(Vector2Int next)
        {
            if (IceTouched == null || !_iceCells.TryGetValue(next, out int segment) || _iceBroken[segment]) return;
            if (_touchedThisRun.Add(segment)) IceTouched(segment, next, _segments.Count >= _iceSegments[segment].weight);
        }

        private bool IceAt(Vector2Int cell, out int segment) => _iceStops.TryGetValue(cell, out segment) && !_iceBroken[segment];

        /// <summary>Điểm dừng băng còn nguyên mà rắn chưa đủ nặng: không dừng (hàm thuần, xem trước đường đi cũng dùng).</summary>
        private bool SlidesOver(Vector2Int cell) =>
            IceAt(cell, out int segment) && _segments.Count < _iceSegments[segment].weight && _iceStop != cell;

        /// <summary>
        /// Trượt trên băng: đi thẳng được thì đi thẳng (dọc đoạn băng), không thì rẽ trái của rắn (vào từ nhánh bên), rồi phải; cả ba đều
        /// không thì dừng.
        /// </summary>
        private bool SlideOn(Vector2Int cell, Direction heading, out Direction next)
        {
            next = heading;
            if (_board.CanMove(cell, heading)) return true;
            next = heading.TurnLeft();
            if (_board.CanMove(cell, next)) return true;
            next = heading.TurnRight();
            if (_board.CanMove(cell, next)) return true;
            next = heading;
            return false;
        }

        /// <summary>Rắn tới ô: điểm dừng băng mà đủ nặng thì cả đoạn vỡ (rồi dừng), nhẹ thì trượt qua; vòng về điểm đã trượt thì dừng.</summary>
        private void ArriveOnIce(Vector2Int cell)
        {
            if (!IceAt(cell, out int segment)) return;
            if (_segments.Count < _iceSegments[segment].weight)
            {
                if (_slidThisRun.Add(cell)) IceSlid?.Invoke(segment);
                else _iceStop = cell;
                return;
            }
            _iceBroken[segment] = true;
            IceBroken?.Invoke(segment);
        }

        private bool[] SaveIce() => _iceBroken.ToArray();

        /// <summary>Hoàn tác: đoạn băng vỡ trong lượt vừa rồi hiện lại.</summary>
        private void RestoreIce(bool[] before)
        {
            if (before == null) return;
            for (int i = 0; i < before.Length && i < _iceBroken.Count; i++)
            {
                if (before[i] || !_iceBroken[i]) continue;
                _iceBroken[i] = false;
                IceRestored?.Invoke(i);
            }
        }
    }
}
