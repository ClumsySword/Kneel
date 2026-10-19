using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // L4's materials: the packs' palettes re-graded for a stronghold at noon under smoke, plain colours for
    // pitch, iron and fire, and the courtyard ground. Asset-only: safe to rerun at any time.
    public static class L4Look
    {
        private const string TexturesPath = L4Build.MaterialsPath + "/Textures";

        [MenuItem("Kneel/L4/Build/Materials")]
        public static void BuildMenu()
        {
            Debug.Log(Materials());
        }

        public static string Materials()
        {
            // Grey stone under a white, smoke-dimmed sun. Colour is nearly gone; what is left is cold in the
            // stone, brown in the timber, and the dried-blood red of old roof tiles and royal cloth.
            Grade("L4_Knights_Stone", "PolyKnights_Mat_01", Stone);
            Grade("L4_Knights_Soot", "PolyKnights_Mat_01", Soot);
            // Statues: old bronze, dark, greening where the weather got at it.
            Grade("L4_Knights_Bronze", "PolyKnights_Mat_01", Bronze);
            // Stone underfoot and in shade: stairs and the pack pieces that map onto the palette's pale cells.
            Grade("L4_Knights_StoneDark", "PolyKnights_Mat_01", (h, s, v) => Stone(h, s, v) * 0.68f);
            // The Adventure palette runs paler than the Knights one; taken down so carts and crates sit with the stone.
            Grade("L4_Adventure_Stone", "PolyAdventureMaterial_01", (h, s, v) => Stone(h, s, v) * 0.76f);
            Grade("L4_Adventure_Soot", "PolyAdventureMaterial_01", Soot);

            L4Build.Flat("L4_Pitch", "#0B0A09", 0.55f);
            L4Build.Flat("L4_Iron", "#1D1C1B", 0.35f);
            L4Build.Flat("L4_Ember", "#2A0E05", 0.05f, "#FF6A1A", 2.2f);
            L4Build.Flat("L4_Chalk", "#BDB8AE", 0.02f);

            Ground();
            // The stronghold's flagstones over L2's cobbles (L4Paving), every time the ground material is rebuilt.
            L4Paving.Apply();
            AssetDatabase.SaveAssets();
            return "L4 look: graded palettes, flat materials and L4_Ground written.";
        }

        // ---- Grades. Each takes a palette colour (hue, saturation, value) and returns what is left of it.

        private static Color Stone(float h, float s, float v)
        {
            bool green = h > 0.17f && h < 0.48f, red = h < 0.05f || h > 0.93f, warm = h >= 0.05f && h < 0.15f;
            if (green)
            {
                return Color.HSVToRGB(Mathf.Lerp(h, 0.11f, 0.7f), s * 0.2f, Mathf.Min(v * 0.55f, 0.42f));
            }

            if (red)
            {
                return Color.HSVToRGB(0.02f, s * 0.5f, Mathf.Min(v * 0.48f, 0.36f));
            }

            if (warm && s > 0.3f)
            {
                // Timber: weathered toward grey-brown.
                return Color.HSVToRGB(0.075f, s * 0.38f, Mathf.Min(v * 0.62f, 0.44f));
            }

            // Stone and everything else: toward a faintly cool grey.
            return Color.HSVToRGB(Mathf.Lerp(h, 0.09f, 0.5f), s * 0.22f, Mathf.Min(v * 0.8f, 0.68f));
        }

        private static Color Bronze(float h, float s, float v)
        {
            var metal = Color.HSVToRGB(0.07f, 0.5f, Mathf.Clamp(v * 0.42f, 0.06f, 0.3f));
            var verdigris = Color.HSVToRGB(0.46f, 0.32f, Mathf.Clamp(v * 0.55f, 0.1f, 0.4f));
            return Color.Lerp(metal, verdigris, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.8f, v)) * 0.4f);
        }

        // Burnt and smoke-blackened.
        private static Color Soot(float h, float s, float v)
        {
            return Color.HSVToRGB(Mathf.Lerp(h, 0.07f, 0.5f), s * 0.12f, Mathf.Min(v * 0.38f, 0.24f));
        }

        // Our own copy of a pack palette material on a re-graded copy of its texture (the pack's material and
        // texture are only read).
        private static void Grade(string name, string packMaterial, System.Func<float, float, float, Color> grade)
        {
            Material source = null;
            foreach (var guid in AssetDatabase.FindAssets(packMaterial + " t:Material", new[] { "Assets/SyntyStudios" }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && candidate.name == packMaterial)
                {
                    source = candidate;
                    break;
                }
            }

            if (source == null)
            {
                throw new System.ArgumentException("Missing pack material " + packMaterial);
            }

            var palette = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? source.GetTexture("_BaseMap") : source.mainTexture;
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(palette)));
            var px = pixels.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float sat, out float v);
                Color c = grade(h, sat, v);
                c.a = 1f;
                px[i] = c;
            }

            var graded = new Texture2D(pixels.width, pixels.height, TextureFormat.RGBA32, false);
            graded.SetPixels(px);
            var texture = Save(graded, name, false);
            Object.DestroyImmediate(pixels);

            string path = L4Build.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
        }

        // ---------------------------------------------------------------- Ground

        // The yards: L2's cobble and mud detail layers on Kneel/Ground, paved everywhere and dry (noon). The layout
        // map is one pale colour; the shader darkens it by its overlays, so it must be far paler than the result.
        private static void Ground()
        {
            var layout = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pave = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    layout.SetPixel(x, y, new Color(0.58f, 0.56f, 0.53f, 0f));
                    pave.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
            }

            var layoutTex = Save(layout, "L4_Ground_Layout", false);
            var paveTex = Save(pave, "L4_Ground_Pave", false);

            var mat = L4Build.LoadOrCreate("L4_Ground", Shader.Find("Kneel/Ground"));
            mat.SetTexture("_BaseMap", layoutTex);
            mat.SetTexture("_PaveMask", paveTex);
            string l2 = L2Build.MaterialsPath + "/Textures/";
            mat.SetTexture("_MudMap", AssetDatabase.LoadAssetAtPath<Texture2D>(l2 + "L2_Mud_Albedo.png"));
            mat.SetTexture("_MudNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(l2 + "L2_Mud_Normal.png"));
            mat.SetTexture("_CobbleMap", AssetDatabase.LoadAssetAtPath<Texture2D>(l2 + "L2_Cobble_Detail_Albedo.png"));
            mat.SetTexture("_CobbleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(l2 + "L2_Cobble_Detail_Normal.png"));
            mat.SetFloat("_MudTile", 4f);
            mat.SetFloat("_CobbleTile", 3f);
            mat.SetFloat("_CobbleNormalScale", 1.35f);
            mat.SetFloat("_Parallax", 0.03f);
            mat.SetFloat("_PuddleAmount", 0f);
            mat.SetFloat("_MossAmount", 0.12f);
            mat.SetColor("_MossColor", new Color(0.3f, 0.32f, 0.24f));
            EditorUtility.SetDirty(mat);
        }

        private static Texture2D Save(Texture2D tex, string name, bool alpha)
        {
            L2Build.EnsureFolder(TexturesPath);
            string path = TexturesPath + "/" + name + ".png";
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = alpha;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
