using UnityEngine;

// He spots the player and squares up to them, then comes for them.
public class NoticeState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public NoticeState_IronHusk(
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

        stateTimer = ironHusk.noticeTime;
        ironHusk.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        ironHusk.transform.rotation = ironHusk.FaceTarget(ironHusk.player.position);

        if (triggerCalled || stateTimer < 0)
            stateMachine.ChangeState(ironHusk.chaseState);
    }
}
