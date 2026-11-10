using UnityEngine;

// One of the Master's ember threads (Threads, §4): a burning line that races along the floor from him at `speed`
// up to `length`, 1 m wide, hurting the player once if its head passes over them. It goes through everything; the
// answer is to step out of it or roll over it (i-frames). Unblockable, unparryable (no glint). The floor fire it
// leaves behind is the hall's (Stage 5, FloorFire).
public class ThreadLine : MonoBehaviour
{
    private MasterP1 owner;
    private Vector3 origin;
    private Vector3 direction;
    private float speed;
    private float length;
    private float width;
    private float damage;
    private float travelled;
    private float fade = -1f;
    private bool hit;
    private LineRenderer line;

    // Raised when a thread reaches its end, with its start and end (the floor fire hooks in here).
    public static event System.Action<Vector3, Vector3, float> OnLineDrawn;

    // Each frame's stretch of the head's path (the hall's winches listen: a thread crossing one snaps its chain).
    public static event System.Action<Vector3, Vector3, float> OnSwept;

    private const float FireLag = 3f;

    // The fire it leaves on the floor (§8), growing behind the head.
    private FirePatch fire;
    private int fireGeneration;

    public static ThreadLine Launch(MasterP1 owner, Vector3 origin, Vector3 direction, float speed, float length, float width, float damage)
    {
        var go = new GameObject("ThreadLine");
        var t = go.AddComponent<ThreadLine>();
        t.owner = owner;
        t.origin = new Vector3(origin.x, owner.transform.position.y + 0.05f, origin.z);
        direction.y = 0f;
        t.direction = direction.normalized;
        t.speed = speed;
        t.length = length;
        t.width = width;
        t.damage = damage;

        t.line = go.AddComponent<LineRenderer>();
        t.line.positionCount = 2;
        t.line.useWorldSpace = true;
        t.line.widthMultiplier = width * 0.6f;
        t.line.alignment = LineAlignment.TransformZ;
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        t.line.numCapVertices = 2;
        t.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.35f, 0.08f) * 4f);
            t.line.sharedMaterial = material;
        }

        t.line.SetPosition(0, t.origin);
        t.line.SetPosition(1, t.origin);
        t.fire = FloorFire.Line(t.origin, t.origin, width, 8f);
        t.fireGeneration = t.fire != null ? t.fire.Generation : 0;
        return t;
    }

    private void Update()
    {
        if (fade >= 0f)
        {
            fade += Time.deltaTime;
            line.widthMultiplier = width * 0.6f * Mathf.Clamp01(1f - fade / 0.6f);
            if (fade >= 0.6f)
                Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        Vector3 from = origin + direction * travelled;
        travelled = Mathf.Min(length, travelled + step);
        Vector3 head = origin + direction * travelled;
        line.SetPosition(1, head);
        OnSwept?.Invoke(from, head, width);
        // The fire catches a few metres behind the head, so a player it hits has a moment to step off the line.
        if (fire != null && fire.Generation == fireGeneration && travelled > FireLag)
            fire.Stretch(head - direction * FireLag);

        if (hit == false && owner != null && owner.player != null)
        {
            Vector3 p = owner.player.position;
            p.y = head.y;
            Vector3 segment = head - from;
            float t = segment.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - from, segment) / segment.sqrMagnitude) : 0f;
            if (Vector3.Distance(p, from + segment * t) <= width * 0.5f + 0.3f)
            {
                hit = true;
                owner.Deal(damage, 10f, true, true, 0f, 0f, direction);
            }
        }

        if (travelled >= length)
        {
            OnLineDrawn?.Invoke(origin, head, width);
            fade = 0f;
        }
    }
}
