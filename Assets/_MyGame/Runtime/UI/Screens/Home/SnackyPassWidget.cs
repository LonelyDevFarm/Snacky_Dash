using SnackDash.Economy;

namespace SnackDash.UI
{
    /// <summary>SNACKY PASS (SeasonPassWidget): mở màn Snacky Pass; chấm đỏ đếm phần thưởng chờ nhận.</summary>
    public class SnackyPassWidget : EventWidget
    {
        private bool _listening;

        protected override int UnlockLevel => SeasonPass.UnlockLevel;

        protected override void Opened()
        {
            var pass = SeasonPass.Shared;
            pass.StartIfUnlocked(Level);
            pass.Changed += Refresh;
            _listening = true;
            Refresh();
            // Không bao giờ đè lên lời giới thiệu Weekly hay popup của nó (sk2 9.5-11 s).
            if (!BadgeFtue.SeenFor(BadgeFtue.SnackyPassSeenKey))
                StartCoroutine(ShowFtue(() => UI.IsOpen<BadgeFtue>() || UI.IsOpen<WeeklyMissionsPopup>()));
        }

        private void OnDestroy()
        {
            if (_listening) SeasonPass.Shared.Changed -= Refresh;
        }

        private void Refresh() => Badge(SeasonPass.Shared.ClaimableCount);

        protected override void Clicked() => UI.Show<SnackyPassView>();
    }
}
