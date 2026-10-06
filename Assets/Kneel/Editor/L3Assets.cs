using System;
using System.Collections.Generic;
using Kneel.Hazards;
using Kneel.Markers;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Builds every prefab in the L3 asset manifest (design note, section 14). Each row of the table below is
    // one manifest ID: its footprint and collider come from the note, its looks from the packs where they
    // fit. The collider is always added from the row, so it matches the note whatever the visuals do.
    public static class L3Assets
    {
        public enum Col
        {
            Box,
            Cylinder,
            Capsule,
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

            // Set for rows served by a prefab that already existed in the project.
            public string ExistingPath;

            public string Path => ExistingPath ?? L3Build.PrefabsPath + "/" + Folder + "/" + Name + ".prefab";
        }

        private static Material M(string name)
        {
            return L3Build.Mat("L3_" + name);
        }

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

        [MenuItem("Kneel/L3/Build/Asset Prefabs")]
        public static void BuildMenu()
        {
            Debug.Log(BuildAll());
        }

        public static string BuildAll()
        {
            items = null;
            L3Look.BuildAll();
            int built = 0;
            foreach (var item in Items)
            {
                if (item.Build == null)
                {
                    continue;
                }

                BuildItem(item);
                built++;
            }

            AssetDatabase.SaveAssets();
            return "L3 assets: built " + built + " prefabs under " + L3Build.PrefabsPath;
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
                    case Col.Capsule:
                        var capsule = root.AddComponent<CapsuleCollider>();
                        capsule.radius = item.Size.x * 0.5f;
                        capsule.height = item.Size.y;
                        capsule.center = new Vector3(0f, item.Size.y * 0.5f, 0f);
                        break;
                }

                item.Build(root);

                // Children take the root's layer unless the builder gave them their own (or they belong to a
                // nested prefab, which keeps its own).
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.layer == 0 && root.layer != 0 && !PrefabUtility.IsPartOfAnyPrefab(t.gameObject))
                    {
                        t.gameObject.layer = root.layer;
                    }
                }

                L2Build.EnsureFolder(L3Build.PrefabsPath + "/" + item.Folder);
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

            const string kit = "14.2 Kit", props = "14.3 Cover and props", hazards = "14.4 Hazards", structures = "14.5 Structures", markers = "14.6 Markers", enemies = "14.7 Enemies";

            // ---- 14.2 Kit: boundaries
            Add("K01", "L3_Hedge_Tall", "Kit", kit, 2f, 1f, 2.5f, Col.Box, L3Build.Boundary, "ADAPTED", "Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_TreeDead_01/02 (stems)",
                "A burnt field hedgerow, not the pack's clipped garden hedge: a thicket of arching canes hung with dead leaves, with bare thorn stems standing out of the top. Nothing under it and no bushes at its foot (owner's review). It moves with the wind shader.",
                root => Hedgerow(root.transform, "L3_Hedge_Tall", 2f, 1f, 2.5f, 11, false));
            Add("K02", "L3_Hedge_Low", "Kit", kit, 2f, 1f, 1.2f, Col.Box, L3Build.Boundary, "ADAPTED", "Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_TreeDead_01/02 (stems)",
                "The same hedgerow cut down to 1.2 m, for south edges.",
                root => Hedgerow(root.transform, "L3_Hedge_Low", 2f, 1f, 1.2f, 12, false));
            Add("K03", "L3_StoneWall_Low", "Kit", kit, 2f, 1f, 1f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Rockwall_Straight_01",
                "The same rock wall as K04, scaled non-uniformly down to 2 x 1 x 0.85 so low and tall runs match. The mesh is 0.85 m thick inside the 1 m collider.",
                root => L3Build.Fit(root.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(2.02f, 1f, 0.85f), Centre));
            Add("K04", "L3_StoneWall_Tall", "Kit", kit, 2f, 1f, 2.5f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Rockwall_Straight_01",
                "Pack rock wall is 4.0 long x 2.7 x 1.5; turned a quarter and scaled non-uniformly to 2 x 2.5 x 0.9.",
                root => L3Build.Fit(root.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(2.02f, 2.5f, 0.9f), Centre));
            Add("K05", "L3_Bank", "Kit", kit, 2f, 2f, 3f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Rockwall_Straight_01",
                "The packs' cliffs are 18 m and more across, so the bank is the rock wall scaled non-uniformly to 2 x 2 x 3 and earth-tinted.",
                root => L3Build.Fit(root.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(2.04f, 3f, 2f), Centre, 0f, M("Knights_Earth")));
            Add("K06", "L3_Rail", "Kit", kit, 2f, 0.2f, 1f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonAdventure SM_Bld_Fence_02",
                "Pack fence is 1.6 x 1.25 x 0.2; lengthened to 2 m and lowered to 1 m.",
                root => L3Build.Fit(root.transform, "A:SM_Bld_Fence_02", 0, new Vector3(2f, 1f, 0.2f), Centre));
            Add("K09", "L3_Rail_Broken", "Kit", kit, 2f, 0.2f, 1f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonAdventure SM_Bld_Fence_02 (taken apart)",
                "Owner's review: field fences, not walls, along the lanes. The same panel as K06 after a few winters: the top rail is off and lies at its foot, one rail hangs by one end, both posts lean. Same collider as K06, so a run stays closed.",
                root => FenceWorn(root, 0));
            Add("K10", "L3_Rail_Leaning", "Kit", kit, 2f, 0.2f, 1f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonAdventure SM_Bld_Fence_02 (taken apart)",
                "As K09: the whole panel pushed over a little, one rail gone, the others slipped in their posts. Same collider as K06.",
                root => FenceWorn(root, 1));
            Add("K07", "L3_KerbStone", "Kit", kit, 1f, 0.5f, 0.3f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonAdventure SM_Prop_StoneBlock_01",
                "Pack stone block scaled non-uniformly to 1 x 0.5 x 0.3 and whitewashed. No collider, as specified.",
                root => L3Build.Fit(root.transform, "A:SM_Prop_StoneBlock_01", 0, new Vector3(1f, 0.3f, 0.5f), Centre, 0f, M("Lime")));
            Add("K08", "L3_Hedge_Passable", "Kit", kit, 2f, 1f, 2.5f, Col.None, L3Build.Unlayered, "ADAPTED", "Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_TreeDead_01/02 (stems)",
                "The same hedgerow burnt thin: fewer canes, bare stems, gaps to see through. No collider, as specified; on Default so the NavMesh bake ignores it.",
                root => Hedgerow(root.transform, "L3_Hedge_Passable", 2f, 1f, 2.5f, 13, true));

            // ---- 14.3 Cover and props
            Add("C01", "L3_HayWagon", "Props", props, 3f, 2f, 1.8f, Col.Box, L3Build.Cover, "ADAPTED", "PolygonKnights SM_Prop_CartHay_01; Adventure sacks, crate",
                "Hay cart turned east-west at 0.93 scale (3.0 x 1.55 x 1.4), with sacks and a crate filling the 2 m depth.",
                root =>
                {
                    L3Build.Seat(root.transform, "K:SM_Prop_CartHay_01", new Vector3(0f, 90f, 0f), 0.93f, new Vector2(0f, 0.28f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_04", new Vector3(0f, 4f, 0f), 1f, new Vector2(-0.35f, -0.62f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Crate_01", new Vector3(0f, 12f, 0f), 0.8f, new Vector2(1.05f, -0.62f));
                });
            Add("C02", "L3_KennelWagon", "Props", props, 3f, 4f, 1.8f, Col.Box, L3Build.Cover, "BUILT", "PolygonAdventure SM_Prop_Cart_02; PolygonKnights iron gate (as cage bars)",
                "No cage cart in the packs. A cart rolled onto its side with a cage made from iron-gate panels, and one panel bent outward.",
                KennelWagon);
            Add("C03", "L3_Cairn", "Props", props, 2f, 2f, 1.2f, Col.Box, L3Build.Cover, "ADAPTED", "PolygonKnights SM_Env_RockPile_03",
                "Pack rock pile (5 x 5 x 6) scaled non-uniformly down to 2 x 2 x 1.2.",
                root => L3Build.Fit(root.transform, "K:SM_Env_RockPile_03", 0, new Vector3(2f, 1.4f, 2f), Centre, -0.2f));
            Add("C04", "L3_HedgeStub", "Props", props, 6f, 2f, 1.5f, Col.Box, L3Build.Cover, "ADAPTED", "Generated thorn thicket (L3Hedges); PolygonAdventure SM_Env_TreeDead_01/02 (stems)",
                "Six metres of the same hedgerow, 2 m deep, 1.5 m high.",
                root => Hedgerow(root.transform, "L3_HedgeStub", 6f, 2f, 1.5f, 14, false));
            Add("C05", "L3_PloughTeam", "Props", props, 4f, 2f, 1.4f, Col.Box, L3Build.Cover, "BUILT", "Generated ox skeletons (L3Bones); PolygonKnights SM_Prop_Beam_01",
                "No ox or plough in the packs. Two ox skeletons collapsed in a beam yoke, with a beam plough behind them.",
                PloughTeam);
            Add("C06", "L3_StoneRoller", "Props", props, 2f, 2f, 1.2f, Col.Box, L3Build.Cover, "BUILT", "Generated faceted stone drum; PolygonKnights SM_Prop_Beam_01",
                "No roller in the packs. A faceted stone drum 1.2 m across in a beam frame.",
                StoneRoller);
            Add("C07", "L3_SeedDrill", "Props", props, 2f, 2f, 1.2f, Col.Box, L3Build.Cover, "BUILT", "PolygonAdventure SM_Prop_Cart_01, crate, cart wheel",
                "No seed drill in the packs. A small tipped cart with a hopper crate and a broken wheel.",
                SeedDrill);
            Add("C08", "L3_Well", "Props", props, 2f, 2f, 1.2f, Col.Cylinder, L3Build.Cover, "ADAPTED", "PolygonKnights SM_Bld_Village_Well_01",
                "Pack well is 3.1 across and 0.9 high; scaled to 2 m across and raised to 1.2 m so it works as cover. Convex cylinder collider.",
                root => L3Build.Fit(root.transform, "K:SM_Bld_Village_Well_01", 0, new Vector3(2f, 1.2f, 2f), Centre));
            Add("C09", "L3_Millstones", "Props", props, 2f, 2f, 1.2f, Col.Box, L3Build.Cover, "BUILT", "Generated faceted stone discs",
                "No millstone in the packs. A stack of three faceted stone discs with a fourth leaning on it.",
                Millstones);
            Add("C10", "L3_GrainCart", "Props", props, 3f, 2f, 1.5f, Col.Box, L3Build.Cover, "ADAPTED", "PolygonAdventure SM_Prop_Cart_01, sacks",
                "Open cart turned east-west at 0.95 scale, loaded with sacks up to 1.4 m.",
                root =>
                {
                    L3Build.Seat(root.transform, "A:SM_Prop_Cart_01", new Vector3(0f, 90f, 0f), 0.95f, new Vector2(0f, 0.1f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_01", new Vector3(0f, 20f, 0f), 1.5f, new Vector2(-0.6f, 0.1f), 0.62f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_02", new Vector3(0f, -30f, 0f), 1.5f, new Vector2(0.1f, 0.2f), 0.62f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_01", new Vector3(0f, 80f, 0f), 1.4f, new Vector2(0.7f, 0f), 0.62f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_04", new Vector3(0f, 3f, 0f), 0.9f, new Vector2(0.3f, -0.72f));
                });
            Add("C11", "L3_Trough", "Props", props, 2f, 2f, 1.2f, Col.Box, L3Build.Cover, "BUILT", "L2PolyKit worn stone blocks; PolygonAdventure barrels, sack, basket",
                "No trough in the packs. A long basin of dressed stone blocks with water in it, and barrels and a sack behind it that bring the cover up to 1.2 m. (The well is C08, which is the same pack well L2 uses.)",
                Trough);
            Add("C12", "L3_LimeMound", "Props", props, 4f, 4f, 1.5f, Col.Cylinder, L3Build.Cover, "ADAPTED", "PolygonAdventure SM_Env_DirtMound_01",
                "Pack dirt mound (7.9 x 2.8 x 7.4) scaled to 4 x 1.5 x 4 and given a plain lime-white material. Convex cylinder collider.",
                root => L3Build.Fit(root.transform, "A:SM_Env_DirtMound_01", 0, new Vector3(4f, 1.5f, 4f), Centre, 0f, M("Lime")));
            Add("C13", "L3_GraveCart", "Props", props, 2f, 3f, 1.5f, Col.Box, L3Build.Cover, "ADAPTED", "PolygonKnights SM_Prop_Cart_01; Adventure crates, sack, basket",
                "Cart at 0.95 scale (1.45 x 1.1 x 3.0), long side north-south, loaded with household goods; a sack and basket beside it fill the 2 m width.",
                root =>
                {
                    L3Build.Seat(root.transform, "K:SM_Prop_Cart_01", Vector3.zero, 0.95f, new Vector2(-0.25f, 0f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Crate_01", new Vector3(0f, 8f, 0f), 0.8f, new Vector2(-0.25f, -0.5f), 0.6f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Crate_02", new Vector3(0f, -15f, 0f), 0.7f, new Vector2(-0.3f, 0.3f), 0.6f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Basket_02", new Vector3(0f, 30f, 0f), 1f, new Vector2(-0.2f, 0.95f), 0.6f);
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_01", new Vector3(0f, 70f, 0f), 1.2f, new Vector2(0.72f, 0.6f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Basket_04", new Vector3(0f, -20f, 0f), 1f, new Vector2(0.68f, -0.5f));
                });
            Add("C14", "L3_RequisitionCart", "Props", props, 2f, 3f, 1.5f, Col.Box, L3Build.Cover, "ADAPTED", "PolygonAdventure SM_Prop_Cart_01, sacks, cart wheel",
                "Open cart turned upside down at 0.95 scale, long side north-south, with spilled sacks and a loose wheel.",
                root =>
                {
                    L3Build.Seat(root.transform, "A:SM_Prop_Cart_01", new Vector3(0f, 0f, 180f), 0.95f, new Vector2(-0.1f, 0f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_03", new Vector3(0f, 40f, 0f), 1.2f, new Vector2(0.7f, -0.9f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Sack_01", new Vector3(0f, -25f, 0f), 1.1f, new Vector2(0.72f, 0.2f));
                    L3Build.Seat(root.transform, "A:SM_Prop_Cart_Wheel_01", new Vector3(0f, 0f, 90f), 1f, new Vector2(0.45f, 1.0f));
                });
            Add("C15", "L3_Carcass", "Props", props, 2f, 1f, 0.6f, Col.None, L3Build.Unlayered, "BUILT", "Generated ox skeleton (L3Bones)",
                "No animal models in the packs. A bare skeleton lying on its side, built the way L2 builds its human skeletons: bone only, no hide and nothing on the ground under it (owner's review). Used for the ox, the feeding carcass and the dead hound. No collider, as specified.",
                root => Bones(root.transform, "Skeleton", L3Bones.OxOnSide, Vector3.zero, 0f));

            // ---- 14.4 Hazards
            Add("H01", "L3_FireCell", "Hazards", hazards, 2f, 2f, 2f, Col.Custom, L3Build.Hazard, "BUILT", "Kneel.Hazards.FireCell; wheat baked from PolygonAdventure SM_Env_Reeds_01-03 (L3Crops); flames from L2Fire",
                "2 x 2 x 2 trigger. Dry is standing wheat about 0.8 m tall on a tilled bed (the note asks for a 0.6 m tan block), swaying with the wind shader; burning chars it; ash is burnt stubble. Contact is polled, not sent by trigger messages.",
                BuildFireCell);
            Add("H02", "L3_CropRow_12", "Hazards", hazards, 2f, 24f, 2f, Col.Custom, L3Build.Hazard, "BUILT", "Kneel.Hazards.CropRow; twelve nested L3_FireCell",
                "Pivot at the south end; cells cover z 0 to 24. NavMesh modifier volume (child NavArea, on Ground so the bake collects it) uses the CropRow area.",
                BuildCropRow);
            Add("H03", "L3_BrandStake", "Hazards", hazards, 0.3f, 0.3f, 1.5f, Col.Custom, L3Build.Unlayered, "BUILT", "Kneel.Hazards.BrandStake; PolygonKnights SM_Prop_Beam_01",
                "Takes hits through the project's IDamageable; its 0.3 x 0.3 x 1.5 hit box is a trigger on the Enemy layer, which is where the sword looks. Inspector button: Knock.",
                BuildBrandStake);
            Add("H04", "L3_MudVolume", "Hazards", hazards, 4f, 4f, 2f, Col.Custom, L3Build.Hazard, "BUILT", "Kneel.Hazards.SurfaceVolume",
                "Resizable through SurfaceVolume.Configure (the inspector's Size). Damp bands are 2 m strips outside the mud, switchable per side; west and east are on by default.",
                BuildMudVolume);
            // H05 (L3_SailHazard) was cut in the owner's review of the blockout. The number is not reused.
            Add("H06", "L3_DitchKill", "Hazards", hazards, 6f, 6f, 2.5f, Col.Custom, L3Build.Hazard, "BUILT", "Kneel.Hazards.DitchKill",
                "Resizable trigger (default 6 x 6 x 2.5, pivot at the bottom centre).",
                root =>
                {
                    L3Build.StandingBox(root, new Vector3(6f, 2.5f, 6f), true);
                    L3Build.Set(root.AddComponent<DitchKill>(), "contact", L3Look.Contact);
                });
            Add("H07", "L3_Gibbet_Inert", "Hazards", hazards, 0.4f, 0.4f, 3f, Col.Box, L3Build.Boundary, "BUILT", "PolygonKnights SM_Prop_Beam_01; Adventure sacks",
                "Post, crossbar and a sack figure. Collider on the post only.",
                root => Gibbet(root.transform));
            Add("H08", "L3_Gibbet_Crowed", "Hazards", hazards, 0.4f, 0.4f, 3f, Col.Box, L3Build.Boundary, "BUILT", "Kneel.Hazards.GibbetAmbush; as H07 plus three generated crows",
                "Adds one extra collider to H07: a trigger on the Enemy layer round the figure, so the sword can strike it early. Inspector buttons: Trigger, Strike early.",
                GibbetCrowed);

            // ---- 14.5 Structures and landmarks
            Add("S01", "L3_StoneBridge", "Structures", structures, 6f, 10f, 1f, Col.Custom, L3Build.Unlayered, "BUILT", "L2PolyKit worn stone blocks (deck); PolygonKnights SM_Bld_Rockwall_Straight_01 (parapets, piers)",
                "Flat deck (not humpbacked) so it bakes and walks cleanly. Deck 6 x 10 with its top at ground level on Ground; parapets 0.5 x 1 x 10 on Obstacles, leaving 5 m clear.",
                StoneBridge);
            Add("S02", "L3_SluiceFootway", "Structures", structures, 4f, 10f, 1f, Col.Custom, L3Build.Unlayered, "BUILT", "L2 plank meshes; nested L3_Rail",
                "Plank deck 4 x 10 with its top at ground level on Ground; five L3_Rail per side.",
                SluiceFootway);
            Add("S03", "L3_Farmhouse", "Structures", structures, 11f, 12f, 4f, Col.Custom, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_House_Room_01/03/05/06 (walls and roofs cut from them), chimney; Adventure table, stores; L2 peasant corpse; L2PolyKit planks",
                "Cut from the pack houses at their own scale: their walls laid as inner and outer skins of 1 m walls (2.3 m to the eaves, colliders 3.2 m), four of their roofs as a double-pile roof pressed down to a 4 m ridge. Door gap 4 m in the west wall. Carries its own board floor (interior 9 x 10 plus the threshold) on Ground. Roof and south wall are separate children with FadeOnEnterMarker and the project OcclusionFade, so they fade when they hide the player. Dressed inside: hearth, table laid for five, the farmer, stores. The chimney stands 0.7 m above the 4 m ridge.",
                L3Farmhouse.Build);
            Add("S04", "L3_Windmill", "Structures", structures, 10f, 8f, 14f, Col.Box, L3Build.Boundary, "BUILT", "PolygonKnights round castle tower pieces, house door; Adventure log pile, barrels, sacks, crates; L2PolyKit planks (sails)",
                "No windmill in the packs. A tapering tower of round tower pieces under a low conical cap (the pack spire roof, flattened), stores stacked against both sides, a hub on the south face and four static sail frames of planks. Not burning (owner's review). One solid box collider.",
                Windmill);
            Add("S08", "L3_MillStair", "Structures", structures, 4.7f, 7.4f, 4f, Col.Custom, L3Build.Unlayered, "BUILT", "L2PolyKit worn stone blocks",
                "Owner's review: the mill stands on a 3 m mound and needs a way up. Twelve worn stone steps (3.6 m wide, 0.25 m risers) between stepped cheek walls, and a 2 m landing at the mill door. Pivot at the middle of the foot; the stair climbs toward +Z. Walkable: a ramp collider under the steps and the landing are on Ground, the cheek walls on Obstacles.",
                MillStair);
            Add("S05", "L3_BoundaryOak", "Structures", structures, 2f, 2f, 12f, Col.Capsule, L3Build.Boundary, "ADAPTED", "PolygonAdventure SM_Env_TreeDead_01, SM_Env_TreeLog_01, SM_Env_TreeStump_01",
                "The pack's dead tree branches from 1.7 m, so it stands on a log trunk: lowest branch at 6.5 m, top at 12 m. Capsule collider on the trunk only.",
                root =>
                {
                    L3Build.Fit(root.transform, "A:SM_Env_TreeStump_01", 0, new Vector3(2.4f, 1.5f, 2.4f), Centre, -0.1f);
                    L3Build.Seat(root.transform, "A:SM_Env_TreeLog_01", new Vector3(0f, 0f, 90f), new Vector3(2.2f, 1.9f, 1.9f), Centre, -0.3f);
                    L3Build.Part(root.transform, "A:SM_Env_TreeDead_01", new Vector3(0f, 5f, 0f), new Vector3(0f, 35f, 0f), Vector3.one * 1.06f);
                });
            Add("S06", "L3_OrchardTree", "Structures", structures, 0.5f, 0.5f, 3.5f, Col.Capsule, L3Build.Boundary, "FOUND", "PolygonKnights SM_Env_Tree_01",
                "Used at its own size (3.55 m tall). Capsule collider on the trunk only.",
                root => L3Build.Part(root.transform, "K:SM_Env_Tree_01", Vector3.zero, Vector3.zero, Vector3.one));
            Add("S07", "L3_CellarFront", "Structures", structures, 4f, 1f, 2.5f, Col.Box, L3Build.Boundary, "ADAPTED", "PolygonKnights SM_Bld_Rockwall_Archway_01",
                "Pack archway turned a quarter and scaled non-uniformly to 4 x 2.5 x 1, with a dark door set in it.",
                root =>
                {
                    L3Build.Fit(root.transform, "K:SM_Bld_Rockwall_Archway_01", 90, new Vector3(4f, 2.5f, 1f), Centre);
                    L3Build.Block(root.transform, "Door", new Vector3(1.9f, 1.9f, 0.5f), new Vector3(0f, 0f, 0f), M("WoodCharred"));
                });

            // ---- 14.6 Markers and interactables
            list.Add(new Item
            {
                Id = "M01", Name = "L3_Shrine", Folder = "Markers", Table = markers, Size = new Vector3(1f, 1.5f, 1f), Collider = Col.Custom, Layer = L3Build.Unlayered,
                Status = "FOUND", Source = "Kneel L1_CheckpointShrine (nested)",
                Notes = "Wraps the existing L1 shrine unchanged and adds a ShrineMarker and a 12 m ember column. The L1 shrine is larger than a 1 x 1 pillar (see its bounds in the checks).",
                Build = Shrine,
            });
            Add("M02", "L3_SluiceGate", "Markers", markers, 4f, 0.3f, 2f, Col.Custom, L3Build.Unlayered, "BUILT", "Kneel.Hazards.SluiceGate (pattern from L2 ShortcutGate and GateLever)",
                "L2's gate opens by a lever and has no barred prompt or Opened event, and existing gameplay code is read-only, so this is a new component. Interact (E) from the +Z side opens it. Inspector buttons: Open from +Z, Try from -Z.",
                BuildSluiceGate);
            Add("M03", "L3_SecretTell", "Markers", markers, 0.3f, 0.05f, 0.3f, Col.None, L3Build.Unlayered, "BUILT", "Kneel.Markers.SecretTell; generated handprint texture",
                "A 0.3 x 0.3 quad 1.2 m up, facing the prefab's +Z. Carries an optional marker post (child 'Post', off by default) for tells that stand alone.",
                SecretTellPrefab);
            Add("M04", "L3_Pickup_Consumable", "Markers", markers, 0.7f, 0.5f, 0.5f, Col.None, L3Build.Unlayered, "FOUND", "Kneel L2_Loot_Heal (nested)",
                "Wraps the existing L2 heal pickup and adds a PickupMarker and a glow.",
                root => Pickup(root, PickupKind.Consumable, 1.2f, 2.5f, r => L3Build.Instance(L2Prefab("L2_Loot_Heal"), r.transform, Vector3.zero)));
            Add("M05", "L3_Pickup_Lore", "Markers", markers, 0.8f, 0.5f, 0.2f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonAdventure SM_Prop_Scroll_02",
                "A scroll with a PickupMarker and a glow.",
                root => Pickup(root, PickupKind.Lore, 1.2f, 2.5f, r => L3Build.Seat(r.transform, "A:SM_Prop_Scroll_02", new Vector3(0f, 25f, 0f), 0.8f, Centre)));
            Add("M06", "L3_Pickup_HealCapacity", "Markers", markers, 0.5f, 0.5f, 0.8f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonAdventure SM_Item_Potion_04, SM_Env_Rock_015",
                "A large flask on a stone with a PickupMarker and a visibly brighter glow.",
                root => Pickup(root, PickupKind.HealCapacity, 3.2f, 4.5f, r =>
                {
                    L3Build.Seat(r.transform, "A:SM_Env_Rock_015", Vector3.zero, 1f, Centre);
                    L3Build.Seat(r.transform, "A:SM_Item_Potion_04", Vector3.zero, 1.5f, Centre, 0.3f);
                }));
            Add("M07", "L3_Pickup_Tonal", "Markers", markers, 0.15f, 0.15f, 0.15f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonPrototype SM_Icon_Apple_01",
                "The unburnt apple: the prototype pack's apple at 0.22 scale, with a PickupMarker and a faint glow.",
                root => Pickup(root, PickupKind.Tonal, 0.7f, 1.8f, r => L3Build.Seat(r.transform, "P:SM_Icon_Apple_01", Vector3.zero, 0.22f, Centre)));
            Add("M08", "L3_VistaTrigger", "Markers", markers, 8f, 6f, 4f, Col.Custom, L3Build.Marker, "BUILT", "Kneel.Markers.VistaMarker",
                "Box trigger (default 8 x 4 x 6) on Ignore Raycast. Resize with L3Build-style helpers or the collider; the magenta box is hidden in play mode.",
                root => MarkerVolume(root, new Vector3(8f, 4f, 6f)).AddComponent<VistaMarker>());
            Add("M09", "L3_ArenaCamVolume", "Markers", markers, 8f, 8f, 6f, Col.Custom, L3Build.Marker, "BUILT", "Kneel.Markers.ArenaMarker",
                "Box trigger (default 8 x 6 x 8, sized to its arena floor when placed) on Ignore Raycast.",
                root => MarkerVolume(root, new Vector3(8f, 6f, 8f)).AddComponent<ArenaMarker>());
            Add("M10a", "L3_PlayerSpawn", "Markers", markers, 0f, 0f, 0f, Col.None, L3Build.Unlayered, "BUILT", "Kneel.Markers.PlayerSpawnMarker",
                "An empty with a forward-arrow gizmo and a magenta arrow that is hidden in play mode.",
                root =>
                {
                    root.AddComponent<PlayerSpawnMarker>();
                    var visual = L3Build.Child(root.transform, "Visual");
                    L3Build.Block(visual.transform, "Shaft", new Vector3(0.25f, 0.06f, 1.5f), new Vector3(0f, 0.02f, 0.75f), M("GB_MarkerSolid"));
                    L3Build.Block(visual.transform, "Head", new Vector3(0.7f, 0.06f, 0.7f), new Vector3(0f, 0.02f, 1.6f), M("GB_MarkerSolid"), 45f);
                    visual.AddComponent<MarkerVisual>();
                });
            Add("M10b", "L3_ExitTrigger", "Markers", markers, 4f, 4f, 3f, Col.Custom, L3Build.Marker, "BUILT", "Kneel.Markers.ExitMarker",
                "Box trigger (default 4 x 3 x 4) on Ignore Raycast.",
                root => L3Build.Set(MarkerVolume(root, new Vector3(4f, 3f, 4f)).AddComponent<ExitMarker>(), "destination", "L4"));
            Add("M11", "L3_FadeOnEnter", "Markers", markers, 0f, 0f, 0f, Col.None, L3Build.Unlayered, "BUILT", "Kneel.Markers.FadeOnEnterMarker",
                "A data-only marker component. The farmhouse's roof and south wall carry it directly; this prefab is the same component on an empty. The project's own Kneel.OcclusionFade could do the fading (see Hooks).",
                root => root.AddComponent<FadeOnEnterMarker>());
            Add("M12", "L3_SpawnMarker", "Markers", markers, 0f, 0f, 0f, Col.None, L3Build.Unlayered, "BUILT", "Kneel.Markers.SpawnMarker",
                "An empty with a gizmo, a SpawnMarker and a magenta disc that is hidden in play mode.",
                root =>
                {
                    root.AddComponent<SpawnMarker>();
                    var visual = L3Build.Child(root.transform, "Visual");
                    L3Build.Prim(visual.transform, "Disc", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.9f, 0.01f, 0.9f), M("GB_MarkerSolid"));
                    L3Build.Block(visual.transform, "Facing", new Vector3(0.12f, 0.04f, 0.7f), new Vector3(0f, 0.02f, 0.7f), M("GB_MarkerSolid"));
                    visual.AddComponent<MarkerVisual>();
                });

            // ---- 14.7 Enemies
            list.Add(new Item
            {
                Id = "E01", Name = "AshenFootman", Folder = "Enemies", Table = enemies, Size = new Vector3(1f, 2f, 1f), Collider = Col.Custom, Layer = L3Build.Unlayered,
                Status = "FOUND", Source = "Assets/Enemies/AshenFootman/AshenFootman.prefab",
                Notes = "The existing enemy, used as it is (no L3 copy, no stand-in). It has a NavMeshAgent (radius 0.25, height 1.5) and no collider of its own.",
                ExistingPath = "Assets/Enemies/AshenFootman/AshenFootman.prefab",
            });
            Add("E02", "L3_StandIn_Hound", "Enemies", enemies, 0.8f, 1.4f, 0.8f, Col.Custom, L3Build.Hittable, "BUILT", "Primitives",
                "No hound in the project. Horizontal capsule 1.4 long, 0.8 tall, radius 0.4, on the Enemy layer, dark with a bone-white collar. Collider only.",
                root =>
                {
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.direction = 2;
                    capsule.radius = 0.4f;
                    capsule.height = 1.4f;
                    capsule.center = new Vector3(0f, 0.4f, 0f);
                    L3Build.Prim(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.4f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.8f, 0.7f, 0.8f), M("Hound"));
                    L3Build.Prim(root.transform, "Collar", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0.38f), new Vector3(90f, 0f, 0f), new Vector3(0.84f, 0.05f, 0.84f), M("Bone"));
                });
            Add("E03", "L3_StandIn_Brute", "Enemies", enemies, 2f, 2f, 2.6f, Col.Capsule, L3Build.Hittable, "BUILT", "Primitives; generated ring mesh",
                "No brute in the project. Capsule 2.6 tall, radius 1.0, on the Enemy layer, with a ground ring 4 m in radius as a child. Collider only.",
                root =>
                {
                    L3Build.Prim(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.3f, 0f), Vector3.zero, new Vector3(2f, 1.3f, 2f), M("Brute"));
                    var ring = L3Build.Child(root.transform, "FlailRing", new Vector3(0f, 0.03f, 0f));
                    ring.AddComponent<MeshFilter>().sharedMesh = L3Look.BruteRing;
                    ring.AddComponent<MeshRenderer>().sharedMaterial = M("BruteRing");
                });

            return list;
        }

        private static GameObject L2Prefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(L2Build.PrefabsPath + "/" + name + ".prefab");
        }

        // ---------------------------------------------------------------- Props

        // A field hedgerow the fire has been through: a generated thorn thicket (canes and dead leaves: what the
        // hedge looks like) with a few of the pack's dead trees shrunk to thorn height standing out of the top.
        // Nothing else: no ground of its own, and no core of charred bushes at its foot, which read as lumps of
        // earth under the hedge (owner's review). The thicket is grown thicker instead, so a hedge is still not
        // seen through. 'sparse' is the burnt-thin version with gaps to walk through.
        private static void Hedgerow(Transform t, string name, float length, float depth, float height, int seed, bool sparse)
        {
            var rng = new System.Random(seed);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material charred = M("Adventure_Charred");

            var thicket = L3Build.MeshChild(t, "Thicket", L3Hedges.Thicket(name, length, depth, height, seed, sparse ? 0.4f : 1.5f), Vector3.zero, L3Hedges.Material);
            thicket.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            int stems = Mathf.Max(1, Mathf.RoundToInt(length / (sparse ? 1.0f : 1.6f)));
            for (int i = 0; i < stems; i++)
            {
                float scale = height / 6.57f * Range(0.8f, 0.99f);
                float x = stems == 1 ? Range(-0.3f, 0.3f) : Mathf.Lerp(-length * 0.5f + 0.55f, length * 0.5f - 0.55f, i / (float)(stems - 1)) + Range(-0.12f, 0.12f);
                string stem = rng.Next(2) == 0 ? "A:SM_Env_TreeDead_01" : "A:SM_Env_TreeDead_02";
                float flat = Mathf.Min(0.5f, (depth + 0.2f) / (4.6f * scale));
                L3Build.Seat(t, stem, new Vector3(0f, 180f * rng.Next(2), 0f), new Vector3(scale * 0.8f, scale, scale * flat), new Vector2(x, Range(-0.1f, 0.1f)), -0.05f, charred);
            }
        }

        // The pack's fence panel taken apart into its two posts (west first) and its rails (lowest first).
        private static void FenceParts(out List<L3Mesh> posts, out List<L3Mesh> rails)
        {
            var source = L3Mesh.From(L3Build.Pack("A:SM_Bld_Fence_02").GetComponentInChildren<MeshFilter>().sharedMesh);
            // Islands: triangles that share a corner belong to the same piece of wood.
            var parent = new int[source.P.Count];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            var byPlace = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < source.P.Count; i++)
            {
                var key = Vector3Int.RoundToInt(source.P[i] * 1000f);
                if (byPlace.TryGetValue(key, out int other))
                {
                    parent[Find(i)] = Find(other);
                }
                else
                {
                    byPlace[key] = i;
                }
            }

            var tris = source.Tris[0];
            for (int t = 0; t < tris.Count; t += 3)
            {
                parent[Find(tris[t])] = Find(tris[t + 1]);
                parent[Find(tris[t])] = Find(tris[t + 2]);
            }

            var islands = new Dictionary<int, L3Mesh>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int island = Find(tris[t]);
                if (!islands.TryGetValue(island, out var part))
                {
                    islands[island] = part = new L3Mesh();
                }

                for (int k = 0; k < 3; k++)
                {
                    int i = tris[t + k];
                    part.P.Add(source.P[i]);
                    part.N.Add(source.N[i]);
                    part.UV.Add(source.UV[i]);
                    part.Tris[0].Add(part.P.Count - 1);
                }
            }

            posts = new List<L3Mesh>();
            rails = new List<L3Mesh>();
            foreach (var part in islands.Values)
            {
                (part.Bounds().size.y > 1f ? posts : rails).Add(part);
            }

            posts.Sort((p, q) => p.Bounds().center.x.CompareTo(q.Bounds().center.x));
            rails.Sort((p, q) => p.Bounds().center.y.CompareTo(q.Bounds().center.y));
        }

        // A weathered fence panel in K06's footprint (2 x 0.2 x 1), built from the same posts and rails.
        private static void FenceWorn(GameObject root, int variant)
        {
            FenceParts(out var posts, out var rails);
            // The pack panel is 1.63 long and 1.25 high; K06 stretches it to 2 x 1.
            var fit = Matrix4x4.Scale(new Vector3(2f / 1.63f, 0.8f, 1f));
            var mesh = new L3Mesh();
            Matrix4x4 About(Vector3 pivot, Quaternion turn)
            {
                return Matrix4x4.TRS(pivot, turn, Vector3.one) * Matrix4x4.Translate(-pivot);
            }

            Vector3 Foot(L3Mesh part)
            {
                Bounds b = part.Bounds();
                return fit.MultiplyPoint3x4(new Vector3(b.center.x, 0f, b.center.z));
            }

            // One end of a rail (side -1 west, +1 east), where it is nailed to its post.
            Vector3 End(L3Mesh part, float side)
            {
                Bounds b = part.Bounds();
                return fit.MultiplyPoint3x4(new Vector3(b.center.x + side * (b.extents.x - 0.1f), b.center.y, b.center.z));
            }

            if (variant == 0)
            {
                mesh.Append(posts[0], About(Foot(posts[0]), Quaternion.Euler(-5f, 0f, 4f)) * fit, 0);
                mesh.Append(posts[1], About(Foot(posts[1]), Quaternion.Euler(3f, 0f, -3f)) * fit, 0);
                mesh.Append(rails[0], fit, 0);
                // Hanging by its west end, the other end in the grass.
                Vector3 hinge = End(rails[1], -1f);
                float drop = Mathf.Asin(Mathf.Clamp01((hinge.y - 0.06f) / 1.72f)) * Mathf.Rad2Deg;
                mesh.Append(rails[1], About(hinge, Quaternion.Euler(0f, 4f, -drop)) * fit, 0);
                mesh.Append(rails[2], About(End(rails[2], 1f), Quaternion.Euler(0f, 0f, 2.5f)) * fit, 0);
                // The top rail, off and lying at the fence's foot.
                Bounds top = rails[3].Bounds();
                var lying = Matrix4x4.TRS(new Vector3(0.12f, 0.05f, 0.34f), Quaternion.Euler(84f, 13f, 0f), Vector3.one) * Matrix4x4.Translate(-fit.MultiplyPoint3x4(top.center));
                mesh.Append(rails[3], lying * fit, 0);
            }
            else
            {
                // Pushed over as one, then each part slipped a little of its own accord.
                var over = About(Vector3.zero, Quaternion.Euler(13f, 0f, 0f));
                mesh.Append(posts[0], over * About(Foot(posts[0]), Quaternion.Euler(0f, 0f, 6f)) * fit, 0);
                mesh.Append(posts[1], over * About(Foot(posts[1]), Quaternion.Euler(-4f, 0f, 2f)) * fit, 0);
                mesh.Append(rails[0], over * About(End(rails[0], -1f), Quaternion.Euler(0f, 0f, -3f)) * fit, 0);
                mesh.Append(rails[2], over * fit, 0);
                mesh.Append(rails[3], over * About(End(rails[3], 1f), Quaternion.Euler(0f, 0f, 5f)) * fit, 0);
            }

            var go = L3Build.MeshChild(root.transform, "Fence", L3Build.SaveMesh(mesh, "Kit", root.name), Vector3.zero, M("Adventure_Dawn"));
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // An animal skeleton (see L3Bones) as a child object.
        private static GameObject Bones(Transform parent, string name, Mesh mesh, Vector3 position, float yaw)
        {
            var go = L3Build.MeshChild(parent, name, mesh, position, L3Bones.Materials);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // A faceted stone drum (see L3Look.StoneDrum), 'diameter' across and 'length' long, with its axis
        // turned by 'euler' from upright.
        private static GameObject Drum(Transform parent, string name, Vector3 centre, Vector3 euler, float diameter, float length)
        {
            var go = L3Build.MeshChild(parent, name, L3Look.StoneDrum, centre, M("Palette_Stone"));
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = new Vector3(diameter, length, diameter);
            return go;
        }

        private static void KennelWagon(GameObject root)
        {
            Transform t = root.transform;
            Material iron = M("Iron");
            // The cart, rolled onto its west side.
            L3Build.Seat(t, "A:SM_Prop_Cart_02", new Vector3(0f, 0f, 90f), 1.05f, new Vector2(-0.5f, 0.1f));
            // The cage that sat on its bed: east side, both ends, a top, and one panel torn outward.
            L3Build.Seat(t, "K:SM_Bld_Castle_Iron_Gate_01", new Vector3(0f, 90f, 0f), new Vector3(1.05f, 0.55f, 1f), new Vector2(1.15f, 0.1f), 0f, iron);
            L3Build.Seat(t, "K:SM_Bld_Castle_Iron_Gate_01", Vector3.zero, new Vector3(0.3f, 0.55f, 1f), new Vector2(0.75f, 1.42f), 0f, iron);
            L3Build.Seat(t, "K:SM_Bld_Castle_Iron_Gate_01", new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.55f, 1f), new Vector2(0.75f, -1.22f), 0f, iron);
            L3Build.Seat(t, "K:SM_Bld_Castle_Iron_Gate_01", new Vector3(90f, 90f, 0f), new Vector3(1.05f, 0.27f, 1f), new Vector2(0.78f, 0.1f), 1.58f, iron);
            L3Build.Seat(t, "K:SM_Bld_Castle_Iron_Gate_01", new Vector3(-38f, 90f, 0f), new Vector3(0.45f, 0.5f, 1f), new Vector2(0.95f, -1.5f), 0f, iron);
        }

        private static void PloughTeam(GameObject root)
        {
            Transform t = root.transform;
            // Two oxen, dead where they stood in the yoke and long since picked clean.
            Bones(t, "Ox_North", L3Bones.OxInYoke, new Vector3(0.6f, 0f, 0.5f), 3f);
            Bones(t, "Ox_South", L3Bones.OxInYoke, new Vector3(0.52f, 0f, -0.5f), -2f).transform.localScale = new Vector3(1f, 1f, -1f);
            // The yoke, still across both necks.
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(90f, 0f, 0f), new Vector3(0.75f, 0.78f, 0.75f), new Vector2(1.0f, 0f), 0.34f);
            // The plough behind them: beam, two handles, an iron share, and the chain forward to the yoke.
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 80f), new Vector3(0.7f, 0.62f, 0.7f), new Vector2(-1.1f, 0f), 0.28f);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 40f), new Vector3(0.45f, 0.5f, 0.45f), new Vector2(-1.5f, 0.22f), 0.3f);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 40f), new Vector3(0.45f, 0.5f, 0.45f), new Vector2(-1.5f, -0.22f), 0.3f);
            L3Build.Seat(t, "A:SM_Prop_StoneBlock_01", new Vector3(0f, 0f, -28f), new Vector3(0.8f, 0.4f, 0.6f), new Vector2(-0.72f, 0f), 0.02f, M("Iron"));
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 86f), new Vector3(0.14f, 0.6f, 0.14f), new Vector2(0.25f, 0f), 0.34f, M("Iron"));
        }

        private static void StoneRoller(GameObject root)
        {
            Transform t = root.transform;
            Drum(t, "Roller", new Vector3(0f, 0.6f, 0f), new Vector3(0f, 0f, 90f), 1.2f, 1.56f);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.4f, 0.78f, 0.4f), Centre, 0.55f, M("Iron"));
            // The frame: a beam down each end and the draw bar across the front.
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(90f, 0f, 0f), new Vector3(0.6f, 0.78f, 0.6f), new Vector2(0.9f, 0f), 0.52f);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(90f, 0f, 0f), new Vector3(0.6f, 0.78f, 0.6f), new Vector2(-0.9f, 0f), 0.52f);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.6f, 0.78f, 0.6f), new Vector2(0f, 0.9f), 0.52f);
        }

        private static void SeedDrill(GameObject root)
        {
            Transform t = root.transform;
            L3Build.Seat(t, "A:SM_Prop_Cart_01", new Vector3(0f, 90f, 10f), 0.6f, new Vector2(0f, 0.15f));
            L3Build.Seat(t, "A:SM_Prop_Crate_01", new Vector3(0f, 4f, 6f), new Vector3(1.5f, 0.6f, 0.85f), new Vector2(0f, 0.15f), 0.55f);
            L3Build.Seat(t, "A:SM_Prop_Cart_Wheel_01", new Vector3(0f, 25f, 82f), 0.9f, new Vector2(0.35f, -0.5f));
        }

        private static void Millstones(GameObject root)
        {
            Transform t = root.transform;
            Drum(t, "Stone_0", new Vector3(-0.12f, 0.18f, 0f), Vector3.zero, 1.7f, 0.36f);
            Drum(t, "Stone_1", new Vector3(-0.06f, 0.54f, 0.05f), new Vector3(0f, 25f, 0f), 1.6f, 0.36f);
            Drum(t, "Stone_2", new Vector3(-0.14f, 0.9f, -0.03f), new Vector3(0f, 55f, 0f), 1.65f, 0.36f);
            // The fourth stone, on edge, leaning against the stack.
            Drum(t, "Stone_Leaning", new Vector3(0.76f, 0.52f, 0.1f), new Vector3(0f, 0f, 74f), 1.05f, 0.22f);
        }

        private static void Trough(GameObject root)
        {
            Transform t = root.transform;
            var stone = new L3Mesh();
            void Block(Vector3 size, Vector3 baseCentre, float yaw, int seed)
            {
                stone.Append(L3Build.Kit(L2PolyKit.Tread(size, seed)), Matrix4x4.TRS(baseCentre + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f), Vector3.one), 0);
            }

            // A long basin of dressed stone on a footing, along the south of the footprint.
            Block(new Vector3(2f, 0.14f, 1.1f), new Vector3(0f, 0f, -0.44f), 0f, 1);
            Block(new Vector3(1.92f, 0.62f, 0.22f), new Vector3(0f, 0.14f, -0.86f), 0f, 2);
            Block(new Vector3(1.92f, 0.62f, 0.22f), new Vector3(0f, 0.14f, -0.02f), 0f, 3);
            Block(new Vector3(0.62f, 0.62f, 0.22f), new Vector3(-0.85f, 0.14f, -0.44f), 90f, 4);
            Block(new Vector3(0.62f, 0.62f, 0.22f), new Vector3(0.85f, 0.14f, -0.44f), 90f, 5);
            L3Build.MeshChild(t, "Basin", L3Build.SaveMesh(stone, "Props", "L3_Trough_Basin"), Vector3.zero, M("Palette_Stone"));

            var water = L3Build.MeshChild(t, "Water", L3Look.FlatQuad, new Vector3(0f, 0.6f, -0.44f), L2Build.Mat("L2_Creek_Water") ?? M("GB_Water"));
            water.transform.localScale = new Vector3(1.5f, 1f, 0.64f);
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The yard clutter behind it brings the cover up to 1.2 m.
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", Vector3.zero, 1f, new Vector2(-0.5f, 0.52f));
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(0f, 70f, 0f), 0.86f, new Vector2(0.32f, 0.6f));
            L3Build.Seat(t, "A:SM_Prop_Sack_01", new Vector3(0f, 20f, 0f), 1.25f, new Vector2(0.78f, 0.32f));
            L3Build.Seat(t, "A:SM_Prop_Basket_02", new Vector3(0f, 40f, 0f), 0.9f, new Vector2(-0.72f, 0.02f + 0.3f));
        }

        // ---------------------------------------------------------------- Hazards

        private static void BuildFireCell(GameObject root)
        {
            Transform t = root.transform;
            var area = L3Build.StandingBox(root, new Vector3(2f, 2f, 2f), true);

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(2f, 2f, 2f);
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.carving = true;
            obstacle.enabled = false;

            // Dry: standing wheat on a tilled bed. No collider: it never blocks movement.
            var dry = L3Build.Child(t, "Dry");
            L3Build.MeshChild(dry.transform, "Crop", L3Crops.Standing(0), Vector3.zero, L3Crops.DryMaterial);

            // Smouldering: embers creeping through the ears and a low glow.
            var smoulder = L3Build.Child(t, "Smoulder");
            L2Fire.GroundEmbers(smoulder.transform, new Vector3(0f, 0.75f, 0f), new Vector2(1.8f, 1.8f));
            var sparks = L1Wayfinding.Particles("Sparks", smoulder.transform, L1Build.FxMat("L1_FX_Ember"), 30, 14f, new Vector2(0.8f, 1.6f), new Vector2(0.5f, 1.2f),
                new Vector2(0.04f, 0.08f), new Color(1f, 0.78f, 0.45f, 1f), new Color(1f, 0.35f, 0.1f, 0.8f), -0.05f);
            sparks.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            sparks.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var sparkShape = sparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Box;
            sparkShape.scale = new Vector3(1.8f, 1.8f, 0.05f);
            var glow = L3Build.Child(smoulder.transform, "Glow", new Vector3(0f, 1f, 0f)).AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = L1Build.Hex("#FF8A2E");
            glow.intensity = 1.4f;
            glow.range = 3.5f;
            glow.shadows = LightShadows.None;
            smoulder.SetActive(false);

            // Burning: the same wheat charred black, in the flame effect built for L2, with its light.
            var burning = L3Build.Child(t, "Burning");
            L3Build.MeshChild(burning.transform, "Crop", L3Crops.Standing(0), Vector3.zero, L3Crops.CharredStandingMaterial);
            L2Fire.Fire(burning.transform, "Flames", new Vector3(0f, 0.3f, 0f), 1f, 2.2f, 5.5f, false, new Vector3(1.7f, 0f, 1.7f), L2Fire.Shape.Box, 0.7f);
            burning.SetActive(false);

            // Ash: burnt stubble on a bed of ash. Harmless.
            var ash = L3Build.Child(t, "Ash");
            L3Build.MeshChild(ash.transform, "Stubble", L3Crops.Stubble(0), Vector3.zero, L3Crops.StubbleMaterial);
            ash.SetActive(false);

            var cell = root.AddComponent<FireCell>();
            L3Build.Set(cell, "tuning", L3Look.Fire);
            L3Build.Set(cell, "contact", L3Look.Contact);
            L3Build.Set(cell, "area", area);
            L3Build.Set(cell, "obstacle", obstacle);
            L3Build.Set(cell, "dryVisual", dry);
            L3Build.Set(cell, "smoulderVisual", smoulder);
            L3Build.Set(cell, "burningVisual", burning);
            L3Build.Set(cell, "ashVisual", ash);
        }

        private static void BuildCropRow(GameObject root)
        {
            const int count = 12;
            var cellPrefab = Load("H01");
            var cells = new UnityEngine.Object[count];
            for (int i = 0; i < count; i++)
            {
                var cell = L3Build.Instance(cellPrefab, root.transform, new Vector3(0f, 0f, 1f + 2f * i));
                cell.name = "Cell_" + i.ToString("00");
                cells[i] = cell.GetComponent<FireCell>();
                // No two neighbours share a crop mesh.
                int variant = (i * 2 + i / 3) % L3Crops.Variants;
                cell.transform.Find("Dry/Crop").GetComponent<MeshFilter>().sharedMesh = L3Crops.Standing(variant);
                cell.transform.Find("Burning/Crop").GetComponent<MeshFilter>().sharedMesh = L3Crops.Standing(variant);
                cell.transform.Find("Ash/Stubble").GetComponent<MeshFilter>().sharedMesh = L3Crops.Stubble(variant);
            }

            var row = root.AddComponent<CropRow>();
            L3Build.Set(row, "tuning", L3Look.Fire);
            L3Build.Set(row, "wind", L3Look.Wind);
            L3Build.SetArray(row, "cells", cells);

            var volume = NavArea(root, "CropRow");
            volume.size = new Vector3(2f, 2f, 2f * count);
            volume.center = new Vector3(0f, 0.5f, count);
        }

        // A NavMesh modifier volume on its own child. NavMeshSurface only collects volumes whose object is on
        // one of the layers it bakes, so the child sits on the walkable layer while the hazard stays on Hazard.
        private static NavMeshModifierVolume NavArea(GameObject root, string area)
        {
            var holder = L3Build.Child(root.transform, "NavArea");
            holder.layer = L3Build.Layer(L3Build.Walkable);
            var volume = holder.AddComponent<NavMeshModifierVolume>();
            volume.area = L3Build.NavArea(area);
            return volume;
        }

        private static void BuildBrandStake(GameObject root)
        {
            Transform t = root.transform;
            var post = L3Build.Child(t, "Post");
            L3Build.Fit(post.transform, "K:SM_Prop_Beam_01", 0, new Vector3(0.18f, 1.36f, 0.18f), Centre, 0f, M("Knights_Charred"));
            // The brand: pitch-soaked rag bound round the head of the post with two iron bands, its crown
            // still glowing, a few tails of rag hanging from the lower band.
            var brand = new L3Mesh();
            float[] heights = { 1.0f, 1.1f, 1.26f, 1.39f, 1.455f }, radii = { 0.088f, 0.142f, 0.156f, 0.128f, 0.082f };
            var lean = new[] { Vector3.zero, new Vector3(0.01f, 0f, -0.008f), new Vector3(-0.008f, 0f, 0.012f), new Vector3(0.012f, 0f, 0.006f), new Vector3(0.004f, 0f, -0.004f) };
            for (int k = 0; k + 1 < heights.Length; k++)
            {
                brand.Prism(0, lean[k] + Vector3.up * heights[k], lean[k + 1] + Vector3.up * heights[k + 1], radii[k], radii[k + 1], 6, Vector2.zero);
            }

            // Two bands and four straps between them: the cage that holds the rag to the post.
            brand.Prism(1, new Vector3(0.008f, 1.085f, -0.006f), new Vector3(0.01f, 1.118f, -0.008f), 0.147f, 0.151f, 6, Vector2.zero);
            brand.Prism(1, new Vector3(-0.002f, 1.3f, 0.01f), new Vector3(0.002f, 1.332f, 0.01f), 0.158f, 0.15f, 6, Vector2.zero);
            for (int k = 0; k < 4; k++)
            {
                float angle = (k * 90f + 30f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                brand.Prism(1, outward * 0.152f + Vector3.up * 1.1f, outward * 0.158f + Vector3.up * 1.32f, 0.013f, 0.013f, 4, Vector2.zero);
            }

            // The crown, still alight.
            brand.Prism(2, new Vector3(0.004f, 1.445f, -0.004f), new Vector3(0.014f, 1.5f, 0f), 0.078f, 0.026f, 5, Vector2.zero);
            // Tails of rag hanging below the lower band.
            for (int k = 0; k < 5; k++)
            {
                float angle = (k * 77f + 10f) * Mathf.Deg2Rad, length = 0.16f + 0.06f * (k % 3);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var across = new Vector3(-outward.z, 0f, outward.x) * 0.034f;
                Vector3 from = outward * 0.14f + Vector3.up * 1.09f, to = outward * (0.16f + 0.02f * k) + Vector3.up * (1.09f - length);
                brand.Tri(0, from - across, from + across, to, Vector2.zero);
                brand.Tri(0, from + across, from - across, to, Vector2.zero);
            }

            L3Build.MeshChild(post.transform, "Brand", L3Build.SaveMesh(brand, "Props", "L3_BrandStake_Head"), Vector3.zero, L3Build.Flat("L3_BrandRag", "#4A3626", 0.3f), M("Iron"), M("BrandTip"));
            var tip = L3Build.Child(post.transform, "TipGlow", new Vector3(0f, 1.47f, 0f));
            L2Fire.Fire(tip.transform, "Flame", Vector3.zero, 0.35f, 1.4f, 3.5f, false, default, L2Fire.Shape.Point, 0.6f);

            var hit = L3Build.Child(t, "Hit");
            hit.layer = L3Build.Layer(L3Build.Hittable);
            L3Build.StandingBox(hit, new Vector3(0.3f, 1.5f, 0.3f), true);

            var stake = root.AddComponent<BrandStake>();
            L3Build.Set(stake, "post", post.transform);
            L3Build.Set(stake, "tipGlow", tip);
        }

        private static void BuildMudVolume(GameObject root)
        {
            Transform t = root.transform;
            var area = L3Build.StandingBox(root, new Vector3(4f, 2f, 4f), true);

            Transform Plane(string name, float y, Material material)
            {
                var go = L3Build.Child(t, name, new Vector3(0f, y, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = L3Look.FlatQuad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return go.transform;
            }

            var volume = NavArea(root, "Mud");

            var surface = root.AddComponent<SurfaceVolume>();
            L3Build.Set(surface, "tuning", L3Look.Mud);
            L3Build.Set(surface, "contact", L3Look.Contact);
            L3Build.Set(surface, "area", area);
            L3Build.Set(surface, "ground", Plane("Mud", 0.02f, M("GB_Mud")));
            L3Build.Set(surface, "bandWest", Plane("Damp_West", 0.015f, M("GB_Damp")));
            L3Build.Set(surface, "bandEast", Plane("Damp_East", 0.015f, M("GB_Damp")));
            L3Build.Set(surface, "bandSouth", Plane("Damp_South", 0.015f, M("GB_Damp")));
            L3Build.Set(surface, "bandNorth", Plane("Damp_North", 0.015f, M("GB_Damp")));
            L3Build.Set(surface, "navVolume", volume);
            surface.Configure(new Vector2(4f, 4f), true, true, false, false);
        }

        // Post, crossbar and a sack figure. Returns the crossbar height.
        private static float Gibbet(Transform t)
        {
            const float bar = 2.35f;
            L3Build.Fit(t, "K:SM_Prop_Beam_01", 0, new Vector3(0.3f, 3f, 0.3f), Centre, 0f, M("Knights_Charred"));
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.72f, 0.7f), new Vector2(0f, -0.2f), bar, M("Knights_Charred"));
            // The figure hangs on the south face, so it reads from the camera.
            L3Build.Seat(t, "A:SM_Prop_Sack_01", Vector3.zero, new Vector3(1.25f, 2.5f, 1.2f), new Vector2(0f, -0.38f), 1.2f);
            // A sack for a head, as on the scarecrows at the end of L2.
            L3Build.Seat(t, "A:SM_Prop_Sack_01", new Vector3(8f, 30f, -12f), 0.75f, new Vector2(0f, -0.38f), 2.42f);
            L3Build.Seat(t, "A:SM_Prop_Sack_02", new Vector3(0f, 0f, 78f), new Vector3(1.1f, 1.3f, 1f), new Vector2(-0.52f, -0.38f), bar - 0.12f);
            L3Build.Seat(t, "A:SM_Prop_Sack_02", new Vector3(0f, 0f, -78f), new Vector3(1.1f, 1.3f, 1f), new Vector2(0.52f, -0.38f), bar - 0.12f);
            return bar;
        }

        // A crow hunched on the crossbar, faceted like everything else: body, head, beak, folded wings, a wedge of
        // tail and two legs. Looks along +Z, feet at the origin. Two materials: feathers, and beak and legs.
        private static Mesh Crow()
        {
            var m = new L3Mesh();
            m.Ellipsoid(0, new Vector3(0f, 0.125f, 0f), Quaternion.Euler(-30f, 0f, 0f), new Vector3(0.075f, 0.08f, 0.155f), 6, 3, Vector2.zero);
            m.Ellipsoid(0, new Vector3(0f, 0.245f, 0.105f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.062f), 5, 3, Vector2.zero);
            m.Prism(1, new Vector3(0f, 0.24f, 0.15f), new Vector3(0f, 0.215f, 0.255f), 0.022f, 0.004f, 4, Vector2.zero);
            foreach (float side in new[] { -1f, 1f })
            {
                m.Ellipsoid(0, new Vector3(side * 0.066f, 0.125f, -0.035f), Quaternion.Euler(-36f, side * 8f, side * 12f), new Vector3(0.022f, 0.062f, 0.155f), 5, 2, Vector2.zero);
                m.Prism(1, new Vector3(side * 0.03f, 0.07f, 0.01f), new Vector3(side * 0.036f, 0f, 0.022f), 0.008f, 0.007f, 3, Vector2.zero);
            }

            Vector3 rump = new Vector3(0f, 0.075f, -0.1f), left = new Vector3(-0.055f, 0.005f, -0.27f), right = new Vector3(0.055f, 0.005f, -0.27f), end = new Vector3(0f, -0.015f, -0.31f);
            foreach (var (a, b, c) in new[] { (rump, left, end), (rump, end, right) })
            {
                m.Tri(0, a, b, c, Vector2.zero);
                m.Tri(0, a, c, b, Vector2.zero);
            }

            return L3Build.SaveMesh(m, "Props", "L3_Crow");
        }

        private static void GibbetCrowed(GameObject root)
        {
            Transform t = root.transform;
            float bar = Gibbet(t) + 0.19f;

            Mesh crowMesh = Crow();
            var crows = new UnityEngine.Object[3];
            float[] xs = { -0.62f, 0.2f, 0.7f };
            float[] yaws = { 150f, 200f, 170f };
            for (int i = 0; i < 3; i++)
            {
                var crow = L3Build.Child(t, "Crow_" + i, new Vector3(xs[i], bar, -0.2f), new Vector3(0f, yaws[i], 0f));
                var bird = L3Build.MeshChild(crow.transform, "Bird", crowMesh, Vector3.zero, M("Crow"), M("Iron"));
                bird.transform.localScale = Vector3.one * (i == 1 ? 1.12f : 1f);
                crows[i] = crow.transform;
            }

            var spawn = L3Build.Child(t, "SpawnPoint", new Vector3(0f, 0f, -1.2f), new Vector3(0f, 180f, 0f));

            var hit = L3Build.Child(t, "Hit", new Vector3(0f, 1.1f, -0.38f));
            hit.layer = L3Build.Layer(L3Build.Hittable);
            L3Build.StandingBox(hit, new Vector3(1f, 1.8f, 0.6f), true);

            var ambush = root.AddComponent<GibbetAmbush>();
            L3Build.Set(ambush, "tuning", L3Look.Gibbet);
            L3Build.SetArray(ambush, "crows", crows);
            L3Build.Set(ambush, "spawnPoint", spawn.transform);
        }

        // ---------------------------------------------------------------- Structures

        private static void StoneBridge(GameObject root)
        {
            Transform t = root.transform;
            var deck = L3Build.Child(t, "Deck");
            deck.layer = L3Build.Layer(L3Build.Walkable);
            var deckBox = deck.AddComponent<BoxCollider>();
            deckBox.size = new Vector3(6f, 0.5f, 10f);
            deckBox.center = new Vector3(0f, -0.25f, 0f);
            // Ten courses of worn stone blocks, drawn a centimetre proud so the deck never fights the floors it
            // overlaps at each end.
            var courses = new L3Mesh();
            for (int row = 0; row < 10; row++)
            {
                for (int half = 0; half < 2; half++)
                {
                    courses.Append(L3Build.Kit(L2PolyKit.Tread(new Vector3(3f, 0.5f, 1f), 200 + row * 2 + half)), Matrix4x4.Translate(new Vector3(-1.5f + 3f * half, -0.24f, -4.5f + row)), 0);
                }
            }

            var slab = L3Build.MeshChild(deck.transform, "Courses", L3Build.SaveMesh(courses, "Structures", "L3_Bridge_Deck"), Vector3.zero, M("Palette_Stone"));
            slab.layer = deck.layer;

            foreach (float x in new[] { -2.75f, 2.75f })
            {
                var parapet = L3Build.Child(t, x < 0f ? "Parapet_West" : "Parapet_East", new Vector3(x, 0f, 0f));
                parapet.layer = L3Build.Layer(L3Build.Boundary);
                L3Build.StandingBox(parapet, new Vector3(0.5f, 1f, 10f));
                for (int i = 0; i < 3; i++)
                {
                    var piece = L3Build.Fit(parapet.transform, "K:SM_Bld_Rockwall_Straight_01", 0, new Vector3(0.5f, 1f, 3.36f), new Vector2(0f, -3.33f + 3.33f * i));
                    piece.layer = parapet.layer;
                }
            }

            // Two piers down to the ditch floor (the ditch is 3 m deep).
            var piers = L3Build.Child(t, "Piers");
            L3Build.Fit(piers.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(5.6f, 2.6f, 1.2f), new Vector2(0f, -2.2f), -3f);
            L3Build.Fit(piers.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(5.6f, 2.6f, 1.2f), new Vector2(0f, 2.2f), -3f);
        }

        private static void SluiceFootway(GameObject root)
        {
            Transform t = root.transform;
            var deck = L3Build.Child(t, "Deck");
            deck.layer = L3Build.Layer(L3Build.Walkable);
            var deckBox = deck.AddComponent<BoxCollider>();
            deckBox.size = new Vector3(4f, 0.3f, 10f);
            deckBox.center = new Vector3(0f, -0.15f, 0f);

            // Boards across the walk, from the plank meshes made for L2's bridge.
            var planks = new List<Mesh>();
            foreach (var guid in AssetDatabase.FindAssets("L2_Plank t:Mesh", new[] { L2Build.MeshesPath + "/Detail" }))
            {
                planks.Add(AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            Material wood = L2PolyKit.Wood;
            const int boards = 22;
            float pitch = 10f / boards;
            for (int i = 0; i < boards; i++)
            {
                Mesh mesh = planks[(i * 7) % planks.Count];
                var board = L3Build.Child(deck.transform, "Board_" + i.ToString("00"));
                board.layer = deck.layer;
                board.AddComponent<MeshFilter>().sharedMesh = mesh;
                board.AddComponent<MeshRenderer>().sharedMaterial = wood;
                Bounds b = mesh.bounds;
                board.transform.localScale = new Vector3(4f / b.size.x, 1f, (pitch - 0.02f) / b.size.z);
                board.transform.localPosition = new Vector3(-b.center.x * board.transform.localScale.x, 0.01f - b.max.y, -5f + pitch * (i + 0.5f));
            }

            Material timber = M("Knights_Charred");
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                L3Build.Fit(t, "K:SM_Prop_Beam_01", 0, new Vector3(0.3f, 10f, 0.32f), new Vector2(x, 0f), -5f, timber).transform.localRotation = Quaternion.identity;
                foreach (float z in new[] { -4.2f, 0f, 4.2f })
                {
                    L3Build.Fit(t, "K:SM_Prop_Beam_01", 0, new Vector3(0.3f, 2.7f, 0.3f), new Vector2(x, z), -3f, timber);
                }
            }

            var rail = Load("K06");
            var rails = L3Build.Child(t, "Rails");
            for (int i = 0; i < 5; i++)
            {
                L3Build.Instance(rail, rails.transform, new Vector3(-1.9f, 0f, -4f + 2f * i), 90f);
                L3Build.Instance(rail, rails.transform, new Vector3(1.9f, 0f, -4f + 2f * i), 90f);
            }
        }


        private static void Windmill(GameObject root)
        {
            Transform t = root.transform;
            // A tapering round tower: three tiers and a cap, 14 m to the top.
            L3Build.Fit(t, "K:SM_Bld_Castle_Tower_Round_01", 0, new Vector3(7.6f, 4.6f, 7.6f), Centre);
            L3Build.Fit(t, "K:SM_Bld_Castle_Tower_Round_01", 0, new Vector3(6.7f, 4.2f, 6.7f), Centre, 4.6f);
            L3Build.Fit(t, "K:SM_Bld_Castle_Tower_Round_01", 0, new Vector3(5.8f, 2.8f, 5.8f), Centre, 8.8f);
            // The cap: the pack spire roof, pressed down into a low cone.
            L3Build.Fit(t, "K:SM_Bld_Castle_Roof_Spire_01", 0, new Vector3(6.5f, 2.4f, 6.5f), Centre, 11.6f, M("Knights_Charred"));

            // What the miller stacked against the walls fills the 10 m width.
            L3Build.Seat(t, "A:SM_Prop_Logpile_01", new Vector3(0f, 90f, 0f), 0.8f, new Vector2(-4.15f, 0.6f));
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", Vector3.zero, 1f, new Vector2(-4.3f, -1.3f));
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(0f, 50f, 0f), 0.9f, new Vector2(-4.45f, -2.2f));
            L3Build.Seat(t, "A:SM_Prop_Sack_04", new Vector3(0f, 86f, 0f), 1f, new Vector2(4.3f, 0.6f));
            L3Build.Seat(t, "A:SM_Prop_Sack_04", new Vector3(0f, 94f, 0f), 0.95f, new Vector2(4.25f, 0.5f), 0.45f);
            L3Build.Seat(t, "A:SM_Prop_Crate_01", new Vector3(0f, 8f, 0f), 1.1f, new Vector2(4.35f, -1.2f));
            L3Build.Seat(t, "A:SM_Prop_Crate_02", new Vector3(0f, -14f, 0f), 0.9f, new Vector2(4.4f, -1.25f), 0.85f);
            L3Build.Seat(t, "A:SM_Prop_Cart_Wheel_01", new Vector3(0f, 8f, 14f), 1.1f, new Vector2(4.5f, 2f));

            // The door, barred from inside, on the south face.
            L3Build.Seat(t, "K:SM_Bld_House_Door_01", new Vector3(0f, 180f, 0f), 1.15f, new Vector2(0f, -3.72f), 0f, M("Knights_Charred"));

            // The hub and four sail frames on the south face, stopped where the wind left them.
            const float hubHeight = 9.6f, sailZ = -3.86f;
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(90f, 0f, 0f), 0.95f, new Vector2(0f, -3.45f), hubHeight - 0.42f, M("Knights_Charred"));
            // Each sail: a spar with four lattice boards, baked once from plank meshes.
            var frame = new L3Mesh();
            frame.Tris.Add(new List<int>());
            frame.Append(L3Build.Board(new Vector3(5.4f, 0.14f, 0.2f), 71), Matrix4x4.TRS(new Vector3(2.7f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 1);
            for (int i = 0; i < 4; i++)
            {
                frame.Append(L3Build.Board(new Vector3(3.9f, 0.05f, 0.2f), 72 + i), Matrix4x4.TRS(new Vector3(3.2f, 0.2f + 0.26f * i, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), 0);
            }

            for (int i = 0; i < 6; i++)
            {
                frame.Append(L3Build.Board(new Vector3(1.05f, 0.04f, 0.1f), 80 + i), Matrix4x4.TRS(new Vector3(1.5f + 0.7f * i, 0.55f, -0.03f), Quaternion.Euler(0f, 0f, 90f), Vector3.one), 1);
            }

            Mesh sailMesh = L3Build.SaveMesh(frame, "Structures", "L3_Windmill_Sail");
            foreach (float angle in new[] { 42f, 134f, 222f, 316f })
            {
                var sail = L3Build.MeshChild(t, "Sail_" + angle, sailMesh, new Vector3(0f, hubHeight, sailZ), L2PolyKit.Wood, L2PolyKit.CharredWood);
                sail.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // The stair up the mill mound: worn stone steps between stepped cheek walls, and a landing at the door.
        // Pivot at the middle of the foot, climbing toward +Z.
        public const int StairSteps = 12;
        public const float StairRise = 0.25f, StairGoing = 0.45f, StairWidth = 3.6f, StairCheek = 0.55f, StairLanding = 2f;

        private static void MillStair(GameObject root)
        {
            Transform t = root.transform;
            float top = StairSteps * StairRise, run = StairSteps * StairGoing, landingTop = top + 0.02f;
            var stone = new L3Mesh();
            void Block(Vector3 size, Vector3 centre, float yaw, int seed)
            {
                stone.Append(L3Build.Kit(L2PolyKit.Tread(size, seed)), Matrix4x4.TRS(centre, Quaternion.Euler(0f, yaw, 0f), Vector3.one), 0);
            }

            // The steps: two slabs to a tread, the joint never twice in the same place.
            for (int i = 0; i < StairSteps; i++)
            {
                float split = (i % 3 - 1) * 0.42f, y = (i + 1) * StairRise - (StairRise + 0.03f) * 0.5f, z = (i + 0.5f) * StairGoing;
                float left = StairWidth * 0.5f + split, right = StairWidth - left;
                Block(new Vector3(left, StairRise + 0.03f, StairGoing + 0.05f), new Vector3(-StairWidth * 0.5f + left * 0.5f, y, z), 0f, 400 + i * 2);
                Block(new Vector3(right, StairRise + 0.03f, StairGoing + 0.05f), new Vector3(StairWidth * 0.5f - right * 0.5f, y, z), 0f, 401 + i * 2);
            }

            // The landing: two courses of paving in front of the door.
            for (int i = 0; i < 2; i++)
            {
                float split = i == 0 ? 0.35f : -0.3f, left = StairWidth * 0.5f + split, right = StairWidth - left, z = run + (i + 0.5f) * StairLanding * 0.5f;
                Block(new Vector3(left, 0.3f, StairLanding * 0.5f + 0.02f), new Vector3(-StairWidth * 0.5f + left * 0.5f, landingTop - 0.15f, z), 0f, 440 + i * 2);
                Block(new Vector3(right, 0.3f, StairLanding * 0.5f + 0.02f), new Vector3(StairWidth * 0.5f - right * 0.5f, landingTop - 0.15f, z), 0f, 441 + i * 2);
            }

            // The cheek walls, in half-metre courses that step up with the stair: a metre above the treads.
            // Each two-step stretch of wall is one course higher than the last; the landing's stands 1 m over it.
            const float course = 0.5f, stretch = 2f * StairGoing;
            int stretches = StairSteps / 2;
            float end = run + StairLanding;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (StairWidth + StairCheek) * 0.5f;
                int courses = stretches + 2;
                for (int c = 0; c < courses; c++)
                {
                    float from = Mathf.Max(0, c - 2) * stretch, at = from;
                    int n = 0;
                    while (at < end - 0.01f)
                    {
                        // Running bond: the joints of one course fall on the middles of the next.
                        float length = Mathf.Min(n == 0 && c % 2 == 1 ? 0.7f : 1.25f + 0.2f * ((c + n) % 3), end - at);
                        if (end - at - length < 0.4f)
                        {
                            length = end - at;
                        }

                        Block(new Vector3(length + 0.02f, course + 0.02f, StairCheek), new Vector3(x, (c + 0.5f) * course, at + length * 0.5f), 90f, 500 + c * 20 + n + (side > 0f ? 300 : 0));
                        at += length;
                        n++;
                    }
                }

                for (int k = 0; k <= stretches; k++)
                {
                    bool landing = k == stretches;
                    float length = landing ? StairLanding : stretch, height = (Mathf.Min(k, stretches - 1) + 3) * course;
                    var wall = L3Build.Child(t, (side < 0f ? "Cheek_West_" : "Cheek_East_") + k, new Vector3(x, 0f, landing ? run + length * 0.5f : (k + 0.5f) * stretch));
                    wall.layer = L3Build.Layer(L3Build.Boundary);
                    L3Build.StandingBox(wall, new Vector3(StairCheek, height, length));
                }
            }

            var masonry = L3Build.MeshChild(t, "Masonry", L3Build.SaveMesh(stone, "Structures", "L3_MillStair"), Vector3.zero, M("Palette_Stone"));
            masonry.layer = L3Build.Layer(L3Build.Walkable);

            // What is walked on: one slope through the middle of the treads (the steps are for the eye), and
            // the landing.
            var ramp = L3Build.Child(t, "Ramp");
            ramp.layer = L3Build.Layer(L3Build.Walkable);
            float hw = StairWidth * 0.5f + 0.02f, lead = 0.2f;
            var wedge = new Mesh { name = "L3_MillStair_Ramp" };
            wedge.SetVertices(new List<Vector3>
            {
                new Vector3(-hw, 0f, -lead), new Vector3(hw, 0f, -lead), new Vector3(hw, 0f, run), new Vector3(-hw, 0f, run),
                new Vector3(-hw, landingTop, run), new Vector3(hw, landingTop, run),
            });
            wedge.SetTriangles(new[] { 0, 4, 5, 0, 5, 1, 0, 1, 2, 0, 2, 3, 3, 2, 5, 3, 5, 4, 0, 3, 4, 1, 5, 2 }, 0);
            wedge.RecalculateNormals();
            wedge.RecalculateBounds();
            var collider = ramp.AddComponent<MeshCollider>();
            collider.sharedMesh = L3Build.SaveMesh(wedge, "L3_MillStair_Ramp");
            collider.convex = true;

            var landingObject = L3Build.Child(t, "Landing", new Vector3(0f, landingTop - 0.3f, run + StairLanding * 0.5f));
            landingObject.layer = L3Build.Layer(L3Build.Walkable);
            var box = landingObject.AddComponent<BoxCollider>();
            box.size = new Vector3(StairWidth + 0.04f, 0.3f, StairLanding);
            box.center = new Vector3(0f, 0.15f, 0f);
        }

        // ---------------------------------------------------------------- Markers

        private static void Shrine(GameObject root)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabsPath + "/L1_CheckpointShrine.prefab");
            L3Build.Instance(source, root.transform, Vector3.zero);
            root.AddComponent<ShrineMarker>();

            // A warm column of embers, tall enough to read from a vista 80 m away.
            var column = L1Wayfinding.Particles("EmberColumn", root.transform, L1Build.FxMat("L1_FX_Ember"), 220, 24f, new Vector2(7.5f, 9f), new Vector2(1.3f, 1.6f),
                new Vector2(0.06f, 0.14f), new Color(1f, 0.72f, 0.36f, 1f), new Color(1f, 0.4f, 0.12f, 0.7f), -0.01f);
            column.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            column.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var shape = column.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            var noise = column.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;

            var glow = L3Build.Child(root.transform, "ColumnLight", new Vector3(0f, 4f, 0f)).AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = L1Build.Hex("#FFB25E");
            glow.intensity = 2.5f;
            glow.range = 12f;
            glow.shadows = LightShadows.None;
        }

        private static void BuildSluiceGate(GameObject root)
        {
            Transform t = root.transform;
            // On Default, not Obstacles: the NavMesh bake must not see a shut gate (it carves at runtime instead).
            var blocker = L3Build.Child(t, "Blocker");
            var blockerBox = L3Build.StandingBox(blocker, new Vector3(4f, 2f, 0.3f));

            // The leaf hangs on the west post.
            var leaf = L3Build.Child(t, "Leaf", new Vector3(-2f, 0f, 0f));
            // Eight upright boards and two braces across their south face, baked from plank meshes.
            var boards = new L3Mesh();
            boards.Tris.Add(new List<int>());
            for (int i = 0; i < 8; i++)
            {
                float height = i % 3 == 1 ? 1.94f : 2f;
                var upright = new Matrix4x4();
                upright.SetColumn(0, Vector3.up);
                upright.SetColumn(1, Vector3.forward);
                upright.SetColumn(2, Vector3.right);
                upright.SetColumn(3, new Vector4(0.25f + 0.5f * i, height * 0.5f, 0f, 1f));
                boards.Append(L3Build.Board(new Vector3(height, 0.11f, 0.48f), 300 + i), upright, 0);
            }

            foreach (float y in new[] { 0.45f, 1.5f })
            {
                var across = new Matrix4x4();
                across.SetColumn(0, Vector3.right);
                across.SetColumn(1, Vector3.back);
                across.SetColumn(2, Vector3.up);
                across.SetColumn(3, new Vector4(2f, y, -0.09f, 1f));
                boards.Append(L3Build.Board(new Vector3(3.9f, 0.07f, 0.17f), 310 + (int)(y * 10f)), across, 1);
            }

            L3Build.MeshChild(leaf.transform, "Boards", L3Build.SaveMesh(boards, "Structures", "L3_SluiceGate_Leaf"), Vector3.zero, L2PolyKit.Wood, L2PolyKit.CharredWood);

            L3Build.Fit(t, "K:SM_Prop_Beam_01", 0, new Vector3(0.3f, 2.3f, 0.3f), new Vector2(-2.15f, 0f), 0f, M("Knights_Charred")).name = "Post_West";
            L3Build.Fit(t, "K:SM_Prop_Beam_01", 0, new Vector3(0.3f, 2.3f, 0.3f), new Vector2(2.15f, 0f), 0f, M("Knights_Charred")).name = "Post_East";

            // The bar, on the +Z face only.
            var bar = L3Build.Child(t, "Bar");
            L3Build.Seat(bar.transform, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.62f, 1.8f, 0.62f), new Vector2(0f, 0.2f), 0.87f);

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(4f, 2f, 0.6f);
            obstacle.center = new Vector3(0f, 1f, 0f);
            obstacle.carving = true;
            obstacle.enabled = false;

            var promptObject = L3Build.Child(t, "Prompt", new Vector3(0f, 2.8f, 0f));
            var prompt = promptObject.AddComponent<TextMeshPro>();
            prompt.text = Hazards.SluiceGate.BarredPrompt;
            prompt.fontSize = 3.2f;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.color = new Color(1f, 0.93f, 0.8f);
            prompt.rectTransform.sizeDelta = new Vector2(9f, 1.2f);
            prompt.textWrappingMode = TextWrappingModes.NoWrap;

            var gate = root.AddComponent<Hazards.SluiceGate>();
            L3Build.Set(gate, "blocker", blockerBox);
            L3Build.Set(gate, "leaf", leaf.transform);
            L3Build.Set(gate, "bar", bar);
            L3Build.Set(gate, "obstacle", obstacle);
            L3Build.Set(gate, "prompt", prompt);
        }

        private static void SecretTellPrefab(GameObject root)
        {
            // Unity's quad faces -Z; turned about so the print faces the prefab's +Z.
            var quad = L3Build.Prim(root.transform, "Handprint", PrimitiveType.Quad, new Vector3(0f, 1.2f, 0f), new Vector3(0f, 180f, 0f), new Vector3(0.3f, 0.3f, 1f), M("Handprint"));
            quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var post = L3Build.Child(root.transform, "Post", new Vector3(0f, 0f, -0.12f));
            L3Build.Fit(post.transform, "K:SM_Prop_Beam_01", 0, new Vector3(0.22f, 1.6f, 0.22f), Centre, 0f, M("Knights_Charred"));
            post.SetActive(false);
            L3Build.Set(root.AddComponent<SecretTell>(), "handprint", quad.GetComponent<MeshRenderer>());
        }

        private static void Pickup(GameObject root, PickupKind kind, float intensity, float range, Action<GameObject> visual)
        {
            visual(root);
            L3Build.Set(root.AddComponent<PickupMarker>(), "kind", kind);

            var glow = L3Build.Child(root.transform, "Glow", new Vector3(0f, 0.45f, 0f));
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = L1Build.Hex(kind == PickupKind.HealCapacity ? "#FFD27A" : "#FFC58A");
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;

            float size = kind == PickupKind.HealCapacity ? 1.1f : kind == PickupKind.Tonal ? 0.35f : 0.6f;
            var halo = L1Wayfinding.Particles("Halo", glow.transform, L1Build.FxMat("L1_FX_Glow"), 3, 1.4f, new Vector2(1.6f, 2f), Vector2.zero,
                new Vector2(size, size * 1.25f), new Color(1f, 0.75f, 0.4f, 0.22f), new Color(1f, 0.6f, 0.25f, 0.16f));
            halo.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.3f;
            if (kind == PickupKind.HealCapacity)
            {
                var motes = L1Wayfinding.Particles("Motes", glow.transform, L1Build.FxMat("L1_FX_Grace"), 24, 7f, new Vector2(1.6f, 2.6f), new Vector2(0.15f, 0.35f),
                    new Vector2(0.03f, 0.06f), new Color(1f, 0.9f, 0.6f, 1f), new Color(1f, 0.75f, 0.35f, 0.6f), -0.02f);
                motes.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var shape = motes.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.4f;
            }
        }

        // A box trigger with a see-through magenta box drawn to match (hidden in play mode).
        private static GameObject MarkerVolume(GameObject root, Vector3 size)
        {
            L3Build.StandingBox(root, size, true);
            var visual = L3Build.Block(root.transform, "Visual", size, Vector3.zero, M("GB_Marker"));
            visual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visual.AddComponent<MarkerVisual>();
            return root;
        }

        // Resizes a marker volume (L3_VistaTrigger, L3_ArenaCamVolume, L3_ExitTrigger) or a ditch kill volume,
        // keeping its box drawn to match. size is (x, height, z).
        public static void SizeVolume(GameObject instance, Vector3 size)
        {
            var box = instance.GetComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            var visual = instance.transform.Find("Visual");
            if (visual != null)
            {
                visual.localScale = size;
                visual.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            }
        }
    }
}
