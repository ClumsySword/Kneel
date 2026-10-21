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

    // Scales all the walk speeds above. The walk clips play faster by the same amount, so the feet stay planted.
    public float walkSpeedMultiplier = 1.2f;

    // How fast the character reaches walk speed, and how fast they plant when input stops (m/s²).
    public float acceleration = 16f;

    public float deceleration = 22f;

    // Degrees per second the character turns toward the aim point.
    public float aimTurnRate = 540f;

    [Header("Sprint Info")]
    // Travel speed of the sprint-forwards clip on the Knight.
    public float sprintSpeed = 3.97f;

    // Scales sprintSpeed. The sprint clip plays faster by the same amount, so the feet stay planted.
    public float sprintSpeedMultiplier = 1.15f;

    public float sprintAcceleration = 9f;

    public float sprintTurnRate = 540f;

    // Degrees per second the sprint's travel direction swings round to follow the body. High values keep
    // the character running where they face; lower ones let momentum carry them wide through turns.
    public float sprintGrip = 1080f;

    [Header("Air Info")]
    public float gravity = 20f;

    public float jumpHeight = 0.9f;

    // Fraction of ground acceleration available while airborne.
    [Range(0f, 1f)]
    public float airControl = 0.3f;
}
