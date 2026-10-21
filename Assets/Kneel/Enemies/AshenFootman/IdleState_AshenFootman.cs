using UnityEngine;

public class IdleState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public IdleState_AshenFootman(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ashenFootman = enemyBase as AshenFootman;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = enemyBase.idleTime;
    }

    public override void Update()
    {
        base.Update();
        if (ashenFootman.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(ashenFootman.recoveryState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(ashenFootman.moveState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
