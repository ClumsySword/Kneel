using UnityEngine;

// A hit landed on the raised shield: a short recoil, guard held.
public class BlockState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public BlockState_AshenFootman(
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

        stateTimer = ashenFootman.blockStunTime;
        ashenFootman.agent.isStopped = true;
        ashenFootman.agent.velocity = Vector3.zero;

        // Restart the recoil on a second blocked hit (the bool alone wouldn't replay it).
        ashenFootman.anim.Play("Block", 0, 0f);
    }

    public override void Update()
    {
        base.Update();

        ashenFootman.transform.rotation = ashenFootman.FaceTarget(ashenFootman.player.position);

        if (stateTimer < 0)
            stateMachine.ChangeState(ashenFootman.chaseState);
    }
}
