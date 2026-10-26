using UnityEngine;

public class RecoveryState_TheFool : EnemyState
{
    private TheFool TheFool;

    public RecoveryState_TheFool(
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

    public override void Exit()
    {
        base.Exit();
    }
}
