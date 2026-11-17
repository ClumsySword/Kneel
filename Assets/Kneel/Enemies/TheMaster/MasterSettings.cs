using UnityEngine;

// Tuning for the Master (L5's boss), read every frame so edits apply live; the F1 overlay's "Master" section writes
// into it. Starting values are the design note's (../Obsidian/02-Design/Enemies/The Master.md, §3-4); distances in
// metres (R = 3 m), times in seconds.
[CreateAssetMenu(fileName = "MasterSettings", menuName = "Kneel/Master Settings")]
public class MasterSettings : ScriptableObject
{
    [Header("Looks shared by both bodies (set by MasterSetup)")]
    // The aim ember's particle material (the cursor's glow): the Commands' ring and the fire rain's rings are made of it.
    public Material emberMaterial;
    // The Hammerfall's lava cracks, smaller: where the Commands' fire, the rain and the Leap strike the floor.
    public Material strikeCrackMaterial;
    public Texture2D strikeCrackMap;

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
    // Owner, 2026-11-15: the player's blows only take his health while he's staggered (down on one knee). Outside it they
    // fill his stagger meter (his poise, shown under the bar) instead. Hazards still burn and crush him as before.
    // Hits landing during an armoured wind-up do this share of their damage (to his poise), and never flinch him (§1).
    public float windupDamageMultiplier = 0.5f;
    // A parry knocks him reeling for this long (quick: not a damage window), and fills this much of the stagger meter.
    public float parryKneelTime = 0.9f;
    public float parryPoise = 34f;
    // The stagger (the meter full, a broken guard): down on one knee for this long. Every hit in it is critical, and
    // it's the only time the player's blows take his health. Never during a wind-up: it waits for the blow to end.
    public float poiseStaggerTime = 3.2f;
    // A hazard breaks his wind-up (the braziers set their own stagger; this is the default).
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
    // After the third sweep's follow-through has played out (owner, 2026-11-15: he held its last pose too long).
    public float sentencePunish = 0.2f;

    [Header("Command: 'Kneel' (a strike of fire from above)")]
    // Owner, 2026-11-15: a circle of cracks follows Kael through the tell and locks commandLock before the strike, so
    // only a roll at the last moment gets out of it. Inside it: commandDamage and a forced kneel.
    public float commandTell = 2.5f;
    public float commandTellFirst = 3f;
    public float commandDamage = 25f;
    public float commandKneel = 1.5f;
    public float commandRadius = 1.8f;
    public float commandLock = 0.3f;
    public float commandPunish = 1.5f;
    // The Decree that follows lands this long after the player can move again, so it's always escapable.
    public float commandFollowupGap = 0.35f;

    [Header("Threads: three ember lines")]
    public float threadsTell = 0.8f;
    // The lanes' floor mark (SpellMark) follows the player until this long before the release, then holds: the threads
    // run down the lanes it drew. Shorter is harder.
    public float threadsLock = 0.3f;
    public float threadSpeed = 9f;
    public float threadLength = 18f;
    public float threadWidth = 1f;
    public float threadFan = 12f;
    public float threadDamage = 20f;
    // The fire a thread (or a Lash line) leaves: this share of its width, burning this long (owner, 2026-11-15: smaller,
    // and gone sooner).
    public float threadFireWidth = 0.5f;
    public float threadFireTime = 3.5f;

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
    public int threadRibbons = 60;

    [Header("Phase 2: the Ash Colossus (§6)")]
    public float p2MaxHealth = 650f;
    public float p2MaxPoise = 160f;
    public float p2PoiseRegenDelay = 3f;
    public float p2PoiseRegenRate = 15f;
    // Owner, 2026-11-15: 1.8 read as slow motion (his walk clip covers 4.85 m/s on him: 1.8 played it at 0.37x; 3.2 is 0.66x).
    public float p2WalkSpeed = 3.2f;
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
    // He no longer drags the blade out of the floor (owner, 2026-11-15); the follow-through plays out, then this.
    public float cleavePunish = 1.2f;

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
    // The claw closes this far ahead of him, after a lunge of up to graspLunge along the locked line. The lunge runs over
    // the reach (graspLungeTime), not a slide (owner, 2026-11-15: "make sure the anim is clean").
    public float graspHandOut = 4.5f;
    public float graspLunge = 3f;
    public float graspLungeTime = 0.5f;
    public float graspRadius = 2f;
    public float graspDamage = 60f;
    public float graspHold = 0.6f;
    public float graspFling = 6f;
    // From the grab: the rest of the (cut) clip, then his stance.
    public float graspPunish = 1.2f;

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

    [Header("Great Command (a strike of fire from above, as the Command)")]
    public float greatCommandTell = 3.5f;
    public float greatCommandTellAgain = 3f;
    public float greatCommandDamage = 30f;
    public float greatCommandKneel = 2f;
    public float greatCommandRadius = 2.4f;
    public float greatCommandLock = 0.3f;
    public float greatCommandPunish = 2f;
    // The Hammerfall that follows lands this long after the player can move again.
    public float greatCommandFollowupGap = 0.35f;

    [Header("Gather (owner, 2026-11-15: he kneels, fire rains on the arena, the player only dodges)")]
    // From going down to standing again (the rain falls for about 10 s of it); untouchable throughout; heals gatherHeal at the end.
    public float gatherTime = 12f;
    public float gatherHeal = 0.08f;
    // The rain: a strike every rainIntervalStart seconds at first, quickening to rainIntervalEnd; each marked by an
    // ember ring for rainTell (the fire falls over the last 0.3 s of it). Every rainAtPlayer-th lands where Kael stands.
    public float rainIntervalStart = 0.75f;
    public float rainIntervalEnd = 0.35f;
    public float rainTell = 1.1f;
    public float rainRadius = 1.6f;
    public float rainDamage = 20f;
    public int rainAtPlayer = 3;
    // The threads into him: a new batch every gatherThreadEvery seconds, gatherThreadBatch at a time.
    public float gatherThreadEvery = 1.6f;
    public int gatherThreadBatch = 8;

    [Header("Thread Lash")]
    public float lashTell = 1f;
    // As threadsLock, for the Lash's five lanes.
    public float lashLock = 0.3f;
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
