using System;
using SnackDash.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// "ARE YOU SURE?" khi bấm BACK TO HOME trong màn (prefab <c>Prefabs/UI/Popups/ConfirmLeavePopup</c>, gốc: ConfirmQuitLevelPopup):
    /// "YOU WILL LOSE 1 LIFE!", hàng tim (8 tim khi Snacky Pass bật thì thu nhỏ cho vừa khung; tim sẽ mất nhấp nháy), giờ hồi tim; CONTINUE rời màn, X quay lại [user].
    /// Mở bằng <c>UIManager.Show&lt;ConfirmLeavePopup&gt;()</c> rồi <see cref="Setup"/>.
    /// </summary>
    public class ConfirmLeavePopup : BasePopup
    {
        [Tooltip("Hàng tim; tim mẫu (con đầu tiên, có Empty và Full) được chép đủ số tim tối đa.")]
        [SerializeField] private RectTransform hearts;
        [SerializeField] private RectTransform heartTemplate;
        [Tooltip("Khoảng cách và cỡ tim lớn nhất; nhiều tim (Snacky Pass 8 tim) thì thu nhỏ cho vừa bề ngang hàng tim [user].")]
        [SerializeField] private float heartGap = 135f;
        [SerializeField] private float heartSize = 125f;
        [Tooltip("Tim sẽ mất (tim đầy cuối) nhấp nháy bấy nhiêu lần mỗi giây [user].")]
        [SerializeField] private float loseBlinkRate = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float loseBlinkMinAlpha = 0.25f;
        [SerializeField] private TMP_Text countdown;
        [Tooltip("Chữ thay cho đồng hồ khi tim đã đầy.")]
        [SerializeField] private string fullText = "FULL";
        [SerializeField] private UiButton continueButton;

        private Action _leave;
        private Image[] _full;
        private float _tick;
        private int _losing = -1; // tim đang nhấp nháy

        public void Setup(Action leave) => _leave = leave;

        protected override void OnInit()
        {
            continueButton.Clicked += () => _leave?.Invoke();
            int count = PlayerEconomy.Shared.LifeCap;
            float gap = Mathf.Min(heartGap, hearts.rect.width / count);
            _full = new Image[count];
            heartTemplate.gameObject.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                var heart = Instantiate(heartTemplate, hearts);
                heart.gameObject.SetActive(true);
                heart.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * gap, 0f);
                heart.sizeDelta = Vector2.one * Mathf.Min(heartSize, gap - 10f);
                _full[i] = heart.Find("Full").GetComponent<Image>();
            }
            PlayerEconomy.Shared.Changed += Refresh;
        }

        protected override void OnShow() => Refresh();

        private void OnDestroy() => PlayerEconomy.Shared.Changed -= Refresh;

        private void Update()
        {
            Blink();
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            var economy = PlayerEconomy.Shared;
            bool infinite = economy.InfiniteLives, full = economy.Lives >= economy.LifeCap;
            for (int i = 0; i < _full.Length; i++) _full[i].enabled = infinite || i < economy.Lives;
            // Tim đầy: "Time to next life: FULL" như bản gốc (video home); chưa đầy: giờ tới tim sau, hoặc giờ còn của tim vô hạn.
            countdown.text = full ? fullText : LivesPopup.Clock(infinite ? economy.InfiniteLeft : economy.NextLifeIn);
            int losing = infinite ? -1 : economy.Lives - 1; // tim vô hạn thì không mất tim nào
            if (losing != _losing && _losing >= 0 && _losing < _full.Length) SetAlpha(_full[_losing], 1f);
            _losing = losing;
        }

        /// <summary>Tim đầy cuối (tim sẽ mất khi rời màn) mờ đi rồi rõ lại liên tục.</summary>
        private void Blink()
        {
            if (_full == null || _losing < 0 || _losing >= _full.Length) return;
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * loseBlinkRate * Mathf.PI * 2f);
            SetAlpha(_full[_losing], Mathf.Lerp(loseBlinkMinAlpha, 1f, wave));
        }

        private static void SetAlpha(Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
