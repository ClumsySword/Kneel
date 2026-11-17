using UnityEngine;

// The tell: a few heavy steps back while he squares up to the player. At its end he picks the spot they stand on.
public class WindupState_IronHusk : EnemyState
{
    private IronHusk ironHusk;

    public WindupState_IronHusk(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ironHusk = enemyBase as IronHusk;
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = ironHusk.windupTime;
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;
    }

    public override void Update()
    {
        base.Update();

        ironHusk.transform.rotation = ironHusk.FaceTarget(ironHusk.player.position);
        float backSpeed = ironHusk.backstepDistance / Mathf.Max(ironHusk.windupTime, 0.01f);
        ironHusk.Move(-ironHusk.transform.forward * (backSpeed * Time.deltaTime), out _);

        if (stateTimer < 0)
        {
            ironHusk.ChargeTarget = ironHusk.player.position;
            stateMachine.ChangeState(ironHusk.chargeState);
        }
    }
}
