using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The Last Command's end (The Master.md §7.5): "the two halves of the mask fall and break". The mask is part of the
// Colossus's skin, so the halves are cut from it at the moment he crumbles: the face's triangles (in front of his head,
// facing forward) are taken from the skin as it stands, split down the crack, and given thickness. The halves part,
// fall, cool, and break on the floor into a few shards that stay where they land. No physics: like the King's sword,
// they're scripted, so they always land in the open and never roll into Kael.
public class MaskShatter : MonoBehaviour
{
    // The face: triangles within `radius` of a point `centreForward` along the head-to-mask line, at least
    // `forwardMin` in front of the head bone, facing within acos(normalMin) of forward. Measured on his rig.
    public float radius = 0.8f;
    public float centreForward = 0.4f;
    public float forwardMin = 0.1f;
    public float normalMin = 0.4f;
    public float thickness = 0.06f;
    public float gravity = 20f;
    public float startGlow = 0.6f;

    public int ShardsOnFloor { get; private set; }
    public bool Landed { get; private set; }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private Material ashMaterial;

    public static MaskShatter Drop(Transform parent, SkinnedMeshRenderer skin, Transform head, Transform mask, Transform body, Material ashMaterial)
    {
        var go = new GameObject("MaskShatter");
        go.transform.SetParent(parent, false);
        var shatter = go.AddComponent<MaskShatter>();
        shatter.ashMaterial = ashMaterial;
        shatter.Begin(skin, head, mask, body);
        return shatter;
    }

    private void Begin(SkinnedMeshRenderer skin, Transform head, Transform mask, Transform body)
    {
        var baked = new Mesh();
        skin.BakeMesh(baked, true);
        Matrix4x4 toWorld = skin.transform.localToWorldMatrix;
        Vector3[] local = baked.vertices;
        var world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = toWorld.MultiplyPoint3x4(local[i]);
        Vector2[] uv = baked.uv;
        Material[] materials = skin.sharedMaterials;

        Vector3 hp = head.position;
        Vector3 mp = mask.position;
        Vector3 forward = (mp - hp).normalized;
        Vector3 centre = hp + forward * centreForward;
        Vector3 right = body.right;

        // Per submesh, so each keeps its own material.
        var left = new List<Tri>();
        var rightHalf = new List<Tri>();
        for (int sub = 0; sub < baked.subMeshCount; sub++)
        {
            int[] tris = baked.GetTriangles(sub);
            for (int t = 0; t < tris.Length; t += 3)
            {
                var tri = new Tri(tris[t], tris[t + 1], tris[t + 2], sub);
                Vector3 a = world[tri.a], b = world[tri.b], c = world[tri.c];
                Vector3 mid = (a + b + c) / 3f;
                if ((mid - centre).magnitude > radius || Vector3.Dot(mid - hp, forward) < forwardMin)
                    continue;
                if (Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, forward) < normalMin)
                    continue;
                (Vector3.Dot(mid - mp, right) > 0f ? rightHalf : left).Add(tri);
            }
        }

        Destroy(baked);
        if (left.Count + rightHalf.Count == 0)
            return;
        StartCoroutine(Fall(left, world, uv, materials, -right, forward));
        StartCoroutine(Fall(rightHalf, world, uv, materials, right, forward));
    }

    private struct Tri
    {
        public int a, b, c, sub;
        public Tri(int a, int b, int c, int sub) { this.a = a; this.b = b; this.c = c; this.sub = sub; }
        public Vector3 Mid(Vector3[] w) => (w[a] + w[b] + w[c]) / 3f;
    }

    // One half: it parts from the other along the crack, tips outward and face-up as it falls, lands, and breaks.
    private IEnumerator Fall(List<Tri> tris, Vector3[] world, Vector2[] uv, Material[] materials, Vector3 outward, Vector3 forward)
    {
        if (tris.Count < 3)
            yield break;

        // The half, and the shards it will break into (cut across its face through its middle, at random angles).
        Vector3 pivot = Vector3.zero;
        foreach (Tri t in tris)
            pivot += t.Mid(world);
        pivot /= tris.Count;
        Transform half = Piece("MaskHalf", tris, world, uv, materials, pivot, null);
        var shards = new List<Transform>();
        foreach (List<Tri> part in Break(tris, world, pivot, forward))
            shards.Add(Piece("MaskShard", part, world, uv, materials, Centre(part, world), half));
        foreach (Transform s in shards)
            s.gameObject.SetActive(false);

        float floor = FloorBelow(pivot);
        var renderers = new List<Renderer>(half.GetComponentsInChildren<Renderer>(true));
        var block = new MaterialPropertyBlock();

        // The crack opens: the halves part a hand's width, the ember light inside going out.
        float start = Time.time;
        Vector3 from = half.position;
        while (Time.time - start < 0.18f)
        {
            float t = (Time.time - start) / 0.18f;
            half.position = from + outward * (0.05f * t);
            Glow(renderers, block, startGlow);
            yield return null;
        }

        // The fall: outward and a little forward, tipping face-up (the way a dropped mask lands), a slow yaw.
        Vector3 velocity = outward * Random.Range(0.7f, 1.1f) + Vector3.ProjectOnPlane(forward, Vector3.up).normalized * Random.Range(0.3f, 0.6f) + Vector3.up * 0.8f;
        Quaternion fromRotation = half.rotation;
        Vector3 face = forward;
        Quaternion faceUp = Quaternion.FromToRotation(face, Vector3.Slerp(Vector3.up, outward, 0.25f).normalized) * fromRotation;
        faceUp = Quaternion.AngleAxis(Random.Range(-50f, 50f), Vector3.up) * faceUp;
        float fallTime = Mathf.Max(0.35f, (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * Mathf.Max(0.2f, half.position.y - floor))) / gravity);
        start = Time.time;
        while (true)
        {
            float dt = Time.deltaTime;
            velocity += Vector3.down * gravity * dt;
            half.position += velocity * dt;
            float t = Mathf.Clamp01((Time.time - start) / fallTime);
            half.rotation = Quaternion.Slerp(fromRotation, faceUp, Mathf.SmoothStep(0f, 1f, t));
            Glow(renderers, block, startGlow * (1f - t));
            float bottom = Bottom(renderers);
            if (bottom <= floor)
            {
                half.position += Vector3.up * (floor - bottom);
                break;
            }

            yield return null;
        }

        // It breaks where it lands: the shards spread a little and settle; a puff of ash, a small jolt.
        half.rotation = faceUp;
        Glow(renderers, block, 0f);
        Landed = true;
        FindAnyObjectByType<PlayerCamera>()?.AddShake(0.06f, 0.15f);
        Puff(half.position);
        half.GetComponent<Renderer>().enabled = false;
        var slides = new List<Vector3>();
        var spins = new List<float>();
        foreach (Transform s in shards)
        {
            s.gameObject.SetActive(true);
            Vector3 away = Vector3.ProjectOnPlane(s.position - half.position, Vector3.up);
            if (away.sqrMagnitude < 0.0001f)
                away = outward;
            slides.Add(away.normalized * Random.Range(0.12f, 0.32f) + outward * 0.08f);
            spins.Add(Random.Range(-35f, 35f));
        }

        ShardsOnFloor += shards.Count;
        var shardFrom = new List<Vector3>();
        var shardRotation = new List<Quaternion>();
        foreach (Transform s in shards)
        {
            shardFrom.Add(s.position);
            shardRotation.Add(s.rotation);
        }

        start = Time.time;
        while (Time.time - start < 0.4f)
        {
            float t = 1f - Mathf.Pow(1f - (Time.time - start) / 0.4f, 3f);
            for (int i = 0; i < shards.Count; i++)
            {
                shards[i].SetPositionAndRotation(shardFrom[i] + slides[i] * t, Quaternion.AngleAxis(spins[i] * t, Vector3.up) * shardRotation[i]);
                Settle(shards[i], floor);
            }

            yield return null;
        }
    }

    // Cuts a half into three shards: once through its middle, then the bigger part again, across the face.
    private static List<List<Tri>> Break(List<Tri> tris, Vector3[] world, Vector3 pivot, Vector3 forward)
    {
        var parts = new List<List<Tri>>();
        Cut(tris, world, pivot, forward, out List<Tri> a, out List<Tri> b);
        List<Tri> big = a.Count >= b.Count ? a : b;
        List<Tri> small = big == a ? b : a;
        if (small.Count > 0)
            parts.Add(small);
        Cut(big, world, Centre(big, world), forward, out List<Tri> c, out List<Tri> d);
        if (c.Count > 0)
            parts.Add(c);
        if (d.Count > 0)
            parts.Add(d);
        // A sliver isn't a shard: fold tiny parts into the biggest.
        parts.Sort((x, y) => y.Count.CompareTo(x.Count));
        for (int i = parts.Count - 1; i > 0; i--)
        {
            if (parts[i].Count < 6)
            {
                parts[0].AddRange(parts[i]);
                parts.RemoveAt(i);
            }
        }

        return parts;
    }

    private static void Cut(List<Tri> tris, Vector3[] world, Vector3 through, Vector3 forward, out List<Tri> a, out List<Tri> b)
    {
        Vector3 normal = Quaternion.AngleAxis(Random.Range(0f, 180f), forward) * Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
        a = new List<Tri>();
        b = new List<Tri>();
        foreach (Tri t in tris)
            (Vector3.Dot(t.Mid(world) - through, normal) > 0f ? a : b).Add(t);
    }

    private static Vector3 Centre(List<Tri> tris, Vector3[] world)
    {
        Vector3 sum = Vector3.zero;
        foreach (Tri t in tris)
            sum += t.Mid(world);
        return tris.Count > 0 ? sum / tris.Count : Vector3.zero;
    }

    // A shell piece: the skin's own triangles in front, the same surface pushed in by `thickness` behind, and walls
    // round its open edges (the broken edge of the ceramic).
    private Transform Piece(string name, List<Tri> tris, Vector3[] world, Vector2[] uv, Material[] materials, Vector3 pivot, Transform parent)
    {
        Vector3 inward = Vector3.zero;
        foreach (Tri t in tris)
            inward -= Vector3.Cross(world[t.b] - world[t.a], world[t.c] - world[t.a]);
        inward = inward.sqrMagnitude > 0f ? inward.normalized : Vector3.back;

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        int subs = 1;
        foreach (Tri t in tris)
            subs = Mathf.Max(subs, t.sub + 1);
        var indices = new List<int>[subs];
        for (int i = 0; i < subs; i++)
            indices[i] = new List<int>();

        void Add(Vector3 p, Vector3 n, Vector2 u)
        {
            positions.Add(p - pivot);
            normals.Add(n);
            uvs.Add(u);
        }

        Vector2 Uv(int i) => uv != null && i < uv.Length ? uv[i] : Vector2.zero;

        // Count each edge to find the open ones.
        var edges = new Dictionary<long, int>();
        long Key(int x, int y) => x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
        foreach (Tri t in tris)
        {
            foreach (long k in new[] { Key(t.a, t.b), Key(t.b, t.c), Key(t.c, t.a) })
                edges[k] = edges.TryGetValue(k, out int n) ? n + 1 : 1;
        }

        foreach (Tri t in tris)
        {
            Vector3 a = world[t.a], b = world[t.b], c = world[t.c];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            Vector3 back = inward * thickness;
            List<int> list = indices[t.sub];

            // Front (flat-shaded: it's low poly, and the cut reads as ceramic).
            int i0 = positions.Count;
            Add(a, n, Uv(t.a));
            Add(b, n, Uv(t.b));
            Add(c, n, Uv(t.c));
            list.Add(i0);
            list.Add(i0 + 1);
            list.Add(i0 + 2);

            // Back, reversed.
            int j0 = positions.Count;
            Add(a + back, -n, Uv(t.a));
            Add(b + back, -n, Uv(t.b));
            Add(c + back, -n, Uv(t.c));
            list.Add(j0);
            list.Add(j0 + 2);
            list.Add(j0 + 1);

            // Walls on open edges, facing away from the triangle.
            Vector3 mid = (a + b + c) / 3f;
            (int, int)[] sides = { (t.a, t.b), (t.b, t.c), (t.c, t.a) };
            foreach ((int p, int q) in sides)
            {
                if (edges[Key(p, q)] != 1)
                    continue;
                Vector3 p0 = world[p], p1 = world[q], p2 = world[q] + back, p3 = world[p] + back;
                Vector3 outward = Vector3.ProjectOnPlane((p0 + p1) * 0.5f - mid, n).normalized;
                Vector3 wall = Vector3.Cross(p1 - p0, p2 - p0);
                bool flip = Vector3.Dot(wall, outward) < 0f;
                int k0 = positions.Count;
                Add(p0, outward, Uv(p));
                Add(p1, outward, Uv(q));
                Add(p2, outward, Uv(q));
                Add(p3, outward, Uv(p));
                if (flip)
                {
                    list.AddRange(new[] { k0, k0 + 2, k0 + 1, k0, k0 + 3, k0 + 2 });
                }
                else
                {
                    list.AddRange(new[] { k0, k0 + 1, k0 + 2, k0, k0 + 2, k0 + 3 });
                }
            }
        }

        var mesh = new Mesh { name = name };
        mesh.indexFormat = positions.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = subs;
        for (int i = 0; i < subs; i++)
            mesh.SetTriangles(indices[i], i);
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.SetPositionAndRotation(pivot, Quaternion.identity);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var mats = new Material[subs];
        for (int i = 0; i < subs; i++)
            mats[i] = materials[Mathf.Min(i, materials.Length - 1)];
        renderer.sharedMaterials = mats;
        return go.transform;
    }

    private static void Glow(List<Renderer> renderers, MaterialPropertyBlock block, float glow)
    {
        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, Color.white * glow);
            r.SetPropertyBlock(block);
        }
    }

    private static float Bottom(List<Renderer> renderers)
    {
        float bottom = float.MaxValue;
        foreach (Renderer r in renderers)
        {
            if (r.enabled && r.gameObject.activeInHierarchy)
                bottom = Mathf.Min(bottom, r.bounds.min.y);
        }

        return bottom;
    }

    private static void Settle(Transform shard, float floor)
    {
        var r = shard.GetComponent<Renderer>();
        if (r != null)
            shard.position += Vector3.up * (floor - r.bounds.min.y);
    }

    private float FloorBelow(Vector3 at)
    {
        int layers = LayerMask.GetMask("Default", "Ground", "Obstacles");
        if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 12f, layers, QueryTriggerInteraction.Ignore))
            return hit.point.y + 0.01f;
        return at.y - 4f;
    }

    // A small puff of ash where a half lands.
    private void Puff(Vector3 at)
    {
        if (ashMaterial == null)
            return;
        var go = new GameObject("MaskAsh");
        go.transform.SetParent(transform, false);
        go.transform.position = at;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.38f, 0.35f, 0.8f), new Color(0.58f, 0.52f, 0.47f, 0.6f));
        main.gravityModifier = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.3f;
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var material = new Material(ashMaterial);
        material.SetColor("_BaseColor", Color.white);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    private void OnDestroy()
    {
        foreach (MeshFilter f in GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh != null)
                Destroy(f.sharedMesh);
        }

        foreach (ParticleSystemRenderer r in GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (r.sharedMaterial != null)
                Destroy(r.sharedMaterial);
        }
    }
}
