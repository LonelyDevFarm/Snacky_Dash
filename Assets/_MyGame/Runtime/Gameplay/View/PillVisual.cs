using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Bật đúng loại trái trên prefab PillMaster_VO (mỗi loại một con dưới Visual/FruitsParent, theo thứ tự PillKind).
    /// PillsView, SnakeView, CratesView gọi. Tên gốc: Raspberry = Strawberry, Lemon = Banana, Peach = Pear, Orange = Carrot.
    /// </summary>
    public static class PillVisual
    {
        private static readonly string[] ChildNames = { "Raspberry", "Blueberry", "Lemon", "Peach", "Apple", "Orange", "Eggplant" };

        public static void SetKind(GameObject pill, PillKind kind)
        {
            var fruits = pill.transform.Find("Visual/FruitsParent");
            if (fruits == null) return;
            for (int i = 0; i < ChildNames.Length; i++)
            {
                var child = fruits.Find(ChildNames[i]);
                if (child != null) child.gameObject.SetActive(i == (int)kind);
            }
        }

        /// <summary>Trái rời bàn (đang bay, trong crate) không có bóng, vì bóng sẽ kéo vệt theo sau.</summary>
        public static void HideShadow(GameObject pill)
        {
            var shadow = pill.transform.Find("Visual/FruitsParent/Shadow");
            if (shadow != null) shadow.gameObject.SetActive(false);
        }

        public static Transform Shown(GameObject pill, PillKind kind)
        {
            int i = (int)kind;
            return i < ChildNames.Length ? pill.transform.Find("Visual/FruitsParent/" + ChildNames[i]) : null;
        }
    }
}
