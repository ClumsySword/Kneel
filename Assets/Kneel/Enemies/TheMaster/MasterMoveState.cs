using UnityEngine;

// One of the Master's five Phase 1 moves (the Seize was cut, owner 2026-11-15) (The Master.md §4). Each has its own animator bool and plays its clip at the
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

    // When the current blow lands (game time), or -1 (it has landed, or the Guard is still walking in).
    public float LandsAt => landed || finished || followupAt >= 0f || (kind == MasterP1.MoveKind.Guard && thrusting == false)
        ? -1f : Time.time + (contactAt - elapsed);
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
        clipEndSet = false;
        master.SetLeftArmRest(kind == MasterP1.MoveKind.Decree || kind == MasterP1.MoveKind.Sentence);

        switch (kind)
        {
            case MasterP1.MoveKind.Decree:
                StartBlow("Decree", S.decreeTell, master.decreeContact, glint: true);
                break;
            case MasterP1.MoveKind.Sentence:
                StartSweep(0);
                break;
            case MasterP1.MoveKind.Command:
                float commandTell = master.CommandTell();
                StartBlow("Command", commandTell, master.commandRelease, glint: false);
                master.bossTell?.Palm(commandTell);
                master.RaiseCommandTell(commandTell);
                // The circle the fire will strike: it follows Kael until commandLock before the strike.
                mark = SpellMark.Ring(master.player.position, master.transform.position, S.commandRadius, commandTell, bold: 1.8f);
                break;
            case MasterP1.MoveKind.Threads:
                StartBlow("Threads", S.threadsTell, master.threadsRelease, glint: false);
                master.bossTell?.Hiss(S.threadsTell);
                master.bossTell?.Charge(S.threadsTell);
                mark = SpellMark.Lanes(ThreadsOrigin(AimAtPlayer()), AimAtPlayer(), 3, S.threadFan, S.threadLength, S.threadsTell);
                master.RaiseThreadsTell();
                break;
            case MasterP1.MoveKind.Guard:
                // His walk under the crossed blade (the GuardUp layer): he steps in, never glides.
                master.anim.SetFloat("Speed", 0f);
                master.anim.SetFloat("MoveRate", 1f);
                master.anim.CrossFadeInFixedTime("GuardWalk", 0.2f, 0, 0f);
                master.anim.SetBool("GuardUp", true);
                master.bossTell?.Hum(S.guardMaxTime);
                tell = S.guardMaxTime;
                break;
        }
    }

    public override void Exit()
    {
        base.Exit();
        master.SetMoveSpeed(1f);
        master.anim.SetBool("GuardUp", false);
        master.Stop();
        master.bossTell?.StopTells();
        if (mark != null)
            mark.Release();
        mark = null;
    }

    // The Threads' floor mark (SpellMark): it follows him onto the player, then locks threadsLock before the release.
    private SpellMark mark;

    private Vector3 AimAtPlayer()
    {
        Vector3 to = master.player.position - master.transform.position;
        to.y = 0f;
        return to.sqrMagnitude > 0.01f ? to.normalized : master.transform.forward;
    }

    private Vector3 ThreadsOrigin(Vector3 aim) => master.transform.position + aim * 1.2f;

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

        if (mark != null && landed == false)
        {
            bool command = kind == MasterP1.MoveKind.Command;
            float lockBefore = command ? S.commandLock : S.threadsLock;
            if (elapsed < contactAt - lockBefore)
            {
                if (command)
                    mark.Aim(master.player.position, master.player.position - master.transform.position);
                else
                    mark.Aim(ThreadsOrigin(AimAtPlayer()), AimAtPlayer());
            }
            else if (mark.Locked == false)
            {
                mark.Lock();
                // The fire starts falling onto the locked circle.
                if (command)
                    FireStrike.Begin(mark.Origin, S.commandRadius, lockBefore);
            }
        }

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
                Vector3 struck = mark != null ? mark.Origin : master.player.position;
                if (mark != null)
                    mark.Release();
                mark = null;
                if (HallActors.FlatDistance(master.player.position, struck) <= S.commandRadius + 0.3f)
                {
                    master.bossTell?.Bell();
                    // Unblockable and unparryable; a roll's i-frames get through it (the last-moment answer).
                    master.Deal(S.commandDamage, 0f, true, true, S.commandKneel, 0f, master.player.position - struck);
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
                // Down the lanes the mark drew at the lock.
                Vector3 forward = mark != null ? mark.Forward : AimAtPlayer();
                Vector3 from = mark != null ? mark.Origin : ThreadsOrigin(forward);
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis(i * S.threadFan, Vector3.up) * forward;
                    ThreadLine.Launch(master, from, direction, S.threadSpeed, S.threadLength, S.threadWidth, S.threadDamage);
                }
                if (mark != null)
                    mark.Release();
                mark = null;
                punishUntil = elapsed + 0.6f;
                break;
        }
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
                    // Until the follow-through has played out (set below, once the animator reports the clip).
                    punishUntil = elapsed + 1.5f;
                }
            }
            return;
        }

        if (clipEndSet == false && (swing == 2 || cancelled) && nextSweepAt < 0f)
        {
            AnimatorStateInfo info = master.anim.GetCurrentAnimatorStateInfo(0);
            if (master.anim.IsInTransition(0) == false && (info.IsName("Sentence3") || info.IsName("Sentence2")))
            {
                clipEndSet = true;
                float left = Mathf.Max(0f, 1f - info.normalizedTime) * info.length;
                punishUntil = elapsed + left + S.sentencePunish;
            }
        }

        if (nextSweepAt >= 0f && elapsed >= nextSweepAt)
        {
            nextSweepAt = -1f;
            if (cancelled)
                punishUntil = elapsed + 1.5f;
            else
                StartSweep(swing + 1);
        }
    }

    private float nextSweepAt = -1f;
    private bool clipEndSet;

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

    // --- Regal Guard: walk in behind the crossed blade, then the thrust ---

    private void UpdateGuard()
    {
        if (thrusting == false)
        {

            // Turns toward the player at guardTurnRate while walking in.
            Vector3 to = master.player.position - master.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                master.transform.rotation = Quaternion.RotateTowards(master.transform.rotation, Quaternion.LookRotation(to), S.guardTurnRate * Time.deltaTime);

            bool walking = elapsed > 0.45f && master.agent.enabled && master.agent.isOnNavMesh;
            if (walking)
                master.agent.Move(master.transform.forward * S.guardWalkSpeed * Time.deltaTime);
            // The walk tree at the speed he's really going, so the feet match it.
            master.anim.SetFloat("Speed", walking ? S.guardWalkSpeed : 0f);

            if (elapsed >= S.guardMaxTime || (elapsed > 0.6f && master.PlayerDistance() <= S.thrustReach - 0.5f))
            {
                thrusting = true;
                master.anim.SetBool("GuardUp", false);
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
