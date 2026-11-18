using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Kneel.Core;
using Kneel.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Kneel.EditorTools.Transitions
{
    // Plays a level's exit through the real triggers and writes Temp/TransitionPlaytest_<scene>_<scenario>.txt, plus a
    // Game-view filmstrip in Temp/TransitionShots. Start Play mode in the level, then call Begin(scenario):
    //   walk      preload trigger, then walk in from 7 m; carried state, save, timings, frames, audio, memory
    //   presses   walk, with Esc / F1 / F2 / attack / roll / draw / jump pressed in every phase (real key events)
    //   held      walk with W and the left mouse button held through the whole transition
    //   sprint, air, roll, armed, hitstop, enemy   the threshold under those conditions
    //   continue  walk, then Continue (plain load of the saved level), then a death and Restart in it
    //   deathpreload, quitpreload   death / Quit to Main Menu while the preload waits
    // The test drives input by writing PlayerMovement's cached input (CLAUDE.md's recipe) and times itself on real
    // time. Set CaptureFramerate first for the frame-rate repeats.
    public static class TransitionPlaytest
    {
        public static int CaptureFramerate;
        public static bool Shots = true;
        public static float ShotEvery = 0.45f;

        // Two audible test tones (Music and SFX) to meter the fades and the loading screen's silence. Off by
        // default: they are heard through the speakers while a run plays.
        public static bool Tones;

        private static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private enum Step { Setup, Preloading, Approach, WalkIn, InTransition, After, Continue, ContinueCheck, Die, DeathScreen, Restarted, Done }

        private static Step step;
        private static string scenario;
        private static string fromScene;
        private static string toScene;
        private static float stepAt;
        private static float lastShot;
        private static int shotIndex;
        private static readonly StringBuilder log = new StringBuilder();
        private static readonly List<string> fails = new List<string>();
        private static Vector3 walkDir;
        private static float setHealth;
        private static int setPotions;
        private static int errorsAtStart;
        private static int errors;
        private static AudioSource toneMusic;
        private static AudioSource toneSfx;
        private static readonly float[] samples = new float[512];
        private static readonly List<(float t, int phase, float rms)> audioTrace = new List<(float, int, float)>();
        private static readonly HashSet<string> pressedPhases = new HashSet<string>();
        private static bool heldDown;
        private static float controlAt = -1f;
        private static bool firedAfterControl;
        private static CarriedState entryAtSave;
        private static Enemy chaser;
        private static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        private static InputSettings.BackgroundBehavior savedBackground;
        private static float chaserHealth;

        public static string ResultPath => $"Temp/TransitionPlaytest_{fromScene}_{scenario}.txt";

        // The tests never touch the player's real save: they write Temp/TransitionTestSave.json instead. Set
        // SeedSave to a JSON text to start from it (an old-format save, for the compatibility check).
        public const string TestSavePath = "Temp/TransitionTestSave.json";
        public static string SeedSave;

        private class TestSave : ISaveService
        {
            public bool HasSave() => File.Exists(TestSavePath);

            public SaveData Load() => HasSave() ? JsonUtility.FromJson<SaveData>(File.ReadAllText(TestSavePath)) : null;

            public void Save(SaveData data)
            {
                data.savedAtUtc = DateTime.UtcNow.ToString("o");
                File.WriteAllText(TestSavePath, JsonUtility.ToJson(data, true));
            }

            public void DeleteSave() => File.Delete(TestSavePath);
        }

        public static void Begin(string which)
        {
            scenario = which;
            if (File.Exists(TestSavePath))
            {
                File.Delete(TestSavePath);
            }

            if (string.IsNullOrEmpty(SeedSave) == false)
            {
                File.WriteAllText(TestSavePath, SeedSave);
            }

            SaveServices.Current = new TestSave();
            fromScene = SceneManager.GetActiveScene().name;
            toScene = LevelCatalog.NextOf(fromScene);
            log.Clear();
            fails.Clear();
            audioTrace.Clear();
            pressedPhases.Clear();
            shotIndex = 0;
            heldDown = false;
            controlAt = -1f;
            firedAfterControl = false;
            chaser = null;
            errors = 0;
            Application.runInBackground = true;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            if (CaptureFramerate > 0)
            {
                Time.captureFramerate = CaptureFramerate;
            }

            savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Directory.CreateDirectory("Temp/TransitionShots");
            step = Step.Setup;
            stepAt = Now;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Note($"begin {scenario} in {fromScene} -> {toScene} (captureFramerate {Time.captureFramerate})");
        }

        private static float Now => Time.realtimeSinceStartup;

        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                errors++;
                if (errors < 12)
                {
                    log.AppendLine("  [console] " + message.Split('\n')[0]);
                }
            }
        }

        private static void Note(string s) => log.AppendLine($"{Now:0.000} {s}");

        private static void Fail(string s)
        {
            fails.Add(s);
            Note("FAIL " + s);
        }

        private static void Go(Step next)
        {
            step = next;
            stepAt = Now;
        }

        private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Any).GetValue(o);

        private static void Set(object o, string field, object value) => o.GetType().GetField(field, Any).SetValue(o, value);

        private static void Call(object o, string method) => o.GetType().GetMethod(method, Any).Invoke(o, null);

        private static void Tick()
        {
            if (EditorApplication.isPlaying == false)
            {
                Finish("Play mode ended");
                return;
            }

            try
            {
                Meter();
                switch (step)
                {
                    case Step.Setup: Setup(); break;
                    case Step.Preloading: Preloading(); break;
                    case Step.Approach: Approach(); break;
                    case Step.WalkIn: WalkIn(); break;
                    case Step.InTransition: InTransition(); break;
                    case Step.After: After(); break;
                    case Step.Continue: ContinueStep(); break;
                    case Step.ContinueCheck: ContinueCheck(); break;
                    case Step.Die: Die(); break;
                    case Step.DeathScreen: DeathScreen(); break;
                    case Step.Restarted: Restarted(); break;
                }
            }
            catch (Exception e)
            {
                Fail("exception: " + e);
                Finish("exception");
            }
        }

        // --- Setup ---

        private static void Setup()
        {
            if (Now - stepAt < 1.5f)
            {
                return;
            }

            errorsAtStart = errors;
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var potions = combat.GetComponent<PlayerPotions>();
            setHealth = Mathf.Round(combat.Health.Max * 0.63f);
            combat.Health.SetCurrent(setHealth);
            while (potions.Count > 2 && potions.TryTake())
            {
            }

            while (potions.Count < 2 && potions.TryAdd())
            {
            }

            setPotions = potions.Count;
            Note($"set health {setHealth}/{combat.Health.Max}, potions {setPotions}");
            // Unhurt from here to the threshold (which makes him invincible itself, and lets go when control returns):
            // the teleport into a preload trigger can land him under a hazard (L4's is under a murder hole), and the
            // carried-state check needs the health set here.
            combat.Health.invincible = true;
            if (Tones)
            {
                MakeTones();
            }

            if (scenario == "quitpreload" || scenario == "deathpreload" || SceneLoader.PendingScene == null)
            {
                var trigger = UnityEngine.Object.FindAnyObjectByType<PreloadTrigger>();
                if (trigger != null)
                {
                    Teleport(trigger.GetComponent<Collider>().bounds.center - Vector3.up * 1.2f, trigger.transform.forward);
                    Note($"teleported into the preload trigger at {trigger.transform.position}");
                }
                else
                {
                    Fail("no PreloadTrigger in the level");
                }
            }

            Go(Step.Preloading);
        }

        private static void Preloading()
        {
            if (scenario == "deathpreload" && SceneLoader.PendingScene != null && Now - stepAt > 0.3f)
            {
                Note($"preload pending ({SceneLoader.PendingScene}); killing Kael");
                Go(Step.Die);
                return;
            }

            if (scenario == "quitpreload" && SceneLoader.PendingScene != null && Now - stepAt > 0.3f)
            {
                Note($"preload pending ({SceneLoader.PendingScene}); quitting to the main menu");
                var menu = PauseMenu.Current;
                Call(menu, "Open");
                menu.GetType().GetMethod("LeaveToMainMenu", Any).Invoke(menu, null);
                Go(Step.Restarted);
                return;
            }

            if (SceneLoader.PreloadReady)
            {
                Note($"preload ready: {SceneLoader.PendingScene} in {SceneLoader.PreloadSeconds:0.00} s");
                Go(Step.Approach);
            }
            else if (Now - stepAt > 90f)
            {
                Fail("preload never became ready");
                Finish("preload timeout");
            }
        }

        // --- Walking in ---

        private static void Approach()
        {
            var exit = UnityEngine.Object.FindAnyObjectByType<LevelExit>();
            Bounds box = exit.GetComponent<Collider>().bounds;
            Vector3 ahead = exit.walkPath.Length > 0 ? exit.walkPath[exit.walkPath.Length - 1].position : box.center + Vector3.forward;
            walkDir = ahead - box.center;
            walkDir.y = 0f;
            walkDir.Normalize();
            float back = scenario == "sprint" ? 12f : 7f;
            Vector3 target = new Vector3(box.center.x, box.min.y, box.center.z);
            Vector3 side = Vector3.Cross(Vector3.up, walkDir);
            Vector3 from = target - walkDir * back;

            // The first line in that is physically clear (L4's Shrine B statue stands on the axis before the doors).
            int solid = ~LayerMask.GetMask("Player", "Ignore Raycast");
            foreach (float offset in new[] { 0f, 2.5f, -2.5f, 3.5f, -3.5f })
            {
                Vector3 start = SeamGround(target - walkDir * back + side * offset);
                Vector3 to = target - start;
                to.y = 0f;
                // Sampled along the line at the exit's own floor height (overlap tests, which see mesh colliders).
                bool clear = true;
                for (float d = 0f; d < to.magnitude - 2f && clear; d += 0.5f)
                {
                    Vector3 p = start + to.normalized * d;
                    p.y = target.y;
                    clear = Physics.CheckCapsule(p + Vector3.up * 1.0f, p + Vector3.up * 1.8f, 0.3f, solid, QueryTriggerInteraction.Ignore) == false;
                }

                if (clear)
                {
                    from = start;
                    walkDir = to.normalized;
                    break;
                }
            }

            Teleport(from, walkDir);
            Note($"approach from {from}, walking {walkDir}");

            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            if (scenario == "armed" || scenario == "roll" || scenario == "hitstop")
            {
                combat.GetType().GetMethod("OnDrawPressed", Any).Invoke(combat, null);
            }

            if (scenario == "enemy")
            {
                chaser = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Where(e => e.GetComponent<Health>() != null && e.GetComponent<Health>().Current > 0f).OrderBy(e => (e.transform.position - from).sqrMagnitude).FirstOrDefault();
                if (chaser != null)
                {
                    var agent = chaser.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    Vector3 at = from - walkDir * 4f;
                    if (agent != null && UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 3f, agent.areaMask))
                    {
                        agent.Warp(hit.position);
                    }

                    Note($"chaser {chaser.name} placed 4 m behind");
                }
                else
                {
                    Note("no enemy left to chase");
                }
            }

            if (scenario == "held" || scenario == "presses")
            {
                HoldKeys(scenario == "held");
            }

            Go(Step.WalkIn);
        }

        private static void WalkIn()
        {
            var movement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            var combat = movement.GetComponent<PlayerCombat>();
            if (LevelTransition.Active)
            {
                Note($"threshold: state {combat.State}, grounded {movement.IsGrounded}, speed {movement.CurrentSpeed:0.00}, timeScale {Time.timeScale}");
                if (scenario == "hitstop")
                {
                    HitStop.Freeze(0.8f);
                    Note("hit-stop frozen at the threshold");
                }

                Set(movement, "moveInput", Vector2.zero);
                Set(movement, "sprintHeld", false);
                lastShot = -1f;
                Go(Step.InTransition);
                return;
            }

            if (Now - stepAt < 0.6f)
            {
                return;
            }

            Camera cam = Camera.main;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f)) * walkDir;
            Set(movement, "moveInput", new Vector2(local.x, local.z));
            Set(movement, "aimInput", (Vector2)cam.WorldToScreenPoint(movement.transform.position + walkDir * 6f));
            if (scenario == "sprint")
            {
                Set(movement, "sprintHeld", true);
            }

            var exit = UnityEngine.Object.FindAnyObjectByType<LevelExit>();
            float toTrigger = Vector3.Dot(exit.GetComponent<Collider>().bounds.ClosestPoint(movement.transform.position) - movement.transform.position, walkDir);
            if (scenario == "air" && toTrigger < 1.6f && movement.IsGrounded)
            {
                movement.GetType().GetMethod("SafeJump", Any).Invoke(movement, null);
            }

            if (scenario == "roll" && toTrigger < 1.4f && combat.State == PlayerCombat.CombatState.Armed)
            {
                Set(combat, "dodgeRequestTime", Time.time);
            }

            if (Now - stepAt > 20f)
            {
                Fail("never reached the exit trigger");
                Finish("walk timeout");
            }
        }

        // --- Through the transition ---

        private static void InTransition()
        {
            Shoot();
            Probe();
            if (scenario == "enemy" && chaser != null)
            {
                var health = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>()?.Health;
                if (health != null && health.Current < setHealth - 0.01f && LevelTransition.Current != LevelTransition.Phase.Intro)
                {
                    Fail($"took damage during the outro ({health.Current})");
                }
            }

            if (LevelTransition.Active == false)
            {
                controlAt = Now;
                Note("control returned");
                Go(Step.After);
            }
            else if (Now - stepAt > 40f)
            {
                Fail("transition never finished");
                Finish("transition timeout");
            }
        }

        // Every phase gets the press battery once, a little way in.
        private static void Probe()
        {
            if (scenario != "presses" || LevelTransition.Active == false)
            {
                return;
            }

            string phase = LevelTransition.Current.ToString();
            if (pressedPhases.Contains(phase) || Now - stepAt < 0.5f)
            {
                return;
            }

            if (phase == "Loading" && SceneLoader.BlackAlpha > 0.05f)
            {
                return;
            }

            pressedPhases.Add(phase);
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var before = combat != null ? combat.State : PlayerCombat.CombatState.Dead;
            PressAll();
            EditorApplication.delayCall += () =>
            {
                var c = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
                string r = $"presses in {phase}: paused {PauseMenu.IsPaused}, F1 {DebugTuningOverlay.IsOpen}, F2 {MasterDevTool.IsOpen}, state {before}->{(c != null ? c.State.ToString() : "-")}";
                Note(r);
                if (PauseMenu.IsPaused || DebugTuningOverlay.IsOpen || MasterDevTool.IsOpen)
                {
                    Fail("a menu opened during " + phase);
                }

                if (c != null && c.State != PlayerCombat.CombatState.Sheathed && c.State != PlayerCombat.CombatState.Sheathing && c.State != before)
                {
                    Fail($"a press acted during {phase}: {before}->{c.State}");
                }
            };
        }

        private static void After()
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            if (heldDown && Now - controlAt < 1.2f)
            {
                if (combat.State == PlayerCombat.CombatState.Attacking || combat.State == PlayerCombat.CombatState.Drawing || combat.State == PlayerCombat.CombatState.Dodging)
                {
                    firedAfterControl = true;
                }

                Shoot();
                return;
            }

            if (heldDown)
            {
                ReleaseKeys();
                Note($"held keys released 1.2 s after control; anything fired on arrival: {firedAfterControl}");
                if (firedAfterControl)
                {
                    Fail("a held key fired on arrival");
                }
            }

            if (Now - controlAt < 1.0f)
            {
                Shoot();
                return;
            }

            Report();
            if (scenario == "continue")
            {
                Go(Step.Continue);
                return;
            }

            Finish("done");
        }

        // --- Continue, death, restart ---

        private static void ContinueStep()
        {
            Note($"Continue: plain load of {toScene}");
            SceneLoader.Instance.LoadScene(toScene, 0.3f, 0.3f);
            Go(Step.ContinueCheck);
        }

        private static void ContinueCheck()
        {
            if (SceneLoader.IsLoading || Now - stepAt < 2.5f)
            {
                return;
            }

            CheckState("after Continue", entryAtSave);
            var spawn = UnityEngine.Object.FindAnyObjectByType<Kneel.Markers.PlayerSpawnMarker>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            if (spawn != null)
            {
                float d = Vector3.Distance(Flat(spawn.transform.position), Flat(player.transform.position));
                Note($"Continue landed {d:0.00} m from the spawn marker");
                if (d > 1.5f)
                {
                    Fail("Continue did not land at the level's start");
                }
            }

            // Lose some health and a potion, then die: the restart must give back the carried-in state, not this.
            var combat = player.GetComponent<PlayerCombat>();
            combat.GetComponent<PlayerPotions>().TryTake();
            Go(Step.Die);
        }

        private static void Die()
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            combat.ReleaseShield();
            combat.Health.invincible = false;
            combat.Health.ApplyDamage(99999f);
            Note("killed Kael");
            Go(Step.DeathScreen);
        }

        private static void DeathScreen()
        {
            if (PauseMenu.IsPaused == false)
            {
                if (Now - stepAt > 10f)
                {
                    Fail("the death screen never opened");
                    Finish("no death screen");
                }

                return;
            }

            if (Now - stepAt < 4.5f)
            {
                return;
            }

            Note($"death screen open; Restart Level (pending preload: {SceneLoader.PendingScene ?? "none"})");
            var menu = PauseMenu.Current;
            Set(menu, "mode", Enum.Parse(menu.GetType().GetNestedType("Mode", BindingFlags.NonPublic), "List"));
            menu.GetType().GetMethod("RestartLevel", Any).Invoke(menu, null);
            Go(Step.Restarted);
        }

        private static void Restarted()
        {
            if (SceneLoader.IsLoading || Now - stepAt < 3f)
            {
                return;
            }

            string now = SceneManager.GetActiveScene().name;
            Note($"after the reload: active scene {now}, pending preload {SceneLoader.PendingScene ?? "none"}, scenes loaded {SceneManager.sceneCount}");
            if (scenario == "deathpreload")
            {
                if (now != fromScene)
                {
                    Fail($"death during the preload reloaded {now}, not {fromScene}");
                }
            }
            else if (scenario == "quitpreload")
            {
                if (now != "MainMenu")
                {
                    Fail($"Quit to Main Menu during the preload landed in {now}");
                }
            }
            else
            {
                CheckState("after death and Restart", entryAtSave);
            }

            Finish("done");
        }

        // --- Report ---

        private static void Report()
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var potions = combat.GetComponent<PlayerPotions>();
            var spawn = UnityEngine.Object.FindAnyObjectByType<Kneel.Markers.PlayerSpawnMarker>();
            Note($"arrived in {SceneManager.GetActiveScene().name}: health {combat.Health.Current}/{combat.Health.Max}, potions {potions.Count}, at {combat.transform.position}, spawn {(spawn != null ? spawn.transform.position.ToString() : "-")}");
            if (SceneManager.GetActiveScene().name != toScene)
            {
                Fail("arrived in the wrong scene");
            }

            if (spawn != null && (Mathf.Abs(combat.transform.position.y - spawn.transform.position.y) > 1.5f || Vector3.Distance(Flat(combat.transform.position), Flat(spawn.transform.position)) > 8f))
            {
                Fail("arrived away from the level's start (fell, or lost)");
            }

            if (Mathf.Abs(combat.Health.Current - setHealth) > 0.01f || potions.Count != setPotions)
            {
                Fail("carried state not applied");
            }

            if (LevelTransition.ControlReturns != 1)
            {
                Fail($"control returned {LevelTransition.ControlReturns} times");
            }

            SaveData save = SaveServices.Current.Load();
            entryAtSave = save?.entry;
            Note($"save: scene {save?.sceneName}, playtime {save?.playtimeSeconds:0.0}, flags {save?.flags?.Count}, entry {save?.entry}");
            if (save == null || save.sceneName != toScene || save.entry == null || save.entry.valid == false || Mathf.Abs(save.entry.health - setHealth) > 0.01f || save.entry.potions != setPotions)
            {
                Fail("the save does not hold the next level and the carried state");
            }

            float art = LevelTransition.ArtDownAt - LevelTransition.ArtUpAt;
            Note($"timings: threshold->art {LevelTransition.ArtUpAt - LevelTransition.ThresholdAt:0.00} s, loading screen {art:0.00} s, activation->ready {LevelTransition.ReadyAt - LevelTransition.ActivatedAt:0.00} s, art down->control {LevelTransition.ControlAt - LevelTransition.ArtDownAt:0.00} s, threshold->control {LevelTransition.ControlAt - LevelTransition.ThresholdAt:0.00} s");
            Note($"preload took {LevelTransition.PreloadSecondsMeasured:0.00} s (90%)");
            if (art < 5f)
            {
                Fail($"loading screen up only {art:0.00} s");
            }

            Note($"memory both resident: allocated {Mb(LevelTransition.BothResidentAllocatedBytes)}, reserved {Mb(LevelTransition.BothResidentReservedBytes)}, graphics {Mb(LevelTransition.BothResidentGraphicsBytes)}; peak allocated {Mb(LevelTransition.PeakAllocatedBytes)}, reserved {Mb(LevelTransition.PeakReservedBytes)}, graphics {Mb(LevelTransition.PeakGraphicsBytes)}");

            // Frames, per phase. The loading phase's activation frame is reported, not failed: it is under the art.
            var trace = LevelTransition.FrameTrace;
            foreach (LevelTransition.Phase p in Enum.GetValues(typeof(LevelTransition.Phase)))
            {
                var ms = trace.Where(f => (int)f.x == (int)p).Select(f => f.y).ToList();
                if (ms.Count == 0)
                {
                    continue;
                }

                int over = ms.Count(m => m > 50f);
                Note($"frames {p}: {ms.Count}, max {ms.Max():0.0} ms, mean {ms.Average():0.0} ms, over 50 ms: {over}");
                if (over > 0 && p != LevelTransition.Phase.Loading)
                {
                    Fail($"{over} frames over 50 ms in {p}");
                }
            }

            var spikes = trace.Select((f, i) => (f, i)).Where(x => x.f.y > 50f).Take(10).Select(x => $"#{x.i} {(LevelTransition.Phase)(int)x.f.x} {x.f.y:0}ms");
            Note("spikes: " + string.Join(", ", spikes));

            // Audio: the listener's own output, while two test tones (one on Music, one on SFX) play throughout.
            var byPhase = audioTrace.GroupBy(a => a.phase).Select(g => $"{(LevelTransition.Phase)g.Key}: max {g.Max(a => a.rms):0.0000} min {g.Min(a => a.rms):0.0000}");
            Note("audio rms " + string.Join("; ", byPhase));
            if (Tones == false)
            {
                Note("audio: not metered this run (Tones off)");
            }

            float loudest = audioTrace.Where(a => a.phase == (int)LevelTransition.Phase.Loading && a.t > LevelTransition.ArtUpAt + 0.05f && a.t < LevelTransition.ArtDownAt).Select(a => a.rms).DefaultIfEmpty(0f).Max();
            Note($"loading screen loudest rms {loudest:0.000000} (listener volume {AudioListener.volume:0.00} now)");
            if (Tones && loudest > 0f)
            {
                Fail("the loading screen was not silent");
            }

            File.WriteAllLines($"Temp/TransitionAudio_{fromScene}_{scenario}.csv", audioTrace.Select(a => $"{a.t - LevelTransition.ThresholdAt:0.000},{a.phase},{a.rms:0.000000}"));
            File.WriteAllLines($"Temp/TransitionFrames_{fromScene}_{scenario}.csv", trace.Select(f => $"{f.x},{f.y:0.00}"));
            foreach (string l in LevelTransition.Log)
            {
                log.AppendLine("  [flow] " + l);
            }
        }

        private static void CheckState(string when, CarriedState expect)
        {
            var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            var potions = combat.GetComponent<PlayerPotions>();
            Note($"{when}: scene {SceneManager.GetActiveScene().name}, health {combat.Health.Current}/{combat.Health.Max}, potions {potions.Count} (expected {expect})");
            if (expect == null || Mathf.Abs(combat.Health.Current - expect.health) > 0.01f || potions.Count != expect.potions)
            {
                Fail($"{when}: not the carried-in state");
            }
        }

        private static void Finish(string why)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Time.captureFramerate = 0;
            SaveServices.Current = new JsonFileSaveService();
            ReleaseKeys();
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
            InputSystem.settings.backgroundBehavior = savedBackground;
            if (toneMusic != null)
            {
                UnityEngine.Object.Destroy(toneMusic.gameObject);
            }

            Note($"finished ({why}); console errors during the run: {errors - errorsAtStart}");
            log.Insert(0, (fails.Count == 0 ? "PASS" : $"FAIL ({fails.Count}): " + string.Join(" | ", fails)) + "\n");
            File.WriteAllText(ResultPath, log.ToString());
            Step done = Step.Done;
            step = done;
        }

        // --- Helpers ---

        private static void Teleport(Vector3 at, Vector3 facing)
        {
            var movement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            facing.y = 0f;
            movement.Teleport(SeamGround(at), Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing : Vector3.forward));
        }

        private static Vector3 SeamGround(Vector3 p)
        {
            int mask = LayerMask.GetMask("Ground", "Default", "Obstacles");
            return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, mask, QueryTriggerInteraction.Ignore) ? hit.point + Vector3.up * 0.05f : p;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static string Mb(long bytes) => $"{bytes / (1024f * 1024f):0} MB";

        private static void Shoot()
        {
            if (Shots == false || Now - lastShot < ShotEvery)
            {
                return;
            }

            lastShot = Now;
            string phase = LevelTransition.Active ? LevelTransition.Current.ToString() : "Control";
            ScreenCapture.CaptureScreenshot($"Temp/TransitionShots/{fromScene}_{scenario}_{shotIndex++:00}_{phase}.png");
        }

        private static void Meter()
        {
            AudioListener.GetOutputData(samples, 0);
            float sum = 0f;
            foreach (float s in samples)
            {
                sum += s * s;
            }

            audioTrace.Add((Now, LevelTransition.Active ? (int)LevelTransition.Current : -1, Mathf.Sqrt(sum / samples.Length)));
        }

        // Two quiet sine loops that live through the scene change: one routed to Music, one to SFX.
        private static void MakeTones()
        {
            var mixer = Resources.Load<AudioMixer>("KneelMixer");
            var go = new GameObject("[TestTones]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            toneMusic = Tone(go, "Music", 220f, mixer);
            toneSfx = Tone(go, "SFX", 330f, mixer);
        }

        private static AudioSource Tone(GameObject host, string group, float hz, AudioMixer mixer)
        {
            const int rate = 44100;
            var data = new float[rate];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * i / rate) * 0.25f;
            }

            var clip = AudioClip.Create("tone" + hz, rate, 1, rate, false);
            clip.SetData(data, 0);
            var src = host.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0.5f;
            AudioMixerGroup[] g = mixer != null ? mixer.FindMatchingGroups(group) : null;
            if (g != null && g.Length > 0)
            {
                src.outputAudioMixerGroup = g[0];
            }

            src.Play();
            return src;
        }

        // --- Real key events ---

        private static void PressAll()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null)
            {
                return;
            }

            var keys = new[] { Key.Escape, Key.F1, Key.F2, Key.Space, Key.LeftCtrl, Key.R, Key.Q, Key.F, Key.E };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            if (mouse != null)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            }

            EditorApplication.delayCall += () =>
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (mouse != null)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                }
            };
        }

        private static void HoldKeys(bool hold)
        {
            if (hold == false || Keyboard.current == null)
            {
                return;
            }

            heldDown = true;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.W));
            if (Mouse.current != null)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState { buttons = 1 });
            }
        }

        private static void ReleaseKeys()
        {
            if (heldDown == false)
            {
                return;
            }

            heldDown = false;
            if (Keyboard.current != null)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }

            if (Mouse.current != null)
            {
                InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            }
        }
    }
}
