using UnityEngine;

public class IdleState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public IdleState_BlightedBrute(
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

        stateTimer = enemyBase.idleTime;
    }

    public override void Update()
    {
        base.Update();
        if (blightedBrute.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(blightedBrute.recoveryState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(blightedBrute.moveState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
