using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>4 booster theo thứ tự thanh dưới: xẻng, boomerang, undo, nam châm. Thanh booster và thẻ NEW BOOSTER! cùng đọc ở đây.</summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Boosters", fileName = "BoosterConfig")]
    public class BoosterConfig : ScriptableObject
    {
        [System.Serializable]
        public class Booster
        {
            public string name;
            [Tooltip("Màn đầu tiên dùng được (màn đó tặng FREE và hiện thẻ NEW BOOSTER!).")]
            public int unlockLevel;
            [Tooltip("Lời mô tả trên băng chọn booster và trên thẻ (LocData; <color=#8929F6>…</color> là chữ tím).")]
            [TextArea(2, 4)]
            public string description;
        }

        [Tooltip("Xẻng, boomerang, undo, nam châm.")]
        public Booster[] boosters = new Booster[4];

        public static Booster[] All => GameConfig.Current.boosters.boosters;
    }
}
