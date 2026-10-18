namespace SnackDash.UI
{
    /// <summary>
    /// Luật chơi Lucky Ladybug (prefab <c>Prefabs/UI/Popups/LuckyLadybug/LuckyLadybugRules</c>, chép khuôn từ JungleHeistRules): tiêu đề, 3 bước
    /// (chạm bọ trên khay, thắng màn để giữ, thu 5 con mở rương) hiện lần lượt, lời nhắc thua thì mất bọ, "Tap to continue!".
    /// Hiện tự động lần đầu mở sự kiện và khi bấm nút i của popup chính.
    /// </summary>
    public class LuckyLadybugRules : RulesPopup
    {
        private const string Key = "lb_rules_seen";
        public static bool Seen => SeenFor(Key);
        protected override string SeenKey => Key;
    }
}
