using UnityEngine;

public class Arrow : MonoBehaviour
{
    private const int IgnoreRaycastLayer = 2;

    // The archer that fired this arrow. It owns the damage numbers; the arrow only reports what it hit.
    private HollowArcher shooter;

    private Collider cd;
    private Rigidbody rb;
    private MeshRenderer meshRenderer;
    private TrailRenderer trailRenderer;

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

    public void ArrowSetup(HollowArcher owner, float flyDistance = 100, float impactForce = 100)
    {
        shooter = owner;
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
        if (arrowDisabled && (trailRenderer == null || trailRenderer.time < 0))
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

    // The collider is a trigger: as a solid collider the player's CharacterController would climb
    // over the arrow when it arrived, lifting the player slightly on every hit.
    protected virtual void OnTriggerEnter(Collider other)
    {
        // Fly through other triggers (encounter and arena volumes); only solid colliders stop the arrow.
        if (other.isTrigger)
            return;

        // Nor do the levels' unseen boundary caps: an arrow can't stop in mid-air at a wall nobody can see.
        if (other.gameObject.layer == IgnoreRaycastLayer)
            return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null && target is not PlayerCombat)
        {
            if (cd != null)
                Physics.IgnoreCollision(cd, other, true);
            return;
        }

        // Rolled through: the arrow flies on past the player.
        if (target is PlayerCombat rolling && rolling.IsInvulnerable)
            return;

        CreateImpactFx();
        ReturnArrowToPool();

        if (target is PlayerCombat && shooter != null)
        {
            Vector3 direction = rb.linearVelocity.normalized;
            Vector3 point = other.ClosestPoint(transform.position);

            shooter.OnArrowHit(target, point, direction, gameObject);
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
        // The collider sits on the tip, and the arrow's origin is its nock.
        HitSpark.Spawn(cd.bounds.center, -transform.forward, new Color(1f, 0.7f, 0.35f), 8);
    }
}
