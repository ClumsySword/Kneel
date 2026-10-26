using UnityEngine;

// Short flinch from a hit that didn't break poise.
public class HitState_TheFool : EnemyState
{
    private TheFool TheFool;

    public HitState_TheFool(
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

        stateTimer = TheFool.hitStunTime;
        TheFool.telegraph = 0f;
        TheFool.agent.isStopped = true;
        TheFool.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        TheFool.anim.Play("Hit", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(TheFool.chaseState);
    }
}
