using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Shared helpers for building L1 content from Synty parts without touching the Synty sources.
    public static class L1Build
    {
        public const string MaterialsPath = "Assets/Kneel/Levels/L1/Materials";
        public const string PrefabsPath = "Assets/Kneel/Levels/L1/Prefabs";

        public const StaticEditorFlags EnvironmentStatic =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        // Small props: never occlude anything, so they're not occluders.
        public const StaticEditorFlags PropStatic =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        // "K/Props/SM_Prop_Beam_01" -> PolygonKnights, "A/..." -> PolygonAdventure,
        // "L1_..." / "Monsters/L1_..." / "Corpses/L1_..." -> our prefabs.
        public static string PrefabPath(string key)
        {
            if (IsOwn(key))
            {
                return PrefabsPath + "/" + key + ".prefab";
            }

            string pack = key[0] == 'K' ? "PolygonKnights" : "PolygonAdventure";
            return "Assets/SyntyStudios/" + pack + "/Prefabs/" + key.Substring(2) + ".prefab";
        }

        public static Material Mat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/" + name + ".mat");
        }

        public static Material FxMat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/FX/" + name + ".mat");
        }

        // Instantiates a Synty or L1 prefab and swaps Synty palette materials for the L1 ash copies.
        public static GameObject Spawn(string key, Transform parent, Vector3 localPosition, Vector3 localEuler, float scale = 1f, string name = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(key));
            if (source == null)
            {
                throw new System.ArgumentException("Missing prefab " + PrefabPath(key));
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = Vector3.one * scale;
            if (name != null)
            {
                go.name = name;
            }

            if (!IsOwn(key))
            {
                ApplyAshMaterials(go, key.Contains("Banner") || key.Contains("Tent_"));
            }

            return go;
        }

        private static bool IsOwn(string key)
        {
            return key.StartsWith("L1_") || key.StartsWith("Monsters/") || key.StartsWith("Corpses/");
        }

        public static void ApplyAshMaterials(GameObject go, bool cloth)
        {
            var knights = Mat(cloth ? "L1_Knights_Cloth" : "L1_Knights_Ash");
            var adventure = Mat("L1_Adventure_Ash");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    if (mats[i].name == "PolyKnights_Mat_01")
                    {
                        mats[i] = knights;
                    }
                    else if (mats[i].name == "PolyAdventureMaterial_01")
                    {
                        mats[i] = adventure;
                    }
                }

                r.sharedMaterials = mats;
            }
        }

        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }
        }

        public static void SetStatic(GameObject go, StaticEditorFlags flags)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<ParticleSystem>() != null || t.GetComponent<Light>() != null || t.GetComponent<Animation>() != null)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            }
        }

        public static void SetLayer(GameObject go, string layer)
        {
            int index = LayerMask.NameToLayer(layer);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = index;
            }
        }

        public static Transform Group(Transform parent, string name, bool clear)
        {
            var existing = parent.Find(name);
            if (existing != null && clear)
            {
                Object.DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // True if nothing solid (other than the ground and the invisible boundary) is within radius of p.
        public static bool IsFree(Vector3 p, float radius)
        {
            int mask = ~((1 << LayerMask.NameToLayer("Ground")) | (1 << LayerMask.NameToLayer("Ignore Raycast")));
            return !Physics.CheckSphere(p + Vector3.up * 0.8f, radius, mask, QueryTriggerInteraction.Ignore);
        }

        // Looping legacy clip (no scripts needed at runtime).
        public static AnimationClip SaveLegacyClip(string path, AnimationClip clip)
        {
            clip.legacy = true;
            clip.wrapMode = WrapMode.Loop;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
