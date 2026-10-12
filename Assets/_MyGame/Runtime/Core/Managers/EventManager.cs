using SnackDash.Economy;

namespace SnackDash.Core
{
    /// <summary>
    /// Ba sự kiện: Nhiệm vụ tuần (level 15), Snacky Pass (level 25), Jungle Heist (level 35). Khi mở game: bắt đầu sự kiện
    /// nào đã mở theo level, ghi ngày đăng nhập. Luật từng sự kiện nằm trong <c>LiveOps/&lt;tên&gt;/</c> (có test); các sự kiện
    /// tự nghe ví (thắng màn, tiêu xu, dùng booster) qua <see cref="PlayerEconomy.Goal"/>.
    /// </summary>
    public class EventManager : ManagerBase
    {
        public WeeklyMissions Weekly { get; private set; }
        public SeasonPass SnackyPass { get; private set; }
        public JungleHeist JungleHeist { get; private set; }

        public override void Init()
        {
            int level = SnackDash.Bootstrap.LevelProgress.Index + 1;
            Weekly = WeeklyMissions.Shared;
            Weekly.StartIfUnlocked(level);
            Weekly.Login();
            SnackyPass = SeasonPass.Shared;
            SnackyPass.StartIfUnlocked(level);
            JungleHeist = JungleHeist.Shared;
            JungleHeist.StartIfUnlocked(level);
            Services.Register(this);
        }
    }
}
