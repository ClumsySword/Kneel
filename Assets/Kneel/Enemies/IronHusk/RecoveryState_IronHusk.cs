using UnityEngine;

// After a charge he stands winded: the player's window to hit him.
public class RecoveryState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public RecoveryState_IronHusk(
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

        stateTimer = ironHusk.recoveryTime;
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ironHusk.chaseState);
    }
}
