using System;
using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Các công tắc của popup Cài đặt (SettingsPopup gốc: Nhạc, Rung, Âm thanh), lưu trong PlayerPrefs để Home và Game
    /// dùng chung.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static bool Music { get => Get("settings_music"); set => Set("settings_music", value); }
        public static bool Sound { get => Get("settings_sound"); set => Set("settings_sound", value); }
        public static bool Haptics { get => Get("settings_haptics"); set => Set("settings_haptics", value); }

        private static bool Get(string key) => PlayerPrefs.GetInt(key, 1) != 0;

        private static void Set(string key, bool on)
        {
            PlayerPrefs.SetInt(key, on ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
