using UnityEngine;

public class AttackState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    // Seconds from entering the attack until the weapon connects: the AttackHit event sits at 0.30 s
    // of Attack_B, which plays at 0.6x in the animator. Used to ramp the red telegraph.
    private const float WindupTime = 0.5f;

    private float elapsed;

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    public AttackState_AshenFootman(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ashenFootman = enemyBase as AshenFootman;
    }

    public override void Enter()
    {
        base.Enter();
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;
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
            ashenFootman.transform.rotation = ashenFootman.FaceTarget(ashenFootman.player.position);
            ashenFootman.telegraph = Mathf.Clamp01(elapsed / WindupTime);
        }
        else
        {
            ashenFootman.telegraph = 0f;
        }

        if (triggerCalled)
            stateMachine.ChangeState(ashenFootman.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
        ashenFootman.telegraph = 0f;
    }
}
