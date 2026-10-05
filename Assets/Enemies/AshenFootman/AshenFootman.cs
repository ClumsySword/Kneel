using UnityEngine;

public class AshenFootman : Enemy
{
    public IdleState_AshenFootman idleState { get; private set; }
    public MoveState_AshenFootman moveState { get; private set; }
    public RecoveryState_AshenFootman recoveryState { get; private set; }
    public ChaseState_AshenFootman chaseState { get; private set; }
    public AttackState_AshenFootman attackState { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        idleState = new IdleState_AshenFootman(this, stateMachine, "Idle");
        moveState = new MoveState_AshenFootman(this, stateMachine, "Move");
        recoveryState = new RecoveryState_AshenFootman(this, stateMachine, "Recovery");
        chaseState = new ChaseState_AshenFootman(this, stateMachine, "Chase");
        attackState = new AttackState_AshenFootman(this, stateMachine, "Attack");
    }

    protected override void Start()
    {
        base.Start();

        stateMachine.Initialize(idleState);
    }

    protected override void Update()
    {
        base.Update();

        stateMachine.currentState.Update();
    }
}
