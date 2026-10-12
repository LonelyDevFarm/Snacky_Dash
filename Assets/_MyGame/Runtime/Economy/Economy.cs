using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.Economy
{
    /// <summary>Where the economy keeps its numbers: PlayerPrefs in the game, memory in tests.</summary>
    public interface IStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
    }

    public sealed class PlayerPrefsStore : IStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
    }

    public sealed class MemoryStore : IStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public int GetInt(string key, int fallback) => _values.TryGetValue(key, out var v) && int.TryParse(v, out int i) ? i : fallback;
        public void SetInt(string key, int value) => _values[key] = value.ToString();
        public string GetString(string key, string fallback) => _values.TryGetValue(key, out var v) ? v : fallback;
        public void SetString(string key, string value) => _values[key] = value;
    }

    /// <summary>
    /// The clock of the shared economy and events: real UTC time plus <see cref="Offset"/>, which the cheat panel moves
    /// forward to test lives coming back, mission days and the end of a season or cycle. Not saved: a new run is real time.
    /// </summary>
    public static class GameClock
    {
        public static TimeSpan Offset { get; set; }
        public static DateTime UtcNow => DateTime.UtcNow + Offset;
    }

    /// <summary>
    /// The player's coins, lives and win streak, with the rules the user set (07/10, FINDINGS):
    /// - Coins: 1800 to start; a win pays 20 / 30 / 40 (normal / hard / super hard), doubled while the
    ///   streak is on fire; combo coins go to the wallet too. Snip costs 900 every time. Boosters: 1800
    ///   for 3, all the same price.
    /// - Lives: 5 at most, one comes back every 30 minutes; a lost level, leaving a level and closing the
    ///   app during a level each cost one. Refill to 5 costs 900; the "video" gives one at once.
    ///   Infinite lives (shop bundles) for a while.
    /// - Win streak (from Level 23): 5 wins, not necessarily in a row (a loss does not reset them), set
    ///   it on fire; while on fire a win pays double, and a loss puts it back to 0.
    /// </summary>
    public sealed class PlayerEconomy
    {
        public const int StartCoins = 1800;
        public const int SnipPrice = 900;
        public const int BoosterPrice = 1800, BoosterPack = 3;
        public const int ExtraCratePrice = 700; // the extra crate of a level [user screenshot, Level 30]
        public const int MaxLives = 5; // the usual cap; Snacky Pass raises it to 8 (LifeCap)
        public const int LifeRegenSeconds = 30 * 60;
        public const int RefillPrice = 900;
        public const int StreakFromLevel = 23, StreakWinsToFire = 5, StreakMultiplier = 2;

        private static PlayerEconomy _shared;
        /// <summary>The game's economy (PlayerPrefs, real clock).</summary>
        public static PlayerEconomy Shared => _shared ?? (_shared = new PlayerEconomy(new PlayerPrefsStore(), () => GameClock.UtcNow));

        private readonly IStore _store;
        private readonly Func<DateTime> _now;

        public event Action Changed;

        /// <summary>Something weekly missions count: coins earned or spent, levels won, boosters used, x2, purchases.</summary>
        public event Action<MissionGoal, int> Goal;

        /// <summary>Tells the missions about something outside the wallet (the x2 on the win screen, a shop pack).</summary>
        public void Report(MissionGoal goal, int amount = 1) => Goal?.Invoke(goal, amount);

        public PlayerEconomy(IStore store, Func<DateTime> now)
        {
            _store = store;
            _now = now;
        }

        // ------------------------------------------------------------------ coins

        public int Coins => _store.GetInt("eco_coins", StartCoins);

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            _store.SetInt("eco_coins", Coins + amount);
            Changed?.Invoke();
            Goal?.Invoke(MissionGoal.CollectCoins, amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            _store.SetInt("eco_coins", Coins - amount);
            Changed?.Invoke();
            if (amount > 0) Goal?.Invoke(MissionGoal.SpendCoins, amount);
            return true;
        }

        /// <summary>Cheat / test: the wallet becomes <paramref name="coins"/> without counting for the missions.</summary>
        public void SetCoins(int coins)
        {
            _store.SetInt("eco_coins", Math.Max(0, coins));
            Changed?.Invoke();
        }

        /// <summary>Coins for winning a level of <paramref name="difficulty"/> (LevelDefinition), before the streak.</summary>
        public static int WinReward(int difficulty) => difficulty >= 4 ? 40 : difficulty >= 2 ? 30 : 20;

        // ------------------------------------------------------------------ lives

        private long Ticks(string key) => long.TryParse(_store.GetString(key, "0"), out long t) ? t : 0;
        private void SetTicks(string key, long ticks) => _store.SetString(key, ticks.ToString());

        public bool InfiniteLives => _now().Ticks < Ticks("eco_infinite_until");
        public TimeSpan InfiniteLeft => InfiniteLives ? new TimeSpan(Ticks("eco_infinite_until") - _now().Ticks) : TimeSpan.Zero;

        /// <summary>Lives now, after the ones that came back while away.</summary>
        public int Lives
        {
            get
            {
                Regenerate();
                return _store.GetInt("eco_lives", MaxLives);
            }
        }

        /// <summary>Time until the next life comes back (zero when full).</summary>
        public TimeSpan NextLifeIn
        {
            get
            {
                Regenerate();
                if (_store.GetInt("eco_lives", MaxLives) >= LifeCap) return TimeSpan.Zero;
                var next = new DateTime(Ticks("eco_life_since")).AddSeconds(LifeRegenSeconds);
                var left = next - _now();
                return left > TimeSpan.Zero ? left : TimeSpan.Zero;
            }
        }

        public bool CanPlay => InfiniteLives || Lives > 0;

        /// <summary>Most lives now: 5, or 8 while Snacky Pass is active (SeasonPassConfig MaxLivesPerk).</summary>
        public int LifeCap => _store.GetInt("eco_life_cap", MaxLives);

        /// <summary>Sets the cap; keeping or raising it fills the lives up to it ("Maximum amount of lives has increased to 8!"), lowering it keeps no more than it.</summary>
        public void SetLifeCap(int cap)
        {
            int lives = Lives;
            bool fill = cap >= LifeCap;
            _store.SetInt("eco_life_cap", cap);
            _store.SetInt("eco_lives", fill ? cap : Math.Min(lives, cap));
            Changed?.Invoke();
        }

        private void Regenerate()
        {
            int lives = _store.GetInt("eco_lives", MaxLives);
            if (lives >= LifeCap) return;
            var since = new DateTime(Ticks("eco_life_since"));
            var now = _now();
            int back = (int)((now - since).TotalSeconds / LifeRegenSeconds);
            if (back <= 0) return;
            lives = Math.Min(LifeCap, lives + back);
            _store.SetInt("eco_lives", lives);
            SetTicks("eco_life_since", since.AddSeconds(back * (double)LifeRegenSeconds).Ticks);
        }

        public void LoseLife()
        {
            if (InfiniteLives) return;
            int lives = Lives;
            if (lives <= 0) return;
            if (lives >= LifeCap) SetTicks("eco_life_since", _now().Ticks); // the 30 minutes start now
            _store.SetInt("eco_lives", lives - 1);
            Changed?.Invoke();
        }

        /// <summary>One more life, never above the maximum ("video" in the refill popup).</summary>
        public void AddLife()
        {
            int lives = Lives;
            if (lives >= LifeCap) return;
            _store.SetInt("eco_lives", lives + 1);
            Changed?.Invoke();
        }

        public bool TryRefill()
        {
            if (Lives >= LifeCap || !TrySpend(RefillPrice)) return false;
            _store.SetInt("eco_lives", LifeCap);
            Changed?.Invoke();
            return true;
        }

        /// <summary>No life is lost meanwhile and the lives are full when it ends [user].</summary>
        public void AddInfiniteLives(TimeSpan time)
        {
            _store.SetInt("eco_lives", LifeCap);
            long from = Math.Max(_now().Ticks, Ticks("eco_infinite_until"));
            SetTicks("eco_infinite_until", from + time.Ticks);
            Changed?.Invoke();
        }

        /// <summary>
        /// A level is under way: closing the app now costs a life (charged on the next start). Cleared by
        /// winning or losing.
        /// </summary>
        public bool LevelInProgress
        {
            get => _store.GetInt("eco_level_active", 0) != 0;
            set => _store.SetInt("eco_level_active", value ? 1 : 0);
        }

        /// <summary>Called once at app start: a level left open by closing the app costs its life.</summary>
        public void ChargeUncleanExit()
        {
            if (!LevelInProgress) return;
            LevelInProgress = false;
            LoseLife();
            _store.SetInt("eco_wins_in_row", 0); // "Win N levels in a row" starts over
            Goal?.Invoke(MissionGoal.CompleteLevelInStreak, 0);
        }

        // ------------------------------------------------------------------ win streak

        public int StreakWins => _store.GetInt("eco_streak_wins", 0);
        public bool StreakOnFire => _store.GetInt("eco_streak_fire", 0) != 0;
        public static bool StreakShown(int level) => level >= StreakFromLevel;
        public int StreakMultiplierNow => StreakOnFire ? StreakMultiplier : 1;

        /// <summary>A level won: pays its reward (doubled on fire) and moves the streak. Returns the coins.</summary>
        public int Win(int level, int difficulty)
        {
            LevelInProgress = false;
            int coins = WinReward(difficulty) * StreakMultiplierNow;
            AddCoins(coins);
            if (StreakShown(level) && !StreakOnFire)
            {
                int wins = StreakWins + 1;
                _store.SetInt("eco_streak_wins", Math.Min(wins, StreakWinsToFire));
                if (wins >= StreakWinsToFire) _store.SetInt("eco_streak_fire", 1);
            }
            int inARow = _store.GetInt("eco_wins_in_row", 0) + 1;
            _store.SetInt("eco_wins_in_row", inARow);
            Goal?.Invoke(MissionGoal.CompleteAnyLevel, 1);
            if (difficulty >= 4) Goal?.Invoke(MissionGoal.CompleteSuperHardLevel, 1);
            else if (difficulty >= 2) Goal?.Invoke(MissionGoal.CompleteHardLevel, 1);
            Goal?.Invoke(MissionGoal.CompleteLevelInStreak, inARow);
            Changed?.Invoke();
            return coins;
        }

        /// <summary>Cheat / test: the streak on fire (x2) or back to 0.</summary>
        public void SetStreak(bool onFire)
        {
            _store.SetInt("eco_streak_fire", onFire ? 1 : 0);
            _store.SetInt("eco_streak_wins", onFire ? StreakWinsToFire : 0);
            Changed?.Invoke();
        }

        /// <summary>A level lost or left: a life, and the streak if it was on fire.</summary>
        public void Lose()
        {
            LevelInProgress = false;
            LoseLife();
            _store.SetInt("eco_wins_in_row", 0);
            Goal?.Invoke(MissionGoal.CompleteLevelInStreak, 0);
            if (StreakOnFire)
            {
                _store.SetInt("eco_streak_fire", 0);
                _store.SetInt("eco_streak_wins", 0);
            }
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ boosters

        /// <summary>Stock keys, in bar order: shovel, boomerang, undo, magnet.</summary>
        public static readonly string[] BoosterKeys = { "booster_shovel", "booster_boomerang", "booster_undo", "booster_magnet" };
        public const int StartBoosterStock = 0; // none until a booster unlocks (it gives 3) or is bought; the 99 test stock is over [user]

        public int Boosters(string key) => _store.GetInt(key, StartBoosterStock);

        /// <summary>Sets the stock; a lower count is boosters used (missions), unless <paramref name="countUse"/> is false (cheats).</summary>
        public void SetBoosters(string key, int count, bool countUse = true)
        {
            int used = Boosters(key) - Math.Max(0, count);
            _store.SetInt(key, Math.Max(0, count));
            Changed?.Invoke();
            if (used > 0 && countUse) ReportBoosterUse(key, used);
        }

        public void AddBoosters(string key, int count) => SetBoosters(key, Boosters(key) + count);

        private void ReportBoosterUse(string key, int used)
        {
            Goal?.Invoke(MissionGoal.SpendAnyBooster, used);
            switch (key)
            {
                case "booster_shovel": Goal?.Invoke(MissionGoal.SpendShovel, used); break;
                case "booster_boomerang": Goal?.Invoke(MissionGoal.SpendBoomerang, used); break;
                case "booster_undo": Goal?.Invoke(MissionGoal.SpendUndo, used); break;
                case "booster_magnet": Goal?.Invoke(MissionGoal.SpendMagnet, used); break;
            }
        }
    }
}
