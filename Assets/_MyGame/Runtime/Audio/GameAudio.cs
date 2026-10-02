using System.Collections.Generic;
using SnackDash.Gameplay;
using UnityEngine;

namespace SnackDash.Audio
{
    /// <summary>
    /// Plays the original sound names on game events. The event-to-name pairing follows the names in
    /// the original sound table (Swipe, GetPill1-3, ShootToCrate, Complete_Crate, CrateConfetti,
    /// SpawnPill, Confetti, Music_Core); the original call sites are stripped, so the pairing itself is
    /// inferred from those names.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [SerializeField] private SoundLibrary library;
        [SerializeField] private int voices = 10; // original entries use poolSize 10

        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private AudioSource _music;
        private int _next;
        private int _eatStreak;

        public void Bind(GameSession session, SnackDash.Presentation.CratesView crates)
        {
            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                _sources.Add(source);
            }
            PlayMusic("Music_Core");

            session.MoveStarted += direction => { _eatStreak = 0; Play("Swipe"); };
            // Three eat sounds; rising through them within one run is an inference from the names.
            session.PillEaten += (cell, kind, index) => Play("GetPill" + Mathf.Min(++_eatStreak, 3));
            session.SegmentDelivered += (index, kind, column) => Play("ShootToCrate");
            // Completion sounds go with the crate's flash, which follows the full-crate hold.
            crates.CompletionEffectStarted += column => { Play("Complete_Crate"); Play("CrateConfetti"); };
            session.PillSpawned += (cell, kind) => PlayOncePerFrame("SpawnPill");
            session.Won += () => Play("Confetti");
            session.TailBitten += count => Play("HitTail");
        }

        private int _lastSpawnSoundFrame = -1;

        private void PlayOncePerFrame(string soundName)
        {
            if (_lastSpawnSoundFrame == Time.frameCount) return;
            _lastSpawnSoundFrame = Time.frameCount;
            Play(soundName);
        }

        public void Play(string soundName)
        {
            var sound = library != null ? library.Get(soundName) : null;
            if (sound?.clip == null || _sources.Count == 0) return;
            var source = _sources[_next];
            _next = (_next + 1) % _sources.Count;
            source.pitch = sound.pitch;
            source.PlayOneShot(sound.clip, sound.volume);
        }

        private void PlayMusic(string soundName)
        {
            var sound = library != null ? library.Get(soundName) : null;
            if (sound?.clip == null) return;
            _music = gameObject.AddComponent<AudioSource>();
            _music.clip = sound.clip;
            _music.volume = sound.volume;
            _music.pitch = sound.pitch;
            _music.loop = true;
            _music.Play();
        }
    }
}
