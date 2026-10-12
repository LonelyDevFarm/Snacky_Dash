using SnackDash.Audio;
using UnityEngine;

namespace SnackDash.Core
{
    /// <summary>
    /// Âm thanh, nhạc và rung. Thư viện âm thanh (tên → clip, âm lượng, cao độ) là file <c>Data/SoundLibrary</c>, kéo vào
    /// ô Library; thêm âm thanh mới = thêm một dòng trong file đó. Bật / tắt nhạc, tiếng, rung theo Cài đặt (GameSettings).
    /// </summary>
    public class AudioManager : ManagerBase
    {
        [Tooltip("Data/SoundLibrary: bảng âm thanh của game.")]
        [SerializeField] private SoundLibrary library;

        public override void Init()
        {
            if (library != null) SoundPlayer.Use(library);
            Services.Register(this);
        }

        /// <summary>Phát một âm thanh theo tên trong thư viện (vd. "CollectCoin").</summary>
        public void Play(string sound) => SoundPlayer.Play(sound);

        /// <summary>Đổi nhạc nền (vd. "Music_Lobby").</summary>
        public void Music(string track) => SoundPlayer.Music(track);

        public void HapticLight() => Haptics.Light();
        public void HapticMedium() => Haptics.Medium();
        public void HapticHeavy() => Haptics.Heavy();
    }
}
