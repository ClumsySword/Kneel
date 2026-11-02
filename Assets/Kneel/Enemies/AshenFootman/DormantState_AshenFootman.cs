using UnityEngine;

// Held out of the world on a gibbet: the gibbet shows the thrall until it tears free. Leaving this state lands it at
// the gibbet's spawn point.
public class DormantState_AshenFootman : EnemyState
{
    private AshenFootman ashenFootman;

    public DormantState_AshenFootman(
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
        ashenFootman.SetInWorld(false);
    }

    public override void Exit()
    {
        base.Exit();

        // The agent has to come back on at the new spot, not the old one.
        ashenFootman.transform.position = ashenFootman.AmbushSpawnPoint;
        ashenFootman.SetInWorld(true);
    }
}
