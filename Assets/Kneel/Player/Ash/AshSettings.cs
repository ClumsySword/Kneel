using UnityEngine;

// Tuning for the Ash meter and its five abilities (AshMeter, PlayerAsh, AshPresenter). Kept in an asset so edits made
// in Play mode (the F1 overlay's Ash section) are saved.
[CreateAssetMenu(fileName = "AshSettings", menuName = "Kneel/Ash Settings")]
public class AshSettings : ScriptableObject
{
    [Header("Meter")]
    // Every ability needs a full meter and empties it.
    public float maxAsh = 100f;

    // A kill releases most of the meter, a parry or a roll through an attack skims a little. Blocking gives nothing.
    public float footmanKill = 50f;

    public float archerKill = 40f;

    public float bruteKill = 100f;

    public float foolKill = 100f;

    // Only with the F1 overlay's switch on.
    public float dummyKill = 50f;

    public float parryGain = 15f;

    public float evadeGain = 5f;

    // One attack can pass through the roll on several frames; it only pays once in this long.
    public float evadeDedupWindow = 0.5f;

    // A kill pays when the enemy dies within this far of the player, past its aggression range, whatever killed it.
    public float engagedMargin = 4f;

    [Header("Casting")]
    // A press during another action casts when it ends, if within this long.
    public float castBufferTime = 0.3f;

    // Uses of any ability before each one unlocks. Ashen Sword comes with the meter.
    public int blindUnlock = 3;

    public int surgeUnlock = 10;

    public int ashfallUnlock = 20;

    public int rekindleUnlock = 30;

    [Header("Ashen Sword")]
    // Ignites the blade in ash-fire for this long; the sword's hits deal this much more damage meanwhile. Instant,
    // no animation. With the sword away it's drawn first. Casting again while lit restarts the timer.
    public float ashenSwordDuration = 10f;

    public float ashenSwordDamageMultiplier = 1.5f;

    // Flame particles per second along the blade (sparks and ash scale from it), and the blade's light.
    public float ashenSwordFlameRate = 140f;

    public float ashenSwordLightIntensity = 2.5f;

    public float ashenSwordLightRange = 2.5f;

    [Header("Blind")]
    // Picks the enemy nearest the crosshair within aimRadius of it, or failing that the nearest one ahead.
    public float blindRange = 12f;

    public float blindAimRadius = 2.5f;

    public float blindDuration = 4f;

    public float blindProjectileSpeed = 16f;

    // Playback speed of the throw, the clip-seconds at which the ash leaves the hand, and when control comes back.
    public float throwSpeed = 1.3f;

    public float throwReleaseTime = 0.35f;

    public float throwDuration = 0.75f;

    [Range(0f, 1f)]
    public float throwMoveMultiplier = 0.5f;

    [Header("Surge")]
    public float surgeDistance = 4f;

    public float surgeDuration = 0.3f;

    // Fraction of the distance covered over the burst's normalized time.
    public AnimationCurve surgeCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(0.6f, 0.85f, 1f, 1f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("Ashfall")]
    public float ashfallRadius = 6f;

    public float ashfallDamage = 5f;

    // Clip-seconds of the trimmed casting clip: the hand meets the ground, and the body is up again.
    public float ashfallSpeed = 1f;

    public float ashfallImpactTime = 0.47f;

    public float ashfallDuration = 1.5f;

    public float ashfallShake = 0.4f;

    public float ashfallHitStop = 0.1f;

    [Header("Rekindle")]
    // Share of max health it gives back, at the end of the channel.
    [Range(0f, 1f)]
    public float rekindleHeal = 0.35f;

    public float rekindleSpeed = 1.6f;

    // Clip-seconds of the power-up clip: the heal lands as the hands come back up, then control returns.
    public float rekindleHealTime = 1.7f;

    public float rekindleDuration = 2.2f;

    [Range(0f, 1f)]
    public float rekindleMoveMultiplier = 0.3f;

    [Header("Arm")]
    // The glow along the right forearm and hand (Kneel/Ember Glow). Steady and saturated, unlike the lantern.
    [ColorUsage(false, true)]
    public Color emberColor = new Color(3f, 0.9f, 0.25f);

    // Glow brightness while filling (at the top of the fill) and once full.
    public float fillingIntensity = 0.6f;

    public float fullIntensity = 1.6f;

    // How far outside the skin the glow sits, in metres.
    public float shellOffset = 0.012f;

    // The point light on the hand. Off until the meter is full, so light on the ground means ready.
    public float handLightIntensity = 1.5f;

    public float handLightRange = 1.6f;

    // Sparks clinging to the arm and dark ash wisps trailing off it, per second at full.
    public float emberRate = 18f;

    public float wispRate = 8f;

    // Strength of the full-screen grade while full (Kneel/Ash Grade): everything but reds and oranges loses colour.
    [Range(0f, 1f)]
    public float gradeStrength = 0.6f;

    [Header("Audio")]
    // Placeholders are generated when these are empty.
    public AudioClip fullClip;

    public AudioClip humClip;

    public AudioClip fizzleClip;

    public AudioClip lockedClip;

    public AudioClip spendClip;

    public AudioClip siphonClip;

    public AudioClip unlockClip;

    [Range(0f, 1f)]
    public float volume = 0.8f;

    [Range(0f, 1f)]
    public float humVolume = 0.35f;

    [Header("Assets")]
    public Material glowMaterial;

    public Material emberParticleMaterial;

    public Material ashParticleMaterial;

    public Material gradeMaterial;
}
