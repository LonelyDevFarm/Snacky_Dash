using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Một chặng của Snacky Pass (prefab <c>Prefabs/UI/Popups/SnackyPass/SnackyPassRow</c>, gốc: BP_MilestoneUI, 1080 x 285): làn miễn phí
    /// tím và làn premium vàng, mỗi làn một thẻ (khung, hình, số, CLAIM, dấu tích, vầng sáng, sao nhấp nháy; làn premium có ổ
    /// khoá), thẻ số chặng, thanh vé "0/1" ở chặng kế tiếp. Màu làn theo trạng thái sửa trên Inspector.
    /// </summary>
    public class SnackyPassRow : MonoBehaviour
    {
        [Serializable]
        public class Lane
        {
            public UiGradient gradient;
            public RectTransform card;
            public Button frame;
            public Image icon;
            public TMP_Text amount;
            public GameObject claimed;
            public Transform tick;
            public GameObject claim;
            public Image claimButton;
            public GameObject glow;
            public GameObject sparkles;
            [Tooltip("Ổ khoá (chỉ làn premium).")]
            public RectTransform lockIcon;
            [Tooltip("Màu trên / dưới: đã mở, chặng kế, chưa tới.")]
            public Color[] open = new Color[2], next = new Color[2], locked = new Color[2];
        }

        [SerializeField] private Lane free = new Lane();
        [SerializeField] private Lane premium = new Lane();
        [SerializeField] private TMP_Text stageNumber;
        [SerializeField] private GameObject ticketBar;
        [SerializeField] private RectTransform ticketFill;
        [SerializeField] private TMP_Text ticketText;
        [Tooltip("Hình chặng thường / chặng to (rương, gói 8 tim).")]
        [SerializeField] private float iconSize = 150f, bigIconSize = 175f;
        [SerializeField] private float iconY = 12f, bigIconY = 2f;

        /// <summary>Chạm thẻ hoặc CLAIM của một làn (true = premium).</summary>
        public event Action<bool> CardClicked;

        public Lane LaneOf(bool isPremium) => isPremium ? premium : free;

        private void Awake()
        {
            free.frame.onClick.AddListener(() => CardClicked?.Invoke(false));
            free.claimButton.GetComponent<Button>().onClick.AddListener(() => CardClicked?.Invoke(false));
            premium.frame.onClick.AddListener(() => CardClicked?.Invoke(true));
            premium.claimButton.GetComponent<Button>().onClick.AddListener(() => CardClicked?.Invoke(true));
        }

        /// <summary>Chặng 1 cao hơn ở trên: nội dung nằm ở 285 phía dưới như các chặng khác.</summary>
        public void MakeFirst(float shift)
        {
            foreach (var lane in new[] { free, premium })
            {
                lane.card.anchoredPosition += new Vector2(0f, shift);
                ((RectTransform)lane.glow.transform).anchoredPosition = lane.card.anchoredPosition;
                if (lane.lockIcon != null) lane.lockIcon.anchoredPosition += new Vector2(0f, shift);
            }
        }

        public void ShowStage(int stage, bool next, int have, int need)
        {
            stageNumber.text = stage.ToString();
            ticketBar.SetActive(next);
            if (!next) return;
            ticketFill.anchorMax = new Vector2(1f, need > 0 ? Mathf.Clamp01((float)have / need) : 0f);
            ticketFill.gameObject.SetActive(have > 0);
            ticketText.text = have + "/" + need;
        }

        /// <summary>Một làn: màu theo trạng thái (0 đã mở, 1 kế tiếp, 2 chưa tới), hình và số, đã nhận / nhận được, ổ khoá.</summary>
        public void ShowLane(bool isPremium, int state, Sprite sprite, string label, bool big, bool claimed, bool claimable, bool locked)
        {
            var lane = LaneOf(isPremium);
            var colours = state == 0 ? lane.open : state == 1 ? lane.next : lane.locked;
            lane.gradient.Set(colours[0], colours[1]);
            lane.icon.sprite = sprite;
            lane.icon.rectTransform.sizeDelta = Vector2.one * (big ? bigIconSize : iconSize);
            lane.icon.rectTransform.anchoredPosition = new Vector2(0f, big ? bigIconY : iconY);
            lane.amount.text = label;
            lane.amount.gameObject.SetActive(label.Length > 0);
            lane.claimed.SetActive(claimed);
            lane.claim.SetActive(claimable);
            lane.glow.SetActive(claimable);
            if (claimable) UiShiny.Row(lane.claimButton);
            if (lane.lockIcon != null) lane.lockIcon.gameObject.SetActive(locked);
            lane.sparkles.SetActive(claimable);
        }
    }
}
