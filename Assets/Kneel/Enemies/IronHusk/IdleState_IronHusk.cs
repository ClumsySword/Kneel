using UnityEngine;

public class IdleState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public IdleState_IronHusk(
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

        stateTimer = enemyBase.idleTime;
    }

    public override void Update()
    {
        base.Update();
        if (ironHusk.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(ironHusk.noticeState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(ironHusk.moveState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
