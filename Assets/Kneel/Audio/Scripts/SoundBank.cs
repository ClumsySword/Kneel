using System.Collections.Generic;
using UnityEngine;

namespace Kneel.Audio
{
    public enum FootSurface
    {
        Stone,
        Earth,
    }

    // A level's sound: an exploration bed, combat drums that fade in while enemies hunt the player, an ambience bed,
    // what fire sounds like, and what the ground is made of.
    [System.Serializable]
    public class LevelSound
    {
        // The scene's name (L1_BrokenLine_Playable_L, L2_HollowVillage, ...).
        public string scene;
        public AudioClip[] explore = new AudioClip[0];
        public AudioClip[] combat = new AudioClip[0];
        public AudioClip ambience;
        public AudioClip fire;
        public FootSurface surface = FootSurface.Stone;

        [Range(0f, 1f)]
        public float exploreVolume = 0.35f;

        [Range(0f, 1f)]
        public float combatVolume = 0.55f;

        [Range(0f, 1f)]
        public float ambienceVolume = 0.35f;

        [Range(0f, 1f)]
        public float fireVolume = 0.5f;
    }

    // Every sound event by name, and each level's music and ambience. Lives in Resources so it loads without being
    // placed in a scene. Kneel/Audio/Build Sound Bank fills it from the folders under Assets/Kneel/Audio.
    [CreateAssetMenu(menuName = "Kneel/Audio/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public SfxId id;
            public SoundEvent sound;
        }

        public List<Entry> sounds = new List<Entry>();
        public List<LevelSound> levels = new List<LevelSound>();

        private Dictionary<SfxId, SoundEvent> lookup;

        private static SoundBank current;
        private static bool loaded;

        public static SoundBank Current
        {
            get
            {
                if (loaded == false)
                {
                    loaded = true;
                    current = Resources.Load<SoundBank>("SoundBank");
                }

                return current;
            }
        }

        // Statics outlive Play mode in this project (no domain reload).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            current = null;
        }

        public SoundEvent Get(SfxId id)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<SfxId, SoundEvent>();
                foreach (Entry entry in sounds)
                {
                    if (entry.sound != null)
                    {
                        lookup[entry.id] = entry.sound;
                    }
                }
            }

            return lookup.TryGetValue(id, out SoundEvent sound) ? sound : null;
        }

        public LevelSound ForScene(string scene)
        {
            foreach (LevelSound level in levels)
            {
                if (level.scene == scene)
                {
                    return level;
                }
            }

            return null;
        }

        private void OnValidate() => lookup = null;
    }
}
