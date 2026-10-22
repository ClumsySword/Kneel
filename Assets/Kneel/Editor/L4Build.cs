using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Shared helpers for building L4 content: paths and materials. Placing pack meshes goes through L3Build
    // (Part, Seat, Fit) with an L4 material passed in, so the pack sources are never touched.
    public static class L4Build
    {
        public const string PrefabsPath = "Assets/Kneel/Prefabs/L4";
        public const string MaterialsPath = "Assets/Kneel/Materials/L4";
        public const string MeshesPath = "Assets/Kneel/Meshes/L4";
        public const string SettingsPath = "Assets/Kneel/Settings/L4";
        public const string ScenesPath = "Assets/Kneel/Scenes/Levels";
        public const string ReviewScenePath = ScenesPath + "/L4_AssetReview.unity";
        public const string ReviewRootName = "L4_AssetReview";
        public const string ReportPath = PrefabsPath + "/L4_AssetReport.md";

        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        public static Material LoadOrCreate(string name, Shader shader)
        {
            L2Build.EnsureFolder(MaterialsPath);
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }

            return mat;
        }

        // A plain, single-colour material. emission is HDR ("" for none).
        public static Material Flat(string name, string hex, float smoothness = 0.08f, string emissionHex = "", float emissionStrength = 1f)
        {
            var mat = LoadOrCreate(name, Lit);
            mat.SetColor("_BaseColor", L1Build.Hex(hex));
            mat.SetFloat("_Smoothness", smoothness);
            if (!string.IsNullOrEmpty(emissionHex))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", L1Build.Hex(emissionHex) * emissionStrength);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // URP Lit as an alpha-blended decal. Setting the surface type from script leaves the depth passes on (the
        // inspector would turn them off), and then the decal writes into the depth-normals prepass that the
        // PC renderer's SSAO reads, which drew a straight-edged veil across the yard. So switch them off here.
        public static void MakeTransparent(Material mat, int queueOffset = 0)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_ReceiveShadows", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("DepthNormals", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + queueOffset;
            EditorUtility.SetDirty(mat);
        }

        // Children that move or switch on and off at runtime: their meshes are baked separately, under themselves.
        public static readonly System.Collections.Generic.HashSet<string> BakeBoundaries = new System.Collections.Generic.HashSet<string>
        {
            "Swinging", "Falling", "Grid", "Drum", "Alive", "Dead", "Unlit", "Burning", "Spent", "Tell", "Pour", "Scald",
            "Creak", "Debris", "Rattle", "Leaf", "Bar",
        };

        // Generated meshes (blocks, planks, bones) exist only in memory until saved, and a prefab cannot keep
        // them. This merges them into saved meshes, one per bake group and material, each on a child "Baked" of
        // its group, and removes the loose ones. A group is the root, or the nearest ancestor that is inactive at
        // build time or named in BakeBoundaries, so state visuals and moving parts keep working.
        public static void BakeLooseMeshes(GameObject root, string meshName)
        {
            var groups = new System.Collections.Generic.Dictionary<Transform, System.Collections.Generic.List<MeshFilter>>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || AssetDatabase.Contains(filter.sharedMesh) || filter.GetComponent<TMPro.TMP_Text>() != null || filter.name == "Baked")
                {
                    continue;
                }

                var owner = filter.transform;
                while (owner != root.transform && owner.gameObject.activeSelf && BakeBoundaries.Contains(owner.name) == false)
                {
                    owner = owner.parent;
                }

                if (!groups.TryGetValue(owner, out var list))
                {
                    groups[owner] = list = new System.Collections.Generic.List<MeshFilter>();
                }

                list.Add(filter);
            }

            int index = 0;
            foreach (var pair in groups)
            {
                BakeGroup(pair.Key, pair.Value, meshName + (pair.Key == root.transform ? "" : "_" + pair.Key.name + "_" + index), root);
                index++;
            }
        }

        private static void BakeGroup(Transform owner, System.Collections.Generic.List<MeshFilter> loose, string meshName, GameObject root)
        {
            var byMaterial = new System.Collections.Generic.Dictionary<Material, System.Collections.Generic.List<CombineInstance>>();
            foreach (var filter in loose)
            {
                var mats = filter.GetComponent<MeshRenderer>().sharedMaterials;
                for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                {
                    var mat = mats[Mathf.Min(s, mats.Length - 1)];
                    if (!byMaterial.TryGetValue(mat, out var list))
                    {
                        byMaterial[mat] = list = new System.Collections.Generic.List<CombineInstance>();
                    }

                    list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = s, transform = owner.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }

            var baked = new GameObject("Baked");
            baked.transform.SetParent(owner, false);
            var parts = new System.Collections.Generic.List<CombineInstance>();
            var materials = new System.Collections.Generic.List<Material>();
            foreach (var pair in byMaterial)
            {
                var sub = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                sub.CombineMeshes(pair.Value.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                materials.Add(pair.Key);
            }

            var combined = new Mesh { name = meshName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(parts.ToArray(), false, true);
            combined.RecalculateBounds();
            foreach (var part in parts)
            {
                Object.DestroyImmediate(part.mesh);
            }

            L2Build.EnsureFolder(MeshesPath + "/Kit");
            string path = MeshesPath + "/Kit/" + meshName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(combined, existing);
                Object.DestroyImmediate(combined);
                combined = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(combined, path);
            }

            baked.AddComponent<MeshFilter>().sharedMesh = combined;
            baked.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();
            baked.layer = owner.gameObject.layer;

            // Remove the loose meshes, then any holder emptied by that (and only those: empty markers such as
            // sockets stay).
            var emptied = new System.Collections.Generic.HashSet<Transform>();
            foreach (var filter in loose)
            {
                var go = filter.gameObject;
                Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
                if (go != root && go.transform != owner)
                {
                    emptied.Add(go.transform);
                }
            }

            bool removed = true;
            while (removed)
            {
                removed = false;
                foreach (var t in emptied)
                {
                    if (t != null && t != root.transform && t != owner && t.childCount == 0 && t.GetComponents<Component>().Length == 1)
                    {
                        var parent = t.parent;
                        emptied.Remove(t);
                        Object.DestroyImmediate(t.gameObject);
                        if (parent != null && parent != owner)
                        {
                            emptied.Add(parent);
                        }

                        removed = true;
                        break;
                    }
                }
            }
        }

        // Lets a piece fade when it hides the player (the project's OcclusionFade, with mesh probes on the FadeProbe
        // layer), as L3Build.AddFade does, but with the see-through copies kept under L4's materials: L3's helper
        // writes them into L3's folder by source name, and rewrote L3's own copy of a shared material.
        public static void AddFade(GameObject go)
        {
            int probeLayer = L3Build.Layer(Kneel.OcclusionFade.ProbeLayer);
            var renderers = new System.Collections.Generic.List<Renderer>();
            var sources = new System.Collections.Generic.List<Material>();
            var faded = new System.Collections.Generic.List<Material>();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                renderers.Add(r);
                var probe = new GameObject("FadeProbe");
                probe.layer = probeLayer;
                probe.transform.SetParent(r.transform, false);
                probe.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
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

            var fade = go.AddComponent<Kneel.OcclusionFade>();
            fade.Configure(renderers.ToArray(), sources.ToArray(), faded.ToArray());
        }

        // A see-through copy of a URP Lit material under Materials/L4/Faded (same settings as L3's).
        public static Material FadedMaterial(Material source)
        {
            if (!source.HasProperty("_Surface") || !source.HasProperty("_BaseColor"))
            {
                return null;
            }

            string folder = MaterialsPath + "/Faded";
            L2Build.EnsureFolder(folder);
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

        // Pack palette materials that L3Build.Part put on (L3's dawn grade) swapped for L4's.
        public static void Regrade(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    string swap = mats[i].name == "L3_Knights_Dawn" ? "L4_Knights_Stone" : mats[i].name == "L3_Adventure_Dawn" ? "L4_Adventure_Stone" : null;
                    if (swap != null)
                    {
                        mats[i] = Mat(swap);
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }

        public static Material Mat(string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/" + name + ".mat");
            if (mat == null)
            {
                throw new System.ArgumentException("Missing material " + name + "; run Kneel/L4/Build/Materials.");
            }

            return mat;
        }
    }
}
