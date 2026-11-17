using Kneel.Hazards;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;

// A big man in plate with no weapon: he backs off a few steps, then runs his shoulder through where the player was
// standing. His armour takes three heavy attacks to break, and nothing else touches him until it's off.
public class IronHusk : Enemy, IDamageable, IStaggerable, IArmoured
{
    [Header("Armour Data")]
    // Heavy attacks it takes to break the armour. They do no damage to him; every other blow glances off.
    public int armourHits = 3;

    // Plates on the model, hidden one per heavy that lands (the last with the break). Optional until the model has them.
    public GameObject[] armourPlates = new GameObject[0];

    // How long breaking the armour leaves him reeling (hits during it are critical).
    public float armourBreakStagger = 1.5f;

    [Header("Reaction Data")]
    // How long a light hit stops him once the armour is off. A committed charge carries on through it.
    public float hitStunTime = 0.5f;

    // How long a heavy staggers him once the armour is off (hits during it are critical). Low poise: any heavy does.
    public float staggerTime = 1.5f;

    // How far an unarmoured hit shoves him back.
    public float knockback = 0.15f;

    [Header("Notice Data")]
    // How long he takes to square up to the player the first time he sees them.
    public float noticeTime = 0.8f;

    [Header("Charge Data")]
    // He only charges along a clear line: the NavMesh between him and the player must be unbroken.
    // Enemy.attackRange is how close he walks in before he does.

    // The tell: he steps back this far over windupTime (24 frames at 30 fps), still turning to the player.
    public float windupTime = 0.8f;
    public float backstepDistance = 1.2f;

    // The charge runs at the spot the player stood on at the end of the wind-up, and this far past it.
    public float chargeOvershoot = 2f;
    public float chargeMinDistance = 4f;
    public float chargeMaxDistance = 14f;
    public float chargeSpeed = 10f;
    public float chargeAcceleration = 60f;

    // The last stretch of the run where he slows to a stop.
    public float chargeStopDistance = 1.5f;

    // The blow: a sphere this far in front of his chest, once per charge. Its damage is Enemy.attackDamage.
    public float chargeHitReach = 0.6f;
    public float chargeHitRadius = 0.9f;

    // A charge into a wall (the NavMesh's edge) stops him dead and leaves him dazed (hits critical).
    public float crashStunTime = 2.5f;

    // A charge that reaches the breach's lip leaves him teetering there (L4 decision 18), as open as a stagger.
    public float teeterTime = 2.5f;

    // His NavMesh stops his radius short of the lip, so being blocked at an edge this close to one counts as reaching it.
    public float breachReach = 1f;

    // How long he stands winded after a charge: the player's window.
    public float recoveryTime = 1.8f;

    [Header("Sound Data")]
    // All optional: a blow glancing off the armour (the "odd sound" that tells the player lights don't work),
    // a heavy cracking it, and the armour coming off.
    public AudioClip[] deflectClips = new AudioClip[0];
    public AudioClip[] crackClips = new AudioClip[0];
    public AudioClip breakClip;

    private static readonly Collider[] hazardBuffer = new Collider[8];

    private EdgeGuard[] edgeGuards;
    private AudioSource voice;
    private int armourLeft;

    public IdleState_IronHusk idleState { get; private set; }
    public MoveState_IronHusk moveState { get; private set; }
    public NoticeState_IronHusk noticeState { get; private set; }
    public ChaseState_IronHusk chaseState { get; private set; }
    public WindupState_IronHusk windupState { get; private set; }
    public ChargeState_IronHusk chargeState { get; private set; }
    public RecoveryState_IronHusk recoveryState { get; private set; }
    public HitState_IronHusk hitState { get; private set; }
    public StaggerState_IronHusk staggerState { get; private set; }
    public TeeterState_IronHusk teeterState { get; private set; }
    public DeadState_IronHusk deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    // Dazed, reeling or teetering: hits are critical.
    public bool IsStaggered => stateMachine.currentState == staggerState || stateMachine.currentState == teeterState;

    public bool IsArmoured => armourLeft > 0;

    public int ArmourLeft => armourLeft;

    // Where the charge is headed, set as the wind-up ends.
    public Vector3 ChargeTarget { get; set; }

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_IronHusk(this, stateMachine, "Idle");
        moveState = new MoveState_IronHusk(this, stateMachine, "Move");
        noticeState = new NoticeState_IronHusk(this, stateMachine, "Notice");
        chaseState = new ChaseState_IronHusk(this, stateMachine, "Chase");
        windupState = new WindupState_IronHusk(this, stateMachine, "Windup");
        chargeState = new ChargeState_IronHusk(this, stateMachine, "Charge");
        recoveryState = new RecoveryState_IronHusk(this, stateMachine, "Recovery");
        hitState = new HitState_IronHusk(this, stateMachine, "Hit");
        staggerState = new StaggerState_IronHusk(this, stateMachine, "Stagger");
        teeterState = new TeeterState_IronHusk(this, stateMachine, "Teeter");
        deadState = new DeadState_IronHusk(this, stateMachine, "Dead");

        armourLeft = armourHits;
    }

    protected override void Start()
    {
        base.Start();

        edgeGuards = FindObjectsByType<EdgeGuard>();
        voice = BuildVoice();
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();
        UpdateSpeedParameter();
    }

    // --- Being hit ---

    public bool Deflects(DamageInfo info) => IsArmoured && info.isEnvironmental == false && info.heavy == false;

    public void TakeHit(DamageInfo info)
    {
        if (IsDead)
            return;

        // Fire goes round the plate and causes no reaction.
        if (info.isEnvironmental)
        {
            Damage(info.amount);
            return;
        }

        if (IsArmoured)
        {
            if (info.heavy)
                CrackArmour();
            else
                Play(deflectClips);

            return;
        }

        Flash();
        Damage(info.amount);
        if (IsDead)
            return;

        Shove(info.direction * knockback);

        // A dazed or reeling Husk stays that way; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        // Low poise: any heavy staggers him, even mid-charge.
        if (info.heavy)
        {
            Stagger(staggerTime);
            return;
        }

        // A light only stops him outside the run itself.
        if (stateMachine.currentState == chargeState)
            return;

        stateMachine.ChangeState(hitState);
    }

    private void CrackArmour()
    {
        armourLeft--;
        Flash();

        int plate = armourHits - armourLeft - 1;
        if (plate >= 0 && plate < armourPlates.Length && armourPlates[plate] != null)
            armourPlates[plate].SetActive(false);

        if (armourLeft > 0)
        {
            Play(crackClips);
            return;
        }

        // The armour comes off: every plate left goes, and he reels.
        foreach (GameObject left in armourPlates)
        {
            if (left != null)
                left.SetActive(false);
        }

        Play(breakClip);
        Stagger(armourBreakStagger);
    }

    private void Damage(float amount)
    {
        health.ApplyDamage(amount);
        if (health.Current <= 0f)
            stateMachine.ChangeState(deadState);
    }

    public void Stagger(float duration)
    {
        staggerState.Duration = duration;
        stateMachine.ChangeState(staggerState);
    }

    // --- The charge ---

    // Charges only along a clear line on the NavMesh, so he never runs at a player he'd have to path round to.
    public bool HasChargeLine()
    {
        if (agent.enabled == false)
            return false;

        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        return NavMesh.Raycast(transform.position, player.position, out _, filter) == false;
    }

    // Moves him by `step` along the ground. Returns how far he really went: the NavMesh stops him at walls, and a
    // breach's lip (EdgeGuard) stops him short of the drop and reports it in `atLip`.
    public float Move(Vector3 step, out bool atLip)
    {
        atLip = false;
        if (agent.enabled == false || step.sqrMagnitude < 1e-8f)
            return 0f;

        Vector3 from = transform.position;
        Vector3 to = from + step;
        EdgeGuard lip = null;
        foreach (EdgeGuard guard in edgeGuards)
        {
            if (guard != null && guard.ClampEnemyMove(from, ref to))
                lip = guard;
        }

        agent.Move(to - from);
        Vector3 moved = transform.position - from;
        moved.y = 0f;

        // Blocked by the NavMesh's edge: at a breach that's the lip, anywhere else a wall.
        if (lip == null && moved.magnitude < step.magnitude * 0.5f)
            lip = BreachAt(from, step);

        if (lip != null)
        {
            atLip = true;
            lip.ReportTeeter(gameObject);
        }

        return moved.magnitude;
    }

    private EdgeGuard BreachAt(Vector3 from, Vector3 step)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.Raycast(from, from + step.normalized * Mathf.Max(step.magnitude, breachReach), out NavMeshHit edge, filter) == false)
            return null;

        foreach (EdgeGuard guard in edgeGuards)
        {
            if (guard != null && guard.InsetOf(edge.position) < breachReach)
                return guard;
        }

        return null;
    }

    // Shoved by a hit: stopped by walls and the breach's lip like any other move, but never teeters.
    private void Shove(Vector3 step)
    {
        if (agent.enabled == false || step.sqrMagnitude < 1e-8f)
            return;

        Vector3 from = transform.position;
        Vector3 to = from + step;
        foreach (EdgeGuard guard in edgeGuards)
        {
            if (guard != null)
                guard.ClampEnemyMove(from, ref to);
        }

        agent.Move(to - from);
    }

    // The shoulder lands on whatever is in front of him. True when it hits the player.
    public bool ChargeHit() => TryAttackHit(chargeHitReach, chargeHitRadius, attackDamage, attackPoiseDamage);

    // A charge through a hanging light's post brings it down (L4 decision 17). Their strike colliders are on the
    // Enemy layer, where the sword looks.
    public void StrikeHazards()
    {
        Vector3 center = transform.position + transform.forward * chargeHitReach + Vector3.up * 0.9f;
        int count = Physics.OverlapSphereNonAlloc(center, chargeHitRadius, hazardBuffer, 1 << gameObject.layer, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            HangingLight light = hazardBuffer[i].GetComponentInParent<HangingLight>();
            if (light != null && light.Current == HangingLight.State.Hanging)
                light.Strike();
        }
    }

    // --- Sound ---

    private AudioSource BuildVoice()
    {
        if (deflectClips.Length == 0 && crackClips.Length == 0 && breakClip == null)
            return null;

        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 3f;
        source.maxDistance = 30f;

        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        if (mixer != null)
        {
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups("SFX"))
            {
                if (group.name == "SFX")
                    source.outputAudioMixerGroup = group;
            }
        }

        return source;
    }

    private void Play(AudioClip[] clips)
    {
        if (clips.Length > 0)
            Play(clips[Random.Range(0, clips.Length)]);
    }

    private void Play(AudioClip clip)
    {
        if (voice != null && clip != null)
            voice.PlayOneShot(clip);
    }
}
