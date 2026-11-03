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
    // A scripted fight with one Ashen Footman. Start it once a level with Footmen is playing (Kneel/Enemies/Ashen
    // Footman/Playtest). Player input goes through the real handlers with the input cached the way CLAUDE.md says, and
    // the Footman runs his own AI, so what it measures is the fight itself: wind-up, hit timing, reach, parry, roll,
    // shield and foot sliding. Results go to Temp/FootmanPlaytest.txt. Nothing in the scene is saved.
    public static class FootmanPlaytest
    {
        public const string ResultPath = "Temp/FootmanPlaytest.txt";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Screenshots stall a frame, so timing runs leave them off.
        public static bool TakeShots;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();

        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static AshenFootman footman;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static Vector3 home;
        private static Quaternion homeFacing;
        private static float homeAttackRange;
        private static float homeTurnSpeed;
        private static float homeChaseSpeed;
        private static float homeAggression;
        private static float lastPlayerDamage;
        private static float lastPlayerDamageTime;
        private static float worstFrame;
        private static int failures;
        private static Vector3 playerMove;

        private static float Now => Time.unscaledTime;

        [MenuItem("Kneel/Enemies/Ashen Footman/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in a level with Footmen first.");
                return;
            }

            Lines.Clear();
            Errors.Clear();
            failures = 0;
            worstFrame = 0f;
            Application.runInBackground = true;
            Application.targetFrameRate = 90;
            Application.logMessageReceived += OnLog;

            routines.Clear();
            routines.Push(Run());
            waitFor = null;
            waitUntil = 0f;
            EditorApplication.update += Tick;
        }

        // Walks up to every Footman in the open level: each must be on its NavMesh, wake, and chase. Gibbet Footmen must
        // wait in the post, land at the spawn point, take the long stagger if struck early, and keep to their leash.
        [MenuItem("Kneel/Enemies/Ashen Footman/Level Check")]
        public static void BeginLevelCheck()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in a level with Footmen first.");
                return;
            }

            Lines.Clear();
            Errors.Clear();
            failures = 0;
            worstFrame = 0f;
            Application.runInBackground = true;
            Application.targetFrameRate = 90;
            Application.logMessageReceived += OnLog;

            routines.Clear();
            routines.Push(LevelCheck());
            waitFor = null;
            waitUntil = 0f;
            EditorApplication.update += Tick;
        }

        private static IEnumerator LevelCheck()
        {
            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            playerHealth = combat.GetComponent<Health>();
            playerRoot = combat.transform;
            ((PlayerControls)typeof(PlayerCombat).GetField("controls", Any).GetValue(combat)).Disable();
            ((PlayerControls)typeof(PlayerMovement).GetField("controls", Any).GetValue(movement)).Disable();
            playerHealth.SetMax(100000f, true);

            yield return 1f;

            AshenFootman[] all = UnityEngine.Object.FindObjectsByType<AshenFootman>(FindObjectsInactive.Include)
                .Where(f => f.gameObject.scene.IsValid()).OrderBy(f => f.transform.parent != null ? f.transform.parent.name : f.name).ToArray();
            Note(all.Length + " Footmen in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            foreach (AshenFootman candidate in all)
            {
                footman = candidate;
                Transform spawn = candidate.transform.parent;
                string label = spawn != null ? spawn.name : candidate.name;

                if (candidate.stateMachine.currentState == candidate.dormantState)
                {
                    yield return GibbetFootman(candidate, label);
                    continue;
                }

                // A gibbet's 8 m trigger can reach the check's visit to a neighbour, which wakes this one early.
                if (typeof(AshenFootman).GetField("ambush", Any).GetValue(candidate) != null)
                {
                    Check(label + " was woken early by its gibbet and is hunting", candidate.agent.isOnNavMesh && candidate.stateMachine.currentState != candidate.idleState, candidate.stateMachine.currentState.GetType().Name);
                    continue;
                }

                Check(label + " has its AI on at " + candidate.transform.position.ToString("F1"), candidate.enabled && candidate.health.enabled, "");
                Check(label + " is on its NavMesh", candidate.agent.enabled && candidate.agent.isOnNavMesh, NavMeshName(candidate));
                Check(label + " is holding its post", candidate.stateMachine.currentState == candidate.idleState || candidate.stateMachine.currentState == candidate.moveState, candidate.stateMachine.currentState.GetType().Name);

                Vector3 before = candidate.transform.position;
                PlaceNear(candidate, 5f);
                yield return (Func<bool>)(() => candidate.stateMachine.currentState == candidate.chaseState);
                yield return 1f;
                float placed = Vector3.Distance(before, playerRoot.position);
                bool closeEnoughToSwing = placed < candidate.attackRange + 0.5f;
                Check(label + " wakes and comes for the player", closeEnoughToSwing || Vector3.Distance(before, candidate.transform.position) > 0.5f,
                    "moved " + Vector3.Distance(before, candidate.transform.position).ToString("F1") + " m toward a player " + placed.ToString("F1") + " m away, path " + candidate.agent.pathStatus + ", now " + candidate.stateMachine.currentState.GetType().Name);

                PlaceNear(candidate, 2.6f);
                yield return 0.6f;
                Shot("Level_" + label);
                ClearPlayer();
                yield return 0.2f;
            }
        }

        private static IEnumerator GibbetFootman(AshenFootman candidate, string label)
        {
            var ambush = (Kneel.Hazards.GibbetAmbush)typeof(AshenFootman).GetField("ambush", Any).GetValue(candidate);
            var leash = (Collider)typeof(AshenFootman).GetField("leash", Any).GetValue(candidate);
            bool strikeEarly = label.EndsWith("_4");

            Check(label + " waits out of the world", candidate.GetComponentInChildren<Renderer>().enabled == false && candidate.agent.enabled == false, "");

            if (strikeEarly)
            {
                ambush.TakeHit(new DamageInfo { amount = 1f, source = combat.gameObject, direction = Vector3.forward });
            }
            else
            {
                PlaceNearPoint(ambush.transform.position + Vector3.back * 4f, candidate);
            }

            yield return (Func<bool>)(() => candidate.stateMachine.currentState != candidate.dormantState);
            Check(label + " tears free at the gibbet's spawn point", Vector3.Distance(candidate.transform.position, ambush.SpawnPoint.position) < 1f && candidate.agent.isOnNavMesh, NavMeshName(candidate));
            Check(label + " is visible with its weapons", candidate.GetComponentsInChildren<Renderer>().All(r => r.enabled), "");

            if (strikeEarly)
            {
                float start = Now;
                Check(label + " struck early is staggered", candidate.IsStaggered, candidate.stateMachine.currentState.GetType().Name);
                yield return (Func<bool>)(() => candidate.IsStaggered == false);
                Check(label + " takes the long stagger", Mathf.Abs(Now - start - candidate.ambushStaggerTime) < 0.4f, (Now - start).ToString("F2") + " s");
            }
            else
            {
                Check(label + " comes straight for the player", candidate.stateMachine.currentState == candidate.chaseState, candidate.stateMachine.currentState.GetType().Name);
            }

            if (leash != null)
            {
                Bounds bounds = leash.bounds;
                PlaceNearPoint(new Vector3(bounds.center.x, candidate.transform.position.y, bounds.max.z + 8f), candidate);
                yield return (Func<bool>)(() => candidate.stateMachine.currentState == candidate.idleState || candidate.stateMachine.currentState == candidate.moveState);
                Vector3 stopped = candidate.transform.position;
                yield return 2f;
                Check(label + " gives up at the edge of its mud", candidate.PlayerInLeash == false && Vector3.Distance(stopped, candidate.transform.position) < 0.5f, "stays at " + candidate.transform.position.ToString("F1"));
            }

            ClearPlayer();
            yield return 0.2f;
        }

        private static string NavMeshName(AshenFootman candidate)
        {
            return UnityEngine.AI.NavMesh.GetSettingsNameFromID(candidate.agent.agentTypeID);
        }

        // Up-screen of the player, facing the camera, so the shield's face shows in a capture.
        private static void PlaceNear(AshenFootman candidate, float distance)
        {
            Vector3 toCamera = Camera.main != null ? -Camera.main.transform.forward : Vector3.back;
            toCamera.y = 0f;
            PlaceNearPoint(candidate.transform.position + toCamera.normalized * distance, candidate);
        }

        private static void PlaceNearPoint(Vector3 point, AshenFootman candidate)
        {
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = candidate.agent.agentTypeID, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            if (UnityEngine.AI.NavMesh.SamplePosition(point, out UnityEngine.AI.NavMeshHit hit, 6f, filter))
            {
                point = hit.position;
            }

            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            playerRoot.position = point + Vector3.up * 0.1f;
            controller.enabled = true;
        }

        // Up and out of everyone's sight, so the next Footman starts clean.
        private static void ClearPlayer()
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            playerRoot.position += Vector3.up * 60f;
            controller.enabled = true;
        }

        private static void Tick()
        {
            if (EditorApplication.isPlaying == false)
            {
                Finish();
                return;
            }

            worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
            DriveInput();

            if (Now < waitUntil)
            {
                return;
            }

            if (waitFor != null)
            {
                if (waitFor() == false && Now < waitForDeadline)
                {
                    return;
                }

                waitFor = null;
            }

            // Steps the innermost routine; a scenario yields nested routines, and one that ends hands back to its parent.
            while (routines.Count > 0)
            {
                IEnumerator top = routines.Peek();
                bool more;
                try
                {
                    more = top.MoveNext();
                }
                catch (Exception exception)
                {
                    Note("EXCEPTION " + exception);
                    failures++;
                    routines.Clear();
                    break;
                }

                if (more == false)
                {
                    routines.Pop();
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }

                if (top.Current is float seconds)
                {
                    waitUntil = Now + seconds;
                }
                else if (top.Current is Func<bool> condition)
                {
                    waitFor = condition;
                    waitForDeadline = Now + 12f;
                }

                return;
            }

            Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Application.targetFrameRate = -1;

            Note("worst frame " + worstFrame.ToString("F3") + " s");
            Note("console errors: " + Errors.Count);
            foreach (string error in Errors.Distinct().Take(10))
            {
                Note("  " + error);
            }

            string summary = failures == 0 && Errors.Count == 0 ? "PASS" : "FAIL (" + failures + " checks, " + Errors.Count + " errors)";
            Lines.Insert(0, summary);
            File.WriteAllLines(ResultPath, Lines);
        }

        private static IEnumerator Run()
        {
            Setup();
            yield return 0.5f;

            yield return Scenario(OverheadTiming());
            yield return Scenario(Reach(1.45f, true));
            yield return Scenario(Reach(1.7f, true));
            yield return Scenario(Reach(1.9f, false));
            yield return Scenario(Parry());
            yield return Scenario(EarlyBlock());
            yield return Scenario(RollThrough());
            yield return Scenario(PunishFromFront());
            yield return Scenario(LightsOnShield());
            yield return Scenario(HeavyOnShield());
            yield return Scenario(Bash());
            yield return Scenario(HitFromBehind());
            yield return Scenario(AttackOrder());
            yield return Scenario(SweepWhenClose());
            yield return Scenario(Fire());
            yield return Scenario(Riposte());
            yield return Scenario(TwoLightsAndHeavy());
            yield return Scenario(FootSlide(1.4f, "walk clip at its own speed"));
            yield return Scenario(FootSlide(3.87f, "run clip at its own speed"));
            yield return Scenario(FootSlide(1.5f, "patrol"));
            yield return Scenario(FootSlide(2.5f, "chase"));
        }

        private static IEnumerator Scenario(IEnumerator body)
        {
            yield return ResetFight();
            yield return body;
        }

        private static void Setup()
        {
            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            playerHealth = combat.GetComponent<Health>();
            playerRoot = combat.transform;

            ((PlayerControls)typeof(PlayerCombat).GetField("controls", Any).GetValue(combat)).Disable();
            ((PlayerControls)typeof(PlayerMovement).GetField("controls", Any).GetValue(movement)).Disable();

            // Fight the one nearest the player; the rest leave the level for the test. Gibbet Footmen can't be it: they
            // start off the NavMesh, and a leash ends the fight early.
            AshenFootman[] all = UnityEngine.Object.FindObjectsByType<AshenFootman>();
            footman = all.Where(f => typeof(AshenFootman).GetField("ambush", Any).GetValue(f) == null)
                .OrderBy(f => Vector3.Distance(f.transform.position, playerRoot.position)).First();
            foreach (AshenFootman other in all)
            {
                other.gameObject.SetActive(other == footman);
            }

            home = footman.transform.position;
            homeFacing = OpenGround(home);
            homeAttackRange = footman.attackRange;
            homeTurnSpeed = footman.turnSpeed;
            homeChaseSpeed = footman.chaseSpeed;
            homeAggression = footman.aggressionRange;

            playerHealth.OnDamaged += amount =>
            {
                lastPlayerDamage = amount;
                lastPlayerDamageTime = Now;
            };

            Note("fighting " + footman.name + " at " + home + " in " + footman.gameObject.scene.name);
        }

        // The facing with the most room both ahead and behind, since the fight happens along that line.
        private static Quaternion OpenGround(Vector3 from)
        {
            float best = -1f;
            Quaternion facing = Quaternion.identity;
            for (int angle = 0; angle < 360; angle += 15)
            {
                Quaternion candidate = Quaternion.Euler(0f, angle, 0f);
                float ahead = Room(from, candidate * Vector3.forward, 13f);
                float behind = Room(from, candidate * Vector3.back, 4f);
                float room = Mathf.Min(ahead, behind * 3f);
                if (room > best)
                {
                    best = room;
                    facing = candidate;
                }
            }

            Note("open ground " + best.ToString("F1") + " m at yaw " + facing.eulerAngles.y.ToString("F0"));
            return facing;
        }

        private static float Room(Vector3 from, Vector3 direction, float length)
        {
            return UnityEngine.AI.NavMesh.Raycast(from, from + direction * length, out UnityEngine.AI.NavMeshHit hit, UnityEngine.AI.NavMesh.AllAreas) ? hit.distance : length;
        }

        private static IEnumerator ResetFight()
        {
            // Dead is final in the controller, so a body is put back by hand.
            if (footman.IsDead)
            {
                footman.GetComponent<Collider>().enabled = true;
                footman.agent.enabled = true;
                footman.anim.Play("Idle", 0, 0f);
            }

            footman.attackRange = homeAttackRange;
            footman.turnSpeed = homeTurnSpeed;
            footman.chaseSpeed = homeChaseSpeed;
            footman.aggressionRange = homeAggression;
            footman.agent.updateRotation = true;
            footman.health.SetMax(footman.health.Max, true);
            footman.poise.Reset();
            footman.agent.Warp(home);
            footman.stateMachine.ChangeState(footman.idleState);
            footman.transform.rotation = homeFacing;

            // After the state change: leaving an attack lowers the guard.
            typeof(AshenFootman).GetField("guardDownTimer", Any).SetValue(footman, 0f);
            typeof(AshenFootman).GetField("overheadsSinceSweep", Any).SetValue(footman, 0);

            playerHealth.SetMax(100000f, true);
            playerMove = Vector3.zero;
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);

            // A far-off start, so he's idle with nothing to do while the player gets ready.
            Place(12f, 0f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
            {
                Press("OnDrawPressed");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.1f;
        }

        // --- Scenarios ---

        private static IEnumerator OverheadTiming()
        {
            Note("--- overhead timing");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            float start = Now;
            float lastDamageBefore = lastPlayerDamageTime;

            yield return 0.45f;
            Shot("Windup");
            yield return 0.25f;
            Tint();
            Shot("LateWindup");

            yield return (Func<bool>)(() => lastPlayerDamageTime > lastDamageBefore);
            float hit = Now - start;
            Check("overhead lands at 0.80 s (design note: 24 frames at 30 fps)", Mathf.Abs(hit - 0.8f) < 0.08f, hit.ToString("F3") + " s");
            Check("wind-up at least 300 ms (Combat Feel Spec)", hit >= 0.3f, hit.ToString("F3") + " s");
            Check("overhead damage", Mathf.Approximately(lastPlayerDamage, footman.attackDamage), lastPlayerDamage.ToString("F1"));

            float hitTime = Now;
            Place(30f, 0f);
            yield return 0.3f;
            Shot("Recovery");
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.recoveryState);
            Note("swing clip ends " + (Now - hitTime).ToString("F2") + " s after contact");
            yield return (Func<bool>)(() => footman.IsGuarding);
            Note("shield back up " + (Now - hitTime).ToString("F2") + " s after contact (the punish window)");
        }

        private static IEnumerator Reach(float distance, bool hits)
        {
            Note("--- reach at " + distance + " m");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            float before = lastPlayerDamageTime;
            yield return 0.7f;
            Place(distance, 0f);
            yield return 0.35f;
            bool landed = lastPlayerDamageTime > before;
            Check((hits ? "lands" : "misses") + " at " + distance + " m", landed == hits, landed ? "hit" : "missed");
        }

        private static IEnumerator Parry()
        {
            Note("--- parry on the down-beat");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            float parryBefore = combat.LastParryTime;
            float damageBefore = lastPlayerDamageTime;
            yield return 0.66f;
            Press("OnBlockPressed");
            yield return 0.3f;
            Check("parry window catches the overhead", combat.LastParryTime > parryBefore, "last parry " + (Now - combat.LastParryTime).ToString("F2") + " s ago");
            Check("parry takes no damage", lastPlayerDamageTime <= damageBefore, "");
            Check("parry staggers him", footman.IsStaggered, footman.stateMachine.currentState.GetType().Name);
            Shot("Parried");
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
        }

        private static IEnumerator EarlyBlock()
        {
            Note("--- block held from early in the wind-up");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            float before = lastPlayerDamageTime;
            yield return 0.2f;
            Press("OnBlockPressed");
            yield return 0.8f;
            Check("an early block is only a block", lastPlayerDamageTime > before, "took " + lastPlayerDamage.ToString("F1"));
            Check("he isn't staggered by a block", footman.IsStaggered == false, footman.stateMachine.currentState.GetType().Name);
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
        }

        private static IEnumerator RollThrough()
        {
            Note("--- roll through the overhead, then hit twice");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            float attackStart = Now;
            float before = lastPlayerDamageTime;
            yield return 0.56f;

            playerMove = (footman.transform.position - playerRoot.position).normalized;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Dodging);
            playerMove = Vector3.zero;
            Shot("Roll");

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            Check("the roll's i-frames beat the overhead", lastPlayerDamageTime <= before, "");
            Note("player free " + (Now - attackStart).ToString("F2") + " s after the swing began, " + Vector3.Distance(playerRoot.position, footman.transform.position).ToString("F2") + " m from him");

            yield return HitTwice(1.15f);
        }

        private static IEnumerator PunishFromFront()
        {
            Note("--- dodge the overhead by stepping back, then hit twice from the front");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            yield return 0.7f;
            Place(2.2f, 0f);
            yield return 0.2f;
            Place(1.15f, 0f);
            Check("shield is down in the recovery", footman.IsGuarding == false, "");
            yield return HitTwice(1.15f);
        }

        // Face him, then the two-slash combo. The second slash is queued through the same button.
        private static IEnumerator HitTwice(float distance)
        {
            float health = footman.health.Current;
            Vector3 toPlayer = playerRoot.position - footman.transform.position;
            toPlayer.y = 0f;
            PlaceAt(footman.transform.position + toPlayer.normalized * distance);

            // A teleport leaves the player ungrounded for a frame, and a strike in the air is the air attack.
            yield return 0.05f;
            Press("OnStrikePressed");
            yield return 0.4f;
            Press("OnStrikePressed");
            yield return 1.5f;

            float dealt = health - footman.health.Current;
            Check("both hits land", dealt >= 20f + 12f - 0.01f, dealt.ToString("F1") + " of 32 (he's in " + footman.stateMachine.currentState.GetType().Name + ")");
        }

        private static IEnumerator LightsOnShield()
        {
            Note("--- light hits on the raised shield");
            footman.attackRange = 0f;
            Place(1.2f, 0f);
            yield return (Func<bool>)(() => footman.IsGuarding && footman.stateMachine.currentState == footman.chaseState);
            Shot("Guard");
            float health = footman.health.Current;
            float poise = footman.poise.Current;

            yield return 0.05f;
            Press("OnStrikePressed");

            // Waits on the poise, not the state: an editor hitch can step right over a 0.45 s state.
            yield return (Func<bool>)(() => footman.poise.Current < poise - 1f);
            Shot("Blocked");
            Check("shield takes the hit", Mathf.Approximately(footman.health.Current, health), "health " + footman.health.Current);
            Check("a blocked hit costs half its poise", Mathf.Abs(poise - footman.poise.Current - 10f) < 0.5f, (poise - footman.poise.Current).ToString("F1"));
            Check("he recoils and keeps his guard", footman.IsGuarding, footman.stateMachine.currentState.GetType().Name);
            yield return 1.2f;
        }

        private static IEnumerator HeavyOnShield()
        {
            Note("--- heavy on the raised shield");
            footman.attackRange = 0f;
            Place(1.5f, 0f);
            yield return (Func<bool>)(() => footman.IsGuarding && footman.stateMachine.currentState == footman.chaseState);
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => footman.IsStaggered);
            Check("a heavy breaks the guard", footman.IsStaggered, footman.stateMachine.currentState.GetType().Name);
            Check("a guard-breaking heavy deals its damage", footman.health.Current < footman.health.Max, footman.health.Current.ToString("F1"));
            yield return 2f;
        }

        private static IEnumerator Bash()
        {
            Note("--- shield bash breaks the guard");
            footman.attackRange = 0f;
            Place(1f, 0f);
            yield return (Func<bool>)(() => footman.IsGuarding && footman.stateMachine.currentState == footman.chaseState);

            Press("OnBlockPressed");
            yield return 0.3f;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => footman.IsStaggered);
            float broke = Now;
            Check("bash staggers him through the guard", footman.IsStaggered, footman.stateMachine.currentState.GetType().Name);
            Check("guard is down", footman.IsGuarding == false, "");
            Shot("GuardBroken");
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.1f;
            float health = footman.health.Current;
            Press("OnStrikePressed");
            yield return 0.8f;
            Check("hits on the broken guard are critical", Mathf.Abs(health - footman.health.Current - 18f) < 0.5f, (health - footman.health.Current).ToString("F1") + " (12 x 1.5)");

            yield return (Func<bool>)(() => footman.IsGuarding);
            float back = Now - broke;
            Check("shield returns after the break", Mathf.Abs(back - footman.guardDownAfterBreak) < 2.2f, back.ToString("F2") + " s (a landed hit also holds it down)");
        }

        private static IEnumerator HitFromBehind()
        {
            Note("--- hit from behind, past the shield");
            footman.attackRange = 0f;
            footman.turnSpeed = 0f;
            footman.agent.updateRotation = false;
            Place(-1f, 0f);
            yield return (Func<bool>)(() => footman.IsGuarding && footman.stateMachine.currentState == footman.chaseState);

            float health = footman.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => footman.health.Current < health);
            float landed = Now;
            Check("a hit from behind goes past the shield", health - footman.health.Current >= 11.9f, (health - footman.health.Current).ToString("F1"));
            Check("guard drops after a hit lands", footman.IsGuarding == false, "");
            Shot("GuardDown");
            yield return (Func<bool>)(() => footman.IsGuarding);
            float back = Now - landed;
            Check("guard returns after a couple of seconds", Mathf.Abs(back - footman.guardDownAfterHit) < 0.3f, back.ToString("F2") + " s");
        }

        private static IEnumerator AttackOrder()
        {
            Note("--- attack order, player standing in range");
            Place(1.4f, 0f);
            var order = new List<string>();
            float sweepWindup = 0f;

            for (int i = 0; i < 3; i++)
            {
                yield return (Func<bool>)(() => footman.IsAttacking);
                bool sweep = footman.stateMachine.currentState == footman.sweepState;
                order.Add(sweep ? "sweep" : "overhead");

                float start = Now;
                float before = lastPlayerDamageTime;
                if (sweep)
                {
                    yield return 0.4f;
                    Shot("Sweep");
                }

                yield return (Func<bool>)(() => lastPlayerDamageTime > before);
                if (sweep)
                {
                    sweepWindup = Now - start;
                    Check("sweep damage", Mathf.Approximately(lastPlayerDamage, footman.sweepDamage), lastPlayerDamage.ToString("F1"));
                }

                yield return (Func<bool>)(() => footman.IsAttacking == false);
            }

            Check("two overheads, then a sweep", string.Join(",", order) == "overhead,overhead,sweep", string.Join(",", order));
            Check("sweep lands at 0.67 s", Mathf.Abs(sweepWindup - footman.sweepWindup) < 0.08f, sweepWindup.ToString("F3") + " s");
            Check("sweep wind-up at least 300 ms", sweepWindup >= 0.3f, sweepWindup.ToString("F3") + " s");
        }

        private static IEnumerator SweepWhenClose()
        {
            Note("--- stepping inside the overhead draws the sweep");
            Place(1.4f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            yield return (Func<bool>)(() => footman.IsAttacking == false);
            Place(1f, 0f);
            yield return (Func<bool>)(() => footman.IsAttacking);
            Check("he sweeps a player inside his range", footman.stateMachine.currentState == footman.sweepState, footman.stateMachine.currentState.GetType().Name);
            yield return (Func<bool>)(() => footman.IsAttacking == false);
        }

        private static IEnumerator Fire()
        {
            Note("--- fire goes round the shield");
            footman.attackRange = 0f;
            Place(3f, 0f);
            yield return (Func<bool>)(() => footman.IsGuarding && footman.stateMachine.currentState == footman.chaseState);
            float health = footman.health.Current;
            footman.TakeHit(new DamageInfo { amount = 5f, direction = -footman.transform.forward, point = footman.transform.position, isEnvironmental = true });
            Check("fire burns him through the guard", Mathf.Approximately(health - footman.health.Current, 5f), (health - footman.health.Current).ToString("F1"));
            Check("fire causes no reaction", footman.stateMachine.currentState == footman.chaseState, footman.stateMachine.currentState.GetType().Name);
            yield return 0.1f;
        }

        private static IEnumerator Riposte()
        {
            Note("--- design note: 1 riposte (parry, then a heavy on the stagger)");
            Place(1.3f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.attackState);
            yield return 0.66f;
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => footman.IsStaggered);
            typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);

            // A human reaction after the parry lands, not a frame-perfect press.
            yield return 0.3f;
            float parried = Now;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => footman.health.Current < footman.health.Max);
            Note("heavy landed " + (Now - parried + 0.3f).ToString("F2") + " s after the parry, stagger lasts " + footman.staggerTime.ToString("F1") + " s");
            yield return 0.3f;
            Check("a riposte kills him", footman.IsDead, "health " + footman.health.Current.ToString("F1"));
            Shot("Dead");
            yield return 2.4f;
        }

        private static IEnumerator TwoLightsAndHeavy()
        {
            Note("--- design note: 2 light hits + 1 heavy, from behind so the shield stays out of it");
            footman.attackRange = 0f;
            footman.turnSpeed = 0f;
            footman.agent.updateRotation = false;
            footman.poise.maxPoise = 1000f;
            footman.poise.Reset();

            Place(-1f, 0f);
            yield return 0.8f;
            Press("OnStrikePressed");
            yield return 1.6f;
            float afterFirst = footman.health.Current;

            Place(-1f, 0f);
            Press("OnStrikePressed");
            yield return 1.6f;
            float afterSecond = footman.health.Current;
            Check("still standing after two lights", footman.IsDead == false, afterFirst.ToString("F1") + " then " + afterSecond.ToString("F1"));

            Place(-1.3f, 0f);
            Press("OnHeavyPressed");
            yield return 2.2f;
            Check("the heavy finishes him", footman.IsDead, "health " + footman.health.Current.ToString("F1"));
            footman.poise.maxPoise = 30f;
            yield return 2.4f;
        }

        // What foot sliding is: a planted foot has to travel backward along the body exactly as fast as the body goes
        // forward. So this measures the feet's backward speed mid-stance, in the body's space, against the agent's speed.
        // World heights don't work for this: L1's ground isn't flat. He runs at the player from further than the fight
        // distance so he is at full speed for the whole sample.
        private static IEnumerator FootSlide(float speed, string name)
        {
            Note("--- feet at " + speed + " m/s (" + name + ")");
            footman.attackRange = 0f;
            footman.chaseSpeed = speed;
            footman.aggressionRange = 40f;
            Place(11.5f, 0f);
            yield return (Func<bool>)(() => footman.stateMachine.currentState == footman.chaseState);
            yield return 0.6f;

            var animator = footman.GetComponent<Animator>();
            Transform body = footman.transform;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            var left = new List<(float time, float along)>();
            var right = new List<(float time, float along)>();
            float until = Now + 2.2f;
            float agentSpeed = 0f;
            int frames = 0;

            while (Now < until)
            {
                yield return 0f;
                left.Add((Time.time, Vector3.Dot(leftFoot.position - body.position, body.forward)));
                right.Add((Time.time, Vector3.Dot(rightFoot.position - body.position, body.forward)));
                agentSpeed += footman.agent.velocity.magnitude;
                frames++;
            }

            agentSpeed /= Mathf.Max(1, frames);
            Shot("Run" + name);

            float stance = (StanceSpeed(left) + StanceSpeed(right)) * 0.5f;
            Check("agent reaches " + speed + " m/s", Mathf.Abs(agentSpeed - speed) < 0.2f, agentSpeed.ToString("F2"));
            Check("feet keep pace with the ground", Mathf.Abs(stance - agentSpeed) < speed * 0.15f, "stance " + stance.ToString("F2") + " m/s against " + agentSpeed.ToString("F2") + " m/s of travel (" + (Mathf.Abs(stance - agentSpeed) / agentSpeed * 100f).ToString("F0") + "% slip)");
        }

        // Average backward speed of a foot through the middle of its stance: moving backward, in the central half of its
        // stride. Windows of a few frames even out the animator's frame-to-frame steps.
        private static float StanceSpeed(List<(float time, float along)> track)
        {
            float front = track.Max(s => s.along);
            float back = track.Min(s => s.along);
            float middle = (front + back) * 0.5f;
            float half = (front - back) * 0.25f;

            var speeds = new List<float>();
            for (int i = 3; i < track.Count - 3; i++)
            {
                float dt = track[i + 3].time - track[i - 3].time;
                float velocity = dt > 0.0001f ? (track[i + 3].along - track[i - 3].along) / dt : 0f;
                if (velocity < 0f && Mathf.Abs(track[i].along - middle) < half)
                {
                    speeds.Add(-velocity);
                }
            }

            return speeds.Count > 0 ? speeds.Average() : 0f;
        }

        // --- Player driving ---

        private static void DriveInput()
        {
            if (footman == null || Camera.main == null)
            {
                return;
            }

            // At his feet: the aim ray stops at the ground, so aiming at his chest lands past him and turns the player away.
            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(footman.transform.position));

            Quaternion yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * playerMove;
            typeof(PlayerMovement).GetField("moveInput", Any).SetValue(movement, new Vector2(local.x, local.z));
        }

        private static void Press(string handler)
        {
            typeof(PlayerCombat).GetMethod(handler, Any).Invoke(combat, null);
        }

        // `distance` metres along his facing, `side` metres to his right; he and the player end up facing each other,
        // or back to back when the distance is negative.
        private static void Place(float distance, float side)
        {
            Transform body = footman.transform;
            PlaceAt(body.position + body.forward * distance + body.right * side);
        }

        private static void PlaceAt(Vector3 position)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = footman.transform.position.y;
            playerRoot.position = position;

            Vector3 toFootman = footman.transform.position - position;
            toFootman.y = 0f;
            if (toFootman.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toFootman);
            }

            controller.enabled = true;
        }

        // --- Reporting ---

        // Reads the tint off every renderer at the end of the wind-up, weapons included. He has no colour telegraph.
        private static void Tint()
        {
            var block = new MaterialPropertyBlock();
            int id = Shader.PropertyToID("_BaseColor");
            foreach (Renderer renderer in footman.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                Color colour = block.GetColor(id);
                Check("no colour change on " + renderer.name, Mathf.Abs(colour.r - colour.g) < 0.05f, colour.ToString("F2") + ", wind-up " + footman.windup.ToString("F2"));
            }
        }

        private static void Shot(string name)
        {
            if (TakeShots)
            {
                ScreenCapture.CaptureScreenshot("Temp/Footman_" + name + ".png");
            }
        }

        private static void Check(string what, bool passed, string detail)
        {
            Note((passed ? "ok   " : "FAIL ") + what + (detail.Length > 0 ? ": " + detail : ""));
            if (passed == false)
            {
                failures++;
            }
        }

        private static void Note(string line)
        {
            Lines.Add(line);
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(message);
            }
        }
    }
}
