using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Mesh surgery for L3's baked pieces: cut a copy of a pack mesh cleanly along axis planes (a house into
    // wall panels and a roof), move the parts about and merge them into a new mesh of our own. The pack mesh
    // is only ever read.
    public class L3Mesh
    {
        public readonly List<Vector3> P = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();

        // One triangle list per material.
        public readonly List<List<int>> Tris = new List<List<int>> { new List<int>() };

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (var list in Tris)
                {
                    n += list.Count / 3;
                }

                return n;
            }
        }

        public static L3Mesh From(Mesh mesh)
        {
            var data = new L3Mesh();
            var v = mesh.vertices;
            var n = mesh.normals;
            var uv = mesh.uv;
            for (int i = 0; i < v.Length; i++)
            {
                data.P.Add(v[i]);
                data.N.Add(n.Length > i ? n[i] : Vector3.up);
                data.UV.Add(uv.Length > i ? uv[i] : Vector2.zero);
            }

            data.Tris.Clear();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                data.Tris.Add(new List<int>(mesh.GetTriangles(s)));
            }

            return data;
        }

        private int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            P.Add(p);
            N.Add(n);
            UV.Add(uv);
            return P.Count - 1;
        }

        private List<int> Sub(int index)
        {
            while (Tris.Count <= index)
            {
                Tris.Add(new List<int>());
            }

            return Tris[index];
        }

        // A flat-shaded triangle of one palette colour (uv), facing by its winding.
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 uv)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            var list = Sub(sub);
            list.Add(AddVertex(a, normal, uv));
            list.Add(AddVertex(b, normal, uv));
            list.Add(AddVertex(c, normal, uv));
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector3 normal)
        {
            var list = Sub(sub);
            list.Add(AddVertex(a, normal, uva));
            list.Add(AddVertex(b, normal, uvb));
            list.Add(AddVertex(c, normal, uvc));
        }

        // The part of this mesh on one side of an axis plane (axis 0 = x, 1 = y, 2 = z). Triangles that cross
        // the plane are cut exactly, so the edge is clean.
        public L3Mesh Clip(int axis, float value, bool keepBelow)
        {
            var result = new L3Mesh();
            result.Tris.Clear();
            var polygon = new List<(Vector3 p, Vector3 n, Vector2 uv)>(4);
            for (int s = 0; s < Tris.Count; s++)
            {
                var source = Tris[s];
                var target = result.Sub(s);
                for (int t = 0; t < source.Count; t += 3)
                {
                    polygon.Clear();
                    for (int k = 0; k < 3; k++)
                    {
                        int i = source[t + k], j = source[t + (k + 1) % 3];
                        float di = (P[i][axis] - value) * (keepBelow ? 1f : -1f), dj = (P[j][axis] - value) * (keepBelow ? 1f : -1f);
                        if (di <= 0f)
                        {
                            polygon.Add((P[i], N[i], UV[i]));
                        }

                        if ((di < 0f && dj > 0f) || (di > 0f && dj < 0f))
                        {
                            float f = di / (di - dj);
                            polygon.Add((Vector3.Lerp(P[i], P[j], f), Vector3.Lerp(N[i], N[j], f).normalized, Vector2.Lerp(UV[i], UV[j], f)));
                        }
                    }

                    for (int k = 1; k + 1 < polygon.Count; k++)
                    {
                        target.Add(result.AddVertex(polygon[0].p, polygon[0].n, polygon[0].uv));
                        target.Add(result.AddVertex(polygon[k].p, polygon[k].n, polygon[k].uv));
                        target.Add(result.AddVertex(polygon[k + 1].p, polygon[k + 1].n, polygon[k + 1].uv));
                    }
                }
            }

            return result;
        }

        // Only the triangles that pass 'keep' (given each triangle's centre and face normal).
        public L3Mesh Where(System.Func<Vector3, Vector3, bool> keep)
        {
            var result = new L3Mesh();
            result.Tris.Clear();
            for (int s = 0; s < Tris.Count; s++)
            {
                var source = Tris[s];
                var target = result.Sub(s);
                for (int t = 0; t < source.Count; t += 3)
                {
                    Vector3 a = P[source[t]], b = P[source[t + 1]], c = P[source[t + 2]];
                    if (!keep((a + b + c) / 3f, Vector3.Cross(b - a, c - a).normalized))
                    {
                        continue;
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        int i = source[t + k];
                        target.Add(result.AddVertex(P[i], N[i], UV[i]));
                    }
                }
            }

            return result;
        }

        public L3Mesh Between(int axis, float min, float max)
        {
            return Clip(axis, max, true).Clip(axis, min, false);
        }

        // Adds another mesh, moved by 'matrix'. Its triangles all go to material slot 'sub' (or keep their own
        // slots when sub is negative). A mirroring matrix has its winding turned back the right way round.
        public void Append(L3Mesh other, Matrix4x4 matrix, int sub = -1)
        {
            bool mirrored = matrix.determinant < 0f;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            int offset = P.Count;
            for (int i = 0; i < other.P.Count; i++)
            {
                P.Add(matrix.MultiplyPoint3x4(other.P[i]));
                N.Add(normalMatrix.MultiplyVector(other.N[i]).normalized);
                UV.Add(other.UV[i]);
            }

            for (int s = 0; s < other.Tris.Count; s++)
            {
                var target = Sub(sub < 0 ? s : sub);
                var source = other.Tris[s];
                for (int t = 0; t < source.Count; t += 3)
                {
                    target.Add(source[t] + offset);
                    target.Add(source[t + (mirrored ? 2 : 1)] + offset);
                    target.Add(source[t + (mirrored ? 1 : 2)] + offset);
                }
            }
        }

        public Bounds Bounds()
        {
            var b = new Bounds(P.Count > 0 ? P[0] : Vector3.zero, Vector3.zero);
            foreach (var p in P)
            {
                b.Encapsulate(p);
            }

            return b;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(P);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.subMeshCount = Tris.Count;
            for (int s = 0; s < Tris.Count; s++)
            {
                mesh.SetTriangles(Tris[s], s);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- Faceted shapes

        // A tapered, faceted shaft from a to b, capped at both ends.
        public void Prism(int sub, Vector3 a, Vector3 b, float ra, float rb, int sides, Vector2 uv)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector3 d = axis.normalized;
            Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(d, u);
            var ringA = new Vector3[sides];
            var ringB = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                Vector3 o = u * Mathf.Cos(angle) + w * Mathf.Sin(angle);
                ringA[i] = a + o * ra;
                ringB[i] = b + o * rb;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Tri(sub, ringA[i], ringB[i], ringB[j], uv);
                Tri(sub, ringA[i], ringB[j], ringA[j], uv);
                Tri(sub, a, ringA[i], ringA[j], uv);
                Tri(sub, b, ringB[j], ringB[i], uv);
            }
        }

        public void Ellipsoid(int sub, Vector3 centre, Quaternion rotation, Vector3 radii, int lon, int lat, Vector2 uv)
        {
            Vector3 Point(int i, int j)
            {
                float theta = j / (float)lat * Mathf.PI;
                float phi = i / (float)lon * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi) * radii.x, Mathf.Cos(theta) * radii.y, Mathf.Sin(theta) * Mathf.Sin(phi) * radii.z);
                return centre + rotation * local;
            }

            for (int j = 0; j < lat; j++)
            {
                for (int i = 0; i < lon; i++)
                {
                    Vector3 a = Point(i, j), b = Point(i + 1, j), c = Point(i + 1, j + 1), d = Point(i, j + 1);
                    if (j > 0)
                    {
                        Tri(sub, a, b, c, uv);
                    }

                    if (j < lat - 1)
                    {
                        Tri(sub, a, c, d, uv);
                    }
                }
            }
        }

        // A flat polygon fan round 'centre', facing 'normal'.
        public void Disc(int sub, Vector3 centre, Vector3 normal, float radius, int sides, Vector2 uv)
        {
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(normal, u);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Tri(sub, centre, centre + (u * Mathf.Cos(a0) + w * Mathf.Sin(a0)) * radius, centre + (u * Mathf.Cos(a1) + w * Mathf.Sin(a1)) * radius, uv);
            }
        }
    }
}
