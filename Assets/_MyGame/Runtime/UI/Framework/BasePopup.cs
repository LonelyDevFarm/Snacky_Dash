using System;
using System.Collections;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Lớp chung của mọi popup: mở bằng <c>Services.Get&lt;UIManager&gt;().Show&lt;TênPopup&gt;()</c>, có nút X (kéo khối nút
    /// vào ô Close Button), hoạt ảnh hiện / ẩn (<see cref="PopupAnimator"/> trên gốc prefab), đóng thì tự xoá và báo
    /// <see cref="Closed"/>. Popup riêng kế thừa lớp này và chỉ viết phần của mình (OnInit, OnShow).
    /// </summary>
    public abstract class BasePopup : MonoBehaviour
    {
        [Tooltip("Nút X (có thể trống nếu popup không có).")]
        [SerializeField] protected UiButton closeButton;
        [Tooltip("Hoạt ảnh hiện / ẩn (có thể trống: hiện / ẩn ngay).")]
        [SerializeField] protected PopupAnimator animator;

        /// <summary>Popup đã đóng hẳn (sau hoạt ảnh).</summary>
        public event Action Closed;

        public bool IsClosing { get; private set; }

        private bool _inited;

        /// <summary>Gọi bởi UIManager ngay sau khi tạo.</summary>
        public void Show()
        {
            if (!_inited)
            {
                _inited = true;
                if (closeButton != null) closeButton.Clicked += () => Close();
                OnInit();
            }
            OnShow();
            if (animator != null) StartCoroutine(animator.Open());
        }

        /// <summary>Đóng (có hoạt ảnh), rồi chạy <paramref name="then"/> và báo Closed.</summary>
        public void Close(Action then = null)
        {
            if (IsClosing) return;
            IsClosing = true;
            StartCoroutine(CloseRoutine(then));
        }

        private IEnumerator CloseRoutine(Action then)
        {
            if (animator != null) yield return animator.Close();
            OnHide();
            Destroy(gameObject);
            then?.Invoke();
            Closed?.Invoke();
        }

        /// <summary>Một lần, lúc tạo: gắn nút, chuẩn bị.</summary>
        protected virtual void OnInit() { }

        /// <summary>Mỗi lần hiện: vẽ dữ liệu.</summary>
        protected virtual void OnShow() { }

        /// <summary>Lúc đóng: huỷ đăng ký sự kiện.</summary>
        protected virtual void OnHide() { }
    }
}
