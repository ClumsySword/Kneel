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

        if (TheFool.PlayerInAttackRange())
            stateMachine.ChangeState(TheFool.attackState);

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
