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
    // A scripted fight with The Fool on his own ground, L4's E4 terrace beside the breach (Kneel/Enemies/The Fool/Playtest,
    // in Play mode in L4_BurningGate). Player input goes through the real handlers, the way CLAUDE.md says, and he runs his
    // own AI, so what it measures is the fight itself: the chase, the three-swing string and its wind-ups, the blade at
    // each contact, parrying each swing, rolling out of his line, hitting him in a wind-up, his health in lights and
    // heavies, and the breach. Results go to Temp/FoolPlaytest.txt. Nothing in the scene is saved. Leave TakeShots off for
    // timing: a screenshot stalls a frame.
    public static class FoolPlaytest
    {
        public const string ResultPath = "Temp/FoolPlaytest.txt";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // The middle of the terrace's clear core, and spots by the breach's lip (x 93, z 142-154) and the inner parapet's
        // gap (x 80, z 139-142, the drop onto the gallows).
        private static readonly Vector3 Arena = new Vector3(86f, 5f, 136f);
        private static readonly Vector3 ByTheLip = new Vector3(90.6f, 5f, 147f);
        private static readonly Vector3 ByTheGap = new Vector3(83.2f, 5f, 140.5f);

        // How long before he commits a roll has to start to take the player out of his line.
        private const float RollLead = 0.2f;

        // Screenshots stall a frame, so timing runs leave them off.
        public static bool TakeShots;

        // The camera's distance for the run, when set: closer shows the sword and the grip.
        public static float Zoom;

        // Runs only the scenarios of this name, when set.
        public static string Only;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static TheFool fool;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static float homeAttackRange;
        private static TheFool.SwingString[] allStrings;
        private static float homeAggression;
        private static float lastPlayerDamage;
        private static float lastPlayerDamageTime;
        private static int playerHits;
        private static float worstFrame;
        private static int failures;
        private static Vector3 playerMove;
        private static Action<float> damageListener;

        private static float Now => Time.unscaledTime;

        private static EnemyState FoolState => fool.stateMachine.currentState;

        [MenuItem("Kneel/Enemies/The Fool/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in L4_BurningGate first.");
                return;
            }

            // Statics outlive Play mode in this project, and these may point at the last session's objects.
            fool = null;
            combat = null;
            playerMove = Vector3.zero;

            // Unity's clock restarts with each session, so a time kept from the last one would outrank every new hit.
            lastPlayerDamageTime = -1f;
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
            Setup();
            yield return 0.5f;

            // The first hit of a session stalls about a second, so it's spent here, away from anything timed.
            yield return ResetFight(Arena, Quaternion.identity);
            fool.attackRange = 0f;
            Place(1.6f, 0f);
            yield return 0.05f;
            Press("OnStrikePressed");
            yield return 1.5f;

            var scenarios = new (string name, Func<IEnumerator> body)[]
            {
                ("chase", Chase),
                ("string", () => TheString(0)),
                ("string", () => TheString(1)),
                ("string", () => TheString(2)),
                ("string", () => TheString(3)),
                ("variety", Variety),
                ("parry", () => ParrySwing(0)),
                ("parry", () => ParrySwing(1)),
                ("parry", () => ParrySwing(2)),
                ("roll", RollOutOfTheLine),
                ("windup", HitInTheWindup),
                ("health", ThreeLights),
                ("health", OneHeavy),
                ("breach", KnockbackStopsAtTheLip),
                ("breach", TheGapHoldsHim),
                ("breach", OverTheLip),
            };

            foreach (var scenario in scenarios)
            {
                if (string.IsNullOrEmpty(Only) || Only.Split(',').Contains(scenario.name))
                {
                    yield return Scenario(scenario.body());
                }
            }

            if (Only != null && Only.Split(',').Contains("gallery"))
            {
                yield return Scenario(Gallery());
            }
        }

        private static IEnumerator Scenario(IEnumerator body)
        {
            yield return ResetFight(Arena, Quaternion.Euler(0f, 0f, 0f));
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

            // The one placed in the level: a stray editor copy (HideFlags) is not part of it.
            fool = UnityEngine.Object.FindObjectsByType<TheFool>().First(f => f.gameObject.hideFlags == HideFlags.None);
            foreach (Enemy other in UnityEngine.Object.FindObjectsByType<Enemy>())
            {
                other.gameObject.SetActive(other == fool);
            }

            // An Editor in the background can stop rendering, and with nothing rendered this culling mode stops his animation
            // events, which stalls the fight. A build always renders.
            fool.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (Zoom > 0f)
            {
                UnityEngine.Object.FindAnyObjectByType<PlayerCamera>().CurrentDistance = Zoom;
            }

            homeAttackRange = fool.attackRange;
            allStrings = fool.strings;
            homeAggression = fool.aggressionRange;

            damageListener = amount =>
            {
                lastPlayerDamage = amount;
                lastPlayerDamageTime = Now;
                playerHits++;
            };
            playerHealth.OnDamaged += damageListener;

            Note("fighting " + fool.name + " in " + fool.gameObject.scene.name + ", agent type " + UnityEngine.AI.NavMesh.GetSettingsNameFromID(fool.agent.agentTypeID));
        }

        private static IEnumerator ResetFight(Vector3 at, Quaternion facing)
        {
            // Dead is final in the controller, so a body is put back by hand, falls and all.
            if (fool.IsDead)
            {
                typeof(TheFool).GetField("falling", Any).SetValue(fool, false);
                foreach (Collider body in fool.GetComponents<Collider>())
                {
                    body.enabled = true;
                }

                foreach (Renderer body in fool.GetComponentsInChildren<Renderer>())
                {
                    body.enabled = true;
                }

                fool.transform.position = at;
                fool.agent.enabled = true;
                fool.anim.Play("Idle", 0, 0f);
            }

            fool.attackRange = homeAttackRange;
            fool.aggressionRange = homeAggression;
            fool.strings = allStrings;
            typeof(TheFool).GetField("lastString", Any).SetValue(fool, -1);
            fool.health.SetMax(fool.health.Max, true);
            fool.agent.Warp(at);
            fool.stateMachine.ChangeState(fool.idleState);
            fool.transform.rotation = facing;

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            playerMove = Vector3.zero;
            SetBlock(false);
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);

            // Out of his sight while the player gets ready.
            fool.aggressionRange = 0f;
            Place(6f, 0f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
            {
                Press("OnDrawPressed");
            }

            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.2f;
            fool.aggressionRange = homeAggression;
        }

        // --- Scenarios ---

        private static IEnumerator Gallery()
        {
            fool.aggressionRange = 0f;
            PlaceDownScreen(3f);
            yield return 1.5f;
            Shot("Idle");

            fool.aggressionRange = homeAggression;
            yield return (Func<bool>)(() => FoolState == fool.noticeState);
            yield return 0.7f;
            Shot("Notice");

            fool.attackRange = 0f;
            yield return (Func<bool>)(() => FoolState == fool.chaseState);
            fool.agent.Warp(Arena + Vector3.forward * 8f);
            yield return 0.6f;
            Shot("Chase");

            fool.attackRange = homeAttackRange;
            PlaceDownScreen(3.6f);
            yield return (Func<bool>)(() => FoolState == fool.attackState);
            yield return 0.55f;
            Shot("Windup");
            for (int swing = 0; swing < fool.attackState.SwingCount; swing++)
            {
                int before = playerHits;
                yield return (Func<bool>)(() => playerHits > before || FoolState != fool.attackState);
                Shot("Swing" + (swing + 1));
            }

            yield return (Func<bool>)(() => FoolState != fool.attackState);
            yield return 0.6f;
            Shot("Recovery");
        }

        private static IEnumerator Chase()
        {
            Note("--- first sight and the chase");

            // From the north end, so the player has the length of the terrace to run down.
            yield return ResetFight(new Vector3(86f, 5f, 152f), Quaternion.Euler(0f, 180f, 0f));
            fool.aggressionRange = 0f;
            Place(homeAggression + 3f, 0f);
            yield return 0.2f;
            fool.aggressionRange = homeAggression;
            Place(homeAggression - 1f, 0f);
            yield return (Func<bool>)(() => FoolState == fool.noticeState);
            Check("he spots the player at " + homeAggression + " m (the Archer's range)", FoolState == fool.noticeState, FoolState.GetType().Name);
            float noticed = Now;
            yield return (Func<bool>)(() => FoolState == fool.chaseState);
            Note("the notice takes " + (Now - noticed).ToString("F2") + " s");

            // Run straight away from him down the terrace.
            fool.attackRange = 0f;
            fool.aggressionRange = 60f;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, true);
            playerMove = Flat(playerRoot.position - fool.transform.position).normalized;
            yield return 0.6f;
            float startGap = Vector3.Distance(Flat(fool.transform.position), Flat(playerRoot.position));
            Vector3 playerFrom = playerRoot.position;
            yield return 1.2f;
            float gap = Vector3.Distance(Flat(fool.transform.position), Flat(playerRoot.position));
            float playerSpeed = Vector3.Distance(Flat(playerFrom), Flat(playerRoot.position)) / 1.2f;
            Shot("Chase");
            playerMove = Vector3.zero;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);

            Check("he runs about 5 m/s", Mathf.Abs(fool.agent.velocity.magnitude - fool.chaseSpeed) < 0.4f, fool.agent.velocity.magnitude.ToString("F2") + " m/s");
            Check("he closes on a sprinting player", gap < startGap - 0.3f, startGap.ToString("F1") + " to " + gap.ToString("F1") + " m in 1.2 s, the player at " + playerSpeed.ToString("F2") + " m/s");
        }

        // Plays string `which` through, taken standing still: each swing's wind-up, its blade, and how far it carries him.
        private static IEnumerator TheString(int which)
        {
            Force(which);
            Note("--- the " + allStrings[which].name + " string, taken standing still");
            Place(3.6f, 0f);
            yield return (Func<bool>)(() => FoolState == fool.attackState);
            float start = Now;
            int hitsBefore = playerHits;
            int count = fool.attackState.SwingCount;
            Vector3 from = Flat(fool.transform.position);

            for (int swing = 0; swing < count; swing++)
            {
                float swingStart = Now;
                int before = playerHits;
                float windup = fool.attackState.Windup;
                Vector3 swingFrom = Flat(fool.transform.position);
                if (swing == 0)
                {
                    yield return 0.5f;
                    Shot(allStrings[which].name + "_Windup");
                }

                yield return (Func<bool>)(() => playerHits > before || FoolState != fool.attackState);
                if (playerHits == before)
                {
                    Note("  swing " + (swing + 1) + " never landed: " + FoolState.GetType().Name + " after " + (Now - swingStart).ToString("F2") + " s");
                    failures++;
                    break;
                }

                float landed = Now - swingStart;
                string name = fool.swings[allStrings[which].swings[swing]].state;
                Shot(allStrings[which].name + "_" + (swing + 1));
                CheckBlade(name);
                Check(name + " lands at its wind-up (" + windup + " s)", Mathf.Abs(landed - windup) < 0.08f, landed.ToString("F3") + " s from its start");
                Check(name + " wind-up at least 300 ms", landed >= 0.3f, "");
                Note("  " + name + " carried him " + Vector3.Distance(swingFrom, Flat(fool.transform.position)).ToString("F2") + " m, damage " + lastPlayerDamage.ToString("F0"));

                // The next swing starts when this clip ends.
                int index = swing;
                yield return (Func<bool>)(() => FoolState != fool.attackState || CurrentSwing() > index);
                if (swing + 1 < count)
                {
                    Note("  the next starts " + (Now - swingStart - landed).ToString("F2") + " s after it lands");
                }
            }

            Check("all " + count + " swings land", playerHits - hitsBefore == count, (playerHits - hitsBefore) + " hits");
            yield return (Func<bool>)(() => FoolState != fool.attackState);
            Check("he recovers after the last", FoolState == fool.recoveryState, FoolState.GetType().Name + ", string took " + (Now - start).ToString("F2") + " s");
            Note("the string carried him " + Vector3.Distance(from, Flat(fool.transform.position)).ToString("F2") + " m");
            yield return 0.5f;
            Shot(allStrings[which].name + "_Recovery");
        }

        // His pick: the plain string first, then all four turn up and none twice running.
        private static IEnumerator Variety()
        {
            Note("--- which string he picks");
            typeof(TheFool).GetField("lastString", Any).SetValue(fool, -1);
            var picks = new List<string>();
            for (int i = 0; i < 40; i++)
            {
                picks.Add(fool.NextString().name);
            }

            int repeats = Enumerable.Range(1, picks.Count - 1).Count(i => picks[i] == picks[i - 1]);
            Check("the first is the plain three", picks[0] == allStrings[0].name, picks[0]);
            Check("all four turn up", picks.Distinct().Count() == allStrings.Length, string.Join(" ", picks.Take(12)) + " ...");
            Check("never the same twice running", repeats == 0, repeats + " repeats");
            yield break;
        }

        // Blocks the swings before it, then parries swing `index` with a fresh press just before it lands.
        private static IEnumerator ParrySwing(int index)
        {
            Note("--- parrying swing " + (index + 1));
            Force(0);
            Place(3.6f, 0f);
            if (index > 0)
            {
                SetBlock(true);
            }

            yield return (Func<bool>)(() => FoolState == fool.attackState);
            yield return (Func<bool>)(() => CurrentSwing() == index || FoolState != fool.attackState);
            SetBlock(false);
            float swingStart = Time.time;
            int before = playerHits;
            float parryBefore = combat.LastParryTime;
            yield return fool.attackState.Windup - 0.1f;
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => FoolState != fool.attackState);
            SetBlock(false);

            Check("the parry catches swing " + (index + 1), combat.LastParryTime > parryBefore, (combat.LastParryTime - swingStart).ToString("F2") + " s in");
            Check("it staggers him and ends the string", FoolState == fool.staggerState, FoolState.GetType().Name);
            Shot("Parried" + (index + 1));
            yield return 1.2f;
            Check("no swing comes after it", playerHits == before, (playerHits - before) + " hits");

            if (index == 0)
            {
                // A heavy in the stagger is a riposte, and it kills him.
                Place(1.6f, 0f);
                yield return 0.05f;
                Press("OnHeavyPressed");
                yield return (Func<bool>)(() => fool.IsDead);
                Check("a riposte kills him", fool.IsDead, fool.health.Current.ToString("F0") + " health left");
            }
        }

        private static IEnumerator RollOutOfTheLine()
        {
            Note("--- rolling out of his line");
            Force(0);
            Place(3.6f, 0f);
            yield return (Func<bool>)(() => FoolState == fool.attackState);
            int before = playerHits;
            Vector3 from = Flat(fool.transform.position);
            Vector3 line = fool.transform.forward;

            // As he commits: the roll is under way when his line is set, and takes the player out of it.
            yield return fool.firstWindup - fool.commitTime - RollLead;
            playerMove = fool.transform.right;

            // The roll takes PlayerMovement's direction from the frame before (PlayerCombat runs first), so the stick goes
            // down a frame ahead of the press, as a player's does.
            DriveInput();
            yield return 0.03f;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return RollLead + 0.02f;
            line = fool.transform.forward;
            from = Flat(fool.transform.position);
            yield return (Func<bool>)(() => CurrentSwing() > 0 || FoolState != fool.attackState);
            playerMove = Vector3.zero;
            float along = Vector3.Dot(Flat(fool.transform.position) - from, line);
            Check("the roll clears the first swing", playerHits == before, (playerHits - before) + " hits");
            Check("his swing carries on its whole length", along > fool.lungeDistance - 0.3f, along.ToString("F2") + " m along his line");
            Place(20f, 0f);
            yield return (Func<bool>)(() => FoolState != fool.attackState);
        }

        // No poise: a light in his slow first wind-up knocks him out of the string.
        private static IEnumerator HitInTheWindup()
        {
            Note("--- hitting him in the wind-up");
            Place(1.7f, 0f);
            yield return (Func<bool>)(() => FoolState == fool.attackState);
            int before = playerHits;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => FoolState != fool.attackState);
            Check("a light in the wind-up stops the string", FoolState == fool.hitState && playerHits == before, FoolState.GetType().Name + ", " + (playerHits - before) + " hits taken");
            Shot("HitInWindup");
        }

        private static IEnumerator ThreeLights()
        {
            Note("--- three lights");
            fool.attackRange = 0f;
            Place(1.6f, 0f);
            int hits = 0;
            for (int light = 1; light <= 3 && fool.IsDead == false; light++)
            {
                float health = fool.health.Current;
                Press("OnStrikePressed");
                yield return (Func<bool>)(() => fool.health.Current < health);
                hits++;
                Note("  light " + light + ": " + (health - fool.health.Current).ToString("F0") + " damage, " + fool.health.Current.ToString("F0") + " left");
                if (light == 2)
                {
                    Check("two lights don't kill him", fool.IsDead == false, "");
                }

                yield return 0.6f;
                Place(1.6f, 0f);
                yield return 0.05f;
            }

            Check("three lights do", fool.IsDead, hits + " lights");
        }

        private static IEnumerator OneHeavy()
        {
            Note("--- one heavy");
            fool.attackRange = 0f;
            Place(1.6f, 0f);
            yield return 0.05f;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => fool.health.Current < fool.health.Max);
            Check("one heavy kills him", fool.IsDead, fool.health.Current.ToString("F0") + " left");
        }

        // Knockback never takes him over: the NavMesh clamps it short of the lip.
        private static IEnumerator KnockbackStopsAtTheLip()
        {
            Note("--- knockback at the lip");
            yield return ResetFight(ByTheLip + Vector3.right * 1.5f, Quaternion.Euler(0f, -90f, 0f));
            fool.attackRange = 0f;
            fool.aggressionRange = 0f;
            Place(1.6f, 0f);
            yield return 0.05f;
            for (int light = 0; light < 2; light++)
            {
                float health = fool.health.Current;
                Press("OnStrikePressed");
                yield return (Func<bool>)(() => fool.health.Current < health);
                yield return 0.7f;
                Place(1.6f, 0f);
                yield return 0.05f;
            }

            Check("two hits toward the breach leave him on the terrace", fool.IsFalling == false && fool.transform.position.y > 4.5f && fool.transform.position.x < 93f, fool.transform.position.ToString("F2"));
        }

        // The inner parapet's gap drops onto the gallows. Enemies must not follow, so his swing stops at its edge.
        private static IEnumerator TheGapHoldsHim()
        {
            Note("--- a swing at the inner gap");
            yield return ResetFight(ByTheGap, Quaternion.Euler(0f, -90f, 0f));
            yield return SwingPastThePlayer(-1f);
            Check("he stays on the terrace", fool.IsFalling == false && fool.transform.position.y > 4.5f && fool.transform.position.x > 80.3f, fool.transform.position.ToString("F2") + ", " + FoolState.GetType().Name);
        }

        // The one place his swing can carry him over: the breach, when the player steps out of his line (L4 decision 18).
        private static IEnumerator OverTheLip()
        {
            Note("--- a swing at the breach");
            yield return ResetFight(ByTheLip, Quaternion.Euler(0f, 90f, 0f));
            yield return SwingPastThePlayer(1f);
            yield return 0.6f;
            Shot("OverTheLip");
            Check("the swing carries him over the lip", fool.IsFalling || fool.transform.position.y < 3f, fool.transform.position.ToString("F2") + ", " + FoolState.GetType().Name);
            yield return 2f;
            Check("and he's gone", fool.IsDead && fool.transform.position.y < -10f, fool.transform.position.y.ToString("F1") + " m");
        }

        // Stands the player between him and an edge along x, then rolls out of his line as he commits.
        private static IEnumerator SwingPastThePlayer(float towardX)
        {
            fool.aggressionRange = homeAggression;
            PlaceAt(fool.transform.position + Vector3.right * towardX * 1.9f);
            yield return (Func<bool>)(() => FoolState == fool.attackState);
            yield return fool.firstWindup - fool.commitTime - RollLead;
            playerMove = Vector3.forward;
            DriveInput();
            yield return 0.03f;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return RollLead + fool.commitTime + fool.lungeTail + 0.1f;
            playerMove = Vector3.zero;
            Note("  after the swing: " + fool.transform.position.ToString("F2") + ", " + FoolState.GetType().Name);
        }

        // His next string will be `which`.
        private static void Force(int which)
        {
            fool.strings = new[] { allStrings[which] };
            typeof(TheFool).GetField("lastString", Any).SetValue(fool, -1);
        }

        private static int CurrentSwing() => (int)typeof(AttackState_TheFool).GetField("swing", Any).GetValue(fool.attackState);

        // The greatsword's tip at the frame a swing lands: the hit has to land where the blade is, not just on time.
        private static void CheckBlade(string swing)
        {
            // The first hit of a session stalls about a second, and the pose jumps ahead of the event inside that frame.
            if (Time.unscaledDeltaTime > 0.1f)
            {
                Note("  blade not read: stalled frame of " + Time.unscaledDeltaTime.ToString("F2") + " s");
                return;
            }

            Transform socket = fool.anim.GetBoneTransform(HumanBodyBones.RightHand).Find("Socket_SwordHand");
            Vector3 tip = socket.TransformPoint(0f, 2f, 0f) - fool.transform.position;
            float angle = Vector3.SignedAngle(fool.transform.forward, Flat(tip), Vector3.up);
            Check(swing + ": the blade is in front of him at contact", Mathf.Abs(angle) <= 45f, angle.ToString("F0") + " deg, " + Flat(tip).magnitude.ToString("F2") + " m out, " + tip.y.ToString("F2") + " m up, player " + Vector3.Distance(Flat(playerRoot.position), Flat(fool.transform.position)).ToString("F2") + " m away");
        }

        // --- Player driving ---

        private static void DriveInput()
        {
            if (fool == null || Camera.main == null)
            {
                return;
            }

            // At his feet: the aim ray stops at the ground, so aiming at his chest lands past him and turns the player away.
            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(fool.transform.position));

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

        // `distance` metres along his facing, `side` metres to his right; he and the player end up facing each other.
        private static void Place(float distance, float side)
        {
            Transform body = fool.transform;
            PlaceAt(body.position + body.forward * distance + body.right * side);
        }

        // Down-screen of him, where the camera keeps him in frame, and him turned to the player.
        private static void PlaceDownScreen(float distance)
        {
            Vector3 up = Camera.main.transform.forward;
            up.y = 0f;
            PlaceAt(fool.transform.position - up.normalized * distance);
            fool.transform.rotation = Quaternion.LookRotation(Flat(playerRoot.position - fool.transform.position));
        }

        private static void PlaceAt(Vector3 position)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = fool.transform.position.y;
            playerRoot.position = position;

            Vector3 toFool = Flat(fool.transform.position - position);
            if (toFool.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toFool);
            }

            controller.enabled = true;
        }

        // --- Reporting ---

        private static void Shot(string name)
        {
            if (TakeShots)
            {
                ScreenCapture.CaptureScreenshot("Temp/Fool_" + name + ".png");
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
                if (Errors.Contains(message) == false)
                {
                    Note("console: " + message + " | fool at " + (fool != null ? fool.transform.position + " " + FoolState?.GetType().Name : "?") + " | " + stack);
                }

                Errors.Add(message);
            }
        }
    }
}
