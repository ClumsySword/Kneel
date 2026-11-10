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

    [Header("Transition: the Unmaking (§5)")]
    public float unmakingTime = 10f;
    // Where the Colossus may rise: the nearest NavMesh point this clear of any edge, at most this far from where he knelt.
    public float colossusClearance = 3.5f;
    public float colossusMaxOffset = 4f;
    // The floor's ring at the mask split pushes a player inside it out to this distance (3 R).
    public float ringPush = 9f;
    // A chandelier the transition drops shoves a player under it this far clear (1 R), with no damage.
    public float chandelierShove = 3f;
    public int threadRibbons = 60;

    [Header("Phase 2: the Ash Colossus (§6)")]
    public float p2MaxHealth = 650f;
    public float p2MaxPoise = 160f;
    public float p2PoiseRegenDelay = 3f;
    public float p2PoiseRegenRate = 15f;
    public float p2WalkSpeed = 1.8f;
    public float p2TurnSpeed = 3.5f;
    public float p2PauseMin = 0.8f;
    public float p2PauseMax = 1.5f;
    // Ash armour: a light (not a heavy, not a bash) does this share of its damage and no poise. A Topple ignores it.
    public float lightArmour = 0.4f;
    // Poise break or three parries running: he falls forward onto his hands, hits critical.
    public float toppleTime = 3.5f;
    // A parry (only the Cleave has a glint): he recoils and loses some poise.
    public float recoilTime = 1.2f;
    public float recoilPoise = 25f;
    public int parriesToTopple = 3;
    // A hazard landing on him (a panel giving way, a statue) breaks a wind-up into a Topple.

    [Header("Phase 2: picker (§6)")]
    public float p2BandNear = 6f;
    public float p2BandMid = 12f;
    public float greatCommandCooldown = 15f;
    public float gatherCooldown = 30f;
    // Gather only below this share of his health.
    public float gatherBelow = 0.75f;
    public float leapCooldown = 6f;
    public float lashCooldown = 6f;
    public float graspCooldown = 5f;

    [Header("Cleave (glint)")]
    public float cleaveTell = 1f;
    public float cleaveDamage = 45f;
    // A line along the blade's fall: from cleaveStart to cleaveStart + cleaveLength ahead of him, cleaveWidth wide.
    public float cleaveStart = 1.5f;
    public float cleaveLength = 5f;
    public float cleaveWidth = 1.5f;
    public float cleavePunish = 1.8f;

    [Header("Reaping Sweep (ember)")]
    public float sweepTell = 1.1f;
    public float sweepDamage = 40f;
    public float sweepReach = 6f;
    public float sweepArc = 180f;
    public float sweepFling = 2f;
    public float sweepPunish = 1.5f;

    [Header("Grasp (ember)")]
    public float graspTell = 1f;
    public float graspLock = 0.3f;
    // The claw closes this far ahead of him, after a lunge of up to graspLunge along the locked line (3 R in all).
    public float graspHandOut = 4.5f;
    public float graspLunge = 4.5f;
    public float graspLungeTime = 0.3f;
    public float graspRadius = 2f;
    public float graspDamage = 60f;
    public float graspHold = 0.6f;
    public float graspFling = 6f;
    public float graspPunish = 2f;

    [Header("Hammerfall (no glint)")]
    public float hammerTell = 1.4f;
    // The fists land this far ahead of him.
    public float hammerOut = 3.5f;
    public float hammerRadius = 4f;
    public float hammerDamage = 50f;
    // The lava cracks (owner, 2026-11-10, replacing the burning circle and the ring): they race out from the impact to
    // hammerRingRadius in hammerCrackGrow, glow brighter for hammerCrackBrighten, then burst: hammerCrackDamage to a
    // player standing on one. They fade over hammerCrackFade.
    public float hammerRingRadius = 8f;
    public float hammerCrackGrow = 0.45f;
    public float hammerCrackBrighten = 0.8f;
    public float hammerCrackDamage = 25f;
    public float hammerCrackFade = 1.4f;
    // How near a crack counts as standing on it (m).
    public float hammerCrackMargin = 0.45f;
    // The panel under the impact gives way this long after it.
    public float hammerPanelDelay = 3f;
    public float hammerPunish = 2.5f;

    [Header("Leap")]
    public float leapTell = 1.2f;
    // The target (the shadow) stops following the player this long before he lands.
    public float leapLock = 0.4f;
    public float leapRadius = 4f;
    public float leapDamage = 50f;
    public float leapPunish = 2f;

    [Header("Great Command (mask light)")]
    public float greatCommandTell = 3.5f;
    public float greatCommandTellAgain = 3f;
    public float greatCommandDamage = 30f;
    public float greatCommandKneel = 2f;
    public float greatCommandPunish = 2f;
    // The Hammerfall that follows lands this long after the player can move again.
    public float greatCommandFollowupGap = 0.35f;

    [Header("Gather (channel)")]
    public float gatherTime = 4f;
    public float gatherHeal = 0.08f;
    // Broken by this much of the player's raw damage (heavies count double), or this many bashes: he Topples.
    public float gatherBreakDamage = 60f;
    public float gatherHeavyWeight = 2f;
    public int gatherBreakBashes = 2;

    [Header("Thread Lash")]
    public float lashTell = 1f;
    public int lashCount = 5;
    public float lashFan = 12f;
    public float lashLength = 24f;
    public float lashDamage = 25f;
    // Only at a player beyond 4 R.
    public float lashMinRange = 12f;

    [Header("Phase 2: 50% beat")]
    public float roarTime = 3f;
    // In P2, a darkness wave every this many seconds (never during a Great Command).
    public float darknessEveryMin = 35f;
    public float darknessEveryMax = 45f;
}
