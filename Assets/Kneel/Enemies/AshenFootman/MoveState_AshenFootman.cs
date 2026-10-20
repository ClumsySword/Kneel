using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;

public class MoveState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;
    private Vector3 destination;

    public MoveState_AshenFootman(
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

        ashenFootman.agent.speed = ashenFootman.moveSpeed;

        destination = ashenFootman.GetPatrolDestination();
        ashenFootman.agent.SetDestination(destination);
    }

    public override void Update()
    {
        base.Update();

        if (ashenFootman.PlayerInAgressionRange())
        {
            stateMachine.ChangeState(ashenFootman.recoveryState);
            return;
        }

        ashenFootman.transform.rotation = ashenFootman.FaceTarget(GetNextPathPoint());

        if (ashenFootman.agent.remainingDistance <= ashenFootman.agent.stoppingDistance + .05f)
            ashenFootman.stateMachine.ChangeState(ashenFootman.idleState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
