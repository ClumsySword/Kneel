using UnityEngine;

public class AttackState_HollowArcher : EnemyState
{
    private HollowArcher HollowArcher;

    // Seconds from entering the attack until the weapon connects: the AttackHit event sits at 0.30 s
    // of Attack_B, which plays at 0.6x in the animator. Used to ramp the red telegraph.
    private const float WindupTime = 0.5f;

    private float elapsed;
    private bool hasFired;

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

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
        swingStarted = false;
        elapsed = 0f;
        hasFired = false;
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        // Track the player during the wind-up; once the swing starts it's committed.
        if (swingStarted == false)
        {
            HollowArcher.transform.rotation = HollowArcher.FaceTarget(HollowArcher.player.position);
            HollowArcher.telegraph = Mathf.Clamp01(elapsed / WindupTime);
            if (!hasFired && elapsed >= WindupTime)
            {
                HollowArcher.FireArrow();
                hasFired = true;
            }
        }
        else
        {
            HollowArcher.telegraph = 0f;
        }

        if (triggerCalled)
            stateMachine.ChangeState(HollowArcher.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
        HollowArcher.telegraph = 0f;
    }
}
