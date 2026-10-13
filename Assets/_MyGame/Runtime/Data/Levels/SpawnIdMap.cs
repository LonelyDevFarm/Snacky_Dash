using System.Collections.Generic;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// Đổi id dấu rải trái 371..377 trên lớp spawn1..N ra loại trái; GameSession gọi khi rải.
    /// Id gán theo thứ tự loại trái xuất hiện lần đầu trong các đợt (gốc: LevelSpawnIdMap; khớp số dấu Level 2).
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
