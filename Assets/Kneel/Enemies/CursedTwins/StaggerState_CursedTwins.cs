using UnityEngine;

// Off balance from a broken poise or a parry: fully open, and hits on him are critical. Whoever sends him here
// sets how long it lasts.
public class StaggerState_CursedTwins : EnemyState
{
    private CursedTwins twin;

    public StaggerState_CursedTwins(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        twin = enemyBase as CursedTwins;
    }

    public float Duration { get; set; } = 1.5f;

    public override void Enter()
    {
        base.Enter();
        stateTimer = Duration;
        twin.agent.isStopped = true;
        twin.agent.velocity = Vector3.zero;
        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        twin.anim.CrossFadeInFixedTime("Stagger", 0.08f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(twin.chaseState);
    }
}
