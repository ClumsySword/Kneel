using System.IO;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // L3's materials, generated meshes and tuning assets. Asset-only: safe to rerun at any time.
    public static class L3Look
    {
        private const string TexturesPath = L3Build.MaterialsPath + "/Textures";

        [MenuItem("Kneel/L3/Build/Materials, Meshes and Data")]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        public static string BuildAll()
        {
            string settings = L3Build.EnsureProjectSettings();
            Materials();
            Meshes();
            Data();
            AssetDatabase.SaveAssets();
            return "L3 look: " + settings + "; materials, meshes and data assets written.";
        }

        // ---------------------------------------------------------------- Materials

        public static void Materials()
        {
            // The packs' palettes re-graded for L3, the way L1 and L2 grade theirs: a copy of each palette
            // texture with every colour pushed toward ash and soot. Nothing in this world is clean any more.
            Grade("L3_Knights_Dawn", "PolyKnights_Mat_01", Dawn, false);
            Grade("L3_Adventure_Dawn", "PolyAdventureMaterial_01", Dawn, false);
            Grade("L3_Knights_Charred", "PolyKnights_Mat_01", Charred, false);
            Grade("L3_Adventure_Charred", "PolyAdventureMaterial_01", Charred, false);
            Grade("L3_Adventure_Hedge", "PolyAdventureMaterial_01", Withered, true);
            Grade("L3_Adventure_HedgeThin", "PolyAdventureMaterial_01", Scorched, true);
            Grade("L3_Knights_Earth", "PolyKnights_Mat_01", Earth, false);

            // Plain colours for the pieces built from primitives.
            L3Build.Flat("L3_WoodCharred", "#2A2420", 0.05f);
            L3Build.Flat("L3_Sack", "#7A6D55");
            L3Build.Flat("L3_Bone", "#A89E88", 0.15f);
            L3Build.Flat("L3_BoneDark", "#1B1612", 0.05f);
            L3Build.Flat("L3_HideDry", "#3B2C22", 0.05f);
            L3Build.Flat("L3_Stain", "#1E1712", 0.2f);
            L3Build.Flat("L3_Iron", "#1F1D1C", 0.3f);
            L3Build.Flat("L3_Crow", "#0D0D10", 0.25f);
            L3Build.Flat("L3_Lime", "#BDB9AC");

            // Dressed stone for the pieces built from L2PolyKit blocks: the same eight cells as its palettes
            // (top, top light, top dark, side, end, edge, iron, joint), in a paler dawn stone.
            StripPalette("L3_Palette_Stone", new[] { "#5C5852", "#67625B", "#514D48", "#46423D", "#716B63", "#7B746B", "#242120", "#3D3934" }, 0.12f);
            L3Crops.Materials();
            L3Hedges.Materials();
            L3Build.Flat("L3_Hound", "#191716", 0.15f);
            L3Build.Flat("L3_Brute", "#3B2A22", 0.1f);
            L3Build.Flat("L3_BruteRing", "#3A1C10", 0.05f, "#C4521E", 1.2f);
            L3Build.Flat("L3_BrandTip", "#3A1A0C", 0.05f, "#FF7A1E", 3f);

            // The greybox palette from the design note.
            L3Build.Flat("L3_GB_Lane", "#ADA693");
            L3Build.Flat("L3_GB_Arena", "#3A3027");
            L3Build.Flat("L3_GB_Stone", "#8A8A88");
            L3Build.Flat("L3_GB_Hedge", "#5E6B3C");
            L3Build.Flat("L3_GB_Bank", "#6B4F35");
            L3Build.Flat("L3_GB_Cover", "#C2A878");
            L3Build.Flat("L3_GB_Fire", "#7A2E08", 0.05f, "#FF6A14", 2.5f);
            L3Build.Flat("L3_GB_Ash", "#0C0C0C", 0.02f);
            L3Build.Flat("L3_GB_Mud", "#231912", 0.55f);
            L3Build.Flat("L3_GB_Damp", "#392C21", 0.45f);
            L3Build.Flat("L3_GB_Water", "#12233F", 0.8f);
            L3Build.Flat("L3_GB_MarkerSolid", "#FF00FF", 0f, "#FF00FF", 0.6f);
            L3Build.Transparent("L3_GB_Marker", new Color(1f, 0f, 1f, 0.22f));

            Handprint();
            ReviewGrid();
        }

        // ---- Grades. Each takes a palette colour (hue, saturation, value) and returns what the fire left of it.

        // Standing, unburnt things: timber, plaster, cloth and stone keep a ghost of their colour under the ash.
        // Roof tiles dry to the colour of old blood; anything green has withered.
        private static Color Dawn(float h, float s, float v)
        {
            bool green = h > 0.17f && h < 0.48f, red = h < 0.05f || h > 0.93f;
            if (green)
            {
                return Color.HSVToRGB(Mathf.Lerp(h, 0.115f, 0.65f), s * 0.24f, Mathf.Min(v * 0.6f, 0.46f));
            }

            if (red)
            {
                return Color.HSVToRGB(0.03f, s * 0.42f, Mathf.Min(v * 0.5f, 0.4f));
            }

            return Color.HSVToRGB(h, s * 0.4f, Mathf.Min(v * 0.68f, 0.58f));
        }

        // Burnt through: charcoal, with the warmth all but gone.
        private static Color Charred(float h, float s, float v)
        {
            bool warm = h < 0.12f || h > 0.9f;
            return Color.HSVToRGB(Mathf.Lerp(h, 0.07f, 0.4f), s * 0.16f, Mathf.Min(v * (warm ? 0.42f : 0.5f), 0.3f));
        }

        // Hedge leaves the heat killed without burning: brown-olive and brittle. The wood under them is dark.
        private static Color Withered(float h, float s, float v)
        {
            bool green = h > 0.17f && h < 0.48f;
            return green
                ? Color.HSVToRGB(Mathf.Lerp(h, 0.1f, 0.78f), s * 0.34f, Mathf.Min(v * 0.5f, 0.38f))
                : Color.HSVToRGB(h, s * 0.3f, Mathf.Min(v * 0.42f, 0.3f));
        }

        private static Color Scorched(float h, float s, float v)
        {
            Color c = Withered(h, s, v);
            return new Color(c.r * 0.62f, c.g * 0.6f, c.b * 0.6f);
        }

        // Stone turned to packed earth, for the banks.
        private static Color Earth(float h, float s, float v)
        {
            return Color.HSVToRGB(0.075f, Mathf.Max(s * 0.5f, 0.24f), Mathf.Min(v * 0.52f, 0.36f));
        }

        // Our own copy of a pack palette material on a re-graded copy of its texture (the pack's material and
        // texture are only read). 'wind' puts it on the project's wind shader, for leaves.
        internal static void Grade(string name, string packMaterial, System.Func<float, float, float, Color> grade, bool wind)
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
            L2Build.EnsureFolder(TexturesPath);
            string texturePath = TexturesPath + "/" + name + ".png";
            File.WriteAllBytes(texturePath, graded.EncodeToPNG());
            Object.DestroyImmediate(graded);
            Object.DestroyImmediate(pixels);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);

            string path = L3Build.MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find(wind ? "Kneel/Lit Wind" : "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
        }

        // A one-row palette texture with a URP Lit material on it; faces pick a cell by UV (point filtered).
        private static void StripPalette(string name, string[] tones, float smoothness)
        {
            var tex = new Texture2D(tones.Length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < tones.Length; i++)
            {
                tex.SetPixel(i, 0, L1Build.Hex(tones[i]));
            }

            var saved = SaveTexture(tex, name, false);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(saved));
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var mat = L3Build.Flat(name, "#FFFFFF", smoothness);
            mat.SetTexture("_BaseMap", saved);
            EditorUtility.SetDirty(mat);
        }

        // The scorched handprint: a black hand (alpha-clipped) with an ember edge in the emission map.
        private static void Handprint()
        {
            const int n = 128;
            var body = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var edge = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = HandDistance(new Vector2((x + 0.5f) / n, (y + 0.5f) / n));
                    body.SetPixel(x, y, new Color(0.02f, 0.015f, 0.012f, d < 0f ? 1f : 0f));
                    float glow = d < 0f ? Mathf.Clamp01(1f + d / 0.035f) : 0f;
                    edge.SetPixel(x, y, new Color(glow, glow, glow, 1f));
                }
            }

            var bodyTex = SaveTexture(body, "L3_Handprint", true);
            var edgeTex = SaveTexture(edge, "L3_Handprint_Edge", false);

            var mat = L3Build.Flat("L3_Handprint", "#FFFFFF", 0.02f);
            mat.SetTexture("_BaseMap", bodyTex);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetTexture("_EmissionMap", edgeTex);
            mat.SetColor("_EmissionColor", new Color(1.6f, 0.5f, 0.1f));
            EditorUtility.SetDirty(mat);
        }

        // Signed distance to a right hand, fingers up, in a unit square. Negative inside.
        private static float HandDistance(Vector2 p)
        {
            Vector2 palm = (p - new Vector2(0.5f, 0.33f));
            float d = (new Vector2(palm.x / 0.19f, palm.y / 0.2f).magnitude - 1f) * 0.19f;
            float[] lengths = { 0.3f, 0.37f, 0.35f, 0.27f };
            for (int i = 0; i < 4; i++)
            {
                var from = new Vector2(0.36f + 0.093f * i, 0.46f);
                float angle = (-12f + 8f * i) * Mathf.Deg2Rad;
                var to = from + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * lengths[i];
                d = Mathf.Min(d, SegmentDistance(p, from, to) - 0.04f);
            }

            return Mathf.Min(d, SegmentDistance(p, new Vector2(0.36f, 0.3f), new Vector2(0.15f, 0.47f)) - 0.045f);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // A 2 m grid tile for the review floor (the floor meshes carry world-space UVs, one tile per 2 m).
        private static void ReviewGrid()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color fill = L1Build.Hex("#3E3933"), line = L1Build.Hex("#26231F"), half = L1Build.Hex("#37322D");
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    bool border = x < 1 || y < 1 || x >= n - 1 || y >= n - 1;
                    bool mid = x == n / 2 || y == n / 2;
                    tex.SetPixel(x, y, border ? line : mid ? half : fill);
                }
            }

            var saved = SaveTexture(tex, "L3_Review_Grid", false);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(saved));
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();

            var mat = L3Build.Flat("L3_Review_Grid", "#FFFFFF", 0.02f);
            mat.SetTexture("_BaseMap", saved);
            EditorUtility.SetDirty(mat);
        }

        private static Texture2D SaveTexture(Texture2D tex, string name, bool alpha)
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
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- Meshes

        public static Mesh FlatQuad => AssetDatabase.LoadAssetAtPath<Mesh>(L3Build.MeshesPath + "/L3_FlatQuad.asset");

        public static Mesh BruteRing => AssetDatabase.LoadAssetAtPath<Mesh>(L3Build.MeshesPath + "/L3_Ring_4m.asset");

        // A faceted stone drum 1 m across and 1 m long, standing on its axis (y), centred on its middle.
        public static Mesh StoneDrum => AssetDatabase.LoadAssetAtPath<Mesh>(L3Build.MeshesPath + "/Props/L3_StoneDrum.asset");

        public static void Meshes()
        {
            // A 1 x 1 quad lying flat, facing up, pivot in the middle: ground decals scale it in x and z.
            var quad = new Mesh();
            quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            L3Build.SaveMesh(quad, "L3_FlatQuad");

            // The brute's reach, gouged into the dirt: a flat ring 4 m in radius.
            L3Build.SaveMesh(Ring(3.8f, 4f, 64), "L3_Ring_4m");

            L3Build.SaveMesh(Drum(14, 0.06f, 5), "Props", "L3_StoneDrum");
            L3Bones.BuildAll();
            L3Crops.Build();
        }

        // Millstones and the roller: a faceted cylinder with chamfered rims, each facet its own shade of the
        // stone palette, and a dark eye in each end.
        private static L3Mesh Drum(int sides, float bevel, int seed)
        {
            var rng = new System.Random(seed);
            var drum = new L3Mesh();
            Vector2 Cell(int cell) => new Vector2((cell + 0.5f) / 8f, 0.5f);
            const int top = 0, topLight = 1, topDark = 2, side = 3, end = 4, edge = 5, joint = 7;
            Vector3 Ring(int i, float radius, float y)
            {
                float a = i * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, d) = (d, b);
                }

                drum.Tri(0, a, b, c, uv);
                drum.Tri(0, a, c, d, uv);
            }

            for (int i = 0; i < sides; i++)
            {
                int j = i + 1;
                Vector3 outward = Ring(i, 1f, 0f) + Ring(j, 1f, 0f);
                // The side, split into an upper and a lower band so it reads as worked stone, not a tube.
                float seam = ((float)rng.NextDouble() - 0.5f) * 0.3f;
                Quad(Ring(i, 0.5f, -0.5f + bevel), Ring(j, 0.5f, -0.5f + bevel), Ring(j, 0.5f, seam), Ring(i, 0.5f, seam), Cell(rng.Next(2) == 0 ? side : end), outward);
                Quad(Ring(i, 0.5f, seam), Ring(j, 0.5f, seam), Ring(j, 0.5f, 0.5f - bevel), Ring(i, 0.5f, 0.5f - bevel), Cell(rng.Next(2) == 0 ? side : topDark), outward);
                foreach (float s in new[] { -1f, 1f })
                {
                    float y = 0.5f * s;
                    Vector3 face = Vector3.up * s;
                    Quad(Ring(i, 0.5f, y - bevel * s), Ring(j, 0.5f, y - bevel * s), Ring(j, 0.5f - bevel, y), Ring(i, 0.5f - bevel, y), Cell(edge), outward + face * 2f);
                    Quad(Ring(i, 0.5f - bevel, y), Ring(j, 0.5f - bevel, y), Ring(j, 0.1f, y), Ring(i, 0.1f, y), Cell(new[] { top, topLight, topDark }[rng.Next(3)]), face);
                    Quad(Ring(i, 0.1f, y), Ring(j, 0.1f, y), Ring(j, 0.1f, y - 0.04f * s), Ring(i, 0.1f, y - 0.04f * s), Cell(joint), -outward);
                    Quad(Ring(i, 0.1f, y - 0.04f * s), Ring(j, 0.1f, y - 0.04f * s), new Vector3(0f, y - 0.04f * s, 0f), new Vector3(0f, y - 0.04f * s, 0f), Cell(joint), face);
                }
            }

            return drum;
        }

        private static Mesh Ring(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
                uvs[i * 2] = new Vector2((float)i / segments, 0f);
                uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
                int j = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2;
                tris[t + 1] = j * 2;
                tris[t + 2] = i * 2 + 1;
                tris[t + 3] = i * 2 + 1;
                tris[t + 4] = j * 2;
                tris[t + 5] = j * 2 + 1;
            }

            var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- Data

        public static WindField Wind => Asset<WindField>("L3_Wind");

        public static FireTuning Fire => Asset<FireTuning>("L3_FireTuning");

        public static SurfaceTuning Mud => Asset<SurfaceTuning>("L3_MudTuning");

        public static GibbetTuning Gibbet => Asset<GibbetTuning>("L3_GibbetTuning");

        public static HazardTuning Contact => Asset<HazardTuning>("L3_HazardTuning");

        // Creates the tuning assets with the design note's values. Existing assets are left alone, so values the
        // owner has tuned are never overwritten.
        public static void Data()
        {
            Asset<WindField>("L3_Wind");
            Asset<FireTuning>("L3_FireTuning");
            Asset<SurfaceTuning>("L3_MudTuning");

            bool newGibbet = AssetDatabase.LoadAssetAtPath<GibbetTuning>(DataPath("L3_GibbetTuning")) == null;
            var gibbet = Asset<GibbetTuning>("L3_GibbetTuning");
            if (newGibbet)
            {
                gibbet.playerMask = 1 << L3Build.Layer("Player");
                EditorUtility.SetDirty(gibbet);
            }

            bool newContact = AssetDatabase.LoadAssetAtPath<HazardTuning>(DataPath("L3_HazardTuning")) == null;
            var contact = Asset<HazardTuning>("L3_HazardTuning");
            if (newContact)
            {
                contact.actorMask = (1 << L3Build.Layer("Player")) | (1 << L3Build.Layer("Enemy"));
                EditorUtility.SetDirty(contact);
            }
        }

        private static string DataPath(string name)
        {
            return L3Build.SettingsPath + "/" + name + ".asset";
        }

        private static T Asset<T>(string name) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(DataPath(name));
            if (asset == null)
            {
                L2Build.EnsureFolder(L3Build.SettingsPath);
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, DataPath(name));
            }

            return asset;
        }
    }
}
