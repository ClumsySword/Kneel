using UnityEngine;

// One of the Ash Colossus's eight moves (The Master.md §6). Each has its own animator bool and plays its clip at the
// speed that lands its contact at the end of its tell, so tells are tuned live in MasterSettings. Until a blow lands
// he is armoured (the cuts multiply with the ash armour); he tracks the player until he commits.
// - Cleave (glint): a 5 m line along the blade's fall; parry it, or roll aside. The blade sticks in the floor.
// - Reaping Sweep (ember claw): 180 degrees, 6 m, a knockdown; roll through it.
// - Grasp (ember claw): a lunge and a grab; held in the fist, then flung into a knockdown. Roll.
// - Hammerfall (no glint): a 4 m circle where the fists land; lava cracks race out from it to 8 m, brighten and burst,
//   hurting a player standing on one (owner, 2026-11-10: it was a burning circle and a ring). The panel under it gives
//   way 3 s later.
// - Leap: a shadow follows the player and locks; he lands in it.
// - Great Command (mask light): the whole hall, cover only; a kneel, then an escapable Hammerfall.
// - Gather: a 4 s channel; breaks into a Topple under 60 raw damage or two bashes; completed, he heals and the hall
//   goes dark.
// - Thread Lash: five burning lines fanned at the player.
public class ColossusMoveState : EnemyState
{
    private readonly AshColossus colossus;
    private readonly AshColossus.MoveKind kind;

    private float elapsed;
    private float tell;
    private float contactAt;
    private bool landed;
    private float punishUntil;
    private bool finished;

    // Grasp: the locked line and the lunge along it.
    private Vector3 line;
    private float lunged;

    // Hammerfall and Leap: where it lands (locked late), and the ring after the Hammerfall.
    private Vector3 target;
    private GameObject shadow;

    // Leap: where he took off from.
    private Vector3 takeoffFrom;
    private bool airborne;

    // Great Command: the Hammerfall that follows lands after the player rises.
    private float followupAt = -1f;
    private float followupHit;
    private bool followupFar;

    // Gather: what the player has done to the claw.
    private float gatherDamage;
    private int gatherBashes;

    public ColossusMoveState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName, AshColossus.MoveKind kind)
        : base(enemyBase, stateMachine, animBoolName)
    {
        colossus = enemyBase as AshColossus;
        this.kind = kind;
    }

    public AshColossus.MoveKind Kind => kind;

    // Before the blow lands (the whole Gather): hits do windupDamageMultiplier and never stop him.
    public bool Armoured => landed == false;

    public float Tell => tell;

    public float SinceStart => elapsed;

    public bool Landed => landed;

    public Vector3 Target => target;

    // When the current blow lands (game time), or -1.
    public float LandsAt => landed || finished || followupAt >= 0f || kind == AshColossus.MoveKind.Gather ? -1f : Time.time + (contactAt - elapsed);

    // The Gather's damage so far, for the playtest.
    public float GatherDamage => gatherDamage;

    private MasterSettings S => colossus.settings;

    public override void Enter()
    {
        base.Enter();
        colossus.Stop();
        elapsed = 0f;
        landed = false;
        finished = false;
        followupAt = -1f;
        punishUntil = float.MaxValue;
        lunged = 0f;
        airborne = false;
        gatherDamage = 0f;
        gatherBashes = 0;
        target = colossus.player.position;

        switch (kind)
        {
            case AshColossus.MoveKind.Cleave:
                StartBlow("Cleave", S.cleaveTell, colossus.cleaveContact);
                colossus.bossTell?.Glint(S.cleaveTell);
                break;
            case AshColossus.MoveKind.Sweep:
                StartBlow("Sweep", S.sweepTell, colossus.sweepContact);
                colossus.bossTell?.Ember(S.sweepTell);
                break;
            case AshColossus.MoveKind.Grasp:
                StartBlow("Grasp", S.graspTell, colossus.graspContact);
                colossus.bossTell?.Ember(S.graspTell);
                break;
            case AshColossus.MoveKind.Hammerfall:
                StartBlow("Hammerfall", S.hammerTell, colossus.hammerContact);
                colossus.bossTell?.Hum(S.hammerTell);
                break;
            case AshColossus.MoveKind.Leap:
                StartBlow("Leap", S.leapTell, colossus.leapLand);
                colossus.bossTell?.Ember(S.leapTell);
                shadow = ColossusFx.Shadow(colossus.shadowMaterial, target, S.leapRadius);
                break;
            case AshColossus.MoveKind.GreatCommand:
                float commandTell = colossus.CommandTell();
                StartBlow("Command", commandTell, colossus.commandRelease);
                colossus.bossTell?.Palm(commandTell);
                colossus.RaiseCommandTell(commandTell);
                break;
            case AshColossus.MoveKind.Gather:
                tell = S.gatherTime;
                contactAt = S.gatherTime;
                colossus.SetMoveSpeed(colossus.gatherLength / Mathf.Max(S.gatherTime, 0.5f));
                colossus.anim.CrossFadeInFixedTime("Gather", 0.2f, 0, 0f);
                colossus.bossTell?.Hum(S.gatherTime);
                colossus.RaiseGatherStart();
                break;
            case AshColossus.MoveKind.ThreadLash:
                StartBlow("ThreadLash", S.lashTell, colossus.lashRelease);
                colossus.bossTell?.Hiss(S.lashTell);
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        colossus.SetMoveSpeed(1f);
        colossus.StickBlade(false);
        colossus.bossTell?.StopTells();
        if (shadow != null)
            Object.Destroy(shadow);
        if (airborne)
            Land();
        if (colossus.agent.enabled)
            colossus.Stop();
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        if (followupAt >= 0f)
        {
            // Waiting out the kneel he forced: the Hammerfall lands after the player can move again.
            if (followupFar && Time.time - followupHit >= 0.4f)
            {
                finished = true;
                colossus.ApproachFor(AshColossus.MoveKind.Hammerfall, followupAt);
            }
            else if (followupFar == false && Time.time >= followupAt)
            {
                finished = true;
                colossus.StartMove(AshColossus.MoveKind.Hammerfall);
            }
            return;
        }

        switch (kind)
        {
            case AshColossus.MoveKind.Grasp: UpdateGrasp(); break;
            case AshColossus.MoveKind.Leap: UpdateLeap(); break;
            case AshColossus.MoveKind.Gather: UpdateGather(); break;
            default: UpdateSingle(); break;
        }

        if (kind == AshColossus.MoveKind.Cleave && landed && colossus.BladeStuck && elapsed >= punishUntil - 0.45f)
        {
            colossus.StickBlade(false);
            colossus.SetMoveSpeed(1.6f);
            ColossusFx.Burst(colossus, colossus.swordTip.position, 0.8f, 20);
        }

        if (landed && elapsed >= punishUntil && finished == false)
        {
            finished = true;
            colossus.MoveFinished();
        }
    }

    private void StartBlow(string state, float blowTell, float clipContact)
    {
        tell = blowTell;
        contactAt = blowTell;
        colossus.SetMoveSpeed(clipContact / Mathf.Max(blowTell, 0.05f));
        colossus.anim.CrossFadeInFixedTime(state, 0.15f, 0, 0f);
    }

    // --- Cleave, Sweep, Hammerfall, Great Command, Thread Lash ---

    private void UpdateSingle()
    {
        if (landed == false && elapsed < contactAt - 0.25f)
        {
            Track();
            target = colossus.transform.position + colossus.transform.forward * S.hammerOut;
        }

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        colossus.SetMoveSpeed(1f);
        Vector3 forward = colossus.transform.forward;
        Vector3 at = colossus.transform.position;
        switch (kind)
        {
            case AshColossus.MoveKind.Cleave:
            {
                colossus.StrikeLine(S.cleaveStart + S.cleaveLength, S.cleaveWidth, S.cleaveDamage, 50f, S.cleaveStart);
                Vector3 blade = at + forward * (S.cleaveStart + S.cleaveLength * 0.6f);
                ColossusFx.Burst(colossus, blade, 1.2f, 30);
                Shake(0.25f);
                colossus.RaiseCleave(blade);
                punishUntil = elapsed + S.cleavePunish;
                // The blade bites into the floor and he holds there, hauling at it, until it tears free.
                colossus.SetMoveSpeed(0.03f);
                colossus.StickBlade(true);
                break;
            }
            case AshColossus.MoveKind.Sweep:
            {
                Vector3 to = colossus.player.position - at;
                to.y = 0f;
                if (to.magnitude <= S.sweepReach + 0.35f && Vector3.Angle(forward, to) <= S.sweepArc * 0.5f)
                {
                    colossus.Deal(new DamageInfo
                    {
                        amount = S.sweepDamage,
                        poiseDamage = 40f,
                        direction = to.sqrMagnitude > 0.01f ? to.normalized : forward,
                        unblockable = true,
                        unparryable = true,
                        knockdown = true,
                        fling = S.sweepFling,
                    });
                }

                punishUntil = elapsed + S.sweepPunish;
                break;
            }
            case AshColossus.MoveKind.Hammerfall:
            {
                if (HallActors.FlatDistance(colossus.player.position, target) <= S.hammerRadius)
                    colossus.Deal(S.hammerDamage, 50f, true, true, 0f, 0.8f, colossus.player.position - target);
                ColossusFx.Burst(colossus, target, S.hammerRadius, 60);
                Shake(0.45f);
                // Lava cracks race out from the fists, brighten, and burst (owner, 2026-11-10).
                LavaCracks cracks = LavaCracks.Spawn(colossus.crackMaterial, colossus.crackMap, target, S.hammerRingRadius, Random.Range(0f, 360f));
                colossus.LastCracks = cracks;
                Vector3 erupt = target;
                colossus.StartCoroutine(cracks.Erupt(S.hammerCrackGrow, S.hammerCrackBrighten, S.hammerCrackFade, () =>
                {
                    if (colossus == null)
                        return;
                    LavaBurst.Spawn(erupt, Vector2.one * S.hammerRingRadius * 1.2f, colossus.emberMaterial, colossus.smokeMaterial, colossus.debrisMesh, colossus.debrisMaterial, 0.8f);
                    Shake(0.35f);
                    if (cracks.Covers(colossus.player.position, S.hammerCrackMargin))
                        colossus.Deal(S.hammerCrackDamage, 20f, true, true, 0f, 0.4f, colossus.player.position - erupt);
                }));
                colossus.RaiseHammerfall(target);
                punishUntil = elapsed + S.hammerPunish;
                break;
            }
            case AshColossus.MoveKind.GreatCommand:
                colossus.bossTell?.Bell();
                if (CommandReaches())
                {
                    colossus.Deal(S.greatCommandDamage, 0f, true, true, S.greatCommandKneel, 0f, colossus.player.position - at, ignoresRoll: true);
                    followupHit = Time.time;
                    followupAt = Time.time + S.greatCommandKneel + S.greatCommandFollowupGap - S.hammerTell;
                    followupFar = colossus.PlayerDistance() > colossus.StartRange(AshColossus.MoveKind.Hammerfall);
                }
                else
                {
                    punishUntil = elapsed + S.greatCommandPunish;
                }
                break;
            case AshColossus.MoveKind.ThreadLash:
            {
                Vector3 to = colossus.player.position - at;
                to.y = 0f;
                Vector3 aim = to.sqrMagnitude > 0.01f ? to.normalized : forward;
                int count = Mathf.Max(1, S.lashCount);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i - (count - 1) * 0.5f) * S.lashFan;
                    Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;
                    ThreadLine.Launch(colossus, at + aim * 1.8f, direction, S.threadSpeed, S.lashLength, S.threadWidth, S.lashDamage);
                }

                punishUntil = elapsed + 0.6f;
                break;
            }
        }
    }

    // The Great Command's line of sight (§6, decided): a ray from the mask's light to Kael's chest, anywhere in the
    // hall. What the drawn shadow covers is exactly what's safe.
    private bool CommandReaches()
    {
        Vector3 from = colossus.bossTell != null ? colossus.bossTell.PalmLightPosition : colossus.transform.position + Vector3.up * 5f;
        Vector3 chest = colossus.player.position + Vector3.up * 1.2f;
        return BossTell.LineOfSight(from, chest, colossus.transform, colossus.player);
    }

    // --- Grasp: the claw lunges along a locked line and closes ---

    private void UpdateGrasp()
    {
        float lockAt = contactAt - S.graspLock;
        float lungeAt = contactAt - S.graspLungeTime;

        if (landed == false && elapsed < lockAt)
        {
            Track();
            line = colossus.transform.forward;
        }

        if (landed == false && elapsed >= lungeAt)
        {
            // Lunges as far as it takes to put the claw on the player at the lock, at most graspLunge.
            float want = Mathf.Clamp(Vector3.Dot(colossus.player.position - colossus.transform.position, line) - S.graspHandOut, 0f, S.graspLunge);
            float t = Mathf.Clamp01((elapsed - lungeAt) / Mathf.Max(S.graspLungeTime, 0.05f));
            float goal = Mathf.Min(want, S.graspLunge) * (1f - (1f - t) * (1f - t));
            if (goal > lunged && colossus.agent.enabled && colossus.agent.isOnNavMesh)
                colossus.agent.Move(line * (goal - lunged));
            lunged = Mathf.Max(lunged, goal);
        }

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        colossus.SetMoveSpeed(1f);
        Vector3 hand = colossus.transform.position + line * S.graspHandOut;
        if (HallActors.FlatDistance(colossus.player.position, hand) <= S.graspRadius)
        {
            colossus.Deal(new DamageInfo
            {
                amount = S.graspDamage,
                poiseDamage = 0f,
                direction = line,
                unblockable = true,
                unparryable = true,
                knockdown = true,
                fling = S.graspFling,
                holdTime = S.graspHold,
                holder = colossus.palm,
            });
        }

        punishUntil = elapsed + S.graspPunish;
    }

    // --- Leap: crouch, the shadow follows the player and locks, then he comes down in it ---

    private void UpdateLeap()
    {
        float takeoff = contactAt * colossus.leapTakeoff / Mathf.Max(colossus.leapLand, 0.01f);
        if (landed == false && elapsed < contactAt - S.leapLock)
        {
            target = colossus.player.position;
            Track();
            ColossusFx.MoveShadow(shadow, target, S.leapRadius, Mathf.Clamp01(elapsed / contactAt));
        }

        if (landed == false && airborne == false && elapsed >= takeoff)
        {
            // Off the floor: out of the NavMesh's hands until he lands.
            airborne = true;
            takeoffFrom = colossus.transform.position;
            colossus.agent.enabled = false;
        }

        if (airborne && landed == false)
        {
            float t = Mathf.Clamp01((elapsed - takeoff) / Mathf.Max(contactAt - takeoff, 0.05f));
            Vector3 p = Vector3.Lerp(takeoffFrom, LandingPoint(), Mathf.SmoothStep(0f, 1f, t));
            colossus.transform.position = new Vector3(p.x, takeoffFrom.y, p.z);
        }

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        Land();
        colossus.SetMoveSpeed(1f);
        if (HallActors.FlatDistance(colossus.player.position, target) <= S.leapRadius)
            colossus.Deal(S.leapDamage, 50f, true, true, 0f, 0.8f, colossus.player.position - target);
        ColossusFx.Burst(colossus, target, S.leapRadius, 60);
        Shake(0.5f);
        if (shadow != null)
            Object.Destroy(shadow);
        punishUntil = elapsed + S.leapPunish;
    }

    // Where he comes down: the target on his NavMesh (a hole or the dais edge moves it to the nearest floor).
    private Vector3 LandingPoint()
    {
        var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = colossus.agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
        return UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit hit, 6f, filter) ? hit.position : target;
    }

    private void Land()
    {
        airborne = false;
        Vector3 at = LandingPoint();
        colossus.transform.position = at;
        colossus.agent.enabled = true;
        colossus.agent.Warp(at);
    }

    // --- Gather: the claw in the floor, the threads pulling; break it or he heals and the hall goes dark ---

    private void UpdateGather()
    {
        if (landed || elapsed < contactAt)
            return;

        landed = true;
        colossus.SetMoveSpeed(1f);
        colossus.Heal(S.gatherHeal);
        colossus.RaiseGatherComplete();
        punishUntil = elapsed + 0.6f;
    }

    // A hit on the Gather: true once it's broken (raw damage with heavies weighted, or bashes on the claw).
    public bool CountGatherHit(float weighted, bool bash)
    {
        if (landed)
            return false;
        gatherDamage += weighted;
        if (bash)
            gatherBashes++;
        return gatherDamage >= S.gatherBreakDamage || gatherBashes >= S.gatherBreakBashes;
    }

    private void Track()
    {
        colossus.transform.rotation = colossus.FaceTarget(colossus.player.position);
    }

    private static void Shake(float amount)
    {
        Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(amount, 0.4f);
    }
}
