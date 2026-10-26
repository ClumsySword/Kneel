using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The cover, props and dressing figures of the L4 manifest (14.3). Cover stands 1.2-1.8 m: it blocks bolts
    // and hounds but never hides the player.
    public static partial class L4Assets
    {
        private const string Corpses = "Assets/Kneel/Levels/L1/Prefabs/Corpses/";

        // The dead are bones now (owner, 2026-10-19: "replace soldiers with skeletons. This stronghold has been
        // overtaken by the evil master long ago"): L2's generated skeletons, with the kneeling and hanging poses.
        public const string Skeletons = "Assets/Kneel/Prefabs/L2/Skeletons/L2_Skeleton_";
        private const string KnightsCharacters = "Assets/SyntyStudios/PolygonKnights/Prefabs/Characters/";

        private static Material Wood => L2PolyKit.Wood;
        private static Material Charred => L2PolyKit.CharredWood;
        private static Vector2 WoodCell(int cell) => L4Kit.Cell(cell);
        private static Vector2 PropCell(int cell) => L4Kit.Cell(cell);

        private static void PropRows(AddRow Add)
        {
            const string props = "14.3 Cover and props";
            const string cover = L3Build.Cover;

            Add("C01", "L4_Mantlet", "Props", props, 3f, 1f, 1.8f, Col.Box, cover, "BUILT", "L2 planks (L2PolyKit.Plank)",
                "No mantlet in the packs. A wall of upright planks on two skids, braced behind and leaning back, with crossbow bolts stuck in its face.",
                Mantlet);
            Add("C02", "L4_DeadHorse", "Props", props, 3f, 2f, 1.2f, Col.Box, cover, "BUILT", "L3 ox skeleton (L3Bones) as the horse; PolygonAdventure SM_Prop_Cart_01",
                "No horse in the packs, and the owner wants carcasses as bare skeletons: the L3 quadruped skeleton lying in its shafts, the supply cart it pulled tipped behind it for the height.",
                DeadHorse);
            Add("C03", "L4_SupplyCart", "Props", props, 3f, 2f, 1.5f, Col.Box, cover, "ADAPTED", "PolygonAdventure SM_Prop_Cart_01, sacks, barrel, crate",
                "Open cart turned east-west, loaded with sacks, a barrel and a crate.",
                SupplyCart);
            Add("C04a", "L4_Tumbril_Turned", "Props", props, 2f, 3f, 1.5f, Col.Box, cover, "ADAPTED", "PolygonKnights SM_Prop_Cart_01; PolygonKnights Character_Soldier_01/02 baked laid out (L1Bakers.BakeStanding); generated canvas shroud; TextMeshPro chalk",
                "An execution cart with two of the condemned in royal colours laid head to foot under a canvas shroud, TURNED chalked on its tail board.",
                root => Tumbril(root, "TURNED", 1));
            Add("C04b", "L4_Tumbril_Traitor", "Props", props, 2f, 3f, 1.5f, Col.Box, cover, "ADAPTED", "PolygonKnights SM_Prop_Cart_01; PolygonKnights Character_Soldier_01/02 baked laid out (L1Bakers.BakeStanding); generated canvas shroud; TextMeshPro chalk",
                "The same cart chalked TRAITOR. The bodies in the two carts look exactly alike (section 11, vignette 4).",
                root => Tumbril(root, "TRAITOR", 2));
            Add("C05", "L4_Well", "Props", props, 2f, 2f, 1.2f, Col.Cylinder, cover, "FOUND", "L3_Well (PolygonKnights SM_Bld_Village_Well_01)",
                "L3's well, re-graded to L4's stone.",
                root => Nest(root, "Assets/Kneel/Prefabs/L3/Props/L3_Well.prefab", Vector3.zero, 0f));
            Add("C06", "L4_CoffinStack", "Props", props, 3.6f, 2.6f, 1.8f, Col.Box, cover, "BUILT", "Generated coffins (L4Shapes.CoffinShell) in L2's wood palette; L1 Vanguard corpse",
                "No coffin in the packs. Joiner-made six-sided coffins (hollow box, shaped lid with a raised panel, iron handles and nails): two side by side, one on top with its lid knocked aside, one leaning on the stack, one open on the ground with its dead in it.",
                CoffinStack);
            Add("C07", "L4_Ballista", "Props", props, 3f, 3f, 1.6f, Col.Box, cover, "BUILT", "PolygonKnights SM_Prop_Beam_01; L2 planks; generated cord and bolt",
                "No ballista in the packs. A beam stock on a plank turntable, bow arms and cord, a bolt laid in the groove; aimed toward -Z, down into the yard (section 11, vignette 5).",
                Ballista);
            Add("C08", "L4_Cauldron", "Props", props, 1.5f, 1.5f, 1.2f, Col.Cylinder, cover, "BUILT", "Generated iron cauldron (L4Shapes.Lathe) on L4Kit stones",
                "No cauldron in the packs. A faceted iron cauldron on three short legs in a ring of hearth stones, cold pitch skinned over inside.",
                Cauldron);
            Add("C09", "L4_Font", "Props", props, 2f, 2f, 1.2f, Col.Cylinder, cover, "BUILT", "Generated stone font (L4Shapes.Lathe) in the L4 stone palette",
                "No font in the packs. An eight-sided stone basin on a stepped pedestal.",
                Font);
            Add("C10", "L4_FallenBell", "Props", props, 4f, 4f, 2f, Col.Custom, cover, "BUILT", "Generated bronze bell (L4Shapes.Lathe); PolygonKnights SM_Prop_Beam_01 (headstock)",
                "No bell in the packs. The great bell lying on its side, split down one flank, its burnt headstock beside it. Collider round the bell only.",
                FallenBell);
            Add("C11", "L4_ToppledStatue", "Props", props, 4f, 2f, 1.5f, Col.Box, cover, "ADAPTED", "PolygonKnights SM_Prop_Statue_01, SM_Prop_Statue_Base_01",
                "The pack statue lying face down off its broken base.",
                ToppledStatue);
            Add("C12", "L4_BrokenStatue", "Props", props, 2f, 4.8f, 2.2f, Col.Box, cover, "ADAPTED", "PolygonKnights SM_Prop_Statue_01 (cut with L3Mesh.Clip, capped with L4Shapes.CapCut), SM_Prop_Statue_Base_01",
                "The bronze statue broken at the thighs: its legs still on the pedestal, the rest of it on its back at the pedestal's foot, sword arm flung out, with fallen stones.",
                BrokenStatue);
            Add("C13", "L4_Wagon", "Props", props, 3f, 2f, 1.5f, Col.Box, cover, "ADAPTED", "PolygonAdventure SM_Prop_Cart_02, barrels",
                "A covered-frame wagon turned east-west with barrels.",
                Wagon);
            Add("C14", "L4_FallenPortcullis", "Props", props, 8f, 1.2f, 0.6f, Col.Box, cover, "ADAPTED", "PolygonKnights SM_Bld_Castle_Iron_Gate_01 (three panels)",
                "A buckled portcullis grid lying along a wall, three panels kinked against each other.",
                FallenPortcullis);
            Add("C15a", "L4_Barricade_Out", "Props", props, 8f, 2f, 1.6f, Col.Box, cover, "BUILT", "PolygonAdventure carts, crates; PolygonKnights beams (stakes)",
                "The outer barricade of the gatehouse passage, facing the causeway: an overturned cart, crates and sharpened stakes.",
                root => Barricade(root, false));
            Add("C15b", "L4_Barricade_In", "Props", props, 8f, 2f, 1.6f, Col.Custom, cover, "BUILT", "PolygonAdventure carts, crates; PolygonKnights beams (stakes)",
                "The inner barricade, built against the garrison's own gate guard, with the 4 m gap the Husk holds. Colliders either side of the gap.",
                root => Barricade(root, true));
            Add("C16", "L4_Ram", "Props", props, 10f, 2f, 2f, Col.None, cover, "FOUND", "L1_BatteringRam",
                "L1's battering ram, buried in the burnt gate.",
                root => Nest(root, "Assets/Kneel/Levels/L1/Prefabs/L1_BatteringRam.prefab", Vector3.zero, 0f));
            Add("C17", "L4_RefugeeCart", "Props", props, 2f, 3f, 1.5f, Col.Box, cover, "FOUND", "L3_GraveCart",
                "L3's household cart, turned back toward the causeway.",
                root => Nest(root, "Assets/Kneel/Prefabs/L3/Props/L3_GraveCart.prefab", Vector3.zero, 0f));
            Add("C18", "L4_Pew", "Props", props, 4f, 1f, 1f, Col.Box, cover, "BUILT", "L2 planks",
                "No pew in the packs. Seat, back and three end boards; one end has given way.",
                Pew);
            Add("C19", "L4_TreasureChest", "Props", props, 1.2f, 0.7f, 0.7f, Col.Box, cover, "ADAPTED", "PolygonAdventure SM_Prop_Chest_01; generated coin pile",
                "The pack chest, open, its coin spilled at its foot.",
                TreasureChest);
            Add("C20", "L4_OilJars", "Props", props, 2f, 1f, 1f, Col.Box, cover, "BUILT", "Generated clay jars (L4Shapes.Lathe); PolygonAdventure barrel",
                "Fat clay oil jars stoppered with pitch, and a barrel, stencilled for the causeway defences.",
                OilJars);
            Add("C21", "L4_WeaponRack", "Props", props, 2f, 0.6f, 1.8f, Col.Box, cover, "BUILT", "PolygonKnights SM_Prop_Beam_01; pack weapons",
                "A beam rack with swords and axes; two slots empty.",
                WeaponRack);
            Add("C22a", "L4_Kennel", "Props", props, 4f, 3f, 2.5f, Col.Custom, L3Build.Boundary, "BUILT", "L4Kit stone courses; PolygonKnights SM_Bld_Castle_Iron_Gate_01; L2 planks",
                "An open stone bay in a side wall, its iron grate swung back, a chain on the floor. Opens toward -Z. Colliders on the walls.",
                root => Kennel(root, false));
            Add("C22b", "L4_Kennel_DeadHound", "Props", props, 4f, 3f, 2.5f, Col.Custom, L3Build.Boundary, "BUILT", "As C22a; L3_Carcass",
                "As C22a with a dead hound's skeleton in it (the east kennel of E6c).",
                root => Kennel(root, true));
            Add("C23", "L4_ExecutionBlock", "Props", props, 1f, 1f, 0.5f, Col.Box, cover, "BUILT", "L4Kit stone block; PolygonAdventure SM_Wep_Axe_01",
                "A squat block of dressed stone, its top darkened, the headsman's axe left in it.",
                ExecutionBlock);
            Add("C24", "L4_SiegeCampRuin", "Props", props, 20f, 14f, 6f, Col.None, L3Build.Unlayered, "ADAPTED", "PolygonKnights SM_Bld_Tent_01; L1_TentWreck, L1_SiegeTowerWreck_A",
                "The neighbours' broken siege camp, seen across the ravine from V3b. Backdrop: no collider.",
                SiegeCamp);
        }

        private static void FigureRows(AddRow Add)
        {
            const string figures = "14.3 Figures";
            const string none = L3Build.Unlayered;
            Add("F01a", "L4_Kneeler_Slumped_Soldier", "Figures", figures, 1f, 1.4f, 1.1f, Col.None, none, "ADAPTED", "L2_Skeleton_Kneeling; PolygonKnights SM_Wep_Shield_01",
                "The dead who knelt, long ago: a skeleton on its knees, bowed forward, its shield fallen beside it.", root =>
                {
                    Nest(root, Skeletons + "Kneeling.prefab", Vector3.zero, 0f);
                    L3Build.Seat(root.transform, "K:SM_Wep_Shield_01", new Vector3(-84f, 30f, 0f), 0.9f, new Vector2(0.5f, 0.15f), 0f, Stone);
                });
            Add("F01b", "L4_Kneeler_Slumped_Knight", "Figures", figures, 1f, 1.4f, 1.1f, Col.None, none, "ADAPTED", "L2_Skeleton_Kneeling; PolygonKnights SM_Wep_Broadsword_01",
                "As F01a, its sword planted point-down before it.", root =>
                {
                    Nest(root, Skeletons + "Kneeling.prefab", Vector3.zero, 0f);
                    var sword = L3Build.Part(root.transform, "K:SM_Wep_Broadsword_01", new Vector3(0.05f, 0.9f, 0.72f), new Vector3(180f, 90f, 4f), Vector3.one, Stone);
                    sword.name = "PlantedSword";
                });
            Add("F02", "L4_Kneeler_Upright", "Figures", figures, 1f, 1.2f, 1.4f, Col.None, none, "ADAPTED", "L2_Skeleton_Kneeling",
                "Kneeling (decision 8): one of the bowed dead, so the ambush hides among them. The ambush wraps this.", root => Nest(root, Skeletons + "Kneeling.prefab", Vector3.zero, 0f));
            Add("F03a", "L4_Hanged_Soldier", "Figures", figures, 0.8f, 0.8f, 2.4f, Col.None, none, "BUILT", "L2_Skeleton_Hanging; generated rope",
                "A hanged skeleton, long dead. Origin at the beam: the rope runs down 0.6 m to the noose.", root => Hanged(root, false));
            Add("F03b", "L4_Hanged_Captain", "Figures", figures, 0.8f, 0.8f, 2.4f, Col.None, none, "BUILT", "L2_Skeleton_Hanging; generated rope and sash",
                "The tribunal's captain on the last noose, bones now, a pale sash across his ribs (vignette 4).", root => Hanged(root, true));
            Add("F04", "L4_GateCaptain", "Figures", figures, 2f, 2.2f, 0.6f, Col.None, none, "ADAPTED", "L2_Skeleton_Supine; PolygonKnights SM_Wep_Broadsword_01 (as a two-handed blade)",
                "The gate guard's captain on his back just inside the breach, his heavy blade across him: the heavy-weapon pickup lies here (decision 6).", GateCaptain);
            Add("F05", "L4_Cook", "Figures", figures, 1f, 1.4f, 1.1f, Col.None, none, "FOUND", "L2_Skeleton_Slumped",
                "The cook's bones, slumped among his oil jars on the hoarding (vignette 3).", root => Nest(root, Skeletons + "Slumped.prefab", Vector3.zero, 0f));
            Add("F06", "L4_Gatewarden", "Figures", figures, 2.6f, 1.6f, 2.4f, Col.None, none, "BUILT", "L2_Skeleton_Slumped; L4Kit wall; generated rings, chain links, manacles",
                "The gatewarden's skeleton slumped against his cell wall, still in the chains run down from two rings (D3).", Gatewarden);
            Add("F07", "L4_DeadScout", "Figures", figures, 2f, 1f, 0.6f, Col.None, none, "FOUND", "L2_Skeleton_Curled",
                "The neighbours' scout, curled where he wrote (D2), long dead.", root => Nest(root, Skeletons + "Curled.prefab", Vector3.zero, 0f));
        }

        // ---------------------------------------------------------------- Helpers

        private static GameObject Nest(GameObject root, string path, Vector3 position, float yaw, float scale = 1f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                throw new ArgumentException("Missing prefab " + path);
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static GameObject Plank(Transform parent, Vector3 size, Vector3 centre, Quaternion rot, int seed, bool charred = false)
        {
            var go = L3Build.MeshChild(parent, "Plank", L2PolyKit.Plank(size, seed), centre, charred ? Charred : Wood);
            go.transform.localRotation = rot;
            return go;
        }

        // A plank standing upright: its length (x) up, width (z) along +x, thickness (y) along z.
        private static readonly Quaternion Upright = Quaternion.LookRotation(Vector3.right, Vector3.forward);

        private static GameObject Shapes(Transform parent, string name, Material material, Action<L3Mesh> build)
        {
            var m = new L3Mesh();
            build(m);
            return L3Build.MeshChild(parent, name, m.ToMesh(name), Vector3.zero, material);
        }

        // A board seen from both sides: L2's plank is a deck board, open underneath, so two are laid back to back.
        // size: length along x, thickness y, width z.
        private static GameObject Board(Transform parent, Vector3 size, Vector3 centre, Quaternion rot, int seed, bool charred = false)
        {
            var holder = L3Build.Child(parent, "Board", centre).transform;
            holder.localRotation = rot;
            var half = new Vector3(size.x, size.y * 0.5f, size.z);
            var mat = charred ? Charred : Wood;
            L3Build.MeshChild(holder, "Face", L2PolyKit.Plank(half, seed), new Vector3(0f, size.y * 0.25f, 0f), mat);
            var back = L3Build.MeshChild(holder, "Back", L2PolyKit.Plank(half, seed + 7), new Vector3(0f, -size.y * 0.25f, 0f), mat);
            back.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            return holder.gameObject;
        }

        // A board standing on edge, its face toward 'facing', running along 'along' (both horizontal).
        private static Quaternion OnEdge(Vector3 along, Vector3 facing)
        {
            return Quaternion.LookRotation(Vector3.Cross(along, facing), facing);
        }

        // ---------------------------------------------------------------- Builders

        private static void Mantlet(GameObject root)
        {
            var t = root.transform;
            var panel = L3Build.Child(t, "Panel", new Vector3(0f, 0f, -0.1f), new Vector3(-12f, 0f, 0f)).transform;
            var rng = new System.Random(201);
            for (int i = 0; i < 9; i++)
            {
                float x = -1.32f + i * 0.33f;
                float h = 1.72f + (float)rng.NextDouble() * 0.12f;
                Board(panel, new Vector3(h, 0.08f, 0.31f), new Vector3(x, h * 0.5f, 0f), Upright, rng.Next(100000), rng.NextDouble() < 0.25);
            }

            foreach (float y in new[] { 0.45f, 1.35f })
            {
                Board(panel, new Vector3(2.9f, 0.07f, 0.18f), new Vector3(0f, y, 0.09f), OnEdge(Vector3.right, Vector3.forward), rng.Next(100000));
            }

            foreach (float x in new[] { -1.1f, 1.1f })
            {
                Board(t, new Vector3(0.9f, 0.09f, 0.16f), new Vector3(x, 0.05f, 0.25f), Quaternion.Euler(0f, 90f, 0f), rng.Next(100000));
                L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(55f, 0f, 0f), new Vector3(0.6f, 0.7f, 0.6f), new Vector2(x, 0.45f), 0f, Wood);
            }

            // Bolts in the face.
            Shapes(panel, "Bolts", L4Shapes.Props, m =>
            {
                for (int i = 0; i < 6; i++)
                {
                    var at = new Vector3(-1.2f + (float)rng.NextDouble() * 2.4f, 0.4f + (float)rng.NextDouble() * 1.2f, -0.05f);
                    var dir = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, ((float)rng.NextDouble() - 0.2f) * 0.3f, -1f).normalized;
                    L4Shapes.Shaft(m, 0, at, at + dir * 0.45f, 0.014f, 0.014f, 5, PropCell(L4Shapes.Rope));
                    L4Shapes.Shaft(m, 0, at + dir * 0.36f, at + dir * 0.47f, 0.035f, 0.005f, 3, PropCell(L4Shapes.Clay));
                }
            });
        }

        private static void DeadHorse(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "A:SM_Prop_Cart_01", new Vector3(0f, 90f, 24f), 0.9f, new Vector2(0.2f, 0.45f), 0f, AdvSoot);
            var bones = L3Build.MeshChild(t, "Skeleton", L3Bones.OxOnSide, new Vector3(-0.2f, 0.12f, -0.45f), L3Bones.Materials);
            bones.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (float x in new[] { -0.45f, 0.35f })
            {
                L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 84f), new Vector3(0.5f, 0.9f, 0.5f), new Vector2(-0.4f, x - 0.1f), 0f, Charred);
            }
        }

        private static void SupplyCart(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "A:SM_Prop_Cart_01", new Vector3(0f, 90f, 0f), 0.92f, new Vector2(0f, 0.25f), 0f, AdvStone);
            L3Build.Seat(t, "A:SM_Prop_Sack_04", new Vector3(0f, 14f, 0f), 1f, new Vector2(-0.6f, -0.65f), 0f, AdvStone);
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", Vector3.zero, 0.95f, new Vector2(0.55f, -0.62f), 0f, AdvStone);
            L3Build.Seat(t, "A:SM_Prop_Crate_01", new Vector3(0f, 21f, 0f), 0.85f, new Vector2(1.15f, -0.55f), 0f, AdvStone);
        }

        private static void Tumbril(GameObject root, string chalk, int seed)
        {
            var t = root.transform;
            var cart = L3Build.Seat(t, "K:SM_Prop_Cart_01", Vector3.zero, 0.95f, Centre, 0f, Soot);
            var cartBounds = L3Build.LocalBounds(cart, t);
            var rng = new System.Random(seed);

            // Where the bed is: the highest surface straight down the cart's middle, below the rails.
            var probes = new List<MeshCollider>();
            foreach (var f in cart.GetComponentsInChildren<MeshFilter>())
            {
                var c = f.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = f.sharedMesh;
                probes.Add(c);
            }

            Physics.SyncTransforms();
            // Rays only count what belongs to this cart (the scene it is built in may hold other colliders).
            bool Cast(Vector3 local, out Vector3 point)
            {
                var hits = Physics.RaycastAll(t.TransformPoint(local), Vector3.down, 4f);
                System.Array.Sort(hits, (h1, h2) => h1.distance.CompareTo(h2.distance));
                foreach (var h in hits)
                {
                    if (h.collider is MeshCollider mc && probes.Contains(mc))
                    {
                        point = t.InverseTransformPoint(h.point);
                        return true;
                    }
                }

                point = Vector3.zero;
                return false;
            }

            float bed = 0.55f;
            if (Cast(new Vector3(0f, 3f, 0f), out var bedPoint))
            {
                bed = bedPoint.y;
            }

            // The bed's extent: how far rays down the middle still land on its floor (a gap between planks is allowed).
            bool OnBed(float x, float z) => Cast(new Vector3(x, 3f, z), out var p) && Mathf.Abs(p.y - bed) < 0.06f;
            float Reach(float x, float z, float dx, float dz, int steps)
            {
                float last = 0f;
                int misses = 0;
                for (int s = 1; s <= steps && misses < 3; s++)
                {
                    if (OnBed(x + dx * s, z + dz * s))
                    {
                        last = s;
                        misses = 0;
                    }
                    else
                    {
                        misses++;
                    }
                }

                return last;
            }

            float bz0 = -Reach(0f, 0f, 0f, -0.05f, 40) * 0.05f, bz1 = Reach(0f, 0f, 0f, 0.05f, 40) * 0.05f;
            float bmid = (bz0 + bz1) * 0.5f, bx = Reach(0f, bmid, 0.03f, 0f, 30) * 0.03f;

            // Two of the condemned laid out side by side, head to foot, arms at their sides.
            string[] bodies = { "Soldier_01", "Soldier_02" };
            for (int i = 0; i < 2; i++)
            {
                var mesh = Laid(bodies[(i + seed) % 2]);
                var body = L3Build.MeshChild(t, "Body", mesh, Vector3.zero, L1Build.Mat("L1_Regiment_Vanguard"));
                body.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 0f : 180f, 0f) * Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 6f, 0f);
                body.transform.localScale = Vector3.one * 0.94f;
                var b = L3Build.LocalBounds(body, t);
                float x = (i == 0 ? -1f : 1f) * Mathf.Max(0.2f, bx * 0.48f);
                body.transform.localPosition += new Vector3(x - b.center.x, bed + 0.01f - b.min.y, bmid + (i == 0 ? 0.04f : -0.04f) - b.center.z);
                var c = body.AddComponent<MeshCollider>();
                c.sharedMesh = mesh;
                probes.Add(c);
            }

            // A canvas shroud over the middle of them: one head and one pair of boots show at each end. It rests
            // on what is under it a hand's width around (so nothing pokes through), sags between, and is tucked
            // in along the rails.
            Physics.SyncTransforms();
            const int nx = 16, nz = 22;
            float x0 = -bx + 0.03f, x1 = bx - 0.03f, z0 = bz0 + 0.42f, z1 = bz1 - 0.3f;
            var rest = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int k = 0; k <= nz; k++)
                {
                    float x = Mathf.Lerp(x0, x1, (float)i / nx), z = Mathf.Lerp(z0, z1, (float)k / nz);
                    float y = bed + 0.03f;
                    for (int a = 0; a < 9; a++)
                    {
                        var o = a == 0 ? Vector2.zero : new Vector2(Mathf.Cos(a * Mathf.PI / 4f), Mathf.Sin(a * Mathf.PI / 4f)) * 0.09f;
                        if (Cast(new Vector3(x + o.x, 3f, z + o.y), out var point))
                        {
                            y = Mathf.Max(y, point.y + 0.03f);
                        }
                    }

                    rest[i, k] = y;
                }
            }

            var heights = (float[,])rest.Clone();
            for (int pass = 0; pass < 3; pass++)
            {
                var next = (float[,])heights.Clone();
                for (int i = 1; i < nx; i++)
                {
                    for (int k = 1; k < nz; k++)
                    {
                        float avg = (heights[i - 1, k] + heights[i + 1, k] + heights[i, k - 1] + heights[i, k + 1]) * 0.25f;
                        next[i, k] = Mathf.Max(rest[i, k], Mathf.Lerp(heights[i, k], avg, 0.5f));
                    }
                }

                heights = next;
            }

            var sheet = new L3Mesh();
            Vector3 V(int i, int k)
            {
                float y = heights[i, k] + Mathf.PerlinNoise(i * 0.5f + seed, k * 0.5f) * 0.015f;
                if (i == 0 || i == nx)
                {
                    y = bed + 0.06f;
                }
                else if (k == 0 || k == nz)
                {
                    y -= 0.03f;
                }

                return new Vector3(Mathf.Lerp(x0, x1, (float)i / nx), y, Mathf.Lerp(z0, z1, (float)k / nz));
            }

            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    var a = V(i, k);
                    var b = V(i + 1, k);
                    var c = V(i + 1, k + 1);
                    var d = V(i, k + 1);
                    L4Shapes.Quad(sheet, 0, a, b, c, d, Vector3.up, Vector2.one * 0.5f);
                    L4Shapes.Quad(sheet, 0, a, b, c, d, Vector3.down, Vector2.one * 0.5f);
                }
            }

            L3Build.MeshChild(t, "Sheet", sheet.ToMesh("TumbrilSheet"), Vector3.zero, L4Build.Flat("L4_Canvas", "#2F2A24", 0.03f));
            Debug.Log("Tumbril bed z " + bz0 + ".." + bz1 + " half width " + bx + " floor " + bed);
            foreach (var c in probes)
            {
                UnityEngine.Object.DestroyImmediate(c);
            }

            var text = new GameObject("Chalk");
            text.transform.SetParent(t, false);
            text.transform.localPosition = new Vector3(0f, bed + 0.12f, cartBounds.min.z - 0.03f);
            text.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            var tmp = text.AddComponent<TMPro.TextMeshPro>();
            tmp.text = chalk;
            tmp.fontSize = 2.2f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = new Color(0.78f, 0.76f, 0.7f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(1.6f, 0.4f);
        }

        private static void CoffinStack(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(211);
            // Two side by side across the stack, one on top with its lid knocked aside.
            Coffin(t, new Vector3(-0.3f, 0f, -0.36f), 90f, 0f, rng, false, false);
            Coffin(t, new Vector3(-0.32f, 0f, 0.36f), 88f, 0f, rng, false, true);
            Coffin(t, new Vector3(-0.25f, 0.48f, 0.02f), 93f, 0f, rng, true, false);
            // One leaning on the stack's front edge (its foot on the ground), and one on the ground with its lid
            // off and its dead in it.
            Coffin(t, new Vector3(0.15f, 0f, -0.13f), 0f, -50f, rng, false, false);
            Coffin(t, new Vector3(1.22f, 0f, 0.1f), 2f, 0f, rng, false, true, true);
        }

        // A coffin as a joiner made it: the six-sided box (narrow foot, broad shoulders) on a skirt, a shaped lid
        // with a raised panel, board joints scored down its sides, iron handles and nails, all in old dark wood.
        // 'askew' slides the lid off a little; 'open' leaves it on the ground beside the box and puts the dead in it.
        private static void Coffin(Transform parent, Vector3 at, float yaw, float lean, System.Random rng, bool askew, bool charred, bool open = false)
        {
            const float length = 1.95f, shoulder = 0.64f, height = 0.4f;
            var c = L3Build.Child(parent, "Coffin", at, new Vector3(lean, yaw, 0f)).transform;
            var wood = charred ? Charred : Wood;
            Vector2 side = WoodCell(L2PolyKit.Side), top = WoodCell(L2PolyKit.Top), dark = WoodCell(L2PolyKit.TopDark), joint = WoodCell(L2PolyKit.Joint), edge = WoodCell(L2PolyKit.End);
            Shapes(c, "Box", wood, m =>
            {
                L4Shapes.Coffin(m, 0, Matrix4x4.Scale(new Vector3(1.05f, 1f, 1.03f)), length, shoulder, 0.05f, edge, edge);
                L4Shapes.CoffinShell(m, 0, Matrix4x4.Translate(new Vector3(0f, 0.04f, 0f)), length, shoulder, height - 0.04f, 0.045f, side, dark, top);
                // Board joints scored along the sides.
                var outline = L4Shapes.CoffinOutline(length, shoulder);
                for (int i = 0; i < outline.Length; i++)
                {
                    var a = outline[i];
                    var b = outline[(i + 1) % outline.Length];
                    var mid = (a + b) * 0.5f;
                    var along = new Vector3(b.x - a.x, 0f, b.y - a.y);
                    var outward = new Vector3(mid.x, 0f, mid.y).normalized;
                    var rot = Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up), Vector3.up);
                    L4Shapes.Box(m, 0, new Vector3(mid.x, height * 0.55f, mid.y) + outward * 0.004f, rot, new Vector3(along.magnitude * 0.98f, 0.012f, 0.012f), joint);
                }
            });

            void Lid(Transform holder)
            {
                Shapes(holder, "Lid", wood, m =>
                {
                    L4Shapes.Coffin(m, 0, Matrix4x4.Scale(new Vector3(1.06f, 1f, 1.04f)), length, shoulder, 0.05f, edge, top);
                    L4Shapes.Coffin(m, 0, Matrix4x4.TRS(new Vector3(0f, 0.05f, 0.02f), Quaternion.identity, new Vector3(0.74f, 1f, 0.84f)), length, shoulder, 0.03f, dark, top);
                    foreach (float x in new[] { -0.11f, 0.11f })
                    {
                        L4Shapes.Box(m, 0, new Vector3(x, 0.081f, 0.02f), Quaternion.identity, new Vector3(0.012f, 0.008f, length * 0.8f), joint);
                    }
                });
                Shapes(holder, "Nails", L4Shapes.Props, m =>
                {
                    for (int k = 0; k < 6; k++)
                    {
                        float z = -length * 0.42f + k * length * 0.168f;
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            float half = z < length * 0.22f ? Mathf.Lerp(shoulder * 0.29f, shoulder * 0.5f, Mathf.InverseLerp(-length * 0.5f, length * 0.22f, z)) : shoulder * 0.45f;
                            L4Shapes.Box(m, 0, new Vector3(sx * (half - 0.03f), 0.052f, z), Quaternion.identity, new Vector3(0.022f, 0.006f, 0.022f), PropCell(L4Shapes.IronDark));
                        }
                    }
                });
            }

            if (open)
            {
                var lid = L3Build.Child(c, "LidOff", new Vector3(shoulder + 0.12f, 0.03f, 0.05f), new Vector3(0f, -10f, 7f)).transform;
                Lid(lid);
                var body = Nest(c.gameObject, Corpses + "L1_Corpse_Vanguard_Soldier_01_OnBack.prefab", Vector3.zero, 0f, 0.84f);
                var b = L3Build.LocalBounds(body, c);
                if (b.size.x > b.size.z)
                {
                    body.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    b = L3Build.LocalBounds(body, c);
                }

                body.transform.localPosition += new Vector3(-b.center.x, 0.1f - b.min.y, -b.center.z);
            }
            else
            {
                var lid = L3Build.Child(c, "Lid", new Vector3(askew ? 0.08f : 0f, height, askew ? 0.14f : 0f), new Vector3(0f, askew ? 8f : 0f, 0f)).transform;
                Lid(lid);
            }

            // Iron handles, two on each long side.
            Shapes(c, "Handles", L4Shapes.Props, m =>
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { -0.35f, 0.25f })
                    {
                        float half = z < length * 0.22f ? Mathf.Lerp(shoulder * 0.29f, shoulder * 0.5f, Mathf.InverseLerp(-length * 0.5f, length * 0.22f, z)) : shoulder * 0.45f;
                        var at2 = new Vector3(sx * (half + 0.015f), height * 0.62f, z);
                        L4Shapes.Box(m, 0, at2, Quaternion.identity, new Vector3(0.02f, 0.06f, 0.06f), PropCell(L4Shapes.IronDark));
                        L4Shapes.Shaft(m, 0, at2 + new Vector3(sx * 0.03f, -0.02f, -0.06f), at2 + new Vector3(sx * 0.03f, -0.02f, 0.06f), 0.012f, 0.012f, 4, PropCell(L4Shapes.Iron));
                    }
                }
            });

            // Leaning coffins rest on their foot.
            if (lean != 0f)
            {
                c.localPosition += c.localRotation * new Vector3(0f, 0f, length * 0.5f) - new Vector3(0f, 0f, length * 0.5f);
            }
        }

        private static void Ballista(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(207);
            for (int i = 0; i < 5; i++)
            {
                Plank(t, new Vector3(2.2f, 0.08f, 0.42f), new Vector3(0f, 0.32f, -0.88f + i * 0.44f), Quaternion.identity, rng.Next(100000), i == 4);
            }

            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1f, 0.95f, 1f), new Vector2(0f, -0.5f), 0f, Wood);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(1f, 0.95f, 1f), new Vector2(0f, 0.5f), 0f, Wood);
            // The stock, aimed toward -Z and tipped down into the yard, on an upright.
            L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(1.1f, 0.35f, 1.1f), new Vector2(0f, 0.2f), 0.36f, Wood);
            var stock = L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(-80f, 0f, 0f), new Vector3(1f, 1.05f, 1f), new Vector2(0f, -0.1f), 0.95f, Wood);
            Shapes(t, "BowAndCord", L4Shapes.Props, m =>
            {
                var front = new Vector3(0f, 1.18f, -1.25f);
                var left = front + new Vector3(-1.15f, 0.08f, 0.55f);
                var right = front + new Vector3(1.15f, 0.08f, 0.55f);
                L4Shapes.Shaft(m, 0, front + Vector3.left * 0.1f, left, 0.08f, 0.05f, 6, PropCell(L4Shapes.Rope));
                L4Shapes.Shaft(m, 0, front + Vector3.right * 0.1f, right, 0.08f, 0.05f, 6, PropCell(L4Shapes.Rope));
                var nock = new Vector3(0f, 1.25f, 0.45f);
                L4Shapes.Shaft(m, 0, left, nock, 0.015f, 0.015f, 4, PropCell(L4Shapes.Rope));
                L4Shapes.Shaft(m, 0, right, nock, 0.015f, 0.015f, 4, PropCell(L4Shapes.Rope));
                L4Shapes.Shaft(m, 0, nock, new Vector3(0f, 1.2f, -1.45f), 0.03f, 0.03f, 5, PropCell(L4Shapes.Clay));
                L4Shapes.Shaft(m, 0, new Vector3(0f, 1.2f, -1.4f), new Vector3(0f, 1.19f, -1.62f), 0.06f, 0.005f, 4, PropCell(L4Shapes.Iron));
            });
            stock.name = "Stock";
        }

        private static void Cauldron(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(208);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                var block = L3Build.MeshChild(t, "Hearth", L2PolyKit.Tread(new Vector3(0.5f, 0.3f, 0.32f), rng.Next(100000)), new Vector3(Mathf.Cos(a) * 0.62f, 0.15f, Mathf.Sin(a) * 0.62f), L4Kit.Stone);
                block.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }

            Shapes(t, "Cauldron", L4Shapes.Props, m =>
            {
                var profile = new[]
                {
                    new Vector2(0f, 0.32f), new Vector2(0.32f, 0.34f), new Vector2(0.55f, 0.48f), new Vector2(0.66f, 0.72f), new Vector2(0.64f, 0.98f),
                    new Vector2(0.7f, 1.04f), new Vector2(0.7f, 1.1f), new Vector2(0.6f, 1.1f), new Vector2(0.58f, 0.98f), new Vector2(0.56f, 0.8f),
                };
                L4Shapes.Lathe(m, 0, Matrix4x4.identity, profile, 12, PropCell(L4Shapes.Iron));
                // The cold pitch inside, skinned over.
                L4Shapes.Lathe(m, 0, Matrix4x4.identity, new[] { new Vector2(0.57f, 0.8f), new Vector2(0f, 0.8f) }, 12, PropCell(L4Shapes.Pitch));
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2f / 3f + 0.3f;
                    var foot = new Vector3(Mathf.Cos(a) * 0.38f, 0f, Mathf.Sin(a) * 0.38f);
                    L4Shapes.Shaft(m, 0, foot, foot * 0.8f + Vector3.up * 0.42f, 0.06f, 0.08f, 5, PropCell(L4Shapes.IronDark));
                }
            });
        }

        private static void Font(GameObject root)
        {
            Shapes(root.transform, "Font", L4Kit.Stone, m =>
            {
                var profile = new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.72f, 0f), new Vector2(0.72f, 0.18f), new Vector2(0.48f, 0.24f), new Vector2(0.38f, 0.32f),
                    new Vector2(0.34f, 0.7f), new Vector2(0.62f, 0.8f), new Vector2(0.86f, 1.05f), new Vector2(0.88f, 1.2f), new Vector2(0.74f, 1.2f),
                    new Vector2(0.66f, 1.02f), new Vector2(0f, 0.98f),
                };
                L4Shapes.Lathe(m, 0, Matrix4x4.identity, profile, 8, L4Kit.Cell(L2PolyKit.Side), -1, Mathf.PI / 8f);
            });
        }

        private static void FallenBell(GameObject root)
        {
            var t = root.transform;
            // Lathe about y, then laid on its side along x; scaled so the mouth is 2 m across.
            var frame = Matrix4x4.TRS(new Vector3(-0.2f, 1f, -0.3f), Quaternion.Euler(0f, 18f, 92f), Vector3.one * 0.74f);
            Shapes(t, "Bell", L4Shapes.Props, m =>
            {
                var outside = new[]
                {
                    new Vector2(1.18f, -0.9f), new Vector2(1.35f, -0.86f), new Vector2(1.3f, -0.62f), new Vector2(1.08f, -0.2f), new Vector2(0.94f, 0.4f),
                    new Vector2(0.88f, 0.86f), new Vector2(0.72f, 1.08f), new Vector2(0.3f, 1.18f), new Vector2(0f, 1.2f),
                };
                var inside = new[]
                {
                    new Vector2(0f, 1.0f), new Vector2(0.62f, 0.92f), new Vector2(0.78f, 0.6f), new Vector2(0.92f, -0.1f), new Vector2(1.08f, -0.62f), new Vector2(1.18f, -0.9f),
                };
                L4Shapes.Lathe(m, 0, frame, outside, 18, PropCell(L4Shapes.Bronze), 4);
                L4Shapes.Lathe(m, 0, frame, inside, 18, PropCell(L4Shapes.BronzeDark), 4);
            });
            var bounds = new Vector3(2.1f, 2.0f, 2.6f);
            var col = L3Build.Child(t, "BellCollider", new Vector3(-0.2f, 0f, -0.3f), new Vector3(0f, 18f, 0f));
            L3Build.StandingBox(col, bounds);
            var head = L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 32f, 88f), new Vector3(1.6f, 0.95f, 1.6f), new Vector2(0.9f, 1.3f), 0f, Charred);
            head.name = "Headstock";
            Shapes(t, "BurntRope", L4Shapes.Props, m =>
            {
                L4Shapes.Shaft(m, 0, new Vector3(0.7f, 0.05f, 1.0f), new Vector3(1.7f, 0.04f, 0.4f), 0.035f, 0.03f, 5, PropCell(L4Shapes.Rope));
                L4Shapes.Shaft(m, 0, new Vector3(1.7f, 0.04f, 0.4f), new Vector3(1.8f, 0.03f, -0.4f), 0.03f, 0.012f, 5, PropCell(L4Shapes.Pitch));
            });
        }

        private static void ToppledStatue(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "K:SM_Prop_Statue_Base_01", new Vector3(0f, 0f, 9f), 0.9f, new Vector2(1.45f, 0.1f), -0.25f, Stone);
            L3Build.Seat(t, "K:SM_Prop_Statue_01", new Vector3(0f, 0f, 86f), 0.74f, new Vector2(-0.6f, 0f), 0f, L4Build.Mat("L4_Knights_Bronze"));
        }

        // The bronze knight snapped at the knees on its pedestal, the rest of him face down at its foot. Both breaks
        // are capped with rough faceted metal, so neither half shows the inside of its shell.
        private static void BrokenStatue(GameObject root)
        {
            var t = root.transform;
            var bronze = L4Build.Mat("L4_Knights_Bronze");
            var pedestal = L3Build.Seat(t, "K:SM_Prop_Statue_Base_01", new Vector3(0f, 12f, 0f), new Vector3(0.82f, 0.55f, 0.82f), new Vector2(0f, 0.9f), 0f, Stone);
            float top = L3Build.LocalBounds(pedestal, t).max.y;
            var source = L3Build.Pack("K:SM_Prop_Statue_01").GetComponentInChildren<MeshFilter>();
            var full = L3Mesh.From(source.sharedMesh);
            float cut = full.Bounds().min.y + 1.45f;
            var lowerMesh = L4Shapes.KeepGrounded(full.Clip(1, cut, true), full.Bounds().min.y, 2f, 0.72f);
            L4Shapes.CapCut(lowerMesh, 0, cut, true, lowerMesh.UV.Count > 0 ? lowerMesh.UV[0] : Vector2.one * 0.5f, 5);
            var upperMesh = full.Clip(1, cut, false);
            L4Shapes.CapCut(upperMesh, 0, cut, false, upperMesh.UV.Count > 0 ? upperMesh.UV[0] : Vector2.one * 0.5f, 6);
            var lower = L3Build.MeshChild(t, "Standing", lowerMesh.ToMesh("StatueLower"), Vector3.zero, bronze);
            var upper = L3Build.MeshChild(t, "Fallen", upperMesh.ToMesh("StatueUpper"), Vector3.zero, bronze);
            var lb = L3Build.LocalBounds(lower, t);
            lower.transform.localPosition = new Vector3(-lb.center.x, top - 0.02f - lb.min.y, 0.9f - lb.center.z);
            // On his back in front of the pedestal (-Z), the broken end toward it, head away, sword arm flung out.
            upper.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 10f);
            var ub = L3Build.LocalBounds(upper, t);
            upper.transform.localPosition = new Vector3(0.2f - ub.center.x, -0.04f - ub.min.y, -0.25f - ub.max.z);
            var rng = new System.Random(219);
            for (int i = 0; i < 4; i++)
            {
                L4Kit.Fallen(t, new Vector3(((float)rng.NextDouble() - 0.5f) * 2f, 0f, 0.2f + ((float)rng.NextDouble() - 0.5f) * 0.6f), rng).transform.localScale = Vector3.one * 0.55f;
            }
        }

        private static void Wagon(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "A:SM_Prop_Cart_02", new Vector3(0f, 90f, 0f), 0.9f, new Vector2(0f, 0.2f), 0f, AdvStone);
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(0f, 30f, 0f), 0.95f, new Vector2(-0.9f, -0.62f), 0f, AdvStone);
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", new Vector3(80f, 0f, 20f), 0.9f, new Vector2(0.4f, -0.68f), 0f, AdvSoot);
        }

        private static void FallenPortcullis(GameObject root)
        {
            var t = root.transform;
            var iron = L4Build.Mat("L4_Iron");
            float[] tilt = { 9f, -14f, 6f };
            for (int i = 0; i < 3; i++)
            {
                var panel = L3Build.Part(t, "K:SM_Bld_Castle_Iron_Gate_01", Vector3.zero, new Vector3(90f + tilt[i], 0f, (i - 1) * 7f), new Vector3(1.07f, 0.42f, 1f), iron);
                var b = L3Build.LocalBounds(panel, t);
                panel.transform.localPosition += new Vector3(-2.67f + i * 2.67f - b.center.x, -b.min.y, -b.center.z);
            }
        }

        private static void Barricade(GameObject root, bool gap)
        {
            var t = root.transform;
            var rng = new System.Random(gap ? 215 : 216);
            float[] xs = gap ? new[] { -3f, 3f } : new[] { -2.2f, 1.8f };
            foreach (float x in xs)
            {
                L3Build.Seat(t, "A:SM_Prop_Cart_02", new Vector3(0f, 90f + (float)rng.NextDouble() * 12f, 82f), 0.8f, new Vector2(x, 0.25f), 0f, AdvSoot);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = gap ? (i < 2 ? -3.4f + i * 1.3f : 2.1f + (i - 2) * 1.3f) : -3.4f + i * 2.2f;
                L3Build.Seat(t, "A:SM_Prop_Crate_01", new Vector3(0f, rng.Next(360), 0f), 0.9f + (float)rng.NextDouble() * 0.2f, new Vector2(x, -0.55f), 0f, AdvStone);
            }

            // Sharpened stakes leaning out toward -Z (the side the attack came from).
            for (float x = -3.8f; x <= 3.8f; x += 0.65f)
            {
                if (gap && Mathf.Abs(x) < 2f)
                {
                    continue;
                }

                L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(-48f, (float)rng.NextDouble() * 16f - 8f, 0f), new Vector3(0.55f, 0.75f, 0.55f), new Vector2(x, -0.4f), 0f, Charred);
            }

            if (gap)
            {
                foreach (float x in new[] { -3f, 3f })
                {
                    var c = L3Build.Child(t, "Collider", new Vector3(x, 0f, 0f));
                    L3Build.StandingBox(c, new Vector3(2f, 1.6f, 2f));
                }
            }
        }

        private static void Pew(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(218);
            Board(t, new Vector3(4f, 0.07f, 0.42f), new Vector3(0f, 0.46f, 0f), Quaternion.Euler(0f, 0f, -3f), rng.Next(100000));
            Board(t, new Vector3(4f, 0.06f, 0.5f), new Vector3(0f, 0.78f, 0.24f), Quaternion.Euler(-80f, 0f, -3f), rng.Next(100000));
            foreach (float x in new[] { -1.9f, 0f })
            {
                Board(t, new Vector3(0.95f, 0.07f, 0.5f), new Vector3(x, 0.47f, 0.02f), Upright, rng.Next(100000));
            }

            // The far end has given way.
            Board(t, new Vector3(0.9f, 0.07f, 0.5f), new Vector3(1.92f, 0.3f, 0.12f), Quaternion.Euler(0f, 90f, 70f), rng.Next(100000), true);
        }

        private static void TreasureChest(GameObject root)
        {
            var t = root.transform;
            L3Build.Seat(t, "A:SM_Prop_Chest_01", Vector3.zero, 1f, new Vector2(0f, 0.05f), 0f, AdvStone);
            var rng = new System.Random(219);
            Shapes(t, "Coin", L4Shapes.Props, m =>
            {
                L4Shapes.Lathe(m, 0, Matrix4x4.TRS(new Vector3(0.2f, 0f, -0.42f), Quaternion.identity, Vector3.one), new[] { new Vector2(0.28f, 0f), new Vector2(0.16f, 0.07f), new Vector2(0f, 0.1f) }, 9, PropCell(L4Shapes.Gold));
                for (int i = 0; i < 16; i++)
                {
                    var at = new Vector3(-0.5f + (float)rng.NextDouble() * 1.1f, 0.006f, -0.65f + (float)rng.NextDouble() * 0.35f);
                    L4Shapes.Shaft(m, 0, at, at + new Vector3(0.002f, 0.008f, 0f), 0.025f, 0.025f, 7, PropCell(L4Shapes.Gold));
                }
            });
        }

        private static void OilJars(GameObject root)
        {
            var t = root.transform;
            var jar = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.27f, 0.22f), new Vector2(0.3f, 0.45f), new Vector2(0.24f, 0.68f),
                new Vector2(0.11f, 0.78f), new Vector2(0.1f, 0.88f), new Vector2(0.06f, 0.9f), new Vector2(0f, 0.9f),
            };
            Shapes(t, "Jars", L4Shapes.Props, m =>
            {
                var spots = new[] { new Vector3(-0.65f, 0f, -0.15f), new Vector3(-0.05f, 0f, 0.18f), new Vector3(0.12f, 0f, -0.25f), new Vector3(0.75f, 0.3f, 0.05f) };
                for (int i = 0; i < spots.Length; i++)
                {
                    var frame = i == 3 ? Matrix4x4.TRS(spots[i], Quaternion.Euler(0f, 30f, 90f), Vector3.one * 0.95f) : Matrix4x4.TRS(spots[i], Quaternion.Euler(0f, i * 40f, 0f), Vector3.one * (0.95f + i * 0.05f));
                    L4Shapes.Lathe(m, 0, frame, jar, 9, PropCell(L4Shapes.Clay));
                    L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(0.075f, 0.86f), new Vector2(0.06f, 0.94f), new Vector2(0f, 0.95f) }, 7, PropCell(L4Shapes.Pitch));
                }
            });
            L3Build.Seat(t, "A:SM_Prop_Barrel_01", Vector3.zero, 0.85f, new Vector2(0.62f, 0.22f), 0f, AdvSoot);
        }

        private static void WeaponRack(GameObject root)
        {
            var t = root.transform;
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                L3Build.Seat(t, "K:SM_Prop_Beam_01", Vector3.zero, new Vector3(0.6f, 0.72f, 0.6f), new Vector2(x, 0.1f), 0f, Wood);
            }

            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.5f, 0.78f, 0.5f), new Vector2(0f, 0.1f), 1.55f, Wood);
            L3Build.Seat(t, "K:SM_Prop_Beam_01", new Vector3(0f, 0f, 90f), new Vector3(0.5f, 0.78f, 0.5f), new Vector2(0f, 0.1f), 0.45f, Wood);
            var weapons = new[] { "A:SM_Wep_Sword_01", "K:SM_Wep_Broadsword_01", "A:SM_Wep_Axe_01", "A:SM_Wep_Sword_01" };
            float[] at = { -0.6f, -0.25f, 0.35f, 0.65f };
            for (int i = 0; i < weapons.Length; i++)
            {
                L3Build.Seat(t, weapons[i], new Vector3(-12f, 0f, (i % 2 == 0 ? 4f : -5f)), weapons[i].Contains("Broad") ? 1.5f : 1.1f, new Vector2(at[i], -0.05f), 0.05f, AdvStone);
            }

            L3Build.Seat(t, "A:SM_Wep_GreatAxe_01", new Vector3(-20f, 0f, 14f), 0.9f, new Vector2(-1.05f, -0.2f), 0f, AdvStone);
        }

        private static void Kennel(GameObject root, bool deadHound)
        {
            var t = root.transform;
            var rng = new System.Random(deadHound ? 222 : 221);
            // Back wall at +Z, side walls, a lintel over the open front at -Z.
            L4Kit.Courses(t, new Vector3(-2f, 0f, 1.5f), Vector3.right, Vector3.forward, 4f, 2.5f, rng, 0.1f, 0.5f);
            foreach (float x in new[] { -2f, 1.5f })
            {
                L4Kit.Courses(t, new Vector3(x, 0f, -1.5f), Vector3.forward, Vector3.left, 2.6f, 2.5f, rng, 0.1f, 0.5f);
            }

            var lintel = L3Build.Child(t, "Lintel", new Vector3(0f, 2.05f, 0f)).transform;
            L4Kit.Courses(lintel, new Vector3(-2f, 0f, -1.5f), Vector3.right, Vector3.back, 4f, 0.45f, rng, 0f, 0.5f);
            // The grate, swung back against the side wall.
            var grate = L3Build.Part(t, "K:SM_Bld_Castle_Iron_Gate_01", Vector3.zero, new Vector3(0f, 98f, 0f), new Vector3(1.2f, 0.7f, 1f), L4Build.Mat("L4_Iron"));
            var gb = L3Build.LocalBounds(grate, t);
            grate.transform.localPosition += new Vector3(-1.45f - gb.center.x, -gb.min.y, -0.2f - gb.center.z);
            Shapes(t, "Chain", L4Shapes.Props, m =>
            {
                var a = new Vector3(0.2f, 0.9f, 1.2f);
                var b = new Vector3(0.3f, 0.03f, 0.4f);
                var c = new Vector3(-0.4f, 0.03f, -0.4f);
                L4Shapes.Shaft(m, 0, a, b, 0.03f, 0.03f, 4, PropCell(L4Shapes.IronDark));
                L4Shapes.Shaft(m, 0, b, c, 0.03f, 0.03f, 4, PropCell(L4Shapes.IronDark));
            });
            for (int i = 0; i < 3; i++)
            {
                Plank(t, new Vector3(1.4f, 0.04f, 0.3f), new Vector3(-0.6f + i * 0.5f, 0.02f, 0.6f), Quaternion.Euler(0f, 20f + i * 30f, 0f), rng.Next(100000), true);
            }

            if (deadHound)
            {
                Nest(root, "Assets/Kneel/Prefabs/L3/Props/L3_Carcass.prefab", new Vector3(0.2f, 0f, 0.3f), 160f, 0.55f);
            }

            foreach (var (x, z, sx, sz) in new[] { (0f, 1.25f, 4f, 0.5f), (-1.75f, 0f, 0.5f, 3f), (1.75f, 0f, 0.5f, 3f) })
            {
                var c = L3Build.Child(t, "Wall", new Vector3(x, 0f, z));
                L3Build.StandingBox(c, new Vector3(sx, 2.5f, sz));
            }
        }

        private static void ExecutionBlock(GameObject root)
        {
            var t = root.transform;
            var block = L3Build.MeshChild(t, "Block", L2PolyKit.Tread(new Vector3(0.95f, 0.48f, 0.7f), 223), new Vector3(0f, 0.24f, 0f), L4Kit.Stone);
            block.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);
            Shapes(t, "Stain", L4Shapes.Props, m => L4Shapes.Box(m, 0, new Vector3(0.05f, 0.485f, 0f), Quaternion.Euler(0f, 6f, 0f), new Vector3(0.62f, 0.01f, 0.5f), PropCell(L4Shapes.Pitch)));
            L3Build.Seat(t, "A:SM_Wep_Axe_01", new Vector3(0f, 70f, -38f), 1.25f, new Vector2(0.25f, 0.05f), 0.36f, AdvSoot);
        }

        private static void SiegeCamp(GameObject root)
        {
            var t = root.transform;
            Nest(root, "Assets/Kneel/Levels/L1/Prefabs/L1_TentWreck.prefab", new Vector3(-5f, 0f, 2f), 20f);
            Nest(root, "Assets/Kneel/Levels/L1/Prefabs/L1_TentWreck.prefab", new Vector3(4f, 0f, -3f), -35f, 0.9f);
            Nest(root, "Assets/Kneel/Levels/L1/Prefabs/L1_SiegeTowerWreck_A.prefab", new Vector3(5f, 0f, 4f), 70f);
            L3Build.Seat(t, "K:SM_Bld_Tent_01", new Vector3(0f, 10f, 4f), 1f, new Vector2(-7f, -4f), 0f, Soot);
        }

        // ---------------------------------------------------------------- Figures

        private static void GateCaptain(GameObject root)
        {
            Nest(root, Skeletons + "Supine.prefab", Vector3.zero, 90f);
            var blade = L3Build.Seat(root.transform, "K:SM_Wep_Broadsword_01", new Vector3(90f, 0f, 30f), 2.1f, new Vector2(0.1f, 0f), 0.22f, Stone);
            blade.name = "HeavyBlade";
        }

        // The gatewarden in his cell (D3): chained to the wall long ago, his bones slid down it, the chains run slack
        // from the rings to his wrists.
        private static void Gatewarden(GameObject root)
        {
            var t = root.transform;
            var rng = new System.Random(231);
            // The piece of cell wall he is chained to (his back against its -Z face).
            L4Kit.Courses(t, new Vector3(-1.3f, 0f, 0.35f), Vector3.right, Vector3.back, 2.6f, 2.4f, rng, 0.3f, 0.5f, 2f);
            // His bones slid down the wall in the chains: turned so his back is to the wall (+Z), hands in his lap.
            var bones = Nest(root, Skeletons + "Slumped.prefab", new Vector3(0f, 0f, 0.22f), 180f);
            Vector3 handL = bones.transform.localPosition + bones.transform.localRotation * new Vector3(-0.28f, 0.06f, 0.14f);
            Vector3 handR = bones.transform.localPosition + bones.transform.localRotation * new Vector3(0.24f, 0.14f, 0.24f);
            Shapes(t, "Chains", L4Shapes.Props, m =>
            {
                foreach (var hand in new[] { handL, handR })
                {
                    var ring = new Vector3(Mathf.Sign(hand.x) * 0.55f, 1.25f, 0.33f);
                    L4Shapes.Lathe(m, 0, Matrix4x4.TRS(ring, Quaternion.Euler(90f, 0f, 0f), Vector3.one), new[] { new Vector2(0.06f, -0.015f), new Vector2(0.085f, -0.015f), new Vector2(0.085f, 0.015f), new Vector2(0.06f, 0.015f), new Vector2(0.06f, -0.015f) }, 8, PropCell(L4Shapes.IronDark));
                    L4Shapes.Chain(m, 0, ring + Vector3.down * 0.07f, hand, 0.25f, PropCell(L4Shapes.Iron));
                    // The manacle round the wrist.
                    L4Shapes.Lathe(m, 0, Matrix4x4.TRS(hand, Quaternion.FromToRotation(Vector3.up, (ring - hand).normalized), Vector3.one), new[] { new Vector2(0.045f, -0.04f), new Vector2(0.06f, -0.04f), new Vector2(0.06f, 0.04f), new Vector2(0.045f, 0.04f), new Vector2(0.045f, -0.04f) }, 8, PropCell(L4Shapes.IronDark));
                }
            });
        }

        private static Vector3 Bone(Transform rig, string name)
        {
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return rig.InverseTransformPoint(t.position);
                }
            }

            return Vector3.zero;
        }

        // Sat on the floor, legs out in front, back against the wall behind him (+Z), arms hauled up and out to the
        // rings, head fallen forward and to one side.
        private static void ChainedPose(Transform r, float s)
        {
            Transform F(string name)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }

                throw new ArgumentException("No bone " + name);
            }

            void Aim(string bone, string child, Vector3 dir)
            {
                var b = F(bone);
                var c = F(child);
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            var l = new Vector3(s, 0f, 0f);
            Aim("Spine_01", "Spine_02", new Vector3(0f, 1f, 0.22f));
            Aim("UpperLeg_L", "LowerLeg_L", l * 0.18f + new Vector3(0f, -0.08f, -1f));
            Aim("LowerLeg_L", "Ankle_L", l * 0.05f + new Vector3(0f, -0.25f, -1f));
            Aim("UpperLeg_R", "LowerLeg_R", -l * 0.22f + new Vector3(0f, -0.02f, -1f));
            Aim("LowerLeg_R", "Ankle_R", -l * 0.1f + new Vector3(0f, -0.45f, -0.9f));
            Aim("Shoulder_L", "Elbow_L", l * 0.75f + new Vector3(0f, 0.6f, 0.15f));
            Aim("Elbow_L", "Hand_L", l * 0.35f + new Vector3(0f, 0.9f, 0.1f));
            Aim("Shoulder_R", "Elbow_R", -l * 0.75f + new Vector3(0f, 0.55f, 0.15f));
            Aim("Elbow_R", "Hand_R", -l * 0.35f + new Vector3(0f, 0.9f, 0.1f));
            var head = F("Head");
            head.rotation = Quaternion.AngleAxis(-24f * s, Vector3.forward) * Quaternion.AngleAxis(-32f, Vector3.right) * head.rotation;
        }

        // A Synty character baked hanging: arms dropped, legs dangling and a little bent, toes down, head fallen
        // to one side. The origin is the beam; the rope runs down to the noose.
        // A skeleton on a rope: the noose 0.6 m under the beam, the bones hanging from it, turned a little.
        private static void Hanged(GameObject root, bool sash)
        {
            const float neck = 1.47f, noose = 0.62f;
            var body = Nest(root, Skeletons + "Hanging.prefab", new Vector3(0f, -noose - 0.06f - neck, 0f), 0f);
            body.transform.localRotation = Quaternion.Euler(3f, 0f, 2f);
            Shapes(root.transform, "Rope", L4Shapes.Props, m =>
            {
                L4Shapes.Shaft(m, 0, Vector3.zero, new Vector3(0f, -noose, 0f), 0.025f, 0.025f, 5, PropCell(L4Shapes.Rope));
                L4Shapes.Lathe(m, 0, Matrix4x4.Translate(new Vector3(0f, -noose - 0.08f, 0.01f)), new[] { new Vector2(0.06f, -0.03f), new Vector2(0.085f, -0.03f), new Vector2(0.085f, 0.03f), new Vector2(0.06f, 0.03f), new Vector2(0.06f, -0.03f) }, 8, PropCell(L4Shapes.Rope));
                if (sash)
                {
                    L4Shapes.Box(m, 0, new Vector3(0f, -noose - 0.4f, 0.04f), Quaternion.Euler(0f, 0f, 38f), new Vector3(0.1f, 0.6f, 0.26f), PropCell(L4Shapes.Clay));
                }
            });
        }

        // A Synty character baked laid out: standing straight, arms at its sides, feet together, head fallen a
        // little to one side. Lay it down by turning it -90 about x (face up, head toward -z).
        private static Mesh Laid(string body)
        {
            string meshName = "L4_Laid_" + body;
            string path = L4Build.MeshesPath + "/Figures/" + meshName + ".asset";
            var baked = L1Bakers.BakeStanding(KnightsCharacters + "Character_" + body + "_Blue.prefab", "Character_" + body, LaidPose, null, out var rig);
            UnityEngine.Object.DestroyImmediate(rig);
            L2Build.EnsureFolder(L4Build.MeshesPath + "/Figures");
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null)
            {
                saved.Clear();
                EditorUtility.CopySerialized(baked, saved);
                UnityEngine.Object.DestroyImmediate(baked);
            }
            else
            {
                AssetDatabase.CreateAsset(baked, path);
                saved = baked;
            }

            saved.name = meshName;
            return saved;
        }

        private static void LaidPose(Transform r, float s)
        {
            Transform F(string name)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }

                throw new ArgumentException("No bone " + name);
            }

            void Aim(string bone, string child, Vector3 dir)
            {
                var b = F(bone);
                var c = F(child);
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            var l = new Vector3(s, 0f, 0f);
            Aim("Shoulder_L", "Elbow_L", l * 0.1f + new Vector3(0f, -1f, 0f));
            Aim("Elbow_L", "Hand_L", l * 0.02f + new Vector3(0f, -1f, 0.06f));
            Aim("Shoulder_R", "Elbow_R", -l * 0.1f + new Vector3(0f, -1f, 0f));
            Aim("Elbow_R", "Hand_R", -l * 0.02f + new Vector3(0f, -1f, 0.06f));
            Aim("UpperLeg_L", "LowerLeg_L", l * 0.02f + new Vector3(0f, -1f, 0f));
            Aim("LowerLeg_L", "Ankle_L", new Vector3(0f, -1f, 0f));
            Aim("UpperLeg_R", "LowerLeg_R", -l * 0.02f + new Vector3(0f, -1f, 0f));
            Aim("LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, 0f));
            var head = F("Head");
            head.rotation = Quaternion.AngleAxis(22f * s, Vector3.up) * head.rotation;
        }

        private static void HangingPose(Transform r, float s)
        {
            Transform F(string name)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        return t;
                    }
                }

                throw new ArgumentException("No bone " + name);
            }

            void Aim(string bone, string child, Vector3 dir)
            {
                var b = F(bone);
                var c = F(child);
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            var l = new Vector3(s, 0f, 0f);
            Aim("Shoulder_L", "Elbow_L", l * 0.12f + new Vector3(0f, -1f, 0.05f));
            Aim("Elbow_L", "Hand_L", l * 0.05f + new Vector3(0f, -1f, 0.12f));
            Aim("Shoulder_R", "Elbow_R", -l * 0.12f + new Vector3(0f, -1f, 0.02f));
            Aim("Elbow_R", "Hand_R", -l * 0.04f + new Vector3(0f, -1f, 0.1f));
            Aim("UpperLeg_L", "LowerLeg_L", l * 0.04f + new Vector3(0f, -1f, 0.1f));
            Aim("LowerLeg_L", "Ankle_L", new Vector3(0f, -1f, -0.14f));
            Aim("UpperLeg_R", "LowerLeg_R", -l * 0.02f + new Vector3(0f, -1f, 0.05f));
            Aim("LowerLeg_R", "Ankle_R", new Vector3(0f, -1f, -0.1f));
            var head = F("Head");
            head.rotation = Quaternion.AngleAxis(28f * s, Vector3.forward) * Quaternion.AngleAxis(18f, Vector3.right) * head.rotation;
        }
    }
}
