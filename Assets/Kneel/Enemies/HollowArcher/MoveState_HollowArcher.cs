using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;

public class MoveState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;
    private Vector3 destination;

    public MoveState_HollowArcher(
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

        HollowArcher.agent.speed = HollowArcher.moveSpeed;

        destination = HollowArcher.GetPatrolDestination();
        HollowArcher.agent.SetDestination(destination);
    }

    public override void Update()
    {
        base.Update();

        if (HollowArcher.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(HollowArcher.recoveryState);
            return;
        }

        HollowArcher.transform.rotation = HollowArcher.FaceTarget(GetNextPathPoint());

        if (HollowArcher.agent.remainingDistance <= HollowArcher.agent.stoppingDistance + .05f)
            HollowArcher.stateMachine.ChangeState(HollowArcher.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
