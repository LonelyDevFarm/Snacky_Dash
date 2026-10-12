using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>
    /// Các gói của Shop (ShopConfig.asset gốc, phần Bundles và Coins). Bỏ [user]: No Ads, Starter Pack, No Ads Pack. Giá đều 0đ,
    /// bấm là nhận. Booster của gói theo thứ tự xẻng, boomerang, undo, nam châm.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Config/Shop", fileName = "ShopConfig")]
    public class ShopConfig : ScriptableObject
    {
        [Tooltip("Thẻ gói lớn (tên, xu, giây tim vô hạn, booster).")]
        public ShopPack[] bundles;
        [Tooltip("Thẻ xu.")]
        public ShopPack[] coins;
    }
}
