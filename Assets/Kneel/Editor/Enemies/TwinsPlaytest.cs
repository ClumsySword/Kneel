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
    // A scripted fight with the Cursed Twins (Kneel/Enemies/Cursed Twins/Playtest, in Play mode in L4_BurningGate). The pair
    // is spawned from its prefab onto E2's yard, the player's input goes through the real handlers and they run their own
    // AI, so it measures the fight: waking together, the shared beat (both swings on one frame, from two sides), the tell,
    // a parry and a roll against the pair, one swinging alone when the other can't reach, a twin's health, and the
    // survivor's rage. Results go to Temp/TwinsPlaytest.txt. Nothing in the scene is saved.
    public static class TwinsPlaytest
    {
        public const string ResultPath = "Temp/TwinsPlaytest.txt";
        private const string PrefabPath = "Assets/Kneel/Enemies/CursedTwins/CursedTwins.prefab";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // E2's yard, between the two kneeling Twins of the level plan.
        private static readonly Vector3 Yard = new Vector3(62f, 0f, 96f);

        // Runs only the scenarios of this name, when set.
        public static string Only;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static CursedTwins a, b;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static Vector3 home;
        private static float homeAggression;
        private static float homeAttackRange;
        private static float lastPlayerDamage;
        private static float lastPlayerDamageTime;
        private static List<float> playerDamage = new List<float>();
        private static int playerHits;
        private static float worstFrame;
        private static int failures;
        private static Vector3 playerMove;
        private static Action<float> damageListener;

        private static float Now => Time.unscaledTime;

        [MenuItem("Kneel/Enemies/Cursed Twins/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in L4_BurningGate first.");
                return;
            }

            a = b = null;
            combat = null;
            playerMove = Vector3.zero;
            lastPlayerDamage = 0f;
            lastPlayerDamageTime = -1f;
            playerHits = 0;
            playerDamage.Clear();

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
            yield return ResetFight();
            Hold(a);
            Hold(b);
            Place(a, 1.6f);
            yield return 0.05f;
            Press("OnStrikePressed");
            yield return 1.5f;

            var scenarios = new (string name, Func<IEnumerator> body)[]
            {
                ("beat", TheBeat),
                ("parry", ParryThePair),
                ("roll", RollThePair),
                ("alone", AloneWhenThePartnerCantReach),
                ("health", FiveLights),
                ("health", TwoRipostes),
                ("health", Heavies),
                ("rage", TheSurvivorsRage),
            };

            foreach (var scenario in scenarios)
            {
                if (string.IsNullOrEmpty(Only) || Only.Split(',').Contains(scenario.name))
                {
                    yield return ResetFight();
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
            int agentType = prefab.GetComponentInChildren<NavMeshAgent>().agentTypeID;
            if (OnNavMesh(Yard, agentType, out home) == false)
            {
                Note("FAIL no " + NavMesh.GetSettingsNameFromID(agentType) + " NavMesh at E2's yard: run this in L4_BurningGate");
                failures++;
                return false;
            }

            GameObject pair = UnityEngine.Object.Instantiate(prefab, home, Quaternion.identity);
            pair.name = "CursedTwins (playtest)";
            CursedTwins[] twins = pair.GetComponentsInChildren<CursedTwins>();
            a = twins[0];
            b = twins[1];

            foreach (CursedTwins twin in twins)
            {
                // A backgrounded Editor can stop rendering, which would cull the animator and its events.
                twin.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            homeAggression = a.aggressionRange;
            homeAttackRange = a.attackRange;

            damageListener = amount =>
            {
                lastPlayerDamage = amount;
                lastPlayerDamageTime = Now;
                playerDamage.Add(amount);
                playerHits++;
            };
            playerHealth.OnDamaged += damageListener;

            Note("fighting " + a.name + " and " + b.name + " in " + a.gameObject.scene.name + ", agent type " + NavMesh.GetSettingsNameFromID(agentType));
            return true;
        }

        // Puts the pair back side by side 2 m apart, whole and calm, facing a freshly armed player 8 m away.
        private static IEnumerator ResetFight()
        {
            foreach (CursedTwins twin in new[] { a, b })
            {
                twin.gameObject.SetActive(true);
                if (twin.IsDead)
                {
                    foreach (Collider body in twin.GetComponents<Collider>())
                    {
                        body.enabled = true;
                    }

                    twin.agent.enabled = true;
                    twin.anim.Play("Idle", 0, 0f);
                }

                typeof(CursedTwins).GetField("enraged", Any).SetValue(twin, false);
                twin.telegraph = 0f;
                twin.aggressionRange = 0f;
                twin.attackRange = homeAttackRange;
                twin.health.SetMax(twin.health.Max, true);
                twin.poise.Reset();
                twin.ResetReady();
                twin.stateMachine.ChangeState(twin.idleState);
            }

            OnNavMesh(home + Vector3.left, a.agent.agentTypeID, out Vector3 left);
            OnNavMesh(home + Vector3.right, a.agent.agentTypeID, out Vector3 right);
            a.agent.Warp(left);
            b.agent.Warp(right);
            a.transform.rotation = b.transform.rotation = Quaternion.identity;

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            playerMove = Vector3.zero;
            SetBlock(false);
            PlaceAt(home + Vector3.forward * 8f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
            {
                Press("OnDrawPressed");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.3f;
            a.aggressionRange = b.aggressionRange = homeAggression;
        }

        // --- The pair ---

        // Seeing the player, they wake together, close from either side and swing on one frame; each blow lands the tell
        // after it starts.
        private static IEnumerator TheBeat()
        {
            Note("--- the beat");
            b.aggressionRange = 0f;
            yield return (Func<bool>)(() => Is(a, a.noticeState));
            yield return 0.05f;
            Check("one seeing the player wakes the other", Is(b, b.noticeState) || Is(b, b.chaseState), State(b));
            b.aggressionRange = homeAggression;

            int before = playerHits;
            yield return (Func<bool>)(() => Is(a, a.attackState) || Is(b, b.attackState));
            float gap = Mathf.Abs(a.attackState.StartedAt - b.attackState.StartedAt);
            Check("they swing on the same frame", Is(a, a.attackState) && Is(b, b.attackState) && gap < 0.001f, State(a) + " / " + State(b) + ", " + gap.ToString("F3") + " s apart");

            Vector3 toA = Flat(a.transform.position - playerRoot.position);
            Vector3 toB = Flat(b.transform.position - playerRoot.position);
            float spread = Vector3.Angle(toA, toB);
            Check("from either side of the player", spread > 60f, spread.ToString("F0") + "° apart");

            float started = Now;
            yield return (Func<bool>)(() => playerHits >= before + 2);
            float tell = lastPlayerDamageTime - started;
            Check("both blows land", playerHits == before + 2, (playerHits - before) + " hits: " + string.Join(", ", playerDamage.Skip(before).Select(d => d.ToString("F0"))));
            Check("16 frames after the swing starts", Mathf.Abs(tell - a.windupTime) < 0.06f, tell.ToString("F2") + " s");
        }

        // The blows arrive together, so one press in the parry window turns both.
        private static IEnumerator ParryThePair()
        {
            Note("--- parrying the pair");
            int before = playerHits;
            yield return (Func<bool>)(() => Is(a, a.attackState) && Is(b, b.attackState));
            yield return a.windupTime - 0.1f;
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => (Is(a, a.staggerState) && Is(b, b.staggerState)) || playerHits > before);
            Check("one parry staggers both", Is(a, a.staggerState) && Is(b, b.staggerState), State(a) + " / " + State(b) + ", " + (playerHits - before) + " hits taken");
            SetBlock(false);
        }

        // A roll as they commit takes the player out of both blows.
        private static IEnumerator RollThePair()
        {
            Note("--- rolling the pair");
            int before = playerHits;
            yield return (Func<bool>)(() => Is(a, a.attackState) && Is(b, b.attackState));
            yield return a.windupTime - a.commitTime - 0.15f;

            // Back out between them. The roll takes PlayerMovement's direction from the frame before.
            playerMove = Flat(playerRoot.position - (a.transform.position + b.transform.position) * 0.5f).normalized;
            DriveInput();
            yield return 0.02f;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return (Func<bool>)(() => Is(a, a.recoveryState) && Is(b, b.recoveryState));
            playerMove = Vector3.zero;
            Check("a roll clears both blows", playerHits == before, (playerHits - before) + " hits");
        }

        // A twin that can swing doesn't wait forever on one that can't reach: after syncWait he goes alone.
        private static IEnumerator AloneWhenThePartnerCantReach()
        {
            Note("--- alone when the partner can't reach");
            Hold(b);
            yield return (Func<bool>)(() => Is(a, a.chaseState) && a.ReadyToSwing);
            float ready = Now;
            yield return (Func<bool>)(() => Is(a, a.attackState));
            float waited = Now - ready;
            Check("he waits for his partner, then swings alone", Mathf.Abs(waited - a.syncWait) < 0.1f && Is(b, b.attackState) == false, waited.ToString("F2") + " s");
        }

        // --- A twin's health ---

        private static IEnumerator FiveLights()
        {
            Note("--- five lights");
            Hold(a);
            Hold(b);
            int lights = 0;
            for (int light = 1; light <= 5 && a.IsDead == false; light++)
            {
                float health = a.health.Current;
                yield return WaitFree(a);
                Place(a, 1.6f);
                yield return 0.05f;
                Press("OnStrikePressed");
                yield return (Func<bool>)(() => a.health.Current < health);
                lights++;
                Note("  light " + light + ": " + (health - a.health.Current).ToString("F0") + " damage, " + a.health.Current.ToString("F0") + " left, " + State(a));
                if (light == 4)
                {
                    Check("four lights don't kill him", a.IsDead == false, "");
                }

                yield return 0.7f;
            }

            Check("five lights do", a.IsDead, lights + " lights");
        }

        // A parry, then a heavy into the opening: twice.
        private static IEnumerator TwoRipostes()
        {
            Note("--- two ripostes");
            Hold(b);
            for (int riposte = 1; riposte <= 2 && a.IsDead == false; riposte++)
            {
                Place(a, 2f);
                yield return (Func<bool>)(() => Is(a, a.attackState));
                yield return a.windupTime - 0.1f;
                Press("OnBlockPressed");
                yield return (Func<bool>)(() => Is(a, a.staggerState));
                SetBlock(false);
                yield return 0.1f;
                float health = a.health.Current;
                Place(a, 1.6f);
                yield return 0.05f;
                Press("OnHeavyPressed");
                yield return (Func<bool>)(() => a.health.Current < health);
                Note("  riposte " + riposte + ": " + (health - a.health.Current).ToString("F0") + " damage, " + a.health.Current.ToString("F0") + " left");
                if (riposte == 1)
                {
                    Check("one riposte doesn't kill him", a.IsDead == false, "");
                }

                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
                yield return (Func<bool>)(() => Is(a, a.staggerState) == false);
            }

            Check("two do", a.IsDead, a.health.Current.ToString("F0") + " left");
        }

        // Low poise: any heavy staggers him. Counts the heavies a twin takes, each landed outside his stagger.
        private static IEnumerator Heavies()
        {
            Note("--- heavies");
            Hold(a);
            Hold(b);
            int heavies = 0;
            for (int heavy = 1; heavy <= 3 && a.IsDead == false; heavy++)
            {
                float health = a.health.Current;
                yield return WaitFree(a);
                Place(a, 1.6f);
                yield return 0.05f;
                Press("OnHeavyPressed");
                yield return (Func<bool>)(() => a.health.Current < health);
                heavies++;
                Note("  heavy " + heavy + ": " + (health - a.health.Current).ToString("F0") + " damage, " + a.health.Current.ToString("F0") + " left, " + State(a));
                if (heavy == 1)
                {
                    Check("a heavy staggers him", Is(a, a.staggerState), State(a));
                }

                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
                yield return 0.3f;
            }

            Check("heavies kill him", a.IsDead, heavies + " heavies (the note says 3)");
        }

        // --- The bond ---

        // Killing one enrages the other: he roars, then hits twice as hard.
        private static IEnumerator TheSurvivorsRage()
        {
            Note("--- the survivor's rage");
            Hold(a);
            Hold(b);
            a.health.ApplyDamage(a.health.Current - 1f);
            Place(a, 1.6f);
            yield return 0.05f;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => a.IsDead);
            yield return 0.05f;
            Check("his partner's death enrages him", b.IsEnraged && Is(b, b.enrageState), State(b));

            b.attackRange = homeAttackRange;
            b.aggressionRange = homeAggression;
            Place(b, 2f);
            int before = playerHits;
            yield return (Func<bool>)(() => playerHits > before);
            Check("and he hits twice as hard", Mathf.Abs(lastPlayerDamage - b.attackDamage * b.enragedDamageMultiplier) < 0.01f, lastPlayerDamage.ToString("F0") + " damage");
        }

        // --- Helpers ---

        // Keeps a twin from swinging (and out of the beat) while the player works on him.
        private static void Hold(CursedTwins twin)
        {
            twin.attackRange = 0f;
            twin.aggressionRange = 0f;
        }

        // Until the player can swing again and the twin is out of his stagger; then he's stood still where he is, since a
        // held twin back in Chase still walks to his flank and would step out from under the next blow.
        private static IEnumerator WaitFree(CursedTwins twin)
        {
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed && (Is(twin, twin.staggerState) == false || twin.IsDead));
            if (twin.IsDead == false && Is(twin, twin.chaseState))
            {
                twin.stateMachine.ChangeState(twin.idleState);
            }
        }

        private static bool Is(CursedTwins twin, EnemyState state) => twin.stateMachine.currentState == state;

        private static string State(CursedTwins twin) => twin.stateMachine.currentState.GetType().Name;

        private static bool OnNavMesh(Vector3 near, int agentType, out Vector3 point)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            bool found = NavMesh.SamplePosition(near, out NavMeshHit hit, 6f, filter);
            point = found ? hit.position : near;
            return found;
        }

        private static void DriveInput()
        {
            if (a == null || Camera.main == null)
            {
                return;
            }

            CursedTwins aimAt = a.IsDead || a.isActiveAndEnabled == false ? b : a;
            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(aimAt.transform.position));

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

        // `distance` metres in front of a twin, with him turned to face the player.
        private static void Place(CursedTwins twin, float distance)
        {
            PlaceAt(twin.transform.position + twin.transform.forward * distance);
        }

        private static void PlaceAt(Vector3 position)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = a.transform.position.y;
            playerRoot.position = position;

            Vector3 toPair = Flat((a.transform.position + b.transform.position) * 0.5f - position);
            if (toPair.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toPair);
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
                    Note("console: " + message + " | " + (a != null ? State(a) + " / " + State(b) : "?") + " | " + stack);
                }

                Errors.Add(message);
            }
        }
    }
}
