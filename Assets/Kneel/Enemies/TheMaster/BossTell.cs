using UnityEngine;
using UnityEngine.Audio;

// The Master's tells, one grammar for both phases (The Master.md §1): a white glint up his ember cracks and a high
// bell = parry; an ember-red left hand and an inhaled drone = roll; no glint = move. The palm light is the Command's
// (a real shadow-casting light: what its shadow covers is safe). All VFX are built at runtime; sounds are slots that
// MasterSetup fills with the placeholder clips (TheMaster/Audio), on the SFX mixer group.
public class BossTell : MonoBehaviour
{
    private static readonly int GlintColorId = Shader.PropertyToID("_KneelGlintColor");
    private static readonly int GlintBandId = Shader.PropertyToID("_KneelGlintBand");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Rig")]
    public Transform leftHand;
    public Transform swordTip;
    public float bodyHeight = 4.2f;

    [Header("Look")]
    public Color glintColor = new Color(1f, 0.95f, 0.85f);
    public float glintStrength = 7f;
    public Color emberColor = new Color(1f, 0.32f, 0.08f);
    public float emberLightIntensity = 8f;
    public Color palmColor = new Color(1f, 0.55f, 0.25f);
    public float palmLightIntensity = 60f;
    public float palmLightRange = 40f;
    // The cracks' resting glow (the material's emission intensity); darkness and phase scale it (EmberScale).
    public float crackGlow = 2.5f;

    [Header("Sounds (placeholders)")]
    public AudioClip rasp;
    public AudioClip bell;
    public AudioClip drone;
    public AudioClip deepDrone;
    public AudioClip hiss;
    public AudioClip hum;
    public AudioClip impact;
    public AudioClip deflect;

    // Multiplies the crack glow and every tell (1 normal; the darkness wave raises it so darkness never hides a tell).
    public float EmberScale { get; set; } = 1f;

    private AudioSource source;
    private AudioSource loop;
    private Light emberLight;
    private Light palmLight;
    private Light tipLight;
    private ParticleSystem emberSparks;
    private Renderer[] bodyRenderers;
    private MaterialPropertyBlock block;

    private float glintStart = -1f, glintLength;
    private float emberUntil = -1f;
    private float palmStart = -1f, palmLength;
    private float tipFlash;

    public Vector3 PalmLightPosition => palmLight != null ? palmLight.transform.position : transform.position + Vector3.up * bodyHeight;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.spatialBlend = 0.6f;
        source.playOnAwake = false;
        source.minDistance = 6f;
        source.maxDistance = 60f;
        loop = gameObject.AddComponent<AudioSource>();
        loop.spatialBlend = 0.6f;
        loop.playOnAwake = false;
        loop.loop = true;
        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        AudioMixerGroup[] sfx = mixer != null ? mixer.FindMatchingGroups("SFX") : null;
        if (sfx != null && sfx.Length > 0)
        {
            source.outputAudioMixerGroup = sfx[0];
            loop.outputAudioMixerGroup = sfx[0];
        }

        bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        block = new MaterialPropertyBlock();

        emberLight = MakeLight("EmberHandLight", leftHand, emberColor, 3.5f, false);
        palmLight = MakeLight("PalmLight", leftHand, palmColor, palmLightRange, true);
        palmLight.transform.localPosition = Vector3.zero;
        tipLight = MakeLight("GlintTipLight", swordTip, Color.white, 4f, false);
        emberSparks = MakeSparks(leftHand);
    }

    private void OnDisable() => Shader.SetGlobalVector(GlintColorId, Vector4.zero);

    private Light MakeLight(string name, Transform parent, Color color, float range, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        // Only the Command's palm light casts shadows, and only while it's lit (the L5 atlas lesson: one at a time).
        light.shadows = LightShadows.None;
        light.enabled = false;
        if (shadows)
        {
            light.shadowStrength = 1f;
            light.shadowBias = 0.05f;
        }

        return light;
    }

    private ParticleSystem MakeSparks(Transform parent)
    {
        var go = new GameObject("EmberHandSparks");
        go.transform.SetParent(parent != null ? parent : transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(emberColor * 2f, new Color(1f, 0.6f, 0.2f) * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.gravityModifier = -0.15f;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor", Color.white);
            renderer.sharedMaterial = material;
        }

        return ps;
    }

    // --- Tells ---

    // Parry: a white-hot band runs up his cracks over the tell and the blade flashes as it ends. Rasp, then the bell.
    public void Glint(float duration)
    {
        glintStart = Time.time;
        glintLength = Mathf.Max(duration, 0.1f);
        Play(rasp, 0.7f);
        Play(bell, 0.9f);
    }

    // Roll: the left hand goes ember-red with sparks, and an inhaled drone rises.
    public void Ember(float duration)
    {
        emberUntil = Time.time + duration + 0.15f;
        Loop(drone, duration);
        var emission = emberSparks.emission;
        emission.rateOverTime = 70f;
        emberSparks.Play();
    }

    // The Command: ember light swells in the palm and lights the hall (hard shadows), a deep drone under it.
    public void Palm(float duration)
    {
        palmStart = Time.time;
        palmLength = Mathf.Max(duration, 0.1f);
        palmLight.enabled = true;
        palmLight.shadows = LightShadows.Hard;
        Loop(deepDrone, duration);
    }

    // Threads: a hiss as the hand flicks.
    public void Hiss(float duration) => Play(hiss, 0.9f);

    // The Regal Guard's low hum.
    public void Hum(float duration) => Loop(hum, duration);

    // The Command's release.
    public void Bell() => Play(bell, 1f);

    public void Deflect()
    {
        Play(deflect, 1f);
        tipFlash = 1f;
    }

    public void GuardSpark(Vector3 point)
    {
        HitSpark.Spawn(point, Vector3.up, new Color(1f, 0.85f, 0.5f), 18);
        Play(deflect, 0.8f);
    }

    public void GuardBroken()
    {
        HitSpark.Spawn(swordTip != null ? swordTip.position : transform.position + Vector3.up * 3f, Vector3.up, new Color(1f, 0.7f, 0.3f), 40);
        Play(impact, 1f);
    }

    public void Kneel() => Play(impact, 0.6f);

    public void Impact(Vector3 point)
    {
        HitSpark.Spawn(point, Vector3.up, emberColor, 40);
        Play(impact, 1f);
    }

    public void StopTells()
    {
        glintStart = -1f;
        emberUntil = -1f;
        palmStart = -1f;
        palmLight.enabled = false;
        palmLight.shadows = LightShadows.None;
        loop.Stop();
        var emission = emberSparks.emission;
        emission.rateOverTime = 0f;
        Shader.SetGlobalVector(GlintColorId, Vector4.zero);
    }

    private void Update()
    {
        float scale = EmberScale;

        // Glint band: from his feet to his crown across the tell, strongest as it nears the top.
        if (glintStart >= 0f)
        {
            float t = (Time.time - glintStart) / glintLength;
            if (t >= 1f)
            {
                glintStart = -1f;
                tipFlash = 1f;
                Shader.SetGlobalVector(GlintColorId, Vector4.zero);
            }
            else
            {
                float baseY = transform.position.y;
                float height = baseY + Mathf.Lerp(0.2f, bodyHeight, t);
                Shader.SetGlobalVector(GlintBandId, new Vector4(height, bodyHeight * 0.12f, 0f, 0f));
                Color c = glintColor;
                Shader.SetGlobalVector(GlintColorId, new Vector4(c.r, c.g, c.b, glintStrength * scale * Mathf.SmoothStep(0.3f, 1f, t + 0.3f)));
            }
        }

        // Blade flash at the end of a glint (and on a deflect).
        tipFlash = Mathf.MoveTowards(tipFlash, 0f, Time.deltaTime * 6f);
        tipLight.enabled = tipFlash > 0f;
        tipLight.intensity = tipFlash * 12f * scale;

        // Ember hand.
        bool ember = Time.time < emberUntil;
        emberLight.enabled = ember;
        emberLight.intensity = ember ? emberLightIntensity * scale * (0.8f + 0.2f * Mathf.Sin(Time.time * 30f)) : 0f;
        if (ember == false && emberSparks.isEmitting)
        {
            var emission = emberSparks.emission;
            emission.rateOverTime = 0f;
        }

        // Palm light swells over the Command's tell.
        if (palmStart >= 0f)
        {
            float t = Mathf.Clamp01((Time.time - palmStart) / palmLength);
            palmLight.intensity = palmLightIntensity * scale * t * t;
            if (Time.time - palmStart > palmLength + 0.25f)
            {
                palmStart = -1f;
                palmLight.enabled = false;
                palmLight.shadows = LightShadows.None;
            }
        }

        // The cracks' resting glow, brighter while a tell is up.
        float glow = crackGlow * scale * (ember || palmStart >= 0f ? 1.6f : 1f);
        foreach (Renderer r in bodyRenderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, Color.white * glow);
            r.SetPropertyBlock(block);
        }
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null)
            source.PlayOneShot(clip, volume);
    }

    private void Loop(AudioClip clip, float duration)
    {
        if (clip == null)
            return;
        loop.clip = clip;
        loop.Play();
        loop.SetScheduledEndTime(AudioSettings.dspTime + duration + 0.2f);
    }

    // True when nothing solid stands between the two points (his body and the player don't count). The Command's test.
    public static bool LineOfSight(Vector3 from, Vector3 to, Transform ignoreA, Transform ignoreB)
    {
        int mask = ~(LayerMask.GetMask("Enemy", "Player", "Ignore Raycast", "UI"));
        Vector3 d = to - from;
        RaycastHit[] hits = Physics.RaycastAll(from, d.normalized, d.magnitude, mask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;
            if ((ignoreA != null && t.IsChildOf(ignoreA)) || (ignoreB != null && t.IsChildOf(ignoreB)))
                continue;
            return false;
        }

        return true;
    }
}
