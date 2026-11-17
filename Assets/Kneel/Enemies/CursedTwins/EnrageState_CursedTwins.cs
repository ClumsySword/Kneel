using UnityEngine;

// His partner has fallen: he roars over the body, and from now on hits twice as hard. Lights don't stop the roar.
public class EnrageState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public EnrageState_CursedTwins(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        twin = enemyBase as CursedTwins;
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = twin.enrageTime;
        twin.agent.isStopped = true;
        twin.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();
        twin.transform.rotation = twin.FaceTarget(twin.player.position);

        if (stateTimer < 0)
            stateMachine.ChangeState(twin.chaseState);
    }
}
