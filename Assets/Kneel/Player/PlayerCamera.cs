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
        Vector3 position = focusPoint - rotation * Vector3.forward * distance;

        // A scripted shot (the L5 reveal, the Master's intro and transition) eases in over the follow and back out.
        overrideWeight = Mathf.MoveTowards(overrideWeight, overrideActive ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(overrideBlend, 0.01f));
        float lens = 0f;
        if (overrideWeight > 0f)
        {
            float t = Mathf.SmoothStep(0f, 1f, overrideWeight);
            position = Vector3.Lerp(position, overridePosition, t);
            rotation = Quaternion.Slerp(rotation, overrideRotation, t);
            lens = t;
        }

        // The shot's lens (a long one frames a far boss with the player in the foreground), back to the follow's own.
        if (lensCamera == null)
        {
            lensCamera = GetComponent<Camera>();
            baseFieldOfView = lensCamera != null ? lensCamera.fieldOfView : 60f;
        }

        if (lensCamera != null)
            lensCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, overrideFieldOfView > 0f ? overrideFieldOfView : baseFieldOfView, lens);

        transform.SetPositionAndRotation(position + GetShakeOffset(), rotation);
    }

    private bool overrideActive;
    private float overrideWeight;
    private float overrideBlend = 1f;
    private Vector3 overridePosition;
    private Quaternion overrideRotation = Quaternion.identity;
    private float overrideFieldOfView;
    private Camera lensCamera;
    private float baseFieldOfView;

    // True while a scripted shot holds the camera (or is still easing back out of it).
    public bool HasOverride => overrideWeight > 0f;

    // Eases the camera to a fixed pose over `blendTime` seconds and holds it there until ClearOverride. Calling it again
    // while one is held moves the held pose (the reveal's push). fieldOfView > 0 also changes the lens.
    public void SetOverride(Vector3 position, Quaternion rotation, float blendTime, float fieldOfView = 0f)
    {
        overridePosition = position;
        overrideRotation = rotation;
        overrideFieldOfView = fieldOfView;
        overrideBlend = blendTime;
        overrideActive = true;
    }

    // Eases back to the follow camera over `blendTime` seconds.
    public void ClearOverride(float blendTime)
    {
        overrideBlend = blendTime;
        overrideActive = false;
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
