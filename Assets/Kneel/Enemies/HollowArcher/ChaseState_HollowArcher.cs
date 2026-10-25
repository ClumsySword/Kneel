using UnityEngine;

public class ChaseState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;
    private float lastTimeUpdatedDestination;

    public ChaseState_HollowArcher(
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
        HollowArcher.agent.speed = HollowArcher.chaseSpeed;
        HollowArcher.agent.isStopped = false;
    }

    public override void Update()
    {
        base.Update();

        if (HollowArcher.PlayerInAttackRange())
            stateMachine.ChangeState(HollowArcher.attackState);

        HollowArcher.transform.rotation = HollowArcher.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            HollowArcher.agent.destination = HollowArcher.player.position;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    private bool CanUpdateDestination()
    {
        if (Time.time > lastTimeUpdatedDestination + .25f)
        {
            lastTimeUpdatedDestination = Time.time;
            return true;
        }
        return false;
    }
}
