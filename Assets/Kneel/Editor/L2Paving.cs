using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The village ground as a material, not geometry (Kneel/Ground on the terrain): two tiled layers over the
    // painted layout map. Trodden mud everywhere (lumps, hollows that hold puddles, pebbles, straw), and cobbles
    // (rounded stones, dark joints) where a paving mask in the layout's UV space says so: full on the village
    // streets, fraying at their edges and in worn patches, where the shader's height blend lets the stones
    // stand up out of the mud. The country roads at either end stay mud. Each layer is an overlay (crevice
    // occlusion baked in), a normal map from its height field, and its height in alpha.
    public static class L2Paving
    {
        private const string TexturesPath = L2Build.MaterialsPath + "/Textures";
        private const int Size = 1024;          // detail texture, tileable
        private const int Cells = 9;            // stones across one tile
        public const float TileMetres = 3f;     // one tile covers 3 x 3 m: stones about a third of a metre

        [MenuItem("Kneel/L2/Look/Paving")]
        public static void BuildMenu()
        {
            Debug.Log("[L2] " + Apply());
        }

        public static string Apply()
        {
            L2Build.EnsureFolder(TexturesPath);
            var (cobble, cobbleNormal) = CobbleTextures();
            var (mud, mudNormal) = MudTextures();
            var mask = PavingMask();
            var mat = L2Build.Mat("L2_Ground");
            if (mat == null)
            {
                return "No L2_Ground material yet (Kneel/L2/Look/Repaint Ground first).";
            }

            // The layout map keeps its slot (_BaseMap, terrain UV0); the rest is the ground shader's own.
            var layout = mat.GetTexture("_BaseMap");
            mat.shader = Shader.Find("Kneel/Ground");
            mat.SetTexture("_BaseMap", layout);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetTexture("_PaveMask", mask);
            mat.SetTexture("_MudMap", mud);
            mat.SetTexture("_MudNormal", mudNormal);
            mat.SetTexture("_CobbleMap", cobble);
            mat.SetTexture("_CobbleNormal", cobbleNormal);
            mat.SetFloat("_MudTile", MudMetres);
            mat.SetFloat("_CobbleTile", TileMetres);
            mat.SetFloat("_MudNormalScale", 1f);
            mat.SetFloat("_CobbleNormalScale", 1.35f);
            mat.SetFloat("_Parallax", 0.03f);
            // Puddles (patches over the wet ground) and moss (patches through the cobbles): see Kneel/Ground.
            mat.SetFloat("_PuddleScale", 4.5f);
            mat.SetFloat("_PuddleAmount", 0.37f);
            mat.SetFloat("_PuddleDepth", 0.66f);
            mat.SetColor("_PuddleSky", new Color(0.035f, 0.045f, 0.065f));
            mat.SetColor("_MossColor", new Color(0.33f, 0.42f, 0.25f));   // dull, dark olive: never a fresh green
            mat.SetFloat("_MossAmount", 0.55f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return "paving: mud + cobbles height-blended on L2_Ground (Kneel/Ground)";
        }

        // ---------------------------------------------------------------- Cobble textures

        // A tileable field of rounded stones: jittered seeds wrapped round the tile; each pixel finds its
        // nearest seed (F1) and the second nearest (F2); F2 - F1 is the distance to the joint, which shapes
        // each stone's dome and the dark mortar between.
        private static (Texture2D albedo, Texture2D normal) CobbleTextures()
        {
            var rng = new System.Random(17);
            var seeds = new Vector2[Cells, Cells];
            var tone = new float[Cells, Cells];
            var lift = new float[Cells, Cells];
            for (int j = 0; j < Cells; j++)
            {
                for (int i = 0; i < Cells; i++)
                {
                    float shift = (j % 2) * 0.5f;
                    seeds[i, j] = new Vector2((i + shift + ((float)rng.NextDouble() - 0.5f) * 0.55f) / Cells, (j + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.5f) / Cells);
                    tone[i, j] = (float)rng.NextDouble();
                    lift[i, j] = 0.8f + (float)rng.NextDouble() * 0.2f;
                }
            }

            var height = new float[Size * Size];
            var occlusion = new float[Size * Size];
            var stoneTone = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float f1 = float.MaxValue, f2 = float.MaxValue;
                    int bi = 0, bj = 0;
                    for (int j = 0; j < Cells; j++)
                    {
                        for (int i = 0; i < Cells; i++)
                        {
                            Vector2 d = seeds[i, j] - p;
                            d.x -= Mathf.Round(d.x);   // wrap: the tile repeats seamlessly
                            d.y -= Mathf.Round(d.y);
                            float dist = d.magnitude;
                            if (dist < f1)
                            {
                                f2 = f1;
                                f1 = dist;
                                bi = i;
                                bj = j;
                            }
                            else if (dist < f2)
                            {
                                f2 = dist;
                            }
                        }
                    }

                    // Distance to the joint in stone widths; the dome rises over the first ~third of the stone.
                    float edge = (f2 - f1) * Cells * 0.5f;
                    float joint = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.025f, 0.09f, edge));
                    float dome = Mathf.Sqrt(Mathf.Clamp01(edge / 0.32f));
                    float wear = Mathf.PerlinNoise(p.x * 40f + bi * 3.1f, p.y * 40f + bj * 1.7f) * 0.12f;
                    int k = y * Size + x;
                    height[k] = joint * (dome * lift[bi, bj] - wear);
                    occlusion[k] = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(edge / 0.16f)) * joint + (1f - joint) * 0.3f;
                    stoneTone[k] = joint > 0.5f ? tone[bi, bj] : -1f;
                }
            }

            // Albedo overlay (URP detail is x2: 0.5 = no change): stones a little lighter than the painted
            // ground with per-stone variation, crevices dark with grime.
            var albedo = new Color[Size * Size];
            for (int k = 0; k < albedo.Length; k++)
            {
                float x = k % Size, y = k / Size;
                float speck = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.08f + Mathf.PerlinNoise(x * 0.37f + 5f, y * 0.37f + 9f) * 0.05f;
                float v;
                Color tint;
                if (stoneTone[k] < 0f)
                {
                    v = 0.2f + speck;                                     // mortar and dirt
                    tint = new Color(1f, 0.97f, 0.92f);
                }
                else
                {
                    v = 0.5f + (stoneTone[k] - 0.5f) * 0.16f + speck - 0.04f;
                    tint = Color.Lerp(new Color(1.02f, 0.99f, 0.95f), new Color(0.95f, 0.98f, 1.03f), stoneTone[k]);   // warm and cool stones
                }

                v *= Mathf.Lerp(0.55f, 1f, occlusion[k]);
                albedo[k] = new Color(v * tint.r, v * tint.g, v * tint.b, height[k]);
            }

            // Tangent-space normals from the height field (central differences, wrapped).
            var normals = new Color[Size * Size];
            const float strength = 6f;
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

            var albedoTex = Save("L2_Cobble_Detail_Albedo", albedo, Size, Size, false, true);   // A: height (the blend and parallax)
            var normalTex = Save("L2_Cobble_Detail_Normal", normals, Size, Size, true, true);
            return (albedoTex, normalTex);
        }

        // ---------------------------------------------------------------- Mud

        public const float MudMetres = 4f;   // one mud tile covers 4 x 4 m

        // Trodden village mud, as an overlay (0.5 = the painted ground's own colour): soft lumps and hollows in
        // a few flat painted tones (the low-poly look), wetter and darker in the hollows, scattered pebbles,
        // and stalks of straw and ash flecks trodden in. Alpha carries the height (puddles fill its hollows).
        private static (Texture2D albedo, Texture2D normal) MudTextures()
        {
            const int size = 1024;
            var rng = new System.Random(23);
            var height = new float[size * size];
            var tone = new float[size * size];
            var tint = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float lumps = TileFbm(u, v, 4, 3, 11) * 0.65f + TileFbm(u, v, 16, 2, 29) * 0.35f;
                    float h = Mathf.Clamp01((lumps - 0.3f) / 0.45f);
                    int k = y * size + x;
                    height[k] = h;
                    // A few painted bands rather than a smooth gradient.
                    float band = Mathf.Lerp(h, Mathf.Round(h * 4f) / 4f, 0.55f);
                    float patch = TileFbm(u, v, 2, 3, 61);   // broad dry crusts and dark wet stretches
                    tone[k] = 0.38f + band * 0.12f + (patch - 0.5f) * 0.2f + (TileFbm(u, v, 32, 1, 5) - 0.5f) * 0.05f;
                    tint[k] = Color.Lerp(new Color(1.04f, 0.95f, 0.84f), new Color(1f, 0.98f, 0.95f), TileFbm(u, v, 3, 2, 41));
                }
            }

            // Pebbles: little domes, lighter and greyer than the mud.
            for (int i = 0; i < 1400; i++)
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                float r = 2f + (float)rng.NextDouble() * 4f;
                float shade = 0.56f + (float)rng.NextDouble() * 0.12f;
                for (int dy = -6; dy <= 6; dy++)
                {
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                        if (d > 1f)
                        {
                            continue;
                        }

                        int k = ((cy + dy + size) % size) * size + (cx + dx + size) % size;
                        height[k] = Mathf.Max(height[k], 0.55f + 0.35f * Mathf.Sqrt(1f - d * d));
                        tone[k] = shade * (0.85f + 0.15f * (1f - d));
                        tint[k] = new Color(0.97f, 0.98f, 1f);
                    }
                }
            }

            // Straw and twigs: short pale strokes; ash: tiny grey flecks.
            for (int i = 0; i < 520; i++)
            {
                float x = rng.Next(size), y = rng.Next(size);
                float a = (float)(rng.NextDouble() * Mathf.PI * 2f);
                int len = 8 + rng.Next(18);
                bool straw = rng.NextDouble() < 0.7;
                for (int t = 0; t < len; t++)
                {
                    int k = (((int)(y + Mathf.Sin(a) * t) % size + size) % size) * size + (((int)(x + Mathf.Cos(a) * t) % size + size) % size);
                    tone[k] = straw ? 0.55f : 0.34f;
                    tint[k] = straw ? new Color(1.12f, 0.98f, 0.72f) : new Color(0.95f, 0.92f, 0.9f);
                    height[k] = Mathf.Max(height[k], 0.5f);
                }
            }

            var albedo = new Color[size * size];
            for (int k = 0; k < albedo.Length; k++)
            {
                // Hollows are wetter and darker.
                float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.15f, height[k]));
                float v = tone[k] * Mathf.Lerp(1f, 0.78f, wet);
                albedo[k] = new Color(v * tint[k].r, v * tint[k].g, v * tint[k].b, height[k]);
            }

            var normals = new Color[size * size];
            const float strength = 3.2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float hl = height[y * size + (x - 1 + size) % size], hr = height[y * size + (x + 1) % size];
                    float hd = height[((y - 1 + size) % size) * size + x], hu = height[((y + 1) % size) * size + x];
                    var n = new Vector3((hl - hr) * strength, (hd - hu) * strength, 1f).normalized;
                    normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            return (Save("L2_Mud_Albedo", albedo, size, size, false, true), Save("L2_Mud_Normal", normals, size, size, true, true));
        }

        // Tileable fractal value noise: lattice values wrap at the tile edge, so the result repeats seamlessly.
        private static float TileFbm(float u, float v, int cells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                int c = cells << o;
                sum += ValueNoise(u * c, v * c, c, seed + o * 17) * amp;
                norm += amp;
                amp *= 0.5f;
            }

            return sum / norm;
        }

        private static float ValueNoise(float x, float y, int period, int seed)
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

        // ---------------------------------------------------------------- Street mask

        // In the painted ground's UV space (the terrain's UV0). Alpha carries the mask (URP Lit reads the detail
        // mask's alpha).
        private static Texture2D PavingMask()
        {
            const int width = 768;
            int height = Mathf.RoundToInt(width * L2Layout.GroundLength / L2Layout.GroundWidth);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float z = L2Layout.GroundMinZ + (y + 0.5f) / height * L2Layout.GroundLength;
                for (int x = 0; x < width; x++)
                {
                    float wx = L2Layout.GroundMinX + (x + 0.5f) / width * L2Layout.GroundWidth;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Weight(new Vector2(wx, z)));
                }
            }

            var tex = Save("L2_Paving_Mask", pixels, width, height, false, false);
            return tex;
        }

        // How much of the cobbled street shows at p: full on the street, fraying out over a ragged band at its
        // edge, thinned in worn patches; none on the country roads, the perch ramps or the weapon alley.
        public static float Weight(Vector2 p)
        {
            if (p.x < -52f || p.y > 132f)
            {
                return 0f;
            }

            foreach (var lane in new[] { "PerchRampW", "PerchRampE", "WeaponAlley" })
            {
                var nodes = L2Layout.Branches[lane];
                if (L2Layout.DistanceToSegment(p, nodes[0].P, nodes[1].P, out _) < nodes[0].HalfWidth + 0.4f)
                {
                    return 0f;
                }
            }

            float sd = L2Layout.SignedDistance(p);
            if (sd > 1.5f)
            {
                return 0f;
            }

            float ragged = (Mathf.PerlinNoise(p.x * 0.55f + 3f, p.y * 0.55f + 11f) - 0.5f) * 1.6f + (Mathf.PerlinNoise(p.x * 2.1f, p.y * 2.1f) - 0.5f) * 0.5f;
            float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.2f, 0.4f, sd + ragged));
            float worn = Mathf.PerlinNoise(p.x * 0.16f + 40f, p.y * 0.16f + 7f);
            w *= 1f - 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 0.8f, worn));

            // Past E5 the paving gives out: it breaks into islands of setts that thin and vanish into the farm track.
            float rural = L2Layout.Rural(p);
            if (rural > 0f)
            {
                float patch = Mathf.PerlinNoise(p.x * 0.3f + 61f, p.y * 0.3f + 23f) * 0.8f + Mathf.PerlinNoise(p.x * 1.1f, p.y * 1.1f) * 0.2f;
                w *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rural * 1.15f - 0.05f, rural * 1.15f + 0.12f, patch));
            }

            return Mathf.Clamp01(w);
        }

        private static Texture2D Save(string name, Color[] pixels, int width, int height, bool normalMap, bool repeat)
        {
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
            importer.sRGBTexture = !normalMap && repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.anisoLevel = repeat ? 4 : 1;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
