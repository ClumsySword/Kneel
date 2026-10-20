using UnityEngine;

// Knocked off balance by a parry or a broken poise: fully open, and hits on it are critical.
public class StaggerState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public StaggerState_AshenFootman(
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

        stateTimer = ashenFootman.staggerTime;
        ashenFootman.telegraph = 0f;
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;

        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        ashenFootman.anim.Play("Stagger", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ashenFootman.chaseState);
    }
}
