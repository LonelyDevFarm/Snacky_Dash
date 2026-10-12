using System;
using System.Collections;
using SnackDash.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Hoạt ảnh hiện / ẩn của một popup: nền tối mờ dần, khung bật ra (nảy) và thu lại khi đóng. Gắn trên gốc prefab popup,
    /// kéo nền tối vào Overlay và khung vào Panel; chỉnh thời gian / độ nảy ngay trên Inspector. Chạy cả khi game tạm dừng.
    /// </summary>
    public class PopupAnimator : MonoBehaviour
    {
        [Tooltip("Nền tối phủ màn hình (độ đậm lấy theo màu đang đặt).")]
        [SerializeField] private Graphic overlay;
        [Tooltip("Khung popup bật ra / thu lại.")]
        [SerializeField] private RectTransform panel;
        [Header("Hiện")]
        [SerializeField, Min(0f)] private float openTime = 0.35f;
        [Tooltip("Độ nảy khi bật ra (OutBack).")]
        [SerializeField, Min(0f)] private float openBack = 1.6f;
        [Tooltip("Khung bật từ cỡ này (0 = từ không; 0.9 = từ 90 %).")]
        [SerializeField, Range(0f, 1f)] private float openFrom = 0f;
        [Tooltip("Nền tối đậm hết sau bao nhiêu phần thời gian (0.5 = nửa đầu).")]
        [SerializeField, Range(0.05f, 1f)] private float overlayShare = 0.5f;
        [Tooltip("Nếu đặt: khung hiện dần cùng lúc bật ra (đậm hết sau nửa đầu).")]
        [SerializeField] private CanvasGroup panelFade;
        [Header("Ẩn")]
        [SerializeField, Min(0f)] private float closeTime = 0.22f;
        [SerializeField, Min(0f)] private float closeBack = 1.5f;
        [Tooltip("Nếu đặt: khi đóng cả popup mờ dần (popup nền đen kiểu \"Tap to continue\"), thay cho khung thu lại.")]
        [SerializeField] private CanvasGroup fadeGroup;

        private float _overlayAlpha = -1f;
        private Vector3 _panelScale;

        private void Remember()
        {
            if (_overlayAlpha >= 0f) return;
            _overlayAlpha = overlay != null ? overlay.color.a : 0f;
            _panelScale = panel != null ? panel.localScale : Vector3.one;
        }

        public IEnumerator Open()
        {
            Remember();
            Action<float> step = t =>
            {
                SetOverlay(_overlayAlpha * Mathf.Clamp01(t / overlayShare));
                if (panel != null) panel.localScale = _panelScale * Mathf.LerpUnclamped(openFrom, 1f, Tween.OutBack(openBack)(t));
                if (panelFade != null) panelFade.alpha = Mathf.Clamp01(t * 2f);
            };
            step(0f);
            yield return Tween.Run(openTime, Tween.Linear, step, true);
        }

        public IEnumerator Close()
        {
            Remember();
            if (fadeGroup != null)
            {
                yield return Tween.Run(closeTime, Tween.Linear, t => fadeGroup.alpha = 1f - t, true);
                yield break;
            }
            yield return Tween.Run(closeTime, Tween.Linear, t =>
            {
                if (panel != null) panel.localScale = _panelScale * (1f - Tween.InBack(closeBack)(t));
                SetOverlay(_overlayAlpha * (1f - t));
            }, true);
        }

        private void SetOverlay(float alpha)
        {
            if (overlay == null) return;
            var c = overlay.color;
            overlay.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
