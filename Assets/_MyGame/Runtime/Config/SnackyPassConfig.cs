using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// SNACKY PASS: level mở, độ dài mùa, 20 chặng thưởng thường / premium (đọc từ video sk2 / sk2.1). Đồ trong rương không có trong
    /// video: tạm đặt sao cho tổng làn premium khớp bảng tóm tắt của gói (8 tim, 3000 xu, mỗi booster x10, 6 giờ tim vô hạn).
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Snacky Pass", fileName = "SnackyPassConfig")]
    public class SnackyPassConfig : ScriptableObject
    {
        public int unlockLevel = 25;
        [Tooltip("Số ngày một mùa; hết mùa thì mùa mới bắt đầu từ đầu.")]
        public int seasonDays = 7;
        [Tooltip("Số tim tối đa khi có pass premium.")]
        public int premiumLives = 8;
        [Tooltip("Thưởng thường của từng chặng (chest 0: không phải rương; 1 bạc, 2 đỏ, 3 xanh, 4 hồng).")]
        public PassReward[] free;
        [Tooltip("Thưởng premium của từng chặng (Lives Perk: chặng 1, tim tối đa lên premiumLives).")]
        public PassReward[] premium;
    }
}
