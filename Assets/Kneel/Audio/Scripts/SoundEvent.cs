using UnityEngine;

namespace Kneel.Audio
{
    // Every sound the game triggers by name. Each has a folder of clips under Assets/Kneel/Audio/SFX/<name>, which
    // Kneel/Audio/Build Sound Bank turns into a SoundEvent. Add new ones at the end: the bank stores them by name.
    public enum SfxId
    {
        PlayerSwingLight,
        PlayerSwingHeavy,
        HitFlesh,
        HitShield,
        Parry,
        Block,
        GuardBreak,
        Roll,
        FootstepStone,
        FootstepEarth,
        Land,
        SwordDraw,
        SwordSheathe,
        PlayerHurt,
        PlayerDeath,
        BodyFall,
        Drink,
        Pickup,
        Lever,
        Gate,
        ShrineRest,
        TellFootman,
        TellBrute,
        TellFool,
        TellArcher,
        BowRelease,
        ArrowImpact,
        EnemyHurt,
        EnemyDeath,
        BruteRoar,
    }

    public enum SoundGroup
    {
        SFX,
        Music,
        Ambience,
    }

    // One sound: a few variations picked at random, with a little pitch and volume spread so repeats don't sound
    // identical. Played through Sfx.Play. Tune the numbers here; the builder only refreshes the clip list.
    [CreateAssetMenu(menuName = "Kneel/Audio/Sound Event")]
    public class SoundEvent : ScriptableObject
    {
        public AudioClip[] clips = new AudioClip[0];

        [Range(0f, 1f)]
        public float volume = 0.8f;

        // Each play is this much quieter at most (0.1 = up to 10%).
        [Range(0f, 0.5f)]
        public float volumeSpread = 0.1f;

        public float pitch = 1f;

        // Each play's pitch is pitch ± this.
        [Range(0f, 0.5f)]
        public float pitchSpread = 0.06f;

        public SoundGroup group = SoundGroup.SFX;

        // 0 = flat (2D), 1 = fully positional (3D).
        [Range(0f, 1f)]
        public float spatial = 1f;

        // Full volume within minDistance, fading to silence at maxDistance.
        public float minDistance = 2f;
        public float maxDistance = 30f;

        // Two plays of this event closer together than this are merged into one (a flurry of hits in one frame).
        public float cooldown = 0.04f;

        [System.NonSerialized]
        public float lastPlayed = -1f;

        [System.NonSerialized]
        private int lastClip = -1;

        public bool IsEmpty => clips == null || clips.Length == 0;

        // A random clip, never the same one twice running when there's a choice.
        public AudioClip PickClip()
        {
            if (IsEmpty)
            {
                return null;
            }

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClip)
            {
                index = (index + 1) % clips.Length;
            }

            lastClip = index;
            return clips[index];
        }
    }
}
