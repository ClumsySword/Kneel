using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Command's strike (owner, 2026-11-15, replacing the line-of-sight rule): fire falling from the vault like a bolt of
// lightning onto the ember ring that has followed Kael through the tell. Begin it at the lock: a streak drops from high
// up onto the locked point over `fall` seconds (the last-moment cue), then the bolt flashes, sparks fly, a short light
// flares, and the floor cracks open in the Hammerfall's lava-crack style, smaller (StrikeCracks). The move deals the
// blow itself, at the same moment. Also the Gather's fire rain (FireRain).
public class FireStrike : MonoBehaviour
{
    private const float Height = 17f;
    private const int Segments = 26;
    private const float FlashTime = 0.22f;
    private const float FadeTime = 0.35f;
    private const float CoreWidth = 0.11f;
    private const float SheathWidth = 0.32f;
    private const float FlareIntensity = 14f;

    private LineRenderer core;
    private LineRenderer sheath;
    private Light flare;
    private MasterSettings settings;
    private Vector3 at;
    private Vector3 top;
    private float radius;
    private float fall;
    private float started;
    private float struck = -1f;
    private float nextJitter;
    private readonly List<Vector3> path = new List<Vector3>();
    private System.Random random;

    public static FireStrike Begin(MasterSettings settings, Vector3 at, float radius, float fall)
    {
        var go = new GameObject("FireStrike");
        var strike = go.AddComponent<FireStrike>();
        strike.settings = settings;
        strike.at = at;
        strike.radius = radius;
        strike.fall = Mathf.Max(fall, 0.05f);
        strike.started = Time.time;
        strike.random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        Vector2 lean = Random.insideUnitCircle * 1.2f;
        strike.top = at + new Vector3(lean.x, Height, lean.y);
        strike.core = EmberLines.Line(go.transform, "Core", EmberLines.Ember(1.4f), CoreWidth, false);
        strike.sheath = EmberLines.Line(go.transform, "Sheath", EmberLines.Ember(0.18f), SheathWidth, false);
        strike.Jitter();
        return strike;
    }

    // A jagged path from the sky to the floor; the kinks shrink near the ground so the bolt ends on the mark.
    private void Jitter()
    {
        path.Clear();
        for (int i = 0; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            Vector3 p = Vector3.Lerp(top, at, t);
            float kink = (1f - t) * 0.65f + 0.08f;
            if (i > 0 && i < Segments)
                p += new Vector3(Range(-kink, kink), 0f, Range(-kink, kink));
            path.Add(p);
        }
    }

    private float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

    private void Update()
    {
        float now = Time.time;
        if (struck < 0f)
        {
            // The streak: the bolt's head falling down its path, a short tail behind it.
            float head = Mathf.Clamp01((now - started) / fall);
            Draw(Mathf.Max(0f, head - 0.35f), head, 0.55f);
            if (head >= 1f)
                Strike();
            return;
        }

        float since = now - struck;
        if (since < FlashTime && now >= nextJitter)
        {
            // The bolt flickers: a new path every few frames.
            nextJitter = now + 0.05f;
            Jitter();
        }

        float k = since < FlashTime ? 1f : 1f - (since - FlashTime) / FadeTime;
        if (k <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Draw(0f, 1f, k);
        if (flare != null)
            flare.intensity = FlareIntensity * Mathf.Clamp01(1f - since / 0.25f);
    }

    private void Strike()
    {
        struck = Time.time;
        Jitter();
        StrikeCracks.Spawn(settings, at, radius * 1.5f, 0.12f, 0.15f, 1.1f);
        EmberLines.Sparks(at, 46, 6f);
        var go = new GameObject("Flare");
        go.transform.SetParent(transform, false);
        go.transform.position = at + Vector3.up * 1.2f;
        flare = go.AddComponent<Light>();
        flare.type = LightType.Point;
        flare.color = new Color(1f, 0.45f, 0.15f);
        flare.range = 7f;
        flare.intensity = FlareIntensity;
        flare.shadows = LightShadows.None;
        HallActors.LightBosses(flare);
        Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.25f, 0.3f);
    }

    // The path from `from` to `to` (0 = sky, 1 = floor), widths scaled by `k`.
    private void Draw(float from, float to, float k)
    {
        var points = new List<Vector3>();
        float a = from * Segments, b = to * Segments;
        points.Add(Point(a));
        for (int i = Mathf.CeilToInt(a + 1e-4f); i < b; i++)
            points.Add(path[i]);
        points.Add(Point(b));
        foreach (LineRenderer line in new[] { core, sheath })
        {
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
        }

        core.widthMultiplier = CoreWidth * k;
        sheath.widthMultiplier = SheathWidth * k;
    }

    private Vector3 Point(float index)
    {
        int i = Mathf.Clamp(Mathf.FloorToInt(index), 0, Segments - 1);
        return Vector3.Lerp(path[i], path[i + 1], index - i);
    }
}

// The Hammerfall's lava cracks, small and quick (owner, 2026-11-15: "a small version of hammerfall (not identical)"):
// its own crack texture (fewer, shorter cracks), turned at random, racing out over `grow`, flaring, then cooling over
// `fade`. The Commands' strikes, the fire rain and the Leap's landing. Visual only.
public static class StrikeCracks
{
    public static LavaCracks Spawn(MasterSettings settings, Vector3 at, float radius, float grow, float hold, float fade)
    {
        if (settings == null || settings.strikeCrackMaterial == null)
            return null;
        LavaCracks cracks = LavaCracks.Spawn(settings.strikeCrackMaterial, settings.strikeCrackMap, at, radius, Random.Range(0f, 360f));
        cracks.StartCoroutine(Run(cracks, grow, hold, fade));
        return cracks;
    }

    private static IEnumerator Run(LavaCracks cracks, float grow, float hold, float fade)
    {
        float start = Time.time;
        while (Time.time - start < grow)
        {
            float t = (Time.time - start) / grow;
            cracks.Reveal = 1.05f * (1f - (1f - t) * (1f - t));
            cracks.Intensity = Mathf.Lerp(1.5f, LavaCracks.BurstIntensity, t);
            yield return null;
        }

        cracks.Reveal = 1.05f;
        cracks.Intensity = LavaCracks.BurstIntensity;
        yield return new WaitForSeconds(hold);
        start = Time.time;
        while (Time.time - start < fade)
        {
            float t = (Time.time - start) / fade;
            cracks.Intensity = Mathf.Lerp(LavaCracks.BurstIntensity, 0f, Mathf.Sqrt(t));
            yield return null;
        }

        Object.Destroy(cracks.gameObject);
    }
}

// The ember-line look the Master's spells share (the threads, the strike's bolt): additive ember lines and small sparks.
// Materials are cached and shared.
public static class EmberLines
{
    private static readonly Dictionary<int, Material> embers = new Dictionary<int, Material>();
    private static Material spark;

    // The Hammerfall cracks' colours (MasterSetup.CrackMaterial): the ember and the hotter core.
    public static readonly Color CrackColour = new Color(1f, 0.22f, 0.03f);
    public static readonly Color CrackHot = new Color(1f, 0.42f, 0.1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        embers.Clear();
        spark = null;
    }

    private static Shader Particles => Shader.Find("Universal Render Pipeline/Particles/Unlit");

    // The threads' ember (1 = as bright as a thread's core).
    public static Material Ember(float brightness) => Additive(new Color(1f, 0.34f, 0.07f) * 0.8f * brightness);

    // An additive line in `colour` (cached by colour).
    public static Material Additive(Color colour)
    {
        int key = (Mathf.RoundToInt(colour.r * 255f) << 16) ^ (Mathf.RoundToInt(colour.g * 255f) << 8) ^ Mathf.RoundToInt(colour.b * 255f);
        if (embers.TryGetValue(key, out Material m) && m != null)
            return m;
        m = Make(colour);
        embers[key] = m;
        return m;
    }

    private static Material Make(Color colour)
    {
        Shader shader = Particles;
        if (shader == null)
            return null;
        var m = new Material(shader);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3001;
        m.SetColor("_BaseColor", colour);
        return m;
    }

    // A line renderer; `flat` lays it on the floor (facing up), otherwise it faces the camera.
    public static LineRenderer Line(Transform parent, string name, Material material, float width, bool flat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        if (flat)
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
        line.widthMultiplier = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 0.7f), new Keyframe(1f, 0.15f));
        line.positionCount = 0;
        return line;
    }

    // A burst of small sparks thrown up from `at` (they fall back and go out).
    public static void Sparks(Vector3 at, int count, float speed)
    {
        var go = new GameObject("Sparks");
        go.transform.position = at + Vector3.up * 0.1f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = new Color(1f, 0.6f, 0.25f);
        main.gravityModifier = 1.6f;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 1.5f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (spark == null)
            spark = Make(new Color(1f, 0.55f, 0.2f));
        renderer.sharedMaterial = spark;
        ps.Play();
    }
}
