using SnackDash.LevelSystem;
using UnityEngine;

namespace SnackDash.Presentation
{
    /// <summary>
    /// Shows one fruit kind on a PillMaster_VO instance. The prefab holds one child per kind under
    /// Visual/FruitsParent; the original PillColorShape components bind them in PillKind order
    /// (Raspberry = Strawberry, Lemon = Banana, Peach = Pear, Orange = Carrot).
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

        /// <summary>The shown child of <paramref name="kind"/>, or null.</summary>
        public static Transform Shown(GameObject pill, PillKind kind)
        {
            int i = (int)kind;
            return i < ChildNames.Length ? pill.transform.Find("Visual/FruitsParent/" + ChildNames[i]) : null;
        }
    }
}
