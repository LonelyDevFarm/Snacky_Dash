namespace SnackDash.UI
{
    /// <summary>Phần dùng chung của Jungle Heist. Các khối giao diện (tiêu đề, khung gỗ, nút xanh, đồng hồ, avatar) là prefab trong Prefabs/UI/Popups/JungleHeist.</summary>
    public static class JungleHeistUi
    {
        /// <summary>"1D 04H", "5H 12M", "12M 30S".</summary>
        public static string Clock(System.TimeSpan left) =>
            left.TotalDays >= 1 ? $"{(int)left.TotalDays}D {left.Hours:00}H" : left.TotalHours >= 1 ? $"{left.Hours}H {left.Minutes:00}M" : $"{left.Minutes}M {left.Seconds:00}S";
    }
}
