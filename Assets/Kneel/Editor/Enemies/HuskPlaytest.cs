using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // A scripted fight with the Iron Husk (Kneel/Enemies/Iron Husk/Playtest, in Play mode in L4_BurningGate). He's spawned
    // from his prefab onto E1's courtyard, the player's input goes through the real handlers and he runs his own AI, so it
    // measures the fight: the chase, the wind-up's backstep, the charge (standing in it, stepping out of its line, rolling
    // through it, blocking it), the armour (lights glance off, three heavies break it), his body's health, a charge into
    // a wall and one to the breach's lip at E4. Results go to Temp/HuskPlaytest.txt. Nothing in the scene is saved.
    public static class HuskPlaytest
    {
        public const string ResultPath = "Temp/HuskPlaytest.txt";
        private const string PrefabPath = "Assets/Kneel/Enemies/IronHusk/IronHusk.prefab";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // E1's courtyard (the inner gate's yard, where the first Husk stands) and a spot back from E4's breach lip
        // (x 93, z 142-154), charging out toward it.
        private static readonly Vector3 Courtyard = new Vector3(62f, 0f, 76f);
        private static readonly Vector3 BackFromTheLip = new Vector3(84f, 5f, 147f);

        // Runs only the scenarios of this name, when set.
        public static string Only;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static IronHusk husk;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static float homeAttackRange;
        private static float homeAggression;
        private static float lastPlayerDamage;
        private static int playerHits;
        private static float worstFrame;
        private static int failures;
        private static Vector3 playerMove;
        private static Action<float> damageListener;

        private static float Now => Time.unscaledTime;

        private static EnemyState HuskState => husk.stateMachine.currentState;

        [MenuItem("Kneel/Enemies/Iron Husk/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in L4_BurningGate first.");
                return;
            }

            husk = null;
            combat = null;
            playerMove = Vector3.zero;
            lastPlayerDamage = 0f;
            playerHits = 0;

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
                    waitForDeadline = Now + 10f;
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

            if (playerHealth != null && damageListener != null)
            {
                playerHealth.OnDamaged -= damageListener;
            }

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
            if (Setup() == false)
            {
                yield break;
            }

            yield return 0.5f;

            // The first hit of a session stalls, so it's spent here, away from anything timed.
            yield return ResetFight(Courtyard);
            husk.attackRange = 0f;
            Place(1.8f);
            yield return 0.05f;
            Press("OnStrikePressed");
            yield return 1.5f;

            var scenarios = new (string name, Func<IEnumerator> body)[]
            {
                ("charge", StandInTheCharge),
                ("charge", StepOutOfTheLine),
                ("charge", RollThrough),
                ("charge", BlockTheCharge),
                ("armour", LightsGlanceOff),
                ("armour", ThreeHeaviesBreakIt),
                ("health", ThreeLights),
                ("health", TwoHeavies),
                ("wall", IntoAWall),
                ("lip", ToTheLip),
            };

            foreach (var scenario in scenarios)
            {
                if (string.IsNullOrEmpty(Only) || Only.Split(',').Contains(scenario.name))
                {
                    yield return ResetFight(Courtyard);
                    yield return scenario.body();
                }
            }
        }

        private static bool Setup()
        {
            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            playerHealth = combat.GetComponent<Health>();
            playerRoot = combat.transform;

            ((PlayerControls)typeof(PlayerCombat).GetField("controls", Any).GetValue(combat)).Disable();
            ((PlayerControls)typeof(PlayerMovement).GetField("controls", Any).GetValue(movement)).Disable();

            foreach (Enemy other in UnityEngine.Object.FindObjectsByType<Enemy>())
            {
                other.gameObject.SetActive(false);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (OnNavMesh(Courtyard, prefab.GetComponent<NavMeshAgent>().agentTypeID, out Vector3 start) == false)
            {
                Note("FAIL no Husk NavMesh at E1's courtyard: run this in L4_BurningGate");
                failures++;
                return false;
            }

            husk = UnityEngine.Object.Instantiate(prefab, start, Quaternion.identity).GetComponent<IronHusk>();
            husk.name = "IronHusk (playtest)";

            // A backgrounded Editor can stop rendering, which would cull his animator.
            husk.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            homeAttackRange = husk.attackRange;
            homeAggression = husk.aggressionRange;

            damageListener = amount =>
            {
                lastPlayerDamage = amount;
                playerHits++;
            };
            playerHealth.OnDamaged += damageListener;

            Note("fighting " + husk.name + " in " + husk.gameObject.scene.name + ", agent type " + NavMesh.GetSettingsNameFromID(husk.agent.agentTypeID));
            return true;
        }

        // Puts him back at `at`, whole: armour on, full health, out of sight of a freshly armed player 7 m in front of him.
        private static IEnumerator ResetFight(Vector3 at)
        {
            if (husk.IsDead)
            {
                foreach (Collider body in husk.GetComponents<Collider>())
                {
                    body.enabled = true;
                }

                husk.agent.enabled = true;
                husk.anim.Play("Idle", 0, 0f);
            }

            OnNavMesh(at, husk.agent.agentTypeID, out Vector3 start);
            typeof(IronHusk).GetField("armourLeft", Any).SetValue(husk, husk.armourHits);
            husk.attackRange = homeAttackRange;
            husk.aggressionRange = 0f;
            husk.health.SetMax(husk.health.Max, true);
            husk.agent.Warp(start);
            husk.stateMachine.ChangeState(husk.idleState);
            husk.transform.rotation = Quaternion.identity;

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            playerMove = Vector3.zero;
            SetBlock(false);
            Place(7f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
            {
                Press("OnDrawPressed");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.3f;
            husk.aggressionRange = homeAggression;
        }

        // --- The charge ---

        // Measures the tell (its length and how far he backs off) and the run, from 7 m with the player standing still.
        private static IEnumerator StandInTheCharge()
        {
            Note("--- standing in the charge");
            int before = playerHits;
            yield return (Func<bool>)(() => HuskState == husk.windupState);
            float windupStart = Time.time;
            Vector3 windupFrom = Flat(husk.transform.position);
            Vector3 playerAt = Flat(playerRoot.position);
            Vector3 toPlayer = (playerAt - windupFrom).normalized;

            yield return (Func<bool>)(() => HuskState != husk.windupState);
            float windup = Time.time - windupStart;
            float backstep = Vector3.Dot(windupFrom - Flat(husk.transform.position), toPlayer);
            Check("the wind-up lasts 24 frames", Mathf.Abs(windup - husk.windupTime) < 0.05f, windup.ToString("F2") + " s");
            Check("he backs off a few steps", Mathf.Abs(backstep - husk.backstepDistance) < 0.3f, backstep.ToString("F2") + " m");
            Check("then charges", HuskState == husk.chargeState, HuskState.GetType().Name);

            float chargeStart = Time.time;
            Vector3 chargeFrom = Flat(husk.transform.position);
            yield return (Func<bool>)(() => playerHits > before);
            float reach = Time.time - chargeStart;
            Check("the shoulder lands on a player who stays put", playerHits == before + 1, (playerHits - before) + " hits, " + lastPlayerDamage.ToString("F0") + " damage, " + reach.ToString("F2") + " s into the run");

            yield return (Func<bool>)(() => HuskState != husk.chargeState);
            Check("one charge lands once", playerHits == before + 1, (playerHits - before) + " hits");
            float run = Vector3.Distance(chargeFrom, Flat(husk.transform.position));
            Note("  run " + run.ToString("F2") + " m in " + (Time.time - chargeStart).ToString("F2") + " s, ends in " + HuskState.GetType().Name);
            Check("a clear run ends in his recovery", HuskState == husk.recoveryState, HuskState.GetType().Name);
        }

        // He doesn't steer once the run starts: sprinting out of the line as he commits beats it, and he overshoots.
        // (A walk from the commit is too slow at 7 m; the counter the note names is the roll.)
        private static IEnumerator StepOutOfTheLine()
        {
            Note("--- sprinting out of the line");
            int before = playerHits;
            yield return (Func<bool>)(() => HuskState == husk.chargeState);
            Vector3 target = Flat(husk.ChargeTarget);
            Vector3 from = Flat(husk.transform.position);
            playerMove = husk.transform.right;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, true);
            yield return (Func<bool>)(() => HuskState != husk.chargeState);
            playerMove = Vector3.zero;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);

            float past = Vector3.Dot(Flat(husk.transform.position) - target, (target - from).normalized);
            Check("sprinting out of the line beats it", playerHits == before, (playerHits - before) + " hits");
            Check("he runs on past where the player stood", past > husk.chargeOvershoot - 0.5f, past.ToString("F2") + " m past");
        }

        // Rolling into him as he comes: the roll's invulnerability takes the player through the shoulder.
        private static IEnumerator RollThrough()
        {
            Note("--- rolling through the charge");
            int before = playerHits;
            yield return (Func<bool>)(() => HuskState == husk.chargeState);
            yield return (Func<bool>)(() => Vector3.Distance(Flat(husk.transform.position), Flat(playerRoot.position)) < 3.2f || HuskState != husk.chargeState);

            // The roll takes PlayerMovement's direction from the frame before, so the stick goes down a frame ahead.
            playerMove = Flat(husk.transform.position - playerRoot.position).normalized;
            DriveInput();
            yield return 0.02f;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return (Func<bool>)(() => HuskState != husk.chargeState);
            playerMove = Vector3.zero;
            Check("a roll through the charge takes no hit", playerHits == before, (playerHits - before) + " hits");
        }

        // The charge can be blocked, never parried: a press timed to the impact only blocks it.
        private static IEnumerator BlockTheCharge()
        {
            Note("--- blocking the charge");
            int before = playerHits;
            float parries = combat.LastParryTime;
            yield return (Func<bool>)(() => HuskState == husk.chargeState);
            yield return (Func<bool>)(() => Vector3.Distance(Flat(husk.transform.position), Flat(playerRoot.position)) < 2.2f || HuskState != husk.chargeState);
            SetBlock(true);
            yield return (Func<bool>)(() => playerHits > before || HuskState != husk.chargeState);
            float taken = playerHits > before ? lastPlayerDamage : 0f;
            yield return 0.1f;
            Check("a block takes a fraction of it", playerHits == before + 1 && taken < husk.attackDamage * 0.5f, taken.ToString("F1") + " damage, " + combat.State);
            Check("a press timed to it doesn't parry", combat.LastParryTime == parries, "");
            SetBlock(false);
        }

        // --- The armour ---

        private static IEnumerator LightsGlanceOff()
        {
            Note("--- lights on the armour");
            husk.attackRange = 0f;
            for (int light = 1; light <= 3; light++)
            {
                Place(1.8f);
                yield return 0.05f;
                Press("OnStrikePressed");
                yield return 0.7f;
            }

            Check("lights do no damage", husk.health.Current == husk.health.Max, husk.health.Current.ToString("F0") + " / " + husk.health.Max.ToString("F0"));
            Check("and leave the armour whole", husk.ArmourLeft == husk.armourHits, husk.ArmourLeft + " left");
            Check("and don't stop him", HuskState != husk.hitState && HuskState != husk.staggerState, HuskState.GetType().Name);
        }

        private static IEnumerator ThreeHeaviesBreakIt()
        {
            Note("--- heavies on the armour");
            husk.attackRange = 0f;
            for (int heavy = 1; heavy <= 3; heavy++)
            {
                int armour = husk.ArmourLeft;
                Place(1.8f);
                yield return 0.05f;
                Press("OnHeavyPressed");
                yield return (Func<bool>)(() => husk.ArmourLeft < armour);
                Note("  heavy " + heavy + ": armour " + husk.ArmourLeft + " left, health " + husk.health.Current.ToString("F0") + ", " + HuskState.GetType().Name);
                if (heavy < 3)
                {
                    Check("a heavy on the armour cracks it without hurting him", husk.health.Current == husk.health.Max && husk.IsArmoured, "");
                }

                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
                yield return 0.1f;
            }

            Check("the third breaks it", husk.IsArmoured == false, husk.ArmourLeft + " left");
            Check("and leaves him reeling", HuskState == husk.staggerState, HuskState.GetType().Name);
        }

        // --- His body ---

        private static IEnumerator ThreeLights()
        {
            Note("--- three lights, armour off");
            husk.attackRange = 0f;
            typeof(IronHusk).GetField("armourLeft", Any).SetValue(husk, 0);
            int hits = 0;
            for (int light = 1; light <= 3 && husk.IsDead == false; light++)
            {
                float health = husk.health.Current;
                Place(1.8f);
                yield return 0.05f;
                Press("OnStrikePressed");
                yield return (Func<bool>)(() => husk.health.Current < health);
                hits++;
                Note("  light " + light + ": " + (health - husk.health.Current).ToString("F0") + " damage, " + husk.health.Current.ToString("F0") + " left");
                if (light == 2)
                {
                    Check("two lights don't kill him", husk.IsDead == false, "");
                }

                yield return 0.7f;
            }

            Check("three lights do", husk.IsDead, hits + " lights");
        }

        private static IEnumerator TwoHeavies()
        {
            Note("--- two heavies, armour off");
            husk.attackRange = 0f;
            typeof(IronHusk).GetField("armourLeft", Any).SetValue(husk, 0);
            Place(1.8f);
            yield return 0.05f;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => husk.health.Current < husk.health.Max);
            Check("one heavy doesn't kill him", husk.IsDead == false, husk.health.Current.ToString("F0") + " left");
            Check("but staggers him", HuskState == husk.staggerState, HuskState.GetType().Name);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            Place(1.8f);
            yield return 0.05f;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => husk.IsDead);
            Check("the second does", husk.IsDead, husk.health.Current.ToString("F0") + " left");
        }

        // --- Where a charge ends early ---

        // The player stands just inside the NavMesh's edge, so the overshoot runs him into it.
        private static IEnumerator IntoAWall()
        {
            Note("--- a charge into a wall");
            husk.aggressionRange = 0f;
            var filter = new NavMeshQueryFilter { agentTypeID = husk.agent.agentTypeID, areaMask = NavMesh.AllAreas };
            if (NavMesh.FindClosestEdge(husk.transform.position, out NavMeshHit edge, filter) == false)
            {
                Check("found a wall", false, "");
                yield break;
            }

            Vector3 inward = Flat(edge.normal).normalized;
            OnNavMesh(edge.position + inward * 7f, husk.agent.agentTypeID, out Vector3 start);
            husk.agent.Warp(start);
            husk.transform.rotation = Quaternion.LookRotation(-inward);
            PlaceAt(edge.position + inward * 1.2f);
            Note("  wall at " + edge.position.ToString("F1") + ", " + edge.distance.ToString("F1") + " m from the courtyard spot");
            husk.aggressionRange = homeAggression;

            yield return (Func<bool>)(() => HuskState == husk.chargeState);
            playerMove = Vector3.Cross(Vector3.up, inward);
            yield return (Func<bool>)(() => HuskState != husk.chargeState);
            playerMove = Vector3.zero;
            float dazed = Time.time;
            Check("running into the wall dazes him", HuskState == husk.staggerState, HuskState.GetType().Name);
            yield return (Func<bool>)(() => HuskState != husk.staggerState);
            Check("for crashStunTime", Mathf.Abs(Time.time - dazed - husk.crashStunTime) < 0.1f, (Time.time - dazed).ToString("F2") + " s");
        }

        // E4's breach: the charge stops at the lip and leaves him teetering (L4 decision 18), never over it.
        private static IEnumerator ToTheLip()
        {
            Note("--- a charge to the breach's lip");
            if (OnNavMesh(BackFromTheLip, husk.agent.agentTypeID, out Vector3 start) == false)
            {
                Check("found E4's terrace", false, "");
                yield break;
            }

            husk.aggressionRange = 0f;
            husk.agent.Warp(start);
            husk.transform.rotation = Quaternion.LookRotation(Vector3.right);
            // Close enough to the lip that the overshoot carries him to it.
            PlaceAt(start + Vector3.right * 7.5f);
            husk.aggressionRange = homeAggression;

            yield return (Func<bool>)(() => HuskState == husk.chargeState);
            playerMove = Vector3.forward;
            yield return (Func<bool>)(() => HuskState != husk.chargeState);
            playerMove = Vector3.zero;
            Check("the lip leaves him teetering", HuskState == husk.teeterState, HuskState.GetType().Name + " at x " + husk.transform.position.x.ToString("F2"));
            Check("on the near side of it", husk.transform.position.y > start.y - 0.5f && husk.IsDead == false, husk.transform.position.ToString("F2"));
            Check("and open to crits", husk.IsStaggered, "");
            yield return (Func<bool>)(() => HuskState != husk.teeterState);
        }

        // --- Helpers ---

        private static bool OnNavMesh(Vector3 near, int agentType, out Vector3 point)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            bool found = NavMesh.SamplePosition(near, out NavMeshHit hit, 6f, filter);
            point = found ? hit.position : near;
            return found;
        }

        private static void DriveInput()
        {
            if (husk == null || Camera.main == null)
            {
                return;
            }

            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(husk.transform.position));

            Quaternion yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * playerMove;
            typeof(PlayerMovement).GetField("moveInput", Any).SetValue(movement, new Vector2(local.x, local.z));
        }

        private static void Press(string handler)
        {
            typeof(PlayerCombat).GetMethod(handler, Any).Invoke(combat, null);
        }

        private static void SetBlock(bool held)
        {
            if (held)
            {
                Press("OnBlockPressed");
            }
            else
            {
                typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
            }
        }

        private static Vector3 Flat(Vector3 point)
        {
            point.y = 0f;
            return point;
        }

        // `distance` metres along his facing; he and the player end up facing each other.
        private static void Place(float distance)
        {
            PlaceAt(husk.transform.position + husk.transform.forward * distance);
        }

        private static void PlaceAt(Vector3 position)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = husk.transform.position.y;
            playerRoot.position = position;

            Vector3 toHusk = Flat(husk.transform.position - position);
            if (toHusk.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toHusk);
            }

            controller.enabled = true;
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
                if (Errors.Contains(message) == false)
                {
                    Note("console: " + message + " | husk " + (husk != null ? husk.transform.position + " " + HuskState?.GetType().Name : "?") + " | " + stack);
                }

                Errors.Add(message);
            }
        }
    }
}
