using UnityEngine;

// Standing his post until the player comes near (or his partner sees them).
public class IdleState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public IdleState_CursedTwins(
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
        stateTimer = twin.idleTime;
    }

    public override void Update()
    {
        base.Update();

        if (twin.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(twin.noticeState);
            return;
        }

        if (stateTimer < 0)
            stateMachine.ChangeState(twin.moveState);
    }
}
