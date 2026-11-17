using UnityEngine;

// One fast, weak swing: a short tell (he tracks the player), a commit, a small step in as the blade lands, then
// the follow-through. A committed swing carries on through a hit; a parry or a broken poise stops it.
public class AttackState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public AttackState_CursedTwins(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        twin = enemyBase as CursedTwins;
    }

    private static readonly int SwingSpeedId = Animator.StringToHash("SwingSpeed");

    private CursedTwins.Swing current;
    private float elapsed;
    private bool committed;
    private bool landed;
    private Vector3 line;
    private float stepped;
    private float stepLength;

    // From the commit on, hits don't stop the swing.
    public bool Committed => committed;

    // When this swing started (Time.time), to check the pair swing together.
    public float StartedAt { get; private set; }

    public override void Enter()
    {
        base.Enter();
        twin.agent.isStopped = true;
        twin.agent.velocity = Vector3.zero;

        current = twin.NextSwing();
        elapsed = 0f;
        committed = false;
        landed = false;
        stepped = 0f;
        stepLength = 0f;
        StartedAt = Time.time;

        // The clip plays at whatever speed lands its blade at the end of the tell.
        twin.anim.SetFloat(SwingSpeedId, current.contact / Mathf.Max(twin.windupTime, 0.05f));

        // Straight into the clip: a transition still blending out can't be interrupted, so the bool alone starts it late.
        twin.anim.CrossFadeInFixedTime(current.state, 0.1f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        if (committed == false)
        {
            if (elapsed < twin.windupTime - twin.commitTime)
                twin.transform.rotation = twin.FaceTarget(twin.player.position);
            else
            {
                committed = true;
                line = twin.transform.forward;
                stepLength = twin.lungeDistance;
            }
        }

        // The step in, easing out to end as the blade lands, stopping short at a player in the way.
        float stepStart = twin.windupTime - twin.lungeLead;
        if (committed && elapsed >= stepStart && stepped < stepLength)
        {
            if (PlayerAhead())
                stepLength = stepped;

            float t = Mathf.Clamp01((elapsed - stepStart) / Mathf.Max(twin.lungeLead, 0.01f));
            float target = stepLength * (1f - (1f - t) * (1f - t));
            if (twin.agent.enabled)
                twin.agent.Move(line * (target - stepped));
            stepped = target;
        }

        if (triggerCalled)
            stateMachine.ChangeState(twin.recoveryState);
    }

    // AttackHit: the blade crosses in front of him. One check per swing.
    public void BladeLands()
    {
        if (landed)
            return;

        landed = true;
        twin.anim.SetFloat(SwingSpeedId, twin.followThroughSpeed);
        twin.LandBlow();
    }

    private bool PlayerAhead()
    {
        Vector3 toPlayer = twin.player.position - twin.transform.position;
        toPlayer.y = 0f;
        float along = Vector3.Dot(toPlayer, line);
        float across = (toPlayer - line * along).magnitude;
        return along > 0f && along < twin.lungeStopDistance && across < twin.agent.radius + 0.5f;
    }
}
