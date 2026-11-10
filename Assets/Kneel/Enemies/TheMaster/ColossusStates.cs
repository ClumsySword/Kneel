using UnityEngine;

// The Ash Colossus's states other than his eight moves (ColossusMoveState). One class per behaviour, each setting the
// animator bool of its name, as for every Kneel enemy.

// Out of the Unmaking's vortex (§5, 5.5-7.5 s): crouched, he rises flexing (Mutant Flexing Muscles). Untouchable; the
// mask splits and he roars once MasterBoss says so (Roar), then waits for Engage.
public class ColossusRiseState : EnemyState
{
    private readonly AshColossus colossus;

    public float Duration = 2f;

    public ColossusRiseState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public override void Enter()
    {
        base.Enter();
        colossus.Untouchable = true;
        colossus.Stop();
        colossus.anim.CrossFadeInFixedTime("Rise", 0f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        if (colossus.player != null)
            colossus.transform.rotation = Quaternion.RotateTowards(colossus.transform.rotation, colossus.FaceTarget(colossus.player.position), 30f * Time.deltaTime);
    }

    // The mask splits (§5, 7.5 s): the roar, played over whatever is left of the rise.
    public void Roar() => colossus.anim.CrossFadeInFixedTime("RoarSplit", 0.3f, 0, 0f);
}

// Walks into reach for the move he has picked: 1.8 m/s, never a stride (he Leaps instead, §6). He starts it once in
// range, on screen and facing the player.
public class ColossusApproachState : EnemyState
{
    private readonly AshColossus colossus;
    private float nextPath;
    private float started;

    public float NotBefore;

    public ColossusApproachState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public override void Enter()
    {
        base.Enter();
        nextPath = 0f;
        started = Time.time;
        colossus.anim.CrossFadeInFixedTime("Move", 0.25f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        colossus.FeedLocomotion();

        float distance = colossus.PlayerDistance();
        float range = colossus.StartRange(colossus.Pending);
        bool visible = colossus.OnScreen(3f);
        if (distance <= range && (visible || distance < 6f))
        {
            colossus.Stop();
            colossus.transform.rotation = colossus.FaceTarget(colossus.player.position);
            colossus.attackFacingAngle = 25f;
            if (Time.time >= NotBefore && colossus.IsFacing(colossus.player.position) && visible)
                colossus.StartMove(colossus.Pending);
            return;
        }

        if (Time.time - started > 8f)
        {
            colossus.BeginNextMove();
            return;
        }

        if (Time.time >= nextPath)
        {
            nextPath = Time.time + 0.25f;
            colossus.Go(colossus.player.position, colossus.settings.p2WalkSpeed);
        }

        if (colossus.agent.enabled && colossus.agent.isOnNavMesh && colossus.agent.hasPath)
            colossus.transform.rotation = colossus.FaceTarget(colossus.agent.steeringTarget);
    }
}

// The pause between moves (§6: 0.8-1.5 s): he stands his ground, breathing, and turns to face the player (no sideways
// glide: owner, 2026-11-10).
public class ColossusCircleState : EnemyState
{
    private readonly AshColossus colossus;
    private float until;

    // Degrees a second: slow, heavy.
    public const float TurnRate = 70f;

    public ColossusCircleState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public void Hold(float seconds) => until = Time.time + seconds;

    public override void Enter()
    {
        base.Enter();
        colossus.Stop();
        colossus.anim.CrossFadeInFixedTime("Circle", 0.3f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        colossus.FeedLocomotion();
        if (colossus.player == null)
            return;
        Vector3 to = colossus.player.position - colossus.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            colossus.transform.rotation = Quaternion.RotateTowards(colossus.transform.rotation, Quaternion.LookRotation(to), TurnRate * Time.deltaTime);

        if (colossus.Engaged && colossus.Untouchable == false && Time.time >= until)
            colossus.BeginNextMove();
    }
}

// A parry on the Cleave: he recoils (Large Hit Reaction slowed to 1.2 s). Not a stagger: nothing is critical.
public class ColossusRecoilState : EnemyState
{
    private readonly AshColossus colossus;
    private float duration;

    public ColossusRecoilState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public void Begin(float seconds)
    {
        duration = seconds;
        if (stateMachine.currentState == this) Enter(); else stateMachine.ChangeState(this);
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = duration;
        colossus.Stop();
        colossus.bossTell?.StopTells();
        colossus.bossTell?.Deflect();
        colossus.SetMoveSpeed(colossus.recoilLength / Mathf.Max(duration, 0.2f));
        colossus.anim.CrossFadeInFixedTime("Recoil", 0.06f, 0, 0f);
    }

    public override void Exit()
    {
        base.Exit();
        colossus.SetMoveSpeed(1f);
    }

    public override void Update()
    {
        base.Update();
        if (stateTimer <= 0f)
            colossus.MoveFinished();
    }
}

// Poise broken, three parries running, a broken Gather or a hazard: he falls forward onto his hands, the mask at the
// floor, for 3.5 s (§6). Hits are critical and ignore the armour. Down, held, then the same fall played back as the
// get-up.
public class ColossusToppleState : EnemyState
{
    private readonly AshColossus colossus;
    private float duration;
    private int phase;
    private float t;

    private const float DownTime = 0.8f;
    private const float UpTime = 0.9f;

    public ColossusToppleState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public void Begin(float seconds)
    {
        duration = seconds;
        if (stateMachine.currentState == this) Enter(); else stateMachine.ChangeState(this);
    }

    public override void Enter()
    {
        base.Enter();
        phase = 0;
        t = 0f;
        colossus.Stop();
        colossus.bossTell?.StopTells();
        colossus.bossTell?.Kneel();
        colossus.poise.Reset();
        colossus.SetMoveSpeed(colossus.toppleDown / DownTime);
        colossus.anim.CrossFadeInFixedTime("ToppleDown", 0.1f, 0, 0f);
    }

    public override void Exit()
    {
        base.Exit();
        colossus.SetMoveSpeed(1f);
    }

    public override void Update()
    {
        t += Time.deltaTime;
        if (phase == 0 && t >= DownTime)
        {
            phase = 1;
            Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.3f, 0.4f);
            ColossusFx.Burst(colossus, colossus.transform.position + colossus.transform.forward * 2.5f, 2.5f, 40);
            colossus.anim.CrossFadeInFixedTime("ToppleHold", 0.1f, 0, 0f);
        }

        if (phase == 1 && t >= duration - UpTime)
        {
            phase = 2;
            // The fall backwards: the get-up.
            colossus.SetMoveSpeed(colossus.toppleDown / UpTime);
            colossus.anim.CrossFadeInFixedTime("ToppleUp", 0.1f, 0, colossus.toppleDown);
        }

        if (phase == 2 && t >= duration)
            colossus.MoveFinished();
    }

    public float Elapsed => t;
}

// At half health (§6): he roars; the statues fall and the ash storm begins (the hall's director). Untouchable, ~3 s.
public class ColossusRoarState : EnemyState
{
    private readonly AshColossus colossus;

    public ColossusRoarState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public override void Enter()
    {
        base.Enter();
        stateTimer = colossus.settings.roarTime;
        colossus.Untouchable = true;
        colossus.Stop();
        colossus.bossTell?.StopTells();
        if (colossus.bossTell != null) colossus.bossTell.EmberScale = 1.8f;
        colossus.anim.CrossFadeInFixedTime("Roar", 0.2f, 0, 0f);
        Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.35f, 1.2f);
    }

    public override void Update()
    {
        base.Update();
        if (stateTimer > 0f)
            return;
        colossus.Untouchable = false;
        if (colossus.bossTell != null) colossus.bossTell.EmberScale = 1f;
        colossus.MoveFinished();
    }
}

// Phase 2 is over: he drops to both knees (§7). MasterBoss runs the finisher from here (Stage 7).
public class ColossusFallenState : EnemyState
{
    private readonly AshColossus colossus;

    public ColossusFallenState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { colossus = enemyBase as AshColossus; }

    public override void Enter()
    {
        base.Enter();
        colossus.Untouchable = true;
        colossus.Stop();
        colossus.bossTell?.StopTells();
        colossus.SetMoveSpeed(colossus.toppleDown / 1.2f);
        colossus.anim.CrossFadeInFixedTime("Fallen", 0.15f, 0, 0f);
    }
}
