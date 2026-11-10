using System.Collections.Generic;
using UnityEngine;

// The Master, Phase 1 (L5's boss, ../Obsidian/02-Design/Enemies/The Master.md §4): a 4.2 m duellist with the King's
// broken sword in his right hand and the curse in his left. Same state-machine pattern as the other enemies: one
// EnemyState per behaviour, each setting an animator bool of its name. MasterBoss owns him and starts each beat
// (seated, rising, the fight, the 50% beat); tuning is MasterSettings.
//
// Rules carried here (§1): hits during an armoured wind-up do windupDamageMultiplier of their damage and never flinch
// him; a poise break waits for the blow to end; only hazards (HazardHit) break a wind-up; the bash only breaks the
// Regal Guard. A parry drives him to one knee (critical window), except on Sentence's first two sweeps (deflected).
public class MasterP1 : MasterBody, IDamageable, IParryable, IStaggerable
{
    public enum MoveKind { Decree, Sentence, Seize, Command, Threads, Guard }

    private static readonly int LeftArmRestId = Animator.StringToHash("LeftArmRest");

    [Header("Rig (set by MasterSetup)")]
    // Clip seconds at which each blow lands on his rig, measured with the animator running (the sword tip's pass in
    // front of him). The move states play each clip at whatever speed puts that moment at the end of its tell.
    public float decreeContact = 0.75f;
    public float[] sentenceContacts = { 0.4f, 0.4f, 0.4f };
    public float seizeContact = 1f;
    public float commandRelease = 1f;
    public float threadsRelease = 0.8f;
    public float thrustContact = 0.9f;
    public float pommelContact = 1.2f;
    // The stagger clip's length (Large Hit Reaction), fitted to each stagger's window.
    public float staggerLength = 1.37f;

    // Raised when a beat starts, for the hall and the Ash hooks (§9): Command tell (seconds), Threads tell, the 50% beat.
    public event System.Action<float> OnCommandTell;
    public event System.Action OnThreadsTell;
    public event System.Action OnHalfHealth;
    public event System.Action OnDefeated;

    public MasterSeatedState seatedState { get; private set; }
    public MasterRiseState riseState { get; private set; }
    public MasterWalkDownState walkDownState { get; private set; }
    public MasterApproachState approachState { get; private set; }
    public MasterCircleState circleState { get; private set; }
    public MasterMoveState decreeState { get; private set; }
    public MasterMoveState sentenceState { get; private set; }
    public MasterMoveState seizeState { get; private set; }
    public MasterMoveState commandState { get; private set; }
    public MasterMoveState threadsState { get; private set; }
    public MasterMoveState guardState { get; private set; }
    public MasterParriedState parriedState { get; private set; }
    public MasterStaggerState staggerState { get; private set; }
    public MasterFlinchState flinchState { get; private set; }
    public MasterPommelState pommelState { get; private set; }
    public MasterFallenState fallenState { get; private set; }

    public override bool IsDead => stateMachine.currentState == fallenState;

    public bool IsStaggered => stateMachine.currentState == parriedState || stateMachine.currentState == staggerState;

    // The opening is fixed; then the picker takes over (§4).
    private static readonly MoveKind[] Opening = { MoveKind.Decree, MoveKind.Command, MoveKind.Sentence, MoveKind.Seize };
    private int openingIndex;
    private readonly List<MoveKind> history = new List<MoveKind>();
    private readonly Dictionary<MoveKind, float> readyAt = new Dictionary<MoveKind, float>();
    private bool firstCommandDone;
    private bool halfReached;
    private bool pendingPoiseBreak;
    private float lastFlinchTime = -99f;

    // The move the approach is walking into reach for.
    public MoveKind Pending { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        seatedState = new MasterSeatedState(this, stateMachine, "Seated");
        riseState = new MasterRiseState(this, stateMachine, "Rise");
        walkDownState = new MasterWalkDownState(this, stateMachine, "Move");
        approachState = new MasterApproachState(this, stateMachine, "Move");
        circleState = new MasterCircleState(this, stateMachine, "Circle");
        decreeState = new MasterMoveState(this, stateMachine, "Decree", MoveKind.Decree);
        sentenceState = new MasterMoveState(this, stateMachine, "Sentence", MoveKind.Sentence);
        seizeState = new MasterMoveState(this, stateMachine, "Seize", MoveKind.Seize);
        commandState = new MasterMoveState(this, stateMachine, "Command", MoveKind.Command);
        threadsState = new MasterMoveState(this, stateMachine, "Threads", MoveKind.Threads);
        guardState = new MasterMoveState(this, stateMachine, "Guard", MoveKind.Guard);
        parriedState = new MasterParriedState(this, stateMachine, "Parried");
        staggerState = new MasterStaggerState(this, stateMachine, "Stagger");
        flinchState = new MasterFlinchState(this, stateMachine, "Flinch");
        pommelState = new MasterPommelState(this, stateMachine, "Pommel");
        fallenState = new MasterFallenState(this, stateMachine, "Fallen");
    }

    protected override void Start()
    {
        base.Start();
        ApplySettings();
        health.SetMax(settings.maxHealth, true);
        stateMachine.Initialize(seatedState);
    }

    protected override void Update()
    {
        ApplySettings();
        base.Update();
        stateMachine.currentState.Update();
    }

    private void ApplySettings()
    {
        poise.maxPoise = settings.maxPoise;
        poise.regenDelay = settings.poiseRegenDelay;
        poise.regenRate = settings.poiseRegenRate;
        turnSpeed = settings.turnSpeed;
        if (Mathf.Approximately(health.Max, settings.maxHealth) == false && Engaged == false)
            health.SetMax(settings.maxHealth, true);
    }

    // --- Beats MasterBoss starts ---

    // Also the retry reset: he waits on the throne, out of the fight.
    public void Seat() { Engaged = false; stateMachine.ChangeState(seatedState); }

    public void Rise(Vector3 daisFoot) { walkDownState.Target = daisFoot; stateMachine.ChangeState(riseState); }

    // Straight to the dais foot (the retry intro): stands there, untouchable, until Engage.
    public void StandAt(Vector3 position, Quaternion rotation)
    {
        if (agent.enabled) agent.Warp(position); else transform.position = position;
        transform.rotation = rotation;
        anim.CrossFadeInFixedTime("Idle", 0f, 0, 0f);
        stateMachine.ChangeState(circleState);
        circleState.Hold(9999f);
        Untouchable = true;
    }

    // The bar has filled: the fight starts with the fixed opening.
    public void Engage()
    {
        Engaged = true;
        Untouchable = false;
        openingIndex = 0;
        history.Clear();
        readyAt.Clear();
        firstCommandDone = false;
        halfReached = false;
        pendingPoiseBreak = false;
        poise.Reset();
        health.SetMax(settings.maxHealth, true);
        circleState.Hold(0.4f);
        stateMachine.ChangeState(circleState);
    }

    // --- The picker (§4) ---

    // The next move: the fixed opening, then weighted by range band, off cooldown, never the same three times running.
    // For MasterPlaytest: the next move, instead of the picker's (cleared once used).
    public static MoveKind? ForceNext;

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
        if (distance < settings.bandNear)
            weights.AddRange(new[] { (MoveKind.Sentence, 35f), (MoveKind.Decree, 25f), (MoveKind.Seize, 25f), (MoveKind.Guard, 15f) });
        else if (distance < settings.bandMid)
            weights.AddRange(new[] { (MoveKind.Seize, 30f), (MoveKind.Decree, 25f), (MoveKind.Guard, 25f), (MoveKind.Command, 20f) });
        else
            weights.AddRange(new[] { (MoveKind.Command, 40f), (MoveKind.Threads, halfReached ? 120f : 60f) });

        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            MoveKind kind = weights[i].kind;
            bool blocked = Time.time < ReadyAt(kind) || ThreeRunning(kind) || (kind == MoveKind.Threads && distance < settings.threadsMinRange);
            if (blocked) weights[i] = (kind, 0f);
            total += weights[i].weight;
        }

        // Nothing in this band is ready (between 9 and 12 m, say, with the Command cooling down): he walks in for a
        // Decree, or a Sentence after two Decrees.
        if (total <= 0f)
            return ThreeRunning(MoveKind.Decree) ? MoveKind.Sentence : MoveKind.Decree;

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

    // How close he has to be to start a move (he walks into reach first, §4).
    public float StartRange(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Decree: return settings.decreeReach - 0.6f;
            case MoveKind.Sentence: return settings.sentenceReach - 0.8f;
            case MoveKind.Seize: return settings.seizeLunge;
            case MoveKind.Guard: return settings.bandMid;
            case MoveKind.Command: return settings.bandFar;
            default: return settings.bandFar;
        }
    }

    public MasterMoveState StateFor(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Decree: return decreeState;
            case MoveKind.Sentence: return sentenceState;
            case MoveKind.Seize: return seizeState;
            case MoveKind.Command: return commandState;
            case MoveKind.Threads: return threadsState;
            default: return guardState;
        }
    }

    // Picks the next move and either starts it or walks into its range.
    public void BeginNextMove()
    {
        if (TryHalfBeat())
            return;

        // A move held back for a hazard's impact (StartMove) comes back first.
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
        approachState.Hurry = false;
        if (PlayerDistance() <= StartRange(Pending) && OnScreen())
            StartMove(Pending);
        else
            stateMachine.ChangeState(approachState);
    }

    // Strides into reach for `kind` and starts it no earlier than `notBefore` (the Command's follow-up Decree, when
    // the kneeling player is out of its reach).
    public void ApproachFor(MoveKind kind, float notBefore)
    {
        Pending = kind;
        approachState.NotBefore = notBefore;
        approachState.Hurry = true;
        stateMachine.ChangeState(approachState);
    }

    // When a move's first blow would land, from its start (-1 when it can't be known: the Guard walks in for as long as
    // it takes).
    public float FirstContact(MoveKind kind)
    {
        switch (kind)
        {
            case MoveKind.Decree: return settings.decreeTell;
            case MoveKind.Sentence: return settings.sentenceTell1;
            case MoveKind.Seize: return settings.seizeTell;
            case MoveKind.Command: return firstCommandDone ? settings.commandTell : settings.commandTellFirst;
            case MoveKind.Threads: return settings.threadsTell;
            default: return -1f;
        }
    }

    private MoveKind? deferred;

    // How many times he held a blow back for a hazard (the playtest reads it).
    public int HeldForHazards { get; private set; }

    public void StartMove(MoveKind kind)
    {
        // The pacing rule (§8): never a blow landing within 0.5 s of a hazard's impact. He waits a beat instead.
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
            case MoveKind.Command: readyAt[kind] = Time.time + settings.commandCooldown; break;
            case MoveKind.Guard: readyAt[kind] = Time.time + settings.guardCooldown; break;
            case MoveKind.Seize: readyAt[kind] = Time.time + settings.seizeCooldown; break;
            case MoveKind.Threads: readyAt[kind] = Time.time + (halfReached ? settings.threadsCooldownLate : settings.threadsCooldown); break;
        }

        stateMachine.ChangeState(StateFor(kind));
    }

    // The Command's first tell each attempt is the slow one (§4).
    public float CommandTell()
    {
        float tell = firstCommandDone ? settings.commandTell : settings.commandTellFirst;
        firstCommandDone = true;
        return tell;
    }

    // A move ended normally: a deferred poise break or the 50% beat comes first, then a pause and the next move.
    public void MoveFinished()
    {
        if (pendingPoiseBreak)
        {
            pendingPoiseBreak = false;
            staggerState.Begin(settings.poiseStaggerTime);
            return;
        }

        if (TryHalfBeat())
            return;

        circleState.Hold(Random.Range(settings.pauseMin, settings.pauseMax));
        stateMachine.ChangeState(circleState);
    }

    private bool TryHalfBeat()
    {
        if (halfReached || health.Fraction > 0.5f || IsDead)
            return false;

        halfReached = true;
        OnHalfHealth?.Invoke();
        stateMachine.ChangeState(pommelState);
        return true;
    }

    // --- Taking hits ---

    public void TakeHit(DamageInfo info)
    {
        if (IsDead || Untouchable)
            return;

        Vector3 toAttacker = -info.direction;
        toAttacker.y = 0f;
        bool frontal = Vector3.Angle(transform.forward, toAttacker) <= settings.guardArc;
        MasterMoveState move = stateMachine.currentState as MasterMoveState;

        // The Regal Guard: every frontal hit stops on the crossed blade; a bash breaks it, a heavy wears its poise.
        if (move != null && move.Guarding && frontal && info.isEnvironmental == false)
        {
            if (info.interrupts)
            {
                bossTell?.GuardBroken();
                staggerState.Begin(settings.guardBrokenStagger);
                return;
            }

            bossTell?.GuardSpark(info.point);
            if (info.heavy && poise.ApplyDamage(info.poiseDamage * settings.guardHeavyPoise))
                staggerState.Begin(settings.guardBrokenStagger);
            return;
        }

        bool armoured = move != null && move.Armoured;
        float amount = armoured ? info.amount * settings.windupDamageMultiplier : info.amount;
        health.ApplyDamage(amount);
        if (info.isEnvironmental == false)
            Flash();

        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(fallenState);
            OnDefeated?.Invoke();
            return;
        }

        if (info.isEnvironmental)
            return;

        // Hits on him while he's open don't knock him out of it.
        if (IsStaggered || stateMachine.currentState == pommelState)
            return;

        bool broke = poise.ApplyDamage(info.poiseDamage);
        if (move != null)
        {
            // In a move: the blow carries on. A break waits for it to end (§1).
            if (broke) pendingPoiseBreak = true;
            return;
        }

        if (broke)
        {
            staggerState.Begin(settings.poiseStaggerTime);
            return;
        }

        if (Engaged && Time.time - lastFlinchTime >= settings.flinchCooldown)
        {
            lastFlinchTime = Time.time;
            stateMachine.ChangeState(flinchState);
        }
    }

    // A hazard (brazier, chandelier, statue, panel) lands on him: it breaks any wind-up (§1 rule 3).
    public override void HazardHit(float damage, float stagger)
    {
        if (IsDead || Untouchable)
            return;

        health.ApplyDamage(damage);
        Flash();
        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(fallenState);
            OnDefeated?.Invoke();
            return;
        }

        if (stagger > 0f && stateMachine.currentState != pommelState)
            staggerState.Begin(stagger);
    }

    public void OnParried(GameObject parrier)
    {
        if (IsDead || Untouchable)
            return;

        if (stateMachine.currentState is MasterMoveState move && move.OnParried())
            return;

        parriedState.Begin(settings.parryKneelTime);
    }

    // --- Helpers for the states ---

    // The left arm's rest pose over the sword-and-shield clips (no shield), off for the spells (§15).
    public void SetLeftArmRest(bool on) => anim.SetBool(LeftArmRestId, on);

    public void RaiseCommandTell(float tell) => OnCommandTell?.Invoke(tell);

    public void RaiseThreadsTell() => OnThreadsTell?.Invoke();
}
