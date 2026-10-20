using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// 4 booster theo thứ tự thanh dưới: xẻng, boomerang, undo, nam châm; rồi các booster sáng tạo (bản gốc không có) ở cuối thanh lướt
    /// ngang. Thanh booster và thẻ NEW BOOSTER! cùng đọc ở đây.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Boosters", fileName = "BoosterConfig")]
    public class BoosterConfig : ScriptableObject
    {
        [System.Serializable]
        public class Booster
        {
            public string name;
            [Tooltip("Màn đầu tiên dùng được (booster gốc: màn đó tặng FREE và hiện thẻ NEW BOOSTER!; booster sáng tạo: chỉ mở, không thẻ, không hướng dẫn).")]
            public int unlockLevel;
            [Tooltip("Lời mô tả trên băng chọn booster và trên thẻ (LocData; <color=#8929F6>…</color> là chữ tím).")]
            [TextArea(2, 4)]
            public string description;
        }

        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        public Booster[] boosters = new Booster[4];

        [Tooltip("Booster sáng tạo, theo PlayerEconomy.CreativeBoosterKeys: Khay thần kỳ. Không thẻ lần đầu, không hướng dẫn, chỉ mua được [user 20/10].")]
        public Booster[] creative = new Booster[0];

        public static Booster[] All => GameConfig.Current.boosters.boosters;
        public static Booster[] Creative => GameConfig.Current.boosters.creative;
    }
}
