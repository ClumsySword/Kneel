using System;
using System.Collections.Generic;
using UnityEngine;

// A boss the throne hall's hazards can hurt (The Master.md §1 rule 3: "the hall hurts him too"): the Master now, the
// Ash Colossus in Stage 6. Hazards are the only thing that breaks his armoured wind-ups.
public interface IHallBoss
{
    Transform transform { get; }

    bool IsDead { get; }

    // Damage from the hall, and a stagger (seconds; 0 for none: fire burns, it doesn't stagger).
    void HazardHit(float damage, float stagger);
}

// Who the hall's hazards look for, and how they hurt them.
public static class HallActors
{
    public static readonly List<IHallBoss> Bosses = new List<IHallBoss>();

    private static PlayerCombat player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // The project enters Play without a domain reload.
        Bosses.Clear();
        player = null;
    }

    public static PlayerCombat Player
    {
        get
        {
            if (player == null)
                player = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            return player;
        }
    }

    // A blow from the hall (a falling chandelier, a statue, the floor going): no block, no parry; kneel > 0 forces a
    // kneel. A roll's i-frames still dodge it, as they would a blow.
    public static void HitPlayer(float damage, Vector3 from, GameObject source, float kneel = 0f)
    {
        PlayerCombat p = Player;
        if (p == null)
            return;
        Vector3 away = p.transform.position - from;
        away.y = 0f;
        p.TakeHit(new DamageInfo
        {
            amount = damage,
            poiseDamage = damage,
            point = p.transform.position + Vector3.up,
            direction = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward,
            source = source,
            unblockable = true,
            unparryable = true,
            kneelTime = kneel,
            impact = 0.6f,
        });
    }

    // Fire and ash: environmental, no reaction (so a tick never locks the player inside what is hurting them).
    public static void BurnPlayer(float damage, GameObject source)
    {
        PlayerCombat p = Player;
        if (p == null || damage <= 0f)
            return;
        p.TakeHit(new DamageInfo
        {
            amount = damage,
            point = p.transform.position + Vector3.up,
            direction = Vector3.forward,
            source = source,
            isEnvironmental = true,
        });
    }

    // Every live boss within `reach` of the point (flat), for an area hazard.
    public static IEnumerable<IHallBoss> BossesNear(Vector3 point, float reach)
    {
        for (int i = Bosses.Count - 1; i >= 0; i--)
        {
            IHallBoss boss = Bosses[i];
            if (boss == null || (boss as UnityEngine.Object) == null || boss.IsDead)
                continue;
            Vector3 d = boss.transform.position - point;
            d.y = 0f;
            if (d.magnitude <= reach)
                yield return boss;
        }
    }

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Distance from p to the segment a-b on the floor plane.
    public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        p.y = a.y = b.y = 0f;
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }
}
