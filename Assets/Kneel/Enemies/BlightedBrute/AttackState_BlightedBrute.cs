using UnityEngine;

public class AttackState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    // Seconds from entering the attack until the weapon connects: the AttackHit event sits at 0.30 s
    // of Attack_B, which plays at 0.6x in the animator. Used to ramp the red telegraph.
    private const float WindupTime = 0.5f;

    private float elapsed;

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    public AttackState_BlightedBrute(
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
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.velocity = Vector3.zero;
        swingStarted = false;
        elapsed = 0f;
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        // Track the player during the wind-up; once the swing starts it's committed.
        if (swingStarted == false)
        {
            blightedBrute.transform.rotation = blightedBrute.FaceTarget(blightedBrute.player.position);
            blightedBrute.telegraph = Mathf.Clamp01(elapsed / WindupTime);
        }
        else
        {
            blightedBrute.telegraph = 0f;
        }

        if (triggerCalled)
            stateMachine.ChangeState(blightedBrute.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
        blightedBrute.telegraph = 0f;
    }
}
