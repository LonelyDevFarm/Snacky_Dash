using System;
using System.Collections.Generic;
using System.Linq;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Editor.LevelTools
{
    /// <summary>
    /// Người chơi thử "có suy nghĩ" [user 19/10]: không vuốt ngẫu nhiên, không Snip, không booster. Mỗi thế cờ thử từng hướng đi
    /// được trên bản chép nguyên ván (DeepCopy, cùng luật game nên ra đúng như thật), chấm điểm thế cờ sau nước đó rồi đi nước
    /// tốt nhất:
    /// 1. thắng; cắn đuôi hay hết đường đi là loại;
    /// 2. giao được nhiều trái (trái đã vào khay);
    /// 3. trái khay đang cần trong thân là tốt, trái không khay nào cần nữa là xấu (rắn dài, dễ cắn đuôi);
    /// 4. gần trái khay cần / gần khay giao được;
    /// 5. thế cờ đã gặp thì bỏ (khỏi đi vòng mãi).
    /// Gặp ngõ cụt thì lùi lại thử nước tốt kế tiếp (<see cref="Play"/>), như người chơi thua rồi chơi lại tránh chỗ sai.
    /// Thắng = màn có đường qua mà không cần booster nào.
    /// </summary>
    public sealed class SmartPlayer
    {
        private const float Step = 1f / 60f;
        private const int MaxTicks = 3600;

        private readonly Func<GameSession> _newGame;
        private readonly List<Direction> _moves = new List<Direction>();

        public int Moves => _moves.Count;
        public IReadOnlyList<Direction> Line => _moves;
        /// <summary>Vì sao thua (null khi thắng).</summary>
        public string Failure { get; private set; }
        /// <summary>Nhiều nhất bao nhiêu trái thừa (không khay nào cần) nằm trong thân cùng lúc (trên đường thắng đã tìm).</summary>
        public int MaxWaste { get; private set; }
        /// <summary>Số thế cờ đã thử (đo độ khó: màn dễ ~ bằng số nước, màn khó phải lùi lại nhiều).</summary>
        public int Nodes { get; private set; }
        /// <summary>Số lần lùi lại vì nhánh đang đi dẫn tới cắn đuôi / hết đường / thế cờ đã gặp.</summary>
        public int Backtracks { get; private set; }
        /// <summary>Thử tối đa bao nhiêu thế cờ rồi coi là thua.</summary>
        public int MaxNodes { get; set; } = 3000;

        /// <summary>Mỗi nước thử dẫn tới cắn đuôi / hết đường: thế cờ ngay trước nước đó và ván sau nước đó (LevelAnalysis ghi chỗ chết).</summary>
        public event Action<GameSession, GameSession> Died;

        /// <param name="newGame">Tạo ván mới đã Begin (cùng hạt), như LevelGenerator.Validate.</param>
        public SmartPlayer(Func<GameSession> newGame) => _newGame = newGame;

        /// <summary>Tìm đường thắng dài tối đa <paramref name="maxMoves"/> nước; <see cref="Line"/> là đường đó.</summary>
        public bool Play(int maxMoves)
        {
            _moves.Clear();
            Nodes = 1;
            Backtracks = 0;
            MaxWaste = 0;
            var root = _newGame();
            if (root.IsWon) { Failure = null; return true; }
            var stack = new List<Frame> { new Frame(root, Children(root)) };
            var seen = new HashSet<string> { Key(root) };
            var waste = new List<int>();
            while (stack.Count > 0 && Nodes < MaxNodes)
            {
                var top = stack[stack.Count - 1];
                if (top.Next >= top.Options.Count || stack.Count > maxMoves)
                {
                    stack.RemoveAt(stack.Count - 1);
                    if (_moves.Count > 0) { _moves.RemoveAt(_moves.Count - 1); waste.RemoveAt(waste.Count - 1); }
                    Backtracks++;
                    continue;
                }
                var (move, game, score) = top.Options[top.Next];
                top.Options[top.Next] = (move, null, score); // thả bản chép đã dùng
                top.Next++;
                if (score <= -1e8) { Died?.Invoke(top.From, game); continue; }
                if (!seen.Add(Key(game))) continue;
                Nodes++;
                _moves.Add(move);
                waste.Add(Waste(game));
                if (game.IsWon)
                {
                    Failure = null;
                    MaxWaste = waste.Max();
                    return true;
                }
                stack.Add(new Frame(Died != null ? game : null, Children(game)));
            }
            _moves.Clear();
            Failure = Nodes >= MaxNodes ? $"thử quá {MaxNodes} thế cờ chưa thấy đường thắng" : "mọi đường đều dẫn tới cắn đuôi / hết đường";
            return false;
        }

        /// <summary>Các nước đi được từ <paramref name="from"/> (đã đi thử trên bản chép), nước điểm cao trước.</summary>
        private List<(Direction move, GameSession game, double score)> Children(GameSession from)
        {
            var list = new List<(Direction move, GameSession game, double score)>();
            foreach (var d in DirectionExtensions.All.Where(from.CanGo))
            {
                var game = DeepCopy.Of(from);
                if (Apply(game, d)) list.Add((d, game, Score(game)));
            }
            list.Sort((a, b) => b.score.CompareTo(a.score));
            return list;
        }

        private sealed class Frame
        {
            public readonly List<(Direction move, GameSession game, double score)> Options;
            /// <summary>Thế cờ của khung này (chỉ giữ khi có người nghe <see cref="Died"/>, đỡ tốn bộ nhớ).</summary>
            public readonly GameSession From;
            public int Next;
            public Frame(GameSession from, List<(Direction move, GameSession game, double score)> options) { From = from; Options = options; }
        }

        private static bool Apply(GameSession game, Direction d)
        {
            Run(game, () => game.IsDelivering || game.IsRewinding);
            if (!game.RequestMove(d)) return false;
            Run(game, () => game.IsMoving || game.IsDelivering);
            return true;
        }

        private static void Run(GameSession game, Func<bool> condition)
        {
            for (int i = 0; i < MaxTicks && condition(); i++) game.Tick(Step);
        }

        // ------------------------------------------------------------------ chấm điểm

        private static double Score(GameSession game)
        {
            if (game.IsWon) return 1e9;
            if (game.IsStuck) return -1e9;
            if (!DirectionExtensions.All.Any(game.CanGo)) return -1e8;
            var needNow = new HashSet<PillKind>();
            var needLater = new HashSet<PillKind>();
            int delivered = 0;
            for (int c = 0; c < game.Columns.Count; c++)
            {
                var column = game.Columns[c];
                for (int i = 0; i < column.CurrentIndex && i < column.Crates.Count; i++) delivered += column.Crates[i].requiredCount;
                if (column.IsComplete) continue;
                delivered += column.Filled;
                if (column.IsExtra && game.ExtraLocked) continue;
                needNow.Add(column.Current.kind);
                for (int i = column.CurrentIndex; i < column.Crates.Count; i++) needLater.Add(column.Crates[i].kind);
            }
            int useful = game.Segments.Count(k => needNow.Contains(k));
            int later = game.Segments.Count(k => !needNow.Contains(k) && needLater.Contains(k));
            int waste = game.Segments.Count(k => !needLater.Contains(k));
            // Chỗ trống của các khay đang nhận (theo loại): mang đủ lấp một khay thì đi giao.
            var room = new Dictionary<PillKind, int>();
            foreach (var column in game.Columns)
            {
                if (column.IsComplete || (column.IsExtra && game.ExtraLocked)) continue;
                int left = column.Current.requiredCount - column.Filled;
                room[column.Current.kind] = (room.TryGetValue(column.Current.kind, out int r) ? r : 0) + left;
            }
            bool filling = room.Any(pair => game.Segments.Count(k => k == pair.Key) >= pair.Value);
            bool pillNeeded = game.Pills.Values.Any(needNow.Contains);
            // Người chơi thật: mang đủ cho một khay, thân đã dài, hay hết trái cần ăn thì đi giao; không thì đi ăn trái khay cần.
            bool deliver = useful > 0 && (filling || game.Segments.Count >= 8 || !pillNeeded);
            double score = delivered * 1000.0 + later * 5.0 - waste * 80.0 - game.Segments.Count * 20.0 + (deliver ? 0 : useful * 30.0);
            int distance = Distance(game, cell =>
                deliver ? game.Board.CrateColumnAt(cell) is int col && col >= 0 && game.CanDeliverTo(col)
                        : pillNeeded ? game.Pills.TryGetValue(cell, out var kind) && needNow.Contains(kind)
                                     : game.Pills.ContainsKey(cell));
            score -= (distance < 0 ? 60 : distance) * 10.0;
            return score;
        }

        private static int Waste(GameSession game)
        {
            var needLater = new HashSet<PillKind>();
            foreach (var column in game.Columns)
                for (int i = column.CurrentIndex; i < column.Crates.Count; i++) needLater.Add(column.Crates[i].kind);
            return game.Segments.Count(k => !needLater.Contains(k));
        }

        /// <summary>Số ô (theo đường đi được) từ đầu rắn tới ô gần nhất thoả <paramref name="target"/>; -1 khi không tới được.</summary>
        private static int Distance(GameSession game, Func<Vector2Int, bool> target)
        {
            var board = game.Board;
            var seen = new HashSet<Vector2Int> { game.CurrentCell };
            var queue = new Queue<(Vector2Int cell, int steps)>();
            queue.Enqueue((game.CurrentCell, 0));
            while (queue.Count > 0)
            {
                var (cell, steps) = queue.Dequeue();
                if (steps > 0 && target(cell)) return steps;
                foreach (var d in DirectionExtensions.All)
                {
                    if (!board.CanMove(cell, d)) continue;
                    var next = cell + d.ToCell();
                    if (seen.Add(next)) queue.Enqueue((next, steps + 1));
                }
            }
            return -1;
        }

        /// <summary>Thế cờ: ô đầu, hướng, thân (loại và chỗ từng đốt — dáng thân quyết định có cắn đuôi không), trái trên bàn, đợt,
        /// tiến độ các khay.</summary>
        private static string Key(GameSession game)
        {
            var body = Enumerable.Range(0, game.Segments.Count).Select(k =>
            {
                var cell = Vector2Int.RoundToInt(game.SegmentPosition(k));
                return $"{(int)game.Segments[k]}{cell.x}.{cell.y}";
            });
            var pills = game.Pills.OrderBy(p => p.Key.x).ThenBy(p => p.Key.y).Select(p => $"{p.Key.x}.{p.Key.y}{(int)p.Value}");
            return $"{game.CurrentCell.x},{game.CurrentCell.y},{(int)game.Heading}|{string.Join(";", body)}|{string.Join(";", pills)}|{game.BatchIndex}|{string.Join(",", game.Columns.Select(c => c.CurrentIndex * 100 + c.Filled))}";
        }
    }
}
