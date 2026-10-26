using UnityEngine;

public class DeadState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public DeadState_BlightedBrute(
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

        blightedBrute.telegraph = 0f;
        blightedBrute.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);

        // The body stays where it fell: no more pathing, and nothing can hit or bump into it.
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.enabled = false;

        foreach (Collider bodyCollider in blightedBrute.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }

    // Dead is final: no transitions out.
}
