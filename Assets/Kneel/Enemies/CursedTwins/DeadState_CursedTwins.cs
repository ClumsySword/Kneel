using UnityEngine;

// Dead is final: the body stays where it fell.
public class DeadState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public DeadState_CursedTwins(
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
        twin.telegraph = 0f;
        twin.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);
        // No more pathing, and nothing can hit or bump into the body.
        twin.agent.isStopped = true;
        twin.agent.enabled = false;
        foreach (Collider bodyCollider in twin.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }
}
