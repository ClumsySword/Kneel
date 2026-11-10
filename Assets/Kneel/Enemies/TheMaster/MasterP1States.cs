using UnityEngine;

// The Master's Phase 1 states other than the six moves (MasterMoveState). One class per behaviour, each setting the
// animator bool of its name, as for every Kneel enemy.

// On the throne before the intro: untouchable, still.
public class MasterSeatedState : EnemyState
{
    private readonly MasterP1 master;

    public MasterSeatedState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        master.Untouchable = true;
        master.Stop();
        master.SetLeftArmRest(false);
        master.anim.CrossFadeInFixedTime("Seated", 0f, 0, 0f);
    }
}

// Stands up off the throne (intro step 4), then walks down the dais.
public class MasterRiseState : EnemyState
{
    private readonly MasterP1 master;

    public MasterRiseState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        master.Untouchable = true;
        master.SetLeftArmRest(false);
        stateTimer = master.settings.introStandTime;
        master.anim.SetFloat("RiseSpeed", RiseClipLength / Mathf.Max(master.settings.introStandTime, 0.5f));
        master.anim.CrossFadeInFixedTime("Rise", 0.2f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        if (stateTimer <= 0f)
            stateMachine.ChangeState(master.walkDownState);
    }

    // Set by MasterSetup: the Stand Up clip's length.
    public static float RiseClipLength = 4.83f;
}

// Walks down the dais to its foot and waits there, untouchable, until MasterBoss engages him.
public class MasterWalkDownState : EnemyState
{
    private readonly MasterP1 master;

    public Vector3 Target;

    public bool Arrived { get; private set; }

    public MasterWalkDownState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        Arrived = false;
        master.Untouchable = true;
        master.SetLeftArmRest(true);
        master.anim.CrossFadeInFixedTime("Move", 0.25f, 0, 0f);
        if (master.agent.enabled == false)
        {
            master.agent.enabled = true;
            master.agent.Warp(master.transform.position);
        }
        // A measured descent: the walk fills the intro's walk beat (§3, 7.5-11 s), never faster than his walk.
        Vector3 to = Target - master.transform.position;
        to.y = 0f;
        master.Go(Target, Mathf.Min(master.settings.walkSpeed, to.magnitude / Mathf.Max(master.settings.introWalkTime, 0.5f)));
    }

    public override void Update()
    {
        base.Update();
        master.FeedLocomotion();
        Vector3 to = Target - master.transform.position;
        to.y = 0f;
        if (Arrived == false && to.magnitude < 0.4f)
        {
            Arrived = true;
            master.Stop();
        }

        if (Arrived)
            master.transform.rotation = master.FaceTarget(master.player.position);
        else if (master.agent.enabled && master.agent.isOnNavMesh && master.agent.hasPath)
            master.transform.rotation = master.FaceTarget(master.agent.steeringTarget);
    }
}

// Walks (or strides, from beyond the far band) into range for the move he has picked (§4: he never attacks out of a
// stride). He starts it once in range, on screen and facing the player.
public class MasterApproachState : EnemyState
{
    private readonly MasterP1 master;
    private float nextPath;
    private float started;

    // The move waits until this time once in range (the Command's follow-up), and Hurry strides the whole way.
    public float NotBefore;
    public bool Hurry;

    public MasterApproachState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        nextPath = 0f;
        started = Time.time;
        master.SetLeftArmRest(true);
        master.anim.CrossFadeInFixedTime("Move", 0.2f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        master.FeedLocomotion();

        float distance = master.PlayerDistance();
        float range = master.StartRange(master.Pending);
        bool visible = master.OnScreen();

        // In range: he stops and starts it. In range but off the camera's frame, he keeps walking in until he's seen
        // (he never attacks from off screen, and standing there would stall the fight).
        if (distance <= range && (visible || distance < 4f))
        {
            master.Stop();
            master.transform.rotation = master.FaceTarget(master.player.position);
            master.attackFacingAngle = 25f;
            if (Time.time >= NotBefore && master.IsFacing(master.player.position) && visible)
                master.StartMove(master.Pending);
            return;
        }

        // Taking too long (the player keeps away): pick again from where things stand.
        if (Time.time - started > 10f)
        {
            master.BeginNextMove();
            return;
        }

        if (Time.time >= nextPath)
        {
            nextPath = Time.time + 0.25f;
            float speed = Hurry || distance > master.settings.bandFar ? master.settings.strideSpeed : master.settings.walkSpeed;
            master.Go(master.player.position, speed);
        }

        if (master.agent.enabled && master.agent.isOnNavMesh && master.agent.hasPath)
            master.transform.rotation = master.FaceTarget(master.agent.steeringTarget);
    }
}

// The pause between strings: he circles the player at a slow walk, facing them (§4: 1-2 s).
public class MasterCircleState : EnemyState
{
    private readonly MasterP1 master;
    private float until;
    private float side = 1f;

    public MasterCircleState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public void Hold(float seconds) => until = Time.time + seconds;

    public override void Enter()
    {
        base.Enter();
        side = Random.value < 0.5f ? -1f : 1f;
        master.SetLeftArmRest(true);
        master.anim.CrossFadeInFixedTime("Circle", 0.25f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        master.FeedLocomotion();
        master.transform.rotation = master.FaceTarget(master.player.position);

        if (master.Engaged && master.Untouchable == false)
        {
            Vector3 to = master.player.position - master.transform.position;
            to.y = 0f;
            Vector3 strafe = Vector3.Cross(Vector3.up, to.normalized) * side;
            if (master.agent.enabled && master.agent.isOnNavMesh)
                master.agent.Move(strafe * 0.8f * Time.deltaTime);
        }

        if (master.Engaged && master.Untouchable == false && Time.time >= until)
            master.BeginNextMove();
    }
}

// Parried: driven to one knee for the riposte window (every hit critical, §4), then up.
public class MasterParriedState : EnemyState
{
    private readonly MasterP1 master;
    private float duration;
    private int phase;

    public MasterParriedState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public void Begin(float seconds)
    {
        duration = seconds;
        if (stateMachine.currentState == this)
            Enter();
        else
            stateMachine.ChangeState(this);
    }

    public override void Enter()
    {
        base.Enter();
        phase = 0;
        elapsedTime = 0f;
        master.Stop();
        master.SetLeftArmRest(false);
        master.bossTell?.StopTells();
        master.bossTell?.Kneel();
        master.anim.CrossFadeInFixedTime("KneelDown", 0.06f, 0, 0f);
    }

    public override void Update()
    {
        elapsedTime += Time.deltaTime;
        if (phase == 0 && elapsedTime >= 0.5f)
        {
            phase = 1;
            master.anim.CrossFadeInFixedTime("KneelIdle", 0.12f, 0, 0f);
        }

        if (phase == 1 && elapsedTime >= duration - 0.6f)
        {
            phase = 2;
            master.anim.CrossFadeInFixedTime("KneelUp", 0.1f, 0, 0f);
        }

        if (phase == 2 && elapsedTime >= duration)
            master.MoveFinished();
    }

    public override void Exit()
    {
        base.Exit();
        elapsedTime = 0f;
    }

    private float elapsedTime;
}

// Off balance (a poise break, a broken guard, a hazard): hits on him are critical.
public class MasterStaggerState : EnemyState
{
    private readonly MasterP1 master;
    private float duration;

    public MasterStaggerState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public void Begin(float seconds)
    {
        duration = seconds;
        if (stateMachine.currentState == this)
            Enter();
        else
            stateMachine.ChangeState(this);
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = duration;
        master.Stop();
        master.SetLeftArmRest(true);
        master.bossTell?.StopTells();
        master.poise.Reset();
        master.anim.CrossFadeInFixedTime("Stagger", 0.06f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        if (stateTimer <= 0f)
            master.MoveFinished();
    }
}

// A short flinch for a hit outside any move (at most every flinchCooldown seconds).
public class MasterFlinchState : EnemyState
{
    private readonly MasterP1 master;

    public MasterFlinchState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        stateTimer = 0.55f;
        master.Stop();
        master.SetLeftArmRest(true);
        master.anim.CrossFadeInFixedTime("Flinch", 0.05f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        if (stateTimer <= 0f)
            master.MoveFinished();
    }
}

// At half health (§4): the pommel into the floor. Untouchable, no attacks, ~3 s; the floor panel F1 goes (MasterBoss
// passes it to the hall, Stage 5). From here on he throws Threads more often.
public class MasterPommelState : EnemyState
{
    private readonly MasterP1 master;
    private bool struck;

    public MasterPommelState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public event System.Action OnPommelStrike;

    public override void Enter()
    {
        base.Enter();
        struck = false;
        stateTimer = master.settings.pommelTime;
        master.Untouchable = true;
        master.Stop();
        master.SetLeftArmRest(false);
        master.bossTell?.StopTells();
        master.anim.CrossFadeInFixedTime("Pommel", 0.15f, 0, 0f);
    }

    public override void Update()
    {
        base.Update();
        if (struck == false && master.settings.pommelTime - stateTimer >= master.pommelContact)
        {
            struck = true;
            master.bossTell?.Impact(master.transform.position + master.transform.forward * 1.2f);
            OnPommelStrike?.Invoke();
        }

        if (stateTimer <= 0f)
        {
            master.Untouchable = false;
            master.MoveFinished();
        }
    }
}

// Phase 1 is over: he drops to one knee (§5, 0.0 s). MasterBoss runs the transition from here.
public class MasterFallenState : EnemyState
{
    private readonly MasterP1 master;

    public MasterFallenState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName)
        : base(enemyBase, stateMachine, animBoolName) { master = enemyBase as MasterP1; }

    public override void Enter()
    {
        base.Enter();
        master.Untouchable = true;
        master.Stop();
        master.SetLeftArmRest(false);
        master.bossTell?.StopTells();
        master.anim.CrossFadeInFixedTime("Fallen", 0.1f, 0, 0f);
    }
}
