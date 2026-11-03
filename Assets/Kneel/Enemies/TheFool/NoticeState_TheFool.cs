using UnityEngine;

// He spots the player, strikes a pose with the sword, then comes for them.
public class NoticeState_TheFool : EnemyState
{
    private TheFool TheFool;

    public NoticeState_TheFool(
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

        TheFool.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        TheFool.transform.rotation = TheFool.FaceTarget(TheFool.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(TheFool.chaseState);
    }
}
