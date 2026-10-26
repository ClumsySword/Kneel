using UnityEngine;

public class TheFool : Enemy, IDamageable, IParryable, IStaggerable
{
    [Header("Reaction Data")]
    // How long a normal hit interrupts the footman.
    public float hitStunTime = 0.6f;

    // How long a parry or a broken poise leaves it open (hits during it are critical).
    public float staggerTime = 1.4f;

    // How far a hit shoves it back.
    public float knockback = 0.35f;

    [Header("Lunge Data")]
    // Hitbox reach during the lunge. Kept short so the hit travels with the body instead of
    // covering the whole lunge path from the first frame.
    public float lungeHitReach = 0.6f;

    // The lunge stops this far short of where the player stood when the swing began.
    public float lungeStopDistance = 0.9f;

    public IdleState_TheFool idleState { get; private set; }
    public MoveState_TheFool moveState { get; private set; }
    public RecoveryState_TheFool recoveryState { get; private set; }
    public ChaseState_TheFool chaseState { get; private set; }
    public AttackState_TheFool attackState { get; private set; }
    public HitState_TheFool hitState { get; private set; }
    public StaggerState_TheFool staggerState { get; private set; }
    public DeadState_TheFool deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_TheFool(this, stateMachine, "Idle");
        moveState = new MoveState_TheFool(this, stateMachine, "Move");
        recoveryState = new RecoveryState_TheFool(this, stateMachine, "Recovery");
        chaseState = new ChaseState_TheFool(this, stateMachine, "Chase");
        attackState = new AttackState_TheFool(this, stateMachine, "Attack");
        hitState = new HitState_TheFool(this, stateMachine, "Hit");
        staggerState = new StaggerState_TheFool(this, stateMachine, "Stagger");
        deadState = new DeadState_TheFool(this, stateMachine, "Dead");
    }

    protected override void Start()
    {
        base.Start();

        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();
    }

    public void TakeHit(DamageInfo info)
    {
        if (IsDead)
            return;

        health.ApplyDamage(info.amount);

        DamageNumberStyle style = info.isCritical
            ? DamageNumberStyle.Critical
            : DamageNumberStyle.Normal;
        DamageNumber.Spawn(
            transform.position + Vector3.up * popupHeight,
            Mathf.RoundToInt(info.amount).ToString(),
            style
        );
        Flash();

        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(deadState);
            return;
        }

        if (agent.enabled)
            agent.Move(info.direction * knockback);

        // A staggered footman stays staggered; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        // Catching it mid wind-up is a counter-hit and hits its poise harder.
        bool attacking = stateMachine.currentState == attackState;
        bool inWindup = attacking && attackState.swingStarted == false;
        float poiseDamage = info.poiseDamage * (inWindup ? windupPoiseMultiplier : 1f);

        if (poise.ApplyDamage(poiseDamage))
        {
            stateMachine.ChangeState(staggerState);
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

        if (stateMachine.currentState == attackState)
        {
            attackState.Interrupt();
            return;
        }

        stateMachine.ChangeState(staggerState);
    }

    public override void AttackHit()
    {
        if (attackState.CanApplyAttackHit == false)
            return;

        // Keep checking each frame of the lunge until the swing connects once.
        if (TryAttackHit(lungeHitReach))
            attackState.MarkAttackHit();
    }
}
