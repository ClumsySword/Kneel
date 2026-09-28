using System;
using UnityEngine;

// Timings for one swing, in seconds of the clip at attackSpeed 1. Defaults were measured from the
// sword-tip speed of each clip on the Knight (hit window = fastest part of the swing).
[Serializable]
public class AttackDefinition
{
    public string stateName;

    public float damage;

    public float staminaCost;

    public float hitStart;

    public float hitEnd;

    // From here a (buffered) click chains into the next swing.
    public float comboWindow;

    // Swing is over and control returns.
    public float recoveryEnd;
}

// Tuning values for PlayerCombat. Kept in an asset so edits made during Play mode are saved.
[CreateAssetMenu(fileName = "PlayerCombatSettings", menuName = "Kneel/Player Combat Settings")]
public class PlayerCombatSettings : ScriptableObject
{
    [Header("Attack Info")]
    public AttackDefinition[] attacks =
    {
        new AttackDefinition { stateName = "Slash1", damage = 12f, staminaCost = 12f, hitStart = 0.5f, hitEnd = 0.66f, comboWindow = 0.66f, recoveryEnd = 0.95f },
        new AttackDefinition { stateName = "Slash2", damage = 20f, staminaCost = 18f, hitStart = 0.66f, hitEnd = 0.92f, comboWindow = 0.95f, recoveryEnd = 1.25f },
    };

    // Damage multiplier against a staggered target (e.g. after a parry).
    public float criticalMultiplier = 1.5f;

    // A click this long before the combo window opens is remembered.
    public float comboBufferTime = 0.3f;

    // Forward distance covered at the start of each swing.
    public float attackLunge = 0.5f;

    // Seconds into a swing before turning locks, so you can still line it up.
    public float attackTurnWindow = 0.15f;

    // Speed of the swing animations.
    [Range(0.5f, 2f)]
    public float attackSpeed = 1f;

    [Header("Draw Info")]
    [Range(0.5f, 2.5f)]
    public float drawSpeed = 1.3f;

    // Seconds into the draw (Draw1 then Draw2) when the hand closes on the hilt.
    public float drawGrabTime = 0.45f;

    public float drawDuration = 1.2f;

    // Seconds into the sheathe (Sheath1 then Sheath2) when the sword is let go in the scabbard.
    public float sheathReleaseTime = 1.03f;

    public float sheathDuration = 1.7f;

    [Header("Dodge Info")]
    // Distance covered by a full roll.
    public float dodgeDistance = 3f;

    // Total roll time, including getting back up. The roll animation is sped up to fit.
    public float dodgeDuration = 0.85f;

    public float dodgeStaminaCost = 20f;

    // Invulnerable between these times into the roll (roughly frames 4-13 at 30 fps, per the Combat Roll spec).
    public float dodgeInvulnerableStart = 0.07f;

    public float dodgeInvulnerableEnd = 0.43f;

    // A roll pressed this long before the current action ends is performed when it does.
    public float dodgeBufferTime = 0.35f;

    // Roll through enemies instead of colliding with them (Souls-style). Open design question.
    public bool dodgeThroughEnemies = false;

    // Fraction of the roll distance covered over normalized roll time, measured from the root motion
    // of the trimmed "Stand To Roll" clip so the body and the translation stay in sync.
    public AnimationCurve dodgeTravelCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.071f, 0.079f), new Keyframe(0.143f, 0.182f), new Keyframe(0.214f, 0.286f),
        new Keyframe(0.286f, 0.39f), new Keyframe(0.357f, 0.494f), new Keyframe(0.429f, 0.591f), new Keyframe(0.5f, 0.682f),
        new Keyframe(0.571f, 0.772f), new Keyframe(0.643f, 0.847f), new Keyframe(0.714f, 0.912f), new Keyframe(0.786f, 0.958f),
        new Keyframe(0.857f, 0.985f), new Keyframe(0.929f, 0.997f), new Keyframe(1f, 1f));

    [Header("Block Info")]
    [Range(0f, 1f)]
    public float blockMoveMultiplier = 0.5f;

    // Fraction of damage the shield absorbs.
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.8f;

    // Stamina lost per point of damage absorbed by the shield.
    public float blockStaminaPerDamage = 1f;

    // Hits from within this angle of where you face can be blocked or parried.
    public float blockAngle = 70f;

    [Header("Parry Info")]
    // The first moments of a fresh block press parry instead of block.
    public float parryWindow = 0.2f;

    // Pressing block again within this time of the last press gives no parry window.
    public float parryCooldown = 0.4f;

    [Header("Feel Info")]
    public float hitStop = 0.06f;

    public float parryHitStop = 0.12f;

    public float hitShake = 0.08f;

    public float hitStunDuration = 0.45f;

    public float guardBreakDuration = 0.9f;

    [Header("Health Info")]
    public float maxHealth = 100f;
}
