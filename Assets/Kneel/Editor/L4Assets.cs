using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Builds every prefab in the L4 asset manifest (L4_AssetReport.md, "Asset manifest"). Each row is one
    // manifest ID: footprint and collider from the manifest, looks from the packs and the L4 kit. Prefab frame:
    // the footprint is centred on x and z, the base at y 0. For wall pieces -Z is the face toward the
    // playfield (the camera side for an up-screen wall) and +Z the far side.
    public static partial class L4Assets
    {
        public enum Col
        {
            Box,
            Cylinder,
            None,
            Custom,
        }

        public class Item
        {
            public string Id, Name, Folder, Table, Layer, Status, Source, Notes;

            // x, height, z.
            public Vector3 Size;
            public Col Collider;
            public Action<GameObject> Build;
            public string ExistingPath;

            public string Path => ExistingPath ?? L4Build.PrefabsPath + "/" + Folder + "/" + Name + ".prefab";
        }

        private const float Tier = 5f;
        private static readonly Vector2 Centre = Vector2.zero;
        private static List<Item> items;

        public static List<Item> Items => items ?? (items = Table());

        public static Item Find(string id)
        {
            return Items.Find(i => i.Id == id);
        }

        public static GameObject Load(string id)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Find(id).Path);
        }

        private static Material Stone => L4Build.Mat("L4_Knights_Stone");
        private static Material Soot => L4Build.Mat("L4_Knights_Soot");
        private static Material AdvStone => L4Build.Mat("L4_Adventure_Stone");
        private static Material AdvSoot => L4Build.Mat("L4_Adventure_Soot");

        [MenuItem("Kneel/L4/Build/Asset Prefabs")]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        public static string BuildAll(string prefix = "")
        {
            items = null;
            L4Look.Materials();
            L4Kit.BuildStonePalette();
            L4Shapes.BuildPropsPalette();
            EnsureProjectSettings();
            int built = 0;
            var failed = new List<string>();
            foreach (var item in Items)
            {
                if (item.Build == null || !item.Id.StartsWith(prefix))
                {
                    continue;
                }

                try
                {
                    BuildItem(item);
                    built++;
                }
                catch (Exception e)
                {
                    failed.Add(item.Id + ": " + e.Message);
                    Debug.LogException(e);
                }
            }

            AssetDatabase.SaveAssets();
            return "L4 assets: built " + built + " prefabs under " + L4Build.PrefabsPath + (failed.Count > 0 ? "; FAILED " + string.Join(" | ", failed) : "");
        }

        public static void BuildOne(string id)
        {
            BuildItem(Find(id));
            AssetDatabase.SaveAssets();
        }

        private static void BuildItem(Item item)
        {
            var root = new GameObject(item.Name);
            try
            {
                root.layer = L3Build.Layer(item.Layer);
                switch (item.Collider)
                {
                    case Col.Box:
                        L3Build.StandingBox(root, item.Size);
                        break;
                    case Col.Cylinder:
                        L3Build.StandingCylinder(root, item.Size.x, item.Size.y);
                        break;
                }

                item.Build(root);
                L4Build.Regrade(root);
                L4Build.BakeLooseMeshes(root, item.Name);

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.layer == 0 && root.layer != 0 && !PrefabUtility.IsPartOfAnyPrefab(t.gameObject))
                    {
                        t.gameObject.layer = root.layer;
                    }
                }

                L2Build.EnsureFolder(L4Build.PrefabsPath + "/" + item.Folder);
                PrefabUtility.SaveAsPrefabAsset(root, item.Path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- The manifest

        private static List<Item> Table()
        {
            var list = new List<Item>();
            void Add(string id, string name, string folder, string table, float x, float z, float h, Col col, string layer, string status, string source, string notes, Action<GameObject> build)
            {
                list.Add(new Item { Id = id, Name = name, Folder = folder, Table = table, Size = new Vector3(x, h, z), Collider = col, Layer = layer, Status = status, Source = source, Notes = notes, Build = build });
            }

            KitRows(Add);
            PropRows(Add);
            FigureRows(Add);
            HazardRows(Add);
            MarkerRows(Add);
            StructureRows(Add);
            EnemyRows(Add, list);
            return list;
        }

        private delegate void AddRow(string id, string name, string folder, string table, float x, float z, float h, Col col, string layer, string status, string source, string notes, Action<GameObject> build);

        // ---------------------------------------------------------------- 14.2 Wall kit

        private static void KitRows(AddRow Add)
        {
            const string kit = "14.2 Wall kit";
            foreach (int length in new[] { 6, 4, 2 })
            {
                int l = length;
                Add("W01" + (l == 6 ? "a" : l == 4 ? "b" : "c"), "L4_CurtainFace_" + l, "Kit", kit, l, 2f, Tier, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Castle_Wall_01",
                    "The pack curtain wall (5.4 x 5.1 x 2.2) fitted to " + l + " x 5 x 2, x" + (l / 5.4f).ToString("F2") + " along the run; runs close with the 4 m and 2 m cuts. Smoke soot along the foot of the yard face.",
                    root => CurtainFace(root, l));
            }

            var states = new[] { L4Kit.Merlon.Whole, L4Kit.Merlon.Chipped, L4Kit.Merlon.Stub, L4Kit.Merlon.Gone };
            string[] suffix = { "a", "b", "c", "d" };
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                Add("W02" + suffix[i], "L4_Merlon_" + state, "Kit", kit, 2f, 0.6f, L4Kit.MerlonHeight, Col.Box, L3Build.Boundary, "BUILT", "L4Kit worn stone blocks (L2PolyKit.Tread) in a palette sampled from the graded castle stone",
                    "One 2 m module of the outer parapet: a 1.2 m merlon on a 0.9 m crenel sill. " + (state == L4Kit.Merlon.Whole ? "Whole." : state == L4Kit.Merlon.Chipped ? "Top course knocked about." : state == L4Kit.Merlon.Stub ? "Broken to one course above the sill." : "Merlon gone, sill only.") + " The collider is the full 1.8 m on every variant, so the player cannot jump the parapet where it is broken.",
                    root => L4Kit.MerlonRun(root.transform, new Vector3(-1f, 0f, 0.3f), Vector3.right, Vector3.forward, 1, 60 + (int)state, 0f, 0f, null, state, false));
            }

            Add("W03a", "L4_ParapetLow", "Kit", kit, 2f, 0.5f, 0.9f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit worn stone blocks",
                "Two courses of blocks, 0.9 m. An unseen cap (Ignore Raycast) raises its collider to 2 m: the player can jump 1 m walls.",
                root => Parapet(root, 0.05f, 71));
            Add("W03b", "L4_ParapetLow_Broken", "Kit", kit, 2f, 0.5f, 0.9f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit worn stone blocks",
                "As W03a with the top course knocked about: blocks missing and shifted. Same collider and cap.",
                root => Parapet(root, 0.6f, 72));
            Add("W04", "L4_CutWall", "Kit", kit, 2f, 2f, Tier, Col.Box, L3Build.Boundary, "BUILT", "PolygonKnights SM_Bld_Castle_Wall_01; L4Kit merlons",
                "Camera-side yard wall: a whole 2 m bay of curtain, 5 m with merlons, that fades (OcclusionFade) while it hides the player (refinement; it was cut to 0.9 m). The collider is the whole wall.",
                CutWall);
            Add("W05", "L4_RubbleLow", "Kit", kit, 2f, 2f, 0.8f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit fallen blocks; PolygonAdventure rocks",
                "A heap of fallen dressed blocks and dark stones, 0.8 m. Collider 0.8 m plus an unseen cap to 2 m.",
                Rubble);
            Add("W07", "L4_TowerShell", "Kit", kit, 10f, 10f, 9f, Col.Custom, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Castle_Wall_01 (two storeys), SM_Bld_Castle_Door_01; L4Kit merlons",
                "Four faces of curtain wall in two storeys round a 10 m square, broken merlons on top, a door in the south face. Colliders on the walls only. Every renderer carries the project's OcclusionFade, so the tower fades when it hides the player.",
                TowerShell);
            Add("W08a", "L4_GateArch_8", "Kit", kit, 12f, 2.4f, 8f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit voussoirs and courses",
                "An 8 m gateway: 2 m piers, a segmental arch springing at 4 m and rising 2.5 m (6.5 m clear at the crown), haunches coursed to 8 m. A chalk-white studded slot groove across the opening. The pack's gate wall has a 2 m door and would not scale.",
                root => GateArch(root, 8f, 4f, 2.5f, 8f));
            Add("W08b", "L4_GateArch_12", "Kit", kit, 16f, 2.4f, 9f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit voussoirs and courses",
                "The 12 m outer arch of the barbican: springing at 4.5 m, rising 3 m (7.5 m clear), coursed to 9 m. Slot groove as W08a.",
                root => GateArch(root, 12f, 4.5f, 3f, 9f));
            Add("W09a", "L4_Stair2R", "Kit", kit, 4f, 7.8f, Tier, Col.Custom, L3Build.Walkable, "ADAPTED", "PolygonKnights SM_Bld_StairsLarge_02 (two flights)",
                "Two flights of the pack's block-built stair (refinement: was the plain StairsLarge_01), each widened to 4 m and raised to 2.5 m: 5 m over 7.8 m (33 degrees), the upper flight on a coursed plinth. Climbs toward +Z. Visual steps, one ramp collider.",
                root => Stair(root, 1));
            Add("W09b", "L4_GrandStair", "Kit", kit, 16f, 7.8f, Tier, Col.Custom, L3Build.Walkable, "ADAPTED", "PolygonKnights SM_Bld_StairsLarge_02 (four across, two flights)",
                "The grand stair to the Kneeling Court: four W09a widths side by side, 16 m.",
                root => Stair(root, 4));
            Add("W10", "L4_Machicolation", "Kit", kit, 2f, 2.1f, 3.6f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Castle_Wood_Battlement_04; SM_Prop_Grate_01",
                "A timber box corbelled out from the wall top over a pour, sooted, its floor slot barred with an iron grate. Origin at the wall's top edge on the yard face; it hangs toward -Z. A SteamSocket child marks the slot for the pour's tell.",
                Machicolation);
            Add("W11", "L4_Hoarding", "Kit", kit, 2f, 2.1f, 3.6f, Col.Custom, L3Build.Walkable, "ADAPTED", "PolygonKnights SM_Bld_Castle_Wood_Battlement_04; L2 planks",
                "A 2 m bay of timber gallery on the wall top, hung toward -Z: plank floor (walkable) and the battlement's front as its rail. Floor collider on Ground, rail collider on Obstacles.",
                Hoarding);
            Add("W12", "L4_WallWalkDeck", "Kit", kit, 2f, 4f, 0.3f, Col.Box, L3Build.Walkable, "BUILT", "L4Kit worn stone flags",
                "A 2 x 4 m bay of wall-walk paving, flags 0.3 m thick laid in courses across the walk.",
                Deck);
        }

        // ---------------------------------------------------------------- Kit builders

        private static void CurtainFace(GameObject root, float length)
        {
            L3Build.Fit(root.transform, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(length, Tier, 2f), Centre, 0f, Stone);
            L4LookDev.Stain(root.transform, "SootFoot", new Vector3(0f, 0f, -1.03f), 0f, new Vector2(length + 0.2f, 1.8f), L4LookDev.StainMaterial("L4_SootFoot", 1));
        }

        private static void Parapet(GameObject root, float damage, int seed)
        {
            L4Kit.ParapetRun(root.transform, new Vector3(-1f, 0f, 0.25f), Vector3.right, Vector3.forward, 2f, seed, damage, 0.5f);
            L3Build.StandingBox(root, new Vector3(2f, 0.9f, 0.5f));
            Cap(root, new Vector3(2f, 1.1f, 0.5f), 0.9f);
        }

        // An unseen collider above a low boundary, on Ignore Raycast so mouse aim and the NavMesh bake ignore it.
        private static void Cap(GameObject root, Vector3 size, float baseY)
        {
            var cap = L3Build.Child(root.transform, "Cap", new Vector3(0f, baseY, 0f));
            L3Build.StandingBox(cap, size);
            L3Build.SetLayer(cap, L3Build.Marker);
        }

        // The camera-side yard wall (owner, 2026-10-19: "The CutWalls look weird. Please make these into walls.
        // Walls should disappear or lose opacity when they are blocking player view"): a whole 2 m bay of curtain,
        // 5 m, merlons on top, that fades (OcclusionFade) while it stands between the camera and the player. It was
        // 0.9 m of blocks with a shadows-only wall above.
        private static void CutWall(GameObject root)
        {
            CurtainFace(root, 2f);
            L4Kit.MerlonRun(root.transform, new Vector3(-1f, Tier, -1f), Vector3.right, Vector3.back, 1, 81, 0.4f, 0f, null, null, false);
            L4Build.BakeLooseMeshes(root, "L4_CutWall");
            L4Build.AddFade(root);
        }

        private static void Rubble(GameObject root)
        {
            var rng = new System.Random(91);
            var rocks = new[] { "A:SM_Env_Rock_07", "A:SM_Env_Rock_08", "A:SM_Env_Rock_09", "A:SM_Env_Rock_014" };
            for (int i = 0; i < 9; i++)
            {
                var at = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.6f, (i / 5) * 0.22f, ((float)rng.NextDouble() - 0.5f) * 1.6f);
                L4Kit.Fallen(root.transform, at, rng);
            }

            for (int i = 0; i < 6; i++)
            {
                var at = new Vector2(((float)rng.NextDouble() - 0.5f) * 1.8f, ((float)rng.NextDouble() - 0.5f) * 1.8f);
                L3Build.Seat(root.transform, rocks[rng.Next(rocks.Length)], new Vector3(0f, rng.Next(360), 0f), 0.35f + (float)rng.NextDouble() * 0.25f, at, -0.05f, AdvSoot);
            }

            L3Build.StandingBox(root, new Vector3(2f, 0.8f, 2f));
            Cap(root, new Vector3(2f, 1.2f, 2f), 0.8f);
        }

        private static void TowerShell(GameObject root)
        {
            var t = root.transform;
            for (int side = 0; side < 4; side++)
            {
                Vector2 dir = side == 0 ? Vector2.down : side == 1 ? Vector2.left : side == 2 ? Vector2.up : Vector2.right;
                Vector2 at = dir * 3.9f;
                bool ns = side % 2 == 0;
                int yaw = ns ? 0 : 90;
                L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", yaw, ns ? new Vector3(10f, Tier, 2.2f) : new Vector3(2.2f, Tier, 10f), at, 0f, Stone);
                L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", yaw, ns ? new Vector3(10f, 4f, 2.2f) : new Vector3(2.2f, 4f, 10f), at, Tier, Stone);
                var wall = L3Build.Child(t, "Wall_" + side, new Vector3(at.x, 0f, at.y));
                L3Build.StandingBox(wall, ns ? new Vector3(10f, 9f, 2.2f) : new Vector3(2.2f, 9f, 10f));
            }

            L4Kit.MerlonRun(t, new Vector3(-5f, 9f, -5f), Vector3.right, Vector3.back, 5, 41, 0.55f, 0f, null, null, false);
            L4Kit.MerlonRun(t, new Vector3(5f, 9f, -5f), Vector3.forward, Vector3.right, 5, 42, 0.55f, 0f, null, null, false);
            L4Kit.MerlonRun(t, new Vector3(5f, 9f, 5f), Vector3.left, Vector3.forward, 5, 43, 0.55f, 0f, null, null, false);
            L4Kit.MerlonRun(t, new Vector3(-5f, 9f, 5f), Vector3.back, Vector3.left, 5, 44, 0.55f, 0f, null, null, false);
            L3Build.Seat(t, "K:SM_Bld_Castle_Door_01", Vector3.zero, 0.9f, new Vector2(0f, -5.05f), 0f, Soot);
            L4Build.BakeLooseMeshes(root, "L4_TowerShell");
            L4Build.AddFade(root);
        }

        private static void GateArch(GameObject root, float span, float springY, float rise, float height)
        {
            const float pier = 2f, thickness = 2.4f;
            L4Kit.GateArch(root.transform, span, pier, springY, rise, height, thickness, (int)(span * 10));
            float half = span * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                var p = L3Build.Child(root.transform, "Pier", new Vector3(side * (half + pier * 0.5f), 0f, 0f));
                L3Build.StandingBox(p, new Vector3(pier, height, thickness));
            }

            var top = L3Build.Child(root.transform, "Lintel", new Vector3(0f, springY + rise * 0.6f, 0f));
            L3Build.StandingBox(top, new Vector3(span, height - springY - rise * 0.6f, thickness));
            SlotGroove(root.transform, span);
        }

        // The portcullis slot across a gate's floor: a dark groove 0.3 m wide between chalk-white kerbs, iron
        // studs along it, old blood in the joints. Opaque, set into the floor (no decal).
        private static void SlotGroove(Transform parent, float span)
        {
            var g = L3Build.Child(parent, "SlotGroove").transform;
            var rng = new System.Random((int)span);
            var chalk = L4Build.Flat("L4_ChalkKerb", "#9E998F", 0.05f);
            var iron = L4Build.Mat("L4_Iron");
            var dark = L4Build.Flat("L4_Groove", "#1A1716", 0.2f);
            L3Build.MeshChild(g, "Groove", L2PolyKit.Tread(new Vector3(span, 0.04f, 0.3f), 9), new Vector3(0f, -0.01f, 0f), dark);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = -span * 0.5f;
                while (x < span * 0.5f - 0.1f)
                {
                    float w = Mathf.Min(0.6f + (float)rng.NextDouble() * 0.4f, span * 0.5f - x);
                    L3Build.MeshChild(g, "Kerb", L2PolyKit.Tread(new Vector3(w - 0.03f, 0.05f, 0.25f), rng.Next(100000)), new Vector3(x + w * 0.5f, 0.012f, side * 0.29f), chalk);
                    x += w;
                }
            }

            for (float x = -span * 0.5f + 0.4f; x < span * 0.5f; x += 0.8f)
            {
                L3Build.MeshChild(g, "Stud", L2PolyKit.Tread(new Vector3(0.09f, 0.04f, 0.09f), (int)(x * 100)), new Vector3(x, 0.02f, 0f), iron);
            }
        }

        private static void Stair(GameObject root, int across)
        {
            const float width = 4f, rise = 2.5f, run = 3.9f;
            float total = width * across;
            for (int a = 0; a < across; a++)
            {
                float x = -total * 0.5f + width * (a + 0.5f);
                for (int f = 0; f < 2; f++)
                {
                    L3Build.Fit(root.transform, "K:SM_Bld_StairsLarge_02", 180, new Vector3(width, rise, run), new Vector2(x, -run * 0.5f + run * f), rise * f, L4Build.Mat("L4_Knights_StoneDark"));
                }

                // The upper flight stands on coursed masonry, not on air.
                L3Build.Fit(root.transform, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(width - 0.12f, rise, run - 0.1f), new Vector2(x, run * 0.5f + 0.02f), 0f, L4Build.Mat("L4_Knights_StoneDark"));
            }

            // One ramp collider through the step nosings.
            var ramp = L3Build.Child(root.transform, "Ramp");
            float length = Mathf.Sqrt(run * 2f * run * 2f + rise * 2f * rise * 2f);
            float angle = Mathf.Atan2(rise * 2f, run * 2f) * Mathf.Rad2Deg;
            ramp.transform.localPosition = new Vector3(0f, rise - 0.15f, 0f);
            ramp.transform.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            var box = ramp.AddComponent<BoxCollider>();
            box.size = new Vector3(total, 0.3f, length);
            L3Build.SetLayer(ramp, L3Build.Walkable);
        }

        private static void Machicolation(GameObject root)
        {
            L3Build.Seat(root.transform, "K:SM_Bld_Castle_Wood_Battlement_04", new Vector3(0f, 180f, 0f), new Vector3(1.11f, 1f, 1f), new Vector2(0f, -1.05f), -1.6f, Soot);
            L3Build.Seat(root.transform, "K:SM_Prop_Grate_01", Vector3.zero, new Vector3(1.6f, 1f, 1.4f), new Vector2(0f, -1.05f), -1.62f, L4Build.Mat("L4_Iron"));
            L3Build.Child(root.transform, "SteamSocket", new Vector3(0f, -1.7f, -1.05f));
            var box = root.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, -1.05f);
        }

        private static void Hoarding(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "K:SM_Bld_Castle_Wood_Battlement_04", new Vector3(0f, 180f, 0f), new Vector3(1.11f, 1f, 1f), new Vector2(0f, -1.05f), -1.6f, Soot);
            var rng = new System.Random(111);
            for (int i = 0; i < 5; i++)
            {
                var plank = L3Build.MeshChild(t, "Plank", L2PolyKit.Plank(new Vector3(2f, 0.06f, 0.4f), rng.Next(100000)), new Vector3(0f, 0.03f, -0.22f - i * 0.42f), L2PolyKit.CharredWood);
                plank.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 2f, 0f);
            }

            var floor = L3Build.Child(t, "Floor", new Vector3(0f, -0.2f, -1.05f));
            L3Build.StandingBox(floor, new Vector3(2f, 0.26f, 2.1f));
            L3Build.SetLayer(floor, L3Build.Walkable);
            var rail = L3Build.Child(t, "Rail", new Vector3(0f, 0f, -2.05f));
            L3Build.StandingBox(rail, new Vector3(2f, 1.8f, 0.2f));
            L3Build.SetLayer(rail, L3Build.Boundary);
        }

        private static void Deck(GameObject root)
        {
            var rng = new System.Random(121);
            float z = -2f;
            while (z < 1.95f)
            {
                float d = Mathf.Min(0.6f + (float)rng.NextDouble() * 0.35f, 2f - z);
                float x = -1f;
                while (x < 0.95f)
                {
                    float w = Mathf.Min(0.7f + (float)rng.NextDouble() * 0.6f, 1f - x);
                    var flag = L3Build.MeshChild(root.transform, "Flag", L2PolyKit.Tread(new Vector3(w - 0.03f, 0.3f, d - 0.03f), rng.Next(100000)), new Vector3(x + w * 0.5f, 0.15f, z + d * 0.5f), L4Kit.Stone);
                    flag.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 1.2f, 0f, ((float)rng.NextDouble() - 0.5f) * 1.2f);
                    x += w;
                }

                z += d;
            }
        }
    }
}
