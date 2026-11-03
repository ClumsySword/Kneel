using UnityEngine;

// One of his strings: two to four swings, each carrying him forward along its own line. Each swing has a wind-up (he
// tracks the player), a commit (the line is set), the lunge with the blade landing in it, and a follow-through. A parry,
// or any hit outside a committed swing, ends it (see TheFool).
public class AttackState_TheFool : EnemyState
{
    private static readonly int SwingSpeedId = Animator.StringToHash("SwingSpeed");

    private TheFool TheFool;

    private TheFool.SwingString current;
    private int swing;
    private float elapsed;
    private bool committed;
    private bool landed;
    private Vector3 line;
    private float lungeLength;
    private float lunged;

    public AttackState_TheFool(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName)
    {
        TheFool = enemyBase as TheFool;
    }

    // From the commit until the lunge ends, the swing can't be stopped by a hit. A parry still stops it.
    public bool Committed => committed && elapsed < Windup + TheFool.lungeTail;

    // The string under way, how many swings it has, and which of them this is.
    public string StringName => current.name;
    public int SwingCount => current.swings.Length;
    public int SwingIndex => swing;

    public float Windup => TheFool.SwingWindup(Current, swing);

    private TheFool.Swing Current => TheFool.swings[current.swings[swing]];

    public override void Enter()
    {
        base.Enter();
        TheFool.agent.isStopped = true;
        TheFool.agent.velocity = Vector3.zero;
        current = TheFool.NextString();
        StartSwing(0);
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        if (committed == false)
        {
            if (elapsed < Windup - TheFool.commitTime)
                TheFool.transform.rotation = TheFool.FaceTarget(TheFool.player.position);
            else
                Commit();
        }

        // The lunge: a fast burst that eases out, the blade landing two-thirds of the way along.
        float lungeStart = Windup - TheFool.lungeLead;
        float lungeTime = TheFool.lungeLead + TheFool.lungeTail;

        // He stops at a player still standing in his line, rather than running through them.
        if (committed && elapsed >= lungeStart && lunged < lungeLength && PlayerAhead())
            lungeLength = lunged;

        if (committed && elapsed >= lungeStart && lunged < lungeLength)
        {
            float t = Mathf.Clamp01((elapsed - lungeStart) / lungeTime);
            float target = lungeLength * (1f - (1f - t) * (1f - t));
            TheFool.Lunge(line * (target - lunged));
            lunged = target;

            // Over the breach.
            if (stateMachine.currentState != this)
                return;
        }

        if (triggerCalled)
        {
            if (swing + 1 < SwingCount)
                StartSwing(swing + 1);
            else
                stateMachine.ChangeState(TheFool.recoveryState);
        }
    }

    // AttackHit: the blade crosses in front of him. One check per swing, and the rest of the clip plays at its own pace.
    public void BladeLands()
    {
        if (landed)
            return;

        landed = true;
        TheFool.anim.SetFloat(SwingSpeedId, TheFool.followThroughSpeed);
        TheFool.TryAttackHit();
    }

    private void StartSwing(int index)
    {
        swing = index;
        elapsed = 0f;
        committed = false;
        landed = false;
        lunged = 0f;
        lungeLength = 0f;
        triggerCalled = false;

        // The clip plays at whatever speed lands its blade at the end of this swing's wind-up.
        TheFool.anim.SetFloat(SwingSpeedId, Current.contact / Windup);

        // Straight into the clip. A transition still blending out of the last state can't be interrupted, so the bool
        // alone starts the swing late and the hit no longer lands where the wind-up says.
        TheFool.anim.CrossFadeInFixedTime(Current.state, index == 0 ? 0.15f : 0.1f, 0, 0f);
    }

    // The swing's line is set: straight ahead, the whole lungeDistance. Only a player still standing in it stops it short,
    // so one who rolls out of it leaves him to carry on, which is what can take him over the breach.
    private void Commit()
    {
        committed = true;
        line = TheFool.transform.forward;
        lungeLength = TheFool.lungeDistance;
    }

    private bool PlayerAhead()
    {
        Vector3 toPlayer = TheFool.player.position - TheFool.transform.position;
        toPlayer.y = 0f;
        float along = Vector3.Dot(toPlayer, line);
        float across = (toPlayer - line * along).magnitude;
        return along > 0f && along < TheFool.lungeStopDistance && across < TheFool.agent.radius + 0.5f;
    }
}
