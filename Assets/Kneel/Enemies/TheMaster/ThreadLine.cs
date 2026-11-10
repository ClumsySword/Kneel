using System.Collections.Generic;
using UnityEngine;

// One of the Master's ember threads (P1's Threads, §4; the Colossus's Thread Lash, §6): a burning crack that races
// along the floor from him at `speed` up to `length`, hurting the player once if it passes over them. It goes through
// everything; the answer is to step out of it or roll over it (i-frames). Unblockable, unparryable (no glint).
//
// It runs like a crack in lava (owner, 2026-11-10: "spread out like cracks, at some random interval, each time"): the
// main line wanders inside its width, and at random intervals it forks: a side crack shoots off at an angle for a few
// metres, sometimes forking again. Only the main line hurts (once), snaps winches and leaves the floor fire (§8): the
// forks are thinner and dimmer, so the answer stays "step between the lines" (a fork from the next line could
// otherwise reach into the gap).
public class ThreadLine : MonoBehaviour
{
    private class Crack
    {
        public LineRenderer core;
        public LineRenderer glow;
        public readonly List<Vector3> points = new List<Vector3>();
        public Vector3 origin;
        public Vector3 direction;
        public float length;
        public float travelled;
        public float nextPoint;
        public float nextFork;
        public float width;
        public float wander;
        public int depth;
        public bool done;

        public Vector3 Head => origin + direction * travelled + Side * wander;
        public Vector3 Side => Vector3.Cross(Vector3.up, direction);
    }

    private MasterBody owner;
    private Vector3 origin;
    private Vector3 direction;
    private float speed;
    private float length;
    private float width;
    private float damage;
    private float fade = -1f;
    private bool hit;
    private Material material;
    private Material glowMaterial;
    private readonly List<Crack> cracks = new List<Crack>();
    private System.Random random;

    // Raised when a thread reaches its end, with its start and end (the floor fire hooks in here).
    public static event System.Action<Vector3, Vector3, float> OnLineDrawn;

    // Each frame's stretch of a crack's head (the hall's winches listen: a thread crossing one snaps its chain).
    public static event System.Action<Vector3, Vector3, float> OnSwept;

    private const float FireLag = 3f;
    private const float PointSpacing = 0.55f;

    // The fire it leaves on the floor (§8), growing behind the main crack's head.
    private FirePatch fire;
    private int fireGeneration;

    public static ThreadLine Launch(MasterBody owner, Vector3 origin, Vector3 direction, float speed, float length, float width, float damage)
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
        t.random = new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            // Several threads leave him from one spot, and additive lines add up there: hot enough to read, no hotter.
            t.material = Additive(shader, new Color(1f, 0.5f, 0.16f) * 2.2f);
            t.glowMaterial = Additive(shader, new Color(1f, 0.26f, 0.05f) * 0.35f);
        }

        t.cracks.Add(t.NewCrack(t.origin, t.direction, length, width, 0));
        t.fire = FloorFire.Line(t.origin, t.origin, width, 8f);
        t.fireGeneration = t.fire != null ? t.fire.Generation : 0;
        return t;
    }

    private static Material Additive(Shader shader, Color colour)
    {
        var m = new Material(shader);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        m.SetColor("_BaseColor", colour);
        return m;
    }

    private float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

    private Crack NewCrack(Vector3 from, Vector3 dir, float len, float w, int depth)
    {
        var crack = new Crack { origin = from, direction = dir.normalized, length = len, width = w, depth = depth };
        crack.core = Line("Crack", w * (depth == 0 ? 0.32f : 0.24f), material);
        crack.glow = Line("Glow", w * (depth == 0 ? 1.1f : 0.8f), glowMaterial);
        crack.points.Add(from);
        crack.nextPoint = PointSpacing;
        // The first fork comes 1.5-4 m in (the main line), a side crack's own (rarer) fork 1-2 m in.
        crack.nextFork = depth == 0 ? Range(1.5f, 4f) : depth == 1 && random.NextDouble() < 0.45 ? Range(1f, 2f) : float.MaxValue;
        return crack;
    }

    private LineRenderer Line(string name, float lineWidth, Material lineMaterial)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 2;
        line.numCornerVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = lineMaterial;
        // Tapering to the tip, like a crack.
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 0.7f), new Keyframe(1f, 0.15f));
        line.positionCount = 0;
        return line;
    }

    private void Update()
    {
        if (fade >= 0f)
        {
            fade += Time.deltaTime;
            float k = Mathf.Clamp01(1f - fade / 0.6f);
            foreach (Crack crack in cracks)
            {
                crack.core.widthMultiplier = crack.width * (crack.depth == 0 ? 0.32f : 0.24f) * k;
                crack.glow.widthMultiplier = crack.width * (crack.depth == 0 ? 1.1f : 0.8f) * k;
            }

            if (fade >= 0.6f)
                Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;
        bool growing = false;
        for (int i = 0; i < cracks.Count; i++)
        {
            Crack crack = cracks[i];
            if (crack.done)
                continue;
            growing = true;

            Vector3 from = crack.Head;
            crack.travelled = Mathf.Min(crack.length, crack.travelled + step);

            // A crack's kinks: each new point wanders sideways a little (the main line stays inside its width, so the
            // hit corridor is the straight line the tell showed).
            while (crack.travelled >= crack.nextPoint && crack.nextPoint <= crack.length)
            {
                float limit = crack.depth == 0 ? crack.width * 0.3f : 0.6f;
                crack.wander = Mathf.Clamp(crack.wander + Range(-0.3f, 0.3f), -limit, limit);
                crack.points.Add(crack.origin + crack.direction * crack.nextPoint + crack.Side * crack.wander);
                crack.nextPoint += PointSpacing * Range(0.7f, 1.3f);
            }

            Vector3 head = crack.Head;
            Draw(crack, head);
            if (crack.depth == 0)
            {
                OnSwept?.Invoke(from, head, crack.width);
                Touch(from, head, crack.width);
            }

            // A fork: a side crack shoots off at an angle, from where the head is now.
            if (crack.travelled >= crack.nextFork && crack.travelled < crack.length - 0.5f)
            {
                float side = random.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 dir = Quaternion.AngleAxis(side * Range(25f, 55f), Vector3.up) * crack.direction;
                float len = crack.depth == 0 ? Range(1.5f, 3.2f) : Range(0.8f, 1.6f);
                cracks.Add(NewCrack(head, dir, len, crack.width * 0.45f, crack.depth + 1));
                crack.nextFork = crack.depth == 0 ? crack.travelled + Range(2f, 4.5f) : float.MaxValue;
            }

            if (crack.depth == 0 && fire != null && fire.Generation == fireGeneration && crack.travelled > FireLag)
                fire.Stretch(origin + direction * (crack.travelled - FireLag));

            if (crack.travelled >= crack.length)
            {
                crack.done = true;
                if (crack.depth == 0)
                    OnLineDrawn?.Invoke(origin, origin + direction * length, width);
            }
        }

        if (growing == false)
            fade = 0f;
    }

    private static void Draw(Crack crack, Vector3 head)
    {
        int count = crack.points.Count + 1;
        crack.core.positionCount = count;
        crack.glow.positionCount = count;
        for (int i = 0; i < crack.points.Count; i++)
        {
            crack.core.SetPosition(i, crack.points[i]);
            crack.glow.SetPosition(i, crack.points[i] + Vector3.down * 0.01f);
        }

        crack.core.SetPosition(count - 1, head);
        crack.glow.SetPosition(count - 1, head + Vector3.down * 0.01f);
    }

    // The player under this frame's stretch of a crack's head: hurt once for the whole thread.
    private void Touch(Vector3 from, Vector3 head, float hitWidth)
    {
        if (hit || owner == null || owner.player == null)
            return;
        Vector3 p = owner.player.position;
        p.y = head.y;
        Vector3 segment = head - from;
        float t = segment.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - from, segment) / segment.sqrMagnitude) : 0f;
        if (Vector3.Distance(p, from + segment * t) <= hitWidth * 0.5f + 0.3f)
        {
            hit = true;
            owner.Deal(damage, 10f, true, true, 0f, 0f, segment.sqrMagnitude > 1e-6f ? segment : direction);
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
