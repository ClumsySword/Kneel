using UnityEngine;

// Knocked off balance by a parry or a broken poise: fully open, and hits on it are critical.
public class StaggerState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public StaggerState_BlightedBrute(
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

        stateTimer = blightedBrute.staggerTime;
        blightedBrute.telegraph = 0f;
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.velocity = Vector3.zero;

        // Restart the clip even when already staggered (the bool alone wouldn't replay it).
        blightedBrute.anim.Play("Stagger", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(blightedBrute.chaseState);
    }
}
