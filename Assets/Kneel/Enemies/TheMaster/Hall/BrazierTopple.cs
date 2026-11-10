using System;
using System.Collections.Generic;
using UnityEngine;

// One of the four braziers on the arena's pillar lines (The Master.md §4, §8). Strike it and it rocks for 1.0 s
// (the tell), then goes over away from the blow and spills its burning oil: a 1 R pool (1.5 m round) that burns 8 s
// as floor fire. If he's in the spill as it lands: 40 + a 1.5 s stagger; after that the fire itself (carved from his
// NavMesh) costs him 25 to cross. A Leap or Hammerfall landing within 2 m tips it too, and the transition sends all
// four over (Stage 6). Standing again at a level reset.
public class BrazierTopple : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public enum State { Standing, Rocking, Falling, Fallen }

    public static readonly List<BrazierTopple> All = new List<BrazierTopple>();

    // What goes over (the brazier art; its pivot at the floor).
    public Transform art;

    // Its body's collider (it lies down with it).
    public Transform block;

    public HallStrikeTarget strike;

    public float rockTime = 1f;
    public float fallTime = 0.45f;
    public float rockDegrees = 7f;
    public float spillRadius = 1.5f;
    public float spillDistance = 1.9f;
    public float bossDamage = 40f;
    public float bossStagger = 1.5f;
    public float burnTime = 8f;

    public State Current { get; private set; } = State.Standing;

    // When it lands (game time), while it rocks or falls; -1 otherwise.
    public float ImpactAt { get; private set; } = -1f;

    public Vector3 SpillCentre => new Vector3(transform.position.x, transform.position.y, transform.position.z) + direction * spillDistance;

    public event Action<BrazierTopple> Toppled;

    // The brazier's own fire light: once it's on its side, burning oil on the floor, it's far too close to the floor
    // at full strength (the spill's fire carries the light from there).
    private Light[] fireLights;
    private float[] fireLightRest;

    private Vector3 direction = Vector3.right;
    private float started;
    private Vector3 artPosition, blockPosition;
    private Quaternion artRotation, blockRotation;

    private void Awake()
    {
        fireLights = art.GetComponentsInChildren<Light>(true);
        fireLightRest = new float[fireLights.Length];
        for (int i = 0; i < fireLights.Length; i++)
            fireLightRest[i] = fireLights[i].intensity;
        artPosition = art.position;
        artRotation = art.rotation;
        if (block != null)
        {
            blockPosition = block.position;
            blockRotation = block.rotation;
        }

        if (strike != null)
            strike.Struck += info => Trigger(info.source != null ? info.source.transform.position : transform.position - info.direction);
    }

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);

    // Struck from `from`: it goes over away from it.
    public void Trigger(Vector3 from)
    {
        if (Current != State.Standing)
            return;

        Vector3 away = transform.position - from;
        away.y = 0f;
        direction = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right;
        Current = State.Rocking;
        started = Time.time;
        ImpactAt = started + rockTime + fallTime;
    }

    private void Update()
    {
        if (Current != State.Rocking && Current != State.Falling)
            return;

        float t = Time.time - started;
        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        Vector3 pivot = transform.position + direction * 0.3f;
        float angle;
        if (t < rockTime)
        {
            // Rocking on its feet, growing toward the fall.
            float k = t / rockTime;
            angle = rockDegrees * Mathf.Sin(k * Mathf.PI * 5f) * k;
        }
        else
        {
            Current = State.Falling;
            float k = Mathf.Clamp01((t - rockTime) / fallTime);
            angle = 90f * k * k;
        }

        Pose(Quaternion.AngleAxis(angle, axis), pivot);
        SetFire(Current == State.Falling ? 1f - Mathf.Clamp01((t - rockTime) / fallTime) : 1f);
        if (t >= rockTime + fallTime)
            Land();
    }

    private void Pose(Quaternion turn, Vector3 pivot)
    {
        art.SetPositionAndRotation(pivot + turn * (artPosition - pivot), turn * artRotation);
        if (block != null)
            block.SetPositionAndRotation(pivot + turn * (blockPosition - pivot), turn * blockRotation);
    }

    private void Land()
    {
        Current = State.Fallen;
        ImpactAt = -1f;
        SetFire(0f);
        Vector3 spill = SpillCentre;
        foreach (IHallBoss boss in HallActors.BossesNear(spill, spillRadius + 0.6f))
            boss.HazardHit(bossDamage, bossStagger);
        FloorFire.Circle(spill, spillRadius, burnTime);
        UnityEngine.Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.15f, 0.25f);
        Toppled?.Invoke(this);
    }

    private void SetFire(float level)
    {
        for (int i = 0; i < fireLights.Length; i++)
        {
            var flicker = fireLights[i].GetComponent<Kneel.Lighting.LightFlicker>();
            if (flicker != null)
                flicker.Dim = level;
            else
                fireLights[i].intensity = fireLightRest[i] * level;
        }
    }

    public void ResetState()
    {
        Current = State.Standing;
        if (fireLights != null)
            SetFire(1f);
        ImpactAt = -1f;
        art.SetPositionAndRotation(artPosition, artRotation);
        if (block != null)
            block.SetPositionAndRotation(blockPosition, blockRotation);
    }
}
