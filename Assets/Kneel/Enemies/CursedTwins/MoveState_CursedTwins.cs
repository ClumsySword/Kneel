using UnityEngine;

// Walking his patrol.
public class MoveState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public MoveState_CursedTwins(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        twin = enemyBase as CursedTwins;
    }

    public override void Enter()
    {
        base.Enter();
        twin.agent.speed = twin.moveSpeed;
        twin.agent.isStopped = false;
        twin.agent.SetDestination(twin.GetPatrolDestination());
    }

    public override void Update()
    {
        base.Update();

        if (twin.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(twin.noticeState);
            return;
        }

        twin.transform.rotation = twin.FaceTarget(GetNextPathPoint());

        if (twin.agent.remainingDistance <= twin.agent.stoppingDistance + .05f)
            stateMachine.ChangeState(twin.idleState);
    }
}
