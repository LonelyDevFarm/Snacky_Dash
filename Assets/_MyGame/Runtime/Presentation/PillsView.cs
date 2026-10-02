using System.Collections;
using System.Collections.Generic;
using SnackDash.Gameplay;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Fruit lying on the board, one PillMaster_VO per pill cell. New pills jump in from above
    /// [PillView: _respawnSpawnZ -3, _respawnJumpHeight 2, _respawnJumpDuration 0.2].
    /// A whole wave does not pop in at once: measured on LV2-3 (wave 2, 15 fps) the first fruit
    /// lands ~0.2 s after the last one of the previous wave was eaten, and the rest follow over
    /// ~0.25 s, nearest to the snake first (RespawnScheduler.SpawnWaveFromPlayer sorts by distance).
    /// </summary>
    public class PillsView : MonoBehaviour
    {
        [SerializeField] private GameObject pillPrefab;
        [SerializeField] private float spawnZ = -3f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float jumpDuration = 0.2f;

        [Header("Wave (measured on LV2-3)")]
        [SerializeField] private float waveDelay = 0.2f;
        [SerializeField] private float waveSpread = 0.25f;

        private readonly Dictionary<Vector2Int, GameObject> _pills = new Dictionary<Vector2Int, GameObject>();
        private readonly List<(Vector2Int cell, PillKind kind)> _pending = new List<(Vector2Int, PillKind)>();
        private GameSession _session;

        public void Bind(GameSession session)
        {
            _session = session;
            session.PillSpawned += (cell, kind) => _pending.Add((cell, kind));
            session.PillEaten += (cell, kind, segment) => Remove(cell);
        }

        /// <summary>Pills spawned in the same frame form a wave; stagger them by distance to the head.</summary>
        private void LateUpdate()
        {
            if (_pending.Count == 0) return;
            bool wave = _pending.Count > 1;
            var head = _session.HeadPosition;
            float near = float.MaxValue, far = 0f;
            foreach (var (cell, _) in _pending)
            {
                float distance = Vector2.Distance(cell, head);
                near = Mathf.Min(near, distance);
                far = Mathf.Max(far, distance);
            }
            foreach (var (cell, kind) in _pending)
            {
                if (!_session.Pills.ContainsKey(cell)) continue; // eaten in the frame it spawned
                float delay = 0f;
                if (wave)
                {
                    float t = far > near ? (Vector2.Distance(cell, head) - near) / (far - near) : 0f;
                    delay = waveDelay + waveSpread * t;
                }
                Spawn(cell, kind, delay);
            }
            _pending.Clear();
        }

        private void Spawn(Vector2Int cell, PillKind kind, float delay)
        {
            Remove(cell);
            var pill = Instantiate(pillPrefab, transform);
            pill.name = $"Pill {kind} {cell}";
            PillVisual.SetKind(pill, kind);
            _pills[cell] = pill;
            pill.SetActive(delay <= 0f);
            StartCoroutine(JumpIn(pill, BoardView.CellToWorld(cell), delay));
        }

        private IEnumerator JumpIn(GameObject pill, Vector3 landing, float delay)
        {
            pill.transform.localPosition = landing + new Vector3(0f, 0f, spawnZ);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
                if (pill == null) yield break; // eaten before it showed up
                pill.SetActive(true);
            }
            yield return Tween.Run(jumpDuration, Tween.Linear, t =>
            {
                if (pill == null) return;
                float arc = 4f * t * (1f - t) * jumpHeight; // up and down on the way in
                pill.transform.localPosition = landing + new Vector3(0f, 0f, spawnZ * (1f - t) - arc);
            });
        }

        private void Remove(Vector2Int cell)
        {
            if (!_pills.TryGetValue(cell, out var pill)) return;
            Destroy(pill);
            _pills.Remove(cell);
        }
    }
}
