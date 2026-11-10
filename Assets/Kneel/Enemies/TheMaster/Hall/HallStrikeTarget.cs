using System;
using UnityEngine;

// Something in the hall the sword sets off (a winch, a brazier): its collider sits on the Enemy layer, where the
// sword's hitbox looks, and any blow that isn't fire raises Struck.
public class HallStrikeTarget : MonoBehaviour, IDamageable
{
    public event Action<DamageInfo> Struck;

    public void TakeHit(DamageInfo info)
    {
        if (info.isEnvironmental)
            return;
        Struck?.Invoke(info);
    }
}
