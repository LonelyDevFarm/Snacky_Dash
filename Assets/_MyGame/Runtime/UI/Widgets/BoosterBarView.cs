using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Thanh booster dưới đáy và các luồng booster [video Lv7 và booster; gốc Game.unity _bottom/button_list,
    /// Boosters_Pop-up_Tutorial, BoosterTutorialView, BoosterData, LocData]:
    /// - Thanh: xẻng, boomerang, hoàn tác (LVL. 11), nam châm (LVL. 16); hàng giữa màn, ô rộng 150.6 ở tỉ lệ 0.85; nút cài đặt
    ///   nằm cạnh thanh HUD như màn 27 (video Man_Lose).
    /// - Chạm một booster: viền tối, băng tên (tên + mô tả LocData), bàn chơi co lại giữa băng và thanh. Xẻng: vòng tròn trên
    ///   mọi điểm dừng đào tới được, chạm một cái. Boomerang: mũi tên bấm được ở các điểm giao, chạm một cái. Hoàn tác và Nam
    ///   châm: CONFIRM. X để huỷ.
    /// - Thứ tự mở 4, 7, 11, 16 [user]. Hoàn tác hiện icon mờ khi chưa dùng được.
    /// - Lần dùng đầu mỗi booster (BoosterData _forcedTutorialEntry): tới lúc thì màn tối, chỉ còn booster đó và mũi tên tím
    ///   nhún phía trên (một lần); quà mở khoá hiện FREE tới khi dùng [user].
    /// Số booster nằm trong PlayerEconomy: 0 lúc đầu, 3 khi mở khoá, mua 3 một lần hoặc từ gói trong Shop [user].
    /// - Booster sáng tạo (BoosterConfig.creative) ở cuối thanh lướt ngang: Khay thần kỳ chọn khay bằng mũi tên như boomerang; không quà
    ///   mở khoá, không thẻ, không hướng dẫn, hết thì mua bằng dấu + [user 20/10].
    /// </summary>
    public class BoosterBarView : MonoBehaviour
    {
        private enum Booster { Shovel, Boomerang, Undo, Magnet, MagicCrate, Ghost }

        private sealed class Info
        {
            public Booster id;
            public string name, description, stockKey;
            public Sprite icon;
            public int unlockLevel;
            public int amount;     // booster sáng tạo: Rắn bóng ma = số lần xuyên thân (BoosterConfig)
            public bool confirm;  // used with the CONFIRM button (otherwise by tapping on the board)
            public BoosterButton view;
            public bool unlocked;
            public bool free;      // FREE on its unlock level until that level is passed [user]
            public string TutorialKey => stockKey + "_tutorial";
            public int Stock
            {
                get => SnackDash.Economy.PlayerEconomy.Shared.Boosters(stockKey);
                set => SnackDash.Economy.PlayerEconomy.Shared.SetBoosters(stockKey, value);
            }
        }


        // Bàn chơi thu vào giữa băng booster và thanh (canvas 1080x1920, khớp chiều cao).
        private const float BarHeight = 190f;
        private const float BannerHeight = 396.8f;

        [Tooltip("Prefabs/UI/Screens/Game/BoosterBar: thanh, 4 nút, băng chọn booster, lớp hướng dẫn.")]
        [SerializeField] private BoosterBarLayout barPrefab;
        [Tooltip("Vòng tím dưới đầu rắn khi nam châm chạy (TX_FX_Magnet_Base).")]
        [SerializeField] private Texture2D magnetField;
        [SerializeField] private float tutorialBobDuration = 0.3f;              // BoosterForcedTutorialView
        [SerializeField] private Vector2 tutorialBobOffset = new Vector2(0, -30);
        [SerializeField] private float boardRefitDuration = 0.3f;

        [Header("World effects (original prefabs)")]
        [SerializeField] private GameObject shovelCirclePrefab;   // GameObject/Booster_Tunnel_CircleToClick
        [SerializeField] private GameObject shovelHolePrefab;     // GameObject/Booster_Tunnel_Hole
        [SerializeField] private GameObject magnetFxPrefab;       // Resources/boosters/magnet/FX_Snake_Magnetic
        [SerializeField] private GameObject undoFxPrefab;         // Resources/boosters/shuffle/Vortex Big2
        [Tooltip("The booster trail (BoosterTrailsVFX layout = Crates/ExtraCrate_Trail_FX with the booster icon).")]
        [SerializeField] private GameObject trailPrefab;

        private readonly List<Info> _boosters = new List<Info>();
        private GameSession _session;
        private BoardCameraFitter _fitter;
        private Camera _camera;
        private Transform _snakeHead;
        private SnakeView _snake;
        private RectTransform _root, _bar, _aimPanel, _tutorialPanel, _confirmButton;
        private TMP_Text _bannerTitle, _bannerText;
        private Info _aiming;
        private bool _tutorialShown;
        private Info _tutorialBooster;
        private readonly Dictionary<int, GameObject> _arrows = new Dictionary<int, GameObject>();
        private readonly Dictionary<Vector2Int, GameObject> _circles = new Dictionary<Vector2Int, GameObject>();
        private GameObject _magnetFx, _hole;

        /// <summary>The bar hides while this is true (a tutorial step is up).</summary>
        public Func<bool> HideWhen { get; set; }

        /// <summary>Boomerang stock (tests and older callers).</summary>
        public static int Stock
        {
            get => SnackDash.Economy.PlayerEconomy.Shared.Boosters("booster_boomerang");
            set => SnackDash.Economy.PlayerEconomy.Shared.SetBoosters("booster_boomerang", value);
        }

        public void Bind(GameSession session, int levelNumber, BoardView board, BoardCameraFitter fitter, SnakeView snake)
        {
            _session = session;
            _fitter = fitter;
            _camera = Camera.main;
            _snake = snake; // GameBootstrap đưa vào (không tìm khắp scene)
            _snakeHead = _snake != null ? _snake.transform.Find("SnakeHead") : null;

            var layout = Instantiate(barPrefab, transform);
            _root = (RectTransform)layout.transform;
            var area = Screen.safeArea;
            layout.safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            layout.safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _bar = layout.bar;
            _aimPanel = layout.aimPanel;
            _tutorialPanel = layout.tutorialPanel;
            _bannerTitle = layout.bannerTitle;
            _bannerText = layout.bannerText;
            _confirmButton = (RectTransform)layout.confirmButton.transform;
            layout.closeButton.onClick.AddListener(() => SetAiming(null));
            layout.confirmButton.onClick.AddListener(OnConfirm);
            _aimPanel.gameObject.SetActive(false);
            _tutorialPanel.gameObject.SetActive(false);

            // Tên, level mở và lời mô tả ở Data/Config/BoosterConfig; undo và nam châm dùng bằng nút CONFIRM, hai cái kia bằng chạm lên bàn.
            var config = SnackDash.Economy.BoosterConfig.All;
            for (int i = 0; i < config.Length; i++)
                _boosters.Add(new Info { id = (Booster)i, name = config[i].name, description = config[i].description, unlockLevel = config[i].unlockLevel,
                    stockKey = SnackDash.Economy.PlayerEconomy.BoosterKeys[i], confirm = i == (int)Booster.Undo || i == (int)Booster.Magnet });
            for (int i = 0; i < _boosters.Count; i++)
            {
                var b = _boosters[i];
                b.view = layout.buttons[i];
                b.icon = b.view.Icon;
                b.unlocked = levelNumber >= b.unlockLevel;
                b.view.Setup(b.unlocked, b.unlockLevel);
                var tapped = b;
                b.view.Clicked += () => OnTapped(tapped);
                // The unlock level: FREE until that level is passed, and the unlock gives 3 once [user].
                b.free = levelNumber == b.unlockLevel;
                if (b.free && PlayerPrefs.GetInt(b.stockKey + "_gift", 0) == 0)
                {
                    SnackDash.Economy.PlayerEconomy.Shared.AddBoosters(b.stockKey, SnackDash.Economy.PlayerEconomy.BoosterPack);
                    PlayerPrefs.SetInt(b.stockKey + "_gift", 1);
                    PlayerPrefs.Save();
                }
            }

            var creative = SnackDash.Economy.BoosterConfig.Creative;
            for (int i = 0; i < creative.Length && i < layout.creativeButtons.Length; i++)
            {
                var b = new Info { id = Booster.MagicCrate + i, name = creative[i].name, description = creative[i].description, unlockLevel = creative[i].unlockLevel, amount = creative[i].amount,
                    stockKey = SnackDash.Economy.PlayerEconomy.CreativeBoosterKeys[i], view = layout.creativeButtons[i] };
                b.confirm = b.id == Booster.Ghost; // bóng ma dùng bằng CONFIRM như nam châm
                b.icon = b.view.Icon;
                b.unlocked = levelNumber >= b.unlockLevel;
                b.view.Setup(b.unlocked, b.unlockLevel);
                b.view.Clicked += () => OnTapped(b);
                _boosters.Add(b);
            }

            foreach (var stop in session.Board.CrateStops)
            {
                var cover = board != null ? board.GetCover(stop.Value) : null;
                var arrow = cover != null ? cover.transform.Find("ArrowCLICKABLE_DO_NOT_DELETE") : null;
                if (arrow != null) _arrows[stop.Key] = arrow.gameObject;
            }
            session.BoomerangThrown += (column, thrown) => SetAiming(null);
            session.MoveStarted += _ => SetAiming(null); // moving while a booster waits for its target or CONFIRM drops it, back to plain play (as the original) [user]
            session.ShovelDug += OnShovelDug;
            session.MagnetChanged += OnMagnetChanged;
            session.Stopped += cell => { if (session.Board.CrateColumnAt(cell) < 0) TryStartTutorial(); }; // at a crate the delivery comes first
            session.Rewound += OnRewound;
        }

        private Info Get(Booster id) => _boosters.Find(b => b.id == id);

        private void OnTapped(Info booster)
        {
            if (!_session.IsIdle || _session.InputBlocked || _session.InputLocked || _session.ForcedSwipe.HasValue) return; // not over a tutorial message or an effect
            if (!booster.free && booster.Stock <= 0)
            {
                SnackDash.Core.Services.Get<SnackDash.Core.UIManager>().Show<BoosterBuyPopup>()?.Setup(booster.name, booster.description, booster.icon, booster.stockKey, null); // the + : buy 3 for 1800 [user]
                return;
            }
            if (booster.id == Booster.Undo && !_session.CanUndo) return;
            if (booster.id == Booster.Magnet && _session.MagnetActive) return;
            if (booster.id == Booster.Ghost && _session.GhostActive) return;
            SetAiming(_aiming == booster ? null : booster);
        }

        private void SetAiming(Info booster)
        {
            if (_aiming == booster) return;
            _aiming = booster;
            _aimPanel.gameObject.SetActive(booster != null);
            ClearCircles();
            if (booster != null)
            {
                HideTutorial();
                _bannerTitle.text = booster.name;
                _bannerText.text = booster.description;
                _confirmButton.gameObject.SetActive(booster.confirm);
                if (booster.id == Booster.Shovel) ShowCircles();
            }
            if (_fitter != null)
            {
                if (booster != null) _fitter.FitBetween(BarHeight, 1920f - BannerHeight, boardRefitDuration);
                else _fitter.FitDefault(boardRefitDuration);
            }
        }

        private void ShowCircles()
        {
            if (shovelCirclePrefab == null) return;
            foreach (var cell in _session.ShovelTargets())
            {
                var circle = Instantiate(shovelCirclePrefab, transform);
                circle.transform.position = BoardView.CellToWorld(cell);
                _circles[cell] = circle;
            }
        }

        private void ClearCircles()
        {
            foreach (var circle in _circles.Values) if (circle != null) Destroy(circle);
            _circles.Clear();
        }

        /// <summary>CONFIRM (Undo, Magnet): the icon flies into the snake's mouth, then the booster acts.</summary>
        private void OnConfirm()
        {
            var booster = _aiming;
            if (booster == null || !_session.IsIdle) return;
            SetAiming(null);
            StartCoroutine(FlyIcon(booster, () =>
            {
                bool used = booster.id == Booster.Undo ? _session.Undo() : booster.id == Booster.Ghost ? _session.UseGhost(booster.amount) : _session.UseMagnet();
                if (used) Spend(booster);
            }));
        }

        private Vector3 BoardCentre()
        {
            if (_camera == null) _camera = Camera.main;
            var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            return new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
        }

        /// <summary>
        /// BoosterPreUseAnimationCoordinator + BoosterTrailsVFX (original Game.unity), for every booster: the booster's trail
        /// (ExtraCrate_Trail_FX's layout with the booster icon: icon particle, soft trail, sparkles) flies on the world plane
        /// z = -2 from its button to a point 1.4 in front of the snake's head in 0.5 s, X on InOutCubic and Y on OutQuad (it
        /// rises first, then comes over), shrinking from 1 to 0.35; the snake starts eating 0.3 s before it arrives; then it
        /// sinks into the head in 0.2 s (linear, to 0) and the booster acts. Input is locked meanwhile.
        /// </summary>
        private IEnumerator FlyIcon(Info booster, Action arrived)
        {
            const float flyDuration = 0.5f, flyZ = -2f, startScale = 1f, endScale = 0.35f, sinkDuration = 0.2f, eatLead = 0.3f;
            var approachOffset = new Vector3(0f, 1.4f, 0f);
            if (_camera == null) _camera = Camera.main;
            // The booster acts right after the bite (the eat starts eatLead before arrival), one after the other with no
            // pause [user: at the sink it was too early, after the whole clip too late].
            float chew = _snake != null ? Mathf.Max(0f, SnakeView.EatBoosterSwallowed - eatLead - sinkDuration) : 0f;
            _session.LockInput(flyDuration + sinkDuration + chew + 0.1f);
            var canvas = _root.GetComponent<Canvas>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, booster.view.transform.position);
            Vector3 OnPlane(Vector3 point) => new Vector3(point.x, point.y, flyZ);
            var ray = _camera.ScreenPointToRay(screen);
            var start = new Plane(Vector3.forward, new Vector3(0f, 0f, flyZ)).Raycast(ray, out float distance) ? ray.GetPoint(distance) : OnPlane(BoardCentre());
            Vector3 Head() => _snakeHead != null ? _snakeHead.position : BoardCentre();
            Vector3 Approach() => OnPlane(_snakeHead != null ? _snakeHead.TransformPoint(approachOffset) : BoardCentre());

            var trail = trailPrefab != null ? Instantiate(trailPrefab, start, Quaternion.identity) : null;
            var baseScale = trail != null ? trail.transform.localScale : Vector3.one;
            if (trail != null)
                foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var sheet = ps.textureSheetAnimation;
                    if (sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 0 && ps.name.EndsWith("Sprite")) sheet.SetSprite(0, booster.icon);
                }

            bool eating = false;
            for (float time = 0f; time < flyDuration; time += Time.deltaTime)
            {
                float t = time / flyDuration;
                var end = Approach();
                if (trail != null)
                {
                    trail.transform.position = new Vector3(Mathf.Lerp(start.x, end.x, Tween.InOutCubic(t)), Mathf.Lerp(start.y, end.y, Tween.OutQuad(t)), flyZ);
                    trail.transform.localScale = baseScale * Mathf.Lerp(startScale, endScale, t);
                }
                if (!eating && time >= flyDuration - eatLead) { eating = true; if (_snake != null) _snake.EatBooster(); }
                yield return null;
            }
            if (!eating && _snake != null) _snake.EatBooster();
            var from = Approach();
            for (float time = 0f; time < sinkDuration; time += Time.deltaTime)
            {
                float t = time / sinkDuration;
                if (trail != null)
                {
                    trail.transform.position = Vector3.Lerp(from, Head(), t);
                    trail.transform.localScale = baseScale * Mathf.Lerp(endScale, 0f, t);
                }
                yield return null;
            }
            if (trail != null)
            {
                foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                trail.transform.localScale = Vector3.zero;
                Destroy(trail, 1f); // the trail and sparkles fade out
            }
            if (chew > 0f) yield return new WaitForSeconds(chew);
            arrived();
        }

        private Vector2 ScreenToCanvas(Vector2 screen)
        {
            var canvas = _root.GetComponent<Canvas>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            return local;
        }

        // ------------------------------------------------------------------ world effects

        /// <summary>Booster_Tunnel_Hole: TunnelEntrance at the head's stop, TunnelExit at the chosen one; both burst open.</summary>
        private void OnShovelDug(Vector2Int from, Vector2Int to)
        {
            SetAiming(null);
            var shovel = Get(Booster.Shovel);
            Spend(shovel);
            if (shovelHolePrefab == null) return;
            if (_hole != null) Destroy(_hole);
            _hole = Instantiate(shovelHolePrefab, transform);
            _hole.transform.position = BoardView.CellToWorld(from);
            var exit = _hole.transform.Find("TunnelExit");
            if (exit != null) exit.position = BoardView.CellToWorld(to);
        }

        /// <summary>FX_Snake_Magnetic rides on the head while the magnet works (FX_ActivateMagnetic once at the start).</summary>
        private GameObject _magnetRing;

        private void FollowHead()
        {
            if (_magnetRing == null || _snakeHead == null) return;
            var p = _snakeHead.localPosition;
            _magnetRing.transform.localPosition = new Vector3(p.x, p.y, 0f);
        }

        private void OnMagnetChanged(bool on)
        {
            if (!on)
            {
                if (_magnetFx != null) Destroy(_magnetFx);
                if (_magnetRing != null) Destroy(_magnetRing);
                _magnetFx = null; _magnetRing = null;
                return;
            }
            if (magnetFxPrefab == null || _snakeHead == null) return;
            _magnetFx = Instantiate(magnetFxPrefab, _snakeHead);
            _magnetFx.transform.localPosition = Vector3.zero;
            _magnetFx.transform.localRotation = Quaternion.identity;
            var activate = _magnetFx.transform.Find("FX_Container/FX_ActivateMagnetic");
            if (activate != null && activate.TryGetComponent<ParticleSystem>(out var burst)) burst.Play(true);
            // The ground ring is a URP decal in the original (no decal feature here): a flat purple circle instead.
            // It sits on the board, not under the FX prefab (whose own scale shrank it), follows the head in
            // Update, and like the decal it is drawn over the road, the grass and the fruit [user: it was small and covered].
            if (magnetField == null) return;
            var ring = new GameObject("Magnet Ring");
            ring.transform.SetParent(_snakeHead.parent, false);
            var sprite = ring.AddComponent<SpriteRenderer>();
            var texture = magnetField;
            sprite.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            sprite.color = new Color(0.616f, 0.353f, 1f, 0.5f); // MT_FX_Magnet_Projection _ColorA, a bit under its _Alpha 0.75
            sprite.material = new Material(Shader.Find("UI/Default"));
            sprite.material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            sprite.sortingOrder = 50;
            ring.transform.localScale = Vector3.one * (GameSession.MagnetRadius * 2f); // the sprite is one cell wide
            _magnetRing = ring;
            FollowHead();
        }

        /// <summary>The booster video: a big swirl over the board as everything rewinds.</summary>
        private void OnRewound()
        {
            if (undoFxPrefab == null) return;
            var swirl = Instantiate(undoFxPrefab, transform);
            swirl.transform.position = BoardCentre() + new Vector3(0f, 0f, -2f);
            foreach (var particles in swirl.GetComponentsInChildren<ParticleSystem>()) particles.Play(true);
            Destroy(swirl, 2f);
        }

        // ------------------------------------------------------------------ forced tutorial (first use of each booster)

        /// <summary>
        /// BoosterData _forcedTutorialEntry: the shovel waits for a tail of 9 (_delayedTailLengthThreshold),
        /// the boomerang 36 (Level 7: every fruit eaten) and a crate it can feed, undo 1 (and something to
        /// undo), the magnet comes at once (_flowType 1).
        /// </summary>
        private bool TutorialReady(Info booster)
        {
            switch (booster.id)
            {
                case Booster.Shovel: return _session.Segments.Count >= 9 && _session.ShovelTargets().Count > 0;
                case Booster.Boomerang: return _session.Pills.Count == 0 && AnyColumnFeedable();
                case Booster.Undo: return _session.Segments.Count >= 1 && _session.CanUndo;
                default: return true;
            }
        }

        /// <summary>A use: FREE on the unlock level (the stock stays), otherwise one from the stock.</summary>
        private void Spend(Info booster)
        {
            if (!booster.free) booster.Stock -= 1;
        }

        /// <summary>
        /// The forced first use, once its booster is ready. Also run the moment the snake stops (before a buffered swipe
        /// is applied), so a fast swipe cannot carry the snake past it [user].
        /// </summary>
        private void TryStartTutorial()
        {
            if (_tutorialShown || _aiming != null || !_session.IsIdle || _session.InputBlocked || _session.InputLocked || _session.ForcedSwipe.HasValue) return;
            foreach (var booster in _boosters)
                if (booster.unlocked && booster.free && PlayerPrefs.GetInt(booster.TutorialKey, 0) == 0 && TutorialReady(booster)) { ShowTutorial(booster); break; }
        }

        /// <summary>BoosterForcedTutorialView: dim everything but the booster, purple arrow bobbing over it. Once [user].</summary>
        private void ShowTutorial(Info booster)
        {
            _tutorialShown = true;
            _tutorialBooster = booster;
            _session.SwipesHeld = true; // only the booster now
            PlayerPrefs.SetInt(booster.TutorialKey, 1);
            PlayerPrefs.Save();
            var slot = (RectTransform)booster.view.transform;
            _tutorialPanel.gameObject.SetActive(true);
            _tutorialPanel.SetAsLastSibling(); // over the bar, so only the lifted slot stays lit
            // The slot stays in the bar (moving it to the panel lost its place when the screen changed size) and is
            // drawn above the dim by a nested canvas; the arrow rides on it, in panel units (215 above, 160 big).
            var lift = slot.GetComponent<Canvas>();
            if (lift == null) lift = slot.gameObject.AddComponent<Canvas>();
            lift.overrideSorting = true;
            lift.sortingOrder = _root.GetComponent<Canvas>().sortingOrder + 1;
            if (slot.GetComponent<GraphicRaycaster>() == null) slot.gameObject.AddComponent<GraphicRaycaster>();
            // Mũi tên tím (sẵn trong nút, tắt) nhún lên xuống, theo đơn vị của lớp tối (nút thu 0.85).
            var arrow = booster.view.Arrow;
            arrow.gameObject.SetActive(true);
            StartCoroutine(Bob(arrow, 1f / slot.localScale.x));
        }

        private IEnumerator Bob(RectTransform arrow, float scale = 1f)
        {
            var start = arrow.anchoredPosition;
            var offset = tutorialBobOffset * scale;
            while (arrow != null && arrow.gameObject.activeInHierarchy)
            {
                yield return Tween.Run(tutorialBobDuration, Tween.InOutSine, t => { if (arrow != null) arrow.anchoredPosition = start + offset * t; });
                yield return Tween.Run(tutorialBobDuration, Tween.InOutSine, t => { if (arrow != null) arrow.anchoredPosition = start + offset * (1f - t); });
            }
        }

        private void HideTutorial()
        {
            if (!_tutorialPanel.gameObject.activeSelf) return;
            _session.SwipesHeld = false;
            var slot = (RectTransform)_tutorialBooster.view.transform;
            var lift = slot.GetComponent<Canvas>();
            if (lift != null) lift.overrideSorting = false; // back under the bar's own order
            var arrow = _tutorialBooster.view.Arrow;
            arrow.gameObject.SetActive(false);
            _tutorialPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_session == null) return;
            bool hide = HideWhen != null && HideWhen();
            if (_bar.gameObject.activeSelf == hide) _bar.gameObject.SetActive(!hide);
            // Số còn lại / FREE / dấu +; Undo xám và mờ ngay sau booster / undo, tới lần vuốt mới [user]; rắn chạy thì không mờ.
            foreach (var booster in _boosters)
                if (booster.unlocked) booster.view.Show(booster.free, booster.Stock, booster.id != Booster.Undo || _session.UndoReady);

            FollowHead();

            // The shovel's holes go once nothing of the snake is underground any more.
            if (_hole != null && !_session.HeadInTunnel && !AnySegmentUnderground()) { Destroy(_hole); _hole = null; }

            var boomerang = Get(Booster.Boomerang);
            TryStartTutorial();

            bool magic = _aiming != null && _aiming.id == Booster.MagicCrate;
            bool arrows = false;
            foreach (var pair in _arrows)
            {
                bool show = (_aiming == boomerang && _session.CanBoomerang(pair.Key)) || (magic && _session.CanMagicCrate(pair.Key));
                arrows |= show;
                if (pair.Value.activeSelf != show) pair.Value.SetActive(show);
            }
            if (_aiming == null || !UnityEngine.Input.GetMouseButtonDown(0) || IsOverUi()) return;

            if (_aiming.id == Booster.Shovel)
            {
                var cell = CircleUnder(UnityEngine.Input.mousePosition);
                if (!cell.HasValue) return;
                // The shovel too flies into the snake before it digs (Shovel Trail FX in the original) [user].
                var shovel = _aiming;
                SetAiming(null);
                StartCoroutine(FlyIcon(shovel, () => _session.UseShovel(cell.Value)));
                return;
            }
            if ((_aiming != boomerang && !magic) || !arrows) return;
            int column = ArrowUnder(UnityEngine.Input.mousePosition);
            if (magic)
            {
                // Khay thần kỳ: bay vào miệng rắn như mọi booster rồi khay được lấp.
                if (column < 0 || !_session.CanMagicCrate(column)) return;
                var crate = _aiming;
                SetAiming(null);
                StartCoroutine(FlyIcon(crate, () => { if (_session.UseMagicCrate(column)) Spend(crate); }));
                return;
            }
            if (column < 0 || !_session.CanBoomerang(column)) return;
            SetAiming(null);
            // Like the others, the boomerang flies into the snake's mouth before it is thrown [user].
            StartCoroutine(FlyIcon(boomerang, () =>
            {
                if (!_session.UseBoomerang(column)) return;
                Spend(boomerang);
            }));
        }

        private bool AnySegmentUnderground()
        {
            for (int i = 0; i < _session.Segments.Count; i++)
                if (_session.SegmentInTunnel(i)) return true;
            return false;
        }

        private bool AnyColumnFeedable()
        {
            for (int c = 0; c < _session.Columns.Count; c++)
                if (_session.CanBoomerang(c)) return true;
            return false;
        }

        private static bool IsOverUi()
        {
            var events = EventSystem.current;
            if (events == null) return false;
            if (UnityEngine.Input.touchCount > 0) return events.IsPointerOverGameObject(UnityEngine.Input.GetTouch(0).fingerId);
            return events.IsPointerOverGameObject();
        }

        /// <summary>Shovel circle nearest the tap on the board plane, within about a cell.</summary>
        private Vector2Int? CircleUnder(Vector3 screen)
        {
            if (_camera == null) _camera = Camera.main;
            var ray = _camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance)) return null;
            var point = (Vector2)ray.GetPoint(distance);
            Vector2Int? best = null;
            float bestDistance = 1f;
            foreach (var cell in _circles.Keys)
            {
                float d = Vector2.Distance(point, cell);
                if (d < bestDistance) { bestDistance = d; best = cell; }
            }
            return best;
        }

        /// <summary>Column whose clickable arrow (CapsuleCollider on the delivery point) is under the tap.</summary>
        private int ArrowUnder(Vector3 screen)
        {
            if (_camera == null) _camera = Camera.main;
            var ray = _camera.ScreenPointToRay(screen);
            foreach (var hit in Physics.RaycastAll(ray, 500f))
                foreach (var pair in _arrows)
                    if (hit.collider.transform == pair.Value.transform || hit.collider.transform.IsChildOf(pair.Value.transform))
                        return pair.Key;
            return -1;
        }
    }
}
