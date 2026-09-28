using UnityEngine;

public class DeadState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public DeadState_AshenFootman(
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

        ashenFootman.telegraph = 0f;
        ashenFootman.anim.CrossFadeInFixedTime("Dead", 0.1f, 0, 0f);

        // The body stays where it fell: no more pathing, and nothing can hit or bump into it.
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.enabled = false;

        foreach (Collider bodyCollider in ashenFootman.GetComponents<Collider>())
            bodyCollider.enabled = false;
    }

    // Dead is final: no transitions out.
}
