using Kneel.Hazards;
using UnityEngine;
using UnityEngine.AI;

// The one long-range enemy. He has no weapon to parry, so he isn't IParryable: his arrows can be blocked or rolled through.
public class HollowArcher : Enemy, IDamageable, IStaggerable
{
    private static readonly int ShootId = Animator.StringToHash("Shoot");

    [Header("Arrow Data")]
    public float fireRate = 0.3f; // Arrows per second
    public float arrowSpeed = 14f;

    // How far a missed arrow flies before it's gone.
    public float arrowRange = 25f;
    public GameObject arrowPrefab;

    // On the bow: the arrow leaves along the line through this point.
    public Transform shootPoint;

    [Header("Shot Data")]
    public BowString bowString;

    // Where the hand reaches to in the quiver. It rides on the spine with the quiver.
    public Transform quiverMouth;

    // The pinch of the drawing hand: the arrow's nock and the string sit here.
    public Transform stringHand;

    // The arrow that shows in the hand from the quiver to the release.
    public Transform heldArrow;

    // Seconds into the shot at which the hand is at the quiver. The ArrowDrawn event is on the same moment.
    public float reachTime = 0.21f;

    // How long the hand takes to get there and back. It's IK, since the clip's reach passes in front of the shoulder.
    public float reachBlend = 0.12f;

    // How long the arrow takes to swing from the quiver's line to the bow's.
    public float nockSwing = 0.16f;

    // Seconds into the shot at which the chest starts holding the arrow on the player: from the nock, since the clip
    // swings him well to his left before it comes round.
    public float aimStart = 0.37f;

    // Most the chest will turn for it, in degrees. It's what lets a bow on a wall point down at the yard.
    public float maxChestAim = 45f;

    [Header("Reaction Data")]
    // How long a normal hit interrupts him.
    public float hitStunTime = 0.6f;

    // How long a broken poise leaves him open (hits during it are critical).
    public float staggerTime = 1.4f;

    // How far a hit shoves him back.
    public float knockback = 0.35f;

    [Header("Perch Data")]
    // How far he'll go from where he was placed. On a Perch NavMesh the mesh's own edge usually stops him first.
    public float leashRadius = 5f;

    // Seconds the player has to stay out of his aggression range before he walks back to his post.
    public float giveUpTime = 4f;

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

    public bool IsAttacking => stateMachine.currentState == attackState;

    // The first wind-up waits for the gate where a level has one (L4 decision 9), and every one waits for the fire rate.
    public bool CanStartShot => Time.time >= nextShotTime && (gate == null || gate.CanStartWindUp);

    private OnScreenGate gate;
    private Renderer body;
    private Transform chest;
    private Transform bowHand;
    private Transform drawHand;
    private Transform leftShoulder;
    private Transform rightShoulder;
    private Vector3 stanceShoulders;
    private NavMeshQueryFilter navFilter;
    private Vector3 home;
    private float nextShotTime;
    private float arrowDrawnTime;
    private float chestAim;

    private bool MidShot => IsAttacking && attackState.released == false;

    private Vector3 AimPoint => player.position + Vector3.up;

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

        // In L4 it's on the spawn marker this archer stands under.
        gate = GetComponentInParent<OnScreenGate>();
        body = GetComponentInChildren<SkinnedMeshRenderer>();
        chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
        bowHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        drawHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        leftShoulder = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightShoulder = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
    }

    protected override void Start()
    {
        base.Start();

        navFilter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = NavMesh.AllAreas };
        home = NavMesh.SamplePosition(transform.position, out NavMeshHit onMesh, 2f, navFilter) ? onMesh.position : transform.position;

        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();
        UpdateSpeedParameter();
    }

    // Layer's IK pass, on top of the clip: the hand goes to the quiver itself.
    private void OnAnimatorIK(int layerIndex)
    {
        float weight = 0f;
        if (MidShot)
            weight = Mathf.Clamp01(1f - Mathf.Abs(attackState.elapsed - reachTime) / reachBlend);

        weight = Mathf.SmoothStep(0f, 1f, weight);
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, weight);
        if (weight > 0f)
            anim.SetIKPosition(AvatarIKGoal.RightHand, quiverMouth.position);
    }

    private void LateUpdate()
    {
        TurnChestOntoPlayer();
        PlaceHeldArrow();
        bowString.UpdateString();
    }

    // On top of the animator's pose. Off camera the animator stops rewriting the bones (CullUpdateTransforms), so
    // applying this there would stack up frame after frame.
    private void TurnChestOntoPlayer()
    {
        bool aiming = MidShot && attackState.elapsed >= aimStart;
        chestAim = Mathf.MoveTowards(chestAim, aiming ? 1f : 0f, Time.deltaTime * 5f);
        if ((chestAim <= 0f && MidShot == false) || body.isVisible == false)
            return;

        Vector3 arrowLine = bowHand.position - drawHand.position;
        Quaternion aim = Quaternion.FromToRotation(arrowLine, AimPoint - shootPoint.position);
        aim = Quaternion.RotateTowards(Quaternion.identity, aim, maxChestAim);
        chest.rotation = Quaternion.Slerp(HoldShoulders(), aim, chestAim) * chest.rotation;
    }

    // The clip swings him about 30 degrees to his left through the reach before it comes round to side-on. This undoes
    // that swing and nothing else, so his shoulders never go left of where the stance had them.
    private Quaternion HoldShoulders()
    {
        if (MidShot == false)
            return Quaternion.identity;

        float left = Vector3.SignedAngle(Shoulders, transform.TransformDirection(stanceShoulders), Vector3.up);
        return Quaternion.AngleAxis(Mathf.Max(0f, left), Vector3.up);
    }

    private Vector3 Shoulders => Vector3.ProjectOnPlane(rightShoulder.position - leftShoulder.position, Vector3.up);

    // The nock stays in the fingers. The arrow comes out along the quiver and swings round to lie against the bow.
    private void PlaceHeldArrow()
    {
        if (heldArrow.gameObject.activeSelf == false)
            return;

        Vector3 toBow = (shootPoint.position - stringHand.position).normalized;
        float swing = Mathf.SmoothStep(0f, 1f, (Time.time - arrowDrawnTime) / nockSwing);
        heldArrow.SetPositionAndRotation(stringHand.position, Quaternion.LookRotation(Vector3.Slerp(-quiverMouth.up, toBow, swing)));
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

        // A staggered archer stays staggered; more hits don't restart or shorten it.
        if (IsStaggered)
            return;

        // Catching him before the arrow leaves is a counter-hit and hits his poise harder.
        float poiseDamage = info.poiseDamage * (MidShot ? windupPoiseMultiplier : 1f);

        if (poise.ApplyDamage(poiseDamage))
        {
            stateMachine.ChangeState(staggerState);
            return;
        }

        // Poise held: the shot carries on through the hit.
        if (IsAttacking)
            return;

        stateMachine.ChangeState(hitState);
    }

    // The fire rate counts from one wind-up to the next, so the cycle doesn't depend on what he did in between.
    public void StartShot()
    {
        nextShotTime = Time.time + 1f / fireRate;
        stanceShoulders = transform.InverseTransformDirection(Shoulders);

        if (gate != null)
            gate.WindUpStarted();
    }

    // Puts everything back when a shot ends, however it ended.
    public void EndShot()
    {
        heldArrow.gameObject.SetActive(false);
        bowString.Release();
    }

    // Animation event: the hand is at the quiver.
    public void ArrowDrawn()
    {
        if (MidShot == false)
            return;

        arrowDrawnTime = Time.time;
        heldArrow.gameObject.SetActive(true);
    }

    // Animation event: the arrow is on the string, which follows the hand from here.
    public void ArrowNocked()
    {
        if (MidShot)
            bowString.Pull(stringHand);
    }

    // Animation event: the string goes, and the arrow with it.
    public void ArrowRelease()
    {
        if (MidShot == false)
            return;

        attackState.released = true;
        anim.SetTrigger(ShootId);
        FireArrow();
        EndShot();
    }

    private void FireArrow()
    {
        Vector3 direction = (AimPoint - shootPoint.position).normalized;

        // From the string, where the nocked arrow was, along the line through the bow.
        Vector3 position = shootPoint.position - direction * Vector3.Distance(shootPoint.position, heldArrow.position);
        Quaternion rotation = Quaternion.LookRotation(direction);

        GameObject newArrow =
            ObjectPool.instance != null
                ? ObjectPool.instance.GetObject(arrowPrefab)
                : Instantiate(arrowPrefab, position, rotation);

        // The body as well as the transform: it interpolates, and would otherwise write its old pose back over this.
        Rigidbody arrowBody = newArrow.GetComponent<Rigidbody>();
        newArrow.transform.SetPositionAndRotation(position, rotation);
        arrowBody.position = position;
        arrowBody.rotation = rotation;

        newArrow.GetComponent<Arrow>().ArrowSetup(this, arrowRange);
        arrowBody.linearVelocity = direction * arrowSpeed;
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
                source = arrow,
                isCritical = target is IStaggerable staggerable && staggerable.IsStaggered,
                unparryable = true,
            }
        );
    }

    // The next patrol point, kept on his ground: a prefab offset can land off a perch or outside the leash. He walks
    // out from his post along the NavMesh and stops where it ends, so a point he can't reach still gives him a walk.
    public Vector3 NextPatrolPoint() => KeepToPost(GetPatrolDestination());

    // The nearest he can get to `wanted` without leaving his post. Sits on the NavMesh.
    public Vector3 KeepToPost(Vector3 wanted)
    {
        Vector3 fromHome = wanted - home;
        fromHome.y = 0f;
        Vector3 target = home + Vector3.ClampMagnitude(fromHome, leashRadius);

        NavMesh.Raycast(home, target, out NavMeshHit hit, navFilter);
        return hit.position;
    }

    public Vector3 Home => home;
}
