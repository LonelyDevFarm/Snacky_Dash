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
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private TileDefinitionDatabase database;

        [Header("Scene references")]
        [SerializeField] private BoardView board;
        [SerializeField] private BoardCameraFitter cameraFitter;
        [SerializeField] private SnakeView snake;
        [SerializeField] private PillsView pills;
        [SerializeField] private CratesView crates;
        [SerializeField] private SwipeInput input;
        [SerializeField] private GameAudio gameAudio;

        public GameSession Session { get; private set; }

        private void Start()
        {
            var map = TiledMap.Parse(level.map.text);
            var graph = new BoardGraph(map, database);
            Session = new GameSession(graph, map, level);

            board.Build(map);
            snake.Bind(Session);
            pills.Bind(Session);
            crates.Bind(Session);
            if (gameAudio != null) gameAudio.Bind(Session, crates);
            input.Swiped += direction => Session.RequestMove(direction);
            Session.Won += () => Debug.Log($"[Game] {level.name} won");

            // The original frames the crates together with the board (see FINDINGS: camera bounds).
            var bounds = board.BoardBounds;
            foreach (var anchor in graph.CrateAnchors.Values)
                bounds.Encapsulate(new Bounds(CratesView.CratePosition(anchor), CratesView.CrateSize));
            cameraFitter.Fit(bounds);

            Session.Begin();
        }

        private void Update() => Session?.Tick(Time.deltaTime);
    }
}
