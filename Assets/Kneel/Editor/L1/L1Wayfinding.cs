using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Wayfinding and readability pass for L1: torches, campfires, arena gates and thresholds,
    // arena rim lights, ash and haze, and leading lines in the dressing.
    // Each generator clears and rebuilds only its own group, so it is safe to re-run.
    public static class L1Wayfinding
    {
        private const string AnimationsPath = "Assets/Kneel/Levels/L1/Animations";
        private static readonly Color TorchColor = L1Build.Hex("#FF8A3A");

        // Fire light strengths, tuned for the dark mood (L1Look.ApplyDarkMood): the fires carry the scene.
        public const float TorchIntensity = 4f;
        public const float TorchRange = 6.5f;
        public const float BrazierIntensity = 5.5f;
        public const float BrazierRange = 8f;
        public const float CampfireIntensity = 4f;
        public const float CampfireRange = 6f;

        [MenuItem("Kneel/L1/Wayfinding/Rebuild All")]
        public static void RebuildAllMenu()
        {
            Debug.Log("[L1] " + RebuildAll());
        }

        public static string RebuildAll()
        {
            BuildPrefabs();
            string placed = PlaceLights();
            string atmosphere = BuildAtmosphere();
            string lines = ApplyLeadingLines();
            EditorSceneManager.MarkSceneDirty(L1LevelTools.Root.scene);
            return placed + " " + atmosphere + " " + lines;
        }

        // ---------------------------------------------------------------- Prefabs

        public static void BuildPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(AnimationsPath))
            {
                AssetDatabase.CreateFolder("Assets/Kneel/Levels/L1", "Animations");
            }

            var flicker = BuildFlickerClip("L1_TorchFlicker", TorchIntensity);
            var braziers = BuildFlickerClip("L1_BrazierFlicker", BrazierIntensity);

            // Battlefield torch: a stake with a burning head.
            var torch = new GameObject("L1_Torch");
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", torch.transform, Vector3.zero, new Vector3(3f, 0f, -2f), 1f, "Stake").transform.localScale = new Vector3(1.1f, 0.95f, 1.1f);
            BuildFlame(torch.transform, new Vector3(0.06f, 2.36f, -0.05f), 0.8f, TorchIntensity, TorchRange, flicker);
            L1Build.SetStatic(torch, L1Build.PropStatic);
            L1Build.SavePrefab(torch, L1Build.PrefabsPath + "/L1_Torch.prefab");

            // Gate pylon: three lashed stakes leaning outward, a fire basket on top and a red rag.
            var pylon = new GameObject("L1_GatePylon");
            var lean = new GameObject("Leaning").transform;
            lean.SetParent(pylon.transform, false);
            lean.localRotation = Quaternion.Euler(0f, 0f, 7f);
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", lean, new Vector3(-0.18f, 0f, 0.12f), new Vector3(0f, 10f, 2f)).transform.localScale = new Vector3(1.8f, 1.55f, 1.8f);
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", lean, new Vector3(0.18f, 0f, 0.1f), new Vector3(0f, -20f, -3f)).transform.localScale = new Vector3(1.6f, 1.45f, 1.6f);
            L1Build.Spawn("K/Props/SM_Prop_Beam_01", lean, new Vector3(0f, 0f, -0.2f), new Vector3(2f, 40f, 0f)).transform.localScale = new Vector3(1.7f, 1.6f, 1.7f);
            L1Build.Spawn("K/Props/SM_Prop_Brazier_01", lean, new Vector3(0f, 3.25f, 0f), Vector3.zero, 0.45f, "FireBasket");
            L1Build.Spawn("K/Props/SM_Prop_Banner_02", lean, new Vector3(0.32f, 3.2f, 0f), new Vector3(0f, 90f, 0f), 0.8f, "Rag");
            BuildFlame(lean, new Vector3(0f, 4.02f, 0f), 1.1f, BrazierIntensity, BrazierRange, braziers);
            L1Build.SetStatic(pylon, L1Build.EnvironmentStatic);
            L1Build.SavePrefab(pylon, L1Build.PrefabsPath + "/L1_GatePylon.prefab");

            // Low threshold for arena entries on the camera side: never taller than 1 m.
            var threshold = new GameObject("L1_ArenaThreshold");
            for (int i = 0; i < 4; i++)
            {
                var shield = L1Build.Spawn(i % 2 == 0 ? "K/Weapons/SM_Wep_Shield_01" : "K/Weapons/SM_Wep_Shield_03", threshold.transform,
                    new Vector3(i * 0.55f - 0.8f, 0.32f, 0f), new Vector3(-14f + i * 5f, i * 7f - 10f, 0f), 1.25f, "Shield_" + i);
                L1Build.StripColliders(shield);
            }

            var fallen = L1Build.Spawn("K/Props/SM_Prop_Beam_01", threshold.transform, new Vector3(0f, 0.15f, 0.45f), new Vector3(90f, 80f, 0f), 1f, "FallenBeam");
            L1Build.StripColliders(fallen);
            L1Build.SetStatic(threshold, L1Build.PropStatic);
            L1Build.SavePrefab(threshold, L1Build.PrefabsPath + "/L1_ArenaThreshold.prefab");

            AssetDatabase.SaveAssets();
        }

        internal static AnimationClip BuildFlickerClip(string name, float baseIntensity)
        {
            var rng = new System.Random(name.GetHashCode());
            var curve = new AnimationCurve();
            const float length = 3.2f;
            for (float t = 0f; t < length; t += 0.08f + (float)rng.NextDouble() * 0.14f)
            {
                curve.AddKey(t, baseIntensity * (0.78f + (float)rng.NextDouble() * 0.34f));
            }

            curve.AddKey(length, curve.keys[0].value);
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Light), "m_Intensity", curve);
            return L1Build.SaveLegacyClip(AnimationsPath + "/" + name + ".anim", clip);
        }

        // A flame, embers, a soft glow and a flickering warm light.
        private static void BuildFlame(Transform parent, Vector3 localPosition, float size, float intensity, float range, AnimationClip flicker)
        {
            var head = new GameObject("Flame").transform;
            head.SetParent(parent, false);
            head.localPosition = localPosition;

            var flame = Particles("Fire", head, L1Build.FxMat("L1_FX_Flame"), 30, 20f * size, new Vector2(0.35f, 0.7f), new Vector2(0.5f, 1f) * size,
                new Vector2(0.2f, 0.42f) * size, new Color(1f, 0.85f, 0.6f, 0.9f), new Color(1f, 0.35f, 0.1f, 0.3f));
            flame.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.1f * size;
            var sizeOverLife = flame.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var embers = Particles("Embers", head, L1Build.FxMat("L1_FX_Ember"), 20, 4f * size, new Vector2(2f, 3.5f), new Vector2(0.4f, 0.9f),
                new Vector2(0.025f, 0.05f), new Color(1f, 0.7f, 0.4f, 1f), new Color(1f, 0.3f, 0.1f, 0.8f), -0.04f);
            embers.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var emberShape = embers.shape;
            emberShape.shapeType = ParticleSystemShapeType.Cone;
            emberShape.angle = 18f;
            emberShape.radius = 0.12f;
            var noise = embers.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;

            Particles("Glow", head, L1Build.FxMat("L1_FX_Glow"), 2, 1.5f, new Vector2(1.5f, 1.5f), Vector2.zero,
                new Vector2(1.4f, 1.7f) * size, new Color(1f, 0.6f, 0.3f, 0.3f), new Color(1f, 0.5f, 0.25f, 0.3f));

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(head, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = TorchColor;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            var animation = lightGo.AddComponent<Animation>();
            animation.AddClip(flicker, flicker.name);
            animation.clip = flicker;
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.Loop;
        }

        public static ParticleSystem Particles(string name, Transform parent, Material material, int max, float rate, Vector2 life, Vector2 speed,
            Vector2 size, Color start, Color end, float gravity = 0f, ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.2f), new GradientAlphaKey(end.a, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = mode == ParticleSystemRenderMode.HorizontalBillboard ? 50f : 5f;
            return ps;
        }

        // ---------------------------------------------------------------- Placement

        public static string PlaceLights()
        {
            var root = L1LevelTools.Root;
            var dressing = root.transform.Find("SetDressing");
            var torches = L1Build.Group(dressing, "Torches", true);
            var gates = L1Build.Group(dressing, "Gates", true);
            var campfires = L1Build.Group(dressing, "Campfires", false);
            foreach (var old in FindChildren(campfires, "L1_DyingCampfire_Extra"))
            {
                Object.DestroyImmediate(old);
            }

            Physics.SyncTransforms();
            int nTorch = 0, nPylon = 0, nThreshold = 0, nFire = 0;
            var skipped = new List<string>();

            // Arena mouths: gates on the far (exit) side, low thresholds plus set-back torches on the camera (entry) side.
            foreach (var arena in L1Layout.Arenas)
            {
                foreach (var capsule in L1Layout.Capsules)
                {
                    float da = (capsule.A - arena.Center).magnitude;
                    float db = (capsule.B - arena.Center).magnitude;
                    if (Mathf.Min(da, db) > arena.Radius + 1.5f)
                    {
                        continue;
                    }

                    Vector2 far = da > db ? capsule.A : capsule.B;
                    Vector2 dir = (far - arena.Center).normalized;
                    Vector2 lateral = new Vector2(dir.y, -dir.x);
                    Vector2 mouth = arena.Center + dir * (arena.Radius + 0.3f);

                    if (dir.y > 0.3f)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = mouth + dir * 1.2f + lateral * side * (capsule.HalfWidth + 1.2f);
                            if (!FindSpot(ref p, lateral * side, 0.6f, 4.6f))
                            {
                                skipped.Add(arena.Name + " exit pylon");
                                continue;
                            }

                            // Local +X faces the path so the pylon leans away from it.
                            float yaw = Mathf.Atan2(-side * lateral.x, -side * lateral.y) * Mathf.Rad2Deg - 90f;
                            var pylon = L1Build.Spawn("L1_GatePylon", gates, new Vector3(p.x, 0f, p.y), new Vector3(0f, yaw, 0f), 1f, arena.Name + "_ExitPylon_" + (side < 0 ? "L" : "R"));
                            nPylon++;

                            if (side > 0)
                            {
                                // Broken lintel: fell outward from the pylon top, clear of the path.
                                Vector3 top = pylon.transform.position + Vector3.up * 3.6f;
                                Vector3 foot = new Vector3(p.x, 0f, p.y) + new Vector3(lateral.x, 0f, lateral.y) * 3.6f + new Vector3(dir.x, 0f, dir.y) * 0.8f;
                                var lintel = L1Build.Spawn("K/Props/SM_Prop_Beam_01", gates, foot, Vector3.zero, 1f, arena.Name + "_BrokenLintel");
                                lintel.transform.rotation = Quaternion.FromToRotation(Vector3.up, (top - foot).normalized);
                                lintel.transform.localScale = new Vector3(1.8f, (top - foot).magnitude / 2.5f, 1.8f);
                            }
                        }
                    }
                    else if (dir.y < -0.3f)
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 t = mouth + lateral * side * (capsule.HalfWidth - 0.6f);
                            // Row of shields (local X) runs across the mouth edge.
                            float yaw = Mathf.Atan2(lateral.x, lateral.y) * Mathf.Rad2Deg - 90f;
                            L1Build.Spawn("L1_ArenaThreshold", gates, new Vector3(t.x, 0f, t.y), new Vector3(0f, yaw, 0f), 1f, arena.Name + "_Threshold_" + (side < 0 ? "L" : "R"));
                            nThreshold++;

                            // Torches stand back out of the arena's camera-side height cap.
                            Vector2 p = arena.Center + dir * (arena.Radius + 7.6f) + lateral * side * (capsule.HalfWidth + 0.9f);
                            if (FindSpot(ref p, lateral * side, 0.4f, 2.6f))
                            {
                                L1Build.Spawn("L1_Torch", torches, new Vector3(p.x, 0f, p.y), new Vector3(0f, p.x * 37f % 360f, 0f), 1f, arena.Name + "_EntryTorch_" + (side < 0 ? "L" : "R"));
                                nTorch++;
                            }
                            else
                            {
                                skipped.Add(arena.Name + " entry torch");
                            }
                        }
                    }
                }
            }

            // Bends: one torch just past each turn, on the inside, where the top of the frame lands.
            var caps = L1Layout.Capsules;
            for (int i = 0; i < caps.Length - 1; i++)
            {
                if ((caps[i].B - caps[i + 1].A).sqrMagnitude > 0.01f || caps[i + 1].HalfWidth < 4f)
                {
                    continue;
                }

                Vector2 d0 = (caps[i].B - caps[i].A).normalized;
                Vector2 d1 = (caps[i + 1].B - caps[i + 1].A).normalized;
                if (Vector2.Angle(d0, d1) < 25f || L1Layout.IsInArena(caps[i].B, 8f))
                {
                    continue;
                }

                float turn = Mathf.Sign(d0.x * d1.y - d0.y * d1.x);
                Vector2 inward = new Vector2(-d1.y, d1.x) * turn;
                bool placed = false;
                foreach (float s in new[] { 1f, -1f })
                {
                    Vector2 p = caps[i].B + d1 * 7f + inward * s * (caps[i + 1].HalfWidth + 0.9f);
                    if (FindSpot(ref p, inward * s, 0.4f, 2.6f))
                    {
                        L1Build.Spawn("L1_Torch", torches, new Vector3(p.x, 0f, p.y), new Vector3(0f, i * 53f % 360f, 0f), 1f, "BendTorch_" + i);
                        nTorch++;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    skipped.Add("bend " + i);
                }
            }

            // Extra dying campfires where the corridors have none.
            var campfire = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabsPath + "/L1_DyingCampfire.prefab");
            float[] targets = { 28f, 98f, 164f, 246f, 318f, 358f };
            int sideSign = 1;
            foreach (float tz in targets)
            {
                Vector2 best = Vector2.zero;
                float bestScore = float.MaxValue;
                for (float z = tz - 8f; z <= tz + 8f; z += 0.5f)
                {
                    for (float x = -30f; x <= 32f; x += 0.5f)
                    {
                        var p = new Vector2(x, z);
                        float sd = L1Layout.SignedDistance(p);
                        if (sd < 0.3f || sd > 1.5f || L1Layout.IsInArena(p, 4f) || !L1Build.IsFree(new Vector3(x, 0f, z), 0.9f))
                        {
                            continue;
                        }

                        if (NearAny(p, torches, 5f) || NearAny(p, campfires, 12f))
                        {
                            continue;
                        }

                        float score = Mathf.Abs(z - tz) + (Mathf.Sign(x) == sideSign ? 0f : 6f);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = p;
                        }
                    }
                }

                sideSign = -sideSign;
                if (bestScore == float.MaxValue)
                {
                    skipped.Add("campfire z" + tz);
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(campfire, campfires);
                go.name = "L1_DyingCampfire_Extra_" + nFire++;
                go.transform.SetPositionAndRotation(new Vector3(best.x, 0f, best.y), Quaternion.Euler(0f, tz * 41f % 360f, 0f));
            }

            // Cool rim light per arena, from the south-east, so wind-ups read even inside long wall shadows.
            var lighting = L1Build.Group(root.transform.Find("_Lighting"), "ArenaRim", true);
            foreach (var arena in L1Layout.Arenas)
            {
                var go = new GameObject("ArenaRim_" + arena.Name);
                go.transform.SetParent(lighting, false);
                go.transform.position = new Vector3(arena.Center.x + arena.Radius * 0.75f, 7f, arena.Center.y - arena.Radius * 0.75f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = L1Build.Hex("#7FA2C8");
                light.intensity = 1.1f;
                light.range = arena.Radius * 2.3f;
                light.shadows = LightShadows.None;
            }

            foreach (var group in new[] { torches, gates, campfires })
            {
                L1Build.SetLayer(group.gameObject, "Obstacles");
            }

            return $"Torches {nTorch}, gate pylons {nPylon}, thresholds {nThreshold}, extra campfires {nFire}" +
                   (skipped.Count > 0 ? " (skipped: " + string.Join(", ", skipped) + ")" : "") + ".";
        }

        // Nudges p outward (away from the path) until it is outside the walkable space, under the camera
        // height limit for a piece of the given height, and clear of other solid dressing.
        private static bool FindSpot(ref Vector2 p, Vector2 outward, float minSignedDistance, float height)
        {
            for (float step = 0f; step <= 3f; step += 0.25f)
            {
                Vector2 candidate = p + outward.normalized * step;
                if (L1Layout.SignedDistance(candidate) < minSignedDistance || L1Layout.MaxPropHeight(candidate) < height)
                {
                    continue;
                }

                if (L1Build.IsFree(new Vector3(candidate.x, 0f, candidate.y), 0.45f))
                {
                    p = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool NearAny(Vector2 p, Transform group, float radius)
        {
            foreach (Transform t in group)
            {
                if ((new Vector2(t.position.x, t.position.z) - p).magnitude < radius)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GameObject> FindChildren(Transform parent, string prefix)
        {
            var list = new List<GameObject>();
            foreach (Transform t in parent)
            {
                if (t.name.StartsWith(prefix))
                {
                    list.Add(t.gameObject);
                }
            }

            return list;
        }

        // ---------------------------------------------------------------- Atmosphere

        public static string BuildAtmosphere()
        {
            var root = L1LevelTools.Root;

            // Ash drifting north on the wind, carried with the camera so it is always on screen.
            var cam = GameObject.Find("Main Camera");
            if (cam != null)
            {
                var old = cam.transform.Find("AshFall");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var ash = Particles("AshFall", cam.transform, AshMaterial(), 500, 55f, new Vector2(7f, 10f), Vector2.zero,
                    new Vector2(0.03f, 0.07f), new Color(0.72f, 0.7f, 0.66f, 0.8f), new Color(0.6f, 0.58f, 0.55f, 0.6f), 0.015f);
                ash.transform.localPosition = new Vector3(0f, 0f, 13f);
                var shape = ash.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(34f, 26f, 6f);
                var velocity = ash.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.15f, -0.05f);
                velocity.z = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                var noise = ash.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.3f;
                var start = ash.main;
                start.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.73f, 0.7f), new Color(1f, 0.55f, 0.25f));
            }

            // Thicker haze over the walls in the last stretch, and smouldering wrecks sending smoke up at the edges.
            var fog = root.transform.Find("_Lighting/GroundFog");
            foreach (var old in FindChildren(fog, "ExitHaze_"))
            {
                Object.DestroyImmediate(old);
            }

            var rng = new System.Random(31);
            int haze = 0;
            for (int attempt = 0; attempt < 200 && haze < 10; attempt++)
            {
                var p = new Vector2((float)rng.NextDouble() * 60f - 26f, 300f + (float)rng.NextDouble() * 82f);
                float sd = L1Layout.SignedDistance(p);
                if (sd < 5f || sd > 18f || L1Layout.IsInArena(p, 12f))
                {
                    continue;
                }

                var sheet = Particles("ExitHaze_" + haze++, fog, L1Build.FxMat("L1_FX_GroundFog"), 40, 1.8f, new Vector2(18f, 26f), Vector2.zero,
                    new Vector2(8f, 13f), new Color(0.58f, 0.54f, 0.48f, 0.26f), new Color(0.47f, 0.44f, 0.4f, 0.22f), 0f, ParticleSystemRenderMode.HorizontalBillboard);
                sheet.transform.position = new Vector3(p.x, 1.4f, p.y);
                var shape = sheet.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(14f, 1f, 14f);
            }

            var background = root.transform.Find("Background");
            foreach (var old in FindChildren(background, "SmoulderSmoke_"))
            {
                Object.DestroyImmediate(old);
            }

            var smokeSpots = new[] { new Vector2(-20f, 118f), new Vector2(22f, 196f), new Vector2(-22f, 262f), new Vector2(19f, 322f), new Vector2(-16f, 356f), new Vector2(18f, 366f) };
            int smoke = 0;
            foreach (var p in smokeSpots)
            {
                var plume = Particles("SmoulderSmoke_" + smoke++, background, L1Build.FxMat("L1_FX_Smoke"), 40, 1.6f, new Vector2(12f, 16f), new Vector2(1f, 1.6f),
                    new Vector2(2f, 3.5f), new Color(0.17f, 0.16f, 0.15f, 0.55f), new Color(0.3f, 0.29f, 0.27f, 0f));
                plume.transform.position = new Vector3(p.x, 0.5f, p.y);
                plume.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                var shape = plume.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 10f;
                shape.radius = 1f;
                var size = plume.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.4f));
                var velocity = plume.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                plume.GetComponent<ParticleSystemRenderer>().maxParticleSize = 20f;
            }

            return $"Ash on camera, exit haze {haze}, smoulder plumes {smoke}.";
        }

        private static Material AshMaterial()
        {
            const string path = L1Build.MaterialsPath + "/FX/L1_FX_Ash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(L1Build.FxMat("L1_FX_GroundFog"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.CopyPropertiesFromMaterial(L1Build.FxMat("L1_FX_GroundFog"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(L1Build.MaterialsPath + "/FX/L1_FX_SoftDot.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_SoftParticlesEnabled", 0f);
            mat.DisableKeyword("_SOFTPARTICLES_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Leading lines

        // Debris and bodies along the path edges fall roughly in the direction of travel, so the
        // eye reads a current flowing north. Fallen standards mark the side of each upcoming turn.
        public static string ApplyLeadingLines()
        {
            var root = L1LevelTools.Root;
            var rng = new System.Random(77);
            float Gauss(float sigma) => (float)((rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * sigma * 1.4);

            int debris = 0, bodies = 0;
            foreach (Transform t in root.transform.Find("SetDressing/Debris"))
            {
                var p = new Vector2(t.position.x, t.position.z);
                float sd = L1Layout.SignedDistance(p);
                if (sd < -3f || sd > 1.2f || L1Layout.IsInArena(p, 0.5f) || !TryPathYaw(p, out float yaw))
                {
                    continue;
                }

                var euler = t.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 90f)) < 5f)
                {
                    t.rotation = Quaternion.Euler(90f, yaw + Gauss(15f), 0f);
                    debris++;
                }
                else if (t.name.Contains("Loghalf"))
                {
                    t.rotation = Quaternion.Euler(0f, yaw - 90f + Gauss(15f), 0f);
                    debris++;
                }
            }

            foreach (Transform t in root.transform.Find("SetDressing/Corpses"))
            {
                var p = new Vector2(t.position.x, t.position.z);
                float sd = L1Layout.SignedDistance(p);
                if (t.name.Contains("Slumped") || sd < -3f || sd > 1f || rng.NextDouble() > 0.6 || !TryPathYaw(p, out float yaw))
                {
                    continue;
                }

                t.rotation = Quaternion.Euler(0f, yaw + Gauss(22f), 0f);
                bodies++;
            }

            // Fallen standards on the side of the next turn.
            var standards = root.transform.Find("SetDressing/Standards");
            foreach (var old in FindChildren(standards, "Fallen_"))
            {
                Object.DestroyImmediate(old);
            }

            var caps = L1Layout.Capsules;
            int fallen = 0;
            for (int i = 0; i < caps.Length - 1; i++)
            {
                Vector2 d0 = caps[i].B - caps[i].A;
                if (d0.magnitude < 10f || caps[i].HalfWidth < 4f || (caps[i].B - caps[i + 1].A).sqrMagnitude > 0.01f)
                {
                    continue;
                }

                Vector2 d1 = (caps[i + 1].B - caps[i + 1].A).normalized;
                Vector2 dir = d0.normalized;
                float turn = Mathf.Sign(dir.x * d1.y - dir.y * d1.x);
                Vector2 side = new Vector2(-dir.y, dir.x) * turn;
                foreach (float t in new[] { 0.5f, 0.8f })
                {
                    Vector2 p = caps[i].A + d0 * t + side * (caps[i].HalfWidth - 0.7f);
                    if (L1Layout.IsInArena(p, 1f))
                    {
                        continue;
                    }

                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    var group = new GameObject("Fallen_" + fallen++).transform;
                    group.SetParent(standards, false);
                    group.position = new Vector3(p.x, 0f, p.y);
                    group.rotation = Quaternion.Euler(0f, yaw + Gauss(12f), 0f);
                    var pole = L1Build.Spawn("K/Buildings/SM_Bld_Castle_Flagpole_01", group, new Vector3(0f, 0.1f, -1.2f), new Vector3(90f, 0f, 0f), 1.2f, "Pole");
                    var cloth = L1Build.Spawn("K/Props/SM_Prop_Banner_02", group, new Vector3(0.15f, 0.08f, 0.3f), new Vector3(-88f, 0f, 0f), 1f, "Banner");

                    // Red stays scarce: only the standard nearest the turn keeps its colour, the other is burnt.
                    if (t < 0.6f)
                    {
                        foreach (var r in cloth.GetComponentsInChildren<Renderer>())
                        {
                            r.sharedMaterial = L1Build.Mat("L1_Knights_Charred");
                        }
                    }

                    L1Build.StripColliders(pole);
                    L1Build.StripColliders(cloth);
                    L1Build.SetStatic(group.gameObject, L1Build.PropStatic);
                    L1Build.SetLayer(group.gameObject, "Obstacles");
                }
            }

            return $"Leading lines: {debris} debris and {bodies} bodies aligned, {fallen} fallen standards.";
        }

        // Direction of travel at p, from the nearest corridor.
        private static bool TryPathYaw(Vector2 p, out float yaw)
        {
            yaw = 0f;
            float best = float.MaxValue;
            bool found = false;
            foreach (var capsule in L1Layout.Capsules)
            {
                if (capsule.HalfWidth < 4f)
                {
                    continue; // side nooks don't lead anywhere
                }

                Vector2 ab = capsule.B - capsule.A;
                float t = Mathf.Clamp01(Vector2.Dot(p - capsule.A, ab) / ab.sqrMagnitude);
                float d = (p - (capsule.A + ab * t)).magnitude;
                if (d < best)
                {
                    best = d;
                    yaw = Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg;
                    found = true;
                }
            }

            return found && best < 9f;
        }
    }
}
