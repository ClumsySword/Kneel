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

        // Boss framing (L5, The Master.md §10): the focus leans toward the boss and the camera backs off just enough
        // to keep him in frame, never so far that Kael gets smaller than framingMinPlayerPixels.
        framingWeight = Mathf.MoveTowards(framingWeight, framingSubject != null ? 1f : 0f, Time.unscaledDeltaTime / 0.8f);
        float framedDistance = distance;
        if (framingWeight > 0f && lastFramingSubject != null)
        {
            // Across the screen the frame leans half as far as along it: a camera off to Kael's side looks at the floor
            // beside him past the pillars (the mouse ray hits a pillar's top instead of the winch at its foot).
            Vector3 bossAt = lastFramingSubject.position + Vector3.up * settings.targetHeight;
            Vector3 across = Quaternion.Euler(0f, settings.yaw, 0f) * Vector3.right;
            Vector3 toBoss = bossAt - desiredFocus;
            Vector3 boss = desiredFocus + toBoss - across * Vector3.Dot(toBoss, across) * (1f - framingAcross);
            // 65% on Kael by preference; when that can't hold them both within the cap, the lean changes a step at a
            // time, less toward the boss first, then more. If nothing holds them both, the frame keeps the floor round
            // Kael (he has to be able to aim at it) and leans as far toward the boss as that allows.
            Vector3 framedFocus = Vector3.Lerp(desiredFocus, boss, framingBias);
            float framed = FramingDistance(framedFocus, target.position, lastFramingSubject.position, out bool fits);
            for (int i = 0; fits == false && i < LeanSteps.Length; i++)
            {
                Vector3 focus = Vector3.Lerp(desiredFocus, boss, Mathf.Clamp(framingBias + LeanSteps[i], 0.05f, 0.5f));
                float d = FramingDistance(focus, target.position, lastFramingSubject.position, out fits);
                if (fits)
                {
                    framedFocus = focus;
                    framed = d;
                }
            }

            if (fits == false)
            {
                float cap = Mathf.Max(distance, framingMax);
                Quaternion view = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
                for (float bias = framingBias; bias >= -0.001f; bias -= 0.05f)
                {
                    framedFocus = Vector3.Lerp(desiredFocus, boss, Mathf.Max(0f, bias));
                    framed = cap;
                    if (KaelRoomFits(view, framedFocus, cap, target.position))
                        break;
                }
            }

            desiredFocus = Vector3.Lerp(desiredFocus, framedFocus, framingWeight);
            framedDistance = Mathf.Lerp(distance, framed, framingWeight);
        }

        framingDistance = Mathf.SmoothDamp(framingDistance <= 0f ? framedDistance : framingDistance, framedDistance, ref framingDistanceVelocity, 0.45f);
        focusPoint = Vector3.SmoothDamp(focusPoint, desiredFocus, ref focusVelocity, settings.followSmoothTime);

        ApplyTransform();
    }

    // --- Boss framing ---

    private static readonly float[] LeanSteps = { -0.05f, -0.1f, 0.05f, -0.15f, 0.1f, -0.2f, 0.15f, -0.25f };

    private Transform framingSubject;
    private Transform lastFramingSubject;
    private float framingMax = 13f;
    private float framingWeight;
    private float framingDistance;
    private float framingDistanceVelocity;

    // 0 frames Kael alone, 1 the boss alone: 0.35 keeps the frame 65% on the player.
    public float framingBias = 0.35f;
    public float framingSubjectHeight = 5f;
    public float framingMinPlayerPixels = 60f;

    public Transform FramingSubject => framingSubject;

    // The distance the camera is at now, framing included (for the playtest).
    public float FramedDistance => framingWeight > 0f ? framingDistance : distance;

    // Frames `subject` with the player, the camera allowed out to `maxDistance`.
    public void SetFraming(Transform subject, float maxDistance)
    {
        framingSubject = subject;
        lastFramingSubject = subject;
        framingMax = maxDistance;
    }

    public void ClearFraming() => framingSubject = null;

    // The nearest distance (from the player's own zoom up to framingMax) at which the boss, head to foot, and Kael are
    // both inside the frame's middle 84%; capped where Kael would be shorter than framingMinPlayerPixels on screen.
    private float FramingDistance(Vector3 focus, Vector3 player, Vector3 boss, out bool fits)
    {
        fits = true;
        if (lensCamera == null)
        {
            lensCamera = GetComponent<Camera>();
            baseFieldOfView = lensCamera != null ? lensCamera.fieldOfView : 60f;
        }

        Quaternion rotation = Quaternion.Euler(settings.pitch, settings.yaw, 0f);
        float tanV = Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = lensCamera != null ? lensCamera.aspect : 16f / 9f;
        float tanH = tanV * aspect;
        float screenHeight = lensCamera != null ? lensCamera.pixelHeight : 1080f;
        float lower = Mathf.Max(distance, settings.minDistance);
        float upper = Mathf.Max(lower, framingMax);

        // Kael's height on screen caps how far out it can go.
        for (float d = upper; d > lower; d -= 0.25f)
        {
            if (PixelHeight(rotation, focus, d, player, tanV, screenHeight) >= framingMinPlayerPixels)
                break;
            upper = d - 0.25f;
        }

        upper = Mathf.Max(lower, upper);
        for (float d = lower; d <= upper; d += 0.25f)
        {
            if (Inside(rotation, focus, d, boss, tanV, tanH) && Inside(rotation, focus, d, boss + Vector3.up * framingSubjectHeight, tanV, tanH)
                && KaelRoomFits(rotation, focus, d, player))
                return d;
        }

        fits = false;
        return upper;
    }

    // The floor this far round Kael always stays in the frame: the mouse aims on it (a winch beside him, a roll away
    // from the boss), and a frame leaning toward the boss mustn't push it off the edge.
    public float framingPlayerRoom = 3f;

    // How far the frame leans across the screen, relative to along it.
    public float framingAcross = 0.5f;

    private bool KaelRoomFits(Quaternion rotation, Vector3 focus, float d, Vector3 player)
    {
        float tanV = Mathf.Tan(baseFieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (lensCamera != null ? lensCamera.aspect : 16f / 9f);
        float r = framingPlayerRoom;
        return Inside(rotation, focus, d, player + Vector3.up * 1.8f, tanV, tanH)
            && Inside(rotation, focus, d, player + Vector3.left * r, tanV, tanH, 0.92f) && Inside(rotation, focus, d, player + Vector3.right * r, tanV, tanH, 0.92f)
            && Inside(rotation, focus, d, player + Vector3.forward * r, tanV, tanH, 0.92f) && Inside(rotation, focus, d, player + Vector3.back * r, tanV, tanH, 0.92f);
    }

    // Whether a point sits inside the frame's middle `edge` (-1 to 1 being its edges).
    private static bool Inside(Quaternion rotation, Vector3 focus, float d, Vector3 point, float tanV, float tanH, float edge = 0.84f)
    {
        Vector3 local = Quaternion.Inverse(rotation) * (point - (focus - rotation * Vector3.forward * d));
        if (local.z <= 0.1f)
            return false;
        return Mathf.Abs(local.x / (local.z * tanH)) <= edge && Mathf.Abs(local.y / (local.z * tanV)) <= edge;
    }

    // How big Kael looks: the larger side of the screen box round his body (0.9 m across, 1.8 m tall). Not a line
    // from feet to head: near the bottom of a framed shot the camera sees him from almost straight above, and his
    // height shrinks to nothing while his shoulders and cape still read.
    private static float PixelHeight(Quaternion rotation, Vector3 focus, float d, Vector3 feet, float tanV, float screenHeight)
    {
        Vector3 eye = focus - rotation * Vector3.forward * d;
        Quaternion inverse = Quaternion.Inverse(rotation);
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = feet + new Vector3((i & 1) == 0 ? -0.45f : 0.45f, (i & 2) == 0 ? 0f : 1.8f, (i & 4) == 0 ? -0.45f : 0.45f);
            Vector3 local = inverse * (corner - eye);
            float x = local.x / (local.z * tanV), y = local.y / (local.z * tanV);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }

        return Mathf.Max(maxX - minX, maxY - minY) * 0.5f * screenHeight;
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
        float followDistance = framingWeight > 0f ? Mathf.Max(distance, framingDistance) : distance;
        Vector3 position = focusPoint - rotation * Vector3.forward * followDistance;

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
