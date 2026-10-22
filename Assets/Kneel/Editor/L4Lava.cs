using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Refinement (owner, 2026-10-19): "Fill up the surrounding stronghold with Lava. This should look realistic but
    // fit within the theme of the low poly game." The ravine round the crag is now a river of lava a little above
    // its old floor, so the cliffs' scree and the far banks make its shore:
    // - one faceted sheet, flat-shaded like the land, where the ground lies under it; a crust of dark plates split
    //   by glowing cracks (a generated tileable texture), drifting slowly and breathing (Kneel.LavaFlow);
    // - orange light thrown up the cliffs from below, sparks and heat rising, the odd burst of flame on the crust.
    // Nothing here has a collider: a fall still ends in the ravine's kill volume above it. Rebuilding replaces
    // only this group.
    public static class L4Lava
    {
        public const string GroupName = "Lava";
        public const float LavaY = -12.8f;

        private const float Cell = 4f;
        private const float MinX = -110f, MaxX = 230f, MinZ = -190f, MaxZ = 420f;
        private const int Tile = 24;
        private const string TexturesPath = "Assets/Kneel/Textures/L4";

        [MenuItem("Kneel/L4/Build/Lava (replaces that group)")]
        public static void BuildMenu()
        {
            var root = L4Level.Root;
            if (root == null)
            {
                Debug.LogWarning("Open L4_BurningGate first.");
                return;
            }

            Debug.Log(Build(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            var mat = Material();
            int tiles = Sheet(g, mat, out var lowCells);
            int lights = Lights(g, lowCells);
            int fx = Effects(g, lowCells);
            g.gameObject.AddComponent<Kneel.LavaFlow>();
            return GroupName + ": " + tiles + " tiles, " + lights + " glow lights, " + fx + " effects";
        }

        public static float Ground(Vector2 p) => L4Surroundings.Height(p);

        // ---------------------------------------------------------------- The sheet

        private static int Sheet(Transform g, Material mat, out List<Vector2> lowCells)
        {
            int nx = Mathf.CeilToInt((MaxX - MinX) / Cell), nz = Mathf.CeilToInt((MaxZ - MinZ) / Cell);
            var ground = new float[nx + 1, nz + 1];
            var surface = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= nz; j++)
                {
                    float x = MinX + i * Cell, z = MinZ + j * Cell;
                    ground[i, j] = Ground(new Vector2(x, z));
                    // A slight swell: the crust rides up and down a hand's breadth, enough to catch light facet by facet.
                    surface[i, j] = LavaY + (L3Ground.Fbm(x * 0.09f + 5f, z * 0.09f) - 0.5f) * 0.35f;
                }
            }

            lowCells = new List<Vector2>();
            int tiles = 0;
            for (int ti = 0; ti < nx; ti += Tile)
            {
                for (int tj = 0; tj < nz; tj += Tile)
                {
                    var verts = new List<Vector3>();
                    var uvs = new List<Vector2>();
                    var tris = new List<int>();
                    for (int i = ti; i < Mathf.Min(ti + Tile, nx); i++)
                    {
                        for (int j = tj; j < Mathf.Min(tj + Tile, nz); j++)
                        {
                            float lowest = Mathf.Min(Mathf.Min(ground[i, j], ground[i + 1, j]), Mathf.Min(ground[i, j + 1], ground[i + 1, j + 1]));
                            if (lowest > LavaY + 0.3f)
                            {
                                continue;
                            }

                            float x = MinX + i * Cell, z = MinZ + j * Cell;
                            var a = new Vector3(x, surface[i, j], z);
                            var b = new Vector3(x, surface[i, j + 1], z + Cell);
                            var c = new Vector3(x + Cell, surface[i + 1, j + 1], z + Cell);
                            var d = new Vector3(x + Cell, surface[i + 1, j], z);
                            // Alternate the diagonal so the facets do not read as a grid.
                            if (((i + j) & 1) == 0)
                            {
                                Tri(verts, uvs, tris, a, b, c);
                                Tri(verts, uvs, tris, a, c, d);
                            }
                            else
                            {
                                Tri(verts, uvs, tris, a, b, d);
                                Tri(verts, uvs, tris, b, c, d);
                            }

                            if (lowest < LavaY - 0.5f)
                            {
                                lowCells.Add(new Vector2(x + Cell * 0.5f, z + Cell * 0.5f));
                            }
                        }
                    }

                    if (tris.Count == 0)
                    {
                        continue;
                    }

                    var mesh = new Mesh { name = "L4_Lava_" + ti / Tile + "_" + tj / Tile };
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.SetVertices(verts);
                    mesh.SetUVs(0, uvs);
                    mesh.SetTriangles(tris, 0);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    var go = L3Build.MeshChild(g, mesh.name, L4Level.SaveMesh(mesh), Vector3.zero, mat);
                    var r = go.GetComponent<MeshRenderer>();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    go.layer = L3Build.Layer(L3Build.Unlayered);
                    tiles++;
                }
            }

            return tiles;
        }

        // Each triangle its own three vertices: flat shading, like the land.
        private static void Tri(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            foreach (var v in new[] { a, b, c })
            {
                verts.Add(v);
                uvs.Add(new Vector2(v.x, v.z));
            }

            tris.AddRange(new[] { i, i + 1, i + 2 });
        }

        // ---------------------------------------------------------------- Light and fire

        // Firelight from below on the cliffs: a warm light low over the lava wherever it runs near the walls.
        private static int Lights(Transform g, List<Vector2> lowCells)
        {
            var parent = L3Build.Child(g, "Glow").transform;
            var placed = new List<Vector2>();
            int n = 0;
            foreach (var p in lowCells)
            {
                float d = DistanceToCastle(p);
                if (d < 6f || d > 34f)
                {
                    continue;
                }

                bool near = false;
                foreach (var q in placed)
                {
                    if ((q - p).sqrMagnitude < 44f * 44f)
                    {
                        near = true;
                        break;
                    }
                }

                if (near)
                {
                    continue;
                }

                placed.Add(p);
                var go = L3Build.Child(parent, "LavaLight", new Vector3(p.x, LavaY + 3.5f, p.y));
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = L1Build.Hex("#FF5A1C");
                light.intensity = 12f;
                light.range = 38f;
                light.shadows = LightShadows.None;
                n++;
            }

            return n;
        }

        // Sparks and heat rising off the lava along the walls, and small fires breaking through the crust.
        private static int Effects(Transform g, List<Vector2> lowCells)
        {
            var parent = L3Build.Child(g, "Effects").transform;
            var rng = new System.Random(707);
            var placed = new List<Vector2>();
            int n = 0;
            foreach (var p in lowCells)
            {
                float d = DistanceToCastle(p);
                if (d < 7f || d > 45f || rng.NextDouble() > 0.12)
                {
                    continue;
                }

                bool near = false;
                foreach (var q in placed)
                {
                    if ((q - p).sqrMagnitude < 40f * 40f)
                    {
                        near = true;
                        break;
                    }
                }

                if (near)
                {
                    continue;
                }

                placed.Add(p);
                var at = new Vector3(p.x, LavaY + 0.1f, p.y);
                // A tongue of flame where the crust has split, and sparks lifting off the heat.
                L2Fire.Fire(parent, "Breakout", at, 0.9f + (float)rng.NextDouble() * 0.8f, 0f, 0f, false, new Vector3(2.5f, 0f, 2.5f));
                var sparks = L1Wayfinding.Particles("Sparks", parent, L1Build.FxMat("L1_FX_Ember"), 60, 6f, new Vector2(3f, 6f), new Vector2(1.2f, 3f),
                    new Vector2(0.05f, 0.12f), new Color(1f, 0.75f, 0.35f, 1f), new Color(1f, 0.25f, 0.05f, 0.6f), -0.04f);
                sparks.transform.position = at;
                sparks.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14f, 14f, 0.2f);
                var noise = sparks.noise;
                noise.enabled = true;
                noise.strength = 0.8f;
                noise.frequency = 0.3f;
                sparks.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.03f;
                n++;
            }

            return n;
        }

        private static float DistanceToCastle(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var f in L4Plan.Floors)
            {
                if (f.Bridge)
                {
                    continue;
                }

                float dx = Mathf.Max(f.X0 - p.x, 0f, p.x - f.X1), dz = Mathf.Max(f.Z0 - p.y, 0f, p.y - f.Z1);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        // ---------------------------------------------------------------- The material

        // Crust and glow, one tileable texture pair (one tile is 40 m of lava). Not cells: the cracks are the zero
        // lines of warped fractal noise, so they branch and meander like real crust and the plates between them come
        // in every size. A slow large-scale "heat" opens the cracks into glowing channels in places and lets them
        // close to dull red hairlines elsewhere; only the hottest channels run yellow. A finer second network of
        // hairlines shows where it is hot. Every input is periodic over the tile, so it repeats without a seam.
        private static Material Material()
        {
            const int size = 1024;
            var rng = new System.Random(913);
            var lattices = new Dictionary<int, float[,]>();
            float[,] Lattice(int period)
            {
                if (!lattices.TryGetValue(period, out var grid))
                {
                    grid = new float[period, period];
                    for (int i = 0; i < period; i++)
                    {
                        for (int j = 0; j < period; j++)
                        {
                            grid[i, j] = (float)rng.NextDouble();
                        }
                    }

                    lattices[period] = grid;
                }

                return grid;
            }

            // Periodic value noise in [0, 1] with smooth interpolation.
            float Noise(float u, float v, int period)
            {
                var grid = Lattice(period);
                float x = u * period, y = v * period;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int Wrap(int k) => ((k % period) + period) % period;
                float a = grid[Wrap(x0), Wrap(y0)], b = grid[Wrap(x0 + 1), Wrap(y0)], c = grid[Wrap(x0), Wrap(y0 + 1)], d = grid[Wrap(x0 + 1), Wrap(y0 + 1)];
                return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
            }

            float Fractal(float u, float v, int basePeriod, int octaves)
            {
                float sum = 0f, amp = 1f, norm = 0f;
                int period = basePeriod;
                for (int o = 0; o < octaves; o++)
                {
                    sum += Noise(u, v, period) * amp;
                    norm += amp;
                    amp *= 0.5f;
                    period *= 2;
                }

                return sum / norm;
            }

            var albedo = new Color[size * size];
            var glow = new Color[size * size];
            Color crustA = L1Build.Hex("#110C0A"), crustB = L1Build.Hex("#2A211C"), lip = L1Build.Hex("#5A1407"), hot = L1Build.Hex("#C8380C"), heart = L1Build.Hex("#FF9426");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float wu = u + (Fractal(u, v, 4, 3) - 0.5f) * 0.09f, wv = v + (Fractal(u + 0.37f, v + 0.61f, 4, 3) - 0.5f) * 0.09f;
                    float heat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.75f, Fractal(u, v, 3, 2)));
                    float ridge = 1f - Mathf.Abs(Fractal(wu, wv, 6, 4) * 2f - 1f);
                    float hair = 1f - Mathf.Abs(Fractal(wu + 0.21f, wv + 0.47f, 14, 3) * 2f - 1f);
                    float crack = Mathf.InverseLerp(0.9f - heat * 0.1f, 1f, ridge);
                    float fine = Mathf.InverseLerp(0.955f, 1f, hair) * (0.35f + heat * 0.65f);
                    float g = Mathf.Max(crack, fine * 0.7f);
                    Color a, e;
                    if (g > 0.02f)
                    {
                        a = g < 0.35f ? Color.Lerp(lip, hot, g / 0.35f) : g < 0.75f || heat < 0.45f ? hot : Color.Lerp(hot, heart, (g - 0.75f) / 0.25f);
                        e = a * Mathf.Lerp(0.35f, 1f, g);
                    }
                    else
                    {
                        // Crust, darker toward its middle, a dull red bloom close to a crack.
                        a = Color.Lerp(crustA, crustB, Fractal(u, v, 20, 2));
                        float near = Mathf.InverseLerp(0.8f - heat * 0.1f, 0.9f - heat * 0.1f, ridge);
                        a = Color.Lerp(a, lip * 0.6f, near * 0.5f);
                        e = lip * near * 0.18f;
                    }

                    albedo[y * size + x] = a;
                    glow[y * size + x] = e;
                }
            }

            var baseMap = Save("L4_Lava_Crust", albedo, size);
            var emissionMap = Save("L4_Lava_Glow", glow, size);
            var mat = L4Build.LoadOrCreate("L4_Lava", Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            mat.mainTextureScale = new Vector2(0.025f, 0.025f);
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", emissionMap);
            mat.SetColor("_EmissionColor", new Color(2.4f, 0.85f, 0.22f));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D Save(string name, Color[] pixels, int size)
        {
            L2Build.EnsureFolder(TexturesPath);
            string path = TexturesPath + "/" + name + ".png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
