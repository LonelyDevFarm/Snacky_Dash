namespace SnackDash.UI
{
    /// <summary>
    /// Luật chơi SNACKY PASS (prefab <c>Prefabs/UI/Popups/SnackyPass/SnackyPassRules</c>, gốc: seasonpass2/SeasonPassPopupFtue, video sk2 13.5-17 s):
    /// vé Snacky Pass trong vầng sáng cam, rồi 4 bước lần lượt: trái cây có dấu tích ("Complete levels!") và mũi tên, thanh vé "8/10"
    /// ("Collect tickets!") và mũi tên, rương ("Win amazing rewards!"), vé vàng trong vầng sáng ("Activate Snacky Pass…") và "Tap to
    /// continue!". Hiện lần đầu mở pass và khi bấm i. Mở bằng <c>UIManager.Show&lt;SnackyPassRules&gt;()</c>.
    /// </summary>
    public class SnackyPassRules : RulesPopup
    {
        private const string Key = "sp_rules_seen";
        public static bool Seen => SeenFor(Key);
        protected override string SeenKey => Key;
    }
}
