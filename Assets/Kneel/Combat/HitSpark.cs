using UnityEngine;

// Short burst of sparks at a contact point. Built at runtime with a shared URP particle material,
// so no prefab or asset is needed.
public static class HitSpark
{
    private static Material material;

    public static void Spawn(Vector3 position, Vector3 direction, Color color, int count = 14)
    {
        var go = new GameObject("HitSpark");
        go.transform.position = position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            go.transform.rotation = Quaternion.LookRotation(direction);
        }

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = color;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.02f;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 1f;
        renderer.sharedMaterial = GetMaterial();

        particles.Play();
    }

    private static Material GetMaterial()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
        }

        return material;
    }
}
