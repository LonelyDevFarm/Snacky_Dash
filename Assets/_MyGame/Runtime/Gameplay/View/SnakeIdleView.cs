using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Rắn đứng yên lâu [user, tự thêm, không có trong bản gốc]: sau mỗi lần chớp mắt thì liếc (trái, phải xen kẽ), và bong bóng
    /// trên đầu hiện trái mà một khay đang mở cần (không có thì trái bất kỳ trên sân), đứng yên tới khi rắn chạy. Vuốt là tắt ngay. Chỉ đổi hình,
    /// luật chơi không đổi. Nằm trên ---- MÀN CHƠI ----/Snake, GameBootstrap gọi Bind.
    /// </summary>
    public class SnakeIdleView : MonoBehaviour
    {
        [Tooltip("Prefab Prefabs/InGame/fx/SnakeThoughtBubble: Canvas đặt trong bàn chơi, con FruitIcon (Image) là chỗ đặt hình trái.")]
        [SerializeField] private GameObject bubblePrefab;
        [Tooltip("Data/FruitGraphicSet: hình trái trong bong bóng (dùng icon trên sân).")]
        [SerializeField] private FruitGraphicSet fruits;

        [Header("Liếc mắt")]
        [Tooltip("Đứng yên bao giây thì bắt đầu liếc.")]
        [SerializeField] private float glanceDelay = 5f;
        [Tooltip("Đồng tử dịch ngang bấy nhiêu (đơn vị mắt). Tăng: liếc mạnh hơn.")]
        [SerializeField] private float glanceOffset = 0.0006f;
        [Tooltip("Giây liếc sang, giữ, rồi về giữa (mỗi lần chớp mắt xong liếc một lần, trái / phải xen kẽ).")]
        [SerializeField] private float glanceMove = 0.12f, glanceHold = 0.4f;

        [Header("Bong bóng")]
        [Tooltip("Đứng yên bao giây thì bong bóng hiện.")]
        [SerializeField] private float bubbleDelay = 6f;
        [Tooltip("Chỗ bong bóng so với đầu rắn (Y lên màn, Z âm về phía camera).")]
        [SerializeField] private Vector3 bubbleOffset = new Vector3(0.6f, 1.4f, -2.5f);
        [SerializeField] private float popDuration = 0.25f;
        [Tooltip("Kéo bong bóng về phía camera bấy nhiêu (dọc tia nhìn, chỗ trên màn hình và cỡ giữ nguyên) để khay, đá... không che [user].")]
        [SerializeField] private float bubblePull = 6f;

        [Header("Tức giận")]
        [Tooltip("Hình dấu tức giận trong bong bóng (Source/Art/InGame/Textures/S_Mood_Angry).")]
        [SerializeField] private Sprite angrySprite;
        [Tooltip("Vuốt vào tường / vật cản bấy nhiêu lần liên tiếp (không đi được bước nào) thì tức giận.")]
        [SerializeField] private int angryHits = 3;
        [Tooltip("Hai lần đập cách nhau quá bấy nhiêu giây thì không tính là liên tiếp, đếm lại từ đầu [user].")]
        [SerializeField] private float angryHitGap = 1.5f;
        [Tooltip("Giây bong bóng tức giận giữ (3-7 s [user]); hết thì về bình thường, đếm đứng yên lại từ đầu.")]
        [SerializeField] private float angryDuration = 5f;
        [Tooltip("Dấu tức giận lắc bao nhiêu độ, nhanh bao nhiêu lần mỗi giây.")]
        [SerializeField] private float angryShakeAngle = 10f, angryShakeSpeed = 9f;
        [Tooltip("Chỉ lắc bấy nhiêu giây lúc mới hiện (2-3 lần) rồi đứng yên [user].")]
        [SerializeField] private float angryShakeTime = 0.35f;

        private GameSession _session;
        private SnakeView _snake;
        private Func<bool> _blocked;
        private readonly List<Transform> _pupils = new List<Transform>();
        private readonly List<Vector3> _pupilRest = new List<Vector3>();
        private Transform _bubble;
        private Vector3 _bubbleScale; // tỉ lệ của prefab (Canvas 0.01), bật nảy nhân lên đó
        private float _pop; // 0..1 lúc bật nảy
        private UnityEngine.UI.Image _icon;
        private Coroutine _glance, _talk, _angry;
        private int _hits; // lần vuốt bị chặn liên tiếp
        private float _lastHit = -99f;
        private float _idle, _glanceSide = -1f;

        public void Bind(GameSession session, SnakeView snake, Func<bool> blocked)
        {
            _session = session;
            _snake = snake;
            _blocked = blocked;
            foreach (var t in snake.Head.GetComponentsInChildren<Transform>(true))
                if (t.name == "eye" && t.parent != null && t.parent.parent != null && t.parent.parent.name == "Eyes_Open")
                {
                    _pupils.Add(t);
                    _pupilRest.Add(t.localPosition);
                }
            if (bubblePrefab != null)
            {
                _bubble = Instantiate(bubblePrefab, transform).transform;
                _bubbleScale = _bubble.localScale;
                var icon = _bubble.Find("FruitIcon");
                _icon = icon != null ? icon.GetComponent<UnityEngine.UI.Image>() : null;
                _bubble.gameObject.SetActive(false);
            }
            session.MoveStarted += _ => { _hits = 0; Stop(); };
            session.MoveBlocked += _ => OnBlocked();
            snake.Blinked += OnBlinked;
        }

        private bool CanShow =>
            _session.IsIdle && !_session.InputBlocked && !_session.InputLocked && !_session.ForcedSwipe.HasValue
            && Time.timeScale > 0f && (_blocked == null || !_blocked());

        private void Update()
        {
            if (_session == null) return;
            if (!CanShow) { Stop(); return; }
            _idle += Time.deltaTime;
            if (_talk == null && _angry == null && _bubble != null && _idle >= bubbleDelay) _talk = StartCoroutine(Talk());
        }

        private void LateUpdate()
        {
            if (_bubble == null || !_bubble.gameObject.activeSelf) return;
            var at = _snake.HeadWorldPosition + bubbleOffset;
            var cam = Camera.main;
            float size = 1f;
            if (cam != null)
            {
                // Kéo về phía camera mà chỗ và cỡ trên màn hình không đổi: camera vuông góc (game này) thì lùi dọc hướng nhìn;
                // camera phối cảnh thì trượt trên tia nhìn và thu nhỏ theo khoảng cách.
                if (cam.orthographic) at -= cam.transform.forward * bubblePull;
                else
                {
                    var eye = cam.transform.position;
                    float far = Vector3.Distance(eye, at);
                    float near = Mathf.Max(1f, far - bubblePull);
                    at = eye + (at - eye) * (near / far);
                    size = near / far;
                }
                _bubble.rotation = cam.transform.rotation;
            }
            _bubble.position = at;
            _bubble.localScale = _bubbleScale * (size * _pop);
        }

        /// <summary>Rắn chạy, popup, hướng dẫn...: tắt hết, mắt về giữa.</summary>
        private void Stop()
        {
            _idle = 0f;
            if (_glance != null) { StopCoroutine(_glance); _glance = null; }
            if (_talk != null) { StopCoroutine(_talk); _talk = null; }
            if (_angry != null) { StopCoroutine(_angry); _angry = null; }
            if (_icon != null) _icon.rectTransform.localRotation = Quaternion.identity;
            SetPupils(0f);
            if (_bubble != null) _bubble.gameObject.SetActive(false);
        }

        private void SetPupils(float side)
        {
            for (int i = 0; i < _pupils.Count; i++)
                if (_pupils[i] != null) _pupils[i].localPosition = _pupilRest[i] + new Vector3(side * glanceOffset, 0f, 0f);
        }

        /// <summary>Chớp xong thì liếc một lần (đứng yên đủ lâu), trái / phải xen kẽ: chớp, liếc, chớp, liếc [user].</summary>
        private void OnBlinked()
        {
            if (_session == null || !CanShow || _idle < glanceDelay || _glance != null) return;
            _glance = StartCoroutine(Glance(_glanceSide));
            _glanceSide = -_glanceSide;
        }

        private IEnumerator Glance(float side)
        {
            yield return Tween.Run(glanceMove, Tween.OutSine, t => SetPupils(side * t));
            yield return new WaitForSeconds(glanceHold);
            yield return Tween.Run(glanceMove, Tween.OutSine, t => SetPupils(side * (1f - t)));
            _glance = null;
        }

        /// <summary>
        /// Vuốt vào tường / vật cản: từ lần thứ <see cref="angryHits"/> liên tiếp thì bong bóng thành dấu tức giận; mỗi lần đập
        /// tiếp sau đó bật lại và tính lại thời gian [user]. Đang có bong bóng trái thì đổi sang tức giận.
        /// </summary>
        private void OnBlocked()
        {
            if (Time.time - _lastHit > angryHitGap) _hits = 0; // phải liên tiếp [user]
            _lastHit = Time.time;
            _hits++;
            if ((_hits < angryHits && _angry == null) || _bubble == null || _icon == null || angrySprite == null || _session == null || !CanShow) return;
            if (_talk != null) { StopCoroutine(_talk); _talk = null; }
            if (_angry != null) StopCoroutine(_angry);
            _angry = StartCoroutine(Angry());
        }

        private IEnumerator Angry()
        {
            _icon.sprite = angrySprite;
            _bubble.gameObject.SetActive(true);
            LateUpdate();
            float time = 0f;
            var icon = _icon.rectTransform;
            while (time < angryDuration)
            {
                float pop = Mathf.Clamp01(time / popDuration);
                _pop = Tween.OutBack(2.5f)(pop);
                float shake = time < angryShakeTime ? (1f - time / angryShakeTime) * angryShakeAngle : 0f; // lắc nhỏ dần rồi đứng yên
                icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * angryShakeSpeed * Mathf.PI * 2f) * shake);
                time += Time.deltaTime;
                yield return null;
            }
            icon.localRotation = Quaternion.identity;
            _bubble.gameObject.SetActive(false);
            // Hết giận thì về bình thường: đếm đứng yên lại từ đầu, trạng thái tiếp theo tuỳ điều kiện nào đạt trước [user].
            _angry = null;
            _talk = null;
            _idle = 0f;
            _hits = 0;
        }

        /// <summary>Bong bóng bật lên một lần rồi đứng yên tới khi rắn chạy [user].</summary>
        private IEnumerator Talk()
        {
            if (_icon == null || fruits == null || !PickFruit(out var kind)) yield break;
            var entry = fruits.Get(kind);
            _icon.sprite = entry == null ? null : entry.fieldSprite != null ? entry.fieldSprite : entry.sprite;
            _bubble.gameObject.SetActive(true);
            LateUpdate();
            yield return Tween.Run(popDuration, Tween.OutBack(2.5f), t => _pop = t);
        }

        /// <summary>Trái mà khay đang mở của một cột còn thiếu (ngẫu nhiên giữa các cột); không có thì một trái bất kỳ trên sân.</summary>
        private bool PickFruit(out PillKind kind)
        {
            var wanted = new List<PillKind>();
            foreach (var column in _session.Columns)
                if (!column.IsComplete && column.Crates.Count > 0 && !(column.IsExtra && _session.ExtraLocked)) wanted.Add(column.Current.kind);
            if (wanted.Count > 0) { kind = wanted[UnityEngine.Random.Range(0, wanted.Count)]; return true; }
            foreach (var pill in _session.Pills) { kind = pill.Value; return true; }
            kind = default;
            return false;
        }
    }
}
