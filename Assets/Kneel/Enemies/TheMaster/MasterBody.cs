using UnityEngine;

// What the Master's two bodies share (The Master.md): Phase 1's duellist (MasterP1) and Phase 2's Ash Colossus
// (AshColossus). Each is an Enemy with its own states; this holds the blows they deal, how far and where the player
// is, the walk feed, and their place in the hall (its hazards hurt them, its chandeliers fade in front of them).
public abstract class MasterBody : Enemy, IHallBoss
{
    protected static readonly int MoveRateId = Animator.StringToHash("MoveRate");
    protected static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");

    public MasterSettings settings;
    public BossTell bossTell;

    // The sword's tip (glint and blade checks) and the curse's hand: P1's left palm, the Colossus's claw.
    public Transform swordTip;
    public Transform palm;

    // The walk clip's own travel speed on him (m/s), for the Move tree's playback rate.
    public float walkClipSpeed = 2.2f;

    // Set by MasterBoss; the arena's hazard director can veto a blow that would land on top of a hazard (§8 pacing).
    public static System.Func<float, bool> HazardImpactNear;

    // Hits pass through him (the intro, the beats, the transition, the fall).
    public bool Untouchable { get; set; }

    public bool Engaged { get; protected set; }

    public abstract bool IsDead { get; }

    public abstract void HazardHit(float damage, float stagger);

    // Raised whenever one of his blows lands on the player (after the player's guard has dealt with it), with
    // whether it took any health (block chip included).
    public event System.Action<bool> OnBlowLanded;

    private Renderer bodyRenderer;

    protected override void Start()
    {
        base.Start();
        bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    // The hall's chandeliers fade while they hang between the camera and him (Kneel.FightClearance), and its hazards
    // look for him (HallActors).
    protected virtual void OnEnable()
    {
        Kneel.FightClearance.Subjects.Add(transform);
        HallActors.Bosses.Add(this);
    }

    protected virtual void OnDisable()
    {
        Kneel.FightClearance.Subjects.Remove(transform);
        HallActors.Bosses.Remove(this);
    }

    public float PlayerDistance()
    {
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    // He never starts an attack the camera can't see (§1). `height` is where on him the camera has to see.
    public bool OnScreen(float height = 2f)
    {
        Camera cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up * height);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
    }

    // Plays the Move/Circle tree: Speed = the agent's real speed, MoveRate speeds the walk up past the clip's own.
    public void FeedLocomotion()
    {
        UpdateSpeedParameter();
        float speed = agent.enabled ? agent.velocity.magnitude : 0f;
        anim.SetFloat(MoveRateId, Mathf.Max(1f, speed / Mathf.Max(walkClipSpeed, 0.1f)));
    }

    public void SetMoveSpeed(float speed) => anim.SetFloat(MoveSpeedId, speed);

    public void Stop()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    public void Go(Vector3 destination, float speed)
    {
        if (agent.enabled == false || agent.isOnNavMesh == false) return;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(destination);
    }

    public PlayerCombat PlayerCombat => player != null ? player.GetComponent<PlayerCombat>() : null;

    // One blow in an arc in front of him: true if it reached the player (whatever the player's guard did with it).
    public bool StrikeArc(float reach, float arc, float damage, float poiseDamage, bool unblockable = false, float kneel = 0f, float impact = 0f)
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.magnitude > reach + 0.35f || Vector3.Angle(transform.forward, to) > arc * 0.5f)
            return false;

        return Deal(damage, poiseDamage, unblockable, unblockable, kneel, impact, transform.forward);
    }

    // A straight thrust or line ahead of him, from `start` metres out.
    public bool StrikeLine(float length, float width, float damage, float poiseDamage, float start = 0f)
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float along = Vector3.Dot(to, transform.forward);
        float across = (to - transform.forward * along).magnitude;
        if (along < start - 0.35f || along > length + 0.35f || across > width * 0.5f + 0.35f)
            return false;

        return Deal(damage, poiseDamage, false, false, 0f, 0f, transform.forward);
    }

    public bool Deal(float damage, float poiseDamage, bool unblockable, bool unparryable, float kneel, float impact, Vector3 direction, bool ignoresRoll = false)
    {
        direction.y = 0f;
        return Deal(new DamageInfo
        {
            amount = damage,
            poiseDamage = poiseDamage,
            direction = direction.normalized,
            unblockable = unblockable,
            unparryable = unparryable,
            kneelTime = kneel,
            impact = impact,
            ignoresRoll = ignoresRoll,
        });
    }

    // Any blow of his, filled in with the point and source.
    public bool Deal(DamageInfo info)
    {
        var target = player.GetComponent<IDamageable>();
        if (target == null) return false;
        info.point = player.position + Vector3.up * 1.2f;
        info.source = gameObject;
        Health playerHealth = player.GetComponent<Health>();
        float before = playerHealth != null ? playerHealth.Current : 0f;
        target.TakeHit(info);
        OnBlowLanded?.Invoke(playerHealth != null && playerHealth.Current < before);
        return true;
    }

    public bool BodyVisible => bodyRenderer == null || bodyRenderer.isVisible;
}
