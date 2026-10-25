using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField]
    private float damage = 15f;

    [SerializeField]
    private float poiseDamage = 15f;

    private Collider cd;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private TrailRenderer trailRenderer;

    [SerializeField]
    private GameObject arrowImpactFX;

    private Vector3 startPosition;
    private float flyDistance;
    private bool arrowDisabled;

    protected virtual void Awake()
    {
        cd = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        meshRenderer = GetComponent<MeshRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
    }

    public void ArrowSetup(float flyDistance = 100, float impactForce = 100, GameObject owner = null)
    {
        arrowDisabled = false;
        cd.enabled = true;
        if (meshRenderer != null)
            meshRenderer.enabled = true;

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.time = .25f;
        }

        if (owner != null && cd != null)
        {
            foreach (var ownerCollider in owner.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(cd, ownerCollider, true);
        }

        startPosition = transform.position;
        this.flyDistance = flyDistance + .5f; // magic number .5f is a length of tip of the laser ( Check method UpdateAimVisuals() on PlayerAim script) ;
    }

    protected virtual void Update()
    {
        FadeTrailIfNeeded();
        DisableArrowIfNeeded();
        ReturnToPoolIfNeeded();
    }

    protected void ReturnToPoolIfNeeded()
    {
        if (trailRenderer != null && trailRenderer.time < 0)
            ReturnArrowToPool();
    }

    protected void DisableArrowIfNeeded()
    {
        if (Vector3.Distance(startPosition, transform.position) > flyDistance && !arrowDisabled)
        {
            if (cd != null)
                cd.enabled = false;
            if (meshRenderer != null)
                meshRenderer.enabled = false;
            arrowDisabled = true;
        }
    }

    protected void FadeTrailIfNeeded()
    {
        if (
            trailRenderer != null
            && Vector3.Distance(startPosition, transform.position) > flyDistance - 1.5f
        )
            trailRenderer.time -= 2 * Time.deltaTime; // magic number 2 is choosen trhou testing
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        IDamageable target = collision.collider.GetComponentInParent<IDamageable>();
        if (target != null && target is not PlayerCombat)
        {
            if (cd != null)
                Physics.IgnoreCollision(cd, collision.collider, true);
            return;
        }

        CreateImpactFx();
        ReturnArrowToPool();

        if (target is PlayerCombat)
        {
            Vector3 direction = rb.linearVelocity.normalized;
            Vector3 point =
                collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : collision.collider.ClosestPoint(transform.position);

            target.TakeHit(
                new DamageInfo
                {
                    amount = damage,
                    poiseDamage = poiseDamage,
                    point = point,
                    direction = direction,
                    source = gameObject,
                    isCritical = target is IStaggerable staggerable && staggerable.IsStaggered,
                }
            );
        }
    }

    protected void ReturnArrowToPool()
    {
        if (ObjectPool.instance != null && TryGetComponent<PooledObject>(out _))
            ObjectPool.instance.ReturnObject(gameObject);
        else
            Destroy(gameObject);
    }

    protected void CreateImpactFx()
    {
        if (arrowImpactFX == null || ObjectPool.instance == null)
            return;

        GameObject newImpactFx = ObjectPool.instance.GetObject(arrowImpactFX);
        ObjectPool.instance.ReturnObject(newImpactFx, 1);
    }
}
