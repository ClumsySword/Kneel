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
    private const float LungeDistance = 2f;

    private float elapsed;
    private int swingIndex;
    private AttackPhase phase;
    private bool hitApplied;

    private enum AttackPhase
    {
        Windup,
        Swing,
        BetweenSwings,
        FinalRecovery,
    }

    // Set by the AttackHit animation event. After this the swing is committed and can't be interrupted.
    public bool swingStarted { get; set; }

    public bool CanApplyAttackHit => phase == AttackPhase.Windup && hitApplied == false;

    public void MarkAttackHit()
    {
        hitApplied = true;
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
                    TheFool.AttackHit();
                    phase = AttackPhase.Swing;
                    elapsed = 0f;
                    TheFool.telegraph = 0f;
                }
                break;

            case AttackPhase.Swing:
                TheFool.agent.Move(
                    TheFool.transform.forward * (LungeDistance / SwingDuration) * Time.deltaTime
                );
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
                if (elapsed >= FinalRecoveryTime)
                    stateMachine.ChangeState(TheFool.chaseState);
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        TheFool.telegraph = 0f;
    }

    private float CurrentWindupTime() => swingIndex == 0 ? FirstWindupTime : FollowupWindupTime;
}
