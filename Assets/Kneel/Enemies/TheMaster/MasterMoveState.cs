using UnityEngine;

// One of the Master's six Phase 1 moves (The Master.md §4). Each has its own animator bool and plays its clip at the
// speed that lands its contact at the end of its tell, so tells are tuned live in MasterSettings. Until a blow lands
// he is armoured (hits do half damage, no flinch); he tracks the player until he commits.
public class MasterMoveState : EnemyState
{
    private readonly MasterP1 master;
    private readonly MasterP1.MoveKind kind;

    private float elapsed;
    private float tell;
    private float contactAt;
    private bool landed;
    private float punishUntil;
    private bool finished;

    // Sentence: which sweep, and whether a parry on the second cancelled the third.
    private int swing;
    private float swingStart;
    private bool cancelled;

    // Seize: the lunge's locked line and how far along it he is.
    private Vector3 line;
    private float lunged;

    // Command: a follow-up Decree is due at this time (after the player rises); out of its reach, he strides in.
    private float followupAt = -1f;
    private float followupHit;
    private bool followupFar;

    // Guard: walking in behind the crossed blade, then the thrust.
    private bool thrusting;

    public MasterMoveState(Enemy enemyBase, EnemyStateMachine stateMachine, string animBoolName, MasterP1.MoveKind kind)
        : base(enemyBase, stateMachine, animBoolName)
    {
        master = enemyBase as MasterP1;
        this.kind = kind;
    }

    public MasterP1.MoveKind Kind => kind;

    // Before the blow lands: hits do windupDamageMultiplier and never stop him.
    public bool Armoured => landed == false && (kind != MasterP1.MoveKind.Guard || thrusting);

    // The Regal Guard is up: frontal hits stop on the blade. It holds through the thrust's wind-up until he commits,
    // so a player who closes in for the bash as the thrust starts still breaks it.
    public bool Guarding => kind == MasterP1.MoveKind.Guard && elapsed > 0.25f && finished == false
        && (thrusting == false || (landed == false && elapsed < contactAt - 0.2f));

    // The current blow's tell, and seconds since it started (for the playtest).
    public float Tell => tell;
    public float SinceStart => kind == MasterP1.MoveKind.Sentence ? elapsed - swingStart
        : kind == MasterP1.MoveKind.Guard && thrusting ? elapsed - (contactAt - tell) : elapsed;
    public bool Thrusting => thrusting;
    public int Swing => swing;
    public bool Landed => landed;

    private MasterSettings S => master.settings;

    public override void Enter()
    {
        base.Enter();
        master.Stop();
        elapsed = 0f;
        landed = false;
        finished = false;
        cancelled = false;
        thrusting = false;
        followupAt = -1f;
        punishUntil = float.MaxValue;
        swing = 0;
        swingStart = 0f;
        lunged = 0f;
        master.SetLeftArmRest(kind == MasterP1.MoveKind.Decree || kind == MasterP1.MoveKind.Sentence);

        switch (kind)
        {
            case MasterP1.MoveKind.Decree:
                StartBlow("Decree", S.decreeTell, master.decreeContact, glint: true);
                break;
            case MasterP1.MoveKind.Sentence:
                StartSweep(0);
                break;
            case MasterP1.MoveKind.Seize:
                StartBlow("Seize", S.seizeTell, master.seizeContact, glint: false);
                master.bossTell?.Ember(S.seizeTell);
                break;
            case MasterP1.MoveKind.Command:
                float commandTell = master.CommandTell();
                StartBlow("Command", commandTell, master.commandRelease, glint: false);
                master.bossTell?.Palm(commandTell);
                master.RaiseCommandTell(commandTell);
                break;
            case MasterP1.MoveKind.Threads:
                StartBlow("Threads", S.threadsTell, master.threadsRelease, glint: false);
                master.bossTell?.Hiss(S.threadsTell);
                master.RaiseThreadsTell();
                break;
            case MasterP1.MoveKind.Guard:
                master.anim.CrossFadeInFixedTime("GuardIn", 0.15f, 0, 0f);
                master.bossTell?.Hum(S.guardMaxTime);
                tell = S.guardMaxTime;
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        master.SetMoveSpeed(1f);
        master.Stop();
        master.bossTell?.StopTells();
    }

    public override void Update()
    {
        base.Update();
        elapsed += Time.deltaTime;

        if (followupAt >= 0f)
        {
            // Waiting out the kneel he forced: the Decree that follows lands after the player can move again.
            if (followupFar && Time.time - followupHit >= 0.4f)
            {
                // The arm comes down, then he strides in: the Decree still lands only after the player can move.
                finished = true;
                master.ApproachFor(MasterP1.MoveKind.Decree, followupAt);
            }
            else if (followupFar == false && Time.time >= followupAt)
            {
                finished = true;
                master.StartMove(MasterP1.MoveKind.Decree);
            }
            return;
        }

        switch (kind)
        {
            case MasterP1.MoveKind.Sentence: UpdateSentence(); break;
            case MasterP1.MoveKind.Guard: UpdateGuard(); break;
            case MasterP1.MoveKind.Seize: UpdateSeize(); break;
            default: UpdateSingle(); break;
        }

        if (landed && elapsed >= punishUntil && finished == false)
        {
            finished = true;
            master.MoveFinished();
        }
    }

    // --- One blow: Decree, Command, Threads ---

    private void StartBlow(string state, float blowTell, float clipContact, bool glint)
    {
        tell = blowTell;
        contactAt = blowTell;
        master.SetMoveSpeed(clipContact / Mathf.Max(blowTell, 0.05f));
        master.anim.CrossFadeInFixedTime(state, 0.12f, 0, 0f);
        if (glint)
            master.bossTell?.Glint(blowTell);
    }

    private void UpdateSingle()
    {
        if (landed == false && elapsed < contactAt - 0.25f)
            Track();

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        master.SetMoveSpeed(1f);
        switch (kind)
        {
            case MasterP1.MoveKind.Decree:
                master.StrikeArc(S.decreeReach, S.decreeArc, S.decreeDamage, 40f);
                punishUntil = elapsed + S.decreePunish;
                break;
            case MasterP1.MoveKind.Command:
                if (CommandReaches())
                {
                    master.bossTell?.Bell();
                    master.Deal(S.commandDamage, 0f, true, true, S.commandKneel, 0f, master.player.position - master.transform.position, ignoresRoll: true);
                    followupHit = Time.time;
                    followupAt = Time.time + S.commandKneel + S.commandFollowupGap - S.decreeTell;
                    followupFar = master.PlayerDistance() > master.StartRange(MasterP1.MoveKind.Decree);
                }
                else
                {
                    master.bossTell?.Bell();
                    punishUntil = elapsed + S.commandPunish;
                }
                break;
            case MasterP1.MoveKind.Threads:
                Vector3 to = master.player.position - master.transform.position;
                to.y = 0f;
                Vector3 forward = to.sqrMagnitude > 0.01f ? to.normalized : master.transform.forward;
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(i * S.threadFan, Vector3.up) * forward;
                    ThreadLine.Launch(master, master.transform.position + forward * 1.2f, direction, S.threadSpeed, S.threadLength, S.threadWidth, S.threadDamage);
                }
                punishUntil = elapsed + 0.6f;
                break;
        }
    }

    // The Command's line of sight (§6, decided): a ray from the palm light to Kael's chest, inside his front cone.
    // What the drawn shadow covers is exactly what's safe.
    private bool CommandReaches()
    {
        Vector3 to = master.player.position - master.transform.position;
        to.y = 0f;
        if (Vector3.Angle(master.transform.forward, to) > S.commandCone * 0.5f)
            return false;

        Vector3 from = master.bossTell != null ? master.bossTell.PalmLightPosition : master.transform.position + Vector3.up * 4f;
        Vector3 chest = master.player.position + Vector3.up * 1.2f;
        return BossTell.LineOfSight(from, chest, master.transform, master.player);
    }

    // --- Sentence: three sweeps, each its own glint; the third holds at the top ---

    private void StartSweep(int index)
    {
        swing = index;
        swingStart = elapsed;
        landed = false;
        tell = index == 0 ? S.sentenceTell1 : index == 1 ? S.sentenceTell2 : S.sentenceTell3;
        contactAt = swingStart + tell;
        float clipContact = master.sentenceContacts[Mathf.Clamp(index, 0, master.sentenceContacts.Length - 1)];
        master.SetMoveSpeed(clipContact / Mathf.Max(tell, 0.05f));
        master.anim.CrossFadeInFixedTime("Sentence" + (index + 1), index == 0 ? 0.12f : 0.08f, 0, 0f);
        master.bossTell?.Glint(tell);
    }

    private void UpdateSentence()
    {
        if (landed == false)
        {
            if (elapsed < contactAt - 0.2f)
                Track();

            if (elapsed >= contactAt)
            {
                landed = true;
                master.SetMoveSpeed(1f);
                master.StrikeArc(S.sentenceReach, S.sentenceArc, S.sentenceDamage, 35f);

                // The next sweep starts as this one's blade passes, unless it was the last (or a parry cancelled it).
                if (swing < 2 && cancelled == false)
                {
                    float next = elapsed + 0.15f;
                    nextSweepAt = next;
                }
                else
                {
                    punishUntil = elapsed + S.sentencePunish;
                }
            }
            return;
        }

        if (nextSweepAt >= 0f && elapsed >= nextSweepAt)
        {
            nextSweepAt = -1f;
            if (cancelled)
                punishUntil = elapsed + S.sentencePunish;
            else
                StartSweep(swing + 1);
        }
    }

    private float nextSweepAt = -1f;

    // A parry on a glint. True when the move handles it itself (Sentence's first two sweeps are only deflected; a parry
    // on the second cancels the third); false lets MasterP1 drive him to one knee.
    public bool OnParried()
    {
        if (kind != MasterP1.MoveKind.Sentence)
            return false;

        if (swing == 0)
        {
            master.bossTell?.Deflect();
            return true;
        }

        if (swing == 1)
        {
            master.bossTell?.Deflect();
            cancelled = true;
            return true;
        }

        return false;
    }

    // --- Seize: an ember-handed lunge and grab ---

    private void UpdateSeize()
    {
        float lockAt = contactAt - S.seizeLock;
        float lungeAt = contactAt - S.seizeLungeTime;

        if (landed == false && elapsed < lockAt)
        {
            Track();
            line = master.transform.forward;
        }

        if (landed == false && elapsed >= lungeAt && lunged < S.seizeLunge)
        {
            // The lunge: a fast burst along the locked line, stopping at a player still standing in it.
            float t = Mathf.Clamp01((elapsed - lungeAt) / Mathf.Max(S.seizeLungeTime, 0.05f));
            float target = S.seizeLunge * (1f - (1f - t) * (1f - t));
            Vector3 toPlayer = master.player.position - master.transform.position;
            toPlayer.y = 0f;
            if (Vector3.Dot(toPlayer, line) > 0f && Vector3.Dot(toPlayer, line) < 1.4f && (toPlayer - line * Vector3.Dot(toPlayer, line)).magnitude < 1f)
                target = lunged;
            if (master.agent.enabled && master.agent.isOnNavMesh)
                master.agent.Move(line * (target - lunged));
            lunged = target;
        }

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        master.SetMoveSpeed(1f);
        Vector3 hand = master.transform.position + master.transform.forward * 1.4f;
        Vector3 offset = master.player.position - hand;
        offset.y = 0f;
        if (offset.magnitude <= S.seizeGrabRadius)
        {
            // Jerked about 1 m toward him, then slammed to his knees (decided: no paired animation).
            PlayerCombat combat = master.PlayerCombat;
            float shove = combat != null ? Mathf.Max(combat.Settings.impactShove, 0.01f) : 1f;
            master.Deal(S.seizeDamage, 0f, true, true, S.seizeKneel, 1f / shove, master.transform.position - master.player.position);
        }

        punishUntil = elapsed + S.seizePunish;
    }

    // --- Regal Guard: walk in behind the crossed blade, then the thrust ---

    private void UpdateGuard()
    {
        if (thrusting == false)
        {
            if (elapsed > 0.45f && elapsed - Time.deltaTime <= 0.45f)
                master.anim.CrossFadeInFixedTime("GuardIdle", 0.1f, 0, 0f);

            // Turns toward the player at guardTurnRate while walking in.
            Vector3 to = master.player.position - master.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                master.transform.rotation = Quaternion.RotateTowards(master.transform.rotation, Quaternion.LookRotation(to), S.guardTurnRate * Time.deltaTime);

            if (elapsed > 0.45f && master.agent.enabled && master.agent.isOnNavMesh)
                master.agent.Move(master.transform.forward * S.guardWalkSpeed * Time.deltaTime);

            if (elapsed >= S.guardMaxTime || (elapsed > 0.6f && master.PlayerDistance() <= S.thrustReach - 0.5f))
            {
                thrusting = true;
                master.bossTell?.StopTells();
                StartBlow("Thrust", S.thrustTell, master.thrustContact, glint: true);
                contactAt = elapsed + S.thrustTell;
            }
            return;
        }

        if (landed == false && elapsed < contactAt - 0.2f)
            Track();

        if (landed || elapsed < contactAt)
            return;

        landed = true;
        master.SetMoveSpeed(1f);
        master.StrikeLine(S.thrustReach, S.thrustWidth, S.thrustDamage, 45f);
        punishUntil = elapsed + S.thrustPunish;
    }

    private void Track()
    {
        master.transform.rotation = master.FaceTarget(master.player.position);
    }
}
