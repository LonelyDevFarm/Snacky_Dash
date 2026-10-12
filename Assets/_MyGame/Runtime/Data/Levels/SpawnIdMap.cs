using System.Collections.Generic;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// Maps the spawn-marker tile ids 371..377 on spawn1..N layers to fruit kinds.
    /// The original LevelSpawnIdMap is built from the level's RespawnSequence; matching the marker
    /// counts against the batches (e.g. Level 2: spawn1 = 371x9 + 372x9 = Blueberry 9 + Banana 9,
    /// spawn2 = 371x9 + 373x4 = Blueberry 9 + Strawberry 4) shows ids are assigned to kinds in order
    /// of first appearance in the sequence.
    /// </summary>
    public static class SpawnIdMap
    {
        public const int FirstTileId = 371;
        public const int LastTileId = 377;

        public static Dictionary<int, PillKind> Build(IReadOnlyList<SpawnBatchDefinition> batches)
        {
            var map = new Dictionary<int, PillKind>();
            var seen = new HashSet<PillKind>();
            foreach (var batch in batches)
            foreach (var pill in batch.pills)
            {
                if (!seen.Add(pill.kind)) continue;
                map[FirstTileId + map.Count] = pill.kind;
            }
            return map;
        }
    }
}
