using System;
using System.Collections;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// "NO WAY OUT!" khi rắn cắn đuôi (prefab <c>Prefabs/UI/Popups/NoWayOut</c>, gốc: LoseInBoardPopup, video Man_Lose): chữ to bật ra, giữ,
    /// thu lại; rồi phần thân (dải tối trên / dưới và 2 nút SNIP TAIL!, GIVE UP) hiện dần trong lúc bàn chơi thu lên trên các nút.
    /// Giá cắt hiện cạnh đồng xu ("FREE" thì chỉ chữ, ở giữa). Dòng chảy thua do <see cref="LoseFlowView"/> điều khiển.
    /// </summary>
    public class NoWayOutPopup : BasePopup
    {
        [SerializeField] private GameObject failText;
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup body;
        [SerializeField] private UiButton snipButton;
        [SerializeField] private UiButton giveUpButton;
        [SerializeField] private TMP_Text price;
        [SerializeField] private RectTransform coin;
        [Header("Nhịp (AppearDissappear của LoseInBoardPopup)")]
        [SerializeField] private float titleScaleDuration = 0.2f;
        [SerializeField] private float titleHoldDuration = 0.75f;
        [SerializeField] private float bodyFadeDuration = 0.2f;

        public event Action Snip, GiveUp;

        protected override void OnInit()
        {
            snipButton.Clicked += () => Snip?.Invoke();
            giveUpButton.Clicked += () => GiveUp?.Invoke();
            SetBody(0f, false);
        }

        /// <param name="cost">Giá cắt (0 = FREE).</param>
        /// <param name="forced">Màn hướng dẫn cắt (Level 3): không có GIVE UP [user].</param>
        public void Setup(int cost, bool forced)
        {
            bool free = cost == 0;
            price.text = free ? "FREE" : cost.ToString();
            giveUpButton.gameObject.SetActive(!forced);
            // Đồng xu ngay bên trái số (sprite TMP trong bản gốc); FREE như FREE của booster: chỉ chữ, ở giữa, không xu [user].
            coin.gameObject.SetActive(!free);
            float width = price.GetPreferredValues(price.text).x;
            price.rectTransform.anchoredPosition = new Vector2(free ? 0f : 30f, price.rectTransform.anchoredPosition.y);
            coin.anchoredPosition = new Vector2(-width / 2f - 34f, 4f);
        }

        /// <summary>Chữ bật 0.2 s, giữ 0.75 s, thu 0.2 s; rồi <paramref name="beforeBody"/> (thu bàn chơi) và thân hiện dần.</summary>
        public IEnumerator Reveal(Action beforeBody)
        {
            failText.SetActive(true);
            title.localScale = Vector3.zero;
            yield return Tween.Run(titleScaleDuration, Tween.OutBack(3f), t => title.localScale = Vector3.one * t);
            yield return new WaitForSeconds(titleHoldDuration);
            yield return Tween.Run(titleScaleDuration, Tween.InQuad, t => title.localScale = Vector3.one * (1f - t));
            failText.SetActive(false);
            beforeBody?.Invoke();
            yield return Tween.Run(bodyFadeDuration, Tween.Linear, t => body.alpha = t);
            SetBody(1f, true);
        }

        private void SetBody(float alpha, bool interactive)
        {
            body.alpha = alpha;
            body.interactable = interactive;
            body.blocksRaycasts = interactive;
        }
    }
}
