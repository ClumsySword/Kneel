using UnityEngine;

public class ChaseState_IronHusk : EnemyState
{
    private IronHusk ironHusk;
    private float lastTimeUpdatedDestination;

    public ChaseState_IronHusk(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ironHusk = enemyBase as IronHusk;
    }

    public override void Enter()
    {
        base.Enter();
        ironHusk.agent.speed = ironHusk.chaseSpeed;
        ironHusk.agent.isStopped = false;
    }

    public override void Update()
    {
        base.Update();

        // In range with a clear run at them: stop and turn to the player, and only wind up once facing them.
        if (ironHusk.PlayerInAttackRange() && ironHusk.HasChargeLine())
        {
            ironHusk.agent.isStopped = true;
            ironHusk.agent.velocity = Vector3.zero;
            ironHusk.transform.rotation = ironHusk.FaceTarget(ironHusk.player.position);
            if (ironHusk.IsFacing(ironHusk.player.position))
                stateMachine.ChangeState(ironHusk.windupState);
            return;
        }

        ironHusk.agent.isStopped = false;
        ironHusk.transform.rotation = ironHusk.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            ironHusk.agent.destination = ironHusk.player.position;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    private bool CanUpdateDestination()
    {
        if (Time.time > lastTimeUpdatedDestination + .25f)
        {
            lastTimeUpdatedDestination = Time.time;
            return true;
        }
        return false;
    }
}
