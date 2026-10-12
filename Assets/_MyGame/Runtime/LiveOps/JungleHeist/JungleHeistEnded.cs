using SnackDash.Economy;
using TMPro;
using UnityEngine;

namespace SnackDash.UI
{
    /// <summary>
    /// Popup kết thúc một lượt Jungle Heist (prefab <c>Prefabs/UI/Popups/JungleHeist/JungleHeistEnded</c>, gốc: Popup_Lava_Quest_Ended_Won / _Lose):
    /// khung gỗ với tranh thắng hoặc thua, lời nhắn, đồng hồ tới lượt sau và nút CONTINUE. Lời nhắn sửa ngay trên Inspector.
    /// Mở bằng <c>UIManager.Show&lt;JungleHeistEnded&gt;()</c> rồi <see cref="Setup"/>; thắng mà chưa nhận thì đóng sẽ trả thưởng.
    /// </summary>
    public class JungleHeistEnded : BasePopup
    {
        [Tooltip("Phần tranh khi thắng / khi thua (bật một trong hai).")]
        [SerializeField] private GameObject wonPart;
        [SerializeField] private GameObject lostPart;
        [SerializeField] private TMP_Text hint;
        [TextArea(2, 4)] [SerializeField] private string wonHint = "<size=45>You <color=#FED025>won</color> the challenge!</size>\nNext <color=#FED025>Jungle Heist</color> will start in:";
        [TextArea(2, 4)] [SerializeField] private string lostHint = "<size=45>You <color=#FED025>failed</color> the challenge.</size>\nNext <color=#FED025>Jungle Heist</color> will start in:";
        [SerializeField] private TimerBanner timer;
        [SerializeField] private UiButton continueButton;

        private bool _won;
        private float _tick;

        /// <summary>Thắng hay thua.</summary>
        public void Setup(bool won)
        {
            _won = won;
            wonPart.SetActive(won);
            lostPart.SetActive(!won);
            hint.text = won ? wonHint : lostHint;
            timer.Show(JungleHeist.Shared.Left);
        }

        protected override void OnInit() => continueButton.Clicked += () => Close();

        protected override void OnHide()
        {
            if (_won && JungleHeist.Shared.ClaimPrize()) SnackDash.Audio.SoundPlayer.Play("CollectCoin");
        }

        private void Update()
        {
            if ((_tick -= Time.unscaledDeltaTime) > 0f) return;
            _tick = 1f;
            timer.Show(JungleHeist.Shared.Left);
        }
    }
}
