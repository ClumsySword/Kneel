using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Palette-level look for L2: the night copies of the Synty palettes, the night sky and the painted ground.
    // Synty materials are never edited; everything here is a copy under Assets/Kneel/Materials/L2.
    public static class L2Look
    {
        private const string TexturesPath = L2Build.MaterialsPath + "/Textures";
        private const string KnightsMaterial = "Assets/SyntyStudios/PolygonKnights/Materials/PolyKnights_Mat_01.mat";
        private const string AdventureMaterial = "Assets/SyntyStudios/PolygonAdventure/Materials/PolyAdventureMaterial_01.mat";

        [MenuItem("Kneel/L2/Look/Rebuild All")]
        public static void RebuildAllMenu()
        {
            Debug.Log("[L2] " + RebuildMaterials() + " " + RepaintGround() + " " + RebuildSky());
        }

        // ---------------------------------------------------------------- Palettes

        [MenuItem("Kneel/L2/Look/Rebuild Materials")]
        public static void RebuildMaterialsMenu()
        {
            Debug.Log("[L2] " + RebuildMaterials());
        }

        // Burnt: the Synty palette pushed toward soot. Values drop, saturation mostly goes, and warm hues (roof tiles,
        // timber) lean to charcoal so nothing reads pink under the moon.
        // Faded: cloth and painted plaster keep a desaturated hint of their colour (the village's last colour).
        // Charred: the black-brown for the inside of burnt shells.
        public static string RebuildMaterials()
        {
            L2Build.EnsureFolder(TexturesPath);
            string knights = PaletteSource(KnightsMaterial);
            string adventure = PaletteSource(AdventureMaterial);

            Recolour(knights, "L2_Knights_Burnt", (h, s, v) => Burnt(h, s, v));
            Recolour(knights, "L2_Knights_Faded", (h, s, v) => Faded(h, s, v));
            Recolour(adventure, "L2_Adventure_Burnt", (h, s, v) => Burnt(h, s, v));
            Recolour(adventure, "L2_Adventure_Faded", (h, s, v) => Faded(h, s, v));
            Recolour(knights, "L2_Knights_Night", (h, s, v) => Night(h, s, v));
            Recolour(adventure, "L2_Adventure_Night", (h, s, v) => Night(h, s, v));

            // Soot: the burnt palette darkened further for stonework, whose flat tops otherwise catch the moon
            // and read as pale slabs.
            foreach (var (from, to) in new[] { ("L2_Knights_Burnt", "L2_Knights_Soot"), ("L2_Adventure_Burnt", "L2_Adventure_Soot") })
            {
                var soot = L2Build.CopyMaterial(L2Build.MaterialsPath + "/" + from + ".mat", to);
                soot.SetColor("_BaseColor", L1Build.Hex("#6E6D72"));
                soot.SetFloat("_Smoothness", 0.08f);
                EditorUtility.SetDirty(soot);
            }

            var charred = L2Build.CopyMaterial(L1Build.MaterialsPath + "/L1_Knights_Charred.mat", "L2_Knights_Charred");
            charred.SetColor("_BaseColor", L1Build.Hex("#8C8680"));
            EditorUtility.SetDirty(charred);

            // The raiders' colours on props (banners, rags): the Vanguard's dried-blood red as a flat colour.
            // (L1_Regiment_Vanguard is a character texture; prop UVs would sample the wrong cells.)
            string clothPath = L2Build.MaterialsPath + "/L2_Vanguard_Cloth.mat";
            var cloth = AssetDatabase.LoadAssetAtPath<Material>(clothPath);
            if (cloth == null)
            {
                cloth = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(cloth, clothPath);
            }

            cloth.SetColor("_BaseColor", L1Build.Hex("#5E1814"));
            cloth.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(cloth);
            AssetDatabase.SaveAssets();
            return "Night palettes rebuilt.";
        }

        private static Color Burnt(float h, float s, float v)
        {
            bool warm = h < 0.12f || h > 0.9f;
            float value = v * (warm ? 0.5f : 0.62f);
            return Color.HSVToRGB(Mathf.Lerp(h, 0.08f, 0.3f), s * (warm ? 0.18f : 0.25f), Mathf.Min(value, 0.42f));
        }

        // Night: the untouched village under the moon. Timber, plaster and thatch keep a muted version of their
        // colour (lived in, not burnt); greens go to dull olive-grey so nothing reads fresh.
        private static Color Night(float h, float s, float v)
        {
            bool green = h > 0.18f && h < 0.48f;
            return Color.HSVToRGB(green ? Mathf.Lerp(h, 0.12f, 0.5f) : h, s * (green ? 0.22f : 0.5f), Mathf.Min(v * 0.74f, 0.62f));
        }

        private static Color Faded(float h, float s, float v)
        {
            return Color.HSVToRGB(h, s * 0.38f, Mathf.Min(v * 0.62f, 0.5f));
        }

        // The palette texture a Synty material samples (read only; the material itself is never touched).
        private static string PaletteSource(string materialPath)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var tex = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            return AssetDatabase.GetAssetPath(tex);
        }

        private static void Recolour(string sourceTexture, string name, System.Func<float, float, float, Color> recolour)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(sourceTexture));
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                var c = recolour(h, s, v);
                c.a = 1f;
                px[i] = c;
            }

            string texPath = TexturesPath + "/" + name + ".png";
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(texPath);

            var mat = L2Build.CopyMaterial(L1Build.MaterialsPath + "/L1_Knights_Ash.mat", name);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(mat);
        }

        // ---------------------------------------------------------------- Sky

        [MenuItem("Kneel/L2/Look/Rebuild Sky")]
        public static void RebuildSkyMenu()
        {
            Debug.Log("[L2] " + RebuildSky());
        }

        public static string RebuildSky()
        {
            var sky = L2Build.CopyMaterial(L1Build.MaterialsPath + "/L1_DuskSky.mat", "L2_NightSky");
            sky.SetColor("_SkyTint", L1Build.Hex("#2B3550"));
            sky.SetFloat("_AtmosphereThickness", 0.7f);
            sky.SetColor("_GroundColor", L1Build.Hex("#141312"));
            sky.SetFloat("_Exposure", 0.35f);
            sky.SetFloat("_SunSize", 0.02f);
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            return "Night sky rebuilt.";
        }

        // ---------------------------------------------------------------- Ground

        [MenuItem("Kneel/L2/Look/Repaint Ground")]
        public static void RepaintGroundMenu()
        {
            Debug.Log("[L2] " + RepaintGround());
        }

        // The walkable path is packed earth, a shade lighter and cooler than the ash and scorched soil around it,
        // so the moonlit road always reads. Plateau earth is a little paler (churchyard clay), and soot and ember
        // scorch pools gather round the fires. Alpha carries smoothness.
        public static string RepaintGround()
        {
            L2Build.EnsureFolder(TexturesPath);
            const int width = 1536;
            int height = Mathf.RoundToInt(width * L2Layout.GroundLength / L2Layout.GroundWidth);
            var pixels = new Color[width * height];

            Color outside = L1Build.Hex("#221F1D"), path = L1Build.Hex("#4A4A4C"), clay = L1Build.Hex("#4C4640");
            Color soot = L1Build.Hex("#141212"), ash = L1Build.Hex("#55524F");

            for (int y = 0; y < height; y++)
            {
                float z = L2Layout.GroundMinZ + (y + 0.5f) / height * L2Layout.GroundLength;
                for (int x = 0; x < width; x++)
                {
                    float wx = L2Layout.GroundMinX + (x + 0.5f) / width * L2Layout.GroundWidth;
                    var p = new Vector2(wx, z);
                    float sd = L2Layout.SignedDistance(p);
                    float wPath = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 1.4f, sd + (Fbm(wx * 0.45f, z * 0.45f) - 0.5f) * 1.3f));

                    var c = Color.Lerp(outside, path, wPath);
                    if (L2Layout.Height(p) > 3f)
                    {
                        c = Color.Lerp(c, clay, wPath * 0.6f);
                    }

                    // Ash drifts: pale grey streaks, thicker off the path.
                    float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.75f, Fbm(wx * 0.09f + 13f, z * 0.05f + 7f)));
                    c = Color.Lerp(c, ash, drift * (0.25f + 0.35f * (1f - wPath)));

                    // Soot: charred patches everywhere but thin on the trodden middle.
                    float sootMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.7f, Fbm(wx * 0.11f + 31f, z * 0.11f + 17f)));
                    c = Color.Lerp(c, soot, sootMask * (1f - wPath * 0.6f) * 0.8f);

                    float grain = Mathf.PerlinNoise(wx * 1.4f, z * 1.4f);
                    c *= 0.9f + 0.2f * grain;

                    // The creek's banks and bed: dark, wet mud.
                    float creek = L2Layout.CreekDistance(p, out _);
                    if (creek < L2Layout.CreekHalfWidth + 1.2f)
                    {
                        float bank = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(L2Layout.WaterHalfWidth, L2Layout.CreekHalfWidth + 1.2f, creek));
                        c = Color.Lerp(c, L1Build.Hex("#1E1C19"), bank * 0.8f);
                        c.a = Mathf.Lerp(c.a, 0.2f, bank);
                    }

                    // The fields at the level's end: ploughed earth in ridge and furrow, water standing in the troughs.
                    // The road between them is a farm track: two wet wheel ruts and a straw-strewn crown. Both fade in
                    // across the rural band (L2Layout.Rural), never along a line.
                    float fieldWet = 0f, trackWet = -1f;
                    float rural = L2Layout.Rural(p);
                    if (rural > 0.01f && sd < 0.5f)
                    {
                        float fromCentre = L2Farmland.RoadDistance(p) + (Fbm(wx * 0.6f, z * 0.6f) - 0.5f) * 0.3f;
                        float rut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(fromCentre - 0.95f) / 0.3f);
                        float crown = 1f - Mathf.SmoothStep(0f, 1f, fromCentre / 0.55f);
                        c = Color.Lerp(c, L1Build.Hex("#100E0B"), rut * 0.85f * rural);
                        c = Color.Lerp(c, L1Build.Hex("#5C5241"), crown * 0.45f * rural);
                        // A damp track, wettest in the ruts: puddles stand along them, smaller ones on the crown.
                        trackWet = 0.12f + rut * 0.08f;
                    }

                    if (L2Farmland.InField(p, out var rows, out float fieldBlend))
                    {
                        float ridge = L2Farmland.Furrow(p, rows);
                        float inField = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f, 2.6f, sd));
                        var tilled = Color.Lerp(L1Build.Hex("#1A1714"), L1Build.Hex("#73665A"), ridge) * (0.88f + 0.24f * grain);
                        c = Color.Lerp(c, tilled, inField * 0.9f * fieldBlend);
                        fieldWet = Mathf.Max(fieldWet, (1f - ridge) * inField * 0.12f * fieldBlend);
                    }

                    // Bare earth sits darker than the cobbled streets (L2Paving), which keep this paint's full value.
                    c *= Mathf.Lerp(0.62f, 1f, L2Paving.Weight(p));
                    float villageWet = 0.08f + Mathf.Max(0.12f * wPath, fieldWet);
                    c.a = trackWet >= 0f ? Mathf.Lerp(villageWet, trackWet, rural) : villageWet;
                    pixels[y * width + x] = c;
                }
            }

            // Scorch around every fire: burning lots, fire beds and fire blockers.
            foreach (var lot in L2Layout.KeyLots)
            {
                if (lot.Kind == L2Layout.LotKind.Burning || lot.Kind == L2Layout.LotKind.BurningLow)
                {
                    Stamp(pixels, width, height, lot.Center, Mathf.Max(lot.Size.x, lot.Size.y) * 0.75f, soot, 0.4f);
                }
            }

            // Light soot only round the fires: black ground would swallow their light pools.
            foreach (var z in L2Layout.FireZones)
            {
                Stamp(pixels, width, height, z.Center, Mathf.Max(z.Size.x, z.Size.y) * 0.8f, soot, 0.35f);
            }

            foreach (var b in L2Layout.Blockers)
            {
                Stamp(pixels, width, height, b.Center, Mathf.Max(b.Size.x, b.Size.y) * 0.9f, soot, 0.45f);
            }

            string texPath = TexturesPath + "/L2_Ground_Layout.png";
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var mat = L2Build.CopyMaterial(L1Build.MaterialsPath + "/L1_Ground.mat", "L2_Ground");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            // Point the terrain chunks at the painted material.
            var root = L2Build.Root;
            if (root != null)
            {
                var terrain = root.transform.Find("Terrain");
                foreach (var r in terrain.GetComponentsInChildren<MeshRenderer>())
                {
                    if (r.name.StartsWith("L2_Ground"))
                    {
                        r.sharedMaterial = mat;
                    }
                }

                L2Build.MarkDirty();
            }

            return "Ground repainted.";
        }

        private static void Stamp(Color[] pixels, int width, int height, Vector2 world, float radius, Color colour, float strength)
        {
            float ppm = width / L2Layout.GroundWidth;
            int cx = (int)((world.x - L2Layout.GroundMinX) * ppm);
            int cy = (int)((world.y - L2Layout.GroundMinZ) / L2Layout.GroundLength * height);
            int r = (int)(radius * ppm) + 2;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height)
                    {
                        continue;
                    }

                    float d = new Vector2((x - cx) / ppm, (y - cy) / ppm).magnitude;
                    float edge = radius * (0.75f + 0.5f * Mathf.PerlinNoise(x * 0.12f + world.x, y * 0.12f + world.y));
                    float w = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge * 0.4f, edge, d))) * strength;
                    if (w > 0f)
                    {
                        var c = pixels[y * width + x];
                        float a = c.a;
                        c = Color.Lerp(c, colour, w);
                        c.a = a;
                        pixels[y * width + x] = c;
                    }
                }
            }
        }

        private static float Fbm(float x, float z)
        {
            return Mathf.PerlinNoise(x, z) * 0.55f + Mathf.PerlinNoise(x * 2.1f + 7f, z * 2.1f + 3f) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 13f, z * 4.3f + 19f) * 0.15f;
        }
    }
}
