using Kneel.Hazards;
using UnityEngine;
using UnityEngine.AI;

public class TheFool : Enemy, IDamageable, IParryable, IStaggerable
{
    [System.Serializable]
    public class Swing
    {
        public string state;
        public float contact;

        // This swing's own wind-up when it follows another, or 0 for followupWindup.
        public float windup;
    }

    [System.Serializable]
    public class SwingString
    {
        public string name;
        public int[] swings;
    }

    [Header("Reaction Data")]
    // How long a hit stops him. He has no poise, so any hit outside a committed swing does, and it ends his string.
    public float hitStunTime = 0.6f;

    // How long a parry leaves him open (hits during it are critical).
    public float staggerTime = 2f;

    // How far a hit shoves him back. The NavMesh clamps it, so a hit never knocks him over the breach.
    public float knockback = 0.35f;

    [Header("Swing Data")]
    // Seconds from the start of each swing's clip until the blade lands: the first is the slow tell, the next two the
    // re-coil between swings. The swing's damage is Enemy.attackDamage.
    public float firstWindup = 1f;
    public float followupWindup = 0.4f;

    // He stops tracking the player this long before a blade lands, and the swing's line is set then.
    public float commitTime = 0.3f;

    // Each swing carries him this far along that line, from lungeLead before the blade lands to lungeTail after it.
    public float lungeDistance = 3f;
    public float lungeLead = 0.25f;
    public float lungeTail = 0.1f;

    // A swing stops when it comes this close to a player still standing in its line, rather than running through them.
    public float lungeStopDistance = 1.2f;

    // How long he stands open after the third swing: the player's window to hit him.
    public float recoveryTime = 1.6f;

    [Header("Grip Data")]
    // Where his left hand holds the greatsword (built by TheFoolSetup from his stance). The sword rides his right hand;
    // the clips' left hand only comes near the grip, so it's pinned there when it's within offHandReach of it, and
    // left alone, fading out by twice that, where a clip lets go of the sword.
    public Transform offHandGrip;
    public float offHandReach = 0.25f;

    [Header("Edge Data")]
    // A swing that meets the NavMesh's edge this close to a breach's lip (EdgeGuard) carries him over it.
    public float breachReach = 1f;

    // Every swing he has, set by TheFoolSetup: its animator state and the clip second its blade lands (its AttackHit
    // event). A swing plays at whatever speed puts that moment at its wind-up, so the wind-ups can be tuned live.
    public Swing[] swings = new Swing[0];

    // His strings: each is a run of swings by index into `swings`, two to four long. He picks one at random each time,
    // never the same twice running, and the first is the plain three-swing string the design note describes.
    public SwingString[] strings = new SwingString[0];

    // How fast a swing's clip plays once the blade has landed.
    public float followThroughSpeed = 1f;

    private EdgeGuard[] edgeGuards;
    private SkinnedMeshRenderer bodyRenderer;
    private int lastString = -1;
    private Vector3 fallVelocity;
    private bool falling;
    private float fallStartY;

    public IdleState_TheFool idleState { get; private set; }
    public MoveState_TheFool moveState { get; private set; }
    public NoticeState_TheFool noticeState { get; private set; }
    public ChaseState_TheFool chaseState { get; private set; }
    public AttackState_TheFool attackState { get; private set; }
    public RecoveryState_TheFool recoveryState { get; private set; }
    public HitState_TheFool hitState { get; private set; }
    public StaggerState_TheFool staggerState { get; private set; }
    public DeadState_TheFool deadState { get; private set; }

    public bool IsDead => stateMachine.currentState == deadState;

    public bool IsStaggered => stateMachine.currentState == staggerState;

    // Went over the breach on a swing.
    public bool IsFalling => falling;

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_TheFool(this, stateMachine, "Idle");
        moveState = new MoveState_TheFool(this, stateMachine, "Move");
        noticeState = new NoticeState_TheFool(this, stateMachine, "Notice");
        chaseState = new ChaseState_TheFool(this, stateMachine, "Chase");
        attackState = new AttackState_TheFool(this, stateMachine, "Attack");
        recoveryState = new RecoveryState_TheFool(this, stateMachine, "Recovery");
        hitState = new HitState_TheFool(this, stateMachine, "Hit");
        staggerState = new StaggerState_TheFool(this, stateMachine, "Stagger");
        deadState = new DeadState_TheFool(this, stateMachine, "Dead");
    }

    protected override void Start()
    {
        base.Start();

        edgeGuards = FindObjectsByType<EdgeGuard>();
        bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();

        if (falling)
            Fall();
        else
            UpdateSpeedParameter();
    }

    private void LateUpdate()
    {
        // Bones aren't written off camera (CullUpdateTransforms), so there's nothing to correct there.
        if (offHandGrip == null || IsDead || bodyRenderer.isVisible == false)
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
            return;
        }

        // Fire and other world damage causes no reaction.
        if (info.isEnvironmental)
            return;

        if (agent.enabled)
            agent.Move(info.direction * knockback);

        // A staggered fool stays staggered; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        // No poise: a committed swing carries on through the hit, and anything else, the wind-ups between swings
        // included, is knocked out of him.
        if (stateMachine.currentState == attackState && attackState.Committed)
            return;

        stateMachine.ChangeState(hitState);
    }

    // A parry on any swing stops the string and leaves him open.
    public void OnParried(GameObject parrier)
    {
        if (IsDead)
            return;

        stateMachine.ChangeState(staggerState);
    }

    // Animation event on each swing, where the blade crosses in front of him.
    public override void AttackHit()
    {
        if (stateMachine.currentState == attackState)
            attackState.BladeLands();
    }

    // Wind-up of a swing at `position` in its string: the opener is always the slow one.
    public float SwingWindup(Swing swing, int position)
    {
        if (position == 0)
            return firstWindup;

        return swing.windup > 0f ? swing.windup : followupWindup;
    }

    // The string to use next: the plain one first, then any, never the same twice running.
    public SwingString NextString()
    {
        int pick;
        if (lastString < 0)
            pick = 0;
        else
        {
            pick = Random.Range(0, strings.Length - 1);
            if (pick == lastString)
                pick = strings.Length - 1;
        }

        lastString = pick;
        return strings[pick];
    }

    // Carries him along a swing's line by `step`. The NavMesh stops him at every edge but one: where it meets the E4
    // breach's lip, the swing takes him over (L4 decisions 18 and section 12; knockback never does).
    public void Lunge(Vector3 step)
    {
        if (agent.enabled == false || step.sqrMagnitude < 1e-8f)
            return;

        Vector3 from = transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (NavMesh.Raycast(from, from + step, out NavMeshHit edge, filter) && AtBreach(edge.position))
        {
            GoOver(step / Mathf.Max(Time.deltaTime, 1e-4f));
            return;
        }

        agent.Move(step);
    }

    private bool AtBreach(Vector3 point)
    {
        foreach (EdgeGuard guard in edgeGuards)
        {
            if (guard != null && guard.InsetOf(point) < breachReach)
                return true;
        }

        return false;
    }

    private void GoOver(Vector3 velocity)
    {
        stateMachine.ChangeState(deadState);
        health.ApplyDamage(health.Current);
        agent.enabled = false;

        velocity.y = 0f;
        fallVelocity = Vector3.ClampMagnitude(velocity, lungeDistance / Mathf.Max(lungeLead + lungeTail, 0.05f));
        fallStartY = transform.position.y;
        falling = true;
    }

    private void Fall()
    {
        fallVelocity += Physics.gravity * Time.deltaTime;
        transform.position += fallVelocity * Time.deltaTime;

        // Gone into the smoke: stop moving him once he's well out of sight.
        if (transform.position.y < fallStartY - 30f)
        {
            falling = false;
            foreach (Renderer bodyRenderer in GetComponentsInChildren<Renderer>())
                bodyRenderer.enabled = false;
        }
    }
}
