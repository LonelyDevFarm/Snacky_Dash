using System;

namespace SnackDash.Economy
{
    /// <summary>
    /// JUNGLE HEIST (tính năng LavaQuest của bản gốc, mở ở màn 35): tham gia rồi thắng 7 màn liên tiếp; bắt đầu với 100 người
    /// chơi, mỗi bước còn lại ít hơn; thua một màn là trượt thử thách. Người về đích chia giải 10 000 xu (chữ của
    /// Popup_Lava_Quest_Ftue / _Join). Chạy theo vòng: thắng hay trượt đều hiện "Next Jungle Heist will start in:" tới hết vòng
    /// (Popup_Lava_Quest_Ended_*), widget đếm ngược thời gian đó. Số người còn lại sau mỗi bước và độ dài vòng bản gốc lấy từ
    /// máy chủ (LavaQuestRunSnapshot.SurvivorsPerStep, CurrentCycleDuration): <see cref="SurvivorsPerStep"/> và
    /// <see cref="CycleHours"/> là số thay thế.
    /// </summary>
    public sealed class JungleHeist
    {
        // Số liệu ở Data/Config/JungleHeistConfig.
        private static JungleHeistConfig Config => GameConfig.Current.jungleHeist;
        public static int UnlockLevel => Config.unlockLevel;
        public static int Steps => Config.steps;
        public static int Participants => Config.participants;
        public static int GrandPrize => Config.grandPrize;

        /// <summary>Người còn lại sau 0..Steps chặng thắng. TẠM ĐẶT (đường cong của server không có trong file).</summary>
        public static int[] SurvivorsPerStep => Config.survivorsPerStep;

        /// <summary>Giờ một vòng. TẠM ĐẶT (giá trị của server).</summary>
        public static int CycleHours => Config.cycleHours;

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
