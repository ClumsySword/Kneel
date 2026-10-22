using System.Collections.Generic;
using Kneel.Hazards;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Refinement, second pass (owner, 2026-10-19, on the first pass's captures): "There is something about this that
    // screams: This is a game. Not realistic. The stronghold should feel used and like something that has function."
    // What this adds on top of the blockout, the citadel, the atmosphere and the aftermath:
    // - the outposts: the bastions L4Plan now makes walkable (lookout over the Lists, guard post by the west lane,
    //   signal post north of the side yard) dressed as the posts they were: lean-tos, stores, braziers, a ballista,
    //   a watchtower each; the outer bailey a garrison camp; the timber bridge between guard and signal post;
    // - timber watchtowers on the rock pinnacles round the walls ("add more of those watchtowers");
    // - the master's red banners down the walls of every yard ("more red banners... for contrast") and flags on the
    //   towers' tops;
    // - more fire: burning wreckage that burns whoever walks into it (Kneel.FireHazard), and fire vents, cracks in
    //   the paving where the lava under the crag breaks through on a loop (Kneel.Hazards.FireVent);
    // all kept off the route's narrow joins, the stairs, every hazard, spawn and marker. Rebuilding replaces only
    // this group.
    public static class L4Stronghold
    {
        public const string GroupName = "Stronghold";
        private const float T1 = L4Plan.T1;

        private static Material KStone => L4Build.Mat("L4_Knights_Stone");
        private static Material KSoot => L4Build.Mat("L4_Knights_Soot");
        private static Material AStone => L4Build.Mat("L4_Adventure_Stone");
        private static Material ASoot => L4Build.Mat("L4_Adventure_Soot");
        private static Material Wood => L2PolyKit.Wood;
        private static Material Charred => L2PolyKit.CharredWood;
        private static Material Canvas => L4Build.Mat("L4_Canvas");
        private static Material Crack => L4Build.Flat("L4_LavaCrack", "#1A0503", 0.2f, "#A0240A", 1.3f);

        [MenuItem("Kneel/L4/Build/Stronghold (replaces that group)")]
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

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            Physics.SyncTransforms();
            int props = Outposts(g) + Bailey(g);
            Bridge(g);
            int towers = Watchtowers(g, root);
            Physics.SyncTransforms();
            int banners = Banners(g);
            int fires = Wreckage(g);
            int vents = Vents(g);
            GateApproach(g);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<Collider>() != null)
                {
                    continue;
                }

                r.gameObject.layer = L3Build.Layer(L3Build.Unlayered);
                if (r.sharedMaterial == Crack)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<ParticleSystem>() == null && t.GetComponentInParent<FireVent>() == null && t.GetComponentInParent<Light>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            return GroupName + ": " + props + " outpost and camp props, " + towers + " watchtowers, " + banners + " banners, " + fires + " burning wrecks, " + vents + " fire vents";
        }

        // ---------------------------------------------------------------- Helpers

        // A pack piece seated on 'at' (its y the ground), turned by yaw; 'solid' gives it a box collider on Obstacles.
        internal static GameObject Prop(Transform parent, string key, Vector3 at, float yaw, float scale, Material mat, bool solid)
        {
            var go = L3Build.Seat(parent, key, new Vector3(0f, yaw, 0f), scale, new Vector2(at.x, at.z), at.y, mat);
            if (solid)
            {
                Solid(go);
            }

            return go;
        }

        internal static void Solid(GameObject go)
        {
            var b = L3Build.LocalBounds(go, go.transform);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            go.layer = L3Build.Layer(L3Build.Cover);
        }

        // A pack beam stretched from a to b, 'thick' across.
        internal static GameObject Beam(Transform parent, Vector3 a, Vector3 b, float thick, Material mat)
        {
            var go = L3Build.Part(parent, "K:SM_Prop_Beam_01", Vector3.zero, Vector3.zero, Vector3.one, mat);
            var lb = L3Build.LocalBounds(go, go.transform);
            go.transform.localScale = new Vector3(thick / Mathf.Max(lb.size.x, 0.01f), Vector3.Distance(a, b) / lb.size.y, thick / Mathf.Max(lb.size.z, 0.01f));
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            go.transform.localPosition = (a + b) * 0.5f - go.transform.localRotation * Vector3.Scale(lb.center, go.transform.localScale);
            return go;
        }

        internal static void Flag(Transform parent, Vector3 at, float yaw, float scale)
        {
            L3Build.Part(parent, "K:SM_Bld_Castle_Flagpole_01", at, new Vector3(0f, yaw, 0f), Vector3.one * scale, KSoot).name = "Flagpole";
            L3Build.Part(parent, "K:SM_Bld_Castle_Flag_01", at + Vector3.up * 2.55f * scale, new Vector3(0f, yaw, 0f), Vector3.one * scale, L4Citadel.BannerRed).name = "Flag";
        }

        // A brazier with its fire; 'solid' on walkable ground.
        internal static void Brazier(Transform parent, Vector3 at, float scale = 1.2f)
        {
            var b = Prop(parent, "K:SM_Prop_Brazier_01", at, 0f, scale, KSoot, true);
            var bb = L3Build.LocalBounds(b, parent);
            L2Fire.Fire(parent, "BrazierFire", new Vector3(bb.center.x, bb.max.y - 0.3f * scale, bb.center.z), 0.75f * scale, 2.6f, 9f, true);
        }

        // Stores: crates and barrels stacked against a wall, a sack or two.
        internal static void Stores(Transform parent, Vector3 at, float yaw, int seed, bool solid = true)
        {
            var rng = new System.Random(seed);
            var t = L3Build.Child(parent, "Stores", at, new Vector3(0f, yaw, 0f)).transform;
            Prop(t, "A:SM_Prop_Crate_01", new Vector3(0f, 0f, 0f), rng.Next(-8, 8), 1f, ASoot, solid);
            Prop(t, "A:SM_Prop_Crate_02", new Vector3(1.05f, 0f, 0.1f), rng.Next(-10, 10), 1f, ASoot, solid);
            Prop(t, "A:SM_Prop_Crate_01", new Vector3(0.1f, 0.78f, 0.05f), rng.Next(-15, 15), 0.8f, ASoot, false);
            Prop(t, "A:SM_Prop_Barrel_01", new Vector3(-1f, 0f, 0.2f), rng.Next(360), 1f, ASoot, solid);
            Prop(t, "A:SM_Prop_Barrel_02", new Vector3(-1.75f, 0f, -0.2f), rng.Next(360), 1f, ASoot, solid);
            Prop(t, "A:SM_Prop_Sack_01", new Vector3(1.9f, 0f, -0.3f), rng.Next(360), 1f, ASoot, false);
        }

        // ---------------------------------------------------------------- The outposts

        private static int Outposts(Transform g)
        {
            var o = L3Build.Child(g, "Outposts").transform;
            int n = 0;

            // The lookout over the Lists (M02): a ballista trained on the Lists from the south strip, a lean-to and
            // stores along the north parapet, a brazier at the stair head, the watchtower in the north-east corner.
            var lookout = L3Build.Child(o, "Lookout").transform;
            var ballista = L4Level.PutPrefab("C07", lookout, new Vector3(67.6f, T1, 87.4f), 270f);
            if (ballista != null)
            {
                n++;
            }

            Prop(lookout, "K:SM_Bld_Leanto_01", new Vector3(67.5f, T1, 96.6f), 180f, 1f, KSoot, true);
            Stores(lookout, new Vector3(71.6f, T1, 97.1f), 180f, 11);
            Brazier(lookout, new Vector3(74.4f, T1, 89.6f));
            Watchtower(lookout, new Vector3(77.4f, T1, 95.6f), 0f, 7f, true, 21);
            Flag(lookout, new Vector3(79.3f, T1, 86.6f), 90f, 1.1f);
            n += 6;

            // The guard post (M03): its watchtower at the west end over the lane, a cook-fire and a lean-to by the
            // south parapet, stores against the chapel wall.
            var guard = L3Build.Child(o, "GuardPost").transform;
            Watchtower(guard, new Vector3(55.3f, T1, 170.1f), 90f, 7f, true, 22);
            Prop(guard, "K:SM_Bld_Leanto_01", new Vector3(60.4f, T1, 169.2f), 0f, 1f, KSoot, true);
            Prop(guard, "K:SM_Prop_CampFire_01", new Vector3(63.3f, T1, 170.6f), 30f, 1f, KSoot, false);
            L2Fire.Fire(guard, "CookFire", new Vector3(63.3f, T1 + 0.15f, 170.6f), 0.6f, 2.2f, 7f, true);
            Stores(guard, new Vector3(64.2f, T1, 179.7f), 90f, 12);
            Brazier(guard, new Vector3(54.6f, T1, 179.8f));
            n += 5;

            // The signal post (M04): the great signal brazier, a store of fuel, the flag.
            var signal = L3Build.Child(o, "SignalPost").transform;
            var bowl = Prop(signal, "K:SM_Prop_Brazier_01", new Vector3(63.9f, T1, 193.4f), 0f, 2.2f, KSoot, true);
            L2Fire.Fire(signal, "SignalFire", new Vector3(63.9f, L3Build.LocalBounds(bowl, signal).max.y - 0.2f, 193.4f), 1.5f, 4f, 14f, true);
            Prop(signal, "A:SM_Prop_Logpile_01", new Vector3(55.2f, T1, 193.9f), 90f, 1f, ASoot, true);
            Stores(signal, new Vector3(59.2f, T1, 195.9f), 180f, 13);
            Flag(signal, new Vector3(54.2f, T1, 195.8f), 0f, 1.2f);
            n += 4;

            // The strip behind the NE tower (M05), out of reach: stores.
            Stores(o, new Vector3(90.5f, T1, 166.6f), 180f, 14, false);
            n++;

            // Flags on the two towers' roofs.
            Flag(o, new Vector3(48.4f, T1 + 5f, 163.1f), 0f, 1.2f);
            Flag(o, new Vector3(92.1f, T1 + 5f, 155f), 180f, 1.2f);
            return n;
        }

        // The outer bailey (M01, out of reach behind the Lists' cut wall): the garrison's camp as it was left, tents
        // and lean-tos against the walls, a cart, stores, a fire still burning in the middle.
        private static int Bailey(Transform g)
        {
            var b = L3Build.Child(g, "Bailey").transform;
            Prop(b, "K:SM_Bld_Tent_02", new Vector3(41.2f, 0f, 80.6f), 90f, 0.7f, Canvas, false);
            Prop(b, "K:SM_Bld_Tent_03", new Vector3(46.6f, 0f, 71.4f), 0f, 0.65f, Canvas, false);
            Prop(b, "K:SM_Bld_Leanto_01", new Vector3(38f, 0f, 70.4f), 90f, 1f, KSoot, false);
            Prop(b, "K:SM_Bld_Leanto_01", new Vector3(50.2f, 0f, 83.6f), 180f, 1f, KSoot, false);
            Prop(b, "K:SM_Prop_Cart_01", new Vector3(48.4f, 0f, 78.2f), 35f, 1f, KSoot, false);
            Stores(b, new Vector3(37.4f, 0f, 74.6f), 90f, 15, false);
            Prop(b, "A:SM_Prop_Logpile_01", new Vector3(44.6f, 0f, 84.9f), 0f, 1f, ASoot, false);
            Prop(b, "K:SM_Prop_CampFire_01", new Vector3(44.2f, 0f, 76.2f), 0f, 1.2f, KSoot, false);
            L2Fire.Fire(b, "CampFire", new Vector3(44.2f, 0.15f, 76.2f), 0.9f, 3f, 10f, true);
            // One tent burning.
            L2Fire.Fire(b, "TentFire", new Vector3(46.6f, 0.2f, 71.4f), 1.3f, 3f, 10f, true, new Vector3(3f, 0f, 3f));
            foreach (var (x, z, yaw, pose) in new[] { (42.6f, 74.2f, 30f, "Supine"), (47.2f, 81.6f, 200f, "Prone"), (39.4f, 83.4f, 120f, "Curled") })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L4Assets.Skeletons + pose + ".prefab");
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, b);
                    go.transform.SetPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                }
            }

            Flag(b, new Vector3(36.8f, 0f, 85.2f), 90f, 1.6f);
            return 14;
        }

        // The timber bridge from the guard post to the signal post over the side yard (floor F16t, its collider): two
        // heavy stringers with knee braces from the bastion faces, a deck of boards across, posts and rails.
        private static void Bridge(Transform g)
        {
            var b = L3Build.Child(g, "TimberBridge").transform;
            const float x0 = 57.5f, x1 = 60.5f, z0 = 181f, z1 = 191f, y = T1;
            var rng = new System.Random(31);
            foreach (float x in new[] { x0 + 0.35f, x1 - 0.35f })
            {
                Beam(b, new Vector3(x, y - 0.35f, z0 - 1.2f), new Vector3(x, y - 0.35f, z1 + 1.2f), 0.42f, Charred);
                Beam(b, new Vector3(x, y - 2.6f, z0 + 0.1f), new Vector3(x, y - 0.5f, z0 + 2.4f), 0.28f, Charred);
                Beam(b, new Vector3(x, y - 2.6f, z1 - 0.1f), new Vector3(x, y - 0.5f, z1 - 2.4f), 0.28f, Charred);
            }

            for (float z = z0 + 0.17f; z < z1; z += 0.34f)
            {
                var plank = L3Build.MeshChild(b, "Board", L2PolyKit.Plank(new Vector3(x1 - x0 + 0.3f, 0.08f, 0.32f), rng.Next(100000)), new Vector3((x0 + x1) * 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.12f, y - 0.04f, z), Wood);
                plank.transform.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 2f, 0f);
            }

            foreach (float x in new[] { x0 + 0.1f, x1 - 0.1f })
            {
                for (float z = z0 + 0.6f; z <= z1 - 0.5f; z += 2.2f)
                {
                    Beam(b, new Vector3(x, y - 0.4f, z), new Vector3(x, y + 1.1f, z), 0.2f, Charred);
                }

                Beam(b, new Vector3(x, y + 1.05f, z0 + 0.2f), new Vector3(x, y + 1.05f, z1 - 0.2f), 0.16f, Charred);
                Beam(b, new Vector3(x, y + 0.55f, z0 + 0.2f), new Vector3(x, y + 0.55f, z1 - 0.2f), 0.12f, Charred);
                var rail = L3Build.Child(b, "RailCollider", new Vector3(x, y, (z0 + z1) * 0.5f));
                L3Build.StandingBox(rail, new Vector3(0.2f, 1.15f, z1 - z0));
                rail.layer = L3Build.Layer(L3Build.Boundary);
            }
        }

        // The approach to the outer gate (owner: "I also want the initial gate to the stronghold to feel grand"): where
        // the causeway reaches the apron, two tall stone pillars carry braziers and the master's banners, framing
        // the gate keep beyond.
        private static void GateApproach(Transform g)
        {
            var parent = L3Build.Child(g, "GateApproach").transform;
            foreach (float x in new[] { 53.2f, 66.8f })
            {
                const float z = 41.4f - L4Plan.Gap;
                var pillar = L3Build.Fit(parent, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(1.8f, 6.2f, 1.8f), new Vector2(x, z), 0f, KStone);
                Solid(pillar);
                var cap = L3Build.Fit(parent, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(2.3f, 0.5f, 2.3f), new Vector2(x, z), 6.2f, KStone);
                var bowl = Prop(parent, "K:SM_Prop_Brazier_01", new Vector3(x, 6.7f, z), 0f, 1.5f, KSoot, false);
                L2Fire.Fire(parent, "PillarFire", new Vector3(x, L3Build.LocalBounds(bowl, parent).max.y - 0.2f, z), 1.1f, 3.2f, 12f, true);
                L4Citadel.HangBanner(parent, new Vector3(x, 5.9f, z - 0.94f), 0f, 1.45f, x < 60f ? 0 : 1);
                cap.name = "PillarCap";
            }
        }

        // ---------------------------------------------------------------- Watchtowers

        // A timber watchtower: four posts braced in two tiers, a ladder up the south side, the pack's octagonal
        // timber hoarding as its lookout hut on top, lamplight inside, the red flag on its roof.
        internal static void Watchtower(Transform parent, Vector3 at, float yaw, float height, bool solid, int seed)
        {
            var t = L3Build.Child(parent, "Watchtower", at, new Vector3(0f, yaw, 0f)).transform;
            var rng = new System.Random(seed);
            const float half = 1.55f;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Beam(t, new Vector3(sx * half, -0.3f, sz * half), new Vector3(sx * half * 0.92f, height + 0.4f, sz * half * 0.92f), 0.34f, KSoot);
                }
            }

            // Cross braces on all four faces, in two tiers; a ring beam at each tier.
            float[] tiers = { 0.4f, height * 0.5f, height - 0.2f };
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                var r = Quaternion.Euler(0f, a, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var p0 = r * new Vector3(-half, tiers[k], -half);
                    var p1 = r * new Vector3(half, tiers[k + 1], -half);
                    if ((i + k + seed) % 2 == 0)
                    {
                        p0 = r * new Vector3(half, tiers[k], -half);
                        p1 = r * new Vector3(-half, tiers[k + 1], -half);
                    }

                    Beam(t, p0, p1, 0.2f, KSoot);
                }

                Beam(t, r * new Vector3(-half, tiers[1], -half - 0.05f), r * new Vector3(half, tiers[1], -half - 0.05f), 0.22f, KSoot);
            }

            // The ladder.
            foreach (float x in new[] { -0.32f, 0.32f })
            {
                Beam(t, new Vector3(x, 0f, -half - 0.45f), new Vector3(x, height + 0.2f, -half - 0.12f), 0.1f, KSoot);
            }

            for (float y = 0.35f; y < height; y += 0.42f)
            {
                float z = Mathf.Lerp(-half - 0.45f, -half - 0.12f, y / height);
                Beam(t, new Vector3(-0.32f, y, z - 0.04f), new Vector3(0.32f, y, z - 0.04f), 0.06f, KSoot);
            }

            // The hut on top, its legs over the post heads.
            var hut = L3Build.Seat(t, "K:SM_Bld_Castle_Wood_Battlement_01", new Vector3(0f, rng.Next(4) * 45f, 0f), new Vector3(0.85f, 0.95f, 0.85f), Vector2.zero, height - 1.25f, KSoot);
            var hb = L3Build.LocalBounds(hut, t);
            var lamp = L3Build.Child(t, "Lamp", new Vector3(0f, height + 1.3f, 0f)).AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = L1Build.Hex("#FFA052");
            lamp.intensity = 4f;
            lamp.range = 9f;
            lamp.shadows = LightShadows.None;
            Flag(t, new Vector3(0f, hb.max.y - 0.5f, 0f), 30f, 1.1f);
            if (solid)
            {
                var col = L3Build.Child(t, "Collider");
                L3Build.StandingBox(col, new Vector3(half * 2f + 0.5f, height, half * 2f + 1.2f));
                col.transform.localPosition = new Vector3(0f, 0f, -0.3f);
                col.layer = L3Build.Layer(L3Build.Boundary);
            }
        }

        // Watchtowers on the rock pinnacles round the walls, east and west of the castle and behind the citadel (never
        // south of a yard, where they would stand between the camera and the fight).
        private static int Watchtowers(Transform g, Transform root)
        {
            var parent = L3Build.Child(g, "Watchtowers").transform;
            var rocks = root.Find(L4Surroundings.GroupName);
            if (rocks == null)
            {
                return 0;
            }

            var candidates = new List<Bounds>();
            foreach (var t in rocks.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Cliff")
                {
                    continue;
                }

                var r = t.GetComponentInChildren<Renderer>();
                if (r == null)
                {
                    continue;
                }

                var b = r.bounds;
                bool side = b.center.x < 30f || b.center.x > 95f || b.center.z > 345f;
                if (side && b.max.y > -3.5f && b.center.z > 30f - L4Plan.Gap)
                {
                    candidates.Add(b);
                }
            }

            candidates.Sort((a, b) => a.center.z.CompareTo(b.center.z));
            var placed = new List<Vector2>();
            int n = 0, seed = 40;
            foreach (var b in candidates)
            {
                var p = new Vector2(b.center.x, b.center.z);
                bool near = false;
                foreach (var q in placed)
                {
                    if ((q - p).sqrMagnitude < 46f * 46f)
                    {
                        near = true;
                        break;
                    }
                }

                if (near)
                {
                    continue;
                }

                placed.Add(p);
                // Turned so its ladder faces the castle.
                float yaw = Mathf.Atan2(60f - p.x, 170f - p.y) * Mathf.Rad2Deg + 180f;
                Watchtower(parent, new Vector3(p.x, b.max.y - 0.4f, p.y), yaw, 8.5f + (seed % 3), false, seed++);
                n++;
            }

            return n;
        }

        // ---------------------------------------------------------------- Banners

        // The master's banners down the walls of the yards: from points just inside each floor's edges, look out to
        // the wall; where a tall face stands, hang a banner from near its top, every 8-9 m, clear of everything placed.
        private static int Banners(Transform g)
        {
            var parent = L3Build.Child(g, "Banners").transform;
            var keepOut = new List<Vector2>();
            foreach (var p in L4Plan.Places)
            {
                keepOut.Add(new Vector2(p.Position.x, p.Position.z));
            }

            foreach (var s in L4Plan.Stairs)
            {
                keepOut.Add(new Vector2(s.Centre.x, s.Centre.z));
            }

            int n = 0;
            int mask = (1 << L3Build.Layer(L3Build.Boundary)) | (1 << L3Build.Layer(L3Build.Walkable));
            foreach (var f in L4Plan.Floors)
            {
                if (f.Mass || f.Hidden || f.Tower || f.Bridge || f.Outpost)
                {
                    continue;
                }

                foreach (char side in new[] { 'N', 'W', 'E', 'S' })
                {
                    bool alongX = side == 'S' || side == 'N';
                    float a = alongX ? f.X0 : f.Z0, b = alongX ? f.X1 : f.Z1;
                    var outward = side == 'S' ? Vector3.back : side == 'N' ? Vector3.forward : side == 'W' ? Vector3.left : Vector3.right;
                    float edge = side == 'S' ? f.Z0 : side == 'N' ? f.Z1 : side == 'W' ? f.X0 : f.X1;
                    for (float s = a + 3.5f; s < b - 3f; s += 8.5f)
                    {
                        var inside = alongX ? new Vector3(s, f.Y + 3.2f, edge) - outward * 1.2f : new Vector3(edge, f.Y + 3.2f, s) - outward * 1.2f;
                        var plan = new Vector2(inside.x, inside.z);
                        bool clear = true;
                        foreach (var k in keepOut)
                        {
                            if ((k - plan).sqrMagnitude < 3.2f * 3.2f)
                            {
                                clear = false;
                                break;
                            }
                        }

                        if (!clear || !Physics.Raycast(inside, outward, out var hit, 3.2f, mask, QueryTriggerInteraction.Ignore) || Vector3.Dot(hit.normal, -outward) < 0.9f)
                        {
                            continue;
                        }

                        // The face's top: down onto it from above, just inside the face.
                        var probe = hit.point + outward * 0.25f;
                        if (!Physics.Raycast(new Vector3(probe.x, f.Y + 14f, probe.z), Vector3.down, out var top, 14f, mask, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        float height = top.point.y - f.Y;
                        if (height < 4.2f)
                        {
                            continue;
                        }

                        float scale = Mathf.Clamp((Mathf.Min(height, 7f) - 1.4f) / 3.1f, 0.9f, 1.5f);
                        var at = new Vector3(hit.point.x, Mathf.Min(top.point.y, f.Y + 7.4f) - 0.35f, hit.point.z) - outward * 0.14f;
                        float yaw = Mathf.Atan2(-outward.x, -outward.z) * Mathf.Rad2Deg;
                        L4Citadel.HangBanner(parent, at, yaw, scale, n);
                        keepOut.Add(plan);
                        n++;
                    }
                }
            }

            return n;
        }

        // ---------------------------------------------------------------- Fire

        // Burning wreckage at the yards' edges: a heap of charred beams and boards still alight, which burns whoever
        // walks into it (Kneel.FireHazard: slow, heavy, environmental ticks). Each in a corner, off the route.
        private static int Wreckage(Transform g)
        {
            var parent = L3Build.Child(g, "Wreckage").transform;
            var spots = new (float x, float y, float z, float yaw)[]
            {
                (74.6f, 0f, -34.2f, 20f),       // the apron, its south-east corner
                (46.6f, 0f, -20.3f, -30f),      // the apron, by the lookout path
                (37.8f, 0f, 11.8f, 40f),        // the mustering yard, south-west corner
                (80.2f, 0f, 27.8f, -20f),       // the stable row, its east end
                (57.8f, 0f, 43.8f, 75f),        // the forge yard, south-west corner
                (52.2f, 0f, 62.2f, 10f),        // the barracks yard, north-east corner
                (37.6f, 0f, 87.8f, 70f),        // the Lists, south-west corner
                (53.6f, 0f, 125.6f, 10f),       // the courtyard, south-west corner
                (91.4f, T1, 126.2f, 45f),       // the terrace, south end
                (41.6f, 0f, 207.6f, -15f),      // the barbican yard, south-west corner
                (66.4f, 0f, 224.6f, 60f),       // the barbican yard, north-east corner
                (88.6f, 0f, 202.6f, 0f),        // the cloister's east end
            };
            var rng = new System.Random(51);
            int n = 0;
            foreach (var (x, y, z, yaw) in spots)
            {
                var t = L3Build.Child(parent, "BurningWreck", new Vector3(x, y, z), new Vector3(0f, yaw, 0f)).transform;
                for (int i = 0; i < 5; i++)
                {
                    float a = (float)rng.NextDouble() * 180f;
                    var dir = Quaternion.Euler(0f, a, 0f) * Vector3.right;
                    float len = 1.8f + (float)rng.NextDouble() * 1.2f, lift = 0.15f + i * 0.12f;
                    Beam(t, -dir * len * 0.5f + new Vector3(0f, lift, 0f), dir * len * 0.5f + new Vector3(0f, lift + ((float)rng.NextDouble() - 0.3f) * 0.6f, 0f), 0.26f, Charred);
                }

                L2Fire.Fire(t, "Fire", new Vector3(0f, 0.2f, 0f), 1.05f, 3.2f, 9f, true, new Vector3(1.6f, 0f, 1.6f));
                var area = L3Build.Child(t, "Burn");
                area.layer = L3Build.Layer(L3Build.Hazard);
                var box = area.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2.4f, 1.6f, 2.4f);
                box.center = new Vector3(0f, 0.8f, 0f);
                area.AddComponent<Kneel.FireHazard>();
                n++;
            }

            return n;
        }

        // Fire vents: cracks in the paving glowing with the lava under the crag, which flare into a column of fire
        // on a loop (Kneel.Hazards.FireVent). Placed in the open parts of the arenas, never on a stair, a doorway,
        // a pour or a slick; neighbouring vents a beat apart.
        private static int Vents(Transform g)
        {
            var parent = L3Build.Child(g, "FireVents").transform;
            var spots = new (float x, float y, float z)[]
            {
                (49.5f, 0f, -31.5f), (70.5f, 0f, -30f),             // the apron
                (47.5f, 0f, 14.5f), (60.8f, 0f, 19f),              // the mustering yard
                (68.5f, 0f, 46.5f), (72.5f, 0f, 52f),              // the forge yard
                (55.5f, 0f, 131.5f), (61.5f, 0f, 143.5f),           // the courtyard
                (88.6f, T1, 131f),                                  // the terrace
                (48.5f, 0f, 220.5f), (60.5f, 0f, 210.5f),           // the barbican yard
                (72f, 0f, 162f),                                    // the inner ward
            };
            var rng = new System.Random(61);
            int n = 0;
            foreach (var (x, y, z) in spots)
            {
                var t = L3Build.Child(parent, "FireVent", new Vector3(x, y, z), new Vector3(0f, rng.Next(360), 0f)).transform;
                t.gameObject.layer = L3Build.Layer(L3Build.Hazard);
                var box = t.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.9f, 2.2f, 1.9f);
                box.center = new Vector3(0f, 1.1f, 0f);

                // The crack: a few jagged runs wandering out from a split, each a chain of short kinked segments
                // that thin as they go, a hair proud of the paving.
                var m = new L3Mesh();
                int runs = 4 + rng.Next(2);
                for (int i = 0; i < runs; i++)
                {
                    float heading = i * 360f / runs + (float)rng.NextDouble() * 50f;
                    var at = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.2f, 0.012f, ((float)rng.NextDouble() - 0.5f) * 0.2f);
                    float width = 0.07f;
                    int segments = 3 + rng.Next(3);
                    for (int k = 0; k < segments; k++)
                    {
                        heading += ((float)rng.NextDouble() - 0.5f) * 70f;
                        float len = 0.16f + (float)rng.NextDouble() * 0.2f;
                        var dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                        L4Shapes.Box(m, 0, at + dir * len * 0.5f, Quaternion.LookRotation(dir), new Vector3(width, 0.02f, len + 0.03f), new Vector2(0.5f, 0.5f));
                        at += dir * len;
                        width *= 0.8f;
                    }
                }
                L3Build.MeshChild(t, "Crack", m.ToMesh("L4_FireVentCrack_" + n), Vector3.zero, Crack);

                var glow = L3Build.Child(t, "Glow", new Vector3(0f, 0.6f, 0f)).AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = L1Build.Hex("#FF5A1A");
                glow.intensity = 0.6f;
                glow.range = 5f;
                glow.shadows = LightShadows.None;

                // The tell: sparks spitting out of the crack.
                var tell = L3Build.Child(t, "Tell").transform;
                var sparks = L1Wayfinding.Particles("Sparks", tell, L1Build.FxMat("L1_FX_Ember"), 60, 40f, new Vector2(0.4f, 0.9f), new Vector2(2f, 4f),
                    new Vector2(0.04f, 0.08f), new Color(1f, 0.85f, 0.5f, 1f), new Color(1f, 0.35f, 0.08f, 0.8f), 0.4f);
                sparks.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var shape = sparks.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 18f;
                shape.radius = 0.4f;
                tell.gameObject.SetActive(false);

                // The burst: a column of fire out of the crack.
                var burst = L3Build.Child(t, "Burst").transform;
                L2Fire.Fire(burst, "Column", Vector3.zero, 1.5f, 0f, 0f, false, new Vector3(1.1f, 0f, 1.1f), L2Fire.Shape.Box, 1.4f);
                burst.gameObject.SetActive(false);

                var vent = t.gameObject.AddComponent<FireVent>();
                L3Build.Set(vent, "contact", L4Assets.Contact);
                L3Build.Set(vent, "offset", n * 1.9f);
                L3Build.Set(vent, "tellVisual", tell.gameObject);
                L3Build.Set(vent, "burstVisual", burst.gameObject);
                L3Build.Set(vent, "glow", glow);
                n++;
            }

            return n;
        }
    }
}
