using UnityEngine;

// Short flinch from a hit that didn't break poise.
public class HitState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    public HitState_HollowArcher(
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

        stateTimer = HollowArcher.hitStunTime;
        HollowArcher.telegraph = 0f;
        HollowArcher.agent.isStopped = true;
        HollowArcher.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        HollowArcher.anim.Play("Hit", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(HollowArcher.chaseState);
    }
}
