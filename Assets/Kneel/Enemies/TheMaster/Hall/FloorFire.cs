using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Fire on the hall floor (The Master.md §8, redesigned 2026-11-10): it lands where it's cast (his Threads and Thread
// Lash lines, the Hammerfall circle, a brazier's spill, a fallen chandelier), burns 8 s and fades over the last
// 1.5 s as its tell, does 25 on contact and then 25/s to the player and to him alike, never spreads, and is cut out
// of every NavMesh. Pooled: at most 12 patches and about 20% of the arena at once; the oldest goes out first.
// A patch is a segment with a half-width (a circle is a zero-length one). Its look is the L4 oil slick's burning
// flames, stretched over the patch; only the newest few patches carry a light (§8: at most 8 extra lights).
public class FloorFire : MonoBehaviour, Kneel.Hazards.ILevelResettable
{
    public static FloorFire Instance { get; private set; }

    // The L4 oil slick's "Burning" visual (flames, heart, embers, glow, smoke, light, scorched puddle).
    [SerializeField] private GameObject template;

    public int maxPatches = 12;
    public float maxArea = 186f;
    public int maxLights = 4;
    public float contactDamage = 25f;
    public float damagePerSecond = 25f;
    public float tickInterval = 0.5f;
    public float fadeTime = 1.5f;
    public float floorY = 9f;

    private readonly List<FirePatch> live = new List<FirePatch>();
    private readonly Stack<FirePatch> pool = new Stack<FirePatch>();

    public IReadOnlyList<FirePatch> Live => live;

    public float BurningArea
    {
        get
        {
            float area = 0f;
            foreach (FirePatch patch in live)
                area += patch.Area;
            return area;
        }
    }

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // A burning line, `width` wide (Threads: it grows with the thread's head through Stretch).
    public static FirePatch Line(Vector3 a, Vector3 b, float width, float seconds)
    {
        return Instance != null ? Instance.Spawn(a, b, width * 0.5f, seconds, false) : null;
    }

    public static FirePatch Circle(Vector3 centre, float radius, float seconds)
    {
        return Instance != null ? Instance.Spawn(centre, centre, radius, seconds, true) : null;
    }

    public static bool IsBurning(Vector3 point, float margin = 0f)
    {
        if (Instance == null)
            return false;
        foreach (FirePatch patch in Instance.live)
        {
            if (patch.Contains(point, margin))
                return true;
        }

        return false;
    }

    private FirePatch Spawn(Vector3 a, Vector3 b, float half, float seconds, bool circle)
    {
        FirePatch patch = pool.Count > 0 ? pool.Pop() : FirePatch.Build(template, transform);
        a.y = b.y = floorY;
        patch.Begin(a, b, half, seconds, circle);
        live.Add(patch);
        Trim(patch);
        return patch;
    }

    // Over the count or the area: the oldest go out first (quickly, not instantly).
    private void Trim(FirePatch keep)
    {
        int burning = 0;
        float area = 0f;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            FirePatch patch = live[i];
            if (patch.Dying)
                continue;
            burning++;
            area += patch.Area;
            if (patch != keep && (burning > maxPatches || area > maxArea))
                patch.GoOut(0.5f);
        }
    }

    private void Update()
    {
        float now = Time.time;
        int lights = 0;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            FirePatch patch = live[i];
            patch.Tick(now, fadeTime, lights < maxLights);
            if (patch.HasLight)
                lights++;

            if (patch.Over(now))
            {
                patch.End();
                live.RemoveAt(i);
                pool.Push(patch);
                continue;
            }

            Burn(patch, now);
        }
    }

    private void Burn(FirePatch patch, float now)
    {
        PlayerCombat player = HallActors.Player;
        if (player != null && patch.Contains(player.transform.position, 0.3f))
        {
            float damage = patch.Touch(player, now, contactDamage, damagePerSecond * tickInterval, tickInterval);
            if (damage > 0f)
                HallActors.BurnPlayer(damage, gameObject);
        }

        foreach (IHallBoss boss in HallActors.Bosses)
        {
            if (boss == null || (boss as Object) == null || boss.IsDead)
                continue;
            if (patch.Contains(boss.transform.position, 0.6f) == false)
            {
                if (ReferenceEquals(patch.Spared, boss))
                    patch.Spared = null;
                continue;
            }

            if (ReferenceEquals(patch.Spared, boss))
                continue;
            float damage = patch.Touch(boss, now, contactDamage, damagePerSecond * tickInterval, tickInterval);
            if (damage > 0f)
                boss.HazardHit(damage, 0f);
        }
    }

    public void ResetState()
    {
        foreach (FirePatch patch in live)
        {
            patch.End();
            pool.Push(patch);
        }

        live.Clear();
    }
}

public class FirePatch
{
    private static readonly int BoxRef = 32;

    public Vector3 A { get; private set; }
    public Vector3 B { get; private set; }
    public float Half { get; private set; }
    public bool Circle { get; private set; }
    public float Started { get; private set; }
    public float Ends { get; private set; }
    public int Generation { get; private set; }
    public bool Dying { get; private set; }

    // A boss standing in it as it lights (his own Hammerfall's circle) isn't burnt until he has stepped out of it.
    public IHallBoss Spared { get; set; }
    public bool HasLight => light != null && light.enabled;

    private GameObject root;
    private ParticleSystem[] systems;
    private float[] baseRates;
    private Vector3[] baseShape;
    private Transform puddle;
    private Light light;
    private float lightBase;
    private NavMeshObstacle obstacle;
    private readonly Dictionary<object, float> nextTick = new Dictionary<object, float>();

    public float Length => HallActors.FlatDistance(A, B);

    public float Area => Circle ? Mathf.PI * Half * Half : Length * Half * 2f + Mathf.PI * Half * Half;

    public static FirePatch Build(GameObject template, Transform parent)
    {
        var patch = new FirePatch();
        patch.root = template != null ? Object.Instantiate(template, parent) : new GameObject();
        patch.root.name = "FirePatch";
        patch.root.SetActive(true);
        patch.systems = patch.root.GetComponentsInChildren<ParticleSystem>(true);
        patch.baseRates = new float[patch.systems.Length];
        patch.baseShape = new Vector3[patch.systems.Length];
        for (int i = 0; i < patch.systems.Length; i++)
        {
            patch.baseRates[i] = patch.systems[i].emission.rateOverTimeMultiplier;
            patch.baseShape[i] = patch.systems[i].shape.scale;
            ParticleSystem.MainModule main = patch.systems[i].main;
            main.playOnAwake = false;
            main.loop = true;
        }

        patch.light = patch.root.GetComponentInChildren<Light>(true);
        if (patch.light != null)
        {
            patch.lightBase = patch.light.intensity;
            patch.light.shadows = LightShadows.None;
            // Its own flicker script would fight the fade.
            foreach (MonoBehaviour flicker in patch.light.GetComponents<MonoBehaviour>())
                flicker.enabled = false;
        }

        foreach (MeshRenderer r in patch.root.GetComponentsInChildren<MeshRenderer>(true))
        {
            patch.puddle = r.transform;
            break;
        }

        patch.obstacle = patch.root.AddComponent<NavMeshObstacle>();
        patch.obstacle.shape = NavMeshObstacleShape.Box;
        patch.obstacle.carving = true;
        patch.obstacle.carveOnlyStationary = false;
        patch.root.SetActive(false);
        return patch;
    }

    public void Begin(Vector3 a, Vector3 b, float half, float seconds, bool circle)
    {
        Spared = null;
        Generation++;
        Circle = circle;
        Half = half;
        Started = Time.time;
        Ends = Time.time + seconds;
        Dying = false;
        nextTick.Clear();
        root.SetActive(true);
        A = a;
        Shape(b);
        foreach (ParticleSystem system in systems)
        {
            system.Clear(true);
            system.Play(true);
        }
    }

    // Moves the far end (a thread's fire follows its head).
    public void Stretch(Vector3 to)
    {
        to.y = A.y;
        Shape(to);
    }

    private void Shape(Vector3 b)
    {
        B = b;
        Vector3 along = B - A;
        float length = along.magnitude;
        Vector3 mid = (A + B) * 0.5f;
        // LookRotation(along x up) turns local X onto the patch's line.
        root.transform.SetPositionAndRotation(mid, length > 0.01f ? Quaternion.LookRotation(Vector3.Cross(along, Vector3.up).normalized, Vector3.up) : Quaternion.identity);

        // Local X runs along the patch, Z across it.
        float spanX = Circle ? Half * 2f : length + Half * 2f;
        float spanZ = Half * 2f;
        float scale = spanX * spanZ / (3.2f * 3.2f);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.ShapeModule shape = systems[i].shape;
            Vector3 s = baseShape[i];
            if (Circle)
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = Half * s.x / 3.2f;
                shape.radiusThickness = 1f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(s.x * spanX / 3.2f, s.y * spanZ / 3.2f, s.z);
            }

            ParticleSystem.EmissionModule emission = systems[i].emission;
            emission.rateOverTimeMultiplier = baseRates[i] * Mathf.Clamp(scale, 0.15f, 8f);
            ParticleSystem.MainModule main = systems[i].main;
            main.maxParticles = Mathf.Max(BoxRef, Mathf.CeilToInt(baseRates[i] * Mathf.Clamp(scale, 0.15f, 8f) * main.startLifetime.constantMax * 1.3f));
        }

        if (puddle != null)
            puddle.localScale = new Vector3(spanX / 3.2f, 1f, spanZ / 3.2f);

        obstacle.center = new Vector3(0f, 1f, 0f);
        obstacle.size = new Vector3(spanX, 2f, spanZ);
    }

    public bool Contains(Vector3 point, float margin)
    {
        return HallActors.SegmentDistance(point, A, B) <= Half + margin;
    }

    // Damage due to an actor in the fire now: the contact hit on entering, then a tick every interval.
    public float Touch(object actor, float now, float contact, float tick, float interval)
    {
        if (nextTick.TryGetValue(actor, out float next) == false || now > next + interval * 2f)
        {
            nextTick[actor] = now + interval;
            return contact;
        }

        if (now < next)
            return 0f;
        nextTick[actor] = next + interval;
        return tick;
    }

    public void GoOut(float seconds)
    {
        Dying = true;
        Ends = Mathf.Min(Ends, Time.time + seconds);
    }

    public void Tick(float now, float fadeTime, bool lightAllowed)
    {
        float left = Ends - now;
        float fade = Mathf.Clamp01(left / Mathf.Max(fadeTime, 0.01f));
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.EmissionModule emission = systems[i].emission;
            float scale = Circle ? Half * Half * 4f / (3.2f * 3.2f) : (Length + Half * 2f) * Half * 2f / (3.2f * 3.2f);
            emission.rateOverTimeMultiplier = baseRates[i] * Mathf.Clamp(scale, 0.15f, 8f) * fade;
        }

        if (light != null)
        {
            light.enabled = lightAllowed && fade > 0.05f;
            light.intensity = lightBase * fade * (0.85f + 0.15f * Mathf.PerlinNoise(Generation * 3.1f, now * 6f));
        }
    }

    public bool Over(float now) => now >= Ends;

    public void End()
    {
        Generation++;
        foreach (ParticleSystem system in systems)
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        root.SetActive(false);
    }
}
