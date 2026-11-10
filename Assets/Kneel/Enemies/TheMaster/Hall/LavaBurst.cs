using UnityEngine;

// The moment lava cracks burst (a floor panel opening, the Hammerfall's cracks erupting): a flash of light, a fountain
// of embers, chunks of stone thrown up, and ash. Cleans itself up.
public static class LavaBurst
{
    public static void Spawn(Vector3 at, Vector2 size, Material ember, Material smoke, Mesh debris, Material debrisMaterial, float strength = 1f)
    {
        var root = new GameObject("LavaBurst");
        root.transform.position = at + Vector3.up * 0.1f;

        // The flash.
        var lightGo = new GameObject("Flash");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = Vector3.up * 1.2f;
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.15f);
        light.shadows = LightShadows.None;
        light.range = Mathf.Max(size.x, size.y) * 1.6f + 4f;
        lightGo.AddComponent<FadeLight>().Begin(35f * strength, 0.5f);

        Vector3 box = new Vector3(size.x, 0.3f, size.y);
        Emit(root.transform, "Embers", ember, new Color(1f, 0.55f, 0.18f), box, Mathf.RoundToInt(110 * strength), new Vector2(0.05f, 0.14f), new Vector2(0.7f, 1.6f), new Vector2(4f, 11f), 0.6f, null);
        Emit(root.transform, "Ash", smoke, new Color(0.3f, 0.26f, 0.24f, 0.7f), box, Mathf.RoundToInt(24 * strength), new Vector2(1.2f, 2.6f), new Vector2(1.4f, 2.6f), new Vector2(1f, 3f), -0.05f, null);
        if (debris != null && debrisMaterial != null)
            Emit(root.transform, "Stones", debrisMaterial, Color.white, box, Mathf.RoundToInt(18 * strength), new Vector2(0.12f, 0.32f), new Vector2(1.2f, 1.8f), new Vector2(4f, 8f), 2.2f, debris);
        Object.Destroy(root, 3.5f);
    }

    private static void Emit(Transform parent, string name, Material material, Color color, Vector3 box, int count, Vector2 size, Vector2 life, Vector2 speed, float gravity, Mesh mesh)
    {
        if (material == null || count <= 0)
            return;
        var go = new GameObject(name);
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
        if (mesh != null)
        {
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            var plane = new GameObject("Floor").transform;
            plane.SetParent(parent, false);
            plane.localPosition = Vector3.down * 0.1f;
            collision.SetPlane(0, plane);
            collision.dampen = 0.5f;
            collision.bounce = 0.25f;
        }

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        shape.randomDirectionAmount = 0.35f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, mesh != null ? 0.85f : 0.4f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mesh != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
        }

        ps.Play();
    }
}
