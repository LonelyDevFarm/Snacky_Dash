using System;
using System.Collections.Generic;

namespace SnackDash.Economy
{
    /// <summary>
    /// Thứ một nhiệm vụ tuần đếm; đánh số theo MissionGoalType gốc.
    /// </summary>
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

    [Serializable]
    public sealed class Mission
    {
        public MissionGoal goal;
        public int amount;
        public MissionReward reward;

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

    /// <summary>Tuần nhiệm vụ và các mốc, đọc từ Data/Config/WeeklyMissionsConfig (bản gốc lấy từ remote config).</summary>
    public static class WeeklyMissionsData
    {
        public static WeeklyMissionsConfig.Day[] Days => GameConfig.Current.weeklyMissions.days;
        public static WeeklyMissionsConfig.Milestone[] Milestones => GameConfig.Current.weeklyMissions.milestones;
    }

    /// <summary>
    /// WEEKLY ADVENTURE (opens at Level 15): seven days of five missions. Every mission counts from the start
    /// of the week, a day's missions can only be claimed from that day on ("Unlock at Day N!"); claiming gives
    /// its reward and tokens, and tokens fill the bar of milestone chests. After seven days a new week starts.
    /// </summary>
    public sealed class WeeklyMissions
    {
        public static int UnlockLevel => GameConfig.Current.weeklyMissions.unlockLevel;
        public static int WeekDays => WeeklyMissionsData.Days.Length;

        private static WeeklyMissions _shared;
        public static WeeklyMissions Shared => _shared ?? (_shared = new WeeklyMissions(new PlayerPrefsStore(), () => GameClock.UtcNow, PlayerEconomy.Shared));

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

        /// <summary>Cheat / test: every mission of <paramref name="day"/> (1 to 7) done, waiting to be claimed.</summary>
        public void CheatFinishDay(int day)
        {
            if (day < 1 || day > WeekDays) return;
            for (int i = 0; i < WeeklyMissionsData.Days[day - 1].Length; i++)
                _store.SetInt(Key("wm_p", day - 1, i), WeeklyMissionsData.Days[day - 1][i].amount);
            Changed?.Invoke();
        }

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
