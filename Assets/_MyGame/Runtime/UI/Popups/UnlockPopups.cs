namespace SnackDash.UI
{
    /// <summary>
    /// Những gì mỗi màn đầu game mang tới [IngredientUnlockConfig_FTUE_AB (v1.15, khớp thứ tự màn của dự án), LocData ldf_*_name /
    /// ldf_*_desc, băng booster; user]. Cơ chế mở khi thắng màn trước nó ("UNLOCKED!" ở màn thắng) và thẻ của nó ("NEW ITEM!",
    /// <see cref="UnlockCardPopup"/>) hiện khi vào màn đó; thẻ booster ("NEW BOOSTER!") cũng hiện khi vào màn của nó.
    /// </summary>
    public static class UnlockPopups
    {
        public struct Entry
        {
            public int level;      // màn đầu tiên có nó
            public bool booster;
            public int icon;       // chỉ số mechanicIcons, hoặc booster (0 xẻng, 1 boomerang, 2 undo, 3 nam châm)
            public string name, description;
        }

        private const string Highlight = "#8929F6";

        /// <summary>Các cơ chế mới; booster lấy từ Data/Config/BoosterConfig (xem <see cref="TryGet"/>).</summary>
        public static readonly Entry[] Mechanics =
        {
            new Entry { level = 5, icon = 0, name = "BRIDGE", description = $"<color={Highlight}>CROSS THE ROAD</color> WITHOUT HITTING YOURSELF!" },
            new Entry { level = 9, icon = 1, name = "GATE", description = $"YOU CAN PASS THEM ONLY THROUGH <color={Highlight}>ONE WAY!</color>" },
            new Entry { level = 14, icon = 2, name = "TUNNEL", description = $"<color={Highlight}>DIVE</color> IN ONE SIDE, <color={Highlight}>LEAVE</color> FROM THE OTHER!" },
            new Entry { level = 21, icon = 3, name = "WALL", description = $"<color={Highlight}>COMPLETE ORDERS</color> AND DESTROY THE BLOCKING WALLS!" },
            new Entry { level = 26, icon = 4, name = "MYSTERY FRUIT", description = $"COLLECT THE <color={Highlight}>MYSTERY FRUITS</color> TO REVEAL WHAT'S INSIDE!" },
            new Entry { level = 31, icon = 5, name = "ARROWS", description = $"MAKES THE ROAD ONE WAY, <color={Highlight}>CHANGES THE DIRECTION</color> WHEN YOU PASS!" },
        };

        /// <summary>
        /// Thanh "NEW ITEM" ở màn thắng (IngredientUnlockConfig_FTUE_AB): đầy dần từ levelToStartShowingUp tới levelToUnlock, cơ chế có ở
        /// màn sau đó. Icon = chỉ số mechanicIcons.
        /// </summary>
        private static readonly (int show, int unlock, int icon)[] Bar =
        {
            (0, 4, 0), (4, 8, 1), (8, 13, 2), (13, 20, 3), (20, 25, 4), (25, 30, 5), (30, 40, 6), (40, 50, 7),
        };

        /// <summary>Thanh sau khi thắng <paramref name="level"/>: phần đầy trước / sau trận và hình; false khi không có cơ chế sắp tới.</summary>
        public static bool NewItemBar(int level, out float from, out float to, out int icon)
        {
            foreach (var b in Bar)
                if (level > b.show && level <= b.unlock)
                {
                    from = (level - 1 - b.show) / (float)(b.unlock - b.show);
                    to = (level - b.show) / (float)(b.unlock - b.show);
                    icon = b.icon;
                    return true;
                }
            from = to = 0f;
            icon = 0;
            return false;
        }

        public static bool TryGet(int level, bool booster, out Entry entry)
        {
            if (booster)
            {
                var boosters = SnackDash.Economy.BoosterConfig.All;
                for (int i = 0; i < boosters.Length; i++)
                    if (boosters[i].unlockLevel == level) { entry = new Entry { level = level, booster = true, icon = i, name = boosters[i].name, description = boosters[i].description }; return true; }
            }
            else foreach (var e in Mechanics)
                if (e.level == level) { entry = e; return true; }
            entry = default;
            return false;
        }
    }
}
