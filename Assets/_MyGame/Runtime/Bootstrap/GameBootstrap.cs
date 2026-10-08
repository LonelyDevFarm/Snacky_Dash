using SnackDash.Audio;
using SnackDash.Gameplay;
using SnackDash.Input;
using SnackDash.LevelSystem;
using SnackDash.Presentation;
using UnityEngine;

namespace SnackDash.Bootstrap
{
    /// <summary>
    /// Scene entry point: builds the board, the session and the views for one level and drives the
    /// session from input and time. Everything is wired through serialized references.
    /// The level comes from the saved progress in <see cref="levels"/>; a level set in
    /// <see cref="testLevel"/> is played first instead (for testing), then play continues after it.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelList levels;
        [Tooltip("Play this level first (testing). After winning it, the next level of the list follows.")]
        [UnityEngine.Serialization.FormerlySerializedAs("level")]
        [SerializeField] private LevelDefinition testLevel;
        [SerializeField] private TileDefinitionDatabase database;

        [Header("Scene references")]
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraFitter cameraFitter;
        [SerializeField] private SnakeView snake;
        [SerializeField] private PillsView pills;
        [SerializeField] private CratesView crates;
        [SerializeField] private SwipeInput input;
        [SerializeField] private GameAudio gameAudio;
        [SerializeField] private SnackDash.UI.LoseFlowView loseFlow;
        [SerializeField] private SnackDash.UI.VictoryView victory;
        [SerializeField] private SnackDash.UI.HudView hud;
        [SerializeField] private SnackDash.UI.BoosterBarView boosters;
        [SerializeField] private SnackDash.UI.TutorialView tutorial;
        [SerializeField] private SnackDash.UI.ComboView combo;

        [Tooltip("Seconds after a stop during which a finger still held down cannot start a new move (a fresh touch can).")]
        [SerializeField] private float heldSwipeGraceAfterStop = 0.15f;

        public GameSession Session { get; private set; }
        public LevelDefinition Level { get; private set; }

        /// <summary>1-based level number in the list (what the HUD shows as "Level N").</summary>
        public int LevelNumber => levels != null ? levels.levels.IndexOf(Level) + 1 : 0;

        /// <summary>Coins; persistence and earning arrive with the coin economy (Snip is free for now).</summary>

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
            pills.Bind(Session);
            crates.Bind(Session);
            if (gameAudio != null) gameAudio.Bind(Session, crates);
            if (loseFlow != null) loseFlow.Bind(Session, RestartLevel, cameraFitter);
            if (hud != null) hud.Bind(Session, LevelNumber);
            if (boosters != null) boosters.Bind(Session, LevelNumber, board, cameraFitter);
            if (tutorial != null) tutorial.Bind(Session, LevelNumber);
            if (combo != null) combo.Bind(Session, crates, hud);
            if (hud != null) hud.gameObject.AddComponent<SnackDash.UI.ExtraCrateView>().Bind(Session, crates, hud.Skin); // the extra crate, where the level has one
            if (tutorial != null && loseFlow != null) loseFlow.BeforeNoWayOut = tutorial.LoseTutorial;
            if (tutorial != null && loseFlow != null) loseFlow.ForcedSnip = () => tutorial.ForcedSnip;
            // The bar hides under the tutorials that ask for it and whenever the snake is stuck (NO WAY OUT / GIVE UP), so
            // it never covers SNIP TAIL! [user].
            if (boosters != null) boosters.HideWhen = () => (Session != null && Session.IsStuck) || (tutorial != null && tutorial.HidesBoosterBar);
            input.Swiped += (direction, fingerHeld) => Session.RequestMove(direction, fingerHeld);
            Session.Won += OnWon;

            // The original frames the crates together with the board (see FINDINGS: camera bounds).
            var bounds = board.BoardBounds;
            foreach (var anchor in graph.CrateAnchors.Values)
                bounds.Encapsulate(new Bounds(CratesView.CratePosition(anchor), CratesView.CrateSize));
            cameraFitter.Fit(bounds);

            Session.Begin();
            SnackDash.Economy.PlayerEconomy.Shared.LevelInProgress = true; // closing the app now costs a life
        }

        private void OnWon()
        {
            if (Level == testLevel) LevelProgress.TestLevelDone = true;
            LevelProgress.Advance(levels, Level);
            // The next level brings a new mechanic (boosters do not count): the win screen shows it UNLOCKED! [user].
            // Once Home exists (Level 6) every CONTINUE goes Home; before that straight to the next level [user, 08/10].
            bool unlocks = SnackDash.UI.UnlockPopups.TryGet(LevelNumber + 1, false, out var unlocked);
            bool toHome = LevelNumber + 1 >= LevelProgress.HomeFromLevel;
            // Coins: the win (20 / 30 / 40, x2 on a streak on fire) and the combo coins go to the wallet now;
            // x2 on the win screen pays the total once more [user].
            var economy = SnackDash.Economy.PlayerEconomy.Shared;
            bool onFire = economy.StreakOnFire;
            int comboCoins = combo != null ? combo.LevelCoins : 0;
            int baseCoins = economy.Win(LevelNumber, Level.difficulty);
            economy.AddCoins(comboCoins);
            var reward = new SnackDash.UI.VictoryReward
            {
                baseCoins = baseCoins, comboCoins = comboCoins, onFire = onFire, level = LevelNumber,
                doubled = () => { economy.AddCoins(baseCoins + comboCoins); economy.Report(SnackDash.Economy.MissionGoal.WatchRewardedVideo); }, // the ad button, free here [user]
            };
            if (victory != null) victory.Show(toHome ? () => SnackDash.UI.LoadingScreen.Load(SnackDash.UI.LoadingScreen.HomeScene) : (System.Action)RestartLevel, crates != null ? crates.LastCompletionLag : 0f, unlocks ? unlocked : (SnackDash.UI.UnlockPopups.Entry?)null, reward);
            else RestartLevel();
        }

        private void Update() => Session?.Tick(Time.deltaTime);
    }
}
