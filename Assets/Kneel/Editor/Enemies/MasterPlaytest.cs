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
    // A scripted fight with The Master's Phase 1 in his hall (Kneel/Enemies/The Master/Playtest, in Play mode in
    // L5_ThroneRoom). Player input goes through the real handlers, the way CLAUDE.md says, and he runs his own states,
    // so what it measures is the fight itself. Between scenarios he's held circling; each scenario forces his next move
    // (MasterP1.ForceNext) and reads the blow the moment it lands on the player: the time into its tell, the sword tip
    // against his front, and what the player's guard did with it. Results go to Temp/MasterPlaytest.txt. Nothing in the
    // scene is saved; the save file is put back after the long intro. Leave TakeShots off for timing.
    public static class MasterPlaytest
    {
        public const string ResultPath = "Temp/MasterPlaytest.txt";

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // The middle of the arena, clear of the pillar lines (x 39 and 69); he faces the doors (south, down-screen).
        private static readonly Vector3 Centre = new Vector3(54f, 9f, 452f);

        // A pillar stump (39, 438; 3 x 4 x 3 m) between him and the player for the Command's cover, and a spot beside it
        // in the open at the same range.
        private static readonly Vector3 CoverCaster = new Vector3(39f, 9f, 445f);
        private static readonly Vector3 BehindPillar = new Vector3(39f, 9f, 435.8f);
        private static readonly Vector3 BesidePillar = new Vector3(42.5f, 9f, 435.8f);

        public static bool TakeShots;

        // Runs only the scenario groups named (comma-separated), when set. "intro_long" plays the first-time reveal
        // and intro (the save is backed up and restored).
        public static string Only;

        // The camera's distance for the run: he's 4.2 m tall and Threads start 14 m out.
        public static float Zoom = 13f;

        private struct Blow
        {
            public float time;
            public float amount;
            public string state;
            public int swing;
            public float since;
            public float tell;
            public float tipAngle;
            public float tipOut;
            public float tipUp;
            public float distance;
            public bool parried;
            public bool blocked;
            public string player;
        }

        private static readonly List<string> Lines = new List<string>();
        private static readonly List<string> Errors = new List<string>();
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static readonly List<Blow> blows = new List<Blow>();

        private static float waitUntil;
        private static Func<bool> waitFor;
        private static float waitForDeadline;

        private static MasterBoss boss;
        private static MasterP1 master;
        private static PlayerCombat combat;
        private static PlayerMovement movement;
        private static Health playerHealth;
        private static Transform playerRoot;
        private static PlayerCameraSettings cameraSettings;
        private static float homeMaxDistance;
        private static int failures;
        private static float worstFrame;
        private static Vector3 playerMove;
        private static Action<float> damageListener;
        private static float lastParry;
        private static float lastBlock;
        private static string saveBackup;
        private static int halfBeats;
        private static int defeats;

        // Where the player aims, when set (the hall's levers); otherwise at his feet.
        private static Vector3? aimAt;

        private static float Now => Time.unscaledTime;

        private static EnemyState State => master.stateMachine.currentState;

        private static MasterMoveState Move => State as MasterMoveState;

        [MenuItem("Kneel/Enemies/The Master/Playtest")]
        public static void Begin()
        {
            if (Application.isPlaying == false)
            {
                Debug.LogWarning("Enter Play mode in L5_ThroneRoom first.");
                return;
            }

            // Statics outlive Play mode in this project.
            boss = null;
            master = null;
            combat = null;
            playerMove = Vector3.zero;
            blows.Clear();
            Lines.Clear();
            Errors.Clear();
            failures = 0;
            worstFrame = 0f;
            halfBeats = 0;
            defeats = 0;
            saveBackup = null;
            Directory.CreateDirectory("Temp/MasterShots");
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
            if (framedName != null && Now >= framedUntil)
            {
                Shot(framedName);
                framedName = null;
                releaseFrameAt = Now + 0.15f;
            }

            if (releaseFrameAt > 0f && Now >= releaseFrameAt)
            {
                releaseFrameAt = 0f;
                UnityEngine.Object.FindAnyObjectByType<PlayerCamera>()?.ClearOverride(0.01f);
            }

            if (Now < waitUntil)
                return;

            if (waitFor != null)
            {
                if (waitFor() == false && Now < waitForDeadline)
                    return;
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
                    waitForDeadline = Now + 15f;
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
            MasterP1.ForceNext = null;

            if (playerHealth != null && damageListener != null)
                playerHealth.OnDamaged -= damageListener;

            // The camera settings asset persists edits made in Play mode.
            if (cameraSettings != null && homeMaxDistance > 0f)
                cameraSettings.maxDistance = homeMaxDistance;

            RestoreSave();

            Note("worst frame " + worstFrame.ToString("F3") + " s");
            Note("console errors: " + Errors.Count);
            foreach (string error in Errors.Distinct().Take(10))
                Note("  " + error);

            string summary = failures == 0 && Errors.Count == 0 ? "PASS" : "FAIL (" + failures + " checks, " + Errors.Count + " errors)";
            Lines.Insert(0, summary);
            File.WriteAllLines(ResultPath, Lines);
        }

        private static bool Wants(string group) => string.IsNullOrEmpty(Only) || Only.Split(',').Contains(group);

        private static IEnumerator Run()
        {
            Setup();
            yield return 0.5f;

            if (Only == "intro_long")
            {
                yield return LongIntro();
                yield break;
            }

            yield return ShortIntro();
            if (master.Engaged == false)
            {
                Note("he never engaged: the rest can't run");
                failures++;
                yield break;
            }

            if (Wants("opening"))
                yield return Opening();

            var scenarios = new (string group, Func<IEnumerator> body)[]
            {
                ("picker", Picker),
                ("decree", DecreeLands),
                ("decree", DecreeReach),
                ("decree", DecreeBlocked),
                ("decree", DecreeParried),
                ("sentence", SentenceLands),
                ("sentence", () => SentenceParry(0)),
                ("sentence", () => SentenceParry(1)),
                ("sentence", () => SentenceParry(2)),
                ("seize", SeizeLands),
                ("seize", () => SeizeThroughGuard(false)),
                ("seize", () => SeizeThroughGuard(true)),
                ("seize", SeizeRoll),
                ("command", CommandNear),
                ("command", CommandFar),
                ("command", CommandCover),
                ("command", CommandBehind),
                ("command", CommandRoll),
                ("threads", ThreadsLand),
                ("threads", ThreadsRoll),
                ("guard", GuardBlocks),
                ("guard", GuardBash),
                ("guard", GuardThrust),
                ("guard", GuardThrustParried),
                ("armour", Armour),
                ("hazard", Hazard),
                ("hall", HallLook),
                ("chandelier", ChandelierLever),
                ("chandelier", ChandelierOnPlayer),
                ("chandelier", ThreadsCutChain),
                ("brazier", BrazierLever),
                ("fire", FireRules),
                ("statues", Statues),
                ("darkness", Darkness),
                ("ash", Ash),
                ("pacing", Pacing),
                ("floor", FloorPanels),
                ("perf", Performance),
                ("half", HalfBeat),
                ("down", Down),
                ("lava", LavaDrop),
            };

            foreach (var scenario in scenarios)
            {
                if (Wants(scenario.group) && master.IsDead == false)
                    yield return scenario.body();
            }
        }

        private static void Setup()
        {
            combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            movement = combat.GetComponent<PlayerMovement>();
            playerHealth = combat.GetComponent<Health>();
            playerRoot = combat.transform;

            ((PlayerControls)typeof(PlayerCombat).GetField("controls", Any).GetValue(combat)).Disable();
            ((PlayerControls)typeof(PlayerMovement).GetField("controls", Any).GetValue(movement)).Disable();

            boss = UnityEngine.Object.FindAnyObjectByType<MasterBoss>();
            master = boss.master;
            foreach (Enemy other in UnityEngine.Object.FindObjectsByType<Enemy>())
            {
                if (other != master)
                    other.gameObject.SetActive(false);
            }

            // An Editor in the background can stop rendering; a build always renders.
            master.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var playerCamera = UnityEngine.Object.FindAnyObjectByType<PlayerCamera>();
            cameraSettings = (PlayerCameraSettings)typeof(PlayerCamera).GetField("settings", Any).GetValue(playerCamera);
            homeMaxDistance = cameraSettings.maxDistance;
            cameraSettings.maxDistance = Mathf.Max(homeMaxDistance, Zoom);
            playerCamera.CurrentDistance = Zoom;

            MasterBoss.ForceShortIntro = Only != "intro_long";
            master.OnHalfHealth += () => halfBeats++;
            master.OnDefeated += () => defeats++;
            combat.OnParry += _ => lastParry = Time.time;

            damageListener = amount =>
            {
                MasterMoveState move = Move;
                Vector3 tip = master.swordTip != null ? master.swordTip.position - master.transform.position : Vector3.zero;
                blows.Add(new Blow
                {
                    time = Time.time,
                    amount = amount,
                    state = move != null ? move.Kind.ToString() : State.GetType().Name,
                    swing = move != null ? move.Swing : -1,
                    since = move != null ? move.SinceStart : -1f,
                    tell = move != null ? move.Tell : -1f,
                    tipAngle = Vector3.SignedAngle(master.transform.forward, Flat(tip), Vector3.up),
                    tipOut = Flat(tip).magnitude,
                    tipUp = tip.y,
                    distance = Distance(),
                    blocked = combat.LastBlockHitTime >= Time.time - 0.001f,
                    player = combat.State + " " + ((float)typeof(PlayerCombat).GetField("stateTime", Any).GetValue(combat)).ToString("F2") + " s",
                });
            };
            playerHealth.OnDamaged += damageListener;

            Note("fighting " + master.name + " in " + master.gameObject.scene.name + ", agent " + UnityEngine.AI.NavMesh.GetSettingsNameFromID(master.agent.agentTypeID) + ", camera yaw " + Camera.main.transform.eulerAngles.y.ToString("F0"));
            Note("tells: Decree " + S.decreeTell + ", Sentence " + S.sentenceTell1 + "/" + S.sentenceTell2 + "/" + S.sentenceTell3 + ", Seize " + S.seizeTell + ", Command " + S.commandTellFirst + " then " + S.commandTell + ", Threads " + S.threadsTell + ", Thrust " + S.thrustTell);
            Note("reaches: Decree " + S.decreeReach + ", Sentence " + S.sentenceReach + ", Thrust " + S.thrustReach + " m; contacts (clip s): Decree " + master.decreeContact + ", Sentence " + string.Join("/", master.sentenceContacts) + ", Seize " + master.seizeContact + ", Command " + master.commandRelease + ", Threads " + master.threadsRelease + ", Thrust " + master.thrustContact);
        }

        private static MasterSettings S => master.settings;

        // --- Intros ---

        private static IEnumerator ShortIntro()
        {
            Note("--- the retry intro");
            PlaceAbsolute(new Vector3(54f, 9f, 429f), Vector3.forward);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
                Press("OnDrawPressed");
            yield return 0.6f;
            Check("he waits on the throne", boss.Current == MasterBoss.Beat.Seated && State == master.seatedState, boss.Current + ", " + State.GetType().Name);

            float crossed = Time.time;
            PlaceAbsolute(new Vector3(54f, 9f, 435f), Vector3.forward);
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Intro);
            yield return 0.3f;
            Check("the seal kindles behind the player", boss.seal != null && boss.seal.Kindled, "");
            Check("he's at the dais foot, untouchable", master.Untouchable && Vector3.Distance(Flat(master.transform.position), Flat(boss.daisFoot.position)) < 0.5f, master.transform.position.ToString("F1"));
            float before = master.health.Current;
            master.TakeHit(new DamageInfo { amount = 50f, poiseDamage = 50f, direction = Vector3.forward, source = combat.gameObject });
            Check("a hit in the intro passes through him", master.health.Current == before, "");
            Shot("Intro_Short");

            yield return (Func<bool>)(() => master.Engaged);
            Check("the fight starts after the 4 s retry intro", Mathf.Abs(Time.time - crossed - S.retryIntroTime) < 0.4f, (Time.time - crossed).ToString("F2") + " s");
            Check("he's touchable once the bar is full", master.Untouchable == false, "");
        }

        // The first time per save: the reveal at the doors, then the long intro. Only with Only = "intro_long".
        private static IEnumerator LongIntro()
        {
            BackupSave();
            boss.Seat();
            Note("--- the reveal and the long intro (save backed up and cleared)");
            PlaceAbsolute(new Vector3(54f, 9f, 405f), Vector3.forward);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
                Press("OnDrawPressed");
            yield return 1f;

            float start = Time.time;
            PlaceAbsolute(new Vector3(54f, 9f, 409f), Vector3.forward);
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Reveal);
            for (int i = 0; i < 6; i++)
            {
                yield return 1f;
                Shot("Reveal_" + i);
            }

            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Seated);
            Check("the reveal takes about 6 s", Mathf.Abs(Time.time - start - boss.revealTime) < 0.5f, (Time.time - start).ToString("F2") + " s");

            PlaceAbsolute(new Vector3(54f, 9f, 433.5f), Vector3.forward);
            start = Time.time;
            yield return (Func<bool>)(() => boss.Current == MasterBoss.Beat.Intro);
            float pulledFrom = playerRoot.position.z;
            playerMove = Vector3.forward;
            for (int i = 0; i < 13; i++)
            {
                yield return i == 0 ? 0.5f : 1f;
                Shot("Intro_" + i.ToString("00"));
                if (i == 0)
                {
                    Note("  0.5 s in: palm " + master.anim.GetBool("PalmUp") + ", player restricted " + combat.IsRestricted);
                }

                if (i == 1)
                {
                    float speed = (playerRoot.position.z - pulledFrom) / 1.5f;
                    Check("the pull holds the player to a slow walk", speed < 1.2f, speed.ToString("F2") + " m/s");
                    playerMove = Vector3.zero;
                }

                if (i == 3 || i == 8)
                {
                    Camera cam = Camera.main;
                    Transform head = combat.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
                    Note("  shot at " + (i + 0.5f) + " s: camera " + cam.transform.position.ToString("F1") + " fov " + cam.fieldOfView.ToString("F0") + ", Kael's head " + head.position.ToString("F1") + " at viewport " + cam.WorldToViewportPoint(head.position).ToString("F2") + ", the Master at " + cam.WorldToViewportPoint(master.transform.position + Vector3.up * 2.5f).ToString("F2"));
                }

                if (master.Engaged)
                    break;
            }

            yield return (Func<bool>)(() => master.Engaged);
            Check("the long intro takes about 12 s", Mathf.Abs(Time.time - start - 12f) < 1.2f, (Time.time - start).ToString("F2") + " s, he stands at " + master.transform.position.ToString("F1"));
            Check("it marks the save (retries play the short one)", Kneel.Core.SaveFlags.Has("L5.MasterMet") && Kneel.Core.SaveFlags.Has("L5.Reveal"), "");
            yield return 2.5f;
            Shot("Intro_Fight");
        }

        private static void BackupSave()
        {
            string path = Path.Combine(Application.persistentDataPath, "save.json");
            saveBackup = File.Exists(path) ? File.ReadAllText(path) : "";
            File.WriteAllText("Temp/MasterPlaytest_save_backup.json", saveBackup);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void RestoreSave()
        {
            if (saveBackup == null)
                return;
            string path = Path.Combine(Application.persistentDataPath, "save.json");
            if (saveBackup.Length > 0)
                File.WriteAllText(path, saveBackup);
            else if (File.Exists(path))
                File.Delete(path);
            saveBackup = null;
        }

        // --- The fixed opening, under his own AI ---

        private static IEnumerator Opening()
        {
            Note("--- the fixed opening, the player standing still 6 m away");
            yield return ResetFight(Centre);
            typeof(MasterP1).GetField("openingIndex", Any).SetValue(master, 0);
            typeof(MasterP1).GetField("firstCommandDone", Any).SetValue(master, false);
            PlaceFront(6f);
            master.circleState.Hold(0f);
            var seen = new List<string>();
            float start = Now;
            MasterMoveState last = null;
            float kneelEnd = -1f;
            float followupLanded = -1f;
            while (Now - start < 40f && seen.Count < 5)
            {
                if (Move != null && Move != last)
                {
                    last = Move;
                    seen.Add(Move.Kind.ToString());
                    Note("  " + (Now - start).ToString("F1") + " s: " + Move.Kind + " from " + Distance().ToString("F1") + " m");
                }
                else if (Move == null)
                {
                    last = null;
                }

                if (combat.IsKneeling)
                    kneelEnd = Time.time;
                if (seen.Count == 3 && followupLanded < 0f && blows.Count > 0 && blows[blows.Count - 1].state == "Decree" && blows[blows.Count - 1].time > kneelEnd && kneelEnd > 0f)
                    followupLanded = blows[blows.Count - 1].time;

                // He keeps to his range: the player follows him at 6 m.
                if (Move == null && combat.IsKneeling == false && Distance() > 7f)
                    PlaceFront(6f);
                yield return 0.02f;
            }

            Check("Decree, Command, its follow-up Decree, Sentence, Seize", string.Join(" ", seen) == "Decree Command Decree Sentence Seize", string.Join(" ", seen));
            if (followupLanded > 0f)
                Check("the follow-up lands after the player can move", followupLanded - kneelEnd > 0.1f && followupLanded - kneelEnd < 1f, (followupLanded - kneelEnd).ToString("F2") + " s after the kneel");
            else
                Note("  the follow-up Decree didn't land on the player");
        }

        // His picks by range band: no move three times running, Threads only beyond 12 m.
        private static IEnumerator Picker()
        {
            Note("--- his picks by range band");
            yield return ResetFight(Centre);
            FieldInfo openingField = typeof(MasterP1).GetField("openingIndex", Any);
            var history = (List<MasterP1.MoveKind>)typeof(MasterP1).GetField("history", Any).GetValue(master);
            openingField.SetValue(master, 99);
            foreach (float distance in new[] { 3f, 7f, 10.5f, 14f })
            {
                PlaceFront(distance);
                history.Clear();
                var picks = new List<MasterP1.MoveKind>();
                for (int i = 0; i < 60; i++)
                {
                    MasterP1.MoveKind pick = master.NextMove();
                    picks.Add(pick);
                    history.Add(pick);
                }

                int threeRunning = Enumerable.Range(2, picks.Count - 2).Count(i => picks[i] == picks[i - 1] && picks[i] == picks[i - 2]);
                string counts = string.Join(", ", picks.GroupBy(p => p).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count()));
                Check("at " + distance + " m: never three running", threeRunning == 0, counts);
                if (distance < 12f)
                    Check("at " + distance + " m: no Threads", picks.Contains(MasterP1.MoveKind.Threads) == false, "");
            }

            history.Clear();
            openingField.SetValue(master, 99);
        }

        // --- Decree ---

        private static IEnumerator DecreeLands()
        {
            Note("--- Decree, taken standing");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            yield return Filmstrip("Decree", S.decreeTell);
            ReportBlow("Decree", S.decreeTell, S.decreeReach, S.decreeDamage);
            yield return WaitMoveEnds();
        }

        private static IEnumerator DecreeReach()
        {
            Note("--- Decree's reach (" + S.decreeReach + " m)");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= S.decreeTell - 0.15f);
            PlaceFront(S.decreeReach + 0.8f, keepFacing: true);
            int before = blows.Count;
            yield return WaitMoveEnds();
            Check("a step out of reach at the last moment: no hit", blows.Count == before, (blows.Count - before) + " hits");

            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= S.decreeTell - 0.15f);
            PlaceFront(S.decreeReach - 0.3f, keepFacing: true);
            before = blows.Count;
            yield return WaitMoveEnds();
            Check("just inside reach: it lands", blows.Count == before + 1, (blows.Count - before) + " hits");
        }

        private static IEnumerator DecreeBlocked()
        {
            Note("--- Decree into a held block");
            yield return ResetFight(Centre);
            SetBlock(true);
            yield return 0.4f;
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            int before = blows.Count;
            yield return WaitMoveEnds();
            SetBlock(false);
            Check("the block takes it (glint moves can be blocked)", blows.Count == before + 1 && blows[blows.Count - 1].amount < S.decreeDamage * 0.5f, blows.Count > before ? blows[blows.Count - 1].amount.ToString("F1") + " damage" : "no hit");
        }

        private static IEnumerator DecreeParried()
        {
            Note("--- Decree parried");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            yield return ParryAt(S.decreeTell);
            Check("the parry catches it", lastParry > Time.time - 0.5f, "");
            Check("he drops to one knee", State == master.parriedState, State.GetType().Name);
            float kneeled = Time.time;
            Shot("Decree_Parried");

            // A heavy pressed at once lands inside the kneel, critical.
            PlaceFront(2.3f, keepFacing: true);
            float health = master.health.Current;
            Press("OnHeavyPressed");
            yield return (Func<bool>)(() => master.health.Current < health || State != master.parriedState);
            float damage = health - master.health.Current;
            Check("a heavy in the kneel is critical", Mathf.Abs(damage - 35f * 1.5f) < 1f, damage.ToString("F1") + " damage, " + (Time.time - kneeled).ToString("F2") + " s into the kneel");
            Shot("Decree_Riposte");
            yield return (Func<bool>)(() => State != master.parriedState);
            Check("the kneel lasts " + S.parryKneelTime + " s", Mathf.Abs(Time.time - kneeled - S.parryKneelTime) < 0.2f, (Time.time - kneeled).ToString("F2") + " s");
        }

        // --- Sentence ---

        private static IEnumerator SentenceLands()
        {
            Note("--- Sentence, taken standing");
            yield return ResetFight(Centre);
            int before = blows.Count;
            yield return Start(MasterP1.MoveKind.Sentence, 3.5f);
            yield return Filmstrip("Sentence1", S.sentenceTell1);
            yield return WaitMoveEnds();
            float[] tells = { S.sentenceTell1, S.sentenceTell2, S.sentenceTell3 };
            var landed = blows.Skip(before).ToList();
            Check("all three sweeps land", landed.Count == 3, landed.Count + " hits");
            for (int i = 0; i < landed.Count; i++)
                ReportBlow("Sentence " + (landed[i].swing + 1), tells[Mathf.Clamp(landed[i].swing, 0, 2)], S.sentenceReach, S.sentenceDamage, landed[i]);
        }

        // Parries sweep `index` (blocking the ones before): 1 deflects and the string goes on, 2 deflects and cancels the
        // third, 3 kneels him.
        private static IEnumerator SentenceParry(int index)
        {
            Note("--- Sentence, parrying sweep " + (index + 1));
            yield return ResetFight(Centre);
            if (index > 0)
            {
                SetBlock(true);
                yield return 0.3f;
            }

            yield return Start(MasterP1.MoveKind.Sentence, 3.5f);
            yield return (Func<bool>)(() => Move == null || Move.Swing == index);
            SetBlock(false);
            int before = blows.Count;
            float[] tells = { S.sentenceTell1, S.sentenceTell2, S.sentenceTell3 };
            yield return ParryAt(tells[index]);
            bool caught = lastParry > Time.time - 0.5f;
            Check("the parry catches sweep " + (index + 1), caught, "");
            Shot("Sentence_Parry" + (index + 1));
            if (index == 0)
            {
                yield return (Func<bool>)(() => Move == null || Move.Swing == 1);
                Check("the string goes on into sweep 2", Move != null && Move.Swing == 1, State.GetType().Name);
                yield return WaitMoveEnds();
                Check("sweeps 2 and 3 still land", blows.Count - before == 2, (blows.Count - before) + " hits");
            }
            else if (index == 1)
            {
                yield return WaitMoveEnds();
                Check("the third is cancelled", blows.Count == before, (blows.Count - before) + " hits after the parry");
                Check("he isn't knelt", State != master.parriedState, State.GetType().Name);
            }
            else
            {
                Check("the third's parry kneels him", State == master.parriedState, State.GetType().Name);
                yield return (Func<bool>)(() => State != master.parriedState);
            }
        }

        // --- Seize ---

        private static IEnumerator SeizeLands()
        {
            Note("--- Seize, taken standing");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Seize, 4.5f);
            float from = Distance();
            yield return Filmstrip("Seize", S.seizeTell);
            ReportBlow("Seize", S.seizeTell, 99f, S.seizeDamage);
            yield return 0.1f;
            Check("the grab kneels the player", combat.IsKneeling, combat.State.ToString());
            Note("  he lunged from " + from.ToString("F1") + " m; the player ends " + Distance().ToString("F1") + " m from him");
            yield return WaitMoveEnds();
        }

        private static IEnumerator SeizeThroughGuard(bool parry)
        {
            Note("--- Seize into a " + (parry ? "parry" : "held block"));
            yield return ResetFight(Centre);
            if (parry == false)
            {
                SetBlock(true);
                yield return 0.3f;
            }

            yield return Start(MasterP1.MoveKind.Seize, 4.5f);
            int before = blows.Count;
            float parriesBefore = lastParry;
            if (parry)
                yield return ParryAt(S.seizeTell);
            yield return (Func<bool>)(() => blows.Count > before || Move == null || Move.Landed);
            yield return 0.05f;
            SetBlock(false);
            Check("the grab goes through the " + (parry ? "parry" : "block"), blows.Count == before + 1 && Mathf.Approximately(blows[blows.Count - 1].amount, S.seizeDamage) && lastParry == parriesBefore, blows.Count > before ? blows[blows.Count - 1].amount.ToString("F0") + " damage" : "no hit");
            Check("and kneels the player", combat.IsKneeling, combat.State.ToString());
            yield return WaitMoveEnds();
        }

        private static IEnumerator SeizeRoll()
        {
            Note("--- Seize, rolled");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Seize, 4.5f);
            int before = blows.Count;
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= S.seizeTell - 0.2f);
            yield return Roll(master.transform.right);
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            yield return 0.1f;
            playerMove = Vector3.zero;
            Check("a roll as he lunges clears the grab", blows.Count == before, (blows.Count - before) + " hits");
            yield return WaitMoveEnds();
        }

        // --- Command ---

        private static IEnumerator CommandNear()
        {
            Note("--- the Command in the open, 3.5 m away");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Command, 3.5f);
            float tell = Move.Tell;
            yield return Filmstrip("Command", tell);
            ReportBlow("Command", tell, 99f, S.commandDamage);
            yield return FollowUp();
        }

        private static IEnumerator CommandFar()
        {
            Note("--- the Command in the open, 10 m away");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Command, 10f);
            float tell = Move.Tell;
            yield return (Func<bool>)(() => Move == null || Move.Landed || blows.Count > 0 && blows[blows.Count - 1].time > Time.time - 0.05f);
            ReportBlow("Command", tell, 99f, S.commandDamage);
            yield return FollowUp();
        }

        // After a Command lands: the player kneels for commandKneel, then a Decree lands after they can move again.
        private static IEnumerator FollowUp()
        {
            yield return (Func<bool>)(() => combat.IsKneeling);
            float knelt = blows.Count > 0 && blows[blows.Count - 1].state == "Command" ? blows[blows.Count - 1].time : Time.time;
            yield return (Func<bool>)(() => combat.IsKneeling == false);
            float rose = Time.time;
            Check("the player kneels " + S.commandKneel + " s (+ the get-up)", rose - knelt > S.commandKneel - 0.1f && rose - knelt < S.commandKneel + 0.8f, (rose - knelt).ToString("F2") + " s");
            int before = blows.Count;
            yield return (Func<bool>)(() => blows.Count > before || (Move == null && State != master.approachState));
            if (blows.Count > before)
            {
                Blow follow = blows[blows.Count - 1];
                Check("the follow-up Decree lands after the player can move", follow.state == "Decree" && follow.time > rose + 0.1f, (follow.time - rose).ToString("F2") + " s after the get-up, from " + follow.distance.ToString("F1") + " m");
            }
            else
            {
                Check("a follow-up Decree comes", false, State.GetType().Name);
            }

            yield return WaitMoveEnds();
        }

        private static IEnumerator CommandCover()
        {
            Note("--- the Command against the pillar stump at (39, 438)");
            yield return ResetFight(CoverCaster);
            Check("he stands on his NavMesh by the pillar line", master.agent.isOnNavMesh, master.transform.position.ToString("F1"));
            PlaceAbsolute(BehindPillar, Vector3.forward);
            yield return 0.3f;
            int before = blows.Count;
            yield return Start(MasterP1.MoveKind.Command, -1f);
            float tell = Move != null ? Move.Tell : 0f;
            yield return (Func<bool>)(() => Move == null || Move.SinceStart > tell * 0.6f);
            Shot("Command_Cover");
            bool sight = BossTell.LineOfSight(master.bossTell.PalmLightPosition, playerRoot.position + Vector3.up * 1.2f, master.transform, playerRoot);
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            Check("in the pillar's shadow: no hit", blows.Count == before && combat.IsKneeling == false, "palm at " + master.bossTell.PalmLightPosition.ToString("F1") + ", line of sight " + sight);
            yield return WaitMoveEnds();

            yield return ResetFight(CoverCaster);
            PlaceAbsolute(BesidePillar, Vector3.forward);
            yield return 0.3f;
            before = blows.Count;
            yield return Start(MasterP1.MoveKind.Command, -1f);
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            yield return 0.05f;
            Check("3.5 m to the side, in the open: hit", blows.Count == before + 1 && combat.IsKneeling, (blows.Count - before) + " hits");
            yield return (Func<bool>)(() => combat.IsKneeling == false);
            yield return WaitMoveEnds();
        }

        private static IEnumerator CommandBehind()
        {
            Note("--- the Command with the player behind him");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Command, 4f);
            int before = blows.Count;
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= Move.Tell - 0.35f);
            PlaceAbsolute(master.transform.position - master.transform.forward * 4f, master.transform.forward);
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            Check("behind him (outside his 160 deg): no hit", blows.Count == before, (blows.Count - before) + " hits");
            yield return WaitMoveEnds();
        }

        private static IEnumerator CommandRoll()
        {
            Note("--- the Command, rolled");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Command, 6f);
            int before = blows.Count;
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= Move.Tell - 0.2f);
            yield return Roll(master.transform.right);
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            yield return 0.05f;
            playerMove = Vector3.zero;
            Check("a roll doesn't escape the Command (only cover does)", blows.Count == before + 1 && combat.IsKneeling, (blows.Count - before) + " hits, " + combat.State);
            yield return (Func<bool>)(() => combat.IsKneeling == false);
            yield return WaitMoveEnds();
        }

        // --- Threads ---

        private static IEnumerator ThreadsLand()
        {
            Note("--- Threads at 13 m, taken standing (camera at 17 m, the P2 framing)");
            yield return ResetFight(Centre);
            yield return FarCamera(true);
            yield return Start(MasterP1.MoveKind.Threads, 13f);
            float startedAt = Distance();
            int before = blows.Count;
            float start = Time.time;
            yield return Filmstrip("Threads", S.threadsTell);
            yield return (Func<bool>)(() => blows.Count > before);

            // Off the line before its fire catches (it trails the head by 3 m).
            Vector3 hitAt = playerRoot.position;
            Vector3 casterAt = master.transform.position;
            // Into the gap between the centre line and a side one (they fan 12 degrees apart).
            PlaceAbsolute(playerRoot.position + master.transform.right * 1.4f, master.transform.position - playerRoot.position);
            yield return 1f;
            var hits = blows.Skip(before).ToList();
            Check("one thread hits, once", hits.Count == 1 && Mathf.Approximately(hits[0].amount, S.threadDamage), hits.Count + " hits: " + string.Join(", ", hits.Select(h => h.amount.ToString("F1"))));
            Check("it leaves fire on the floor where it ran", FloorFire.IsBurning(hitAt) && FloorFire.IsBurning(casterAt + (hitAt - casterAt) * 0.5f), FloorFire.Instance != null ? FloorFire.Instance.Live.Count + " patches" : "no FloorFire");
            if (hits.Count > 0)
            {
                float expected = S.threadsTell + (startedAt - 1.2f) / S.threadSpeed;
                Check("it reaches the player at the line's speed", Mathf.Abs(hits[0].time - start - expected) < 0.25f, (hits[0].time - start).ToString("F2") + " s after the tell began (expected " + expected.ToString("F2") + ")");
            }

            yield return WaitMoveEnds();
            yield return FarCamera(false);
        }

        private static IEnumerator ThreadsRoll()
        {
            Note("--- Threads, rolled over");
            yield return ResetFight(Centre);
            yield return FarCamera(true);
            yield return Start(MasterP1.MoveKind.Threads, 9.5f);
            int before = blows.Count;
            // The head hits within width / 2 + 0.3 m of the player, so it touches them that much early.
            float arrive = Time.time + S.threadsTell + (Distance() - 1.2f - S.threadWidth * 0.5f - 0.3f) / S.threadSpeed;
            // A roll pressed now is invulnerable from about 0.12 s (a frame, then dodgeInvulnerableStart).
            yield return (Func<bool>)(() => Time.time >= arrive - 0.25f);
            // Into the line's head: the i-frames carry the player through it.
            float rolled = Time.time;
            yield return Roll(master.transform.right);
            yield return 1.2f;
            playerMove = Vector3.zero;
            Check("a roll across the line clears it, and its fire", blows.Count == before, (blows.Count - before) + " hits" + (blows.Count > before ? ", " + (blows[blows.Count - 1].time - rolled).ToString("F2") + " s after the roll was pressed, the player " + blows[blows.Count - 1].player + ", " + blows[blows.Count - 1].distance.ToString("F1") + " m from him; head due " + (arrive - rolled).ToString("F2") + " s after" : ""));
            yield return WaitMoveEnds();
            yield return FarCamera(false);
        }

        // How far up-screen of the player he stays in frame, at the camera's current distance.
        private static float FrameReach()
        {
            for (float d = 4f; d < 30f; d += 0.5f)
            {
                Vector3 at = playerRoot.position + Vector3.forward * d + Vector3.up * 2f;
                Vector3 v = Camera.main.WorldToViewportPoint(at);
                if (v.z <= 0f || v.y > 1.05f)
                    return d - 0.5f;
            }

            return 30f;
        }

        private static IEnumerator FarCamera(bool far)
        {
            var playerCamera = UnityEngine.Object.FindAnyObjectByType<PlayerCamera>();
            if (far)
            {
                cameraSettings.maxDistance = Mathf.Max(cameraSettings.maxDistance, 17f);
                playerCamera.CurrentDistance = 17f;
            }
            else
            {
                playerCamera.CurrentDistance = Zoom;
            }

            yield return 1.5f;
            Note("  camera at " + playerCamera.CurrentDistance.ToString("F1") + " m keeps him in frame up to " + FrameReach().ToString("F1") + " m up-screen");
        }

        // --- Regal Guard ---

        private static IEnumerator GuardBlocks()
        {
            Note("--- the Regal Guard against a light and a heavy");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Guard, 7f);
            yield return (Func<bool>)(() => Move == null || Move.Guarding);
            Shot("Guard");
            PlaceFront(2.4f, keepFacing: true);
            float health = master.health.Current;
            Press("OnStrikePressed");
            yield return 0.65f;
            Check("a frontal light stops on the blade", master.health.Current == health && Move != null, (health - master.health.Current).ToString("F1") + " damage, " + State.GetType().Name);
            yield return WaitMoveEnds();
        }

        private static IEnumerator GuardBash()
        {
            Note("--- the Regal Guard, bashed");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Guard, 7f);
            yield return (Func<bool>)(() => Move == null || Move.Guarding);
            yield return 0.3f;
            PlaceFront(1.8f, keepFacing: true);
            SetBlock(true);
            yield return 0.15f;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => State == master.staggerState || Move == null);
            SetBlock(false);
            Check("the bash breaks the guard into a stagger", State == master.staggerState, State.GetType().Name + (Move != null ? " guarding " + Move.Guarding : ""));
            float broke = Time.time;
            Shot("Guard_Broken");
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            float health = master.health.Current;
            PlaceFront(2.3f, keepFacing: true);
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => master.health.Current < health || State != master.staggerState);
            Check("a light in it is critical", Mathf.Abs(health - master.health.Current - 12f * 1.5f) < 0.6f, (health - master.health.Current).ToString("F1") + " damage");
            yield return (Func<bool>)(() => State != master.staggerState);
            Check("the stagger lasts " + S.guardBrokenStagger + " s", Mathf.Abs(Time.time - broke - S.guardBrokenStagger) < 0.25f, (Time.time - broke).ToString("F2") + " s");
        }

        private static IEnumerator GuardThrust()
        {
            Note("--- the Guard walks in, then the Thrust");
            yield return ResetFight(Centre);
            int before = blows.Count;
            yield return Start(MasterP1.MoveKind.Guard, 7f);
            float start = Time.time;
            Vector3 from = Flat(master.transform.position);
            yield return (Func<bool>)(() => Move == null || Move.Thrusting);
            Note("  the thrust starts " + (Time.time - start).ToString("F2") + " s in, after walking " + Vector3.Distance(from, Flat(master.transform.position)).ToString("F2") + " m");
            yield return Filmstrip("Thrust", S.thrustTell);
            yield return WaitMoveEnds();
            var hits = blows.Skip(before).ToList();
            Check("the thrust lands", hits.Count == 1, hits.Count + " hits");
            if (hits.Count == 1)
                ReportBlow("Thrust", -1f, S.thrustReach, S.thrustDamage, hits[0]);
        }

        private static IEnumerator GuardThrustParried()
        {
            Note("--- the Thrust parried");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Guard, 3.2f);
            yield return (Func<bool>)(() => Move == null || Move.Thrusting);
            float thrustStart = Time.time;
            yield return ParryAt(S.thrustTell);
            Check("the parry kneels him", lastParry > thrustStart && State == master.parriedState, State.GetType().Name);
            yield return (Func<bool>)(() => State != master.parriedState);
        }

        // --- Armour, flinch, poise ---

        private static IEnumerator Armour()
        {
            Note("--- his armour in a wind-up");
            yield return ResetFight(Centre);
            PlaceFront(2.3f);
            yield return 0.1f;
            float health = master.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => master.health.Current < health);
            float open = health - master.health.Current;
            yield return 0.05f;
            Check("a light outside a move flinches him", State == master.flinchState, State.GetType().Name + ", " + open.ToString("F1") + " damage");
            yield return 1.2f;

            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 2.3f);
            health = master.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => master.health.Current < health || Move == null || Move.Landed);
            float armoured = health - master.health.Current;
            Check("in the Decree's wind-up: " + (S.windupDamageMultiplier * 100f) + "% damage, no flinch", Mathf.Abs(armoured - open * S.windupDamageMultiplier) < 0.6f && Move != null && Move.Kind == MasterP1.MoveKind.Decree, armoured.ToString("F1") + " of " + open.ToString("F1") + ", " + State.GetType().Name);
            yield return WaitMoveEnds();

            // A poise break in a move waits for the blow to end, then staggers him.
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 2.3f);
            master.poise.ApplyDamage(master.poise.maxPoise - 1f);
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            Check("a poise break in the wind-up doesn't stop the blow", Move != null && Move.Landed, State.GetType().Name);
            yield return (Func<bool>)(() => Move == null);
            Check("it staggers him once the blow ends", State == master.staggerState, State.GetType().Name);
            yield return (Func<bool>)(() => State != master.staggerState);
        }

        // A hazard (chandelier, brazier) breaks a wind-up. The hazards are Stage 5; this drives their call.
        private static IEnumerator Hazard()
        {
            Note("--- a hazard in his wind-up (the call the chandeliers and braziers make)");
            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            yield return 0.3f;
            float health = master.health.Current;
            int before = blows.Count;
            master.HazardHit(60f, 2.5f);
            float hit = Time.time;
            Check("a chandelier (60, 2.5 s) breaks the Decree", State == master.staggerState && Mathf.Approximately(health - master.health.Current, 60f), State.GetType().Name + ", " + (health - master.health.Current) + " damage");
            yield return (Func<bool>)(() => State != master.staggerState);
            Check("2.5 s stagger, and the Decree never lands", Mathf.Abs(Time.time - hit - 2.5f) < 0.2f && blows.Count == before, (Time.time - hit).ToString("F2") + " s, " + (blows.Count - before) + " hits");

            yield return ResetFight(Centre);
            yield return Start(MasterP1.MoveKind.Seize, 4.5f);
            yield return 0.3f;
            master.HazardHit(40f, 1.5f);
            hit = Time.time;
            yield return (Func<bool>)(() => State != master.staggerState);
            Check("a brazier (40, 1.5 s) breaks the Seize", Mathf.Abs(Time.time - hit - 1.5f) < 0.2f, (Time.time - hit).ToString("F2") + " s");
        }

        // --- The hall (Stage 5) ---

        private static BossArenaDirector Director => UnityEngine.Object.FindAnyObjectByType<BossArenaDirector>();

        private static HallFloor Floor => UnityEngine.Object.FindAnyObjectByType<HallFloor>();

        // Wide shots of the hall's levers and the statues, for the checkpoint.
        private static IEnumerator HallLook()
        {
            Note("--- the hall's levers (shots only)");
            yield return ResetFight(Centre);
            yield return FarCamera(true);
            PlaceAbsolute(new Vector3(54f, 9f, 446f), Vector3.forward);
            master.agent.Warp(new Vector3(54f, 9f, 462f));
            yield return 1.5f;
            Framed("Hall_Overview", new Vector3(54f, 9f, 450f), 24f, 22f);
            PlaceAbsolute(new Vector3(37f, 9f, 446f), Vector3.forward);
            yield return 1.5f;
            Framed("Hall_West", new Vector3(39f, 10f, 447f), 9f, 6f, -60f);
            yield return FarCamera(false);
        }

        // The player strikes a winch: the chain snaps and the chandelier lands on him 1.0 s later.
        private static IEnumerator ChandelierLever()
        {
            Note("--- a chandelier, from its winch");
            ChandelierDrop chandelier = ChandelierDrop.All.OrderBy(c => Mathf.Abs(c.transform.position.z - 450f)).First();
            Vector3 under = new Vector3(chandelier.transform.position.x, 9f, chandelier.transform.position.z);
            yield return ResetFight(under);
            master.circleState.Hold(9999f);
            Vector3 winch = chandelier.winch.transform.position;
            Vector3 side = winch.x < 54f ? Vector3.left : Vector3.right;
            PlaceAbsolute(winch - side * 1.7f, side);
            aimAt = winch;
            yield return 0.4f;
            float health = master.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => chandelier.Current != ChandelierDrop.State.Hanging);
            float struck = Time.time;
            Check("a blow on the winch snaps the chain", chandelier.Current == ChandelierDrop.State.Falling, chandelier.Current.ToString());
            yield return 0.5f;
            Framed("Chandelier_Falling", under + Vector3.up * 5f, 14f, 6f);
            yield return (Func<bool>)(() => chandelier.Current == ChandelierDrop.State.Fallen);
            Check("it lands 1.0 s after the blow", Mathf.Abs(Time.time - struck - 1f) < 0.08f, (Time.time - struck).ToString("F2") + " s");
            yield return 0.05f;
            Check("under it, he takes 60 (+ 25 if he stands in the burning wreck) and a 2.5 s stagger", (Mathf.Abs(health - master.health.Current - 60f) < 1f || Mathf.Abs(health - master.health.Current - 85f) < 1f) && State == master.staggerState, (health - master.health.Current).ToString("F0") + " damage, " + State.GetType().Name + ", " + HallActors.FlatDistance(master.transform.position, under).ToString("F1") + " m from its centre");
            float staggered = Time.time;
            yield return 0.4f;
            Framed("Chandelier_Fallen", under, 10f, 7f);
            Check("the wreck burns and is cover", FloorFire.IsBurning(under) && chandelier.wreck.enabled && BossTell.LineOfSight(under + new Vector3(0f, 0.6f, -6f), under + new Vector3(0f, 0.6f, 6f), null, null) == false, "");
            yield return (Func<bool>)(() => State != master.staggerState);
            Check("the stagger lasts 2.5 s", Mathf.Abs(Time.time - staggered - 2.45f) < 0.25f, (Time.time - staggered + 0.05f).ToString("F2") + " s");
            yield return 6.2f;
            Check("the fire goes out after 6 s, the wreck stays", FloorFire.IsBurning(under) == false && chandelier.wreck.enabled, "");
        }

        private static IEnumerator ChandelierOnPlayer()
        {
            Note("--- a chandelier on the player");
            ChandelierDrop chandelier = ChandelierDrop.All.OrderBy(c => Mathf.Abs(c.transform.position.z - 436f)).First();
            Vector3 under = new Vector3(chandelier.transform.position.x, 9f, chandelier.transform.position.z);
            yield return ResetFight(Centre + Vector3.forward * 8f);
            PlaceAbsolute(under + Vector3.right * 1f, Vector3.forward);
            yield return 0.3f;
            int before = blows.Count;
            chandelier.Trigger();
            yield return (Func<bool>)(() => chandelier.Current == ChandelierDrop.State.Fallen);
            yield return 0.05f;
            var hits = blows.Skip(before).ToList();
            Check("it does 40 to the player under it", hits.Count >= 1 && Mathf.Abs(hits[0].amount - 40f) < 0.5f, string.Join(", ", hits.Select(h => h.amount.ToString("F0"))));
        }

        // A Threads line crossing a winch snaps its chain by accident.
        private static IEnumerator ThreadsCutChain()
        {
            Note("--- his Threads across a winch");
            yield return ResetFight(Centre);
            ChandelierDrop chandelier = ChandelierDrop.All.OrderBy(c => c.winch.transform.position.x).First();
            Vector3 winch = chandelier.winch.transform.position;
            Vector3 from = winch + new Vector3(6f, 0f, -6f);
            ThreadLine.Launch(master, from, winch - from, S.threadSpeed, 12f, S.threadWidth, 0f);
            yield return (Func<bool>)(() => chandelier.Current != ChandelierDrop.State.Hanging);
            Check("the thread snaps the chain", chandelier.Current != ChandelierDrop.State.Hanging, chandelier.Current.ToString());
            yield return 1.2f;
        }

        // The player strikes a brazier from the aisle: it rocks 1 s, goes over into the arena and spills on him.
        private static IEnumerator BrazierLever()
        {
            Note("--- a brazier");
            BrazierTopple brazier = BrazierTopple.All.OrderBy(b => b.transform.position.x).ThenBy(b => b.transform.position.z).First();
            Vector3 at = brazier.transform.position;
            Vector3 spill = at + Vector3.right * brazier.spillDistance;
            yield return ResetFight(spill);
            master.circleState.Hold(9999f);
            PlaceAbsolute(at + Vector3.left * 1.6f, Vector3.right);
            aimAt = at + Vector3.up;
            yield return 0.4f;
            float health = master.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => brazier.Current != BrazierTopple.State.Standing);
            float struck = Time.time;
            master.agent.Warp(spill);
            yield return 0.5f;
            Framed("Brazier_Rocking", at + Vector3.up, 6f, 3.5f, 60f);
            yield return (Func<bool>)(() => brazier.Current == BrazierTopple.State.Fallen);
            Check("it rocks 1.0 s, then goes over away from the blow", Mathf.Abs(Time.time - struck - brazier.rockTime - brazier.fallTime) < 0.08f && brazier.SpillCentre.x > at.x, (Time.time - struck).ToString("F2") + " s, spill at " + brazier.SpillCentre.ToString("F1"));
            yield return 0.05f;
            Check("in the spill, he takes 40 (+ the fire's 25) and a 1.5 s stagger", health - master.health.Current >= 40f && State == master.staggerState, (health - master.health.Current).ToString("F0") + " damage, " + State.GetType().Name);
            yield return 0.4f;
            Framed("Brazier_Spill", at + Vector3.right * 1.5f, 7f, 5f, 60f);
            var filter = new NavMeshQueryFilter { agentTypeID = master.agent.agentTypeID, areaMask = NavMesh.AllAreas };
            Check("the spill is carved out of his NavMesh", NavMesh.SamplePosition(brazier.SpillCentre, out NavMeshHit hit, 0.4f, filter) == false, "");
        }

        // Fire: 25 on contact and 25/s; at most 12 patches and ~20% of the arena; it hurts him too.
        private static IEnumerator FireRules()
        {
            Note("--- floor fire");
            yield return ResetFight(Centre);
            Vector3 spot = Centre + new Vector3(-8f, 0f, -6f);
            PlaceAbsolute(spot + Vector3.left * 3f, Vector3.right);
            yield return 0.2f;
            int before = blows.Count;
            FloorFire.Circle(spot, 1.5f, 8f);
            PlaceAbsolute(spot, Vector3.right);
            yield return 2.05f;
            var burns = blows.Skip(before).ToList();
            float total = burns.Sum(b => b.amount);
            Check("standing in it 2 s: 25 on contact, then 25/s", Mathf.Abs(total - 75f) < 13f, burns.Count + " ticks, " + total.ToString("F1") + " damage");
            PlaceAbsolute(spot + Vector3.left * 4f, Vector3.right);

            float health = master.health.Current;
            FloorFire.Circle(master.transform.position, 1.5f, 3f);
            yield return 1.05f;
            Check("it burns him too, without a stagger", health - master.health.Current >= 25f && State != master.staggerState, (health - master.health.Current).ToString("F1") + " damage, " + State.GetType().Name);

            for (int i = 0; i < 16; i++)
                FloorFire.Circle(new Vector3(42f + (i % 4) * 7f, 9f, 434f + (i / 4) * 8f), 1.8f, 8f);
            yield return 0.8f;
            int burning = FloorFire.Instance.Live.Count(p => p.Dying == false);
            Check("at most 12 patches and about 20% of the arena", burning <= 12 && FloorFire.Instance.Live.Where(p => p.Dying == false).Sum(p => p.Area) <= FloorFire.Instance.maxArea + 0.5f, burning + " burning, " + FloorFire.Instance.Live.Where(p => p.Dying == false).Sum(p => p.Area).ToString("F0") + " m2");
            Framed("Fire_Cap", new Vector3(54f, 9f, 448f), 24f, 22f);
        }

        private static IEnumerator Statues()
        {
            Note("--- the statues");
            yield return ResetFight(Centre);
            HallStatue statue = HallStatue.All.OrderBy(s => s.transform.position.x).ThenBy(s => s.transform.position.z).First();
            Quaternion rest = statue.upper.localRotation;
            yield return Start(MasterP1.MoveKind.Command, 6f);
            yield return 1f;
            float bow = Quaternion.Angle(rest, statue.upper.localRotation);
            Check("they bow at a Command", bow > 6f, bow.ToString("F1") + " deg");
            yield return WaitMoveEnds();
            yield return (Func<bool>)(() => combat.IsKneeling == false);

            yield return ResetFight(Centre);
            Vector3 knee = statue.upper.position;
            Vector3 fall = statue.transform.forward;
            master.agent.Warp(new Vector3(knee.x, 9f, knee.z) + fall * 3.2f);
            master.circleState.Hold(9999f);
            PlaceAbsolute(new Vector3(knee.x, 9f, knee.z) + fall * 1.6f, -fall);
            yield return 0.2f;
            float health = master.health.Current;
            int before = blows.Count;
            statue.Topple();
            float start = Time.time;
            yield return 0.8f;
            Framed("Statue_Crack", statue.transform.position + Vector3.up * 2.5f, 9f, 3f, statue.transform.position.x < 54f ? -70f : 70f);
            yield return (Func<bool>)(() => statue.Current == HallStatue.State.Fallen);
            Check("it cracks for 1.5 s, then falls", Mathf.Abs(Time.time - start - statue.tell - statue.fallTime) < 0.1f, (Time.time - start).ToString("F2") + " s");
            yield return 0.05f;
            var hits = blows.Skip(before).ToList();
            Check("50 to the player under it", hits.Count >= 1 && Mathf.Abs(hits[0].amount - 50f) < 0.5f, string.Join(", ", hits.Select(h => h.amount.ToString("F0"))));
            Check("50 to him under it", Mathf.Abs(health - master.health.Current - 50f) < 1f, (health - master.health.Current).ToString("F0"));
            yield return 0.4f;
            Framed("Statue_Fallen", statue.transform.position + statue.transform.forward * 2f, 9f, 6f, statue.transform.position.x < 54f ? -50f : 50f);
            yield return 1.6f;
        }

        private static IEnumerator Darkness()
        {
            Note("--- the darkness wave");
            yield return ResetFight(Centre);
            DarknessWave wave = UnityEngine.Object.FindAnyObjectByType<DarknessWave>();
            PlaceAbsolute(new Vector3(54f, 9f, 440f), Vector3.forward);
            yield return 1f;
            Framed("Dark_Before", new Vector3(54f, 9f, 450f), 24f, 18f);
            Light torch = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Point && l.GetComponentInParent<FloorFire>() == null && l.GetComponentInParent<MasterP1>() == null && l.GetComponentInParent<PlayerCombat>() == null && l.name.Contains("Glow") == false && l.transform.position.z > 430f && l.transform.position.z < 470f && l.intensity > 0.5f).OrderBy(l => l.transform.position.z).FirstOrDefault();
            float lit = torch != null ? torch.intensity : 0f;
            float ambient = RenderSettings.ambientIntensity;
            wave.Begin();
            float start = Time.time;
            yield return 2.4f;
            Check("the hall's lights gutter out in 1.5 s (the torches' flicker too)", torch != null && torch.intensity < lit * 0.05f && RenderSettings.ambientIntensity < ambient * 0.3f, (torch != null ? torch.name + " " + lit.ToString("F1") + " to " + torch.intensity.ToString("F2") : "no light") + ", ambient " + ambient.ToString("F2") + " to " + RenderSettings.ambientIntensity.ToString("F2"));
            Check("his tells burn brighter in the dark", Mathf.Abs(BossTell.DarkBoost - 1.5f) < 0.01f, BossTell.DarkBoost.ToString("F2"));
            Framed("Dark_Wave", new Vector3(54f, 9f, 450f), 24f, 18f);
            yield return (Func<bool>)(() => wave.Active == false);
            Check("it lasts about 8 s, then the hall comes back", Time.time - start > 9f && Time.time - start < 13f && torch != null && torch.intensity > lit * 0.6f, (Time.time - start).ToString("F1") + " s");
        }

        private static IEnumerator Ash()
        {
            Note("--- the ash storm");
            yield return ResetFight(Centre);
            AshStorm storm = UnityEngine.Object.FindAnyObjectByType<AshStorm>();
            storm.Begin();
            yield return 2.5f;
            PlaceAbsolute(new Vector3(36f, 9f, 446f), Vector3.forward);
            aimAt = playerRoot.position + Vector3.forward * 8f;
            yield return 0.3f;
            int before = blows.Count;
            Vector3 from = playerRoot.position;
            playerMove = Vector3.forward;
            yield return 2f;
            float inBand = Vector3.Distance(Flat(from), Flat(playerRoot.position)) / 2f;
            float burned = blows.Skip(before).Sum(b => b.amount);
            Framed("Ash_Band", new Vector3(44f, 9f, 448f), 16f, 12f);
            PlaceAbsolute(new Vector3(54f, 9f, 440f), Vector3.forward);
            aimAt = playerRoot.position + Vector3.forward * 8f;
            yield return 0.4f;
            from = playerRoot.position;
            yield return 2f;
            float open = Vector3.Distance(Flat(from), Flat(playerRoot.position)) / 2f;
            playerMove = Vector3.zero;
            Check("in the band: 30% slower", Mathf.Abs(inBand / open - 0.7f) < 0.06f, inBand.ToString("F2") + " vs " + open.ToString("F2") + " m/s");
            Check("and 10/s", Mathf.Abs(burned - 20f) < 5.5f, burned.ToString("F1") + " in 2 s");
            storm.Thicken(1.5f);
            yield return 1f;
            Check("a Gather widens it to 3 R at 15/s", storm.Width > 8.5f && Mathf.Approximately(storm.Rate, 15f), storm.Width.ToString("F1") + " m, " + storm.Rate + "/s");
            storm.Stop();
        }

        // The pacing rule: no blow of his lands within 0.5 s of a hazard, and a scripted hazard waits for his blow.
        private static IEnumerator Pacing()
        {
            Note("--- pacing (0.5 s between a hazard and his blow)");
            ChandelierDrop chandelier = ChandelierDrop.All.OrderBy(c => Mathf.Abs(c.transform.position.z - 463f)).First();
            yield return ResetFight(Centre);
            PlaceFront(3.5f);
            yield return 0.1f;
            int held = master.HeldForHazards;
            chandelier.Trigger();
            float impact = chandelier.ImpactAt;
            MasterP1.ForceNext = MasterP1.MoveKind.Decree;
            master.circleState.Hold(0f);
            int before = blows.Count;
            yield return (Func<bool>)(() => blows.Count > before);
            float landed = blows[blows.Count - 1].time;
            Check("he holds a Decree that would land on a falling chandelier", master.HeldForHazards > held && Mathf.Abs(landed - impact) >= 0.5f, (landed - impact).ToString("F2") + " s from its impact, held " + (master.HeldForHazards - held) + "x");
            yield return WaitMoveEnds();

            yield return ResetFight(Centre);
            BossArenaDirector director = Director;
            int directorHeld = director.Held;
            bool begun = false;
            float began = -1f;
            yield return Start(MasterP1.MoveKind.Decree, 3.5f);
            director.Schedule("test", S.decreeTell, () => { begun = true; began = Time.time; });
            float blow = Move != null ? Move.LandsAt : -1f;
            yield return (Func<bool>)(() => begun);
            Check("a scripted hazard waits until it's clear of his blow", director.Held > directorHeld && began + S.decreeTell - blow >= 0.49f, "its impact " + (began + S.decreeTell - blow).ToString("F2") + " s after the blow");
            yield return WaitMoveEnds();
        }

        // Panel F1 at his 50% beat, with the player standing on it: 50, a kneel, thrown to the edge; then the lip holds.
        private static IEnumerator FloorPanels()
        {
            Note("--- the floor (F1 at the 50% beat)");
            HallFloor floor = Floor;
            HallFloor.Panel f1 = floor.panels[floor.f1];
            yield return ResetFight(Centre);
            typeof(MasterP1).GetField("halfReached", Any).SetValue(master, false);
            master.health.ApplyDamage(master.health.Max * 0.5f - 5f);
            PlaceFront(2.3f);
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => State == master.pommelState);
            yield return (Func<bool>)(() => floor.StateOf(floor.f1) != HallFloor.PanelState.Whole);
            float cracked = Time.time;
            Check("the pommel cracks F1", floor.StateOf(floor.f1) == HallFloor.PanelState.Cracking, floor.StateOf(floor.f1).ToString());
            Vector3 onIt = new Vector3(f1.rect.center.x, 9f, f1.rect.center.y);
            PlaceAbsolute(new Vector3(onIt.x, 9f, f1.rect.yMin - 1.5f), Vector3.forward);
            yield return 0.8f;
            Framed("Floor_Crack", onIt, 10f, 9f);
            yield return (Func<bool>)(() => floor.StateOf(floor.f1) == HallFloor.PanelState.Fallen);
            Check("it goes 2.0 s after the crack", Mathf.Abs(Time.time - cracked - floor.tell) < 0.08f, (Time.time - cracked).ToString("F2") + " s");
            yield return 0.6f;
            Framed("Floor_Hole", onIt, 10f, 9f);
            yield return 1.2f;
            Framed("Floor_Hole2", onIt, 10f, 9f);
            yield return 1.5f;
            Framed("Floor_Hole3", onIt, 10f, 9f);
            yield return (Func<bool>)(() => State != master.pommelState);

            // The lip: walk at the hole, then roll at it.
            Vector3 toHole = Flat(onIt - playerRoot.position).normalized;
            playerMove = toHole;
            yield return 1.5f;
            Check("walking into the hole: the lip stops it", f1.rect.Contains(new Vector2(playerRoot.position.x, playerRoot.position.z)) == false && playerRoot.position.y > 8.5f, playerRoot.position.ToString("F1"));
            yield return Roll(toHole);
            yield return 1f;
            playerMove = Vector3.zero;
            Check("and rolling", f1.rect.Contains(new Vector2(playerRoot.position.x, playerRoot.position.z)) == false && playerRoot.position.y > 8.5f, playerRoot.position.ToString("F1"));
            var filter = new NavMeshQueryFilter { agentTypeID = master.agent.agentTypeID, areaMask = NavMesh.AllAreas };
            Check("the hole is carved from his NavMesh", NavMesh.SamplePosition(onIt, out NavMeshHit hit, 0.5f, filter) == false, "");
            int centre = floor.IndexAt(new Vector3(54f, 9f, 450f));
            Check("the centre column never falls", floor.CanFall(centre) == false && floor.Crack(centre) == false, floor.panels[centre].name);
        }

        // Last (the player doesn't get up from it): standing on a panel as it goes, Kael drops with it into the lava.
        private static IEnumerator LavaDrop()
        {
            Note("--- standing on a panel as it goes");
            HallFloor floor = Floor;
            int index = floor.IndexAt(new Vector3(59f, 9f, 446.4f));
            HallFloor.Panel panel = floor.panels[index];
            Vector3 onIt = new Vector3(panel.rect.center.x, 9f, panel.rect.center.y);
            yield return ResetFight(Centre);
            master.agent.Warp(Centre + Vector3.forward * 10f);
            master.circleState.Hold(9999f);
            PlaceAbsolute(onIt, Vector3.forward);
            yield return 0.3f;
            floor.Crack(index);
            yield return (Func<bool>)(() => floor.StateOf(index) == HallFloor.PanelState.Fallen);
            float dropped = Time.time;
            yield return 0.3f;
            Shot("LavaDrop_In");
            yield return (Func<bool>)(() => combat.IsDead);
            Check("the player on it drops into the lava and dies", combat.IsDead && floor.PlayerInLava, (Time.time - dropped).ToString("F2") + " s after it fell, " + combat.State);
            yield return 1.2f;
            Shot("LavaDrop_Under");
            Check("and sinks, rather than falling on", playerRoot.position.y > floor.lavaY - 3f && playerRoot.position.y < floor.lavaY - 0.5f, playerRoot.position.y.ToString("F2"));
        }

        // Everything at once for 15 s while he fights: frame times, lights and particles.
        private static IEnumerator Performance()
        {
            Note("--- performance: the hall's systems all at once, 15 s of fighting");
            yield return ResetFight(Centre);
            FloorFire.Instance.ResetState();
            for (int i = 0; i < 12; i++)
                FloorFire.Circle(new Vector3(42f + (i % 4) * 8f, 9f, 436f + (i / 4) * 10f), 1.6f, 20f);
            UnityEngine.Object.FindAnyObjectByType<DarknessWave>().Begin(4f);
            UnityEngine.Object.FindAnyObjectByType<AshStorm>().Begin();
            foreach (ChandelierDrop c in ChandelierDrop.All)
                c.Trigger();
            PlaceFront(6f);
            master.circleState.Hold(0f);
            var frames = new List<float>();
            int peakParticles = 0, peakLights = 0;
            float start = Now;
            while (Now - start < 15f)
            {
                frames.Add(Time.unscaledDeltaTime * 1000f);
                if (frames.Count % 15 == 0)
                {
                    int particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(p => p.particleCount);
                    int lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled && l.intensity > 0.01f && l.type != LightType.Directional && Vector3.Distance(l.transform.position, playerRoot.position) < 40f);
                    peakParticles = Mathf.Max(peakParticles, particles);
                    peakLights = Mathf.Max(peakLights, lights);
                }

                yield return null;
            }

            frames.Sort();
            float p50 = frames[frames.Count / 2], p95 = frames[(int)(frames.Count * 0.95f)], worst = frames[frames.Count - 1];
            Note("  frame time (Editor, Play mode): p50 " + p50.ToString("F1") + " ms, p95 " + p95.ToString("F1") + " ms, worst " + worst.ToString("F1") + " ms over " + frames.Count + " frames");
            Note("  peak particles alive " + peakParticles + "; lights lit within 40 m " + peakLights + " (the hall's own fixtures included)");
            UnityEngine.Object.FindAnyObjectByType<AshStorm>().Stop();
            UnityEngine.Object.FindAnyObjectByType<Kneel.Hazards.LevelReset>()?.ResetAll();
        }

        // --- The 50% beat and the fall ---

        private static IEnumerator HalfBeat()
        {
            Note("--- the 50% beat");
            yield return ResetFight(Centre);
            typeof(MasterP1).GetField("halfReached", Any).SetValue(master, false);
            master.health.ApplyDamage(master.health.Max * 0.5f - 5f);
            PlaceFront(2.3f);
            int beats = halfBeats;
            float health = master.health.Current;
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => State == master.pommelState);
            float start = Time.time;
            Check("crossing half health starts the pommel beat", State == master.pommelState && halfBeats == beats + 1, State.GetType().Name + ", " + master.health.Fraction.ToString("P0"));
            yield return 0.6f;
            Shot("Pommel");
            float during = master.health.Current;
            Press("OnStrikePressed");
            yield return 0.8f;
            Check("untouchable in it", master.health.Current == during && master.Untouchable, "");
            yield return (Func<bool>)(() => State != master.pommelState);
            Check("about 3 s, then the fight resumes", Mathf.Abs(Time.time - start - S.pommelTime) < 0.3f && master.Untouchable == false, (Time.time - start).ToString("F2") + " s, then " + State.GetType().Name);
        }

        private static IEnumerator Down()
        {
            Note("--- Phase 1 ends");
            yield return ResetFight(Centre);
            typeof(MasterP1).GetField("halfReached", Any).SetValue(master, true);
            master.health.ApplyDamage(master.health.Current - 5f);
            PlaceFront(2.3f);
            Press("OnStrikePressed");
            yield return (Func<bool>)(() => master.IsDead);
            Check("he falls to one knee", master.IsDead && defeats == 1 && boss.Current == MasterBoss.Beat.Phase1Down, State.GetType().Name + ", beat " + boss.Current);
            yield return 1.5f;
            Shot("Fallen");
        }

        // --- Fight control ---

        // He's put at `at`, facing down-screen, held circling, full health and poise, cooldowns clear; the player armed.
        private static IEnumerator ResetFight(Vector3 at)
        {
            MasterP1.ForceNext = null;
            playerMove = Vector3.zero;
            aimAt = null;
            SetBlock(false);

            // The hall back to its start (fires out, chandeliers up, the floor whole).
            UnityEngine.Object.FindAnyObjectByType<Kneel.Hazards.LevelReset>()?.ResetAll();
            yield return (Func<bool>)(() => combat.IsKneeling == false && combat.State != PlayerCombat.CombatState.Dodging);

            if (master.agent.enabled == false)
                master.agent.enabled = true;
            master.agent.Warp(at);
            float yaw = Camera.main.transform.eulerAngles.y + 180f;
            master.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            master.stateMachine.ChangeState(master.circleState);
            master.circleState.Hold(9999f);
            typeof(MasterP1).GetField("pendingPoiseBreak", Any).SetValue(master, false);
            typeof(MasterP1).GetField("lastFlinchTime", Any).SetValue(master, -99f);
            ((Dictionary<MasterP1.MoveKind, float>)typeof(MasterP1).GetField("readyAt", Any).GetValue(master)).Clear();
            ((List<MasterP1.MoveKind>)typeof(MasterP1).GetField("history", Any).GetValue(master)).Clear();
            typeof(MasterP1).GetField("openingIndex", Any).SetValue(master, 99);
            master.poise.Reset();
            master.health.SetMax(master.health.Max, true);

            playerHealth.SetMax(100000f, true);
            combat.Poise.Reset();
            movement.GetType().GetField("sprintHeld", Any).SetValue(movement, false);
            PlaceFront(8f);
            if (combat.State == PlayerCombat.CombatState.Sheathed)
                Press("OnDrawPressed");
            yield return (Func<bool>)(() => combat.State == PlayerCombat.CombatState.Armed);
            yield return 0.3f;
        }

        // Forces his next move and lets him start it, with the player `distance` in front of him (-1: where they are).
        private static IEnumerator Start(MasterP1.MoveKind kind, float distance)
        {
            if (distance > 0f)
                PlaceFront(distance);
            yield return 0.1f;
            MasterP1.ForceNext = kind;
            master.circleState.Hold(0f);
            MasterMoveState state = master.StateFor(kind);
            yield return (Func<bool>)(() => State == state);
            if (State != state)
            {
                Note("  " + kind + " never started: " + State.GetType().Name + " at " + Distance().ToString("F1") + " m, on screen " + master.OnScreen());
                failures++;
            }
        }

        // Shots at half the tell, at contact and just after; returns once the blow has landed (or the move ended).
        private static IEnumerator Filmstrip(string name, float tell)
        {
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= tell * 0.5f);
            Shot(name + "_1tell");
            yield return (Func<bool>)(() => Move == null || Move.Landed);
            Shot(name + "_2contact");
            yield return 0.3f;
            Shot(name + "_3after");
        }

        private static IEnumerator WaitMoveEnds()
        {
            yield return (Func<bool>)(() => Move == null && State != master.approachState);
            playerMove = Vector3.zero;
            master.circleState.Hold(9999f);
        }

        // A fresh press on the block 0.1 s before the blow lands (the parry window is 0.2 s).
        private static IEnumerator ParryAt(float tell)
        {
            yield return (Func<bool>)(() => Move == null || Move.SinceStart >= tell - 0.1f);
            Press("OnBlockPressed");
            yield return (Func<bool>)(() => Move == null || Move.Landed || State == master.parriedState);
            yield return 0.05f;
            SetBlock(false);
        }

        // The roll reads PlayerMovement's direction from the frame before, so the stick goes down a frame ahead.
        private static IEnumerator Roll(Vector3 direction)
        {
            playerMove = direction;
            DriveInput();
            yield return 0.03f;
            typeof(PlayerCombat).GetField("dodgeRequestTime", Any).SetValue(combat, Time.time);
            yield return 0.05f;
        }

        // The last blow on the player: when it landed in its tell, the sword tip, the damage.
        private static void ReportBlow(string name, float tell, float reach, float damage, Blow? given = null)
        {
            if (given.HasValue == false && blows.Count == 0)
            {
                Check(name + " lands", false, "no hit");
                return;
            }

            Blow blow = given ?? blows[blows.Count - 1];
            if (Time.time - blow.time > 3f && given.HasValue == false)
            {
                Check(name + " lands", false, "no recent hit");
                return;
            }

            float expected = tell > 0f ? tell : blow.tell;
            Check(name + " lands at the end of its tell (" + expected.ToString("F2") + " s)", Mathf.Abs(blow.since - expected) < 0.06f, blow.since.ToString("F3") + " s in");
            Check(name + " does " + damage, Mathf.Abs(blow.amount - damage) < 0.5f, blow.amount.ToString("F1"));
            if (reach < 90f)
            {
                Check(name + ": the blade is in front of him", Mathf.Abs(blow.tipAngle) <= 50f, "tip " + blow.tipAngle.ToString("F0") + " deg, " + blow.tipOut.ToString("F2") + " m out, " + blow.tipUp.ToString("F2") + " m up; player " + blow.distance.ToString("F2") + " m away, reach " + reach);
            }
        }

        // --- Player driving ---

        private static void DriveInput()
        {
            if (master == null || Camera.main == null)
                return;

            typeof(PlayerMovement).GetField("aimInput", Any).SetValue(movement, (Vector2)Camera.main.WorldToScreenPoint(aimAt ?? master.transform.position));
            Quaternion yaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * playerMove;
            typeof(PlayerMovement).GetField("moveInput", Any).SetValue(movement, new Vector2(local.x, local.z));
        }

        private static void Press(string handler) => typeof(PlayerCombat).GetMethod(handler, Any).Invoke(combat, null);

        private static void SetBlock(bool held)
        {
            if (held)
                Press("OnBlockPressed");
            else
                typeof(PlayerCombat).GetField("blockHeld", Any).SetValue(combat, false);
        }

        private static Vector3 Flat(Vector3 point)
        {
            point.y = 0f;
            return point;
        }

        private static float Distance() => Vector3.Distance(Flat(master.transform.position), Flat(playerRoot.position));

        // `distance` metres along his facing; he turns to the player unless keepFacing (mid-blow).
        private static void PlaceFront(float distance, bool keepFacing = false)
        {
            Vector3 at = master.transform.position + master.transform.forward * distance;
            PlaceAbsolute(at, master.transform.position - at);
            if (keepFacing == false)
                master.transform.rotation = Quaternion.LookRotation(Flat(playerRoot.position - master.transform.position));
        }

        private static void PlaceAbsolute(Vector3 position, Vector3 facing)
        {
            var controller = playerRoot.GetComponent<CharacterController>();
            controller.enabled = false;
            position.y = 9f;
            playerRoot.position = position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
                playerRoot.rotation = Quaternion.LookRotation(facing);
            controller.enabled = true;
        }

        // --- Reporting ---

        // A held shot of a hall event: from `distance` back and `height` up, south of it (the follow camera's side).
        private static void Framed(string name, Vector3 target, float distance = 11f, float height = 8f, float yaw = 0f)
        {
            // Never lower than 55 degrees: the walls' sconces hang their glow sprites in a lower camera's sight line.
            height = Mathf.Max(height, distance * 1.45f);
            var cam = UnityEngine.Object.FindAnyObjectByType<PlayerCamera>();
            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            Vector3 at = target + back * distance + Vector3.up * height;
            cam.SetOverride(at, Quaternion.LookRotation(target - at), 0.01f);
            framedName = name;
            framedUntil = Now + 0.12f;
        }

        // What's lit and drawing within `radius` of a point (to track down a glare in a shot).
        private static void NoteNear(Vector3 point, float radius)
        {
            foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.isActiveAndEnabled && Vector3.Distance(l.transform.position, point) < radius)
                    Note("    light " + PathOf(l.transform) + " " + l.intensity.ToString("F1") + " range " + l.range.ToString("F1") + " @" + l.transform.position.ToString("F1"));
            }

            foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (ps.particleCount > 0 && Vector3.Distance(ps.transform.position, point) < radius)
                    Note("    particles " + PathOf(ps.transform) + " x" + ps.particleCount + " " + ps.GetComponent<ParticleSystemRenderer>().sharedMaterial?.name);
            }
        }

        private static string PathOf(Transform t) => t.parent != null ? PathOf(t.parent) + "/" + t.name : t.name;

        private static string framedName;
        private static float releaseFrameAt;
        private static float framedUntil;

        private static void Shot(string name)
        {
            if (TakeShots)
                ScreenCapture.CaptureScreenshot("Temp/MasterShots/Master_" + name + ".png");
        }

        private static void Check(string what, bool passed, string detail)
        {
            Note((passed ? "ok   " : "FAIL ") + what + (detail.Length > 0 ? ": " + detail : ""));
            if (passed == false)
                failures++;
        }

        private static void Note(string line) => Lines.Add(line);

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                if (Errors.Contains(message) == false)
                    Note("console: " + message + " | master " + (master != null ? master.transform.position + " " + State?.GetType().Name : "?") + " | " + stack);
                Errors.Add(message);
            }
        }
    }
}
