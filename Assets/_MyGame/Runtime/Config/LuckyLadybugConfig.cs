using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// LUCKY LADYBUG (sự kiện sáng tạo, không có trong bản gốc; thiết kế Docs/KE_HOACH_LUCKY_LADYBUG.md): level mở, độ dài một vòng,
    /// số bọ cần thu, phần thưởng rương, và nhịp của con bọ trong màn. Sửa trên Inspector của Data/Config/LuckyLadybugConfig.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Lucky Ladybug", fileName = "LuckyLadybugConfig")]
    public class LuckyLadybugConfig : ScriptableObject
    {
        public int unlockLevel = 30;
        [Tooltip("Giờ một vòng; hết giờ chưa đủ bọ thì mất hết, vòng mới mở ngay.")]
        public int roundHours = 48;
        [Tooltip("Số bọ phải thu để mở rương.")]
        public int ladybugsToCollect = 5;

        [Header("Rương")]
        public int rewardCoins = 1500;
        [Tooltip("Số mỗi loại booster (xẻng, boomerang, hoàn tác, nam châm).")]
        public int rewardEachBooster = 1;
        [Tooltip("Phút tim vô hạn.")]
        public int rewardInfiniteMinutes = 30;

        [Header("Trong màn")]
        [Tooltip("Bàn tay chỉ vào con bọ lần đầu, giây.")]
        public float handSeconds = 3f;
    }
}
