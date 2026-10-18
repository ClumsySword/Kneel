using System.Collections.Generic;
using Kneel.Hazards;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.EditorTools
{
    // Builds L3_AssetReview: every manifest prefab laid out in rows with its ID, name and status, plus a test
    // lane where the hazards run as they will be used. It doubles as the metrics map (1 R bars, the hound run,
    // the brute's ring). The scene is generated: rebuild it rather than editing it by hand.
    public static class L3Review
    {
        public const string RootName = "L3_AssetReview";
        public const string DummyPath = L3Build.PrefabsPath + "/Review/L3_HazardTestDummy.prefab";
        private const string SceneFolder = L3Build.ScenesPath + "/L3_AssetReview";

        // Rows wrap at this width. Fifteen props at 6 m do not fit the note's 80 m floor, so the floor is
        // sized to what it has to hold.
        private const float Width = 96f;
        private const float Spacing = 6f;
        private const float LaneDepth = 48f;

        // Layout footprints (x, z) where they differ from the manifest row.
        private static readonly Dictionary<string, Vector2> Footprints = new Dictionary<string, Vector2>
        {
            { "H02", new Vector2(2f, 24f) }, { "H04", new Vector2(8f, 4f) }, { "S05", new Vector2(9f, 9f) },
            { "M01", new Vector2(5f, 5f) }, { "E03", new Vector2(8f, 8f) }, { "S04", new Vector2(10f, 8f) },
        };

        [MenuItem("Kneel/L3/Build/Asset Review Scene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "Stop play mode first.";
            }

            L2Build.EnsureFolder(L3Build.ScenesPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            DummyPrefab();

            var root = new GameObject(RootName).transform;
            var rows = L3Build.Child(root, "Rows").transform;
            var labels = L3Build.Child(root, "Labels").transform;

            // ---- The rows, south to north, in table order.
            float z = 2f;
            string table = null;
            float x = 0f, rowDepth = 0f, previousHalf = 0f;
            Transform group = null;
            int placed = 0;
            foreach (var item in L3Assets.Items)
            {
                Vector2 footprint = Footprints.TryGetValue(item.Id, out var custom) ? custom : new Vector2(Mathf.Max(item.Size.x, 0.5f), Mathf.Max(item.Size.z, 0.5f));
                float half = footprint.x * 0.5f;
                bool newTable = item.Table != table;
                float next = x + Mathf.Max(Spacing, previousHalf + half + 2f);
                if (newTable || next + half > Width - 2f)
                {
                    z += newTable && table == null ? 0f : rowDepth + 5f;
                    rowDepth = 0f;
                    if (newTable)
                    {
                        table = item.Table;
                        group = L3Build.Child(rows, table).transform;
                        Label(labels, "Header " + table, table, new Vector3(1f, 0.03f, z - 0.6f), 7f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f), 40f);
                    }

                    ScaleReference(group, labels, z);
                    next = 10f + half;
                }

                x = next;
                previousHalf = half;
                rowDepth = Mathf.Max(rowDepth, footprint.y);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.Path);
                // H02's pivot is its south end; everything else is centred on its footprint.
                float pivotZ = item.Id == "H02" ? z + 2f : z + 2f + footprint.y * 0.5f;
                var instance = L3Build.Instance(prefab, group, new Vector3(x, 0f, pivotZ));
                if (item.Id == "E01")
                {
                    MakeInert(instance);
                }

                Label(labels, "Label " + item.Id, item.Id + "\n" + item.Name + "\n" + item.Status, new Vector3(x, 0.03f, z + 0.4f), 3.4f, TextAlignmentOptions.Center, Color.white, 5.6f);
                placed++;
            }

            float rowsEnd = z + rowDepth + 6f;

            // ---- The test lane.
            float z0 = Mathf.Ceil(rowsEnd / 2f) * 2f;
            var lane = L3Build.Child(root, "TestLane").transform;
            Label(labels, "Header Test Lane", "Hazard test lane", new Vector3(1f, 0.03f, z0 - 0.6f), 7f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f), 40f);
            ScaleReference(lane, labels, z0);

            var rowPrefab = L3Assets.Load("H02");
            var stakePrefab = L3Assets.Load("H03");
            for (int i = 0; i < 2; i++)
            {
                float rx = 14f + 10f * i;
                var row = L3Build.Instance(rowPrefab, lane, new Vector3(rx, 0f, z0 + 6f));
                row.name = i == 0 ? "CropRow_West" : "CropRow_East";
                var stake = L3Build.Instance(stakePrefab, lane, new Vector3(rx, 0f, z0 + 5f));
                stake.name = i == 0 ? "BrandStake_West" : "BrandStake_East";
                L3Build.Set(stake.GetComponent<BrandStake>(), "row", row.GetComponent<CropRow>());
            }

            Label(labels, "Lane Crop", "Two L3_CropRow_12, 8 m lane between.\nHit a brand stake (or use its Knock button).", new Vector3(19f, 0.03f, z0 + 3.2f), 3f, TextAlignmentOptions.Center, Color.white, 16f);

            var mud = L3Build.Instance(L3Assets.Load("H04"), lane, new Vector3(38f, 0f, z0 + 12f));
            mud.GetComponent<SurfaceVolume>().Configure(new Vector2(8f, 8f), true, true, false, false);
            Label(labels, "Lane Mud", "L3_MudVolume 8 x 8\nwith damp bands", new Vector3(38f, 0.03f, z0 + 6.4f), 3f, TextAlignmentOptions.Center, Color.white, 12f);

            L3Build.Instance(L3Assets.Load("H08"), lane, new Vector3(80f, 0f, z0 + 8f));
            L3Build.Instance(L3Assets.Load("H07"), lane, new Vector3(86f, 0f, z0 + 8f));
            Label(labels, "Lane Gibbets", "Crowed (wakes within 8 m)        Inert", new Vector3(83f, 0.03f, z0 + 5.6f), 3f, TextAlignmentOptions.Center, Color.white, 14f);

            L3Build.Instance(L3Assets.Load("E03"), lane, new Vector3(86f, 0f, z0 + 22f));
            Label(labels, "Lane Brute", "Brute stand-in, ring radius 4 m (2 R)", new Vector3(86f, 0.03f, z0 + 16.8f), 3f, TextAlignmentOptions.Center, Color.white, 14f);

            // The hound run: 14 m (7 R), which a hound at 7 m/s covers in 2 s.
            const float runWest = 48f, runEast = 62f;
            float runZ = z0 + 26f;
            L3Build.Block(lane, "HoundRun_Line", new Vector3(14f, 0.02f, 0.15f), new Vector3((runWest + runEast) * 0.5f, 0.01f, runZ), L3Build.Mat("L3_GB_Lane"));
            L3Build.Block(lane, "HoundRun_Start", new Vector3(0.2f, 1.2f, 0.2f), new Vector3(runWest, 0f, runZ + 0.8f), L3Build.Mat("L3_GB_MarkerSolid"));
            L3Build.Block(lane, "HoundRun_End", new Vector3(0.2f, 1.2f, 0.2f), new Vector3(runEast, 0f, runZ + 0.8f), L3Build.Mat("L3_GB_MarkerSolid"));
            L3Build.Instance(L3Assets.Load("E02"), lane, new Vector3(runWest, 0f, runZ), 90f);
            Label(labels, "Lane Run", "14 m straight (7 R): a hound at 7 m/s covers it in 2.0 s", new Vector3((runWest + runEast) * 0.5f, 0.03f, runZ - 1.4f), 3f, TextAlignmentOptions.Center, Color.white, 22f);

            // The ditch: 6 m wide, 3 m deep, crossed by the bridge and the footway.
            const float ditchWest = 4f, ditchEast = 36f;
            float ditchSouth = z0 + 34f, ditchNorth = z0 + 40f, ditchZ = z0 + 37f;
            var ditch = L3Build.Child(lane, "Ditch").transform;
            var bed = L3Build.Block(ditch, "Bed", new Vector3(ditchEast - ditchWest, 0.5f, 6f), new Vector3((ditchWest + ditchEast) * 0.5f, -3.5f, ditchZ), L3Build.Mat("L3_GB_Water"));
            bed.layer = L3Build.Layer(L3Build.Walkable);
            bed.AddComponent<BoxCollider>();
            Bank(ditch, "Bank_South", new Vector3(ditchEast - ditchWest, 3f, 0.3f), new Vector3((ditchWest + ditchEast) * 0.5f, -3f, ditchSouth - 0.15f));
            Bank(ditch, "Bank_North", new Vector3(ditchEast - ditchWest, 3f, 0.3f), new Vector3((ditchWest + ditchEast) * 0.5f, -3f, ditchNorth + 0.15f));
            Bank(ditch, "Bank_West", new Vector3(0.3f, 3f, 6.6f), new Vector3(ditchWest - 0.15f, -3f, ditchZ));
            Bank(ditch, "Bank_East", new Vector3(0.3f, 3f, 6.6f), new Vector3(ditchEast + 0.15f, -3f, ditchZ));
            var kill = L3Build.Instance(L3Assets.Load("H06"), ditch, new Vector3((ditchWest + ditchEast) * 0.5f, -3f, ditchZ));
            L3Assets.SizeVolume(kill, new Vector3(ditchEast - ditchWest, 2.5f, 6f));

            L3Build.Instance(L3Assets.Load("S01"), lane, new Vector3(12f, 0f, ditchZ));
            L3Build.Instance(L3Assets.Load("S02"), lane, new Vector3(27f, 0f, ditchZ));
            L3Build.Instance(L3Assets.Load("M02"), lane, new Vector3(27f, 0f, ditchZ + 5f));
            Label(labels, "Lane Ditch", "Ditch 6 m wide, 3 m deep, with L3_DitchKill. L3_StoneBridge and L3_SluiceFootway cross it; the gate opens from the north (+Z) side only.",
                new Vector3(20f, 0.03f, ditchSouth - 6.6f), 3f, TextAlignmentOptions.Center, Color.white, 30f);

            // Test dummies, one per kind of contact.
            Dummy(lane, labels, "Dummy_Hound_InRow", HazardTestDummy.ActorKind.Hound, new Vector3(14f, 0f, z0 + 15f));
            Dummy(lane, labels, "Dummy_Footman_InRow", HazardTestDummy.ActorKind.Footman, new Vector3(24f, 0f, z0 + 9f));
            Dummy(lane, labels, "Dummy_Player_InLane", HazardTestDummy.ActorKind.Player, new Vector3(19f, 0f, z0 + 15f));
            Dummy(lane, labels, "Dummy_Player_InMud", HazardTestDummy.ActorKind.Player, new Vector3(38f, 0f, z0 + 12f));
            Dummy(lane, labels, "Dummy_Footman_InDitch", HazardTestDummy.ActorKind.Footman, new Vector3(20f, -3f, ditchZ));

            // ---- The floor, with the ditch cut out of it.
            float depth = z0 + LaneDepth;
            var floor = L3Build.Child(root, "Floor").transform;
            FloorPiece(floor, "Floor_South", 0f, Width, 0f, ditchSouth);
            FloorPiece(floor, "Floor_DitchWest", 0f, ditchWest, ditchSouth, ditchNorth);
            FloorPiece(floor, "Floor_DitchEast", ditchEast, Width, ditchSouth, ditchNorth);
            FloorPiece(floor, "Floor_North", 0f, Width, ditchNorth, depth);

            // ---- The player, the player's own camera, and the level's light.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(19f, 0.05f, z0 + 1.5f);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 300f;
            camera.AddComponent<AudioListener>();
            var playerCamera = camera.AddComponent<PlayerCamera>();
            L3Build.Set(playerCamera, "settings", AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/Player/Settings/PlayerCameraSettings.asset"));
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 8.7f, -3.6f), Quaternion.Euler(65f, 0f, 0f));
            // As on the L2 camera: fades scenery that hides the player (the farmhouse roof and south wall).
            camera.AddComponent<Kneel.OcclusionFader>();

            // The same light and grade the level will have, so every asset is judged in the world it belongs to.
            Kneel.Lighting.EditorTools.L3LightingPass.Apply(root);

            // ---- A NavMesh for the review floor, so fire and the gate can be seen carving it.
            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << L3Build.Layer(L3Build.Walkable)) | (1 << L3Build.Layer(L3Build.Boundary));
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            L2Build.EnsureFolder(SceneFolder);
            string navPath = SceneFolder + "/NavMesh-L3_AssetReview.asset";
            AssetDatabase.DeleteAsset(navPath);
            AssetDatabase.CreateAsset(surface.navMeshData, navPath);

            EditorSceneManager.SaveScene(scene, L3Build.ReviewScenePath);
            AssetDatabase.SaveAssets();
            return "L3_AssetReview built: " + placed + " manifest items in rows, floor " + Width + " x " + depth + " m, test lane from z = " + z0 + ".";
        }

        private static void Bank(Transform parent, string name, Vector3 size, Vector3 baseCentre)
        {
            var bank = L3Build.Block(parent, name, size, baseCentre, L3Build.Mat("L3_GB_Bank"));
            bank.layer = L3Build.Layer(L3Build.Boundary);
            bank.AddComponent<BoxCollider>();
        }

        // A 1.8 m capsule for player scale and a 2 m bar marked "1 R", at the start of a row.
        private static void ScaleReference(Transform parent, Transform labels, float z)
        {
            L3Build.Prim(parent, "Scale_Player_1.8m", PrimitiveType.Capsule, new Vector3(2f, 0.9f, z + 3f), Vector3.zero, new Vector3(0.6f, 0.9f, 0.6f), L3Build.Mat("L3_GB_Cover"));
            L3Build.Block(parent, "Scale_1R_2m", new Vector3(2f, 0.12f, 0.25f), new Vector3(5f, 0f, z + 3f), L3Build.Mat("L3_GB_MarkerSolid"));
            Label(labels, "Label 1R", "1 R", new Vector3(5f, 0.03f, z + 2.2f), 3.4f, TextAlignmentOptions.Center, Color.white, 3f);
        }

        // Text lying flat on the floor, read from the gameplay camera (up-screen is +Z).
        private static void Label(Transform parent, string name, string text, Vector3 position, float size, TextAlignmentOptions alignment, Color colour, float width)
        {
            var go = L3Build.Child(parent, name, position, new Vector3(90f, 0f, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = colour;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = new Vector2(width, 2f);
            // Left-aligned headers start at the given point; centred labels are centred on it.
            tmp.rectTransform.pivot = alignment == TextAlignmentOptions.Left ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The footman is shown, not run: without its AI and agent it neither hunts the player nor complains
        // about the NavMesh. (The instance is otherwise the project's own prefab.)
        private static void MakeInert(GameObject footman)
        {
            foreach (var behaviour in footman.GetComponents<Behaviour>())
            {
                if (!(behaviour is Animator))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void Dummy(Transform parent, Transform labels, string name, HazardTestDummy.ActorKind kind, Vector3 position)
        {
            var dummy = L3Build.Instance(AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath), parent, position);
            dummy.name = name;
            L3Build.Set(dummy.GetComponent<HazardTestDummy>(), "actsAs", kind);
            var tag = dummy.GetComponentInChildren<TextMeshPro>();
            tag.text = "dummy: " + kind;
        }

        // A capsule that implements IHazardTarget and logs every contact.
        private static void DummyPrefab()
        {
            var root = new GameObject("L3_HazardTestDummy");
            root.layer = L3Build.Layer(L3Build.Hittable);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.4f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            var body = L3Build.Prim(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(0.8f, 0.9f, 0.8f), L3Build.Mat("L3_GB_MarkerSolid"));
            body.layer = root.layer;
            root.AddComponent<HazardTestDummy>();

            var tag = L3Build.Child(root.transform, "Tag", new Vector3(0f, 0.03f, -0.9f), new Vector3(90f, 0f, 0f)).AddComponent<TextMeshPro>();
            tag.text = "dummy";
            tag.fontSize = 2.6f;
            tag.alignment = TextAlignmentOptions.Center;
            tag.color = new Color(1f, 0.6f, 1f);
            tag.rectTransform.sizeDelta = new Vector2(5f, 1f);
            tag.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            L2Build.EnsureFolder(L3Build.PrefabsPath + "/Review");
            PrefabUtility.SaveAsPrefabAsset(root, DummyPath);
            Object.DestroyImmediate(root);
        }

        // A 0.5 m slab with its top at Y = 0 and world-space UVs on top, so the grid has one cell per 2 m.
        private static void FloorPiece(Transform parent, string name, float west, float east, float south, float north)
        {
            var go = L3Build.Child(parent, name, new Vector3((west + east) * 0.5f, 0f, (south + north) * 0.5f));
            go.layer = L3Build.Layer(L3Build.Walkable);
            float hx = (east - west) * 0.5f, hz = (north - south) * 0.5f;
            const float thickness = 0.5f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    Vector3 world = go.transform.position + v;
                    uvs.Add(new Vector2((world.x + world.y) * 0.5f, world.z * 0.5f));
                }

                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            Vector3 P(float sx, float y, float sz)
            {
                return new Vector3(sx * hx, y, sz * hz);
            }

            Quad(P(-1, 0, -1), P(-1, 0, 1), P(1, 0, 1), P(1, 0, -1));
            Quad(P(-1, -thickness, -1), P(-1, 0, -1), P(1, 0, -1), P(1, -thickness, -1));
            Quad(P(1, -thickness, 1), P(1, 0, 1), P(-1, 0, 1), P(-1, -thickness, 1));
            Quad(P(-1, -thickness, 1), P(-1, 0, 1), P(-1, 0, -1), P(-1, -thickness, -1));
            Quad(P(1, -thickness, -1), P(1, 0, -1), P(1, 0, 1), P(1, -thickness, 1));

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            L2Build.EnsureFolder(L3Build.MeshesPath + "/Review");
            string path = L3Build.MeshesPath + "/Review/L3_" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = L3Build.Mat("L3_Review_Grid");
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(hx * 2f, thickness, hz * 2f);
            box.center = new Vector3(0f, -thickness * 0.5f, 0f);
        }
    }
}
