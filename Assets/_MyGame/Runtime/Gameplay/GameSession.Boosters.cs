using System;
using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// Booster rules besides the Boomerang (GameSession.UseBoomerang) [booster video, user]:
    /// Undo rewinds the last swipe (any number of times, but only the latest swipe is remembered, so
    /// never twice in a row) [user], Magnet pulls fruit near the head into the crates for five swipes
    /// [user] (_magnetPullIntervalSeconds 0.05), Shovel digs from the
    /// head's stop to another stop. None of them works once the tail is bitten (that is a loss) [user].
    /// Stock is the caller's job.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>No move, delivery, bite or win in progress: a booster may be used.</summary>
        public bool IsIdle => !IsMoving && !IsDelivering && !IsStuck && !IsWon && !IsRewinding;

        // ------------------------------------------------------------------ Undo

        /// <summary>The last swipe was rewound: views rebuild the snake and the fruit (crates stay).</summary>
        public event Action Rewound;

        /// <summary>Last undo: the head's way from its restored cell (first) to where it was (last).</summary>
        public IReadOnlyList<Vector2> RewindPath { get; private set; } = new List<Vector2>();

        /// <summary>Per RewindPath point: the way to it ran inside a tunnel (the snake runs back through the tube too) [user].</summary>
        public IReadOnlyList<bool> RewindUnderground { get; private set; } = new List<bool>();

        /// <summary>Last undo: fruit the swipe ate that drops off the tail again, in eating order, with how far along RewindPath it was eaten.</summary>
        public IReadOnlyList<(PillKind kind, float along)> RewindDropped { get; private set; } = new List<(PillKind, float)>();

        /// <summary>Views are still running the snake back after an undo: swipes and boosters wait until it is done [user].</summary>
        public bool IsRewinding => _rewindTime > 0f;
        private float _rewindTime;

        /// <summary>How fast views run the snake back on undo (cells per second; VFX_hoantac measured about 12, the user found that a bit slow and 20 too fast; settled on 18; on the phone 18 still looked fast, then 14 felt slow, now 16 [user 08/10]).</summary>
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
            public (HashSet<Vector2Int>, Dictionary<Vector2Int, Direction>) board;
        }

        // Only the latest swipe is kept: after an undo there is nothing more to go back to until the next swipe.
        private Snapshot _beforeLastMove;
        private readonly List<PillKind> _deliveredSinceSwipe = new List<PillKind>();
        // The head's way on this swipe (the trail is cut to the body's length, so it cannot be used) and the fruit eaten on it.
        private readonly List<Vector2> _swipePath = new List<Vector2>();
        private readonly List<bool> _swipeUnderground = new List<bool>(); // per point: the way to it ran inside a tunnel
        private readonly List<(Vector2Int cell, PillKind kind)> _eatenSinceSwipe = new List<(Vector2Int, PillKind)>();

        /// <summary>
        /// Undo is greyed out right after an undo or any other booster, and while the magnet works: it
        /// needs a fresh swipe first [user].
        /// </summary>
        public bool CanUndo => _beforeLastMove != null && IsIdle && !_magnetOn;

        private void ForgetUndo() => _beforeLastMove = null;

        /// <summary>Called as a swipe starts a move: the state Undo goes back to.</summary>
        private void SaveForUndo()
        {
            _beforeLastMove = new Snapshot
            {
                head = HeadPosition, cell = CurrentCell, target = _target, heading = Heading,
                segments = new List<PillKind>(_segments),
                trail = new List<Vector2>(_trail), trailJump = new List<float>(_trailJump),
                pills = new Dictionary<Vector2Int, PillKind>(_pills), secret = new HashSet<Vector2Int>(_secret),
                arrowEntered = new Dictionary<int, Vector2Int>(_arrowEntered),
                batch = BatchIndex, board = _board.SaveState(),
            };
            _deliveredSinceSwipe.Clear();
            _swipePath.Clear();
            _swipePath.Add(HeadPosition);
            _swipeUnderground.Clear();
            _swipeUnderground.Add(false);
            _eatenSinceSwipe.Clear();
        }

        /// <summary>
        /// Puts the snake and the fruit it ate back as before the last swipe. Fruit already delivered
        /// stays in its crate [user]: crates, rocks and the delivered segments are kept as they are now.
        /// </summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            var s = _beforeLastMove;
            // The way the head came on this swipe, from where it goes back to (0) to where it is now:
            // views run the snake back along it [user: undo rewinds gradually].
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
                // Eaten on this swipe and delivered too: it does not come back to the board.
                foreach (var pill in s.pills)
                    if (pill.Value == kind && pills.ContainsKey(pill.Key) && !_pills.ContainsKey(pill.Key)) { pills.Remove(pill.Key); break; }
            }
            HeadPosition = s.head; CurrentCell = s.cell; _target = s.target; Heading = s.heading;
            _segments.Clear(); _segments.AddRange(segments);
            _trail.Clear(); _trail.AddRange(s.trail);
            _trailJump.Clear(); _trailJump.AddRange(s.trailJump);
            _pills.Clear(); foreach (var pill in pills) _pills[pill.Key] = pill.Value;
            // Eaten fruit that goes back to the board drops off the tail as the head runs back over it [VFX_hoantac].
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
            _board.RestoreState((_board.SaveState().closedWalls, s.board.Item2)); // arrows back, rocks as now
            _deliveredSinceSwipe.Clear();
            _bufferedSwipe = null;
            _tunnelLeft = _tunnelWait = 0f;
            _beforeLastMove = null;
            Rewound?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ Magnet

        /// <summary>BoosterData._magnetPullIntervalSeconds: one fruit pulled this often.</summary>
        public const float MagnetPullInterval = 0.05f;
        /// <summary>The field lasts five swipes and goes once the fifth has run [user, booster video].</summary>
        public const int MagnetSwipes = 5;
        /// <summary>
        /// Remote config (MagnetBoosterConfig.Radius) in the original, so no value in the build; booster.mp4
        /// at 18.7 s: the field reaches about five cells from the head [user: 1.6 and 2.5 were too small].
        /// </summary>
        public const float MagnetRadius = 5f;

        public event Action<bool> MagnetChanged;
        public event Action<Vector2Int, PillKind, int, int> PillMagnetized; // cell, kind, column, crate index

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
            _magnetTimer += deltaTime;
            while (_magnetTimer >= MagnetPullInterval)
            {
                _magnetTimer -= MagnetPullInterval;
                // Every fruit in reach goes, nearest first [user: all in range, Level 16]. One per interval was too
                // few: at 26 cells/s the lane under the head used every pull and the lanes beside it were missed.
                int pulled = 0;
                while (true)
                {
                    int before = _pills.Count;
                    if (!PullNearestFruit()) break;
                    pulled++;
                    if (_pills.Count >= before) break; // the last one brought the next wave: it lands first
                }
                if (pulled == 0) break;
            }
        }

        /// <summary>The nearest fruit in reach whose kind a crate still takes flies into that crate.</summary>
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
            if (!TryReserveCrate(kind, out int column, out int crate)) return false;
            _pills.Remove(cell);
            _secret.Remove(cell);
            PillMagnetized?.Invoke(cell, kind, column, crate);
            if (_pills.Count == 0) SpawnNextBatch();
            if (IsIdle) StartSettlingFullCrates(); // pulled while standing: a filled crate completes now
            return true;
        }

        private bool CrateTakes(PillKind kind)
        {
            foreach (var column in Columns)
                for (int crate = column.CurrentIndex; crate < column.Crates.Count; crate++)
                    if (column.Crates[crate].kind == kind && !column.IsFull(crate)) return true;
            return false;
        }

        /// <summary>A swipe started: it uses up one magnet swipe.</summary>
        private void OnMoveStartedForBoosters()
        {
            if (_magnetOn) _magnetSwipesLeft--;
        }

        /// <summary>The snake stopped: the field goes after its fifth swipe; crates it filled complete.</summary>
        private void OnStoppedForBoosters()
        {
            if (_magnetOn && _magnetSwipesLeft <= 0)
            {
                _magnetOn = false;
                ForgetUndo(); // the swipes ran with the magnet on: undo waits for a fresh one
                MagnetChanged?.Invoke(false);
            }
            if (IsIdle) StartSettlingFullCrates();
        }

        // ------------------------------------------------------------------ Shovel

        /// <summary>Booster video: after the body has gone down, the head comes up about a second later.</summary>
        public const float ShovelUndergroundTime = 1f;

        public event Action<Vector2Int, Vector2Int> ShovelDug; // from (the head's stop), to (the chosen stop)

        private bool _stopOnArrival;

        /// <summary>Stops the shovel can dig to [user: stop squares, not delivery points]: all but the head's and the body's.</summary>
        public List<Vector2Int> ShovelTargets()
        {
            var targets = new List<Vector2Int>();
            if (!IsIdle) return targets;
            var body = new HashSet<Vector2Int>();
            for (int i = 0; i < _segments.Count; i++)
                if (!SegmentInTunnel(i)) body.Add(Vector2Int.RoundToInt(SegmentPosition(i)));
            foreach (var cell in _board.StopCells)
                if (cell != CurrentCell && !body.Contains(cell) && !_board.IsCrateStopCell(cell)) targets.Add(cell); // not delivery points [user]
            return targets;
        }

        /// <summary>
        /// Digs down at the head's stop and comes up at <paramref name="to"/>, like a tunnel the length of
        /// the snake: the body slides down (tunnel speed), the head comes up after
        /// <see cref="ShovelUndergroundTime"/> and waits there; the body follows out as it moves on.
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
            _trailJump.Insert(0, 0f); // grows as the body goes down (Advance)
            _tunnelLeft = (_segments.Count + 1) * SegmentSpacing;
            _tunnelWait = ShovelUndergroundTime;
            _target = to;
            _stopOnArrival = true;
            IsMoving = true;
            return true;
        }
    }
}
