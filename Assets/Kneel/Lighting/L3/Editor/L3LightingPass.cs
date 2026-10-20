using System.Collections.Generic;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.Lighting.EditorTools
{
    // The look of L3 "The Fallow Fields": dawn, but a dawn seen through the smoke of a kingdom burning its own
    // harvest. A low, tired sun in the east throws long shadows to screen-left and just reaches the sides of
    // things that face the camera; the smoke fills the shadows with cold grey and swallows the distance. Colour is nearly
    // gone from the world (ash, soot, dry straw), so the only saturated things on screen are fire and embers.
    // Same recipe as L1 and L2 (trilight ambient, linear fog, ACES with a cold-shadow / ember-highlight split),
    // moved from night to a sick morning. Re-runnable: it rebuilds its own objects under the given parent.
    public static class L3LightingPass
    {
        public const string AssetsPath = "Assets/Kneel/Lighting/L3";
        private const string VolumesPath = AssetsPath + "/Volumes";

        // ---------------------------------------------------------------- Tuning (judged in the Game view)

        // The sun: low in the east and a little to the south, so shadows fall to screen-left and slightly
        // up-screen, and south-facing walls (the ones the camera sees) are lit. From the north-east it left
        // everything the player looks at in its own shadow.
        private const float SunElevation = 30f;
        private const float SunYaw = 292f;
        private const float SunIntensity = 2.6f;
        private static readonly Color SunColor = Hex("#FFC489");

        // As in L2, the ambient is scaled up: at face value it reads as black under ACES from this camera.
        private const float AmbientBoost = 2.2f;
        private static readonly Color AmbientSky = Hex("#59637A") * AmbientBoost;
        private static readonly Color AmbientEquator = Hex("#4B4744") * AmbientBoost;
        private static readonly Color AmbientGround = Hex("#1D1A18") * AmbientBoost;

        // Smoke, lit from behind.
        private static readonly Color FogColor = Hex("#6F655B");
        private const float FogStart = 24f;
        private const float FogEnd = 120f;

        private static readonly Color FillColor = Hex("#B4C0D4");
        private const float FillIntensity = 0.55f;

        private const float Exposure = 1.7f;
        private const float Contrast = 20f;
        private const float Saturation = -34f;

        [MenuItem("Kneel/L3/Lighting/Apply To Open Scene")]
        public static void ApplyMenu()
        {
            var root = GameObject.Find("L3_Root") ?? GameObject.Find(L3Review.RootName);
            if (root == null)
            {
                Debug.LogWarning("Open the L3 level or L3_AssetReview first.");
                return;
            }

            Debug.Log(Apply(root.transform));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        }

        // Builds the sun, ambient, fog, sky, character fill and the global grade under parent/_Lighting.
        public static string Apply(Transform parent)
        {
            L2Build.EnsureFolder(VolumesPath);
            var lighting = L1Build.Group(parent, "_Lighting", false);
            Sun(lighting);
            Environment();
            CharacterFill(lighting);
            GlobalGrade(lighting);
            string characters = TagCharacters();
            AssetDatabase.SaveAssets();
            return "L3 lighting applied. " + characters;
        }

        private static GameObject Named(Transform lighting, string name)
        {
            var existing = lighting.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(lighting, false);
            return go;
        }

        private static void Sun(Transform lighting)
        {
            var go = Named(lighting, "Sun");
            go.transform.rotation = Quaternion.Euler(SunElevation, SunYaw, 0f);
            var sun = go.GetComponent<Light>() != null ? go.GetComponent<Light>() : go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.renderingLayerMask = -1;
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
        }

        private static void Environment()
        {
            // The sky above the smoke: a copy of L1's procedural sky, kept with L3's materials.
            string skyPath = L3Build.MaterialsPath + "/L3_DawnSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                L2Build.EnsureFolder(L3Build.MaterialsPath);
                AssetDatabase.CopyAsset(L1Build.MaterialsPath + "/L1_DuskSky.mat", skyPath);
                sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            }

            // Dim: the grade lifts the whole frame by 1.7 stops, and a bright sky would blow out to white in
            // every vista. What is left is a sallow band over the horizon under a dark, smoke-filled sky.
            sky.SetColor("_SkyTint", Hex("#806B5C"));
            sky.SetFloat("_AtmosphereThickness", 1.5f);
            sky.SetColor("_GroundColor", Hex("#2A2622"));
            sky.SetFloat("_Exposure", 0.28f);
            sky.SetFloat("_SunSize", 0.035f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = FogEnd;
            DynamicGI.UpdateEnvironment();
        }

        // A shadowless cold light from behind the camera that only touches the Characters rendering layer, so
        // the player and enemies never sink into their own shadow side against the low sun.
        private static void CharacterFill(Transform lighting)
        {
            var go = Named(lighting, "CharacterFill");
            var light = go.GetComponent<Light>() != null ? go.GetComponent<Light>() : go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = FillColor;
            light.intensity = FillIntensity;
            light.shadows = LightShadows.None;
            uint characters = (uint)RenderingLayerMask.GetMask(L1LightingPass.CharactersLayer);
            light.renderingLayerMask = (int)characters;
            var data = go.GetComponent<UniversalAdditionalLightData>() != null ? go.GetComponent<UniversalAdditionalLightData>() : go.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = characters;
            go.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
        }

        private static string TagCharacters()
        {
            var player = L1LightingShots.PlayerRoot();
            if (player == null || Camera.main == null)
            {
                return "No player or camera in the scene: characters not tagged.";
            }

            L1LightingShots.SetCharacterLayer(player.gameObject);
            var cam = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            if (cam == null)
            {
                cam = Camera.main.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cam.volumeTrigger = player;
            cam.volumeLayerMask = 1;
            cam.renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
            return "Player on the Characters layer; post-processing on.";
        }

        private static void GlobalGrade(Transform lighting)
        {
            var profile = Profile("L3_Global");
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(Saturation);

            // Cold, greenish shadows under an ember-coloured light: the sun is the same colour as the fires.
            var split = Get<SplitToning>(profile);
            split.shadows.Override(Hex("#2F5A62"));
            split.highlights.Override(Hex("#D89650"));
            split.balance.Override(-12f);
            Get<WhiteBalance>(profile).temperature.Override(4f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.38f);
            bloom.scatter.Override(0.68f);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.8f);
            Get<ChromaticAberration>(profile).intensity.Override(0.1f);
            EditorUtility.SetDirty(profile);

            var go = Named(lighting, "GlobalVolume");
            var volume = go.GetComponent<Volume>() != null ? go.GetComponent<Volume>() : go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = profile;
        }

        private static VolumeProfile Profile(string name)
        {
            string path = VolumesPath + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (var component in new List<VolumeComponent>(profile.components))
                {
                    profile.Remove(component.GetType());
                    Object.DestroyImmediate(component, true);
                }
            }

            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                return existing;
            }

            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
