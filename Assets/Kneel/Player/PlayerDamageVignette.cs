using UnityEngine;
using UnityEngine.UI;

// The player's health readout: blood creeping in from the screen edges instead of a health bar.
// Missing health sets how far in it reaches, hits pulse it, low health makes it throb, and death covers the screen.
// Builds its own overlay canvas at runtime, so it works in any scene the player is in and leaves the
// level's post-processing alone. Runs on unscaled time, since hit-stop freezes Time.timeScale.
// Tuning values live in a PlayerDamageVignetteSettings asset.
[RequireComponent(typeof(Health))]
public class PlayerDamageVignette : MonoBehaviour
{
    [SerializeField]
    private PlayerDamageVignetteSettings settings;

    // Above the game UI, below the F1 overlay (IMGUI always draws last).
    private const int SortingOrder = 100;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int DepthId = Shader.PropertyToID("_Depth");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int NoiseOffsetId = Shader.PropertyToID("_NoiseOffset");
    private static readonly int ShadowWidthId = Shader.PropertyToID("_ShadowWidth");
    private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");

    private Health health;
    private PlayerCombat combat;
    private GameObject canvasObject;
    private RawImage image;
    private Material material;

    private float shownCoverage = 0f;
    private float pulse = 0f;
    private float heartbeatPhase = 0f;
    private float deathFade = 0f;
    private bool dead = false;

    // Health fraction to show instead of the real one, for tuning from the F1 overlay. Negative means off.
    [System.NonSerialized]
    public float previewHealth = -1f;

    public PlayerDamageVignetteSettings Settings => settings;

    // Coverage drawn this frame.
    public float Coverage { get; private set; }

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogWarning("PlayerDamageVignette has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerDamageVignetteSettings>();
        }

        health = GetComponent<Health>();
        combat = GetComponent<PlayerCombat>();

        if (settings.shader == null)
        {
            Debug.LogWarning("PlayerDamageVignette settings have no shader assigned; no vignette will show.", this);
            enabled = false;
            return;
        }

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        canvasObject = new GameObject("DamageVignette");

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var imageObject = new GameObject("Blood", typeof(RectTransform));
        imageObject.transform.SetParent(canvasObject.transform, false);

        var rect = (RectTransform)imageObject.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        material = new Material(settings.shader);
        image = imageObject.AddComponent<RawImage>();
        image.material = material;
        image.raycastTarget = false;
        image.enabled = false;
    }

    private void OnEnable()
    {
        health.OnDamaged += OnDamaged;

        if (combat != null)
        {
            combat.OnDied += OnDied;
        }
    }

    private void OnDisable()
    {
        health.OnDamaged -= OnDamaged;

        if (combat != null)
        {
            combat.OnDied -= OnDied;
        }
    }

    private void OnDamaged(float amount)
    {
        pulse = Mathf.Max(pulse, amount / health.Max * settings.hitPulseStrength);
    }

    private void OnDied()
    {
        dead = true;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float fraction = previewHealth >= 0f ? Mathf.Clamp01(previewHealth) : health.Fraction;

        float target = settings.coverageCurve.Evaluate(1f - fraction);
        shownCoverage = Mathf.MoveTowards(shownCoverage, target, settings.creepSpeed * dt);
        pulse = Mathf.MoveTowards(pulse, 0f, settings.hitPulseDecay * dt);

        float coverage = shownCoverage + pulse + Heartbeat(fraction, dt);

        // Past 1 so the noisy edge closes over the centre too.
        if (dead == true)
        {
            deathFade = Mathf.MoveTowards(deathFade, 1f, dt / settings.deathFadeTime);
            coverage = Mathf.Lerp(coverage, 1.3f, deathFade);
        }

        Coverage = Mathf.Clamp(coverage, 0f, 1.5f);
        Apply();
    }

    // A double thump that speeds up and deepens as health runs out.
    private float Heartbeat(float fraction, float dt)
    {
        if (fraction >= settings.heartbeatThreshold || settings.heartbeatThreshold <= 0f || dead == true)
        {
            heartbeatPhase = 0f;
            return 0f;
        }

        float severity = 1f - fraction / settings.heartbeatThreshold;
        heartbeatPhase = Mathf.Repeat(heartbeatPhase + settings.heartbeatRate * (1f + severity) * dt, 1f);

        float thump = Mathf.Exp(-Mathf.Pow((heartbeatPhase - 0.1f) * 14f, 2f))
            + 0.6f * Mathf.Exp(-Mathf.Pow((heartbeatPhase - 0.3f) * 14f, 2f));

        return thump * settings.heartbeatStrength * (0.5f + 0.5f * severity);
    }

    private void Apply()
    {
        image.enabled = Coverage > 0.001f;
        if (image.enabled == false)
        {
            return;
        }

        float drift = Time.unscaledTime * settings.noiseDrift * settings.noiseScale;

        material.SetColor(ColorId, settings.bloodColor);
        material.SetColor(DarkColorId, settings.deepColor);
        material.SetFloat(OpacityId, settings.opacity);
        material.SetFloat(CoverageId, Coverage);
        material.SetFloat(EdgeSoftnessId, settings.edgeSoftness);
        material.SetFloat(DepthId, settings.deepColorDepth);
        material.SetFloat(NoiseScaleId, settings.noiseScale);
        material.SetFloat(NoiseStrengthId, settings.noiseStrength);
        material.SetVector(NoiseOffsetId, new Vector4(drift, drift * 0.6f, 0f, 0f));
        material.SetFloat(ShadowWidthId, settings.shadowWidth);
        material.SetFloat(ShadowStrengthId, settings.shadowStrength);
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }

        if (material != null)
        {
            Destroy(material);
        }
    }
}
