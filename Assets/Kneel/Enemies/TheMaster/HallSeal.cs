using UnityEngine;

// The wall that closes the throne hall behind the player at the aggro line (The Master.md §3, decided: a souls-like
// ash mist, not fire, so it can't be mistaken for the hazards). A grey translucent veil with drifting ash and faint
// embers; walking into it pushes you back (a solid collider), no damage. Kindles at the crossing, stays until he dies
// or you do. Built at runtime from the two materials MasterSetup assigns.
public class HallSeal : MonoBehaviour
{
    public Vector3 size = new Vector3(42f, 7f, 0.6f);
    public Material mistMaterial;
    public Material emberMaterial;

    private BoxCollider wall;
    private ParticleSystem mist;
    private ParticleSystem embers;

    public bool Kindled { get; private set; }

    private void Awake()
    {
        wall = gameObject.AddComponent<BoxCollider>();
        wall.size = size;
        wall.center = new Vector3(0f, size.y * 0.5f, 0f);
        wall.enabled = false;

        // A pale veil, kept thin and low: the top-down camera looks down through it whenever the player fights near the
        // line, and the level's dark smoke blacked the player out there.
        if (mistMaterial != null)
        {
            mistMaterial = new Material(mistMaterial);
            mistMaterial.SetColor("_BaseColor", new Color(0.82f, 0.78f, 0.74f, 1f));

            // It thins out within a few metres of the camera, so the intro's low shot from behind the line looks
            // through it; the follow camera, 10 m and more away, sees the wall.
            mistMaterial.EnableKeyword("_FADING_ON");
            mistMaterial.SetFloat("_CameraFadingEnabled", 1f);
            mistMaterial.SetFloat("_CameraNearFadeDistance", 2.5f);
            mistMaterial.SetFloat("_CameraFarFadeDistance", 7f);
            mistMaterial.SetVector("_CameraFadeParams", new Vector4(2.5f, 1f / (7f - 2.5f), 0f, 0f));
        }

        mist = Make("Mist", mistMaterial, 110, new Vector2(2.5f, 5f), new Vector2(1.2f, 2.4f), new Color(1f, 1f, 1f, 0.12f), 0.2f, new Vector3(size.x, 3.5f, 0.3f));
        embers = Make("Embers", emberMaterial, 60, new Vector2(1.5f, 3f), new Vector2(0.05f, 0.12f), new Color(1f, 0.45f, 0.15f, 1f), 0.6f, size);
    }

    private ParticleSystem Make(string name, Material material, float rate, Vector2 life, Vector2 sizeRange, Color color, float rise, Vector3 box)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, box.y * 0.5f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startSpeed = 0f;
        main.startColor = color;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = box;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.5f, rise);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    public void Kindle()
    {
        Kindled = true;
        wall.enabled = true;
        mist.Play();
        embers.Play();
    }

    public void Dissolve()
    {
        Kindled = false;
        wall.enabled = false;
        mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
