using System.Collections.Generic;
using System.IO;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.Lighting.EditorTools
{
    // Lighting pass for L1 "The Broken Line": Dark Souls melancholy (low dusk sun rimming silhouettes,
    // haze, a bonfire-like safe spot) with Diablo's top-down pools of light (lit arenas, darker corridors).
    // Warmth is rare: only the sun, fire and the ember get it. Safe to re-run; it rebuilds its own objects.
    public static class L1LightingPass
    {
        public const string CharactersLayer = "Characters";

        public const string AssetsPath = "Assets/Kneel/Lighting/L1";
        private const string VolumesPath = AssetsPath + "/Volumes";
        private const string MaterialsPath = AssetsPath + "/Materials";
        private const string TexturesPath = AssetsPath + "/Textures";

        // ---------------------------------------------------------------- Tuning

        // Sun: a low dusk sun from the north-west. At ~9 degrees it barely lights flat ground but rims
        // everything standing, which is what carries the silhouettes.
        private const float SunElevation = 9f;
        private const float SunYaw = 135f;               // light travels south-east, i.e. comes from the north-west
        private const float SunIntensity = 3.2f;
        private static readonly Color SunColor = Hex("#FF9559");

        // The brief's gradient, scaled up: at face value it reads as black under ACES from this camera.
        private const float AmbientBoost = 1.6f;
        private static readonly Color AmbientSky = Hex("#3E4A5C") * AmbientBoost;
        private static readonly Color AmbientEquator = Hex("#2B303A") * AmbientBoost;
        private static readonly Color AmbientGround = Hex("#1A1816") * AmbientBoost;

        private static readonly Color FogColor = Hex("#3B3E46");
        private const float FogStart = 22f;
        private const float FogEnd = 100f;

        // Well above the brief's 0.3-0.5: the fill is the one light that separates characters from the floor
        // (ambient and the arena pools light both), and the readability test uses near-black armour.
        private static readonly Color FillColor = Hex("#A9BCD6");
        private const float FillIntensity = 1.0f;

        // Grade. The brief's -0.4 starting exposure crushes this ambient under ACES, so exposure is set
        // by eye and by the greyscale readability test (Kneel/L1/Lighting/Readability Test).
        private const float Exposure = 1.35f;
        private const float Contrast = 20f;
        private const float Saturation = -35f;

        // Arena pools: a high, soft, cool spot per arena so the stage is even and readable.
        private const float ArenaPoolHeight = 18f;
        private const float ArenaPoolIntensity = 100f;
        private static readonly Color ArenaPoolColor = Hex("#B7C2D3");

        private static readonly Color ShrineColor = Hex("#FF9A52");
        private const float ShrineIntensity = 6f;
        private static readonly Color ExitRed = Hex("#8E2418");
        private static readonly Color ExitMoonColor = Hex("#8FA6D1");

        // ---------------------------------------------------------------- Entry point

        [MenuItem("Kneel/L1/Lighting/Apply Lighting Pass")]
        public static void ApplyMenu()
        {
            Debug.Log("[L1] " + Apply());
        }

        public static string Apply()
        {
            EnsureFolders();
            var log = new List<string>();
            log.Add(EnsureCharactersRenderingLayer());

            var root = L1LevelTools.Root;
            var lighting = root.transform.Find("_Lighting");

            Sun();
            Environment();
            CharacterFill(lighting);
            log.Add(TagCharacters());
            GlobalGrade(lighting);

            var zones = L1Build.Group(lighting, "Zones", true);
            ZoneVolumes(zones);
            ArenaPools(lighting);
            BannerSliver(zones, root.transform);
            RamBreak(zones);
            Shrine(zones, root.transform);
            log.Add(Exit(zones, root.transform));
            log.Add(ShadowPolicy(root.transform));

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();
            return "Lighting pass applied. " + string.Join(" ", log);
        }

        // ---------------------------------------------------------------- Project-wide: Rendering Layer

        // Renames the unused Rendering Layer slot 1 to "Characters" (ProjectSettings/TagManager.asset).
        private static string EnsureCharactersRenderingLayer()
        {
            if (System.Array.IndexOf(RenderingLayerMask.GetDefinedRenderingLayerNames(), CharactersLayer) >= 0)
            {
                return "Rendering Layer 'Characters' present.";
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("m_RenderingLayers");
            layers.GetArrayElementAtIndex(1).stringValue = CharactersLayer;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return "Renamed Rendering Layer 1 to 'Characters'.";
        }

        // ---------------------------------------------------------------- Sun, ambient, fog

        private static void Sun()
        {
            var sun = RenderSettings.sun;
            sun.transform.rotation = Quaternion.Euler(SunElevation, SunYaw, 0f);
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.renderingLayerMask = -1;
            EditorUtility.SetDirty(sun);
        }

        private static void Environment()
        {
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

        // ---------------------------------------------------------------- Character fill

        // A shadowless cold light from the camera side that only touches the Characters rendering layer.
        private static void CharacterFill(Transform lighting)
        {
            var go = FindOrCreate(lighting, "CharacterFill");
            var light = go.GetComponent<Light>() != null ? go.GetComponent<Light>() : go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = FillColor;
            light.intensity = FillIntensity;
            light.shadows = LightShadows.None;
            // URP reads the layer from its additional light data (Forward+); set the Light's own mask too.
            uint characters = (uint)RenderingLayerMask.GetMask(CharactersLayer);
            light.renderingLayerMask = (int)characters;
            var data = go.GetComponent<UniversalAdditionalLightData>() != null ? go.GetComponent<UniversalAdditionalLightData>() : go.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = characters;
            // From behind and above the camera (which looks along CameraYaw), angled a little off-axis for form.
            go.transform.rotation = Quaternion.Euler(50f, L1Layout.CameraYaw - 25f, 0f);
        }

        // The player (scene instance override only) and the enemy spawns' future occupants go on Characters.
        private static string TagCharacters()
        {
            var player = L1LightingShots.PlayerRoot();
            if (player == null)
            {
                return "No Player in scene.";
            }

            L1LightingShots.SetCharacterLayer(player.gameObject);
            var cam = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            // Zone Volumes blend on the player's position, not the camera's (which hangs 15 m away).
            cam.volumeTrigger = player;
            cam.volumeLayerMask = 1;
            cam.renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
            return "Player on Characters layer; Volumes follow the player.";
        }

        // ---------------------------------------------------------------- Grade

        private static void GlobalGrade(Transform lighting)
        {
            var profile = Profile("L1_Global", true);
            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);

            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(Saturation);

            var split = Get<SplitToning>(profile);
            split.shadows.Override(Hex("#3E7A80"));
            split.highlights.Override(Hex("#C98B4E"));
            split.balance.Override(-20f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(Color.white);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            grain.response.Override(0.8f);

            Get<ChromaticAberration>(profile).intensity.Override(0.08f);
            Get<WhiteBalance>(profile).temperature.Override(0f);
            EditorUtility.SetDirty(profile);

            var volume = lighting.Find("GlobalVolume").GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = profile;
        }

        // Local Volumes for the zone beats. They blend on the player position (see TagCharacters).
        private static void ZoneVolumes(Transform zones)
        {
            var volumes = L1Build.Group(zones, "Volumes", true);

            // Start: the darkest, coldest spot.
            var start = Profile("L1_Zone_Start", true);
            Get<ColorAdjustments>(start).postExposure.Override(Exposure - 0.3f);
            Get<ColorAdjustments>(start).saturation.Override(-48f);
            Get<WhiteBalance>(start).temperature.Override(-22f);
            Get<Vignette>(start).intensity.Override(0.42f);
            LocalVolume(volumes, "Start", new Vector3(0f, 0f, 0f), new Vector3(20f, 30f, 20f), 7f, 1, start);

            // Arenas: evenly lit, readable stages.
            var arena = Profile("L1_Zone_Arena", true);
            Get<ColorAdjustments>(arena).postExposure.Override(Exposure);
            Get<ColorAdjustments>(arena).contrast.Override(16f);
            Get<Vignette>(arena).intensity.Override(0.26f);
            foreach (var a in L1Layout.Arenas)
            {
                float size = a.Radius * 2f + 3f;
                LocalVolume(volumes, "Arena_" + a.Name, new Vector3(a.Center.x, 0f, a.Center.y), new Vector3(size, 30f, size), 5f, 1, arena);
            }

            // Battering ram: the sun breaks through. Saddest and most beautiful frame.
            var ram = Profile("L1_Zone_Ram", true);
            Get<ColorAdjustments>(ram).postExposure.Override(Exposure + 0.2f);
            Get<ColorAdjustments>(ram).saturation.Override(-26f);
            Get<WhiteBalance>(ram).temperature.Override(12f);
            Get<Bloom>(ram).intensity.Override(0.55f);
            Get<Bloom>(ram).threshold.Override(0.9f);
            LocalVolume(volumes, "Ram", new Vector3(0f, 0f, 130f), new Vector3(30f, 30f, 30f), 10f, 1, ram);

            // Shrine: the only warm, safe light.
            var shrine = Profile("L1_Zone_Shrine", true);
            Get<ColorAdjustments>(shrine).postExposure.Override(Exposure + 0.1f);
            Get<ColorAdjustments>(shrine).saturation.Override(-28f);
            Get<WhiteBalance>(shrine).temperature.Override(9f);
            Get<Bloom>(shrine).intensity.Override(0.45f);
            LocalVolume(volumes, "Shrine", new Vector3(2f, 0f, 336f), new Vector3(20f, 30f, 20f), 12f, 1, shrine);

            // Exit: colder, sliding toward L2's night.
            var exit = Profile("L1_Zone_Exit", true);
            Get<ColorAdjustments>(exit).postExposure.Override(Exposure - 0.05f);
            Get<ColorAdjustments>(exit).saturation.Override(-42f);
            Get<ColorAdjustments>(exit).colorFilter.Override(Hex("#C9D5EE"));
            Get<WhiteBalance>(exit).temperature.Override(-32f);
            Get<Vignette>(exit).intensity.Override(0.38f);
            LocalVolume(volumes, "Exit", new Vector3(4f, 0f, 372f), new Vector3(30f, 30f, 20f), 14f, 2, exit);
        }

        private static void LocalVolume(Transform parent, string name, Vector3 center, Vector3 size, float blend, int priority, VolumeProfile profile)
        {
            var go = new GameObject("Volume_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.layer = 0;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = blend;
            volume.priority = priority;
            volume.sharedProfile = profile;
        }

        // ---------------------------------------------------------------- Pools of light

        // Replaces the old cool rim point lights with one high, wide spot per arena.
        private static void ArenaPools(Transform lighting)
        {
            var rims = lighting.Find("ArenaRim");
            if (rims != null)
            {
                rims.gameObject.SetActive(false);
            }

            var pools = L1Build.Group(lighting, "ArenaPools", true);
            foreach (var a in L1Layout.Arenas)
            {
                var go = new GameObject("ArenaPool_" + a.Name);
                go.transform.SetParent(pools, false);
                go.transform.position = new Vector3(a.Center.x, ArenaPoolHeight, a.Center.y);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = ArenaPoolColor;
                // E4 sits in an open stripe of low sun, so its pool is dimmed to keep the five stages even.
                light.intensity = ArenaPoolIntensity * (a.Name == "E4" ? 0.45f : 1f);
                light.range = ArenaPoolHeight + 8f;
                light.spotAngle = 2f * Mathf.Atan((a.Radius + 2.5f) / ArenaPoolHeight) * Mathf.Rad2Deg;
                light.innerSpotAngle = light.spotAngle * 0.6f;
                light.shadows = LightShadows.None;
            }
        }

        // One sliver of low sun through the smoke, catching the red banner at the lore spot.
        private static void BannerSliver(Transform zones, Transform root)
        {
            var group = L1Build.Group(zones, "BannerSliver", true);
            var banner = FindDeep(root, "Banner_Draped");
            Vector3 target = banner != null ? banner.GetComponent<Renderer>().bounds.center : new Vector3(6.8f, 0.2f, 16.5f);

            // Along the sun's direction, raised to 24 degrees so the beam lands on the cloth, not the dirt around it.
            Vector3 toSun = Quaternion.Euler(-24f, SunYaw + 180f, 0f) * Vector3.forward;
            Vector3 from = target + toSun * 10f;
            var light = Spot(group, "SliverLight", from, target, SunColor, 90f, 16f, 11f);
            light.innerSpotAngle = 6f;
            Shaft(group, "Shaft", from + (target - from) * 0.1f, target, 0.9f, WarmShaft(0.12f));
        }

        // Behind the ram (the far side from the camera) the sun breaks through: a warm back light and shafts.
        private static void RamBreak(Transform zones)
        {
            var group = L1Build.Group(zones, "RamBreak", true);
            Vector3 camForward = Flat(Quaternion.Euler(0f, L1Layout.CameraYaw, 0f) * Vector3.forward);
            var ram = new Vector3(0f, 1.3f, 130f);

            // "Behind" = further from the camera along its view; the sun comes from there, low.
            Vector3 from = ram + camForward * 15f + Vector3.up * 8f;
            Spot(group, "BreakLight", from, ram, SunColor, 240f, 32f, 56f);

            var rng = new System.Random(11);
            for (int i = 0; i < 5; i++)
            {
                float along = -6f + i * 3f;
                Vector3 side = new Vector3(0f, 0f, along);
                Vector3 top = ram + camForward * (16f + (float)rng.NextDouble() * 3f) + Vector3.up * (13f + (float)rng.NextDouble() * 3f) + side;
                Vector3 bottom = ram + camForward * (-2f + (float)rng.NextDouble() * 3f) + side * 1.1f + Vector3.down * 1.2f;
                Shaft(group, "Shaft_" + i, top, bottom, 2.6f + (float)rng.NextDouble() * 1.8f, WarmShaft(i % 2 == 0 ? 0.05f : 0.08f));
            }
        }

        // The only warm, safe light: a flickering, shadow-casting bonfire glow with faint shafts and a ground glow.
        private static void Shrine(Transform zones, Transform root)
        {
            var group = L1Build.Group(zones, "Shrine", true);
            var shrine = FindDeep(root, "L1_CheckpointShrine");

            // The prefab's own brazier light is replaced (scene override) by one that casts shadows and flickers.
            Vector3 lightPos = new Vector3(2f, 2f, 336f);
            foreach (var l in shrine.GetComponentsInChildren<Light>(true))
            {
                if (l.name == "WarmLight")
                {
                    lightPos = l.transform.position;
                    l.enabled = false;
                }
                else if (l.name == "GraceLight")
                {
                    l.color = Hex("#FFB47A");   // warm amber, not gold
                }
            }

            var go = new GameObject("ShrineLight");
            go.transform.SetParent(group, false);
            go.transform.position = lightPos + Vector3.up * 0.3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ShrineColor;
            light.intensity = ShrineIntensity;
            light.range = 14f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            var flicker = go.AddComponent<LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = ShrineIntensity;
            so.FindProperty("amplitude").floatValue = 0.16f;
            so.FindProperty("speed").floatValue = 1.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Warm ground glow, readable from the approach down the last corridor.
            Glow(group, "GroundGlow", new Vector3(lightPos.x, 0.08f, lightPos.z), 16f, GlowMaterial("L1_Glow_Shrine", ShrineColor, 0.2f));

            // Soft shafts falling through the ash onto the shrine.
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                Vector3 bottom = lightPos + new Vector3(Mathf.Cos(a) * 1.2f, -1.6f, Mathf.Sin(a) * 1.2f);
                Vector3 top = bottom + new Vector3(-1.5f + i, 12f, 2f - i);
                Shaft(group, "Shaft_" + i, top, bottom, 1f + i * 0.3f, WarmShaft(0.05f));
            }
        }

        // Exit: the path ends at a road sign for the Hollow Village under a cold, night-leaning light,
        // and the village burns low in the northern fog, its rooftops silhouetted against the glow.
        private static string Exit(Transform zones, Transform root)
        {
            var group = L1Build.Group(zones, "ExitGlow", true);
            string dressing = L1ExitDressing.Build(root);

            // Cold light over the last stretch of road: the threshold into L2's night.
            Spot(group, "ExitMoon", new Vector3(6f, 15f, 371f), new Vector3(4f, 0f, 374f), ExitMoonColor, 150f, 24f, 50f);

            // Fires behind the houses (north of them), so the rooftops read as silhouettes.
            var red = GlowMaterial("L1_Glow_ExitRed", ExitRed * 1.6f, 0.6f);
            var fires = new[] { new Vector3(-16f, 1.5f, 392f), new Vector3(-6f, 1.5f, 399f), new Vector3(-28f, 1.5f, 401f) };
            for (int i = 0; i < fires.Length; i++)
            {
                var go = new GameObject("VillageFire_" + i);
                go.transform.SetParent(group, false);
                go.transform.position = fires[i];
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = ExitRed;
                light.intensity = 90f;
                light.range = 26f;
                light.shadows = LightShadows.None;
                // Raised above the lip where the level's ground meets the far plane, so the red washes over the edge.
                Glow(group, "FireGlow_" + i, new Vector3(fires[i].x + 4f, 1.9f, fires[i].z - 9f), 34f, red);
            }

            // Red haze standing low in the northern fog, facing the camera.
            var haze = AdditiveMaterial("L1_Haze_ExitRed", HazeTexture(), new Color(ExitRed.r * 1.4f, ExitRed.g * 1.2f, ExitRed.b, 0.35f));
            Vector3 toCamera = Flat(-(Quaternion.Euler(0f, L1Layout.CameraYaw, 0f) * Vector3.forward));
            for (int i = 0; i < 3; i++)
            {
                var card = Card(group, "RedHaze_" + i, haze);
                card.transform.position = new Vector3(-34f + i * 13f, 5f, 400f + (i % 2) * 4f);
                card.transform.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
                card.transform.localScale = new Vector3(34f, 12f, 1f);
            }

            return dressing;
        }

        // ---------------------------------------------------------------- Shadows

        // Only the sun and the shrine cast shadows.
        private static string ShadowPolicy(Transform root)
        {
            int changed = 0;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                bool allowed = light == RenderSettings.sun || light.name == "ShrineLight";
                if (!allowed && light.shadows != LightShadows.None)
                {
                    light.shadows = LightShadows.None;
                    changed++;
                }
            }

            return $"Shadows: sun + shrine only ({changed} other lights switched off).";
        }

        // ---------------------------------------------------------------- Helpers

        private static Light Spot(Transform parent, string name, Vector3 from, Vector3 to, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(to - from);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.5f;
            light.shadows = LightShadows.None;
            return light;
        }

        // A soft additive card from top to bottom, turned to face the gameplay camera around its long axis.
        private static void Shaft(Transform parent, string name, Vector3 top, Vector3 bottom, float width, Material material)
        {
            Vector3 up = (top - bottom).normalized;
            Vector3 toCamera = -(Quaternion.Euler(L1Layout.CameraPitch, L1Layout.CameraYaw, 0f) * Vector3.forward);
            Vector3 normal = Vector3.ProjectOnPlane(toCamera, up).normalized;
            var go = Card(parent, name, material);
            go.transform.position = (top + bottom) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(-normal, up);
            go.transform.localScale = new Vector3(width, (top - bottom).magnitude, 1f);
        }

        // A flat additive disc of light on the ground.
        private static void Glow(Transform parent, string name, Vector3 center, float size, Material material)
        {
            var go = Card(parent, name, material);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
        }

        private static GameObject Card(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        private static Material WarmShaft(float alpha)
        {
            int key = Mathf.RoundToInt(alpha * 100f);
            return AdditiveMaterial("L1_Shaft_Warm_" + key, ShaftTexture(), new Color(SunColor.r, SunColor.g * 0.9f, SunColor.b * 0.75f, alpha));
        }

        private static Material GlowMaterial(string name, Color color, float alpha)
        {
            return AdditiveMaterial(name, GlowTexture(), new Color(color.r, color.g, color.b, alpha));
        }

        // Transparent additive, unlit, two-sided (URP Particles/Unlit), fog-affected so it sinks into the haze.
        private static Material AdditiveMaterial(string name, Texture2D texture, Color color)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);                                  // Additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Soft along its width (gaussian), fading in at the top and out at the bottom.
        private static Texture2D ShaftTexture()
        {
            return SoftTexture("L1_SoftShaft", 64, 256, (u, v) =>
            {
                float across = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.2f, 2f));
                float along = Mathf.SmoothStep(0f, 1f, v / 0.25f) * Mathf.SmoothStep(0f, 1f, (1f - v) / 0.45f);
                return across * along;
            });
        }

        // Bright at the bottom, fading upwards, soft at the sides: a glow sitting low in fog.
        private static Texture2D HazeTexture()
        {
            return SoftTexture("L1_SoftHaze", 128, 128, (u, v) =>
            {
                float across = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.28f, 2f));
                return across * Mathf.Pow(1f - v, 1.8f) * Mathf.SmoothStep(0f, 1f, v / 0.08f);
            });
        }

        private static Texture2D GlowTexture()
        {
            return SoftTexture("L1_SoftGlow", 128, 128, (u, v) =>
            {
                float d = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
                return Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            });
        }

        private static Texture2D SoftTexture(string name, int w, int h, System.Func<float, float, float> alpha)
        {
            string path = TexturesPath + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        px[y * w + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / w, (y + 0.5f) / h));
                    }
                }

                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static VolumeProfile Profile(string name, bool reset)
        {
            string path = VolumesPath + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else if (reset)
            {
                foreach (var component in new List<VolumeComponent>(profile.components))
                {
                    profile.Remove(component.GetType());
                    Object.DestroyImmediate(component, true);
                }
            }

            return profile;
        }

        // Gets or adds a component, stored as a sub-asset of the profile so it survives a reload.
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

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null)
            {
                return t.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.normalized;
        }

        private static void EnsureFolders()
        {
            foreach (var path in new[] { VolumesPath, MaterialsPath, TexturesPath })
            {
                Directory.CreateDirectory(path);
            }

            AssetDatabase.Refresh();
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
