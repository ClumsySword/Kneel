using UnityEngine;

public class RecoveryState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    public RecoveryState_HollowArcher(
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

        HollowArcher.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        HollowArcher.transform.rotation = HollowArcher.FaceTarget(HollowArcher.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(HollowArcher.chaseState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
