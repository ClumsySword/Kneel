using UnityEngine;

// Everything a receiver needs to resolve a hit: how much, where, from which direction and who dealt it.
public struct DamageInfo
{
    public float amount;

    // World position of the contact.
    public Vector3 point;

    // Direction the hit travels in (attacker toward receiver), flattened to the ground plane.
    public Vector3 direction;

    public GameObject source;

    public bool isCritical;
}

public interface IDamageable
{
    void TakeHit(DamageInfo info);
}

// Implemented by attackers that react when their attack is parried.
public interface IParryable
{
    void OnParried(GameObject parrier);
}

// Targets that can be knocked off balance; hits on a staggered target are critical.
public interface IStaggerable
{
    bool IsStaggered { get; }
}
