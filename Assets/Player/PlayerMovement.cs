using UnityEngine;

// Weighty top-down locomotion: the character accelerates, plants and turns at capped rates.
// Walking keeps them facing the aim point; sprinting turns them toward where they run and drains stamina.
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

    private float stamina;
    private float staminaRegenTimer = 0f;

    public PlayerMovementSettings Settings => settings;

    public bool IsSprinting { get; private set; }

    // True after stamina runs dry, until it refills to settings.exhaustedRecoverFraction.
    public bool IsExhausted { get; private set; }

    public float Stamina01 => settings.maxStamina > 0f ? stamina / settings.maxStamina : 0f;

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
    [HideInInspector]
    public Vector3? forcedVelocity = null;

    // Collision result of the last CharacterController.Move.
    public CollisionFlags LastCollisionFlags { get; private set; }

    // Horizontal displacement actually achieved by the last move, after collisions.
    public Vector3 LastMoveDelta { get; private set; }

    public float Stamina => stamina;

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

        stamina = settings.maxStamina;
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
        UpdateStamina();
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
        IsSprinting = sprintHeld && hasMoveInput && IsExhausted == false && stamina > 0f && sprintBlocked == false;
    }

    // Spends stamina for an action (attack, blocked hit). Returns false without spending if exhausted or empty.
    // An action may overdraw what's left; hitting zero exhausts the player like sprinting does.
    public bool TrySpendStamina(float amount)
    {
        if (IsExhausted == true || stamina <= 0f)
        {
            return false;
        }

        DrainStamina(amount);
        return true;
    }

    // Unconditionally removes stamina (e.g. absorbing a hit on the shield). Returns true if stamina ran out.
    public bool DrainStamina(float amount)
    {
        stamina -= amount;
        staminaRegenTimer = settings.staminaRegenDelay;

        if (stamina <= 0f)
        {
            stamina = 0f;
            IsExhausted = true;
            return true;
        }

        return false;
    }

    // Adds a burst of horizontal velocity (e.g. an attack lunge) that decays with deceleration.
    public void AddImpulse(Vector3 velocity)
    {
        velocity.y = 0f;
        planarVelocity += velocity;
    }

    private void UpdateStamina()
    {
        if (IsSprinting == true)
        {
            stamina -= settings.sprintStaminaDrain * Time.deltaTime;
            staminaRegenTimer = settings.staminaRegenDelay;

            if (stamina <= 0f)
            {
                stamina = 0f;
                IsExhausted = true;
                IsSprinting = false;
            }

            return;
        }

        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
            return;
        }

        stamina = Mathf.Min(stamina + settings.staminaRegen * Time.deltaTime, settings.maxStamina);

        if (IsExhausted == true && Stamina01 >= settings.exhaustedRecoverFraction)
        {
            IsExhausted = false;
        }
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

        Vector3 desiredVelocity;
        float speedUpRate;

        if (IsSprinting == true)
        {
            // Momentum: you run where you face, and hard direction changes bleed speed until you've turned.
            float alignment = Mathf.Max(0f, Vector3.Dot(transform.forward, movementDirection.normalized));
            desiredVelocity = transform.forward * (settings.sprintSpeed * alignment * movementDirection.magnitude);

            // Get going at walk responsiveness, then build the rest of the sprint slowly.
            bool belowWalkSpeed = planarVelocity.magnitude < settings.walkSpeedForward;
            speedUpRate = belowWalkSpeed ? settings.acceleration : settings.sprintAcceleration;
        }
        else
        {
            desiredVelocity = movementDirection * GetWalkSpeed(movementDirection);
            speedUpRate = settings.acceleration;
        }

        desiredVelocity *= moveSpeedMultiplier;

        // Speed up gradually, but plant quickly so the controls stay responsive.
        float rate = desiredVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? speedUpRate : settings.deceleration;

        if (characterController.isGrounded == false)
        {
            rate *= settings.airControl;
        }

        planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, rate * Time.deltaTime);
    }

    // Walk speed for a world direction, matched to the walk clip for that direction relative to facing.
    private float GetWalkSpeed(Vector3 worldDirection)
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
            verticalVelocity -= settings.gravity * Time.deltaTime;
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

        // Don't keep building speed while pushing into a wall.
        if (planarVelocity.sqrMagnitude > actualVelocity.sqrMagnitude + 0.01f && characterController.isGrounded == true)
        {
            planarVelocity = Vector3.ClampMagnitude(planarVelocity, Mathf.Max(actualVelocity.magnitude, 0.1f));
        }
    }

    private void AnimatorControllers()
    {
        // The blend tree is laid out in m/s, so feed it the real local velocity and the feet match the ground.
        Vector3 actualVelocity = characterController.velocity;
        actualVelocity.y = 0f;
        Vector3 localVelocity = transform.InverseTransformDirection(actualVelocity);

        animator.SetFloat("xVelocity", localVelocity.x, .1f, Time.deltaTime);
        animator.SetFloat("zVelocity", localVelocity.z, .1f, Time.deltaTime);
        animator.SetBool("IsGrounded", characterController.isGrounded);
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
