using UnityEngine;

namespace Kneel.Hazards
{
    public static class HazardContact
    {
        // Tells an actor a hazard touched it. An IHazardTarget decides for itself; anything else that is
        // IDamageable takes environmental damage from the tuning asset (no parry, block or hit-stun).
        public static void Report(Component actor, HazardKind kind, HazardTuning tuning, GameObject source)
        {
            if (actor == null)
            {
                return;
            }

            if (actor is IHazardTarget target)
            {
                target.OnHazard(kind);
                return;
            }

            float damage = tuning != null ? tuning.FallbackDamage(kind) : 0f;
            if (damage <= 0f || !(actor is IDamageable damageable))
            {
                return;
            }

            Vector3 away = actor.transform.position - source.transform.position;
            away.y = 0f;
            damageable.TakeHit(new DamageInfo
            {
                amount = damage,
                point = actor.transform.position,
                direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward,
                source = source,
                isEnvironmental = true,
            });
        }
    }
}
