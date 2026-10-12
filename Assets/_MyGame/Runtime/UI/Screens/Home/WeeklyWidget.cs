using SnackDash.Economy;

namespace SnackDash.UI
{
    /// <summary>WEEKLY ADVENTURE (WeeklyMissionsWidget): mở popup nhiệm vụ (lần đầu kèm luật chơi); chấm đỏ đếm nhiệm vụ chờ nhận (sk1).</summary>
    public class WeeklyWidget : EventWidget
    {
        private bool _listening;

        protected override int UnlockLevel => WeeklyMissions.UnlockLevel;

        protected override void Opened()
        {
            var missions = WeeklyMissions.Shared;
            missions.StartIfUnlocked(Level);
            missions.Changed += Refresh;
            _listening = true;
            Refresh();
            if (!BadgeFtue.SeenFor(BadgeFtue.WeeklySeenKey)) StartCoroutine(ShowFtue(() => false));
        }

        private void OnDestroy()
        {
            if (_listening) WeeklyMissions.Shared.Changed -= Refresh;
        }

        private void Refresh() => Badge(WeeklyMissions.Shared.ClaimableCount);

        protected override void Clicked()
        {
            UI.Show<WeeklyMissionsPopup>();
            if (!WeeklyMissionsRules.Seen) UI.Show<WeeklyMissionsRules>();
        }
    }
}
