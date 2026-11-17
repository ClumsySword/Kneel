using UnityEngine;

// Everything a receiver needs to resolve a hit: how much, where, from which direction and who dealt it.
public struct DamageInfo
{
    public float amount;

    // How hard the hit knocks the receiver off balance (see Poise). Zero never staggers.
    public float poiseDamage;

    // World position of the contact.
    public Vector3 point;

    // Direction the hit travels in (attacker toward receiver), flattened to the ground plane.
    public Vector3 direction;

    public GameObject source;

    public bool isCritical;

    // Damage from the world (fire), not an attack: it can't be parried or blocked and causes no hit reaction.
    public bool isEnvironmental;

    // Shield bash: cancels an attack that is still winding up.
    public bool interrupts;

    // No weapon to parry, such as an arrow: a press in the parry window only blocks it.
    public bool unparryable;

    // How much heavier than a normal blow this lands: 1 doubles the hit-stop and shake, and it shoves the receiver.
    public float impact;

    // A grab (the Master's Seize, the Colossus's Grasp): it goes through the shield and the parry window alike.
    public bool unblockable;

    // Forces the receiver to its knees for this many seconds (the Master's Command and Seize). The player can't act
    // and nothing lands meanwhile; whoever forces it times their next blow for after the release.
    public float kneelTime;

    // The player's heavy attack, which the Ash Colossus's armour doesn't soften.
    public bool heavy;

    // Not a blow but a light (the Master's Command): a roll's i-frames don't escape it, only cover does.
    public bool ignoresRoll;

    // Knocks the receiver flat (the Ash Colossus's Reaping Sweep and Grasp): the player is down, invulnerable, then
    // gets up; `fling` metres along `direction` first. A grab holds them `holdTime` seconds inside `holder` (hidden),
    // then flings them.
    public bool knockdown;
    public float fling;
    public float holdTime;
    public Transform holder;
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
