using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một hàng nhiệm vụ của WEEKLY ADVENTURE (prefab <c>Prefabs/UI/Popups/WeeklyMissions/WeeklyMissionRow</c>, gốc: Mission / MissionPremium).
    /// Có sẵn cả 3 dáng (đang làm, nhận được, đã nhận), <see cref="Show"/> bật đúng phần; hàng mua hàng (premium) đổi sang màu vàng.
    /// </summary>
    public class WeeklyMissionRow : MonoBehaviour
    {
        [Serializable]
        public struct RewardItem
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text amount;
        }

        [SerializeField] private Image paper;
        [SerializeField] private Image claimBackground;
        [SerializeField] private TMP_Text claimLabel;
        [SerializeField] private TMP_Text claimMission;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject defaultLayout;
        [SerializeField] private TMP_Text title;
        [SerializeField] private Image bar;
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject claimedArea;
        [SerializeField] private TMP_Text claimedText;
        [Tooltip("Dấu tích xanh của hàng đã nhận (bật ra sau hàng).")]
        [SerializeField] private Transform tick;
        [SerializeField] private RectTransform rewards;
        [SerializeField] private RewardItem[] items = new RewardItem[2];
        [SerializeField] private GameObject lockIcon;
        [Tooltip("Nút và hiệu ứng nhấn: chỉ bật ở hàng nhận được.")]
        [SerializeField] private Button button;
        [SerializeField] private PressFeedback press;

        [Header("Hàng mua hàng (MissionPremium)")]
        [SerializeField] private Sprite premiumPaper;
        [SerializeField] private Sprite premiumClaim;
        [SerializeField] private Material premiumClaimMaterial;
        [SerializeField] private Color premiumBrown = new Color(0.675f, 0.424f, 0.141f);
        [SerializeField] private Color premiumBar = new Color(0.761f, 0.596f, 0.278f);

        /// <summary>Chạm hàng nhận được.</summary>
        public event Action Clicked;

        /// <summary>Dấu tích (đang hiện) của hàng đã nhận, hoặc null.</summary>
        public Transform Tick => tick != null && tick.gameObject.activeInHierarchy ? tick : null;

        /// <summary>Ô phần thưởng thứ <paramref name="index"/> nếu đang hiện, hoặc null.</summary>
        public RectTransform Item(int index) => index < items.Length && items[index].root.gameObject.activeSelf ? items[index].root : null;

        private void Awake() => button.onClick.AddListener(() => Clicked?.Invoke());

        public void Show(string text, Sprite missionIcon, bool premium, bool claimable, bool claimed, bool open, int progress, int amount, List<(Sprite icon, string amount)> rewardList)
        {
            if (premium)
            {
                paper.sprite = premiumPaper;
                paper.color = Color.white;
                claimBackground.sprite = premiumClaim;
                claimLabel.fontSharedMaterial = premiumClaimMaterial;
                claimMission.color = premiumBrown;
                title.color = premiumBrown;
                bar.color = premiumBar;
                claimedText.color = premiumBrown;
            }

            claimBackground.gameObject.SetActive(claimable);
            if (claimable) UiShiny.Row(claimBackground); // ánh sáng quét qua (UIShiny của prefab Mission)
            claimLabel.gameObject.SetActive(claimable);
            claimMission.gameObject.SetActive(claimable);
            claimMission.text = text;
            icon.gameObject.SetActive(!claimed);
            icon.sprite = missionIcon;
            defaultLayout.SetActive(!claimable && !claimed);
            title.text = text;
            fill.anchorMax = new Vector2(amount > 0 ? Mathf.Clamp01(progress / (float)amount) : 0f, 1f);
            fill.gameObject.SetActive(progress > 0);
            progressText.text = $"{progress}/{amount}";
            claimedArea.SetActive(claimed);
            claimedText.text = text;
            lockIcon.SetActive(!open);

            if (claimable) rewards.SetAsLastSibling(); // nằm trên nền xanh
            if (rewardList.Count == 1) items[0].root.anchoredPosition = (items[0].root.anchoredPosition + items[1].root.anchoredPosition) * 0.5f; // một món thì ở giữa
            for (int i = 0; i < items.Length; i++)
            {
                items[i].root.gameObject.SetActive(i < rewardList.Count);
                if (i >= rewardList.Count) continue;
                items[i].icon.sprite = rewardList[i].icon;
                items[i].amount.text = rewardList[i].amount;
            }
            if (claimed) rewards.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
            button.enabled = claimable;
            press.enabled = claimable;
        }
    }
}
