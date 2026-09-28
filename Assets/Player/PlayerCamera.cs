using UnityEngine;

// Angled top-down camera that follows the player, zooms with the scroll wheel
// and pans toward the pointer when it is pushed against the screen edges.
[RequireComponent(typeof(Camera))]
public class PlayerCamera : MonoBehaviour
{
    private PlayerControls controls;

    [Header("Target Info")]
    [SerializeField]
    private Transform target;

    // Point on the player the camera looks at, relative to their feet.
    [SerializeField]
    private float targetHeight = 1f;

    [SerializeField]
    private float followSmoothTime = 0.1f;

    [Header("Angle Info")]
    [SerializeField]
    [Range(30f, 90f)]
    private float pitch = 65f;

    [SerializeField]
    private float yaw = 0f;

    [Header("Zoom Info")]
    [SerializeField]
    private float minDistance = 8f;

    [SerializeField]
    private float maxDistance = 25f;

    [SerializeField]
    private float startDistance = 15f;

    // Distance changed per scroll notch.
    [SerializeField]
    private float zoomStep = 2f;

    [SerializeField]
    private float zoomSmoothTime = 0.1f;

    [Header("Edge Look Info")]
    // Width of the edge band as a fraction of the screen (0.1 = outer 10% on each side).
    [SerializeField]
    [Range(0.01f, 0.5f)]
    private float edgeSize = 0.1f;

    // How far (world units) the camera can pan away from the player at full edge push and max zoom.
    // Scaled down with zoom so the player stays on screen when zoomed in.
    [SerializeField]
    private float maxEdgeOffset = 10f;

    [SerializeField]
    private float edgeSmoothTime = 0.3f;

    private float distance;
    private float targetDistance;
    private float zoomVelocity;

    private Vector3 focusPoint;
    private Vector3 focusVelocity;

    private Vector3 edgeOffset;
    private Vector3 edgeOffsetVelocity;

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void Start()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerMovement>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
        distance = targetDistance;

        // Snap into place on the first frame instead of swooping in.
        if (target != null)
        {
            focusPoint = target.position + Vector3.up * targetHeight;
            ApplyTransform();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateZoom();
        UpdateEdgeOffset();

        Vector3 desiredFocus = target.position + Vector3.up * targetHeight;
        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, followSmoothTime);

        ApplyTransform();
    }

    private void UpdateZoom()
    {
        // Input System's scrollDeltaBehavior (UniformAcrossAllPlatforms) makes one notch = 1 on every platform.
        float scroll = controls.Character.Zoom.ReadValue<float>();

        targetDistance = Mathf.Clamp(targetDistance - scroll * zoomStep, minDistance, maxDistance);
        distance = Mathf.SmoothDamp(distance, targetDistance, ref zoomVelocity, zoomSmoothTime);
    }

    private void UpdateEdgeOffset()
    {
        Vector2 edgePush = GetEdgePush(controls.Character.Aim.ReadValue<Vector2>());

        // Map the screen direction onto the ground plane using only the camera's yaw.
        Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 right = flatRotation * Vector3.right;
        Vector3 forward = flatRotation * Vector3.forward;

        float offsetDistance = maxEdgeOffset * (distance / maxDistance);
        Vector3 desiredOffset = (right * edgePush.x + forward * edgePush.y) * offsetDistance;
        edgeOffset = Vector3.SmoothDamp(edgeOffset, desiredOffset, ref edgeOffsetVelocity, edgeSmoothTime);
    }

    // Returns how hard the pointer is pushed into each screen edge, from -1 to 1 per axis.
    private Vector2 GetEdgePush(Vector2 pointer)
    {
        bool pointerOnScreen = pointer.x >= 0f && pointer.y >= 0f && pointer.x <= Screen.width && pointer.y <= Screen.height;
        if (Application.isFocused == false || pointerOnScreen == false)
        {
            return Vector2.zero;
        }

        // -1..1 from screen centre to each edge.
        float x = pointer.x / Screen.width * 2f - 1f;
        float y = pointer.y / Screen.height * 2f - 1f;

        float edgeStart = 1f - edgeSize * 2f;
        Vector2 push = new Vector2(
            Mathf.Sign(x) * Mathf.InverseLerp(edgeStart, 1f, Mathf.Abs(x)),
            Mathf.Sign(y) * Mathf.InverseLerp(edgeStart, 1f, Mathf.Abs(y)));

        return Vector2.ClampMagnitude(push, 1f);
    }

    private void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookPoint = focusPoint + edgeOffset;

        transform.SetPositionAndRotation(lookPoint - rotation * Vector3.forward * distance, rotation);
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(maxDistance, minDistance);
        startDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
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
