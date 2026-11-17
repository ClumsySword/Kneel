using UnityEngine;

// A short flinch from a hit that didn't break his poise.
public class HitState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public HitState_CursedTwins(
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
        stateTimer = twin.hitStunTime;
        twin.agent.isStopped = true;
        twin.agent.velocity = Vector3.zero;
        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        twin.anim.CrossFadeInFixedTime("Hit", 0.05f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(twin.chaseState);
    }
}
