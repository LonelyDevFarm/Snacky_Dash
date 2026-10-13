using System;
using SnackDash.Economy;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// "Snip Tail" (cắt đuôi để chơi tiếp khi rắn tự cắn đuôi): giá cố định <see cref="PlayerEconomy.SnipPrice"/> [user].
    /// LoseFlowView gọi; bản gốc tăng giá mỗi lần (900, 1900) từ remote config.
    /// </summary>
    [Serializable]
    public class SnipOffer
    {
        public int UsesThisLevel { get; private set; }
        public int CurrentCost => PlayerEconomy.SnipPrice;

        /// <summary>False khi rắn không kẹt hoặc thiếu xu (bên gọi mở Shop).</summary>
        public bool TryUse(PlayerEconomy economy, GameSession session) => TryUse(economy, session, CurrentCost);

        /// <param name="cost">Giá lần này (0 ở hướng dẫn snip Level 3).</param>
        public bool TryUse(PlayerEconomy economy, GameSession session, int cost)
        {
            if (!session.IsStuck || !economy.TrySpend(cost)) return false;
            UsesThisLevel++;
            return session.Snip();
        }
    }
}
