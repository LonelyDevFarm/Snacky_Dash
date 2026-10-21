using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Phần booster của GameSession: undo, nam châm, xẻng (boomerang ở GameSession.cs); không dùng được khi đã cắn đuôi [user].
    /// Thanh booster gọi các hàm Use*/Undo; số lượng booster do người gọi lo.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Không chạy, không giao, không cắn đuôi, chưa thắng, không đang undo: được dùng booster.</summary>
        public bool IsIdle => !IsMoving && !IsDelivering && !IsStuck && !IsWon && !IsRewinding;

        // ------------------------------------------------------------------ Undo

        /// <summary>Đã undo lượt vuốt cuối: view dựng lại rắn và trái (crate giữ nguyên).</summary>
        public event Action Rewound;

        /// <summary>Đường của đầu trong lần undo cuối, từ ô được trả về (đầu danh sách) tới chỗ cũ (cuối).</summary>
        public IReadOnlyList<Vector2> RewindPath { get; private set; } = new List<Vector2>();

        /// <summary>Theo từng điểm RewindPath: đoạn tới điểm đó đi trong hầm (chạy lùi cũng qua hầm) [user].</summary>
        public IReadOnlyList<bool> RewindUnderground { get; private set; } = new List<bool>();

        /// <summary>Trái lượt vuốt đã ăn sẽ rơi khỏi đuôi khi lùi, theo thứ tự ăn, kèm quãng trên RewindPath lúc ăn.</summary>
        public IReadOnlyList<(PillKind kind, float along)> RewindDropped { get; private set; } = new List<(PillKind, float)>();

        /// <summary>View còn đang chạy lùi rắn sau undo: vuốt và booster chờ xong [user].</summary>
        public bool IsRewinding => _rewindTime > 0f;
        private float _rewindTime;

        /// <summary>Tốc độ chạy lùi khi undo, ô/giây (VFX_hoantac ~12, chỉnh theo điện thoại [user 08/10]).</summary>
        public const float RewindSpeed = 16f;

        private sealed class Snapshot
        {
            public Vector2 head;
            public Vector2Int cell, target;
            public Direction heading;
            public List<PillKind> segments;
            public List<Vector2> trail;
            public List<float> trailJump;
            public Dictionary<Vector2Int, PillKind> pills;
            public HashSet<Vector2Int> secret;
            public Dictionary<int, Vector2Int> arrowEntered;
            public int batch;
            public (HashSet<Vector2Int> closedWalls, Dictionary<Vector2Int, Direction> ways, HashSet<Vector2Int> risen) board;
            public MechanicsState mechanics;
            public bool[] ice;
        }

        // Chỉ nhớ lượt vuốt mới nhất: undo xong thì không lùi tiếp được tới lượt vuốt sau.
        private Snapshot _beforeLastMove;
        private readonly List<PillKind> _deliveredSinceSwipe = new List<PillKind>();
        // Đường đầu đi trong lượt vuốt này (không dùng _trail vì nó bị cắt theo độ dài thân) và trái đã ăn trên đó.
        private readonly List<Vector2> _swipePath = new List<Vector2>();
        private readonly List<bool> _swipeUnderground = new List<bool>(); // theo từng điểm: đoạn tới đó đi trong hầm
        private readonly List<(Vector2Int cell, PillKind kind)> _eatenSinceSwipe = new List<(Vector2Int, PillKind)>();

        /// <summary>
        /// Nút Undo sáng: có lượt vuốt để quay lại. Mờ ngay sau một undo hay booster khác và khi nam châm đang bật,
        /// tới lượt vuốt mới [user]; không mờ chỉ vì rắn đang chạy (nút nhấp nháy mỗi lượt, user 15/10).
        /// </summary>
        public bool UndoReady => _beforeLastMove != null && !_magnetOn && !_ghostOn;

        /// <summary>Undo dùng được ngay: nút sáng và rắn đang đứng yên.</summary>
        public bool CanUndo => UndoReady && IsIdle;

        private void ForgetUndo() => _beforeLastMove = null;

        /// <summary>Gọi khi lượt vuốt bắt đầu chạy: lưu trạng thái để undo quay về.</summary>
        private void SaveForUndo()
        {
            _beforeLastMove = new Snapshot
            {
                head = HeadPosition, cell = CurrentCell, target = _target, heading = Heading,
                segments = new List<PillKind>(_segments),
                trail = new List<Vector2>(_trail), trailJump = new List<float>(_trailJump),
                pills = new Dictionary<Vector2Int, PillKind>(_pills), secret = new HashSet<Vector2Int>(_secret),
                arrowEntered = new Dictionary<int, Vector2Int>(_arrowEntered),
                batch = BatchIndex, board = _board.SaveState(), mechanics = SaveMechanics(), ice = SaveIce(),
            };
            _deliveredSinceSwipe.Clear();
            _swipePath.Clear();
            _swipePath.Add(HeadPosition);
            _swipeUnderground.Clear();
            _swipeUnderground.Add(false);
            _eatenSinceSwipe.Clear();
        }

        /// <summary>
        /// Trả rắn và trái đã ăn về như trước lượt vuốt cuối. Trái đã giao vẫn nằm trong crate; crate và đá giữ như hiện tại [user].
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            var s = _beforeLastMove;
            // View chạy lùi rắn dần theo đường này [user].
            var path = new List<Vector2>(_swipePath) { HeadPosition };
            RewindPath = path;
            RewindUnderground = new List<bool>(_swipeUnderground) { false };
            float length = 0f;
            for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
            _rewindTime = length / RewindSpeed;
            var segments = new List<PillKind>(s.segments);
            var pills = new Dictionary<Vector2Int, PillKind>(s.pills);
            foreach (var kind in _deliveredSinceSwipe)
            {
                int index = segments.LastIndexOf(kind);
                if (index >= 0) { segments.RemoveAt(index); continue; }
                // Ăn trong lượt này rồi giao luôn: không trả lại lên bàn.
                foreach (var pill in s.pills)
                    if (pill.Value == kind && pills.ContainsKey(pill.Key) && !_pills.ContainsKey(pill.Key)) { pills.Remove(pill.Key); break; }
            }
            HeadPosition = s.head; CurrentCell = s.cell; _target = s.target; Heading = s.heading;
            _segments.Clear(); _segments.AddRange(segments);
            _trail.Clear(); _trail.AddRange(s.trail);
            _trailJump.Clear(); _trailJump.AddRange(s.trailJump);
            _pills.Clear(); foreach (var pill in pills) _pills[pill.Key] = pill.Value;
            // Trái trả lại bàn rơi khỏi đuôi khi đầu lùi qua ô đó (VFX_hoantac).
            var dropped = new List<(PillKind, float)>();
            foreach (var eaten in _eatenSinceSwipe)
            {
                if (!_pills.ContainsKey(eaten.cell)) continue;
                float along = 0f;
                for (int i = 1; i < path.Count; i++)
                {
                    along += Vector2.Distance(path[i - 1], path[i]);
                    if (Vector2Int.RoundToInt(path[i]) == eaten.cell) break;
                }
                dropped.Add((eaten.kind, along));
            }
            RewindDropped = dropped;
            _eatenSinceSwipe.Clear();
            _secret.Clear(); foreach (var cell in s.secret) if (_pills.ContainsKey(cell)) _secret.Add(cell);
            _arrowEntered.Clear(); foreach (var a in s.arrowEntered) _arrowEntered[a.Key] = a.Value;
            BatchIndex = s.batch;
            var now = _board.SaveState();
            _board.RestoreState((now.closedWalls, s.board.ways, now.risen)); // mũi tên trả về, đá giữ như hiện tại
            RestoreMechanics(s.mechanics, s.board.risen); // trả về số đếm rèm và khối nâng
            RestoreIce(s.ice); // băng vỡ trong lượt này hiện lại
            _deliveredSinceSwipe.Clear();
            _bufferedSwipe = null;
            _tunnelLeft = _tunnelWait = 0f;
            _beforeLastMove = null;
            Rewound?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Nam châm

        /// <summary>Giây giữa hai lần hút (gốc: BoosterData._magnetPullIntervalSeconds 0.05).</summary>
        public const float MagnetPullInterval = 0.05f;
        /// <summary>Nam châm kéo dài 5 lượt vuốt, tắt khi lượt thứ 5 chạy xong (video booster, [user]).</summary>
        public const int MagnetSwipes = 5;
        /// <summary>Bán kính hút, ô (gốc là remote config; video booster ~5 ô [user]).</summary>
        public const float MagnetRadius = 5f;

        public event Action<bool> MagnetChanged;
        public event Action<Vector2Int, PillKind, int, int> PillMagnetized; // ô, loại, cột, crate

        private bool _magnetOn;
        private int _magnetSwipesLeft;
        private float _magnetTimer;

        public bool MagnetActive => _magnetOn;

        public bool UseMagnet()
        {
            if (!IsIdle || _magnetOn) return false;
            _magnetOn = true;
            _magnetSwipesLeft = MagnetSwipes;
            _magnetTimer = 0f;
            ForgetUndo();
            MagnetChanged?.Invoke(true);
            return true;
        }

        private void TickMagnet(float deltaTime)
        {
            if (!_magnetOn || IsStuck || IsWon) return;
            // Chỉ hút khi đầu đang chạy bằng vuốt, như bản gốc; chuyến xẻng không tính,
            // vì hút ngay khi xẻng trồi lên thì xẻng + nam châm dọn quá nhiều miễn phí [user].
            if (!IsMoving || _stopOnArrival) { _magnetTimer = 0f; return; }
            _magnetTimer += deltaTime;
            while (_magnetTimer >= MagnetPullInterval)
            {
                _magnetTimer -= MagnetPullInterval;
                // Hút hết trái trong tầm, gần trước [user, Level 16]; mỗi nhịp một trái thì sót các làn bên cạnh.
                int pulled = 0;
                while (true)
                {
                    int before = _pills.Count;
                    if (!PullNearestFruit()) break;
                    pulled++;
                    if (_pills.Count >= before) break; // trái vừa hút làm ra đợt mới: chờ đợt đó rơi xuống trước
                }
                if (pulled == 0) break;
            }
        }

        /// <summary>Trái gần nhất trong tầm mà còn crate nhận thì bay vào crate đó.</summary>
        private bool PullNearestFruit()
        {
            Vector2Int? best = null;
            float bestDistance = MagnetRadius;
            foreach (var pill in _pills)
            {
                float d = Vector2.Distance(pill.Key, HeadPosition);
                if (d > bestDistance || !CrateTakes(pill.Value)) continue;
                bestDistance = d;
                best = pill.Key;
            }
            if (!best.HasValue) return false;
            var cell = best.Value;
            var kind = _pills[cell];
            if (!TryReserveCrate(kind, out int column, out int crate, allowClosed: false)) return false; // nam châm không hút vào khay đóng
            _pills.Remove(cell);
            _secret.Remove(cell);
            PillMagnetized?.Invoke(cell, kind, column, crate);
            if (_pills.Count == 0) SpawnNextBatch();
            if (IsIdle) StartSettlingFullCrates(); // hút lúc đứng yên: crate đầy hoàn thành luôn
            return true;
        }

        private bool CrateTakes(PillKind kind)
        {
            foreach (var column in Columns)
                for (int crate = column.CurrentIndex; crate < column.Crates.Count; crate++)
                    if (column.Crates[crate].kind == kind && !column.IsFull(crate)) return true;
            return false;
        }

        private void OnMoveStartedForBoosters()
        {
            if (_magnetOn) _magnetSwipesLeft--;
        }

        /// <summary>Rắn dừng: tắt nam châm sau lượt thứ 5; crate nó lấp đầy thì hoàn thành.</summary>
        private void OnStoppedForBoosters()
        {
            if (_magnetOn && _magnetSwipesLeft <= 0)
            {
                _magnetOn = false;
                ForgetUndo(); // các lượt vừa rồi chạy với nam châm: undo chờ lượt vuốt mới
                MagnetChanged?.Invoke(false);
            }
            if (IsIdle) StartSettlingFullCrates();
        }

        // ------------------------------------------------------------------ Xẻng

        /// <summary>Thân xuống hết thì khoảng 1 s sau đầu mới trồi lên (video booster).</summary>
        public const float ShovelUndergroundTime = 1f;

        public event Action<Vector2Int, Vector2Int> ShovelDug; // từ (điểm dừng của đầu), tới (điểm dừng được chọn)

        private bool _stopOnArrival;

        /// <summary>Điểm dừng xẻng đào tới được: không phải điểm giao crate [user], trừ chỗ đầu và thân đang nằm.</summary>
        public List<Vector2Int> ShovelTargets()
        {
            var targets = new List<Vector2Int>();
            if (!IsIdle) return targets;
            var body = new HashSet<Vector2Int>();
            for (int i = 0; i < _segments.Count; i++)
                if (!SegmentInTunnel(i)) body.Add(Vector2Int.RoundToInt(SegmentPosition(i)));
            foreach (var cell in _board.StopCells)
                if (cell != CurrentCell && !body.Contains(cell) && !_board.IsCrateStopCell(cell) && !IceAt(cell, out _)) targets.Add(cell); // không đào lên mặt băng
            return targets;
        }

        /// <summary>
        /// Đào xuống tại chỗ đầu và trồi lên ở <paramref name="to"/>, như đường hầm: thân trượt xuống, sau
        /// <see cref="ShovelUndergroundTime"/> đầu trồi lên và đứng chờ; thân ra theo khi rắn chạy tiếp.
        /// </summary>
        public bool UseShovel(Vector2Int to)
        {
            if (!ShovelTargets().Contains(to)) return false;
            var from = CurrentCell;
            ForgetUndo();
            ShovelDug?.Invoke(from, to);
            CurrentCell = to;
            HeadPosition = to;
            _trail.Insert(0, to);
            _trailJump.Insert(0, 0f); // dài dần khi thân đi xuống (Advance)
            _tunnelLeft = (_segments.Count + 1) * SegmentSpacing;
            _tunnelWait = ShovelUndergroundTime;
            _target = to;
            _stopOnArrival = true;
            IsMoving = true;
            return true;
        }
    }
}
