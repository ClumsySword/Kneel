using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The villagers who didn't get away. Bakes posed PolygonAdventure peasants and shopkeepers into static
    // meshes (same posing as L1Bakers) in a soot-dulled palette, and lays them out where the raid caught them:
    // in doorways, by the well, at the market. Only a couple of the raiders fell here, in the Vanguard's red.
    public static class L2Bakers
    {
        private const string MeshesPath = L2Build.MeshesPath + "/Corpses";
        private const string PrefabsPath = L2Build.PrefabsPath + "/Corpses";
        private const string AdventureCharacters = "Assets/SyntyStudios/PolygonAdventure/Prefabs/Characters/";

        private static readonly (string prefab, string renderer, string label)[] Bodies =
        {
            ("Character_Peasant_Brown", "Character_Peasant", "Peasant"),
            ("Character_Shopkeeper_Brown", "Character_Shopkeeper", "Shopkeeper"),
        };

        private static readonly string[] BodyPoses = { "FaceDown_A", "FaceDown_B", "OnSide", "OnBack", "Slumped" };

        [MenuItem("Kneel/L2/Bake/Commoner Corpses")]
        public static void BakeMenu()
        {
            Debug.Log("[L2] " + Bake());
        }

        public static string Bake()
        {
            L2Build.EnsureFolder(MeshesPath);
            L2Build.EnsureFolder(PrefabsPath);
            var material = CommonerMaterial();
            int made = 0;
            foreach (var body in Bodies)
            {
                foreach (var pose in BodyPoses)
                {
                    var standing = L1Bakers.BakeStanding(AdventureCharacters + body.prefab + ".prefab", body.renderer, L1Bakers.Poses[pose].pose, null, out var rig);
                    Object.DestroyImmediate(rig);
                    LieDown(standing, L1Bakers.Poses[pose].lie, 0.04f);
                    var mesh = SaveMesh(standing, "L2_Corpse_" + body.label + "_" + pose);
                    SavePrefab("L2_Corpse_" + body.label + "_" + pose, mesh, material);
                    made++;
                }
            }

            AssetDatabase.SaveAssets();
            return $"Baked {made} commoner corpses.";
        }

        private static void LieDown(Mesh mesh, Quaternion lie, float sink)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = lie * v[i];
                n[i] = lie * n[i];
            }

            mesh.vertices = v;
            mesh.normals = n;
            mesh.RecalculateBounds();
            var b = mesh.bounds;
            var offset = new Vector3(-b.center.x, -b.min.y - sink, -b.center.z);
            for (int i = 0; i < v.Length; i++)
            {
                v[i] += offset;
            }

            mesh.vertices = v;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = MeshesPath + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetTangents(mesh.tangents);
            existing.SetUVs(0, mesh.uv);
            existing.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                existing.SetTriangles(mesh.GetTriangles(s), s);
            }

            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void SavePrefab(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mats = new Material[mesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = material;
            }

            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, L1Build.PropStatic);
            L1Build.SavePrefab(go, PrefabsPath + "/" + name + ".prefab");
        }

        // Villagers' clothes, dulled by soot: colour mostly gone, a little of it kept so they read as people
        // (not soldiers) against the ash.
        private static Material CommonerMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdventureCharacters + "Character_Peasant_Brown.prefab");
            Material source = null;
            foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name == "Character_Peasant")
                {
                    source = r.sharedMaterial;
                }
            }

            var tex = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? source.GetTexture("_BaseMap") : source.mainTexture;
            string texturePath = AssetDatabase.GetAssetPath(tex);

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(texturePath));
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                var c = Color.HSVToRGB(h, s * 0.4f, Mathf.Min(v * 0.62f, 0.5f));
                c.a = 1f;
                px[i] = c;
            }

            L2Build.EnsureFolder(L2Build.MaterialsPath + "/Textures");
            string outPath = L2Build.MaterialsPath + "/Textures/L2_Commoner.png";
            var outTex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            File.WriteAllBytes(outPath, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(outPath);

            string matPath = L2Build.MaterialsPath + "/L2_Commoner.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(outPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Placement

        // Bodies lie in front of the houses they ran from, by the well and among the stalls. Never inside a fight
        // space, never upright where they could be mistaken for an enemy.
        public static string PlaceDead(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Dead", true);
            var bodies = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab L2_Corpse_", new[] { PrefabsPath }))
            {
                bodies.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            if (bodies.Count == 0)
            {
                return "dead: none baked";
            }

            var rng = new System.Random(88);
            var taken = new List<Vector2>();
            int placed = 0;

            // By the well: the lore beat.
            var well = L2Layout.Markers["Well"];
            foreach (var offset in new[] { new Vector2(2.4f, -0.8f), new Vector2(-1.2f, -2.4f) })
            {
                if (Place(group, bodies, rng, well + offset, taken, false))
                {
                    placed++;
                }
            }

            // In front of the houses.
            foreach (var (lot, _) in L2Greybox.ReadLots(root))
            {
                if (lot.Kind == L2Layout.LotKind.Church || rng.NextDouble() > 0.42)
                {
                    continue;
                }

                Vector3 front = Quaternion.Euler(0f, lot.Yaw, 0f) * Vector3.forward;
                Vector2 p = lot.Center + new Vector2(front.x, front.z) * (lot.Size.y * 0.5f + 1f) + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 2f;
                if (Place(group, bodies, rng, p, taken, true))
                {
                    placed++;
                }

                if (placed >= 34)
                {
                    break;
                }
            }

            // Two raiders the villagers took with them, face down in the street.
            int raiders = 0;
            foreach (var (key, p, yaw) in new[] { ("L1/Corpses/L1_Corpse_Vanguard_Soldier_01_FaceDown_A", new Vector2(-40.5f, -58.8f), 140f), ("L1/Corpses/L1_Corpse_Vanguard_Soldier_02_OnSide", new Vector2(36.8f, -5.5f), 20f) })
            {
                var go = L2Build.Spawn(key, group, Vector3.zero, Vector3.zero);
                go.transform.SetPositionAndRotation(L2Build.Ground(p), Quaternion.Euler(0f, yaw, 0f));
                L1Build.SetLayer(go, "Obstacles");
                raiders++;
            }

            return $"dead: {placed} villagers, {raiders} raiders";
        }

        private static bool Place(Transform group, List<GameObject> bodies, System.Random rng, Vector2 p, List<Vector2> taken, bool allowUpright)
        {
            float sd = L2Layout.SignedDistance(p);
            if (sd < -2.5f || sd > 1.2f || L2Layout.IsInArena(p, 1.5f))
            {
                return false;
            }

            foreach (var t in taken)
            {
                if ((t - p).sqrMagnitude < 4f)
                {
                    return false;
                }
            }

            foreach (var kv in L2Layout.Markers)
            {
                if ((kv.Value - p).sqrMagnitude < 2.5f * 2.5f)
                {
                    return false;
                }
            }

            var prefab = bodies[rng.Next(bodies.Count)];
            if (prefab.name.Contains("Slumped") && (!allowUpright || sd < 0.3f))
            {
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            go.transform.SetPositionAndRotation(L2Build.Ground(p), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            if (!L2Dressing.FitsUnderCamera(go))
            {
                Object.DestroyImmediate(go);
                return false;
            }

            L1Build.SetLayer(go, "Obstacles");
            taken.Add(p);
            return true;
        }
    }
}
