using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The stronghold's yards as a material (Kneel/Ground): dressed flagstones laid in courses, not a village's
    // cobbles. Slabs are 0.5-0.8 m deep and 0.6-1.3 m long, each course broken at its own joints, the arrises
    // chipped, a few slabs cracked or sunk. Where the paving mask thins, L2's trodden ground shows through: at
    // wall feet, round the pour rings and in worn patches. Same layer format as L2Paving: an overlay (0.5 = the
    // painted ground's colour) with height in alpha, and a normal map from that height.
    public static class L4Paving
    {
        private const string TexturesPath = L4Build.MaterialsPath + "/Textures";
        private const int Size = 1024;
        public const float TileMetres = 4f;

        [MenuItem("Kneel/L4/Build/Flagstones")]
        public static void BuildMenu()
        {
            Debug.Log(Apply());
        }

        public static string Apply()
        {
            L2Build.EnsureFolder(TexturesPath);
            var (albedo, normal) = Flagstones();
            var mat = L4Build.Mat("L4_Ground");
            mat.SetTexture("_CobbleMap", albedo);
            mat.SetTexture("_CobbleNormal", normal);
            mat.SetFloat("_CobbleTile", TileMetres);
            mat.SetFloat("_CobbleNormalScale", 0.9f);
            mat.SetFloat("_Parallax", 0.02f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return "flagstones on L4_Ground";
        }

        private static (Texture2D albedo, Texture2D normal) Flagstones()
        {
            var rng = new System.Random(41);

            // Courses: depths that sum to the tile.
            const int courses = 6;
            var rowEdge = new float[courses + 1];
            float total = 0f;
            var depth = new float[courses];
            for (int r = 0; r < courses; r++)
            {
                depth[r] = 0.75f + (float)rng.NextDouble() * 0.5f;
                total += depth[r];
            }

            for (int r = 0; r < courses; r++)
            {
                rowEdge[r + 1] = rowEdge[r] + depth[r] / total;
            }

            // Joints along each course, wrapped round the tile; per-slab tone, sink and crack.
            var cuts = new System.Collections.Generic.List<float>[courses];
            var tone = new System.Collections.Generic.List<float>[courses];
            var sink = new System.Collections.Generic.List<float>[courses];
            var crack = new System.Collections.Generic.List<Vector3>[courses];
            for (int r = 0; r < courses; r++)
            {
                cuts[r] = new System.Collections.Generic.List<float>();
                tone[r] = new System.Collections.Generic.List<float>();
                sink[r] = new System.Collections.Generic.List<float>();
                crack[r] = new System.Collections.Generic.List<Vector3>();
                float u = (float)rng.NextDouble();
                float end = u + 1f;
                while (true)
                {
                    cuts[r].Add(u);
                    tone[r].Add((float)rng.NextDouble());
                    sink[r].Add(rng.NextDouble() < 0.12 ? 0.35f + (float)rng.NextDouble() * 0.3f : (float)rng.NextDouble() * 0.12f);
                    crack[r].Add(rng.NextDouble() < 0.18 ? new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() * Mathf.PI) : Vector3.zero);
                    float next = u + (0.6f + (float)rng.NextDouble() * 0.7f) / TileMetres;
                    if (next > end - 0.45f / TileMetres)
                    {
                        break;
                    }

                    u = next;
                }
            }

            var height = new float[Size * Size];
            var occlusion = new float[Size * Size];
            var slabTone = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;
                int r = 0;
                while (r < courses - 1 && v >= rowEdge[r + 1])
                {
                    r++;
                }

                float dv = Mathf.Min(v - rowEdge[r], rowEdge[r + 1] - v) * TileMetres;
                var c = cuts[r];
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    // Which slab: the last cut at or before u (wrapping).
                    int s = c.Count - 1;
                    float best = float.MaxValue;
                    float du = float.MaxValue;
                    for (int i = 0; i < c.Count; i++)
                    {
                        float a = Mathf.Repeat(u - c[i], 1f);
                        if (a < best)
                        {
                            best = a;
                            s = i;
                        }
                    }

                    float start = c[s], stop = c[(s + 1) % c.Count];
                    float len = Mathf.Repeat(stop - start, 1f);
                    if (len <= 0f)
                    {
                        len = 1f;
                    }

                    du = Mathf.Min(best, len - best) * TileMetres;

                    // Chipped arrises: the edge distance is nibbled by noise near the joints.
                    float chip = (TileNoise(u * 24f, v * 24f, 24, 5) - 0.5f) * 0.05f + (TileNoise(u * 64f, v * 64f, 64, 9) - 0.5f) * 0.02f;
                    float edge = Mathf.Min(du, dv) + chip;
                    const float mortar = 0.016f;
                    float joint = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mortar, mortar + 0.012f, edge));
                    float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - mortar) / 0.07f));
                    float surface = 0.82f + (TileNoise(u * 10f + s, v * 10f + r, 10, 13) - 0.5f) * 0.1f - sink[r][s] * 0.4f;

                    // A hairline crack across some slabs.
                    var k3 = crack[r][s];
                    float crackDark = 0f;
                    if (k3 != Vector3.zero)
                    {
                        float cu = (best / len), cv = (v - rowEdge[r]) / (rowEdge[r + 1] - rowEdge[r]);
                        var dir = new Vector2(Mathf.Cos(k3.z), Mathf.Sin(k3.z));
                        var p = new Vector2(cu - k3.x, cv - k3.y);
                        float dist = Mathf.Abs(p.x * dir.y - p.y * dir.x) + (TileNoise(u * 40f, v * 40f, 40, 21) - 0.5f) * 0.05f;
                        crackDark = 1f - Mathf.SmoothStep(0f, 1f, dist / 0.012f);
                    }

                    int k = y * Size + x;
                    height[k] = joint * (bevel * surface) * (1f - crackDark * 0.5f);
                    occlusion[k] = joint * Mathf.Lerp(0.55f, 1f, bevel) * (1f - crackDark * 0.6f) + (1f - joint) * 0.3f;
                    slabTone[k] = joint > 0.5f ? tone[r][s] - sink[r][s] * 0.6f : -1f;
                }
            }

            var albedo = new Color[Size * Size];
            for (int k = 0; k < albedo.Length; k++)
            {
                float x = k % Size, y = k / Size;
                float grime = TileNoise(x / Size * 6f, y / Size * 6f, 6, 31);
                float speck = TileNoise(x / Size * 90f, y / Size * 90f, 90, 37) * 0.07f;
                float v;
                Color tint;
                if (slabTone[k] < -0.5f)
                {
                    v = 0.17f + speck;
                    tint = new Color(1f, 0.97f, 0.93f);
                }
                else
                {
                    v = 0.5f + (slabTone[k] - 0.5f) * 0.14f + speck - 0.035f - grime * 0.08f;
                    tint = Color.Lerp(new Color(1.02f, 1f, 0.97f), new Color(0.96f, 0.98f, 1.02f), slabTone[k]);
                }

                v *= Mathf.Lerp(0.55f, 1f, occlusion[k]);
                albedo[k] = new Color(v * tint.r, v * tint.g, v * tint.b, height[k]);
            }

            var normals = new Color[Size * Size];
            const float strength = 5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float hl = height[y * Size + (x - 1 + Size) % Size], hr = height[y * Size + (x + 1) % Size];
                    float hd = height[((y - 1 + Size) % Size) * Size + x], hu = height[((y + 1) % Size) * Size + x];
                    var n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    normals[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            return (Save("L4_Flag_Detail_Albedo", albedo, Size, Size, false, true), Save("L4_Flag_Detail_Normal", normals, Size, Size, true, true));
        }

        // A paving mask for a rectangular floor (its UV0 spans the rectangle): full paving, thinned at the given
        // wall feet and in worn patches, broken round each centre in 'scuffs' (pour rings, gates).
        public static Texture2D Mask(string name, Vector2 min, Vector2 size, Rect[] wallFeet, Vector2[] scuffs, int seed)
        {
            int width = Mathf.Clamp(Mathf.RoundToInt(size.x * 18f), 64, 1024);
            int height = Mathf.Clamp(Mathf.RoundToInt(size.y * 18f), 64, 1024);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var p = new Vector2(min.x + (x + 0.5f) / width * size.x, min.y + (y + 0.5f) / height * size.y);
                    float ragged = (Mathf.PerlinNoise(p.x * 0.7f + seed, p.y * 0.7f) - 0.5f) * 1.2f + (Mathf.PerlinNoise(p.x * 2.3f, p.y * 2.3f + seed) - 0.5f) * 0.5f;
                    float w = 1f;
                    foreach (var foot in wallFeet)
                    {
                        float dx = Mathf.Max(foot.xMin - p.x, 0f, p.x - foot.xMax);
                        float dy = Mathf.Max(foot.yMin - p.y, 0f, p.y - foot.yMax);
                        float d = Mathf.Sqrt(dx * dx + dy * dy) + ragged;
                        w = Mathf.Min(w, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1.6f, d)));
                    }

                    foreach (var c in scuffs)
                    {
                        float d = (p - c).magnitude + ragged * 0.8f;
                        w = Mathf.Min(w, 0.25f + 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 2.6f, d)));
                    }

                    float worn = Mathf.PerlinNoise(p.x * 0.14f + 40f + seed, p.y * 0.14f + 7f);
                    w *= 1f - 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.8f, worn));
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(w));
                }
            }

            return Save(name, pixels, width, height, false, false);
        }

        // A murder hole's mark on the floor: years of pitch, painted into the ground itself (no decal). Radius is
        // the pour's reach; seed turns and shapes it so no two match.
        public struct Splash
        {
            public Vector2 Centre;
            public float Radius;
            public int Seed;

            public Splash(Vector2 centre, float radius, int seed)
            {
                Centre = centre;
                Radius = radius;
                Seed = seed;
            }
        }

        // The painted layout that goes with a mask: stone grey where paved, a darker ash-brown earth where not
        // (the mud overlay alone reads as pale sand under the noon sun), and black pitch in each splash. Alpha is
        // the shader's wetness: 0 dry, and a standing-water sheen on the pitch (needs _PuddleAmount 1).
        public static Texture2D LayoutFromMask(string name, Texture2D mask, Vector2 min, Vector2 size, Splash[] splashes)
        {
            var src = mask.GetPixels();
            int w = mask.width, h = mask.height;
            var pixels = new Color[src.Length];
            var stone = new Color(0.58f, 0.56f, 0.53f);
            var earth = new Color(0.36f, 0.32f, 0.28f);
            var pitch = new Color(0.11f, 0.1f, 0.09f);
            var flecks = new System.Collections.Generic.List<Vector4>[splashes.Length];
            for (int s = 0; s < splashes.Length; s++)
            {
                var rng = new System.Random(splashes[s].Seed);
                flecks[s] = new System.Collections.Generic.List<Vector4>();
                for (int i = 0; i < 16; i++)
                {
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = 0.55f + (float)rng.NextDouble() * 0.45f;
                    flecks[s].Add(new Vector4(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0.01f + (float)rng.NextDouble() * 0.012f, angle));
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var p = new Vector2(min.x + (x + 0.5f) / w * size.x, min.y + (y + 0.5f) / h * size.y);
                    float cover = 0f;
                    for (int s = 0; s < splashes.Length; s++)
                    {
                        cover = Mathf.Max(cover, SplashCover(p, splashes[s], flecks[s]));
                    }

                    var c = Color.Lerp(earth, stone, Mathf.SmoothStep(0f, 1f, src[i].a));
                    c = Color.Lerp(c, pitch, cover);
                    c.a = cover * 0.13f;
                    pixels[i] = c;
                }
            }

            return Save(name, pixels, w, h, false, false, true);
        }

        // 0..1: a lobed, uneven core thickest where the pour lands and thinning out, with flecks thrown outward
        // (each stretched along the line from the centre). Fades to nothing by its radius.
        private static float SplashCover(Vector2 p, Splash s, System.Collections.Generic.List<Vector4> flecks)
        {
            var d = (p - s.Centre) / s.Radius;
            float r = d.magnitude;
            if (r > 1.05f)
            {
                return 0f;
            }

            float theta = Mathf.Atan2(d.y, d.x);
            float phase = s.Seed * 1.7f;
            float fbm = Mathf.PerlinNoise(p.x * 0.45f + s.Seed, p.y * 0.45f) * 0.6f + Mathf.PerlinNoise(p.x * 1.3f, p.y * 1.3f + s.Seed) * 0.4f;
            float lobes = 1f + 0.16f * Mathf.Sin(5f * theta + phase) + 0.09f * Mathf.Sin(9f * theta + phase * 2.3f) + (fbm - 0.5f) * 0.8f;
            float shape = r / Mathf.Max(lobes, 0.2f);
            float thickness = 0.6f + Mathf.PerlinNoise(p.x * 1.8f + 11f, p.y * 1.8f + s.Seed) * 0.4f;
            float cover = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.4f, shape)) * 0.9f * thickness;
            foreach (var f in flecks)
            {
                float px = d.x - f.x, py = d.y - f.y;
                float c = Mathf.Cos(f.w), sn = Mathf.Sin(f.w);
                float along = px * c + py * sn, across = -px * sn + py * c;
                float e = Mathf.Sqrt(along * along / 4f + across * across) / f.z;
                if (e < 1.3f)
                {
                    cover = Mathf.Max(cover, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 0.7f, e)) * 0.8f);
                }
            }

            return cover * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.05f, 0.9f, r));
        }

        private static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float Hash(int i, int j)
            {
                i = ((i % period) + period) % period;
                j = ((j % period) + period) % period;
                uint h = (uint)(i * 374761393 + j * 668265263 + seed * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }

            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx), Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx), fy);
        }

        private static Texture2D Save(string name, Color[] pixels, int width, int height, bool normalMap, bool repeat, bool srgb = false)
        {
            L2Build.EnsureFolder(TexturesPath);
            string path = TexturesPath + "/" + name + ".png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.sRGBTexture = srgb || (!normalMap && repeat);
            importer.isReadable = !repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
