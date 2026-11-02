using UnityEngine;

// The cooldown after a blow: he stands getting his breath back, and this is the player's window to hit him.
public class RecoveryState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public RecoveryState_BlightedBrute(
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

        stateTimer = blightedBrute.recoveryTime;
        blightedBrute.agent.isStopped = true;
    }

    public override void Update()
    {
        base.Update();

        blightedBrute.transform.rotation = blightedBrute.FaceTarget(blightedBrute.player.position);

        if (stateTimer < 0)
            stateMachine.ChangeState(blightedBrute.chaseState);
    }

    public override void Exit()
    {
        base.Exit();
    }
}
