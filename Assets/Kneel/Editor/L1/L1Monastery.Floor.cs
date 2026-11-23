using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.EditorTools
{
    // The monastery's floors: flat meshes under a painted flagstone texture, not thousands of separate flag blocks.
    // Seen from the gameplay camera, every block's hair-thin joint and chamfered edge was a line 1-2 px wide, and they
    // crawled and flashed as the camera moved (about ten times the battlefield's flicker, measured with
    // L1MonasteryFlicker). A texture is mipmapped: its grout lines soften with distance instead of aliasing.
    public static partial class L1Monastery
    {
        private const float FloorTile = 4f;          // metres one repeat of the texture covers
        private const int FloorTexSize = 1024;

        private static Material FloorMaterial()
        {
            string texPath = MatFolder + "/L1_Mon_Flagstones.png";
            var rng = new System.Random(3301);
            float ppm = FloorTexSize / FloorTile;

            // Courses across v, each with stones across u; both sum to the tile, so it repeats seamlessly.
            var courses = Split(rng, FloorTile, 0.62f, 0.95f);
            var stones = new List<(float[] widths, float offset, Color[] tones)>();
            var baseTone = L1Build.Hex("#5C6672");
            foreach (var _ in courses)
            {
                var widths = Split(rng, FloorTile, 0.7f, 1.35f);
                var tones = new Color[widths.Length];
                for (int i = 0; i < tones.Length; i++)
                {
                    float v = 0.88f + (float)rng.NextDouble() * 0.2f;
                    float warm = ((float)rng.NextDouble() - 0.5f) * 0.03f;
                    tones[i] = new Color(baseTone.r * v + warm, baseTone.g * v, baseTone.b * v - warm, 1f);
                }

                stones.Add((widths, (float)rng.NextDouble() * FloorTile, tones));
            }

            var grout = L1Build.Hex("#3E4651");
            var pixels = new Color[FloorTexSize * FloorTexSize];
            for (int y = 0; y < FloorTexSize; y++)
            {
                float v = (y + 0.5f) / ppm;
                int c = 0;
                float c0 = 0f;
                while (c < courses.Length - 1 && v > c0 + courses[c])
                {
                    c0 += courses[c];
                    c++;
                }

                float dy = Mathf.Min(v - c0, c0 + courses[c] - v);
                var row = stones[c];
                for (int x = 0; x < FloorTexSize; x++)
                {
                    float u = ((x + 0.5f) / ppm + row.offset) % FloorTile;
                    int s = 0;
                    float s0 = 0f;
                    while (s < row.widths.Length - 1 && u > s0 + row.widths[s])
                    {
                        s0 += row.widths[s];
                        s++;
                    }

                    float dx = Mathf.Min(u - s0, s0 + row.widths[s] - u);
                    float d = Mathf.Min(dx, dy);
                    // Soft grout, a worn darker margin, and a little grain in the stone.
                    float stone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.016f, 0.04f, d));
                    float margin = Mathf.Lerp(0.9f, 1f, Mathf.InverseLerp(0.03f, 0.12f, d));
                    float grain = 1f + (Mathf.PerlinNoise(x * 0.07f + c * 13f, y * 0.07f + s * 7f) - 0.5f) * 0.08f;
                    var col = row.tones[s] * margin * grain;
                    var p = Color.Lerp(grout, col, stone);
                    p.a = 1f;
                    pixels[y * FloorTexSize + x] = p;
                }
            }

            var tex = new Texture2D(FloorTexSize, FloorTexSize, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            var mat = LoadOrCreate("Flagstones", Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            Mats["Flagstones"] = mat;
            return mat;
        }

        // Random lengths between min and max that add up to 'total' exactly.
        private static float[] Split(System.Random rng, float total, float min, float max)
        {
            var parts = new List<float>();
            float sum = 0f;
            while (sum < total - min)
            {
                float w = min + (float)rng.NextDouble() * (max - min);
                parts.Add(w);
                sum += w;
            }

            float scale = total / sum;
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i] *= scale;
            }

            return parts.ToArray();
        }

        // The floors: the round hall (a 64-gon under the wall's inner face), and, 2 mm higher so they never fight it
        // where they meet, the inner threshold, vestibule, corridor, outer threshold and the landing outside.
        private static void Floors(Transform root)
        {
            var mat = FloorMaterial();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var centre = new Vector3(HallCentre.x, 0f, HallCentre.y);
            const int sides = 64;
            int c = verts.Count;
            verts.Add(centre);
            for (int i = 0; i <= sides; i++)
            {
                float a = 90f + i * 360f / sides;
                verts.Add(HallPoint(a, HallApothem + 0.4f));
            }

            for (int i = 0; i < sides; i++)
            {
                tris.AddRange(new[] { c, c + i + 2, c + i + 1 });
            }

            void Rect(float x0, float x1, float z0, float z1, float y)
            {
                int b = verts.Count;
                verts.Add(new Vector3(x0, y, z0));
                verts.Add(new Vector3(x1, y, z0));
                verts.Add(new Vector3(x1, y, z1));
                verts.Add(new Vector3(x0, y, z1));
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }

            const float lift = 0.002f;
            Rect(-DoorHalf - 0.1f, DoorHalf + 0.1f, SanctuaryZ1 - 0.1f, VestibuleZ0, lift);
            Rect(-VestibuleX, VestibuleX, VestibuleZ0, VestibuleZ1, lift);
            Rect(-CorridorX, CorridorX, VestibuleZ1, CorridorZ1, lift);
            Rect(-DoorHalf - 0.1f, DoorHalf + 0.1f, CorridorZ1, CorridorZ1 + Wall, lift);
            Rect(-1.7f, 1.7f, CorridorZ1 + Wall, CorridorZ1 + Wall + 1.3f, lift);

            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            foreach (var v in verts)
            {
                uvs.Add(new Vector2(v.x, v.z) / FloorTile);
                normals.Add(Vector3.up);
            }

            var mesh = new Mesh { name = "Floors", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject("#Floor");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "L1_Mon_FloorSheet");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, L1Build.EnvironmentStatic);
        }
    }
}
