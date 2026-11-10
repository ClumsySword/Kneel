using System.Collections.Generic;
using UnityEngine;

// The Master, Phase 2 (L5's boss, ../Obsidian/02-Design/Enemies/The Master.md §6): the Ash Colossus, 6 m, hunched,
// the King's sword crusted into his right fist and a huge ember claw for a left hand. Same state-machine pattern as
// MasterP1: one EnemyState per behaviour, each setting the animator bool of its name; MasterBoss raises him out of the
// Unmaking and engages him; tuning is MasterSettings' Phase 2 sections.
//
// Rules carried here (§1, §6):
// - Ash armour: a light does lightArmour of its damage and no poise; heavies and bashes land in full. In a wind-up a
//   hit does windupDamageMultiplier on top (the cuts multiply). A Topple ignores the armour.
// - No flinch. A poise break is a Topple (waiting, in a move, for the blow to end); so are three parries running (the
//   count resets when one of his blows takes Kael's health, block chip included, or after a Topple).
// - Only hazards (a panel giving way, a statue) and a broken Gather stop a wind-up. The bash only breaks the Gather.
public class AshColossus : MasterBody, IDamageable, IParryable, IStaggerable
{
    public enum MoveKind { Cleave, Sweep, Grasp, Hammerfall, Leap, GreatCommand, Gather, ThreadLash }

    [Header("Rig (set by MasterSetup)")]
    // Clip seconds at which each blow lands on his rig, measured with the animator running; the move states play each
    // clip at the speed that puts that moment at the end of its tell.
    public float cleaveContact = 0.9f;
    public float sweepContact = 1.2f;
    public float graspContact = 1.4f;
    public float hammerContact = 1.6f;
    // Leap: the feet leave the floor, and land.
    public float leapTakeoff = 1.1f;
    public float leapLand = 1.7f;
    public float commandRelease = 0.9f;
    public float gatherDig = 1f;
    public float lashRelease = 1f;
    // Topple: the clip second he's down on his hands (the cut's end, played back in reverse for the get-up).
    public float toppleDown = 1.5f;
    public float flexLength = 4.67f;
    public float gatherLength = 3.25f;
    public float recoilLength = 1.37f;

    // The mask (the Great Command's light and the transition's split).
    public Transform mask;

    [Header("Effects (the hall's own sprites)")]
    public Material smokeMaterial;
    public Material emberMaterial;
    public Material shadowMaterial;
    // The Hammerfall's lava cracks (Kneel/LavaCrack) and a readable copy of their texture.
    public Material crackMaterial;
    public Texture2D crackMap;
    public Mesh debrisMesh;
    public Material debrisMaterial;

    // The Hammerfall's cracks now on the floor (for the playtest).
    public LavaCracks LastCracks { get; set; }

    // Raised for the hall and the Ash hooks (§9).
    public event System.Action<float> OnCommandTell;
    public event System.Action OnGatherStart;
    public event System.Action OnGatherComplete;
    public event System.Action OnTopple;
    public event System.Action OnHalfHealth;
    public event System.Action OnDefeated;
    // A Hammerfall's impact, with where it landed (the hall cracks the panel under it).
    public event System.Action<Vector3> OnHammerfall;
    // A Cleave's blade in the floor, with where it struck.
    public event System.Action<Vector3> OnCleave;

    public ColossusRiseState riseState { get; private set; }
    public ColossusApproachState approachState { get; private set; }
    public ColossusCircleState circleState { get; private set; }
    public ColossusMoveState cleaveState { get; private set; }
    public ColossusMoveState sweepState { get; private set; }
    public ColossusMoveState graspState { get; private set; }
    public ColossusMoveState hammerState { get; private set; }
    public ColossusMoveState leapState { get; private set; }
    public ColossusMoveState commandState { get; private set; }
    public ColossusMoveState gatherState { get; private set; }
    public ColossusMoveState lashState { get; private set; }
    public ColossusRecoilState recoilState { get; private set; }
    public ColossusToppleState toppleState { get; private set; }
    public ColossusRoarState roarState { get; private set; }
    public ColossusFallenState fallenState { get; private set; }

    public override bool IsDead => stateMachine.currentState == fallenState;

    // Hits on a Topple are critical (the parry recoil is too short a stagger for a riposte).
    public bool IsStaggered => stateMachine.currentState == toppleState;

    public bool IsToppled => stateMachine.currentState == toppleState;

    // Parries since one of his blows last hurt Kael (three topple him).
    public int ParryStreak { get; private set; }

    // For MasterPlaytest: the next move, instead of the picker's (cleared once used).
    public static MoveKind? ForceNext;

    private static readonly MoveKind[] Opening = { MoveKind.GreatCommand, MoveKind.Cleave, MoveKind.Hammerfall };
    private int openingIndex;
    private readonly List<MoveKind> history = new List<MoveKind>();
    private readonly Dictionary<MoveKind, float> readyAt = new Dictionary<MoveKind, float>();
    private bool firstCommandDone;
    private bool halfReached;
    private bool pendingTopple;
    private MoveKind? deferred;

    public MoveKind Pending { get; private set; }

    public int HeldForHazards { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        riseState = new ColossusRiseState(this, stateMachine, "Rise");
        approachState = new ColossusApproachState(this, stateMachine, "Move");
        circleState = new ColossusCircleState(this, stateMachine, "Circle");
        cleaveState = new ColossusMoveState(this, stateMachine, "Cleave", MoveKind.Cleave);
        sweepState = new ColossusMoveState(this, stateMachine, "Sweep", MoveKind.Sweep);
        graspState = new ColossusMoveState(this, stateMachine, "Grasp", MoveKind.Grasp);
        hammerState = new ColossusMoveState(this, stateMachine, "Hammerfall", MoveKind.Hammerfall);
        leapState = new ColossusMoveState(this, stateMachine, "Leap", MoveKind.Leap);
        commandState = new ColossusMoveState(this, stateMachine, "Command", MoveKind.GreatCommand);
        gatherState = new ColossusMoveState(this, stateMachine, "Gather", MoveKind.Gather);
        lashState = new ColossusMoveState(this, stateMachine, "ThreadLash", MoveKind.ThreadLash);
        recoilState = new ColossusRecoilState(this, stateMachine, "Recoil");
        toppleState = new ColossusToppleState(this, stateMachine, "Topple");
        roarState = new ColossusRoarState(this, stateMachine, "Roar");
        fallenState = new ColossusFallenState(this, stateMachine, "Fallen");
    }

    protected override void Start()
    {
        base.Start();
        ApplySettings();
        health.SetMax(settings.p2MaxHealth, true);
        Untouchable = true;
        if (stateMachine.currentState == null)
        {
            stateMachine.Initialize(circleState);
            circleState.Hold(9999f);
        }

        OnBlowLanded += damaged =>
        {
            if (damaged)
                ParryStreak = 0;
        };
    }

    protected override void Update()
    {
        ApplySettings();
        base.Update();
        stateMachine.currentState.Update();
    }

    private void ApplySettings()
    {
        poise.maxPoise = settings.p2MaxPoise;
        poise.regenDelay = settings.p2PoiseRegenDelay;
        poise.regenRate = settings.p2PoiseRegenRate;
        windupPoiseMultiplier = 1f;
        turnSpeed = settings.p2TurnSpeed;
    }

    // --- Beats MasterBoss starts ---

    // In the hall from the start but out of it until the Unmaking raises him: no body, no collider, no agent, and
    // nothing in the hall looks for him.
    public bool Present { get; private set; } = true;

    public void Hide()
    {
        SetPresent(false);
        Present = false;
        Untouchable = true;
        Engaged = false;
        Kneel.FightClearance.Subjects.Remove(transform);
        HallActors.Bosses.Remove(this);
    }

    public void Show()
    {
        SetPresent(true);
        Present = true;
        if (Kneel.FightClearance.Subjects.Contains(transform) == false)
            Kneel.FightClearance.Subjects.Add(transform);
        if (HallActors.Bosses.Contains(this) == false)
            HallActors.Bosses.Add(this);
    }

    // Out of the vortex: crouched where he was raised, rising (the Flex), untouchable.
    public void RiseAt(Vector3 position, Quaternion rotation, float seconds)
    {
        if (agent.enabled) agent.Warp(position); else transform.position = position;
        transform.rotation = rotation;
        Engaged = false;
        Untouchable = true;
        riseState.Duration = seconds;
        stateMachine.ChangeState(riseState);
    }

    // Input returns (§5, 10 s): the fight starts with the fixed opening.
    public void Engage()
    {
        Engaged = true;
        Untouchable = false;
        openingIndex = 0;
        history.Clear();
        readyAt.Clear();
        firstCommandDone = false;
        halfReached = false;
        pendingTopple = false;
        deferred = null;
        ParryStreak = 0;
        poise.Reset();
        circleState.Hold(0.2f);
        stateMachine.ChangeState(circleState);
    }

    // Back out of the fight (a retry, a reset): hidden by MasterBoss.
    public void Disengage()
    {
        Engaged = false;
        Untouchable = true;
        ForceNext = null;
        health.SetMax(settings.p2MaxHealth, true);
        stateMachine.ChangeState(circleState);
        circleState.Hold(9999f);
    }

    // --- The picker (§6) ---

    public MoveKind NextMove()
    {
        if (ForceNext.HasValue)
        {
            MoveKind forced = ForceNext.Value;
            ForceNext = null;
            return forced;
        }

        if (openingIndex < Opening.Length)
            return Opening[openingIndex++];

        float distance = PlayerDistance();
        var weights = new List<(MoveKind kind, float weight)>();
        if (distance < settings.p2BandNear)
            weights.AddRange(new[] { (MoveKind.Cleave, 30f), (MoveKind.Sweep, 30f), (MoveKind.Grasp, 20f), (MoveKind.Hammerfall, 20f) });
        else if (distance < settings.p2BandMid)
            weights.AddRange(new[] { (MoveKind.Cleave, 25f), (MoveKind.Grasp, 25f), (MoveKind.Hammerfall, 20f), (MoveKind.GreatCommand, 15f), (MoveKind.Gather, 15f) });
        else
            weights.AddRange(new[] { (MoveKind.Leap, 35f), (MoveKind.ThreadLash, 35f), (MoveKind.GreatCommand, 30f) });

        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            MoveKind kind = weights[i].kind;
            bool blocked = Time.time < ReadyAt(kind) || ThreeRunning(kind)
                || (kind == MoveKind.Gather && health.Fraction >= settings.gatherBelow)
                || (kind == MoveKind.ThreadLash && distance < settings.lashMinRange);
            if (blocked) weights[i] = (kind, 0f);
            total += weights[i].weight;
        }

        // Nothing ready in this band: he walks in for a Cleave (a Sweep after two).
        if (total <= 0f)
            return ThreeRunning(MoveKind.Cleave) ? MoveKind.Sweep : MoveKind.Cleave;

        float roll = Random.value * total;
        foreach (var (kind, weight) in weights)
        {
            if (roll < weight) return kind;
            roll -= weight;
        }

        return weights[weights.Count - 1].kind;
    }

    private float ReadyAt(MoveKind kind) => readyAt.TryGetValue(kind, out float t) ? t : 0f;

    private bool ThreeRunning(MoveKind kind) =>
        history.Count >= 2 && history[history.Count - 1] == kind && history[history.Count - 2] == kind;

    // How close he has to be to start a move (he walks into reach first).
    public float StartRange(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Cleave: return settings.cleaveStart + settings.cleaveLength - 0.8f;
            case MoveKind.Sweep: return settings.sweepReach - 0.8f;
            case MoveKind.Grasp: return settings.graspHandOut + settings.graspLunge;
            case MoveKind.Hammerfall: return settings.hammerOut + settings.hammerRadius - 0.5f;
            default: return 99f;
        }
    }

    public ColossusMoveState StateFor(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Cleave: return cleaveState;
            case MoveKind.Sweep: return sweepState;
            case MoveKind.Grasp: return graspState;
            case MoveKind.Hammerfall: return hammerState;
            case MoveKind.Leap: return leapState;
            case MoveKind.GreatCommand: return commandState;
            case MoveKind.Gather: return gatherState;
            default: return lashState;
        }
    }

    // When a move's first blow lands, from its start (-1 for the Gather, which has none).
    public float FirstContact(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Cleave: return settings.cleaveTell;
            case MoveKind.Sweep: return settings.sweepTell;
            case MoveKind.Grasp: return settings.graspTell;
            case MoveKind.Hammerfall: return settings.hammerTell;
            case MoveKind.Leap: return settings.leapTell;
            case MoveKind.GreatCommand: return firstCommandDone ? settings.greatCommandTellAgain : settings.greatCommandTell;
            case MoveKind.ThreadLash: return settings.lashTell;
            default: return -1f;
        }
    }

    public void BeginNextMove()
    {
        if (TryHalfBeat())
            return;

        if (deferred.HasValue)
        {
            Pending = deferred.Value;
            deferred = null;
        }
        else
        {
            Pending = NextMove();
        }

        approachState.NotBefore = 0f;
        if (PlayerDistance() <= StartRange(Pending) && OnScreen(3f))
            StartMove(Pending);
        else
            stateMachine.ChangeState(approachState);
    }

    // Walks into reach for `kind` and starts it no earlier than `notBefore` (the Great Command's Hammerfall).
    public void ApproachFor(MoveKind kind, float notBefore)
    {
        Pending = kind;
        approachState.NotBefore = notBefore;
        stateMachine.ChangeState(approachState);
    }

    public void StartMove(MoveKind kind)
    {
        // The pacing rule (§8): never a blow landing within 0.5 s of a hazard's impact.
        float contact = FirstContact(kind);
        if (contact >= 0f && HazardImpactNear != null && HazardImpactNear(Time.time + contact))
        {
            HeldForHazards++;
            deferred = kind;
            circleState.Hold(0.25f);
            stateMachine.ChangeState(circleState);
            return;
        }

        history.Add(kind);
        if (history.Count > 4) history.RemoveAt(0);
        switch (kind)
        {
            case MoveKind.GreatCommand: readyAt[kind] = Time.time + settings.greatCommandCooldown; break;
            case MoveKind.Gather: readyAt[kind] = Time.time + settings.gatherCooldown; break;
            case MoveKind.Leap: readyAt[kind] = Time.time + settings.leapCooldown; break;
            case MoveKind.ThreadLash: readyAt[kind] = Time.time + settings.lashCooldown; break;
            case MoveKind.Grasp: readyAt[kind] = Time.time + settings.graspCooldown; break;
        }

        stateMachine.ChangeState(StateFor(kind));
    }

    public float CommandTell()
    {
        float tell = firstCommandDone ? settings.greatCommandTellAgain : settings.greatCommandTell;
        firstCommandDone = true;
        return tell;
    }

    // A move ended: a deferred Topple or the 50% beat first, then a pause and the next move.
    public void MoveFinished()
    {
        if (IsDead)
            return;

        if (pendingTopple)
        {
            pendingTopple = false;
            Topple();
            return;
        }

        if (TryHalfBeat())
            return;

        circleState.Hold(Random.Range(settings.p2PauseMin, settings.p2PauseMax));
        stateMachine.ChangeState(circleState);
    }

    private bool TryHalfBeat()
    {
        if (halfReached || health.Fraction > 0.5f || IsDead || Engaged == false)
            return false;

        halfReached = true;
        stateMachine.ChangeState(roarState);
        OnHalfHealth?.Invoke();
        return true;
    }

    public void Topple()
    {
        ParryStreak = 0;
        pendingTopple = false;
        toppleState.Begin(settings.toppleTime);
        OnTopple?.Invoke();
    }

    // --- Taking hits ---

    public void TakeHit(DamageInfo info)
    {
        if (IsDead || Untouchable)
            return;

        ColossusMoveState move = stateMachine.currentState as ColossusMoveState;
        bool toppled = IsToppled;
        bool light = info.heavy == false && info.interrupts == false && info.isEnvironmental == false;
        float amount = info.amount;
        float poiseDamage = info.poiseDamage;
        if (toppled == false)
        {
            if (light)
            {
                amount *= settings.lightArmour;
                poiseDamage = 0f;
            }

            if (move != null && move.Armoured)
                amount *= settings.windupDamageMultiplier;
        }

        // The Gather breaks under the player's raw damage (heavies double) or two bashes on the planted claw.
        bool gatherBroken = move != null && move.Kind == MoveKind.Gather && info.isEnvironmental == false
            && move.CountGatherHit(info.amount * (info.heavy ? settings.gatherHeavyWeight : 1f), info.interrupts);

        health.ApplyDamage(amount);
        if (info.isEnvironmental == false)
            Flash();

        if (health.Current <= 0f)
        {
            Fall();
            return;
        }

        if (gatherBroken)
        {
            bossTell?.GuardBroken();
            Topple();
            return;
        }

        if (info.isEnvironmental || toppled || stateMachine.currentState == recoilState || stateMachine.currentState == roarState)
            return;

        if (poise.ApplyDamage(poiseDamage))
        {
            // In a move the blow carries on, and the Topple waits for it to end (§1).
            if (move != null) pendingTopple = true;
            else Topple();
        }
    }

    // A hazard (a panel giving way under him, a statue): it breaks any wind-up into a Topple.
    public override void HazardHit(float damage, float stagger)
    {
        if (IsDead || Untouchable)
            return;

        health.ApplyDamage(damage);
        Flash();
        if (health.Current <= 0f)
        {
            Fall();
            return;
        }

        if (stagger > 0f && stateMachine.currentState != roarState)
            Topple();
    }

    public void OnParried(GameObject parrier)
    {
        if (IsDead || Untouchable)
            return;

        // Too big to be driven to his knees by one: he recoils and loses poise; the third running topples him.
        ParryStreak++;
        if (ParryStreak >= settings.parriesToTopple || poise.ApplyDamage(settings.recoilPoise))
        {
            Topple();
            return;
        }

        recoilState.Begin(settings.recoilTime);
    }

    private void Fall()
    {
        if (IsDead)
            return;
        stateMachine.ChangeState(fallenState);
        OnDefeated?.Invoke();
    }

    // --- Helpers for the states ---

    public void RaiseCommandTell(float tell) => OnCommandTell?.Invoke(tell);

    public void RaiseGatherStart() => OnGatherStart?.Invoke();

    public void RaiseGatherComplete() => OnGatherComplete?.Invoke();

    public void RaiseHammerfall(Vector3 at) => OnHammerfall?.Invoke(at);

    public void RaiseCleave(Vector3 at) => OnCleave?.Invoke(at);

    public void Heal(float fraction) => health.Heal(health.Max * fraction);

    // --- The Cleave's blade in the floor ---

    // While set, the sword is turned about his fist so its tip sinks into the floor ahead of him (§6: "the blade sticks
    // in the floor"). The Decree clip's follow-through carries the blade under him, so the stuck blade is posed here.
    private bool bladeStuck;
    private float bladeWeight;

    public void StickBlade(bool stuck) => bladeStuck = stuck;

    public bool BladeStuck => bladeStuck;

    private void LateUpdate()
    {
        bladeWeight = Mathf.MoveTowards(bladeWeight, bladeStuck ? 1f : 0f, Time.deltaTime * (bladeStuck ? 12f : 4f));
        if (bladeWeight <= 0f || swordTip == null || BodyVisible == false)
            return;

        Transform sword = swordTip.parent;
        Vector3 grip = sword.position;
        float length = Vector3.Distance(grip, swordTip.position);
        float floor = transform.position.y - 0.35f;
        float drop = Mathf.Max(0.1f, grip.y - floor);
        Vector3 ahead = transform.forward;
        float reach = drop < length ? Mathf.Sqrt(length * length - drop * drop) : 0f;
        Vector3 into = new Vector3(grip.x, floor, grip.z) + ahead * reach;
        Vector3 want = (into - grip).normalized;
        Quaternion turn = Quaternion.FromToRotation(swordTip.position - grip, want);
        sword.rotation = Quaternion.Slerp(Quaternion.identity, turn, bladeWeight) * sword.rotation;
    }
}
