using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One of the four arena chandeliers (The Master.md §4, §8): each hangs 12 m up and is tied off by a chain that runs
// over its pillar to a winch at the pillar's foot. Strike the winch (or let his Threads cross it) and the chain
// snaps: a jolt, its shadow spreading on the floor, and it lands 1.0 s after the blow. Under it: 60 + a 2.5 s
// stagger to him, 40 to the player. The wreck burns 6 s, then stays as cover (it blocks the Command and carves the
// NavMesh). The transition drops any still hanging (Stage 6). Back on the chain at a level reset.
public class ChandelierDrop : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public enum State { Hanging, Falling, Fallen }

    public static readonly List<ChandelierDrop> All = new List<ChandelierDrop>();

    // The vault chain above the bridle ring: it stays hanging when the chandelier falls.
    public Transform chain;

    // The winch at its pillar's foot; its collider (a HallStrikeTarget on the Enemy layer) is what the sword hits.
    public HallStrikeTarget winch;

    // The tie: from the hoop over the pillar's top to the winch.
    public LineRenderer tether;

    public Light hangingLight;
    public GameObject shadow;
    public Collider wreck;
    public NavMeshObstacle wreckCarve;
    public ParticleSystem snapSparks;
    public ParticleSystem impactDust;

    public float floorY = 9f;
    public float reach = 3f;
    public float tell = 1f;
    public float jolt = 0.3f;
    public float bossDamage = 60f;
    public float bossStagger = 2.5f;
    public float playerDamage = 40f;
    public float burnTime = 6f;
    public float landedLight = 6f;
    public float landedRange = 8f;

    public State Current { get; private set; } = State.Hanging;

    // When it lands (game time), while it falls; -1 otherwise (BossArenaDirector's pacing rule reads it).
    public float ImpactAt { get; private set; } = -1f;

    public event Action<ChandelierDrop> Triggered;
    public event Action<ChandelierDrop> Landed;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Transform chainParent;
    private Vector3 chainLocal;
    private Quaternion chainLocalRotation;
    private float restIntensity;
    private float restRange;
    private float started;
    private Quaternion landRotation;
    private float shadowSize;
    private Kneel.FightClearance clearance;

    private void Awake()
    {
        restPosition = transform.position;
        restRotation = transform.rotation;
        if (chain != null)
        {
            chainParent = chain.parent;
            chainLocal = chain.localPosition;
            chainLocalRotation = chain.localRotation;
        }

        if (hangingLight != null)
        {
            restIntensity = hangingLight.intensity;
            restRange = hangingLight.range;
        }

        if (shadow != null)
            shadowSize = shadow.transform.localScale.x;
        clearance = GetComponent<Kneel.FightClearance>();
        wave = FindAnyObjectByType<DarknessWave>();
        if (winch != null)
            winch.Struck += _ => Trigger();
    }

    // The hanging light, through the darkness wave (it dims this light too, and has to restore the right brightness).
    private void SetLight(float intensity, float range)
    {
        hangingLight.range = range;
        hangingLight.intensity = intensity * (wave != null ? wave.Rebase(hangingLight, intensity) : 1f);
    }

    private DarknessWave wave;

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    // The chain snaps: it lands `tell` seconds from now.
    // The Unmaking's fall (§5, 3.0 s): no damage to anyone; a player under it is shoved `shove` metres clear.
    public void Trigger(float shove)
    {
        if (Current != State.Hanging)
            return;
        harmlessShove = shove;
        Trigger();
    }

    private float harmlessShove = -1f;

    public void Trigger()
    {
        if (Current != State.Hanging)
            return;

        Current = State.Falling;
        started = Time.time;
        ImpactAt = started + tell;

        // Off the camera's fade first (switching it off shows everything again), then the chain goes.
        if (clearance != null)
            clearance.enabled = false;
        if (tether != null)
            tether.enabled = false;
        if (snapSparks != null)
        {
            snapSparks.transform.position = winch != null ? winch.transform.position + Vector3.up * 0.8f : transform.position;
            snapSparks.Play(true);
        }

        // The vault chain stays where it hangs.
        if (chain != null)
            chain.SetParent(transform.parent, true);

        float tilt = UnityEngine.Random.Range(10f, 16f);
        landRotation = Quaternion.AngleAxis(tilt, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward) * restRotation;
        if (shadow != null)
        {
            shadow.SetActive(true);
            shadow.transform.localScale = Vector3.one * shadowSize * 0.4f;
        }

        Triggered?.Invoke(this);
    }

    private void Update()
    {
        if (Current != State.Falling)
            return;

        float t = Time.time - started;
        float landY = floorY + 0.35f;

        if (t < jolt)
        {
            // The jolt: it drops a hand's width as the chain gives, and shudders.
            float k = t / jolt;
            transform.position = restPosition + Vector3.down * 0.25f * Mathf.Sin(k * Mathf.PI * 0.5f);
            transform.rotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 60f) * 1.5f * (1f - k), 0f, Mathf.Cos(t * 55f) * 1.5f * (1f - k));
        }
        else
        {
            // Free fall, timed to land at the end of the tell.
            float fall = Mathf.Max(tell - jolt, 0.05f);
            float drop = restPosition.y - 0.25f - landY;
            float g = 2f * drop / (fall * fall);
            float ft = Mathf.Min(t - jolt, fall);
            transform.position = new Vector3(restPosition.x, restPosition.y - 0.25f - 0.5f * g * ft * ft, restPosition.z);
            transform.rotation = Quaternion.Slerp(restRotation, landRotation, ft / fall * ft / fall);
        }

        // Its light is set for its height (a pool on the floor 12 m down); it dims as it drops, or it would glare.
        if (hangingLight != null)
        {
            float height = Mathf.Max(transform.position.y - floorY, 1f);
            float rest = Mathf.Max(restPosition.y - floorY, 1f);
            SetLight(Mathf.Max(landedLight, restIntensity * height * height / (rest * rest)), Mathf.Max(landedRange, restRange * height / rest));
        }

        if (shadow != null)
        {
            shadow.transform.position = new Vector3(restPosition.x, floorY + 0.03f, restPosition.z);
            shadow.transform.localScale = Vector3.one * shadowSize * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(t / tell));
        }

        if (t >= tell)
            Land();
    }

    private void Land()
    {
        Current = State.Fallen;
        ImpactAt = -1f;
        transform.SetPositionAndRotation(new Vector3(restPosition.x, floorY + 0.35f, restPosition.z), landRotation);
        if (shadow != null)
            shadow.SetActive(false);

        Vector3 at = transform.position;
        PlayerCombat player = HallActors.Player;
        if (harmlessShove >= 0f)
        {
            if (player != null && HallActors.FlatDistance(player.transform.position, at) <= reach + 0.3f)
            {
                Vector3 away = player.transform.position - at;
                away.y = 0f;
                float d = away.magnitude;
                player.Push(d > 0.1f ? away / d : Vector3.back, reach + harmlessShove - d);
            }
        }
        else
        {
            foreach (IHallBoss boss in HallActors.BossesNear(at, reach + 0.6f))
                boss.HazardHit(bossDamage, bossStagger);

            if (player != null && HallActors.FlatDistance(player.transform.position, at) <= reach + 0.3f)
                HallActors.HitPlayer(playerDamage, at, gameObject);
        }

        if (impactDust != null)
        {
            impactDust.transform.position = new Vector3(at.x, floorY + 0.2f, at.z);
            impactDust.Play(true);
        }

        UnityEngine.Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.35f, 0.4f);

        if (hangingLight != null)
        {
            SetLight(landedLight, landedRange);
        }

        FloorFire.Circle(at, reach * 0.8f, burnTime);
        if (wreck != null)
            wreck.enabled = true;
        if (wreckCarve != null)
            wreckCarve.enabled = true;
        Landed?.Invoke(this);
    }

    public void ResetState()
    {
        harmlessShove = -1f;
        Current = State.Hanging;
        ImpactAt = -1f;
        transform.SetPositionAndRotation(restPosition, restRotation);
        if (chain != null && chainParent != null)
        {
            chain.SetParent(chainParent, false);
            chain.localPosition = chainLocal;
            chain.localRotation = chainLocalRotation;
        }

        if (tether != null)
            tether.enabled = true;
        if (shadow != null)
            shadow.SetActive(false);
        if (wreck != null)
            wreck.enabled = false;
        if (wreckCarve != null)
            wreckCarve.enabled = false;
        if (hangingLight != null)
        {
            SetLight(restIntensity, restRange);
        }

        if (clearance != null)
            clearance.enabled = true;
    }
}
