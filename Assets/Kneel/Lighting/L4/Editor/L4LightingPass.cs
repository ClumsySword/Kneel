using System.Collections.Generic;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.Lighting.EditorTools
{
    // The look of L4 "The Burning Gate" (owner, 2026-10-21: "feel like endgame dark souls level... gritty and
    // hardcore"): an ashen, overcast end of the world. A dull sun smothered low in grey ash, cold slate shadows, a
    // pale ash haze closing in at 34-220 m, the grade drained (saturation -30, contrast 28) so only fire, lava and
    // the master's crimson keep their colour; heavy vignette and grain, ash falling over the route (L4Atmosphere).
    // The character fill keeps the fights readable. Before that,
    // dusk over a stronghold that burns (owner, 2026-10-19: "a bit later at day,
    // this should feel ambient with the fires"; before that an afternoon sun at 47 degrees, and noon at 52). A low
    // orange sun from the west-south-west rakes the walls, the sky is a burnt red band under smoke, the shadows go
    // violet, and the fires, the lava and the braziers carry the frame. Earlier notes on the noon look follow:
    // the sun was high and hard,
    // a little south of overhead, so the yards are bright where the sky is open and the walls the camera sees
    // are lit; the smoke above takes the warmth out of it and fills the shadows with grey. Grey stone and black
    // pitch carry the frame, and the only saturated colour is fire: oil burning orange, braziers gold.
    // Same recipe as L1-L3 (trilight ambient, linear fog, ACES with a split tone). Re-runnable: it rebuilds its
    // own objects under the given parent.
    public static class L4LightingPass
    {
        public const string AssetsPath = "Assets/Kneel/Lighting/L4";
        private const string VolumesPath = AssetsPath + "/Volumes";

        // ---------------------------------------------------------------- Tuning (judged in the Game view)

        // High and from the south-south-west: shadows fall short and up-screen, south faces are lit. A true noon
        // sun (60 degrees and more) leaves the walls the camera looks at half as bright as the yards.
        private const float SunElevation = 18f;
        private const float SunYaw = 55f;
        private const float SunIntensity = 1.9f;
        private static readonly Color SunColor = Hex("#E2A47A");

        private const float AmbientBoost = 3.1f;
        private static readonly Color AmbientSky = Hex("#3E4248") * AmbientBoost;
        private static readonly Color AmbientEquator = Hex("#4E4640") * AmbientBoost;
        private static readonly Color AmbientGround = Hex("#201C1A") * AmbientBoost;

        // Smoke: haze builds only beyond the arenas (decision 10), so the fog starts past a yard's width.
        private static readonly Color FogColor = Hex("#5E5650");
        private const float FogStart = 34f;
        private const float FogEnd = 220f;

        private static readonly Color FillColor = Hex("#C2C8D2");
        private const float FillIntensity = 0.6f;

        private const float Exposure = 1.5f;
        private const float Contrast = 28f;
        private const float Saturation = -30f;

        [MenuItem("Kneel/L4/Lighting/Apply To Open Scene")]
        public static void ApplyMenu()
        {
            var root = GameObject.Find("L4_Root") ?? GameObject.Find(L4Build.ReviewRootName);
            if (root == null)
            {
                Debug.LogWarning("Open the L4 level or L4_AssetReview first.");
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
            return "L4 lighting applied. " + characters;
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
            sun.shadowStrength = 0.8f;
            sun.renderingLayerMask = -1;
            RenderSettings.sun = sun;
            EditorUtility.SetDirty(sun);
        }

        private static void Environment()
        {
            string skyPath = L4Build.MaterialsPath + "/L4_SmokeSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                L2Build.EnsureFolder(L4Build.MaterialsPath);
                AssetDatabase.CopyAsset(L1Build.MaterialsPath + "/L1_DuskSky.mat", skyPath);
                sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            }

            // A smoke-filled noon sky: flat grey-brown, brighter toward the sun. The camera looks down past the
            // walls into the sky's lower half, which fog does not reach, so its ground colour is the smoke's: the
            // distance dissolves into haze instead of going black.
            // A red tint on the procedural sky cuts its blue and leaves green; a grey-brown one with a thicker
            // atmosphere gives the burnt orange band at the horizon instead.
            sky.SetColor("_SkyTint", Hex("#5A5452"));
            sky.SetFloat("_AtmosphereThickness", 2.6f);
            sky.SetColor("_GroundColor", Hex("#2A2624"));
            sky.SetFloat("_Exposure", 0.85f);
            sky.SetFloat("_SunSize", 0.09f);
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

        // A shadowless cool light that only touches the Characters rendering layer, so the player and enemies
        // keep a readable silhouette against grey stone.
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
            if (Camera.main != null)
            {
                var cam = Camera.main.GetComponent<UniversalAdditionalCameraData>();
                if (cam == null)
                {
                    cam = Camera.main.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                cam.volumeTrigger = player != null ? player : Camera.main.transform;
                cam.volumeLayerMask = 1;
                cam.renderPostProcessing = true;
                EditorUtility.SetDirty(cam);
            }

            if (player == null)
            {
                return "No player in the scene: characters not tagged.";
            }

            L1LightingShots.SetCharacterLayer(player.gameObject);
            return "Player on the Characters layer; post-processing on.";
        }

        private static void GlobalGrade(Transform lighting)
        {
            var profile = Profile("L4_Global");
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(Saturation);

            // Grey-blue shadows under a pale, smoke-yellowed light; fire is the only warmth that survives.
            var split = Get<SplitToning>(profile);
            split.shadows.Override(Hex("#2A3236"));
            split.highlights.Override(Hex("#BC8A5E"));
            split.balance.Override(-25f);
            Get<WhiteBalance>(profile).temperature.Override(-6f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.44f);
            vignette.smoothness.Override(0.4f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.65f);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.36f);
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
