using System.Collections.Generic;
using Kneel.Hazards;
using Kneel.Lighting.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Lays every prefab of the L4 manifest out in L4_AssetReview for the owner: one row per table, south to north,
    // each item with its ID, name and status on the floor in front of it and a 1.8 m figure and a 1 R bar at the
    // start of each row. A hazard test lane to the west wires the hazards together as the level will (lights to
    // slicks, the gate to its murder holes, the winch to its gate, the twins as partners, the tender to the rings)
    // with test dummies in harm's way. The look-dev corner (LookDev) is left as it is.
    public static class L4Review
    {
        public const string RowsName = "Review";
        public const string LaneName = "TestLane";

        [MenuItem("Kneel/L4/Build/Asset Review Scene")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "Leave Play mode first.";
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != L4Build.ReviewScenePath)
            {
                scene = EditorSceneManager.OpenScene(L4Build.ReviewScenePath, OpenSceneMode.Single);
            }

            var root = GameObject.Find(L4Build.ReviewRootName);
            foreach (var temp in new[] { "__KitPreview", "__PropPreview", "__PackSheet" })
            {
                var t = root.transform.Find(temp);
                if (t != null)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            var rows = L2Build.Group(root.transform, RowsName, true);
            var labels = L3Build.Child(rows, "Labels").transform;
            int placed = 0;
            float z = -22f;
            string[] order = { "14.2 Wall kit", "14.3 Cover and props", "14.3 Figures", "14.4 Hazards", "14.6 Markers", "14.7 Enemies", "14.5 Structures" };
            float maxX = 0f;
            foreach (var table in order)
            {
                var items = L4Assets.Items.FindAll(i => i.Table == table && (i.Build != null || i.ExistingPath != null));
                float depth = 0f;
                foreach (var item in items)
                {
                    depth = Mathf.Max(depth, Footprint(item).y);
                }

                float rowZ = z - depth * 0.5f - 3f;
                Label(labels, "Header " + table, table, new Vector3(-6f, 0.03f, rowZ + depth * 0.5f + 2.2f), 7f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f), 40f);
                ScaleReference(rows, labels, new Vector3(-4f, 0f, rowZ));
                float x = 2f;
                float gap = table == "14.5 Structures" ? 8f : 3f;
                foreach (var item in items)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.Path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    var fp = Footprint(item);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rows);
                    float lift = item.Id.StartsWith("F03") ? 2.6f : 0f;
                    go.transform.localPosition = new Vector3(x + fp.x * 0.5f, lift, rowZ);
                    if (item.Id == "N01")
                    {
                        MakeInert(go);
                    }

                    Label(labels, "Label " + item.Id, item.Id + "\n" + item.Name + "\n" + item.Status, new Vector3(x + fp.x * 0.5f, 0.03f, rowZ - fp.y * 0.5f - 1.3f), 2.6f, TextAlignmentOptions.Center, Color.white, Mathf.Max(fp.x, 4.5f));
                    x += fp.x + gap;
                    placed++;
                }

                maxX = Mathf.Max(maxX, x);
                z = rowZ - depth * 0.5f - 6f;
            }

            Floor(rows, new Vector2(-12f, maxX + 6f), new Vector2(z - 4f, -18f));
            string lane = TestLane(root.transform);
            string lighting = L4LightingPass.Apply(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "L4 review: " + placed + " prefabs in " + order.Length + " rows; " + lane + "; " + lighting;
        }

        // x by z, from the manifest row (structures and big pieces by their true size).
        private static Vector2 Footprint(L4Assets.Item item)
        {
            return new Vector2(Mathf.Max(item.Size.x, 1.2f), Mathf.Max(item.Size.z, 1.2f));
        }

        private static void Floor(Transform parent, Vector2 xRange, Vector2 zRange)
        {
            var quad = L3Build.MeshChild(parent, "Floor", L4LookDev.FlatQuad(), new Vector3((xRange.x + xRange.y) * 0.5f, 0f, (zRange.x + zRange.y) * 0.5f), L4Build.Mat("L4_Ground"));
            quad.transform.localScale = new Vector3(xRange.y - xRange.x, 1f, zRange.y - zRange.x);
            L3Build.SetLayer(quad, L3Build.Walkable);
            var col = quad.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 0.02f, 1f);
            col.center = new Vector3(0f, -0.01f, 0f);
        }

        private static void ScaleReference(Transform parent, Transform labels, Vector3 at)
        {
            var figure = L3Build.Prim(parent, "Scale 1.8 m", PrimitiveType.Capsule, at + new Vector3(0f, 0.9f, 0f), Vector3.zero, new Vector3(0.5f, 0.9f, 0.5f), L4Build.Flat("L4_ReviewFigure", "#B8B2A8"));
            var bar = L3Build.Prim(parent, "Scale 1 R", PrimitiveType.Cube, at + new Vector3(0f, 0.05f, -1.4f), Vector3.zero, new Vector3(2f, 0.1f, 0.2f), L4Build.Flat("L4_ReviewBar", "#E0A040"));
            Label(labels, "Label 1R", "1 R", at + new Vector3(0f, 0.03f, -2.1f), 2.4f, TextAlignmentOptions.Center, Color.white, 3f);
            figure.name = "Scale 1.8 m";
            bar.name = "Scale 1 R";
        }

        private static void Label(Transform parent, string name, string text, Vector3 position, float size, TextAlignmentOptions alignment, Color colour, float width)
        {
            var go = L3Build.Child(parent, name, position, new Vector3(90f, 0f, 0f));
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = colour;
            tmp.rectTransform.sizeDelta = new Vector2(width, size * 1.6f);
        }

        private static void MakeInert(GameObject footman)
        {
            foreach (var behaviour in footman.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour.GetType().Namespace == null || behaviour.GetType().Name.Contains("Footman") || behaviour.GetType().Name.Contains("Enemy"))
                {
                    behaviour.enabled = false;
                }
            }

            var agent = footman.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        // ---------------------------------------------------------------- The hazard test lane

        public static readonly Vector3 LaneOrigin = new Vector3(-110f, 0f, -30f);

        private static GameObject Put(Transform parent, string id, Vector3 local, float yaw = 0f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(L4Assets.Load(id), parent);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static HazardTestDummy Dummy(Transform parent, Transform labels, string name, HazardTestDummy.ActorKind kind, Vector3 local)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Prefabs/L3/Review/L3_HazardTestDummy.prefab"), parent);
            go.name = name;
            go.transform.localPosition = local;
            var dummy = go.GetComponent<HazardTestDummy>();
            dummy.ActsAs = kind;
            var tag = go.GetComponentInChildren<TextMeshPro>();
            if (tag != null)
            {
                tag.text = name;
            }

            return dummy;
        }

        private static string TestLane(Transform root)
        {
            var lane = L2Build.Group(root, LaneName, true);
            lane.localPosition = LaneOrigin;
            var labels = L3Build.Child(lane, "Labels").transform;
            Label(labels, "Header Lane", "Hazard test lane", new Vector3(-30f, 0.03f, 34f), 7f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.4f), 40f);
            Floor(lane, new Vector2(-36f, 36f), new Vector2(-34f, 32f));

            // 1. Murder holes: three rings in sequence under a curtain with machicolations, the pour-tender on top.
            for (int i = 0; i < 3; i++)
            {
                Put(lane, "W01a", new Vector3(-24f + i * 6f, 0f, 22f));
                Put(lane, "W10", new Vector3(-24f + i * 6f, 5f, 21f));
            }

            var rings = new List<MurderHole>();
            for (int i = 0; i < 3; i++)
            {
                var ring = Put(lane, "H01", new Vector3(-24f + i * 6f, 0f, 17f)).GetComponent<MurderHole>();
                L3Build.Set(ring, "order", i);
                L3Build.Set(ring, "ringsInLoop", 3);
                rings.Add(ring);
            }

            var trough = Put(lane, "H10", new Vector3(-18f, 5f, 23.6f), 180f);
            L3Build.SetArray(trough.GetComponent<PourTender>(), "holes", rings.ConvertAll(r => (Object)r).ToArray());
            Dummy(lane, labels, "Dummy_Pour", HazardTestDummy.ActorKind.Player, new Vector3(-24f, 0f, 17f));
            Label(labels, "Lane Pours", "Murder holes (12 s loop, 4 s apart). Hit the pour-tender on the wall to stop them.", new Vector3(-18f, 0.03f, 12.5f), 2.4f, TextAlignmentOptions.Center, Color.white, 22f);

            // 2. Oil: a cresset over one slick, a brazier beside another.
            var slickA = Put(lane, "H02", new Vector3(-24f, 0f, 2f)).GetComponent<OilSlick>();
            var cresset = Put(lane, "H03", new Vector3(-24f, 0f, -0.4f));
            L3Build.Set(cresset.GetComponent<HangingLight>(), "slick", slickA);
            var slickB = Put(lane, "H02", new Vector3(-12f, 0f, 2f)).GetComponent<OilSlick>();
            var brazier = Put(lane, "H03c", new Vector3(-12f, 0f, -0.6f));
            L3Build.Set(brazier.GetComponent<HangingLight>(), "slick", slickB);
            Dummy(lane, labels, "Dummy_Oil", HazardTestDummy.ActorKind.Player, new Vector3(-24f, 0f, 2.5f));
            Dummy(lane, labels, "Dummy_OilHound", HazardTestDummy.ActorKind.Hound, new Vector3(-12f, 0f, 2.5f));
            Label(labels, "Lane Oil", "Strike the post or the brazier: 1.8 s swing, then the slick burns 6 s.", new Vector3(-18f, 0.03f, -3.5f), 2.4f, TextAlignmentOptions.Center, Color.white, 22f);

            // 3. The gate passage: the cycling portcullis with its two murder holes.
            var gate = Put(lane, "H04a", new Vector3(12f, 0f, 22f)).GetComponent<PortcullisCycle>();
            var holeA = Put(lane, "H01", new Vector3(12f, 0f, 17.5f)).GetComponent<MurderHole>();
            L3Build.Set(holeA, "schedule", MurderHole.Schedule.External);
            L3Build.SetArray(gate, "downPhasePours", new Object[] { holeA });
            Dummy(lane, labels, "Dummy_Slot", HazardTestDummy.ActorKind.Brute, new Vector3(12f, 0f, 22f));
            Label(labels, "Lane Gate", "Portcullis: up 6, rattle 1.5, drop 0.3, down 4, rise 2.5. The slot kills.", new Vector3(12f, 0.03f, 13.5f), 2.4f, TextAlignmentOptions.Center, Color.white, 20f);

            // 4. The lane gate and its winch; the postern and the key grille.
            var laneGate = Put(lane, "H04c", new Vector3(28f, 0f, 22f)).GetComponent<PortcullisCycle>();
            var winch = Put(lane, "H05", new Vector3(24f, 0f, 18f)).GetComponent<Winch>();
            L3Build.Set(winch, "gate", laneGate);
            Put(lane, "M06a", new Vector3(22f, 0f, 6f));
            Put(lane, "M06b", new Vector3(30f, 0f, 6f));
            Label(labels, "Lane Gates", "Winch [E] raises the lane gate.  Postern: opens from +Z only.  Grille: needs the tribunal key.", new Vector3(26f, 0.03f, 2.5f), 2.4f, TextAlignmentOptions.Center, Color.white, 22f);

            // 5. The scaffold demo with its trigger, dummies under it and on it.
            Put(lane, "H06b", new Vector3(10f, 0f, -12f));
            Dummy(lane, labels, "Dummy_Under", HazardTestDummy.ActorKind.Footman, new Vector3(10f, 0f, -14.5f));
            Label(labels, "Lane Scaffold", "Walk into the trigger (south) or strike the strut: creak 1.5 s, then it falls.", new Vector3(10f, 0.03f, -30f), 2.4f, TextAlignmentOptions.Center, Color.white, 22f);

            // 6. The kneelers: a Twin pair (partners) and a footman kneeler.
            var twinA = Put(lane, "H08b", new Vector3(-26f, 0f, -16f)).GetComponent<KneelerAmbush>();
            var twinB = Put(lane, "H08b", new Vector3(-22f, 0f, -17f)).GetComponent<KneelerAmbush>();
            L3Build.SetArray(twinA, "partners", new Object[] { twinB });
            L3Build.SetArray(twinB, "partners", new Object[] { twinA });
            Put(lane, "H08a", new Vector3(-14f, 0f, -16f));
            Label(labels, "Lane Kneelers", "Twins (strike one, both rise) and a footman kneeler. Wake within 3 R.", new Vector3(-20f, 0.03f, -21f), 2.4f, TextAlignmentOptions.Center, Color.white, 22f);

            // 7. The breach: three segments of edge on a raised strip with the drop beyond.
            for (int i = 0; i < 3; i++)
            {
                Put(lane, "W06", new Vector3(26f + i * 2f, 0f, -10f));
            }

            Label(labels, "Lane Breach", "Breach edge: lip at the north edge of the strip, buffer inward, kill volume beyond.", new Vector3(28f, 0.03f, -16f), 2.4f, TextAlignmentOptions.Center, Color.white, 20f);

            var reset = L3Build.Child(lane, "LevelReset").AddComponent<LevelReset>();
            reset.name = "LevelReset";

            // The player stands at the lane's south end for a manual try, followed by the project's own gameplay
            // camera (used as it is, with its settings asset).
            var player = GameObject.Find("Player");
            if (player != null)
            {
                player.transform.position = LaneOrigin + new Vector3(0f, 0f, -4f);
            }

            var cam = Camera.main;
            if (cam != null && cam.GetComponent<PlayerCamera>() == null)
            {
                var follow = cam.gameObject.AddComponent<PlayerCamera>();
                L3Build.Set(follow, "settings", AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/Player/Settings/PlayerCameraSettings.asset"));
            }

            return "test lane wired (3 rings + tender, 2 lights over slicks, gate with its pour, winch gate, postern, grille, scaffold demo, twins, breach)";
        }
    }
}
