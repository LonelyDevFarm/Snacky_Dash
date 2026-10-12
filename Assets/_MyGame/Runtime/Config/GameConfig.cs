using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// Gốc các file cấu hình số liệu (Data/Config): kinh tế, booster, Shop, Weekly Adventure, Snacky Pass, Jungle Heist.
    /// Nằm ở Data/Config/Resources/GameConfig để code tự nạp được ở mọi lúc (cả trước khi [Managers] chạy và trong test).
    /// Sửa số trong từng file con trên Inspector; luật tính vẫn ở code.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public EconomyConfig economy;
        public BoosterConfig boosters;
        public ShopConfig shop;
        public WeeklyMissionsConfig weeklyMissions;
        public SnackyPassConfig snackyPass;
        public JungleHeistConfig jungleHeist;

        private static GameConfig _current;

        public static GameConfig Current
        {
            get
            {
                if (_current == null) _current = Resources.Load<GameConfig>("GameConfig");
                return _current;
            }
        }
    }
}
