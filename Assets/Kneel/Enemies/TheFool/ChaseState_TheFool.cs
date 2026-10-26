using UnityEngine;

public class ChaseState_TheFool : EnemyState
{
    private TheFool TheFool;
    private float lastTimeUpdatedDestination;

    public ChaseState_TheFool(
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
        TheFool.agent.speed = TheFool.chaseSpeed;
        TheFool.agent.isStopped = false;
    }

    public override void Update()
    {
        base.Update();

        // In range: stop and turn to the player, and only start the attack once facing them.
        if (TheFool.PlayerInAttackRange())
        {
            TheFool.agent.isStopped = true;
            TheFool.agent.velocity = Vector3.zero;
            TheFool.transform.rotation = TheFool.FaceTarget(TheFool.player.position);
            if (TheFool.IsFacing(TheFool.player.position))
                stateMachine.ChangeState(TheFool.attackState);
            return;
        }

        TheFool.agent.isStopped = false;
        TheFool.transform.rotation = TheFool.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            TheFool.agent.destination = TheFool.player.position;
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
