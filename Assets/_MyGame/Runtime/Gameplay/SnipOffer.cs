using System;
using UnityEngine;

namespace SnackDash.Gameplay
{
    /// <summary>Coins owned by the player. Persistence and earning come with the coin economy (later).</summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }

        public event Action<int> Changed;

        public Wallet(int coins = 0) => Coins = coins;

        public void Add(int amount)
        {
            Coins += amount;
            Changed?.Invoke(Coins);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            Changed?.Invoke(Coins);
            return true;
        }
    }

    /// <summary>
    /// Price of "Snip Tail" (continue after biting the tail), rising with each use in a level.
    /// The original charged 900 coins, then 1900 (video Man_Lose); this project keeps the price at 0
    /// until the coin economy exists, so set baseCost / costIncrease here when it does.
    /// </summary>
    [Serializable]
    public class SnipOffer
    {
        [Tooltip("Original: 900.")] public int baseCost;
        [Tooltip("Original: +1000 per use.")] public int costIncrease;

        public int UsesThisLevel { get; private set; }
        public int CurrentCost => baseCost + costIncrease * UsesThisLevel;

        public bool TryUse(Wallet wallet, GameSession session)
        {
            if (!session.IsStuck || !wallet.TrySpend(CurrentCost)) return false;
            UsesThisLevel++;
            return session.Snip();
        }
    }
}
