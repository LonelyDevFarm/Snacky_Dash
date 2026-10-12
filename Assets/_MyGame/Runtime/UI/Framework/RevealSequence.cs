using System;
using System.Collections;
using System.Collections.Generic;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Kiểu hiện "từng phần một" của các popup nền đen (luật chơi, Finding players, phần thưởng…): nền tối hiện dần, từng phần
    /// trong danh sách Parts bật ra (nảy) đúng thời điểm At, cuối cùng chữ "Tap to continue!" hiện. Sửa danh sách / thời điểm
    /// trên Inspector. Chạm sớm thì hiện hết ngay (<see cref="Skip"/>). Popup có thể thêm phần lúc chạy (<see cref="AddPart"/>).
    /// </summary>
    public class RevealSequence : MonoBehaviour
    {
        [Serializable]
        public struct Part
        {
            public Transform target;
            [Tooltip("Giây bắt đầu bật ra.")]
            public float at;
            [Tooltip("Thời gian bật của riêng phần này (0 = dùng Pop Time).")]
            public float time;
        }

        [Tooltip("Nền tối (hiện dần trong Cover Time giây).")]
        [SerializeField] private Graphic cover;
        [SerializeField, Min(0.01f)] private float coverTime = 0.2f;
        [Tooltip("Chữ \"Tap to continue!\" (hiện sau phần cuối).")]
        [SerializeField] private TMP_Text tap;
        [SerializeField] private List<Part> parts = new List<Part>();
        [Tooltip("Thời gian mỗi phần bật ra.")]
        [SerializeField, Min(0.01f)] private float popTime = 0.25f;
        [Tooltip("Độ nảy khi bật (OutBack).")]
        [SerializeField, Min(0f)] private float popBack = 1.7f;
        [Tooltip("Chữ Tap hiện sau phần cuối bao nhiêu giây.")]
        [SerializeField, Min(0f)] private float tapDelay = 0.3f;
        [Tooltip("Nếu ≥ 0: chữ Tap hiện đúng giây này (bỏ qua Tap Delay).")]
        [SerializeField] private float tapAt = -1f;

        /// <summary>Đã hiện hết (chạm lúc này là đóng).</summary>
        public bool Ready { get; private set; }

        /// <summary>Mỗi khung trong lúc hiện, với thời gian đã chạy (popup dùng để chạy số đếm…).</summary>
        public event Action<float> Tick;

        public void AddPart(Transform target, float at) => parts.Add(new Part { target = target, at = at });

        public float LastAt
        {
            get { float last = 0f; foreach (var p in parts) last = Mathf.Max(last, p.at + (p.time > 0f ? p.time : popTime) - popTime); return last; }
        }

        public void Skip() => Ready = true;

        public IEnumerator Play()
        {
            float coverAlpha = cover != null ? cover.color.a : 0f;
            var scales = new Vector3[parts.Count];
            for (int i = 0; i < parts.Count; i++) { scales[i] = parts[i].target.localScale; parts[i].target.localScale = Vector3.zero; }
            float tapAlpha = tap != null ? tap.alpha : 0f;
            if (tap != null) tap.alpha = 0f;
            float end = tapAt >= 0f ? tapAt : LastAt + tapDelay;
            float finish = Mathf.Max(end + 0.2f, LastAt + popTime);
            var curve = Tween.OutBack(popBack);
            for (float t = 0f; t < finish && !Ready; t += Time.unscaledDeltaTime)
            {
                SetCover(coverAlpha * Mathf.Clamp01(t / coverTime), coverAlpha);
                for (int i = 0; i < parts.Count; i++)
                    parts[i].target.localScale = scales[i] * curve(Mathf.Clamp01((t - parts[i].at) / (parts[i].time > 0f ? parts[i].time : popTime)));
                if (tap != null) tap.alpha = Mathf.Clamp01((t - end) / 0.2f) * tapAlpha;
                Tick?.Invoke(t);
                yield return null;
            }
            SetCover(coverAlpha, coverAlpha);
            for (int i = 0; i < parts.Count; i++) parts[i].target.localScale = scales[i];
            if (tap != null) tap.alpha = tapAlpha;
            Tick?.Invoke(float.MaxValue);
            Ready = true;
        }

        private void SetCover(float alpha, float full)
        {
            if (cover == null) return;
            var c = cover.color;
            cover.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}
