using UnityEngine;

// After a swing he stands a moment: the player's window.
public class RecoveryState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public RecoveryState_CursedTwins(
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
        stateTimer = twin.recoveryTime;
        twin.agent.isStopped = true;
        twin.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(twin.chaseState);
    }
}
