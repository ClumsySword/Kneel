using UnityEngine;

public class AshenFootman : Enemy, IDamageable, IParryable, IStaggerable
{
    [Header("Reaction Data")]
    // How long a normal hit interrupts the footman.
    public float hitStunTime = 0.6f;

    // How long a parry or a broken poise leaves it open (hits during it are critical).
    public float staggerTime = 1.4f;

    // How far a hit shoves it back.
    public float knockback = 0.35f;

    public IdleState_AshenFootman idleState { get; private set; }
    public MoveState_AshenFootman moveState { get; private set; }
    public RecoveryState_AshenFootman recoveryState { get; private set; }
    public ChaseState_AshenFootman chaseState { get; private set; }
    public AttackState_AshenFootman attackState { get; private set; }
    public HitState_AshenFootman hitState { get; private set; }
    public StaggerState_AshenFootman staggerState { get; private set; }
    public DeadState_AshenFootman deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_AshenFootman(this, stateMachine, "Idle");
        moveState = new MoveState_AshenFootman(this, stateMachine, "Move");
        recoveryState = new RecoveryState_AshenFootman(this, stateMachine, "Recovery");
        chaseState = new ChaseState_AshenFootman(this, stateMachine, "Chase");
        attackState = new AttackState_AshenFootman(this, stateMachine, "Attack");
        hitState = new HitState_AshenFootman(this, stateMachine, "Hit");
        staggerState = new StaggerState_AshenFootman(this, stateMachine, "Stagger");
        deadState = new DeadState_AshenFootman(this, stateMachine, "Dead");
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

        DamageNumberStyle style = info.isCritical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal;
        DamageNumber.Spawn(transform.position + Vector3.up * popupHeight, Mathf.RoundToInt(info.amount).ToString(), style);
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
