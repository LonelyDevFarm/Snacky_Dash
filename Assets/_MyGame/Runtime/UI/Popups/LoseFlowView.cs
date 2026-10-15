using System;
using System.Collections;
using SnackDash.Core;
using SnackDash.Economy;
using SnackDash.Gameplay;
using SnackDash.Presentation;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Dòng chảy thua như bản gốc (video Man_Lose): cắn đuôi hiện <see cref="NoWayOutPopup"/> với SNIP TAIL (trả xu để chơi tiếp) và
    /// GIVE UP; bỏ cuộc thì cho cắt thêm một lần (<see cref="SecondChancePopup"/>), huỷ nữa thì <see cref="GameOverPopup"/> / TRY AGAIN
    /// chơi lại màn. Ba màn là prefab riêng mở qua UIManager; script này chỉ nối chúng, tính giá cắt (900 xu, <see cref="SnipOffer"/>)
    /// và thu / trả bàn chơi.
    /// </summary>
    public class LoseFlowView : MonoBehaviour
    {
        [SerializeField] private SnipOffer snipOffer = new SnipOffer();
        [Tooltip("Bàn chơi thu lên trên các nút; khoảng 0.33 s đo trên Man_Lose (30 fps), không rõ đường cong.")]
        [SerializeField] private float boardRefitDuration = 0.35f;
        [Tooltip("Bottom_Container (500) và Top_Container_Version_B (130 + 15) của LoseInBoardPopup.")]
        [SerializeField] private float bottomHeight = 500f, topHeight = 145f;
        [Tooltip("HUD trong scene: lúc NO WAY OUT nút cài đặt nhường chỗ cho ô xu (video Man_Lose).")]
        [SerializeField] private HudView hud;

        private GameSession _session;
        private Action _restart;
        private BoardCameraFitter _cameraFitter;
        private BasePopup _open;

        private static UIManager UI => Services.Get<UIManager>();

        public void Bind(GameSession session, Action restart, BoardCameraFitter cameraFitter)
        {
            _session = session;
            _restart = restart;
            _cameraFitter = cameraFitter;
            session.TailBitten += snipCount => StartCoroutine(NoWayOut());
        }

        /// <summary>Chạy trước NO WAY OUT khi trả về một routine (lời hướng dẫn Level 3, TutorialView).</summary>
        public Func<IEnumerator> BeforeNoWayOut { get; set; }

        /// <summary>True khi bắt buộc cắt (hướng dẫn Level 3): NO WAY OUT không có GIVE UP.</summary>
        public Func<bool> ForcedSnip { get; set; }

        private bool Forced => ForcedSnip != null && ForcedSnip();

        /// <summary>900 xu; FREE ở Level 3 (hướng dẫn cắt) tới khi qua màn đó [user].</summary>
        private int SnipCost => Forced ? 0 : snipOffer.CurrentCost;

        private T Open<T>() where T : BasePopup
        {
            CloseOpen();
            var popup = UI.Show<T>();
            _open = popup;
            if (hud != null) hud.ShowCoins(false); // NO WAY OUT bật ô xu cùng lúc với nút SNIP TAIL
            return popup;
        }

        private void CloseOpen()
        {
            if (_open != null) Destroy(_open.gameObject);
            _open = null;
            if (hud != null) hud.ShowCoins(false);
        }

        private IEnumerator NoWayOut()
        {
            var before = BeforeNoWayOut?.Invoke();
            if (before != null) yield return before;
            var popup = Open<NoWayOutPopup>();
            if (popup == null) yield break;
            popup.Setup(SnipCost, Forced);
            popup.Snip += Snip;
            popup.GiveUp += SecondChance;
            popup.StartCoroutine(popup.Reveal(() =>
            {
                if (_cameraFitter != null) _cameraFitter.FitBetween(bottomHeight, 1920f - topHeight, boardRefitDuration);
                if (hud != null && _open == popup) hud.ShowCoins(true); // cùng đợt với SNIP TAIL [user 15/10]
            }));
        }

        private void Snip()
        {
            if (PlayerEconomy.Shared.Coins < SnipCost)
            {
                UI.Show<ShopView>(); // thiếu xu: Shop đè lên tất cả, X của nó quay lại đây [user]
                return;
            }
            if (!snipOffer.TryUse(PlayerEconomy.Shared, _session, SnipCost)) return;
            CloseOpen();
            if (_cameraFitter != null) _cameraFitter.FitDefault(boardRefitDuration);
        }

        private void SecondChance()
        {
            var popup = Open<SecondChancePopup>();
            if (popup == null) return;
            popup.Setup(SnipCost);
            popup.PlayOn += Snip;
            popup.GiveUp += GameOver;
        }

        private void GameOver()
        {
            PlayerEconomy.Shared.Lose(); // thua màn: mất 1 tim, và chuỗi lửa nếu đang có [user]
            EventBus.Publish(new LevelLostEvent(SnackDash.Bootstrap.LevelProgress.Index + 1));
            var popup = Open<GameOverPopup>();
            if (popup == null) return;
            popup.Setup(SnackDash.Bootstrap.LevelProgress.Index + 1 >= SnackDash.Bootstrap.LevelProgress.HomeFromLevel);
            popup.TryAgain += () => { CloseOpen(); _restart?.Invoke(); };
            popup.Home += () => { CloseOpen(); LoadingScreen.Load(LoadingScreen.HomeScene); }; // tim đã mất lúc thua, X không trừ thêm
        }
    }
}
