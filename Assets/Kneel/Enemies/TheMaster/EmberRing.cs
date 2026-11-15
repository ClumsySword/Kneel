using UnityEngine;

// A ring of glowing ember on the floor (owner, 2026-11-15: "a ring of glowing ember, like how the ember of the cursor is
// glowing"; then: "not a circle of dots ... a circle with the lines drawing up, in the same style as the ember from the
// cursor"). Two lines draw round from the side facing him and meet opposite as the strike lands, so the ring is also the
// timer. Each line is a hot ember core in a soft glow band, both flickering like the cursor's ember, with its sparks
// and embers (its material) rising off the drawn part. Where the Commands' fire will strike (it follows Kael, then
// locks) and each strike of the Gather's rain. At the lock it burns hotter; Release lets it go out.
public class EmberRing : MonoBehaviour
{
    private const int Points = 64;
    private const float CoreWidth = 0.09f;
    private const float GlowWidth = 0.5f;

    // The cursor ember's colours: its core (1, 0.55, 0.18) and its halo (1, 0.3, 0.06).
    private static readonly Color CoreColour = new Color(1f, 0.55f, 0.18f);
    private static readonly Color HotColour = new Color(1f, 0.72f, 0.38f);
    private static readonly Color GlowColour = new Color(1f, 0.3f, 0.06f);

    private static Material coreMaterial;
    private static Material glowMaterial;

    private readonly LineRenderer[] cores = new LineRenderer[2];
    private readonly LineRenderer[] glows = new LineRenderer[2];
    private ParticleSystem sparks;
    private ParticleSystem embers;
    private float radius;
    private float started;
    private float duration;
    private Vector3 startDirection = Vector3.forward;
    private float lockedAt = -1f;
    private float released = -1f;
    private float seed;
    private float[] wander;

    public bool Locked => lockedAt >= 0f;

    public Vector3 Origin => transform.position;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        coreMaterial = null;
        glowMaterial = null;
    }

    // A ring of `radius` at `at`, drawing round from the side facing `from` over `duration` seconds.
    public static EmberRing Spawn(Material emberMaterial, Vector3 at, Vector3 from, float radius, float duration)
    {
        var go = new GameObject("EmberRing");
        go.transform.position = Floor(at);
        var ring = go.AddComponent<EmberRing>();
        ring.radius = radius;
        ring.started = Time.time;
        ring.duration = Mathf.Max(duration, 0.1f);
        ring.seed = Random.value * 100f;
        Vector3 toward = from - at;
        toward.y = 0f;
        if (toward.sqrMagnitude > 0.01f)
            ring.startDirection = toward.normalized;

        // A little unevenness round the ring, as a burning line has.
        ring.wander = new float[Points + 1];
        for (int i = 0; i <= Points; i++)
            ring.wander[i] = (Mathf.PerlinNoise(ring.seed, i * 0.35f) - 0.5f) * 0.08f;

        EnsureMaterials();
        for (int side = 0; side < 2; side++)
        {
            ring.glows[side] = Line(go.transform, "Glow", glowMaterial, GlowWidth);
            ring.cores[side] = Line(go.transform, "Core", coreMaterial, CoreWidth);
        }

        float around = Mathf.PI * 2f * radius;
        ring.sparks = Rising(go.transform, "Sparks", emberMaterial, radius, new Color(1f, 0.45f, 0.12f, 1f), new Vector2(0.025f, 0.045f), new Vector2(0.7f, 1.2f), around * 4f, 0.4f, 0.9f);
        ring.embers = Rising(go.transform, "Embers", emberMaterial, radius, new Color(1f, 0.3f, 0.05f, 1f), new Vector2(0.014f, 0.022f), new Vector2(1.4f, 2.2f), around * 2.5f, 0.3f, 0.7f);
        ring.LateUpdate();
        return ring;
    }

    private static void EnsureMaterials()
    {
        if (coreMaterial != null && glowMaterial != null)
            return;
        // A soft band across the line's width (v): bright down the middle, fading to nothing at both edges.
        var band = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float v = (y + 0.5f) / 64f * 2f - 1f;
            float a = Mathf.Exp(-v * v * 4.5f) * Mathf.Clamp01((1f - Mathf.Abs(v)) * 6f);
            for (int x = 0; x < 4; x++)
                band.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        band.Apply();
        coreMaterial = Additive(band, Color.white);
        glowMaterial = Additive(band, Color.white);
    }

    private static Material Additive(Texture2D texture, Color colour)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3001;
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", colour);
        return m;
    }

    private static LineRenderer Line(Transform parent, string name, Material material, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        // The drawing end tapers like a line still burning its way round.
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0.35f));
        line.positionCount = 0;
        return line;
    }

    // The cursor ember's sparks or embers, rising off the ring and staying where they rose.
    private static ParticleSystem Rising(Transform parent, string name, Material material, float radius, Color colour, Vector2 size, Vector2 life, float rate, float rise, float riseMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.05f;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = colour;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(rate * life.y * 2.5f) + 8;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(rise, riseMax);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.8f;
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }

    private static Vector3 Floor(Vector3 at)
    {
        if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, LayerMask.GetMask("Default", "Ground"), QueryTriggerInteraction.Ignore))
            return hit.point;
        return at;
    }

    // While it follows: the ring's centre (ignored once locked).
    public void Aim(Vector3 at)
    {
        if (Locked == false)
            transform.position = new Vector3(at.x, transform.position.y, at.z);
    }

    // It stops where it is and burns hotter: the fire lands here.
    public void Lock()
    {
        if (Locked == false)
            lockedAt = Time.time;
    }

    // The fire has landed (or the spell was cut short): it stops and goes out.
    public void Release()
    {
        if (released >= 0f)
            return;
        released = Time.time;
        foreach (ParticleSystem ps in new[] { sparks, embers })
            if (ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 2.5f);
    }

    private void LateUpdate()
    {
        float now = Time.time;
        // How far round it has drawn: eased out, closing as the strike lands.
        float p = Mathf.Clamp01((now - started) / duration);
        float drawn = 1f - (1f - p) * (1f - p);
        float hot = Locked ? Mathf.Clamp01((now - lockedAt) / 0.08f) : 0f;
        float fade = released >= 0f ? Mathf.Clamp01(1f - (now - released) / 0.35f) : 1f;
        // The cursor ember's flicker (its light wavers by 35% at 6 Hz), a little gentler on a line.
        float flicker = 1f + 0.22f * (Mathf.PerlinNoise(seed, now * 6f) * 2f - 1f);

        Color core = Color.Lerp(CoreColour, HotColour, hot) * (0.95f * flicker * fade);
        Color glow = GlowColour * (Mathf.Lerp(0.2f, 0.32f, hot) * flicker * fade);
        core.a = 1f;
        glow.a = 1f;
        Vector3 centre = transform.position + Vector3.up * 0.045f;
        int count = Mathf.Max(2, Mathf.CeilToInt(Points * 0.5f * drawn) + 1);
        for (int side = 0; side < 2; side++)
        {
            float dir = side == 0 ? 1f : -1f;
            LineRenderer c = cores[side], g = glows[side];
            c.positionCount = count;
            g.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Min(i / (Points * 0.5f), drawn);
                float angle = dir * t * 180f;
                int w = Mathf.Clamp(Mathf.RoundToInt(side == 0 ? t * Points * 0.5f : Points - t * Points * 0.5f), 0, Points);
                Vector3 p3 = centre + Quaternion.AngleAxis(angle, Vector3.up) * startDirection * (radius + wander[w]);
                c.SetPosition(i, p3 + Vector3.up * 0.003f);
                g.SetPosition(i, p3);
            }

            c.startColor = c.endColor = core;
            g.startColor = g.endColor = glow;
            c.widthMultiplier = CoreWidth * Mathf.Lerp(1f, 1.35f, hot);
            g.widthMultiplier = GlowWidth * Mathf.Lerp(1f, 1.2f, hot);
        }

        // Sparks and embers off the drawn part (more once it burns hot).
        float around = Mathf.PI * 2f * radius;
        if (released < 0f)
        {
            ParticleSystem.EmissionModule se = sparks.emission;
            se.rateOverTime = around * 4f * drawn * Mathf.Lerp(1f, 2.2f, hot);
            ParticleSystem.EmissionModule ee = embers.emission;
            ee.rateOverTime = around * 2.5f * drawn;
        }
    }
}
