using System.Collections.Generic;
using UnityEngine;

// The Command's strike (owner, 2026-11-15, replacing the line-of-sight rule): fire falling from the vault like a bolt of
// lightning onto the circle that has followed Kael through the tell. Begin it at the lock: a streak drops from high up
// onto the locked point over `fall` seconds (the last-moment cue), then the bolt flashes, sparks fly, cracks run out
// across the floor and a short light flares. Lines and small sparks only: no glow sprite (owner, 2026-11-11). The move
// deals the blow itself, at the same moment.
public class FireStrike : MonoBehaviour
{
    private const float Height = 17f;
    private const int Segments = 26;
    private const float FlashTime = 0.22f;
    private const float CoreWidth = 0.11f;
    private const float FlareIntensity = 14f;
    private const float SheathWidth = 0.32f;
    private const float FadeTime = 0.35f;

    private LineRenderer core;
    private LineRenderer sheath;
    private Light flare;
    private Vector3 at;
    private Vector3 top;
    private float radius;
    private float fall;
    private float started;
    private float struck = -1f;
    private float nextJitter;
    private readonly List<Vector3> path = new List<Vector3>();
    private System.Random random;

    public static FireStrike Begin(Vector3 at, float radius, float fall)
    {
        var go = new GameObject("FireStrike");
        var strike = go.AddComponent<FireStrike>();
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
        GroundCracks.Spawn(at, radius * 1.15f, 7, 0.9f);
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
        Object.FindAnyObjectByType<PlayerCamera>()?.AddShake(0.3f, 0.35f);
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

// Thin lava cracks that run out across the floor from a point, glow, and go out: the Command's strike and the
// Colossus's Leap (owner, 2026-11-15: "small cracks in the floor that appear briefly when he lands"). Each ember
// crack has a dark scorched outline under it, as the threads' fire does.
public class GroundCracks : MonoBehaviour
{
    private class Crack
    {
        public LineRenderer ember;
        public LineRenderer scorch;
        public readonly List<Vector3> points = new List<Vector3>();
    }

    private readonly List<Crack> cracks = new List<Crack>();
    private float started;
    private float life;
    private const float Grow = 0.12f;
    private float Width;

    public static GroundCracks Spawn(Vector3 at, float reach, int count, float life)
    {
        var go = new GameObject("GroundCracks");
        var g = go.AddComponent<GroundCracks>();
        g.started = Time.time;
        g.life = life;
        g.Width = Mathf.Clamp(reach * 0.03f, 0.05f, 0.12f);
        var random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        float y = at.y + 0.035f;
        float offset = Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = offset + i * 360f / count + Range(-18f, 18f);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            var crack = new Crack
            {
                scorch = EmberLines.Line(go.transform, "Scorch", EmberLines.Scorch(0.75f), g.Width * 2.4f, true),
                ember = EmberLines.Line(go.transform, "Ember", EmberLines.Ember(1f), g.Width, true),
            };
            float length = reach * Range(0.55f, 1f);
            Vector3 p = new Vector3(at.x, y, at.z) + dir * 0.25f;
            crack.points.Add(p);
            for (float d = 0.45f; d < length; d += Range(0.3f, 0.5f))
            {
                dir = Quaternion.AngleAxis(Range(-28f, 28f), Vector3.up) * dir;
                p += dir * 0.4f;
                crack.points.Add(p);
            }

            g.cracks.Add(crack);
        }

        return g;
    }

    private void Update()
    {
        float t = Time.time - started;
        if (t >= life)
        {
            Destroy(gameObject);
            return;
        }

        float grown = Mathf.Clamp01(t / Grow);
        // Bright at once, cooling over the second half.
        float heat = t < life * 0.4f ? 1f : 1f - (t - life * 0.4f) / (life * 0.6f);
        foreach (Crack crack in cracks)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(crack.points.Count * grown));
            crack.ember.positionCount = count;
            crack.scorch.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                crack.ember.SetPosition(i, crack.points[i] + Vector3.up * 0.005f);
                crack.scorch.SetPosition(i, crack.points[i]);
            }

            crack.ember.widthMultiplier = Width * heat;
            crack.scorch.startColor = crack.scorch.endColor = new Color(1f, 1f, 1f, Mathf.Clamp01(heat * 1.4f));
        }
    }
}

// The ember-line look the Master's spells share (the threads, the marks, the strike): additive ember lines, dark
// scorch lines under them, and small sparks. Materials are cached and shared.
public static class EmberLines
{
    private static readonly Dictionary<int, Material> embers = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> scorches = new Dictionary<int, Material>();
    private static Material spark;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        embers.Clear();
        scorches.Clear();
        spark = null;
    }

    private static Shader Particles => Shader.Find("Universal Render Pipeline/Particles/Unlit");

    // The threads' ember (1 = as bright as a thread's core).
    public static Material Ember(float brightness)
    {
        int key = Mathf.RoundToInt(brightness * 100f);
        if (embers.TryGetValue(key, out Material m) && m != null)
            return m;
        m = Make(true, new Color(1f, 0.34f, 0.07f) * 0.8f * brightness);
        embers[key] = m;
        return m;
    }

    // Soot: an alpha-blended near-black, for the outline round a burning crack.
    public static Material Scorch(float alpha)
    {
        int key = Mathf.RoundToInt(alpha * 100f);
        if (scorches.TryGetValue(key, out Material m) && m != null)
            return m;
        m = Make(false, new Color(0.035f, 0.02f, 0.015f, alpha));
        scorches[key] = m;
        return m;
    }

    private static Material Make(bool additive, Color colour)
    {
        Shader shader = Particles;
        if (shader == null)
            return null;
        var m = new Material(shader);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetInt("_ZWrite", 0);
        m.renderQueue = additive ? 3001 : 3000;
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
            spark = Make(true, new Color(1f, 0.55f, 0.2f));
        renderer.sharedMaterial = spark;
        ps.Play();
    }
}
