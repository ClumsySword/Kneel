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

        // L4: a murder-hole pour and a collapse cost two hits; burning oil one on contact and one a second;
        // the portcullis slot and a fall off the walls kill.
        public int pourHits = 2;
        public int crushHits = 2;
        public int oilEnterHits = 1;
        public int oilTickHits = 1;

        public float FallbackDamage(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.FireEnter: return fireEnterHits * fallbackHitDamage;
                case HazardKind.FireTick: return fireTickHits * fallbackHitDamage;
                case HazardKind.Ditch: return ditchDamage;
                case HazardKind.Pour: return pourHits * fallbackHitDamage;
                case HazardKind.Crush: return crushHits * fallbackHitDamage;
                case HazardKind.OilBurnEnter: return oilEnterHits * fallbackHitDamage;
                case HazardKind.OilBurnTick: return oilTickHits * fallbackHitDamage;
                case HazardKind.GateSlot: return ditchDamage;
                case HazardKind.Fall: return ditchDamage;
                default: return 0f;
            }
        }
    }
}
