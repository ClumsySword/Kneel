using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

// Shows the Ash meter on Kael himself, never on a bar. The one thing the player must read is whether it's full, so
// that gets the strongest cues: a sound as it fills, a flare up the arm, the hand's light coming on (light on the ground
// means ready), heavy wisps, the hum at its loudest and the colour grade. While filling, the glow climbs from the hand
// toward the elbow and the hum rises. The glow is a shell just outside the skin of the right forearm and hand
// (Kneel/Ember Glow), so the body's model and material are untouched. Also flies siphon motes from what gave the Ash
// to the hand, and shows each ability's key the first time it can be cast.
[RequireComponent(typeof(AshMeter))]
[RequireComponent(typeof(PlayerAsh))]
public class AshPresenter : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int FillRangeId = Shader.PropertyToID("_FillRange");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int GradeId = Shader.PropertyToID("_KneelAshGrade");

    // Skin radius of the forearm and hand around their bones, measured on the Ember Warden (80th percentile of the
    // vertex distances, so the shell sits outside the gauntlet as well as the skin).
    [SerializeField]
    private float forearmRadius = 0.088f;

    [SerializeField]
    private float handRadius = 0.07f;

    // How long a key hint stays up, in real seconds.
    [SerializeField]
    private float hintTime = 4f;

    // The first fill of a session overshoots the grade, as the signature moment.
    private static bool firstFullShown;

    private AshMeter meter;
    private PlayerAsh ash;
    private AshSettings settings;
    private Animator animator;

    private readonly List<Renderer> shells = new List<Renderer>();
    private readonly List<Vector4> shellRanges = new List<Vector4>();
    private MaterialPropertyBlock block;
    private Light handLight;
    private Transform hand;
    private ParticleSystem embers;
    private ParticleSystem wisps;
    private ParticleSystem fizzleAsh;
    private ParticleSystem motes;
    private SkinnedMeshRenderer body;
    private AudioSource cueSource;
    private AudioSource humSource;

    private float shownFill;
    private float pulse;
    private float stutter;
    private float grade;
    private float gradeOvershoot;
    private float surgeGhostTime = -1f;
    private int surgeGhosts;

    private readonly List<Mote> flying = new List<Mote>();
    private ParticleSystem.Particle[] moteParticles = new ParticleSystem.Particle[64];

    private GameObject hintCanvas;
    private RectTransform hintCanvasRect;
    private TextMeshProUGUI hintText;
    private float hintLeft;

    private struct Mote
    {
        public Vector3 from;
        public Vector3 lift;
        public float age;
        public float duration;
        public float size;
    }

    private void Awake()
    {
        meter = GetComponent<AshMeter>();
        ash = GetComponent<PlayerAsh>();
        block = new MaterialPropertyBlock();
    }

    private void Start()
    {
        settings = meter.Settings;
        animator = GetComponentInChildren<Animator>();
        body = LargestSkinnedMesh();

        if (animator != null && animator.isHuman)
        {
            BuildArm();
        }

        BuildAudio();
        BuildHint();
        shownFill = meter.Fraction;
    }

    private void OnEnable()
    {
        meter.Gained += OnGained;
        meter.BecameFull += OnBecameFull;
        meter.Spent += OnSpent;
        meter.Emptied += OnEmptied;
        ash.Fizzled += OnFizzled;
        ash.Locked += OnLocked;
        ash.Cast += OnCast;
        ash.Unlocked += OnUnlocked;
        ash.AshfallLanded += OnAshfallLanded;
        ash.Surged += OnSurged;
        ash.BlindThrown += OnBlindThrown;
        ash.Rekindled += OnRekindled;
    }

    private void OnDisable()
    {
        meter.Gained -= OnGained;
        meter.BecameFull -= OnBecameFull;
        meter.Spent -= OnSpent;
        meter.Emptied -= OnEmptied;
        ash.Fizzled -= OnFizzled;
        ash.Locked -= OnLocked;
        ash.Cast -= OnCast;
        ash.Unlocked -= OnUnlocked;
        ash.AshfallLanded -= OnAshfallLanded;
        ash.Surged -= OnSurged;
        ash.BlindThrown -= OnBlindThrown;
        ash.Rekindled -= OnRekindled;
        Shader.SetGlobalFloat(GradeId, 0f);
    }

    private void OnDestroy()
    {
        if (hintCanvas != null)
        {
            Destroy(hintCanvas);
        }
    }

    // --- Building ---

    private SkinnedMeshRenderer LargestSkinnedMesh()
    {
        SkinnedMeshRenderer best = null;
        foreach (SkinnedMeshRenderer candidate in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (candidate.sharedMesh != null && (best == null || candidate.sharedMesh.vertexCount > best.sharedMesh.vertexCount))
            {
                best = candidate;
            }
        }

        return best;
    }

    private void BuildArm()
    {
        Transform elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform knuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        if (elbow == null || hand == null)
        {
            return;
        }

        // The forearm from just past the elbow to the wrist, and the back of the hand to the knuckles. They stop short of
        // the joints so a bent elbow or a fist doesn't show the shape. Fill runs from the hand (0) to the elbow (1).
        Vector3 forearm = hand.position - elbow.position;
        Renderer forearmShell = MakeShell("AshGlow_Forearm", elbow, elbow.position + forearm * 0.12f, hand.position - forearm * 0.04f, forearmRadius, new Vector4(0.2f, 1f, 0f, 0f));
        if (knuckle != null)
        {
            MakeShell("AshGlow_Hand", hand, hand.position, knuckle.position, handRadius, new Vector4(0f, 0.2f, 0f, 0f));
        }

        var lightObject = new GameObject("AshHandLight");
        lightObject.transform.SetParent(hand, false);
        handLight = lightObject.AddComponent<Light>();
        handLight.type = LightType.Point;
        handLight.shadows = LightShadows.None;
        handLight.range = settings.handLightRange;
        handLight.intensity = 0f;
        handLight.enabled = false;

        // Sparks that cling to the arm a moment, and dark ash wisps that trail off it, both from the forearm's shell.
        embers = ArmSystem("AshArmEmbers", forearmShell, settings.emberParticleMaterial);
        var main = embers.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = settings.emberColor;
        main.gravityModifier = -0.04f;
        AshEffects.Shrink(embers);

        wisps = ArmSystem("AshArmWisps", forearmShell, settings.ashParticleMaterial);
        var wispMain = wisps.main;
        wispMain.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.9f);
        wispMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        wispMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        wispMain.startColor = new Color(0.1f, 0.085f, 0.075f, 0.6f);
        var drift = wisps.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.World;
        drift.x = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        drift.y = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        drift.z = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        AshEffects.GrowAndFade(wisps);

        // Dead grey ash falling from the fingers when a press fizzles.
        fizzleAsh = AshEffects.MakeSystem("AshFizzle", settings.ashParticleMaterial, hand, hand.position);
        var fizzleMain = fizzleAsh.main;
        fizzleMain.stopAction = ParticleSystemStopAction.None;
        fizzleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        fizzleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        fizzleMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        fizzleMain.startColor = new Color(0.45f, 0.43f, 0.41f, 0.8f);
        fizzleMain.gravityModifier = 0.35f;
        var fizzleShape = fizzleAsh.shape;
        fizzleShape.enabled = true;
        fizzleShape.shapeType = ParticleSystemShapeType.Sphere;
        fizzleShape.radius = 0.05f;
        AshEffects.GrowAndFade(fizzleAsh);

        // Siphon motes, placed by hand each frame.
        motes = AshEffects.MakeSystem("AshMotes", settings.emberParticleMaterial, null, transform.position);
        var moteMain = motes.main;
        moteMain.stopAction = ParticleSystemStopAction.None;
        moteMain.maxParticles = moteParticles.Length;
        motes.Play();
    }

    private Renderer MakeShell(string name, Transform bone, Vector3 from, Vector3 to, float radius, Vector4 fillRange)
    {
        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        DestroyImmediate(shell.GetComponent<Collider>());
        shell.name = name;
        shell.layer = body != null ? body.gameObject.layer : gameObject.layer;

        // Unity's capsule runs along its Y, 2 long and 1 across. +Y points down the arm, toward the fingers.
        float r = radius + settings.shellOffset;
        Vector3 along = to - from;
        shell.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, along));
        shell.transform.SetParent(bone, true);
        float boneScale = Mathf.Max(0.0001f, bone.lossyScale.x);
        shell.transform.localScale = new Vector3(r * 2f, along.magnitude * 0.5f + r, r * 2f) / boneScale;

        var shellRenderer = shell.GetComponent<MeshRenderer>();
        shellRenderer.sharedMaterial = settings.glowMaterial;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;
        shellRenderer.enabled = false;
        shells.Add(shellRenderer);
        shellRanges.Add(fillRange);
        return shellRenderer;
    }

    private ParticleSystem ArmSystem(string name, Renderer shell, Material material)
    {
        ParticleSystem particles = AshEffects.MakeSystem(name, material, shell.transform, shell.transform.position);
        particles.transform.localRotation = Quaternion.identity;
        var main = particles.main;
        main.loop = true;
        main.duration = 1f;
        main.stopAction = ParticleSystemStopAction.None;
        main.maxParticles = 80;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.MeshRenderer;
        shape.meshRenderer = (MeshRenderer)shell;

        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 1.5f;

        particles.Play();
        return particles;
    }

    private void BuildAudio()
    {
        AudioMixerGroup sfx = null;
        var mixer = Resources.Load<AudioMixer>("KneelMixer");
        if (mixer != null)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
            sfx = groups.Length > 0 ? groups[0] : null;
        }

        cueSource = gameObject.AddComponent<AudioSource>();
        cueSource.playOnAwake = false;
        cueSource.spatialBlend = 0.3f;
        cueSource.outputAudioMixerGroup = sfx;

        humSource = gameObject.AddComponent<AudioSource>();
        humSource.playOnAwake = false;
        humSource.loop = true;
        humSource.spatialBlend = 0.3f;
        humSource.volume = 0f;
        humSource.clip = AshAudio.Hum(settings);
        humSource.outputAudioMixerGroup = sfx;
    }

    private void BuildHint()
    {
        hintCanvas = new GameObject("AshKeyHint");
        var canvas = hintCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        hintCanvasRect = (RectTransform)hintCanvas.transform;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(hintCanvas.transform, false);
        hintText = textObject.AddComponent<TextMeshProUGUI>();
        hintText.fontSize = 22f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.textWrappingMode = TextWrappingModes.NoWrap;
        hintText.raycastTarget = false;
        hintText.color = new Color(0.92f, 0.88f, 0.8f, 0f);
        hintText.outlineWidth = 0.2f;
        hintText.outlineColor = new Color32(10, 8, 6, 255);
        hintText.enabled = false;

        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.sizeDelta = new Vector2(400f, 80f);
    }

    // --- Each frame ---

    private void Update()
    {
        bool granted = meter.Granted;
        bool full = granted && meter.IsFull;
        float dt = Time.deltaTime;

        // Filling glides up; a spend or a death drops it at once (they set shownFill).
        shownFill = Mathf.MoveTowards(shownFill, granted ? meter.Fraction : 0f, dt * 1.5f);
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 2f);
        stutter = Mathf.MoveTowards(stutter, 0f, dt);

        float time = Time.time;
        float intensity;
        float fill;
        if (full)
        {
            // A slow heartbeat while it waits to be spent.
            intensity = settings.fullIntensity * (0.92f + 0.08f * Mathf.Sin(time * 2.4f));
            fill = 1f;
        }
        else
        {
            intensity = settings.fillingIntensity * Mathf.Lerp(0.25f, 1f, shownFill);
            fill = shownFill * 0.85f;
        }

        // A fizzle makes the glow stutter.
        if (stutter > 0f)
        {
            intensity *= Mathf.PerlinNoise(time * 40f, 0f) < 0.5f ? 0.15f : 1f;
        }

        bool visible = granted && (shownFill > 0.001f || pulse > 0.001f);
        for (int i = 0; i < shells.Count; i++)
        {
            shells[i].enabled = visible;
            if (visible == false)
            {
                continue;
            }

            shells[i].GetPropertyBlock(block);
            block.SetColor(ColorId, settings.emberColor);
            block.SetFloat(FillId, fill);
            block.SetVector(FillRangeId, shellRanges[i]);
            block.SetFloat(IntensityId, intensity);
            block.SetFloat(PulseId, pulse);
            shells[i].SetPropertyBlock(block);
        }

        if (handLight != null)
        {
            float lightLevel = (full ? settings.handLightIntensity * (0.95f + 0.05f * Mathf.PerlinNoise(time * 3f, 1f)) : 0f)
                + pulse * settings.handLightIntensity * 0.8f;
            handLight.color = (settings.emberColor / Mathf.Max(0.01f, settings.emberColor.maxColorComponent));
            handLight.range = settings.handLightRange;
            handLight.intensity = lightLevel;
            handLight.enabled = lightLevel > 0.01f;
        }

        SetRate(embers, granted ? settings.emberRate * (full ? 1f : shownFill * 0.3f) : 0f);
        SetRate(wisps, granted ? settings.wispRate * (full ? 1f : Mathf.Clamp01(shownFill * 2f - 0.8f) * 0.5f) : 0f);

        UpdateHum(granted, full, time);
        UpdateGrade(full, dt);
        UpdateMotes(dt);
        UpdateSurgeGhosts();
        UpdateHint();
    }

    private static void SetRate(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        var emission = particles.emission;
        emission.rateOverTime = rate;
    }

    private void UpdateHum(bool granted, bool full, float time)
    {
        float target = granted ? settings.humVolume * settings.volume * (full ? 0.85f + 0.15f * Mathf.Sin(time * 5f) : shownFill * 0.55f) : 0f;
        humSource.volume = Mathf.MoveTowards(humSource.volume, target, Time.unscaledDeltaTime * 0.8f);

        // Up three semitones from empty to full.
        humSource.pitch = 1f + 0.19f * (full ? 1f : shownFill);

        if (humSource.volume > 0.001f && humSource.isPlaying == false)
        {
            humSource.Play();
        }
        else if (humSource.volume <= 0.001f && humSource.isPlaying)
        {
            humSource.Stop();
        }
    }

    private void UpdateGrade(bool full, float dt)
    {
        grade = Mathf.MoveTowards(grade, full ? 1f : 0f, dt / (full ? 0.6f : 0.4f));
        gradeOvershoot = Mathf.MoveTowards(gradeOvershoot, 0f, dt / 0.8f);
        Shader.SetGlobalFloat(GradeId, Mathf.Clamp01(grade + gradeOvershoot * 0.4f) * settings.gradeStrength * (1f + gradeOvershoot * 0.4f));
    }

    // --- Motes ---

    private void OnGained(AshMeter.Source source, float amount, Vector3 origin)
    {
        PlayCue(AshAudio.Siphon(settings), source == AshMeter.Source.Kill ? 0.8f : 0.45f, Random.Range(0.9f, 1.15f));

        if (motes == null)
        {
            return;
        }

        int count = source == AshMeter.Source.Kill ? Random.Range(8, 15) : source == AshMeter.Source.Parry ? Random.Range(2, 4) : Random.Range(1, 3);
        for (int i = 0; i < count && flying.Count < moteParticles.Length; i++)
        {
            // Up first and over, so they never cross an enemy's chest between it and the camera.
            flying.Add(new Mote
            {
                from = origin + Random.insideUnitSphere * 0.25f,
                lift = Vector3.up * Random.Range(1f, 1.8f) + Random.insideUnitSphere * 0.4f,
                age = -i * 0.03f,
                duration = Random.Range(0.4f, 0.8f),
                size = Random.Range(0.03f, 0.06f),
            });
        }
    }

    private void UpdateMotes(float dt)
    {
        if (motes == null)
        {
            return;
        }

        Vector3 to = hand != null ? hand.position : transform.position + Vector3.up;
        int shown = 0;
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            Mote mote = flying[i];
            mote.age += dt;
            if (mote.age >= mote.duration)
            {
                flying.RemoveAt(i);
                pulse = Mathf.Min(1f, pulse + 0.06f);
                continue;
            }

            flying[i] = mote;
            if (mote.age < 0f)
            {
                continue;
            }

            float t = mote.age / mote.duration;
            float ease = t * t * (3f - 2f * t);
            Vector3 high = Vector3.Lerp(mote.from, to, 0.5f) + mote.lift;
            Vector3 position = Vector3.Lerp(Vector3.Lerp(mote.from, high, ease), Vector3.Lerp(high, to, ease), ease);

            moteParticles[shown].position = position;
            moteParticles[shown].startSize = mote.size * (1f - t * 0.5f);
            moteParticles[shown].startColor = settings.emberColor;
            moteParticles[shown].remainingLifetime = 1f;
            moteParticles[shown].startLifetime = 1f;
            shown++;
        }

        motes.SetParticles(moteParticles, shown);
    }

    // --- Events ---

    private void OnBecameFull()
    {
        bool first = firstFullShown == false;
        firstFullShown = true;

        PlayCue(AshAudio.Full(settings), first ? 1f : 0.8f, 1f);
        pulse = 1f;
        gradeOvershoot = first ? 1f : 0f;
        if (embers != null)
        {
            embers.Emit(first ? 40 : 25);
        }

        ShowUnseenHints();
    }

    private void OnSpent()
    {
        PlayCue(AshAudio.Spend(settings), 0.9f, 1f);
        shownFill = 0f;
        pulse = 1f;
        grade = 0f;
        gradeOvershoot = 0f;
    }

    private void OnEmptied()
    {
        shownFill = 0f;
    }

    private void OnFizzled(PlayerAsh.Ability ability)
    {
        PlayCue(AshAudio.Fizzle(settings), 0.8f, Random.Range(0.95f, 1.05f));
        stutter = 0.35f;
        if (fizzleAsh != null)
        {
            fizzleAsh.Emit(12);
        }
    }

    private void OnLocked(PlayerAsh.Ability ability)
    {
        PlayCue(AshAudio.Locked(settings), 0.7f, 1f);
    }

    private void OnCast(PlayerAsh.Ability ability)
    {
        if (ability == PlayerAsh.Ability.AshenSword && embers != null)
        {
            embers.Emit(30);
        }
    }

    private void OnUnlocked(PlayerAsh.Ability ability)
    {
        PlayCue(AshAudio.Unlock(settings), 1f, 1f);
        pulse = 1.5f;
        if (embers != null)
        {
            embers.Emit(40);
        }
    }

    private void OnAshfallLanded(Vector3 center)
    {
        PlayCue(AshAudio.Slam(), 1f, 1f);
    }

    private void OnSurged()
    {
        PlayCue(AshAudio.Whoosh(), 0.9f, 1f);
        surgeGhostTime = Time.time;
        surgeGhosts = 0;
    }

    private void OnBlindThrown(Vector3 from)
    {
        PlayCue(AshAudio.Puff(), 0.8f, 1f);
    }

    private void OnRekindled()
    {
        PlayCue(AshAudio.Unlock(settings), 0.6f, 1.6f);
        pulse = 1f;
    }

    // Three afterimages over the burst.
    private void UpdateSurgeGhosts()
    {
        if (surgeGhostTime < 0f)
        {
            return;
        }

        float duration = settings.surgeDuration;
        while (surgeGhosts < 3 && Time.time - surgeGhostTime >= surgeGhosts * duration / 3f)
        {
            AshEffects.Afterimage(body, settings, 0.35f);
            surgeGhosts++;
        }

        if (surgeGhosts >= 3)
        {
            surgeGhostTime = -1f;
        }
    }

    private void PlayCue(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || cueSource == null)
        {
            return;
        }

        cueSource.pitch = pitch;
        cueSource.PlayOneShot(clip, volume * settings.volume);
    }

    // --- Key hints ---

    // When the meter fills, the keys of the unlocked abilities not yet shown, once per save.
    private void ShowUnseenHints()
    {
        var lines = new System.Text.StringBuilder();
        for (int i = 0; i < PlayerAsh.Names.Length; i++)
        {
            var ability = (PlayerAsh.Ability)i;
            int bit = 1 << i;
            if ((AshProgress.HintsSeen & bit) != 0 || ash.IsUnlocked(ability) == false)
            {
                continue;
            }

            AshProgress.HintsSeen |= bit;
            if (lines.Length > 0)
            {
                lines.Append('\n');
            }

            lines.Append(KeybindService.KeyFor("Ash" + (i + 1))).Append("  ").Append(PlayerAsh.Names[i]);
        }

        if (lines.Length == 0)
        {
            return;
        }

        hintText.text = lines.ToString();
        hintLeft = hintTime;
    }

    private void UpdateHint()
    {
        if (hintText == null)
        {
            return;
        }

        hintLeft -= Time.unscaledDeltaTime;
        float alpha = Mathf.Clamp01(hintLeft / 0.4f) * Mathf.Clamp01((hintTime - hintLeft) / 0.3f);
        Camera cam = Camera.main;
        Vector3 screen = cam != null ? cam.WorldToScreenPoint(transform.position + Vector3.up * 2.3f) : Vector3.zero;
        bool shown = alpha > 0f && screen.z > 0f;
        hintText.enabled = shown;
        if (shown == false)
        {
            return;
        }

        float scale = hintCanvasRect.lossyScale.x > 0f ? hintCanvasRect.lossyScale.x : 1f;
        ((RectTransform)hintText.transform).anchoredPosition = new Vector2(screen.x, screen.y) / scale;
        Color c = hintText.color;
        c.a = alpha * 0.9f;
        hintText.color = c;
    }
}
