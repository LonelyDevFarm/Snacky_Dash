using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// WEEKLY ADVENTURE: level mở, 7 ngày × 5 nhiệm vụ, các mốc rương. Ngày 1-5 chép từ video sk1, ngày 6-7 từ ảnh của bạn; chỉ phần
    /// thưởng mốc đầu là tạm đặt. Điểm (points) của nhiệm vụ là số mũ tích vào thanh mốc.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Weekly Missions", fileName = "WeeklyMissionsConfig")]
    public class WeeklyMissionsConfig : ScriptableObject
    {
        [System.Serializable]
        public class Day
        {
            public Mission[] missions;

            public Mission this[int index] => missions[index];
            public int Length => missions.Length;
        }

        [System.Serializable]
        public struct Milestone
        {
            [Tooltip("Số mũ cần để tới mốc.")]
            public int points;
            public MissionReward reward;
        }

        public int unlockLevel = 15;
        [Tooltip("7 ngày, mỗi ngày 5 nhiệm vụ.")]
        public Day[] days;
        [Tooltip("Mốc đầu, rương xanh, rương tím, giải lớn. 1000 / 2000 và thưởng của chúng từ video sk1.1; rương xanh ~500 đo trên thanh.")]
        public Milestone[] milestones;
    }
}
