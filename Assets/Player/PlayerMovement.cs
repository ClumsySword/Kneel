using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private PlayerControls controls;

    private CharacterController characterController;

    // SerializeField lets you see Private fields in the editor.
    [Header("Movement Info")]
    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float sprintMultiplier = 2f;

    [Header("Aim Info")]
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] private Transform aim;

    private Vector3 lookingDirection;


    private float speedMultiplier = 1f;
    private float verticalVelocity = 0f;

    private bool justJumped = false;

    private Vector2 moveInput;
    private Vector2 aimInput;

    public Vector3 movementDirection;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        controls = new PlayerControls();
        controls.Character.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Character.Movement.canceled += ctx => moveInput = Vector2.zero;

        controls.Character.Aim.performed += ctx => aimInput = ctx.ReadValue<Vector2>();
        controls.Character.Aim.canceled += ctx => aimInput = Vector2.zero;

        controls.Character.Sprint.performed += ctx => speedMultiplier = sprintMultiplier;
        controls.Character.Sprint.canceled  += ctx => speedMultiplier = 1f;

        controls.Character.Jump.performed += ctx => SafeJump();
    }

    private void SafeJump()
    {
        if(characterController.isGrounded == true)
        {
            justJumped = true;
        }
    }

    private void Update()
    {
        // Rebuild the direction every frame from the latest input.
        movementDirection = new Vector3(moveInput.x, 0f, moveInput.y);

        ApplyGravityVelocity();
        ApplyJumpVelocity();
        ApplyMovement();
        AimTowardsMouse();
    }

    private void AimTowardsMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(aimInput);

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, aimLayerMask))
        {
            lookingDirection = hitInfo.point - transform.position;
            lookingDirection.y = 0f; // Negates the y axis of aim
            lookingDirection.Normalize();

            transform.forward = lookingDirection;

            aim.position = new Vector3 (hitInfo.point.x, hitInfo.point.y, hitInfo.point.z);
        }
    }

    private void ApplyJumpVelocity()
    {
        if(justJumped == true)
        {
            verticalVelocity += 5f;
            justJumped = false;
        }
    }

    private void ApplyMovement()
    {
        // We times by delta time because update is called every frame. 
        // Delta time is the time between frames which kind of averages it out.
        Vector3 velocity = speedMultiplier * walkSpeed * movementDirection;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void ApplyGravityVelocity()
    {
        if (characterController.isGrounded == false)
        {
            verticalVelocity -= 9.81f * Time.deltaTime;
        }
        else
        {
            // Small downward bias keeps isGrounded reliable on slopes and steps.
            verticalVelocity = -0.5f;
        }
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
