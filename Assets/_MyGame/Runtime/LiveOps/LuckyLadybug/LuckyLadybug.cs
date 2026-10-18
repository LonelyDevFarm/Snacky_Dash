using System;

namespace SnackDash.Economy
{
    /// <summary>
    /// LUCKY LADYBUG (sự kiện sáng tạo, mở ở Level 30; Docs/KE_HOACH_LUCKY_LADYBUG.md): mỗi vòng 48 giờ, thu 5 con bọ rùa để mở rương.
    /// Màn có bọ: bọ đậu lên một khay ở đợt trái thứ nhất, chạm để bắt; chỉ **thắng màn** mới giữ được bọ (thua / thoát thì mất).
    /// Thắng màn có bắt bọ thì màn thắng kế tiếp không có bọ, màn sau đó lại có; không bắt thì màn sau vẫn có. Lượt thua không
    /// đổi gì (chơi lại vẫn có bọ). Màn đầu của vòng luôn có bọ. Hết giờ chưa đủ thì mất hết; đủ thì nhận rương; xong vòng thì
    /// vòng mới mở ngay. Luật ở đây; con bọ trong màn (LadybugView) và Home chỉ hỏi / báo.
    /// </summary>
    public sealed class LuckyLadybug
    {
        // Số liệu ở Data/Config/LuckyLadybugConfig.
        private static LuckyLadybugConfig Config => GameConfig.Current.luckyLadybug;
        public static int UnlockLevel => Config.unlockLevel;
        public static int RoundHours => Config.roundHours;
        public static int Target => Config.ladybugsToCollect;

        private static LuckyLadybug _shared;
        public static LuckyLadybug Shared => _shared ?? (_shared = new LuckyLadybug(new PlayerPrefsStore(), () => GameClock.UtcNow, PlayerEconomy.Shared));

        private readonly IStore _store;
        private readonly Func<DateTime> _now;
        private readonly PlayerEconomy _economy;
        private bool _caught; // bắt trong màn đang chơi, chưa thắng nên chưa lưu

        public event Action Changed;

        public LuckyLadybug(IStore store, Func<DateTime> now, PlayerEconomy economy)
        {
            _store = store;
            _now = now;
            _economy = economy;
            economy.Goal += Record;
        }

        // ------------------------------------------------------------------ vòng

        private long Ticks(string key) => long.TryParse(_store.GetString(key, "0"), out long t) ? t : 0L;
        public bool Started => Ticks("lb_round") > 0;
        public DateTime RoundEnd => new DateTime(Ticks("lb_round")).AddHours(RoundHours);
        public TimeSpan Left { get { var left = RoundEnd - _now(); return left > TimeSpan.Zero ? left : TimeSpan.Zero; } }

        /// <summary>Bọ đã thu trong vòng.</summary>
        public int Count => _store.GetInt("lb_count", 0);
        /// <summary>Đủ bọ, rương chờ nhận (kể cả khi đã hết giờ).</summary>
        public bool Complete => Started && Count >= Target;
        public bool Claimed => _store.GetInt("lb_claimed", 0) != 0;
        /// <summary>Đang thu bọ: vòng còn giờ và chưa đủ.</summary>
        public bool Active => Started && !Complete && _now() < RoundEnd;

        /// <summary>Từ level mở: vòng đầu; và vòng mới khi vòng trước xong (rương đã nhận, hoặc hết giờ mà chưa đủ).</summary>
        public void StartIfUnlocked(int level)
        {
            if (level < UnlockLevel) return;
            bool over = !Started || (Complete ? Claimed : _now() >= RoundEnd);
            if (over) StartRound();
        }

        private void StartRound()
        {
            _store.SetString("lb_round", _now().Ticks.ToString());
            _store.SetInt("lb_count", 0);
            _store.SetInt("lb_seen", 0);
            _store.SetInt("lb_claimed", 0);
            _store.SetInt("lb_skip", 0); // màn đầu của vòng luôn có bọ
            _caught = false;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ trong màn

        /// <summary>Màn sắp chơi / đang chơi có bọ (nút Play gắn huy hiệu, bọ đậu lên khay ở đợt trái thứ nhất).</summary>
        public bool LadybugThisLevel => Active && _store.GetInt("lb_skip", 0) == 0;

        /// <summary>Đã bắt bọ trong màn này (chưa lưu tới khi thắng).</summary>
        public bool CaughtThisLevel => _caught;

        /// <summary>Chạm trúng bọ: giữ tạm tới khi thắng màn.</summary>
        public bool Catch()
        {
            if (!LadybugThisLevel || _caught) return false;
            _caught = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Thắng: bọ vừa bắt được lưu, màn kế không có bọ; không bắt thì màn kế vẫn có. Thua: mất bọ vừa bắt, không đổi gì khác.</summary>
        public void Record(MissionGoal goal, int amount)
        {
            if (goal == MissionGoal.CompleteLevelInStreak && amount == 0) { _caught = false; return; }
            if (goal != MissionGoal.CompleteAnyLevel || !Active) return;
            bool hadLadybug = _store.GetInt("lb_skip", 0) == 0;
            if (hadLadybug && _caught)
            {
                _store.SetInt("lb_count", Count + 1);
                _store.SetInt("lb_skip", 1);
            }
            else if (!hadLadybug) _store.SetInt("lb_skip", 0);
            _caught = false;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Home

        /// <summary>Bọ Home đã cho bay vào widget; ít hơn <see cref="Count"/> là có bọ mới cần bay.</summary>
        public int SeenCount { get => _store.GetInt("lb_seen", 0); set => _store.SetInt("lb_seen", value); }

        public bool HasNewLadybug => Started && SeenCount < Count;

        /// <summary>Rương: xu, mỗi loại booster, tim vô hạn; một lần; rồi vòng mới mở ngay.</summary>
        public bool Claim()
        {
            if (!Complete || Claimed) return false;
            _store.SetInt("lb_claimed", 1);
            _economy.AddCoins(Config.rewardCoins);
            foreach (var key in PlayerEconomy.BoosterKeys) _economy.AddBoosters(key, Config.rewardEachBooster);
            if (Config.rewardInfiniteMinutes > 0) _economy.AddInfiniteLives(TimeSpan.FromMinutes(Config.rewardInfiniteMinutes));
            StartRound();
            return true;
        }

        public static int RewardCoins => Config.rewardCoins;
        public static int RewardEachBooster => Config.rewardEachBooster;
        public static int RewardInfiniteMinutes => Config.rewardInfiniteMinutes;
        public static float HandSeconds => Config.handSeconds;

        /// <summary>Cheat / test: thêm bọ ngay (như đã bắt và thắng).</summary>
        public void CheatAdd(int count)
        {
            if (!Started) return;
            _store.SetInt("lb_count", Math.Min(Target, Count + count));
            Changed?.Invoke();
        }

        /// <summary>Cheat / test: hết giờ vòng ngay.</summary>
        public void CheatEndRound()
        {
            _store.SetString("lb_round", _now().AddHours(-RoundHours).AddSeconds(-1).Ticks.ToString());
            Changed?.Invoke();
        }
    }
}
