using UnityEngine;

// Short flinch from a light hit once his armour is off. A charge carries on through it.
public class HitState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public HitState_IronHusk(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ironHusk = enemyBase as IronHusk;
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = ironHusk.hitStunTime;
        ironHusk.telegraph = 0f;
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        ironHusk.anim.CrossFadeInFixedTime("Hit", 0.05f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ironHusk.chaseState);
    }
}
