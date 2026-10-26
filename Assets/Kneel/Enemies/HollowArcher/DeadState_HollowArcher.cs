using UnityEngine;

public class DeadState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    public DeadState_HollowArcher(
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

        HollowArcher.telegraph = 0f;
        HollowArcher.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);

        // The body stays where it fell: no more pathing, and nothing can hit or bump into it.
        HollowArcher.agent.isStopped = true;
        HollowArcher.agent.enabled = false;

        foreach (Collider bodyCollider in HollowArcher.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }

    // Dead is final: no transitions out.
}
