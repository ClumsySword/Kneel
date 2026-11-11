using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Phase 2 of MasterPlaytest (Stage 6): the Unmaking under its own timeline, then the Ash Colossus move by move,
    // through the real input handlers. Groups: unmaking, skip, p2opening, cleave, sweep, grasp, hammer, leap, gcommand,
    // gather, lash, p2armour, p2half, p2perf, p2down. With none of them named (Only set to Phase 1 groups), it's skipped.
    public static partial class MasterPlaytest
    {
        private static AshColossus colossus;
        private static readonly List<Blow> p2Blows = new List<Blow>();
        private static Action<float> p2Listener;

        private static EnemyState P2State => colossus.stateMachine.currentState;

        private static ColossusMoveState P2Move => P2State as ColossusMoveState;

        private static MasterSettings S2 => colossus.settings;

        private static readonly string[] P2Groups = { "unmaking", "skip", "p2opening", "cleave", "sweep", "grasp", "hammer", "leap", "gcommand", "gather", "lash", "p2armour", "p2half", "p2perf", "p2down" };

        // The middle of the arena for him: the centre column (x 51.5-56.5), which never falls.
        private static readonly Vector3 P2Centre = new Vector3(54f, 9f, 455f);

        private static bool WantsAnyP2 => string.IsNullOrEmpty(Only) || Only.Split(',').Any(g => P2Groups.Contains(g));

        private static IEnumerator Phase2()
        {
            colossus = boss.colossus;
            if (colossus == null || WantsAnyP2 == false)
                yield break;

            colossus.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            p2Blows.Clear();
            p2Listener = amount =>
            {
                ColossusMoveState move = P2Move;
                Vector3 tip = colossus.swordTip != null ? colossus.swordTip.position - colossus.transform.position : Vector3.zero;
                p2Blows.Add(new Blow
                {
                    time = Time.time,
                    amount = amount,
                    state = move != null ? move.Kind.ToString() : P2State.GetType().Name,
                    since = move != null ? move.SinceStart : -1f,
                    tell = move != null ? move.Tell : -1f,
                    tipAngle = Vector3.SignedAngle(colossus.transform.forward, Flat(tip), Vector3.up),
                    tipOut = Flat(tip).magnitude,
                    tipUp = tip.y,
                    distance = P2Distance(),
                    blocked = combat.LastBlockHitTime >= Time.time - 0.001f,
                    player = combat.State.ToString(),
                });
            };
            playerHealth.OnDamaged += p2Listener;
            BackupSave();

            try
            {
                Note("Phase 2: the Ash Colossus. Tells: Cleave " + S2.cleaveTell + ", Sweep " + S2.sweepTell + ", Grasp " + S2.graspTell + ", Hammerfall " + S2.hammerTell + ", Leap " + S2.leapTell + ", Great Command " + S2.greatCommandTell + " then " + S2.greatCommandTellAgain + ", Lash " + S2.lashTell + ", Gather " + S2.gatherTime);
                Note("contacts (clip s): Cleave " + colossus.cleaveContact + ", Sweep " + colossus.sweepContact + ", Grasp " + colossus.graspContact + ", Hammerfall " + colossus.hammerContact + ", Leap " + colossus.leapTakeoff + "-" + colossus.leapLand + ", Command " + colossus.commandRelease + ", Lash " + colossus.lashRelease);
            }
            catch (Exception) { }

            if (Wants("unmaking") || string.IsNullOrEmpty(Only))
                yield return UnmakingRun(false);
            if (Wants("skip"))
                yield return UnmakingRun(true);

            if (colossus.Present == false || colossus.Engaged == false)
                yield return IntoPhase2();

            var scenarios = new (string group, Func<IEnumerator> body)[]
            {
                ("p2opening", P2Opening),
                ("cleave", CleaveLands),
                ("cleave", CleaveBlocked),
                ("cleave", CleaveParries),
                ("cleave", CleaveRoll),
                ("sweep", SweepLands),
                ("sweep", SweepRoll),
                ("grasp", GraspLands),
                ("grasp", GraspThroughGuard),
                ("grasp", GraspRoll),
                ("hammer", HammerLands),
                ("hammer", HammerCracks),
                ("leap", LeapLands),
                ("leap", LeapDodged),
                ("gcommand", GreatCommandOpen),
                ("gcommand", GreatCommandCover),
                ("gather", GatherBroken),
                ("gather", GatherBashed),
                ("gather", GatherCompletes),
                ("lash", LashLands),
                ("p2armour", P2Armour),
                ("p2perf", P2Performance),
                ("p2half", P2Half),
                ("p2down", P2Down),
            };

            foreach (var scenario in scenarios)
            {
                if (Wants(scenario.group) && colossus.IsDead == false)
                    yield return scenario.body();
            }

            playerHealth.OnDamaged -= p2Listener;
            RestoreSave();
        }

        // --- The Unmaking ---

        private static IEnumerator UnmakingRun(bool skip)
        {
            Note(skip ? "--- the Unmaking, skipped (not the first time)" : "--- the Unmaking (10 s)");
            // The Master on his feet again, whatever came before (Phase 1's end starts the real Unmaking).
            yield return ReviveP1();
            MasterBoss.ForceSkippableUnmaking = skip;

            // The player 6 m from him, in the open; a chandelier over the player's head to see the harmless drop.
            PlaceFront(6f);
            Vector3 knelt = master.transform.position;
            float playerHealthBefore = playerHealth.Current;
            int chandeliersUp = ChandelierDrop.All.Count(c => c.Current == ChandelierDrop.State.Hanging);
            boss.EndPhase1();
            float began = Time.time;
            Unmaking unmaking = boss.unmaking;
            yield return (Func<bool>)(() => unmaking.Running);
            if (skip)
            {
                yield return 1f;
                Unmaking.ForceSkip = true;
                yield return (Func<bool>)(() => unmaking.Running == false);
                Unmaking.ForceSkip = false;
                MasterBoss.ForceSkippableUnmaking = false;
                Check("the skip jumps to the mask split: input back after about 4 s", Mathf.Abs(Time.time - began - 4.1f) < 0.6f && unmaking.Skipped, (Time.time - began).ToString("F2") + " s, skipped " + unmaking.Skipped);
                Check("the hazards are down all the same", ChandelierDrop.All.All(c => c.Current != ChandelierDrop.State.Hanging) && BrazierTopple.All.All(b => b.Current != BrazierTopple.State.Standing), "");
                Check("and the Colossus is up and fighting", colossus.Present && colossus.Engaged, "");
                yield break;
            }

            yield return (Func<bool>)(() => unmaking.Elapsed >= 0.95f);
            Check("he turns the sword on himself (0.5 s)", master.GetComponent<MasterStabPose>() != null, "");
            Shot("Unmaking_1stab");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 2.2f);
            Check("the threads go taut (1.5 s)", UnityEngine.Object.FindAnyObjectByType<ThreadPull>() != null, UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Count(l => l.name == "Thread" && l.enabled) + " threads drawn");
            Shot("Unmaking_2threads");

            // Nothing lands on Kael: the hall's blows and fire alike.
            HallActors.HitPlayer(40f, playerRoot.position + Vector3.forward, null, 1.5f);
            HallActors.BurnPlayer(25f, null);
            yield return (Func<bool>)(() => unmaking.Elapsed >= 3.2f);
            Check("nothing lands on the player", playerHealth.Current >= playerHealthBefore - 0.01f && combat.IsKneeling == false, (playerHealthBefore - playerHealth.Current).ToString("F1") + " damage");
            var darkness = UnityEngine.Object.FindAnyObjectByType<DarknessWave>();
            Check("the darkness wave (2.5 s)", darkness != null && darkness.Active, "");
            Check("the chandeliers and braziers come down (3.0 s)", ChandelierDrop.All.All(c => c.Current != ChandelierDrop.State.Hanging) && BrazierTopple.All.All(b => b.Current != BrazierTopple.State.Standing), chandeliersUp + " chandeliers were up");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 4.8f);
            Shot("Unmaking_3vortex");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 5.2f);
            Check("the Master is gone at 5.0 s, in the vortex", master.gameObject.activeSelf == false, "");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 5.7f);
            float offset = HallActors.FlatDistance(unmaking.RisePoint, knelt);
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = colossus.agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            UnityEngine.AI.NavMesh.FindClosestEdge(unmaking.RisePoint, out UnityEngine.AI.NavMeshHit edge, filter);
            float clear = edge.distance + colossus.agent.radius;
            // Where nothing within reach is that clear (the fallen chandeliers lie about), he takes the clearest point.
            Check("the Colossus rises within " + S2.colossusMaxOffset + " m, " + S2.colossusClearance + " m clear of anything (or the clearest spot there is)", colossus.Present && offset <= S2.colossusMaxOffset + 0.1f && (clear >= S2.colossusClearance - 0.05f || Unmaking.LastRiseFellBack), offset.ToString("F1") + " m from the kneel, " + clear.ToString("F1") + " m clear (his NavMesh edge + his radius)" + (Unmaking.LastRiseFellBack ? "; nothing within reach was that clear, so the clearest spot" : ""));
            Check("untouchable as he rises", colossus.Untouchable, "");

            // The ring at the mask split pushes a player inside it out to 3 R.
            yield return (Func<bool>)(() => unmaking.Elapsed >= 7.1f);
            Shot("Unmaking_4rise");
            // 4 m from him on a side with nothing in the way of the push (the fallen chandeliers lie about).
            Vector3 toward = Flat(playerRoot.position - colossus.transform.position).normalized;
            // Everything that stops the player (the holes' lip walls are on Ignore Raycast), not him or Kael.
            int blocks = ~LayerMask.GetMask("Player", "Enemy");
            for (int i = 0; i < 24; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.back;
                Vector3 from = colossus.transform.position + d * 4f;
                bool roomToStand = Physics.CheckCapsule(from + Vector3.up * 0.5f, from + Vector3.up * 1.6f, 0.45f, blocks, QueryTriggerInteraction.Ignore) == false;
                if (roomToStand && Physics.CapsuleCast(from + Vector3.up * 0.5f, from + Vector3.up * 1.6f, 0.45f, d, out RaycastHit _, S2.ringPush - 3f, blocks, QueryTriggerInteraction.Ignore) == false)
                {
                    toward = d;
                    break;
                }
            }

            PlaceAbsolute(colossus.transform.position + toward * 4f, -toward);
            yield return (Func<bool>)(() => unmaking.Elapsed >= 7.45f);
            Note("  7.45 s: player " + P2Distance().ToString("F1") + " m from him (him at " + colossus.transform.position.ToString("F1") + ")");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 7.65f);
            Note("  7.65 s: player " + P2Distance().ToString("F1") + " m, " + combat.State + ", shoving " + typeof(PlayerCombat).GetField("shoving", Any).GetValue(combat) + ", left " + ((float)typeof(PlayerCombat).GetField("shoveLeft", Any).GetValue(combat)).ToString("F1"));
            yield return (Func<bool>)(() => unmaking.Elapsed >= 8.2f);
            Check("the mask splits and the ring pushes the player out to 3 R (7.5 s)", P2Distance() >= S2.ringPush - 0.6f, P2Distance().ToString("F1") + " m");
            Shot("Unmaking_5split");
            yield return (Func<bool>)(() => unmaking.Elapsed >= 9.0f);
            Check("the bar refills (8.5 s)", boss.Bar.Filling, "");
            Shot("Unmaking_6bar");
            yield return (Func<bool>)(() => unmaking.Running == false);
            Check("input returns at 10 s", Mathf.Abs(Time.time - began - S2.unmakingTime) < 0.3f && combat.IsRestricted == false, (Time.time - began).ToString("F2") + " s");
            Check("Phase 2 begins", boss.Current == MasterBoss.Beat.Phase2 && colossus.Engaged && colossus.Untouchable == false, boss.Current.ToString());
            yield return (Func<bool>)(() => P2Move != null || colossus.IsDead);
            Check("he opens with a Great Command", P2Move != null && P2Move.Kind == AshColossus.MoveKind.GreatCommand, P2State.GetType().Name + (P2Move != null ? " " + P2Move.Kind : ""));
            yield return P2WaitMoveEnds(true);
        }

        // The Master back on his feet, mid-fight, the Colossus hidden (for a second Unmaking).
        private static IEnumerator ReviveP1()
        {
            boss.StopBeats();
            UnityEngine.Object.FindAnyObjectByType<Kneel.Hazards.LevelReset>()?.ResetAll();
            colossus.Disengage();
            colossus.Hide();
            master.gameObject.SetActive(true);
            yield return null;
            master.Engage();
            typeof(MasterBoss).GetProperty("Current").SetValue(boss, MasterBoss.Beat.Phase1);
            yield return ResetFight(Centre);
        }

        // Straight into Phase 2 (the groups after the Unmaking, when it wasn't run).
        private static IEnumerator IntoPhase2()
        {
            yield return ResetFight(Centre);
            boss.StartPhase2At(P2Centre);
            yield return 0.3f;
        }

        // --- Fight control ---

        // He's put at `at`, facing down-screen, held, full health and poise, cooldowns clear, no half beat; the player armed.
        private static IEnumerator ResetP2(Vector3 at)
        {
            AshColossus.ForceNext = null;
            playerMove = Vector3.zero;
            aimAt = null;
            SetBlock(false);
            UnityEngine.Object.FindAnyObjectByType<Kneel.Hazards.LevelReset>()?.ResetAll();
            yield return (Func<bool>)(() => combat.IsKneeling == false && combat.IsKnockedDown == false && combat.State != PlayerCombat.CombatState.Dodging);

            if (colossus.Present == false)
                colossus.Show();
            if (colossus.agent.enabled == false)
                colossus.agent.enabled = true;
            colossus.agent.Warp(at);
            colossus.transform.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y + 180f, 0f);
            colossus.Untouchable = false;
            typeof(MasterBody).GetProperty("Engaged").SetValue(colossus, true);
            colossus.stateMachine.ChangeState(colossus.circleState);
            colossus.circleState.Hold(9999f);
            typeof(AshColossus).GetField("pendingTopple", Any).SetValue(colossus, false);
            typeof(AshColossus).GetField("halfReached", Any).SetValue(colossus, true);
            typeof(AshColossus).GetField("firstCommandDone", Any).SetValue(colossus, false);
            typeof(AshColossus).GetField("openingIndex", Any).SetValue(colossus, 99);
            typeof(AshColossus).GetField("deferred", Any).SetValue(colossus, null);
            typeof(AshColossus).GetProperty("ParryStreak").SetValue(colossus, 0);
            ((Dictionary<AshColossus.MoveKind, float>)typeof(AshColossus).GetField("readyAt", Any).GetValue(colossus)).Clear();
            ((List<AshColossus.MoveKind>)typeof(AshColossus).GetField("history", Any).GetValue(colossus)).Clear();
            colossus.poise.Reset();
            colossus.health.SetMax(colossus.health.Max, true);

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);
            P2PlaceFront(9f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
                Press("OnDrawPressed");
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.3f;
        }

        private static IEnumerator StartP2(AshColossus.MoveKind kind, float distance)
        {
            if (distance > 0f)
                P2PlaceFront(distance);
            AshColossus.ForceNext = kind;
            p2Mark = p2Blows.Count;
            colossus.circleState.Hold(0f);
            yield return (Func<bool>)(() => P2Move != null && P2Move.Kind == kind);
        }

        private static IEnumerator P2WaitMoveEnds(bool keepFighting = false)
        {
            yield return (Func<bool>)(() => P2Move == null && P2State != colossus.approachState);
            playerMove = Vector3.zero;
            if (keepFighting == false)
                colossus.circleState.Hold(9999f);
        }

        private static float P2Distance() => HallActors.FlatDistance(colossus.transform.position, playerRoot.position);

        private static void P2PlaceFront(float distance, bool keepFacing = false)
        {
            Vector3 at = colossus.transform.position + colossus.transform.forward * distance;
            PlaceAbsolute(at, colossus.transform.position - at);
            if (keepFacing == false)
                colossus.transform.rotation = Quaternion.LookRotation(Flat(playerRoot.position - colossus.transform.position));
        }

        // Where the current move's blows start in p2Blows (StartP2 sets it): its own blow, not a fire tick after it.
        private static int p2Mark;

        private static void ReportP2(string name, float tell, float damage, bool bladeInFront = false)
        {
            if (p2Blows.Count <= p2Mark)
            {
                Check(name + " lands", false, "no hit");
                return;
            }

            Blow blow = p2Blows[p2Mark];
            Check(name + " lands at the end of its tell (" + tell.ToString("F2") + " s)", Mathf.Abs(blow.since - tell) < 0.07f, blow.since.ToString("F3") + " s in");
            Check(name + " does " + damage, Mathf.Abs(blow.amount - damage) < 0.5f, blow.amount.ToString("F1"));
            if (bladeInFront)
                Check(name + ": the blade is in front of him", Mathf.Abs(blow.tipAngle) <= 50f, "tip " + blow.tipAngle.ToString("F0") + " deg, " + blow.tipOut.ToString("F2") + " m out, " + blow.tipUp.ToString("F2") + " m up; player " + blow.distance.ToString("F2") + " m away");
        }

        // --- The fixed opening ---

        private static IEnumerator P2Opening()
        {
            Note("--- his opening, the player standing 8 m away");
            yield return ResetP2(P2Centre);
            typeof(AshColossus).GetField("openingIndex", Any).SetValue(colossus, 0);
            P2PlaceFront(8f);
            colossus.circleState.Hold(0f);
            var seen = new List<string>();
            float start = Time.time;
            ColossusMoveState last = null;
            while (Time.time - start < 30f && seen.Count < 3)
            {
                if (P2Move != null && P2Move != last)
                {
                    last = P2Move;
                    seen.Add(P2Move.Kind.ToString());
                    Note("  " + (Time.time - start).ToString("F1") + " s: " + P2Move.Kind + " from " + P2Distance().ToString("F1") + " m");
                }
                else if (P2Move == null)
                {
                    last = null;
                }

                if (P2Move == null && combat.IsKneeling == false && combat.IsKnockedDown == false && P2Distance() > 9f)
                    P2PlaceFront(8f);
                yield return null;
            }

            Check("Great Command, then Cleave, then Hammerfall", string.Join(" ", seen.Take(3)) == "GreatCommand Hammerfall Cleave" || seen.Take(3).SequenceEqual(new[] { "GreatCommand", "Cleave", "Hammerfall" }) || (seen.Count >= 3 && seen[0] == "GreatCommand" && seen[1] == "Hammerfall" && seen[2] == "Cleave"), string.Join(" ", seen));
            yield return P2WaitMoveEnds();
        }

        // --- Cleave ---

        private static IEnumerator CleaveLands()
        {
            Note("--- Cleave, taken standing");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            Filmstrip2("Cleave");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            ReportP2("Cleave", S2.cleaveTell, S2.cleaveDamage);
            if (p2Blows.Count > p2Mark)
            {
                Blow cleave = p2Blows[p2Mark];
                // The Decree's chop comes down across his front from his right: at contact the tip is on its way past.
                Check("Cleave: the blade is coming down across his front", Mathf.Abs(cleave.tipAngle) <= 85f, "tip " + cleave.tipAngle.ToString("F0") + " deg, " + cleave.tipOut.ToString("F2") + " m out, " + cleave.tipUp.ToString("F2") + " m up");
            }
            Check("the blade sticks in the floor", colossus.BladeStuck, "");
            yield return 0.3f;
            Shot("P2_Cleave_stuck");
            if (colossus.swordTip != null)
                Check("its tip is down at the floor in front of him", colossus.swordTip.position.y < colossus.transform.position.y + 0.6f && Vector3.Dot(colossus.swordTip.position - colossus.transform.position, colossus.transform.forward) > 2f, (colossus.swordTip.position - colossus.transform.position).ToString("F1"));
            float stuck = Time.time;
            yield return (Func<bool>)(() => colossus.BladeStuck == false);
            Check("stuck about " + S2.cleavePunish + " s", Mathf.Abs(Time.time - stuck - (S2.cleavePunish - 0.45f - 0.35f)) < 0.3f, (Time.time - stuck + 0.35f).ToString("F2") + " s from contact");
            yield return P2WaitMoveEnds();

            // Out of the line: 3 m to his side.
            yield return ResetP2(P2Centre);
            before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            yield return (Func<bool>)(() => P2Move.SinceStart > S2.cleaveTell - 0.15f);
            PlaceAbsolute(colossus.transform.position + colossus.transform.forward * 4.5f + colossus.transform.right * 2.8f, colossus.transform.forward * -1f);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Check("2.8 m off the line: no hit", p2Blows.Count == before, (p2Blows.Count - before) + " hits");
            yield return P2WaitMoveEnds();
        }

        private static void Filmstrip2(string name)
        {
            if (TakeShots == false)
                return;
            EditorApplication.CallbackFunction tick = null;
            float started = Now;
            int shot = 0;
            tick = () =>
            {
                if (EditorApplication.isPlaying == false || P2Move == null || shot > 3)
                {
                    EditorApplication.update -= tick;
                    return;
                }

                float[] at = { P2Move.Tell * 0.5f, P2Move.Tell - 0.05f, P2Move.Tell + 0.25f, P2Move.Tell + 0.8f };
                if (P2Move.SinceStart >= at[shot])
                    Shot("P2_" + name + "_" + shot++);
            };
            EditorApplication.update += tick;
        }

        private static IEnumerator CleaveBlocked()
        {
            Note("--- Cleave into a held block");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            SetBlock(true);
            float before = playerHealth.Current;
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            float taken = before - playerHealth.Current;
            Check("the block takes it (glint)", taken > 0f && taken < S2.cleaveDamage * 0.5f, taken.ToString("F1") + " damage");
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator CleaveParries()
        {
            Note("--- Cleave parried: recoil; three running topple him");
            yield return ResetP2(P2Centre);
            for (int i = 0; i < 3; i++)
            {
                yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
                yield return (Func<bool>)(() => P2Move == null || P2Move.SinceStart >= S2.cleaveTell - 0.1f);
                Press("OnBlockPressed");
                yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
                yield return 0.05f;
                SetBlock(false);
                if (i < 2)
                {
                    Check("parry " + (i + 1) + ": he recoils, not toppled", P2State == colossus.recoilState && colossus.ParryStreak == i + 1, P2State.GetType().Name + ", streak " + colossus.ParryStreak);
                    float recoiled = Time.time;
                    yield return (Func<bool>)(() => P2State != colossus.recoilState);
                    if (i == 0)
                        Check("the recoil lasts " + S2.recoilTime + " s", Mathf.Abs(Time.time - recoiled - S2.recoilTime) < 0.2f, (Time.time - recoiled).ToString("F2") + " s");
                    colossus.circleState.Hold(9999f);
                    yield return 0.3f;
                }
                else
                {
                    Check("the third running topples him", P2State == colossus.toppleState, P2State.GetType().Name);
                }
            }

            yield return 0.9f;
            Shot("P2_Topple");
            float health = colossus.health.Current;
            P2PlaceFront(2.8f, keepFacing: true);
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => colossus.health.Current < health || P2State != colossus.toppleState);
            Check("a heavy on the Topple: critical, no armour (52.5)", Mathf.Abs(health - colossus.health.Current - 52.5f) < 0.6f, (health - colossus.health.Current).ToString("F1") + " damage");
            yield return (Func<bool>)(() => P2State != colossus.toppleState);
            yield return P2WaitMoveEnds();

            // A blow that hurts Kael resets the count.
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            yield return (Func<bool>)(() => P2Move == null || P2Move.SinceStart >= S2.cleaveTell - 0.1f);
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            yield return (Func<bool>)(() => P2State != colossus.recoilState);
            colossus.circleState.Hold(9999f);
            yield return 0.3f;
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            Check("a Cleave that hurts the player resets the streak", colossus.ParryStreak == 0, "streak " + colossus.ParryStreak);
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator CleaveRoll()
        {
            Note("--- Cleave, rolled aside");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Cleave, 4.5f);
            yield return (Func<bool>)(() => P2Move.SinceStart >= S2.cleaveTell - 0.3f);
            yield return Roll(colossus.transform.right);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Check("a roll to the side clears it", p2Blows.Count == before, (p2Blows.Count - before) + " hits");
            yield return P2WaitMoveEnds();
        }

        // --- Reaping Sweep: knockdown ---

        private static IEnumerator SweepLands()
        {
            Note("--- Reaping Sweep, taken standing (knockdown)");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Sweep, 4.5f);
            Filmstrip2("Sweep");
            SetBlock(true);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            ReportP2("Reaping Sweep (through the block)", S2.sweepTell, S2.sweepDamage);
            Check("the player is knocked down", combat.IsKnockedDown, combat.State.ToString());
            float down = Time.time;
            float before = playerHealth.Current;
            yield return 0.5f;
            Shot("P2_Knockdown");
            HallActors.HitPlayer(30f, playerRoot.position + Vector3.forward, null);
            Check("down: invulnerable", playerHealth.Current >= before - 0.01f, (before - playerHealth.Current).ToString("F1"));
            yield return (Func<bool>)(() => combat.IsKnockedDown == false);
            Check("down 1.0 s and up over 1.4 s", Mathf.Abs(Time.time - down - (combat.Settings.knockdownDownTime + combat.Settings.knockdownUpTime)) < 0.15f, (Time.time - down).ToString("F2") + " s");
            yield return P2WaitMoveEnds();

            // A roll cuts the get-up short (0.4 s into it).
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Sweep, 4.5f);
            yield return (Func<bool>)(() => combat.IsKnockedDown);
            down = Time.time;
            yield return combat.Settings.knockdownDownTime + combat.Settings.knockdownRollAfter + 0.05f;
            yield return Roll(Vector3.right);
            yield return (Func<bool>)(() => combat.IsKnockedDown == false);
            Check("a roll cuts the get-up after 0.4 s", combat.State == PlayerCombat.CombatState.Dodging && Time.time - down < combat.Settings.knockdownDownTime + combat.Settings.knockdownUpTime - 0.5f, (Time.time - down).ToString("F2") + " s, " + combat.State);
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator SweepRoll()
        {
            Note("--- Reaping Sweep, rolled through");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Sweep, 4.5f);
            yield return (Func<bool>)(() => P2Move.SinceStart >= S2.sweepTell - 0.2f);
            yield return Roll(Flat(colossus.transform.position - playerRoot.position).normalized);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Check("a roll through the claw clears it", p2Blows.Count == before, (p2Blows.Count - before) + " hits");
            yield return P2WaitMoveEnds();
        }

        // --- Grasp ---

        private static IEnumerator GraspLands()
        {
            Note("--- Grasp, from 7 m");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Grasp, 7f);
            Filmstrip2("Grasp");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            ReportP2("Grasp", S2.graspTell, S2.graspDamage);
            Vector3 grabbedAt = playerRoot.position;
            bool hidden = combat.IsHeld && combat.GetComponentsInChildren<Renderer>().All(r => r.enabled == false || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
            Check("held in the fist, hidden", hidden, combat.State.ToString());
            Shot("P2_Grasp_held");
            float grabbed = Time.time;
            yield return (Func<bool>)(() => combat.IsHeld == false);
            Check("for " + S2.graspHold + " s", Mathf.Abs(Time.time - grabbed - S2.graspHold) < 0.1f, (Time.time - grabbed).ToString("F2") + " s");
            Vector3 released = playerRoot.position;
            yield return 1f;
            float flung = HallActors.FlatDistance(playerRoot.position, released);
            Check("then flung about " + S2.graspFling + " m into a knockdown", flung > S2.graspFling * 0.6f && combat.IsKnockedDown, flung.ToString("F1") + " m, " + combat.State);
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator GraspThroughGuard()
        {
            Note("--- Grasp into a parry");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Grasp, 6f);
            yield return (Func<bool>)(() => P2Move.SinceStart >= S2.graspTell - 0.1f);
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            Check("the grab goes through the parry", p2Blows.Count > before && combat.IsKnockedDown, (p2Blows.Count - before) + " hits, " + combat.State);
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator GraspRoll()
        {
            Note("--- Grasp, rolled");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Grasp, 6f);
            yield return (Func<bool>)(() => P2Move.SinceStart >= S2.graspTell - 0.25f);
            yield return Roll(colossus.transform.right);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Check("a roll as the claw closes clears it", p2Blows.Count == before, (p2Blows.Count - before) + " hits");
            yield return P2WaitMoveEnds();
        }

        // --- Hammerfall ---

        private static IEnumerator HammerLands()
        {
            Note("--- Hammerfall, under it");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Hammerfall, S2.hammerOut);
            Filmstrip2("Hammerfall");
            SetBlock(true);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            ReportP2("Hammerfall (through the block)", S2.hammerTell, S2.hammerDamage);
            Vector3 impact = P2Move != null ? P2Move.Target : colossus.transform.position + colossus.transform.forward * S2.hammerOut;
            HallFloor floor = Floor;
            int panel = floor.IndexAt(impact);
            Note("  impact at " + impact.ToString("F1") + ", panel " + (panel >= 0 ? floor.panels[panel].name : "none") + (panel >= 0 && floor.panels[panel].neverFalls ? " (never falls)" : "") + "; untouched by his own fire: " + (colossus.health.Current >= colossus.health.Max - 0.1f));
            if (panel >= 0 && floor.panels[panel].neverFalls == false)
            {
                float hit = Time.time;
                yield return (Func<bool>)(() => floor.StateOf(panel) == HallFloor.PanelState.Fallen);
                Check("the panel under it gives way " + S2.hammerPanelDelay + " s later", Mathf.Abs(Time.time - hit - S2.hammerPanelDelay) < 0.35f, (Time.time - hit).ToString("F2") + " s");
            }

            yield return P2WaitMoveEnds();

            // Off the centre column, so the panel can go: he stands at x 46 (west band), the player 1 m in front.
            yield return ResetP2(new Vector3(45.5f, 9f, 447f));
            yield return StartP2(AshColossus.MoveKind.Hammerfall, S2.hammerOut);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            impact = P2Move != null ? P2Move.Target : colossus.transform.position;
            int under = floor.IndexAt(impact);
            int his = floor.IndexAt(colossus.transform.position);
            Note("  west band: impact panel " + (under >= 0 ? floor.panels[under].name + (floor.panels[under].neverFalls ? " (never falls)" : "") + " " + floor.StateOf(under) : "none") + ", his " + (his >= 0 ? floor.panels[his].name : "none"));
            if (under >= 0 && floor.panels[under].neverFalls == false)
            {
                float hit = Time.time;
                yield return (Func<bool>)(() => floor.StateOf(under) == HallFloor.PanelState.Fallen);
                Check("the panel under it gives way " + S2.hammerPanelDelay + " s later", Mathf.Abs(Time.time - hit - S2.hammerPanelDelay) < 0.35f, (Time.time - hit).ToString("F2") + " s");
                yield return 0.1f;
                if (under == his)
                    Check("he's on it: he Topples", P2State == colossus.toppleState, P2State.GetType().Name);
                Framed("P2_Hammer_hole", impact, 12f, 18f);
            }

            yield return P2WaitMoveEnds();
        }

        // The Hammerfall's lava cracks: on one when they burst, 25; between them, nothing.
        private static IEnumerator HammerCracks()
        {
            foreach (bool onCrack in new[] { true, false })
            {
                Note(onCrack ? "--- Hammerfall's cracks: standing on one when they burst" : "--- Hammerfall's cracks: standing between them");
                yield return ResetP2(P2Centre);
                yield return StartP2(AshColossus.MoveKind.Hammerfall, S2.hammerOut + 6f);
                yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
                LavaCracks cracks = colossus.LastCracks;
                Check("the cracks run out from the impact", cracks != null, "");
                if (cracks == null)
                    yield break;
                float landed = Time.time;
                yield return S2.hammerCrackGrow + 0.05f;
                Shot("P2_Hammer_cracks_" + (onCrack ? "on" : "off"));

                // A spot 5-7 m from the impact that is (or isn't) on a crack.
                Vector3 centre = cracks.transform.position;
                Vector3? spot = null;
                for (int i = 0; i < 720 && spot == null; i++)
                {
                    float r = 5f + (i % 5) * 0.5f;
                    Vector3 p = centre + Quaternion.Euler(0f, i * 7.3f, 0f) * Vector3.forward * r;
                    bool ok = onCrack ? cracks.Covers(p, 0f) : ClearOfCracks(cracks, p, S2.hammerCrackMargin + 0.5f);
                    if (ok)
                        spot = p;
                }

                Check(onCrack ? "a crack reaches 5-7 m out" : "there's floor between the cracks", spot.HasValue, "");
                if (spot.HasValue == false)
                    yield break;
                int before = p2Blows.Count;
                PlaceAbsolute(spot.Value, centre - spot.Value);
                yield return (Func<bool>)(() => cracks == null || cracks.Intensity >= LavaCracks.BurstIntensity - 0.1f);
                float burst = Time.time;
                yield return 0.1f;
                var hits = p2Blows.Skip(before).ToList();
                if (onCrack)
                {
                    Check("they burst " + (S2.hammerCrackGrow + S2.hammerCrackBrighten).ToString("F2") + " s after the impact", Mathf.Abs(burst - landed - S2.hammerCrackGrow - S2.hammerCrackBrighten) < 0.15f, (burst - landed).ToString("F2") + " s");
                    Check("on a crack: " + S2.hammerCrackDamage, hits.Count == 1 && Mathf.Abs(hits[0].amount - S2.hammerCrackDamage) < 0.5f, string.Join(", ", hits.Select(h => h.amount.ToString("F0"))));
                    Shot("P2_Hammer_burst");
                }
                else
                {
                    Check("between them: nothing", hits.Count == 0, string.Join(", ", hits.Select(h => h.amount.ToString("F0"))));
                }

                yield return S2.hammerCrackFade;
                Check("then they fade away", cracks == null, "");
                yield return P2WaitMoveEnds();
            }
        }

        // No crack anywhere within `radius` of p (a dense check: a crack is thin).
        private static bool ClearOfCracks(LavaCracks cracks, Vector3 p, float radius)
        {
            for (float x = -radius; x <= radius; x += 0.12f)
            {
                for (float z = -radius; z <= radius; z += 0.12f)
                {
                    if (x * x + z * z <= radius * radius && cracks.Covers(p + new Vector3(x, 0f, z), 0f, 0.2f))
                        return false;
                }
            }

            return true;
        }

        // --- Leap ---

        private static IEnumerator LeapLands()
        {
            Note("--- Leap, from 15 m, standing in the shadow");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Leap, 15f);
            Filmstrip2("Leap");
            Vector3 stood = playerRoot.position;
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            ReportP2("Leap", S2.leapTell, S2.leapDamage);
            Check("he comes down where the shadow locked", HallActors.FlatDistance(colossus.transform.position, stood) < 1.5f, HallActors.FlatDistance(colossus.transform.position, stood).ToString("F1") + " m from the player");
            Check("back on his NavMesh", colossus.agent.enabled && colossus.agent.isOnNavMesh, "");
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator LeapDodged()
        {
            Note("--- Leap: a sprint after the lock, then a roll as he lands");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.Leap, 15f);
            yield return (Func<bool>)(() => P2Move.SinceStart >= S2.leapTell - S2.leapLock + 0.02f);
            Vector3 locked = P2Move.Target;
            playerMove = colossus.transform.right;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, true);
            yield return (Func<bool>)(() => P2Move == null || P2Move.SinceStart >= S2.leapTell - 0.2f);
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);
            Note("  sprinting from the lock to 0.2 s before he lands covers " + HallActors.FlatDistance(playerRoot.position, locked).ToString("F1") + " m of the " + S2.leapRadius + " m circle");
            yield return Roll(colossus.transform.right);
            playerMove = Vector3.zero;
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Check("a roll as he lands clears it", p2Blows.Count == before, (p2Blows.Count - before) + " hits, " + HallActors.FlatDistance(playerRoot.position, locked).ToString("F1") + " m from the shadow's centre");
            yield return P2WaitMoveEnds();
        }

        // --- Great Command ---

        private static IEnumerator GreatCommandOpen()
        {
            Note("--- Great Command in the open, then the Hammerfall");
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.GreatCommand, 10f);
            Filmstrip2("Command");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.05f;
            ReportP2("Great Command", S2.greatCommandTell, S2.greatCommandDamage);
            Check("the player kneels " + S2.greatCommandKneel + " s", combat.IsKneeling, combat.State.ToString());
            yield return (Func<bool>)(() => combat.IsKneeling == false);
            float up = Time.time;
            int before = p2Blows.Count;
            yield return (Func<bool>)(() => (P2Move != null && P2Move.Kind == AshColossus.MoveKind.Hammerfall && P2Move.Landed) || Time.time - up > 6f);
            Check("the Hammerfall that follows lands after the player can move", P2Move != null && P2Move.Kind == AshColossus.MoveKind.Hammerfall && Time.time - up > 0.2f, (Time.time - up).ToString("F2") + " s after the kneel");
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator GreatCommandCover()
        {
            Note("--- Great Command against the pillar stump at (39, 438)");
            yield return ResetP2(new Vector3(44f, 9f, 452f));
            int before = p2Blows.Count;
            PlaceAbsolute(new Vector3(39f, 9f, 434.5f), Vector3.forward);
            colossus.transform.rotation = Quaternion.LookRotation(Flat(playerRoot.position - colossus.transform.position));
            AshColossus.ForceNext = AshColossus.MoveKind.GreatCommand;
            colossus.circleState.Hold(0f);
            yield return (Func<bool>)(() => P2Move != null);
            PlaceAbsolute(new Vector3(39f, 9f, 434.5f), Vector3.forward);
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            Vector3 from = colossus.bossTell.PalmLightPosition;
            Check("in the stump's shadow: no hit", p2Blows.Count == before, "mask light at " + from.ToString("F1") + ", line of sight " + BossTell.LineOfSight(from, playerRoot.position + Vector3.up * 1.2f, colossus.transform, playerRoot));
            Shot("P2_Command_cover");
            yield return P2WaitMoveEnds();
        }

        // --- Gather ---

        private static IEnumerator GatherBroken()
        {
            Note("--- the Gather, broken by heavies");
            yield return ResetP2(P2Centre);
            colossus.health.ApplyDamage(colossus.health.Max * 0.3f);
            AshColossus.ForceNext = null;
            var picks = new List<AshColossus.MoveKind>();
            P2PlaceFront(9f);
            for (int i = 0; i < 80; i++) picks.Add(colossus.NextMove());
            ((List<AshColossus.MoveKind>)typeof(AshColossus).GetField("history", Any).GetValue(colossus)).Clear();
            Check("below 75%, he picks the Gather at 6-12 m", picks.Contains(AshColossus.MoveKind.Gather), string.Join(" ", picks.GroupBy(p => p).Select(g => g.Key + " " + g.Count())));
            colossus.health.SetMax(colossus.health.Max, true);
            var above = new List<AshColossus.MoveKind>();
            for (int i = 0; i < 80; i++) above.Add(colossus.NextMove());
            Check("above 75%, never", above.Contains(AshColossus.MoveKind.Gather) == false, "");
            colossus.health.ApplyDamage(colossus.health.Max * 0.3f);

            yield return StartP2(AshColossus.MoveKind.Gather, 2.8f);
            Filmstrip2("Gather");
            yield return 0.6f;
            float health = colossus.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => colossus.health.Current < health || P2Move == null);
            Note("  one heavy: " + (health - colossus.health.Current).ToString("F1") + " damage (35 raw, x0.5 in the channel), counted " + (P2Move != null ? P2Move.GatherDamage.ToString("F0") : "-") + " toward the break, " + P2State.GetType().Name);
            if (P2State != colossus.toppleState)
            {
                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
                Press("OnHeavyPressed");
                yield return (Func<bool>)(() => P2State == colossus.toppleState || P2Move == null);
            }

            Check("heavies break it into a Topple", P2State == colossus.toppleState, P2State.GetType().Name);
            Check("and it heals nothing", colossus.health.Fraction < 0.71f, (colossus.health.Fraction * 100f).ToString("F0") + "%");
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator GatherBashed()
        {
            Note("--- the Gather, broken by two bashes");
            yield return ResetP2(P2Centre);
            colossus.health.ApplyDamage(colossus.health.Max * 0.3f);
            yield return StartP2(AshColossus.MoveKind.Gather, 2.6f);
            yield return 0.5f;
            for (int i = 0; i < 2 && P2State != colossus.toppleState; i++)
            {
                P2PlaceFront(2.4f, keepFacing: true);
                SetBlock(true);
                yield return 0.15f;
                Press("OnStrikePressed");
                yield return 0.7f;
                SetBlock(false);
                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            }

            Check("two bashes on the claw topple him", P2State == colossus.toppleState, P2State.GetType().Name);
            yield return P2WaitMoveEnds();
        }

        private static IEnumerator GatherCompletes()
        {
            Note("--- the Gather, left alone");
            yield return ResetP2(P2Centre);
            colossus.health.ApplyDamage(colossus.health.Max * 0.4f);
            float before = colossus.health.Current;
            var storm = UnityEngine.Object.FindAnyObjectByType<AshStorm>();
            storm?.Begin();
            yield return StartP2(AshColossus.MoveKind.Gather, 10f);
            yield return 2.5f;
            Check("the storm widens to 3 R while he gathers", storm != null && storm.Width >= storm.gatherBand - 0.5f, storm != null ? storm.Width.ToString("F1") + " m" : "no storm");
            Shot("P2_Gather");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.1f;
            float healed = colossus.health.Current - before;
            Check("he heals " + (S2.gatherHeal * 100f) + "%", Mathf.Abs(healed - colossus.health.Max * S2.gatherHeal) < 1f, healed.ToString("F1"));
            var darkness = UnityEngine.Object.FindAnyObjectByType<DarknessWave>();
            Check("and the hall goes dark", darkness != null && darkness.Active, "");
            storm?.Stop();
            yield return P2WaitMoveEnds();
        }

        // --- Thread Lash ---

        private static IEnumerator LashLands()
        {
            Note("--- Thread Lash at 15 m");
            yield return ResetP2(P2Centre);
            int before = p2Blows.Count;
            yield return StartP2(AshColossus.MoveKind.ThreadLash, 15f);
            Filmstrip2("Lash");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            yield return 0.3f;
            Check("five lines", UnityEngine.Object.FindObjectsByType<ThreadLine>(FindObjectsSortMode.None).Length == S2.lashCount, UnityEngine.Object.FindObjectsByType<ThreadLine>(FindObjectsSortMode.None).Length + " lines");
            float waited = Time.time;
            yield return 0.6f;
            Framed("P2_Lash_running", Vector3.Lerp(colossus.transform.position, playerRoot.position, 0.5f), 14f, 20f);
            yield return (Func<bool>)(() => p2Blows.Count > before || Time.time - waited > 3f);
            // Off the line before its fire catches up with the head (the fire is its own lesson).
            PlaceAbsolute(playerRoot.position + colossus.transform.right * 1.6f, colossus.transform.position - playerRoot.position);
            yield return 2f;
            // Only the Lash's own: its threads land after the move itself has ended, and he may start his next meanwhile.
            string[] otherMoves = Enum.GetNames(typeof(AshColossus.MoveKind)).Where(n => n != "ThreadLash").ToArray();
            var hits = p2Blows.Skip(before).Where(h => otherMoves.Contains(h.state) == false).ToList();
            Check("one hits, once: " + S2.lashDamage, hits.Count == 1 && Mathf.Abs(hits[0].amount - S2.lashDamage) < 0.5f, string.Join(", ", hits.Select(h => h.amount.ToString("F0"))));
            yield return P2WaitMoveEnds();
        }

        // --- Armour ---

        private static IEnumerator P2Armour()
        {
            Note("--- his ash armour");
            yield return ResetP2(P2Centre);
            P2PlaceFront(2.8f);
            yield return 0.1f;
            float health = colossus.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => colossus.health.Current < health);
            yield return 0.05f;
            Check("a light: 40%, no flinch (4.8)", Mathf.Abs(health - colossus.health.Current - 12f * S2.lightArmour) < 0.3f && P2State == colossus.circleState, (health - colossus.health.Current).ToString("F1") + ", " + P2State.GetType().Name);
            Check("and no poise", Mathf.Approximately(colossus.poise.Current, colossus.poise.maxPoise), colossus.poise.Current.ToString("F0"));
            yield return 1.2f;

            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Hammerfall, 2.8f);
            health = colossus.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => colossus.health.Current < health || P2Move == null || P2Move.Landed);
            Check("a light in a wind-up: 2.4", Mathf.Abs(health - colossus.health.Current - 2.4f) < 0.2f, (health - colossus.health.Current).ToString("F2"));
            yield return P2WaitMoveEnds();

            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Hammerfall, 2.8f);
            health = colossus.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => colossus.health.Current < health || P2Move == null || P2Move.Landed);
            Check("a heavy in a wind-up: 17.5", Mathf.Abs(health - colossus.health.Current - 17.5f) < 0.3f, (health - colossus.health.Current).ToString("F2"));
            yield return P2WaitMoveEnds();

            yield return ResetP2(P2Centre);
            colossus.Topple();
            yield return 0.9f;
            P2PlaceFront(2.8f, keepFacing: true);
            health = colossus.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => colossus.health.Current < health || P2State != colossus.toppleState);
            Check("a light on a Topple: critical, no armour (18)", Mathf.Abs(health - colossus.health.Current - 18f) < 0.3f, (health - colossus.health.Current).ToString("F2"));
            yield return (Func<bool>)(() => P2State != colossus.toppleState);
            yield return P2WaitMoveEnds();

            // A poise break in a wind-up waits for the blow, then topples him.
            yield return ResetP2(P2Centre);
            yield return StartP2(AshColossus.MoveKind.Hammerfall, 2.8f);
            colossus.poise.ApplyDamage(colossus.poise.maxPoise - 1f);
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => P2Move == null || P2Move.Landed);
            Check("a poise break doesn't stop the blow", P2Move != null && P2Move.Landed, P2State.GetType().Name);
            yield return (Func<bool>)(() => P2Move == null);
            Check("it topples him once the blow ends", P2State == colossus.toppleState, P2State.GetType().Name);
            float toppled = Time.time;
            yield return (Func<bool>)(() => P2State != colossus.toppleState);
            Check("the Topple lasts " + S2.toppleTime + " s", Mathf.Abs(Time.time - toppled - S2.toppleTime) < 0.2f, (Time.time - toppled).ToString("F2") + " s");
            yield return P2WaitMoveEnds();
        }

        // --- 50% ---

        private static IEnumerator P2Half()
        {
            Note("--- his 50% beat");
            yield return ResetP2(P2Centre);
            typeof(AshColossus).GetField("halfReached", Any).SetValue(colossus, false);
            colossus.health.ApplyDamage(colossus.health.Current - colossus.health.Max * 0.5f + 5f);
            P2PlaceFront(2.8f);
            Press("OnHeavyPressed");
            yield return 0.6f;
            // The beat comes at his next move.
            colossus.circleState.Hold(0f);
            yield return (Func<bool>)(() => P2State == colossus.roarState);
            float roared = Time.time;
            Check("he roars, untouchable", colossus.Untouchable, (colossus.health.Fraction * 100f).ToString("F0") + "%");
            var storm = UnityEngine.Object.FindAnyObjectByType<AshStorm>();
            Check("the ash storm begins", storm != null && storm.Running, "");
            yield return 1.5f;
            Shot("P2_Half_statues");
            yield return (Func<bool>)(() => P2State != colossus.roarState);
            Check("about " + S2.roarTime + " s, then the fight resumes", Mathf.Abs(Time.time - roared - S2.roarTime) < 0.2f, (Time.time - roared).ToString("F2") + " s");
            yield return 0.6f;
            Check("the four statues have fallen", HallStatue.All.Count > 0 && HallStatue.All.All(s => s.Current == HallStatue.State.Fallen), string.Join(", ", HallStatue.All.Select(s => s.Current.ToString())));
            storm?.Stop();
            yield return P2WaitMoveEnds();
        }

        // --- Performance: Phase 2 with everything going ---

        private static IEnumerator P2Performance()
        {
            Note("--- performance: Phase 2 with the storm and a darkness wave, 15 s of fighting");
            yield return ResetP2(P2Centre);
            UnityEngine.Object.FindAnyObjectByType<AshStorm>()?.Begin();
            UnityEngine.Object.FindAnyObjectByType<DarknessWave>()?.Begin();
            var times = new List<float>();
            int peakParticles = 0;
            float start = Time.time;
            colossus.circleState.Hold(0f);
            while (Time.time - start < 15f)
            {
                times.Add(Time.unscaledDeltaTime);
                if (Time.frameCount % 20 == 0)
                    peakParticles = Mathf.Max(peakParticles, UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(p => p.particleCount));
                if (P2Distance() > 12f || P2Distance() < 3f)
                    P2PlaceFront(7f);
                yield return null;
            }

            times.Sort();
            Note("  frame time (Editor, Play mode): p50 " + (times[times.Count / 2] * 1000f).ToString("F1") + " ms, p95 " + (times[(int)(times.Count * 0.95f)] * 1000f).ToString("F1") + " ms over " + times.Count + " frames; peak particles " + peakParticles);
            UnityEngine.Object.FindAnyObjectByType<AshStorm>()?.Stop();
            yield return P2WaitMoveEnds();
        }

        // --- Phase 2 ends ---

        private static IEnumerator P2Down()
        {
            Note("--- Phase 2 ends");
            yield return ResetP2(P2Centre);
            int downs = 0;
            boss.OnP2Down += () => downs++;
            colossus.health.ApplyDamage(colossus.health.Current - 3f);
            P2PlaceFront(2.8f);
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => colossus.IsDead);
            Check("he drops to both knees", colossus.IsDead && boss.Current == MasterBoss.Beat.Phase2Down && downs == 1, P2State.GetType().Name + ", beat " + boss.Current);
            yield return 1.5f;
            Shot("P2_Fallen");
        }
    }
}
