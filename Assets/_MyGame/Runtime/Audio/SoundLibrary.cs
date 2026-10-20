using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Bảng âm thanh có tên, kèm âm lượng và cao độ. Tạo từ bảng âm thanh gốc trong Main.unity.
    /// </summary>
    [CreateAssetMenu(menuName = "SnackDash/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public class Sound
        {
            public string name;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            public float pitch = 1f;
            public bool looping;
        }

        [SerializeField] private List<Sound> sounds = new List<Sound>();

        private Dictionary<string, Sound> _byName;

        public Sound Get(string soundName)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, Sound>();
                foreach (var sound in sounds) _byName[sound.name] = sound;
            }
            return _byName.TryGetValue(soundName, out var found) ? found : null;
        }

        public void SetSounds(List<Sound> newSounds)
        {
            sounds = newSounds;
            _byName = null;
        }

        private void OnValidate() => _byName = null;
    }
}
