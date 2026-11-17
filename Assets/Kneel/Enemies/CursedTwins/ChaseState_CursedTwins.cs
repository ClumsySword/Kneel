using UnityEngine;

// Closing on the player from his side of them. In reach and facing them, he waits on his partner's beat.
public class ChaseState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public ChaseState_CursedTwins(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        twin = enemyBase as CursedTwins;
    }

    private float lastTimeUpdatedDestination;

    public override void Enter()
    {
        base.Enter();
        twin.agent.speed = twin.chaseSpeed;
        twin.agent.isStopped = false;
        twin.ResetReady();
        lastTimeUpdatedDestination = -1f;
    }

    public override void Update()
    {
        base.Update();

        if (twin.PlayerInAttackRange())
        {
            twin.agent.isStopped = true;
            twin.agent.velocity = Vector3.zero;
            twin.transform.rotation = twin.FaceTarget(twin.player.position);
            if (twin.IsFacing(twin.player.position))
                twin.OnReadyToSwing();
            return;
        }

        twin.ResetReady();
        twin.agent.isStopped = false;
        twin.transform.rotation = twin.FaceTarget(GetNextPathPoint());

        if (Time.time > lastTimeUpdatedDestination + .25f)
        {
            lastTimeUpdatedDestination = Time.time;
            twin.agent.destination = twin.ChaseDestination();
        }
    }

    public override void Exit()
    {
        base.Exit();
        twin.ResetReady();
    }
}
