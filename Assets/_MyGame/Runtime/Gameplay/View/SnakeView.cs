using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Vẽ rắn: đầu (SnakeHead_VO) và các đốt thân (TailSegment_VO, mỗi đốt mang một trái), đặt lại mỗi frame theo GameSession.
    /// Nằm trong scene Game, GameBootstrap gọi Bind; số "feel" lấy từ PlayerView, TailFeelHandler gốc.
    /// </summary>
    public class SnakeView : MonoBehaviour
    {
        [SerializeField] private GameObject headPrefab;
        [SerializeField] private GameObject segmentPrefab;
        [Tooltip("Vortex2 FX: gió xoáy quanh trái boomerang.")]
        [SerializeField] private GameObject vortexPrefab;
        [Tooltip("View_Secret_Collectible: FX bung khi ăn trái bí ẩn.")]
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
        [SerializeField] private float spawnDuration = 0.6f; // gốc 1 s trông chậm [user]

        [Header("TailFeelHandler / TailSegmentFeedbacksView")]
        [SerializeField] private float spacingSpeed = 20f;
        [SerializeField] private float segmentPopDuration = 0.35f;
        [SerializeField] private float segmentPopOvershoot = 3.25f;
        [Tooltip("Độ cao giữa mặt cầu (TileCov_Bridge_LR ~1.8).")]
        [SerializeField] private float deckHeight = 1.8f;

        [Header("Eyes (SnakeHead_VO Feedbacks/Blinking, Stunned)")]
        [SerializeField] private Vector2 blinkInterval = new Vector2(0.5f, 4f);   // gốc: MMF_Looper pause
        [SerializeField] private Vector2 blinkDuration = new Vector2(0.1f, 0.2f); // gốc: MMF_HoldingPause
        [Tooltip("Độ mở hàm trên khi choáng (gốc MMF_Rotation 90 -> 75).")]
        [SerializeField] private float stunnedJawAngle = 15f;

        [Header("Jaw (SpringChild on Pivot) and blocked-swipe bump (PlayerView)")]
        [SerializeField] private float blockedBumpDistance = 0.1f;
        [SerializeField] private float jawTiltPerSpeed = 10f;   // gốc: SpringChild.tiltAmount
        [SerializeField] private float jawSmoothTime = 0.05f;   // gốc: SpringChild.smoothTime
        [SerializeField] private float jawMaxAngle = 45f;       // gốc: SpringChild.maxAngle

        private const string DeliveryStartState = "SnakeHead_DeliveryStart"; // gốc: SnakeHeadView._deliveryStartStateName
        private const string DeliveryEndState = "SnakeHead_DeliveryEnd";

        private class Segment
        {
            public GameObject root;
            public Transform visual;
            public float distance; // khoảng cách dọc đường đầu đi, tiến dần về chỗ của đốt
            public bool frozen;    // chờ bay boomerang: đứng yên
        }

        private readonly List<Segment> _segments = new List<Segment>();
        private GameSession _session;
        private Transform _head;
        private Animator _headAnimator;
        private Vector3 _headScale;
        private float _bump;      // nhích tới theo hướng đi
        private float _dropHeight; // độ cao rơi lúc xuất hiện, về phía camera (-Z)
        private Coroutine _scaleRoutine, _bumpRoutine, _blinkRoutine;
        private bool _mouthOpen;
        private static readonly int FlashStrength = Shader.PropertyToID("_FlashStrength");
        private MaterialPropertyBlock _flashBlock;

        private GameObject _eyesOpen, _eyesClosed, _eyesStunned, _stunnedFx, _tongue;
        private Transform _jaw;          // Snake_Head/Pivot: hàm trên quay quanh trục X
        private Quaternion _jawRest;
        private bool _stunned;
        private Vector3 _blockOffset;
        private Vector2 _lastHeadPosition;
        private const float TeleportStep = 2f; // gốc: SpringChild._teleportStep
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
            // Miệng mở khi trái ra, đóng khi chờ (khay đầy thì đóng, trái kế mở lại) [user].
            session.DeliveryStarted += column => SetMouth(true);
            session.DeliveryEnded += column => SetMouth(false);
            session.CrateCompleted += (column, crate) => SetMouth(false);
            session.SegmentDelivered += (index, kind, column) => SetMouth(true);
            session.TailBitten += count => { _blinkRoutine = StartCoroutine(BlinkSnipped(count)); SetStunned(true); CameraShake.Hit(); }; // gốc: PlayerView.HandleTailHit
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

            // Đầu rơi xuống bàn lúc vào màn (gốc PlayerView._spawnDropHeight).
            StartCoroutine(SpawnDrop()); // khoá vuốt tới khi đáp xong [user]
            LateUpdate();
        }

        private IEnumerator SpawnDrop()
        {
            // Đợi màn hình loading tắt mới rơi, không thì rơi mà không ai thấy [user].
            _dropHeight = spawnDropHeight;
            while (SnackDash.UI.LoadingScreen.Busy) { _session.LockInput(0.1f); yield return null; }
            _session.LockInput(spawnDuration + scaleDuration);
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

        private void PlayScale(Vector3 stretch)
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            var target = Vector3.Scale(defaultScale, stretch);
            _scaleRoutine = StartCoroutine(Tween.Punch(scaleDuration, t => _headScale = Vector3.LerpUnclamped(defaultScale, target, t)));
        }

        /// <summary>Chớp mắt ngẫu nhiên mãi mãi; bỏ qua khi đang choáng (gốc Feedbacks/Blinking).</summary>
        private IEnumerator BlinkEyes()
        {
            yield return new WaitForSeconds(0.05f); // gốc: MMF_Player InitialDelay
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

        /// <summary>Choáng khi cắn đuôi: mắt @@, sao trên đầu, lè lưỡi; hết khi đuôi đã cắt (gốc Feedbacks/Stunned).</summary>
        private void SetStunned(bool stunned)
        {
            _stunned = stunned;
            ShowEyes(stunned ? _eyesStunned : _eyesOpen);
            if (_stunnedFx != null) _stunnedFx.SetActive(stunned);
            if (_tongue != null) _tongue.SetActive(stunned);
        }

        /// <summary>Vuốt vào tường hay đuôi: đầu nhích về hướng đó rồi về (gốc PlayerView.HandleBump).</summary>
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

        /// <summary>Hàm trên trễ theo chuyển động: chạy nhanh thì mở, dừng thì bật đóng (gốc SpringChild trên Pivot).</summary>
        private void UpdateJaw()
        {
            if (_jaw == null) return;
            Vector2 position = _head.localPosition; // chỉ mặt bàn: bỏ độ rơi theo Z
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float step = (position - _lastHeadPosition).magnitude;
                float speed = step > TeleportStep ? 0f : step / dt;
                float target = _stunned ? stunnedJawAngle : Mathf.Min(speed * jawTiltPerSpeed, jawMaxAngle);
                _jawPitch = Mathf.SmoothDamp(_jawPitch, target, ref _jawVelocity, jawSmoothTime, Mathf.Infinity, dt);
            }
            _lastHeadPosition = position;
            _jaw.localRotation = _jawRest * Quaternion.Euler(-_jawPitch, 0f, 0f); // X âm nâng hàm trên
        }

        private readonly List<Renderer> _hiddenHeadParts = new List<Renderer>();

        /// <summary>Ẩn đầu trong đường hầm; chỉ tắt renderer để mắt và animator giữ trạng thái.</summary>
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

        /// <summary>Icon booster tới đầu: rắn nuốt nó. BoosterBarView gọi.</summary>
        public void EatBooster() => PlayHeadState("SnakeHead_EatBooster");

        /// <summary>Giây nuốt xong trong anim SnakeHead_EatBooster; phần còn lại chỉ là ánh sáng tắt, booster không cần chờ [user].</summary>
        public const float EatBoosterSwallowed = 0.67f;

        private void PlayHeadState(string state)
        {
            if (_headAnimator != null && _headAnimator.isActiveAndEnabled) _headAnimator.Play(state, 0, 0f);
        }

        private void OnPillEaten(Vector2Int cell, PillKind kind, int index) => CreateSegment(kind, index, pop: true);

        /// <summary>Undo: dựng lại thân theo session (không nảy), rồi rắn chạy lùi theo đường cũ.</summary>
        private void OnRewound()
        {
            foreach (var segment in _segments) if (segment.root != null) Destroy(segment.root);
            _segments.Clear();
            for (int i = 0; i < _session.Segments.Count; i++) CreateSegment(_session.Segments[i], i, pop: false);
            // Trái lượt đó ăn vẫn ở đuôi tới khi đầu lùi qua chỗ đó [VFX_hoantac].
            _rewindDrops.Clear();
            foreach (var drop in _session.RewindDropped)
            {
                CreateSegment(drop.kind, _segments.Count, pop: false);
                _rewindDrops.Add(drop.along);
            }
            // Chạy lùi theo đường cũ chứ không nhảy về [user].
            _rewindLeft = 0f;
            var path = _session.RewindPath;
            for (int i = 1; i < path.Count; i++) _rewindLeft += Vector2.Distance(path[i - 1], path[i]);
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
            LateUpdate();
        }

        // Undo: rắn còn được vẽ trước đầu đã khôi phục bao xa (dọc RewindPath); giảm dần về 0.
        private float _rewindLeft;
        // Undo: chỗ mỗi đốt thừa được ăn; đầu lùi tới đó thì đốt rụng.
        private readonly List<float> _rewindDrops = new List<float>();

        /// <summary>Như <see cref="Sample"/> nhưng hỏi điểm đó có trong đường hầm không (undo cũng lùi qua hầm) [user].</summary>
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

        /// <summary>Lúc undo: hướng đầu đã đi trên đoạn RewindPath đang vẽ.</summary>
        private Direction RewindHeading()
        {
            float ahead = _rewindLeft;
            var path = _session.RewindPath;
            for (int i = 1; i < path.Count; i++)
            {
                float step = Vector2.Distance(path[i - 1], path[i]);
                if (ahead <= step && step > 0f)
                {
                    var d = path[i] - path[i - 1];
                    if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) return d.x >= 0f ? Direction.Right : Direction.Left;
                    return d.y >= 0f ? Direction.Up : Direction.Down;
                }
                ahead -= step;
            }
            return _session.Heading;
        }

        /// <summary>Điểm sau đầu đang vẽ <paramref name="behind"/> ô; lúc undo đầu còn ở trước <see cref="_rewindLeft"/> ô.</summary>
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

        /// <summary>Phần đuôi sắp bị cắt nhấp nháy trắng qua _FlashStrength của shader FakeUnlit (video Man_Lose).</summary>
        private IEnumerator BlinkSnipped(int keep)
        {
            if (_flashBlock == null) _flashBlock = new MaterialPropertyBlock();
            for (bool on = true; ; on = !on)
            {
                for (int i = keep; i < _segments.Count; i++) SetFlash(_segments[i].root, on ? 1f : 0f);
                yield return new WaitForSeconds(0.15f);
            }
        }

        /// <summary>Vị trí trái trên đốt <paramref name="index"/> (0 = sát đầu), nằm trên thân chứ không phải mặt đường.</summary>
        public Vector3 SegmentWorldPosition(int index)
        {
            if (index < 0 || index >= _segments.Count) return HeadWorldPosition;
            var root = _segments[index].root.transform;
            var pill = root.Find("GameObject/PillMaster_VO");
            return pill != null ? pill.position : root.position;
        }

        private void SetFlash(GameObject segment, float strength)
        {
            foreach (var renderer in segment.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_flashBlock);
                _flashBlock.SetFloat(FlashStrength, strength);
                renderer.SetPropertyBlock(_flashBlock);
            }
        }

        /// <summary>Đến sau SegmentSnipped (CratesView đã đọc vị trí đốt), nên giờ mới xoá các đốt từ <paramref name="keep"/>.</summary>
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

        /// <summary>Ném boomerang: thân của các trái sắp bay biến mất, trái nằm chờ trong gió xoáy (video Lv7).</summary>
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

        /// <summary>Trái bí ẩn lộ loại khi bị ăn: đốt mới mang trái thật, FX_Snake_Appear bung trên đó [user].</summary>
        private void OnSecretRevealed(Vector2Int cell, int index)
        {
            if (secretPrefab == null || index < 0 || index >= _segments.Count) return;
            var pill = _segments[index].root.transform.Find("GameObject/PillMaster_VO");
            if (pill == null) return;
            // Gắn vào gốc đốt, không vào visual đang nảy từ scale 0 (FX co giãn theo cha).
            var burst = Instantiate(secretPrefab, pill.position, secretPrefab.transform.rotation, _segments[index].root.transform);
            var box = burst.transform.Find("Visual");
            if (box != null) box.gameObject.SetActive(false);
            // playOnAwake tắt (gốc bắn từ feedback) nên phải gọi Play.
            var appear = burst.transform.Find("Feedbacks/ExplodeFeedback/FX_Snake_Appear");
            if (appear != null && appear.TryGetComponent<ParticleSystem>(out var particles)) particles.Play(true);
            Destroy(burst, 2f);
        }

        /// <summary>Boomerang: trái cất cánh (CratesView bay bản sao) nên xoá đốt.</summary>
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
            // Đốt phía sau giữ khoảng cách cũ rồi trượt lên lấp chỗ (LateUpdate).
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
            // Lúc undo đầu giữ hướng của đoạn đang lùi, về tới nơi mới lấy hướng cũ [user].
            var heading = _rewindLeft > 0f ? RewindHeading() : _session.Heading;
            var forward = (Vector3)heading.ToVector();
            var headPosition = Sample(0f);
            float headLift = DeckLift(headPosition, heading.IsHorizontal());
            _head.localPosition = (Vector3)headPosition + forward * _bump + _blockOffset + new Vector3(0f, 0f, -_dropHeight - headLift);
            _head.localRotation = Quaternion.Euler(0f, 0f, HeadAngle(heading));
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
                // Trong đường hầm thì ẩn tới khi ra miệng bên kia.
                bool visible = !SampleHidden(segment.distance);
                if (segment.root.activeSelf != visible) segment.root.SetActive(visible);
            }
        }

        /// <summary>Độ nâng khi bò qua cầu (dài 4.3 ô); không đọc được mesh nên dùng đường cos. Đường dưới cầu giữ mặt đất.</summary>
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
