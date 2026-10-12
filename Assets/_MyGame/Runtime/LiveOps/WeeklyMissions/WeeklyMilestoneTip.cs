using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Bóng nói khi chạm một mốc rương (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyMilestoneTip</c>, gốc: Clicked Area của mốc, sk1.1 33 s):
    /// số mũ cần trên viên thuốc nâu bên dưới, bóng nói bên trên với các phần thưởng xếp hàng; của giải lớn thì bóng mở sang trái.
    /// </summary>
    public class WeeklyMilestoneTip : MonoBehaviour
    {
        [SerializeField] private RectTransform pill;
        [SerializeField] private TMP_Text need;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private RectTransform pointer;
        [Tooltip("Mẫu một phần thưởng (ảnh + số), chép cho mỗi phần.")]
        [SerializeField] private RectTransform rewardTemplate;
        [Header("Kích thước (sk1.1 33-34 s)")]
        [SerializeField] private float pillBase = 52f;
        [SerializeField] private float pillPerDigit = 17f;
        [SerializeField] private float rewardWidth = 105f;
        [SerializeField] private float bubbleMargin = 30f;
        [Header("Giải lớn: bóng mở sang trái")]
        [SerializeField] private float bubbleX = 6f;
        [SerializeField] private float grandBubbleX = 40f;
        [SerializeField] private float grandPointerX = -37.9f;

        public void Show(int points, List<(Sprite icon, string amount)> rewards, bool grand)
        {
            string text = points.ToString();
            need.text = text;
            pill.sizeDelta = new Vector2(pillBase + text.Length * pillPerDigit, pill.sizeDelta.y);

            float width = rewards.Count * rewardWidth + bubbleMargin;
            bubble.pivot = new Vector2(grand ? 1f : 0.5f, 0f);
            bubble.sizeDelta = new Vector2(width, bubble.sizeDelta.y);
            bubble.anchoredPosition = new Vector2(grand ? grandBubbleX : bubbleX, bubble.anchoredPosition.y);
            pointer.anchorMin = pointer.anchorMax = new Vector2(grand ? 1f : 0.5f, 0f);
            pointer.anchoredPosition = new Vector2(grand ? grandPointerX : 0f, pointer.anchoredPosition.y);

            rewardTemplate.gameObject.SetActive(false);
            for (int r = 0; r < rewards.Count; r++)
            {
                var view = Instantiate(rewardTemplate, bubble);
                view.gameObject.SetActive(true);
                view.anchoredPosition = new Vector2(bubbleMargin * 0.5f + rewardWidth * 0.5f + r * rewardWidth, rewardTemplate.anchoredPosition.y);
                view.GetComponentInChildren<Image>().sprite = rewards[r].icon;
                view.GetComponentInChildren<TMP_Text>().text = rewards[r].amount.ToUpperInvariant();
            }
        }
    }
}
