using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Kneel.Audio
{
    // Plays the game's sound effects: Sfx.Play(SfxId.HitFlesh, point). One-shot sources come from a small pool that
    // survives scene loads, routed to the KneelMixer's SFX, Music or Ambience group, so the options menu's volumes
    // and the pause menu (PauseAudio) apply to them. A missing bank or an empty event is silence, never an error.
    public static class Sfx
    {
        private const int PoolSize = 24;

        private static readonly List<AudioSource> pool = new List<AudioSource>();
        private static Transform root;
        private static int next;
        private static AudioMixer mixer;
        private static bool mixerLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pool.Clear();
            root = null;
            next = 0;
            mixer = null;
            mixerLoaded = false;
        }

        public static void Play(SfxId id, Vector3 position, float volumeScale = 1f) => Play(Get(id), position, volumeScale);

        // Flat (2D) whatever the event says: the player's own body (footsteps, grunts), so they don't pan with the camera.
        public static void PlayFlat(SfxId id, float volumeScale = 1f) => Play(Get(id), Vector3.zero, volumeScale, true);

        public static SoundEvent Get(SfxId id)
        {
            SoundBank bank = SoundBank.Current;
            return bank != null ? bank.Get(id) : null;
        }

        public static AudioSource Play(SoundEvent sound, Vector3 position, float volumeScale = 1f, bool flat = false)
        {
            if (sound == null || sound.IsEmpty || volumeScale <= 0f)
            {
                return null;
            }

            float now = Time.unscaledTime;
            if (sound.lastPlayed >= 0f && now - sound.lastPlayed < sound.cooldown && now >= sound.lastPlayed)
            {
                return null;
            }

            sound.lastPlayed = now;
            AudioClip clip = sound.PickClip();
            if (clip == null)
            {
                return null;
            }

            AudioSource source = NextSource();
            source.transform.position = position;
            source.clip = clip;
            source.outputAudioMixerGroup = Group(sound.group);
            source.volume = sound.volume * volumeScale * (1f - Random.Range(0f, sound.volumeSpread));
            source.pitch = sound.pitch + Random.Range(-sound.pitchSpread, sound.pitchSpread);
            source.spatialBlend = flat ? 0f : sound.spatial;
            source.minDistance = sound.minDistance;
            source.maxDistance = sound.maxDistance;
            source.Play();
            return source;
        }

        // The mixer group a sound plays through. Ambience falls back to Music in a mixer without that group.
        public static AudioMixerGroup Group(SoundGroup group)
        {
            if (mixerLoaded == false)
            {
                mixerLoaded = true;
                mixer = Resources.Load<AudioMixer>("KneelMixer");
            }

            if (mixer == null)
            {
                return null;
            }

            AudioMixerGroup[] found = mixer.FindMatchingGroups(group.ToString());
            if ((found == null || found.Length == 0) && group == SoundGroup.Ambience)
            {
                found = mixer.FindMatchingGroups("Music");
            }

            return found != null && found.Length > 0 ? found[0] : null;
        }

        private static AudioSource NextSource()
        {
            if (root == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                root = go.transform;
                pool.Clear();
                for (int i = 0; i < PoolSize; i++)
                {
                    var child = new GameObject("Voice " + i);
                    child.transform.SetParent(root, false);
                    var source = child.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.rolloffMode = AudioRolloffMode.Linear;
                    source.dopplerLevel = 0f;
                    pool.Add(source);
                }
            }

            // The oldest voice that isn't playing, else the oldest voice.
            for (int i = 0; i < pool.Count; i++)
            {
                AudioSource candidate = pool[(next + i) % pool.Count];
                if (candidate != null && candidate.isPlaying == false)
                {
                    next = (next + i + 1) % pool.Count;
                    return candidate;
                }
            }

            AudioSource oldest = pool[next];
            next = (next + 1) % pool.Count;
            oldest.Stop();
            return oldest;
        }
    }
}
