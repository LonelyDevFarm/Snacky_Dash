namespace SnackDash.UI
{
    /// <summary>
    /// Luật chơi Jungle Heist (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistRules</c>, gốc: Popup_Lava_Quest_Ftue): tiêu đề, 4 bước (đám đông
    /// 100 người, thắng màn, chia thưởng, lời cảnh báo) hiện lần lượt, 2 mũi tên nhún, "Tap to continue!". Hiện tự động lần đầu tham
    /// gia và khi bấm nút i. Mở bằng <c>UIManager.Show&lt;JungleHeistRules&gt;()</c>.
    /// </summary>
    public class JungleHeistRules : RulesPopup
    {
        private const string Key = "jh_rules_seen";
        public static bool Seen => SeenFor(Key);
        protected override string SeenKey => Key;
    }
}
