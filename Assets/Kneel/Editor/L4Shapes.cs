using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Faceted low-poly shapes for L4's built props, on L3Mesh (flat-shaded, one palette cell per face). Every
    // triangle is wound against an explicit outward direction, so the stored normal and the rendered front face
    // always agree (a face whose normal is cross(b - a, c - a) renders from that side).
    public static class L4Shapes
    {
        // The props palette: iron, bronze, gold, clay, rope and pitch.
        public const int IronDark = 0, Iron = 1, BronzeDark = 2, Bronze = 3, Gold = 4, Clay = 5, Rope = 6, Pitch = 7;

        public static Material Props => AssetDatabase.LoadAssetAtPath<Material>(L4Build.MaterialsPath + "/L4_Palette_Props.mat") ?? BuildPropsPalette();

        public static Material BuildPropsPalette()
        {
            var tones = new[] { "#1E1C1B", "#34312E", "#2E261E", "#54402C", "#8E6B28", "#6A5644", "#5E4F3B", "#0D0B0A" };
            var colors = new Color[tones.Length];
            for (int i = 0; i < tones.Length; i++)
            {
                colors[i] = L1Build.Hex(tones[i]);
            }

            var mat = L4Kit.Palette("L4_Palette_Props", colors, 0.25f);
            mat.SetFloat("_Metallic", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void Tri(L3Mesh m, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Vector2 uv)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            {
                m.Tri(sub, a, c, b, uv);
            }
            else
            {
                m.Tri(sub, a, b, c, uv);
            }
        }

        public static void Quad(L3Mesh m, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Vector2 uv)
        {
            Tri(m, sub, a, b, c, outward, uv);
            Tri(m, sub, a, c, d, outward, uv);
        }

        // A surface of revolution about the local y axis. 'profile' is (radius, height) traversed counter-clockwise
        // round the solid's cross-section (bottom outward, up the outside, over the rim, down the inside), so the
        // outward side is to the right of each step. 'skip' leaves one segment open (a crack).
        public static void Lathe(L3Mesh m, int sub, Matrix4x4 frame, Vector2[] profile, int segments, Vector2 uv, int skip = -1, float phase = 0f)
        {
            for (int i = 0; i < segments; i++)
            {
                if (i == skip)
                {
                    continue;
                }

                float a0 = phase + i * Mathf.PI * 2f / segments, a1 = phase + (i + 1) * Mathf.PI * 2f / segments;
                float am = (a0 + a1) * 0.5f;
                var radial = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                for (int j = 0; j < profile.Length - 1; j++)
                {
                    Vector2 p0 = profile[j], p1 = profile[j + 1];
                    var d = p1 - p0;
                    if (d.sqrMagnitude < 1e-8f)
                    {
                        continue;
                    }

                    var n2 = new Vector2(d.y, -d.x).normalized;
                    Vector3 outward = frame.MultiplyVector(radial * n2.x + Vector3.up * n2.y);
                    Vector3 P(Vector2 p, float a) => frame.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                    if (p0.x < 1e-4f)
                    {
                        Tri(m, sub, P(p0, a0), P(p1, a0), P(p1, a1), outward, uv);
                    }
                    else if (p1.x < 1e-4f)
                    {
                        Tri(m, sub, P(p0, a0), P(p0, a1), P(p1, a0), outward, uv);
                    }
                    else
                    {
                        Quad(m, sub, P(p0, a0), P(p0, a1), P(p1, a1), P(p1, a0), outward, uv);
                    }
                }
            }
        }

        // A faceted shaft from a to b (radius ra at a, rb at b), capped.
        public static void Shaft(L3Mesh m, int sub, Vector3 a, Vector3 b, float ra, float rb, int sides, Vector2 uv)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 d = axis.normalized;
            Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(d, u);
            for (int i = 0; i < sides; i++)
            {
                float t0 = i * Mathf.PI * 2f / sides, t1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 o0 = u * Mathf.Cos(t0) + w * Mathf.Sin(t0), o1 = u * Mathf.Cos(t1) + w * Mathf.Sin(t1);
                Vector3 om = (o0 + o1).normalized;
                Quad(m, sub, a + o0 * ra, a + o1 * ra, b + o1 * rb, b + o0 * rb, om, uv);
                Tri(m, sub, a, a + o0 * ra, a + o1 * ra, -d, uv);
                Tri(m, sub, b, b + o1 * rb, b + o0 * rb, d, uv);
            }
        }

        // A box with its own rotation, by centre and size.
        public static void Box(L3Mesh m, int sub, Vector3 centre, Quaternion rot, Vector3 size, Vector2 uv)
        {
            Vector3 h = size * 0.5f;
            Vector3 C(float x, float y, float z) => centre + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            Quad(m, sub, C(-1, -1, -1), C(1, -1, -1), C(1, 1, -1), C(-1, 1, -1), rot * Vector3.back, uv);
            Quad(m, sub, C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1), rot * Vector3.forward, uv);
            Quad(m, sub, C(-1, -1, -1), C(-1, -1, 1), C(-1, 1, 1), C(-1, 1, -1), rot * Vector3.left, uv);
            Quad(m, sub, C(1, -1, -1), C(1, -1, 1), C(1, 1, 1), C(1, 1, -1), rot * Vector3.right, uv);
            Quad(m, sub, C(-1, 1, -1), C(1, 1, -1), C(1, 1, 1), C(-1, 1, 1), rot * Vector3.up, uv);
            Quad(m, sub, C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1), rot * Vector3.down, uv);
        }

        // A coffin: the six-sided outline (narrow at the foot, widest at the shoulders), extruded 'height'.
        // Lies along local z, foot at -z, base at y 0.
        public static void Coffin(L3Mesh m, int sub, Matrix4x4 frame, float length, float shoulder, float height, Vector2 side, Vector2 top)
        {
            float foot = shoulder * 0.55f, head = shoulder * 0.75f;
            var outline = new[]
            {
                new Vector2(-foot * 0.5f, -length * 0.5f), new Vector2(foot * 0.5f, -length * 0.5f), new Vector2(shoulder * 0.5f, length * 0.22f),
                new Vector2(head * 0.5f, length * 0.5f), new Vector2(-head * 0.5f, length * 0.5f), new Vector2(-shoulder * 0.5f, length * 0.22f),
            };
            Vector3 P(Vector2 p, float y) => frame.MultiplyPoint3x4(new Vector3(p.x, y, p.y));
            var centre = frame.MultiplyPoint3x4(new Vector3(0f, height * 0.5f, 0f));
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                var mid = (a + b) * 0.5f;
                var outward = frame.MultiplyVector(new Vector3(mid.x, 0f, mid.y));
                Quad(m, sub, P(a, 0f), P(b, 0f), P(b, height), P(a, height), outward, side);
                Tri(m, sub, P(Vector2.zero, height), P(a, height), P(b, height), frame.MultiplyVector(Vector3.up), top);
                Tri(m, sub, P(Vector2.zero, 0f), P(a, 0f), P(b, 0f), frame.MultiplyVector(Vector3.down), side);
            }
        }

        // The coffin's six-sided outline, foot at -z: narrow foot, broad shoulders a fifth of the way from the head.
        public static Vector2[] CoffinOutline(float length, float shoulder)
        {
            float foot = shoulder * 0.58f, head = shoulder * 0.74f, sz = length * 0.22f;
            return new[]
            {
                new Vector2(-foot * 0.5f, -length * 0.5f), new Vector2(foot * 0.5f, -length * 0.5f), new Vector2(shoulder * 0.5f, sz),
                new Vector2(head * 0.5f, length * 0.5f), new Vector2(-head * 0.5f, length * 0.5f), new Vector2(-shoulder * 0.5f, sz),
            };
        }

        // An open coffin box: outer walls, inner walls 'wall' thick, the rim between them, the floor inside and the
        // underside. Base at y 0 in 'frame'.
        public static void CoffinShell(L3Mesh m, int sub, Matrix4x4 frame, float length, float shoulder, float height, float wall, Vector2 outerUV, Vector2 innerUV, Vector2 rimUV)
        {
            var outer = CoffinOutline(length, shoulder);
            var inner = CoffinOutline(length - wall * 2f, shoulder - wall * 2f);
            Vector3 P(Vector2 p, float y) => frame.MultiplyPoint3x4(new Vector3(p.x, y, p.y));
            var up = frame.MultiplyVector(Vector3.up);
            for (int i = 0; i < outer.Length; i++)
            {
                int j = (i + 1) % outer.Length;
                var mid = (outer[i] + outer[j]) * 0.5f;
                var outward = frame.MultiplyVector(new Vector3(mid.x, 0f, mid.y));
                Quad(m, sub, P(outer[i], 0f), P(outer[j], 0f), P(outer[j], height), P(outer[i], height), outward, outerUV);
                Quad(m, sub, P(inner[i], wall), P(inner[j], wall), P(inner[j], height), P(inner[i], height), -outward, innerUV);
                Quad(m, sub, P(outer[i], height), P(outer[j], height), P(inner[j], height), P(inner[i], height), up, rimUV);
                Tri(m, sub, P(Vector2.zero, wall), P(inner[i], wall), P(inner[j], wall), up, innerUV);
                Tri(m, sub, P(Vector2.zero, 0f), P(outer[i], 0f), P(outer[j], 0f), -up, outerUV);
            }
        }

        // Keeps only the pieces of 'm' (triangles joined through shared corners) that reach down to within
        // 'reach' of y 'floor' and stay within 'radius' of the y axis: what is left standing after a cut,
        // without the hand or hilt that would float beside it.
        public static L3Mesh KeepGrounded(L3Mesh m, float floor, float reach, float radius = float.MaxValue)
        {
            var parent = new int[m.P.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Root(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            // Corners at the same place are one corner (meshes split vertices along hard edges and seams).
            var first = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < m.P.Count; i++)
            {
                var key = Vector3Int.RoundToInt(m.P[i] * 1000f);
                if (first.TryGetValue(key, out int j))
                {
                    parent[Root(i)] = Root(j);
                }
                else
                {
                    first[key] = i;
                }
            }

            foreach (var list in m.Tris)
            {
                for (int k = 0; k < list.Count; k += 3)
                {
                    parent[Root(list[k + 1])] = Root(list[k]);
                    parent[Root(list[k + 2])] = Root(list[k]);
                }
            }

            var lowest = new Dictionary<int, float>();
            var widest = new Dictionary<int, float>();
            for (int i = 0; i < m.P.Count; i++)
            {
                int r = Root(i);
                float d = new Vector2(m.P[i].x, m.P[i].z).magnitude;
                lowest[r] = lowest.TryGetValue(r, out float y) ? Mathf.Min(y, m.P[i].y) : m.P[i].y;
                widest[r] = widest.TryGetValue(r, out float w) ? Mathf.Max(w, d) : d;
            }

            var kept = new L3Mesh();
            kept.Tris.Clear();
            var remap = new Dictionary<int, int>();
            foreach (var list in m.Tris)
            {
                var tris = new List<int>();
                for (int k = 0; k < list.Count; k += 3)
                {
                    int root = Root(list[k]);
                    if (lowest[root] > floor + reach || widest[root] > radius)
                    {
                        continue;
                    }

                    for (int c = 0; c < 3; c++)
                    {
                        int v = list[k + c];
                        if (remap.TryGetValue(v, out int n) == false)
                        {
                            n = kept.P.Count;
                            kept.P.Add(m.P[v]);
                            kept.N.Add(m.N[v]);
                            kept.UV.Add(m.UV[v]);
                            remap[v] = n;
                        }

                        tris.Add(n);
                    }
                }

                kept.Tris.Add(tris);
            }

            return kept;
        }

        // Closes a mesh cut by L3Mesh.Clip across y = value (a hollow shell otherwise shows its inside). The cut's
        // outline points are grouped into islands (two legs make two), each island capped by a rough break: a
        // fan from a centre pushed a few centimetres out of the cut, so it reads as broken stone or metal.
        public static void CapCut(L3Mesh m, int sub, float value, bool facingUp, Vector2 uv, int seed, float rough = 0.05f)
        {
            var points = new List<Vector3>();
            foreach (var p in m.P)
            {
                if (Mathf.Abs(p.y - value) < 1e-3f && points.TrueForAll(q => (q - p).sqrMagnitude > 1e-6f))
                {
                    points.Add(p);
                }
            }

            // Islands: points within 0.15 m of each other (union-find).
            var parent = new int[points.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Root(int i) => parent[i] == i ? i : (parent[i] = Root(parent[i]));
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    var d = points[i] - points[j];
                    d.y = 0f;
                    if (d.sqrMagnitude < 0.0225f)
                    {
                        parent[Root(i)] = Root(j);
                    }
                }
            }

            var islands = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < points.Count; i++)
            {
                int r = Root(i);
                if (!islands.TryGetValue(r, out var list))
                {
                    islands[r] = list = new List<Vector3>();
                }

                list.Add(points[i]);
            }

            var rng = new System.Random(seed);
            var outward = facingUp ? Vector3.up : Vector3.down;
            foreach (var island in islands.Values)
            {
                var hull = Hull(island);
                if (hull.Count < 3)
                {
                    continue;
                }

                var centre = Vector3.zero;
                foreach (var h in hull)
                {
                    centre += h;
                }

                centre /= hull.Count;
                centre += outward * rough * (0.5f + (float)rng.NextDouble());
                // A ring of jagged points between the rim and the centre makes the break faceted.
                var mid = new List<Vector3>();
                foreach (var h in hull)
                {
                    mid.Add(Vector3.Lerp(h, centre, 0.45f) + outward * rough * ((float)rng.NextDouble() - 0.3f));
                }

                for (int i = 0; i < hull.Count; i++)
                {
                    int j = (i + 1) % hull.Count;
                    Quad(m, sub, hull[i], hull[j], mid[j], mid[i], outward, uv);
                    Tri(m, sub, mid[i], mid[j], centre, outward, uv);
                }
            }
        }

        // Convex hull in x/z (monotone chain), keeping y.
        private static List<Vector3> Hull(List<Vector3> pts)
        {
            var sorted = new List<Vector3>(pts);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z));
            float Cross(Vector3 o, Vector3 a, Vector3 b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
            var hull = new List<Vector3>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int k = 0; k < sorted.Count; k++)
                {
                    var p = pass == 0 ? sorted[k] : sorted[sorted.Count - 1 - k];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }

                    hull.Add(p);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        // An iron chain from a to b, sagging by 'sag' at its middle: oval links, each turned a quarter from the last.
        public static void Chain(L3Mesh m, int sub, Vector3 a, Vector3 b, float sag, Vector2 uv, float link = 0.075f, float wire = 0.011f)
        {
            float length = Vector3.Distance(a, b) + sag;
            int n = Mathf.Max(2, Mathf.RoundToInt(length / (link * 0.8f)));
            Vector3 Point(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n;
                Vector3 p0 = Point(t0), p1 = Point(t1);
                var dir = (p1 - p0).normalized;
                var side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                if (i % 2 == 1)
                {
                    side = Vector3.Cross(dir, side).normalized;
                }

                var c = (p0 + p1) * 0.5f;
                float half = Vector3.Distance(p0, p1) * 0.62f, width = link * 0.32f;
                const int seg = 6;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    var q0 = c + dir * Mathf.Cos(a0) * half + side * Mathf.Sin(a0) * width;
                    var q1 = c + dir * Mathf.Cos(a1) * half + side * Mathf.Sin(a1) * width;
                    Shaft(m, sub, q0, q1, wire, wire, 4, uv);
                }
            }
        }

        public static Mesh Save(L3Mesh m, string folder, string name)
        {
            var mesh = m.ToMesh(name);
            L2Build.EnsureFolder(L4Build.MeshesPath + "/" + folder);
            string path = L4Build.MeshesPath + "/" + folder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
