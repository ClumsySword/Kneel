using UnityEngine;

// Training mannequin. Flinches and shows damage numbers when hit, never dies (Health refills).
// With attacking enabled it winds up (tinting red), turns toward a nearby player and kicks.
// Parrying the kick staggers it, and hits on a staggered dummy are critical.
[RequireComponent(typeof(Health))]
public class TargetDummy : MonoBehaviour, IDamageable, IParryable, IStaggerable
{
    private enum DummyState
    {
        Idle,
        Flinch,
        Windup,
        Attacking,
        Staggered,
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private float popupHeight = 2f;

    [Header("Attack Info")]
    public bool attackEnabled = false;

    public float attackDamage = 15f;

    // Seconds between attacks.
    public float attackInterval = 2.5f;

    // Telegraph before the kick starts.
    public float attackWindup = 0.6f;

    public float attackRange = 2.2f;

    // Seconds into the kick clip when the foot connects (measured peak foot speed).
    [SerializeField]
    private float attackHitTime = 0.38f;

    [SerializeField]
    private float attackDuration = 1f;

    [SerializeField]
    private float attackReach = 0.9f;

    [SerializeField]
    private float attackRadius = 0.8f;

    [SerializeField]
    private LayerMask playerMask;

    [SerializeField]
    private float turnRate = 360f;

    [Header("Reaction Info")]
    public float staggerDuration = 1.2f;

    [SerializeField]
    private float flinchDuration = 0.4f;

    private readonly Collider[] overlapBuffer = new Collider[8];

    private Health health;
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private Color[] baseColors;
    private Transform player;

    private DummyState state = DummyState.Idle;
    private float stateTime = 0f;
    private float cooldown = 0f;
    private bool attackLanded = false;
    private float flash = 0f;

    public bool IsStaggered => state == DummyState.Staggered;

    public float TotalDamage { get; private set; }

    public int HitCount { get; private set; }

    private void Awake()
    {
        health = GetComponent<Health>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            baseColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
        }
    }

    private void Start()
    {
        var playerCombat = FindAnyObjectByType<PlayerCombat>();
        if (playerCombat != null)
        {
            player = playerCombat.transform;
        }

        cooldown = attackInterval;
    }

    public void TakeHit(DamageInfo info)
    {
        health.ApplyDamage(info.amount);
        TotalDamage += info.amount;
        HitCount++;

        DamageNumberStyle style = info.isCritical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal;
        DamageNumber.Spawn(transform.position + Vector3.up * popupHeight, Mathf.RoundToInt(info.amount).ToString(), style);
        flash = 1f;

        // The kick itself has armour; everything else gets interrupted.
        if (state == DummyState.Attacking)
        {
            return;
        }

        animator.CrossFadeInFixedTime("Hit" + Random.Range(1, 4), 0.05f, 0, 0f);

        if (state != DummyState.Staggered)
        {
            SetState(DummyState.Flinch);
        }
    }

    public void OnParried(GameObject parrier)
    {
        SetState(DummyState.Staggered);
        animator.CrossFadeInFixedTime("Stagger", 0.05f, 0, 0f);
        cooldown = attackInterval;
    }

    private void SetState(DummyState next)
    {
        state = next;
        stateTime = 0f;
    }

    private void Update()
    {
        stateTime += Time.deltaTime;

        switch (state)
        {
            case DummyState.Idle:
                UpdateIdle();
                break;
            case DummyState.Flinch:
                if (stateTime >= flinchDuration)
                {
                    SetState(DummyState.Idle);
                }
                break;
            case DummyState.Windup:
                UpdateWindup();
                break;
            case DummyState.Attacking:
                UpdateAttacking();
                break;
            case DummyState.Staggered:
                if (stateTime >= staggerDuration)
                {
                    SetState(DummyState.Idle);
                    animator.CrossFadeInFixedTime("Idle", 0.2f, 0);
                }
                break;
        }

        UpdateTint();
    }

    private void UpdateIdle()
    {
        cooldown -= Time.deltaTime;

        if (attackEnabled == false || player == null || cooldown > 0f)
        {
            return;
        }

        if (FlatDistanceToPlayer() <= attackRange)
        {
            SetState(DummyState.Windup);
        }
    }

    private void UpdateWindup()
    {
        FacePlayer();

        if (attackEnabled == false)
        {
            SetState(DummyState.Idle);
            return;
        }

        if (stateTime >= attackWindup)
        {
            SetState(DummyState.Attacking);
            attackLanded = false;
            animator.CrossFadeInFixedTime("Attack", 0.1f, 0, 0f);
        }
    }

    private void UpdateAttacking()
    {
        if (attackLanded == false && stateTime >= attackHitTime)
        {
            attackLanded = true;
            TryHitPlayer();
        }

        if (stateTime >= attackDuration)
        {
            SetState(DummyState.Idle);
            cooldown = attackInterval;
            animator.CrossFadeInFixedTime("Idle", 0.2f, 0);
        }
    }

    private void TryHitPlayer()
    {
        Vector3 center = transform.position + transform.forward * attackReach + Vector3.up * 0.9f;
        int count = Physics.OverlapSphereNonAlloc(center, attackRadius, overlapBuffer, playerMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            IDamageable target = overlapBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || (Object)target == this)
            {
                continue;
            }

            target.TakeHit(new DamageInfo
            {
                amount = attackDamage,
                point = overlapBuffer[i].ClosestPoint(center),
                direction = transform.forward,
                source = gameObject,
                isCritical = false,
            });
            return;
        }
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(toPlayer);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnRate * Time.deltaTime);
        }
    }

    private float FlatDistanceToPlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude;
    }

    // White flash on hit, red ramp during the windup telegraph.
    private void UpdateTint()
    {
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 8f);
        float windup = state == DummyState.Windup ? Mathf.Clamp01(stateTime / attackWindup) : 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            Color color = Color.Lerp(baseColors[i], new Color(0.85f, 0.1f, 0.05f), windup * 0.8f);
            // Base colour multiplies the texture, so the flash has to go past white to brighten it.
            color = Color.Lerp(color, new Color(2.5f, 2.5f, 2.5f), flash);

            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * attackReach + Vector3.up * 0.9f, attackRadius);
    }
}
