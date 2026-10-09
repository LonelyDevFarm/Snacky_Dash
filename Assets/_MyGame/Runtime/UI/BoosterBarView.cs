using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Bottom booster bar and the booster flows [Lv7 and booster videos; original Game.unity
    /// _bottom/button_list, Boosters_Pop-up_Tutorial, BoosterTutorialView, BoosterData, LocData].
    /// - Bar: shovel, boomerang, undo (LVL. 11), magnet (LVL. 16); a centred row, slots 150.6 wide at
    ///   scale 0.85; settings sit by the HUD bar, as on Level 27 (Man_Lose video).
    /// - Tapping a booster: dark vignette, its banner (name + LocData description), the board refits
    ///   between banner and bar. Shovel: a circle on every stop it can dig to, tap one. Boomerang: the
    ///   delivery points' clickable arrows, tap one. Undo and Magnet: CONFIRM. X cancels.
    /// - Unlock order 4, 7, 11, 16 [user]. Undo shows its disabled icon while it cannot be used.
    /// - First use of each booster (BoosterData _forcedTutorialEntry): when its moment comes the screen
    ///   dims, only that booster stays and a purple arrow bobs over it (once); the unlock gift shows FREE
    ///   until it is used [user].
    /// Stock lives in PlayerEconomy: 0 to start, 3 when a booster unlocks, bought 3 at a time or from Shop packs [user].
    /// </summary>
    public class BoosterBarView : MonoBehaviour
    {
        private enum Booster { Shovel, Boomerang, Undo, Magnet }

        private sealed class Info
        {
            public Booster id;
            public string name, description, stockKey;
            public Sprite icon;
            public int unlockLevel;
            public bool confirm;  // used with the CONFIRM button (otherwise by tapping on the board)
            public RectTransform slot;
            public GameObject countPanel, plusIcon, freePanel;
            public TMP_Text count;
            public Image iconImage;
            public bool free;      // FREE on its unlock level until that level is passed [user]
            public string TutorialKey => stockKey + "_tutorial";
            public int Stock
            {
                get => SnackDash.Economy.PlayerEconomy.Shared.Boosters(stockKey);
                set => SnackDash.Economy.PlayerEconomy.Shared.SetBoosters(stockKey, value);
            }
        }

        public const int BoomerangUnlockLevel = 7;
        private const string Highlight = "#8929F6"; // LocData booster descriptions

        // Original layout (canvas 1080x1920, match height).
        private const float BarHeight = 190f;
        private const float SlotWidth = 150.6f, SlotHeight = 138f, SlotScale = 0.85f;
        // Four slots, no settings in the row: spread as on Level 27 of the original (centres ~214 apart).
        private const float Spacing = 64f;
        private const float BannerHeight = 396.8f;

        [SerializeField] private UiSkin skin = new UiSkin();
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

        public void Bind(GameSession session, int levelNumber, BoardView board, BoardCameraFitter fitter)
        {
            _session = session;
            _fitter = fitter;
            _camera = Camera.main;
            _snake = FindObjectOfType<SnakeView>();
            _snakeHead = _snake != null ? _snake.transform.Find("SnakeHead") : null;

            _boosters.Add(new Info { id = Booster.Shovel, name = "SHOVEL", stockKey = "booster_shovel", icon = skin.shovelIcon, unlockLevel = 4,
                description = $"<color={Highlight}>DIG A TUNNEL</color> TO ESCAPE!\n<color={Highlight}>CLICK A CIRCLE</color> TO SELECT YOUR EXIT POINT" });
            _boosters.Add(new Info { id = Booster.Boomerang, name = "BOOMERANG", stockKey = "booster_boomerang", icon = skin.boomerangIcon, unlockLevel = BoomerangUnlockLevel,
                description = $"<color={Highlight}>CLICK ARROW </color>TO DELIVER YOUR FRUITS!" });
            _boosters.Add(new Info { id = Booster.Undo, name = "UNDO", stockKey = "booster_undo", icon = skin.undoIcon, unlockLevel = 11, confirm = true,
                description = $"<color={Highlight}>REWINDS</color> YOUR LAST MOVE" });
            _boosters.Add(new Info { id = Booster.Magnet, name = "MAGNET", stockKey = "booster_magnet", icon = skin.magnetIcon, unlockLevel = 16, confirm = true,
                description = $"<color={Highlight}>ATTRACT</color> NEARBY FRUITS TO THE CRATES FOR A FEW SWIPES!" });
            foreach (var b in _boosters)
            {
                // The unlock level: FREE until that level is passed, and the unlock gives 3 once [user].
                b.free = levelNumber == b.unlockLevel;
                if (b.free && PlayerPrefs.GetInt(b.stockKey + "_gift", 0) == 0)
                {
                    SnackDash.Economy.PlayerEconomy.Shared.AddBoosters(b.stockKey, SnackDash.Economy.PlayerEconomy.BoosterPack);
                    PlayerPrefs.SetInt(b.stockKey + "_gift", 1);
                    PlayerPrefs.Save();
                }
            }

            _root = Canvas(transform, "Boosters", 6);
            var safe = Stretch(_root, "SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            _aimPanel = BuildAimPanel(_root);
            _tutorialPanel = Stretch(_root, "Tutorial");
            Image(_tutorialPanel, null, new Color(0f, 0f, 0f, 0.7f));
            _tutorialPanel.gameObject.SetActive(false);

            _bar = Rect(safe, "_bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BarHeight), new Vector2(0.5f, 0));
            float width = 4 * SlotWidth + 3 * Spacing; // settings moved next to the progress bar (HudView)
            float x = -width / 2f + SlotWidth / 2f;
            foreach (var booster in _boosters)
            {
                bool unlocked = levelNumber >= booster.unlockLevel;
                booster.slot = Slot(booster.name, x, booster.icon, unlocked ? null : "LVL. " + booster.unlockLevel);
                x += SlotWidth + Spacing;
                if (!unlocked) continue;
                var button = booster.slot.Find("Button").gameObject;
                button.GetComponent<Image>().raycastTarget = true;
                var tapped = booster;
                button.AddComponent<Button>().onClick.AddListener(() => OnTapped(tapped));
                booster.countPanel = booster.slot.Find("InventoryPanel").gameObject;
                booster.count = booster.countPanel.GetComponentInChildren<TMP_Text>();
                booster.plusIcon = booster.slot.Find("PlusIcon").gameObject;
                booster.freePanel = booster.slot.Find("BoosterFreePanel").gameObject;
                booster.iconImage = booster.slot.Find("Button/Icon")?.GetComponent<Image>();
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

        /// <summary>One booster button (BoosterButtonX): hexagon, icon, count badge / plus, lock or FREE label.</summary>
        private RectTransform Slot(string name, float x, Sprite icon, string lockedLabel)
        {
            var slot = Rect(_bar, "BoosterButton" + name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(SlotWidth, SlotHeight));
            slot.localScale = Vector3.one * SlotScale;
            var button = Rect(slot, "Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(200, 184));
            Image(button, skin.boosterSlot, Color.white);
            bool locked = lockedLabel != null;
            // Locked: the booster's beige silhouette (_Locked sprite) stays under the lock, as in the original.
            int index = _boosters.FindIndex(b => b.name == name);
            var shown = locked && skin.lockedIcons != null && index >= 0 && index < skin.lockedIcons.Length ? skin.lockedIcons[index] : icon;
            if (shown != null)
                Image(Rect(button, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7.5f), new Vector2(130, 120)), shown, Color.white).preserveAspect = true;

            var inventory = Rect(slot, "InventoryPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(51.4f, 5.7f), new Vector2(50, 50));
            var badge = Rect(inventory, "Bg", Vector2.zero, Vector2.one, new Vector2(0.2f, 0), new Vector2(0.6f, 7.8f));
            Image(badge, skin.boosterCountBadge, Color.white);
            var number = Text(Rect(badge, "NumberText", Vector2.zero, Vector2.one, new Vector2(0, -7.1f), new Vector2(-6, 0)), "0", skin.font, skin.plainMaterial, 50, Color.white); // plain white: the shadowed preset smeared at phone size
            number.alignment = TextAlignmentOptions.Center;
            number.enableAutoSizing = true; // NumberText: auto size, max 50 (two digits stay inside the badge)
            number.fontSizeMin = 18f;
            number.fontSizeMax = 44f; // a little inside the red square
            inventory.gameObject.SetActive(false);

            var plus = Rect(slot, "PlusIcon", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(51.4f, 5.7f), new Vector2(50, 50));
            Image(plus, skin.boosterPlus, Color.white);
            plus.gameObject.SetActive(false);

            if (locked)
            {
                var panel = Rect(slot, "LockedPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
                Image(Rect(panel, "Lock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7.7f), new Vector2(83.4f, 133.5f)), skin.lockIcon, Color.white).preserveAspect = true; // the sprite is trimmed: keep its shape inside the 83.4 x 133.5 rect
                Label(panel, "UnlockLevelPanel", lockedLabel, 30, 122f); // 150.9 in the prefab; narrower on the phone [user screenshot]
            }
            Label(slot, "BoosterFreePanel", "FREE", 28, 120f).gameObject.SetActive(false);
            return slot;
        }

        /// <summary>UnlockLevelPanel / BoosterFreePanel: white rounded plate, blue inner plate, white text.</summary>
        private RectTransform Label(RectTransform parent, string name, string text, float size, float width)
        {
            // On the phone: a blue rounded box with an even thin white rim [user screenshots]. The prefab's white
            // plate (JU-Texture-Common-ReosurcesBG, a tall card with a blue drop at its foot) squashed to this height
            // showed a white crescent under a blue pill, so both layers use the rounded box sprite instead.
            var panel = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -72f), new Vector2(width, 50f));
            Image(panel, skin.bluePlate, Color.white, sliced: true).pixelsPerUnitMultiplier = 2.6f;
            var inner = Rect(panel, "Bg", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-9f, -9f));
            Image(inner, skin.bluePlate, Hex("2C64CFFF"), sliced: true).pixelsPerUnitMultiplier = 3f;
            var label = Text(Rect(inner, "Text", Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-8f, 0f)), text, skin.font, skin.plainMaterial, size, Color.white);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = size;
            label.alignment = TextAlignmentOptions.Center;
            return panel;
        }

        /// <summary>Boosters_Pop-up_Tutorial: vignette, banner with the tilted title and the red X, CONFIRM below.</summary>
        private RectTransform BuildAimPanel(RectTransform root)
        {
            var panel = Stretch(root, "Boosters_Pop-up_Tutorial");
            Image(Stretch(panel, "background overlay"), skin.overlayVignette, Color.black, sliced: true);
            var parent = Rect(panel, "Popup Parent", new Vector2(0, 1), Vector2.one, new Vector2(0, -BannerHeight / 2f), new Vector2(0, BannerHeight));
            var background = Rect(parent, "Popup Background", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -198.5f), new Vector2(828, 397));
            Image(Rect(background, "Background Main Color", Vector2.zero, Vector2.one, new Vector2(-0.67f, -73.26f), new Vector2(-98.26f, -156.46f)), skin.bannerFrame, Hex("D2E3FFFF"), sliced: true);

            var content = Rect(parent, "Popup Content", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70.45f), new Vector2(828, 140.9f));
            var contentBack = Rect(content, "Popup Content Background", Vector2.zero, Vector2.one, new Vector2(0, 8.35f), new Vector2(-80, -5.56f));
            Image(Rect(contentBack, "popup content background color", new Vector2(0.0285f, 0), new Vector2(0.9715f, 1), new Vector2(0.4f, 2f), new Vector2(-8.19f, -15.54f)), skin.bannerFrame, Color.white, sliced: true);
            _bannerText = Text(Rect(content, "Text (TMP) - sub text", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(2.84f, 72.05f), new Vector2(686, 116.1f)),
                "", skin.font, skin.plainMaterial, 50, Hex("446890FF"));
            _bannerText.alignment = TextAlignmentOptions.Center;
            _bannerText.enableWordWrapping = true; // the original wraps to two lines
            _bannerText.enableAutoSizing = true;
            _bannerText.fontSizeMin = 30;
            _bannerText.fontSizeMax = 50;

            var title = Rect(background, "SP_BG_Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-7.3f, -174), new Vector2(585.4f, 178));
            Image(title, skin.titleBanner, Color.white);
            _bannerTitle = Text(Rect(background, "Text (TMP) - header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-9.75f, -176.9f), new Vector2(847.5f, 114)),
                "", skin.font, skin.plainMaterial, 72, Color.white);
            _bannerTitle.alignment = TextAlignmentOptions.Center;
            _bannerTitle.rectTransform.localRotation = Quaternion.Euler(0, 0, 2.54f);

            var close = Rect(background, "View_Close_Button", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-68, -169), new Vector2(86, 86));
            Image(close, skin.closeSquare, Color.white, sliced: true, raycast: true);
            var x = Text(Rect(close, "X (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6.5f), new Vector2(86, 72)), "X", skin.font, skin.redCartoonMaterial, 74, Color.white);
            x.alignment = TextAlignmentOptions.Center;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => SetAiming(null));

            // Buttons Area / View_Button_Confirm: the green 440x210 button above the booster bar.
            var buttons = Rect(panel, "Buttons Area", Vector2.zero, new Vector2(1, 0), new Vector2(0, 257.9f), new Vector2(0, 402.6f), new Vector2(0.5f, 0.6405f));
            _confirmButton = GreenButton(buttons, "View_Button_Confirm", "CONFIRM", new Vector2(0, 120));
            _confirmButton.gameObject.AddComponent<Button>().onClick.AddListener(OnConfirm);
            panel.gameObject.SetActive(false);
            return panel;
        }

        /// <summary>The original big green button: Base, Front, Highlight and Outline sheets with the label.</summary>
        private RectTransform GreenButton(RectTransform parent, string name, string label, Vector2 position)
        {
            var button = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(440, 210));
            var visual = Rect(button, "Visual_Container", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, 210));
            var sheets = skin.bigButton;
            Image(Rect(visual, "Base", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), sheets.Length > 0 ? sheets[0] : null, Hex("399F00FF"), sliced: true, raycast: true);
            Image(Rect(visual, "Front", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 8.19f), new Vector2(-20.27f, 124.99f)), sheets.Length > 1 ? sheets[1] : null, Hex("4FCD04FF"), sliced: true);
            Image(Rect(visual, "Highlight", new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.97f, -42.49f), new Vector2(-33.86f, 23.62f)), sheets.Length > 2 ? sheets[2] : null, Hex("78F024FF"), sliced: true);
            Image(Rect(visual, "Outline", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-16.06f, 144.22f)), sheets.Length > 3 ? sheets[3] : null, Hex("2D8700FF"), sliced: true);
            var text = Text(Rect(visual, "Text (TMP)", new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-119.5f, 66)), label, skin.font, skin.greenOutlineMaterial, 66, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void OnTapped(Info booster)
        {
            if (!_session.IsIdle || _session.InputBlocked || _session.InputLocked || _session.ForcedSwipe.HasValue) return; // not over a tutorial message or an effect
            if (!booster.free && booster.Stock <= 0)
            {
                BoosterBuyPopup.Open(_root, skin, booster.name, booster.description, booster.icon, booster.stockKey, null); // the + : buy 3 for 1800 [user]
                return;
            }
            if (booster.id == Booster.Undo && !_session.CanUndo) return;
            if (booster.id == Booster.Magnet && _session.MagnetActive) return;
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
                bool used = booster.id == Booster.Undo ? _session.Undo() : _session.UseMagnet();
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
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, booster.slot.position);
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
            if (skin.magnetField == null) return;
            var ring = new GameObject("Magnet Ring");
            ring.transform.SetParent(_snakeHead.parent, false);
            var sprite = ring.AddComponent<SpriteRenderer>();
            var texture = skin.magnetField;
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
                if (booster.count != null && booster.free && PlayerPrefs.GetInt(booster.TutorialKey, 0) == 0 && TutorialReady(booster)) { ShowTutorial(booster); break; }
        }

        /// <summary>BoosterForcedTutorialView: dim everything but the booster, purple arrow bobbing over it. Once [user].</summary>
        private void ShowTutorial(Info booster)
        {
            _tutorialShown = true;
            _tutorialBooster = booster;
            _session.SwipesHeld = true; // only the booster now
            PlayerPrefs.SetInt(booster.TutorialKey, 1);
            PlayerPrefs.Save();
            var slot = booster.slot;
            _tutorialPanel.gameObject.SetActive(true);
            _tutorialPanel.SetAsLastSibling(); // over the bar, so only the lifted slot stays lit
            // The slot stays in the bar (moving it to the panel lost its place when the screen changed size) and is
            // drawn above the dim by a nested canvas; the arrow rides on it, in panel units (215 above, 160 big).
            var lift = slot.GetComponent<Canvas>();
            if (lift == null) lift = slot.gameObject.AddComponent<Canvas>();
            lift.overrideSorting = true;
            lift.sortingOrder = _root.GetComponent<Canvas>().sortingOrder + 1;
            if (slot.GetComponent<GraphicRaycaster>() == null) slot.gameObject.AddComponent<GraphicRaycaster>();
            var arrow = Rect(slot, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(160, 160) / SlotScale);
            arrow.localRotation = Quaternion.Euler(0, 0, 180);
            var arrowImage = Image(arrow, skin.tutorialArrow, Hex("7F33D4FF"));
            // The original Arrow carries a light cream Outline (alpha 0.47, 4 / -4): a soft rim instead of a hard purple edge [user].
            var rim = arrowImage.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 0.965f, 0.902f, 0.471f);
            rim.effectDistance = new Vector2(4f, -4f);
            StartCoroutine(Bob(arrow, 1f / SlotScale));
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
            var slot = _tutorialBooster.slot;
            var lift = slot.GetComponent<Canvas>();
            if (lift != null) lift.overrideSorting = false; // back under the bar's own order
            var arrow = slot.Find("Arrow");
            if (arrow != null) Destroy(arrow.gameObject);
            _tutorialPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_session == null) return;
            bool hide = HideWhen != null && HideWhen();
            if (_bar.gameObject.activeSelf == hide) _bar.gameObject.SetActive(!hide);
            foreach (var booster in _boosters)
            {
                if (booster.count == null) continue;
                bool free = booster.free;
                int stock = booster.Stock;
                booster.freePanel.SetActive(free);
                booster.countPanel.SetActive(!free && stock > 0);
                booster.plusIcon.SetActive(!free && stock <= 0);
                booster.count.text = stock.ToString();
                // Two digits: a wider badge so the number keeps its size (NumberText auto-sizes to max 50).
                ((RectTransform)booster.countPanel.transform).sizeDelta = new Vector2(stock >= 10 ? 74f : 50f, 50f);
                // Undo is grey while it cannot be used (right after a booster, or before a fresh swipe) [user].
                if (booster.id == Booster.Undo && booster.iconImage != null)
                    booster.iconImage.sprite = _session.CanUndo ? skin.undoIcon : (skin.undoDisabledIcon != null ? skin.undoDisabledIcon : skin.undoIcon);
                // ...and the whole button fades, so the two states read apart [user].
                if (booster.id == Booster.Undo)
                {
                    if (!booster.slot.TryGetComponent(out CanvasGroup group)) group = booster.slot.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = _session.CanUndo ? 1f : 0.45f;
                }
            }

            FollowHead();

            // The shovel's holes go once nothing of the snake is underground any more.
            if (_hole != null && !_session.HeadInTunnel && !AnySegmentUnderground()) { Destroy(_hole); _hole = null; }

            var boomerang = Get(Booster.Boomerang);
            TryStartTutorial();

            bool arrows = false;
            foreach (var pair in _arrows)
            {
                bool show = _aiming == boomerang && _session.CanBoomerang(pair.Key);
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
            if (_aiming != boomerang || !arrows) return;
            int column = ArrowUnder(UnityEngine.Input.mousePosition);
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
