using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Pool;
using UnityEngine.Rendering;

public class HollowArcher : Enemy, IDamageable, IParryable, IStaggerable
{
    [Header("Arrow Data")]
    public float fireRate = 1.0f; // Arrows per second
    public GameObject arrowPrefab;
    public Transform shootPoint;
    private float arrowSpeed = 20;

    [Header("Reaction Data")]
    // How long a normal hit interrupts the footman.
    public float hitStunTime = 0.6f;

    // How long a parry or a broken poise leaves it open (hits during it are critical).
    public float staggerTime = 1.4f;

    // How far a hit shoves it back.
    public float knockback = 0.35f;

    public IdleState_HollowArcher idleState { get; private set; }
    public MoveState_HollowArcher moveState { get; private set; }
    public RecoveryState_HollowArcher recoveryState { get; private set; }
    public ChaseState_HollowArcher chaseState { get; private set; }
    public AttackState_HollowArcher attackState { get; private set; }
    public HitState_HollowArcher hitState { get; private set; }
    public StaggerState_HollowArcher staggerState { get; private set; }
    public DeadState_HollowArcher deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_HollowArcher(this, stateMachine, "Idle");
        moveState = new MoveState_HollowArcher(this, stateMachine, "Move");
        recoveryState = new RecoveryState_HollowArcher(this, stateMachine, "Recovery");
        chaseState = new ChaseState_HollowArcher(this, stateMachine, "Chase");
        attackState = new AttackState_HollowArcher(this, stateMachine, "Attack");
        hitState = new HitState_HollowArcher(this, stateMachine, "Hit");
        staggerState = new StaggerState_HollowArcher(this, stateMachine, "Stagger");
        deadState = new DeadState_HollowArcher(this, stateMachine, "Dead");
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

    public void FireArrow()
    {
        anim.SetTrigger("Shoot");

        Vector3 targetPosition =
            player != null ? player.position : transform.position + transform.forward;
        Vector3 arrowDirection = (targetPosition + Vector3.up - shootPoint.position).normalized;

        GameObject newArrow =
            ObjectPool.instance != null
                ? ObjectPool.instance.GetObject(arrowPrefab)
                : Instantiate(arrowPrefab);
        newArrow.transform.position = shootPoint.position;
        newArrow.transform.rotation = Quaternion.LookRotation(shootPoint.forward);

        newArrow.GetComponent<Arrow>().ArrowSetup(this);

        Rigidbody rbNewArrow = newArrow.GetComponent<Rigidbody>();

        rbNewArrow.mass = 20 / arrowSpeed;
        rbNewArrow.linearVelocity = arrowDirection * arrowSpeed;
    }

    // Called by an arrow this archer fired when it hits something damageable. The archer's
    // attackDamage / attackPoiseDamage set the hit, so all its tuning lives on the enemy.
    public void OnArrowHit(IDamageable target, Vector3 point, Vector3 direction, GameObject arrow)
    {
        target.TakeHit(
            new DamageInfo
            {
                amount = attackDamage,
                poiseDamage = attackPoiseDamage,
                point = point,
                direction = direction,
                // The arrow, not the archer, so parrying an arrow doesn't stagger the archer from range.
                source = arrow,
                isCritical = target is IStaggerable staggerable && staggerable.IsStaggered,
            }
        );
    }

    public override void AttackHit()
    {
        attackState.swingStarted = true;
        base.AttackHit();
    }
}
