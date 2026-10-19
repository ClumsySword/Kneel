using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Refinement (owner, 2026-10-19): "The start of the level should be a smooth transition from lvl 3 Farmland.
    // Meaning we should see elements of the farmland before the bridge. Make it natural - no hard lines cutoff."
    // The player now starts on a farm track (L4Plan F00) south of the causeway, among L3's fields:
    // - the ground itself turns to L3's patchwork south of the bank (L4Surroundings.Farm / FieldAt), the fields
    //   breaking up into burnt land in ragged tongues toward the ravine;
    // - L3's wheat stands in the wheat fields, thinning and charring toward the burnt land, so no field ends on a
    //   line;
    // - the track fenced with L3's rails (its boundary), broken rails along the field edges, L3's farmhouse, mill,
    //   hay wagon, grain cart, well, orchard and boundary oaks, and a hay rick burning by the bank.
    // Rebuilding replaces only this group.
    public static class L4Farmland
    {
        public const string GroupName = "Farmland";
        private const string L3 = "Assets/Kneel/Prefabs/L3/";
        private const float Track0 = -44f, Track1 = -10.6f;

        [MenuItem("Kneel/L4/Build/Farmland (replaces that group)")]
        public static void BuildMenu()
        {
            var root = L4Level.Root;
            if (root == null)
            {
                Debug.LogWarning("Open L4_BurningGate first.");
                return;
            }

            Debug.Log(Build(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        private static float Ground(float x, float z) => L4Surroundings.Height(new Vector2(x, z));

        private static GameObject Put(Transform parent, string path, float x, float z, float yaw, float scale = 1f, float sink = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L3 + path + ".prefab");
            if (prefab == null)
            {
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, Ground(x, z) - sink, z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            int clumps = Wheat(g);
            int rails = Fences(g);
            int props = Farm(g);
            return GroupName + ": " + clumps + " wheat clumps, " + rails + " rails, " + props + " farm pieces";
        }

        // ---------------------------------------------------------------- Wheat

        // L3's wheat clumps (2.5 m patches of some 6,500 triangles each) a clump apart in the wheat fields beside
        // the track, where the player walks; the wheat fields further out are harvested, low stubble (1,500), so the
        // whole farm stays near a million triangles. Merged into one mesh per 32 m tile and material. Where the
        // farmland gives way (Farm near its edge) the wheat thins out, then stands charred, then is stubble, so the
        // fields fade into the burnt land.
        private static int Wheat(Transform g)
        {
            var parent = L3Build.Child(g, "Wheat").transform;
            var rng = new System.Random(808);
            var tiles = new Dictionary<(int, int, int), List<CombineInstance>>();
            var meshes = new Mesh[3, 3];
            for (int v = 0; v < L3Crops.Variants; v++)
            {
                meshes[0, v] = L3Crops.Standing(v);
                meshes[2, v] = L3Crops.Stubble(v);
            }

            int n = 0;
            for (float x = 0f; x < 125f; x += 2.4f)
            {
                for (float z = -105f; z < -12f; z += 2.4f)
                {
                    var p = new Vector2(x + (float)rng.NextDouble() * 1f, z + (float)rng.NextDouble() * 1f);
                    if (L4Surroundings.FieldAt(p) != L4Surroundings.Field.Wheat || Mathf.Abs(p.x - 60f) > 60f)
                    {
                        continue;
                    }

                    float farm = L4Surroundings.Farm(p);
                    bool beside = Mathf.Abs(p.x - 60f) < 22f && p.y > -54f;
                    if (!beside && rng.NextDouble() < 0.35)
                    {
                        continue;
                    }

                    // 0 standing, 1 charred, 2 stubble.
                    int kind = !beside ? 2 : farm > 0.72f ? 0 : farm > 0.6f ? (rng.NextDouble() < 0.6 ? 1 : 0) : (rng.NextDouble() < 0.5 ? 2 : 1);
                    if (rng.NextDouble() > Mathf.InverseLerp(0.5f, 0.66f, farm) + 0.25f)
                    {
                        continue;
                    }

                    var mesh = meshes[kind == 2 ? 2 : 0, rng.Next(L3Crops.Variants)];
                    if (mesh == null)
                    {
                        continue;
                    }

                    var m = Matrix4x4.TRS(new Vector3(p.x, Ground(p.x, p.y) - 0.05f, p.y), Quaternion.Euler(0f, rng.Next(360), 0f), Vector3.one * (0.85f + (float)rng.NextDouble() * 0.3f));
                    var key = (Mathf.FloorToInt(p.x / 32f), Mathf.FloorToInt(p.y / 32f), kind);
                    if (!tiles.TryGetValue(key, out var list))
                    {
                        tiles[key] = list = new List<CombineInstance>();
                    }

                    list.Add(new CombineInstance { mesh = mesh, transform = m });
                    n++;
                }
            }

            foreach (var pair in tiles)
            {
                var combined = new Mesh { name = "L4_FarmWheat_" + pair.Key.Item1 + "_" + pair.Key.Item2 + "_" + pair.Key.Item3, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                combined.CombineMeshes(pair.Value.ToArray(), true, true);
                var mat = pair.Key.Item3 == 0 ? Dusk(L3Crops.DryMaterial) : pair.Key.Item3 == 1 ? L3Crops.CharredStandingMaterial : Dusk(L3Crops.StubbleMaterial);
                var go = L3Build.MeshChild(parent, combined.name, L4Level.SaveMesh(combined), Vector3.zero, mat);
                go.layer = L3Build.Layer(L3Build.Unlayered);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }

            return n;
        }

        // L3's crop material, an L4 copy toned down for dusk: under L4's grade the dawn-lit wheat burnt out white.
        private static Material Dusk(Material source)
        {
            var mat = L4Build.LoadOrCreate(source.name.Replace("L3_", "L4_Farm"), source.shader);
            mat.CopyPropertiesFromMaterial(source);
            mat.SetColor("_BaseColor", source.GetColor("_BaseColor") * new Color(0.62f, 0.56f, 0.48f, 1f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Fences

        // The track's rails both sides (they are its boundary: rail colliders) and a wagon across its far end; broken
        // and leaning rails along a few field edges.
        private static int Fences(Transform g)
        {
            var parent = L3Build.Child(g, "Fences").transform;
            int n = 0;
            foreach (float x in new[] { 55.5f, 64.5f })
            {
                for (float z = Track0 + 1f; z < Track1; z += 2f)
                {
                    var rail = Put(parent, "Kit/L3_Rail", x, z, 90f);
                    if (rail != null)
                    {
                        rail.transform.position = new Vector3(x, 0f, z);
                        n++;
                    }
                }
            }

            for (float x = 57f; x < 64f; x += 2f)
            {
                var rail = Put(parent, "Kit/L3_Rail", x, Track0 - 0.4f, 0f);
                if (rail != null)
                {
                    rail.transform.position = new Vector3(x, 0f, Track0 - 0.4f);
                    n++;
                }
            }

            var wagon = Put(parent, "Props/L3_HayWagon", 60f, Track0 + 1.6f, 84f);
            if (wagon != null)
            {
                wagon.transform.position = new Vector3(60f, 0f, Track0 + 1.6f);
            }

            // Field edges: runs of rails along the field grid, more broken the nearer the burnt land.
            var rng = new System.Random(812);
            foreach (var (start, dir, length) in new[]
            {
                (new Vector2(66f, -20f), new Vector2(0.21f, -0.98f), 60f),
                (new Vector2(54f, -24f), new Vector2(-0.21f, -0.98f), 56f),
                (new Vector2(68f, -46f), new Vector2(0.98f, 0.21f), 40f),
                (new Vector2(52f, -40f), new Vector2(-0.98f, -0.21f), 44f),
            })
            {
                for (float s = 0f; s < length; s += 2f)
                {
                    var p = start + dir * s;
                    float farm = L4Surroundings.Farm(p);
                    if (rng.NextDouble() > farm || Mathf.Abs(p.x - 60f) < 6f)
                    {
                        continue;
                    }

                    string kind = farm > 0.85f && rng.NextDouble() < 0.75 ? "Kit/L3_Rail" : rng.NextDouble() < 0.5 ? "Kit/L3_Rail_Broken" : "Kit/L3_Rail_Leaning";
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f;
                    if (Put(parent, kind, p.x, p.y, yaw) != null)
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        // ---------------------------------------------------------------- The farm

        private static int Farm(Transform g)
        {
            var parent = L3Build.Child(g, "Farm").transform;
            int n = 0;
            void Add(GameObject go)
            {
                if (go != null)
                {
                    n++;
                }
            }

            // The farmhouse and its well off the track's west side; the mill on the rise beyond; an orchard east.
            Add(Put(parent, "Structures/L3_Farmhouse", 33f, -36f, 78f, 1f, 0.2f));
            Add(Put(parent, "Props/L3_Well", 42.5f, -27f, 20f));
            Add(Put(parent, "Structures/L3_Windmill", -6f, -78f, 160f, 1f, 0.3f));
            Add(Put(parent, "Props/L3_GrainCart", 50.8f, -31f, 170f));
            Add(Put(parent, "Props/L3_HayWagon", 76f, -27f, 30f));
            var rng = new System.Random(816);
            for (int i = 0; i < 9; i++)
            {
                float x = 84f + (i % 3) * 6.5f + (float)rng.NextDouble() * 2f, z = -40f - (i / 3) * 7f + (float)rng.NextDouble() * 2f;
                Add(Put(parent, "Structures/L3_OrchardTree", x, z, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.3f));
            }

            foreach (var (x, z) in new[] { (51.5f, -16f), (68.5f, -38f), (47f, -52f) })
            {
                Add(Put(parent, "Structures/L3_BoundaryOak", x, z, rng.Next(360)));
            }

            // Toward the bank the farm burns: a hay rick alight, and the first of the dead.
            L2Fire.Fire(parent, "BurningRick", new Vector3(71f, Ground(71f, -16f) + 0.2f, -16f), 1.4f, 3.5f, 11f, true, new Vector3(2.4f, 0f, 2.4f));
            foreach (var (x, z, yaw, pose) in new[] { (66.6f, -13.8f, 40f, "Prone"), (52.4f, -12.6f, 200f, "Supine") })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L4Assets.Skeletons + pose + ".prefab");
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.SetPositionAndRotation(new Vector3(x, Ground(x, z), z), Quaternion.Euler(0f, yaw, 0f));
                    n++;
                }
            }

            return n;
        }
    }
}
