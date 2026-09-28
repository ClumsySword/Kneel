using UnityEngine;

public class ChaseState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;
    private float lastTimeUpdatedDestination;

    public ChaseState_AshenFootman(
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
        ashenFootman.agent.speed = ashenFootman.chaseSpeed;
        ashenFootman.agent.isStopped = false;
    }

    public override void Update()
    {
        base.Update();

        if (ashenFootman.PlayerInAttackRange())
            stateMachine.ChangeState(ashenFootman.attackState);

        ashenFootman.transform.rotation = ashenFootman.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            ashenFootman.agent.destination = ashenFootman.player.position;
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
