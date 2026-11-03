using UnityEngine;

// After the third swing he stands admiring his sword: the player's window to hit him.
public class RecoveryState_TheFool : EnemyState
{
    private TheFool TheFool;

    public RecoveryState_TheFool(
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

        stateTimer = TheFool.recoveryTime;
        TheFool.agent.isStopped = true;
        TheFool.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(TheFool.chaseState);
    }
}
