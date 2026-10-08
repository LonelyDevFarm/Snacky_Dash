using System;
using System.Collections.Generic;

namespace SnackDash.Economy
{
    /// <summary>What a weekly mission counts; numbered as the original MissionGoalType.</summary>
    public enum MissionGoal
    {
        CollectCoins = 0,
        SpendCoins = 1,
        SpendAnyBooster = 2,
        SpendBoomerang = 5,
        SpendShovel = 6,
        CompleteAnyLevel = 7,
        CompleteHardLevel = 8,
        CompleteSuperHardLevel = 9,
        CompleteLevelInStreak = 10,
        WatchRewardedVideo = 11,
        CompletePurchase = 12,
        LoginDay = 13,
        SpendUndo = 14,
        SpendMagnet = 18,
    }

    /// <summary>A mission or milestone reward: tokens (the hat) plus coins, infinite lives or boosters.</summary>
    [Serializable]
    public struct MissionReward
    {
        public int points, coins, infiniteMinutes;
        public int shovel, boomerang, undo, magnet;

        public MissionReward(int points, int coins = 0, int infiniteMinutes = 0, int shovel = 0, int boomerang = 0, int undo = 0, int magnet = 0)
        {
            this.points = points; this.coins = coins; this.infiniteMinutes = infiniteMinutes;
            this.shovel = shovel; this.boomerang = boomerang; this.undo = undo; this.magnet = magnet;
        }

        /// <summary>Boosters in bar order (PlayerEconomy.BoosterKeys).</summary>
        public int[] Boosters => new[] { shovel, boomerang, undo, magnet };

        public void Grant(PlayerEconomy economy)
        {
            if (coins > 0) economy.AddCoins(coins);
            if (infiniteMinutes > 0) economy.AddInfiniteLives(TimeSpan.FromMinutes(infiniteMinutes));
            var boosters = Boosters;
            for (int i = 0; i < boosters.Length; i++) if (boosters[i] > 0) economy.AddBoosters(PlayerEconomy.BoosterKeys[i], boosters[i]);
        }
    }

    public sealed class Mission
    {
        public readonly MissionGoal goal;
        public readonly int amount;
        public readonly MissionReward reward;

        public Mission(MissionGoal goal, int amount, MissionReward reward) { this.goal = goal; this.amount = amount; this.reward = reward; }

        /// <summary>The English line of LocData weekly_adventure_task_*.</summary>
        public string Text
        {
            get
            {
                bool one = amount == 1;
                switch (goal)
                {
                    case MissionGoal.CollectCoins: return $"Earn {amount} Coins!";
                    case MissionGoal.SpendCoins: return $"Spend {amount} Coins!";
                    case MissionGoal.SpendAnyBooster: return one ? "Use 1 Booster!" : $"Use {amount} Boosters!";
                    case MissionGoal.SpendBoomerang: return one ? "Use 1 Boomerang!" : $"Use {amount} Boomerangs!";
                    case MissionGoal.SpendShovel: return one ? "Use 1 Shovel!" : $"Use {amount} Shovels!";
                    case MissionGoal.SpendUndo: return one ? "Use 1 Undo!" : $"Use {amount} Undos!";
                    case MissionGoal.SpendMagnet: return one ? "Use 1 Magnet!" : $"Use {amount} Magnets!";
                    case MissionGoal.CompleteAnyLevel: return $"Win {amount} Levels!";
                    case MissionGoal.CompleteHardLevel: return $"Win {amount} Hard Levels!";
                    case MissionGoal.CompleteSuperHardLevel: return $"Win {amount} Super Hard Levels!";
                    case MissionGoal.CompleteLevelInStreak: return $"Win {amount} Levels In A Row!";
                    case MissionGoal.WatchRewardedVideo: return one ? "Multiply Coins by Ads 1 Time!" : $"Multiply Coins by Ads {amount} Times!";
                    case MissionGoal.CompletePurchase: return "Make a purchase!";
                    case MissionGoal.LoginDay: return one ? "Login 1 Day!" : $"Login {amount} Days!";
                    default: return goal.ToString();
                }
            }
        }
    }

    /// <summary>
    /// The week of missions. The original takes it from remote config; days 1-5 are copied from the phone
    /// (video sk1), days 6-7 from the user's screenshots; only the first marker's reward is a stand-in.
    /// </summary>
    public static class WeeklyMissionsData
    {
        private static Mission M(MissionGoal goal, int amount, int points, int coins = 0, int infinite = 0, int shovel = 0, int boomerang = 0, int magnet = 0)
            => new Mission(goal, amount, new MissionReward(points, coins, infinite, shovel, boomerang, magnet: magnet));

        public static readonly Mission[][] Days =
        {
            new[] { M(MissionGoal.LoginDay, 1, 20, infinite: 15), M(MissionGoal.CompleteLevelInStreak, 5, 50), M(MissionGoal.CompleteAnyLevel, 8, 20),
                    M(MissionGoal.SpendShovel, 1, 50, coins: 50), M(MissionGoal.CollectCoins, 250, 20) },
            new[] { M(MissionGoal.LoginDay, 2, 20, infinite: 15), M(MissionGoal.WatchRewardedVideo, 1, 50, coins: 50), M(MissionGoal.CompleteHardLevel, 2, 40),
                    M(MissionGoal.SpendAnyBooster, 2, 50), M(MissionGoal.SpendMagnet, 1, 40) },
            new[] { M(MissionGoal.LoginDay, 3, 30, infinite: 15), M(MissionGoal.CompleteLevelInStreak, 8, 70, coins: 100), M(MissionGoal.CompleteAnyLevel, 20, 40),
                    M(MissionGoal.SpendBoomerang, 1, 40), M(MissionGoal.SpendCoins, 900, 70) },
            new[] { M(MissionGoal.LoginDay, 4, 30, infinite: 15), M(MissionGoal.CompleteHardLevel, 6, 70), M(MissionGoal.SpendAnyBooster, 4, 70),
                    M(MissionGoal.CollectCoins, 1000, 40), M(MissionGoal.CompletePurchase, 1, 240, infinite: 120) },
            new[] { M(MissionGoal.LoginDay, 5, 30, infinite: 15), M(MissionGoal.CompleteLevelInStreak, 12, 90, magnet: 1),
                    M(MissionGoal.CompleteSuperHardLevel, 5, 70), M(MissionGoal.SpendUndo, 2, 70), M(MissionGoal.SpendCoins, 1800, 90) },
            // Days 6-7 from the user's screenshots (08/10).
            new[] { M(MissionGoal.LoginDay, 6, 30, infinite: 30), M(MissionGoal.CompleteHardLevel, 12, 120), M(MissionGoal.SpendAnyBooster, 15, 120, boomerang: 1),
                    M(MissionGoal.CollectCoins, 2000, 90), M(MissionGoal.WatchRewardedVideo, 3, 90, coins: 100) },
            new[] { M(MissionGoal.LoginDay, 7, 30, infinite: 30), M(MissionGoal.CompleteLevelInStreak, 15, 140, boomerang: 1), M(MissionGoal.CompleteSuperHardLevel, 7, 130),
                    M(MissionGoal.CompleteAnyLevel, 60, 90), M(MissionGoal.SpendCoins, 3000, 140, shovel: 1) },
        };

        /// <summary>
        /// Points to reach each milestone and what it gives: first marker, blue chest, purple chest, grand prize.
        /// Purple 1000 and grand 2000 with their rewards from video sk1.1 (marker labels and bubbles, 33-34 s); the blue
        /// chest (undo + magnet) seen opening in sk1 at ~500 measured on the bar; the bronze one (boomerang + 15 min of lives) in sk1.3 20 s.
        /// </summary>
        public static readonly (int points, MissionReward reward)[] Milestones =
        {
            (150, new MissionReward(0, infiniteMinutes: 15, boomerang: 1)),
            (500, new MissionReward(0, undo: 1, magnet: 1)),
            (1000, new MissionReward(0, coins: 500, infiniteMinutes: 30, boomerang: 1)),
            (2000, new MissionReward(0, coins: 1000, infiniteMinutes: 60, shovel: 1, boomerang: 1)),
        };
    }

    /// <summary>
    /// WEEKLY ADVENTURE (opens at Level 15): seven days of five missions. Every mission counts from the start
    /// of the week, a day's missions can only be claimed from that day on ("Unlock at Day N!"); claiming gives
    /// its reward and tokens, and tokens fill the bar of milestone chests. After seven days a new week starts.
    /// </summary>
    public sealed class WeeklyMissions
    {
        public const int UnlockLevel = 15, WeekDays = 7;

        private static WeeklyMissions _shared;
        public static WeeklyMissions Shared => _shared ?? (_shared = new WeeklyMissions(new PlayerPrefsStore(), () => DateTime.UtcNow, PlayerEconomy.Shared));

        private readonly IStore _store;
        private readonly Func<DateTime> _now;
        private readonly PlayerEconomy _economy;

        public event Action Changed;

        public WeeklyMissions(IStore store, Func<DateTime> now, PlayerEconomy economy)
        {
            _store = store; _now = now; _economy = economy;
            economy.Goal += Record;
        }

        // ------------------------------------------------------------------ the week

        private long Ticks(string key) => long.TryParse(_store.GetString(key, "0"), out long t) ? t : 0L;

        public bool Started => Ticks("wm_start") > 0;

        /// <summary>Starts the first week once the player reaches Level 15, and a new one when a week is over.</summary>
        public void StartIfUnlocked(int level)
        {
            if (level < UnlockLevel) return;
            if (Started && _now() < WeekStart.AddDays(WeekDays)) return;
            for (int day = 0; day < WeekDays; day++)
                for (int i = 0; i < WeeklyMissionsData.Days[day].Length; i++)
                {
                    _store.SetInt(Key("wm_p", day, i), 0);
                    _store.SetInt(Key("wm_c", day, i), 0);
                }
            _store.SetInt("wm_points", 0);
            _store.SetInt("wm_milestones", 0);
            _store.SetInt("wm_logins", 0);
            _store.SetString("wm_login_day", "");
            _store.SetString("wm_start", _now().Ticks.ToString());
            Login();
            Changed?.Invoke();
        }

        public DateTime WeekStart => new DateTime(Ticks("wm_start"));
        public TimeSpan Left { get { var left = WeekStart.AddDays(WeekDays) - _now(); return left > TimeSpan.Zero ? left : TimeSpan.Zero; } }

        /// <summary>Today, 1 to 7.</summary>
        public int Day => Math.Min(WeekDays, (int)((_now() - WeekStart).TotalDays) + 1);

        public bool DayOpen(int day) => day <= Day;

        /// <summary>Counts today as a login day (once per day).</summary>
        public void Login()
        {
            if (!Started) return;
            string today = (_now() - WeekStart).Days.ToString();
            if (_store.GetString("wm_login_day", "") == today) return;
            _store.SetString("wm_login_day", today);
            int logins = _store.GetInt("wm_logins", 0) + 1;
            _store.SetInt("wm_logins", logins);
            Set(MissionGoal.LoginDay, logins);
        }

        // ------------------------------------------------------------------ missions

        private static string Key(string what, int day, int index) => $"{what}_{day}_{index}";

        public static Mission MissionAt(int day, int index) => WeeklyMissionsData.Days[day - 1][index];

        public int Progress(int day, int index) => Math.Min(MissionAt(day, index).amount, _store.GetInt(Key("wm_p", day - 1, index), 0));
        public bool Done(int day, int index) => Progress(day, index) >= MissionAt(day, index).amount;
        public bool Claimed(int day, int index) => _store.GetInt(Key("wm_c", day - 1, index), 0) != 0;
        public bool Claimable(int day, int index) => Done(day, index) && !Claimed(day, index);

        /// <summary>Missions done but not claimed (the Home widget's number), all days.</summary>
        public int ClaimableCount
        {
            get
            {
                int count = 0;
                for (int day = 1; day <= WeekDays; day++)
                    for (int i = 0; i < WeeklyMissionsData.Days[day - 1].Length; i++)
                        if (Claimable(day, i)) count++;
                return count;
            }
        }

        public bool DayHasClaimable(int day)
        {
            for (int i = 0; i < WeeklyMissionsData.Days[day - 1].Length; i++) if (Claimable(day, i)) return true;
            return false;
        }

        /// <summary>Something happened in the game: every mission of that goal moves on (claimed ones stay).</summary>
        public void Record(MissionGoal goal, int amount)
        {
            if (!Started || _now() >= WeekStart.AddDays(WeekDays)) return;
            if (goal == MissionGoal.CompleteLevelInStreak) { Set(goal, amount); return; }
            ForEach(goal, (day, i) => _store.SetInt(Key("wm_p", day, i), _store.GetInt(Key("wm_p", day, i), 0) + amount));
        }

        /// <summary>Missions that show a running count (logins, wins in a row): the count now; done ones keep done.</summary>
        private void Set(MissionGoal goal, int value)
        {
            ForEach(goal, (day, i) =>
            {
                if (_store.GetInt(Key("wm_p", day, i), 0) < WeeklyMissionsData.Days[day][i].amount) _store.SetInt(Key("wm_p", day, i), value);
            });
        }

        private void ForEach(MissionGoal goal, Action<int, int> act)
        {
            bool any = false;
            for (int day = 0; day < WeekDays; day++)
                for (int i = 0; i < WeeklyMissionsData.Days[day].Length; i++)
                    if (WeeklyMissionsData.Days[day][i].goal == goal && _store.GetInt(Key("wm_c", day, i), 0) == 0) { act(day, i); any = true; }
            if (any) Changed?.Invoke();
        }

        // ------------------------------------------------------------------ claiming and milestones

        public int Points => _store.GetInt("wm_points", 0);

        /// <summary>Milestones already given out (0 to 4).</summary>
        public int MilestonesReached => _store.GetInt("wm_milestones", 0);

        /// <summary>
        /// Claims a done mission of an open day: its reward and tokens; returns the milestones it reached
        /// (their rewards are given too), empty when none or when it could not be claimed.
        /// </summary>
        public List<int> Claim(int day, int index)
        {
            var reached = new List<int>();
            if (!DayOpen(day) || !Claimable(day, index)) return reached;
            _store.SetInt(Key("wm_c", day - 1, index), 1);
            var reward = MissionAt(day, index).reward;
            reward.Grant(_economy);
            int points = Points + reward.points;
            _store.SetInt("wm_points", points);
            var milestones = WeeklyMissionsData.Milestones;
            for (int m = MilestonesReached; m < milestones.Length && points >= milestones[m].points; m++)
            {
                milestones[m].reward.Grant(_economy);
                _store.SetInt("wm_milestones", m + 1);
                reached.Add(m);
            }
            Changed?.Invoke();
            return reached;
        }
    }
}
