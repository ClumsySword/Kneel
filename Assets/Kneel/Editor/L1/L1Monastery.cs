using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools
{
    // The monastery at the start of L1 (the prologue cutscene's, 05-Art/Prologue Cutscene Prompts.md): the flooded
    // sanctuary with the broken relic, the inner door with the guard Kael killed, the corridor of long-dead monks and
    // the main door onto the battlefield. Built for the gameplay camera (yaw 270: the camera on +x looking down
    // towards -x): no roof, tall walls only up-screen (-x) and stepping down at the ends, the camera side (+x) cut
    // to 0.9 m. Rooms are in L1Layout. `Kneel/L1/Monastery/Build Prefabs` (re)builds every part as a prefab under
    // Levels/L1/Prefabs/Monastery and the whole as Levels/L1/Prefabs/L1_Monastery.prefab, in world coordinates
    // (place it at the origin); L1MonasteryScene puts it in a scene.
    public static partial class L1Monastery
    {
        public const string L1Folder = "Assets/Kneel/Levels/L1";
        public const string PartsFolder = L1Folder + "/Prefabs/Monastery";
        public const string MeshFolder = L1Folder + "/Meshes/Monastery";
        public const string MatFolder = L1Folder + "/Materials/Monastery";
        public const string PrefabPath = L1Folder + "/Prefabs/L1_Monastery.prefab";

        // ---------------------------------------------------------------- Dimensions (world, metres)

        public const float Wall = 1f;
        public const float Low = 0.9f;               // every camera-side wall
        public const float SanctuaryHigh = 9f, CorridorHigh = 6.5f, FacadeHigh = 6.3f, CrossHigh = 6.3f;
        // The inner door's segment of the round hall: its inner face (z -33.5) and outer face (z -32.5).
        public const float SanctuaryZ1 = -33.5f;
        public const float VestibuleX = 3.25f, VestibuleZ0 = -32.5f, VestibuleZ1 = -28f;
        // The vestibule's side walls run on into the round hall's wall, which curves away from the door.
        public const float VestibuleWallZ0 = -33.4f;
        public const float CorridorX = 2.25f, CorridorZ1 = -10.5f;
        public const float DoorHalf = 1.25f, InnerSpring = 3f, OuterSpring = 3.4f;
        public const float WaterY = 0.08f;
        public const uint WaterRenderingLayer = 1u << 4;

        // Where Kael lies when L1 opens: beside the altar on the door side, a little towards the camera.
        public static readonly Vector3 WakeSpot = new Vector3(1.4f, 0f, -49.6f);

        public static readonly float[] CorridorBays = { -25.8f, -21.4f, -17f, -12.7f };
        public static readonly float[] CorridorPilasters = { -23.6f, -19.2f, -14.8f };

        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        [MenuItem("Kneel/L1/Monastery/Build Prefabs")]
        public static void BuildMenu()
        {
            Debug.Log("[L1 Monastery] " + Build());
        }

        public static string Build()
        {
            var log = new StringBuilder();
            L2Build.EnsureFolder(PartsFolder);
            L2Build.EnsureFolder(MeshFolder);
            L2Build.EnsureFolder(MatFolder);
            Materials();

            var parts = new List<(string name, GameObject prefab, Vector3 position, Quaternion rotation)>();
            void Add(string name, GameObject prefab) => parts.Add((name, prefab, Vector3.zero, Quaternion.identity));

            Add("Sanctuary", Part("L1_Mon_Sanctuary", Hall, log, layer: "Obstacles"));
            Add("Corridor", Part("L1_Mon_Corridor", Corridor, log, layer: "Obstacles"));
            Add("Floors", Part("L1_Mon_Floors", Floors, log, layer: "Ground"));
            Add("Altar", Part("L1_Mon_Altar", Altar, log, layer: "Obstacles"));
            Add("InnerDoor", Part("L1_Mon_InnerDoor", root => Doors(root, true), log, layer: "Obstacles"));
            Add("OuterDoor", Part("L1_Mon_OuterDoor", root => Doors(root, false), log, layer: "Obstacles"));
            Add("Windows", Part("L1_Mon_Windows", Windows, log, layer: "Obstacles"));
            Add("Dressing", Part("L1_Mon_Dressing", Dressing, log, layer: "Obstacles"));
            Add("Water", Part("L1_Mon_Water", Water, log));
            Add("Bodies", Part("L1_Mon_Bodies", Bodies, log, bake: false, layer: "Obstacles"));
            Add("Lighting", Part("L1_Mon_Lighting", Lighting, log, bake: false));
            Add("Colliders", Part("L1_Mon_Colliders", Colliders, log, bake: false));

            var root = new GameObject("L1_Monastery");
            foreach (var p in parts)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(p.prefab, root.transform);
                instance.name = p.name;
                instance.transform.SetPositionAndRotation(p.position, p.rotation);
            }

            var wake = new GameObject("WakeSpot");
            wake.transform.SetParent(root.transform, false);
            wake.transform.SetPositionAndRotation(WakeSpot, Quaternion.identity);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            log.Append("Saved ").Append(PrefabPath).Append('.');
            return log.ToString();
        }

        // Builds one part into a scratch root (world space), bakes its loose meshes into a few saved ones by
        // material (unless bake is off), and saves it as a prefab.
        private static GameObject Part(string name, System.Action<Transform> build, StringBuilder log, bool bake = true, string layer = null)
        {
            var scratch = new GameObject(name);
            build(scratch.transform);
            GameObject result = scratch;
            if (bake)
            {
                result = Bake(scratch.transform, name);
                // Keep what isn't a loose mesh (lights, colliders, particles, instanced prefabs) alongside the bake.
                foreach (Transform keep in Keepers(scratch.transform))
                {
                    keep.SetParent(result.transform, true);
                }

                Object.DestroyImmediate(scratch);
            }
            else
            {
                Keepers(scratch.transform);   // only strips the "#" marks
            }

            // Renderers go on the part's layer (walls and props on Obstacles, floors on Ground); colliders and
            // water keep the layers they were made with.
            if (layer != null)
            {
                int index = L3Build.Layer(layer);
                foreach (var r in result.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<Collider>() == null && r.gameObject.layer == 0)
                    {
                        r.gameObject.layer = index;
                    }
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(result, PartsFolder + "/" + name + ".prefab");
            int tris = 0;
            foreach (var mf in result.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh != null)
                {
                    tris += (int)(mf.sharedMesh.GetIndexCount(0) / 3);
                    for (int s = 1; s < mf.sharedMesh.subMeshCount; s++)
                    {
                        tris += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
                    }
                }
            }

            log.Append(name).Append(" (").Append(tris).Append(" tris), ");
            Object.DestroyImmediate(result);
            return prefab;
        }

        // Children marked to survive a bake: named "#..." (lights, colliders, prefab instances).
        private static List<Transform> Keepers(Transform root)
        {
            var list = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name.StartsWith("#") && (t.parent == root || !t.parent.name.StartsWith("#")))
                {
                    list.Add(t);
                }
            }

            foreach (var t in list)
            {
                t.name = t.name.Substring(1);
            }

            return list;
        }

        // Combines every loose MeshFilter under 'built' (outside "#" keepers) into one saved mesh per material.
        private static GameObject Bake(Transform built, string name)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            var flags = new Dictionary<Material, bool>();
            foreach (var mf in built.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || InKeeper(mf.transform, built))
                {
                    continue;
                }

                var mr = mf.GetComponent<MeshRenderer>();
                var mats = mr.sharedMaterials;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                {
                    var mat = Remap(mats[Mathf.Min(s, mats.Length - 1)]);
                    if (!byMaterial.TryGetValue(mat, out var list))
                    {
                        byMaterial[mat] = list = new List<CombineInstance>();
                        order.Add(mat);
                        flags[mat] = mr.shadowCastingMode != ShadowCastingMode.Off;
                    }

                    list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = built.worldToLocalMatrix * mf.transform.localToWorldMatrix });
                }
            }

            var root = new GameObject(name);
            foreach (var mat in order)
            {
                var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(byMaterial[mat].ToArray(), true, true);
                mesh.RecalculateBounds();
                string meshName = name + "_" + mat.name.Replace("L1_Mon_", "");
                mesh = SaveMesh(mesh, meshName);
                var child = new GameObject(meshName);
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = flags[mat] ? ShadowCastingMode.On : ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(child, L1Build.EnvironmentStatic);
            }

            return root;
        }

        private static bool InKeeper(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (t.name.StartsWith("#"))
                {
                    return true;
                }
            }

            return false;
        }

        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshFolder + "/" + name + ".asset";
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
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ---------------------------------------------------------------- Materials

        // The kit's stand-in materials map onto the monastery's own: cold blue-grey stone (the cutscene's palette)
        // with the same eight cells by role.
        private static Material Remap(Material m)
        {
            if (m == null)
            {
                return Mat("Stone");
            }

            if (m == L4Kit.Stone)
            {
                return Mat("Stone");
            }

            if (m.name == "L2_Palette_Wood")
            {
                return Mat("Wood");
            }

            return m;
        }

        public static Material Mat(string key)
        {
            if (Mats.TryGetValue(key, out var m) && m != null)
            {
                return m;
            }

            m = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/L1_Mon_" + key + ".mat");
            Mats[key] = m;
            return m;
        }

        private static void Materials()
        {
            Mats.Clear();
            // Cells: top, top light, top dark, side, end, edge, iron, joint.
            // Joint and edge cells close to the stone: dark joints and bright chamfers between thousands of blocks were
            // hair-thin lines that crawled and flashed as the camera moved.
            Palette("Stone", new[] { "#66717E", "#6A7582", "#626D79", "#56606B", "#5E6874", "#6E7986", "#1B1D21", "#47505A" }, 0.08f);
            Palette("Wood", new[] { "#3A2F27", "#46392E", "#2E2520", "#30271F", "#2A221C", "#4E4033", "#141414", "#1C1714" }, 0.08f);
            Palette("Iron", new[] { "#1D1F22", "#2A2D31", "#18191B", "#222427", "#26292D", "#33373C", "#121314", "#0E0F10" }, 0.35f, 0.4f);
            Palette("Dust", new[] { "#6E6A63", "#7B766E", "#625E58", "#57534E", "#67635C", "#86817A", "#2A2826", "#3A3734" }, 0.02f);

            var obsidian = LoadOrCreate("Relic", Shader.Find("Universal Render Pipeline/Lit"));
            obsidian.SetColor("_BaseColor", L1Build.Hex("#0D0E12"));
            obsidian.SetFloat("_Smoothness", 0.72f);
            obsidian.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(obsidian);
            Mats["Relic"] = obsidian;

            // Window glass: the cold sky beyond, a crisp pale pane (no halo, no bloom spill: emission under the
            // bloom threshold after the monastery's grade).
            var glass = LoadOrCreate("Glass", Shader.Find("Universal Render Pipeline/Lit"));
            glass.SetColor("_BaseColor", L1Build.Hex("#2A3442"));
            glass.SetFloat("_Smoothness", 0.6f);
            glass.EnableKeyword("_EMISSION");
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            glass.SetColor("_EmissionColor", L1Build.Hex("#6F87A6") * 0.6f);
            EditorUtility.SetDirty(glass);
            Mats["Glass"] = glass;

            // Still, cold, shallow water over the flags: the project's water shader, teal (the cutscene's), nearly
            // flowless, its glints strong: seen from above, water reflects only a few percent, so the lights' glints
            // and the tint are what make it read.
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Materials/L2/L2_Creek_Water.mat");
            var water = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/L1_Mon_Water.mat");
            if (water == null)
            {
                water = new Material(source);
                AssetDatabase.CreateAsset(water, MatFolder + "/L1_Mon_Water.mat");
            }

            water.shader = source.shader;
            water.CopyPropertiesFromMaterial(source);
            // Murky teal, as the cutscene's: the floor is hidden a few centimetres down (owner, 2026-11-23).
            water.SetColor("_DeepColor", new Color(0.075f, 0.27f, 0.28f, 1f));
            water.SetColor("_ShallowColor", new Color(0.55f, 1f, 0.97f, 1f));
            water.SetColor("_ScumColor", new Color(0.3f, 0.46f, 0.47f, 1f));
            water.SetFloat("_Murk", 38f);
            water.SetFloat("_FlowSpeed", 0.015f);
            water.SetFloat("_RippleStrength", 0.025f);
            water.SetFloat("_RippleTile", 4f);
            water.SetFloat("_ShoreFade", 0.05f);
            water.SetFloat("_Refraction", 0.006f);
            water.SetFloat("_Smoothness", 0.985f);
            water.SetFloat("_Reflection", 0.9f);
            water.SetFloat("_Glint", 1.6f);
            water.SetFloat("_HalfWidth", 100f);
            EditorUtility.SetDirty(water);
            Mats["Water"] = water;

            // Light shafts: soft additive cards in the cold window light.
            var shaftTex = AssetDatabase.LoadAssetAtPath<Texture2D>(L1Folder + "/Lighting/Textures/L1_SoftShaft.png");
            Mats["ShaftCold"] = Additive("ShaftCold", shaftTex, new Color(0.62f, 0.74f, 0.92f, 0.055f));
            Mats["ShaftOculus"] = Additive("ShaftOculus", shaftTex, new Color(0.66f, 0.8f, 0.95f, 0.085f));
            Mats["ShaftWarm"] = Additive("ShaftWarm", shaftTex, new Color(1f, 0.62f, 0.3f, 0.16f));

            // The dead: the L2 commoners' texture graded to dusty grey wool (monks) and left as it is (the guard).
            Mats["MonkDust"] = Graded("MonkDust", "Assets/Kneel/Materials/L2/L2_Commoner.mat", 0.18f, 0.55f, new Color(0.47f, 0.44f, 0.4f));
            Mats["MonkDustDark"] = Graded("MonkDustDark", "Assets/Kneel/Materials/L2/L2_Commoner.mat", 0.2f, 0.4f, new Color(0.36f, 0.33f, 0.3f));
            Mats["BoneDust"] = Graded("BoneDust", AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Prefabs/L2/Skeletons/L2_Skeleton_Slumped.prefab").GetComponentInChildren<Renderer>().sharedMaterial), 0.35f, 0.35f, new Color(0.55f, 0.53f, 0.49f));
            AssetDatabase.SaveAssets();
        }

        private static Material LoadOrCreate(string key, Shader shader)
        {
            string path = MatFolder + "/L1_Mon_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            return mat;
        }

        private static void Palette(string key, string[] tones, float smoothness, float metallic = 0f)
        {
            string texPath = MatFolder + "/L1_Mon_" + key + "_Palette.png";
            var tex = new Texture2D(tones.Length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < tones.Length; i++)
            {
                tex.SetPixel(i, 0, L1Build.Hex(tones[i]));
            }

            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var mat = LoadOrCreate(key, Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            Mats[key] = mat;
        }

        private static Material Additive(string key, Texture2D texture, Color color)
        {
            var mat = LoadOrCreate(key, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // A copy of a material whose texture is desaturated, lifted towards 'dust' and darkened: old dead under dust.
        private static Material Graded(string key, string sourcePath, float saturation, float dustAmount, Color dust)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            var srcTex = source.GetTexture("_BaseMap") as Texture2D;
            if (srcTex == null)
            {
                // A flat-coloured source: grade the colour itself.
                var flat = LoadOrCreate(key, source.shader);
                flat.CopyPropertiesFromMaterial(source);
                var c0 = source.GetColor("_BaseColor");
                float g0 = c0.r * 0.3f + c0.g * 0.59f + c0.b * 0.11f;
                var graded = Color.Lerp(Color.Lerp(new Color(g0, g0, g0), c0, saturation), dust, dustAmount);
                graded.a = 1f;
                flat.SetColor("_BaseColor", graded);
                flat.SetFloat("_Smoothness", 0.02f);
                EditorUtility.SetDirty(flat);
                return flat;
            }

            string texPath = MatFolder + "/L1_Mon_" + key + ".png";
            string srcPath = AssetDatabase.GetAssetPath(srcTex);
            var read = new Texture2D(2, 2);
            read.LoadImage(File.ReadAllBytes(srcPath));
            var pixels = read.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                var desat = Color.Lerp(new Color(grey, grey, grey), c, saturation);
                pixels[i] = Color.Lerp(desat, dust, dustAmount);
                pixels[i].a = 1f;
            }

            read.SetPixels(pixels);
            read.Apply();
            File.WriteAllBytes(texPath, read.EncodeToPNG());
            Object.DestroyImmediate(read);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var srcImporter = (TextureImporter)AssetImporter.GetAtPath(srcPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.filterMode = srcImporter.filterMode;
            importer.mipmapEnabled = srcImporter.mipmapEnabled;
            importer.SaveAndReimport();

            var mat = LoadOrCreate(key, source.shader);
            mat.CopyPropertiesFromMaterial(source);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Walls

        // A wall group whose local frame is the kit's: +x along the wall from 'innerStart' (a point on its inner face
        // at floor level), +z into the room.
        private static Transform WallFrame(Transform parent, string name, Vector3 innerStart, Vector3 inward)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.SetPositionAndRotation(innerStart, Quaternion.LookRotation(inward, Vector3.up));
            return g;
        }

        // Height steps down from 'high' to the low camera-side height across x from..to (world x, so it works for
        // walls running either way), quantised to courses.
        private static System.Func<float, float> StepDown(Transform frame, float high, float from, float to)
        {
            return u => StepHeight(frame.TransformPoint(new Vector3(u, 0f, 0f)).x, high, from, to);
        }

        public static float StepHeight(float x, float high, float from, float to)
        {
            float h = Mathf.Lerp(high, Low, Mathf.InverseLerp(from, to, x));
            return Mathf.Max(Low, Mathf.Round(h / L1MonasteryKit.Course) * L1MonasteryKit.Course);
        }

        // The stepped walls' cut-aways (world x where they start and end stepping down).
        // Walls across the level (facing +-z) are seen almost edge-on from beside the camera, which rides level with
        // Kael's z: anything tall there towers over the frame. So they stand full height only well up-screen and are
        // cut to the low height before the doors' up-screen jambs; the doors are plain gaps with one leaf open.
        public const float EndStepFrom = -4.2f, EndStepTo = -1.8f, DoorStepFrom = -3.4f, DoorStepTo = -1.3f, FacadeStepFrom = -2.6f, FacadeStepTo = -1.3f;

        private static System.Func<float, float> Flat(float h) => _ => h;

        private static void Corridor(Transform root)
        {
            float vx = VestibuleX, cx = CorridorX;
            // Vestibule (inside the inner door): a little wider than the corridor, one window.
            var vFar = WallFrame(root, "VestibuleFar", new Vector3(-vx, 0f, VestibuleZ1), Vector3.right);
            L1MonasteryKit.Wall(vFar, VestibuleZ1 - VestibuleWallZ0, Wall, Flat(CorridorHigh), new[] { new L1MonasteryKit.Opening((VestibuleZ1 - VestibuleZ0) * 0.5f, 0.4f, 1.3f, 3.1f, true) }, 0.15f, 201);
            var vNear = WallFrame(root, "VestibuleCamera", new Vector3(vx, 0f, VestibuleWallZ0), Vector3.left);
            L1MonasteryKit.Wall(vNear, VestibuleZ1 - VestibuleWallZ0, Wall, Flat(Low), null, 0.1f, 202, 3f);

            // The corridor: tall wall up-screen with narrow lancets, low on the camera side, pilasters between bays.
            var far = WallFrame(root, "CorridorFar", new Vector3(-cx, 0f, CorridorZ1), Vector3.right);
            var windows = new List<L1MonasteryKit.Opening>();
            foreach (float z in CorridorBays)
            {
                windows.Add(new L1MonasteryKit.Opening(CorridorZ1 - z, 0.35f, 1.3f, 3.1f, true));
            }

            L1MonasteryKit.Wall(far, CorridorZ1 - VestibuleZ1, Wall, Flat(CorridorHigh), windows.ToArray(), 0.15f, 203);
            var near = WallFrame(root, "CorridorCamera", new Vector3(cx, 0f, VestibuleZ1), Vector3.left);
            L1MonasteryKit.Wall(near, CorridorZ1 - VestibuleZ1, Wall, Flat(Low), null, 0.1f, 204, 3f);
            int seed = 210;
            foreach (float z in CorridorPilasters)
            {
                L1MonasteryKit.Pilaster(far, CorridorZ1 - z, 0.6f, 0.3f, CorridorHigh - 0.45f, 0f, seed++);
                L1MonasteryKit.Pilaster(near, z - VestibuleZ1, 0.6f, 0.3f, Low + 0.2f, 0.6f, seed++);
            }

            // The façade: the main door at x 0, tall up-screen of it, stepping down to the camera-side wall.
            var door = L1MonasteryKit.Opening.Gap(cx + Wall, DoorHalf);
            var facade = WallFrame(root, "Facade", new Vector3(cx + Wall, 0f, CorridorZ1), Vector3.back);
            L1MonasteryKit.Wall(facade, 2f * (cx + Wall), Wall, StepDown(facade, FacadeHigh, FacadeStepFrom, FacadeStepTo), new[] { door }, 0.3f, 220, 1f);

        }

        // ---------------------------------------------------------------- Floors

        // ---------------------------------------------------------------- Altar and relic

        private static void Altar(Transform root)
        {
            var at = new Vector3(L1Layout.AltarCentre.x, 0f, L1Layout.AltarCentre.y);
            var stone = new L3Mesh();
            L1MonasteryKit.Altar(stone);
            L3Build.MeshChild(root, "Altar", stone.ToMesh("Altar"), at, L4Kit.Stone);

            var relic = new L3Mesh();
            L1MonasteryKit.Relic(relic, 401);
            L3Build.MeshChild(root, "Relic", relic.ToMesh("Relic"), at, Mat("Relic"));

            // The altar is solid: a convex hull of its stone.
            var hull = new GameObject("#AltarCollider");
            hull.transform.SetParent(root, false);
            hull.transform.position = at;
            hull.layer = L3Build.Layer("Obstacles");
            var mc = hull.AddComponent<MeshCollider>();
            var simple = new L3Mesh();
            L4Shapes.Lathe(simple, 0, Matrix4x4.identity, new[] { new Vector2(1.95f, 0f), new Vector2(1.95f, 0.54f), new Vector2(1.02f, 1.42f), new Vector2(0f, 1.42f) }, 8, Vector2.zero, -1, Mathf.PI / 8f);
            mc.sharedMesh = SaveMesh(simple.ToMesh("Hull"), "L1_Mon_AltarHull");
            mc.convex = true;
        }

        // ---------------------------------------------------------------- Doors

        // One heavy plank leaf standing open in each doorway, hinged on the up-screen jamb: the inner door swung
        // into the sanctuary, the main door out onto the battlefield.
        private static void Doors(Transform root, bool inner)
        {
            const float leafHeight = 3.1f;
            var o = new L1MonasteryKit.Opening(0f, 2f * DoorHalf - 0.05f, 0f, leafHeight, false);
            var wood = new L3Mesh();
            var iron = new L3Mesh();
            L1MonasteryKit.Leaf(wood, iron, o, true, inner ? 501 : 502, leafHeight);
            var woodMesh = SaveMesh(wood.ToMesh("Leaf"), inner ? "L1_Mon_InnerLeaf_Wood" : "L1_Mon_OuterLeaf_Wood");
            var ironMesh = SaveMesh(iron.ToMesh("LeafIron"), inner ? "L1_Mon_InnerLeaf_Iron" : "L1_Mon_OuterLeaf_Iron");

            // Hinged on the up-screen jamb (x -1.25) on the face the door swings to: the sanctuary side (-z) for
            // the inner door, outside (+z) for the main one. The leaf's frame runs +x from the hinge; turned so it
            // lies open along that face, away from the doorway (towards -x).
            float faceZ = inner ? SanctuaryZ1 - 0.07f : CorridorZ1 + Wall + 0.07f;
            float yaw = inner ? 180f - 8f : 180f + 12f;   // tilted a little off the wall face
            var hinge = new GameObject("#Leaf");
            hinge.transform.SetParent(root, false);
            hinge.transform.SetPositionAndRotation(new Vector3(-DoorHalf, 0f, faceZ), Quaternion.Euler(0f, yaw, 0f));
            foreach (var (name, mesh, mat) in new[] { ("Wood", woodMesh, Mat("Wood")), ("Iron", ironMesh, Mat("Iron")) })
            {
                var g = L3Build.MeshChild(hinge.transform, name, mesh, Vector3.zero, mat);
                GameObjectUtility.SetStaticEditorFlags(g, L1Build.PropStatic);
            }

            var box = new GameObject("Collider");
            box.transform.SetParent(hinge.transform, false);
            box.layer = L3Build.Layer("Obstacles");
            var collider = box.AddComponent<BoxCollider>();
            collider.center = new Vector3(o.W * 0.5f, leafHeight * 0.5f, 0f);
            collider.size = new Vector3(o.W, leafHeight, 0.12f);
        }

        // ---------------------------------------------------------------- Windows

        private static void Windows(Transform root)
        {
            var glass = new L3Mesh();
            var iron = new L3Mesh();
            HallPanes(glass, iron);
            foreach (float z in CorridorBays)
            {
                AddPane(glass, iron, new Vector3(-CorridorX, 0f, z), Vector3.right, new L1MonasteryKit.Opening(0f, 0.35f, 1.3f, 3.1f, true), Wall * 0.55f);
            }

            AddPane(glass, iron, new Vector3(-VestibuleX, 0f, (VestibuleZ0 + VestibuleZ1) * 0.5f), Vector3.right, new L1MonasteryKit.Opening(0f, 0.4f, 1.3f, 3.1f, true), Wall * 0.55f);
            var panes = L3Build.MeshChild(root, "Panes", glass.ToMesh("Panes"), Vector3.zero, Mat("Glass"));
            panes.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            L3Build.MeshChild(root, "Cames", iron.ToMesh("Cames"), Vector3.zero, Mat("Iron"));
        }

        // A pane in the window whose inner face centre is 'innerCentre' (the wall facing 'inward'), 'depth' into it.
        private static void AddPane(L3Mesh glass, L3Mesh iron, Vector3 innerCentre, Vector3 inward, L1MonasteryKit.Opening o, float depth)
        {
            var g = new L3Mesh();
            var i = new L3Mesh();
            L1MonasteryKit.Pane(g, i, o, 0f);
            // Local u (x) runs along the wall, the pane faces 'inward' (into the room).
            var frame = Matrix4x4.TRS(innerCentre - inward * depth, Quaternion.LookRotation(inward, Vector3.up), Vector3.one);
            glass.Append(g, frame, 0);
            iron.Append(i, frame, 0);
        }

        // ---------------------------------------------------------------- Dressing: stands, dust, rubble

        // No candles (owner, 2026-11-23: their tiny stubs flickered as the camera moved).
        private static void Dressing(Transform root)
        {
            var iron = new L3Mesh();
            var dust = new L3Mesh();
            var rng = new System.Random(601);

            HallDressing(root, rng);

            // Empty pricket stands either side of the inner door, in the vestibule; one has fallen.
            L1MonasteryKit.Pricket(iron, new Vector3(-1.95f, 0f, VestibuleZ0 + 0.7f), 1.3f);
            var fallen = new L3Mesh();
            L1MonasteryKit.Pricket(fallen, Vector3.zero, 1.3f);
            var lying = Matrix4x4.TRS(new Vector3(2.25f, 0.28f, VestibuleZ0 + 1.2f), Quaternion.Euler(0f, 35f, 84f), Vector3.one);
            iron.Append(fallen, lying, 0);

            // Dust drifted along both corridor walls.
            for (float z = VestibuleZ1 + 0.4f; z < CorridorZ1 - 0.6f; z += 2.2f + (float)rng.NextDouble() * 1.6f)
            {
                float len = 0.8f + (float)rng.NextDouble() * 1.2f;
                bool farSide = rng.NextDouble() < 0.5;
                L1MonasteryKit.Drift(dust, new Vector3(farSide ? -CorridorX + 0.25f : CorridorX - 0.25f, 0f, z), Vector3.forward, len, rng.Next(100000));
            }

            L1MonasteryKit.Drift(dust, new Vector3(-VestibuleX + 0.3f, 0f, VestibuleZ0 + 0.4f), Vector3.forward, 1.6f, 640);
            L1MonasteryKit.Drift(dust, new Vector3(VestibuleX - 0.3f, 0f, VestibuleZ1 - 1.6f), Vector3.forward, 1.2f, 641);

            L3Build.MeshChild(root, "Iron", iron.ToMesh("Iron"), Vector3.zero, Mat("Iron"));
            var dustGo = L3Build.MeshChild(root, "Dust", dust.ToMesh("Dust"), Vector3.zero, Mat("Dust"));
            dustGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Fallen stones where the camera-side walls are broken down.
            foreach (var p in new[] { new Vector3(1.85f, 0f, -21.8f), new Vector3(2.9f, 0f, -30.9f) })
            {
                for (int i = 0; i < 2 + rng.Next(2); i++)
                {
                    L4Kit.Fallen(root, p + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.6f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.9f), rng);
                }
            }
        }

        // ---------------------------------------------------------------- Water

        // One sheet of still water over the whole sanctuary floor and into the door's threshold: a 1 m grid,
        // uv0 = world x/z (the shader's ripples), uv1.x = 0 (no strip edges; the shore fades on depth).
        private static void Water(Transform root)
        {
            var verts = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var tris = new List<int>();
            HallSheet(verts, uv0, uv1, tris);
            var mesh = new Mesh { name = "Water", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("#Water");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "L1_Mon_WaterSheet");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat("Water");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            // No light touches the water (it sits alone on rendering layer 4; lights only light layer 0): each one
            // left a hard white glint on it, the beam from above a bloomed blob. Its sheen is the reflection of the
            // vaulted hall (L1MonasteryReflection), which already holds the lit altar and windows.
            mr.renderingLayerMask = WaterRenderingLayer;
            go.layer = L3Build.Layer("Water");
        }

        // ---------------------------------------------------------------- The dead

        // Prefab variants of L2's baked commoners and skeletons in the dust materials, and the guard and his spear.
        private static void Bodies(Transform root)
        {
            const string corpses = "Assets/Kneel/Prefabs/L2/Corpses/";
            const string skeletons = "Assets/Kneel/Prefabs/L2/Skeletons/";
            var monkA = Variant(corpses + "L2_Corpse_Peasant_Slumped.prefab", "L1_Mon_Monk_Slumped", Mat("MonkDust"));
            var monkB = Variant(corpses + "L2_Corpse_Shopkeeper_Slumped.prefab", "L1_Mon_Monk_SlumpedB", Mat("MonkDustDark"));
            var monkC = Variant(corpses + "L2_Corpse_Shopkeeper_OnSide.prefab", "L1_Mon_Monk_OnSide", Mat("MonkDust"));
            var bones = Variant(skeletons + "L2_Skeleton_Slumped.prefab", "L1_Mon_Monk_Bones", Mat("BoneDust"));
            var guard = Variant(corpses + "L2_Corpse_Peasant_OnBack.prefab", "L1_Mon_Guard", null);
            var spear = Spear();

            // Corridor corners: slumped against the pilasters with their legs along the wall, off the walking line.
            Place(monkA, root, new Vector3(-1.8f, 0f, -24.5f), 180f);
            Place(monkB, root, new Vector3(1.8f, 0f, -18.3f), 0f);
            Place(monkA, root, new Vector3(-1.8f, 0f, -15.7f), 186f);
            Place(bones, root, new Vector3(-1.75f, 0f, CorridorZ1 - 0.62f), 172f);
            Place(monkC, root, new Vector3(2.55f, 0f, -29.3f), 90f);

            // The guard where he fell, in front of the inner door, his spear beside him.
            Place(guard, root, new Vector3(-2.3f, 0f, -30.6f), 160f);
            Place(spear, root, new Vector3(-1.65f, 0.035f, -31.9f), 0f).transform.rotation = Quaternion.Euler(0f, 74f, 90f);
        }

        private static GameObject Place(GameObject prefab, Transform root, Vector3 at, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        // A prefab variant of 'source' with its renderers on 'material' (null keeps the source's).
        private static GameObject Variant(string source, string name, Material material)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (material != null)
            {
                foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = material;
                    }

                    r.sharedMaterials = mats;
                }
            }

            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PartsFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // The guard's spear: a pack staff (PolygonAdventure SM_Wep_Staff_01) with a faceted iron head.
        private static GameObject Spear()
        {
            var root = new GameObject("L1_Mon_Spear");
            var staff = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntyStudios/PolygonAdventure/Prefabs/Weapons/SM_Wep_Staff_01.prefab"), root.transform);
            staff.transform.localPosition = Vector3.zero;
            L1Build.ApplyAshMaterials(staff, false);
            foreach (var c in staff.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(c);
            }

            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in staff.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    b = r.bounds;
                    first = false;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            var head = new L3Mesh();
            var cell = L4Kit.Cell(L2PolyKit.Edge);
            var tip = new Vector3(0f, b.max.y, 0f);
            L4Shapes.Shaft(head, 0, tip - Vector3.up * 0.12f, tip + Vector3.up * 0.05f, 0.03f, 0.028f, 6, L4Kit.Cell(L2PolyKit.Iron));
            // Leaf blade: a flattened four-sided point.
            var baseC = tip + Vector3.up * 0.05f;
            var apex = tip + Vector3.up * 0.42f;
            var w = new[] { Vector3.right * 0.055f, Vector3.forward * 0.014f, Vector3.left * 0.055f, Vector3.back * 0.014f };
            var shoulder = baseC + Vector3.up * 0.12f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = shoulder + w[i], c = shoulder + w[(i + 1) % 4];
                L4Shapes.Tri(head, 0, a, c, apex, (a + c) * 0.5f - shoulder + Vector3.up * 0.1f, cell);
                L4Shapes.Tri(head, 0, a, c, baseC, (a + c) * 0.5f - shoulder + Vector3.down * 0.1f, cell);
            }

            var headMesh = SaveMesh(head.ToMesh("SpearHead"), "L1_Mon_SpearHead");
            L3Build.MeshChild(root.transform, "Head", headMesh, Vector3.zero, Mat("Iron"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PartsFolder + "/L1_Mon_Spear.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- Lighting

        // Cold window light carved by the openings (shadowed spots outside each lancet, soft shaft cards), the warm
        // dusk pouring in at the main door and a faint warm lead through the inner door. The relic gives none.
        private static void Lighting(Transform root)
        {
            var cold = L1Build.Hex("#A9C2E6");
            var warm = L1Build.Hex("#FF9A4A");
            HallLights(root, cold, L1Build.Hex("#6F86A8"));

            foreach (float z in CorridorBays)
            {
                var window = new Vector3(-CorridorX - 0.5f, 2.3f, z);
                var pool = new Vector3(0.8f, 0f, z - 0.3f);
                Spot(root, "Window", window + (window - pool).normalized * 4.5f, pool, cold, 45f, 14f, 24f, false);
                Shaft(root, "Shaft", new Vector3(-CorridorX + 0.15f, 2.9f, z), pool + Vector3.up * 0.1f, 0.75f, Mat("ShaftCold"));
            }

            {
                float z = (VestibuleZ0 + VestibuleZ1) * 0.5f;
                var window = new Vector3(-VestibuleX - 0.5f, 2.3f, z);
                var pool = new Vector3(-0.3f, 0f, z);
                Spot(root, "Window", window + (window - pool).normalized * 4.5f, pool, cold, 40f, 14f, 26f, false);
            }

            // The way out: dusk through the main door, the brightest thing in the monastery.
            Spot(root, "DuskDoor", new Vector3(0.2f, 2.6f, CorridorZ1 + 6.5f), new Vector3(0f, 0f, CorridorZ1 - 9f), warm, 650f, 30f, 40f, true);
            Point(root, "DuskThreshold", new Vector3(0f, 1.6f, CorridorZ1 + 2.2f), warm, 9f, 7f);
            Point(root, "DuskInside", new Vector3(0f, 1.8f, CorridorZ1 - 2f), warm, 4f, 6f);
            Shaft(root, "DuskShaft", new Vector3(0f, 3.4f, CorridorZ1 + 1.2f), new Vector3(0f, 0.05f, CorridorZ1 - 4.5f), 2.2f, Mat("ShaftWarm"));
            // A faint warm lead through the inner door from the corridor beyond.
            Spot(root, "DuskLead", new Vector3(0f, 2.8f, VestibuleZ1 + 1f), new Vector3(0f, 0f, SanctuaryZ1 - 4.5f), warm, 14f, 13f, 30f, false);

            // Soft cold fill: the night sky's bounce off the walls, so the dark rooms keep their shapes.
            var fill = L1Build.Hex("#6F86A8");
            Point(root, "FillCorridor", new Vector3(-0.5f, 5f, -24f), fill, 2.5f, 11f);
            Point(root, "FillCorridorB", new Vector3(-0.5f, 5f, -15f), fill, 2.5f, 11f);

            // Outside the cut-down camera-side walls the ground lies in the walls' long dusk shadow: a faint cold
            // light keeps it ground rather than a black void at the foot of the frame.
            foreach (float z in new[] { -16f, -27f, -38f, -48f })
            {
                Point(root, "ExteriorFill", new Vector3(z > -30f ? 6.5f : 10.5f, 3f, z), fill, 9f, 12f);
            }

            // The grade inside: colder, darker and duller than the battlefield (blends on Kael's position).
            var volumeGo = new GameObject("#Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 3f;
            volume.blendDistance = 2.5f;
            volume.sharedProfile = Profile();
            foreach (var (c, s) in new[]
            {
                (new Vector3(HallCentre.x, 4f, HallCentre.y), new Vector3(2f * HallApothem, 8f, 2f * HallApothem)),
                (new Vector3(0f, 4f, (VestibuleZ0 + CorridorZ1) * 0.5f), new Vector3(2f * VestibuleX, 8f, CorridorZ1 - VestibuleZ0 - 1.5f)),
            })
            {
                var box = volumeGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = c;
                box.size = s;
            }

            volumeGo.layer = L3Build.Layer("Ignore Raycast");

            // The sanctuary's reflections (the water) come from inside it, not the dusk sky.
            var probeGo = new GameObject("#ReflectionProbe");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.position = new Vector3(HallCentre.x, 2.5f, HallCentre.y);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = new Vector3(2f * HallApothem + 0.5f, 18f, 2f * HallApothem + 0.5f);
            probe.center = new Vector3(0f, 3.5f, 0f);
            probe.importance = 2;
            probe.intensity = 1.5f;
            probe.blendDistance = 0.5f;
            probe.resolution = 256;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = L1Build.Hex("#0B0E13");
            // Once baked (L1MonasteryReflection, with the hall closed up and vaulted), the probe keeps that cubemap.
            var cube = AssetDatabase.LoadAssetAtPath<Texture>(L1MonasteryReflection.CubemapPath);
            if (cube != null)
            {
                probe.mode = ReflectionProbeMode.Custom;
                probe.customBakedTexture = cube;
            }

            // Dust drifting in the window light.
            // No dust motes: specks smaller than a pixel at this camera distance twinkle as they drift.
        }

        private static VolumeProfile Profile()
        {
            string path = L1Folder + "/Lighting/Volumes/L1_Zone_Monastery.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c))
                {
                    c = profile.Add<T>(true);
                    c.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(c, profile);
                }

                return c;
            }

            var color = Get<ColorAdjustments>();
            color.postExposure.Override(0.1f);
            color.saturation.Override(-22f);
            color.contrast.Override(8f);
            color.colorFilter.Override(L1Build.Hex("#D4E0F0"));
            var balance = Get<WhiteBalance>();
            balance.temperature.Override(-14f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Light Spot(Transform root, string name, Vector3 from, Vector3 to, Color color, float intensity, float range, float angle, bool shadows, bool lightsWater = false)
        {
            var go = new GameObject("#" + name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from, Vector3.up));
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = Mathf.Max(range, (to - from).magnitude + 3f);
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 1f;
            light.shadowNearPlane = 0.5f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            var data = go.AddComponent<UniversalAdditionalLightData>();
            if (lightsWater)
            {
                data.renderingLayers = 1u | WaterRenderingLayer;
            }

            return light;
        }

        private static void Point(Transform root, string name, Vector3 at, Color color, float intensity, float range)
        {
            var go = new GameObject("#" + name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            go.AddComponent<UniversalAdditionalLightData>();
        }

        // A soft additive card from top to bottom, turned to face the gameplay camera round its long axis.
        private static void Shaft(Transform root, string name, Vector3 top, Vector3 bottom, float width, Material material)
        {
            var go = new GameObject("#" + name);
            go.transform.SetParent(root, false);
            Vector3 up = (top - bottom).normalized;
            Vector3 toCamera = -(Quaternion.Euler(L1Layout.CameraPitch, L1Layout.CameraYaw, 0f) * Vector3.forward);
            Vector3 normal = Vector3.ProjectOnPlane(toCamera, up).normalized;
            go.transform.position = (top + bottom) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(-normal, up);
            go.transform.localScale = new Vector3(width, (top - bottom).magnitude, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // Slow, sparse dust motes (the L1 ash material, small and pale).
        private static void Motes(Transform root, Vector3 centre, Vector3 size, int count)
        {
            var go = new GameObject("#Motes");
            go.transform.SetParent(root, false);
            go.transform.position = centre;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new Color(0.78f, 0.84f, 0.92f, 0.5f);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = count / 11f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 0.25f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(L1Folder + "/Materials/FX/L1_FX_Ash.mat");
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- Colliders

        // Walls on Obstacles at their seen height; unseen caps on Ignore Raycast over the low camera-side walls (a
        // 0.9 m wall can be jumped), the floors on Ground. Under "Solids" (NotWalkable) and "Floors".
        private static void Colliders(Transform root)
        {
            var solids = new GameObject("Solids").transform;
            solids.SetParent(root, false);
            var modifier = solids.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1;
            var floors = new GameObject("Floors").transform;
            floors.SetParent(root, false);
            var caps = new GameObject("Caps").transform;
            caps.SetParent(root, false);
            var capModifier = caps.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            capModifier.overrideArea = true;
            capModifier.area = 1;

            float w = Wall;
            HallColliders(solids, caps, floors);

            // Vestibule and corridor.
            float vx = VestibuleX, cx = CorridorX;
            Box(solids, "VestibuleFar", -vx - w, -vx, VestibuleWallZ0, VestibuleZ1, CorridorHigh);
            Box(solids, "VestibuleCamera", vx, vx + w, VestibuleWallZ0, VestibuleZ1, Low);
            Cap(caps, vx, vx + w, VestibuleWallZ0, VestibuleZ1);
            Box(solids, "CorridorFar", -cx - w, -cx, VestibuleZ1, CorridorZ1, CorridorHigh);
            Box(solids, "VestibuleShoulderUp", -vx, -cx, VestibuleZ1 - 0.01f, VestibuleZ1 + 0.5f, CorridorHigh);
            Box(solids, "CorridorCamera", cx, cx + w, VestibuleZ1, CorridorZ1, Low);
            Box(solids, "VestibuleShoulderCamera", cx, vx, VestibuleZ1 - 0.01f, VestibuleZ1 + 0.5f, Low);
            Cap(caps, cx, vx + w, VestibuleZ1, CorridorZ1);
            foreach (float z in CorridorPilasters)
            {
                Box(solids, "Pilaster", -cx, -cx + 0.32f, z - 0.3f, z + 0.3f, CorridorHigh);
                Box(solids, "PilasterStump", cx - 0.32f, cx, z - 0.3f, z + 0.3f, Low + 0.2f);
            }

            Stepped(solids, caps, "FacadeUp", -cx - w, -DoorHalf, CorridorZ1, CorridorZ1 + w, FacadeHigh, FacadeStepFrom, FacadeStepTo);
            Stepped(solids, caps, "FacadeCamera", DoorHalf, cx + w, CorridorZ1, CorridorZ1 + w, FacadeHigh, FacadeStepFrom, FacadeStepTo);

            // Floors: top at y 0.
            FloorBox(floors, "InnerThreshold", -DoorHalf, DoorHalf, SanctuaryZ1, VestibuleZ0);
            FloorBox(floors, "Vestibule", -vx, vx, VestibuleZ0, VestibuleZ1);
            FloorBox(floors, "Corridor", -cx, cx, VestibuleZ1, CorridorZ1);
            FloorBox(floors, "OuterThreshold", -DoorHalf, DoorHalf, CorridorZ1, CorridorZ1 + w + 1.7f);
        }

        private static void Box(Transform parent, string name, float x0, float x1, float z0, float z1, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = L3Build.Layer("Obstacles");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((x0 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f);
            box.size = new Vector3(Mathf.Abs(x1 - x0), height, Mathf.Abs(z1 - z0));
        }

        // A wall running along x that steps down towards the camera: solid boxes at its seen height in 0.5 m strips
        // (merged where equal), with an unseen cap wherever it's lower than the cap.
        private static void Stepped(Transform solids, Transform caps, string name, float x0, float x1, float z0, float z1, float high, float from, float to)
        {
            float start = x0;
            float h = StepHeight(x0 + 0.01f, high, from, to);
            for (float x = x0 + 0.25f; ; x += 0.25f)
            {
                bool end = x >= x1 - 1e-3f;
                float next = end ? -1f : StepHeight(x + 0.01f, high, from, to);
                if (end || Mathf.Abs(next - h) > 0.01f)
                {
                    float stop = Mathf.Min(x, x1);
                    Box(solids, name, start, stop, z0, z1, h);
                    if (h < 3f)
                    {
                        Cap(caps, start, stop, z0, z1);
                    }

                    start = stop;
                    h = next;
                }

                if (end)
                {
                    break;
                }
            }
        }

        private static void Cap(Transform parent, float x0, float x1, float z0, float z1)
        {
            var go = new GameObject("Cap");
            go.transform.SetParent(parent, false);
            go.layer = L3Build.Layer("Ignore Raycast");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((x0 + x1) * 0.5f, 3.5f, (z0 + z1) * 0.5f);
            box.size = new Vector3(Mathf.Abs(x1 - x0), 5f, Mathf.Abs(z1 - z0));
        }

        private static void FloorBox(Transform parent, string name, float x0, float x1, float z0, float z1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = L3Build.Layer("Ground");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((x0 + x1) * 0.5f, -0.25f, (z0 + z1) * 0.5f);
            box.size = new Vector3(Mathf.Abs(x1 - x0), 0.5f, Mathf.Abs(z1 - z0));
        }
    }
}
