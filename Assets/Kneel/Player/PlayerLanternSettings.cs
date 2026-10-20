using UnityEngine;

// Tuning values for PlayerLantern. Kept in an asset so edits made during Play mode are saved.
[CreateAssetMenu(fileName = "PlayerLanternSettings", menuName = "Kneel/Player Lantern Settings")]
public class PlayerLanternSettings : ScriptableObject
{
    [Header("Light Info")]
    public Color lightColor = new Color(1f, 0.62f, 0.3f);

    public float lightIntensity = 4f;

    public float lightRange = 8f;

    public bool castShadows = true;

    // Fraction of the intensity the flame wavers by.
    [Range(0f, 1f)]
    public float flickerAmount = 0.15f;

    public float flickerSpeed = 7f;

    // HDR multiplier on the light colour for the glowing glass.
    public float glassGlow = 2.5f;

    [Header("Sway Info")]
    // Hook to the lantern's centre of mass. Longer swings slower.
    public float pendulumLength = 0.26f;

    // How quickly a swing dies out, per second.
    public float swayDamping = 5f;

    // Furthest the lantern can swing away from hanging straight down.
    [Range(0f, 90f)]
    public float maxSwingAngle = 40f;

    private void OnValidate()
    {
        pendulumLength = Mathf.Max(0.01f, pendulumLength);
        swayDamping = Mathf.Max(0f, swayDamping);
    }
}
