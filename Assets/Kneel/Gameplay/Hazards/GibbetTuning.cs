using UnityEngine;

namespace Kneel.Hazards
{
    [CreateAssetMenu(menuName = "Kneel/Hazards/Gibbet Tuning")]
    public class GibbetTuning : ScriptableObject
    {
        // The crows leave when the player comes this close (4 R).
        public float triggerRadius = 8f;

        // Seconds from the crows leaving to the thrall tearing free.
        public float wakeDelay = 1.5f;

        public LayerMask playerMask;
    }
}
