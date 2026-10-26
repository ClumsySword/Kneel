using UnityEngine;

public class BlightedBrute : Enemy, IDamageable, IParryable, IStaggerable
{
    [Header("Reaction Data")]
    // How long a normal hit interrupts the footman.
    public float hitStunTime = 0.6f;

    // How long a parry or a broken poise leaves it open (hits during it are critical).
    public float staggerTime = 1.4f;

    // How far a hit shoves it back.
    public float knockback = 0.35f;

    public IdleState_BlightedBrute idleState { get; private set; }
    public MoveState_BlightedBrute moveState { get; private set; }
    public RecoveryState_BlightedBrute recoveryState { get; private set; }
    public ChaseState_BlightedBrute chaseState { get; private set; }
    public AttackState_BlightedBrute attackState { get; private set; }
    public HitState_BlightedBrute hitState { get; private set; }
    public StaggerState_BlightedBrute staggerState { get; private set; }
    public DeadState_BlightedBrute deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_BlightedBrute(this, stateMachine, "Idle");
        moveState = new MoveState_BlightedBrute(this, stateMachine, "Move");
        recoveryState = new RecoveryState_BlightedBrute(this, stateMachine, "Recovery");
        chaseState = new ChaseState_BlightedBrute(this, stateMachine, "Chase");
        attackState = new AttackState_BlightedBrute(this, stateMachine, "Attack");
        hitState = new HitState_BlightedBrute(this, stateMachine, "Hit");
        staggerState = new StaggerState_BlightedBrute(this, stateMachine, "Stagger");
        deadState = new DeadState_BlightedBrute(this, stateMachine, "Dead");
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
        stateMachine.ChangeState(staggerState);
    }

    public override void AttackHit()
    {
        attackState.swingStarted = true;
        base.AttackHit();
    }
}
