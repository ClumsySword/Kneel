using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools
{
    // The L5 throne room's art polish (owner, 2026-11-11), applied to the scene in place (L5Level's Build Scene is never
    // rerun over the hand-tuned scene). Each pass is safe to rerun.
    // - Glow: "candles should have distinct small light sources, never a giant blob of glow"; the bosses' light only
    //   from their ember cracks, clean, not fuzz. A tight bloom, no halo sprites or fire glow sprites, matte boss
    //   bodies, and a boss lighting rig so the chandeliers don't burn a 6 m body's shoulders.
    // - Doors (L5PolishDoors.cs) and the throne wall (L5PolishThrone.cs).
    public static partial class L5Polish
    {
        public const string Scene = "Assets/Kneel/Scenes/Levels/L5_ThroneRoom.unity";
        private const string Profile = "Assets/Kneel/Lighting/L5/Volumes/L5_Global.asset";
        private static readonly string[] BossMaterials =
        {
            "Assets/Kneel/Enemies/TheMaster/Models/Master/M_Master.mat",
            "Assets/Kneel/Enemies/TheMaster/Models/AshColossus/M_AshColossus.mat",
        };

        // The bloom: only what's really hot (flames, crack cores, glints) blooms, and only a little, tight round it.
        public const float BloomThreshold = 1.1f;
        public const float BloomIntensity = 0.45f;
        public const float BloomScatter = 0.5f;

        // Matte ash and obsidian: the Meshy gloss map at full strength made every chandelier a white smear on them.
        public const float BossSmoothness = 0.3f;

        [MenuItem("Kneel/L5/Polish/Glow")]
        public static void GlowMenu() => Debug.Log(Glow());

        public static GameObject OpenScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != Scene || active.isDirty)
                EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            return GameObject.Find("L5_Root");
        }

        public static string Glow()
        {
            var log = new List<string>();
            EnsureBossLayer();
            log.Add(Bloom());
            log.Add(MatteBosses());
            foreach (string prefab in new[] { MasterSetup.P1Prefab, MasterSetup.P2Prefab })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(prefab);
                try
                {
                    BossLayers(root);
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            OpenScene();
            log.Add(StripGlowSprites());
            log.Add(LightTheBosses());
            log.Add(SpreadSconces());
            foreach (var body in Object.FindObjectsByType<MasterBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                BossLayers(body.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return "L5 glow: " + string.Join("; ", log);
        }

        // Rendering layer 3 is the bosses' (HallActors.BossLayerName).
        public static void EnsureBossLayer()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("m_RenderingLayers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == HallActors.BossLayerName)
                    return;
            }

            while (layers.arraySize < 4)
                layers.InsertArrayElementAtIndex(layers.arraySize);
            layers.GetArrayElementAtIndex(3).stringValue = HallActors.BossLayerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        // The bosses' renderers: Characters (the hall's character fill) and Bosses (every light but the chandeliers'
        // floor lights), never Default.
        public static void BossLayers(GameObject root)
        {
            uint mask = RenderingLayerMask.GetMask("Characters") | HallActors.BossLayer;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer)
                    continue;
                r.renderingLayerMask = mask;
                EditorUtility.SetDirty(r);
            }
        }

        private static string Bloom()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Profile);
            if (profile == null || profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom) == false)
                return "no bloom";
            bloom.threshold.Override(BloomThreshold);
            bloom.intensity.Override(BloomIntensity);
            bloom.scatter.Override(BloomScatter);
            bloom.highQualityFiltering.Override(true);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(profile);
            return "bloom " + BloomThreshold + "/" + BloomIntensity + "/" + BloomScatter + " HQ";
        }

        private static string MatteBosses()
        {
            int n = 0;
            foreach (string path in BossMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;
                material.SetFloat("_Smoothness", BossSmoothness);
                EditorUtility.SetDirty(material);
                n++;
            }

            return n + " boss materials matte (" + BossSmoothness + ")";
        }

        // The fixtures' halo sprites (chandeliers, candelabras, their floor pools) and the fires' soft glow sprites.
        // The pickups keep their glow: that's a signpost, not a light.
        public static string StripGlowSprites()
        {
            GameObject root = GameObject.Find("L5_Root");
            int halos = 0, glows = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null)
                    continue;
                if (t.name == "Halo" && t.parent != null && (t.parent.name.Contains("Chandelier") || t.parent.name.Contains("Candelabra")))
                {
                    Object.DestroyImmediate(t.gameObject);
                    halos++;
                    continue;
                }

                var r = t.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == "L1_FX_Glow" && InFire(t))
                {
                    Object.DestroyImmediate(t.gameObject);
                    glows++;
                }
            }

            return halos + " halos, " + glows + " fire glows removed";
        }

        private static bool InFire(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (p.name.Contains("Flame") || p.name.Contains("Fire") || p.name.Contains("Torch") || p.name.Contains("Brazier") || p.name.Contains("Sconce"))
                    return true;
                if (p.name.Contains("Pickup"))
                    return false;
            }

            return false;
        }

        // A sconce's light sat 0.62 m off its wall, so the stone right behind it took ~40x the light the floor did: a
        // bright oval round every torch. 1.5 m out (over the floor it lights, which barely changes), the wall's
        // hotspot is ~6x weaker and wide, and the flame itself is the bright thing.
        public const float SconceLightOut = 1.5f;

        private static string SpreadSconces()
        {
            int n = 0;
            foreach (Transform t in GameObject.Find("L5_Root").GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Sconce")
                    continue;
                foreach (Light light in t.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Point || light.name == "FixtureGlow")
                        continue;
                    // As far out as it goes (up to 1.5 m), nudged sideways if it must, while staying 0.8 m clear of
                    // everything: two sconces stand in the nave's corners, where straight out is 10 cm from the side wall.
                    Vector3 local = t.InverseTransformPoint(light.transform.position);
                    Vector3 best = local;
                    float bestGap = -1f;
                    bool found = false;
                    foreach (float z in new[] { SconceLightOut, 1.3f, 1.1f, 0.95f, 0.8f, 0.62f })
                    {
                        foreach (float x in new[] { 0f, 0.4f, -0.4f, 0.8f, -0.8f })
                        {
                            var candidate = new Vector3(x, local.y, z);
                            float gap = Clearance(t.TransformPoint(candidate));
                            if (gap >= 0.8f)
                            {
                                best = candidate;
                                found = true;
                                break;
                            }

                            if (gap > bestGap)
                            {
                                bestGap = gap;
                                best = candidate;
                            }
                        }

                        if (found)
                            break;
                    }

                    light.transform.position = t.TransformPoint(best);
                    EditorUtility.SetDirty(light.transform);
                    n++;
                }
            }

            return n + " sconce lights moved " + SconceLightOut + " m off their walls";
        }

        // The distance from a point to the nearest collider, in 40 directions.
        private static float Clearance(Vector3 p)
        {
            float best = 9f;
            for (int i = 0; i < 40; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / 40f, r = Mathf.Sqrt(1f - y * y), a = i * 2.39996f;
                if (Physics.Raycast(p, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), out RaycastHit hit, 9f, ~0, QueryTriggerInteraction.Ignore))
                    best = Mathf.Min(best, hit.distance);
            }

            return best;
        }

        // Every light that lit Default now also lights the bosses, except the hall chandeliers' floor lights; each of
        // those gets a boss light beside it at MasterSetup.ChandelierBossRatio of its strength.
        private static string LightTheBosses()
        {
            List<Transform> chandeliers = MasterSetup.HallChandeliers();
            var floorLights = new HashSet<Light>(chandeliers.Select(c => c.Find("Light")?.GetComponent<Light>()).Where(l => l != null));
            int lit = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (floorLights.Contains(light) || light.name == "BossLight" || (light.renderingLayerMask & 1) == 0)
                    continue;
                HallActors.LightBosses(light);
                EditorUtility.SetDirty(light);
                lit++;
            }

            int made = 0;
            foreach (Light floor in floorLights)
            {
                Transform existing = floor.transform.parent.Find("BossLight");
                Light boss = existing != null ? existing.GetComponent<Light>() : null;
                if (boss == null)
                {
                    var go = new GameObject("BossLight");
                    go.transform.SetParent(floor.transform.parent, false);
                    boss = go.AddComponent<Light>();
                    go.AddComponent<UniversalAdditionalLightData>();
                    made++;
                }

                boss.transform.SetPositionAndRotation(floor.transform.position, floor.transform.rotation);
                boss.type = floor.type;
                boss.color = floor.color;
                boss.range = floor.range;
                boss.intensity = floor.intensity * MasterSetup.ChandelierBossRatio;
                boss.shadows = LightShadows.None;
                boss.renderingLayerMask = (int)HallActors.BossLayer;
                boss.GetComponent<UniversalAdditionalLightData>().renderingLayers = HallActors.BossLayer;
                EditorUtility.SetDirty(boss);
            }

            return lit + " lights reach the bosses, " + floorLights.Count + " chandelier boss lights (" + made + " new)";
        }
    }
}
