using UnityEngine;

// The Ash Colossus's one-shot effects, in the hall's own language (the L1 smoke, ember and glow sprites the chandeliers,
// statues and holes use): a burst of ash and embers where a blow lands, the Hammerfall's low ring of dust, and the
// Leap's ground shadow (the chandeliers' shadow decal). Each cleans itself up.
public static class ColossusFx
{
    // Ash and embers thrown up over a circle of `radius` where a blow lands.
    public static void Burst(AshColossus owner, Vector3 at, float radius, int embers)
    {
        if (owner == null)
            return;
        var root = new GameObject("ColossusBurst");
        root.transform.position = new Vector3(at.x, owner.transform.position.y + 0.15f, at.z);
        Emit(root.transform, owner.smokeMaterial, new Color(0.42f, 0.39f, 0.37f, 0.65f), radius, Mathf.RoundToInt(10 + radius * 5f), new Vector2(1.2f, 2.6f), new Vector2(1.2f, 2.4f), new Vector2(0.8f, 2.6f), -0.04f);
        Emit(root.transform, owner.emberMaterial, new Color(1f, 0.55f, 0.2f), radius * 0.8f, embers, new Vector2(0.04f, 0.1f), new Vector2(0.6f, 1.4f), new Vector2(2f, 6f), 0.35f);
        Object.Destroy(root, 3.5f);
    }

    // The Hammerfall's shockwave: a low ring of dust running out from `from` to `to` metres in `seconds`.
    public static void Ring(AshColossus owner, Vector3 at, float from, float to, float seconds)
    {
        if (owner == null)
            return;
        var go = new GameObject("HammerRing");
        go.transform.position = new Vector3(at.x, owner.transform.position.y + 0.25f, at.z);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = (to - from) / Mathf.Max(seconds, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startColor = new Color(0.45f, 0.41f, 0.38f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 64) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = from;
        shape.radiusThickness = 0f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.08f;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = owner.smokeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        Object.Destroy(go, 2f);
    }

    // The Leap's shadow on the floor: grows as he crouches, sharpens as he comes down.
    public static GameObject Shadow(Material material, Vector3 at, float radius)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "LeapShadow";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MoveShadow(go, at, radius, 0f);
        return go;
    }

    public static void MoveShadow(GameObject shadow, Vector3 at, float radius, float progress)
    {
        if (shadow == null)
            return;
        shadow.transform.position = new Vector3(at.x, at.y + 0.04f, at.z);
        shadow.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.6f, 1f, progress);
    }

    private static void Emit(Transform parent, Material material, Color color, float radius, int count, Vector2 size, Vector2 life, Vector2 speed, float gravity)
    {
        if (count <= 0)
            return;
        var go = new GameObject("Fx");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 4;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = Mathf.Max(0.2f, radius);
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var color2 = ps.colorOverLifetime;
        color2.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        color2.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }
}
