using System;

namespace SnackDash.Economy
{
    /// <summary>One Shop product: what it gives. Every price is 0đ and buying gives it at once [user].</summary>
    [Serializable]
    public sealed class ShopPack
    {
        public string title;        // shown on bundle cards
        public UnityEngine.Sprite image; // hình chính của thẻ
        public int coins;
        public int infiniteSeconds; // infinite lives
        public int[] boosters;      // per PlayerEconomy.BoosterKeys (shovel, boomerang, undo, magnet)

        public void Grant(PlayerEconomy economy)
        {
            if (coins > 0) economy.AddCoins(coins);
            if (infiniteSeconds > 0) economy.AddInfiniteLives(TimeSpan.FromSeconds(infiniteSeconds));
            if (boosters != null)
                for (int i = 0; i < boosters.Length && i < PlayerEconomy.BoosterKeys.Length; i++)
                    if (boosters[i] > 0) economy.AddBoosters(PlayerEconomy.BoosterKeys[i], boosters[i]);
        }
    }

    /// <summary>Các gói của Shop, đọc từ Data/Config/ShopConfig.</summary>
    public static class ShopCatalog
    {
        public static ShopPack[] Bundles => GameConfig.Current.shop.bundles;
        public static ShopPack[] Coins => GameConfig.Current.shop.coins;

        /// <summary>"2500": the video writes amounts without a separator (the prefab samples have "2 500").</summary>
        public static string Amount(int value) => value.ToString();

        /// <summary>Infinite lives on a card: "30m", "1h", "24h".</summary>
        public static string Duration(int seconds) => seconds >= 3600 ? (seconds / 3600) + "h" : (seconds / 60) + "m";
    }
}
