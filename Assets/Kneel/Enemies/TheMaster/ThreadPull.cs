using System.Collections.Generic;
using UnityEngine;

// The curse's threads going taut (The Master.md §5 1.5-4.0 s, and the Gather): thin ember lines that reach from the
// dead outside the hall into his back. Most come through the open grand doors, the rest from the arcade gaps. Each
// line shoots out in a staggered moment, pulls taut, and carries a pulse of brighter ash along it toward him; they all
// fade together at the end. One LineRenderer per thread (§8: up to 60, in the transition only).
public class ThreadPull : MonoBehaviour
{
    private class Thread
    {
        public LineRenderer line;
        public Vector3 from;
        public float start;
        public float speed;
        public float sag;
        public float width;
    }

    private readonly List<Thread> threads = new List<Thread>();
    private Transform target;
    private Vector3 targetOffset;
    private float started;
    private float grow;
    private float hold;
    private float fade = 0.8f;
    private Material material;

    private const int Points = 10;

    // Where the threads come from (world): the grand doors (most) and the arcade gaps along both walls.
    public static Vector3 DoorsCentre = new Vector3(54f, 11f, 407.5f);
    public static float DoorsHalfWidth = 6f;
    public static float WallWest = 33.5f;
    public static float WallEast = 74.5f;
    public static Vector2 ArcadeZ = new Vector2(418f, 466f);

    // `count` threads into `into` (offset from it), shooting out over `grow` seconds, held `hold` seconds, then fading.
    public static ThreadPull Begin(Transform into, Vector3 offset, int count, float grow, float hold, float doorShare = 0.7f)
    {
        var go = new GameObject("ThreadPull");
        var pull = go.AddComponent<ThreadPull>();
        pull.target = into;
        pull.targetOffset = offset;
        pull.started = Time.time;
        pull.grow = Mathf.Max(grow, 0.1f);
        pull.hold = hold;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        pull.material = new Material(shader);
        pull.material.SetFloat("_Surface", 1f);
        pull.material.SetFloat("_Blend", 2f);
        pull.material.SetColor("_BaseColor", new Color(1f, 0.38f, 0.1f) * 1.7f);
        pull.material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        pull.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        pull.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        pull.material.SetInt("_ZWrite", 0);
        pull.material.renderQueue = 3000;

        var random = new System.Random(count * 7919 + Mathf.RoundToInt(into.position.x * 10f));
        for (int i = 0; i < count; i++)
        {
            Vector3 from;
            if (random.NextDouble() < doorShare)
            {
                from = DoorsCentre + new Vector3((float)(random.NextDouble() * 2 - 1) * DoorsHalfWidth, (float)(random.NextDouble() * 2 - 1) * 1.8f, -(float)random.NextDouble() * 6f);
            }
            else
            {
                bool west = random.NextDouble() < 0.5;
                from = new Vector3(west ? WallWest : WallEast, 10f + (float)random.NextDouble() * 4f, Mathf.Lerp(ArcadeZ.x, ArcadeZ.y, (float)random.NextDouble()));
            }

            var line = new GameObject("Thread").AddComponent<LineRenderer>();
            line.transform.SetParent(go.transform, false);
            line.positionCount = Points;
            line.useWorldSpace = true;
            line.numCapVertices = 1;
            line.sharedMaterial = pull.material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            pull.threads.Add(new Thread
            {
                line = line,
                from = from,
                start = (float)random.NextDouble() * pull.grow * 0.75f,
                speed = 0.35f + (float)random.NextDouble() * 0.25f,
                sag = 0.6f + (float)random.NextDouble() * 1.4f,
                width = 0.02f + (float)random.NextDouble() * 0.025f,
            });
            line.enabled = false;
        }

        return pull;
    }

    // Starts fading now (a broken Gather snaps them).
    public void Release(float seconds = 0.3f)
    {
        fade = seconds;
        hold = Mathf.Max(0f, Time.time - started - grow);
    }

    private void Update()
    {
        float t = Time.time - started;
        float fadeT = t - grow - hold;
        if (target == null || fadeT > fade)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 to = target.position + target.rotation * targetOffset;
        float alpha = fadeT > 0f ? 1f - fadeT / fade : 1f;
        foreach (Thread thread in threads)
        {
            float own = t - thread.start;
            if (own <= 0f)
                continue;
            thread.line.enabled = true;

            // It shoots out from its source in 0.4 s, sagging, then pulls taut.
            float reach = Mathf.Clamp01(own / 0.4f);
            float taut = Mathf.Clamp01((own - 0.4f) / 0.5f);
            for (int i = 0; i < Points; i++)
            {
                float u = i / (float)(Points - 1) * reach;
                Vector3 p = Vector3.Lerp(thread.from, to, u);
                p += Vector3.down * Mathf.Sin(u * Mathf.PI) * thread.sag * (1f - taut);
                thread.line.SetPosition(i, p);
            }

            // A pulse of brighter ash running toward him along it.
            float pulse = Mathf.Repeat(own * thread.speed, 1f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.6f, 0.2f, 0.06f), 0f), new GradientColorKey(Color.white, Mathf.Clamp(pulse, 0.02f, 0.98f)), new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(0.15f * alpha, 0f), new GradientAlphaKey(0.9f * alpha, Mathf.Clamp(pulse, 0.02f, 0.98f)), new GradientAlphaKey(0.7f * alpha, 1f) });
            thread.line.colorGradient = gradient;
            thread.line.widthMultiplier = thread.width * (0.6f + 0.4f * taut);
        }
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
