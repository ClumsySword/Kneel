using UnityEngine;
using UnityEngine.AI;

public class MoveState_IronHusk : EnemyState
{
    private IronHusk ironHusk;
    private Vector3 destination;

    public MoveState_IronHusk(
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

        ironHusk.agent.speed = ironHusk.moveSpeed;

        destination = ironHusk.GetPatrolDestination();
        ironHusk.agent.SetDestination(destination);
    }

    public override void Update()
    {
        base.Update();

        if (ironHusk.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(ironHusk.noticeState);
            return;
        }

        ironHusk.transform.rotation = ironHusk.FaceTarget(GetNextPathPoint());

        if (ironHusk.agent.remainingDistance <= ironHusk.agent.stoppingDistance + .05f)
            ironHusk.stateMachine.ChangeState(ironHusk.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
