using UnityEngine;

// He spots the player and squares up, and his partner turns with him: they come as a pair.
public class NoticeState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public NoticeState_CursedTwins(
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
        twin.agent.isStopped = true;
        twin.Alert();
    }

    public override void Update()
    {
        base.Update();
        twin.transform.rotation = twin.FaceTarget(twin.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(twin.chaseState);
    }
}
