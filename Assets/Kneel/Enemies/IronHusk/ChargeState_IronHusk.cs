using UnityEngine;

// The shoulder charge: a straight run at where the player stood as the wind-up ended, and on past it. He doesn't
// steer once it starts, so stepping or rolling out of the line beats it. It ends in his recovery, or early: dazed
// against a wall, or teetering at the breach's lip.
public class ChargeState_IronHusk : EnemyState
{
    private IronHusk ironHusk;
    private Vector3 direction;
    private float distance;
    private float travelled;
    private float speed;
    private bool landed;

    public ChargeState_IronHusk(
        Enemy enemyBase,
        EnemyStateMachine stateMachine,
        string animBoolName
    )
        : base(enemyBase, stateMachine, animBoolName)
    {
        ironHusk = enemyBase as IronHusk;
    }

    // Whether the shoulder has already landed on the player this charge (it only does once).
    public bool Landed => landed;

    public override void Enter()
    {
        base.Enter();
        ironHusk.agent.isStopped = true;
        ironHusk.agent.velocity = Vector3.zero;

        direction = ironHusk.ChargeTarget - ironHusk.transform.position;
        direction.y = 0f;
        float toTarget = direction.magnitude;
        direction = toTarget > 0.01f ? direction / toTarget : ironHusk.transform.forward;

        distance = Mathf.Clamp(toTarget + ironHusk.chargeOvershoot, ironHusk.chargeMinDistance, ironHusk.chargeMaxDistance);
        travelled = 0f;
        speed = 0f;
        landed = false;
        ironHusk.transform.rotation = Quaternion.LookRotation(direction);
    }

    public override void Update()
    {
        base.Update();

        float remaining = distance - travelled;
        float stopDistance = Mathf.Max(ironHusk.chargeStopDistance, 0.01f);
        float topSpeed = ironHusk.chargeSpeed * Mathf.Clamp(remaining / stopDistance, 0.15f, 1f);
        speed = speed < topSpeed ? Mathf.MoveTowards(speed, topSpeed, ironHusk.chargeAcceleration * Time.deltaTime) : topSpeed;

        float step = Mathf.Min(speed * Time.deltaTime, remaining);
        float moved = ironHusk.Move(direction * step, out bool atLip);
        travelled += step;

        if (landed == false && ironHusk.ChargeHit())
            landed = true;

        ironHusk.StrikeHazards();

        if (atLip)
        {
            stateMachine.ChangeState(ironHusk.teeterState);
            return;
        }

        // Most of the step didn't get through: he's run into a wall.
        if (step > 0.02f && moved < step * 0.5f)
        {
            ironHusk.Stagger(ironHusk.crashStunTime);
            return;
        }

        if (travelled >= distance - 0.001f)
            stateMachine.ChangeState(ironHusk.recoveryState);
    }
}
