using UnityEngine;

// A ring of glowing ember on the floor (owner, 2026-11-15: "a ring of glowing ember, like how the ember of the cursor is
// glowing"): the aim ember's own layers (a bright core, a faint glow, sparks, rising embers, in its material) emitted
// along a circle. Where the Commands' fire will strike (it follows Kael, then locks) and each strike of the Gather's
// rain. At the lock it burns hotter and denser; Release lets it go out.
public class EmberRing : MonoBehaviour
{
    private ParticleSystem core;
    private ParticleSystem glow;
    private ParticleSystem sparks;
    private ParticleSystem embers;
    private float radius;
    private float released = -1f;

    public bool Locked { get; private set; }

    public Vector3 Origin => transform.position;

    public static EmberRing Spawn(Material material, Vector3 at, float radius)
    {
        var go = new GameObject("EmberRing");
        go.transform.position = Floor(at);
        var ring = go.AddComponent<EmberRing>();
        ring.radius = radius;
        float around = Mathf.PI * 2f * radius;
        // Core: the bright grains of the ring itself, riding with it.
        ring.core = Layer(go.transform, "Core", material, radius, ParticleSystemSimulationSpace.Local,
            new Color(1f, 0.55f, 0.18f, 1f), new Vector2(0.12f, 0.18f), new Vector2(0.22f, 0.34f), around * 42f, 0f, 0f);
        // Glow: soft and faint round the grains, as the cursor ember's halo.
        ring.glow = Layer(go.transform, "Glow", material, radius, ParticleSystemSimulationSpace.Local,
            new Color(1f, 0.3f, 0.06f, 0.13f), new Vector2(0.5f, 0.7f), new Vector2(0.45f, 0.6f), around * 5f, 0f, 0f);
        // Sparks and embers drift up off it and stay where they rose (world space).
        ring.sparks = Layer(go.transform, "Sparks", material, radius, ParticleSystemSimulationSpace.World,
            new Color(1f, 0.45f, 0.12f, 1f), new Vector2(0.025f, 0.045f), new Vector2(0.7f, 1.2f), around * 4f, 0.4f, 0.9f);
        ring.embers = Layer(go.transform, "Embers", material, radius, ParticleSystemSimulationSpace.World,
            new Color(1f, 0.3f, 0.05f, 1f), new Vector2(0.014f, 0.022f), new Vector2(1.4f, 2.2f), around * 2.5f, 0.3f, 0.7f);
        return ring;
    }

    private static ParticleSystem Layer(Transform parent, string name, Material material, float radius, ParticleSystemSimulationSpace space,
        Color colour, Vector2 size, Vector2 life, float rate, float rise, float riseMax)
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
        main.simulationSpace = space;
        main.maxParticles = Mathf.CeilToInt(rate * life.y * 1.8f) + 8;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;
        if (riseMax > 0f)
        {
            // Up off the floor (the system is turned to lie flat: its local z is the world's up), wandering a little.
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
        }

        // Fading in and out over each grain's life, as the cursor's do.
        ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
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
        if (Locked)
            return;
        Locked = true;
        Boost(core, 1.7f, 1.3f, new Color(1f, 0.72f, 0.38f, 1f));
        Boost(glow, 1.6f, 1.15f, new Color(1f, 0.36f, 0.08f, 0.2f));
        Boost(sparks, 2.2f, 1f, new Color(1f, 0.55f, 0.18f, 1f));
    }

    private static void Boost(ParticleSystem ps, float rate, float size, Color colour)
    {
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTimeMultiplier *= rate;
        ParticleSystem.MainModule main = ps.main;
        main.startSizeMultiplier *= size;
        main.startColor = colour;
        main.maxParticles = Mathf.CeilToInt(main.maxParticles * rate);
    }

    // The fire has landed (or the spell was cut short): it stops and goes out.
    public void Release()
    {
        if (released >= 0f)
            return;
        released = Time.time;
        foreach (ParticleSystem ps in new[] { core, glow, sparks, embers })
            if (ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 2.5f);
    }
}
