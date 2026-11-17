using UnityEngine;

// Reeling from a heavy (armour off), from the armour breaking, or dazed from a charge into a wall: fully open, and
// hits on him are critical. Whoever sends him here sets how long it lasts.
public class StaggerState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public StaggerState_IronHusk(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ironHusk = enemyBase as IronHusk;
    }

    public float Duration { get; set; } = 1.5f;

    public override void Enter()
    {
        base.Enter();

        stateTimer = Duration;
        ironHusk.telegraph = 0f;
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;

        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        ironHusk.anim.CrossFadeInFixedTime("Stagger", 0.08f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ironHusk.chaseState);
    }
}
