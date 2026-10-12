using SnackDash.Bootstrap;
using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Màn chơi: danh sách màn (kéo file <c>Data/Levels/LevelList</c> vào ô Levels), màn đang tới (lưu qua
    /// <see cref="LevelProgress"/>), nhảy tới màn khác. Chuột phải vào component để về Level 1 khi test.
    /// </summary>
    public class LevelManager : ManagerBase
    {
        [Tooltip("Danh sách màn theo thứ tự (Data/Levels/LevelList).")]
        [SerializeField] private LevelList levels;

        public LevelList Levels => levels;

        /// <summary>Số thứ tự màn đang tới, tính từ 1.</summary>
        public int CurrentNumber => LevelProgress.Index + 1;

        public int Count => levels != null ? levels.levels.Count : 0;

        public LevelDefinition Current => levels != null ? LevelProgress.Current(levels) : null;

        public override void Init() => Services.Register(this);

        /// <summary>Đặt màn đang tới (số từ 1).</summary>
        public void SetLevel(int number) => LevelProgress.SetIndex(number - 1);

        [ContextMenu("Về Level 1")]
        private void BackToFirst() => SetLevel(1);
    }
}
