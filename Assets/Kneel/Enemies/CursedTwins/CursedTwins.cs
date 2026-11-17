using UnityEngine;

// One of the Cursed Twins. The pair share nothing but their timing: each swings a fast, weak blow, and when both can
// reach the player they wind up on the same frame, from either side of them. Killing one enrages the other, which then
// hits twice as hard, so the way through is to wear both down evenly before finishing either.
public class CursedTwins : Enemy, IDamageable, IParryable, IStaggerable
{
    [System.Serializable]
    public class Swing
    {
        public string state;

        // Clip seconds from the start of the swing's state until the blade lands (its AttackHit event).
        public float contact;
    }

    [Header("Bond Data")]
    // The other twin. Set on both (the CursedTwins pair prefab does it).
    public CursedTwins partner;

    // How far to the side of the player he aims his approach: each twin takes the side away from his partner, so the
    // pair come at the player from both sides.
    public float flankOffset = 1.5f;

    // How long a twin that's ready to swing waits for his partner before swinging alone.
    public float syncWait = 1.2f;

    [Header("Enrage Data")]
    // Once his partner dies: how long he roars, and how much harder he hits from then on.
    public float enrageTime = 1f;
    public float enragedDamageMultiplier = 2f;

    // The red he burns with once enraged (0..1 of the telegraph tint).
    public float enragedTint = 0.35f;

    [Header("Reaction Data")]
    // How long a hit that doesn't break his poise stops him. A committed swing carries on.
    public float hitStunTime = 0.4f;

    // How long a broken poise leaves him open (hits during it are critical). Low poise: any heavy breaks it.
    public float staggerTime = 1.5f;

    // How long a parry leaves him open: long enough for a riposte.
    public float parriedStaggerTime = 2f;

    public float knockback = 0.25f;

    [Header("Swing Data")]
    // The tell: from the start of his swing to the blade landing (16 frames at 30 fps).
    public float windupTime = 0.53f;

    // He stops tracking the player this long before the blade lands.
    public float commitTime = 0.15f;

    // Each swing carries him this far forward, ending as the blade lands, and stops short at a player in its way.
    public float lungeDistance = 0.8f;
    public float lungeLead = 0.2f;
    public float lungeStopDistance = 1.2f;

    // How fast the clip plays once the blade has landed.
    public float followThroughSpeed = 1.2f;

    // How long he stands after a swing: the player's window.
    public float recoveryTime = 1f;

    // His swings, used in turn.
    public Swing[] swings = new Swing[0];

    [Header("Grip Data")]
    // Where his left hand holds the sword, pinned there with a two-bone IK while the clip's hand is within offHandReach
    // of it (the stand-in is The Fool's rig and clips, which grip two-handed).
    public Transform offHandGrip;
    public float offHandReach = 0.25f;

    private SkinnedMeshRenderer bodyRenderer;
    private int nextSwing;
    private float readySince = -1f;
    private bool enraged;

    public IdleState_CursedTwins idleState { get; private set; }
    public MoveState_CursedTwins moveState { get; private set; }
    public NoticeState_CursedTwins noticeState { get; private set; }
    public ChaseState_CursedTwins chaseState { get; private set; }
    public AttackState_CursedTwins attackState { get; private set; }
    public RecoveryState_CursedTwins recoveryState { get; private set; }
    public HitState_CursedTwins hitState { get; private set; }
    public StaggerState_CursedTwins staggerState { get; private set; }
    public EnrageState_CursedTwins enrageState { get; private set; }
    public DeadState_CursedTwins deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    public bool IsEnraged => enraged;

    // Close enough to swing, facing the player, and free to: what the pair's beat waits on.
    public bool ReadyToSwing { get; private set; }

    private bool PartnerAlive => partner != null && partner.IsDead == false && partner.isActiveAndEnabled;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_CursedTwins(this, stateMachine, "Idle");
        moveState = new MoveState_CursedTwins(this, stateMachine, "Move");
        noticeState = new NoticeState_CursedTwins(this, stateMachine, "Notice");
        chaseState = new ChaseState_CursedTwins(this, stateMachine, "Chase");
        attackState = new AttackState_CursedTwins(this, stateMachine, "Attack");
        recoveryState = new RecoveryState_CursedTwins(this, stateMachine, "Recovery");
        hitState = new HitState_CursedTwins(this, stateMachine, "Hit");
        staggerState = new StaggerState_CursedTwins(this, stateMachine, "Stagger");
        enrageState = new EnrageState_CursedTwins(this, stateMachine, "Enrage");
        deadState = new DeadState_CursedTwins(this, stateMachine, "Dead");
    }

    protected override void Start()
    {
        base.Start();

        bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        ReadyToSwing = false;
        stateMachine.currentState.Update();
        UpdateSpeedParameter();

        if (enraged)
            telegraph = Mathf.Max(telegraph, enragedTint);
    }

    // --- The bond ---

    // One twin seeing the player wakes the other: they always come as a pair.
    public void Alert()
    {
        if (PartnerAlive && (partner.stateMachine.currentState == partner.idleState || partner.stateMachine.currentState == partner.moveState))
            partner.stateMachine.ChangeState(partner.noticeState);
    }

    // Where to walk to: beside the player on his side, so the pair close from either side of them.
    public Vector3 ChaseDestination()
    {
        if (PartnerAlive == false)
            return player.position;

        Vector3 across = partner.transform.position - transform.position;
        across.y = 0f;
        if (across.sqrMagnitude < 0.01f)
            across = player.right;

        // His side is the one away from his partner.
        return player.position - across.normalized * flankOffset;
    }

    // Called each Chase frame he could swing. Swings when his partner can too (starting both at once), when he's waited
    // syncWait for him, or straight away without one.
    public void OnReadyToSwing()
    {
        ReadyToSwing = true;
        if (readySince < 0f)
            readySince = Time.time;

        if (PartnerAlive == false)
        {
            StartSwing();
            return;
        }

        if (partner.ReadyToSwing)
        {
            StartSwing();
            partner.StartSwing();
            return;
        }

        if (Time.time - readySince >= syncWait)
            StartSwing();
    }

    // Left Chase without swinging, or swung: the wait starts over.
    public void ResetReady()
    {
        readySince = -1f;
        ReadyToSwing = false;
    }

    public void StartSwing()
    {
        if (IsDead || stateMachine.currentState == attackState)
            return;

        ResetReady();
        stateMachine.ChangeState(attackState);
    }

    public Swing NextSwing()
    {
        Swing swing = swings[nextSwing % swings.Length];
        nextSwing++;
        return swing;
    }

    public float SwingDamage => attackDamage * (enraged ? enragedDamageMultiplier : 1f);

    // His partner fell: he roars, and burns hotter from then on.
    private void OnPartnerDied()
    {
        if (IsDead || enraged)
            return;

        enraged = true;
        stateMachine.ChangeState(enrageState);
    }

    // --- Being hit ---

    public void TakeHit(DamageInfo info)
    {
        if (IsDead)
            return;

        health.ApplyDamage(info.amount);

        if (info.isEnvironmental == false)
            Flash();

        if (health.Current <= 0f)
        {
            stateMachine.ChangeState(deadState);
            if (partner != null)
                partner.OnPartnerDied();
            return;
        }

        if (info.isEnvironmental)
            return;

        if (agent.enabled)
            agent.Move(info.direction * knockback);

        if (IsStaggered)
            return;

        // Hits in his wind-up count double (Enemy.windupPoiseMultiplier).
        bool inWindup = stateMachine.currentState == attackState && attackState.Committed == false;
        if (poise.ApplyDamage(info.poiseDamage * (inWindup ? windupPoiseMultiplier : 1f)))
        {
            Stagger(staggerTime);
            return;
        }

        // A committed swing carries on through a light; the roar does too.
        if (stateMachine.currentState == attackState && attackState.Committed)
            return;
        if (stateMachine.currentState == enrageState)
            return;

        stateMachine.ChangeState(hitState);
    }

    public void OnParried(GameObject parrier)
    {
        if (IsDead)
            return;

        Stagger(parriedStaggerTime);
    }

    public void Stagger(float duration)
    {
        staggerState.Duration = duration;
        stateMachine.ChangeState(staggerState);
    }

    // Animation event on each swing, where the blade crosses in front of him.
    public override void AttackHit()
    {
        if (stateMachine.currentState == attackState)
            attackState.BladeLands();
    }

    public bool LandBlow() => TryAttackHit(attackReach, attackRadius, SwingDamage, attackPoiseDamage);

    // --- The off hand ---

    private void LateUpdate()
    {
        // Bones aren't written off camera (CullUpdateTransforms), so there's nothing to correct there.
        if (offHandGrip == null || IsDead || bodyRenderer == null || bodyRenderer.isVisible == false)
            return;

        Transform hand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        float weight = 1f - Mathf.InverseLerp(offHandReach, offHandReach * 2f, Vector3.Distance(hand.position, offHandGrip.position));
        if (weight <= 0f)
            return;

        Vector3 target = Vector3.Lerp(hand.position, offHandGrip.position, weight);
        SolveArm(anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), anim.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand, target);
        hand.rotation = Quaternion.Slerp(hand.rotation, offHandGrip.rotation, weight);
    }

    // Two-bone IK: bends the elbow so the hand reaches the target, keeping the arm in the plane it was already bent in.
    private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target)
    {
        Vector3 a = upper.position, b = lower.position, c = hand.position;
        float upperLength = (b - a).magnitude, lowerLength = (c - b).magnitude;
        float reach = Mathf.Clamp((target - a).magnitude, 0.001f, upperLength + lowerLength - 0.001f);

        float shoulderNow = Vector3.Angle(c - a, b - a);
        float elbowNow = Vector3.Angle(a - b, c - b);
        float toTarget = Vector3.Angle(c - a, target - a);
        float shoulder = Mathf.Acos(Mathf.Clamp((lowerLength * lowerLength - upperLength * upperLength - reach * reach) / (-2f * upperLength * reach), -1f, 1f)) * Mathf.Rad2Deg;
        float elbow = Mathf.Acos(Mathf.Clamp((reach * reach - upperLength * upperLength - lowerLength * lowerLength) / (-2f * upperLength * lowerLength), -1f, 1f)) * Mathf.Rad2Deg;

        Vector3 bendAxis = Vector3.Cross(c - a, b - a).normalized;
        Vector3 swingAxis = Vector3.Cross(c - a, target - a).normalized;
        Quaternion upperWorld = upper.rotation, lowerWorld = lower.rotation;

        upper.localRotation *= Quaternion.AngleAxis(shoulder - shoulderNow, Quaternion.Inverse(upperWorld) * bendAxis);
        lower.localRotation *= Quaternion.AngleAxis(elbow - elbowNow, Quaternion.Inverse(lowerWorld) * bendAxis);
        if (swingAxis.sqrMagnitude > 0.5f)
            upper.localRotation *= Quaternion.AngleAxis(toTarget, Quaternion.Inverse(upperWorld) * swingAxis);
    }
}
