using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Fruit lying on the board, one PillMaster_VO per pill cell. New pills jump in from above
    /// [PillView: _respawnSpawnZ -3, _respawnJumpHeight 2, _respawnJumpDuration 0.2].
    /// </summary>
    public class PillsView : MonoBehaviour
    {
        [SerializeField] private GameObject pillPrefab;
        [SerializeField] private float spawnZ = -3f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float jumpDuration = 0.2f;

        private readonly Dictionary<Vector2Int, GameObject> _pills = new Dictionary<Vector2Int, GameObject>();

        public void Bind(GameSession session)
        {
            session.PillSpawned += OnSpawned;
            session.PillEaten += (cell, kind, segment) => Remove(cell);
        }

        private void OnSpawned(Vector2Int cell, PillKind kind)
        {
            Remove(cell);
            var pill = Instantiate(pillPrefab, transform);
            pill.name = $"Pill {kind} {cell}";
            PillVisual.SetKind(pill, kind);
            _pills[cell] = pill;

            var landing = BoardView.CellToWorld(cell);
            pill.transform.localPosition = landing + new Vector3(0f, 0f, spawnZ);
            StartCoroutine(Tween.Run(jumpDuration, Tween.Linear, t =>
            {
                if (pill == null) return;
                float arc = 4f * t * (1f - t) * jumpHeight; // up and down on the way in
                pill.transform.localPosition = landing + new Vector3(0f, 0f, spawnZ * (1f - t) - arc);
            }));
        }

        private void Remove(Vector2Int cell)
        {
            if (!_pills.TryGetValue(cell, out var pill)) return;
            Destroy(pill);
            _pills.Remove(cell);
        }
    }
}
