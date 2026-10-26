using UnityEngine;

// The aim marker: a smouldering ember sitting where the mouse points in the world, in place of a crosshair.
// Its particles (core, glow, sparks) live on this object in the Player prefab; this script keeps it on the aim
// point, flickers its light, and hides the OS pointer so the ember is the only cursor.
public class AimEmber : MonoBehaviour
{
    [SerializeField]
    private PlayerMovement movement;

    [SerializeField]
    private Light glow;

    // Lift off the surface so it doesn't clip into the ground.
    [SerializeField]
    private float lift = 0.04f;

    [SerializeField]
    private float lightIntensity = 0.3f;

    // Fraction of the intensity the light wavers by, and how fast.
    [SerializeField]
    private float flickerAmount = 0.35f;

    [SerializeField]
    private float flickerSpeed = 6f;

    private float seed;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        seed = Random.value * 100f;
    }

    private void OnEnable()
    {
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    // Alt-tabbing back shows the OS pointer again.
    private void OnApplicationFocus(bool focused)
    {
        if (focused && isActiveAndEnabled)
        {
            Cursor.visible = false;
        }
    }

    // After PlayerMovement has placed the aim point for this frame.
    private void LateUpdate()
    {
        transform.SetPositionAndRotation(movement.AimPoint + Vector3.up * lift, Quaternion.identity);

        if (glow != null)
        {
            float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f - 1f;
            glow.intensity = lightIntensity * (1f + flickerAmount * noise);
        }
    }
}
