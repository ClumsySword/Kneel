using UnityEngine;

public class RecoveryState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public RecoveryState_AshenFootman(
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

        ashenFootman.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        ashenFootman.transform.rotation = ashenFootman.FaceTarget(ashenFootman.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(ashenFootman.chaseState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
