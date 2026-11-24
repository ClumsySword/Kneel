using System.Collections.Generic;
using UnityEngine;

// The abilities' one-off effects, built at runtime like HitSpark: the Blind throw and the cloud it leaves on an enemy,
// Ashfall's ring of dust, Surge's afterimages. All short and low, and kept off enemy shoulders and weapons, where
// their tells are.
public static class AshEffects
{
    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int FillRangeId = Shader.PropertyToID("_FillRange");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");

    public static readonly Color AshColor = new Color(0.16f, 0.14f, 0.13f, 0.75f);

    // Blind's smoke: near-black, a little blue so it reads as soot rather than a hole in the picture.
    public static readonly Color BlindSmoke = new Color(0.02f, 0.02f, 0.025f, 1f);
    public static readonly Color BlindSmokeThin = new Color(0.04f, 0.04f, 0.045f, 0.75f);

    private static Material fallbackMaterial;

    // --- Blind ---

    public static void ThrowBlind(Vector3 from, Transform target, IBlindable blindable, AshSettings settings)
    {
        var go = new GameObject("AshBlindThrow");
        go.transform.position = from;
        go.AddComponent<AshBlindProjectile>().Launch(target, blindable, settings);

        // A black puff where it leaves the hand.
        ParticleSystem puff = MakeSystem("BlindPuff", settings.ashParticleMaterial, null, from);
        var main = puff.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = BlindSmokeThin;
        main.gravityModifier = -0.05f;
        GrowAndFade(puff, 0.1f);
        Burst(puff, 12);
    }

    // A small black cloud in front of and above the face for the blind's length. Off the shoulders and the weapon.
    public static void BlindCloud(Transform enemy, float seconds, AshSettings settings)
    {
        Animator animator = enemy.GetComponentInChildren<Animator>();
        Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        Vector3 at = head != null ? head.position : enemy.position + Vector3.up * 1.7f;
        at += enemy.forward * 0.18f + Vector3.up * 0.08f;

        ParticleSystem cloud = MakeSystem("BlindCloud", settings.ashParticleMaterial, head != null ? head : enemy, at);
        var main = cloud.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = BlindSmoke;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;
        main.maxParticles = 60;

        var emission = cloud.emission;
        emission.rateOverTime = 26f;

        var shape = cloud.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        GrowAndFade(cloud);
        cloud.Play();
        Object.Destroy(cloud.gameObject, seconds + 1.2f);
        cloud.gameObject.AddComponent<StopAfter>().seconds = seconds;
    }

    // --- Ashfall ---

    public static void AshfallRing(Vector3 center, AshSettings settings)
    {
        if (Physics.Raycast(center + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 3f, LayerMask.GetMask("Ground", "Obstacles"), QueryTriggerInteraction.Ignore))
        {
            center = ground.point;
        }

        // Dust and ash thrown out along the ground to the ring's reach.
        ParticleSystem dust = MakeSystem("AshfallDust", settings.ashParticleMaterial, null, center + Vector3.up * 0.08f);
        dust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        var main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        float speed = settings.ashfallRadius / 0.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed * 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        // Pale, so it reads on dark mud as well as stone.
        main.startColor = new Color(0.55f, 0.5f, 0.45f, 0.85f);
        // Enough puffs that the ring stays closed at any radius.
        int count = Mathf.Clamp(Mathf.RoundToInt(30f * settings.ashfallRadius), 60, 300);
        main.maxParticles = count + 20;

        var shape = dust.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.radiusThickness = 0f;

        var limit = dust.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.5f;
        limit.dampen = 0.12f;

        GrowAndFade(dust, 0.02f);
        Burst(dust, count);

        // Embers up from where the hand met the ground.
        ParticleSystem embers = MakeSystem("AshfallEmbers", settings.emberParticleMaterial, null, center + Vector3.up * 0.1f);
        var emberMain = embers.main;
        emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        emberMain.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        emberMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        emberMain.startColor = settings.emberColor;
        emberMain.gravityModifier = 0.6f;

        var emberShape = embers.shape;
        emberShape.enabled = true;
        emberShape.shapeType = ParticleSystemShapeType.Hemisphere;
        emberShape.radius = 0.4f;
        embers.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        Shrink(embers);
        Burst(embers, 30);
    }

    // --- Ashen Sword ---

    // The sword's flames (fire and ash along the blade, and a light), built on first use. Light them with Ignite.
    public static AshenSwordFlames IgniteSword(Transform sword, AshSettings settings)
    {
        AshenSwordFlames flames = sword.GetComponentInChildren<AshenSwordFlames>();
        if (flames == null)
        {
            var go = new GameObject("AshenSwordFlames");
            go.transform.SetParent(sword, false);
            flames = go.AddComponent<AshenSwordFlames>();
            flames.Build(sword, settings);
        }

        return flames;
    }

    // --- Surge ---

    private static readonly List<Mesh> afterimageMeshes = new List<Mesh>();
    private static int nextAfterimage;
    private const int AfterimagePool = 4;

    // An ash-glow copy of the body where it stands now, fading over `fade` seconds. Four meshes, reused.
    public static void Afterimage(SkinnedMeshRenderer body, AshSettings settings, float fade)
    {
        if (body == null || settings.glowMaterial == null)
        {
            return;
        }

        if (afterimageMeshes.Count < AfterimagePool)
        {
            afterimageMeshes.Add(new Mesh { name = "AshAfterimage" });
        }

        Mesh mesh = afterimageMeshes[nextAfterimage % afterimageMeshes.Count];
        nextAfterimage++;
        body.BakeMesh(mesh, true);

        var go = new GameObject("AshAfterimage");
        go.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = settings.glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.AddComponent<AfterimageFade>().Begin(renderer, mesh, fade);
    }

    // --- Shared ---

    // A stopped, non-looping system that destroys itself, at `position`, optionally riding `parent`.
    public static ParticleSystem MakeSystem(string name, Material material, Transform parent, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        if (parent != null)
        {
            go.transform.SetParent(parent, true);
        }

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.scalingMode = ParticleSystemScalingMode.Shape;

        var emission = particles.emission;
        emission.rateOverTime = 0f;

        var shape = particles.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material != null ? material : FallbackMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return particles;
    }

    public static void Burst(ParticleSystem particles, int count)
    {
        var emission = particles.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        particles.Play();
    }

    // Puffs swell and thin out, fading in over the first `fadeIn` of their life; sparks shrink to nothing.
    public static void GrowAndFade(ParticleSystem particles, float fadeIn = 0.15f)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));

        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
    }

    public static void Shrink(ParticleSystem particles)
    {
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    private static Material FallbackMaterial()
    {
        if (fallbackMaterial == null)
        {
            fallbackMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        }

        return fallbackMaterial;
    }

    // Stops a looping system's emission after a while; it then destroys itself once the last particle is gone.
    private class StopAfter : MonoBehaviour
    {
        public float seconds;

        private void Update()
        {
            seconds -= Time.deltaTime;
            if (seconds <= 0f)
            {
                GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmitting);
                enabled = false;
            }
        }
    }

    private class AfterimageFade : MonoBehaviour
    {
        private MeshRenderer target;
        private MaterialPropertyBlock block;
        private float fade;
        private float age;

        public void Begin(MeshRenderer renderer, Mesh mesh, float seconds)
        {
            target = renderer;
            fade = Mathf.Max(0.05f, seconds);
            block = new MaterialPropertyBlock();
            block.SetFloat(FillId, 1f);
            block.SetVector(FillRangeId, new Vector4(0f, 0f, 0f, 0f));
            block.SetFloat(PulseId, 0f);
            Apply(1f);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float left = 1f - age / fade;
            if (left <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Apply(left);
        }

        private void Apply(float strength)
        {
            block.SetFloat(IntensityId, 0.5f * strength * strength);
            target.SetPropertyBlock(block);
        }
    }
}

// The thrown ash: flies at the target's face, stopped by walls. Arriving blinds it.
public class AshBlindProjectile : MonoBehaviour
{
    private Transform target;
    private IBlindable blindable;
    private AshSettings settings;
    private Transform head;
    private int blockMask;
    private float life = 2f;

    public void Launch(Transform target, IBlindable blindable, AshSettings settings)
    {
        this.target = target;
        this.blindable = blindable;
        this.settings = settings;
        blockMask = LayerMask.GetMask("Obstacles", "Ground");

        Animator animator = target.GetComponentInChildren<Animator>();
        head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

        // A black, churning ball of smoke: puffs born in a small sphere around the projectile, carried with it, that
        // swell and thin out. No ember.
        ParticleSystem body = AshEffects.MakeSystem("Cloud", settings.ashParticleMaterial, transform, transform.position);
        var main = body.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = AshEffects.BlindSmoke;
        main.maxParticles = 60;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = body.emission;
        emission.rateOverTime = 80f;
        var shape = body.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;
        var spin = body.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        AshEffects.GrowAndFade(body, 0.1f);
        body.Play();

        // The wake: thinner smoke left hanging in the air behind it, drifting up a little.
        ParticleSystem trail = AshEffects.MakeSystem("Trail", settings.ashParticleMaterial, transform, transform.position);
        var trailMain = trail.main;
        trailMain.loop = true;
        trailMain.duration = 1f;
        trailMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        trailMain.startSpeed = 0f;
        trailMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        trailMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        trailMain.startColor = AshEffects.BlindSmokeThin;
        trailMain.gravityModifier = -0.04f;
        trailMain.stopAction = ParticleSystemStopAction.None;
        var trailEmission = trail.emission;
        trailEmission.rateOverDistance = 18f;
        AshEffects.GrowAndFade(trail, 0.1f);
        trail.Play();
    }

    private Vector3 Aim => head != null ? head.position + target.forward * 0.1f : target.position + Vector3.up * 1.6f;

    private void Update()
    {
        life -= Time.deltaTime;
        if (target == null || life <= 0f)
        {
            Finish(false);
            return;
        }

        Vector3 from = transform.position;
        Vector3 toAim = Aim - from;
        float step = settings.blindProjectileSpeed * Time.deltaTime;

        if (toAim.magnitude <= step + 0.05f)
        {
            Finish(true);
            return;
        }

        Vector3 to = from + toAim.normalized * step;
        if (Physics.Linecast(from, to, out RaycastHit wall, blockMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = wall.point;
            Finish(false);
            return;
        }

        transform.position = to;
    }

    private void Finish(bool hit)
    {
        if (hit)
        {
            blindable.Blind(settings.blindDuration);
            AshEffects.BlindCloud(target, settings.blindDuration, settings);
        }

        // The trail lingers and thins out after the thrown ash is gone.
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>())
        {
            particles.transform.SetParent(null, true);
            var main = particles.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(gameObject);
    }
}

// Ashen Sword: flames, sparks and ash along the blade, and a light. Built once per sword and reused; it burns for the
// time it's given, fades over the last second, and goes out early on Extinguish (death).
public class AshenSwordFlames : MonoBehaviour
{
    private const float RampIn = 0.25f;
    private const float FadeOut = 1f;

    private ParticleSystem fire;
    private ParticleSystem wrap;
    private ParticleSystem sparks;
    private ParticleSystem ash;
    private Light glow;
    private AshSettings settings;
    private float fireRate;
    private float wrapRate;
    private float sparkRate;
    private float ashRate;
    private float litAt = -999f;
    private float endAt = -999f;

    public bool IsLit => Time.time < endAt;

    public void Build(Transform sword, AshSettings settings)
    {
        this.settings = settings;

        // Blade from base to tip, in the sword's own space (it rides the hand and the scabbard alike).
        Transform bladeBase = sword.Find("BladeBase");
        Transform bladeTip = sword.Find("BladeTip");
        Vector3 from = bladeBase != null ? bladeBase.localPosition : new Vector3(0f, 0.1f, 0f);
        Vector3 to = bladeTip != null ? bladeTip.localPosition : new Vector3(0f, 1.07f, 0f);
        float halfLength = Vector3.Distance(from, to) * 0.5f;

        // An edge emitter lies along its local X; turn it to run along the blade.
        transform.localPosition = (from + to) * 0.5f;
        transform.localRotation = Quaternion.FromToRotation(Vector3.right, (to - from).normalized);

        fireRate = settings.ashenSwordFlameRate;
        wrapRate = fireRate * 0.6f;
        sparkRate = fireRate * 0.2f;
        ashRate = fireRate * 0.25f;

        // Fire licking up off the edge: short-lived, bright, shrinking, rising in world space so it trails the swing.
        fire = Emitter("Fire", settings.emberParticleMaterial, halfLength);
        var fireMain = fire.main;
        fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.2f);
        fireMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fireMain.startColor = new ParticleSystem.MinMaxGradient(settings.emberColor, new Color(2.2f, 0.35f, 0.08f));
        fireMain.maxParticles = 300;
        Rise(fire, 1.2f, 2f);
        AshEffects.Shrink(fire);

        // A sheath of flame that stays on the blade (it moves with it), so the whole edge reads as burning even
        // standing still, at the game camera's distance.
        wrap = Emitter("Wrap", settings.emberParticleMaterial, halfLength);
        var wrapMain = wrap.main;
        wrapMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        wrapMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        wrapMain.startSpeed = 0f;
        wrapMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.17f);
        wrapMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        wrapMain.startColor = new ParticleSystem.MinMaxGradient(settings.emberColor * 0.8f, new Color(2f, 0.45f, 0.1f));
        wrapMain.maxParticles = 120;
        AshEffects.Shrink(wrap);

        // Loose sparks drifting off.
        sparks = Emitter("Sparks", settings.emberParticleMaterial, halfLength);
        var sparkMain = sparks.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
        sparkMain.startColor = settings.emberColor;
        sparkMain.gravityModifier = -0.15f;
        sparkMain.maxParticles = 60;
        AshEffects.Shrink(sparks);

        // Dark ash smoking off the blade, so it reads as ash-fire rather than a torch.
        ash = Emitter("Ash", settings.ashParticleMaterial, halfLength);
        var ashMain = ash.main;
        ashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        ashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        ashMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
        ashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ashMain.startColor = AshEffects.AshColor;
        ashMain.maxParticles = 80;
        Rise(ash, 0.6f, 1f);
        AshEffects.GrowAndFade(ash, 0.1f);

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(transform, false);
        glow = lightGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.45f, 0.15f);
        glow.range = settings.ashenSwordLightRange;
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
        glow.enabled = false;

        SetStrength(0f);
    }

    public void Ignite(float seconds)
    {
        if (IsLit == false)
        {
            litAt = Time.time;
        }

        endAt = Time.time + seconds;
        fire.Play();
        wrap.Play();
        sparks.Play();
        ash.Play();
        glow.enabled = true;
    }

    public void Extinguish()
    {
        endAt = Mathf.Min(endAt, Time.time);
    }

    private void Update()
    {
        if (glow.enabled == false)
        {
            return;
        }

        float rampIn = Mathf.Clamp01((Time.time - litAt) / RampIn);
        float fade = Mathf.Clamp01((endAt - Time.time) / FadeOut);
        SetStrength(rampIn * fade);

        // Out only once the burn has ended. Not on zero strength: the ramp-in is zero on the frame of the cast
        // itself (Ignite runs from the key press, before this Update), which put a fresh burn out at once.
        if (Time.time >= endAt)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            wrap.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            glow.enabled = false;
        }
    }

    private void SetStrength(float strength)
    {
        var e = fire.emission;
        e.rateOverTime = fireRate * strength;
        e = wrap.emission;
        e.rateOverTime = wrapRate * strength;
        e = sparks.emission;
        e.rateOverTime = sparkRate * strength;
        e = ash.emission;
        e.rateOverTime = ashRate * strength;

        // A fire's flicker, not a lamp's hum.
        float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 9f, 0.37f) + 0.1f * Mathf.Sin(Time.time * 23f);
        glow.intensity = settings.ashenSwordLightIntensity * strength * flicker;
    }

    private ParticleSystem Emitter(string name, Material material, float halfLength)
    {
        ParticleSystem particles = AshEffects.MakeSystem(name, material, transform, transform.position);
        particles.transform.localRotation = Quaternion.identity;
        var main = particles.main;
        main.loop = true;
        main.duration = 1f;
        main.stopAction = ParticleSystemStopAction.None;
        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
        shape.radius = halfLength;
        return particles;
    }

    private static void Rise(ParticleSystem particles, float min, float max)
    {
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(min, max);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }
}
