using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour, IBlindable, IForceStaggerable
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SpeedId = Animator.StringToHash("Speed");

    [Header("Attack Data")]
    public float aggressionRange;
    public float attackRange;
    public float attackMoveSpeed;

    // Damage dealt when the attack animation's AttackHit event fires.
    public float attackDamage = 15f;

    // The hit is a sphere this far in front of the enemy at chest height.
    public float attackReach = 1.1f;
    public float attackRadius = 0.8f;

    // Poise knocked off the player by the attack (see Poise).
    public float attackPoiseDamage = 30f;

    // How much heavier than a normal blow its attacks land (see DamageInfo.impact).
    public float attackImpact;

    // An attack only starts once the player is within this many degrees of straight ahead.
    public float attackFacingAngle = 15f;

    [SerializeField]
    private LayerMask playerMask;

    [Header("Poise Data")]
    public Poise poise = new Poise(30f, 2f, 20f);

    // Hits landed during an attack's wind-up deal this much more poise damage (a counter-hit).
    public float windupPoiseMultiplier = 2f;

    [Header("Idle Data")]
    public float idleTime;

    [Header("Move Data")]
    public float moveSpeed;
    public float chaseSpeed;
    public float turnSpeed;

    [SerializeField]
    private Transform[] patrolPoints;
    private int currentPatrolIndex;

    // Raised once when an enemy's health runs out, whatever killed it (the player's Ash meter listens).
    public static event Action<Enemy> Killed;

    // Turning speed while blinded, as a share of turnSpeed.
    private const float BlindTurnMultiplier = 0.25f;

    private float blindUntil = -999f;
    private bool killed;

    public Transform player { get; private set; }
    public Animator anim { get; private set; }
    public NavMeshAgent agent { get; private set; }
    public EnemyStateMachine stateMachine { get; private set; }
    public Health health { get; private set; }

    // Held by the F1 overlay's spawner: the state machine doesn't run and hits are ignored, so a placed encounter waits
    // in its first state until it's released. Each type's Update and TakeHit check it.
    public bool Frozen { get; set; }

    // 0..1 red telegraph shown while winding up an attack; set by the attack state.
    public float telegraph { get; set; }

    private readonly Collider[] overlapBuffer = new Collider[8];
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Color[] baseColors;
    private float flash;

    protected virtual void Awake()
    {
        stateMachine = new EnemyStateMachine();

        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        health = GetComponent<Health>();
        health.OnDepleted += OnHealthDepleted;
        poise.Reset();

        GameObject playerObject = GameObject.Find("Player");
        if (playerObject == null)
        {
            // The level's player may be renamed; the combat component is the reliable marker.
            var playerCombat = FindAnyObjectByType<PlayerCombat>();
            playerObject = playerCombat != null ? playerCombat.gameObject : null;
        }
        player = playerObject != null ? playerObject.transform : transform;

        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
        }
    }

    protected virtual void Start()
    {
        InitializePatrolPoints();
    }

    protected virtual void Update()
    {
        poise.Tick(Time.deltaTime);
        UpdateTint();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, aggressionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackCenter(), attackRadius);
    }

    public void AnimationTrigger() => stateMachine.currentState.AnimationTrigger();

    // Animation event on the attack clip at the moment the weapon connects.
    public virtual void AttackHit() => TryAttackHit();

    public bool TryAttackHit() => TryAttackHit(attackReach);

    public bool TryAttackHit(float reach) => TryAttackHit(reach, attackRadius, attackDamage, attackPoiseDamage);

    // One overlap check `reach` in front of the enemy. Returns true if it hit the player.
    public bool TryAttackHit(float reach, float radius, float damage, float poiseDamage)
    {
        Vector3 center = AttackCenter(reach);
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, playerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            IDamageable target = overlapBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || (UnityEngine.Object)target == this)
                continue;

            target.TakeHit(new DamageInfo
            {
                amount = damage,
                poiseDamage = poiseDamage,
                point = overlapBuffer[i].ClosestPoint(center),
                direction = transform.forward,
                source = gameObject,
                isCritical = false,
                impact = attackImpact,
            });
            return true;
        }

        return false;
    }

    // Blinded by Ash: it can't start an attack (each type's chase state checks this) and turns slowly.
    public bool IsBlind => Time.time < blindUntil;

    public bool CanStartAttack => IsBlind == false;

    public void Blind(float seconds) => blindUntil = Mathf.Max(blindUntil, Time.time + seconds);

    // Ashfall's stagger. Each type sends itself to its own stagger state; one that can't be staggered leaves this empty.
    public virtual void ForceStagger() { }

    private void OnHealthDepleted()
    {
        if (killed)
            return;

        killed = true;
        Killed?.Invoke(this);
    }

    public virtual bool PlayerInAgressionRange() =>
        Vector3.Distance(transform.position, player.position) < aggressionRange;

    public bool PlayerInAttackRange() =>
        Vector3.Distance(transform.position, player.position) < attackRange;

    public Vector3 GetPatrolDestination()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return transform.position;

        currentPatrolIndex++;

        if (currentPatrolIndex >= patrolPoints.Length)
            currentPatrolIndex = 0;

        return patrolPoints[currentPatrolIndex].transform.position;
    }

    // A spawned enemy drops the prefab's own patrol ring and stands where it was put until it sees the player.
    public void ClearPatrol()
    {
        if (patrolPoints != null)
        {
            foreach (Transform t in patrolPoints)
            {
                if (t != null)
                    Destroy(t.gameObject);
            }
        }

        patrolPoints = Array.Empty<Transform>();
    }

    private void InitializePatrolPoints()
    {
        if (patrolPoints == null)
            return;

        foreach (Transform t in patrolPoints)
            t.parent = null;
    }

    public Quaternion FaceTarget(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return transform.rotation;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget);

        Vector3 currentEulerAngles = transform.rotation.eulerAngles;

        float yRotation = Mathf.LerpAngle(
            currentEulerAngles.y,
            targetRotation.eulerAngles.y,
            turnSpeed * (IsBlind ? BlindTurnMultiplier : 1f) * Time.deltaTime
        );

        return Quaternion.Euler(currentEulerAngles.x, yRotation, currentEulerAngles.z);
    }

    public bool IsFacing(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return true;

        return Vector3.Angle(transform.forward, toTarget) <= attackFacingAngle;
    }

    // White flash on being hit.
    public void Flash() => flash = 1f;

    // Feeds a controller's speed blend tree with how fast the agent really moves (m/s), so the feet match it.
    protected void UpdateSpeedParameter()
    {
        anim.SetFloat(SpeedId, agent.enabled ? agent.velocity.magnitude : 0f);
    }

    // Takes the whole body out of the world, weapons included, or puts it back.
    protected void SetPresent(bool present)
    {
        foreach (Renderer bodyRenderer in renderers)
            bodyRenderer.enabled = present;

        foreach (Collider bodyCollider in GetComponents<Collider>())
            bodyCollider.enabled = present;

        agent.enabled = present;
    }

    private Vector3 AttackCenter() => AttackCenter(attackReach);

    private Vector3 AttackCenter(float reach) =>
        transform.position + transform.forward * reach + Vector3.up * 0.9f;

    private void UpdateTint()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 8f);

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = Color.Lerp(baseColors[i], new Color(0.85f, 0.1f, 0.05f), telegraph * 0.8f);
            // Base colour multiplies the texture, so the flash has to go past white to brighten it.
            color = Color.Lerp(color, new Color(2.5f, 2.5f, 2.5f), flash);

            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }
}
