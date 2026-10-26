using UnityEngine;

public class IdleState_TheFool : EnemyState
{
    private TheFool TheFool;

    public IdleState_TheFool(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        TheFool = enemyBase as TheFool;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemyBase.idleTime;
    }

    public override void Update()
    {
        base.Update();
        if (TheFool.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(TheFool.recoveryState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(TheFool.moveState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
