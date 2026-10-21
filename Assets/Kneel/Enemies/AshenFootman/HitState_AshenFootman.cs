using UnityEngine;

// Short flinch from a hit that didn't break poise.
public class HitState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public HitState_AshenFootman(
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

        stateTimer = ashenFootman.hitStunTime;
        ashenFootman.telegraph = 0f;
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        ashenFootman.anim.Play("Hit", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ashenFootman.chaseState);
    }
}
