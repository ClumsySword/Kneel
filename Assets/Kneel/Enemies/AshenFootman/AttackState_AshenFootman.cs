using UnityEngine;

// One swing, from wind-up to the end of its clip. Both of his attacks are this state with their own animator bool.
public class AttackState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    // Seconds from entering the attack until the weapon connects (the AttackHit event at the state's speed in the
    // animator). Used to ramp the shoulder tell.
    private readonly float windupTime;

    private float elapsed;

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    public AttackState_AshenFootman(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName,
        float windupTime
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ashenFootman = enemyBase as AshenFootman;
        this.windupTime = windupTime;
    }

    public override void Enter()
    {
        base.Enter();
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;
        swingStarted = false;
        elapsed = 0f;

        // Straight into the clip. A transition still blending out of Recovery can't be interrupted, so the bool alone
        // starts the swing late and the hit no longer lands where the wind-up says.
        ashenFootman.anim.CrossFadeInFixedTime(animBoolName, 0.12f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        // Track the player during the wind-up; once the swing starts it's committed.
        if (swingStarted == false)
        {
            ashenFootman.transform.rotation = ashenFootman.FaceTarget(ashenFootman.player.position);
            ashenFootman.windup = Mathf.Clamp01(elapsed / windupTime);
        }
        else
        {
            ashenFootman.windup = 0f;
        }

        if (triggerCalled)
            stateMachine.ChangeState(ashenFootman.recoveryState);
    }

    public override void Exit()
    {
        base.Exit();
        ashenFootman.windup = 0f;

        // The shield only comes back up after the punish window.
        ashenFootman.LowerGuard(ashenFootman.guardDownAfterAttack);
    }
}
