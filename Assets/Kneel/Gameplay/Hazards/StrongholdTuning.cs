using UnityEngine;

namespace Kneel.Hazards
{
    // Every timing of L4's hazards in one place (design note, sections 6 and 9), so they can be tuned without code.
    [CreateAssetMenu(menuName = "Kneel/Hazards/Stronghold Tuning")]
    public class StrongholdTuning : ScriptableObject
    {
        [Header("Murder holes")]
        public float pourTell = 1.5f;
        public float pourDuration = 1f;
        public float scaldDuration = 2f;
        // Sequential rings: one every this many seconds, the loop is rings x spacing.
        public float pourSpacing = 4f;
        public float pourRadius = 3f;

        [Header("Oil and lights")]
        public float lightSwing = 1.8f;
        public float lightFall = 0.3f;
        public float oilCatch = 0.5f;
        public float oilBurn = 6f;
        public float oilTick = 1f;

        [Header("Portcullis")]
        public float gateUp = 6f;
        public float gateRattle = 1.5f;
        public float gateDrop = 0.3f;
        public float gateDown = 4f;
        public float gateRise = 2.5f;
        // The height the gate has to clear before passing under it is safe.
        public float gateSafeHeight = 2f;
        public float winchRaise = 1.5f;

        [Header("Scaffolds")]
        public float scaffoldCreak = 1.5f;
        public float scaffoldFall = 0.6f;

        [Header("Kneelers")]
        public float kneelerRadius = 6f;
        public float kneelerRise = 1f;
        public LayerMask playerMask;

        [Header("Ranged fairness")]
        // A ranged attacker may start its first wind-up only after this long inside the camera frame.
        public float onScreenTime = 1f;
        // The frame is inset by this fraction on every side.
        public float onScreenInset = 0.1f;
    }
}
