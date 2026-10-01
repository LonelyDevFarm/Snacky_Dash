using System.Collections.Generic;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Spawns the tile cover prefab of every cell. The board lies on the XY plane, one unit per cell,
    /// cell (x, y) centred at world (x, y, 0); covers are rotated around Z by the database rotation,
    /// matching the original Tilemap-based layout.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        // Layers whose tiles carry covers. "map" holds ground/path, "ldf" holds mechanics.
        private static readonly string[] CoverLayers = { "map", "ldf" };

        [SerializeField] private TileDefinitionDatabase database;
        [SerializeField] private Transform coverRoot;

        [Tooltip("Original: TilesCovers/VisualOverhaul/FullScreenBackground, loaded by GroundBGController.")]
        [SerializeField] private GameObject backgroundPrefab;

        // Original Game.unity "Ground_BG" transform, which carries GroundBGController; the background
        // prefab is spawned under it with its own local transform, giving a 160x160 plane at z = +1.3.
        private static readonly Quaternion GroundRotation = Quaternion.Euler(-90f, 0f, 0f);
        private static readonly Vector3 GroundScale = new Vector3(8f, 1f, 8f);
        private const float GroundDepth = 0.1f;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        public Bounds BoardBounds { get; private set; }

        public static Vector3 CellToWorld(Vector2Int cell) => new Vector3(cell.x, cell.y, 0f);

        public void Build(TiledMap map)
        {
            Clear();
            var root = coverRoot != null ? coverRoot : transform;
            var unknownIds = new HashSet<int>();

            foreach (var layer in CoverLayers)
            {
                if (!map.HasLayer(layer)) continue;
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int id = map.GetLocalTileId(layer, cell);
                    if (id < 0) continue;
                    if (!database.TryGet(id, out var definition))
                    {
                        unknownIds.Add(id);
                        continue;
                    }
                    if (definition.coverPrefab == null) continue;

                    var cover = Instantiate(definition.coverPrefab, root);
                    cover.name = $"{definition.name} ({x},{y})";
                    cover.transform.localPosition = CellToWorld(cell);
                    cover.transform.localRotation = Quaternion.Euler(0f, 0f, definition.coverZRotation);
                    _spawned.Add(cover);
                }
            }

            BoardBounds = ComputePlayableBounds(map);

            if (backgroundPrefab != null)
            {
                var ground = new GameObject("GroundBG").transform;
                ground.SetParent(root, false);
                ground.localPosition = new Vector3(BoardBounds.center.x, BoardBounds.center.y, GroundDepth);
                ground.localRotation = GroundRotation;
                ground.localScale = GroundScale;
                Instantiate(backgroundPrefab, ground, false);
                _spawned.Add(ground.gameObject);
            }

            // Local id 342 (raw GID 343) is plain grass with no entry in the original database either.
            unknownIds.Remove(342);
            if (unknownIds.Count > 0)
                Debug.LogWarning($"[BoardView] Tile ids without database entry: {string.Join(", ", unknownIds)}", this);
        }

        /// <summary>
        /// Bounds spanning the centres of the non-grass cells (paths, delivery points, mechanics).
        /// Calibrated against Level 2 footage (ortho size ~16.3): the original frames centre-to-centre
        /// path bounds (12 for 13 columns) plus the fitter's 3-cell expansion, not the full map.
        /// </summary>
        private Bounds ComputePlayableBounds(TiledMap map)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                int id = map.GetLocalTileId("map", new Vector2Int(x, y));
                // Road corners are category Grass but carry a corner TileType, so only pure grass is skipped.
                if (id < 0 || !database.TryGet(id, out var definition)) continue;
                if (definition.category == TileCategory.Grass && definition.tileType == TileType.Empty) continue;
                min = Vector2.Min(min, new Vector2(x, y));
                max = Vector2.Max(max, new Vector2(x, y));
            }
            if (min.x > max.x)
                return new Bounds(new Vector3((map.Width - 1) * 0.5f, (map.Height - 1) * 0.5f, 0f), new Vector3(map.Width, map.Height, 0f));
            var center = (min + max) * 0.5f;
            var size = max - min;
            return new Bounds(new Vector3(center.x, center.y, 0f), new Vector3(size.x, size.y, 0f));
        }

        public void Clear()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            _spawned.Clear();
        }
    }
}
