using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Palette-level look for L1: regiment colours, sky, and the painted ground texture.
    public static class L1Look
    {
        private const string TexturesPath = L1Build.MaterialsPath + "/Textures";
        private const string SyntyCharacterTextures = "Assets/SyntyStudios/PolygonKnights/Textures/";

        // ---------------------------------------------------------------- Regiments

        [MenuItem("Kneel/L1/Look/Rebuild Regiment Materials")]
        public static void RebuildRegimentsMenu()
        {
            Debug.Log("[L1] " + RebuildRegiments());
        }

        // Kael's army: slate armour, dried-blood red cloth (matches the red standards).
        // The enemy: near-black iron and cloth. Red against black reads instantly from above.
        // Team-colour cells are found by diffing Synty's Blue and Orange team textures.
        public static string RebuildRegiments()
        {
            var blue = Load(SyntyCharacterTextures + "Characters_Texture_Blue.png");
            var orange = Load(SyntyCharacterTextures + "Characters_Texture_Orange.png");
            var black = Load(SyntyCharacterTextures + "Characters_Texture_Black.png");
            var bluePx = blue.GetPixels();
            var orangePx = orange.GetPixels();
            var blackPx = black.GetPixels();
            int teamCells = 0;

            var kael = new Color[bluePx.Length];
            var enemy = new Color[bluePx.Length];
            for (int i = 0; i < bluePx.Length; i++)
            {
                var c = bluePx[i];
                bool team = Distance(c, orangePx[i]) > 0.18f;
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (team)
                {
                    teamCells++;
                    kael[i] = Color.HSVToRGB(0.995f, 0.62f, Mathf.Lerp(0.2f, 0.36f, v));
                    enemy[i] = Color.HSVToRGB(0.07f, 0.12f, Mathf.Lerp(0.07f, 0.15f, v));
                }
                else
                {
                    bool blood = (h < 0.03f || h > 0.96f) && s > 0.4f;
                    kael[i] = blood ? Color.HSVToRGB(0.995f, Mathf.Min(s * 0.7f, 0.6f), v * 0.55f) : Color.HSVToRGB(h, s * 0.32f, v * 0.72f);

                    Color.RGBToHSV(blackPx[i], out float bh, out float bs, out float bv);
                    enemy[i] = Color.HSVToRGB(bh, bs * 0.2f, bv * 0.62f);
                }

                kael[i].a = 1f;
                enemy[i].a = 1f;
            }

            Save(kael, blue.width, blue.height, "L1_Regiment_Kael", "L1_Regiment_Kael", new Color(0.86f, 0.86f, 0.88f));
            Save(enemy, blue.width, blue.height, "L1_Regiment_Enemy", "L1_Regiment_Enemy", Color.white);
            Object.DestroyImmediate(blue);
            Object.DestroyImmediate(orange);
            Object.DestroyImmediate(black);
            return $"Regiments rebuilt ({100 * teamCells / bluePx.Length}% of texels are team colour).";
        }

        private static float Distance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }

        // ---------------------------------------------------------------- Sky

        [MenuItem("Kneel/L1/Look/Retint Sky")]
        public static void RetintSky()
        {
            var sky = L1Build.Mat("L1_DuskSky");
            sky.SetColor("_SkyTint", L1Build.Hex("#4E5977"));
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_GroundColor", L1Build.Hex("#2A2622"));
            sky.SetFloat("_Exposure", 0.95f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- Ground

        [MenuItem("Kneel/L1/Look/Repaint Ground")]
        public static void RepaintGroundMenu()
        {
            Debug.Log("[L1] " + RepaintGround());
        }

        // Paints the ground layout texture: trampled path, clean compact arena floors, wet mud patches,
        // wheel ruts along the corridors, churned mud at arena rims, and dried blood under bodies.
        // Alpha carries smoothness (wet mud reads glossy).
        public static string RepaintGround()
        {
            const int width = 1024;
            const int height = 3712;
            float pixelsPerMetre = width / L1Layout.GroundWidth;
            var pixels = new Color[width * height];

            Color outside = L1Build.Hex("#2A2623"), walk = L1Build.Hex("#39322B"), arenaFloor = L1Build.Hex("#453D34");
            Color wet = L1Build.Hex("#1D1917"), blood = L1Build.Hex("#3A1813");
            var arenas = new List<L1Layout.Circle>(L1Layout.Arenas);

            for (int y = 0; y < height; y++)
            {
                float z = L1Layout.GroundMinZ + (y + 0.5f) / height * L1Layout.GroundLength;
                for (int x = 0; x < width; x++)
                {
                    float wx = L1Layout.GroundMinX + (x + 0.5f) / width * L1Layout.GroundWidth;
                    var p = new Vector2(wx, z);
                    float sd = L1Layout.SignedDistance(p);

                    float wWalk = 1f - SmoothStep(-0.8f, 1.6f, sd + (Fbm(wx * 0.4f, z * 0.4f) - 0.5f) * 1.4f);
                    float wArena = 0f, rim = 0f;
                    foreach (var a in arenas)
                    {
                        float d = (p - a.Center).magnitude;
                        wArena = Mathf.Max(wArena, 1f - SmoothStep(a.Radius - 1.8f, a.Radius + 0.3f, d));
                        rim = Mathf.Max(rim, SmoothStep(a.Radius - 0.6f, a.Radius + 0.4f, d) * (1f - SmoothStep(a.Radius + 1.5f, a.Radius + 4.5f, d)));
                    }

                    var c = Color.Lerp(outside, walk, wWalk);
                    c = Color.Lerp(c, arenaFloor, wArena * 0.9f);

                    float mud = SmoothStep(0.56f, 0.68f, Fbm(wx * 0.07f + 31f, z * 0.07f + 17f)) * (1f - wArena);
                    c = Color.Lerp(c, wet, mud * 0.85f);

                    // Churned mud where the fighting pooled at each arena's edge.
                    float churn = rim * SmoothStep(0.35f, 0.6f, Fbm(wx * 0.5f + 7f, z * 0.5f + 3f));
                    c = Color.Lerp(c, wet, churn * 0.7f);

                    float grain = Mathf.PerlinNoise(wx * 1.3f, z * 1.3f);
                    float variation = Mathf.Lerp(0.3f, 0.1f, wArena);
                    c *= 1f - variation / 2f + variation * grain;
                    c.a = Mathf.Lerp(0.1f, 0.5f, Mathf.Max(mud, churn * 0.8f));
                    pixels[y * width + x] = c;
                }
            }

            // Wheel ruts: two wobbling wet grooves down each main corridor, fading out before the arenas.
            foreach (var capsule in L1Layout.Capsules)
            {
                if (capsule.HalfWidth < 4f)
                {
                    continue;
                }

                Vector2 ab = capsule.B - capsule.A;
                Vector2 n = new Vector2(-ab.y, ab.x).normalized;
                float length = ab.magnitude;
                float offset = (Mathf.PerlinNoise(capsule.A.x, capsule.A.y) - 0.5f) * 2f;
                for (float t = 0f; t <= length; t += 0.05f)
                {
                    Vector2 centre = capsule.A + ab * (t / length) + n * offset;
                    if (L1Layout.IsInArena(centre, 1.5f))
                    {
                        continue;
                    }

                    float wobble = (Mathf.PerlinNoise(t * 0.15f, offset * 3f) - 0.5f) * 0.6f;
                    foreach (float track in new[] { -0.85f, 0.85f })
                    {
                        Stamp(pixels, width, height, pixelsPerMetre, centre + n * (track + wobble), 0.2f, wet, 0.45f, 0.42f);
                    }
                }
            }

            // Dried blood under the dead (never on arena floors).
            var root = L1LevelTools.Root;
            var stains = new List<Vector3>();
            foreach (var group in new[] { "SetDressing/Corpses", "SetDressing/Monsters" })
            {
                var t = root.transform.Find(group);
                if (t == null)
                {
                    continue;
                }

                foreach (Transform child in t)
                {
                    stains.Add(child.position);
                }
            }

            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("L1_CorpsePile") || t.name.StartsWith("L1_Lore_Rearguard") || t.name.StartsWith("L1_Lore_FallenBanner") || t.name == "L1_BatteringRam")
                {
                    stains.Add(t.position);
                    stains.Add(t.position + new Vector3(1.2f, 0f, 0.8f));
                }
            }

            var rng = new System.Random(5);
            int painted = 0;
            foreach (var s in stains)
            {
                if (rng.NextDouble() < 0.35)
                {
                    continue;
                }

                var sp = new Vector2(s.x + (float)(rng.NextDouble() - 0.5) * 1.2f, s.z + (float)(rng.NextDouble() - 0.5) * 1.2f);
                if (L1Layout.IsInArena(sp, 0.5f))
                {
                    continue;
                }

                float radius = 0.7f + (float)rng.NextDouble() * 1.1f;
                Stamp(pixels, width, height, pixelsPerMetre, sp, radius, blood, 0.55f, 0.32f, true);
                painted++;
            }

            // The ground albedo is authored dark; lift it into a physically plausible wet-mud range.
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                pixels[i] = new Color(Mathf.Clamp01(c.r * 1.87f), Mathf.Clamp01(c.g * 1.85f), Mathf.Clamp01(c.b * 1.82f), c.a);
            }

            string path = TexturesPath + "/L1_Ground_Layout.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return $"Ground repainted ({painted} blood stains, ruts on {L1Layout.Capsules.Length} corridors).";
        }

        private static void Stamp(Color[] pixels, int width, int height, float ppm, Vector2 world, float radius, Color colour, float strength, float smoothness, bool noisyEdge = false)
        {
            int cx = (int)((world.x - L1Layout.GroundMinX) * ppm);
            int cy = (int)((world.y - L1Layout.GroundMinZ) / L1Layout.GroundLength * height);
            int r = (int)(radius * ppm) + 2;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    float dx = (x - cx) / ppm, dy = (y - cy) / ppm;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = noisyEdge ? radius * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.15f + world.x, y * 0.15f + world.y)) : radius;
                    float w = (1f - SmoothStep(edge * 0.5f, edge, d)) * strength;
                    if (w <= 0f)
                    {
                        continue;
                    }

                    var c = pixels[y * width + x];
                    float a = c.a;
                    c = Color.Lerp(c, colour * (0.85f + 0.3f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f)), w);
                    c.a = Mathf.Max(a, smoothness * w / strength);
                    pixels[y * width + x] = c;
                }
            }
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Fbm(float x, float z)
        {
            return Mathf.PerlinNoise(x, z) * 0.55f + Mathf.PerlinNoise(x * 2.1f + 7f, z * 2.1f + 3f) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 13f, z * 4.3f + 19f) * 0.15f;
        }

        // ---------------------------------------------------------------- IO

        private static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        private static void Save(Color[] pixels, int width, int height, string textureName, string materialName, Color tint)
        {
            string path = TexturesPath + "/" + textureName + ".png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            var mat = L1Build.Mat(materialName);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }
    }
}
