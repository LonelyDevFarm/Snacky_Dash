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

        private GameSession _session;
        private SnakeView _snake;
        private Func<bool> _blocked;
        private readonly List<Transform> _pupils = new List<Transform>();
        private readonly List<Vector3> _pupilRest = new List<Vector3>();
        private Transform _bubble;
        private Vector3 _bubbleScale; // tỉ lệ của prefab (Canvas 0.01), bật nảy nhân lên đó
        private UnityEngine.UI.Image _icon;
        private Coroutine _glance, _talk;
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
            session.MoveStarted += _ => Stop();
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
            if (_talk == null && _bubble != null && _idle >= bubbleDelay) _talk = StartCoroutine(Talk());
        }

        private void LateUpdate()
        {
            if (_bubble == null || !_bubble.gameObject.activeSelf) return;
            _bubble.position = _snake.HeadWorldPosition + bubbleOffset;
            var cam = Camera.main;
            if (cam != null) _bubble.rotation = cam.transform.rotation;
        }

        /// <summary>Rắn chạy, popup, hướng dẫn...: tắt hết, mắt về giữa.</summary>
        private void Stop()
        {
            _idle = 0f;
            if (_glance != null) { StopCoroutine(_glance); _glance = null; }
            if (_talk != null) { StopCoroutine(_talk); _talk = null; }
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

        /// <summary>Bong bóng bật lên một lần rồi đứng yên tới khi rắn chạy [user].</summary>
        private IEnumerator Talk()
        {
            if (_icon == null || fruits == null || !PickFruit(out var kind)) yield break;
            var entry = fruits.Get(kind);
            _icon.sprite = entry == null ? null : entry.fieldSprite != null ? entry.fieldSprite : entry.sprite;
            _bubble.gameObject.SetActive(true);
            LateUpdate();
            yield return Tween.Run(popDuration, Tween.OutBack(2.5f), t => _bubble.localScale = _bubbleScale * t);
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
