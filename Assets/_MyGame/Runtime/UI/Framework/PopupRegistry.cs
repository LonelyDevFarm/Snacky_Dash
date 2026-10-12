using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Danh sách prefab popup mà <see cref="SnackDash.Core.UIManager"/> mở được (file <c>Data/UI/PopupRegistry</c>).
    /// Thêm popup mới: kéo prefab của nó vào danh sách. Mở theo loại script, không theo tên.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Popup Registry", fileName = "PopupRegistry")]
    public class PopupRegistry : ScriptableObject
    {
        [SerializeField] private List<BasePopup> popups = new List<BasePopup>();

        public T Get<T>() where T : BasePopup
        {
            foreach (var popup in popups)
                if (popup != null && popup.GetType() == typeof(T)) return (T)popup; // đúng loại (lớp con có mục riêng, vd. ExtraCratePopup)
            return null;
        }
    }
}
