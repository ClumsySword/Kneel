using UnityEngine;

// Knocked off balance by a parry or a broken poise: fully open, and hits on it are critical.
public class StaggerState_TheFool : EnemyState
{
    private TheFool TheFool;

    public StaggerState_TheFool(
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

        stateTimer = TheFool.staggerTime;
        TheFool.telegraph = 0f;
        TheFool.agent.isStopped = true;
        TheFool.agent.velocity = Vector3.zero;

        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        TheFool.anim.Play("Stagger", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(TheFool.chaseState);
    }
}
