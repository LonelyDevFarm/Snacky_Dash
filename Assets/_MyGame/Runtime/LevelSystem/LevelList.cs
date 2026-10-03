using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>Levels in play order (cohort ab-vo-co), written by SnackDash/Restore/Import Level Data.</summary>
    public class LevelList : ScriptableObject
    {
        public List<LevelDefinition> levels = new List<LevelDefinition>();

        [Tooltip("Levels kept in the list (so numbering matches the original) but skipped in play for now, " +
                 "e.g. booster tutorials until boosters exist.")]
        public List<LevelDefinition> skipped = new List<LevelDefinition>();

        /// <summary>First playable index at or after <paramref name="index"/> (wrapping), or -1.</summary>
        public int Playable(int index)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                int candidate = (index + i) % levels.Count;
                if (levels[candidate] != null && !skipped.Contains(levels[candidate])) return candidate;
            }
            return -1;
        }
    }
}
