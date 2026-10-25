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

        if (blightedBrute.PlayerInAttackRange())
            stateMachine.ChangeState(blightedBrute.attackState);

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
