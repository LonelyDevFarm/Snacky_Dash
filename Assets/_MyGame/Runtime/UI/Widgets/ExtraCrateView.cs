using System.Collections;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;
using static SnackDash.UI.UiBuilder;

namespace SnackDash.UI
{
    /// <summary>
    /// Khay phụ của màn (màn 8, 13, 18, 29, 30, 33, 39): hố của nó hiện sàn gỗ và bóng nói "TAP TO UNLOCK" (ExtraCratePadlock
    /// trong Game.unity gốc, 170 x 80, nảy vào trong 0.3 s, OutBack). Chạm (khi rắn đứng yên) mở EXTRA CRATE, mua 700 [user].
    /// Trả xong: vệt bay tới khay (ExtraCrate_Trail_FX, 1 s), khay mở (Particle_Extra_Crate) và các khay đang chờ được chia
    /// lại (GameSession.UnlockExtraColumn). Khoá input trong lúc đó như bản gốc (AcquireUnlockInputBlock).
    /// </summary>
    public class ExtraCrateView : MonoBehaviour
    {
        private GameSession _session;
        private CratesView _crates;
        private RectTransform _canvas, _bubble;
        private bool _busy;
        private float _appear; // 0..1 pop-in

        /// <param name="bubblePrefab">Prefabs/UI/Screens/Game/ExtraCrateBubble: bóng nói "TAP TO UNLOCK" (vùng chạm có Button).</param>
        public void Bind(GameSession session, CratesView crates, RectTransform bubblePrefab)
        {
            _session = session;
            _crates = crates;
            if (session.ExtraColumn < 0) return;
            _canvas = Canvas(transform, "ExtraCrate", 4); // under the HUD (5) and the booster bar (6)
            // Canvas con trong HUD không override sorting nên vẽ theo thứ tự con: đặt lên đầu để bọ Lucky Ladybug và mọi thứ của HUD
            // vẽ đè lên bóng TAP TO UNLOCK [user 20/10: bóng che bọ].
            _canvas.SetAsFirstSibling();
            _bubble = Instantiate(bubblePrefab, _canvas);
            _bubble.GetComponentInChildren<Button>(true).onClick.AddListener(OnTapped);
            _bubble.localScale = Vector3.zero;
            StartCoroutine(Appear());
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
            var popup = SnackDash.Core.Services.Get<SnackDash.Core.UIManager>().Show<ExtraCratePopup>(); // trên HUD và thanh booster
            if (popup == null) return;
            popup.Setup(() => StartCoroutine(Unlock()));
            StartCoroutine(HoldWhileOpen(popup));
        }

        /// <summary>Swipes reach the board under the popup: held while it is open, so the snake is still idle when it is paid.</summary>
        private IEnumerator HoldWhileOpen(ExtraCratePopup popup)
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
