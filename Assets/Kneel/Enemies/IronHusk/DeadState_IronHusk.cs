using UnityEngine;

public class DeadState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public DeadState_IronHusk(
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

        ironHusk.telegraph = 0f;
        ironHusk.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);

        // The body stays where it fell: no more pathing, and nothing can hit or bump into it.
        ironHusk.agent.isStopped = true;
        ironHusk.agent.enabled = false;

        foreach (Collider bodyCollider in ironHusk.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }

    // Dead is final: no transitions out.
}
