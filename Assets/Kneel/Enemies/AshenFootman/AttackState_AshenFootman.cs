using Unity.VisualScripting;
using UnityEngine;

public class AttackState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public AttackState_AshenFootman(
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
        ashenFootman.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        ashenFootman.transform.rotation = ashenFootman.FaceTarget(GetNextPathPoint());

        if (triggerCalled)
            stateMachine.ChangeState(ashenFootman.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
