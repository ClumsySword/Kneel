using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// One of the four 5 m knight statues in the side bands (The Master.md §6, §8: added 2026-11-10, Synty's
// SM_Prop_Statue_01 in L5's statue stone, split at the knee). They bow at every Command; at P2's 50% beat they crack
// at the knee (dust, a ground shadow along the fall, 1.5 s) and topple inward: 50 to anyone under them, him included.
// Fallen, they're cover (they block the Command and carve the NavMesh). Standing again at a level reset.
public class HallStatue : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public enum State { Standing, Cracking, Falling, Fallen }

    public static readonly List<HallStatue> All = new List<HallStatue>();

    // Everything above the knee; its pivot is the knee, on the statue's front.
    public Transform upper;
    public Collider standing;
    public Collider fallen;
    public NavMeshObstacle fallenCarve;
    public GameObject shadow;
    public ParticleSystem crackDust;
    public ParticleSystem impactDust;

    public float bowDegrees = 9f;
    public float tell = 1.5f;
    public float fallTime = 0.75f;
    public float damage = 50f;
    // The fallen statue's footprint, from the knee along its front.
    public float length = 4f;
    public float width = 1.8f;

    public State Current { get; private set; } = State.Standing;

    public float ImpactAt { get; private set; } = -1f;

    public event Action<HallStatue> Fell;

    private Quaternion upperRest;
    private float started;
    private float bowStart = -1f;
    private float bowLength;

    private void Awake() => upperRest = upper.localRotation;

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    // A bow through the Command's tell: down over 0.6 s, held, up again after it.
    public void Bow(float seconds)
    {
        if (Current != State.Standing)
            return;
        bowStart = Time.time;
        bowLength = Mathf.Max(seconds, 1.2f);
    }

    public void Topple()
    {
        if (Current != State.Standing)
            return;
        Current = State.Cracking;
        started = Time.time;
        ImpactAt = started + tell + fallTime;
        bowStart = -1f;
        if (crackDust != null)
            crackDust.Play(true);
        if (shadow != null)
            shadow.SetActive(true);
    }

    private void Update()
    {
        float now = Time.time;
        switch (Current)
        {
            case State.Standing:
                if (bowStart >= 0f)
                {
                    float t = now - bowStart;
                    float amount = t < 0.6f ? Mathf.SmoothStep(0f, 1f, t / 0.6f) : t < bowLength ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - bowLength) / 0.8f);
                    upper.localRotation = upperRest * Quaternion.Euler(bowDegrees * amount, 0f, 0f);
                    if (t > bowLength + 0.8f)
                    {
                        bowStart = -1f;
                        upper.localRotation = upperRest;
                    }
                }

                break;

            case State.Cracking:
            {
                // The crack: a shudder at the knee.
                float t = now - started;
                upper.localRotation = upperRest * Quaternion.Euler(Mathf.Sin(t * 40f) * 0.8f * t / tell, 0f, Mathf.Cos(t * 33f) * 0.5f * t / tell);
                if (t >= tell)
                    Current = State.Falling;
                break;
            }

            case State.Falling:
            {
                float k = Mathf.Clamp01((now - started - tell) / fallTime);
                upper.localRotation = upperRest * Quaternion.Euler(90f * k * k, 0f, 0f);
                if (k >= 1f)
                    Land();
                break;
            }
        }
    }

    private void Land()
    {
        Current = State.Fallen;
        ImpactAt = -1f;
        if (shadow != null)
            shadow.SetActive(false);
        if (standing != null)
            standing.enabled = false;
        if (fallen != null)
            fallen.enabled = true;
        if (fallenCarve != null)
            fallenCarve.enabled = true;

        Vector3 knee = upper.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        Vector3 end = knee + forward.normalized * length;
        PlayerCombat player = HallActors.Player;
        if (player != null && HallActors.SegmentDistance(player.transform.position, knee, end) <= width * 0.5f + 0.3f)
            HallActors.HitPlayer(damage, knee, gameObject);
        foreach (IHallBoss boss in HallActors.Bosses)
        {
            if (boss != null && (boss as UnityEngine.Object) != null && boss.IsDead == false && HallActors.SegmentDistance(boss.transform.position, knee, end) <= width * 0.5f + 0.8f)
                boss.HazardHit(damage, 1.5f);
        }

        if (impactDust != null)
            impactDust.Play(true);
        UnityEngine.Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.3f, 0.4f);
        Fell?.Invoke(this);
    }

    public void ResetState()
    {
        Current = State.Standing;
        ImpactAt = -1f;
        bowStart = -1f;
        upper.localRotation = upperRest;
        if (shadow != null)
            shadow.SetActive(false);
        if (standing != null)
            standing.enabled = true;
        if (fallen != null)
            fallen.enabled = false;
        if (fallenCarve != null)
            fallenCarve.enabled = false;
    }
}
