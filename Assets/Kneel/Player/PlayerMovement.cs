using UnityEngine;

// Weighty top-down locomotion: the character accelerates, plants and turns at capped rates.
// Walking keeps them facing the aim point; sprinting turns them toward where they run.
// Tuning values live in a PlayerMovementSettings asset.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private PlayerControls controls;

    private CharacterController characterController;

    private Animator animator;

    [SerializeField]
    private PlayerMovementSettings settings;

    [Header("Aim Info")]
    [SerializeField]
    private LayerMask aimLayerMask;

    [SerializeField]
    private Transform aim;

    private Vector3 lookingDirection;
    private bool hasAimTarget = false;

    private Vector2 moveInput;
    private Vector2 aimInput;
    private bool sprintHeld = false;
    private bool justJumped = false;

    // Input direction in world space, magnitude 0..1.
    private Vector3 movementDirection;

    private Vector3 planarVelocity;
    private float verticalVelocity = 0f;

    public PlayerMovementSettings Settings => settings;

    public bool IsSprinting { get; private set; }

    // Actual horizontal speed after collisions, in m/s.
    public float CurrentSpeed { get; private set; }

    // Set every frame by PlayerCombat to restrict movement while attacking, blocking or stunned.
    [HideInInspector]
    public float moveSpeedMultiplier = 1f;

    [HideInInspector]
    public float turnRateMultiplier = 1f;

    [HideInInspector]
    public bool sprintBlocked = false;

    [HideInInspector]
    public bool rotationLocked = false;

    [HideInInspector]
    public bool jumpBlocked = false;

    // When set, replaces the planar velocity outright (no acceleration). Used for the dodge roll.
    [System.NonSerialized]
    public Vector3? forcedVelocity = null;

    // When set, replaces the vertical velocity while airborne instead of gravity. Used for the air attack's
    // hang and plunge.
    [System.NonSerialized]
    public float? forcedVerticalVelocity = null;

    public bool IsGrounded => characterController.isGrounded;

    // Where the mouse points on the ground (the aim dot).
    public Vector3 AimPoint => aim != null ? aim.position : transform.position + transform.forward;

    // Collision result of the last CharacterController.Move.
    public CollisionFlags LastCollisionFlags { get; private set; }

    // Horizontal displacement actually achieved by the last move, after collisions.
    public Vector3 LastMoveDelta { get; private set; }

    // Current steering input in world space, magnitude 0..1.
    public Vector3 MoveDirection => movementDirection;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Character.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Character.Movement.canceled += ctx => moveInput = Vector2.zero;

        controls.Character.Aim.performed += ctx => aimInput = ctx.ReadValue<Vector2>();
        controls.Character.Aim.canceled += ctx => aimInput = Vector2.zero;

        controls.Character.Sprint.performed += ctx => sprintHeld = true;
        controls.Character.Sprint.canceled += ctx => sprintHeld = false;

        controls.Character.Jump.performed += ctx => SafeJump();

        if (settings == null)
        {
            Debug.LogWarning("PlayerMovement has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
        }
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
    }

    private void SafeJump()
    {
        if (characterController.isGrounded == true && jumpBlocked == false)
        {
            justJumped = true;
            animator.SetTrigger("Jump");
        }
    }

    private void Update()
    {
        // Rebuild the direction every frame from the latest input, relative to the camera's facing.
        Quaternion cameraYaw = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        movementDirection = Vector3.ClampMagnitude(cameraYaw * new Vector3(moveInput.x, 0f, moveInput.y), 1f);

        AimTowardsMouse();
        UpdateSprintState();
        ApplyRotation();
        ApplyPlanarVelocity();
        ApplyGravityVelocity();
        ApplyJumpVelocity();
        ApplyMovement();
        AnimatorControllers();
    }

    private void AimTowardsMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(aimInput);

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, aimLayerMask))
        {
            lookingDirection = hitInfo.point - transform.position;
            lookingDirection.y = 0f; // Negates the y axis of aim
            hasAimTarget = lookingDirection.sqrMagnitude > 0.0001f;
            lookingDirection.Normalize();

            aim.position = hitInfo.point;
        }
    }

    private void UpdateSprintState()
    {
        bool hasMoveInput = movementDirection.sqrMagnitude > 0.01f;
        IsSprinting = sprintHeld && hasMoveInput && sprintBlocked == false;
    }

    // Adds a burst of horizontal velocity (e.g. an attack lunge) that decays with deceleration.
    public void AddImpulse(Vector3 velocity)
    {
        velocity.y = 0f;
        planarVelocity += velocity;
    }

    private void ApplyRotation()
    {
        // Sprinting faces where you run; otherwise face the aim point. Both turn at a capped rate for weight.
        Vector3 facingTarget;
        float turnRate;

        if (rotationLocked == true)
        {
            return;
        }

        if (IsSprinting == true)
        {
            facingTarget = movementDirection;
            turnRate = settings.sprintTurnRate;
        }
        else if (hasAimTarget == true)
        {
            facingTarget = lookingDirection;
            turnRate = settings.aimTurnRate;
        }
        else
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(facingTarget, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRate * turnRateMultiplier * Time.deltaTime);
    }

    private void ApplyPlanarVelocity()
    {
        if (forcedVelocity.HasValue == true)
        {
            planarVelocity = forcedVelocity.Value;
            return;
        }

        float control = characterController.isGrounded ? 1f : settings.airControl;

        if (IsSprinting == true)
        {
            ApplySprintVelocity(control);
            return;
        }

        Vector3 desiredVelocity = movementDirection * GetWalkSpeed(movementDirection) * moveSpeedMultiplier;

        // Speed up gradually, but plant quickly so the controls stay responsive.
        float rate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? settings.acceleration : settings.deceleration;
        planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, rate * control * Time.deltaTime);
    }

    // Speed and direction are steered separately so the run follows the body: speed builds with acceleration,
    // while the direction swings round to the facing at sprintGrip. Steering them as one vector made direction
    // changes as slow as speeding up, and the character slid sideways through every turn.
    private void ApplySprintVelocity(float control)
    {
        // Momentum: hard direction changes bleed speed until you've turned.
        float alignment = Mathf.Max(0f, Vector3.Dot(transform.forward, movementDirection.normalized));
        float targetSpeed = SprintSpeed * alignment * movementDirection.magnitude * moveSpeedMultiplier;
        float speed = planarVelocity.magnitude;

        // Get going at walk responsiveness, then build the rest of the sprint more slowly.
        float speedUpRate = speed < WalkSpeedForward ? settings.acceleration : settings.sprintAcceleration;
        float rate = targetSpeed > speed ? speedUpRate : settings.deceleration;
        float newSpeed = Mathf.MoveTowards(speed, targetSpeed, rate * control * Time.deltaTime);

        Vector3 direction = speed > 0.01f ? planarVelocity / speed : transform.forward;
        float maxTurn = settings.sprintGrip * control * Mathf.Deg2Rad * Time.deltaTime;
        direction = Vector3.RotateTowards(direction, transform.forward, maxTurn, 0f);

        // A near-reversal can rotate through the vertical; keep it on the ground plane.
        direction.y = 0f;
        planarVelocity = direction.normalized * newSpeed;
    }

    private float WalkSpeedForward => settings.walkSpeedForward * settings.walkSpeedMultiplier;

    private float SprintSpeed => settings.sprintSpeed * settings.sprintSpeedMultiplier;

    // Walk speed for a world direction: the walk clip's speed for that direction relative to facing, times walkSpeedMultiplier.
    private float GetWalkSpeed(Vector3 worldDirection)
    {
        return GetClipWalkSpeed(worldDirection) * settings.walkSpeedMultiplier;
    }

    private float GetClipWalkSpeed(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        // 0° = forward, 180° = backward; left and right are symmetric.
        Vector3 local = transform.InverseTransformDirection(worldDirection.normalized);
        float angle = Vector3.Angle(Vector3.forward, new Vector3(local.x, 0f, local.z));

        if (angle <= 45f)
        {
            return Mathf.Lerp(settings.walkSpeedForward, settings.walkSpeedForwardDiagonal, angle / 45f);
        }

        if (angle <= 90f)
        {
            return Mathf.Lerp(settings.walkSpeedForwardDiagonal, settings.walkSpeedStrafe, (angle - 45f) / 45f);
        }

        if (angle <= 135f)
        {
            return Mathf.Lerp(settings.walkSpeedStrafe, settings.walkSpeedBackwardDiagonal, (angle - 90f) / 45f);
        }

        return Mathf.Lerp(settings.walkSpeedBackwardDiagonal, settings.walkSpeedBackward, (angle - 135f) / 45f);
    }

    private void ApplyGravityVelocity()
    {
        if (characterController.isGrounded == false)
        {
            verticalVelocity = forcedVerticalVelocity ?? verticalVelocity - settings.gravity * Time.deltaTime;
        }
        else if (verticalVelocity < 0f)
        {
            // Small downward bias keeps isGrounded reliable on slopes and steps.
            verticalVelocity = -2f;
        }
    }

    private void ApplyJumpVelocity()
    {
        if (justJumped == true)
        {
            verticalVelocity = Mathf.Sqrt(2f * settings.gravity * settings.jumpHeight);
            justJumped = false;
        }
    }

    private void ApplyMovement()
    {
        // We times by delta time because update is called every frame.
        // Delta time is the time between frames which kind of averages it out.
        Vector3 velocity = planarVelocity;
        velocity.y = verticalVelocity;

        Vector3 positionBefore = transform.position;
        LastCollisionFlags = characterController.Move(velocity * Time.deltaTime);
        Vector3 moveDelta = transform.position - positionBefore;
        moveDelta.y = 0f;
        LastMoveDelta = moveDelta;

        Vector3 actualVelocity = characterController.velocity;
        actualVelocity.y = 0f;
        CurrentSpeed = actualVelocity.magnitude;

        // Don't keep building speed while pushing into a wall. Only an actual side hit counts: on open ground a step can
        // come out shorter than intended (tiny steps at high frame rates or during hit-stop), and clamping to that
        // would pin the player in place.
        bool hitWall = (LastCollisionFlags & CollisionFlags.Sides) != 0;
        if (hitWall == true && planarVelocity.sqrMagnitude > actualVelocity.sqrMagnitude + 0.01f && characterController.isGrounded == true)
        {
            planarVelocity = Vector3.ClampMagnitude(planarVelocity, Mathf.Max(actualVelocity.magnitude, 0.1f));
        }
    }

    private void AnimatorControllers()
    {
        // The blend tree is laid out in the clips' own speeds (m/s). Movement runs faster than the clips by the speed
        // multipliers, so the clips play faster by the same scale and the tree gets the velocity divided by it:
        // the feet match the ground at any multiplier.
        Vector3 actualVelocity = characterController.velocity;
        actualVelocity.y = 0f;
        Vector3 localVelocity = transform.InverseTransformDirection(actualVelocity);
        float scale = GetLocomotionScale(actualVelocity);

        animator.SetFloat("LocomotionSpeed", scale);
        animator.SetFloat("xVelocity", localVelocity.x / scale, .05f, Time.deltaTime);
        animator.SetFloat("zVelocity", localVelocity.z / scale, .05f, Time.deltaTime);
        animator.SetBool("IsGrounded", characterController.isGrounded);
    }

    // Playback scale for the locomotion clips: 1 at rest, the walk multiplier at full walk speed in whatever
    // direction the character is walking, rising to the sprint multiplier at full sprint speed.
    private float GetLocomotionScale(Vector3 velocity)
    {
        float walkScale = Mathf.Max(settings.walkSpeedMultiplier, 0.01f);
        float sprintScale = Mathf.Max(settings.sprintSpeedMultiplier, 0.01f);
        float speed = velocity.magnitude;

        if (speed <= WalkSpeedForward)
        {
            // Strafe and backward walks are slower than forward, so measure against this direction's full walk speed.
            float walkSpeed = Mathf.Max(GetWalkSpeed(velocity), 0.01f);
            return Mathf.Lerp(1f, walkScale, Mathf.Clamp01(speed / walkSpeed));
        }

        return Mathf.Lerp(walkScale, sprintScale, Mathf.InverseLerp(WalkSpeedForward, SprintSpeed, speed));
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }
}
