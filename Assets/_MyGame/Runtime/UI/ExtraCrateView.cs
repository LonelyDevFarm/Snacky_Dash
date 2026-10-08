using System.Collections;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// The extra crate of a level (levels 8, 13, 18, 29, 30, 33, 39): its pit shows the wooden floor and a "TAP TO UNLOCK"
    /// speech bubble (ExtraCratePadlock in the original Game.unity, 170 x 80, pops in over 0.3 s, OutBack). Tapping it
    /// (while the snake is idle) opens EXTRA CRATE, Buy 700 [user]. Once paid: the trail flies to the crate
    /// (ExtraCrate_Trail_FX, 1 s), the crate opens (Particle_Extra_Crate) and the waiting crates are shared out
    /// (GameSession.UnlockExtraColumn). Input is locked meanwhile, as the original does (AcquireUnlockInputBlock).
    /// </summary>
    public class ExtraCrateView : MonoBehaviour
    {
        private GameSession _session;
        private CratesView _crates;
        private UiSkin _skin;
        private RectTransform _canvas, _bubble;
        private bool _busy;
        private float _appear; // 0..1 pop-in

        public void Bind(GameSession session, CratesView crates, UiSkin skin)
        {
            _session = session;
            _crates = crates;
            _skin = skin;
            if (session.ExtraColumn < 0) return;
            _canvas = Canvas(transform, "ExtraCrate", 4); // under the HUD (5) and the booster bar (6)
            _bubble = BuildBubble(_canvas);
            StartCoroutine(Appear());
        }

        /// <summary>ExtraCratePadlock / SpeechBubble2: brown outline, cream inside, the pointer under it, "Tap to unlock".</summary>
        private RectTransform BuildBubble(RectTransform canvas)
        {
            var bubble = Rect(canvas, "ExtraCratePadlock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 80f));
            // Both layers at pixels-per-unit 2.35 as in the original: small rounded corners (at 1 the 48 px corners took half the bubble).
            var outline = Image(Stretch(bubble, "background"), _skin.Get("SpeechBubble_MainOutline"), Hex("84300AFF"), sliced: true);
            outline.pixelsPerUnitMultiplier = 2.35f;
            Image(Stretch(outline.rectTransform, "backgroundInner"), _skin.Get("SpeechBubble_MainInner"), Hex("F3E2D3FF"), sliced: true).pixelsPerUnitMultiplier = 2.35f;
            var label = Text(Rect(outline.rectTransform, "Unlock_Txt", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-38f, -20f)), "TAP TO UNLOCK", _skin.font, _skin.plainMaterial, 25f, Hex("6E2807FF"));
            label.enableWordWrapping = true; // "TAP TO" / "UNLOCK" on two lines, as on the phone
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 25f;
            var pointer = Rect(outline.rectTransform, "Pointer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1.4f), new Vector2(16.2f, 16.2f));
            Image(pointer, _skin.Get("SpeechBubble_PointerOutline"), Hex("84300AFF"));
            Image(Rect(pointer, "PointerInner", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.2f), new Vector2(16.2f, 16.2f)), _skin.Get("SpeechBubble_PointerInner"), Hex("F3E2D3FF"));
            // TouchArea: the bubble and 130 below it, over the wooden crate.
            var touch = Rect(outline.rectTransform, "TouchArea", Vector2.zero, Vector2.one, new Vector2(0f, -65f), new Vector2(0f, 130f));
            Image(touch, null, Color.clear, raycast: true);
            touch.gameObject.AddComponent<Button>().onClick.AddListener(OnTapped);
            CentreOnLetters(bubble);
            Pressable(bubble);
            bubble.localScale = Vector3.zero;
            return bubble;
        }

        private IEnumerator Appear()
        {
            while (LoadingScreen.Busy) yield return null;
            yield return Tween.Run(0.3f, Tween.OutBack(1.70158f), t => _appear = t);
        }

        /// <summary>Keeps the bubble on the top of the crate, wherever the camera frames the board.</summary>
        private void LateUpdate()
        {
            if (_bubble == null || _session == null) return;
            if (!_session.ExtraLocked) { _bubble.gameObject.SetActive(false); return; }
            var camera = Camera.main;
            if (camera == null) return;
            var crate = _crates.ColumnWorldPosition(_session.ExtraColumn);
            var top = crate + camera.transform.up * (CratesView.CrateSize.y * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, camera.WorldToScreenPoint(top), null, out var local);
            // As wide as the crate on screen (phone screenshot), whatever the camera framing: the 170 x 80 bubble is scaled.
            var half = camera.transform.right * (CratesView.CrateSize.x * 0.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, camera.WorldToScreenPoint(top - half), null, out var left);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, camera.WorldToScreenPoint(top + half), null, out var right);
            float size = Mathf.Max(0.5f, (right.x - left.x) / 170f);
            _bubble.localScale = Vector3.one * size * _appear;
            _bubble.anchoredPosition = local + new Vector2(0f, -40f * size); // its middle on the crate's top edge, as on the phone
        }

        private void OnTapped()
        {
            if (_busy || !_session.CanUnlockExtra || _session.InputBlocked || _session.InputLocked) return;
            if (_popupCanvas == null) _popupCanvas = Canvas(transform, "ExtraCratePopup", 40); // over the HUD and the booster bar
            var popup = BoosterBuyPopup.OpenExtraCrate(_popupCanvas, _skin, () => StartCoroutine(Unlock()));
            StartCoroutine(HoldWhileOpen(popup));
        }

        private RectTransform _popupCanvas;

        /// <summary>Swipes reach the board under the popup: held while it is open, so the snake is still idle when it is paid.</summary>
        private IEnumerator HoldWhileOpen(RectTransform popup)
        {
            _session.SwipesHeld = true;
            while (popup != null) yield return null;
            if (!_busy) _session.SwipesHeld = false;
        }

        private IEnumerator Unlock()
        {
            _busy = true;
            _session.LockInput(3f); // trail, burst and the new stack
            _bubble.gameObject.SetActive(false);
            yield return _crates.PlayExtraTrail(_session.ExtraColumn);
            if (!_session.UnlockExtraColumn()) SnackDash.Economy.PlayerEconomy.Shared.AddCoins(SnackDash.Economy.PlayerEconomy.ExtraCratePrice); // cannot happen while held; never keep the coins for nothing
            _session.LockInput(0.5f);
            _session.SwipesHeld = false;
            _busy = false;
        }
    }
}
