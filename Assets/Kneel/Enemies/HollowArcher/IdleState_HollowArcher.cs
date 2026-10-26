using UnityEngine;

public class IdleState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    public IdleState_HollowArcher(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        HollowArcher = enemyBase as HollowArcher;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemyBase.idleTime;
    }

    public override void Update()
    {
        base.Update();
        if (HollowArcher.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(HollowArcher.recoveryState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(HollowArcher.moveState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
