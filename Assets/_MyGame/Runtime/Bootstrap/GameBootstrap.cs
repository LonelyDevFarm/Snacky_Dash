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

        [Tooltip("Seconds after a stop during which a finger still held down cannot start a new move (a fresh touch can).")]
        [SerializeField] private float heldSwipeGraceAfterStop = 0.15f;

        public GameSession Session { get; private set; }
        public LevelDefinition Level { get; private set; }

        /// <summary>1-based level number in the list (what the HUD shows as "Level N").</summary>
        public int LevelNumber => levels != null ? levels.levels.IndexOf(Level) + 1 : 0;

        /// <summary>Coins; persistence and earning arrive with the coin economy (Snip is free for now).</summary>
        public Wallet Wallet { get; } = new Wallet();

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
            pills.Bind(Session);
            crates.Bind(Session);
            if (gameAudio != null) gameAudio.Bind(Session, crates);
            if (loseFlow != null) loseFlow.Bind(Session, Wallet, RestartLevel, cameraFitter);
            if (hud != null) hud.Bind(Session, LevelNumber);
            input.Swiped += (direction, fingerHeld) => Session.RequestMove(direction, fingerHeld);
            Session.Won += OnWon;

            // The original frames the crates together with the board (see FINDINGS: camera bounds).
            var bounds = board.BoardBounds;
            foreach (var anchor in graph.CrateAnchors.Values)
                bounds.Encapsulate(new Bounds(CratesView.CratePosition(anchor), CratesView.CrateSize));
            cameraFitter.Fit(bounds);

            Session.Begin();
        }

        private void OnWon()
        {
            if (Level == testLevel) LevelProgress.TestLevelDone = true;
            LevelProgress.Advance(levels, Level);
            if (victory != null) victory.Show(RestartLevel);
            else RestartLevel();
        }

        private void Update() => Session?.Tick(Time.deltaTime);
    }
}
