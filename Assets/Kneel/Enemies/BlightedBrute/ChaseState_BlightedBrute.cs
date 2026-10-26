using UnityEngine;

public class ChaseState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;
    private float lastTimeUpdatedDestination;

    public ChaseState_BlightedBrute(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        blightedBrute = enemyBase as BlightedBrute;
    }

    public override void Enter()
    {
        base.Enter();
        blightedBrute.agent.speed = blightedBrute.chaseSpeed;
        blightedBrute.agent.isStopped = false;
    }

    public override void Update()
    {
        base.Update();

        // In range: stop and turn to the player, and only start the attack once facing them.
        if (blightedBrute.PlayerInAttackRange())
        {
            blightedBrute.agent.isStopped = true;
            blightedBrute.agent.velocity = Vector3.zero;
            blightedBrute.transform.rotation = blightedBrute.FaceTarget(blightedBrute.player.position);
            if (blightedBrute.IsFacing(blightedBrute.player.position))
                stateMachine.ChangeState(blightedBrute.attackState);
            return;
        }

        blightedBrute.agent.isStopped = false;
        blightedBrute.transform.rotation = blightedBrute.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            blightedBrute.agent.destination = blightedBrute.player.position;
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
