using System.Collections.Generic;
using UnityEngine;

// Where one of the Master's spells will land, drawn into the floor as thin ember cracks (owner, 2026-11-11: "better
// indication of where a spell will land so that the player can react. It should still be difficult"). It speaks the
// floor's lava-crack language and keeps the no-glow rule (owner, 2026-11-11): lines only, no filled zone, no halo.
//  - While he aims, the cracks are faint and follow the aim.
//  - At the lock they go crisp and stop moving: the spell lands exactly there.
//  - Each crack opens from his end over the whole tell and reaches its full length as the spell lands, so the mark is
//    also the spell's timer. Then it fades as the spell itself takes over.
// The lock comes late (threadsLock, lashLock; the Hammerfall's 0.25 s), so reading the mark is a reaction, not a
// free pass. Lanes: Threads (3) and the Thread Lash (5), one crack down the middle of each lane, the line the thread
// will run (its hit corridor is that straight line). Ring: the Hammerfall's impact circle, closing round from his side.
public class SpellMark : MonoBehaviour
{
    private const float PointSpacing = 0.6f;
    private const float FaintAlpha = 0.3f;
    private const float LockedAlpha = 0.75f;
    private const float FaintWidth = 0.07f;
    private const float LockedWidth = 0.1f;
    private const float FadeTime = 0.35f;

    private class Crack
    {
        public LineRenderer line;
        // Lanes: (distance along the lane, sideways wander) per point. Ring: (angle swept from the start, radius wander).
        public readonly List<Vector2> points = new List<Vector2>();
        public float angle;
        public float side;
    }

    private readonly List<Crack> cracks = new List<Crack>();
    private Material material;
    private bool ring;
    private Vector3 origin;
    private Vector3 forward = Vector3.forward;
    private float length;
    private float radius;
    private float startTime;
    private float duration;
    private float lockTime = -1f;
    private float fadeTime = -1f;

    public bool Locked => lockTime >= 0f;

    // A fan of `count` lanes, `fan` degrees apart, from `origin` along `forward` for `length` metres, landing in
    // `duration` seconds.
    public static SpellMark Lanes(Vector3 origin, Vector3 forward, int count, float fan, float length, float duration)
    {
        SpellMark mark = Create("SpellMark_Lanes", duration);
        mark.length = length;
        var random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        for (int i = 0; i < count; i++)
        {
            var crack = new Crack { angle = (i - (count - 1) * 0.5f) * fan, line = mark.Line() };
            float wander = 0f;
            for (float d = 0f; d < length + PointSpacing; d += PointSpacing)
            {
                // A crack's kinks, kept inside a hand's width of the lane's centre: the thread runs straight down it.
                wander = Mathf.Clamp(wander + ((float)random.NextDouble() - 0.5f) * 0.12f, -0.08f, 0.08f);
                crack.points.Add(new Vector2(Mathf.Min(d, length), d == 0f ? 0f : wander));
            }

            mark.cracks.Add(crack);
        }

        mark.Aim(origin, forward);
        return mark;
    }

    // The impact circle of `radius` round `centre`, closing from the side facing `from` (him) to the far side.
    public static SpellMark Ring(Vector3 centre, Vector3 from, float radius, float duration)
    {
        SpellMark mark = Create("SpellMark_Ring", duration);
        mark.ring = true;
        mark.radius = radius;
        var random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        int steps = Mathf.Max(12, Mathf.CeilToInt(Mathf.PI * radius / PointSpacing));
        for (int half = 0; half < 2; half++)
        {
            // Two arcs from his side, one each way round, meeting opposite him as it lands.
            var crack = new Crack { side = half == 0 ? 1f : -1f, line = mark.Line() };
            float wander = 0f;
            for (int i = 0; i <= steps; i++)
            {
                wander = Mathf.Clamp(wander + ((float)random.NextDouble() - 0.5f) * 0.1f, -0.07f, 0.07f);
                crack.points.Add(new Vector2(Mathf.PI * i / steps, i == 0 || i == steps ? 0f : wander));
            }

            mark.cracks.Add(crack);
        }

        mark.Aim(centre, centre - from);
        return mark;
    }

    private static SpellMark Create(string name, float duration)
    {
        var go = new GameObject(name);
        var mark = go.AddComponent<SpellMark>();
        mark.startTime = Time.time;
        mark.duration = Mathf.Max(duration, 0.05f);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
        {
            // The threads' own ember (ThreadLine), dimmer through the line colours: it reads as the crack to come.
            var m = new Material(shader);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.SetColor("_BaseColor", new Color(1f, 0.34f, 0.07f) * 0.8f);
            mark.material = m;
        }

        return mark;
    }

    private LineRenderer Line()
    {
        var go = new GameObject("Crack");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 2;
        line.numCornerVertices = 1;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = material;
        // Tapering to the tip, like a crack opening.
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 0.8f), new Keyframe(1f, 0.2f));
        line.positionCount = 0;
        return line;
    }

    // While he aims: the lanes' start and direction (Lanes), or the circle's centre (Ring). Ignored once locked.
    public void Aim(Vector3 at, Vector3 direction)
    {
        if (Locked)
            return;
        origin = at;
        direction.y = 0f;
        if (direction.sqrMagnitude > 1e-4f)
            forward = direction.normalized;
    }

    // The aim holds from here: the spell lands where the mark now lies.
    public void Lock()
    {
        if (Locked == false)
            lockTime = Time.time;
    }

    // The spell has landed (or was cut short): the mark fades as the spell itself takes over.
    public void Release()
    {
        if (fadeTime < 0f)
            fadeTime = Time.time;
    }

    public Vector3 Origin => origin;

    public Vector3 Forward => forward;

    // The direction of lane `index` (Lanes), as the spell will fire it.
    public Vector3 LaneDirection(int index) => Quaternion.AngleAxis(cracks[index].angle, Vector3.up) * forward;

    private void LateUpdate()
    {
        float fade = 1f;
        if (fadeTime >= 0f)
        {
            fade = 1f - (Time.time - fadeTime) / FadeTime;
            if (fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        // How far each crack has opened: eased out, reaching the end as the spell lands.
        float p = Mathf.Clamp01((Time.time - startTime) / duration);
        float open = 1f - (1f - p) * (1f - p);
        float locked = Locked ? Mathf.Clamp01((Time.time - lockTime) / 0.08f) : 0f;
        float alpha = Mathf.Lerp(FaintAlpha, LockedAlpha, locked) * fade;
        float width = Mathf.Lerp(FaintWidth, LockedWidth, locked);
        var colour = new Color(1f, 1f, 1f, alpha);
        Vector3 side = Vector3.Cross(Vector3.up, forward);

        foreach (Crack crack in cracks)
        {
            crack.line.startColor = colour;
            crack.line.endColor = colour;
            crack.line.widthMultiplier = width;
            float reach = ring ? Mathf.PI * open : length * open;
            int count = 0;
            for (int i = 0; i < crack.points.Count; i++)
            {
                if (crack.points[i].x > reach)
                {
                    // The head, between this point and the last.
                    Vector2 a = crack.points[i - 1], b = crack.points[i];
                    Vector2 head = Vector2.Lerp(a, b, Mathf.InverseLerp(a.x, b.x, reach));
                    Set(crack, count++, head, side);
                    break;
                }

                Set(crack, count++, crack.points[i], side);
            }

            crack.line.positionCount = count;
        }
    }

    private void Set(Crack crack, int index, Vector2 point, Vector3 side)
    {
        Vector3 p;
        if (ring)
        {
            // From the side facing him (-forward), round one way or the other.
            float a = crack.side * point.x;
            Vector3 radial = Quaternion.AngleAxis(a * Mathf.Rad2Deg, Vector3.up) * -forward;
            p = origin + radial * (radius + point.y);
        }
        else
        {
            Vector3 lane = Quaternion.AngleAxis(crack.angle, Vector3.up) * forward;
            p = origin + lane * point.x + Vector3.Cross(Vector3.up, lane) * point.y;
        }

        if (crack.line.positionCount <= index)
            crack.line.positionCount = index + 1;
        crack.line.SetPosition(index, p + Vector3.up * 0.04f);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
