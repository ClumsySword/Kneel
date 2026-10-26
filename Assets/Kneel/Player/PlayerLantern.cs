using UnityEngine;

// Lantern hanging from the belt. This transform is the hook: it rides the hip socket, and the lantern
// swings below it as a damped pendulum, so it sways with the walk cycle and trails behind turns and stops.
// The lantern is kept out of capsules that follow the body's bones, so the legs and hips knock it aside
// instead of passing through it. Also flickers the light and the glowing glass.
// The light follows the lantern smoothed and held a little away from the body: a light swinging right
// against the hip blew out the thigh and swept hard self-shadows across the body with every swing.
// Tuning values live in a PlayerLanternSettings asset.
public class PlayerLantern : MonoBehaviour
{
    // A body part the lantern can't pass through: a capsule between two bones.
    [System.Serializable]
    public class BodyCapsule
    {
        public Transform start;
        public Transform end;
        public float radius = 0.1f;
    }

    // Part of the lantern's volume: a sphere this far down the hanging line from the hook.
    [System.Serializable]
    public class LanternSphere
    {
        public float distance = 0.2f;
        public float radius = 0.08f;
    }

    [SerializeField]
    private PlayerLanternSettings settings;

    [SerializeField]
    private Light lanternLight;

    [SerializeField]
    private Renderer lanternRenderer;

    // Material slot of the glass on lanternRenderer.
    [SerializeField]
    private int glassMaterialIndex = 1;

    [Header("Collision")]
    [SerializeField]
    private BodyCapsule[] bodyCapsules = new BodyCapsule[0];

    [SerializeField]
    private LanternSphere[] lanternSpheres = new LanternSphere[0];

    // Sim step, so the swing behaves the same at any frame rate.
    private const float MaxStep = 1f / 120f;

    // Collision passes per step. More settles contacts with several body parts at once.
    private const int CollisionIterations = 3;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Transform body;
    private Transform socket;
    private MaterialPropertyBlock glassProperties;

    // Lantern's centre of mass in world space, and its velocity.
    private Vector3 bobPosition;
    private Vector3 bobVelocity;
    private Vector3 lastHookPosition;
    private bool simulating = false;

    // Capsule ends last frame and this frame, so each sim step can move them in between.
    private Vector3[] capsuleStartsBefore;
    private Vector3[] capsuleEndsBefore;
    private Vector3[] capsuleStarts;
    private Vector3[] capsuleEnds;

    private float flickerSeed;

    // The light's local position on the lantern, and its smoothed offset from the hook (in the body's space,
    // so turning carries the light round instead of it cutting through the body to catch up).
    private Vector3 lightOffsetOnLantern;
    private Vector3 smoothedLightOffset;
    private bool lightPlaced = false;

    public PlayerLanternSettings Settings => settings;

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogWarning("PlayerLantern has no settings asset assigned, using defaults.", this);
            settings = ScriptableObject.CreateInstance<PlayerLanternSettings>();
        }

        var movement = GetComponentInParent<PlayerMovement>();
        body = movement != null ? movement.transform : transform.root;
        socket = transform.parent;
        glassProperties = new MaterialPropertyBlock();
        flickerSeed = Random.value * 100f;

        if (lanternLight != null)
        {
            lightOffsetOnLantern = lanternLight.transform.localPosition;
        }

        capsuleStartsBefore = new Vector3[bodyCapsules.Length];
        capsuleEndsBefore = new Vector3[bodyCapsules.Length];
        capsuleStarts = new Vector3[bodyCapsules.Length];
        capsuleEnds = new Vector3[bodyCapsules.Length];
    }

    private void OnEnable()
    {
        simulating = false;
        lightPlaced = false;
    }

    private void LateUpdate()
    {
        Swing(Time.deltaTime);
        PlaceLight(Time.deltaTime);
        UpdateLight();
    }

    private void Swing(float deltaTime)
    {
        Vector3 hook = socket != null ? socket.position : transform.position;
        float length = settings.pendulumLength;

        for (int c = 0; c < bodyCapsules.Length; c++)
        {
            capsuleStarts[c] = bodyCapsules[c].start.position;
            capsuleEnds[c] = bodyCapsules[c].end.position;
        }

        // Start hanging still, and snap back after a teleport (respawn, scene placement).
        if (simulating == false || (hook - lastHookPosition).sqrMagnitude > 4f)
        {
            bobPosition = hook + Vector3.down * length;
            bobVelocity = Vector3.zero;
            lastHookPosition = hook;
            System.Array.Copy(capsuleStarts, capsuleStartsBefore, capsuleStarts.Length);
            System.Array.Copy(capsuleEnds, capsuleEndsBefore, capsuleEnds.Length);
            simulating = true;
        }

        if (deltaTime > 0f)
        {
            deltaTime = Mathf.Min(deltaTime, 0.1f);
            int steps = Mathf.CeilToInt(deltaTime / MaxStep);
            float step = deltaTime / steps;
            Vector3 hookVelocity = (hook - lastHookPosition) / deltaTime;
            float damping = Mathf.Exp(-settings.swayDamping * step);

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 stepHook = Vector3.Lerp(lastHookPosition, hook, t);

                // Damp only the swing relative to the hook, so walking at a steady speed doesn't make it trail.
                bobVelocity += Physics.gravity * step;
                bobVelocity = hookVelocity + (bobVelocity - hookVelocity) * damping;
                bobPosition += bobVelocity * step;

                Vector3 direction = ClampSwing(bobPosition - stepHook);
                direction = PushOutOfBody(stepHook, direction, t, deltaTime);
                bobPosition = stepHook + direction * length;

                // The rope is rigid: drop any velocity that would stretch or compress it.
                Vector3 relative = bobVelocity - hookVelocity;
                bobVelocity -= Vector3.Dot(relative, direction) * direction;
            }
        }

        lastHookPosition = hook;
        System.Array.Copy(capsuleStarts, capsuleStartsBefore, capsuleStarts.Length);
        System.Array.Copy(capsuleEnds, capsuleEndsBefore, capsuleEnds.Length);

        // Hang along the swing, facing the same way as the body.
        Vector3 up = (hook - bobPosition).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(body.up, up);
        }

        transform.SetPositionAndRotation(hook, Quaternion.LookRotation(forward, up));
    }

    // Swings the hanging direction until no lantern sphere is inside a body capsule, and stops the lantern
    // moving into the body any faster than the body part itself is moving.
    private Vector3 PushOutOfBody(Vector3 hook, Vector3 direction, float t, float deltaTime)
    {
        for (int iteration = 0; iteration < CollisionIterations; iteration++)
        {
            bool touching = false;

            for (int c = 0; c < bodyCapsules.Length; c++)
            {
                Vector3 start = Vector3.Lerp(capsuleStartsBefore[c], capsuleStarts[c], t);
                Vector3 end = Vector3.Lerp(capsuleEndsBefore[c], capsuleEnds[c], t);

                foreach (LanternSphere sphere in lanternSpheres)
                {
                    Vector3 center = hook + direction * sphere.distance;
                    float segment = ClosestOnSegment(start, end, center);
                    Vector3 closest = Vector3.Lerp(start, end, segment);
                    Vector3 away = center - closest;
                    float distance = away.magnitude;
                    float overlap = bodyCapsules[c].radius + sphere.radius - distance;
                    if (overlap <= 0f || distance < 0.0001f)
                    {
                        continue;
                    }

                    Vector3 normal = away / distance;
                    direction = (center + normal * overlap - hook).normalized;
                    touching = true;

                    // The body part's own velocity at the contact.
                    Vector3 startVelocity = (capsuleStarts[c] - capsuleStartsBefore[c]) / deltaTime;
                    Vector3 endVelocity = (capsuleEnds[c] - capsuleEndsBefore[c]) / deltaTime;
                    Vector3 surfaceVelocity = Vector3.Lerp(startVelocity, endVelocity, segment);

                    float inward = Vector3.Dot(bobVelocity - surfaceVelocity, normal);
                    if (inward < 0f)
                    {
                        bobVelocity -= inward * normal;
                    }
                }
            }

            if (touching == false)
            {
                break;
            }
        }

        return direction;
    }

    private static float ClosestOnSegment(Vector3 start, Vector3 end, Vector3 point)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared < 0.000001f)
        {
            return 0f;
        }

        return Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
    }

    // Unit direction from hook to lantern, kept within maxSwingAngle of straight down.
    private Vector3 ClampSwing(Vector3 offset)
    {
        if (offset.sqrMagnitude < 0.000001f)
        {
            return Vector3.down;
        }

        Vector3 direction = offset.normalized;
        float maxAngle = settings.maxSwingAngle;
        if (Vector3.Angle(Vector3.down, direction) > maxAngle)
        {
            Vector3 axis = Vector3.Cross(Vector3.down, direction);
            if (axis.sqrMagnitude < 0.000001f)
            {
                axis = body.right;
            }

            direction = Quaternion.AngleAxis(maxAngle, axis.normalized) * Vector3.down;
        }

        return direction;
    }

    // Moves the light toward where the lantern's flame is, kept at least lightClearance out from the body's
    // centre line and smoothed over lightSmoothing seconds relative to the hook, so it doesn't follow every swing.
    // Only the swing is smoothed: the body's own turns carry the light at once.
    private void PlaceLight(float deltaTime)
    {
        if (lanternLight == null)
        {
            return;
        }

        Vector3 hook = transform.position;
        Vector3 flame = transform.TransformPoint(lightOffsetOnLantern);

        Vector3 outward = Vector3.ProjectOnPlane(flame - body.position, Vector3.up);
        float distance = outward.magnitude;
        if (distance < settings.lightClearance && distance > 0.0001f)
        {
            flame += outward / distance * (settings.lightClearance - distance);
        }

        Vector3 offset = body.InverseTransformVector(flame - hook);
        if (lightPlaced == false || settings.lightSmoothing <= 0f)
        {
            smoothedLightOffset = offset;
            lightPlaced = true;
        }
        else
        {
            smoothedLightOffset = Vector3.Lerp(smoothedLightOffset, offset, 1f - Mathf.Exp(-deltaTime / settings.lightSmoothing));
        }

        lanternLight.transform.position = hook + body.TransformVector(smoothedLightOffset);
    }

    private void UpdateLight()
    {
        float noise = Mathf.PerlinNoise(Time.time * settings.flickerSpeed, flickerSeed) * 2f - 1f;
        float flicker = 1f + noise * settings.flickerAmount;

        if (lanternLight != null)
        {
            lanternLight.color = settings.lightColor;
            lanternLight.intensity = settings.lightIntensity * flicker;
            lanternLight.range = settings.lightRange;
            lanternLight.shadows = settings.castShadows ? LightShadows.Soft : LightShadows.None;
        }

        if (lanternRenderer != null)
        {
            lanternRenderer.GetPropertyBlock(glassProperties, glassMaterialIndex);
            glassProperties.SetColor(EmissionColorId, settings.lightColor * (settings.glassGlow * flicker));
            lanternRenderer.SetPropertyBlock(glassProperties, glassMaterialIndex);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
        foreach (BodyCapsule capsule in bodyCapsules)
        {
            if (capsule.start == null || capsule.end == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(capsule.start.position, capsule.radius);
            Gizmos.DrawWireSphere(capsule.end.position, capsule.radius);
            Gizmos.DrawLine(capsule.start.position, capsule.end.position);
        }

        Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.8f);
        foreach (LanternSphere sphere in lanternSpheres)
        {
            Gizmos.DrawWireSphere(transform.position - transform.up * sphere.distance, sphere.radius);
        }
    }
}
