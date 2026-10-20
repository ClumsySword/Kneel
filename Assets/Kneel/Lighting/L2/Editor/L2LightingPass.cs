using System.Collections.Generic;
using System.IO;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.Lighting.EditorTools
{
    // Lighting pass for L2 "The Hollow Village": night, later than L1 and colder. A high, dim moon gives cold
    // shape; the fires give warm pools and, placed behind every encounter (north, up-screen), turn enemies into
    // dark silhouettes against the glow. The walkable roads are the palest, coolest ground, and two landmarks glow:
    // the church against its great fire, and the shrine's calm warm light. Re-runnable: it rebuilds its own objects.
    public static class L2LightingPass
    {
        public const string AssetsPath = "Assets/Kneel/Lighting/L2";
        private const string VolumesPath = AssetsPath + "/Volumes";
        private const string MaterialsPath = AssetsPath + "/Materials";

        // ---------------------------------------------------------------- Tuning (judged in the Game view)

        // Moon: high and cold from the north-west, so it rims figures toward the camera and lights roof edges.
        private const float MoonElevation = 50f;
        private const float MoonYaw = 150f;
        private const float MoonIntensity = 1.35f;
        private static readonly Color MoonColor = Hex("#A7BBDD");

        // The brief's night colours, scaled up: at face value they read as black under ACES from this camera.
        private const float AmbientBoost = 2.1f;
        private static readonly Color AmbientSky = Hex("#27324A") * AmbientBoost;
        private static readonly Color AmbientEquator = Hex("#1A202C") * AmbientBoost;
        private static readonly Color AmbientGround = Hex("#100F0E") * AmbientBoost;

        private static readonly Color FogColor = Hex("#131922");
        private const float FogStart = 14f;
        private const float FogEnd = 62f;

        private static readonly Color FillColor = Hex("#9FB4D6");
        private const float FillIntensity = 0.85f;

        private const float Exposure = 1.5f;
        private const float Contrast = 22f;
        private const float Saturation = -38f;

        private static readonly Color Ember = Hex("#FF7A2E");
        private const float ChurchFireIntensity = 80f;
        private static readonly Color ShrineColor = Hex("#FF9A52");
        private const float ShrineIntensity = 6f;
        private static readonly Color MoonBreakColor = Hex("#8FA6CC");

        // ---------------------------------------------------------------- Entry point

        [MenuItem("Kneel/L2/Lighting/Apply Lighting Pass")]
        public static void ApplyMenu()
        {
            Debug.Log("[L2] " + Apply());
        }

        public static string Apply()
        {
            foreach (var path in new[] { VolumesPath, MaterialsPath })
            {
                L2Build.EnsureFolder(path);
            }

            var root = L2Build.RequireRoot();
            var lighting = L2Build.Group(root.transform, "_Lighting", false);
            var log = new List<string>();

            Moon(lighting);
            Environment();
            CharacterFill(lighting);
            log.Add(TagCharacters());
            GlobalGrade(lighting);
            var zones = L2Build.Group(lighting, "Zones", true);
            ZoneVolumes(zones);
            log.Add(ChurchFire(zones));
            log.Add(Shrine(zones, root.transform));
            log.Add(MoonBreaks(lighting));
            log.Add(Haze(lighting));
            log.Add(AshFall());
            log.Add(ShadowPolicy(root.transform));

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();
            return "L2 lighting applied. " + string.Join(" ", log);
        }

        // ---------------------------------------------------------------- Moon, ambient, fog, sky

        private static void Moon(Transform lighting)
        {
            var go = lighting.Find("Moon") != null ? lighting.Find("Moon").gameObject : new GameObject("Moon");
            go.transform.SetParent(lighting, false);
            go.transform.rotation = Quaternion.Euler(MoonElevation, MoonYaw, 0f);
            var moon = go.GetComponent<Light>() != null ? go.GetComponent<Light>() : go.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = MoonColor;
            moon.intensity = MoonIntensity;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.6f;
            moon.renderingLayerMask = -1;
            RenderSettings.sun = moon;
            EditorUtility.SetDirty(moon);
        }

        private static void Environment()
        {
            var sky = L2Build.Mat("L2_NightSky");
            if (sky == null)
            {
                L2Look.RebuildSky();
                sky = L2Build.Mat("L2_NightSky");
            }

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

        // ---------------------------------------------------------------- Characters

        // A shadowless cold light from behind the camera that only touches the Characters rendering layer.
        private static void CharacterFill(Transform lighting)
        {
            var go = lighting.Find("CharacterFill") != null ? lighting.Find("CharacterFill").gameObject : new GameObject("CharacterFill");
            go.transform.SetParent(lighting, false);
            var light = go.GetComponent<Light>() != null ? go.GetComponent<Light>() : go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = FillColor;
            light.intensity = FillIntensity;
            light.shadows = LightShadows.None;
            uint characters = (uint)RenderingLayerMask.GetMask(L1LightingPass.CharactersLayer);
            light.renderingLayerMask = (int)characters;
            var data = go.GetComponent<UniversalAdditionalLightData>() != null ? go.GetComponent<UniversalAdditionalLightData>() : go.AddComponent<UniversalAdditionalLightData>();
            data.renderingLayers = characters;
            go.transform.rotation = Quaternion.Euler(50f, L2Layout.CameraYaw - 25f, 0f);
        }

        private static string TagCharacters()
        {
            var player = L1LightingShots.PlayerRoot();
            if (player == null)
            {
                return "No Player in scene.";
            }

            L1LightingShots.SetCharacterLayer(player.gameObject);
            var cam = Camera.main.GetComponent<UniversalAdditionalCameraData>();
            cam.volumeTrigger = player;
            cam.volumeLayerMask = 1;
            cam.renderPostProcessing = true;
            EditorUtility.SetDirty(cam);
            return "Player on Characters layer; Volumes follow the player.";
        }

        // ---------------------------------------------------------------- Grade

        private static void GlobalGrade(Transform lighting)
        {
            var profile = Profile("L2_Global");
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            var adjust = Get<ColorAdjustments>(profile);
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(Contrast);
            adjust.saturation.Override(Saturation);

            // Cyan-orange: cold shadows, ember highlights. Colder than L1 (balance toward the shadows).
            var split = Get<SplitToning>(profile);
            split.shadows.Override(Hex("#2E6A78"));
            split.highlights.Override(Hex("#D08040"));
            split.balance.Override(-30f);
            Get<WhiteBalance>(profile).temperature.Override(-14f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.36f);
            vignette.smoothness.Override(0.45f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.32f);
            bloom.scatter.Override(0.65f);

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.25f);
            grain.response.Override(0.8f);
            Get<ChromaticAberration>(profile).intensity.Override(0.1f);
            EditorUtility.SetDirty(profile);

            var go = lighting.Find("GlobalVolume") != null ? lighting.Find("GlobalVolume").gameObject : new GameObject("GlobalVolume");
            go.transform.SetParent(lighting, false);
            var volume = go.GetComponent<Volume>() != null ? go.GetComponent<Volume>() : go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.sharedProfile = profile;
        }

        // Local volumes for the beats (they blend on the player's position).
        private static void ZoneVolumes(Transform zones)
        {
            var volumes = L2Build.Group(zones, "Volumes", true);

            // Start: straight off the battlefield road, coldest and darkest.
            var start = Profile("L2_Zone_Start");
            Get<ColorAdjustments>(start).postExposure.Override(Exposure - 0.2f);
            Get<ColorAdjustments>(start).saturation.Override(-48f);
            Get<WhiteBalance>(start).temperature.Override(-24f);
            Get<Vignette>(start).intensity.Override(0.44f);
            LocalVolume(volumes, "Start", L2Layout.Areas[0].Center, new Vector2(24f, 18f), 8f, 1, start);

            // Fights: even and readable, a touch brighter, less vignette.
            var arena = Profile("L2_Zone_Arena");
            Get<ColorAdjustments>(arena).postExposure.Override(Exposure + 0.15f);
            Get<ColorAdjustments>(arena).contrast.Override(18f);
            Get<Vignette>(arena).intensity.Override(0.28f);
            foreach (var a in L2Layout.Arenas)
            {
                float size = a.Radius * 2f + 3f;
                LocalVolume(volumes, "Arena_" + a.Name, a.Center, new Vector2(size, size), 5f, 2, arena);
            }

            // The church: warm backlight through the smoke, a little bloom. The guilt beat.
            var church = Profile("L2_Zone_Church");
            Get<ColorAdjustments>(church).postExposure.Override(Exposure + 0.05f);
            Get<ColorAdjustments>(church).saturation.Override(-30f);
            Get<WhiteBalance>(church).temperature.Override(4f);
            Get<Bloom>(church).intensity.Override(0.5f);
            LocalVolume(volumes, "Church", new Vector2(-4f, -12f), new Vector2(44f, 30f), 8f, 1, church);

            // Shrine: the one calm, warm place.
            var shrine = Profile("L2_Zone_Shrine");
            Get<ColorAdjustments>(shrine).postExposure.Override(Exposure + 0.1f);
            Get<ColorAdjustments>(shrine).saturation.Override(-28f);
            Get<WhiteBalance>(shrine).temperature.Override(10f);
            Get<Bloom>(shrine).intensity.Override(0.45f);
            LocalVolume(volumes, "Shrine", L2Layout.Markers["Checkpoint_Shrine"], new Vector2(18f, 18f), 8f, 1, shrine);

            // Village 3: the fire is closest here.
            var fire = Profile("L2_Zone_Village3");
            Get<ColorAdjustments>(fire).saturation.Override(-32f);
            Get<WhiteBalance>(fire).temperature.Override(-4f);
            Get<Bloom>(fire).intensity.Override(0.45f);
            LocalVolume(volumes, "Village3", new Vector2(26f, 56f), new Vector2(40f, 40f), 8f, 1, fire);

            // Exit: open, cold, toward the fields.
            var exit = Profile("L2_Zone_Exit");
            Get<ColorAdjustments>(exit).postExposure.Override(Exposure - 0.05f);
            Get<ColorAdjustments>(exit).saturation.Override(-44f);
            Get<ColorAdjustments>(exit).colorFilter.Override(Hex("#C9D5EE"));
            Get<WhiteBalance>(exit).temperature.Override(-30f);
            Get<Vignette>(exit).intensity.Override(0.4f);
            LocalVolume(volumes, "Exit", new Vector2(6f, 132f), new Vector2(24f, 28f), 10f, 1, exit);
        }

        private static void LocalVolume(Transform parent, string name, Vector2 center, Vector2 size, float blend, int priority, VolumeProfile profile)
        {
            var go = new GameObject("Volume_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, L2Layout.Height(center), center.y);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(size.x, 30f, size.y);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.blendDistance = blend;
            volume.priority = priority;
            volume.sharedProfile = profile;
        }

        // ---------------------------------------------------------------- Landmarks

        // The fire north of the church (the burning house behind it) is the brightest light in the level, so the
        // church, and the archer on its churchyard edge, stand black against it from every approach.
        private static string ChurchFire(Transform zones)
        {
            var group = L2Build.Group(zones, "ChurchFire", true);
            Vector2 fire = Vector2.zero;
            foreach (var lot in L2Layout.KeyLots)
            {
                if (lot.Name == "Burning_Church")
                {
                    fire = lot.Center;
                }
            }

            var go = new GameObject("ChurchFireLight");
            go.transform.SetParent(group, false);
            go.transform.position = new Vector3(fire.x, L2Layout.Height(fire) + 5f, fire.y + 1f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Ember;
            light.intensity = ChurchFireIntensity;
            light.range = 26f;
            light.shadows = LightShadows.None;
            var flicker = go.AddComponent<LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = ChurchFireIntensity;
            so.FindProperty("amplitude").floatValue = 0.16f;
            so.FindProperty("speed").floatValue = 0.9f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Glow(group, "ChurchFireGlow", new Vector3(fire.x, L2Layout.Height(fire) + 0.1f, fire.y), 22f, GlowMaterial("L2_Glow_ChurchFire", Ember, 0.35f));
            return "Church fire lit.";
        }

        // The shrine's calm warm light (the prefab's own is replaced by a shadowing, slowly breathing one).
        private static string Shrine(Transform zones, Transform root)
        {
            var group = L2Build.Group(zones, "Shrine", true);
            var shrine = L1LightingPass.FindDeep(root, "L1_CheckpointShrine");
            if (shrine == null)
            {
                return "No shrine placed.";
            }

            Vector3 lightPos = shrine.position + Vector3.up * 2f;
            foreach (var l in shrine.GetComponentsInChildren<Light>(true))
            {
                if (l.name == "WarmLight")
                {
                    lightPos = l.transform.position;
                    l.enabled = false;
                }
                else if (l.name == "GraceLight")
                {
                    l.color = Hex("#FFB47A");
                }
            }

            var go = new GameObject("ShrineLight");
            go.transform.SetParent(group, false);
            go.transform.position = lightPos + Vector3.up * 0.3f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ShrineColor;
            light.intensity = ShrineIntensity;
            light.range = 13f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            var flicker = go.AddComponent<LightFlicker>();
            var so = new SerializedObject(flicker);
            so.FindProperty("baseIntensity").floatValue = ShrineIntensity;
            so.FindProperty("amplitude").floatValue = 0.14f;
            so.FindProperty("speed").floatValue = 1.1f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var glow = L2Build.CopyMaterial("Assets/Kneel/Lighting/L1/Materials/L1_Glow_Shrine.mat", "L2_Glow_Shrine");
            Glow(group, "GroundGlow", new Vector3(lightPos.x, shrine.position.y + 0.08f, lightPos.z), 15f, glow);
            return "Shrine lit.";
        }

        // ---------------------------------------------------------------- Moonlit roads

        // Soft cold breaks in the cloud over the roads and lanes (never over a fight), so the path always reads.
        private static string MoonBreaks(Transform lighting)
        {
            var group = L2Build.Group(lighting, "MoonBreaks", true);
            var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Kneel/Lighting/L1/Textures/L1_BreakCookie.png");
            var rng = new System.Random(12);
            var route = L2Layout.Route;
            int placed = 0;
            var used = new List<Vector2>();
            for (int i = 1; i < route.Length; i++)
            {
                if (route[i - 1].Break)
                {
                    continue;
                }

                var p = Vector2.Lerp(route[i - 1].P, route[i].P, 0.5f);
                if (L2Layout.IsInArena(p, 4f) || rng.NextDouble() < 0.35)
                {
                    continue;
                }

                bool near = false;
                foreach (var u in used)
                {
                    near |= (u - p).magnitude < 22f;
                }

                if (near)
                {
                    continue;
                }

                used.Add(p);
                var go = new GameObject("MoonBreak_" + placed++);
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(p.x, L2Layout.Height(p) + 16f, p.y);
                go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = MoonBreakColor;
                light.intensity = 22f + (float)rng.NextDouble() * 10f;
                light.range = 24f;
                light.spotAngle = 55f + (float)rng.NextDouble() * 15f;
                light.innerSpotAngle = light.spotAngle * 0.2f;
                light.cookie = cookie;
                light.shadows = LightShadows.None;
            }

            // The fields at the level's end lie open to the sky: broad breaks over the furrows either side of the
            // road, so the ploughed rows and the dead crop read from the road.
            foreach (var (dx, z) in new[] { (-7.5f, 124f), (8f, 133f), (-8f, 146f) })
            {
                var p = new Vector2(L2Farmland.RoadX(z) + dx, z);
                var go = new GameObject("FieldBreak_" + placed++);
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) + 18f, p.y);
                go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = MoonBreakColor;
                light.intensity = 34f;
                light.range = 26f;
                light.spotAngle = 75f;
                light.innerSpotAngle = 20f;
                light.cookie = cookie;
                light.shadows = LightShadows.None;
            }

            return $"Moon breaks {placed}.";
        }

        // ---------------------------------------------------------------- Atmosphere

        // Low, cold haze lying in the yards and the backdrop, never on a fight floor or over a telegraph.
        private static string Haze(Transform lighting)
        {
            var group = L2Build.Group(lighting, "GroundHaze", true);
            var rng = new System.Random(31);
            int n = 0;
            for (int attempt = 0; attempt < 600 && n < 26; attempt++)
            {
                var p = new Vector2(-100f + (float)rng.NextDouble() * 160f, -70f + (float)rng.NextDouble() * 220f);
                float sd = L2Layout.SignedDistance(p);
                if (sd < 5f || sd > 20f || L2Layout.IsInArena(p, 14f))
                {
                    continue;
                }

                var sheet = L1Wayfinding.Particles("Haze_" + n++, group, L1Build.FxMat("L1_FX_GroundFog"), 30, 1.4f, new Vector2(18f, 26f), Vector2.zero,
                    new Vector2(7f, 12f), new Color(0.42f, 0.48f, 0.58f, 0.22f), new Color(0.36f, 0.4f, 0.48f, 0.18f), 0f, ParticleSystemRenderMode.HorizontalBillboard);
                sheet.transform.position = new Vector3(p.x, L2Layout.TerrainHeight(p) + 0.9f, p.y);
                var shape = sheet.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(10f, 0.5f, 10f);
            }

            return $"Haze {n}.";
        }

        // Ash falling slowly through the frame, carried with the camera, greyer and slower than L1's.
        private static string AshFall()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return "No camera for ash.";
            }

            var old = cam.transform.Find("AshFall");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var ash = L1Wayfinding.Particles("AshFall", cam.transform, L1Build.FxMat("L1_FX_Ash"), 380, 38f, new Vector2(8f, 11f), Vector2.zero,
                new Vector2(0.025f, 0.06f), new Color(0.62f, 0.62f, 0.62f, 0.7f), new Color(0.5f, 0.5f, 0.52f, 0.5f), 0.01f);
            ash.transform.localPosition = new Vector3(0f, 0f, 12f);
            var shape = ash.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 22f, 6f);
            var velocity = ash.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, -0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            var noise = ash.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 0.3f;
            var main = ash.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.66f, 0.66f, 0.66f), new Color(1f, 0.5f, 0.22f));
            return "Ash on camera.";
        }

        // Only the moon and the shrine cast shadows.
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

            // The ground never casts: the flagstones sit a few centimetres above it and would catch its shadow as
            // acne. (Cliffs cast through their retaining walls.)
            var terrain = root.Find("Terrain");
            if (terrain != null)
            {
                foreach (var r in terrain.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            return $"Shadows: moon + shrine only ({changed} switched off), ground casts none.";
        }

        // ---------------------------------------------------------------- Helpers

        private static void Glow(Transform parent, string name, Vector3 center, float size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
        }

        private static Material GlowMaterial(string name, Color color, float alpha)
        {
            var mat = L2Build.CopyMaterial("Assets/Kneel/Lighting/L1/Materials/L1_Glow_Shrine.mat", name);
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, alpha));
            EditorUtility.SetDirty(mat);
            return mat;
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
