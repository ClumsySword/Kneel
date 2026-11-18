using System.Collections;
using System.Collections.Generic;
using Kneel.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.Core
{
    // The walk from one level into the next (L1 -> L2 -> L3 -> L4 -> L5), one flow for all four seams:
    //
    //   preload   the next level loads in the background during play (PreloadTrigger), waiting at 90%
    //   threshold the exit's trigger: pause, F1 and input are off, held input dropped, timeScale 1, Kael can't be
    //             hurt or targeted, the cursor and aim ember go
    //   outro     he sheathes and the game walks him out (or through the door beat), the camera runs the exit's
    //             keys, the grade leans toward the next level, the music fades
    //   loading   black, the save, the art and its line (at least minLoadingScreen), the next level activated
    //             and settled behind it: player placed at the seam, carried state applied, shaders warmed on the
    //             intro's opening shot, frame times steady
    //   intro     the arrival's camera answers the outro, the grade starts leaning back and settles, sound fades in,
    //             he walks in to the spawn, the camera eases into the gameplay framing, control returns once
    //
    // Everything is timed on real time. Other loads (Levels menu, Continue, a death reload) skip the outro and intro
    // and use SceneLoader's plain fade; on those, the save's carried state for that level is applied the same way.
    public class LevelTransition : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Threshold,
            Outro,
            Loading,
            Intro,
        }

        private static LevelTransition instance;

        public static Phase Current { get; private set; } = Phase.Idle;

        // From the threshold until control returns.
        public static bool Active => Current != Phase.Idle;

        // From the next level's activation until control returns: scene scripts that would place the player on
        // their own at start-up (ShrineRest) leave him where the seam put him.
        public static bool IsArriving { get; private set; }

        // --- Telemetry (the playtests read it; nothing in the game does) ---
        public static string LastFrom { get; private set; }
        public static string LastTo { get; private set; }
        public static float ThresholdAt { get; private set; } = -1f;
        public static float ArtUpAt { get; private set; } = -1f;
        public static float ArtDownAt { get; private set; } = -1f;
        public static float ActivatedAt { get; private set; } = -1f;
        public static float ReadyAt { get; private set; } = -1f;
        public static float ControlAt { get; private set; } = -1f;
        public static int ControlReturns { get; private set; }
        public static float PreloadSecondsMeasured { get; private set; } = -1f;
        public static long PeakAllocatedBytes { get; private set; }
        public static long PeakReservedBytes { get; private set; }
        public static long PeakGraphicsBytes { get; private set; }
        public static long BothResidentAllocatedBytes { get; private set; }
        public static long BothResidentReservedBytes { get; private set; }
        public static long BothResidentGraphicsBytes { get; private set; }
        public static readonly List<Vector2> FrameTrace = new List<Vector2>(); // (phase, ms) per frame
        public static readonly List<string> Log = new List<string>();

        private LevelTransitionSettings settings;
        private LevelTransitionCatalog catalog;
        private LoadingScreen screen;
        private CarriedState carried;
        private string arrivingFrom;
        private string arrivingTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            Current = Phase.Idle;
            IsArriving = false;
            ControlReturns = 0;
            ThresholdAt = ArtUpAt = ArtDownAt = ActivatedAt = ReadyAt = ControlAt = PreloadSecondsMeasured = -1f;
            PeakAllocatedBytes = PeakReservedBytes = PeakGraphicsBytes = 0;
            BothResidentAllocatedBytes = BothResidentReservedBytes = BothResidentGraphicsBytes = 0;
            FrameTrace.Clear();
            Log.Clear();
        }

        // Every level load applies the save's carried state for that level, unless it arrives through a seam.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            OnAnySceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static LevelTransition Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[LevelTransition]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<LevelTransition>();
                }

                return instance;
            }
        }

        public static LevelTransitionSettings Settings => Instance.LoadSettings();

        private LevelTransitionSettings LoadSettings()
        {
            if (settings == null)
            {
                settings = Resources.Load<LevelTransitionSettings>(LevelTransitionSettings.ResourceName);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LevelTransitionSettings>();
                }
            }

            return settings;
        }

        private void Awake()
        {
            LoadSettings();
            catalog = LevelTransitionCatalog.Load();
            screen = LoadingScreen.Create(transform);
        }

        // --- Entry points ---

        // From a level's PreloadTrigger: start loading what comes after `fromScene`.
        public static void RequestPreload(string fromScene)
        {
            string next = LevelCatalog.NextOf(fromScene);
            if (next == null || Active)
            {
                return;
            }

            if (SceneLoader.PendingScene == next)
            {
                return;
            }

            if (SceneLoader.Instance.BeginPreload(next))
            {
                Instance.Note($"preload {next} started");
            }
        }

        // From a level's LevelExit trigger. Fires once.
        public static void Begin(LevelExit exit)
        {
            if (Active || exit == null || SceneLoader.IsLoading || PauseMenu.IsPaused)
            {
                return;
            }

            string from = exit.gameObject.scene.name;
            string to = LevelCatalog.NextOf(from);
            if (to == null || SceneLoader.CanLoad(to) == false)
            {
                Debug.LogWarning($"LevelTransition: '{from}' has no next level in LevelCatalog (or it isn't in the build).", exit);
                return;
            }

            var player = FindAnyObjectByType<PlayerCombat>();
            if (player == null || player.IsDead)
            {
                return;
            }

            Instance.StartCoroutine(Instance.Run(exit, from, to));
        }

        private void Note(string message)
        {
            Log.Add($"{Time.realtimeSinceStartup:0.000} {message}");
        }

        // --- The flow ---

        private IEnumerator Run(LevelExit exit, string from, string to)
        {
            LoadSettings();
            catalog = LevelTransitionCatalog.Load();
            LastFrom = from;
            LastTo = to;
            FrameTrace.Clear();
            Log.Clear();
            PeakAllocatedBytes = PeakReservedBytes = PeakGraphicsBytes = 0;
            ArtUpAt = ArtDownAt = ActivatedAt = ReadyAt = ControlAt = -1f;

            // Threshold.
            SetPhase(Phase.Threshold);
            ThresholdAt = Time.realtimeSinceStartup;
            var combat = FindAnyObjectByType<PlayerCombat>();
            var movement = combat.GetComponent<PlayerMovement>();
            var camera = FindAnyObjectByType<PlayerCamera>();
            TakeOver(combat, movement, suspendInput: true);
            exit.Open();

            // The preload should be done long before this; if not (a level without a trigger, or a test), start it.
            if (SceneLoader.PendingScene != to)
            {
                SceneLoader.Instance.BeginPreload(to);
                Note($"preload {to} started late, at the exit");
            }

            // Outro.
            SetPhase(Phase.Outro);
            var grade = new TransitionGrade(catalog != null ? catalog.LookOf(to) : null);
            float outro = Mathf.Clamp(exit.outroTime, 0.5f, settings.outroMaxTime);
            Coroutine walk = StartCoroutine(WalkOut(exit, combat, movement));
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                float t = Time.realtimeSinceStartup - t0;
                Hold(combat);
                combat.RequestSheathe();
                DriveCamera(camera, exit.cameraKeys, t, settings.outroCameraBlend);
                float lean = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / outro));
                grade.Set(settings.outroLean * lean);
                float musicT = Mathf.Clamp01(t / (outro * settings.musicFadeShare));
                SettingsRuntime.SetGains(1f, 1f - Mathf.SmoothStep(0f, 1f, musicT));
                if (t >= outro)
                {
                    break;
                }

                yield return null;
            }

            // Loading screen: to black (everything goes silent with it).
            SetPhase(Phase.Loading);
            yield return FadeBlack(1f, settings.fadeToBlack, (k) => SettingsRuntime.SetGains(1f - k, 0f), combat);
            SettingsRuntime.SetGains(0f, 0f);
            StopCoroutine(walk);
            movement.scriptedMove = Vector3.zero;

            // The save: the next level's start, and everything he carries into it.
            carried = CarriedStates.Capture(combat.gameObject, to);
            bool saved = SaveServices.Update(d =>
            {
                d.sceneName = to;
                d.playtimeSeconds = Playtime.Seconds;
                d.entry = carried.Clone();
            });
            Note($"saved {to} ({(saved ? "ok" : "FAILED")}): {carried}");

            LevelTransitionCatalog.Seam seam = catalog != null ? catalog.SeamFrom(from) : null;
            screen.Show(catalog, seam, settings.artPush, settings.minLoadingScreen);
            ArtUpAt = Time.realtimeSinceStartup;
            yield return SceneLoader.Instance.FadeTo(0f, settings.artFadeIn);

            // Wait for the preload, then let it in while the art is up.
            float artUp = ArtUpAt;
            while (SceneLoader.PreloadReady == false)
            {
                if (SceneLoader.PendingScene != to)
                {
                    SceneLoader.Instance.BeginPreload(to);
                }

                screen.WantEmber(Time.realtimeSinceStartup - artUp > settings.emberAfter);
                SampleMemory(false);
                yield return null;
            }

            PreloadSecondsMeasured = SceneLoader.PreloadSeconds;
            SampleMemory(true);
            arrivingFrom = from;
            arrivingTo = to;
            IsArriving = true;
            AsyncOperation op = SceneLoader.Instance.ActivatePreload();
            Note($"activating {to} (preload took {PreloadSecondsMeasured:0.00} s)");
            while (op != null && op.isDone == false)
            {
                screen.WantEmber(Time.realtimeSinceStartup - artUp > settings.emberAfter);
                yield return null;
            }

            ActivatedAt = Time.realtimeSinceStartup;
            Note($"activated {to}");

            // Settle: the arrival placed him in OnAnySceneLoaded; once every Start has run, his state goes back on.
            combat = FindAnyObjectByType<PlayerCombat>();
            movement = combat != null ? combat.GetComponent<PlayerMovement>() : null;
            camera = FindAnyObjectByType<PlayerCamera>();
            LevelArrival arrival = FindArrival(to, from);
            yield return null;
            CarriedStates.Apply(combat != null ? combat.gameObject : null, carried);
            Note($"applied {carried}");
            if (combat != null)
            {
                TakeOver(combat, movement, suspendInput: false);
            }

            if (arrival != null && arrival.door != null)
            {
                arrival.door.SetOpen(true);
            }

            var back = new TransitionGrade(catalog != null ? catalog.LookOf(from) : null);
            back.Set(settings.introLean);
            DriveCamera(camera, arrival != null ? arrival.cameraKeys : null, 0f, 0f);

            int warm = 0;
            int stable = 0;
            float settleStart = Time.realtimeSinceStartup;
            while (true)
            {
                Hold(combat);
                DriveCamera(camera, arrival != null ? arrival.cameraKeys : null, 0f, 0f);
                SampleMemory(false);
                float ms = Time.unscaledDeltaTime * 1000f;
                if (warm < settings.warmFrames)
                {
                    warm++;
                }
                else
                {
                    stable = ms < settings.stableFrameMs ? stable + 1 : 0;
                }

                bool settled = stable >= settings.stableFrames || Time.realtimeSinceStartup - settleStart > 12f;
                bool longEnough = Time.realtimeSinceStartup - artUp >= settings.minLoadingScreen;
                screen.WantEmber(settled == false && Time.realtimeSinceStartup - artUp > settings.emberAfter);
                if (settled && longEnough)
                {
                    break;
                }

                yield return null;
            }

            ReadyAt = Time.realtimeSinceStartup;
            Note($"ready after {ReadyAt - ActivatedAt:0.00} s settling");

            // Art away, then into the intro.
            screen.WantEmber(false);
            ArtDownAt = Time.realtimeSinceStartup;
            yield return FadeBlack(1f, settings.artFadeOut, null, combat);
            screen.Hide();

            SetPhase(Phase.Intro);
            yield return Intro(arrival, combat, movement, camera, back);

            // Control returns, once.
            Release(combat, movement);
            ControlAt = Time.realtimeSinceStartup;
            ControlReturns++;
            Note($"control returned in {to}");
            IsArriving = false;
            SetPhase(Phase.Idle);
        }

        private IEnumerator Intro(LevelArrival arrival, PlayerCombat combat, PlayerMovement movement, PlayerCamera camera, TransitionGrade back)
        {
            walkingIn = arrival != null && movement != null;
            Coroutine walk = walkingIn ? StartCoroutine(WalkIn(arrival, movement)) : null;
            float t0 = Time.realtimeSinceStartup;
            float keys = arrival != null ? TransitionCamera.Duration(arrival.cameraKeys) : 0f;
            bool released = false;
            StartCoroutine(SceneLoader.Instance.FadeTo(0f, settings.fadeFromBlack));

            // Sound, the grade's lean back and the door behind him run on their own clock and may finish after control
            // has returned: none of them is a reason to keep the player waiting.
            StartCoroutine(Settle(arrival, back, t0));
            while (true)
            {
                float t = Time.realtimeSinceStartup - t0;
                Hold(combat);

                if (released == false)
                {
                    DriveCamera(camera, arrival != null ? arrival.cameraKeys : null, t, 0f);
                    if (t >= keys && (walkingIn == false || t >= keys + 0.25f))
                    {
                        if (camera != null)
                        {
                            camera.ClearOverride(settings.introCameraRelease);
                        }

                        released = true;
                    }
                }

                bool cameraHome = released && (camera == null || camera.HasOverride == false);
                if ((cameraHome && walkingIn == false) || t > settings.introMaxTime + settings.introCameraRelease)
                {
                    break;
                }

                yield return null;
            }

            if (walk != null)
            {
                StopCoroutine(walk);
            }
        }

        private IEnumerator Settle(LevelArrival arrival, TransitionGrade back, float t0)
        {
            float fadeIn = Mathf.Max(settings.soundFadeIn, 0.01f);
            float lean = Mathf.Max(settings.introLeanSettle, 0.01f);
            bool doorClosing = false;
            Scene arrived = SceneManager.GetActiveScene();
            while (true)
            {
                // A new scene (a quick death reload) takes the grade with it: leave its look alone, just the sound back.
                if (SceneManager.GetActiveScene() != arrived)
                {
                    SettingsRuntime.SetGains(1f, 1f);
                    yield break;
                }

                float t = Time.realtimeSinceStartup - t0;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeIn));
                SettingsRuntime.SetGains(k, k);
                back.Set(settings.introLean * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / lean))));
                if (doorClosing == false && arrival != null && arrival.door != null && arrival.closeAt >= 0f && t >= arrival.closeAt)
                {
                    doorClosing = true;
                    StartCoroutine(arrival.door.Close());
                }

                bool doorDone = arrival == null || arrival.door == null || arrival.closeAt < 0f || doorClosing;
                if (t >= fadeIn && t >= lean && doorDone)
                {
                    break;
                }

                yield return null;
            }

            back.Remove();
            SettingsRuntime.SetGains(1f, 1f);
        }

        private bool walkingIn;

        // --- Kael ---

        private void TakeOver(PlayerCombat combat, PlayerMovement movement, bool suspendInput)
        {
            Time.timeScale = 1f;
            if (suspendInput)
            {
                GameplayInput.Suspend();
            }
            else
            {
                GameplayInput.HoldNew();
            }

            combat.ClearBufferedInput();
            combat.Shield(3600f);
            if (combat.Health != null)
            {
                combat.Health.invincible = true;
            }

            if (movement != null)
            {
                movement.ClearInput();
                movement.scriptedTurnRate = settings.walkTurnRate;
            }
        }

        // Every frame of a transition: time runs at 1 (hit-stop may have been mid-freeze), no new input wakes.
        private void Hold(PlayerCombat combat)
        {
            Time.timeScale = 1f;
            if (IsArriving)
            {
                GameplayInput.HoldNew();
            }
            else
            {
                GameplayInput.Suspend();
            }

            if (combat != null)
            {
                combat.ClearBufferedInput();
            }
        }

        private void Release(PlayerCombat combat, PlayerMovement movement)
        {
            if (movement != null)
            {
                movement.scriptedMove = null;
                movement.scriptedFacing = null;
                movement.ClearInput();
            }

            if (combat != null)
            {
                combat.ClearBufferedInput();
                combat.ReleaseShield();
                if (combat.Health != null)
                {
                    combat.Health.invincible = false;
                }
            }

            GameplayInput.Forget();
            GameplayInput.ReleaseHeld();
        }

        private IEnumerator WalkOut(LevelExit exit, PlayerCombat combat, PlayerMovement movement)
        {
            if (exit.door != null && exit.doorStand != null)
            {
                yield return WalkTo(movement, exit.doorStand.position, exit.doorStand.position + exit.doorStand.forward);
                movement.scriptedMove = Vector3.zero;
                // Square to the doors (the stand's forward), never toward the door component's own transform.
                movement.scriptedFacing = Flat(exit.doorStand.forward);
                // He waits for the sword to be away before the doors give.
                float wait = Time.realtimeSinceStartup;
                while (combat.State != PlayerCombat.CombatState.Sheathed && Time.realtimeSinceStartup - wait < settings.sheatheTimeout)
                {
                    yield return null;
                }

                yield return exit.door.Open();
            }

            foreach (Transform p in exit.walkPath)
            {
                if (p != null)
                {
                    yield return WalkTo(movement, p.position, null);
                }
            }

            // Past the last point, keep walking the same way until the black.
            Vector3 on = movement.transform.forward;
            movement.scriptedFacing = null;
            while (true)
            {
                movement.scriptedMove = on * settings.walkInput;
                yield return null;
            }
        }

        private IEnumerator WalkIn(LevelArrival arrival, PlayerMovement movement)
        {
            foreach (Transform p in arrival.walkPath)
            {
                if (p != null)
                {
                    yield return WalkTo(movement, p.position, null);
                }
            }

            // Stop where the level's spawn is, facing on.
            movement.scriptedMove = Vector3.zero;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            walkingIn = false;
        }

        private IEnumerator WalkTo(PlayerMovement movement, Vector3 target, Vector3? faceAtEnd)
        {
            float started = Time.realtimeSinceStartup;
            while (true)
            {
                Vector3 to = Flat(target - movement.transform.position);
                float distance = to.magnitude;
                if (distance <= settings.waypointReach || Time.realtimeSinceStartup - started > 20f)
                {
                    break;
                }

                // Ease off over the last metre only when this is a stop (the door), so he plants rather than skids.
                float input = settings.walkInput;
                if (faceAtEnd.HasValue)
                {
                    input *= Mathf.Clamp(distance / 1.2f, 0.35f, 1f);
                }

                movement.scriptedMove = to / distance * input;
                movement.scriptedFacing = null;
                yield return null;
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // --- Camera ---

        private static void DriveCamera(PlayerCamera camera, CameraKey[] keys, float t, float blend)
        {
            if (camera == null || keys == null || keys.Length == 0)
            {
                return;
            }

            if (TransitionCamera.Evaluate(keys, t, out Vector3 position, out Quaternion rotation, out float fov))
            {
                camera.SetOverride(position, rotation, blend, fov);
            }
        }

        // --- Black, audio, memory ---

        private IEnumerator FadeBlack(float to, float duration, System.Action<float> alongside, PlayerCombat combat)
        {
            float from = SceneLoader.BlackAlpha;
            for (float t = 0f; t < duration; t += UITween.DeltaTime)
            {
                float k = UITween.Smooth(t / duration);
                SceneLoader.Instance.SetBlack(Mathf.Lerp(from, to, k));
                alongside?.Invoke(k);
                Hold(combat);
                yield return null;
            }

            SceneLoader.Instance.SetBlack(to);
            alongside?.Invoke(1f);
        }

        private static void SampleMemory(bool bothResident)
        {
            long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            long graphics = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver();
            PeakAllocatedBytes = System.Math.Max(PeakAllocatedBytes, allocated);
            PeakReservedBytes = System.Math.Max(PeakReservedBytes, reserved);
            PeakGraphicsBytes = System.Math.Max(PeakGraphicsBytes, graphics);
            if (bothResident)
            {
                BothResidentAllocatedBytes = allocated;
                BothResidentReservedBytes = reserved;
                BothResidentGraphicsBytes = graphics;
            }
        }

        private static void SetPhase(Phase phase)
        {
            Current = phase;
        }

        private void LateUpdate()
        {
            if (Active == false)
            {
                return;
            }

            Time.timeScale = 1f;
            Cursor.visible = false;
            FrameTrace.Add(new Vector2((int)Current, Time.unscaledDeltaTime * 1000f));
        }

        // --- Arrival and plain loads ---

        private static LevelArrival FindArrival(string scene, string from)
        {
            LevelArrival any = null;
            foreach (LevelArrival a in FindObjectsByType<LevelArrival>(FindObjectsInactive.Exclude))
            {
                if (a.gameObject.scene.name != scene)
                {
                    continue;
                }

                if (a.fromScene == from)
                {
                    return a;
                }

                any ??= a;
            }

            return any;
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || SceneLoader.IsDiscarding(scene) || LevelCatalog.IsLevel(scene.name) == false)
            {
                return;
            }

            if (instance != null && IsArriving && scene.name == instance.arrivingTo)
            {
                instance.PlaceArrival(scene);
                return;
            }

            SaveData save = SaveServices.Current.Load();
            if (save != null && save.sceneName == scene.name && save.entry != null && save.entry.valid && save.entry.scene == scene.name)
            {
                Instance.StartCoroutine(ApplyAfterStart(scene, save.entry));
            }
        }

        // Before any Start: input held, Kael at the seam (so the camera's own Start snaps to him there).
        private void PlaceArrival(Scene scene)
        {
            GameplayInput.Forget();
            GameplayInput.HoldNew();
            LevelArrival arrival = FindArrival(scene.name, arrivingFrom);
            foreach (PlayerMovement m in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude))
            {
                if (m.gameObject.scene != scene)
                {
                    continue;
                }

                if (arrival != null && arrival.start != null)
                {
                    m.Teleport(arrival.start.position, Quaternion.LookRotation(Flat(arrival.start.forward)));
                }

                Note($"placed at {(arrival != null && arrival.start != null ? arrival.start.position.ToString() : "the scene's own start (no LevelArrival)")}");
            }
        }

        private static IEnumerator ApplyAfterStart(Scene scene, CarriedState state)
        {
            yield return null;
            if (scene.isLoaded == false)
            {
                yield break;
            }

            foreach (PlayerCombat c in FindObjectsByType<PlayerCombat>(FindObjectsInactive.Exclude))
            {
                if (c.gameObject.scene == scene)
                {
                    CarriedStates.Apply(c.gameObject, state);
                }
            }
        }
    }
}
