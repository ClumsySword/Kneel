using UnityEngine;

// One attack, from wind-up to the end of its clip. All of his attacks are this state with their own animator bool.
public class AttackState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    // Seconds from entering the attack until each blow connects (the AttackHit events at the state's speed in the
    // animator). Most attacks are one blow; the combo is several.
    private readonly float[] hitTimes;

    // How long he stands open once the clip ends.
    private readonly float recoveryTime;

    private float elapsed;

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    // How many blows of this attack have come so far.
    private int blowsDealt;

    public AttackState_BlightedBrute(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName,
        float recoveryTime,
        params float[] hitTimes
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        blightedBrute = enemyBase as BlightedBrute;
        this.recoveryTime = recoveryTime;
        this.hitTimes = hitTimes;
    }

    // While this runs he walks forward through the attack, at this speed, between these seconds into it.
    public float advanceSpeed { get; set; }
    public float advanceStart { get; set; }
    public float advanceEnd { get; set; }

    public void NextBlow() => blowsDealt++;

    public override void Enter()
    {
        base.Enter();
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.velocity = Vector3.zero;
        swingStarted = false;
        blowsDealt = 0;
        elapsed = 0f;

        // Straight into the clip. A transition still blending out of Recovery can't be interrupted, so the bool alone
        // starts the swing late and the hit no longer lands where the wind-up says.
        blightedBrute.anim.CrossFadeInFixedTime(animBoolName, 0.12f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        // Track the player up to the last moment he could still change his mind about the next blow.
        if (blowsDealt < hitTimes.Length && elapsed < hitTimes[blowsDealt] - blightedBrute.commitTime)
            blightedBrute.transform.rotation = blightedBrute.FaceTarget(blightedBrute.player.position);

        // A blow's shove puts the player out of reach, so a long attack has to come after them to land the rest.
        if (advanceSpeed > 0f && elapsed >= advanceStart && elapsed <= advanceEnd && blightedBrute.HasRoomToAdvance())
            blightedBrute.agent.Move(blightedBrute.transform.forward * advanceSpeed * Time.deltaTime);

        if (triggerCalled)
        {
            blightedBrute.recoveryTime = recoveryTime;
            stateMachine.ChangeState(blightedBrute.recoveryState);
        }
    }
}
