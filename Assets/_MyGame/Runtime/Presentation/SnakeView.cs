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
        [Tooltip("Resources/boosters/boomerang/Vortex2 FX: the wind around boomerang fruit.")]
        [SerializeField] private GameObject vortexPrefab;
        [Tooltip("GameObject/View_Secret_Collectible: its ExplodeFeedback burst plays where a secret fruit is eaten.")]
        [SerializeField] private GameObject secretPrefab;

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
        [Tooltip("Height of the bridge deck at its middle (TileCov_Bridge_LR top mesh bounds: ~1.8).")]
        [SerializeField] private float deckHeight = 1.8f;

        [Header("Eyes (SnakeHead_VO Feedbacks/Blinking, Stunned)")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(0.5f, 4f);   // MMF_Looper pause
        [SerializeField] private Vector2 blinkDuration = new Vector2(0.1f, 0.2f); // MMF_HoldingPause
        [Tooltip("Stunned: the top jaw opens 90 -> 75 degrees (MMF_Rotation).")]
        [SerializeField] private float stunnedJawAngle = 15f;

        [Header("Jaw (SpringChild on Pivot) and blocked-swipe bump (PlayerView)")]
        [SerializeField] private float blockedBumpDistance = 0.1f;
        [SerializeField] private float jawTiltPerSpeed = 10f;   // SpringChild.tiltAmount
        [SerializeField] private float jawSmoothTime = 0.05f;   // SpringChild.smoothTime
        [SerializeField] private float jawMaxAngle = 45f;       // SpringChild.maxAngle

        private const string DeliveryStartState = "SnakeHead_DeliveryStart"; // SnakeHeadView._deliveryStartStateName
        private const string DeliveryEndState = "SnakeHead_DeliveryEnd";

        private class Segment
        {
            public GameObject root;
            public Transform visual;
            public float distance; // along the head path; eases towards its slot
            public bool frozen;    // waiting for its boomerang flight: stays where it is
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private GameSession _session;
        private Transform _head;
        private Animator _headAnimator;
        private Vector3 _headScale;
        private float _bump;      // forward offset along the heading
        private float _dropHeight; // spawn drop, towards the camera (-Z)
        private Coroutine _scaleRoutine, _bumpRoutine, _blinkRoutine;
        private bool _mouthOpen;
        private static readonly int FlashStrength = Shader.PropertyToID("_FlashStrength");
        private MaterialPropertyBlock _flashBlock;

        private GameObject _eyesOpen, _eyesClosed, _eyesStunned, _stunnedFx, _tongue;
        private Transform _jaw;          // Snake_Head/Pivot: the top jaw hinges on its local X axis
        private Quaternion _jawRest;
        private bool _stunned;
        private Vector3 _blockOffset;
        private Vector2 _lastHeadPosition;
        private const float TeleportStep = 2f; // SpringChild._teleportStep
        private float _jawPitch, _jawVelocity;
        private Coroutine _blockRoutine;

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
            // Mouth open while fruit leaves, closed while waiting: a full crate's pause closes it,
            // the next delivered fruit opens it again [user, observed on the original].
            session.DeliveryStarted += column => SetMouth(true);
            session.DeliveryEnded += column => SetMouth(false);
            session.CrateCompleted += (column, crate) => SetMouth(false);
            session.SegmentDelivered += (index, kind, column) => SetMouth(true);
            session.TailBitten += count => { _blinkRoutine = StartCoroutine(BlinkSnipped(count)); SetStunned(true); };
            session.TailSnipped += OnTailSnipped;
            session.MoveBlocked += OnMoveBlocked;
            session.BoomerangThrown += OnBoomerangThrown;
            session.SegmentRemoved += OnSegmentThrown;
            session.SecretRevealed += OnSecretRevealed;
            session.Rewound += OnRewound;

            var eyes = _head.Find("Snake_Head/Pivot/Snake_Head_Top");
            _eyesOpen = eyes != null ? eyes.Find("Eyes_Open")?.gameObject : null;
            _eyesClosed = eyes != null ? eyes.Find("Eyes_Closed")?.gameObject : null;
            _eyesStunned = eyes != null ? eyes.Find("Eyes_Stunned")?.gameObject : null;
            _stunnedFx = _head.Find("FX_Snake_Stunned")?.gameObject;
            _tongue = _head.Find("Snake_Head/Tongue")?.gameObject;
            _jaw = _head.Find("Snake_Head/Pivot");
            if (_jaw != null) _jawRest = _jaw.localRotation;
            StartCoroutine(BlinkEyes());

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

        /// <summary>Feedbacks/Blinking: eyes closed for 0.1-0.2 s, open again for 0.5-4 s, forever.</summary>
        private IEnumerator BlinkEyes()
        {
            yield return new WaitForSeconds(0.05f); // MMF_Player InitialDelay
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(blinkInterval.x, blinkInterval.y));
                if (_stunned) continue;
                ShowEyes(_eyesClosed);
                yield return new WaitForSeconds(Random.Range(blinkDuration.x, blinkDuration.y));
                if (!_stunned) ShowEyes(_eyesOpen);
            }
        }

        private void ShowEyes(GameObject eyes)
        {
            foreach (var set in new[] { _eyesOpen, _eyesClosed, _eyesStunned })
                if (set != null) set.SetActive(set == eyes);
        }

        /// <summary>
        /// Feedbacks/Stunned (biting the tail): @@ eyes, stars over the head, tongue out, jaw a little
        /// open; Stunned_Reset undoes it once the tail is snipped.
        /// </summary>
        private void SetStunned(bool stunned)
        {
            _stunned = stunned;
            ShowEyes(stunned ? _eyesStunned : _eyesOpen);
            if (_stunnedFx != null) _stunnedFx.SetActive(stunned);
            if (_tongue != null) _tongue.SetActive(stunned);
        }

        /// <summary>
        /// Swipe into a wall or the tail: PlayerView.HandleBump nudges the head that way and back.
        /// </summary>
        private void OnMoveBlocked(Direction direction)
        {
            if (_blockRoutine != null) StopCoroutine(_blockRoutine);
            _blockRoutine = StartCoroutine(BlockedBump((Vector3)(Vector2)direction.ToVector()));
        }

        private IEnumerator BlockedBump(Vector3 direction)
        {
            yield return Tween.Run(bumpForwardDuration, Tween.OutSine, t => _blockOffset = direction * (blockedBumpDistance * t));
            yield return Tween.Run(bumpReturnDuration, Tween.OutSine, t => _blockOffset = direction * (blockedBumpDistance * (1f - t)));
        }

        /// <summary>
        /// SpringChild on Pivot: the top jaw lags behind the head's motion, so it opens with speed
        /// (tiltAmount degrees per cell/s, up to maxAngle) and springs shut when the head stops. A run
        /// cut short by an obstacle opens and closes it at once, which reads as the lips twitching.
        /// </summary>
        private void UpdateJaw()
        {
            if (_jaw == null) return;
            Vector2 position = _head.localPosition; // board plane only: the spawn drop is along Z
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float step = (position - _lastHeadPosition).magnitude;
                float speed = step > TeleportStep ? 0f : step / dt;
                float target = _stunned ? stunnedJawAngle : Mathf.Min(speed * jawTiltPerSpeed, jawMaxAngle);
                _jawPitch = Mathf.SmoothDamp(_jawPitch, target, ref _jawVelocity, jawSmoothTime, Mathf.Infinity, dt);
            }
            _lastHeadPosition = position;
            _jaw.localRotation = _jawRest * Quaternion.Euler(-_jawPitch, 0f, 0f); // negative X lifts the top jaw
        }

        private readonly List<Renderer> _hiddenHeadParts = new List<Renderer>();

        /// <summary>Hides the head inside a tunnel (renderers only, so eyes and animator keep their state).</summary>
        private void ShowHead(bool visible)
        {
            if (!visible && _hiddenHeadParts.Count == 0)
            {
                foreach (var part in _head.GetComponentsInChildren<Renderer>())
                    if (part.enabled) { part.enabled = false; _hiddenHeadParts.Add(part); }
            }
            else if (visible && _hiddenHeadParts.Count > 0)
            {
                foreach (var part in _hiddenHeadParts) if (part != null) part.enabled = true;
                _hiddenHeadParts.Clear();
            }
        }

        private void SetMouth(bool open)
        {
            if (_mouthOpen == open) return;
            _mouthOpen = open;
            PlayHeadState(open ? DeliveryStartState : DeliveryEndState);
        }

        /// <summary>The booster's icon reached the head: the snake gulps it (SnakeHead_Controller state).</summary>
        public void EatBooster() => PlayHeadState("SnakeHead_EatBooster");

        private void PlayHeadState(string state)
        {
            if (_headAnimator != null && _headAnimator.isActiveAndEnabled) _headAnimator.Play(state, 0, 0f);
        }

        private void OnPillEaten(Vector2Int cell, PillKind kind, int index) => CreateSegment(kind, index, pop: true);

        /// <summary>Undo booster: the body goes back to what the session holds now (no pop).</summary>
        private void OnRewound()
        {
            foreach (var segment in _segments) if (segment.root != null) Destroy(segment.root);
            _segments.Clear();
            for (int i = 0; i < _session.Segments.Count; i++) CreateSegment(_session.Segments[i], i, pop: false);
            // The fruit the swipe ate stays on the tail until the head has run back over it [VFX_hoantac].
            _rewindDrops.Clear();
            foreach (var drop in _session.RewindDropped)
            {
                CreateSegment(drop.kind, _segments.Count, pop: false);
                _rewindDrops.Add(drop.along);
            }
            // Run back along the way the head came instead of jumping [user].
            _rewindLeft = 0f;
            var path = _session.RewindPath;
            for (int i = 1; i < path.Count; i++) _rewindLeft += Vector2.Distance(path[i - 1], path[i]);
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
            LateUpdate();
        }

        // Undo: how far ahead of the restored head (along RewindPath) the snake is still drawn; runs to 0.
        private float _rewindLeft;
        // Undo: where along RewindPath each extra tail segment was eaten; it drops off once the head is back there.
        private readonly List<float> _rewindDrops = new List<float>();

        /// <summary>
        /// Point <paramref name="behind"/> cells behind the drawn head: while an undo runs the snake back,
        /// the head is still <see cref="_rewindLeft"/> cells up the way it came (RewindPath).
        /// </summary>
        /// <summary>Like <see cref="Sample"/>: is that point inside a tube (the rewind goes back through tunnels too) [user].</summary>
        private bool SampleHidden(float behind)
        {
            float ahead = _rewindLeft - behind;
            if (ahead <= 0f) return _rewindLeft > 0f ? _session.PathInTunnel(-ahead) : _session.PathInTunnel(behind);
            var path = _session.RewindPath;
            var under = _session.RewindUnderground;
            for (int i = 1; i < path.Count; i++)
            {
                float step = Vector2.Distance(path[i - 1], path[i]);
                if (ahead <= step) return i < under.Count && under[i] && ahead > 0f && ahead < step;
                ahead -= step;
            }
            return false;
        }

        private Vector2 Sample(float behind)
        {
            float ahead = _rewindLeft - behind;
            if (ahead <= 0f) return behind == 0f && _rewindLeft <= 0f ? _session.HeadPosition : _session.PathPoint(-ahead);
            var path = _session.RewindPath;
            for (int i = 1; i < path.Count; i++)
            {
                float step = Vector2.Distance(path[i - 1], path[i]);
                if (ahead <= step && step > 0f) return Vector2.Lerp(path[i - 1], path[i], ahead / step);
                ahead -= step;
            }
            return path.Count > 0 ? path[path.Count - 1] : _session.HeadPosition;
        }

        private void CreateSegment(PillKind kind, int index, bool pop)
        {
            var root = Instantiate(segmentPrefab, transform);
            root.name = $"Segment {kind}";
            var visual = root.transform.Find("GameObject");
            var pill = root.transform.Find("GameObject/PillMaster_VO");
            if (pill != null) PillVisual.SetKind(pill.gameObject, kind);

            var segment = new Segment { root = root, visual = visual, distance = (index + 1) * GameSession.SegmentSpacing };
            _segments.Insert(index, segment);
            root.transform.localPosition = _session.PathPoint(segment.distance);

            if (visual != null && pop)
            {
                visual.localScale = Vector3.zero;
                var ease = Tween.OutBack(segmentPopOvershoot);
                StartCoroutine(Tween.Run(segmentPopDuration, ease, t => { if (visual != null) visual.localScale = Vector3.one * t; }));
            }
        }

        /// <summary>
        /// Video Man_Lose: the part of the tail a snip would remove blinks white. The FakeUnlit shaders
        /// carry _FlashStrength / _FlashColor (pale yellow-white) for exactly this kind of flash.
        /// </summary>
        private IEnumerator BlinkSnipped(int keep)
        {
            if (_flashBlock == null) _flashBlock = new MaterialPropertyBlock();
            for (bool on = true; ; on = !on)
            {
                for (int i = keep; i < _segments.Count; i++) SetFlash(_segments[i].root, on ? 1f : 0f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        /// <summary>World position of tail segment <paramref name="index"/> (0 = right behind the head).</summary>
        public Vector3 SegmentWorldPosition(int index) =>
            index >= 0 && index < _segments.Count ? _segments[index].root.transform.position : HeadWorldPosition;

        private void SetFlash(GameObject segment, float strength)
        {
            foreach (var renderer in segment.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_flashBlock);
                _flashBlock.SetFloat(FlashStrength, strength);
                renderer.SetPropertyBlock(_flashBlock);
            }
        }

        /// <summary>
        /// Raised after every snipped segment was handed to the crates (SegmentSnipped, whose
        /// handlers read the positions first): drop everything from <paramref name="keep"/> on.
        /// </summary>
        private void OnTailSnipped(int keep)
        {
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
            SetStunned(false);
            while (_segments.Count > keep)
            {
                Destroy(_segments[_segments.Count - 1].root);
                _segments.RemoveAt(_segments.Count - 1);
            }
            if (_flashBlock != null)
                foreach (var segment in _segments) SetFlash(segment.root, 0f);
        }

        /// <summary>
        /// Lv7 video: as the boomerang is thrown, the body tiles of every fruit that will fly vanish
        /// and each fruit gets the swirling wind (Vortex2 FX) while it waits on the road.
        /// </summary>
        private void OnBoomerangThrown(int column, IReadOnlyList<int> thrown)
        {
            foreach (int index in thrown)
            {
                if (index < 0 || index >= _segments.Count) continue;
                _segments[index].frozen = true;
                var root = _segments[index].root;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    if (!IsPill(renderer.transform)) renderer.enabled = false;
                var pill = root.transform.Find("GameObject/PillMaster_VO");
                if (vortexPrefab != null && pill != null) Instantiate(vortexPrefab, pill.position, Quaternion.identity, root.transform);
            }
        }

        /// <summary>
        /// A secret fruit shows its kind once eaten [user]: the new segment carries the real fruit and the
        /// ? box's ExplodeFeedback burst (FX_Snake_Appear) goes off on it.
        /// </summary>
        private void OnSecretRevealed(Vector2Int cell, int index)
        {
            if (secretPrefab == null || index < 0 || index >= _segments.Count) return;
            var pill = _segments[index].root.transform.Find("GameObject/PillMaster_VO");
            if (pill == null) return;
            // On the segment root so it travels with the fruit, not under the visual that pops in from
            // scale 0 (the burst scales with its hierarchy).
            var burst = Instantiate(secretPrefab, pill.position, secretPrefab.transform.rotation, _segments[index].root.transform);
            var box = burst.transform.Find("Visual");
            if (box != null) box.gameObject.SetActive(false);
            // ExplodeFeedback: the original fires it from a feedback (playOnAwake is off).
            var appear = burst.transform.Find("Feedbacks/ExplodeFeedback/FX_Snake_Appear");
            if (appear != null && appear.TryGetComponent<ParticleSystem>(out var particles)) particles.Play(true);
            Destroy(burst, 2f);
        }

        /// <summary>Boomerang: the fruit takes off now (CratesView flies a copy), so the segment goes.</summary>
        private void OnSegmentThrown(int index)
        {
            Destroy(_segments[index].root);
            _segments.RemoveAt(index);
        }

        private static bool IsPill(Transform part)
        {
            for (var t = part; t != null; t = t.parent)
                if (t.name == "PillMaster_VO") return true;
            return false;
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

            if (_rewindLeft > 0f) _rewindLeft = Mathf.Max(0f, _rewindLeft - GameSession.RewindSpeed * Time.deltaTime);
            while (_rewindDrops.Count > 0 && _rewindDrops[_rewindDrops.Count - 1] >= _rewindLeft)
            {
                _rewindDrops.RemoveAt(_rewindDrops.Count - 1);
                var last = _segments[_segments.Count - 1];
                if (last.root != null) Destroy(last.root);
                _segments.RemoveAt(_segments.Count - 1);
            }
            var forward = (Vector3)_session.Heading.ToVector();
            var headPosition = Sample(0f);
            float headLift = DeckLift(headPosition, _session.Heading.IsHorizontal());
            _head.localPosition = (Vector3)headPosition + forward * _bump + _blockOffset + new Vector3(0f, 0f, -_dropHeight - headLift);
            _head.localRotation = Quaternion.Euler(0f, 0f, HeadAngle(_session.Heading));
            _head.localScale = _headScale;
            UpdateJaw();
            ShowHead(_rewindLeft > 0f ? !SampleHidden(0f) : !_session.HeadInTunnel);

            float step = spacingSpeed * Time.deltaTime;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];
                if (segment.frozen) continue;
                segment.distance = Mathf.MoveTowards(segment.distance, (i + 1) * GameSession.SegmentSpacing, step);
                Vector3 position = Sample(segment.distance);
                var along = Sample(segment.distance - 0.5f) - Sample(segment.distance + 0.5f);
                position.z -= DeckLift(position, Mathf.Abs(along.x) >= Mathf.Abs(along.y));
                segment.root.transform.localPosition = position;
                // Inside a tunnel the segment is out of sight until it comes out of the other mouth.
                bool visible = !SampleHidden(segment.distance);
                if (segment.root.activeSelf != visible) segment.root.SetActive(visible);
            }
        }

        /// <summary>
        /// Height above the road while riding over a bridge deck (TileCov_Bridge_LR: 4.3 cells long,
        /// top ~1.8 above the road at its middle). The deck mesh is not readable, so the hump is a
        /// smooth cosine through those bounds; the road under the bridge stays at ground level.
        /// </summary>
        private float DeckLift(Vector2 position, bool movingHorizontally)
        {
            foreach (var bridge in _session.Board.Bridges)
            {
                bool deckHorizontal = bridge.Value;
                if (movingHorizontally != deckHorizontal) continue;
                var offset = position - (Vector2)bridge.Key;
                float along = deckHorizontal ? offset.x : offset.y;
                float across = deckHorizontal ? offset.y : offset.x;
                if (Mathf.Abs(across) > 0.5f || Mathf.Abs(along) >= BoardGraph.BridgeDeckHalfLength) continue;
                return deckHeight * 0.5f * (1f + Mathf.Cos(Mathf.PI * along / BoardGraph.BridgeDeckHalfLength));
            }
            return 0f;
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
