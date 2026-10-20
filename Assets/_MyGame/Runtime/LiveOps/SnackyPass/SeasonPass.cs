using System;

namespace SnackDash.Economy
{
    /// <summary>
    /// Phần thưởng một mốc Snacky Pass: phần thưởng thường, rương (kèm thứ bên trong) hoặc quyền 8 tim của mốc 1.
    /// </summary>
    [Serializable]
    public struct PassReward
    {
        /// <summary>No chest; 1 silver, 2 red, 3 green, 4 pink (S_Icon_Chests2_0..3).</summary>
        public int chest;
        public bool livesPerk;
        public MissionReward reward;

        public static PassReward Of(MissionReward reward) => new PassReward { reward = reward };
        public static PassReward Chest(int chest, MissionReward contents) => new PassReward { chest = chest, reward = contents };
        public static PassReward LivesPerk => new PassReward { livesPerk = true };
    }

    /// <summary>20 chặng của Snacky Pass (thường | premium), đọc từ Data/Config/SnackyPassConfig.</summary>
    public static class SeasonPassData
    {
        public static PassReward[] Free => GameConfig.Current.snackyPass.free;
        public static PassReward[] Premium => GameConfig.Current.snackyPass.premium;
    }

    /// <summary>
    /// SNACKY PASS (opens at Level 25): a season of 20 stages. Winning a level gives tickets (normal 1, hard 2,
    /// super hard 3 [user]); stage k opens once k - 1 more tickets come in after stage k - 1, so it needs
    /// (k - 1) k / 2 in all [user]. Every open stage's free reward can be claimed; the premium ones too once the
    /// pass is active (the offer's price button activates it for free here: no purchases in this game), which also
    /// raises the lives to 8 for the season. After the season a new one starts from nothing.
    /// </summary>
    public sealed class SeasonPass
    {
        private static SnackyPassConfig Config => GameConfig.Current.snackyPass;
        public static int UnlockLevel => Config.unlockLevel;
        public static int Stages => Math.Min(Config.free.Length, Config.premium.Length);
        public static int SeasonDays => Config.seasonDays;
        public static int PremiumLives => Config.premiumLives;

        private static SeasonPass _shared;
        public static SeasonPass Shared => _shared ?? (_shared = new SeasonPass(new PlayerPrefsStore(), () => GameClock.UtcNow, PlayerEconomy.Shared));

        private readonly IStore _store;
        private readonly Func<DateTime> _now;
        private readonly PlayerEconomy _economy;

        public event Action Changed;

        public SeasonPass(IStore store, Func<DateTime> now, PlayerEconomy economy)
        {
            _store = store; _now = now; _economy = economy;
            economy.Goal += Record;
        }

        // ------------------------------------------------------------------ the season

        private long Ticks(string key) => long.TryParse(_store.GetString(key, "0"), out long t) ? t : 0L;

        public bool Started => Ticks("sp_start") > 0;
        public DateTime SeasonStart => new DateTime(Ticks("sp_start"));
        public bool Running => Started && _now() < SeasonStart.AddDays(SeasonDays);
        public TimeSpan Left { get { var left = SeasonStart.AddDays(SeasonDays) - _now(); return left > TimeSpan.Zero ? left : TimeSpan.Zero; } }

        /// <summary>Starts the first season at Level 25 and a new one when a season is over (the lives go back to 5).</summary>
        public void StartIfUnlocked(int level)
        {
            if (level < UnlockLevel || Running) return;
            if (Premium) _economy.SetLifeCap(PlayerEconomy.MaxLives);
            for (int stage = 1; stage <= Stages; stage++)
            {
                _store.SetInt(Key("sp_free", stage), 0);
                _store.SetInt(Key("sp_paid", stage), 0);
            }
            _store.SetInt("sp_tickets", 0);
            _store.SetInt("sp_premium", 0);
            _store.SetString("sp_start", _now().Ticks.ToString());
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ tickets and stages

        public int Tickets => _store.GetInt("sp_tickets", 0);

        /// <summary>Tickets in all to open <paramref name="stage"/>: 0, 1, 3, 6, ...</summary>
        public static int TicketsFor(int stage) => (stage - 1) * stage / 2;

        /// <summary>The last open stage (1 at the start, 20 at most).</summary>
        public int Stage
        {
            get
            {
                int stage = 1;
                while (stage < Stages && Tickets >= TicketsFor(stage + 1)) stage++;
                return stage;
            }
        }

        public bool Open(int stage) => stage <= Stage;

        /// <summary>Tickets toward the next stage and how many it needs ("0/1" on the bar), or the last stage's when all are open.</summary>
        public (int have, int need) Progress
        {
            get
            {
                int stage = Stage;
                if (stage >= Stages) return (stage - 1, stage - 1);
                return (Tickets - TicketsFor(stage), stage);
            }
        }

        /// <summary>A won level gives its tickets (normal 1, hard 2, super hard 3): one for any win, one more for hard, two more for super hard.</summary>
        public void Record(MissionGoal goal, int amount)
        {
            if (!Running) return;
            int tickets = goal == MissionGoal.CompleteAnyLevel ? 1 : goal == MissionGoal.CompleteHardLevel ? 1 : goal == MissionGoal.CompleteSuperHardLevel ? 2 : 0;
            if (tickets == 0) return;
            _store.SetInt("sp_tickets", Tickets + tickets * amount);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ premium and claiming

        public bool Premium => _store.GetInt("sp_premium", 0) != 0;

        /// <summary>The pass is active for the season: premium rewards open and the lives go up to 8.</summary>
        public void Activate()
        {
            if (Premium || !Running) return;
            _store.SetInt("sp_premium", 1);
            _economy.SetLifeCap(PremiumLives);
            Changed?.Invoke();
        }

        private static string Key(string what, int stage) => what + "_" + stage;

        public static PassReward RewardAt(int stage, bool premium) => (premium ? SeasonPassData.Premium : SeasonPassData.Free)[stage - 1];

        public bool Claimed(int stage, bool premium) => _store.GetInt(Key(premium ? "sp_paid" : "sp_free", stage), 0) != 0;

        public bool Claimable(int stage, bool premium) => Running && Open(stage) && !Claimed(stage, premium) && (!premium || Premium);

        /// <summary>Rewards waiting to be claimed (the widget's dot).</summary>
        public int ClaimableCount
        {
            get
            {
                int count = 0;
                for (int stage = 1; stage <= Stages; stage++)
                {
                    if (Claimable(stage, false)) count++;
                    if (Claimable(stage, true)) count++;
                }
                return count;
            }
        }

        /// <summary>Gives a stage's reward (a chest gives its contents; the lives perk fills the 8 lives).</summary>
        public bool Claim(int stage, bool premium)
        {
            if (!Claimable(stage, premium)) return false;
            _store.SetInt(Key(premium ? "sp_paid" : "sp_free", stage), 1);
            var reward = RewardAt(stage, premium);
            if (reward.livesPerk) _economy.SetLifeCap(PremiumLives);
            else reward.reward.Grant(_economy);
            Changed?.Invoke();
            return true;
        }
    }
}
