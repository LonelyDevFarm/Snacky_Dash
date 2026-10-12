using System;
using SnackDash.Economy;

namespace SnackDash.UI
{
    /// <summary>
    /// Mua thêm thùng (prefab <c>Prefabs/UI/Popups/ExtraCratePopup</c>, gốc: ExtraCratePurchasePopup): cùng khung với mua booster nhưng thấp
    /// hơn, nền 83 %, không có ô xu; hình thùng, "ADD A CRATE you can deliver into right away" và Buy 700 [user]. Chữ và hình đặt sẵn
    /// trong prefab. Mở bằng <c>UIManager.Show&lt;ExtraCratePopup&gt;()</c> rồi <see cref="Setup"/>.
    /// </summary>
    public class ExtraCratePopup : BoosterBuyPopup
    {
        public void Setup(Action paid) => Pay(PlayerEconomy.ExtraCratePrice, paid);
    }
}
