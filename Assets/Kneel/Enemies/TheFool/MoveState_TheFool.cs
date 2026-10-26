using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;

public class MoveState_TheFool : EnemyState
{
    private TheFool TheFool;
    private Vector3 destination;

    public MoveState_TheFool(
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

        TheFool.agent.speed = TheFool.moveSpeed;

        destination = TheFool.GetPatrolDestination();
        TheFool.agent.SetDestination(destination);
    }

    public override void Update()
    {
        base.Update();

        if (TheFool.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(TheFool.recoveryState);
            return;
        }

        TheFool.transform.rotation = TheFool.FaceTarget(GetNextPathPoint());

        if (TheFool.agent.remainingDistance <= TheFool.agent.stoppingDistance + .05f)
            TheFool.stateMachine.ChangeState(TheFool.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
