using System;
using SnackDash.Economy;

namespace SnackDash.Gameplay
{
    /// <summary>
    /// "Snip Tail" (continue after biting the tail): always <see cref="PlayerEconomy.SnipPrice"/> coins [user]
    /// (the original raised it each use, 900 then 1900, from remote config).
    /// </summary>
    [Serializable]
    public class SnipOffer
    {
        public int UsesThisLevel { get; private set; }
        public int CurrentCost => PlayerEconomy.SnipPrice;

        /// <summary>False when the snake is not stuck or the coins are short (the caller opens the Shop).</summary>
        public bool TryUse(PlayerEconomy economy, GameSession session) => TryUse(economy, session, CurrentCost);

        /// <param name="cost">The price this time (0 on the Level 3 snip tutorial).</param>
        public bool TryUse(PlayerEconomy economy, GameSession session, int cost)
        {
            if (!session.IsStuck || !economy.TrySpend(cost)) return false;
            UsesThisLevel++;
            return session.Snip();
        }
    }
}
