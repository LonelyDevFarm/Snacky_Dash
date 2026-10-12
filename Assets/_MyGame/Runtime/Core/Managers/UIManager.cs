using System.Collections.Generic;
using SnackDash.UI;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Mở popup: <c>Services.Get&lt;UIManager&gt;().Show&lt;JungleHeistJoin&gt;()</c>. Prefab lấy từ danh sách
    /// <c>Data/UI/PopupRegistry</c> (kéo vào ô Registry), hiện trong canvas riêng <c>PopupCanvas</c> (con của [Managers],
    /// nằm trên giao diện Home / màn chơi). Popup mở sau nằm trên popup mở trước.
    /// </summary>
    public class UIManager : ManagerBase
    {
        [Tooltip("Data/UI/PopupRegistry: danh sách prefab popup.")]
        [SerializeField] private PopupRegistry registry;
        [Tooltip("Canvas chứa popup (PopupCanvas, con của [Managers]).")]
        [SerializeField] private RectTransform popupRoot;

        private readonly List<BasePopup> _open = new List<BasePopup>();

        public override void Init()
        {
            Services.Register(this);
            // PopupCanvas đi theo [Managers] qua các scene: đổi scene thì bỏ hết popup đang mở (vd. BACK TO HOME từ Cài đặt).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => CloseAll();
        }

        /// <summary>Bỏ ngay mọi popup đang mở (không chạy hoạt ảnh, không gọi Closed).</summary>
        public void CloseAll()
        {
            foreach (var popup in _open) if (popup != null) Destroy(popup.gameObject);
            _open.Clear();
        }

        /// <summary>Tạo và hiện popup loại <typeparamref name="T"/>; trả về nó để gắn dữ liệu / sự kiện.</summary>
        public T Show<T>() where T : BasePopup
        {
            var prefab = registry != null ? registry.Get<T>() : null;
            if (prefab == null)
            {
                Debug.LogError($"[UI] PopupRegistry chưa có prefab cho {typeof(T).Name} — kéo prefab vào Data/UI/PopupRegistry.");
                return null;
            }
            return Show(prefab);
        }

        /// <summary>Prefab trong PopupRegistry (cho màn tự đặt chỗ, vd. tab Shop ở Home).</summary>
        public T Prefab<T>() where T : BasePopup => registry != null ? registry.Get<T>() : null;

        /// <summary>Mở một prefab cụ thể (popup có nhiều bản cùng script, vd. 3 lời giới thiệu sự kiện BadgeFtue).</summary>
        public T Show<T>(T prefab) where T : BasePopup
        {
            if (prefab == null) return null;
            var popup = Instantiate(prefab, popupRoot);
            popup.name = prefab.name;
            _open.Add(popup);
            popup.Closed += () => _open.Remove(popup);
            popup.Show();
            return popup;
        }

        /// <summary>Có popup loại <typeparamref name="T"/> đang mở không.</summary>
        public bool IsOpen<T>() where T : BasePopup
        {
            foreach (var popup in _open)
                if (popup is T && popup != null) return true;
            return false;
        }

        /// <summary>Có popup nào đang mở không.</summary>
        public bool AnyOpen
        {
            get
            {
                _open.RemoveAll(p => p == null);
                return _open.Count > 0;
            }
        }
    }
}
