using System.Collections;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Hướng dẫn mấy màn đầu [video HuongDan7lvDau, bạn thử trên điện thoại; gốc TutorialScenario_Redesign,
    /// TutorialView_Redesign, TutorialController]:
    /// - Màn 1: bàn tay chỉ cú vuốt đầu (sang phải, ForcedDirection 3) với "Swipe to Collect all the Berries!"; vuốt hướng
    ///   khác không có tác dụng [user].
    /// - Màn 1: khi đợt trái thứ hai ra và rắn dừng: "COLLECT ALL FRUITS TO MAKE THEM REGROW".
    ///   (Bản cũ còn "BE CAREFUL NOT TO HIT YOURSELF..." ở màn 3; bản hiện tại không có [user].)
    /// - Thẻ NEW ITEM! / NEW BOOSTER! khi màn mở cơ chế hay booster mới (UnlockPopups).
    /// - Màn 3, khi cắn đuôi (tới khi qua màn): "WHEN YOU HIT YOURSELF YOU WILL LOSE!" trước NO WAY OUT.
    /// Lời nhắn chờ rắn dừng (EnqueueOneOffPopup / OnPlayerStoppedForPopup), làm tối màn hình và đi tiếp khi chạm, sau khi
    /// "TAP TO CONTINUE!" nảy vào. Chúng hiện lại mỗi lần chơi màn đó cho tới khi qua màn (bạn thoát rồi vào lại: bàn tay vẫn
    /// có). Bản gốc đặt trong không gian thế giới (khung 9 x 3 đơn vị ở y 17.1); ở đây đặt trên canvas đúng cỡ video (rộng
    /// khoảng 2/3 màn hình, cách trên 1/3).
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        // TutorialView prefabs, LocData (prefab_tuto_message_*).
        private const string SwipeMessage = "<color=#446890>Swipe to Collect all the</color>\n<color=#FF6567>Berries!</color>";
        private const string NewBatchMessage = "<color=#446890>COLLECT ALL FRUITS</color>\n<color=#FF6567>TO MAKE THEM REGROW</color>";
        private const string LoseMessage = "<color=#446890>WHEN YOU</color> <color=#FF6567>\nHIT YOURSELF</color>\n<color=#446890>YOU WILL LOSE!</color>";

        // TutorialConfig comes from remote config: levels and thresholds read off the video.
        private const int ForcedSwipeLevel = 1, NewBatchLevel = 1, NewBatchIndex = 1;
        private const string BoosterIntroKey = "booster_intro_";
        private const int LoseTutorialLevel = 3; // TutorialScenario _loseTutorialLevel 2 (0-based)

        [Tooltip("Prefabs/UI/Screens/Game/TutorialLayer: nền tối, khung lời nhắn, TAP TO CONTINUE!, bàn tay.")]
        [SerializeField] private TutorialLayer layerPrefab;
        [Tooltip("The message is up this long before TAP TO CONTINUE! shows and a tap closes it (video: about 1 s).")]
        [SerializeField] private float tapDelay = 0.8f;
        [Tooltip("The lose message waits longer before TAP TO CONTINUE! (video: about 2 s).")]
        [SerializeField] private float loseTapDelay = 2f;
        [SerializeField] private float overlayAlpha = 0.85f; // video: the board goes well dark (0.75 looked too light)
        [Tooltip("How far the hand slides (Hand_Swipe_R: 3.1 units at scale 0.4).")]
        [SerializeField] private float handTravel = 120f;

        [Tooltip("Level 1 forced swipe: half-width of the lit lane along the road, in cells, and its soft edge.")]
        [SerializeField] private float pathLaneRadius = 0.6f;
        [SerializeField] private float pathLaneFeather = 0.25f;

        private GameSession _session;
        private RawImage _pathOverlay;
        private int _level;
        private RectTransform _root, _overlay, _frame, _tap, _hand, _trail;
        private TMP_Text _text;
        private string _pending;
        private bool _newBatchShown, _open, _canClose;

        public void Bind(GameSession session, int levelNumber)
        {
            _session = session;
            session.Stopped += cell => { if (session.Board.CrateColumnAt(cell) < 0) TryOpenPending(); }; // at a crate the delivery comes first
            _level = levelNumber;
            Build();
            // NEW ITEM! each time the level starts until it is passed; NEW BOOSTER! once [user].
            if (UnlockPopups.TryGet(_level, false, out var mechanic)) ShowCard(mechanic);
            else if (UnlockPopups.TryGet(_level, true, out var booster) && PlayerPrefs.GetInt(BoosterIntroKey + booster.name, 0) == 0)
            {
                PlayerPrefs.SetInt(BoosterIntroKey + booster.name, 1);
                PlayerPrefs.Save();
                ShowCard(booster);
            }
            if (_level == ForcedSwipeLevel)
            {
                session.ForcedSwipe = Direction.Right;
                StartCoroutine(ShowPathOverlay(Direction.Right));
                ShowFrame(SwipeMessage);
                _hand.gameObject.SetActive(true);
                StartCoroutine(SwipeHand());
                session.MoveStarted += OnFirstMove;
            }
        }

        /// <summary>Thẻ NEW ITEM! / NEW BOOSTER!: bàn chơi không nhận vuốt tới khi thẻ đóng.</summary>
        private void ShowCard(UnlockPopups.Entry entry)
        {
            var card = SnackDash.Core.Services.Get<SnackDash.Core.UIManager>().Show<UnlockCardPopup>();
            if (card == null) return;
            _session.InputBlocked = true;
            card.Closed += () => _session.InputBlocked = false;
            card.Setup(entry);
        }

        /// <summary>
        /// TutorialPathOverlayService: the screen goes dark but for a rounded, soft-edged lane along the road
        /// the forced swipe runs (head to the next stop) [user screenshot]. Drawn as a screen-sized mask
        /// (quarter resolution, smooth), remade whenever the camera moves.
        /// </summary>
        private IEnumerator ShowPathOverlay(Direction direction)
        {
            var cam = Camera.main;
            if (cam == null) yield break;
            var rect = Stretch(_root, "PathOverlay");
            rect.SetAsFirstSibling(); // under the message and the hand
            _pathOverlay = rect.gameObject.AddComponent<RawImage>();
            _pathOverlay.raycastTarget = false;
            _pathOverlay.color = Color.clear;
            // The camera may still be settling at the level start: remake the mask whenever it has moved.
            Vector3 bakedAt = Vector3.positiveInfinity;
            float bakedSize = -1f;
            while (_pathOverlay != null && _session.ForcedSwipe.HasValue)
            {
                if (cam.transform.position != bakedAt || !Mathf.Approximately(cam.orthographicSize, bakedSize))
                {
                    bakedAt = cam.transform.position;
                    bakedSize = cam.orthographicSize;
                    if (_pathOverlay.texture != null) Destroy(_pathOverlay.texture);
                    _pathOverlay.texture = BakePathMask(cam, direction);
                    _pathOverlay.color = Color.white;
                    PlaceHandOnHead(cam);
                }
                yield return null;
            }
        }

        /// <summary>
        /// The original puts the hand at the screen's centre, which on our Level 1 board is an empty road far from the
        /// snake; the fingertip goes on the snake's head instead, so the swipe starts where the snake is [user].
        /// </summary>
        private const float HandAhead = 0.6f; // in cells

        private void PlaceHandOnHead(Camera cam)
        {
            var parent = (RectTransform)_hand.parent;
            // A little ahead of the head, toward the swipe (on it the hand sat too close) [user].
            var cell = _session.CurrentCell;
            var screen = cam.WorldToScreenPoint(Vector3.Lerp(BoardView.CellToWorld(cell), BoardView.CellToWorld(cell + Vector2Int.right), HandAhead));
            var canvas = _root.GetComponent<Canvas>();
            var eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, eye, out var local))
                parent.anchoredPosition = local;
        }

        private Texture2D BakePathMask(Camera cam, Direction direction)
        {
            var path = _session.PreviewPath(direction);
            var points = new Vector2[path.Count];
            for (int i = 0; i < path.Count; i++) points[i] = BoardView.CellToWorld(path[i]);
            float cell = Vector2.Distance(BoardView.CellToWorld(Vector2Int.zero), BoardView.CellToWorld(Vector2Int.right));
            float radius = pathLaneRadius * cell, feather = pathLaneFeather * cell;
            const int Step = 4;
            int width = Mathf.Max(1, Screen.width / Step), height = Mathf.Max(1, Screen.height / Step);
            var pixels = new Color32[width * height];
            byte dark = (byte)(overlayAlpha * 255f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // The camera is tilted: follow the ray down to the board plane (z = 0).
                var ray = cam.ScreenPointToRay(new Vector3((x + 0.5f) * Step, (y + 0.5f) * Step, 0f));
                Vector2 world = Mathf.Abs(ray.direction.z) > 1e-5f ? (Vector2)ray.GetPoint(-ray.origin.z / ray.direction.z) : (Vector2)ray.origin;
                float d = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[Mathf.Min(i + 1, points.Length - 1)];
                    var ab = b - a;
                    float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(world - a, ab) / ab.sqrMagnitude) : 0f;
                    d = Mathf.Min(d, Vector2.Distance(world, a + ab * t));
                }
                pixels[y * width + x] = new Color32(0, 0, 0, (byte)(dark * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius + feather, d))));
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private void OnFirstMove(Direction direction)
        {
            _session.MoveStarted -= OnFirstMove;
            if (_pathOverlay != null) Destroy(_pathOverlay.gameObject);
            _hand.gameObject.SetActive(false);
            _frame.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_session == null || _session.IsStuck || _session.IsWon) return;
            if (_level == NewBatchLevel && !_newBatchShown && _session.BatchIndex >= NewBatchIndex)
            {
                _newBatchShown = true;
                _pending = NewBatchMessage;
            }
            TryOpenPending();
        }

        /// <summary>A waiting message opens once the snake is idle; also tried the moment it stops, before a buffered swipe runs [user].</summary>
        private void TryOpenPending()
        {
            if (_pending == null || _open || !_session.IsIdle || _session.ForcedSwipe.HasValue || _session.InputBlocked || _session.InputLocked) return;
            Open(_pending);
            _pending = null;
        }

        // ------------------------------------------------------------------ messages

        /// <summary>
        /// A bite on Level 3, until the level is passed [user]: the screen dims with "WHEN YOU HIT YOURSELF YOU WILL LOSE!"
        /// before NO WAY OUT (LoseTutorialView; video: TAP TO CONTINUE! about 2 s later). Null otherwise.
        /// </summary>
        /// <summary>Level 3 teaches the snip: NO WAY OUT has no GIVE UP there [user].</summary>
        public bool ForcedSnip => _level == LoseTutorialLevel;

        /// <summary>The booster bar hides while a tutorial step is up (forced swipe, messages, the Level 3 lose flow) [user].</summary>
        public bool HidesBoosterBar => _session != null && (_session.ForcedSwipe.HasValue || _open || (ForcedSnip && _session.IsStuck));

        public IEnumerator LoseTutorial()
        {
            if (_level != LoseTutorialLevel) return null; // a mechanic intro: every bite until the level is passed [user]
            return RunLoseTutorial();
        }

        private IEnumerator RunLoseTutorial()
        {
            Open(LoseMessage, loseTapDelay);
            while (_open) yield return null;
        }

        private void Open(string message) => Open(message, tapDelay);

        private void Open(string message, float delay)
        {
            _open = true;
            _canClose = false;
            _session.InputBlocked = true;
            _overlay.gameObject.SetActive(true);
            ShowFrame(message);
            StartCoroutine(ShowTapToContinue(delay));
        }

        private IEnumerator ShowTapToContinue(float delay)
        {
            yield return new WaitForSeconds(delay);
            _tap.gameObject.SetActive(true);
            _canClose = true;
            yield return Bounce(_tap);
        }

        private void Close()
        {
            if (!_open || !_canClose) return;
            _open = false;
            _session.InputBlocked = false;
            _overlay.gameObject.SetActive(false);
            _frame.gameObject.SetActive(false);
            _tap.gameObject.SetActive(false);
        }

        private void ShowFrame(string message)
        {
            _text.text = message;
            _frame.gameObject.SetActive(true);
            StartCoroutine(Bounce(_frame));
        }

        /// <summary>Tuto_Msg_Bounce: scale 0.5, 1.1 (0.13 s), 0.9 (0.25 s), 1.06 (0.33 s), 1 (0.5 s).</summary>
        public static IEnumerator Bounce(RectTransform target)
        {
            float[] times = { 0f, 0.1333f, 0.25f, 0.3333f, 0.5f };
            float[] scales = { 0.5f, 1.1f, 0.9f, 1.06f, 1f };
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (target == null) yield break; // closed while it was still bouncing
                int i = 1;
                while (i < times.Length - 1 && t > times[i]) i++;
                float k = Mathf.InverseLerp(times[i - 1], times[i], t);
                target.localScale = Vector3.one * Mathf.Lerp(scales[i - 1], scales[i], Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
        }

        /// <summary>
        /// Hand_Swipe_R (1 s loop at 60 fps): the hand slides 3.1 out in 0.1 s, holds, comes back by
        /// 0.45 s; the blue trail shows from 0.03 to 0.2 s, stretching out and in.
        /// </summary>
        private IEnumerator SwipeHand()
        {
            while (_hand != null && _hand.gameObject.activeSelf)
            {
                for (float t = 0f; t < 1f && _hand != null && _trail != null && _hand.gameObject.activeSelf; t += Time.deltaTime)
                {
                    float x;
                    if (t < 0.1f) x = Mathf.Lerp(0f, 3.1f, t / 0.1f);
                    else if (t < 0.2f) x = Mathf.Lerp(3.1f, 3f, (t - 0.1f) / 0.1f);
                    else if (t < 0.45f) x = Mathf.Lerp(3f, 0f, (t - 0.2f) / 0.25f);
                    else x = 0f;
                    _hand.anchoredPosition = new Vector2(x / 3.1f * handTravel, 0f);
                    bool trail = t >= 0.033f && t < 0.2f;
                    _trail.gameObject.SetActive(trail);
                    float stretch = t < 0.1f ? Mathf.Lerp(0.08f, 1f, t / 0.1f) : Mathf.Lerp(1f, 0.11f, (t - 0.1f) / 0.1f);
                    _trail.localScale = new Vector3(stretch, 1f, 1f);
                    yield return null;
                }
            }
        }

        // ------------------------------------------------------------------ bố cục (prefab TutorialLayer)

        private void Build()
        {
            var layer = Instantiate(layerPrefab, transform);
            _root = (RectTransform)layer.transform;
            _overlay = (RectTransform)layer.overlay.transform;
            layer.overlay.onClick.AddListener(Close);
            _frame = layer.frame;
            _text = layer.text;
            _tap = layer.tap;
            _hand = layer.hand;
            _trail = layer.trail;
        }
    }
}
