using System;

namespace SnackDash.Economy
{
    /// <summary>One Shop product: what it gives. Every price is 0đ and buying gives it at once [user].</summary>
    public sealed class ShopPack
    {
        public string title;        // shown on bundle cards
        public string icon;         // main reward sprite name
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

    /// <summary>
    /// The original ShopConfig.asset (sections Bundles and Coins). Left out [user]: No Ads, Starter Pack, No Ads
    /// Pack and the NoAds item inside packs. Booster items map Tunnel → shovel, FruitCannon → boomerang,
    /// Shuffle → undo, Shrink → magnet (the Starter Pack card in the video shows them in that order).
    /// </summary>
    public static class ShopCatalog
    {
        public static readonly ShopPack[] Bundles =
        {
            new ShopPack { title = "Small Pack", icon = "S_Offer_Stars_Small_Pack", coins = 2500, infiniteSeconds = 1800 },
            new ShopPack { title = "Medium Pack", icon = "S_Offer_Stars_Medium_Pack", coins = 7500, infiniteSeconds = 7200, boosters = new[] { 2, 2, 0, 0 } },
            new ShopPack { title = "Large Pack", icon = "S_Offer_Stars_Large_Pack", coins = 20000, infiniteSeconds = 14400, boosters = new[] { 4, 4, 4, 4 } },
            new ShopPack { title = "Extra Large Pack", icon = "S_Offer_Stars_XL_Pack", coins = 40000, infiniteSeconds = 28800, boosters = new[] { 8, 8, 8, 8 } },
            new ShopPack { title = "XXL Pack", icon = "S_Offer_Stars_XXL_Pack", coins = 90000, infiniteSeconds = 86400, boosters = new[] { 12, 12, 12, 12 } },
        };

        public static readonly ShopPack[] Coins =
        {
            new ShopPack { icon = "S_1000_Coins", coins = 1000 },
            new ShopPack { icon = "S_5 000_Coins", coins = 5000 },
            new ShopPack { icon = "S_10 000_Coins", coins = 10000 },
            new ShopPack { icon = "S_25 000_Coins", coins = 25000 },
            new ShopPack { icon = "S_50 000_Coins", coins = 50000 },
            new ShopPack { icon = "S_100 000_Coins", coins = 100000 },
        };

        /// <summary>"2500": the video writes amounts without a separator (the prefab samples have "2 500").</summary>
        public static string Amount(int value) => value.ToString();

        /// <summary>Infinite lives on a card: "30m", "1h", "24h".</summary>
        public static string Duration(int seconds) => seconds >= 3600 ? (seconds / 3600) + "h" : (seconds / 60) + "m";
    }
}
