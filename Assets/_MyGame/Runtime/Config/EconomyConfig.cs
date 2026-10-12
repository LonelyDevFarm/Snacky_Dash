using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>Xu, giá, tim và streak (<see cref="PlayerEconomy"/>).</summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Economy", fileName = "EconomyConfig")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("Xu")]
        [Tooltip("Xu lúc mới chơi.")]
        public int startCoins = 1800;
        [Tooltip("Xu thắng màn thường / khó / siêu khó (trước khi nhân streak).")]
        public int winNormal = 20, winHard = 30, winSuperHard = 40;

        [Header("Giá (xu)")]
        [Tooltip("NO WAY OUT: cắt dây để chơi tiếp.")]
        public int snipPrice = 900;
        [Tooltip("Mua booster: giá một gói và số booster mỗi gói.")]
        public int boosterPrice = 1800, boosterPack = 3;
        [Tooltip("Thùng phụ của màn [ảnh của bạn, Level 30].")]
        public int extraCratePrice = 700;
        [Tooltip("Nạp đầy tim.")]
        public int refillPrice = 900;

        [Header("Tim")]
        [Tooltip("Số tim tối đa thường ngày (Snacky Pass nâng lên, xem SnackyPassConfig).")]
        public int maxLives = 5;
        [Tooltip("Phút để hồi 1 tim.")]
        public int lifeRegenMinutes = 30;

        [Header("Streak")]
        [Tooltip("Thanh streak hiện từ level này.")]
        public int streakFromLevel = 23;
        [Tooltip("Thắng liền bấy nhiêu trận thì cháy.")]
        public int streakWinsToFire = 5;
        [Tooltip("Lúc cháy, xu thắng màn nhân với số này.")]
        public int streakMultiplier = 2;

        [Header("Booster")]
        [Tooltip("Số booster mỗi loại lúc mới chơi (0: chỉ có khi booster mở khoá hoặc mua) [user].")]
        public int startBoosterStock = 0;
    }
}
