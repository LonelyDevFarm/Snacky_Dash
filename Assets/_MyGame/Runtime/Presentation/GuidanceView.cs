using System;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// The purple guidance arrows of the first levels (original GuidanceController, GuidanceConfig, Resources/Guidance/
    /// GuidanceArrow): when the snake has stood at a stop for 3 s, purple arrows (S_Hint_Arrow, 0.175, alpha 0.9) show the
    /// way to go, one on each cell up to the next stop (20 at most), fading in one after another (0.3 s each, 0.05 s
    /// apart), staying 1.5 s, fading out together in 0.3 s, then after 0.5 s the cycle repeats until the player moves.
    /// The way: the one that reaches soonest (cells walked) a crate the snake can fill when it carries enough of its
    /// fruit (or nothing is left to eat), otherwise a fruit (GuidanceDirectionResolver; its exact rule is not in the files).
    /// Only up to Level 7 (MaxLevelActive index 6), never during a tutorial or a popup.
    /// </summary>
    public class GuidanceView : MonoBehaviour
    {
        // GuidanceConfig.asset
        public const int MaxLevel = 7, MaxArrows = 20;
        public const float IdleDelay = 3f, FadeIn = 0.3f, Cascade = 0.05f, Hold = 1.5f, FadeOut = 0.3f, Pause = 0.5f, MaxAlpha = 0.9f;
        private const float ArrowScale = 0.175f;
        private const int SortingOrder = 5;

        private GameSession _session;
        private Sprite _sprite;
        private Func<bool> _blocked;
        private readonly List<SpriteRenderer> _arrows = new List<SpriteRenderer>();
        private int _shown;
        private float _idle, _cycle;
        private bool _active;

        public void Bind(GameSession session, int levelNumber, Sprite arrow, Func<bool> blocked)
        {
            _session = session;
            _sprite = arrow;
            _blocked = blocked;
            enabled = levelNumber <= MaxLevel && arrow != null;
            session.MoveStarted += _ => Hide();
        }

        private bool CanShow =>
            _session.IsIdle && !_session.InputBlocked && !_session.SwipesHeld && !_session.ForcedSwipe.HasValue && !_session.InputLocked
            && Time.timeScale > 0f && (_blocked == null || !_blocked());

        private void Update()
        {
            if (_session == null) return;
            if (!CanShow) { Hide(); return; }
            if (!_active)
            {
                _idle += Time.deltaTime;
                if (_idle >= IdleDelay) Show();
                return;
            }
            float length = CycleLength;
            int before = Mathf.FloorToInt(_cycle / length);
            _cycle += Time.deltaTime;
            // Each new cycle looks again (a new wave, OnNewBatchReceived; fruit taken by a booster), never in the middle of one.
            if (Mathf.FloorToInt(_cycle / length) != before) { Show(); return; }
            Animate(_cycle);
        }

        private void Hide()
        {
            _idle = 0f;
            _active = false;
            for (int i = 0; i < _arrows.Count; i++) _arrows[i].gameObject.SetActive(false);
        }

        private void Show()
        {
            var cells = Way();
            if (cells.Count == 0) { _idle = 0f; return; }
            _shown = Mathf.Min(cells.Count, MaxArrows);
            for (int i = 0; i < _shown; i++)
            {
                var arrow = Arrow(i);
                arrow.transform.localPosition = BoardView.CellToWorld(cells[i].cell) + new Vector3(0f, 0f, -0.15f); // above the stop marks (Stop_Visual plane at z -0.1) [user]
                arrow.transform.localRotation = Quaternion.Euler(0f, 0f, Rotation(cells[i].heading));
                arrow.gameObject.SetActive(true);
            }
            for (int i = _shown; i < _arrows.Count; i++) _arrows[i].gameObject.SetActive(false);
            _active = true;
            _cycle = 0f;
            Animate(0f);
        }

        private SpriteRenderer Arrow(int index)
        {
            while (_arrows.Count <= index)
            {
                var go = new GameObject("GuidanceArrow");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * ArrowScale;
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _sprite;
                renderer.sortingOrder = SortingOrder;
                _arrows.Add(renderer);
            }
            return _arrows[index];
        }

        /// <summary>GuidanceArrowView.DirectionToRotationZ: the sprite points up.</summary>
        private static float Rotation(Direction heading)
        {
            switch (heading)
            {
                case Direction.Right: return -90f;
                case Direction.Down: return 180f;
                case Direction.Left: return 90f;
                default: return 0f;
            }
        }

        /// <summary>One cycle: cascade in, hold, all out, pause; then again.</summary>
        private float CycleLength => (_shown - 1) * Cascade + FadeIn + Hold + FadeOut + Pause;

        private void Animate(float time)
        {
            float allIn = (_shown - 1) * Cascade + FadeIn;
            float t = time % CycleLength;
            float fadeOut = t <= allIn + Hold ? 1f : Mathf.Clamp01(1f - (t - allIn - Hold) / FadeOut);
            for (int i = 0; i < _shown; i++)
            {
                float fadeIn = Mathf.Clamp01((t - i * Cascade) / FadeIn);
                var colour = _arrows[i].color;
                colour.a = MaxAlpha * Mathf.Min(fadeIn, fadeOut);
                _arrows[i].color = colour;
            }
        }

        // ------------------------------------------------------------------ the way to show

        /// <summary>The cells (after the head's) of the way to take, each with the heading the snake has there.</summary>
        private List<(Vector2Int cell, Direction heading)> Way()
        {
            var result = new List<(Vector2Int, Direction)>();
            var allowed = new List<Direction>();
            foreach (var direction in DirectionExtensions.All) if (_session.CanGo(direction)) allowed.Add(direction);
            if (allowed.Count == 0) return result;

            // The targets: crates it can fill (or deliver to when nothing is left to eat), else the fruit.
            var targets = new HashSet<Vector2Int>();
            if (GuideToCrate()) { foreach (var pair in _session.Board.CrateStops) if (_session.CanDeliverTo(pair.Key)) targets.Add(pair.Value); }
            else foreach (var cell in _session.Pills.Keys) targets.Add(cell);

            // The way that reaches one soonest (cells walked); if none does, the one passing nearest to one.
            Direction best = allowed[0];
            float bestScore = float.MaxValue;
            foreach (var direction in allowed)
            {
                var path = _session.PreviewPath(direction);
                float score = float.MaxValue;
                for (int i = 1; i < path.Count && score == float.MaxValue; i++) if (targets.Contains(path[i])) score = i;
                if (score == float.MaxValue)
                    foreach (var cell in path)
                        foreach (var target in targets) score = Mathf.Min(score, 1000f + (cell - target).sqrMagnitude);
                if (direction == _session.Heading.Opposite() && allowed.Count > 1) score += 0.5f; // rather not straight back
                if (score < bestScore) { bestScore = score; best = direction; }
            }

            var way = _session.PreviewPath(best);
            for (int i = 1; i < way.Count; i++)
            {
                var step = way[i] - way[i - 1];
                var heading = step == Vector2Int.up ? Direction.Up : step == Vector2Int.down ? Direction.Down : step == Vector2Int.left ? Direction.Left : Direction.Right;
                result.Add((way[i], heading));
            }
            return result;
        }

        /// <summary>The snake carries enough of a crate's fruit to fill it, or there is no fruit left to eat but some to deliver.</summary>
        private bool GuideToCrate()
        {
            bool any = false;
            for (int column = 0; column < _session.Columns.Count; column++)
            {
                if (!_session.CanDeliverTo(column)) continue;
                any = true;
                var crate = _session.Columns[column];
                int carried = 0;
                foreach (var kind in _session.Segments) if (kind == crate.Current.kind) carried++;
                if (carried >= crate.Current.requiredCount - crate.Filled) return true;
            }
            return any && _session.Pills.Count == 0;
        }
    }
}
