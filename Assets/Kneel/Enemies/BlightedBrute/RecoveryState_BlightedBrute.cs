using UnityEngine;

public class RecoveryState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public RecoveryState_BlightedBrute(
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

        blightedBrute.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        blightedBrute.transform.rotation = blightedBrute.FaceTarget(blightedBrute.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(blightedBrute.chaseState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
