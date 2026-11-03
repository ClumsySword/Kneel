using UnityEngine;

public class ChaseState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;
    private float lastTimeUpdatedDestination;
    private float lostPlayerTime;

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
        lostPlayerTime = 0f;
    }

    public override void Update()
    {
        base.Update();

        // In range: stop and turn to the player, and only start the shot once facing them and ready.
        if (HollowArcher.PlayerInAttackRange())
        {
            Hold();
            if (HollowArcher.IsFacing(HollowArcher.player.position) && HollowArcher.CanStartShot)
                stateMachine.ChangeState(HollowArcher.attackState);
            return;
        }

        // Out of sight for long enough, he goes back to his post.
        lostPlayerTime = HollowArcher.PlayerInAgressionRange() ? 0f : lostPlayerTime + Time.deltaTime;
        if (lostPlayerTime > HollowArcher.giveUpTime)
        {
            stateMachine.ChangeState(HollowArcher.moveState);
            return;
        }

        // Comes on as far as his post lets him. At the edge of it he stands and watches instead of walking on the spot.
        Vector3 goal = HollowArcher.KeepToPost(HollowArcher.player.position);
        Vector3 toGoal = goal - HollowArcher.transform.position;
        toGoal.y = 0f;
        if (toGoal.magnitude <= HollowArcher.agent.stoppingDistance + 0.1f)
        {
            Hold();
            return;
        }

        HollowArcher.agent.isStopped = false;
        HollowArcher.transform.rotation = HollowArcher.FaceTarget(GetNextPathPoint());

        if (CanUpdateDestination())
        {
            HollowArcher.agent.destination = goal;
        }
    }

    public override void Exit()
    {
        base.Exit();
    }

    private void Hold()
    {
        HollowArcher.agent.isStopped = true;
        HollowArcher.agent.velocity = Vector3.zero;
        HollowArcher.transform.rotation = HollowArcher.FaceTarget(HollowArcher.player.position);
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
