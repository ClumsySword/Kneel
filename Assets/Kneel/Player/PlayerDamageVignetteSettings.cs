using UnityEngine;

// Tuning values for PlayerDamageVignette. Kept in an asset so edits made during Play mode are saved.
[CreateAssetMenu(fileName = "PlayerDamageVignetteSettings", menuName = "Kneel/Player Damage Vignette Settings")]
public class PlayerDamageVignetteSettings : ScriptableObject
{
    // Referenced here (not found by name) so builds include it.
    public Shader shader;

    [Header("Look Info")]
    public Color bloodColor = new Color(0.32f, 0.01f, 0.01f);

    // Colour the blood darkens to the further it has soaked in.
    public Color deepColor = new Color(0.07f, 0f, 0.003f);

    [Range(0f, 1f)]
    public float opacity = 0.85f;

    [Range(0.001f, 0.5f)]
    public float edgeSoftness = 0.025f;

    // How far in (in coverage units) the blood takes to reach the deep colour.
    [Range(0.01f, 1f)]
    public float deepColorDepth = 0.3f;

    public float noiseScale = 4f;

    // How ragged the edge is.
    [Range(0f, 1f)]
    public float noiseStrength = 0.35f;

    // Screen widths per second the noise crawls by.
    public float noiseDrift = 0.03f;

    // Darkening that runs ahead of the blood's edge, so it soaks into the image instead of cutting a hole.
    [Range(0f, 0.5f)]
    public float shadowWidth = 0.12f;

    [Range(0f, 1f)]
    public float shadowStrength = 0.4f;

    [Header("Health Info")]
    // Missing health (0 = full, 1 = dead) to coverage (0 = nothing, 1 = reaches the centre).
    public AnimationCurve coverageCurve = new AnimationCurve(new Keyframe(0f, 0f, 0.35f, 0.35f), new Keyframe(1f, 0.6f, 1f, 1f));

    // Coverage per second the blood creeps toward its target by.
    public float creepSpeed = 0.4f;

    [Header("Hit Pulse Info")]
    // Extra coverage on a hit, per fraction of max health lost.
    public float hitPulseStrength = 1f;

    // Coverage per second the pulse falls off by.
    public float hitPulseDecay = 0.8f;

    [Header("Heartbeat Info")]
    // Health fraction below which the blood throbs.
    [Range(0f, 1f)]
    public float heartbeatThreshold = 0.3f;

    // Beats per second at the threshold. Rises toward double as health runs out.
    public float heartbeatRate = 1.1f;

    // Extra coverage at the peak of a beat.
    public float heartbeatStrength = 0.06f;

    [Header("Death Info")]
    // Seconds for the blood to cover the screen after dying.
    public float deathFadeTime = 1.2f;

    private void OnValidate()
    {
        noiseScale = Mathf.Max(0.01f, noiseScale);
        creepSpeed = Mathf.Max(0.01f, creepSpeed);
        hitPulseStrength = Mathf.Max(0f, hitPulseStrength);
        hitPulseDecay = Mathf.Max(0.01f, hitPulseDecay);
        heartbeatRate = Mathf.Max(0f, heartbeatRate);
        heartbeatStrength = Mathf.Max(0f, heartbeatStrength);
        deathFadeTime = Mathf.Max(0.01f, deathFadeTime);
    }
}
