using System;

namespace SnackDash.Economy
{
    /// <summary>
    /// JUNGLE HEIST (the original LavaQuest feature, opens at Level 35): join, then win 7 levels in a row; you start
    /// with 100 players and fewer stay on after each step; one lost level fails the challenge. Those who finish share the
    /// grand prize of 10 000 coins (texts of Popup_Lava_Quest_Ftue / _Join). It runs in cycles: won or failed, "Next
    /// Jungle Heist will start in:" the end of the cycle (Popup_Lava_Quest_Ended_*), and the widget counts that time down.
    /// The survivors after each step and the cycle's length come from the original's server (LavaQuestRunSnapshot
    /// .SurvivorsPerStep, CurrentCycleDuration): <see cref="SurvivorsPerStep"/> and <see cref="CycleHours"/> are stand-ins.
    /// </summary>
    public sealed class JungleHeist
    {
        public const int UnlockLevel = 35, Steps = 7, Participants = 100, GrandPrize = 10000;

        /// <summary>Players still on after 0..7 wins. STAND-IN (the server's survivors curve is not in the files).</summary>
        public static readonly int[] SurvivorsPerStep = { 100, 82, 64, 48, 34, 22, 13, 7 };

        /// <summary>Length of a cycle. STAND-IN (server value).</summary>
        public const int CycleHours = 24;

        public enum State { NotJoined, Running, Won, Lost }

        private static JungleHeist _shared;
        public static JungleHeist Shared => _shared ?? (_shared = new JungleHeist(new PlayerPrefsStore(), () => GameClock.UtcNow, PlayerEconomy.Shared));

        private readonly IStore _store;
        private readonly Func<DateTime> _now;
        private readonly PlayerEconomy _economy;

        public event Action Changed;

        public JungleHeist(IStore store, Func<DateTime> now, PlayerEconomy economy)
        {
            _store = store;
            _now = now;
            _economy = economy;
            economy.Goal += Record;
        }

        // ------------------------------------------------------------------ cycles

        private long Ticks(string key) => long.TryParse(_store.GetString(key, "0"), out long t) ? t : 0L;
        public bool Started => Ticks("jh_cycle") > 0;
        private DateTime CycleStart => new DateTime(Ticks("jh_cycle"));
        public DateTime CycleEnd => CycleStart.AddHours(CycleHours);
        public TimeSpan Left { get { var left = CycleEnd - _now(); return left > TimeSpan.Zero ? left : TimeSpan.Zero; } }

        /// <summary>From Level 35 on: starts the first cycle, and the next one whenever a cycle is over (a fresh run).</summary>
        public void StartIfUnlocked(int level)
        {
            if (level < UnlockLevel) return;
            if (Started && _now() < CycleEnd) return;
            var start = Started ? CycleEnd : _now();
            while (start.AddHours(CycleHours) <= _now()) start = start.AddHours(CycleHours);
            _store.SetString("jh_cycle", start.Ticks.ToString());
            _store.SetInt("jh_state", (int)State.NotJoined);
            _store.SetInt("jh_step", 0);
            _store.SetInt("jh_seen_step", 0);
            _store.SetInt("jh_claimed", 0);
            _store.SetInt("jh_result_seen", 0);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ the run

        public State Current => (State)_store.GetInt("jh_state", 0);
        public int Step => _store.GetInt("jh_step", 0);
        public int Survivors => SurvivorsPerStep[Math.Min(Step, Steps)];

        /// <summary>The step the main view showed last (it animates from there to <see cref="Step"/>).</summary>
        public int SeenStep { get => _store.GetInt("jh_seen_step", 0); set => _store.SetInt("jh_seen_step", value); }

        /// <summary>Coins each finisher gets: the grand prize shared out.</summary>
        public static int Share => GrandPrize / Math.Max(1, SurvivorsPerStep[Steps]);

        public bool CanJoin => Started && _now() < CycleEnd && Current == State.NotJoined;

        public void Join()
        {
            if (!CanJoin) return;
            _store.SetInt("jh_step", 0);
            _store.SetInt("jh_seen_step", 0);
            _store.SetInt("jh_state", (int)State.Running);
            Changed?.Invoke();
        }

        /// <summary>A win moves one step on (the 7th completes it); a loss fails the run (CompleteLevelInStreak 0 = the row broke).</summary>
        public void Record(MissionGoal goal, int amount)
        {
            if (Current != State.Running) return;
            if (goal == MissionGoal.CompleteAnyLevel)
            {
                int step = Step + 1;
                _store.SetInt("jh_step", step);
                if (step >= Steps) _store.SetInt("jh_state", (int)State.Won);
                Changed?.Invoke();
            }
            else if (goal == MissionGoal.CompleteLevelInStreak && amount == 0)
            {
                _store.SetInt("jh_state", (int)State.Lost);
                Changed?.Invoke();
            }
        }

        public bool PrizeClaimed => _store.GetInt("jh_claimed", 0) != 0 && Current == State.Won;

        /// <summary>The end popup of this run has been shown (a failed run is shown once, then waits for the next cycle).</summary>
        public bool ResultSeen { get => _store.GetInt("jh_result_seen", 0) != 0; set => _store.SetInt("jh_result_seen", value ? 1 : 0); }

        /// <summary>Something for Home to open the river for (LavaQuestHomeEntryGate): wins not shown yet, the prize, or the failed run.</summary>
        public bool HasNews =>
            (Current == State.Running && SeenStep < Step) || (Current == State.Won && !PrizeClaimed) || (Current == State.Lost && !ResultSeen);

        /// <summary>The finished run's share goes to the wallet, once; the next run waits for the next cycle.</summary>
        public bool ClaimPrize()
        {
            if (Current != State.Won || _store.GetInt("jh_claimed", 0) != 0) return false;
            _store.SetInt("jh_claimed", 1);
            _economy.AddCoins(Share);
            Changed?.Invoke();
            return true;
        }
    }
}
