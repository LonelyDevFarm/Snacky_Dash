using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// JUNGLE HEIST: level mở, số chặng, số người, giải lớn chia đều cho người còn lại. Số người còn mỗi chặng và chu kỳ là tạm đặt
    /// (bản gốc lấy từ server: LavaQuestConfig.SurvivorsPerStep, CurrentCycleDuration).
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Jungle Heist", fileName = "JungleHeistConfig")]
    public class JungleHeistConfig : ScriptableObject
    {
        public int unlockLevel = 35;
        [Tooltip("Số chặng phải thắng liền.")]
        public int steps = 7;
        public int participants = 100;
        [Tooltip("Tổng xu chia cho người về đích.")]
        public int grandPrize = 10000;
        [Tooltip("Người còn lại ở mỗi chặng, từ lúc bắt đầu (cần steps + 1 số).")]
        public int[] survivorsPerStep;
        [Tooltip("Giờ một vòng sự kiện.")]
        public int cycleHours = 24;
    }
}
