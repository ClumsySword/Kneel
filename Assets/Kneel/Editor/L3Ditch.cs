using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // What the ditch looks like. Its outline, depth, colliders and kill volume are the plan's (a 6 m cut,
    // 3 m deep) and are not drawn; this draws what a dug ditch becomes after years of weather. Earth banks
    // break away from the lip, slump toward the water and stand in it as a foot that wanders in and out, so
    // the water is a winding channel between them, never a rectangle; and at both ends, outside the play
    // space, the cut shallows out and closes. The banks carry the ground's own material and paint, so the
    // land rolls over the lip into the ditch without a change of surface. No colliders.
    public static class L3Ditch
    {
        public const float WaterY = -2.55f;

        private static float Hash(int column, float side, int k)
        {
            return L3Level.Hash("ditchbank" + (side < 0f ? "S" : "N"), column * 16 + k);
        }

        // Heights are the same on both banks at a given x, so where the banks meet they meet at one level.
        private static float Level(int column, int k)
        {
            return L3Level.Hash("ditchlevel", column * 16 + k);
        }

        // How far the foot of the bank stands in from the ditch's edge at x (side -1 south, +1 north).
        public static float Toe(float x, float side)
        {
            var d = L3Plan.Ditch;
            float toe = 0.55f + 2.1f * L3Ground.Fbm(x * 0.085f + side * 7.3f, 3.1f + side) + 0.6f * (Mathf.PerlinNoise(x * 0.37f, 9f + side * 2f) - 0.5f);
            // The banks close in until they meet at each end of the cut.
            float half = (d.Z1 - d.Z0) * 0.5f;
            return Mathf.Lerp(half + 0.25f, Mathf.Clamp(toe, 0.8f, 2.6f), L3Ground.Step(0.5f, 7f, Mathf.Min(x - d.X0, d.X1 - x)));
        }

        // How deep the cut is at x, as a share of its full depth: it shallows out toward both ends.
        private static float Depth(float x)
        {
            var d = L3Plan.Ditch;
            return L3Ground.Step(0f, 4.5f, Mathf.Min(x - d.X0, d.X1 - x));
        }

        public static GameObject Build(Transform parent)
        {
            var d = L3Plan.Ditch;
            var banks = new L3Mesh();
            var lumps = new L3Mesh();
            const int rows = 6;
            int columns = Mathf.RoundToInt(d.X1 - d.X0);

            foreach (float side in new[] { -1f, 1f })
            {
                float edge = side < 0f ? d.Z0 : d.Z1;
                var grid = new Vector3[columns + 1, rows];
                for (int i = 0; i <= columns; i++)
                {
                    // Unevenly spaced along the cut (the same places on both banks).
                    float x = d.X0 + (d.X1 - d.X0) * (i + (i == 0 || i == columns ? 0f : (Level(i, 12) - 0.5f) * 0.6f)) / columns;
                    float toe = Toe(x, side), deep = Depth(x);
                    // From the lip down: the broken edge, the first fall, the slump, the foot, and on under the water.
                    var inward = new float[rows];
                    var y = new float[rows];
                    inward[0] = 0.02f + 0.24f * Hash(i, side, 0);
                    y[0] = -0.004f;
                    inward[1] = inward[0] + 0.24f + 0.3f * Hash(i, side, 1);
                    y[1] = -0.36f - 0.32f * Level(i, 2);
                    inward[2] = Mathf.Max(inward[1] + 0.18f, toe * (0.42f + 0.22f * Hash(i, side, 3)));
                    y[2] = -1.3f - 0.5f * Level(i, 4);
                    inward[3] = Mathf.Max(inward[2] + 0.15f, toe * (0.8f + 0.14f * Hash(i, side, 5)));
                    y[3] = -2.22f - 0.2f * Level(i, 6);
                    inward[4] = Mathf.Max(inward[3] + 0.1f, toe + 0.05f);
                    y[4] = WaterY - 0.09f;
                    inward[5] = inward[4] + 0.9f;
                    y[5] = -3.05f;
                    float half = (d.Z1 - d.Z0) * 0.5f;
                    for (int r = 0; r < rows; r++)
                    {
                        // Neither bank crosses the centre line. The ends of the cut and the line where the banks
                        // meet stay true; everywhere else every point is a little out of place.
                        bool meets = inward[r] >= half;
                        inward[r] = Mathf.Min(inward[r], half);
                        float along = i == 0 || i == columns || r == 0 || meets ? 0f : (Hash(i, side, 8 + r) - 0.5f) * 0.4f;
                        grid[i, r] = new Vector3(x + along, y[r] * deep, edge - side * inward[r]);
                    }
                }

                for (int i = 0; i < columns; i++)
                {
                    for (int r = 0; r + 1 < rows; r++)
                    {
                        Vector3 a = grid[i, r], b = grid[i + 1, r], c = grid[i + 1, r + 1], e = grid[i, r + 1];
                        // Each quad is split one way or the other by chance, so the facets make no pattern.
                        if (Hash(i, side, 14 - r) < 0.5f)
                        {
                            Face(banks, a, b, c);
                            Face(banks, a, c, e);
                        }
                        else
                        {
                            Face(banks, a, b, e);
                            Face(banks, b, c, e);
                        }
                    }
                }

                // Earth that has slid to the foot of the bank and lies half in the water.
                int seed = 0;
                float Rand()
                {
                    return L3Level.Hash("ditchlump" + side, seed++);
                }

                float at = d.X0 + 6f;
                while (at < d.X1 - 6f)
                {
                    float toe = Toe(at, side);
                    var rotation = Quaternion.Euler((Rand() - 0.5f) * 24f, Rand() * 360f, (Rand() - 0.5f) * 24f);
                    var size = new Vector3(Mathf.Lerp(0.3f, 0.75f, Rand()), Mathf.Lerp(0.12f, 0.3f, Rand()), Mathf.Lerp(0.25f, 0.5f, Rand()));
                    lumps.Ellipsoid(0, new Vector3(at, WaterY + Mathf.Lerp(-0.04f, 0.12f, Rand()), edge - side * (toe + Mathf.Lerp(-0.35f, 0.3f, Rand()))), rotation, size, 6, 3, Vector2.zero);
                    at += Mathf.Lerp(0.8f, 3.2f, Rand());
                }
            }

            var go = L3Build.MeshChild(parent, "Banks", L3Build.SaveMesh(banks, "Level", "L3_Ditch_Banks"), Vector3.zero, L3Ground.Material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var silt = L3Build.MeshChild(parent, "Silt (earth slid to the waterline)", L3Build.SaveMesh(lumps, "Mud", "L3_Mud_Ditch"), Vector3.zero, L3Mud.ClodMaterial);
            silt.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // One flat facet, facing up and out of the bank, with the ground's layout UVs.
        private static void Face(L3Mesh mesh, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-8f)
            {
                return;
            }

            if (normal.y < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }

            mesh.Tri(0, a, b, c, L3Ground.Uv(a.x, a.z), L3Ground.Uv(b.x, b.z), L3Ground.Uv(c.x, c.z), normal.normalized);
        }

        // The project's water shader with L2's creek settings, but not L2's colour: that is water under a moon,
        // near black, lit by lanterns. This lies under a dawn sky at the bottom of a cut, so its murk is a
        // cold grey-green that still reads as water in shadow, with a little more of the sky on it.
        public static Material WaterMaterial()
        {
            string path = L3Build.MaterialsPath + "/L3_Ditch_Water.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(L2Build.MaterialsPath + "/L2_Creek_Water.mat", path);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            mat.SetColor("_DeepColor", new Color(0.21f, 0.225f, 0.22f));
            mat.SetColor("_ScumColor", new Color(0.4f, 0.38f, 0.31f));
            mat.SetFloat("_Reflection", 0.8f);
            mat.SetFloat("_Glint", 0.45f);
            mat.SetFloat("_RippleStrength", 0.11f);
            mat.SetFloat("_ShoreFade", 0.18f);
            mat.SetFloat("_HalfWidth", 3.3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The water: a flat sheet with what the project's water shader asks for (uv0 = world x and z in metres,
        // uv1.x = distance from the centre line). The shader fades it out on depth where it meets the banks,
        // so its shore is the banks' foot, not the sheet's edge.
        public static Mesh Water()
        {
            var d = L3Plan.Ditch;
            const float cellX = 2f, cellZ = 1f;
            int nx = Mathf.RoundToInt((d.X1 - d.X0) / cellX), nz = Mathf.RoundToInt((d.Z1 - d.Z0) / cellZ);
            float centre = (d.Z0 + d.Z1) * 0.5f;
            var verts = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var normals = new List<Vector3>();
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    float x = d.X0 + i * cellX, z = d.Z0 + j * cellZ;
                    verts.Add(new Vector3(x, WaterY, z));
                    uv0.Add(new Vector2(x, z));
                    uv1.Add(new Vector2(Mathf.Abs(z - centre), 0f));
                    normals.Add(Vector3.up);
                }
            }

            var tris = new List<int>();
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, e = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, e });   // clockwise seen from above
                }
            }

            var mesh = new Mesh { name = "L3_Ditch_Water" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
