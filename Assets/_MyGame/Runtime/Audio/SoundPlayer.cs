using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Âm thanh giao diện và nhạc Home cho cả hai scene (âm thanh trong màn do GameAudio lo). Sống qua các lần đổi scene;
    /// theo công tắc Nhạc và Âm thanh (GameSettings).
    /// </summary>
    public class SoundPlayer : MonoBehaviour
    {
        private static SoundPlayer _instance;
        private SoundLibrary _library;
        private AudioSource _effects, _music;

        /// <summary>Hands over the sound table (any scene that has one); the first call makes the player.</summary>
        public static void Use(SoundLibrary library)
        {
            if (library == null) return;
            if (_instance == null)
            {
                var go = new GameObject("SoundPlayer");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SoundPlayer>();
                _instance._effects = go.AddComponent<AudioSource>();
                _instance._music = go.AddComponent<AudioSource>();
                _instance._music.loop = true;
                GameSettings.Changed += () => { if (_instance != null) _instance._music.mute = !GameSettings.Music; };
            }
            _instance._library = library;
        }

        public static void Play(string name)
        {
            if (_instance == null || !GameSettings.Sound) return;
            var sound = _instance._library.Get(name);
            if (sound?.clip == null) return;
            _instance._effects.pitch = sound.pitch;
            _instance._effects.PlayOneShot(sound.clip, sound.volume);
        }

        /// <summary>Loops <paramref name="name"/> (kept playing if it already is); null stops the music.</summary>
        public static void Music(string name)
        {
            if (_instance == null) return;
            var source = _instance._music;
            if (name == null) { source.Stop(); source.clip = null; return; }
            var sound = _instance._library.Get(name);
            if (sound?.clip == null || (source.clip == sound.clip && source.isPlaying)) return;
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.pitch = sound.pitch;
            source.mute = !GameSettings.Music;
            source.Play();
        }
    }
}
