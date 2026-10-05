using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Shared helpers for building L2 from Synty parts. Mirrors L1Build (and reuses its generic helpers)
    // but swaps to the L2 night palette, so L1's output never changes.
    public static class L2Build
    {
        public const string ScenePath = "Assets/Kneel/Scenes/Levels/L2_HollowVillage.unity";
        public const string SceneFolder = "Assets/Kneel/Scenes/Levels/L2_HollowVillage";
        public const string RootName = "L2_HollowVillage";
        public const string MaterialsPath = "Assets/Kneel/Materials/L2";
        public const string PrefabsPath = "Assets/Kneel/Prefabs/L2";
        public const string MeshesPath = "Assets/Kneel/Meshes/L2";
        public const string SettingsPath = "Assets/Kneel/Settings/L2";
        public const string LightingPath = "Assets/Kneel/Lighting/L2";

        public static GameObject Root => GameObject.Find(RootName);

        public static GameObject RequireRoot()
        {
            var root = Root;
            if (root == null)
            {
                throw new System.InvalidOperationException("Open " + ScenePath + " first (Kneel/L2/Greybox/Build Scene creates it).");
            }

            return root;
        }

        public static void MarkDirty()
        {
            var root = Root;
            if (root != null)
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
            }
        }

        // "K/..." PolygonKnights, "A/..." PolygonAdventure, "L1/..." our L1 prefabs, "L2_..." our L2 prefabs.
        public static string PrefabPath(string key)
        {
            if (key.StartsWith("L2_") || key.StartsWith("Corpses/") || key.StartsWith("Skeletons/") || key.StartsWith("Houses/"))
            {
                return PrefabsPath + "/" + key + ".prefab";
            }

            if (key.StartsWith("L1/"))
            {
                return L1Build.PrefabsPath + "/" + key.Substring(3) + ".prefab";
            }

            return L1Build.PrefabPath(key);
        }

        public static Material Mat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/" + name + ".mat");
        }

        // Instantiates a prefab and swaps Synty palette materials (and L1's ash copies) for the L2 night copies.
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

            if (key.StartsWith("K/") || key.StartsWith("A/"))
            {
                ApplyNightMaterials(go, key.Contains("Banner") || key.Contains("Tent_") || key.Contains("Stall_Cover") || key.Contains("HangingCloth") || key.Contains("Washingline"));
            }

            return go;
        }

        // Cloth pieces keep a faded colour; everything else takes the lived-in night palette (burnt pieces are
        // recoloured to L2_Knights_Burnt / Charred by the passes that burn them).
        public static void ApplyNightMaterials(GameObject go, bool cloth)
        {
            var knights = Mat(cloth ? "L2_Knights_Faded" : "L2_Knights_Night") ?? Mat("L2_Knights_Burnt") ?? L1Build.Mat("L1_Knights_Ash");
            var adventure = Mat(cloth ? "L2_Adventure_Faded" : "L2_Adventure_Night") ?? Mat("L2_Adventure_Burnt") ?? L1Build.Mat("L1_Adventure_Ash");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    string n = mats[i].name;
                    if (n == "PolyKnights_Mat_01" || n == "L1_Knights_Ash")
                    {
                        mats[i] = knights;
                    }
                    else if (n == "PolyAdventureMaterial_01" || n == "L1_Adventure_Ash")
                    {
                        mats[i] = adventure;
                    }
                }

                r.sharedMaterials = mats;
            }
        }

        public static Transform Group(Transform parent, string name, bool clear)
        {
            return L1Build.Group(parent, name, clear);
        }

        // A child group by slash path, created as needed (never cleared).
        public static Transform Path(Transform root, string path)
        {
            var t = root;
            foreach (var part in path.Split('/'))
            {
                t = L1Build.Group(t, part, false);
            }

            return t;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        public static Vector3 Ground(Vector2 p, float fallbackHeight = float.NaN)
        {
            int ground = 1 << LayerMask.NameToLayer("Ground");
            var from = new Vector3(p.x, 60f, p.y);
            if (Physics.Raycast(from, Vector3.down, out var hit, 120f, ground))
            {
                return hit.point;
            }

            return new Vector3(p.x, float.IsNaN(fallbackHeight) ? L2Layout.Height(p) : fallbackHeight, p.y);
        }

        public static float Top(GameObject go)
        {
            float top = float.MinValue;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is ParticleSystemRenderer))
                {
                    top = Mathf.Max(top, r.bounds.max.y);
                }
            }

            return top;
        }

        // Only pieces at least this tall can hide the player; everything lower never fades.
        public const float FadeMinHeight = 2.3f;

        // Makes a tall piece fade out while it actually hides the player (Kneel.OcclusionFade): each renderer
        // gets an exact mesh-collider probe on the FadeProbe layer, and each of its materials a see-through
        // copy saved as an asset (Materials/L2/Faded), so nothing is created or compiled at runtime.
        public static void AddFade(GameObject go)
        {
            int probeLayer = LayerMask.NameToLayer(Kneel.OcclusionFade.ProbeLayer);
            float bottom = float.MaxValue, top = float.MinValue;
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    continue;
                }

                renderers.Add(r);
                bottom = Mathf.Min(bottom, r.bounds.min.y);
                top = Mathf.Max(top, r.bounds.max.y);
            }

            if (renderers.Count == 0 || top - bottom < FadeMinHeight || probeLayer < 0)
            {
                return;
            }

            var sources = new System.Collections.Generic.List<Material>();
            var faded = new System.Collections.Generic.List<Material>();
            foreach (var r in renderers)
            {
                var old = r.transform.Find("FadeProbe");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var probe = new GameObject("FadeProbe");
                probe.layer = probeLayer;
                probe.transform.SetParent(r.transform, false);
                probe.AddComponent<MeshCollider>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || sources.Contains(m))
                    {
                        continue;
                    }

                    var see = FadedMaterial(m);
                    if (see != null)
                    {
                        sources.Add(m);
                        faded.Add(see);
                    }
                }
            }

            var fade = go.GetComponent<Kneel.OcclusionFade>();
            if (fade == null)
            {
                fade = go.AddComponent<Kneel.OcclusionFade>();
            }

            fade.Configure(renderers.ToArray(), sources.ToArray(), faded.ToArray());
            EditorUtility.SetDirty(fade);
        }

        // A see-through copy of a URP Lit material, kept as an asset. It still writes depth (a faded house hides
        // its own far walls instead of looking inside out) and still casts its shadow.
        public static Material FadedMaterial(Material source)
        {
            if (!source.HasProperty("_Surface") || !source.HasProperty("_BaseColor"))
            {
                return null;
            }

            string folder = MaterialsPath + "/Faded";
            EnsureFolder(folder);
            string path = folder + "/" + source.name + "_Faded.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = source.shader;
                mat.CopyPropertiesFromMaterial(source);
            }

            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 1f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A plain box (greybox or collider-only helper).
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, float yaw, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }
            else
            {
                Object.DestroyImmediate(go.GetComponent<Renderer>());
                Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            }

            if (!collider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        public static Material CopyMaterial(string sourcePath, string name)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                AssetDatabase.CopyAsset(sourcePath, path);
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            return mat;
        }

        public static string Report(string text)
        {
            Debug.Log("[L2] " + text);
            return text;
        }

        public static bool FileExists(string assetPath)
        {
            return File.Exists(assetPath);
        }
    }
}
