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
    // A scripted fight with one Blighted Brute. Start it once a level with a Brute is playing (Kneel/Enemies/Blighted
    // Brute/Playtest): L3's E2 is his teach arena and the most open floor he has. Player input goes through the real
    // handlers with the input cached the way CLAUDE.md says, and he runs his own AI, so what it measures is the fight
    // itself: wind-ups, reach, taking a hit, blocking, parrying, bashing to a stagger, health in lights and heavies, fleeing
    // and foot sliding. Results go to Temp/BrutePlaytest.txt. Nothing in the scene is saved. Leave TakeShots off for timing:
    // a screenshot stalls a frame.
    public static class BrutePlaytest
    {
        public const string ResultPath = "Temp/BrutePlaytest.txt";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Screenshots stall a frame, so timing runs leave them off.
        public static bool TakeShots;

        // Runs only the scenarios of this name, when set.
        public static string Only;

        // The camera's distance for the run, when set: closer shows the grips and the feet.
        public static float Zoom;

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static BlightedBrute brute;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static Vector3 home;
        private static Quaternion homeFacing;
        private static float homeAttackRange;
        private static float homeTurnSpeed;
        private static float homeChaseSpeed;
        private static float homeMoveSpeed;
        private static float homeAggression;
        private static float homeMaxPoise;
        private static float lastPlayerDamage;
        private static float lastPlayerDamageTime;
        private static float worstFrame;
        private static float lowestTimeScale;
        private static int failures;
        private static Vector3 playerMove;
        private static Action<float> damageListener;

        private static float Now => Time.unscaledTime;

        private static EnemyState BruteState => brute.stateMachine.currentState;

        private static bool Attacking => brute.IsAttacking;

        [MenuItem("Kneel/Enemies/Blighted Brute/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in a level with a Brute first.");
                return;
            }

            // Statics outlive Play mode in this project, and these may point at the last level's objects.
            brute = null;
            combat = null;
            playerMove = Vector3.zero;

            // Unity's clock restarts with each session, so a time kept from the last one would outrank every new hit.
            lastPlayerDamageTime = -1f;
            lastPlayerDamage = 0f;

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
            lowestTimeScale = Mathf.Min(lowestTimeScale, Time.timeScale);
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
                    waitForDeadline = Now + 14f;
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

            var scenarios = new (string name, Func<IEnumerator> body)[]
            {
                ("patrol", Patrol),
                ("swing", SwingTiming),
                ("reach", () => Reach(2.6f, true)),
                ("reach", () => Reach(3.2f, false)),
                ("order", AttackMix),
                ("blows", () => Blow("chop", () => brute.chopState, brute.chopWindup, brute.chopDamage)),
                ("blows", () => Blow("spin", () => brute.spinState, brute.spinWindup, brute.spinDamage)),
                ("combo", ComboPlaysInFull),
                ("combo", ComboThroughTheGuard),
                ("combo", ParryStopsTheCombo),
                ("blows", SpinFindsTheBack),
                ("window", PunishWindow),
                ("hit", TakeTheHit),
                ("block", Block),
                ("parry", Parry),
                ("roll", RollThrough),
                ("bash", BashInTheWindup),
                ("bash", TwoBashes),
                ("poise", TwoHeavies),
                ("poise", FourLightsHold),
                ("poise", LightsDontHoldHim),
                ("health", EightLights),
                ("health", FourHeavies),
                ("flee", Flee),
                ("feet", () => FootSlide(homeMoveSpeed, "patrol", true)),
                ("feet", () => FootSlide(homeChaseSpeed, "chase")),
            };

            foreach (var scenario in scenarios)
            {
                if (string.IsNullOrEmpty(Only) || scenario.name == Only)
                {
                    yield return Scenario(scenario.body());
                }
            }

            if (Only == "gallery")
            {
                yield return Scenario(Gallery());
            }

            if (Only == "offscreen")
            {
                yield return Scenario(OffScreen());
            }
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

            // Fight the one nearest the player; everything else leaves the level for the test.
            brute = UnityEngine.Object.FindObjectsByType<BlightedBrute>().OrderBy(b => Vector3.Distance(b.transform.position, playerRoot.position)).First();
            foreach (Enemy other in UnityEngine.Object.FindObjectsByType<Enemy>())
            {
                other.gameObject.SetActive(other == brute);
            }

            if (Zoom > 0f)
            {
                UnityEngine.Object.FindAnyObjectByType<PlayerCamera>().CurrentDistance = Zoom;
            }

            // An Editor in the background can stop rendering, and with nothing rendered this culling mode stops his animation
            // events, which stalls the fight. A build always renders. OffScreen leaves it as it is, to test the real thing.
            if (Only != "offscreen")
            {
                brute.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            home = brute.transform.position;
            homeFacing = OpenGround(home);
            homeAttackRange = brute.attackRange;
            homeTurnSpeed = brute.turnSpeed;
            homeChaseSpeed = brute.chaseSpeed;
            homeMoveSpeed = brute.moveSpeed;
            homeAggression = brute.aggressionRange;
            homeMaxPoise = brute.poise.maxPoise;

            damageListener = amount =>
            {
                lastPlayerDamage = amount;
                lastPlayerDamageTime = Now;
            };
            playerHealth.OnDamaged += damageListener;

            Note("fighting " + brute.name + " at " + home + " in " + brute.gameObject.scene.name + ", agent type " + brute.agent.agentTypeID);
        }

        // The facing with the most room both ahead and behind, since the fight happens along that line.
        private static Quaternion OpenGround(Vector3 from)
        {
            float best = -1f;
            Quaternion facing = Quaternion.identity;
            for (int angle = 0; angle < 360; angle += 15)
            {
                Quaternion candidate = Quaternion.Euler(0f, angle, 0f);
                float room = Mathf.Min(Room(from, candidate * Vector3.forward, 14f), Room(from, candidate * Vector3.back, 5f) * 3f);
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
            return UnityEngine.AI.NavMesh.Raycast(from, from + direction * length, out UnityEngine.AI.NavMeshHit hit, brute.agent.areaMask) ? hit.distance : length;
        }

        private static IEnumerator ResetFight()
        {
            // Dead is final in the controller, so a body is put back by hand.
            if (brute.IsDead)
            {
                brute.GetComponent<Collider>().enabled = true;
                brute.agent.enabled = true;
                brute.anim.Play("Idle", 0, 0f);
            }

            brute.attackRange = homeAttackRange;
            brute.turnSpeed = homeTurnSpeed;
            brute.chaseSpeed = homeChaseSpeed;
            brute.moveSpeed = homeMoveSpeed;
            brute.aggressionRange = homeAggression;
            brute.poise.maxPoise = homeMaxPoise;
            brute.health.SetMax(brute.health.Max, true);
            brute.poise.Reset();
            brute.agent.Warp(home);
            brute.stateMachine.ChangeState(brute.idleState);
            brute.transform.rotation = homeFacing;

            typeof(BlightedBrute).GetField("lastAttack", Any).SetValue(brute, null);
            typeof(BlightedBrute).GetField("hasRoared", Any).SetValue(brute, false);
            typeof(BlightedBrute).GetField("nextFlinchTime", Any).SetValue(brute, 0f);

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            playerMove = Vector3.zero;
            SetBlock(false);
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);

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

        // Run on its own: does he still act when the camera can't see him? His bones stop updating then, and his roar and
        // attacks end on animation events.
        private static IEnumerator OffScreen()
        {
            var body = brute.GetComponentInChildren<SkinnedMeshRenderer>();
            brute.aggressionRange = 40f;

            Vector3 up = Camera.main.transform.forward;
            up.y = 0f;
            PlaceAt(brute.transform.position + up.normalized * 11f);
            yield return 0.5f;
            Note("placed " + Vector3.Distance(brute.transform.position, playerRoot.position).ToString("F1") + " m up-screen of him: visible " + body.isVisible + ", " + BruteState.GetType().Name + ", culling " + brute.anim.cullingMode);

            for (int i = 0; i < 8; i++)
            {
                yield return 1f;
                var info = brute.anim.GetCurrentAnimatorStateInfo(0);
                Note("  " + (i + 1) + " s: visible " + body.isVisible + ", " + BruteState.GetType().Name + ", notice clip " + info.IsName("Notice") + " at " + info.normalizedTime.ToString("F2") + ", " + Vector3.Distance(brute.transform.position, playerRoot.position).ToString("F1") + " m");
            }

            Check("he comes for a player the camera can't see him from", BruteState == brute.chaseState || Attacking, BruteState.GetType().Name);

            Note("--- attacking off screen");
            brute.chaseSpeed = 0f;
            float before = lastPlayerDamageTime;
            brute.attackRange = 0f;
            yield return 0.2f;
            brute.stateMachine.ChangeState(brute.attackState);
            float start = Now;
            yield return (Func<bool>)(() => BruteState != brute.attackState);
            Note("visible " + body.isVisible + ": the swing's state ended after " + (Now - start).ToString("F2") + " s, now " + BruteState.GetType().Name);
            Check("an attack finishes off screen", Now - start < 5f, (Now - start).ToString("F2") + " s");
        }

        // Run on its own with TakeShots on (Only = "gallery"): what the fight can't show, with him kept in frame.
        private static IEnumerator Gallery()
        {
            brute.aggressionRange = 0f;
            brute.attackRange = 0f;
            PlaceDownScreen(2.7f);
            yield return 1.5f;
            Shot("GalleryIdle");

            brute.stateMachine.ChangeState(brute.moveState);
            brute.agent.isStopped = false;
            brute.agent.SetDestination(home + homeFacing * Vector3.forward * 12f);
            yield return 1.6f;
            PlaceDownScreen(2.7f);
            yield return 0.4f;
            Shot("GalleryPatrol");
            PlaceDownScreen(2.7f);
            yield return 0.55f;
            Shot("GalleryPatrol2");

            brute.aggressionRange = 40f;
            brute.stateMachine.ChangeState(brute.chaseState);
            yield return 1.2f;
            PlaceDownScreen(2.7f);
            yield return 0.3f;
            Shot("GalleryChase");
            PlaceDownScreen(2.7f);
            yield return 0.4f;
            Shot("GalleryChase2");

            brute.attackRange = homeAttackRange;
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            yield return (Func<bool>)(() => BruteState == brute.recoveryState);
            PlaceDownScreen(2.7f);
            yield return 0.45f;
            Shot("GalleryRecovery");
            yield return 0.4f;
            Shot("GalleryRecovery2");
        }

        private static IEnumerator Patrol()
        {
            Note("--- patrol and first sight");
            Check("patrol is slow", brute.moveSpeed <= 1.3f, brute.moveSpeed.ToString("F1") + " m/s");
            Check("chase is under the player's run", brute.chaseSpeed < 4.5f, brute.chaseSpeed.ToString("F1") + " m/s");
            Place(brute.aggressionRange - 1f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.noticeState);
            Check("he notices the player", BruteState == brute.noticeState, BruteState.GetType().Name);
            Shot("Noticed");
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            yield return 1.2f;
            Shot("Chase");
            Check("he walks in after the thump", BruteState == brute.chaseState, BruteState.GetType().Name);
        }

        private static IEnumerator SwingTiming()
        {
            Note("--- swing");
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            float start = Now;
            float before = lastPlayerDamageTime;

            yield return 0.3f;
            Shot("Windup1");
            yield return 0.3f;
            Shot("Windup2");

            yield return (Func<bool>)(() => lastPlayerDamageTime > before || BruteState != brute.attackState);
            if (lastPlayerDamageTime <= before)
            {
                Vector3 toPlayer = Flat(playerRoot.position - brute.transform.position);
                Note("  the swing ended without landing: " + BruteState.GetType().Name + " after " + (Now - start).ToString("F2") + " s, player " + toPlayer.magnitude.ToString("F2") + " m away at " + Vector3.SignedAngle(brute.transform.forward, toPlayer, Vector3.up).ToString("F0") + " deg, " + combat.State + ", grounded " + movement.IsGrounded + ", y " + playerRoot.position.y.ToString("F2") + ", worst frame so far " + worstFrame.ToString("F2"));
                yield return (Func<bool>)(() => lastPlayerDamageTime > before);
            }

            float hit = Now - start;
            Shot("Contact");
            CheckBlade(35f);
            Check("swing lands at its wind-up", Mathf.Abs(hit - brute.swingWindup) < 0.08f, hit.ToString("F3") + " s");
            Check("wind-up at least 300 ms (Combat Feel Spec)", hit >= 0.3f, hit.ToString("F3") + " s");
            Check("swing damage", Mathf.Approximately(lastPlayerDamage, brute.attackDamage), lastPlayerDamage.ToString("F1"));

            float hitTime = Now;
            Place(30f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.recoveryState);
            Note("swing clip ends " + (Now - hitTime).ToString("F2") + " s after contact");
            yield return 0.4f;
            Shot("Recovery");
            yield return (Func<bool>)(() => BruteState != brute.recoveryState);
            Note("he moves again " + (Now - hitTime).ToString("F2") + " s after contact (the punish window)");
        }

        // Measures the right cleaver's tip against his facing at the frame the blow lands: the hit has to land where the
        // blade is, not just on time.
        private static void CheckBlade(float tolerance)
        {
            // The first hit of a session stalls about a second, and the pose jumps ahead of the event inside that frame.
            if (Time.unscaledDeltaTime > 0.1f)
            {
                Note("blade not read: stalled frame of " + Time.unscaledDeltaTime.ToString("F2") + " s");
                return;
            }

            Transform socket = brute.anim.GetBoneTransform(HumanBodyBones.RightHand).Find("Socket_CleaverRight");
            Vector3 tip = socket.TransformPoint(0f, 1.2f, 0f) - brute.transform.position;
            float angle = Vector3.SignedAngle(brute.transform.forward, Flat(tip), Vector3.up);
            Check("the blade is in front of him at contact", Mathf.Abs(angle) <= tolerance, angle.ToString("F0") + " deg, " + Flat(tip).magnitude.ToString("F2") + " m out, " + tip.y.ToString("F2") + " m up");
        }

        // Both blades' tips at the frame the blow lands: how far out the hit really is.
        private static void CheckTip()
        {
            if (Time.unscaledDeltaTime > 0.1f)
            {
                Note("tips not read: stalled frame of " + Time.unscaledDeltaTime.ToString("F2") + " s");
                return;
            }

            Transform right = brute.anim.GetBoneTransform(HumanBodyBones.RightHand).Find("Socket_CleaverRight");
            Transform left = brute.anim.GetBoneTransform(HumanBodyBones.LeftHand).Find("Socket_CleaverLeft");
            Vector3 r = Flat(right.TransformPoint(0f, 1.2f, 0f) - brute.transform.position);
            Vector3 l = Flat(left.TransformPoint(0f, 1.2f, 0f) - brute.transform.position);
            Note("tips at contact: right " + r.magnitude.ToString("F2") + " m at " + Vector3.SignedAngle(brute.transform.forward, r, Vector3.up).ToString("F0") + " deg, left " + l.magnitude.ToString("F2") + " m at " + Vector3.SignedAngle(brute.transform.forward, l, Vector3.up).ToString("F0") + " deg");
        }

        private static IEnumerator Reach(float distance, bool hits)
        {
            Note("--- reach at " + distance + " m");
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            float before = lastPlayerDamageTime;
            yield return brute.swingWindup - 0.15f;
            Place(distance, 0f);

            // Out to the end of his clip, so a slow frame around the blow can't read as a miss.
            yield return (Func<bool>)(() => lastPlayerDamageTime > before || BruteState != brute.attackState);
            bool landed = lastPlayerDamageTime > before;
            Check((hits ? "lands" : "misses") + " at " + distance + " m", landed == hits, landed ? "hit" : "missed");
        }

        // Stands in range and watches what he throws: the swing first, then a mix, never the same blow twice running, and a
        // real cooldown after every one.
        private static IEnumerator AttackMix()
        {
            Note("--- the mix, player standing in range");
            var order = new List<string>();
            var windows = new List<float>();
            var names = new Dictionary<EnemyState, string>
            {
                { brute.attackState, "swing" },
                { brute.chopState, "chop" },
                { brute.spinState, "spin" },
                { brute.comboState, "combo" },
            };

            for (int i = 0; i < 8; i++)
            {
                Place(2.2f, 0f);
                yield return (Func<bool>)(() => Attacking);
                order.Add(names[BruteState]);
                float before = lastPlayerDamageTime;
                yield return (Func<bool>)(() => lastPlayerDamageTime > before);
                float contact = Now;
                yield return (Func<bool>)(() => BruteState == brute.recoveryState);
                yield return (Func<bool>)(() => BruteState != brute.recoveryState);
                windows.Add(Now - contact);
            }

            bool repeats = Enumerable.Range(1, order.Count - 1).Any(i => order[i] == order[i - 1]);
            Check("the swing comes first", order[0] == "swing", string.Join(",", order));
            Check("never the same blow twice running", repeats == false, string.Join(",", order));
            Check("at least three different blows in eight", order.Distinct().Count() >= 3, order.Distinct().Count() + " kinds");
            // The first is left out: the first hit of a session stalls, and that time goes missing from the measure.
            Check("every blow leaves an opening of about 2 s or more", windows.Skip(1).Min() >= 1.9f, string.Join(", ", windows.Select(w => w.ToString("F1"))) + " s");
        }

        // One blow, called up by hand: when it lands, how hard, and how long he stays open after it.
        private static IEnumerator Blow(string name, Func<AttackState_BlightedBrute> state, float windup, float damage)
        {
            Note("--- " + name);
            brute.attackRange = 0f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            InFrame(2.2f);
            yield return 0.05f;

            brute.stateMachine.ChangeState(state());
            float start = Now;
            float before = lastPlayerDamageTime;
            if (TakeShots)
            {
                yield return windup * 0.55f;
                Shot(name + "Windup");
            }

            yield return (Func<bool>)(() => lastPlayerDamageTime > before);
            float hit = Now - start;
            Shot(name + "Contact");
            Check(name + " lands at its wind-up", Mathf.Abs(hit - windup) < 0.1f || Time.unscaledDeltaTime > 0.1f, hit.ToString("F3") + " s");
            Check(name + " wind-up at least 300 ms", hit >= 0.3f, hit.ToString("F3") + " s");
            Check(name + " damage", Mathf.Approximately(lastPlayerDamage, damage), lastPlayerDamage.ToString("F1"));
            CheckTip();

            float contact = Now;
            yield return (Func<bool>)(() => BruteState == brute.recoveryState);
            yield return (Func<bool>)(() => BruteState != brute.recoveryState);
            Note(name + " leaves him open for " + (Now - contact).ToString("F2") + " s after contact");
        }

        // A player who stands there and takes it: the shoves push them away, so this is what actually reaches them. Only blows
        // that really land are counted.
        private static IEnumerator ComboPlaysInFull()
        {
            Note("--- the full combo on a player who stands there");
            brute.attackRange = 0f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            InFrame(2.2f);
            yield return 0.05f;

            Vector3 from = brute.transform.position;
            brute.stateMachine.ChangeState(brute.comboState);
            float start = Now;
            var hits = new List<string>();
            float total = 0f;
            float last = lastPlayerDamageTime;
            int shot = 0;

            while (BruteState == brute.comboState)
            {
                yield return 0f;
                if (lastPlayerDamageTime > last)
                {
                    last = lastPlayerDamageTime;
                    hits.Add((Now - start).ToString("F2"));
                    total += lastPlayerDamage;
                    Shot("Combo" + shot++);
                    CheckTip();
                }
            }

            float advanced = Vector3.Distance(Flat(from), Flat(brute.transform.position));
            Check("every blow reaches them", hits.Count == brute.comboHits.Length, hits.Count + " of " + brute.comboHits.Length + " at " + string.Join(", ", hits) + " s, " + total.ToString("F0") + " damage");
            Check("a full combo doesn't kill from full health", total < 100f, total.ToString("F0"));
            Check("he presses forward as he swings", advanced > 1f, advanced.ToString("F2") + " m");
            float end = Now;
            yield return (Func<bool>)(() => BruteState != brute.recoveryState);
            Note("he moves again " + (Now - end).ToString("F2") + " s after the clip ends");
        }

        private static IEnumerator ComboThroughTheGuard()
        {
            Note("--- blocking the whole combo");
            brute.attackRange = 0f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            Place(2.2f, 0f);
            SetBlock(true);
            yield return 0.2f;

            brute.stateMachine.ChangeState(brute.comboState);
            float taken = 0f;
            int blocks = 0;
            for (int i = 0; i < brute.comboHits.Length && combat.State == PlayerCombat.CombatState.Armed; i++)
            {
                float before = combat.LastBlockHitTime;
                float damaged = lastPlayerDamageTime;
                yield return (Func<bool>)(() => lastPlayerDamageTime > damaged);
                taken += lastPlayerDamage;
                if (combat.LastBlockHitTime > before)
                {
                    blocks++;
                }

                Place(2.2f, 0f);
            }

            Check("a held guard takes the whole combo", blocks == brute.comboHits.Length, blocks + " of " + brute.comboHits.Length + " blocked, " + taken.ToString("F0") + " damage through, poise " + combat.Poise.Current.ToString("F0") + ", " + combat.State);
            SetBlock(false);
        }

        private static IEnumerator ParryStopsTheCombo()
        {
            Note("--- a parry on the first blow ends the combo");
            brute.attackRange = 0f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            Place(2.2f, 0f);
            yield return 0.05f;

            brute.stateMachine.ChangeState(brute.comboState);
            float damaged = lastPlayerDamageTime;
            yield return brute.comboHits[0] - 0.08f;
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => BruteState == brute.parriedState);
            SetBlock(false);
            Check("the parry stops it", BruteState == brute.parriedState && lastPlayerDamageTime <= damaged, BruteState.GetType().Name);
            yield return 0.8f;
            Check("no more blows come", lastPlayerDamageTime <= damaged, "");
        }

        private static IEnumerator SpinFindsTheBack()
        {
            Note("--- the spin finds a player behind him");
            brute.attackRange = 0f;
            brute.turnSpeed = 0f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);
            Place(-2.2f, 0f);
            yield return 0.05f;

            brute.stateMachine.ChangeState(brute.spinState);
            float before = lastPlayerDamageTime;
            yield return (Func<bool>)(() => lastPlayerDamageTime > before);
            Check("it lands behind him", lastPlayerDamageTime > before, lastPlayerDamage.ToString("F0"));
        }

        // The whole point of the cooldown: after a blow the player walks in and lands a heavy before he moves again.
        private static IEnumerator PunishWindow()
        {
            Note("--- punishing the cooldown");
            InFrame(2.2f);
            SetBlock(true);
            yield return (Func<bool>)(() => Attacking);
            yield return (Func<bool>)(() => BruteState == brute.recoveryState);
            SetBlock(false);
            if (TakeShots)
            {
                yield return 0.4f;
                Shot("Cooldown");
            }

            Place(1.5f, 0f);
            yield return Settle();
            float health = brute.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            Check("a heavy lands inside it", BruteState == brute.recoveryState, BruteState.GetType().Name + ", " + (health - brute.health.Current).ToString("F0") + " damage");
        }

        private static IEnumerator TakeTheHit()
        {
            Note("--- taking the swing unguarded");
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            float before = lastPlayerDamageTime;
            Vector3 stood = playerRoot.position;
            lowestTimeScale = 1f;
            float hitStopStart = 0f;

            yield return (Func<bool>)(() => lastPlayerDamageTime > before);
            hitStopStart = Now;
            Shot("TakeHit");
            Check("an unguarded hit breaks the player's poise", combat.State == PlayerCombat.CombatState.HitStun, combat.State.ToString());
            yield return (Func<bool>)(() => Time.timeScale > 0.5f);
            float stop = Now - hitStopStart;
            if (Time.unscaledDeltaTime > 0.1f)
            {
                Note("hit-stop not read: a stalled frame landed inside it");
            }
            else
            {
                Check("hit-stop within the spec's 4 to 8 frames", stop >= 0.06f && stop <= 0.16f, stop.ToString("F3") + " s");
            }

            yield return 0.35f;
            float shoved = Vector3.Distance(Flat(stood), Flat(playerRoot.position));
            Check("the blow moves the player", shoved >= 0.5f && shoved <= 0.9f, shoved.ToString("F2") + " m");
            Place(30f, 0f);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
        }

        private static IEnumerator Block()
        {
            Note("--- blocking the swing");
            Place(2.2f, 0f);
            SetBlock(true);
            yield return 0.3f;
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            float before = lastPlayerDamageTime;
            Vector3 stood = playerRoot.position;
            float blockBefore = combat.LastBlockHitTime;

            yield return (Func<bool>)(() => lastPlayerDamageTime > before);
            Shot("Block");
            Check("the shield takes it", combat.LastBlockHitTime > blockBefore, "");
            Check("blocked damage is the 20% that gets through", Mathf.Abs(lastPlayerDamage - brute.attackDamage * 0.2f) < 0.01f, lastPlayerDamage.ToString("F1"));
            Check("the guard holds", combat.State == PlayerCombat.CombatState.Armed, combat.State.ToString());

            yield return 0.45f;
            float shoved = Vector3.Distance(Flat(stood), Flat(playerRoot.position));
            Check("the blow pushes the guard back", Mathf.Abs(shoved - 0.36f) < 0.15f, shoved.ToString("F2") + " m");
            Check("a block costs real poise", combat.Poise.Current <= 15f, combat.Poise.Current.ToString("F0") + " of 50");

            SetBlock(false);
        }

        private static IEnumerator Parry()
        {
            Note("--- parry: stops the blow, no riposte");
            float parryBefore = combat.LastParryTime;
            float damageBefore = lastPlayerDamageTime;
            float started = Now;

            // The window is 0.2 s and the Editor hitches for 0.26 s every so often, so a press can lose its window to a slow
            // frame. Each miss shows in the report.
            for (int attempt = 1; attempt <= 3 && BruteState != brute.parriedState; attempt++)
            {
                Place(2.2f, 0f);
                yield return (Func<bool>)(() => BruteState == brute.attackState);
                damageBefore = lastPlayerDamageTime;
                started = Now;
                yield return brute.swingWindup - 0.08f;
                Press("OnBlockPressed");
                yield return (Func<bool>)(() => BruteState != brute.attackState);

                if (BruteState != brute.parriedState)
                {
                    Note("  attempt " + attempt + " missed its window: " + BruteState.GetType().Name + ", last frame " + Time.unscaledDeltaTime.ToString("F2") + " s, body visible " + brute.GetComponentInChildren<SkinnedMeshRenderer>().isVisible + ", game view focused " + UnityEditorInternal.InternalEditorUtility.isApplicationActive);
                    SetBlock(false);
                    typeof(BlightedBrute).GetField("lastAttack", Any).SetValue(brute, null);
                    yield return (Func<bool>)(() => BruteState == brute.chaseState);
                    combat.Poise.Reset();
                }
            }

            float parried = Now;
            Note("parried " + (parried - started).ToString("F2") + " s into the swing");
            Check("the parry catches the swing", combat.LastParryTime > parryBefore, "");
            Check("no damage through a parry", lastPlayerDamageTime <= damageBefore, "");
            Check("a parry doesn't stagger him", brute.IsStaggered == false, BruteState.GetType().Name);
            Shot("Parried");
            SetBlock(false);

            // A riposte on anything else would be a heavy on the stagger. Here it's only a heavy.
            brute.attackRange = 0f;
            yield return 0.2f;
            float health = brute.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            float dealt = health - brute.health.Current;
            Check("no critical hit after a parry", Mathf.Abs(dealt - 35f) < 0.5f, dealt.ToString("F1") + " (35 is a plain heavy, 52.5 a riposte)");
            Note("heavy landed " + (Now - parried).ToString("F2") + " s after the parry, he recoils for " + brute.parriedTime.ToString("F1") + " s");
        }

        private static IEnumerator RollThrough()
        {
            Note("--- rolling through the swing");
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            float before = lastPlayerDamageTime;
            yield return brute.swingWindup - 0.2f;
            playerMove = -brute.transform.forward;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return 0.6f;
            Check("the roll clears the swing", lastPlayerDamageTime <= before, combat.State.ToString());
            playerMove = Vector3.zero;
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
        }

        private static IEnumerator BashInTheWindup()
        {
            Note("--- a bash during the wind-up knocks the swing away");
            Place(1.6f, 0f);
            SetBlock(true);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            yield return 0.25f;
            float before = lastPlayerDamageTime;
            Press("OnStrikePressed");
            yield return 0.5f;
            Check("the swing never lands", lastPlayerDamageTime <= before, BruteState.GetType().Name);
            Check("one bash doesn't break his poise", brute.IsStaggered == false, brute.poise.Current.ToString("F0") + " of " + brute.poise.maxPoise.ToString("F0"));
            SetBlock(false);
        }

        // The real sequence: take the swing on the shield, bash in his recovery, again on the next one, then punish.
        private static IEnumerator TwoBashes()
        {
            Note("--- two bashes in his recoveries break his poise");

            for (int i = 1; i <= 2; i++)
            {
                // What he throws next is random, and a combo after a blocked swing can wear a guard right through.
                combat.Poise.Reset();
                Place(2.2f, 0f);
                SetBlock(true);
                yield return (Func<bool>)(() => Attacking);
                yield return (Func<bool>)(() => BruteState == brute.recoveryState);
                Place(1.5f, 0f);
                yield return 0.1f;
                Press("OnStrikePressed");
                yield return 0.4f;
                SetBlock(false);

                if (i == 1)
                {
                    Shot("Bash");
                    Check("one bash holds", brute.IsStaggered == false, brute.poise.Current.ToString("F0") + " of " + brute.poise.maxPoise.ToString("F0") + " left");
                }

                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            }

            Check("the second bash staggers him", brute.IsStaggered, BruteState.GetType().Name);

            // The loop above waits out the bash, which lands 0.2 s into it.
            float broke = Now - 0.3f;
            yield return 0.3f;
            Shot("Stagger");

            float health = brute.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            float dealt = health - brute.health.Current;
            Check("a heavy on the stagger is critical", Mathf.Abs(dealt - 52.5f) < 0.5f, dealt.ToString("F1"));
            Note("heavy landed " + (Now - broke).ToString("F2") + " s into the stagger, which lasts " + brute.staggerTime.ToString("F1") + " s");

            yield return (Func<bool>)(() => brute.IsStaggered == false);
            float length = Now - broke;
            Check("the stagger lasts its time", Mathf.Abs(length - brute.staggerTime) < 0.25f, length.ToString("F2") + " s");
        }

        // A strike straight after a teleport is an air attack: the fight's ground isn't flat, so the player may land a
        // moment later.
        private static IEnumerator Settle()
        {
            yield return (Func<bool>)(() => movement.IsGrounded);
            yield return 0.1f;
        }

        private static void HoldHimStill()
        {
            brute.attackRange = 0f;
            brute.chaseSpeed = 0f;
            brute.moveSpeed = 0f;
            brute.turnSpeed = 0f;
        }

        private static IEnumerator Heavy()
        {
            Place(1.5f, 0f);
            yield return Settle();
            float health = brute.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
        }

        private static IEnumerator TwoHeavies()
        {
            Note("--- design note: two heavies break his poise");
            HoldHimStill();
            yield return Heavy();
            Check("one heavy holds", brute.IsStaggered == false, brute.poise.Current.ToString("F0") + " left");
            yield return Heavy();
            Check("the second staggers him", brute.IsStaggered, BruteState.GetType().Name + ", poise " + brute.poise.Current.ToString("F0"));
        }

        // One press and a press in the combo window: a 12 and a 20.
        private static IEnumerator LightPair()
        {
            Place(1.5f, 0f);
            yield return Settle();
            Press("OnStrikePressed");
            yield return 0.75f;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
        }

        private static IEnumerator FourLightsHold()
        {
            Note("--- four lights don't break his poise");
            HoldHimStill();
            yield return LightPair();
            yield return LightPair();
            Check("still on his feet", brute.IsStaggered == false, "health " + brute.health.Current.ToString("F0") + ", poise " + brute.poise.Current.ToString("F0"));
        }

        private static IEnumerator LightsDontHoldHim()
        {
            Note("--- lights can't stun-lock him");
            brute.attackRange = 0f;
            Place(3f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);

            Place(1.3f, 0f);
            yield return Settle();
            float health = brute.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            Check("the first light makes him flinch", BruteState == brute.hitState, BruteState.GetType().Name + ", took " + (health - brute.health.Current).ToString("F1") + ", player " + combat.State + ", timer " + typeof(BlightedBrute).GetField("nextFlinchTime", Any).GetValue(brute) + " at " + Time.time.ToString("F2") + ", poise " + brute.poise.Current.ToString("F0"));
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);

            Place(1.3f, 0f);
            yield return Settle();
            health = brute.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => brute.health.Current < health);
            Check("the next one inside the cooldown doesn't", BruteState == brute.chaseState, BruteState.GetType().Name);
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);

            brute.attackRange = homeAttackRange;
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => Attacking);
            brute.TakeHit(new DamageInfo { amount = 12f, poiseDamage = 20f, direction = -brute.transform.forward, point = brute.transform.position, source = combat.gameObject });
            yield return 0.1f;
            Check("a swing carries on through a light", Attacking, BruteState.GetType().Name);
        }

        // Strikes in pairs, the second queued in the first's combo window, as a player does: a 12 and a 20.
        private static IEnumerator EightLights()
        {
            Note("--- design note: 8 lights kill him");
            HoldHimStill();
            brute.poise.maxPoise = 1000f;
            brute.poise.Reset();

            var taken = new List<string>();
            while (brute.IsDead == false && taken.Count < 12)
            {
                for (int i = 0; i < 2 && brute.IsDead == false; i++)
                {
                    if (i == 0)
                    {
                        Place(1.3f, 0f);
                        yield return Settle();
                    }

                    float before = brute.health.Current;
                    Press("OnStrikePressed");
                    yield return (Func<bool>)(() => brute.health.Current < before || combat.State == PlayerCombat.CombatState.Armed);

                    if (brute.health.Current < before)
                    {
                        taken.Add((before - brute.health.Current).ToString("F0"));
                    }
                    else
                    {
                        Note("  a strike missed: " + combat.State + ", grounded " + movement.IsGrounded + ", " + Vector3.Distance(Flat(brute.transform.position), Flat(playerRoot.position)).ToString("F2") + " m from him");
                    }
                }

                yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            }

            Check("he falls on the eighth", brute.IsDead && taken.Count == 8, taken.Count + " hits: " + string.Join(",", taken));
            yield return 2.5f;
            Shot("Dead");
            yield return 1f;
        }

        private static IEnumerator FourHeavies()
        {
            Note("--- design note: 4 heavies kill him");
            HoldHimStill();
            brute.poise.maxPoise = 1000f;
            brute.poise.Reset();

            for (int i = 1; i <= 4; i++)
            {
                yield return Heavy();
                if (i == 3)
                {
                    Check("three don't do it", brute.IsDead == false, "health " + brute.health.Current.ToString("F0"));
                }
            }

            Check("he falls on the fourth", brute.IsDead, "health " + brute.health.Current.ToString("F0"));
            yield return 1f;
        }

        private static IEnumerator Flee()
        {
            Note("--- fleeing");
            brute.aggressionRange = 40f;
            Place(4f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.chaseState);

            Vector3 away = brute.transform.forward;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, true);
            playerMove = away;
            float before = lastPlayerDamageTime;
            float startGap = Vector3.Distance(Flat(brute.transform.position), Flat(playerRoot.position));
            yield return 1.5f;
            float gap = Vector3.Distance(Flat(brute.transform.position), Flat(playerRoot.position));
            Shot("Flee");
            playerMove = Vector3.zero;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);

            Check("a sprint opens the gap", gap > startGap + 2.5f, startGap.ToString("F1") + " to " + gap.ToString("F1") + " m in 1.5 s");
            Check("he can't catch a sprint", lastPlayerDamageTime <= before, "");

            Note("--- walking away from a swing");
            Place(2.2f, 0f);
            yield return (Func<bool>)(() => BruteState == brute.attackState);
            before = lastPlayerDamageTime;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, true);
            playerMove = brute.transform.forward;
            yield return 1.3f;
            playerMove = Vector3.zero;
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);
            Check("the wind-up gives time to run", lastPlayerDamageTime <= before, "");
        }

        // What foot sliding is: a planted foot has to travel backward along the body exactly as fast as the body goes
        // forward. So this measures the feet's backward speed mid-stance, in the body's space, against the agent's speed.
        private static IEnumerator FootSlide(float speed, string name, bool patrol = false)
        {
            Note("--- feet at " + speed + " m/s (" + name + ")");
            brute.attackRange = 0f;
            brute.chaseSpeed = speed;

            if (patrol)
            {
                // Off to the side, unnoticed, so he stays in frame: his bones don't move off camera.
                brute.aggressionRange = 0f;
                Place(3f, 3f);
                brute.stateMachine.ChangeState(brute.moveState);
                brute.agent.isStopped = false;
                brute.agent.SetDestination(home + homeFacing * Vector3.forward * 12f);
            }
            else
            {
                brute.aggressionRange = 40f;
                Place(13f, 0f);
                yield return (Func<bool>)(() => BruteState == brute.chaseState);
            }

            yield return 0.8f;

            var animator = brute.GetComponent<Animator>();
            Transform body = brute.transform;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            var left = new List<(float time, float along)>();
            var right = new List<(float time, float along)>();
            float until = Now + 3f;
            float agentSpeed = 0f;
            int frames = 0;

            while (Now < until)
            {
                yield return 0f;
                left.Add((Time.time, Vector3.Dot(leftFoot.position - body.position, body.forward)));
                right.Add((Time.time, Vector3.Dot(rightFoot.position - body.position, body.forward)));
                agentSpeed += brute.agent.velocity.magnitude;
                frames++;
            }

            agentSpeed /= Mathf.Max(1, frames);
            Shot("Walk" + name);

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
            if (brute == null || Camera.main == null)
            {
                return;
            }

            // At his feet: the aim ray stops at the ground, so aiming at his chest lands past him and turns the player away.
            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(brute.transform.position));

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
            Transform body = brute.transform;
            PlaceAt(body.position + body.forward * distance + body.right * side);
        }

        // A plain placement in front of him, or down-screen of him and turned to the player when shots are being taken.
        private static void InFrame(float distance)
        {
            if (TakeShots == false)
            {
                Place(distance, 0f);
                return;
            }

            PlaceDownScreen(distance);
            brute.transform.rotation = Quaternion.LookRotation(Flat(playerRoot.position - brute.transform.position));
        }

        // Down-screen of him, where the camera keeps him in frame: it sees far more ahead of the player than behind.
        private static void PlaceDownScreen(float distance)
        {
            Vector3 up = Camera.main.transform.forward;
            up.y = 0f;
            PlaceAt(brute.transform.position - up.normalized * distance);
        }

        private static void PlaceAt(Vector3 position)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = brute.transform.position.y;
            playerRoot.position = position;

            Vector3 toBrute = brute.transform.position - position;
            toBrute.y = 0f;
            if (toBrute.sqrMagnitude > 0.01f)
            {
                playerRoot.rotation = Quaternion.LookRotation(toBrute);
            }

            controller.enabled = true;
        }

        // --- Reporting ---

        private static void Shot(string name)
        {
            if (TakeShots)
            {
                ScreenCapture.CaptureScreenshot("Temp/Brute_" + name + ".png");
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
                // Noted where it happens, so the report shows which scenario set it off.
                if (Errors.Contains(message) == false)
                {
                    Note("console: " + message + " | brute at " + (brute != null ? brute.transform.position.ToString() + " " + brute.transform.localScale + " " + BruteState?.GetType().Name : "?") + " | " + stack);
                }

                Errors.Add(message);
            }
        }
    }
}
