using UnityEngine;

public class DeadState_TheFool : EnemyState
{
    private TheFool TheFool;

    public DeadState_TheFool(
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

        TheFool.telegraph = 0f;
        TheFool.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);

        // The body stays where it fell: no more pathing, and nothing can hit or bump into it.
        TheFool.agent.isStopped = true;
        TheFool.agent.enabled = false;

        foreach (Collider bodyCollider in TheFool.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }

    // Dead is final: no transitions out.
}
