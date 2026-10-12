using System;
using SnackDash.Audio;
using SnackDash.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SnackDash.UI
{
    /// <summary>
    /// Màn cài đặt dùng chung cho Home và trong màn (prefab <c>Prefabs/UI/Popups/SettingsPopup</c>, gốc: SettingsPopup): đầu răng cưa cam
    /// với SETTINGS và nút X, 3 công tắc Music / Haptics / Sound (xanh khi bật, xám khi tắt), dòng ngôn ngữ (chỉ English), HELP &amp;
    /// SUPPORT, BACK TO HOME khi đang trong màn, TERMS &amp; PRIVACY. Help, Terms và mũi tên ngôn ngữ chỉ để hiện [user]. BACK TO HOME
    /// hỏi lại (<see cref="ConfirmLeavePopup"/>). Chạm SETTINGS 2 lần mở bảng cheat. Mở bằng <c>UIManager.Show&lt;SettingsPopup&gt;()</c>.
    /// </summary>
    public class SettingsPopup : BasePopup
    {
        public enum Setting { Music, Haptics, Sound }

        [Serializable]
        public class Switch
        {
            public Setting setting;
            public Button button;
            public Image state;
            public Image icon;
            public Sprite iconOn, iconOff;
        }

        [SerializeField] private Switch[] switches = new Switch[0];
        [SerializeField] private Sprite stateOn, stateOff;
        [Tooltip("BACK TO HOME: chỉ hiện khi đang trong màn.")]
        [SerializeField] private UiButton backHomeButton;

        private Action _backToHome;

        protected override void OnInit()
        {
            foreach (var item in switches)
            {
                var s = item;
                s.button.onClick.AddListener(() => { Set(s.setting, !Get(s.setting)); Show(s); });
            }
            backHomeButton.Clicked += () =>
            {
                var confirm = Services.Get<UIManager>().Show<ConfirmLeavePopup>();
                if (confirm != null) confirm.Setup(_backToHome);
            };
            backHomeButton.gameObject.SetActive(false);
        }

        protected override void OnShow()
        {
            foreach (var item in switches) Show(item);
        }

        /// <param name="backToHome">Trong màn: việc BACK TO HOME làm sau khi xác nhận (mất 1 tim). Ở Home: null (không có nút).</param>
        public void Setup(Action backToHome)
        {
            _backToHome = backToHome;
            backHomeButton.gameObject.SetActive(backToHome != null);
        }

        private void Show(Switch item)
        {
            bool on = Get(item.setting);
            item.state.sprite = on ? stateOn : stateOff;
            item.icon.sprite = on ? item.iconOn : item.iconOff;
        }

        private static bool Get(Setting setting) =>
            setting == Setting.Music ? GameSettings.Music : setting == Setting.Haptics ? GameSettings.Haptics : GameSettings.Sound;

        private static void Set(Setting setting, bool value)
        {
            if (setting == Setting.Music) GameSettings.Music = value;
            else if (setting == Setting.Haptics) GameSettings.Haptics = value;
            else GameSettings.Sound = value;
        }
    }
}
