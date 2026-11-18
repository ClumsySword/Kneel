using UnityEngine;

namespace Kneel.Core
{
    // Timings and grade leans shared by every level transition. All times are real (unscaled) seconds. Lives in
    // Core/Transitions/Resources so the flow finds it without a scene reference; the F1 overlay has a
    // "Transitions" section that writes into it.
    [CreateAssetMenu(menuName = "Kneel/Level Transition Settings", fileName = "LevelTransitionSettings")]
    public class LevelTransitionSettings : ScriptableObject
    {
        public const string ResourceName = "LevelTransitionSettings";

        [Header("Threshold")]
        [Tooltip("Longest wait for a committed action (swing, roll, drink) to finish and the sword to go away before the walk starts anyway.")]
        public float sheatheTimeout = 3f;

        [Tooltip("Walk speed of the scripted walk, as a fraction of a full forward walk (1 = the clip's own pace).")]
        [Range(0.3f, 1f)] public float walkInput = 1f;

        [Tooltip("How fast the walk turns him toward the path, in degrees per second.")]
        public float walkTurnRate = 240f;

        [Tooltip("Within this many metres of a path point he moves on to the next.")]
        public float waypointReach = 0.6f;

        [Header("Outro")]
        [Tooltip("Blend from the gameplay camera into the outro's first key.")]
        public float outroCameraBlend = 1.1f;

        [Tooltip("Longest the outro may run before the fade, whatever the camera keys say.")]
        public float outroMaxTime = 4.5f;

        [Tooltip("How far the grade leans toward the next level's by the end of the outro (0 none, 1 all the way).")]
        [Range(0f, 1f)] public float outroLean = 0.35f;

        [Tooltip("Music fades out over this fraction of the outro, ending as the fade to black begins.")]
        [Range(0.2f, 1f)] public float musicFadeShare = 1f;

        [Header("Loading screen")]
        public float fadeToBlack = 0.9f;
        public float artFadeIn = 0.9f;
        public float artFadeOut = 0.9f;

        [Tooltip("The art stays up at least this long, from the moment it starts fading in until it starts fading out.")]
        public float minLoadingScreen = 5f;

        [Tooltip("The art's slow push: how much larger it ends than it starts over the minimum time.")]
        [Range(0f, 0.08f)] public float artPush = 0.03f;

        [Tooltip("If the next level still isn't ready this long after the art came up, a small ember shows it's still working.")]
        public float emberAfter = 6f;

        [Tooltip("Frames the next level must run under stableFrameMs, one after another, before it counts as settled.")]
        public int stableFrames = 8;
        public float stableFrameMs = 34f;

        [Tooltip("Frames the next level runs (rendering its opening shot under the screen, warming shaders) before the stable count starts.")]
        public int warmFrames = 6;

        [Header("Intro")]
        public float fadeFromBlack = 1.1f;

        [Tooltip("How far the grade starts leaning back toward the previous level's.")]
        [Range(0f, 1f)] public float introLean = 0.3f;

        [Tooltip("How long the lean back takes to settle into the level's own grade.")]
        public float introLeanSettle = 3.5f;

        [Tooltip("The camera's ease from the intro's last key into the gameplay framing.")]
        public float introCameraRelease = 0.9f;

        [Tooltip("Sound fades in over this long from the first frame of the intro.")]
        public float soundFadeIn = 2.5f;

        [Tooltip("Longest the intro may run, whatever its path and keys say.")]
        public float introMaxTime = 5f;
    }
}
