using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;

public class MoveState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;
    private Vector3 destination;

    public MoveState_BlightedBrute(
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

        blightedBrute.agent.speed = blightedBrute.moveSpeed;

        destination = blightedBrute.GetPatrolDestination();
        blightedBrute.agent.SetDestination(destination);
    }

    public override void Update()
    {
        base.Update();

        if (blightedBrute.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(blightedBrute.recoveryState);
            return;
        }

        blightedBrute.transform.rotation = blightedBrute.FaceTarget(GetNextPathPoint());

        if (blightedBrute.agent.remainingDistance <= blightedBrute.agent.stoppingDistance + .05f)
            blightedBrute.stateMachine.ChangeState(blightedBrute.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
