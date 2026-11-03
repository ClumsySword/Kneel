using UnityEngine;

// One shot, from the reach for the quiver to the end of the release. The animator takes it through Attack - Idle (reach
// and nock), Attack - Draw (the 20-frame final tell) and Attack - Shoot (release); the clips' events drive the rest.
public class AttackState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    // Seconds since the shot began.
    public float elapsed { get; private set; }

    // Set by the ArrowRelease animation event. Until then he tracks the player and the shot can still be broken.
    public bool released { get; set; }

    public AttackState_HollowArcher(
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
        HollowArcher.agent.isStopped = true;
        HollowArcher.agent.velocity = Vector3.zero;
        released = false;
        elapsed = 0f;
        HollowArcher.StartShot();

        // Straight into the clip. A transition still blending can't be interrupted, so the bool alone starts the shot late
        // and the events no longer land where the pose says.
        HollowArcher.anim.CrossFadeInFixedTime("Attack - Idle", 0.15f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        if (released == false)
            HollowArcher.transform.rotation = HollowArcher.FaceTarget(HollowArcher.player.position);

        if (triggerCalled)
            stateMachine.ChangeState(HollowArcher.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
        HollowArcher.EndShot();
    }
}
