using UnityEngine;

public class AttackState_TheFool : EnemyState
{
    private TheFool TheFool;

    private const int SwingCount = 3;
    private const float FirstWindupTime = 24f / 60f;
    private const float FollowupWindupTime = 12f / 60f;
    private const float SwingDuration = 0.25f;
    private const float BetweenSwingsRecovery = 0.5f;
    private const float FinalRecoveryTime = 1f;
    private const float ParriedRecoveryTime = 2f;
    private const float LungeDistance = 2f;

    private float elapsed;
    private int swingIndex;
    private AttackPhase phase;
    private bool hitApplied;
    private float recoveryTime;
    private float lungeDistance;

    private enum AttackPhase
    {
        Windup,
        Swing,
        BetweenSwings,
        FinalRecovery,
    }

    // Set when the wind-up ends. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    // The hitbox stays live for the whole lunge, hitting at most once per swing.
    public bool CanApplyAttackHit => phase == AttackPhase.Swing && hitApplied == false;

    public void MarkAttackHit()
    {
        hitApplied = true;
    }

    // Parried: drop the remaining swings and go straight to a longer final recovery.
    public void Interrupt()
    {
        phase = AttackPhase.FinalRecovery;
        recoveryTime = ParriedRecoveryTime;
        elapsed = 0f;
        TheFool.telegraph = 0f;
    }

    public AttackState_TheFool(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName)
    {
        TheFool = enemyBase as TheFool;
    }

    public override void Enter()
    {
        base.Enter();
        TheFool.agent.isStopped = true;
        TheFool.agent.velocity = Vector3.zero;
        swingIndex = 0;
        phase = AttackPhase.Windup;
        swingStarted = false;
        hitApplied = false;
        recoveryTime = FinalRecoveryTime;
        elapsed = 0f;
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        switch (phase)
        {
            case AttackPhase.Windup:
                TheFool.transform.rotation = TheFool.FaceTarget(TheFool.player.position);
                TheFool.telegraph = Mathf.Clamp01(elapsed / CurrentWindupTime());
                if (elapsed >= CurrentWindupTime())
                {
                    phase = AttackPhase.Swing;
                    swingStarted = true;
                    elapsed = 0f;
                    TheFool.telegraph = 0f;

                    // Lunge only as far as where the player is now, so stepping back still dodges it.
                    Vector3 toPlayer = TheFool.player.position - TheFool.transform.position;
                    toPlayer.y = 0f;
                    lungeDistance = Mathf.Clamp(
                        toPlayer.magnitude - TheFool.lungeStopDistance,
                        0f,
                        LungeDistance
                    );
                }
                break;

            case AttackPhase.Swing:
                // Ease out: a fast burst that slows into the end of the lunge.
                float from = LungeProgress(elapsed - Time.deltaTime);
                float to = LungeProgress(elapsed);
                TheFool.agent.Move(TheFool.transform.forward * (to - from) * lungeDistance);
                TheFool.AttackHit();

                // A parry during AttackHit has already moved us to FinalRecovery.
                if (phase != AttackPhase.Swing)
                    break;

                if (elapsed >= SwingDuration)
                {
                    swingIndex++;
                    phase =
                        swingIndex < SwingCount
                            ? AttackPhase.BetweenSwings
                            : AttackPhase.FinalRecovery;
                    elapsed = 0f;
                }
                break;

            case AttackPhase.BetweenSwings:
                if (elapsed >= BetweenSwingsRecovery)
                {
                    phase = AttackPhase.Windup;
                    hitApplied = false;
                    swingStarted = false;
                    elapsed = 0f;
                    TheFool.anim.Play("Attack", 0, 0f);
                }
                break;

            case AttackPhase.FinalRecovery:
                if (elapsed >= recoveryTime)
                    stateMachine.ChangeState(TheFool.chaseState);
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        TheFool.telegraph = 0f;
    }

    private static float LungeProgress(float time)
    {
        float t = Mathf.Clamp01(time / SwingDuration);
        return 1f - (1f - t) * (1f - t);
    }

    private float CurrentWindupTime() => swingIndex == 0 ? FirstWindupTime : FollowupWindupTime;
}
