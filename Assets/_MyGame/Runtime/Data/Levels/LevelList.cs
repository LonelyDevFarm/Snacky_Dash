using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.LevelSystem
{
    /// <summary>
    /// Danh sách màn theo thứ tự chơi (cohort ab-vo-co), asset Data/Levels/LevelList.asset: 350 màn gốc
    /// (nhập một lần từ bản gốc; công cụ nhập đã gỡ 20/10), Level Generator thêm màn sinh ra. GameBootstrap, HomeView, LevelManager đọc qua LevelProgress.
    /// </summary>
    public class LevelList : ScriptableObject
    {
        public List<LevelDefinition> levels = new List<LevelDefinition>();

        [Tooltip("Màn giữ trong danh sách cho đúng số thứ tự gốc nhưng tạm bỏ qua khi chơi.")]
        public List<LevelDefinition> skipped = new List<LevelDefinition>();

        /// <summary>Chỉ số chơi được đầu tiên từ <paramref name="index"/> trở đi (vòng lại), hoặc -1.</summary>
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
