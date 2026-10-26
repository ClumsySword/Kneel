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

        // In range: stop and turn to the player, and only start the attack once facing them.
        if (ashenFootman.PlayerInAttackRange())
        {
            ashenFootman.agent.isStopped = true;
            ashenFootman.agent.velocity = Vector3.zero;
            ashenFootman.transform.rotation = ashenFootman.FaceTarget(ashenFootman.player.position);
            if (ashenFootman.IsFacing(ashenFootman.player.position))
                stateMachine.ChangeState(ashenFootman.attackState);
            return;
        }

        ashenFootman.agent.isStopped = false;
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
