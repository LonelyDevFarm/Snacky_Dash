using System;
using System.Collections;
using SnackDash.Economy;
using SnackDash.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Cơ hội cắt thêm lần nữa sau GIVE UP (prefab <c>Prefabs/UI/Popups/SecondChance</c>, gốc: LosePopup, video Man_Lose): PLAY ON trả giá cắt
    /// và chơi tiếp, nút huỷ sang GAME OVER. Giữ nền tối thì popup ẩn để nhìn bàn chơi (TMP_See_The_Board). Đang chạy Jungle Heist thì
    /// thay hình rắn bằng lời cảnh báo "We will fail Jungle Heist!" (PopupContentLavaQuest). Phần đuôi sẽ bị cắt nhấp nháy.
    /// </summary>
    public class SecondChancePopup : BasePopup
    {
        [SerializeField] private UiButton playOnButton;
        [SerializeField] private TMP_Text price;
        [SerializeField] private GameObject common;
        [SerializeField] private GameObject heistWarning;
        [Tooltip("Phần đuôi sẽ bị cắt (mờ đi rồi hiện lại liên tục).")]
        [SerializeField] private Graphic fruits;
        [Tooltip("Nửa chu kỳ nhấp nháy (Man_Lose: khoảng 2 s một chu kỳ).")]
        [SerializeField] private float blinkHalf = 1f;

        public event Action PlayOn, GiveUp;

        protected override void OnInit()
        {
            playOnButton.Clicked += () => PlayOn?.Invoke();
            closeButton.Clicked += () => GiveUp?.Invoke();
        }

        protected override void OnShow()
        {
            bool heistOn = JungleHeist.Shared.Current == JungleHeist.State.Running;
            heistWarning.SetActive(heistOn);
            common.SetActive(!heistOn);
            if (fruits != null) StartCoroutine(Blink());
        }

        public void Setup(int cost)
        {
            price.text = cost.ToString();
            var rect = price.rectTransform;
            rect.sizeDelta = new Vector2(price.preferredWidth, rect.sizeDelta.y); // gốc co khung theo chữ
        }

        private IEnumerator Blink()
        {
            while (true)
            {
                yield return Tween.Run(blinkHalf, Tween.InOutSine, t => fruits.color = new Color(1f, 1f, 1f, 1f - t));
                yield return Tween.Run(blinkHalf, Tween.InOutSine, t => fruits.color = new Color(1f, 1f, 1f, t));
            }
        }
    }
}
