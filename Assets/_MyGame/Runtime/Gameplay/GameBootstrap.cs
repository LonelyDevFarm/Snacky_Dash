using SnackDash.Audio;
using SnackDash.Gameplay;
using SnackDash.Input;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using UnityEngine;

namespace SnackDash.Bootstrap
{
    /// <summary>
    /// Điểm vào của scene Game (Scenes/Game.unity): dựng bàn, GameSession và các View cho một màn, nối input và thời gian.
    /// Màn lấy từ tiến độ đã lưu trong <see cref="levels"/>; <see cref="testLevel"/> (nếu có) được chơi trước để test.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelList levels;
        [Tooltip("Màn chơi trước để test; thắng xong thì tiếp màn kế trong danh sách.")]
        [UnityEngine.Serialization.FormerlySerializedAs("level")]
        [SerializeField] private LevelDefinition testLevel;
        [SerializeField] private TileDefinitionDatabase database;

        [Header("Scene references")]
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraFitter cameraFitter;
        [SerializeField] private SnakeView snake;
        [SerializeField] private PillsView pills;
        [SerializeField] private CratesView crates;
        [Tooltip("Rèm (từ Level 41): mảnh rèm, bộ đếm, mở rèm.")]
        [SerializeField] private CurtainsView curtains;
        [SerializeField] private SwipeInput input;
        [SerializeField] private GameAudio gameAudio;
        [SerializeField] private SnackDash.UI.LoseFlowView loseFlow;
        [SerializeField] private SnackDash.UI.VictoryView victory;
        [SerializeField] private SnackDash.UI.HudView hud;
        [SerializeField] private SnackDash.UI.BoosterBarView boosters;
        [SerializeField] private SnackDash.UI.TutorialView tutorial;
        [SerializeField] private SnackDash.UI.ComboView combo;
        [Tooltip("Prefab ExtraCrateBubble: bóng nói TAP TO UNLOCK của khay phụ.")]
        [SerializeField] private RectTransform extraCrateBubble;
        [Tooltip("Prefab GuidanceArrow (S_Hint_Arrow): mũi tên tím chỉ đường ở các màn đầu.")]
        [SerializeField] private SpriteRenderer guidanceArrow;

        [Tooltip("Giây sau khi dừng mà ngón đang giữ chưa được đi tiếp (chạm mới thì được).")]
        [SerializeField] private float heldSwipeGraceAfterStop = 0.15f;

        public GameSession Session { get; private set; }
        public LevelDefinition Level { get; private set; }

        /// <summary>Số màn tính từ 1 (HUD hiện "Level N").</summary>
        public int LevelNumber => levels != null ? levels.levels.IndexOf(Level) + 1 : 0;

        public void RestartLevel() =>
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex >= 0
                ? gameObject.scene.buildIndex : 0);

        private void Start()
        {
            Level = testLevel != null && !LevelProgress.TestLevelDone ? testLevel : LevelProgress.Current(levels);
            if (Level == null) { Debug.LogError("[Game] No level: assign the LevelList (or a test level)", this); return; }

            var map = TiledMap.Parse(Level.map.text);
            var graph = new BoardGraph(map, database);
            Session = new GameSession(graph, map, Level) { HeldSwipeGraceAfterStop = heldSwipeGraceAfterStop };

            board.Build(map);
            snake.Bind(Session);
            board.BindGates(Session);
            if (curtains != null) curtains.Bind(Session);
            pills.Bind(Session);
            crates.Bind(Session);
            if (gameAudio != null) gameAudio.Bind(Session, crates);
            if (loseFlow != null) loseFlow.Bind(Session, RestartLevel, cameraFitter);
            if (hud != null) hud.Bind(Session, LevelNumber);
            if (hud != null && hud.Ladybug != null) hud.Ladybug.Bind(Session); // sự kiện Lucky Ladybug, tách khỏi luật chơi
            if (boosters != null) boosters.Bind(Session, LevelNumber, board, cameraFitter, snake);
            if (tutorial != null) tutorial.Bind(Session, LevelNumber);
            if (combo != null) combo.Bind(Session, crates, hud);
            board.gameObject.AddComponent<GuidanceView>().Bind(Session, LevelNumber, guidanceArrow, () => tutorial != null && tutorial.HidesBoosterBar); // mũi tên tím sau 3 s đứng yên, Level 1-7
            var ghost = snake.GetComponent<SnakeGhostView>(); // booster Rắn bóng ma (sáng tạo): thân mờ, ánh tím
            if (ghost != null) ghost.Bind(Session, snake);
            var idle = snake.GetComponent<SnakeIdleView>(); // mắt liếc, bong bóng trái khi đứng yên lâu [user]
            if (idle != null) idle.Bind(Session, snake, () => tutorial != null && tutorial.HidesBoosterBar);
            if (hud != null) hud.gameObject.AddComponent<SnackDash.UI.ExtraCrateView>().Bind(Session, crates, extraCrateBubble); // khay phụ, nếu màn có
            if (tutorial != null && loseFlow != null) loseFlow.BeforeNoWayOut = tutorial.LoseTutorial;
            if (tutorial != null && loseFlow != null) loseFlow.ForcedSnip = () => tutorial.ForcedSnip;
            // Thanh booster ẩn khi hướng dẫn yêu cầu và khi rắn kẹt, để không che nút SNIP TAIL! [user].
            if (boosters != null) boosters.HideWhen = () => (Session != null && Session.IsStuck) || (tutorial != null && tutorial.HidesBoosterBar);
            input.Swiped += (direction, fingerHeld) => Session.RequestMove(direction, fingerHeld);
            Session.Won += OnWon;

            // Bản gốc đóng khung camera gồm cả bàn lẫn các khay.
            var bounds = board.BoardBounds;
            foreach (var anchor in graph.CrateAnchors.Values)
                bounds.Encapsulate(new Bounds(CratesView.CratePosition(anchor), CratesView.CrateSize));
            cameraFitter.Fit(bounds);

            Session.Begin();
            SnackDash.Economy.PlayerEconomy.Shared.LevelInProgress = true; // thoát app lúc này mất một mạng
            SnackDash.Core.EventBus.Publish(new SnackDash.Core.LevelStartedEvent(LevelNumber));
        }

        private void OnWon()
        {
            if (Level == testLevel) LevelProgress.TestLevelDone = true;
            LevelProgress.Advance(levels, Level);
            // Màn sau có cơ chế mới (không tính booster) thì màn thắng hiện UNLOCKED! [user].
            // Từ khi có Home (Level 6) mọi CONTINUE về Home; trước đó vào thẳng màn kế [user, 08/10].
            bool unlocks = SnackDash.UI.UnlockPopups.TryGet(LevelNumber + 1, false, out var unlocked);
            bool toHome = LevelNumber + 1 >= LevelProgress.HomeFromLevel;
            // Xu thắng (20 / 30 / 40, x2 khi chuỗi thắng đang cháy) và xu combo vào ví ngay; nút x2 trả thêm một lần tổng [user].
            var economy = SnackDash.Economy.PlayerEconomy.Shared;
            bool onFire = economy.StreakOnFire;
            int comboCoins = combo != null ? combo.LevelCoins : 0;
            int baseCoins = economy.Win(LevelNumber, Level.difficulty);
            economy.AddCoins(comboCoins);
            SnackDash.Core.EventBus.Publish(new SnackDash.Core.LevelWonEvent(LevelNumber));
            // Home cho xu bay vào bộ đếm, đếm lên từ tổng cũ (video sk2 7-9 s).
            if (toHome) SnackDash.UI.HomeTopBar.HoldWinCoins(baseCoins + comboCoins);
            var reward = new SnackDash.UI.VictoryReward
            {
                baseCoins = baseCoins, comboCoins = comboCoins, onFire = onFire, level = LevelNumber,
                doubled = () => { economy.AddCoins(baseCoins + comboCoins); if (toHome) SnackDash.UI.HomeTopBar.HoldWinCoins(baseCoins + comboCoins); economy.Report(SnackDash.Economy.MissionGoal.WatchRewardedVideo); }, // nút quảng cáo, ở đây miễn phí [user]
            };
            if (victory != null) victory.Show(toHome ? () => SnackDash.UI.LoadingScreen.Load(SnackDash.UI.LoadingScreen.HomeScene) : (System.Action)RestartLevel, crates != null ? crates.LastCompletionLag : 0f, unlocks ? unlocked : (SnackDash.UI.UnlockPopups.Entry?)null, reward);
            else RestartLevel();
        }

        private void Update() => Session?.Tick(Time.deltaTime);
    }
}
