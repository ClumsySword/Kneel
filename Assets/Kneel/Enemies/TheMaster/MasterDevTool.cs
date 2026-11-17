using System.Collections;
using Kneel.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// F2 in L5 (Editor and development builds only): the Boss Lab, for testing every move of the Master and the Ash
// Colossus and replaying the fight's cutscenes.
// - Kael: invincible (his blows still land, so the grabs, kneels and knockdowns play out, but Kael loses no health), and
//   optionally his blows pass straight through him instead.
// - The boss: "Wait between moves" holds the picker, so he only starts the move asked for (MasterBody.DevHold);
//   "Repeat" asks for it again each time he's ready. A move of the other phase starts that phase first. Time scale.
// - Start and cutscenes reload the hall first (unless unticked), so each runs on an unbroken floor. Save flags go to a
//   sandbox the moment one is used (SaveFlags.Sandbox): the save file is never touched.
// Spawned by itself in any scene with a MasterBoss; nothing in the scene refers to it.
public class MasterDevTool : MonoBehaviour
{
    private enum Run
    {
        None, Phase1, Phase2, Reveal, IntroFirst, IntroRetry, P1Half, UnmakingFirst, UnmakingSkippable, P2Half, Finisher,
    }

    public static bool IsOpen { get; private set; }
    public static bool IsPointerOverPanel { get; private set; }

    // What the reloaded scene's tool runs, and the panel's state, carried over the reload.
    private static Run pendingRun;
    private static bool sticky;
    private static bool invincible;
    private static bool passThrough;
    private static bool repeat;
    private static bool reloadFirst = true;
    private static bool stayInL5 = true;
    private static float speed = 1f;
    private static bool wasOpen;
    private static Vector2 scroll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pendingRun = Run.None;
        sticky = false;
        invincible = false;
        passThrough = false;
        repeat = false;
        reloadFirst = true;
        stayInL5 = true;
        speed = 1f;
        wasOpen = false;
        scroll = Vector2.zero;
        carryP1 = null;
        carryP2 = null;
        IsOpen = false;
        IsPointerOverPanel = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        if (Debug.isDebugBuild == false)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<MasterDevTool>() != null || FindAnyObjectByType<MasterBoss>() == null)
            return;
        new GameObject("MasterDevTool").AddComponent<MasterDevTool>();
    }

    private const float PanelWidth = 380f;

    private MasterBoss boss;
    private PlayerCombat player;
    private Health playerHealth;
    private bool open;
    private string status = "";
    private Coroutine running;

    // The move asked for, waiting until he's ready for it; the last one (for Repeat).
    private MasterP1.MoveKind? queuedP1;
    private AshColossus.MoveKind? queuedP2;
    private MasterP1.MoveKind? lastP1;
    private AshColossus.MoveKind? lastP2;
    private float readySince = -1f;

    private void Start()
    {
        boss = FindAnyObjectByType<MasterBoss>();
        player = FindAnyObjectByType<PlayerCombat>();
        playerHealth = player != null ? player.GetComponent<Health>() : null;
        open = wasOpen;
        if (pendingRun != Run.None)
        {
            Run run = pendingRun;
            pendingRun = Run.None;
            running = StartCoroutine(Play(run));
        }
    }

    private void OnDestroy()
    {
        if (player != null) player.devPassThrough = false;
        if (playerHealth != null) playerHealth.invincible = false;
        IsOpen = false;
        IsPointerOverPanel = false;
        if (Mathf.Approximately(Time.timeScale, speed) && speed != 1f)
            Time.timeScale = 1f;
    }

    // --- Each frame ---

    private void Update()
    {
        // The pause menu (and the death screen, which is the pause menu) comes first: the lab hides under it and F2
        // does nothing there (it could be a key being rebound on the Controls page).
        bool paused = Kneel.UI.PauseMenu.IsPaused;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (paused == false && keyboard != null && keyboard.f2Key.wasPressedThisFrame)
        {
            open = open == false;
            wasOpen = open;
        }

        IsOpen = open && paused == false;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (IsOpen && mouse != null)
        {
            Vector2 pointer = mouse.position.ReadValue();
            IsPointerOverPanel = PanelRect().Contains(new Vector2(pointer.x, Screen.height - pointer.y));
        }
        else
        {
            IsPointerOverPanel = false;
        }

        if (player == null || boss == null)
            return;

        // Kael.
        player.devPassThrough = invincible && passThrough;
        if (playerHealth != null)
            playerHealth.invincible = invincible;

        // The boss.
        MasterBody.DevHold = sticky;
        FeedQueue();

        // Slow motion, under hit-stop and the pause menu (both set the time scale themselves).
        if (speed != 1f && Kneel.UI.PauseMenu.IsPaused == false && Mathf.Approximately(Time.timeScale, 1f))
            Time.timeScale = speed;
    }

    // Hands the asked-for move to whichever body is fighting, once he's standing ready between moves.
    private void FeedQueue()
    {
        MasterP1 master = boss.master;
        AshColossus colossus = boss.colossus;
        bool p1Ready = boss.Current == MasterBoss.Beat.Phase1 && master.Engaged && master.Untouchable == false && master.InCircle;
        bool p2Ready = boss.Current == MasterBoss.Beat.Phase2 && colossus != null && colossus.Engaged && colossus.Untouchable == false && colossus.InCircle;
        if (p1Ready == false && p2Ready == false)
        {
            readySince = -1f;
            return;
        }

        if (readySince < 0f)
            readySince = Time.time;

        if (repeat && queuedP1 == null && queuedP2 == null && Time.time - readySince > 1.2f)
        {
            if (p1Ready && lastP1.HasValue) queuedP1 = lastP1;
            if (p2Ready && lastP2.HasValue) queuedP2 = lastP2;
        }

        if (p1Ready && queuedP1.HasValue)
        {
            MasterP1.ForceNext = queuedP1;
            queuedP1 = null;
            readySince = -1f;
            master.BeginNextMove();
        }
        else if (p2Ready && queuedP2.HasValue)
        {
            AshColossus.ForceNext = queuedP2;
            queuedP2 = null;
            readySince = -1f;
            colossus.BeginNextMove();
        }
    }

    // --- Kael ---

    private void PlaceKael(float x, float z, Vector3 facing)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        var at = new Vector3(x, player.transform.position.y, z);
        if (Physics.Raycast(new Vector3(x, 14f, z), Vector3.down, out RaycastHit ground, 12f, LayerMask.GetMask("Default", "Ground", "Obstacles"), QueryTriggerInteraction.Ignore))
            at = ground.point;
        player.transform.SetPositionAndRotation(at + Vector3.up * 0.05f, Quaternion.LookRotation(facing));
        if (controller != null) controller.enabled = true;
        player.ReleaseRestriction();
    }

    // --- Starting things ---

    // A start or cutscene. A move asked for (AskP1/AskP2) stays queued through it.
    private void Begin(Run run, bool keepQueue = false)
    {
        status = "";
        if (keepQueue == false)
        {
            queuedP1 = null;
            queuedP2 = null;
        }

        SaveFlags.BeginSandbox();
        if (reloadFirst || player == null || player.IsDead)
        {
            pendingRun = run;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(Play(run));
    }

    private IEnumerator Play(Run run)
    {
        // A frame for the reloaded scene's Start calls (the boss seats himself, the hall finds its actors).
        yield return null;
        MasterBoss.ForceShortIntro = false;
        MasterBoss.ForceSkippableUnmaking = false;
        MasterFinisher.HoldMainMenu = stayInL5;

        switch (run)
        {
            case Run.Phase1:
                yield return EnsurePhase1();
                break;
            case Run.Phase2:
                yield return EnsurePhase2();
                break;
            case Run.Reveal:
                boss.Seat();
                SaveFlags.SandboxClear("L5.Reveal");
                // Just over the doors' threshold: the reveal starts at once.
                PlaceKael(54f, boss.doorZ + 0.6f, Vector3.forward);
                break;
            case Run.IntroFirst:
            case Run.IntroRetry:
                boss.Seat();
                SaveFlags.Set("L5.Reveal");
                if (run == Run.IntroFirst) SaveFlags.SandboxClear("L5.MasterMet"); else SaveFlags.Set("L5.MasterMet");
                // Just over the aggro line.
                PlaceKael(54f, boss.aggroZ + 0.6f, Vector3.forward);
                break;
            case Run.P1Half:
                yield return EnsurePhase1();
                yield return WaitUntil(() => boss.master.InCircle && boss.master.Untouchable == false, 8f);
                boss.master.DevHalfBeat();
                break;
            case Run.UnmakingFirst:
            case Run.UnmakingSkippable:
                if (run == Run.UnmakingFirst) SaveFlags.SandboxClear("L5.Unmade"); else SaveFlags.Set("L5.Unmade");
                yield return EnsurePhase1();
                yield return WaitUntil(() => boss.master.Untouchable == false, 8f);
                boss.EndPhase1();
                break;
            case Run.P2Half:
                yield return EnsurePhase2();
                yield return WaitUntil(() => boss.colossus.InCircle && boss.colossus.Untouchable == false, 8f);
                boss.colossus.DevHalfBeat();
                break;
            case Run.Finisher:
                yield return EnsurePhase2();
                yield return WaitUntil(() => boss.colossus.Untouchable == false, 8f);
                boss.EndPhase2();
                status = "Walk up to him; any blow once his palm is down ends him.";
                break;
        }

        running = null;
    }

    private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (condition() == false && Time.unscaledTime < end)
            yield return null;
    }

    // Phase 1 under way, Kael 10 m down the hall from him.
    private IEnumerator EnsurePhase1()
    {
        if (boss.Current == MasterBoss.Beat.Phase1)
            yield break;
        boss.DevStartFight();
        Vector3 foot = boss.daisFoot.position;
        PlaceKael(foot.x, foot.z - 10f, Vector3.forward);
        yield return WaitUntil(() => boss.Current == MasterBoss.Beat.Phase1, 5f);
    }

    // Phase 2 under way (no Unmaking), the Colossus in the middle of the arena, Kael 12 m from him.
    private IEnumerator EnsurePhase2()
    {
        if (boss.Current == MasterBoss.Beat.Phase2)
            yield break;
        var filter = new NavMeshQueryFilter { agentTypeID = boss.colossus.agent.agentTypeID, areaMask = NavMesh.AllAreas };
        Vector3 at = new Vector3(54f, boss.daisFoot.position.y, 454f);
        if (NavMesh.SamplePosition(at, out NavMeshHit hit, 6f, filter))
            at = hit.position;
        boss.StopBeats();
        boss.StartPhase2At(at);
        PlaceKael(at.x, at.z - 12f, Vector3.forward);
        yield return WaitUntil(() => boss.colossus.Untouchable == false, 3f);
    }

    private void AskP1(MasterP1.MoveKind kind)
    {
        lastP1 = kind;
        lastP2 = null;
        queuedP2 = null;
        queuedP1 = kind;
        if (boss.Current != MasterBoss.Beat.Phase1)
            Begin(Run.Phase1, keepQueue: true);
    }

    private void AskP2(AshColossus.MoveKind kind)
    {
        lastP2 = kind;
        lastP1 = null;
        queuedP1 = null;
        queuedP2 = kind;
        if (boss.Current != MasterBoss.Beat.Phase2)
            Begin(Run.Phase2, keepQueue: true);
    }

    // The queue outlives a reload only through these.
    private static MasterP1.MoveKind? carryP1;
    private static AshColossus.MoveKind? carryP2;

    private void OnDisable()
    {
        carryP1 = queuedP1 ?? lastP1;
        carryP2 = queuedP2 ?? lastP2;
    }

    private void Awake()
    {
        if (pendingRun == Run.Phase1 || pendingRun == Run.Phase2)
        {
            queuedP1 = pendingRun == Run.Phase1 ? carryP1 : null;
            queuedP2 = pendingRun == Run.Phase2 ? carryP2 : null;
            lastP1 = queuedP1;
            lastP2 = queuedP2;
        }

        carryP1 = null;
        carryP2 = null;
    }

    // --- The panel ---

    // On the right of the screen, clear of the F1 tuning overlay on the left: with both open on a narrow screen, the
    // lab starts where the overlay ends and gets narrower rather than covering it.
    private static Rect PanelRect()
    {
        float left = DebugTuningOverlay.IsOpen ? DebugTuningOverlay.PanelRight + 10f : 10f;
        float x = Mathf.Max(left, Screen.width - PanelWidth - 10f);
        return new Rect(x, 10f, Mathf.Clamp(Screen.width - x - 10f, 200f, PanelWidth), Screen.height - 20f);
    }

    private void OnGUI()
    {
        if (Kneel.UI.PauseMenu.IsPaused)
            return;

        if (open == false)
        {
            // Top right, where the lab opens (the F1 overlay's hint is top left).
            GUI.Label(new Rect(Screen.width - 110f, 10f, 100f, 22f), "F2: boss lab");
            return;
        }

        if (boss == null || player == null)
            return;

        GUILayout.BeginArea(PanelRect(), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>BOSS LAB</b>  (F2)", Rich());
        DrawState();

        Header("Kael");
        invincible = GUILayout.Toggle(invincible, " Invincible (his blows land but take no health)");
        if (invincible)
            passThrough = GUILayout.Toggle(passThrough, " ...and pass straight through him (no grabs, kneels or knockdowns)");

        Header("The boss");
        sticky = GUILayout.Toggle(sticky, " Wait between moves (only the ones asked for)");
        repeat = GUILayout.Toggle(repeat, " Repeat the last move");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Speed", GUILayout.Width(50f));
        foreach (float s in new[] { 0.25f, 0.5f, 1f })
        {
            bool on = Mathf.Approximately(speed, s);
            if (GUILayout.Toggle(on, s + "x", GUI.skin.button) && on == false)
                SetSpeed(s);
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Health", GUILayout.Width(50f));
        // 74%: the Gather is open (under 75%); 51%: just above the 50% beat; 25%: past it (the beat plays next).
        foreach (float f in new[] { 1f, 0.74f, 0.51f, 0.25f })
        {
            if (GUILayout.Button(Mathf.RoundToInt(f * 100f) + "%"))
                SetHealth(f);
        }

        GUILayout.EndHorizontal();

        Header("Start");
        reloadFirst = GUILayout.Toggle(reloadFirst, " Reload the hall first (a whole floor)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Phase 1 (no intro)")) Begin(Run.Phase1);
        if (GUILayout.Button("Phase 2 (no Unmaking)")) Begin(Run.Phase2);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Reload the hall"))
        {
            SaveFlags.BeginSandbox();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        Header("Phase 1 moves (the Master)");
        MoveGrid(System.Enum.GetValues(typeof(MasterP1.MoveKind)), k => AskP1((MasterP1.MoveKind)k));
        GUILayout.BeginHorizontal();
        bool inP1 = boss.Current == MasterBoss.Beat.Phase1;
        GUI.enabled = inP1;
        if (GUILayout.Button("Parried")) boss.master.parriedState.Begin(boss.settings.parryKneelTime);
        if (GUILayout.Button("Stagger")) boss.master.staggerState.Begin(boss.settings.poiseStaggerTime);
        if (GUILayout.Button("Flinch")) boss.master.stateMachine.ChangeState(boss.master.flinchState);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        Header("Phase 2 moves (the Ash Colossus)");
        MoveGrid(System.Enum.GetValues(typeof(AshColossus.MoveKind)), k => AskP2((AshColossus.MoveKind)k));
        GUILayout.BeginHorizontal();
        GUI.enabled = boss.Current == MasterBoss.Beat.Phase2;
        if (GUILayout.Button("Recoil (parried)")) boss.colossus.recoilState.Begin(boss.settings.recoilTime);
        if (GUILayout.Button("Topple")) boss.colossus.Topple();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        Header("Cutscenes");
        if (GUILayout.Button("The reveal (the doors, the push into the hall)")) Begin(Run.Reveal);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Intro, first time (12 s)")) Begin(Run.IntroFirst);
        if (GUILayout.Button("Intro, retry (4 s)")) Begin(Run.IntroRetry);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Phase 1's 50% beat (the pommel, panel F1)")) Begin(Run.P1Half);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("The Unmaking (first, 10 s)")) Begin(Run.UnmakingFirst);
        if (GUILayout.Button("The Unmaking (skippable)")) Begin(Run.UnmakingSkippable);
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Phase 2's 50% beat (the roar, statues, storm)")) Begin(Run.P2Half);
        if (GUILayout.Button("The finisher (the Last Command, \"Go on.\")")) Begin(Run.Finisher);
        stayInL5 = GUILayout.Toggle(stayInL5, " Stay on the end card (no main menu)");
        MasterFinisher.HoldMainMenu = stayInL5;

        if (SaveFlags.Sandbox != null)
            GUILayout.Label("<i>Save flags are sandboxed until Play mode ends: the save file is untouched.</i>", Rich());
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawState()
    {
        MasterBody body = boss.Current >= MasterBoss.Beat.Transition && boss.colossus != null ? boss.colossus : boss.master;
        string state = body.stateMachine?.currentState?.GetType().Name ?? "-";
        GUILayout.Label($"{boss.Current}   {state}   HP {body.health.Current:0}/{body.health.Max:0}");
        string queued = queuedP1?.ToString() ?? queuedP2?.ToString();
        if (queued != null)
            GUILayout.Label($"Next: {queued} (when he's ready)");
        if (status.Length > 0)
            GUILayout.Label(status);
    }

    private static void MoveGrid(System.Array kinds, System.Action<object> ask)
    {
        int i = 0;
        GUILayout.BeginHorizontal();
        foreach (object kind in kinds)
        {
            if (i > 0 && i % 4 == 0)
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }

            if (GUILayout.Button(kind.ToString()))
                ask(kind);
            i++;
        }

        GUILayout.EndHorizontal();
    }

    private void SetSpeed(float s)
    {
        speed = s;
        if (Kneel.UI.PauseMenu.IsPaused == false)
            Time.timeScale = s;
    }

    // Only down: health goes back up only through Phase 2's own Gather, or a fresh start.
    private void SetHealth(float fraction)
    {
        MasterBody body = boss.Current == MasterBoss.Beat.Phase2 ? boss.colossus : boss.Current == MasterBoss.Beat.Phase1 ? boss.master : null;
        if (body == null)
        {
            status = "Health: start a phase first.";
            return;
        }

        float target = body.health.Max * fraction;
        if (body.health.Current > target)
            body.health.ApplyDamage(body.health.Current - target);
        else
            body.health.Heal(target - body.health.Current);
        boss.Bar?.Set(body.health.Fraction);
        status = "";
    }

    private static void Header(string title)
    {
        GUILayout.Space(8f);
        GUILayout.Label("<b>" + title + "</b>", Rich());
    }

    private static GUIStyle rich;

    private static GUIStyle Rich() => rich ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
}
