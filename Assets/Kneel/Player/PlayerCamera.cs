using UnityEngine;

// Angled top-down camera that follows the player, zooms with the scroll wheel
// and pans toward the pointer when it is pushed against the screen edges.
// Tuning values live in a PlayerCameraSettings asset.
[RequireComponent(typeof(Camera))]
public class PlayerCamera : MonoBehaviour
{
    private PlayerControls controls;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private PlayerCameraSettings settings;

    // Turned off by the debug overlay so reaching for its panel doesn't pan the view.
    [HideInInspector]
    public bool edgeLookEnabled = true;

    private float distance;
    private float targetDistance;
    private float zoomVelocity;

    private Vector3 focusPoint;
    private Vector3 focusVelocity;

    private Vector3 edgeOffset;
    private Vector3 edgeOffsetVelocity;

    private float shakeStrength = 0f;
    private float shakeDuration = 0f;
    private float shakeTimeLeft = 0f;

    public PlayerCameraSettings Settings => settings;

    // The zoom distance the camera is easing toward.
    public float CurrentDistance
    {
        get => targetDistance;
        set => targetDistance = Mathf.Clamp(value, settings.minDistance, settings.maxDistance);
    }

    private void Awake()
    {
        controls = new PlayerControls();

        if (settings == null)
        {
            Debug.LogWarning("PlayerCamera has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerCameraSettings>();
        }
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

        targetDistance = Mathf.Clamp(settings.startDistance, settings.minDistance, settings.maxDistance);
        distance = targetDistance;

        // Snap into place on the first frame instead of swooping in.
        if (target != null)
        {
            focusPoint = target.position + Vector3.up * settings.targetHeight;
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

        Vector3 desiredFocus = target.position + Vector3.up * settings.targetHeight;
        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, settings.followSmoothTime);

        ApplyTransform();
    }

    private void UpdateZoom()
    {
        // Input System's scrollDeltaBehavior (UniformAcrossAllPlatforms) makes one notch = 1 on every platform.
        float scroll = controls.Character.Zoom.ReadValue<float>();

        // Clamped every frame so min/max edits made while playing apply immediately.
        targetDistance = Mathf.Clamp(targetDistance - scroll * settings.zoomStep, settings.minDistance, settings.maxDistance);
        distance = Mathf.SmoothDamp(distance, targetDistance, ref zoomVelocity, settings.zoomSmoothTime);
    }

    private void UpdateEdgeOffset()
    {
        Vector2 edgePush = edgeLookEnabled ? GetEdgePush(controls.Character.Aim.ReadValue<Vector2>()) : Vector2.zero;

        // Map the screen direction onto the ground plane using only the camera's yaw.
        Quaternion flatRotation = Quaternion.Euler(0f, settings.yaw, 0f);
        Vector3 right = flatRotation * Vector3.right;
        Vector3 forward = flatRotation * Vector3.forward;

        float offsetDistance = settings.maxEdgeOffset * (distance / settings.maxDistance);
        Vector3 desiredOffset = (right * edgePush.x + forward * edgePush.y) * offsetDistance;
        edgeOffset = Vector3.SmoothDamp(edgeOffset, desiredOffset, ref edgeOffsetVelocity, settings.edgeSmoothTime);
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

        float edgeStart = 1f - settings.edgeSize * 2f;
        Vector2 push = new Vector2(
            Mathf.Sign(x) * Mathf.InverseLerp(edgeStart, 1f, Mathf.Abs(x)),
            Mathf.Sign(y) * Mathf.InverseLerp(edgeStart, 1f, Mathf.Abs(y)));

        return Vector2.ClampMagnitude(push, 1f);
    }

    // Jolts the camera for impacts. Stronger calls override weaker ones still playing.
    public void AddShake(float strength, float duration)
    {
        float remaining = shakeDuration > 0f ? shakeStrength * (shakeTimeLeft / shakeDuration) : 0f;
        if (strength < remaining)
        {
            return;
        }

        shakeStrength = strength;
        shakeDuration = Mathf.Max(duration, 0.01f);
        shakeTimeLeft = shakeDuration;
    }

    private Vector3 GetShakeOffset()
    {
        if (shakeTimeLeft <= 0f)
        {
            return Vector3.zero;
        }

        // Unscaled so the shake still plays during hit-stop.
        shakeTimeLeft -= Time.unscaledDeltaTime;
        float falloff = Mathf.Clamp01(shakeTimeLeft / shakeDuration);
        return Random.insideUnitSphere * (shakeStrength * falloff * falloff);
    }

    private void ApplyTransform()
    {
        Quaternion rotation = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
        Vector3 lookPoint = focusPoint + edgeOffset;

        transform.SetPositionAndRotation(lookPoint - rotation * Vector3.forward * distance + GetShakeOffset(), rotation);
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
