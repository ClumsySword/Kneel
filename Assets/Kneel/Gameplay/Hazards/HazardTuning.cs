using UnityEngine;

namespace Kneel.Hazards
{
    // Shared by every hazard: who counts as an actor, and what a contact costs an actor that only has the
    // project's IDamageable (no IHazardTarget yet). An IHazardTarget always decides its own consequence.
    [CreateAssetMenu(menuName = "Kneel/Hazards/Hazard Tuning")]
    public class HazardTuning : ScriptableObject
    {
        public LayerMask actorMask;

        // Damage of one "hit" for the IDamageable fallback.
        public float fallbackHitDamage = 20f;

        public int fireEnterHits = 1;
        public int fireTickHits = 1;

        // A fall into the ditch kills.
        public float ditchDamage = 100000f;

        public float FallbackDamage(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.FireEnter: return fireEnterHits * fallbackHitDamage;
                case HazardKind.FireTick: return fireTickHits * fallbackHitDamage;
                case HazardKind.Ditch: return ditchDamage;
                default: return 0f;
            }
        }
    }
}
