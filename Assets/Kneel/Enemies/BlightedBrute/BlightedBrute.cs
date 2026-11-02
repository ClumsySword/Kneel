using UnityEngine;

public class BlightedBrute : Enemy, IDamageable, IParryable, IStaggerable
{
    private static readonly int HoldId = Animator.StringToHash("Hold");

    [Header("Reaction Data")]
    // How long a flinch stops him.
    public float hitStunTime = 0.6f;

    // He only flinches this often, so a stream of light hits can't hold him still: the way to stop him is his poise.
    public float flinchCooldown = 3f;

    // How long a broken poise leaves him open (hits during it are critical). Long enough for a heavy to land.
    public float staggerTime = 2.5f;

    // How long a parry throws his swing off. No opening comes with it: he can't be riposted.
    public float parriedTime = 0.9f;

    // How far a hit shoves him back. He's heavy.
    public float knockback = 0.15f;

    // A shield bash counts this many times over against his poise, which is what makes two of them enough.
    public float bashPoiseMultiplier = 2.4f;

    [Header("Swing Data")]
    // Seconds from starting the swing until the cleaver connects. The swing's damage is Enemy.attackDamage.
    public float swingWindup = 0.9f;

    // He stands heaving this long once a blow's clip ends, on every attack: the player's window to hit him.
    public float swingRecovery = 1.9f;

    // He stops tracking the player this long before the blow lands, so the last step of a dodge counts.
    public float commitTime = 0.3f;

    [Header("Chop Data")]
    // The overhead: slower and harder than the swing, in a narrow line.
    public float chopWindup = 1f;
    public float chopDamage = 60f;
    public float chopPoiseDamage = 90f;
    public float chopReach = 1.75f;
    public float chopRadius = 0.7f;
    public float chopRecovery = 1.7f;

    [Header("Spin Data")]
    // He turns his back and comes round with both arms out: a ring around him, so it finds a player behind him too.
    public float spinWindup = 1.15f;
    public float spinDamage = 40f;
    public float spinPoiseDamage = 70f;
    public float spinRadius = 2.6f;
    public float spinRecovery = 1.5f;

    [Header("Combo Data")]
    // Both cleavers, the whole of Dual Weapon Combo. Seconds from starting it until each of its five blows lands.
    public float[] comboHits = { 0.9f, 1.34f, 2.28f, 3.2f, 3.77f };

    // Light per cut: all five stop short of killing from full health, and a held guard survives them.
    public float comboDamage = 18f;
    public float comboPoiseDamage = 18f;
    public float comboReach = 1.2f;
    public float comboRadius = 1.5f;
    public float comboRecovery = 2.1f;

    // He presses forward through the flurry as the clip does, about 3 m over its middle, and stops this close to the player.
    public float comboAdvanceSpeed = 1.15f;
    public float comboAdvanceStart = 0.7f;
    public float comboAdvanceEnd = 3.3f;
    public float comboStandOff = 1.6f;

    private AttackState_BlightedBrute[] attacks;
    private AttackState_BlightedBrute lastAttack;
    private float nextFlinchTime;
    private bool hasRoared;

    public IdleState_BlightedBrute idleState { get; private set; }
    public MoveState_BlightedBrute moveState { get; private set; }
    public NoticeState_BlightedBrute noticeState { get; private set; }
    public RecoveryState_BlightedBrute recoveryState { get; private set; }
    public ChaseState_BlightedBrute chaseState { get; private set; }
    public AttackState_BlightedBrute attackState { get; private set; }
    public AttackState_BlightedBrute chopState { get; private set; }
    public AttackState_BlightedBrute spinState { get; private set; }
    public AttackState_BlightedBrute comboState { get; private set; }
    public ParriedState_BlightedBrute parriedState { get; private set; }
    public HitState_BlightedBrute hitState { get; private set; }
    public StaggerState_BlightedBrute staggerState { get; private set; }
    public DeadState_BlightedBrute deadState { get; private set; }

    // How long the current recovery lasts; set by the attack that led to it.
    public float recoveryTime { get; set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    public bool IsAttacking => stateMachine.currentState is AttackState_BlightedBrute;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_BlightedBrute(this, stateMachine, "Idle");
        moveState = new MoveState_BlightedBrute(this, stateMachine, "Move");
        noticeState = new NoticeState_BlightedBrute(this, stateMachine, "Notice");
        recoveryState = new RecoveryState_BlightedBrute(this, stateMachine, "Recovery");
        chaseState = new ChaseState_BlightedBrute(this, stateMachine, "Chase");
        attackState = new AttackState_BlightedBrute(this, stateMachine, "Attack", swingRecovery, swingWindup);
        chopState = new AttackState_BlightedBrute(this, stateMachine, "Chop", chopRecovery, chopWindup);
        spinState = new AttackState_BlightedBrute(this, stateMachine, "Spin", spinRecovery, spinWindup);
        comboState = new AttackState_BlightedBrute(this, stateMachine, "Combo", comboRecovery, comboHits)
        {
            advanceSpeed = comboAdvanceSpeed,
            advanceStart = comboAdvanceStart,
            advanceEnd = comboAdvanceEnd,
        };
        parriedState = new ParriedState_BlightedBrute(this, stateMachine, "Parried");
        hitState = new HitState_BlightedBrute(this, stateMachine, "Hit");
        staggerState = new StaggerState_BlightedBrute(this, stateMachine, "Stagger");
        deadState = new DeadState_BlightedBrute(this, stateMachine, "Dead");

        attacks = new[] { attackState, chopState, spinState, comboState };
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

        // The body clips are a bare-handed creature's: the arms carry the cleavers while he stands, walks or gets his
        // breath back.
        EnemyState state = stateMachine.currentState;
        anim.SetBool(HoldId, state == idleState || state == moveState || state == chaseState || state == recoveryState);
        UpdateSpeedParameter();
    }

    public void TakeHit(DamageInfo info)
    {
        if (IsDead)
            return;

        // Fire and other world damage causes no reaction.
        if (info.isEnvironmental)
        {
            health.ApplyDamage(info.amount);
            if (health.Current <= 0f)
                stateMachine.ChangeState(deadState);
            return;
        }

        health.ApplyDamage(info.amount);

        Flash();

        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(deadState);
            return;
        }

        if (agent.enabled)
            agent.Move(info.direction * knockback);

        // A staggered brute stays staggered; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        bool attacking = IsAttacking;
        bool inWindup = attacking && ((AttackState_BlightedBrute)stateMachine.currentState).swingStarted == false;

        float poiseDamage = info.interrupts ? info.poiseDamage * bashPoiseMultiplier : info.poiseDamage;
        if (inWindup && info.interrupts == false)
            poiseDamage *= windupPoiseMultiplier;

        if (poise.ApplyDamage(poiseDamage))
        {
            stateMachine.ChangeState(staggerState);
            return;
        }

        // A bash during the wind-up knocks the swing out of his hands.
        if (info.interrupts && inWindup)
        {
            stateMachine.ChangeState(hitState);
            return;
        }

        // Poise held: the attack (wind-up or swing) carries on through the hit, and a flinch doesn't cut his recovery short,
        // since that's the punish window.
        if (attacking || stateMachine.currentState == recoveryState || Time.time < nextFlinchTime)
            return;

        nextFlinchTime = Time.time + flinchCooldown;
        stateMachine.ChangeState(hitState);
    }

    // The parry stops the blow and throws the swing off, and that's all: no stagger, so no critical hits.
    public void OnParried(GameObject parrier)
    {
        if (IsDead || IsStaggered)
            return;

        stateMachine.ChangeState(parriedState);
    }

    public override void AttackHit()
    {
        var attack = (AttackState_BlightedBrute)stateMachine.currentState;
        attack.swingStarted = true;
        attack.NextBlow();

        if (attack == chopState)
            TryAttackHit(chopReach, chopRadius, chopDamage, chopPoiseDamage);
        else if (attack == spinState)
            TryAttackHit(0f, spinRadius, spinDamage, spinPoiseDamage);
        else if (attack == comboState)
            TryAttackHit(comboReach, comboRadius, comboDamage, comboPoiseDamage);
        else
            base.AttackHit();
    }

    public bool HasRoomToAdvance() => Vector3.Distance(transform.position, player.position) > comboStandOff;

    public void StartCombat()
    {
        stateMachine.ChangeState(hasRoared ? chaseState : noticeState);
        hasRoared = true;
    }

    // The horizontal swing first, so the teaching blow is the one a new player meets. After that any of them, but never
    // the same one twice running.
    public EnemyState NextAttack()
    {
        if (lastAttack == null)
        {
            lastAttack = attackState;
            return attackState;
        }

        int pick = Random.Range(0, attacks.Length - 1);
        if (attacks[pick] == lastAttack)
            pick = attacks.Length - 1;

        lastAttack = attacks[pick];
        return lastAttack;
    }
}
