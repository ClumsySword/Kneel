using UnityEngine;

// Tuning values for PlayerMovement. Kept in an asset so edits made during Play mode are saved.
// Walk speeds match the travel speed of the walk clips so the feet don't slide.
[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Kneel/Player Movement Settings")]
public class PlayerMovementSettings : ScriptableObject
{
    // Walk speed by direction relative to facing, interpolated by angle. Defaults are the clips' travel
    // speeds on the Knight; strafing has no clip of its own and blends the two diagonals (~0.8 m/s).
    [Header("Walk Info")]
    public float walkSpeedForward = 1.44f;

    public float walkSpeedForwardDiagonal = 1.18f;

    public float walkSpeedStrafe = 0.8f;

    public float walkSpeedBackwardDiagonal = 1.04f;

    public float walkSpeedBackward = 1.09f;

    // How fast the character reaches walk speed, and how fast they plant when input stops (m/s²).
    public float acceleration = 8f;

    public float deceleration = 14f;

    // Degrees per second the character turns toward the aim point.
    public float aimTurnRate = 540f;

    [Header("Sprint Info")]
    // Travel speed of the sprint-forwards clip on the Knight.
    public float sprintSpeed = 3.97f;

    public float sprintAcceleration = 4f;

    public float sprintTurnRate = 300f;

    [Header("Air Info")]
    public float gravity = 20f;

    public float jumpHeight = 0.9f;

    // Fraction of ground acceleration available while airborne.
    [Range(0f, 1f)]
    public float airControl = 0.3f;

    [Header("Stamina Info")]
    public float maxStamina = 100f;

    public float sprintStaminaDrain = 25f;

    public float staminaRegen = 30f;

    // Seconds after sprinting stops before stamina starts regenerating.
    public float staminaRegenDelay = 0.8f;

    // After running dry, sprint stays locked until stamina refills to this fraction.
    [Range(0f, 1f)]
    public float exhaustedRecoverFraction = 0.3f;
}
