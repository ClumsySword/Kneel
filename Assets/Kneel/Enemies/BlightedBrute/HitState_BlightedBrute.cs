using UnityEngine;

// Short flinch from a hit that didn't break poise.
public class HitState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public HitState_BlightedBrute(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        blightedBrute = enemyBase as BlightedBrute;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = blightedBrute.hitStunTime;
        blightedBrute.telegraph = 0f;
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        blightedBrute.anim.Play("Hit", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(blightedBrute.chaseState);
    }
}
