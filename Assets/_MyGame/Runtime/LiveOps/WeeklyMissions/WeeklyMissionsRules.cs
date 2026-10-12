namespace SnackDash.UI
{
    /// <summary>
    /// Luật chơi WEEKLY ADVENTURE (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyMissionsRules</c>, gốc: WeeklyMissionsPopupFtue): tiêu đề (khối
    /// WeeklyTitle) với dải "Rules", 3 bước có hình (làm nhiệm vụ lấy mũ, mũ đổi rương, giải lớn). Gốc chạy bằng code nên nhịp đọc
    /// từ video sk1.1 (5.5-7.0 s). Hiện lần đầu mở popup và khi bấm i. Mở bằng <c>UIManager.Show&lt;WeeklyMissionsRules&gt;()</c>.
    /// </summary>
    public class WeeklyMissionsRules : RulesPopup
    {
        private const string Key = "wm_rules_seen";
        public static bool Seen => SeenFor(Key);
        protected override string SeenKey => Key;
    }
}
