using Kneel.Hazards;
using UnityEngine;

public class AshenFootman : Enemy, IDamageable, IParryable, IStaggerable
{
    private static readonly int GuardId = Animator.StringToHash("Guard");

    [Header("Reaction Data")]
    // How long a normal hit interrupts the footman.
    public float hitStunTime = 0.6f;

    // How long a parry or a broken poise leaves it open (hits during it are critical). Long enough for the player's
    // heavy to land inside it, which is what makes a riposte a kill.
    public float staggerTime = 2f;

    // How far a hit shoves it back.
    public float knockback = 0.35f;

    [Header("Overhead Data")]
    // Seconds from starting the overhead until the sword lands. Ramps the shoulder tell.
    public float overheadWindup = 0.8f;

    // Degrees the sword-side shoulder hauls up through the overhead's wind-up. The clip's own rise is 5 cm, which doesn't
    // read from the gameplay camera, and the sweep has none, so this also tells the two apart.
    public float shoulderTell = 30f;

    [Header("Sweep Data")]
    // The low flat cut: quicker and weaker than the overhead, for a player who has come in close.
    public float sweepWindup = 0.67f;
    public float sweepDamage = 10f;
    public float sweepPoiseDamage = 20f;
    public float sweepReach = 1f;
    public float sweepRadius = 0.55f;

    // Closer than this he sweeps instead of raising the overhead.
    public float sweepRange = 1.2f;

    // Overheads he throws before a sweep, however far away the player is.
    public int overheadsPerSweep = 2;

    [Header("Guard Data")]
    // Hits from within this many degrees of straight ahead land on the raised shield.
    public float guardAngle = 70f;

    // Share of a blocked hit's poise damage that reaches his poise, as a block does for the player.
    public float guardPoiseMultiplier = 0.5f;

    // How long a blocked hit stops him.
    public float blockStunTime = 0.45f;

    // How long the shield stays down after a hit gets through.
    public float guardDownAfterHit = 2f;

    // How long the shield stays down after a bash or a poise break breaks the guard. Counts through the stagger.
    public float guardDownAfterBreak = 3f;

    // How long the shield stays down once an attack finishes: the punish window.
    public float guardDownAfterAttack = 1f;

    [Header("Ambush Data")]
    // When set, the footman is out of play until this gibbet frees it.
    [SerializeField]
    private GibbetAmbush ambush;

    // When set, it only fights while the player is inside this volume.
    [SerializeField]
    private Collider leash;

    // How long a thrall hit on its gibbet before waking stays staggered.
    public float ambushStaggerTime = 3f;

    private float guardDownTimer;
    private int overheadsSinceSweep;
    private Transform swordShoulder;
    private Renderer body;
    private float shoulderRaise;

    public IdleState_AshenFootman idleState { get; private set; }
    public MoveState_AshenFootman moveState { get; private set; }
    public RecoveryState_AshenFootman recoveryState { get; private set; }
    public ChaseState_AshenFootman chaseState { get; private set; }
    public AttackState_AshenFootman attackState { get; private set; }
    public AttackState_AshenFootman sweepState { get; private set; }
    public BlockState_AshenFootman blockState { get; private set; }
    public HitState_AshenFootman hitState { get; private set; }
    public StaggerState_AshenFootman staggerState { get; private set; }
    public DeadState_AshenFootman deadState { get; private set; }
    public DormantState_AshenFootman dormantState { get; private set; }

    // 0..1 through the current swing's wind-up; set by the attack state. He has no colour telegraph, so Enemy.telegraph
    // stays at zero: his tell is the pose.
    public float windup { get; set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    public bool IsAttacking => stateMachine.currentState == attackState || stateMachine.currentState == sweepState;

    // The shield is up unless he's busy attacking or reacting, or something has knocked it down.
    public bool IsGuarding
    {
        get
        {
            EnemyState state = stateMachine.currentState;
            bool canGuard = state == idleState || state == moveState || state == recoveryState || state == chaseState || state == blockState;
            return canGuard && guardDownTimer <= 0f;
        }
    }

    public bool PlayerInLeash
    {
        get
        {
            if (leash == null)
                return true;

            Bounds bounds = leash.bounds;
            return bounds.Contains(new Vector3(player.position.x, bounds.center.y, player.position.z));
        }
    }

    public Vector3 AmbushSpawnPoint => ambush.SpawnPoint.position;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_AshenFootman(this, stateMachine, "Idle");
        moveState = new MoveState_AshenFootman(this, stateMachine, "Move");
        recoveryState = new RecoveryState_AshenFootman(this, stateMachine, "Recovery");
        chaseState = new ChaseState_AshenFootman(this, stateMachine, "Chase");
        attackState = new AttackState_AshenFootman(this, stateMachine, "Attack", overheadWindup);
        sweepState = new AttackState_AshenFootman(this, stateMachine, "Sweep", sweepWindup);
        blockState = new BlockState_AshenFootman(this, stateMachine, "Block");
        hitState = new HitState_AshenFootman(this, stateMachine, "Hit");
        staggerState = new StaggerState_AshenFootman(this, stateMachine, "Stagger");
        deadState = new DeadState_AshenFootman(this, stateMachine, "Dead");
        dormantState = new DormantState_AshenFootman(this, stateMachine, "Dormant");

        swordShoulder = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
        body = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    protected override void Start()
    {
        base.Start();

        if (ambush != null)
        {
            ambush.Woken += OnAmbushWoken;
            stateMachine.Initialize(dormantState);
            return;
        }

        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        guardDownTimer -= Time.deltaTime;

        stateMachine.currentState.Update();

        // The block recoil is a full-body clip of its own, so the arm overlay lets it play.
        anim.SetBool(GuardId, IsGuarding && stateMachine.currentState != blockState);
        UpdateSpeedParameter();
    }

    // On top of the animator's pose. Off camera the animator stops rewriting the bones (CullUpdateTransforms), so
    // applying this there would stack up frame after frame.
    private void LateUpdate()
    {
        float target = stateMachine.currentState == attackState ? windup : 0f;
        shoulderRaise = Mathf.MoveTowards(shoulderRaise, target, Time.deltaTime * 6f);

        if (shoulderRaise > 0f && body.isVisible)
            swordShoulder.rotation = Quaternion.AngleAxis(shoulderTell * shoulderRaise, transform.forward) * swordShoulder.rotation;
    }

    private void OnDestroy()
    {
        if (ambush != null)
            ambush.Woken -= OnAmbushWoken;
    }

    public override bool PlayerInAgressionRange() => PlayerInLeash && base.PlayerInAgressionRange();

    public void TakeHit(DamageInfo info)
    {
        if (IsDead)
            return;

        // Fire and other world damage goes round the shield and causes no reaction.
        if (info.isEnvironmental)
        {
            health.ApplyDamage(info.amount);
            if (health.Current <= 0f)
                stateMachine.ChangeState(deadState);
            return;
        }

        if (IsGuarding && HitsShield(info.direction))
        {
            TakeHitOnShield(info);
            return;
        }

        health.ApplyDamage(info.amount);

        Flash();

        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(deadState);
            return;
        }

        LowerGuard(guardDownAfterHit);

        if (agent.enabled)
            agent.Move(info.direction * knockback);

        // A staggered footman stays staggered; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        // Catching it mid wind-up is a counter-hit and hits its poise harder.
        bool attacking = IsAttacking;
        bool inWindup = attacking && ((AttackState_AshenFootman)stateMachine.currentState).swingStarted == false;

        // A shield bash during the wind-up knocks the attack out of it, like a parry.
        if (info.interrupts && inWindup)
        {
            poise.Reset();
            Stagger(staggerTime);
            return;
        }

        float poiseDamage = info.poiseDamage * (inWindup ? windupPoiseMultiplier : 1f);

        if (poise.ApplyDamage(poiseDamage))
        {
            Stagger(staggerTime);
            return;
        }

        // Poise held: the attack (wind-up or swing) carries on through the hit.
        if (attacking)
            return;

        stateMachine.ChangeState(hitState);
    }

    public void OnParried(GameObject parrier)
    {
        if (IsDead)
            return;

        poise.Reset();
        Stagger(staggerTime);
    }

    public override void AttackHit()
    {
        if (stateMachine.currentState == sweepState)
        {
            sweepState.swingStarted = true;
            TryAttackHit(sweepReach, sweepRadius, sweepDamage, sweepPoiseDamage);
            return;
        }

        attackState.swingStarted = true;
        base.AttackHit();
    }

    // Overheads first, so the teaching swing is the one a new player meets. A sweep answers a player who has stepped
    // inside the overhead, and comes in on its own every few swings.
    public EnemyState NextAttack()
    {
        bool close = Vector3.Distance(transform.position, player.position) < sweepRange;

        if (overheadsSinceSweep >= overheadsPerSweep || (close && overheadsSinceSweep > 0))
        {
            overheadsSinceSweep = 0;
            return sweepState;
        }

        overheadsSinceSweep++;
        return attackState;
    }

    public void LowerGuard(float seconds)
    {
        guardDownTimer = Mathf.Max(guardDownTimer, seconds);
    }

    // Used by the dormant state: out of the world until the gibbet wakes it.
    public void SetInWorld(bool inWorld) => SetPresent(inWorld);

    private bool HitsShield(Vector3 direction)
    {
        Vector3 toAttacker = -direction;
        toAttacker.y = 0f;
        return Vector3.Angle(transform.forward, toAttacker) <= guardAngle;
    }

    private void TakeHitOnShield(DamageInfo info)
    {
        // A bash breaks the guard outright. Anything else wears on his poise, which is what lets a heavy break it too.
        if (info.interrupts || poise.ApplyDamage(info.poiseDamage * guardPoiseMultiplier))
        {
            health.ApplyDamage(info.amount);
            Flash();

            if (health.Current <= 0f)
            {
                stateMachine.ChangeState(deadState);
                return;
            }

            poise.Reset();
            Stagger(staggerTime);
            return;
        }

        if (agent.enabled)
            agent.Move(info.direction * knockback * 0.5f);

        HitSpark.Spawn(info.point, -info.direction, new Color(0.8f, 0.85f, 1f), 10);
        stateMachine.ChangeState(blockState);
    }

    // Any stagger leaves the shield down, or a riposte would land on a guard that came back up as it recovered.
    private void Stagger(float seconds)
    {
        LowerGuard(guardDownAfterBreak);
        staggerState.duration = seconds;
        stateMachine.ChangeState(staggerState);
    }

    private void OnAmbushWoken(GibbetAmbush gibbet)
    {
        if (IsDead)
            return;

        if (gibbet.WasStruckEarly)
        {
            Stagger(ambushStaggerTime);
            return;
        }

        stateMachine.ChangeState(chaseState);
    }
}
