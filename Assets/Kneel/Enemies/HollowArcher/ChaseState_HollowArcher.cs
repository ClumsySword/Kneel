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

        // In range: stop and turn to the player, and only start the attack once facing them.
        if (HollowArcher.PlayerInAttackRange())
        {
            HollowArcher.agent.isStopped = true;
            HollowArcher.agent.velocity = Vector3.zero;
            HollowArcher.transform.rotation = HollowArcher.FaceTarget(HollowArcher.player.position);
            if (HollowArcher.IsFacing(HollowArcher.player.position))
                stateMachine.ChangeState(HollowArcher.attackState);
            return;
        }

        HollowArcher.agent.isStopped = false;
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
