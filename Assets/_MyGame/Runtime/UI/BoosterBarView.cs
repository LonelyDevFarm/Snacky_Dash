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
    /// Bottom booster bar and the Boomerang flow [Lv7 video; original Game.unity _bottom/button_list,
    /// Boosters_Pop-up_Tutorial, BoosterTutorialView, TileCov_Crate_Delivery ArrowCLICKABLE].
    /// - Bar: shovel, boomerang, two locked slots (LVL. 11 / LVL. 16); a centred row,
    ///   slots 150.6 wide at scale 0.85; settings sit by the HUD bar, as on Level 27 (Man_Lose video).
    /// - Level 7 (first time): once every fruit is eaten, the screen dims, only the boomerang stays
    ///   and a purple arrow bobs over it; it shows FREE.
    /// - Tapping the boomerang: dark vignette, the "BOOMERANG – Click Arrow to deliver your fruits!"
    ///   banner, the board refits between banner and bar, and the delivery points the snake can feed
    ///   show their clickable arrow. Tapping an arrow throws (GameSession.UseBoomerang); X cancels.
    /// Only the boomerang works yet; the shovel is shown as in the footage but does nothing.
    /// </summary>
    public class BoosterBarView : MonoBehaviour
    {
        public const int BoomerangUnlockLevel = 7;
        private const string StockKey = "booster_boomerang";
        private const string IntroKey = "booster_boomerang_intro";
        private const int DefaultStock = 99; // no coin shop yet [user]

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

        private GameSession _session;
        private BoardCameraFitter _fitter;
        private Camera _camera;
        private RectTransform _bar, _boomerangSlot, _aimPanel, _tutorialPanel;
        private GameObject _freePanel, _countPanel, _plusIcon;
        private TMP_Text _count;
        private bool _aiming, _free, _tutorialShown;
        private readonly Dictionary<int, GameObject> _arrows = new Dictionary<int, GameObject>();

        public static int Stock
        {
            get => PlayerPrefs.GetInt(StockKey, DefaultStock);
            set { PlayerPrefs.SetInt(StockKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public void Bind(GameSession session, int levelNumber, BoardView board, BoardCameraFitter fitter)
        {
            _session = session;
            _fitter = fitter;
            _camera = Camera.main;
            bool unlocked = levelNumber >= BoomerangUnlockLevel;
            _free = unlocked && PlayerPrefs.GetInt(IntroKey, 0) == 0;

            var root = Canvas(transform, "Boosters", 6);
            var safe = Stretch(root, "SafeArea");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);

            _aimPanel = BuildAimPanel(root);
            _tutorialPanel = Stretch(root, "Tutorial");
            Image(_tutorialPanel, null, new Color(0f, 0f, 0f, 0.7f));
            _tutorialPanel.gameObject.SetActive(false);

            _bar = Rect(safe, "_bottom", Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, BarHeight), new Vector2(0.5f, 0));
            float width = 4 * SlotWidth + 3 * Spacing; // settings moved next to the progress bar (HudView)
            float x = -width / 2f + SlotWidth / 2f;
            Slot("Shovel", x, skin.shovelIcon, 1, null); x += SlotWidth + Spacing;
            _boomerangSlot = Slot("Boomerang", x, skin.boomerangIcon, 0, unlocked ? null : "LVL. " + BoomerangUnlockLevel); x += SlotWidth + Spacing;
            Slot("Locked 11", x, null, 0, "LVL. 11"); x += SlotWidth + Spacing;
            Slot("Locked 16", x, null, 0, "LVL. 16");

            if (unlocked)
            {
                var button = _boomerangSlot.Find("Button").gameObject;
                button.GetComponent<Image>().raycastTarget = true;
                button.AddComponent<Button>().onClick.AddListener(OnBoomerangTapped);
                _countPanel = _boomerangSlot.Find("InventoryPanel").gameObject;
                _count = _countPanel.GetComponentInChildren<TMP_Text>();
                _plusIcon = _boomerangSlot.Find("PlusIcon").gameObject;
                _freePanel = _boomerangSlot.Find("BoosterFreePanel").gameObject;
            }

            foreach (var stop in session.Board.CrateStops)
            {
                var cover = board != null ? board.GetCover(stop.Value) : null;
                var arrow = cover != null ? cover.transform.Find("ArrowCLICKABLE_DO_NOT_DELETE") : null;
                if (arrow != null) _arrows[stop.Key] = arrow.gameObject;
            }
            session.BoomerangThrown += (column, thrown) => SetAiming(false);
        }

        /// <summary>One booster button (BoosterButtonX): hexagon, icon, count badge / plus, lock or FREE label.</summary>
        private RectTransform Slot(string name, float x, Sprite icon, int count, string lockedLabel)
        {
            var slot = Rect(_bar, "BoosterButton" + name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(SlotWidth, SlotHeight));
            slot.localScale = Vector3.one * SlotScale;
            var button = Rect(slot, "Button", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(200, 184));
            Image(button, skin.boosterSlot, Color.white);
            bool locked = lockedLabel != null;
            if (icon != null && !locked)
                Image(Rect(button, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7.5f), new Vector2(130, 120)), icon, Color.white).preserveAspect = true;

            var inventory = Rect(slot, "InventoryPanel", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(51.4f, 5.7f), new Vector2(50, 50));
            var badge = Rect(inventory, "Bg", Vector2.zero, Vector2.one, new Vector2(0.2f, 0), new Vector2(0.6f, 7.8f));
            Image(badge, skin.boosterCountBadge, Color.white);
            var number = Text(Rect(badge, "NumberText", Vector2.zero, Vector2.one, new Vector2(0, -7.1f), new Vector2(-6, 0)), count.ToString(), skin.font, skin.homeScreenLevelsMaterial, 50, Color.white);
            number.alignment = TextAlignmentOptions.Center;
            inventory.gameObject.SetActive(!locked && count > 0);

            var plus = Rect(slot, "PlusIcon", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(51.4f, 5.7f), new Vector2(50, 50));
            Image(plus, skin.boosterPlus, Color.white);
            plus.gameObject.SetActive(false);

            if (locked)
            {
                var panel = Rect(slot, "LockedPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
                Image(Rect(panel, "Lock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7.7f), new Vector2(83.4f, 133.5f)), skin.lockIcon, Color.white);
                Label(panel, "UnlockLevelPanel", lockedLabel, 30, 150.9f);
            }
            Label(slot, "BoosterFreePanel", "FREE", 28, 120f).gameObject.SetActive(false);
            return slot;
        }

        /// <summary>UnlockLevelPanel / BoosterFreePanel: white rounded plate, blue inner plate, white text.</summary>
        private RectTransform Label(RectTransform parent, string name, string text, float size, float width)
        {
            var panel = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -76.2f), new Vector2(width, 62.8f));
            Image(panel, skin.whitePlate, Color.white, sliced: true);
            var inner = Rect(panel, "Bg", Vector2.zero, Vector2.one, new Vector2(-0.9f, 4), new Vector2(-24f, -22.2f));
            Image(inner, skin.bluePlate, Hex("2C64CFFF"), sliced: true);
            var label = Text(Stretch(inner, "Text"), text, skin.font, skin.progressBarMaterial, size, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            return panel;
        }

        /// <summary>Boosters_Pop-up_Tutorial: vignette, banner with the tilted title and the red X.</summary>
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
            var sub = Text(Rect(content, "Text (TMP) - sub text", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(2.84f, 72.05f), new Vector2(686, 116.1f)),
                "<color=#7B3FE4>CLICK ARROW </color>TO DELIVER YOUR FRUITS!", skin.font, skin.plainMaterial, 50, Hex("446890FF"));
            sub.alignment = TextAlignmentOptions.Center;
            sub.enableWordWrapping = true; // two lines in the original: "CLICK ARROW TO DELIVER / YOUR FRUITS!"

            var title = Rect(background, "SP_BG_Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-7.3f, -174), new Vector2(585.4f, 178));
            Image(title, skin.titleBanner, Color.white);
            var header = Text(Rect(background, "Text (TMP) - header", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-9.75f, -176.9f), new Vector2(847.5f, 114)),
                "BOOMERANG", skin.font, skin.plainMaterial, 72, Color.white);
            header.alignment = TextAlignmentOptions.Center;
            header.rectTransform.localRotation = Quaternion.Euler(0, 0, 2.54f);

            var close = Rect(background, "View_Close_Button", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-68, -169), new Vector2(86, 86));
            Image(close, skin.closeSquare, Color.white, sliced: true, raycast: true);
            var x = Text(Rect(close, "X (TMP)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6.5f), new Vector2(86, 72)), "X", skin.font, skin.redCartoonMaterial, 74, Color.white);
            x.alignment = TextAlignmentOptions.Center;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => SetAiming(false));
            panel.gameObject.SetActive(false);
            return panel;
        }

        private void OnBoomerangTapped()
        {
            if (_session.IsMoving || _session.IsDelivering || _session.IsStuck) return;
            if (!_free && Stock <= 0) return; // buying with coins: later
            SetAiming(!_aiming);
        }

        private void SetAiming(bool aiming)
        {
            if (_aiming == aiming) return;
            _aiming = aiming;
            _aimPanel.gameObject.SetActive(aiming);
            if (aiming) HideTutorial();
            if (_fitter != null)
            {
                if (aiming) _fitter.FitBetween(BarHeight, 1920f - BannerHeight, boardRefitDuration);
                else _fitter.FitDefault(boardRefitDuration);
            }
        }

        /// <summary>BoosterForcedTutorialView: dim everything but the boomerang, purple arrow bobbing over it.</summary>
        private void ShowTutorial()
        {
            _tutorialShown = true;
            _tutorialPanel.gameObject.SetActive(true);
            _boomerangSlot.SetParent(_tutorialPanel, true);
            var arrow = Rect(_tutorialPanel, "Arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 160));
            arrow.position = _boomerangSlot.position;
            arrow.anchoredPosition += new Vector2(0, 215 * SlotScale);
            arrow.localRotation = Quaternion.Euler(0, 0, 180);
            Image(arrow, skin.tutorialArrow, Hex("7F33D4FF"));
            StartCoroutine(Bob(arrow));
        }

        private IEnumerator Bob(RectTransform arrow)
        {
            var start = arrow.anchoredPosition;
            while (arrow != null && arrow.gameObject.activeInHierarchy)
            {
                yield return Tween.Run(tutorialBobDuration, Tween.InOutSine, t => { if (arrow != null) arrow.anchoredPosition = start + tutorialBobOffset * t; });
                yield return Tween.Run(tutorialBobDuration, Tween.InOutSine, t => { if (arrow != null) arrow.anchoredPosition = start + tutorialBobOffset * (1f - t); });
            }
        }

        private void HideTutorial()
        {
            if (!_tutorialPanel.gameObject.activeSelf) return;
            _boomerangSlot.SetParent(_bar, true);
            _tutorialPanel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_session == null || _count == null) return;
            int stock = Stock;
            _freePanel.SetActive(_free);
            _countPanel.SetActive(!_free && stock > 0);
            _plusIcon.SetActive(!_free && stock <= 0);
            _count.text = stock.ToString();

            bool idle = !_session.IsMoving && !_session.IsDelivering && !_session.IsStuck && !_session.IsWon;
            if (_free && !_tutorialShown && !_aiming && idle && _session.Pills.Count == 0 && AnyColumnFeedable()) ShowTutorial();

            bool any = false;
            foreach (var pair in _arrows)
            {
                bool show = _aiming && _session.CanBoomerang(pair.Key);
                any |= show;
                if (pair.Value.activeSelf != show) pair.Value.SetActive(show);
            }
            if (!any || !UnityEngine.Input.GetMouseButtonDown(0) || IsOverUi()) return;
            int column = ArrowUnder(UnityEngine.Input.mousePosition);
            if (column < 0 || !_session.UseBoomerang(column)) return;
            if (_free)
            {
                _free = false;
                PlayerPrefs.SetInt(IntroKey, 1);
                PlayerPrefs.Save();
            }
            else Stock = stock - 1;
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
