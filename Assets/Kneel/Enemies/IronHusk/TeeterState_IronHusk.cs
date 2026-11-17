using UnityEngine;

// A charge that met the breach's lip (L4 decision 18): he stops on the edge and flails for balance, wide open.
public class TeeterState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public TeeterState_IronHusk(
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
        stateTimer = ironHusk.teeterTime;
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ironHusk.chaseState);
    }
}
