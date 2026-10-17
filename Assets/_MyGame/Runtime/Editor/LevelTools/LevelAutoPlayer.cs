using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Người chơi thử tự động: chơi một màn tới thắng (hoặc hết lượt) để kiểm màn thắng được. Dùng chung cho test
    /// Level_IsWinnable và Level Generator. Một nửa số lượt vuốt nhắm đích (khay giao được, trái gần nhất, vạch công tắc khi
    /// kẹt lâu, cắn đuôi khi kẹt rất lâu để Snip), nửa còn lại ngẫu nhiên; ngõ cụt thì hoàn tác; cắn đuôi thì Snip.
    /// </summary>
    public sealed class LevelAutoPlayer
    {
        public const int DefaultMaxMoves = 3000;
        private const float Step = 1f / 60f;
        private const int MaxTicks = 3600;

        /// <summary>Ngay trước mỗi lượt vuốt.</summary>
        public event Action BeforeMove;
        /// <summary>Sau mỗi tick lúc rắn chạy hay đang giao.</summary>
        public event Action Ticked;
        /// <summary>Sau khi một lượt vuốt chạy xong (rắn đã dừng).</summary>
        public event Action Moved;

        public int Moves { get; private set; }
        public int Snips { get; private set; }
        public int Boomerangs { get; private set; }

        /// <summary>
        /// Chơi tới thắng hoặc hết <paramref name="maxMoves"/> lượt. <paramref name="boomerangWhenNoFruit"/>: màn hướng dẫn
        /// boomerang (khay ngoài tầm), hết trái trên bàn thì ném boomerang. Session phải đã Begin.
        /// </summary>
        public bool Play(GameSession session, int seed, bool boomerangWhenNoFruit = false, int maxMoves = DefaultMaxMoves)
        {
            var random = new System.Random(seed);
            int stall = 0; // lượt vuốt chưa ăn, giao hay cắt được gì
            Action<Vector2Int, LevelSystem.PillKind, int> ate = (c, k, i) => stall = 0;
            Action<int, LevelSystem.PillKind, int> delivered = (i, k, c) => stall = 0;
            Action<int, LevelSystem.PillKind, int, int> snipped = (i, k, c, crate) => stall = 0;
            session.PillEaten += ate;
            session.SegmentDelivered += delivered;
            session.SegmentSnipped += snipped;
            try
            {
                for (int move = 0; move < maxMoves && !session.IsWon; move++)
                {
                    // Cắn đuôi: Snip rồi chơi tiếp; khay Snip làm đầy xong rồi rắn mới đi.
                    if (session.IsStuck && session.Snip()) Snips++;
                    RunWhile(session, () => session.IsDelivering);
                    if (boomerangWhenNoFruit && session.Pills.Count == 0)
                        for (int c = 0; c < session.Columns.Count; c++)
                            if (session.UseBoomerang(c))
                            {
                                Boomerangs++;
                                RunWhile(session, () => session.IsDelivering);
                            }
                    // Ngõ cụt (nhánh bị đá chặn) mà thân ở sau: hoàn tác như người chơi.
                    if (session.IsIdle && !DirectionExtensions.All.Any(session.CanGo) && session.CanUndo)
                    {
                        session.Undo();
                        RunWhile(session, () => session.IsRewinding);
                        continue;
                    }
                    BeforeMove?.Invoke();
                    var aimed = random.NextDouble() < 0.5 ? Aim(session, stall) : null;
                    if (!session.RequestMove(aimed ?? (Direction)random.Next(4))) continue;
                    Moves++;
                    stall++;
                    // Combo dài (1 s mỗi khay đầy) có thể mất vài giây.
                    for (int i = 0; i < MaxTicks && (session.IsMoving || session.IsDelivering); i++)
                    {
                        session.Tick(Step);
                        Ticked?.Invoke();
                    }
                    Moved?.Invoke();
                }
            }
            finally
            {
                session.PillEaten -= ate;
                session.SegmentDelivered -= delivered;
                session.SegmentSnipped -= snipped;
            }
            return session.IsWon;
        }

        private static void RunWhile(GameSession session, Func<bool> condition)
        {
            for (int i = 0; i < MaxTicks && condition(); i++) session.Tick(Step);
        }

        /// <summary>
        /// Đích của lượt vuốt: khay giao được, không thì trái gần nhất; kẹt lâu (có cầu bật / ống rẽ) thì vạch công tắc; kẹt rất
        /// lâu thì cắn đuôi cho Snip (màn 273, 310). Trả về hướng vuốt đầu của đường ngắn nhất, hoặc null.
        /// </summary>
        public static Direction? Aim(GameSession session, int stall)
        {
            var board = session.Board;
            bool Deliverable(Vector2Int cell) => board.CrateColumnAt(cell) is int column && column >= 0 && session.CanDeliverTo(column);
            if (stall > 40 && (board.Turnouts.Any() || board.SwitchBridges.Count > 0) && FirstStep(session, board.IsSwitch) is Direction toSwitch)
                return toSwitch;
            if (stall > 80 && FirstStep(session, cell => Enumerable.Range(0, session.Segments.Count)
                    .Any(k => Vector2Int.RoundToInt(session.SegmentPosition(k)) == cell)) is Direction toBody)
                return toBody;
            return FirstStep(session, Deliverable) ?? FirstStep(session, cell => session.Pills.ContainsKey(cell));
        }

        /// <summary>Hướng vuốt đầu tiên của đường ngắn nhất (theo đường đi được) tới ô thoả <paramref name="isTarget"/>.</summary>
        public static Direction? FirstStep(GameSession session, Func<Vector2Int, bool> isTarget)
        {
            var board = session.Board;
            var first = new Dictionary<Vector2Int, Direction>();
            var queue = new Queue<Vector2Int>();
            foreach (var direction in DirectionExtensions.All)
            {
                if (!session.CanGo(direction)) continue;
                var next = session.CurrentCell + direction.ToCell();
                if (first.ContainsKey(next)) continue;
                first[next] = direction;
                queue.Enqueue(next);
            }
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (isTarget(cell)) return first[cell];
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = cell + direction.ToCell();
                    if (next == session.CurrentCell || first.ContainsKey(next) || !board.CanMove(cell, direction)) continue;
                    first[next] = first[cell];
                    queue.Enqueue(next);
                }
            }
            return null;
        }
    }
}
