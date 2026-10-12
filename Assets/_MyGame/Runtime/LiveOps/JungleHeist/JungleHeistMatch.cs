using System;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// "Finding players on your level" ngay sau JOIN (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistMatch</c>, gốc: Popup_Lava_Quest_PurchaseOverlay):
    /// tiêu đề, rương Grand Prize, ô đếm người chơi chạy lên 100/100 trong lúc đám đông avatar hiện lần lượt, rồi "Tap to continue!".
    /// Mở bằng <c>UIManager.Show&lt;JungleHeistMatch&gt;()</c>, nghe <see cref="Continued"/> (sau khi đóng) để vào màn sông.
    /// </summary>
    public class JungleHeistMatch : BasePopup
    {
        [SerializeField] private Button tapArea;
        [SerializeField] private RevealSequence reveal;
        [Tooltip("Số trên ô đếm (\"n/100\").")]
        [SerializeField] private TMP_Text counter;
        [Tooltip("Đám đông avatar: từng con hiện lần lượt trong lúc đếm.")]
        [SerializeField] private Transform crowd;
        [Tooltip("Giây bắt đầu đếm và hiện đám đông.")]
        [SerializeField] private float crowdFrom = 0.7f;
        [Tooltip("Giây đếm từ 1 lên đủ người (STAND-IN: gốc dùng feedback không đọc được).")]
        [SerializeField, Min(0.1f)] private float countTime = 2.2f;

        /// <summary>Người chơi chạm để đi tiếp (sau khi popup đóng).</summary>
        public event Action Continued;

        private int _shown = -1;

        protected override void OnInit()
        {
            tapArea.onClick.AddListener(() => { if (!reveal.Ready) reveal.Skip(); else Close(() => Continued?.Invoke()); });
            float step = countTime / Mathf.Max(1, crowd.childCount);
            for (int i = 0; i < crowd.childCount; i++) reveal.AddPart(crowd.GetChild(i), crowdFrom + step * i);
            reveal.Tick += ShowCount;
        }

        protected override void OnShow() => StartCoroutine(reveal.Play());

        private void ShowCount(float t)
        {
            int total = JungleHeist.Participants;
            int found = Mathf.Clamp(Mathf.RoundToInt(total * Tween.OutQuad(Mathf.Clamp01((t - crowdFrom) / countTime))), 1, total);
            if (found == _shown) return;
            _shown = found;
            counter.text = $"{found}/{total}";
        }
    }
}
