using UnityEngine;

// Angled top-down camera that follows the player and zooms with the scroll wheel.
// Tuning values live in a PlayerCameraSettings asset.
[RequireComponent(typeof(Camera))]
public class PlayerCamera : MonoBehaviour
{
    private PlayerControls controls;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private PlayerCameraSettings settings;

    private float distance;
    private float targetDistance;
    private float zoomVelocity;

    private Vector3 focusPoint;
    private Vector3 focusVelocity;

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
        transform.SetPositionAndRotation(focusPoint - rotation * Vector3.forward * distance + GetShakeOffset(), rotation);
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
