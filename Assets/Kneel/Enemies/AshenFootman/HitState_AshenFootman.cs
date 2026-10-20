using UnityEngine;

public class HitState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;
    private float duration;

    // Set by Setup and applied in Enter, because re-entering this state runs Exit (which resets) first.
    private float pendingDuration;
    private bool pendingStagger;

    // True when the hit was a parry: the footman is open and hits on it are critical.
    public bool isStagger { get; private set; }

    public HitState_AshenFootman(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ashenFootman = enemyBase as AshenFootman;
    }

    public void Setup(float stunDuration, bool stagger)
    {
        pendingDuration = stunDuration;
        pendingStagger = stagger;
    }

    public override void Enter()
    {
        base.Enter();

        duration = pendingDuration;
        isStagger = pendingStagger;
        stateTimer = duration;
        ashenFootman.telegraph = 0f;
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;

        // Restart the reaction even when already reacting (the bool alone wouldn't replay it).
        ashenFootman.anim.Play("Hit", 0, 0f);
        ashenFootman.anim.speed = isStagger ? 0.6f : 1f;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(ashenFootman.chaseState);
    }

    public override void Exit()
    {
        base.Exit();
        ashenFootman.anim.speed = 1f;
        isStagger = false;
    }
}
