using SnackDash.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Phần thưởng khi thắng Jungle Heist (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistRewards</c>, gốc: Popup_Lava_Quest_Rewards): tiêu đề,
    /// rương Grand Prize, "You win" + gói xu với phần chia, dòng "sharing with N other winners" và hàng avatar người thắng (của mình
    /// đứng đầu, sinh từ khối <c>Widgets/Avatar</c>). Chạm để nhận: cộng xu, xu bay về ô xu Home. Mở bằng UIManager.
    /// </summary>
    public class JungleHeistRewards : BasePopup
    {
        [SerializeField] private Button tapArea;
        [SerializeField] private RevealSequence reveal;
        [Tooltip("Số xu nhận được (dưới gói xu).")]
        [SerializeField] private TMP_Text amount;
        [Tooltip("Gói xu: xu bay về Home từ đây.")]
        [SerializeField] private RectTransform coins;
        [SerializeField] private TMP_Text sharing;
        [Tooltip("{0} = số người thắng khác.")]
        [SerializeField] private string sharingText = "You are sharing the reward with {0} other winners!";
        [Tooltip("Hàng avatar người thắng.")]
        [SerializeField] private RectTransform row;
        [Tooltip("Khối Widgets/Avatar dùng làm mẫu.")]
        [SerializeField] private AvatarView avatarPrefab;
        [SerializeField] private float avatarScale = 1.8f;
        [Tooltip("Khoảng cách giữa tâm 2 avatar.")]
        [SerializeField] private float avatarSpacing = 205f;
        [Tooltip("Giây hiện avatar đầu tiên; mỗi avatar sau trễ thêm Avatar Step.")]
        [SerializeField] private float avatarsFrom = 1f;
        [SerializeField] private float avatarStep = 0.08f;

        protected override void OnInit()
        {
            tapArea.onClick.AddListener(() => { if (!reveal.Ready) reveal.Skip(); else Claim(); });
            int winners = JungleHeist.SurvivorsPerStep[JungleHeist.Steps];
            amount.text = JungleHeist.Share.ToString();
            sharing.text = string.Format(sharingText, winners - 1);
            // Người thắng, của mình đứng đầu (PlayerAvatarIndex 0); hàng thu nhỏ cho vừa bề ngang.
            for (int i = 0; i < winners; i++)
            {
                var avatar = Instantiate(avatarPrefab, row);
                var rect = (RectTransform)avatar.transform;
                rect.anchoredPosition = new Vector2((i - (winners - 1) * 0.5f) * avatarSpacing, 0f);
                rect.localScale = Vector3.one * avatarScale;
                avatar.Set(i == 0 ? 5 : i, i == 0);
                reveal.AddPart(rect, avatarsFrom + avatarStep * i);
            }
            Canvas.ForceUpdateCanvases();
            float wide = (winners - 1) * avatarSpacing + 100f * avatarScale;
            if (wide > row.rect.width) row.localScale = Vector3.one * (row.rect.width / wide);
        }

        protected override void OnShow() => StartCoroutine(reveal.Play());

        /// <summary>PlayCoinFlyThenClose: phần chia vào ví, xu bay từ gói xu về ô xu Home.</summary>
        private void Claim()
        {
            if (JungleHeist.Shared.ClaimPrize())
            {
                var home = SnackDash.Core.Services.Get<HomeTopBar>(); // HomeTopBar tự đăng ký khi Home mở
                if (home != null) home.FlyCoins(coins.position, JungleHeist.Share);
            }
            Close();
        }
    }
}
