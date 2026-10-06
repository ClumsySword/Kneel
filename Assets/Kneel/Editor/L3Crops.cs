using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The standing crop for L3's fire cells: wheat in drilled rows on a tilled bed, baked from the pack's reed
    // meshes into one mesh per 2 x 2 m cell. The reeds' own colours are thrown away: every vertex is given a
    // place in a small wheat palette (column = part of the plant, row = height up the stalk), so the crop is
    // dark at the root and a pale, ash-dusted straw at the ear (dry and dying, never harvest gold), and one material swap turns it to char. It sways with the
    // project's wind shader. Three variants of the standing crop and of the burnt stubble, so a row never
    // repeats cell for cell.
    public static class L3Crops
    {
        public const int Variants = 3;
        private const string MeshFolder = L3Build.MeshesPath + "/Crops";
        private const string TexturesPath = L3Build.MaterialsPath + "/Textures";

        // Palette columns.
        private const int LeafA = 0, LeafB = 1, LeafC = 2, EarA = 3, EarB = 4, Soil = 5, SoilDark = 6, Ember = 7;
        private const int Columns = 8, Rows = 16;

        // Plants per side of a cell, the distance between drill rows, and where the first row stands.
        private const int PerSide = 6;
        private const float Pitch = 2f / PerSide, First = -1f + Pitch * 0.5f;

        // Each column runs from its root colour (bottom of the texture) to its tip colour (top).
        private static readonly string[,] Dry =
        {
            { "#372E1E", "#A08D65" }, { "#31291B", "#917F5A" }, { "#3D331F", "#AE9D74" }, { "#94835A", "#C9BA94" },
            { "#857650", "#B9A982" }, { "#2A221A", "#33291F" }, { "#1E1813", "#251E17" }, { "#C4521E", "#FF8A2E" },
        };

        private static readonly string[,] Charred =
        {
            { "#0C0B0A", "#2B2420" }, { "#0A0908", "#231E1B" }, { "#0E0C0B", "#322820" }, { "#16110E", "#3C2B1E" },
            { "#120F0D", "#33261C" }, { "#171514", "#211E1C" }, { "#0E0D0C", "#161413" }, { "#7A2E08", "#FF6A14" },
        };

        public static Material DryMaterial => L3Build.Mat("L3_Wheat");

        public static Material CharredStandingMaterial => L3Build.Mat("L3_Wheat_Charred");

        public static Material StubbleMaterial => L3Build.Mat("L3_Wheat_Stubble");

        public static Mesh Standing(int variant)
        {
            return Load("L3_Wheat_" + variant) ?? Build()[variant];
        }

        public static Mesh Stubble(int variant)
        {
            return Load("L3_Stubble_" + variant) ?? Build()[Variants + variant];
        }

        private static Mesh Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + name + ".asset");
        }

        [MenuItem("Kneel/L3/Build/Crops")]
        public static void BuildMenu()
        {
            Build();
        }

        public static void Materials()
        {
            var dry = PaletteTexture("L3_Wheat_Palette", Dry);
            var charred = PaletteTexture("L3_Wheat_Charred_Palette", Charred);
            MakeMaterial("L3_Wheat", "Kneel/Lit Wind", dry);
            MakeMaterial("L3_Wheat_Charred", "Kneel/Lit Wind", charred);
            MakeMaterial("L3_Wheat_Stubble", "Universal Render Pipeline/Lit", charred);
        }

        // Standing variants first, then stubble variants.
        public static Mesh[] Build()
        {
            L2Build.EnsureFolder(MeshFolder);
            if (AssetDatabase.LoadAssetAtPath<Material>(L3Build.MaterialsPath + "/L3_Wheat.mat") == null)
            {
                Materials();
            }

            var reeds = Reeds();
            var meshes = new Mesh[Variants * 2];
            for (int v = 0; v < Variants; v++)
            {
                meshes[v] = Save(StandingCell(reeds, 40 + v * 17), "L3_Wheat_" + v);
                meshes[Variants + v] = Save(StubbleCell(60 + v * 13), "L3_Stubble_" + v);
            }

            return meshes;
        }

        private static Mesh Save(L3Mesh data, string name)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var mesh = data.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Vector2 Cell(int column, float height)
        {
            return new Vector2((column + 0.5f) / Columns, Mathf.Clamp(height, 0.03f, 0.97f));
        }

        // ---------------------------------------------------------------- Source plants

        private class Plant
        {
            public readonly List<Vector3> P = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();

            // Per triangle: is it part of the seed head?
            public readonly List<bool> Ear = new List<bool>();
            public float Height;
        }

        // The pack's three reeds as loose triangles, each marked leaf or ear by the colour the pack gave it
        // (the heads are brown, the blades green).
        private static List<Plant> Reeds()
        {
            var plants = new List<Plant>();
            Texture2D atlas = null;
            foreach (string key in new[] { "A:SM_Env_Reeds_01", "A:SM_Env_Reeds_02", "A:SM_Env_Reeds_03" })
            {
                var prefab = L3Build.Pack(key);
                var plant = new Plant();
                foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (atlas == null)
                    {
                        atlas = Readable(renderer.sharedMaterial.GetTexture("_BaseMap"));
                    }

                    Matrix4x4 m = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var mesh = filter.sharedMesh;
                    var v = mesh.vertices;
                    var n = mesh.normals;
                    var uv = mesh.uv;
                    var tris = mesh.triangles;
                    for (int t = 0; t < tris.Length; t += 3)
                    {
                        Color c = atlas.GetPixelBilinear(uv[tris[t]].x, uv[tris[t]].y);
                        plant.Ear.Add(c.r > c.g * 1.08f);
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 p = m.MultiplyPoint3x4(v[tris[t + k]]);
                            plant.P.Add(p);
                            plant.N.Add(m.MultiplyVector(n[tris[t + k]]).normalized);
                            plant.Height = Mathf.Max(plant.Height, p.y);
                        }
                    }
                }

                plants.Add(plant);
            }

            Object.DestroyImmediate(atlas);
            return plants;
        }

        private static Texture2D Readable(Texture source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        // ---------------------------------------------------------------- Cells

        // Six drilled rows of six plants on a tilled bed. The cell's origin is the middle of its base.
        private static L3Mesh StandingCell(List<Plant> reeds, int seed)
        {
            var rng = new System.Random(seed);
            var cell = new L3Mesh();
            Bed(cell, rng, Soil, SoilDark, 0.09f);

            for (int row = 0; row < PerSide; row++)
            {
                for (int i = 0; i < PerSide; i++)
                {
                    var plant = reeds[rng.Next(reeds.Count)];
                    float x = First + row * Pitch + Jitter(rng, 0.06f);
                    float z = First + i * Pitch + Jitter(rng, 0.09f);
                    // About 0.8 m to the ear, whatever reed it came from.
                    float scale = (0.74f + (float)rng.NextDouble() * 0.2f) / plant.Height;
                    // Leaning a little downwind (north), each its own way.
                    var rotation = Quaternion.Euler(4f + Jitter(rng, 6f), (float)rng.NextDouble() * 360f, Jitter(rng, 6f));
                    var matrix = Matrix4x4.TRS(new Vector3(x, 0.05f, z), rotation, new Vector3(scale * 0.9f, scale, scale * 0.9f));
                    int leaf = rng.Next(3), ear = EarA + rng.Next(2);
                    float top = plant.Height * scale + 0.05f;
                    for (int t = 0; t < plant.Ear.Count; t++)
                    {
                        int column = plant.Ear[t] ? ear : leaf;
                        Vector3 a = matrix.MultiplyPoint3x4(plant.P[t * 3]), b = matrix.MultiplyPoint3x4(plant.P[t * 3 + 1]), c = matrix.MultiplyPoint3x4(plant.P[t * 3 + 2]);
                        // Lit as if every blade faced the sky: a field of wheat has no dark side from above.
                        Vector3 normal = (matrix.MultiplyVector(plant.N[t * 3]).normalized * 0.45f + Vector3.up).normalized;
                        cell.Tri(0, a, b, c, Cell(column, a.y / top), Cell(column, b.y / top), Cell(column, c.y / top), normal);
                    }
                }
            }

            return cell;
        }

        // What the fire leaves: a bed of ash, the charred stumps of each plant, and a few stalks that fell.
        private static L3Mesh StubbleCell(int seed)
        {
            var rng = new System.Random(seed);
            var cell = new L3Mesh();
            Bed(cell, rng, Soil, SoilDark, 0.05f);

            for (int row = 0; row < PerSide; row++)
            {
                for (int i = 0; i < PerSide; i++)
                {
                    var foot = new Vector3(First + row * Pitch + Jitter(rng, 0.06f), 0.03f, First + i * Pitch + Jitter(rng, 0.09f));
                    int stumps = 2 + rng.Next(3);
                    for (int s = 0; s < stumps; s++)
                    {
                        Vector3 from = foot + new Vector3(Jitter(rng, 0.07f), 0f, Jitter(rng, 0.07f));
                        Vector3 to = from + new Vector3(Jitter(rng, 0.05f), 0.07f + (float)rng.NextDouble() * 0.16f, Jitter(rng, 0.05f) + 0.02f);
                        cell.Prism(0, from, to, 0.016f, 0.004f, 3, Cell(rng.Next(3), 0.2f + (float)rng.NextDouble() * 0.6f));
                    }
                }
            }

            // Fallen stalks, blown north.
            for (int i = 0; i < 9; i++)
            {
                var from = new Vector3(Jitter(rng, 0.9f), 0.06f, Jitter(rng, 0.9f));
                Vector3 to = from + Quaternion.Euler(0f, Jitter(rng, 50f), 0f) * Vector3.forward * (0.35f + (float)rng.NextDouble() * 0.35f);
                to.y = 0.05f;
                cell.Prism(0, from, to, 0.012f, 0.006f, 3, Cell(EarA + rng.Next(2), 0.5f + (float)rng.NextDouble() * 0.4f));
            }

            // A few embers that never quite went out.
            for (int i = 0; i < 5; i++)
            {
                var at = new Vector3(Jitter(rng, 0.85f), 0.062f, Jitter(rng, 0.85f));
                cell.Disc(0, at, Vector3.up, 0.02f + (float)rng.NextDouble() * 0.02f, 4, Cell(Ember, (float)rng.NextDouble()));
            }

            return cell;
        }

        // The ground under a cell: an uneven patch a little wider than the cell (so neighbours knit together
        // and the strip has a ragged edge), ridged along the drill rows.
        private static void Bed(L3Mesh cell, System.Random rng, int light, int dark, float ridge)
        {
            const int n = 7;
            const float halfX = 1.1f;
            var grid = new Vector3[n, n];
            for (int ix = 0; ix < n; ix++)
            {
                for (int iz = 0; iz < n; iz++)
                {
                    float x = Mathf.Lerp(-halfX, halfX, ix / (float)(n - 1)), z = Mathf.Lerp(-1f, 1f, iz / (float)(n - 1));
                    bool edgeX = ix == 0 || ix == n - 1, edgeZ = iz == 0 || iz == n - 1;
                    // Ridge under each drill row, furrow between; the east and west rims sink to the ground.
                    // The north and south edges are left exact, so one cell meets the next without a seam.
                    float furrow = Mathf.Cos((x - First) / Pitch * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float y = edgeX ? 0.005f : 0.02f + ridge * furrow + (edgeZ ? 0f : (float)rng.NextDouble() * 0.02f);
                    float dx = edgeZ ? 0f : edgeX ? Jitter(rng, 0.1f) : Jitter(rng, 0.03f);
                    grid[ix, iz] = new Vector3(x + dx, y, z + (edgeZ ? 0f : Jitter(rng, 0.05f)));
                }
            }

            for (int ix = 0; ix < n - 1; ix++)
            {
                for (int iz = 0; iz < n - 1; iz++)
                {
                    Vector3 a = grid[ix, iz], b = grid[ix, iz + 1], c = grid[ix + 1, iz + 1], d = grid[ix + 1, iz];
                    Vector2 first = Cell(rng.NextDouble() < 0.6 ? light : dark, (float)rng.NextDouble()), second = Cell(rng.NextDouble() < 0.6 ? light : dark, (float)rng.NextDouble());
                    if ((ix + iz) % 2 == 0)
                    {
                        cell.Tri(0, a, b, c, first);
                        cell.Tri(0, a, c, d, second);
                    }
                    else
                    {
                        cell.Tri(0, a, b, d, first);
                        cell.Tri(0, b, c, d, second);
                    }
                }
            }
        }

        private static float Jitter(System.Random rng, float amount)
        {
            return ((float)rng.NextDouble() - 0.5f) * 2f * amount;
        }

        // ---------------------------------------------------------------- Palette

        internal static Texture2D PaletteTexture(string name, string[,] ramps)
        {
            L2Build.EnsureFolder(TexturesPath);
            var tex = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false);
            for (int column = 0; column < Columns; column++)
            {
                Color root = L1Build.Hex(ramps[column, 0]), tip = L1Build.Hex(ramps[column, 1]);
                for (int row = 0; row < Rows; row++)
                {
                    // Most of the stalk is the tip colour; only the foot is in shadow.
                    float t = Mathf.Pow(row / (float)(Rows - 1), 0.7f);
                    tex.SetPixel(column, row, Color.Lerp(root, tip, t));
                }
            }

            tex.Apply();
            string path = TexturesPath + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static void MakeMaterial(string name, string shader, Texture2D palette)
        {
            string path = L3Build.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find(shader);
            mat.SetTexture("_BaseMap", palette);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.04f);
            // Blades are single sheets: draw both sides.
            mat.SetFloat("_Cull", 0f);
            mat.doubleSidedGI = true;
            EditorUtility.SetDirty(mat);
        }
    }
}
