using UnityEngine;

// Tuning values for PlayerCape. Kept in an asset so edits made during Play mode are saved.
[CreateAssetMenu(fileName = "PlayerCapeSettings", menuName = "Kneel/Player Cape Settings")]
public class PlayerCapeSettings : ScriptableObject
{
    [Header("Motion Info")]
    public float gravityScale = 1f;

    // How quickly the cape's swing relative to the body dies out, per second.
    public float swayDamping = 9f;

    // Drag against the air, per second. Makes the cape stream back while moving. Lower reads as heavier cloth.
    public float airDrag = 0.5f;

    // Extra damping, per second, where the cape rubs on the body.
    public float contactFriction = 10f;

    [Header("Shape Info")]
    // Spring pulling the cape toward its modelled drape, as acceleration per metre away from it (1/s²).
    // The shoulders use shoulderStiffness, blending down to hemStiffness at the hem.
    // A weak hem spring lets gravity hang the cape straight; a strong one holds the modelled flare.
    public float shoulderStiffness = 400f;

    public float hemStiffness = 30f;

    // How far neighbouring chains may spread apart beyond their rest spacing, as a fraction of it.
    [Range(0f, 1f)]
    public float spreadSlack = 0.15f;

    [Header("Collision Info")]
    // Thickness of the cape against the body capsules.
    public float clothRadius = 0.04f;

    private void OnValidate()
    {
        swayDamping = Mathf.Max(0f, swayDamping);
        airDrag = Mathf.Max(0f, airDrag);
        contactFriction = Mathf.Max(0f, contactFriction);
        shoulderStiffness = Mathf.Max(0f, shoulderStiffness);
        hemStiffness = Mathf.Max(0f, hemStiffness);
        clothRadius = Mathf.Max(0f, clothRadius);
    }
}
