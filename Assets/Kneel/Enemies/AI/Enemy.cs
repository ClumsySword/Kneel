using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class Enemy : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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

    [Header("Feedback Data")]
    // Height above the feet where damage numbers appear.
    public float popupHeight = 2f;

    public Transform player { get; private set; }
    public Animator anim { get; private set; }
    public NavMeshAgent agent { get; private set; }
    public EnemyStateMachine stateMachine { get; private set; }
    public Health health { get; private set; }

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

    // One overlap check `reach` in front of the enemy. Returns true if it hit the player.
    public bool TryAttackHit(float reach)
    {
        Vector3 center = AttackCenter(reach);
        int count = Physics.OverlapSphereNonAlloc(center, attackRadius, overlapBuffer, playerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            IDamageable target = overlapBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || (UnityEngine.Object)target == this)
                continue;

            target.TakeHit(new DamageInfo
            {
                amount = attackDamage,
                poiseDamage = attackPoiseDamage,
                point = overlapBuffer[i].ClosestPoint(center),
                direction = transform.forward,
                source = gameObject,
                isCritical = false,
            });
            return true;
        }

        return false;
    }

    public bool PlayerInAgressionRange() =>
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
            turnSpeed * Time.deltaTime
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
