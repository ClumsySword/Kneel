using UnityEngine;

// Knocked off balance by a parry or a broken poise: fully open, and hits on it are critical.
public class StaggerState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    public StaggerState_HollowArcher(
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

        stateTimer = HollowArcher.staggerTime;
        HollowArcher.telegraph = 0f;
        HollowArcher.agent.isStopped = true;
        HollowArcher.agent.velocity = Vector3.zero;

        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        HollowArcher.anim.Play("Stagger", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(HollowArcher.chaseState);
    }
}
