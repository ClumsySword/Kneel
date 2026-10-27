using UnityEngine;

// The aim marker: a smouldering ember sitting where the mouse points in the world, in place of a crosshair.
// Its particles (core, glow, sparks) live on this object in the Player prefab; this script keeps it on the aim
// point, flickers its light, and hides the OS pointer so the ember is the only cursor. While the F1 overlay or the
// pause menu is open it hides itself instead and shows the pointer, which is the plain ring of MenuCursor.
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
    private Renderer[] renderers;
    private bool hidden;

    private void Awake()
    {
        if (movement == null)
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        seed = Random.value * 100f;
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    // After PlayerMovement has placed the aim point for this frame. The pointer's visibility is held every frame,
    // because alt-tabbing back shows the OS pointer again.
    private void LateUpdate()
    {
        bool inMenu = DebugTuningOverlay.IsOpen || Kneel.UI.PauseMenu.IsPaused;
        Cursor.visible = inMenu;
        SetHidden(inMenu);

        transform.SetPositionAndRotation(movement.AimPoint + Vector3.up * lift, Quaternion.identity);

        if (glow != null)
        {
            float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed) * 2f - 1f;
            glow.intensity = lightIntensity * (1f + flickerAmount * noise);
        }
    }

    private void SetHidden(bool hide)
    {
        if (hide == hidden)
        {
            return;
        }

        hidden = hide;

        foreach (Renderer r in renderers)
        {
            r.enabled = hide == false;
        }

        if (glow != null)
        {
            glow.enabled = hide == false;
        }
    }
}
