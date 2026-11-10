using UnityEngine;

// Kael's scar on his left forearm (The Master.md §3, decided): it flares when the curse pulls on him (the Master's palm
// in the intro, every forced kneel), lights the dark in P2, and goes dark in the ending. A warm light and an ember burst
// on the forearm bone; the painted emission strip rides the same Flare (Stage 4 art).
[RequireComponent(typeof(PlayerCombat))]
public class PlayerScar : MonoBehaviour
{
    public Color color = new Color(1f, 0.45f, 0.15f);
    public float flareIntensity = 6f;
    public float flareTime = 1.2f;
    // The resting glow (the darkness wave raises it).
    public float restIntensity = 0f;

    private Light glow;
    private ParticleSystem burst;
    private float flare;

    public float RestIntensity { get => restIntensity; set => restIntensity = value; }

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>();
        Transform forearm = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftLowerArm) : transform;

        var go = new GameObject("ScarGlow");
        go.transform.SetParent(forearm, false);
        glow = go.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 2.5f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;

        burst = go.AddComponent<ParticleSystem>();
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = burst.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor = color * 3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        var emission = burst.emission;
        emission.rateOverTime = 0f;
        var shape = burst.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(shader);

        GetComponent<PlayerCombat>().OnForcedKneel += _ => Flare();
    }

    public void Flare()
    {
        flare = 1f;
        burst.Emit(30);
    }

    private void Update()
    {
        flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime / Mathf.Max(flareTime, 0.05f));
        glow.intensity = restIntensity + flareIntensity * flare * flare;
        glow.enabled = glow.intensity > 0.01f;
    }
}
