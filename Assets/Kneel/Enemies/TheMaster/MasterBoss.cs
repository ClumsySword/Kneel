using System.Collections;
using Kneel.Core;
using UnityEngine;

// The L5 boss fight's owner (The Master.md): the hall's reveal, the intro, the seal and the bar, and the phases. Phase 1
// is MasterP1; the transition, the Colossus and the finisher hook in at OnP1Down (Stages 6-7).
//
// Beats (§3, decided 2026-11-10):
// - The reveal, once per save ("L5.Reveal"): crossing the grand doors, the camera pushes 6 m into the hall from the
//   doorway while Kael stands still.
// - The aggro line (y 432): the ash-mist seal kindles behind Kael. The first time per save ("L5.MasterMet") the long
//   intro plays (12 s): the palm and the pull (walk only, 30%), the scar flare, he stands, walks down the dais, the
//   bar fills. Retries play the short one (4 s, he's already standing at the dais foot), skippable by holding Interact.
// - He's untouchable until the bar has filled.
[DefaultExecutionOrder(20)]
public class MasterBoss : MonoBehaviour
{
    public enum Beat { Waiting, Reveal, Seated, Intro, Phase1, Phase1Down }

    public MasterSettings settings;
    public MasterP1 master;
    public HallSeal seal;
    // The boss bar's prefab (MasterSetup builds it in the menus' style).
    public BossHealthBar barPrefab;

    [Header("Places (world)")]
    // Where he sits on the throne, and where he stops at the dais foot.
    public Transform throneSeat;
    public Transform daisFoot;
    // The aggro line and the grand doors' threshold (world z), inside the hall's walls (x).
    public float aggroZ = 432.5f;
    public float doorZ = 407.5f;
    public float hallMinX = 33f;
    public float hallMaxX = 75f;

    [Header("Intro shot (behind Kael's right shoulder)")]
    public float introShotBack = 4f;
    public float introShotSide = 1f;
    // Above Kael's head.
    public float introShotHeight = 0.3f;

    [Header("Reveal")]
    public float revealTime = 6f;
    public Vector3 revealFrom = new Vector3(54f, 10.6f, 409.5f);
    public Vector3 revealTo = new Vector3(54f, 10.6f, 415.5f);

    public Beat Current { get; private set; } = Beat.Waiting;

    public BossHealthBar Bar { get; private set; }

    // Raised when the fight proper starts (the bar has filled) and when Phase 1 ends.
    public event System.Action OnFightStarted;
    public event System.Action OnP1Down;

    private PlayerCombat player;
    private PlayerCamera playerCamera;
    private PlayerScar scar;
    private PlayerControls controls;
    private float skipHeld;
    private bool introShot;

    // For the playtest: skip the reveal and play the short intro.
    public static bool ForceShortIntro;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // The project enters Play without a domain reload, so statics outlive sessions.
        ForceShortIntro = false;
        MasterP1.HazardImpactNear = null;
        MasterP1.ForceNext = null;
        BossTell.DarkBoost = 1f;
    }

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable() => controls.Character.Interact.Enable();

    private void OnDisable() => controls.Character.Interact.Disable();

    private void Start()
    {
        player = FindAnyObjectByType<PlayerCombat>();
        playerCamera = FindAnyObjectByType<PlayerCamera>();
        scar = player != null ? player.GetComponent<PlayerScar>() : null;
        Bar = BossHealthBar.Spawn(barPrefab, "THE MASTER");

        master.OnDefeated += () =>
        {
            Current = Beat.Phase1Down;
            Bar.Set(0f);
            OnP1Down?.Invoke();
        };
        master.health.OnDamaged += _ => Bar.Set(master.health.Fraction);

        Seat();
    }

    // Back to the start of the fight: on the throne, the seal down (also the retry reset, Stage 7).
    public void Seat()
    {
        Current = Beat.Seated;
        if (master.agent.enabled)
            master.agent.enabled = false;
        if (throneSeat != null)
            master.transform.SetPositionAndRotation(throneSeat.position, throneSeat.rotation);
        master.Seat();
        seal?.Dissolve();
        Bar?.Hide();
    }

    private void Update()
    {
        if (player == null)
            return;

        Vector3 p = player.transform.position;
        bool inHall = p.x > hallMinX && p.x < hallMaxX;

        if (Current == Beat.Seated && inHall && p.z > doorZ && ForceShortIntro == false && SaveFlags.Has("L5.Reveal") == false)
        {
            StartCoroutine(Reveal());
            return;
        }

        if (Current == Beat.Seated && inHall && p.z > aggroZ)
        {
            bool longIntro = ForceShortIntro == false && SaveFlags.Has("L5.MasterMet") == false;
            StartCoroutine(longIntro ? LongIntro() : ShortIntro());
        }
    }

    private IEnumerator Reveal()
    {
        Current = Beat.Reveal;
        player.Restrict(revealTime, 0f);
        Quaternion look = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(-2f, 0f, 0f);
        float start = Time.time;
        playerCamera?.SetOverride(revealFrom, look, 1.2f);
        while (Time.time - start < revealTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, (Time.time - start) / revealTime);
            playerCamera?.SetOverride(Vector3.Lerp(revealFrom, revealTo, t), look, 1.2f);
            yield return null;
        }

        playerCamera?.ClearOverride(1.5f);
        player.ReleaseRestriction();
        SaveFlags.Set("L5.Reveal");
        Current = Beat.Seated;
    }

    // §3, 12 s: 0-1.5 the palm and the pull, 1.5-2.5 the scar flare, 2.5-7.5 he stands, 7.5-11 the dais, 11-12 the bar.
    private IEnumerator LongIntro()
    {
        Current = Beat.Intro;
        seal?.Kindle();

        // The palm rises; his cracks brighten; Kael is dragged to a walk at 30%. The shot drops low behind Kael and
        // looks up the hall at the throne, pushing in until the bar fills.
        master.anim.SetBool("PalmUp", true);
        if (master.bossTell != null) master.bossTell.EmberScale = 1.8f;
        player.Restrict(settings.introPullTime, settings.introPullSpeed);
        StartCoroutine(IntroShot(30f, 23f, 11f));
        yield return new WaitForSeconds(settings.introPullTime);

        // The curse fails on him for the first time: the scar flares, control returns, the palm comes down.
        scar?.Flare();
        player.ReleaseRestriction();
        master.anim.SetBool("PalmUp", false);
        if (master.bossTell != null) master.bossTell.EmberScale = 1f;
        yield return new WaitForSeconds(settings.introFlareTime);

        // He stands (head level with the throne's crest), takes the sword and walks down the dais.
        master.Rise(daisFoot.position);
        yield return new WaitForSeconds(settings.introStandTime);
        float walkEnd = Time.time + settings.introWalkTime;
        while (Time.time < walkEnd && master.walkDownState.Arrived == false)
            yield return null;

        SaveFlags.Set("L5.MasterMet");
        yield return FillAndEngage();
    }

    // Retries: he's already standing at the dais foot as the seal kindles (4 s); holding Interact skips it.
    private IEnumerator ShortIntro()
    {
        Current = Beat.Intro;
        seal?.Kindle();
        master.agent.enabled = true;
        master.StandAt(daisFoot.position, daisFoot.rotation);
        StartCoroutine(IntroShot(26f, 24f, settings.retryIntroTime));

        float end = Time.time + settings.retryIntroTime - settings.introBarFillTime;
        skipHeld = 0f;
        while (Time.time < end)
        {
            skipHeld = controls.Character.Interact.IsPressed() ? skipHeld + Time.deltaTime : 0f;
            if (skipHeld >= settings.skipHoldTime)
                break;
            yield return null;
        }

        yield return FillAndEngage();
    }

    // Over Kael's right shoulder on a long lens (owner, 2026-11-10: the Master clearly in shot with Kael): Kael's head
    // and shoulders in the lower left, the Master in the middle of the frame 30 m up the hall. The lens closes from
    // `fromFov` to `toFov` over `seconds`; the shot follows Kael if he walks. Held until the bar has filled.
    private IEnumerator IntroShot(float fromFov, float toFov, float seconds)
    {
        introShot = true;
        float start = Time.time;
        while (introShot)
        {
            float t = Mathf.SmoothStep(0f, 1f, (Time.time - start) / Mathf.Max(seconds, 0.1f));
            Vector3 kael = KaelHead();
            Vector3 him = master.transform.position + Vector3.up * 2.6f;
            Vector3 forward = him - kael;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 at = kael - forward * introShotBack + right * introShotSide + Vector3.up * introShotHeight;
            Vector3 aim = Vector3.Lerp(him, kael, 0.2f);
            playerCamera?.SetOverride(at, Quaternion.LookRotation(aim - at), 1.2f, Mathf.Lerp(fromFov, toFov, t));
            yield return null;
        }

        playerCamera?.ClearOverride(1.2f);
    }

    private Transform kaelHead;

    private Vector3 KaelHead()
    {
        if (kaelHead == null)
        {
            Animator body = player.GetComponentInChildren<Animator>();
            kaelHead = body != null && body.isHuman ? body.GetBoneTransform(HumanBodyBones.Head) : null;
        }

        return kaelHead != null ? kaelHead.position : player.transform.position + Vector3.up * 1.7f;
    }

    private IEnumerator FillAndEngage()
    {
        Bar.Show();
        Bar.FillTo(0f, 1f, settings.introBarFillTime);
        yield return new WaitForSeconds(settings.introBarFillTime);
        introShot = false;
        Current = Beat.Phase1;
        master.Engage();
        OnFightStarted?.Invoke();
    }
}
