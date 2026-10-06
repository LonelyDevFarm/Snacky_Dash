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
    /// First-levels tutorial [video HuongDan7lvDau, user's phone test; original TutorialScenario_Redesign,
    /// TutorialView_Redesign, TutorialController]:
    /// - Level 1: a hand shows the first swipe (right, ForcedDirection 3) with "Swipe to Collect all the
    ///   Berries!"; any other swipe does nothing [user].
    /// - Level 1: once the second wave is out and the snake stops, "COLLECT ALL FRUITS TO MAKE THEM REGROW".
    /// (The old version also had "BE CAREFUL NOT TO HIT YOURSELF..." on Level 3; the current one has not [user].)
    /// - NEW ITEM! / NEW BOOSTER! cards when a level brings a mechanic or a booster (UnlockPopups).
    /// - Level 3, on a bite (until the level is passed): "WHEN YOU HIT YOURSELF YOU WILL LOSE!" before NO WAY OUT.
    /// Messages wait for the snake to stop (EnqueueOneOffPopup / OnPlayerStoppedForPopup), dim the
    /// screen and go on a tap once "TAP TO CONTINUE!" has bounced in. They come back each time the level
    /// is played until it is passed (the user quit and came back: the hand was there again).
    /// The original is world-space (frame 9 x 3 units at y 17.1); here it is on the canvas at the
    /// size the video shows (about two thirds of the screen wide, a third down).
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

        [SerializeField] private UiSkin skin = new UiSkin();
        [Tooltip("The message is up this long before TAP TO CONTINUE! shows and a tap closes it (video: about 1 s).")]
        [SerializeField] private float tapDelay = 0.8f;
        [Tooltip("The lose message waits longer before TAP TO CONTINUE! (video: about 2 s).")]
        [SerializeField] private float loseTapDelay = 2f;
        [SerializeField] private float overlayAlpha = 0.85f; // video: the board goes well dark (0.75 looked too light)
        [Tooltip("NEW ITEM! / NEW BOOSTER! background (original black 0.99 under a fading canvas; video: the board just shows through).")]
        [SerializeField] private float cardBackgroundAlpha = 0.95f;
        [Tooltip("NEW ITEM! / NEW BOOSTER!: TAP TO CONTINUE! once the card has come in (about 1.6 s) and settled.")]
        [SerializeField] private float cardTapDelay = 2f;
        [Tooltip("Canvas size of the message frame (original 9 x 3 units; video: about two thirds of the screen wide).")]
        [SerializeField] private Vector2 frameSize = new Vector2(585f, 195f);
        [Tooltip("From the top of the screen to the message's centre (video: about 31 % of the height).")]
        [SerializeField] private float frameFromTop = 590f;
        [SerializeField] private float handHeight = 160f;
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

        private void ShowCard(UnlockPopups.Entry entry)
        {
            var card = UnlockPopups.BuildCard(_root, skin, entry, cardBackgroundAlpha);
            _session.InputBlocked = true;
            StartCoroutine(UnlockPopups.Animate(card, this));
            var tap = (RectTransform)card.Find("TapToContinue");
            bool canClose = false;
            StartCoroutine(After(cardTapDelay, () =>
            {
                tap.gameObject.SetActive(true);
                canClose = true;
                StartCoroutine(Bounce(tap));
            }));
            card.Find("Background").gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (!canClose) return;
                Destroy(card.gameObject);
                _session.InputBlocked = false;
            });
        }

        private static IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
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
                }
                yield return null;
            }
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
            if (_pending != null && !_open && _session.IsIdle && !_session.ForcedSwipe.HasValue && !_session.InputBlocked)
            {
                Open(_pending);
                _pending = null;
            }
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
        private static IEnumerator Bounce(RectTransform target)
        {
            float[] times = { 0f, 0.1333f, 0.25f, 0.3333f, 0.5f };
            float[] scales = { 0.5f, 1.1f, 0.9f, 1.06f, 1f };
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                int i = 1;
                while (i < times.Length - 1 && t > times[i]) i++;
                float k = Mathf.InverseLerp(times[i - 1], times[i], t);
                target.localScale = Vector3.one * Mathf.Lerp(scales[i - 1], scales[i], Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        /// <summary>
        /// Hand_Swipe_R (1 s loop at 60 fps): the hand slides 3.1 out in 0.1 s, holds, comes back by
        /// 0.45 s; the blue trail shows from 0.03 to 0.2 s, stretching out and in.
        /// </summary>
        private IEnumerator SwipeHand()
        {
            while (_hand != null && _hand.gameObject.activeSelf)
            {
                for (float t = 0f; t < 1f && _hand.gameObject.activeSelf; t += Time.deltaTime)
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

        // ------------------------------------------------------------------ layout

        private void Build()
        {
            _root = Canvas(transform, "Tutorial", 8);

            _overlay = Stretch(_root, "Overlay");
            Image(_overlay, null, new Color(0f, 0f, 0f, overlayAlpha), raycast: true);
            _overlay.gameObject.AddComponent<Button>().onClick.AddListener(Close);
            _overlay.gameObject.SetActive(false);

            // Tuto_Message: blue frame (S_Tutorial_BG tinted 3DADFF, 9 x 3) round a white panel (8.4 x 2.45).
            _frame = Rect(_root, "Tuto_Message", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -frameFromTop), frameSize);
            float unit = frameSize.x / 9f;
            var frameImage = Image(_frame, skin.tutorialFrame, new Color(0.239f, 0.678f, 1f), sliced: true);
            frameImage.pixelsPerUnitMultiplier = 100f / unit;
            var panel = Rect(_frame, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8.4f, 2.45f) * unit);
            Image(panel, skin.tutorialFrame, Color.white, sliced: true).pixelsPerUnitMultiplier = 100f / unit;
            var text = Stretch(panel, "Text");
            _text = Text(text, "", skin.font, skin.plainMaterial, 40f, Color.white);
            _text.enableAutoSizing = true;
            _text.fontSizeMin = 18f;
            _text.fontSizeMax = 40f;
            _text.richText = true;
            _frame.gameObject.SetActive(false);

            // TapToContinue: white with a dark outline, low on the screen (video: about three quarters down).
            _tap = Rect(_root, "TapToContinue", new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(800f, 90f));
            var tap = Text(_tap, "TAP TO CONTINUE!", skin.font, skin.plainMaterial, 54f, Color.white);
            tap.fontMaterial.EnableKeyword("OUTLINE_ON");
            tap.outlineWidth = 0.25f;
            tap.outlineColor = new Color32(40, 30, 60, 255);
            _tap.gameObject.SetActive(false);

            // Hand_SwipeTuto: screen centre (_yOffsetFromScreenCenter 0), trail behind the hand.
            var handRoot = Rect(_root, "TUTO_HAND_Parent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _hand = Rect(handRoot, "HandPivot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            float handWidth = handHeight * 169.9f / 234.9f;
            _trail = Rect(_hand, "TrailPivot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-handWidth * 0.3f, handHeight * 0.35f), new Vector2(handTravel * 1.6f, handHeight * 0.4f), new Vector2(1f, 0.5f));
            Image(_trail, skin.tutorialHandTrail, new Color(0.127f, 0.238f, 1f, 0.753f));
            var handSprite = Rect(_hand, "HandSprite", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(handWidth, handHeight), new Vector2(0.3f, 0.85f));
            Image(handSprite, skin.tutorialHand, Color.white).preserveAspect = true;
            _hand.gameObject.SetActive(false);
        }
    }
}
