using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools
{
    // L1's battlefield at the blue hour (owner, 2026-11-23, with a reference image: a cold blue-grey twilight, mist
    // over the field, puddles holding the pale sky, the only warmth Kael's own light). In place of the amber dusk:
    //  - Scene (run in each L1 scene): a cold, soft key light a little higher in the sky, blue ambient, blue-grey
    //    fog closer in.
    //  - Assets (shared by both scenes): the sky retinted, the global grade cooled (blue split-toning, cooler white
    //    balance), the ground fog cards cold, the puddles painted into the ground layout, and L1's look in LevelTransitions.asset (what L2's arrival leans toward).
    // Puddles exactly as L2's (owner: "make them like in L2"): L1's ground is drawn by L2's shader, Kneel/Ground,
    // over L2's mud texture. The layout's colour is L1's own (divided by the mud's mean, which the shader multiplies
    // back in); its alpha is now wetness, as that shader reads it: dry everywhere except the pools painted in the
    // terrain's dips and over some flats (0.14) and a damp shore round them (0.11). The shader fills the mud's
    // hollows to that level, so every puddle has a crisp, ragged waterline at screen resolution, flat, glossy water
    // that mirrors the lights, a darker damp halo, damped sky reflections and a faint cold sheen. They're painted
    // from an untouched copy of the layout (LayoutSources~, not imported), so reruns don't build up. If the layout is
    // repainted by another tool (L1Look, or Transitions' Build Seam 1), delete that copy and rerun this.
    // Safe to rerun. The fires, torches and the monastery keep their own light.
    public static class L1Twilight
    {
        private const string LayoutPath = L1Monastery.L1Folder + "/Materials/Textures/L1_Ground_Layout.png";
        private const string SourceFolder = "Assets/Kneel/Editor/L1/LayoutSources~";
        private const string SourcePath = SourceFolder + "/L1_Ground_Layout_PrePuddles.png";
        private const string GroundBackup = L1Monastery.L1Folder + "/Meshes/L1_Ground_PreMonastery.asset";
        private const string L2Textures = "Assets/Kneel/Materials/L2/Textures/";

        // Layout alpha as Kneel/Ground reads it (wetness): below 0.08 dry, 0.11 a damp shore, 0.14 standing water.
        private const float Dry = 0.04f, Damp = 0.11f, Pool = 0.14f;

        public static readonly Color KeyColour = new Color(0.64f, 0.73f, 0.88f);
        public static readonly Color FogColour = new Color(0.3f, 0.36f, 0.46f);
        public static readonly Color AmbientSky = new Color(0.5f, 0.6f, 0.78f) * 0.9f;
        public static readonly Color AmbientEquator = new Color(0.34f, 0.4f, 0.52f) * 0.9f;
        public static readonly Color AmbientGround = new Color(0.17f, 0.18f, 0.21f) * 0.9f;

        [MenuItem("Kneel/L1/Look/Apply Twilight")]
        public static void ApplyMenu()
        {
            Debug.Log("[L1 Twilight] " + ApplyAssets() + " " + ApplyScene());
        }

        // ---------------------------------------------------------------- Scene

        public static string ApplyScene()
        {
            var root = L1LevelTools.Root;
            if (root == null)
            {
                return "No L1 root in the open scene.";
            }

            var sun = root.transform.Find("_Lighting/Sun").GetComponent<Light>();
            sun.useColorTemperature = false;
            sun.color = KeyColour;
            sun.intensity = 0.75f;   // L1's grade adds +1.35 stops
            sun.shadowStrength = 0.5f;
            // Still from the north-west, but up in the sky: a soft twilight, not long black shadows.
            sun.transform.rotation = Quaternion.Euler(32f, 135f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColour;
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 62f;
            RenderSettings.reflectionIntensity = 0.8f;

            EditorSceneManager.MarkSceneDirty(root.scene);
            return "Scene: cold key light, blue ambient, blue-grey fog.";
        }

        // ---------------------------------------------------------------- Assets

        public static string ApplyAssets()
        {
            var log = new StringBuilder();

            var sky = AssetDatabase.LoadAssetAtPath<Material>(L1Monastery.L1Folder + "/Materials/L1_DuskSky.mat");
            if (sky != null)
            {
                sky.SetColor("_SkyTint", new Color(0.42f, 0.52f, 0.72f));
                sky.SetColor("_GroundColor", new Color(0.28f, 0.31f, 0.37f));
                sky.SetFloat("_AtmosphereThickness", 0.62f);
                sky.SetFloat("_Exposure", 0.85f);
                sky.SetFloat("_SunSize", 0.02f);
                EditorUtility.SetDirty(sky);
            }

            var global = AssetDatabase.LoadAssetAtPath<VolumeProfile>(L1Monastery.L1Folder + "/Lighting/Volumes/L1_Global.asset");
            if (global.TryGet<SplitToning>(out var split))
            {
                split.shadows.Override(new Color(0.18f, 0.3f, 0.46f));
                split.highlights.Override(new Color(0.62f, 0.72f, 0.86f));
                split.balance.Override(10f);
            }

            if (global.TryGet<WhiteBalance>(out var balance))
            {
                balance.temperature.Override(-16f);
            }

            if (global.TryGet<ColorAdjustments>(out var colour))
            {
                colour.saturation.Override(-22f);
                colour.contrast.Override(14f);
            }

            EditorUtility.SetDirty(global);

            // Start (outside the monastery door) was graded the level's darkest spot; at twilight it matches the field.
            var start = AssetDatabase.LoadAssetAtPath<VolumeProfile>(L1Monastery.L1Folder + "/Lighting/Volumes/L1_Zone_Start.asset");
            if (start != null && start.TryGet<ColorAdjustments>(out var startColour))
            {
                startColour.postExposure.Override(1.3f);
                EditorUtility.SetDirty(start);
            }

            var fog = AssetDatabase.LoadAssetAtPath<Material>(L1Monastery.L1Folder + "/Materials/FX/L1_FX_GroundFog.mat");
            if (fog != null)
            {
                var c = fog.GetColor("_BaseColor");
                fog.SetColor("_BaseColor", new Color(0.5f, 0.58f, 0.7f, c.a));
                EditorUtility.SetDirty(fog);
            }

            log.Append(Puddles()).Append(' ');

            // The ground on L2's shader, over L2's mud. Puddle cover 1 turns the shader's own scatter off: the water
            // stands only where the layout is painted wet. No cobbles (no paving mask), no moss on a battlefield.
            var ground = AssetDatabase.LoadAssetAtPath<Material>(L1Monastery.L1Folder + "/Materials/L1_Ground.mat");
            var layout = AssetDatabase.LoadAssetAtPath<Texture2D>(LayoutPath);
            ground.shader = Shader.Find("Kneel/Ground");
            ground.SetTexture("_BaseMap", layout);
            ground.SetTextureScale("_BaseMap", Vector2.one);
            ground.SetTextureOffset("_BaseMap", Vector2.zero);
            ground.SetTexture("_PaveMask", null);
            ground.SetTexture("_MudMap", SoftMud());
            ground.SetTexture("_MudNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(L2Textures + "L2_Mud_Normal.png"));
            ground.SetTexture("_CobbleMap", AssetDatabase.LoadAssetAtPath<Texture2D>(L2Textures + "L2_Cobble_Detail_Albedo.png"));
            ground.SetTexture("_CobbleNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(L2Textures + "L2_Cobble_Detail_Normal.png"));
            ground.SetFloat("_MudTile", 5f);
            // L2's mud bumps at full strength read as a rash of bubbles under Kael's lantern, which grazes the ground.
            ground.SetFloat("_MudNormalScale", 0.15f);
            ground.SetFloat("_PuddleScale", 4.5f);
            ground.SetFloat("_PuddleAmount", 1f);
            ground.SetFloat("_PuddleDepth", 1.4f);
            ground.SetFloat("_MossAmount", 0f);
            ground.SetColor("_PuddleSky", new Color(0.12f, 0.15f, 0.21f));
            EditorUtility.SetDirty(ground);
            log.Append(TransitionLook());
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // L1's own copy of L2's mud: its colour pulled most of the way to its mean (under the cold light the pebbles
        // read as pale specks over the whole field, like gravel or snow), its height (A), which shapes the puddles'
        // waterlines, kept as it is. The shader centres the overlay on its mean, so the layout's colour still rules.
        private static Texture2D SoftMud()
        {
            string path = L1Monastery.L1Folder + "/Materials/Textures/L1_Mud_Soft.png";
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(L2Textures + "L2_Mud_Albedo.png"));
            var px = src.GetPixels();
            var mean = Color.clear;
            foreach (var c in px)
            {
                mean += c;
            }

            mean /= px.Length;
            for (int i = 0; i < px.Length; i++)
            {
                var c = Color.Lerp(px[i], mean, 0.65f);
                c.a = px[i].a;
                px[i] = c;
            }

            src.SetPixels(px);
            src.Apply();
            File.WriteAllBytes(path, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var from = (TextureImporter)AssetImporter.GetAtPath(L2Textures + "L2_Mud_Albedo.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = from.sRGBTexture;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = from.anisoLevel;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // L1's look as the level transitions remember it (L2's arrival leans back towards it).
        private static string TransitionLook()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Kneel/Core/Transitions/Resources/LevelTransitions.asset");
            if (catalog == null)
            {
                return "No LevelTransitions.asset.";
            }

            var so = new SerializedObject(catalog);
            var looks = so.FindProperty("looks");
            for (int i = 0; i < looks.arraySize; i++)
            {
                var look = looks.GetArrayElementAtIndex(i);
                if (!look.FindPropertyRelative("scene").stringValue.StartsWith("L1_BrokenLine"))
                {
                    continue;
                }

                look.FindPropertyRelative("fogColor").colorValue = FogColour;
                look.FindPropertyRelative("ambientSky").colorValue = AmbientSky;
                look.FindPropertyRelative("ambientEquator").colorValue = AmbientEquator;
                look.FindPropertyRelative("ambientGround").colorValue = AmbientGround;
                so.ApplyModifiedPropertiesWithoutUndo();
                return "Transition look updated.";
            }

            return "No L1 look in LevelTransitions.asset.";
        }

        // ---------------------------------------------------------------- Puddles

        public static string Puddles()
        {
            Directory.CreateDirectory(SourceFolder);
            if (!File.Exists(SourcePath))
            {
                File.Copy(LayoutPath, SourcePath);
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(SourcePath));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels();
            Object.DestroyImmediate(tex);

            // Ground heights (the original battlefield, before the monastery dipped it), 1 m grid.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundBackup) ?? AssetDatabase.LoadAssetAtPath<Mesh>(L1Monastery.L1Folder + "/Meshes/L1_Ground.asset");
            var v = mesh.vertices;
            const int cols = 115;
            int rows = v.Length / cols;
            float minZ = v[0].z;
            var height = new float[cols, rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    height[i, j] = v[j * cols + i].y;
                }
            }

            // How far each point lies below its surroundings (a 3 m box blur of the heights minus the height).
            var dip = new float[cols, rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int dj = -3; dj <= 3; dj++)
                    {
                        for (int di = -3; di <= 3; di++)
                        {
                            int a = Mathf.Clamp(i + di, 0, cols - 1), b = Mathf.Clamp(j + dj, 0, rows - 1);
                            sum += height[a, b];
                            n++;
                        }
                    }

                    dip[i, j] = sum / n - height[i, j];
                }
            }

            float Dip(float x, float z)
            {
                float fx = Mathf.Clamp(x - L1Layout.GroundMinX, 0f, cols - 1.001f), fz = Mathf.Clamp(z - minZ, 0f, rows - 1.001f);
                int i = (int)fx, j = (int)fz;
                float tx = fx - i, tz = fz - j;
                return Mathf.Lerp(Mathf.Lerp(dip[i, j], dip[i + 1, j], tx), Mathf.Lerp(dip[i, j + 1], dip[i + 1, j + 1], tx), tz);
            }

            // The shader multiplies the layout's colour by the mud overlay x2 (its mean is about 0.96, 0.91, 0.85):
            // divide that back out so L1 keeps its own colours.
            var mudTex = new Texture2D(2, 2);
            mudTex.LoadImage(File.ReadAllBytes(L2Textures + "L2_Mud_Albedo.png"));
            var mudPx = mudTex.GetPixels();
            Object.DestroyImmediate(mudTex);
            var mean = Color.clear;
            foreach (var m in mudPx)
            {
                mean += m;
            }

            mean /= mudPx.Length;
            // Lifted 1.35x: this ground is matte where it's dry (the old material's gloss had brightened it).
            const float lift = 1.35f;
            var gain = new Color(0.5f / mean.r * lift, 0.5f / mean.g * lift, 0.5f / mean.b * lift);

            int puddlePixels = 0;
            for (int y = 0; y < h; y++)
            {
                float z = L1Layout.GroundMinZ + (y + 0.5f) / h * L1Layout.GroundLength;
                for (int x = 0; x < w; x++)
                {
                    int k = y * w + x;
                    var c = px[k];
                    float wx = L1Layout.GroundMinX + (x + 0.5f) / w * L1Layout.GroundWidth;
                    // Water gathers in the dips; broken up by noise so the edges wander and some flats hold pools too.
                    float noise = Mathf.PerlinNoise(wx * 0.23f + 41f, z * 0.23f + 17f) * 0.65f + Mathf.PerlinNoise(wx * 0.9f + 5f, z * 0.9f + 3f) * 0.35f;
                    float wet = Dip(wx, z) * 7f + (noise - 0.63f) * 2.4f;
                    float shore = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wet / 0.25f));
                    float pool = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((wet - 0.25f) / 0.12f));
                    // Dry ground stays below the shader's wet threshold (0.08) everywhere: any wetness there and the
                    // water rises into every pebble hollow of the mud, a glossy speck per hollow across the field.
                    float a = Dry;
                    a = Mathf.Lerp(a, Damp, shore);
                    a = Mathf.Lerp(a, Pool, pool);
                    px[k] = new Color(Mathf.Clamp01(c.r * gain.r), Mathf.Clamp01(c.g * gain.g), Mathf.Clamp01(c.b * gain.b), a);
                    if (pool > 0.5f)
                    {
                        puddlePixels++;
                    }
                }
            }

            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            File.WriteAllBytes(LayoutPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceUpdate);
            float share = puddlePixels * 100f / (w * h);
            return $"Puddles: {share:F1}% of the ground painted for standing water.";
        }
    }
}
