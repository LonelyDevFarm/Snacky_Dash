using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Snake head (SnakeHead_VO) and tail segments (TailSegment_VO, each carrying a PillMaster_VO),
    /// placed every frame from the session state, plus the original "feel" values:
    /// PlayerView (scale, stretch, bump, spawn drop), TailFeelHandler (spacing speed) and
    /// TailSegmentFeedbacksView (segment pop-in). The head prefab faces +Y at rest.
    /// </summary>
    public class SnakeView : MonoBehaviour
    {
        [SerializeField] private GameObject headPrefab;
        [SerializeField] private GameObject segmentPrefab;

        [Header("PlayerView (Game.unity)")]
        [SerializeField] private Vector3 defaultScale = new Vector3(0.8f, 0.8f, 0.8f);
        [SerializeField] private Vector3 startStretch = new Vector3(0.63f, 1.12f, 1f);
        [SerializeField] private Vector3 stopStretch = new Vector3(0.875f, 0.675f, 1f);
        [SerializeField] private float scaleDuration = 0.06f;
        [SerializeField] private float bumpDistance = 0.1f;
        [SerializeField] private float bumpForwardDuration = 0.08f;
        [SerializeField] private float bumpReturnDuration = 0.05f;
        [SerializeField] private float spawnDropHeight = 15f;
        [SerializeField] private float spawnDuration = 1f;

        [Header("TailFeelHandler / TailSegmentFeedbacksView")]
        [SerializeField] private float spacingSpeed = 20f;
        [SerializeField] private float segmentPopDuration = 0.35f;
        [SerializeField] private float segmentPopOvershoot = 3.25f;

        private const string DeliveryStartState = "SnakeHead_DeliveryStart"; // SnakeHeadView._deliveryStartStateName
        private const string DeliveryEndState = "SnakeHead_DeliveryEnd";

        private class Segment
        {
            public GameObject root;
            public Transform visual;
            public float distance; // along the head path; eases towards its slot
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private GameSession _session;
        private Transform _head;
        private Animator _headAnimator;
        private Vector3 _headScale;
        private float _bump;      // forward offset along the heading
        private float _dropHeight; // spawn drop, towards the camera (-Z)
        private Coroutine _scaleRoutine, _bumpRoutine;

        public Vector3 HeadWorldPosition => _head != null ? _head.position : transform.position;

        public void Bind(GameSession session)
        {
            _session = session;
            _head = Instantiate(headPrefab, transform).transform;
            _head.name = "SnakeHead";
            _headAnimator = _head.GetComponentInChildren<Animator>();
            _headScale = defaultScale;

            session.PillEaten += OnPillEaten;
            session.SegmentDelivered += OnSegmentDelivered;
            session.MoveStarted += direction => PlayScale(startStretch);
            session.Stopped += cell => OnStopped();
            session.DeliveryStarted += column => PlayHeadState(DeliveryStartState);
            session.DeliveryEnded += column => PlayHeadState(DeliveryEndState);

            // Spawn: the head drops onto the board [PlayerView._spawnDropHeight / _spawnDuration].
            // Ease is inferred (falling, then the landing squash).
            StartCoroutine(SpawnDrop());
            LateUpdate();
        }

        private IEnumerator SpawnDrop()
        {
            yield return Tween.Run(spawnDuration, Tween.InQuad, t => _dropHeight = spawnDropHeight * (1f - t));
            PlayScale(stopStretch);
        }

        private void OnStopped()
        {
            PlayScale(stopStretch);
            if (_bumpRoutine != null) StopCoroutine(_bumpRoutine);
            _bumpRoutine = StartCoroutine(Bump());
        }

        private IEnumerator Bump()
        {
            yield return Tween.Run(bumpForwardDuration, Tween.OutSine, t => _bump = bumpDistance * t);
            yield return Tween.Run(bumpReturnDuration, Tween.OutSine, t => _bump = bumpDistance * (1f - t));
        }

        /// <summary>Stretch/squash for scaleDuration, then back to the default scale.</summary>
        private void PlayScale(Vector3 stretch)
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            var target = Vector3.Scale(defaultScale, stretch);
            _scaleRoutine = StartCoroutine(Tween.Punch(scaleDuration, t => _headScale = Vector3.LerpUnclamped(defaultScale, target, t)));
        }

        private void PlayHeadState(string state)
        {
            if (_headAnimator != null && _headAnimator.isActiveAndEnabled) _headAnimator.Play(state, 0, 0f);
        }

        private void OnPillEaten(Vector2Int cell, PillKind kind, int index)
        {
            var root = Instantiate(segmentPrefab, transform);
            root.name = $"Segment {kind}";
            var visual = root.transform.Find("GameObject");
            var pill = root.transform.Find("GameObject/PillMaster_VO");
            if (pill != null) PillVisual.SetKind(pill.gameObject, kind);

            var segment = new Segment { root = root, visual = visual, distance = (index + 1) * GameSession.SegmentSpacing };
            _segments.Insert(index, segment);
            root.transform.localPosition = _session.PathPoint(segment.distance);

            if (visual != null)
            {
                visual.localScale = Vector3.zero;
                var ease = Tween.OutBack(segmentPopOvershoot);
                StartCoroutine(Tween.Run(segmentPopDuration, ease, t => { if (visual != null) visual.localScale = Vector3.one * t; }));
            }
        }

        private void OnSegmentDelivered(int index, PillKind kind, int column)
        {
            Destroy(_segments[index].root);
            _segments.RemoveAt(index);
            // Segments behind the gap keep their distance and slide forward (see LateUpdate).
        }

        private void LateUpdate()
        {
            if (_session == null) return;

            var forward = (Vector3)_session.Heading.ToVector();
            _head.localPosition = (Vector3)_session.HeadPosition + forward * _bump + new Vector3(0f, 0f, -_dropHeight);
            _head.localRotation = Quaternion.Euler(0f, 0f, HeadAngle(_session.Heading));
            _head.localScale = _headScale;

            float step = spacingSpeed * Time.deltaTime;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                segment.distance = Mathf.MoveTowards(segment.distance, (i + 1) * GameSession.SegmentSpacing, step);
                segment.root.transform.localPosition = _session.PathPoint(segment.distance);
            }
        }

        private static float HeadAngle(Direction direction)
        {
            switch (direction)
            {
                case Direction.Right: return -90f;
                case Direction.Down: return 180f;
                case Direction.Left: return 90f;
                default: return 0f;
            }
        }
    }
}
