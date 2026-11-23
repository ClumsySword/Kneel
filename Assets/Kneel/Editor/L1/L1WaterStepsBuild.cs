using System.IO;
using Kneel.Audio;
using Kneel.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Builds L1's water steps (Kneel.Levels.L1WaterSteps): the splash sound event, the ripple and droplet particles, the
    // puddle mask, and the prefab, and places it in the playable scene (only there). Safe to rerun: everything is
    // rebuilt in place, and the scene instance keeps its place.
    //
    // The sounds are single steps cut from CC0 Freesound recordings of footsteps in puddles and shallow water (see
    // Audio/CREDITS.md), one step per file in Levels/L1/Audio/FootstepWater, each starting on the foot's contact. The
    // event takes every clip in that folder, so add or remove steps there and rerun this.
    public static class L1WaterStepsBuild
    {
        private const string L1 = L1Monastery.L1Folder;
        private const string AudioFolder = L1 + "/Audio";
        private const string ClipFolder = AudioFolder + "/FootstepWater";
        private const string EventPath = AudioFolder + "/L1_FootstepWater.asset";
        private const string TextureFolder = L1 + "/Materials/Textures";
        private const string RingTexturePath = TextureFolder + "/L1_WaterRing.png";
        private const string DropTexturePath = TextureFolder + "/L1_WaterDrop.png";
        private const string WetMaskPath = TextureFolder + "/L1_WetMask.png";
        private const string LayoutPath = TextureFolder + "/L1_Ground_Layout.png";
        private const string RingMaterialPath = L1 + "/Materials/L1_WaterRing.mat";
        private const string DropMaterialPath = L1 + "/Materials/L1_WaterDrop.mat";
        private const string PrefabPath = L1 + "/Prefabs/L1_WaterSteps.prefab";
        private const string PlayableScene = "Assets/Kneel/Scenes/Levels/L1_BrokenLine_Playable.unity";

        // The layout's wetness (alpha) from damp to standing water, as L1Twilight paints it (0.11 and 0.14).
        private const float WetFrom = 0.115f, WetTo = 0.135f;
        private const int MaskDownsample = 4;

        [MenuItem("Kneel/L1/Water Steps/Build")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "L1 water steps: leave Play mode first.";
            }

            Folder(AudioFolder);
            Folder(ClipFolder);
            var log = new System.Text.StringBuilder();
            log.Append(Sounds()).Append(' ');
            SoundEvent splash = Event();
            Material ring = ParticleMaterial(RingMaterialPath, Texture(RingTexturePath, 256, RingAlpha), new Color(0.72f, 0.88f, 0.9f, 0.3f));
            Material drop = ParticleMaterial(DropMaterialPath, Texture(DropTexturePath, 32, DropAlpha), new Color(0.8f, 0.93f, 0.95f, 0.45f));
            Texture2D mask = WetMask(out float share);
            log.Append($"Wet mask: {share:F1}% of the battlefield ground counts as standing water. ");
            GameObject prefab = Prefab(splash, ring, drop, mask);
            log.Append(Place(prefab));
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ Sounds

        // The recordings' import settings: mono, decompressed on load (short and played often).
        private static string Sounds()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.forceToMono = true;
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.8f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                count++;
            }

            return $"{count} water steps in the sound event.";
        }

        // Every clip in the folder, with the footsteps' numbers.
        private static SoundEvent Event()
        {
            var splash = AssetDatabase.LoadAssetAtPath<SoundEvent>(EventPath);
            if (splash == null)
            {
                splash = ScriptableObject.CreateInstance<SoundEvent>();
                splash.volume = 0.6f;
                splash.volumeSpread = 0.12f;
                splash.pitch = 1f;
                splash.pitchSpread = 0.1f;
                splash.group = SoundGroup.SFX;
                splash.spatial = 0.9f;
                splash.minDistance = 2f;
                splash.maxDistance = 18f;
                splash.cooldown = 0f;
                AssetDatabase.CreateAsset(splash, EventPath);
            }

            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
            {
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            splash.clips = clips.ToArray();
            EditorUtility.SetDirty(splash);
            return splash;
        }

        // ------------------------------------------------------------------ Particles

        // A soft, thin ring, broken along its length (a ripple catches the light unevenly), with a faint trailing
        // wave inside it. A crisp double ring read as a target.
        private static float RingAlpha(float r, float angle)
        {
            float outer = Mathf.Exp(-Mathf.Pow((r - 0.43f) / 0.045f, 2f));
            float inner = Mathf.Exp(-Mathf.Pow((r - 0.32f) / 0.05f, 2f)) * 0.22f;
            float a = angle / (2f * Mathf.PI);
            float broken = 0.45f + 0.55f * Mathf.Clamp01(0.5f + 0.5f * Mathf.Sin(a * 6.28f * 3f + 1.3f) * Mathf.Sin(a * 6.28f * 5f + 0.4f) + 0.25f * Mathf.Sin(a * 6.28f * 2f));
            return Mathf.Clamp01((outer + inner) * broken);
        }

        private static float DropAlpha(float r, float angle) => Mathf.Pow(Mathf.Clamp01(1f - r * 2f), 1.5f);

        private static Texture2D Texture(string path, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var d = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(d.magnitude, Mathf.Atan2(d.y, d.x) + Mathf.PI)));
                }
            }

            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // URP's unlit particle shader, alpha-blended, over the water (which draws in the transparent queue).
        private static Material ParticleMaterial(string path, Texture2D texture, Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Rings(ParticleSystem system, Material material)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 1.2f;
            main.startSpeed = 0f;
            main.startSize = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            // Out fast, then slowing (a ring's spread), and fading as it goes.
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 18f, 18f), new Keyframe(0.35f, 4.2f, 4f, 4f), new Keyframe(1f, 6f, 0.5f, 0.5f)));
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.45f, 0.45f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -10f;
        }

        private static void Droplets(ParticleSystem system, Material material)
        {
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = 0.3f;
            main.startSpeed = 0f;
            main.startSize = 0.03f;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ------------------------------------------------------------------ Puddles

        // White where the layout holds standing water, at a quarter of its resolution (44 cm), readable at runtime.
        private static Texture2D WetMask(out float share)
        {
            var layout = new Texture2D(2, 2);
            layout.LoadImage(File.ReadAllBytes(LayoutPath));
            int w = layout.width / MaskDownsample, h = layout.height / MaskDownsample;
            Color32[] px = layout.GetPixels32();
            int sourceWidth = layout.width;
            Object.DestroyImmediate(layout);

            var mask = new Texture2D(w, h, TextureFormat.RGB24, false);
            var output = new Color[w * h];
            int wet = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int j = 0; j < MaskDownsample; j++)
                    {
                        for (int i = 0; i < MaskDownsample; i++)
                        {
                            float a = px[(y * MaskDownsample + j) * sourceWidth + x * MaskDownsample + i].a / 255f;
                            sum += Mathf.Clamp01((a - WetFrom) / (WetTo - WetFrom));
                        }
                    }

                    float v = sum / (MaskDownsample * MaskDownsample);
                    output[y * w + x] = new Color(v, v, v);
                    if (v >= 0.5f)
                    {
                        wet++;
                    }
                }
            }

            mask.SetPixels(output);
            mask.Apply();
            File.WriteAllBytes(WetMaskPath, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(WetMaskPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(WetMaskPath);
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            share = 100f * wet / (w * h);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(WetMaskPath);
        }

        // ------------------------------------------------------------------ Prefab and scene

        private static GameObject Prefab(SoundEvent splash, Material ring, Material drop, Texture2D mask)
        {
            var root = new GameObject("L1_WaterSteps");
            var steps = root.AddComponent<L1WaterSteps>();
            var rings = new GameObject("Rings").AddComponent<ParticleSystem>();
            rings.transform.SetParent(root.transform, false);
            Rings(rings, ring);
            var droplets = new GameObject("Droplets").AddComponent<ParticleSystem>();
            droplets.transform.SetParent(root.transform, false);
            Droplets(droplets, drop);

            steps.rings = rings;
            steps.droplets = droplets;
            steps.splash = splash;
            steps.wetMask = mask;
            steps.groundRect = new Rect(L1Layout.GroundMinX, L1Layout.GroundMinZ, L1Layout.GroundWidth, L1Layout.GroundLength);
            steps.hallCentre = new Vector2(L1Monastery.HallCentre.x, L1Monastery.HallCentre.y);
            steps.hallRadius = L1Monastery.HallApothem + 0.4f;
            steps.doorStrip = new Rect(-(L1Monastery.DoorHalf + 0.7f), L1Monastery.HallCentre.y, 2f * (L1Monastery.DoorHalf + 0.7f), L1Monastery.HallApothem + 1.2f);
            steps.waterY = L1Monastery.WaterY;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static string Place(GameObject prefab)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != PlayableScene)
            {
                return "Prefab built; open the playable scene and rerun to place it.";
            }

            var flow = GameObject.Find("LevelFlow");
            if (flow == null)
            {
                return "Prefab built; no LevelFlow in the playable scene, not placed.";
            }

            Transform existing = flow.transform.Find("L1_WaterSteps");
            if (existing == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(flow.transform, false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Placed under LevelFlow in the playable scene.";
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
