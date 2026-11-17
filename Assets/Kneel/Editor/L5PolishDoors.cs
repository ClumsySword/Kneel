using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The doors (owner, 2026-11-11): "It should only be one gate with the two doors (split) opening. They should fit the
    // openings perfectly." Each portal held two of Synty's castle doors side by side, and each of those is itself a
    // frame with two leaves: two gates in one arch. Now one gate per arch: two leaves cut to the opening L5Level.Portal
    // builds (8.4 m wide, straight jambs to 6.6 m, a semicircular head of radius 4.2 m to 10.8 m), hinged at the jambs.
    // Vertical planks whose tops follow the arch, iron straps with studs, an iron band along the curve, a ring pull on
    // each leaf, a dark board behind the planks' joints. The colours are the castle door's own (its charred wood and
    // iron swatches in L4_Knights_Soot), so the gate reads as the pack's.
    // The grand doors stand open into the hall; the outer doors are shut behind the player and fade like the old ones.
    public static partial class L5Polish
    {
        private const float HalfSpan = 4.2f;
        private const float Spring = 6.6f;
        private const float LeafThickness = 0.14f;
        private const string MeshFolder = "Assets/Kneel/Meshes/L5/Polish";

        // UVs into the Knights atlas: two of its dark oak browns (alternated plank to plank) and its darkest iron grey,
        // the castle door's own swatches. The gate wears the pack's darker material (PolyKnights_Mat_01_Dark), not the
        // sooted recolour the old doors had: under the nave's dim light that read as one black slab.
        private static readonly Vector2 WoodUv = new Vector2(0.9887f, 0.405f);
        private static readonly Vector2 WoodDarkUv = new Vector2(0.9887f, 0.355f);
        private static readonly Vector2 IronUv = new Vector2(0.8521f, 0.4221f);
        private const string GateMaterial = "Assets/SyntyStudios/PolygonKnights/Materials/PolyKnights_Mat_01_Dark.mat";

        [MenuItem("Kneel/L5/Polish/Doors")]
        public static void DoorsMenu() => Debug.Log(Doors());

        public static string Doors()
        {
            OpenScene();
            var log = new List<string>();
            log.Add(Gate("L5_Root/Art/GrandDoors", 92f, 0.35f, 811));
            log.Add(Gate("L5_Root/Art/OuterDoors", 0f, 0.22f, 801));
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return "L5 doors: " + string.Join("; ", log);
        }

        // One gate in the portal at `path`: the castle doors out, two leaves in, open by `openAngle` (into +z, the side
        // the player stands) with their closed plane at `planeZ` (portal space).
        private static string Gate(string path, float openAngle, float planeZ, int seed)
        {
            GameObject portal = GameObject.Find(path);
            if (portal == null)
                return path + " not found";

            int removed = 0;
            var doomed = new List<GameObject>();
            foreach (Transform t in portal.transform)
            {
                if (t.name.StartsWith("SM_Bld_Castle_Door") || t.name == "Gate")
                    doomed.Add(t.gameObject);
            }

            foreach (GameObject d in doomed)
            {
                Object.DestroyImmediate(d);
                removed++;
            }

            var gate = new GameObject("Gate").transform;
            gate.SetParent(portal.transform, false);
            var soot = AssetDatabase.LoadAssetAtPath<Material>(GateMaterial);
            EnsureFolder(MeshFolder);
            foreach (float side in new[] { -1f, 1f })
            {
                // side -1: the west leaf, hinged at x = -4.2, reaching east to the middle.
                string name = (side < 0f ? "Leaf_W" : "Leaf_E");
                Mesh mesh = LeafMesh(side, seed + (side < 0f ? 0 : 50));
                mesh.name = "L5_" + portal.name + "_" + name;
                mesh = SaveMesh(mesh, MeshFolder + "/" + mesh.name + ".asset");
                var leaf = new GameObject(name);
                leaf.transform.SetParent(gate, false);
                leaf.transform.localPosition = new Vector3(side * HalfSpan, 0f, planeZ);
                // Opening into +z: the west leaf turns its free edge (+x) round to +z (a negative yaw), the east one the other way.
                leaf.transform.localRotation = Quaternion.Euler(0f, side < 0f ? -openAngle : openAngle, 0f);
                leaf.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = leaf.AddComponent<MeshRenderer>();
                r.sharedMaterial = soot;
                leaf.isStatic = true;
                GameObjectUtility.SetStaticEditorFlags(leaf, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }

            // The outer doors fade as the camera ends up behind them: rebuild their fade over what's there now.
            var fade = portal.GetComponent<Kneel.OcclusionFade>();
            if (fade != null)
            {
                Object.DestroyImmediate(fade);
                var probes = new List<GameObject>();
                foreach (Transform t in portal.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "FadeProbe")
                        probes.Add(t.gameObject);
                }

                foreach (GameObject p in probes)
                    Object.DestroyImmediate(p);
                L4Build.AddFade(portal);
            }

            return portal.name + ": " + removed + " castle doors out, one gate in" + (openAngle > 0f ? " (open " + openAngle + "°)" : " (shut)") + (fade != null ? ", fade rebuilt" : "");
        }

        // The top of the opening at x (portal space, -4.2..4.2), less a small clearance.
        private static float ArchTop(float x, float clearance)
        {
            float r = HalfSpan - clearance;
            float c = Mathf.Clamp(Mathf.Abs(x), 0f, r - 0.0001f);
            return Spring + Mathf.Sqrt(r * r - c * c);
        }

        // A leaf in its own space: the hinge at the origin, the leaf running along +x (west leaf) or -x (east leaf) to
        // the middle of the opening, its faces at z = ±thickness/2.
        private static Mesh LeafMesh(float side, int seed)
        {
            var rng = new System.Random(seed);
            var b = new LeafBuilder();
            float width = HalfSpan - 0.01f; // a 2 cm meeting gap
            float hinge = side * HalfSpan;  // portal x of the hinge
            float dir = -side;              // from the hinge toward the middle
            System.Func<float, float> local = px => (px - hinge) * 1f; // portal x to leaf x (leaf x = portal x - hinge)
            const float t = LeafThickness;
            const float clearance = 0.04f;

            // The board behind the planks' joints (fills every gap), then the planks.
            b.Extrude(Outline(hinge, dir, width, 0.01f, clearance + 0.02f, local), -t * 0.5f, -t * 0.5f + 0.03f, WoodDarkUv);
            const int planks = 6;
            float pw = width / planks;
            for (int i = 0; i < planks; i++)
            {
                float a = i * pw + 0.012f, c = (i + 1) * pw - 0.012f;
                float px0 = hinge + dir * a, px1 = hinge + dir * c;
                float proud = (float)rng.NextDouble() * 0.014f;
                var quad = new[]
                {
                    new Vector2(local(px0), 0.02f), new Vector2(local(px1), 0.02f),
                    new Vector2(local(px1), ArchTop(px1, clearance)), new Vector2(local(px0), ArchTop(px0, clearance)),
                };
                b.Extrude(quad, -t * 0.5f + 0.02f, t * 0.5f + proud, rng.NextDouble() < 0.5 ? WoodUv : WoodDarkUv);
            }

            // Iron straps across both faces at three heights, studs along them; a band along the arch.
            foreach (float y in new[] { 1.1f, 3.6f, 6.0f })
            {
                foreach (float face in new[] { -1f, 1f })
                {
                    float z0 = face * (t * 0.5f + 0.012f), z1 = face * (t * 0.5f + 0.045f);
                    float from = 0.06f, to = width - 0.08f;
                    var strap = new[]
                    {
                        new Vector2(local(hinge + dir * from), y - 0.11f), new Vector2(local(hinge + dir * to), y - 0.11f),
                        new Vector2(local(hinge + dir * to), y + 0.11f), new Vector2(local(hinge + dir * from), y + 0.11f),
                    };
                    b.Extrude(strap, Mathf.Min(z0, z1), Mathf.Max(z0, z1), IronUv);
                    for (float s = 0.25f; s < width - 0.15f; s += 0.52f)
                    {
                        float sx = local(hinge + dir * s);
                        b.Box(new Vector3(sx, y, face * (t * 0.5f + 0.06f)), new Vector3(0.07f, 0.07f, 0.035f), IronUv);
                    }
                }
            }

            // The band along the head: short straight runs following the curve, on both faces.
            const int segments = 12;
            for (int k = 0; k < segments; k++)
            {
                float u0 = (float)k / segments, u1 = (float)(k + 1) / segments;
                float px0 = hinge + dir * width * u0, px1 = hinge + dir * width * u1;
                if (Mathf.Abs(px0) > HalfSpan - 0.05f)
                    px0 = Mathf.Sign(px0) * (HalfSpan - 0.05f);
                float y0 = ArchTop(px0, clearance), y1 = ArchTop(px1, clearance);
                foreach (float face in new[] { -1f, 1f })
                {
                    float z0 = face * (t * 0.5f + 0.012f), z1 = face * (t * 0.5f + 0.04f);
                    var band = new[]
                    {
                        new Vector2(local(px0), Mathf.Max(Spring - 0.2f, y0 - 0.2f)), new Vector2(local(px1), Mathf.Max(Spring - 0.2f, y1 - 0.2f)),
                        new Vector2(local(px1), y1 - 0.02f), new Vector2(local(px0), y0 - 0.02f),
                    };
                    b.Extrude(band, Mathf.Min(z0, z1), Mathf.Max(z0, z1), IronUv);
                }
            }

            // A ring pull on each face, near the meeting edge: a plate and a ring of eight links.
            float rx = local(hinge + dir * (width - 0.55f));
            foreach (float face in new[] { -1f, 1f })
            {
                float z = face * (t * 0.5f + 0.03f);
                b.Box(new Vector3(rx, 3.2f, z), new Vector3(0.3f, 0.3f, 0.03f), IronUv);
                const float ring = 0.26f;
                for (int k = 0; k < 8; k++)
                {
                    float a0 = k / 8f * Mathf.PI * 2f, a1 = (k + 1) / 8f * Mathf.PI * 2f;
                    var p0 = new Vector3(rx + Mathf.Sin(a0) * ring, 3.2f - ring - Mathf.Cos(a0) * ring, face * (t * 0.5f + 0.07f));
                    var p1 = new Vector3(rx + Mathf.Sin(a1) * ring, 3.2f - ring - Mathf.Cos(a1) * ring, face * (t * 0.5f + 0.07f));
                    b.Bar(p0, p1, 0.045f, IronUv);
                }
            }

            return b.ToMesh();
        }

        // The leaf's outline (hinge to middle, floor to the arch), in leaf space.
        private static Vector2[] Outline(float hinge, float dir, float width, float bottom, float clearance, System.Func<float, float> local)
        {
            var points = new List<Vector2> { new Vector2(local(hinge), bottom), new Vector2(local(hinge + dir * width), bottom) };
            const int steps = 16;
            for (int k = steps; k >= 0; k--)
            {
                float px = hinge + dir * width * k / steps;
                points.Add(new Vector2(local(px), ArchTop(px, clearance)));
            }

            return points.ToArray();
        }

        // Flat-shaded low-poly pieces (the pack's look): every face its own vertices, one atlas UV per piece.
        private class LeafBuilder
        {
            private readonly List<Vector3> v = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> tris = new List<int>();

            // A face from a convex (or star-shaped from its first point) polygon, wound to face `normal`.
            private void Face(IList<Vector3> points, Vector3 normal, Vector2 u)
            {
                int start = v.Count;
                foreach (Vector3 p in points)
                {
                    v.Add(p);
                    n.Add(normal);
                    uv.Add(u);
                }

                for (int i = 1; i < points.Count - 1; i++)
                {
                    Vector3 a = points[0], b = points[i], c = points[i + 1];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
                        tris.AddRange(new[] { start, start + i, start + i + 1 });
                    else
                        tris.AddRange(new[] { start, start + i + 1, start + i });
                }
            }

            // A polygon in xy, pushed from z0 to z1: both faces and the sides.
            public void Extrude(IList<Vector2> poly, float z0, float z1, Vector2 u)
            {
                var front = new List<Vector3>();
                var back = new List<Vector3>();
                foreach (Vector2 p in poly)
                {
                    front.Add(new Vector3(p.x, p.y, z1));
                    back.Add(new Vector3(p.x, p.y, z0));
                }

                Face(front, Vector3.forward, u);
                Face(back, Vector3.back, u);
                Vector2 centre = Vector2.zero;
                foreach (Vector2 p in poly)
                    centre += p;
                centre /= poly.Count;
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                    if ((b - a).sqrMagnitude < 1e-8f)
                        continue;
                    Vector2 edge = (b - a).normalized;
                    var outward = new Vector3(edge.y, -edge.x, 0f);
                    if (Vector2.Dot(new Vector2(outward.x, outward.y), (a + b) * 0.5f - centre) < 0f)
                        outward = -outward;
                    Face(new[] { new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1) }, outward, u);
                }
            }

            public void Box(Vector3 centre, Vector3 size, Vector2 u)
            {
                Vector3 h = size * 0.5f;
                var square = new[]
                {
                    new Vector2(centre.x - h.x, centre.y - h.y), new Vector2(centre.x + h.x, centre.y - h.y),
                    new Vector2(centre.x + h.x, centre.y + h.y), new Vector2(centre.x - h.x, centre.y + h.y),
                };
                Extrude(square, centre.z - h.z, centre.z + h.z, u);
            }

            // A square pyramid standing on `baseCentre` (its base in the xz plane), `height` tall.
            public void Pyramid(Vector3 baseCentre, float baseSize, float height, Vector2 u)
            {
                float h = baseSize * 0.5f;
                Vector3 apex = baseCentre + Vector3.up * height;
                var corners = new[]
                {
                    baseCentre + new Vector3(-h, 0f, -h), baseCentre + new Vector3(h, 0f, -h),
                    baseCentre + new Vector3(h, 0f, h), baseCentre + new Vector3(-h, 0f, h),
                };
                Face(corners, Vector3.down, u);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = corners[i], b = corners[(i + 1) % 4];
                    Vector3 mid = (a + b) * 0.5f - baseCentre;
                    Vector3 normal = Vector3.Cross(b - a, apex - a).normalized;
                    if (Vector3.Dot(normal, mid) < 0f)
                        normal = -normal;
                    Face(new[] { a, b, apex }, normal, u);
                }
            }

            // A square bar from p0 to p1 (in a plane of constant z).
            public void Bar(Vector3 p0, Vector3 p1, float thick, Vector2 u)
            {
                Vector2 d = new Vector2(p1.x - p0.x, p1.y - p0.y).normalized;
                Vector2 side = new Vector2(-d.y, d.x) * thick * 0.5f;
                Vector2 a = new Vector2(p0.x, p0.y) - d * thick * 0.3f, b = new Vector2(p1.x, p1.y) + d * thick * 0.3f;
                Extrude(new[] { a - side, b - side, b + side, a + side }, p0.z - thick * 0.5f, p0.z + thick * 0.5f, u);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
