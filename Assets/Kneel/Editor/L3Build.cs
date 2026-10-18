using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Shared helpers for building L3 content: paths, the layer mapping, materials, and placing pack meshes
    // inside our own prefabs without touching the pack sources.
    public static class L3Build
    {
        public const string PrefabsPath = "Assets/Kneel/Prefabs/L3";
        public const string MaterialsPath = "Assets/Kneel/Materials/L3";
        public const string MeshesPath = "Assets/Kneel/Meshes/L3";
        public const string SettingsPath = "Assets/Kneel/Settings/L3";
        public const string ScenesPath = "Assets/Kneel/Scenes/Levels";
        public const string ReviewScenePath = ScenesPath + "/L3_AssetReview.unity";
        public const string ReportPath = PrefabsPath + "/L3_AssetReport.md";

        // The design note's layers, mapped onto the project's (the player's mouse aim only hits Ground and
        // Obstacles, so cover and boundaries share Obstacles). Hazard is new. Hittable is where the sword looks.
        public const string Walkable = "Ground";
        public const string Boundary = "Obstacles";
        public const string Cover = "Obstacles";
        public const string Hazard = "Hazard";
        public const string Hittable = "Enemy";
        public const string Marker = "Ignore Raycast";
        public const string Unlayered = "Default";

        public static readonly string[] NavAreas = { "Mud", "CropRow" };
        public static readonly float[] NavAreaCosts = { 3f, 2f };

        // ---------------------------------------------------------------- Project settings

        // Adds the Hazard layer and the two NavMesh areas if they are missing. Returns what it did.
        public static string EnsureProjectSettings()
        {
            var log = new List<string>();
            if (LayerMask.NameToLayer(Hazard) < 0)
            {
                var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var layers = tags.FindProperty("layers");
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = Hazard;
                        tags.ApplyModifiedPropertiesWithoutUndo();
                        log.Add("added layer " + Hazard + " at " + i);
                        break;
                    }
                }
            }

            var nav = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var areas = nav.FindProperty("areas");
            for (int a = 0; a < NavAreas.Length; a++)
            {
                if (UnityEngine.AI.NavMesh.GetAreaFromName(NavAreas[a]) >= 0)
                {
                    continue;
                }

                for (int i = 3; i < areas.arraySize; i++)
                {
                    var slot = areas.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.FindPropertyRelative("name").stringValue))
                    {
                        slot.FindPropertyRelative("name").stringValue = NavAreas[a];
                        slot.FindPropertyRelative("cost").floatValue = NavAreaCosts[a];
                        nav.ApplyModifiedPropertiesWithoutUndo();
                        log.Add("added NavMesh area " + NavAreas[a] + " at " + i + " (cost " + NavAreaCosts[a] + ")");
                        break;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return log.Count == 0 ? "project settings already in place" : string.Join("; ", log);
        }

        public static int Layer(string name)
        {
            int index = LayerMask.NameToLayer(name);
            if (index < 0)
            {
                throw new System.InvalidOperationException("Layer '" + name + "' is missing; run Kneel/L3/Setup Project Settings.");
            }

            return index;
        }

        public static int NavArea(string name)
        {
            int index = UnityEngine.AI.NavMesh.GetAreaFromName(name);
            if (index < 0)
            {
                throw new System.InvalidOperationException("NavMesh area '" + name + "' is missing; run Kneel/L3/Setup Project Settings.");
            }

            return index;
        }

        // ---------------------------------------------------------------- Materials

        private static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");

        private static Material LoadOrCreate(string name, Shader shader)
        {
            L2Build.EnsureFolder(MaterialsPath);
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
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

        // An unlit see-through material (markers).
        public static Material Transparent(string name, Color colour)
        {
            var mat = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Our own copy of a pack's palette material with a tint (the pack material itself is never edited).
        public static Material Skin(string name, string sourceMaterialName, Color tint)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Material source = null;
                foreach (var guid in AssetDatabase.FindAssets(sourceMaterialName + " t:Material", new[] { "Assets/SyntyStudios" }))
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (candidate != null && candidate.name == sourceMaterialName)
                    {
                        source = candidate;
                        break;
                    }
                }

                if (source == null)
                {
                    throw new System.ArgumentException("Missing pack material " + sourceMaterialName);
                }

                L2Build.EnsureFolder(MaterialsPath);
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Mat(string name)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/" + name + ".mat");
            if (mat == null)
            {
                throw new System.ArgumentException("Missing material " + name + "; run Kneel/L3/Build Materials.");
            }

            return mat;
        }

        // ---------------------------------------------------------------- Pack pieces

        private static readonly Dictionary<string, GameObject> PackCache = new Dictionary<string, GameObject>();

        // "K:SM_Prop_Cart_01" PolygonKnights, "A:..." PolygonAdventure, "P:..." PolygonPrototype.
        public static GameObject Pack(string key)
        {
            if (PackCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            string pack = key[0] == 'K' ? "PolygonKnights" : key[0] == 'A' ? "PolygonAdventure" : "PolygonPrototype";
            string name = key.Substring(2);
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/SyntyStudios/" + pack }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    PackCache[key] = prefab;
                    return prefab;
                }
            }

            throw new System.ArgumentException("Missing pack prefab " + key);
        }

        // Copies a pack prefab's meshes under parent as plain renderers (no colliders, no link back to the pack),
        // with the pack's palette materials swapped for ours. 'material' forces one material on everything.
        public static GameObject Part(Transform parent, string key, Vector3 position, Vector3 euler, Vector3 scale, Material material = null)
        {
            var source = Pack(key);
            var holder = new GameObject(source.name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            holder.transform.localRotation = Quaternion.Euler(euler);
            holder.transform.localScale = scale;

            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                GameObject go = holder;
                if (filter.transform != source.transform)
                {
                    go = new GameObject(filter.name);
                    go.transform.SetParent(holder.transform, false);
                    Matrix4x4 m = source.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    go.transform.localPosition = m.GetColumn(3);
                    go.transform.localRotation = m.rotation;
                    go.transform.localScale = m.lossyScale;
                }

                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = material != null ? material : Reskin(mats[i]);
                }

                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }

            return holder;
        }

        private static Material Reskin(Material packMaterial)
        {
            if (packMaterial == null)
            {
                return null;
            }

            if (packMaterial.name == "PolyKnights_Mat_01")
            {
                return Mat("L3_Knights_Dawn");
            }

            if (packMaterial.name == "PolyAdventureMaterial_01")
            {
                return Mat("L3_Adventure_Dawn");
            }

            return packMaterial;
        }

        // The box round a piece's meshes, in the space of 'space' (usually the prefab root).
        public static Bounds LocalBounds(GameObject go, Transform space)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (any == false)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        // Places a pack piece with a given rotation and scale so that its box sits on 'groundY' and is centred on
        // (centre.x, centre.y) in the parent's x/z. Use this instead of trusting where a pack pivot is.
        public static GameObject Seat(Transform parent, string key, Vector3 euler, Vector3 scale, Vector2 centre, float groundY = 0f, Material material = null)
        {
            var go = Part(parent, key, Vector3.zero, euler, scale, material);
            Bounds b = LocalBounds(go, parent);
            go.transform.localPosition = new Vector3(centre.x - b.center.x, groundY - b.min.y, centre.y - b.center.z);
            return go;
        }

        public static GameObject Seat(Transform parent, string key, Vector3 euler, float scale, Vector2 centre, float groundY = 0f, Material material = null)
        {
            return Seat(parent, key, euler, Vector3.one * scale, centre, groundY, material);
        }

        // Stretches a pack piece to exactly 'size' (x, height, z in the parent's space), turned by a multiple of
        // 90 degrees, sitting on groundY and centred on 'centre'.
        public static GameObject Fit(Transform parent, string key, int yaw, Vector3 size, Vector2 centre, float groundY = 0f, Material material = null)
        {
            var go = Part(parent, key, Vector3.zero, new Vector3(0f, yaw, 0f), Vector3.one, material);
            Bounds b = LocalBounds(go, parent);
            bool swapped = Mathf.Abs(yaw) % 180 == 90;
            // A quarter turn swaps which local axis lies along the parent's x and z.
            float sx = size.x / b.size.x, sy = size.y / b.size.y, sz = size.z / b.size.z;
            go.transform.localScale = swapped ? new Vector3(sz, sy, sx) : new Vector3(sx, sy, sz);
            b = LocalBounds(go, parent);
            go.transform.localPosition = new Vector3(centre.x - b.center.x, groundY - b.min.y, centre.y - b.center.z);
            return go;
        }

        public static GameObject Prim(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // A cube given by its size and the centre of its base.
        public static GameObject Block(Transform parent, string name, Vector3 size, Vector3 baseCentre, Material material, float yaw = 0f)
        {
            return Prim(parent, name, PrimitiveType.Cube, baseCentre + Vector3.up * size.y * 0.5f, new Vector3(0f, yaw, 0f), size, material);
        }

        public static GameObject Child(Transform parent, string name, Vector3 position = default, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        public static void SetLayer(GameObject go, string layer)
        {
            int index = Layer(layer);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = index;
            }
        }

        // A box collider standing on the object's pivot: size is (x, height, z).
        public static BoxCollider StandingBox(GameObject go, Vector3 size, bool trigger = false)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.isTrigger = trigger;
            return box;
        }

        // A convex cylinder collider (Unity has no cylinder primitive collider) on its own child.
        public static MeshCollider StandingCylinder(GameObject go, float diameter, float height)
        {
            var holder = Child(go.transform, "Collider", new Vector3(0f, height * 0.5f, 0f));
            holder.layer = go.layer;
            holder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            // The primitive's own mesh: 1 m across, 2 m tall.
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var collider = holder.AddComponent<MeshCollider>();
            collider.sharedMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;
            Object.DestroyImmediate(primitive);
            return collider;
        }

        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                throw new System.ArgumentException(target.GetType().Name + " has no serialized field '" + field + "'");
            }

            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
                case string s: p.stringValue = s; break;
                case Vector2 v2: p.vector2Value = v2; break;
                case Vector3 v3: p.vector3Value = v3; break;
                case Color c: p.colorValue = c; break;
                case LayerMask mask: p.intValue = mask.value; break;
                case System.Enum e: p.enumValueIndex = System.Convert.ToInt32(e); break;
                case Object o: p.objectReferenceValue = o; break;
                case null: p.objectReferenceValue = null; break;
                default: throw new System.ArgumentException("Unsupported value type " + value.GetType().Name);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject Prefab(string relativePath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsPath + "/" + relativePath + ".prefab");
        }

        // An instance of one of our own prefabs, kept linked to it.
        public static GameObject Instance(GameObject prefab, Transform parent, Vector3 position, float yaw = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // Makes a piece fade out while it hides the player, with the project's own Kneel.OcclusionFade (the
        // fader on the camera raycasts mesh probes on the FadeProbe layer). Like L2Build.AddFade, but the
        // see-through material copies are kept under the L3 materials, and low pieces fade too.
        public static void AddFade(GameObject go)
        {
            int probeLayer = Layer(Kneel.OcclusionFade.ProbeLayer);
            var renderers = new List<Renderer>();
            var sources = new List<Material>();
            var faded = new List<Material>();
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

        // A see-through copy of a URP Lit material, kept as an asset. It still writes depth, so a faded wall
        // hides what is behind it in the same piece instead of looking inside out.
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

        // A mesh renderer on a new child.
        public static GameObject MeshChild(Transform parent, string name, Mesh mesh, Vector3 position, params Material[] materials)
        {
            var go = Child(parent, name, position);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        // Saves a generated mesh under Meshes/L3/<folder>, replacing an earlier bake in place.
        public static Mesh SaveMesh(L3Mesh data, string folder, string name)
        {
            L2Build.EnsureFolder(MeshesPath + "/" + folder);
            string path = MeshesPath + "/" + folder + "/" + name + ".asset";
            var mesh = data.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // A kit mesh from L2PolyKit (a plank or a stone tread) as loose data, ready to merge.
        public static L3Mesh Kit(Mesh generated)
        {
            var data = L3Mesh.From(generated);
            Object.DestroyImmediate(generated);
            return data;
        }

        // A plank with a finished face on both sides. L2PolyKit planks are deck boards (no underside), so one
        // stood on edge in a gate or a sail would vanish from behind.
        public static L3Mesh Board(Vector3 size, int seed)
        {
            L3Mesh plank = Kit(L2PolyKit.Plank(size, seed));
            var board = new L3Mesh();
            board.Append(plank, Matrix4x4.identity, 0);
            board.Append(plank, Matrix4x4.Scale(new Vector3(1f, -1f, 1f)), 0);
            return board;
        }

        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            L2Build.EnsureFolder(MeshesPath);
            string path = MeshesPath + "/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
