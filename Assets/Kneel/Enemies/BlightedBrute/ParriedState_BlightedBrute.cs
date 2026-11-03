using UnityEngine;

// His swing thrown off by a parry. He recoils and comes straight back: this isn't a stagger, so nothing here is critical.
public class ParriedState_BlightedBrute : EnemyState
{
    private BlightedBrute blightedBrute;

    public ParriedState_BlightedBrute(
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

        stateTimer = blightedBrute.parriedTime;
        blightedBrute.agent.isStopped = true;
        blightedBrute.agent.velocity = Vector3.zero;

        blightedBrute.anim.CrossFadeInFixedTime("Parried", 0.08f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            stateMachine.ChangeState(blightedBrute.chaseState);
    }
}
