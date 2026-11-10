using UnityEngine;

// Tuning for the Master (L5's boss), read every frame so edits apply live; the F1 overlay's "Master" section writes
// into it. Starting values are the design note's (../Obsidian/02-Design/Enemies/The Master.md, §3-4); distances in
// metres (R = 3 m), times in seconds.
[CreateAssetMenu(fileName = "MasterSettings", menuName = "Kneel/Master Settings")]
public class MasterSettings : ScriptableObject
{
    [Header("Phase 1: body")]
    public float maxHealth = 450f;
    public float maxPoise = 100f;
    public float poiseRegenDelay = 3f;
    public float poiseRegenRate = 12f;
    public float walkSpeed = 2.2f;
    // He strides (the walk, faster) to close distance and never attacks out of a stride.
    public float strideSpeed = 4.5f;
    // Turn rate while walking or tracking a wind-up (Enemy.turnSpeed's lerp factor per second).
    public float turnSpeed = 5f;
    // Between strings he circles for a random time in this range.
    public float pauseMin = 1f;
    public float pauseMax = 2f;

    [Header("Phase 1: reactions")]
    // Hits landing during an armoured wind-up do this share of their damage, and never flinch him (§1).
    public float windupDamageMultiplier = 0.5f;
    // A parry drives him to one knee for this long; every hit in it is critical.
    public float parryKneelTime = 2f;
    // Poise break: a stagger in which hits crit (never during a wind-up: it waits for the blow to end).
    public float poiseStaggerTime = 1.5f;
    // A hazard breaks his wind-up (braziers and chandeliers set their own stagger; this is the default).
    public float hazardStaggerTime = 2f;
    // A hit outside any move makes him flinch, at most this often.
    public float flinchCooldown = 2.5f;

    [Header("Picker (§4)")]
    // Range bands: under bandNear, bandNear-bandMid, bandMid-bandFar, beyond bandFar (he strides in).
    public float bandNear = 4.5f;
    public float bandMid = 9f;
    public float bandFar = 18f;
    // Threads only beyond this.
    public float threadsMinRange = 12f;
    public float commandCooldown = 14f;
    public float guardCooldown = 10f;
    public float seizeCooldown = 5f;
    public float threadsCooldown = 6f;
    public float threadsCooldownLate = 4f;

    [Header("Decree: overhead (glint)")]
    public float decreeTell = 0.8f;
    public float decreeDamage = 35f;
    public float decreeReach = 3.5f;
    public float decreeArc = 40f;
    public float decreePunish = 1.2f;

    [Header("Sentence: three sweeps (glint each)")]
    public float sentenceTell1 = 0.7f;
    public float sentenceTell2 = 0.5f;
    public float sentenceTell3 = 1.2f;
    public float sentenceDamage = 30f;
    public float sentenceReach = 4f;
    public float sentenceArc = 150f;
    public float sentencePunish = 1.5f;

    [Header("Seize: grab (ember)")]
    public float seizeTell = 0.9f;
    // Direction locks this long before he lunges.
    public float seizeLock = 0.3f;
    public float seizeLunge = 6f;
    public float seizeLungeTime = 0.25f;
    public float seizeGrabRadius = 1.6f;
    public float seizeDamage = 55f;
    public float seizeKneel = 1.5f;
    public float seizePunish = 1.5f;

    [Header("Command: 'Kneel' (palm light)")]
    public float commandTell = 2.5f;
    public float commandTellFirst = 3f;
    public float commandDamage = 25f;
    public float commandKneel = 1.5f;
    public float commandCone = 160f;
    public float commandPunish = 1.5f;
    // The Decree that follows lands this long after the player can move again, so it's always escapable.
    public float commandFollowupGap = 0.35f;

    [Header("Threads: three ember lines")]
    public float threadsTell = 0.8f;
    public float threadSpeed = 9f;
    public float threadLength = 18f;
    public float threadWidth = 1f;
    public float threadFan = 12f;
    public float threadDamage = 20f;

    [Header("Regal Guard + Thrust (glint)")]
    public float guardMaxTime = 3f;
    public float guardArc = 60f;
    public float guardTurnRate = 90f;
    public float guardWalkSpeed = 1.2f;
    // A heavy into the guard still knocks off this share of its poise damage.
    public float guardHeavyPoise = 0.5f;
    public float guardBrokenStagger = 2f;
    public float thrustTell = 0.6f;
    public float thrustDamage = 40f;
    public float thrustReach = 4f;
    public float thrustWidth = 1.2f;
    public float thrustPunish = 2f;

    [Header("50% beat")]
    public float pommelTime = 3f;

    [Header("Intro (§3)")]
    // From crossing the aggro line: the palm and the pull, the scar flare, then he stands and walks down the dais.
    public float introPullTime = 1.5f;
    public float introPullSpeed = 0.3f;
    public float introFlareTime = 1f;
    public float introStandTime = 5f;
    public float introWalkTime = 3.5f;
    public float introBarFillTime = 1f;
    public float retryIntroTime = 4f;
    public float skipHoldTime = 0.5f;
}
