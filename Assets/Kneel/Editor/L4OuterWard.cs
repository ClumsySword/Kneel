using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The outer ward's dressing (refinement, owner 2026-10-21: "add 2 more arena... moving the gate/bridge further
    // away and then adding stuff in between the gap... of the same quality as the rest of the level"). The floors,
    // walls, placed props, hazards and spawns come from L4Plan like everywhere else; this is what makes each yard
    // read as a place:
    // - O1, the mustering yard: the herald's dais under the muster rampart, the master's standard, braziers.
    // - The stable row: burnt stalls along the rampart's foot, a dead horse in one, the hay cart in the bend.
    // - O2, the forge yard: the forge against the north wall (hearth, hood and chimney, the master's red fire),
    //   anvils on their blocks, the quench trough, iron stock, coal, a lean-to.
    // - D7, the barracks yard: lean-tos and tents along the walls, bunks with their sleepers still in them, a
    //   smouldering fire, the breach's rubble.
    // - The muster rampart's top (out of reach): a watchtower, a ballista over the yard, stores, a brazier, a flag.
    // Rebuilding replaces only this group.
    public static class L4OuterWard
    {
        public const string GroupName = "OuterWard";
        private const float T1 = L4Plan.T1;

        private static Material KStone => L4Build.Mat("L4_Knights_Stone");
        private static Material KDark => L4Build.Mat("L4_Knights_StoneDark");
        private static Material KSoot => L4Build.Mat("L4_Knights_Soot");
        private static Material ASoot => L4Build.Mat("L4_Adventure_Soot");
        private static Material Iron => L4Build.Mat("L4_Iron");
        private static Material Pitch => L4Build.Mat("L4_Pitch");
        // Coals glowing dull red under the fire (L4_Ember bloomed to white in the grade).
        private static Material Coals => L4Build.Flat("L4_ForgeCoals", "#140503", 0.1f, "#B8320C", 0.55f);
        private static Material Canvas => L4Build.Mat("L4_Canvas");
        private static Material Wood => L2PolyKit.Wood;
        private static Material Charred => L2PolyKit.CharredWood;

        [MenuItem("Kneel/L4/Build/Outer Ward (replaces that group)")]
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
            int n = Muster(g) + Stables(g) + Forge(g) + ListsGate(g) + Barracks(g) + Rampart(g);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is ParticleSystemRenderer) && r.GetComponent<Collider>() == null && r.gameObject.layer == 0)
                {
                    r.gameObject.layer = L3Build.Layer(L3Build.Unlayered);
                }
            }

            foreach (var t in g.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponentInParent<ParticleSystem>() == null && t.GetComponentInParent<Light>() == null)
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                }
            }

            return GroupName + ": " + n + " pieces";
        }

        // ---------------------------------------------------------------- Helpers

        private static void Skeleton(Transform parent, string pose, Vector3 at, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L4Assets.Skeletons + pose + ".prefab");
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        // A stone block fitted to a size, with a collider on Obstacles.
        private static GameObject Block(Transform parent, Vector3 size, Vector3 baseCentre, Material mat, int yaw = 0)
        {
            var go = L3Build.Fit(parent, "K:SM_Bld_Castle_Wall_01", yaw, size, new Vector2(baseCentre.x, baseCentre.z), baseCentre.y, mat);
            L4Stronghold.Solid(go);
            return go;
        }

        private static GameObject Plank(Transform parent, Vector3 size, Vector3 centre, float yaw, int seed, bool charred)
        {
            var go = L3Build.MeshChild(parent, "Board", L2PolyKit.Plank(size, seed), centre, charred ? Charred : Wood);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static GameObject Generated(Transform parent, string name, Vector3 at, float yaw, Material mat, System.Action<L3Mesh> build)
        {
            var m = new L3Mesh();
            build(m);
            var go = L3Build.MeshChild(parent, name, m.ToMesh("L4_" + name), at, mat);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // ---------------------------------------------------------------- O1, the mustering yard

        private static int Muster(Transform g)
        {
            var t = L3Build.Child(g, "MusteringYard").transform;
            // The herald's dais against the rampart's face, the master's standard on it, the herald fallen at its foot.
            Block(t, new Vector3(5f, 0.32f, 2.2f), new Vector3(50f, 0f, 32.9f), KDark);
            L4Stronghold.Flag(t, new Vector3(50f, 0.32f, 33.4f), 180f, 1.8f);
            Skeleton(t, "Prone", new Vector3(48.6f, 0.32f, 32.6f), 160f);
            L4Stronghold.Brazier(t, new Vector3(46.6f, 0f, 32.8f));
            L4Stronghold.Brazier(t, new Vector3(53.4f, 0f, 32.8f));
            // The drill post the ranks dressed on, and the dead sergeant against it.
            L4Stronghold.Beam(t, new Vector3(61.4f, -0.3f, 24f), new Vector3(61.4f, 2.6f, 24f), 0.3f, Charred);
            Skeleton(t, "Curled", new Vector3(61.9f, 0f, 23.3f), 250f);
            return 7;
        }

        // ---------------------------------------------------------------- The stable row

        // Stalls along the rampart's foot (z 34): posts and boarded partitions, burnt; a dead horse in one, hay in the
        // others; the hay cart and stores along the south wall.
        private static int Stables(Transform g)
        {
            var t = L3Build.Child(g, "StableRow").transform;
            var rng = new System.Random(911);
            int n = 0;
            const float back = 34f, front = 31.6f;
            for (float x = 65.5f; x <= 75.6f; x += 2.5f)
            {
                // The partition: a post at the front, three boards back to the wall.
                L4Stronghold.Beam(t, new Vector3(x, -0.2f, front), new Vector3(x, 1.7f, front), 0.18f, Charred);
                for (int k = 0; k < 3; k++)
                {
                    if (rng.NextDouble() < 0.2)
                    {
                        continue;
                    }

                    var board = Plank(t, new Vector3(back - front, 0.06f, 0.26f), new Vector3(x, 0.4f + k * 0.42f, (back + front) * 0.5f), 90f, rng.Next(100000), true);
                    board.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
                }

                n++;
            }

            // The rail along the stall fronts, broken in the middle.
            L4Stronghold.Beam(t, new Vector3(65.5f, 1.1f, front), new Vector3(69.6f, 1.1f, front), 0.12f, Charred);
            L4Stronghold.Beam(t, new Vector3(71.2f, 1.1f, front), new Vector3(75.5f, 1.1f, front), 0.12f, Charred);
            var col = L3Build.Child(t, "StallCollider", new Vector3(70.5f, 0f, (back + front) * 0.5f));
            L3Build.StandingBox(col, new Vector3(10.4f, 1.8f, back - front));
            col.layer = L3Build.Layer(L3Build.Cover);
            L4Level.PutPrefab("C02", t, new Vector3(68.2f, 0f, 32.6f), 90f);
            L4Stronghold.Prop(t, "A:SM_Prop_Sack_02", new Vector3(73.4f, 0f, 33f), 30f, 1f, ASoot, false);
            L4Stronghold.Prop(t, "A:SM_Prop_Sack_01", new Vector3(74f, 0f, 32.6f), 200f, 1f, ASoot, false);
            L4Stronghold.Prop(t, "K:SM_Prop_CartHay_01", new Vector3(72.6f, 0f, 27.3f), 90f, 1f, KSoot, true);
            L4Stronghold.Stores(t, new Vector3(67f, 0f, 26.9f), 0f, 921);
            L2Fire.Smoulder(t, "HaySmoulder", new Vector3(72.6f, 1.2f, 27.3f), new Vector2(2f, 1.2f));
            return n + 6;
        }

        // ---------------------------------------------------------------- O2, the forge yard

        private static int Forge(Transform g)
        {
            var t = L3Build.Child(g, "Forge").transform;
            int n = 0;
            // The hearth against the north wall (its face at z 64), the bed of coals on it, the master's red fire.
            Block(t, new Vector3(5.2f, 1.05f, 2.3f), new Vector3(72f, 0f, 62.85f), KDark);
            Generated(t, "ForgeCoals", new Vector3(72f, 1.05f, 62.75f), 0f, Coals, m => L4Shapes.Box(m, 0, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(4.4f, 0.06f, 1.6f), new Vector2(0.5f, 0.5f)));
            L4Citadel.Hellfire(t, "ForgeFire", new Vector3(72f, 1.15f, 62.7f), 1.1f, 4f, 12f);
            // The hood, corbelled out of the wall over the hearth, and the chimney stack above the wall's top.
            L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(5.8f, 1.3f, 2.4f), new Vector2(72f, 62.8f), 3.3f, KStone);
            L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(4.2f, 1.4f, 1.9f), new Vector2(72f, 63.05f), 4.6f, KStone);
            L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(2.4f, 4.6f, 1.9f), new Vector2(72f, 63.05f), 6f, KSoot);
            L2Fire.Smoulder(t, "ChimneySmoke", new Vector3(72f, 10.6f, 63.05f), new Vector2(1.2f, 1f));
            foreach (float x in new[] { 69.1f, 74.9f })
            {
                // The hood's jambs down to the hearth.
                L3Build.Fit(t, "K:SM_Bld_Castle_Wall_01", 90, new Vector3(0.6f, 2.25f, 0.7f), new Vector2(x, 62.1f), 1.05f, KStone);
            }

            n += 6;

            // Anvils on their blocks, a half-made blade on one; the quench trough; iron stock against the wall.
            foreach (var (x, z, yaw) in new[] { (70.2f, 59.4f, 8f), (75.6f, 59.8f, -14f) })
            {
                var anvil = L3Build.Child(t, "Anvil", new Vector3(x, 0f, z), new Vector3(0f, yaw, 0f)).transform;
                L3Build.Fit(anvil, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(0.75f, 0.5f, 0.6f), Vector2.zero, 0f, KDark);
                Generated(anvil, "Anvil", new Vector3(0f, 0.5f, 0f), 0f, Iron, m =>
                {
                    var uv = new Vector2(0.5f, 0.5f);
                    L4Shapes.Box(m, 0, new Vector3(0f, 0.09f, 0f), Quaternion.identity, new Vector3(0.5f, 0.18f, 0.36f), uv);
                    L4Shapes.Box(m, 0, new Vector3(0f, 0.28f, 0f), Quaternion.identity, new Vector3(0.28f, 0.22f, 0.22f), uv);
                    L4Shapes.Box(m, 0, new Vector3(0.04f, 0.46f, 0f), Quaternion.identity, new Vector3(0.66f, 0.16f, 0.26f), uv);
                    L4Shapes.Box(m, 0, new Vector3(0.46f, 0.47f, 0f), Quaternion.Euler(0f, 0f, 12f), new Vector3(0.28f, 0.1f, 0.14f), uv);
                    L4Shapes.Box(m, 0, new Vector3(0.62f, 0.5f, 0f), Quaternion.Euler(0f, 0f, 20f), new Vector3(0.12f, 0.05f, 0.07f), uv);
                });
                var col = L3Build.Child(anvil, "Collider");
                L3Build.StandingBox(col, new Vector3(1f, 1.05f, 0.7f));
                col.layer = L3Build.Layer(L3Build.Cover);
                n++;
            }

            L3Build.Part(t, "K:SM_Wep_Broadsword_01", new Vector3(70.3f, 1.06f, 59.3f), new Vector3(90f, 98f, 0f), Vector3.one, Iron);

            var trough = L3Build.Child(t, "QuenchTrough", new Vector3(78.2f, 0f, 62.6f)).transform;
            foreach (var (c, s) in new[]
            {
                (new Vector3(0f, 0f, -0.42f), new Vector3(2.4f, 0.7f, 0.16f)), (new Vector3(0f, 0f, 0.42f), new Vector3(2.4f, 0.7f, 0.16f)),
                (new Vector3(-1.12f, 0f, 0f), new Vector3(0.16f, 0.7f, 1f)), (new Vector3(1.12f, 0f, 0f), new Vector3(0.16f, 0.7f, 1f)),
            })
            {
                L3Build.Fit(trough, "K:SM_Bld_Castle_Wall_01", 0, s, new Vector2(c.x, c.z), 0f, KDark);
            }

            Generated(trough, "QuenchWater", new Vector3(0f, 0.55f, 0f), 0f, Pitch, m => L4Shapes.Box(m, 0, Vector3.zero, Quaternion.identity, new Vector3(2.1f, 0.04f, 0.7f), new Vector2(0.5f, 0.5f)));
            var tcol = L3Build.Child(trough, "Collider");
            L3Build.StandingBox(tcol, new Vector3(2.4f, 0.75f, 1f));
            tcol.layer = L3Build.Layer(L3Build.Cover);
            n++;

            for (int i = 0; i < 4; i++)
            {
                float x = 66.6f + i * 0.35f;
                L4Stronghold.Beam(t, new Vector3(x, 0f, 62.9f), new Vector3(x + 0.15f, 2.4f + i * 0.15f, 63.85f), 0.09f, Iron);
            }

            // Coal and fuel under a lean-to at the west end of the forge.
            L4Stronghold.Prop(t, "K:SM_Bld_Leanto_01", new Vector3(63.6f, 0f, 62.3f), 180f, 1f, KSoot, true);
            L4Stronghold.Prop(t, "A:SM_Prop_Logpile_01", new Vector3(63.6f, 0f, 62.6f), 0f, 1f, ASoot, false);
            L4Stronghold.Prop(t, "A:SM_Prop_Barrel_01", new Vector3(76.4f, 0f, 63.3f), 40f, 1f, ASoot, true);
            L4Stronghold.Prop(t, "A:SM_Prop_Barrel_02", new Vector3(66.2f, 0f, 60.6f), 110f, 1f, ASoot, true);
            // The smiths, where they fell at their work.
            Skeleton(t, "Supine", new Vector3(73.2f, 0f, 60.8f), 80f);
            Skeleton(t, "Curled", new Vector3(77.2f, 0f, 61.1f), 300f);
            return n + 9;
        }

        // ---------------------------------------------------------------- The Lists gate

        // The lane north to the Lists: braziers either side of its mouth and halfway, a cart abandoned against the
        // wall, the dead who ran for the Lists.
        private static int ListsGate(Transform g)
        {
            var t = L3Build.Child(g, "ListsGate").transform;
            L4Stronghold.Brazier(t, new Vector3(55.3f, 0f, 66.2f));
            L4Stronghold.Brazier(t, new Vector3(62.7f, 0f, 76.5f));
            L4Stronghold.Brazier(t, new Vector3(55.3f, 0f, 84.2f));
            L4Stronghold.Prop(t, "K:SM_Prop_Cart_01", new Vector3(62.4f, 0f, 69.6f), 170f, 1f, KSoot, true);
            Skeleton(t, "Prone", new Vector3(58.2f, 0f, 80.6f), 10f);
            Skeleton(t, "Curled", new Vector3(56.3f, 0f, 73.4f), 140f);
            return 6;
        }

        // ---------------------------------------------------------------- D7, the barracks yard

        private static int Barracks(Transform g)
        {
            var t = L3Build.Child(g, "Barracks").transform;
            var rng = new System.Random(931);
            int n = 0;
            // Lean-tos along the west wall, tents along the north; one tent burnt to its frame.
            L4Stronghold.Prop(t, "K:SM_Bld_Leanto_01", new Vector3(37.8f, 0f, 50f), 90f, 1f, KSoot, true);
            L4Stronghold.Prop(t, "K:SM_Bld_Leanto_01", new Vector3(37.8f, 0f, 55.6f), 90f, 1f, KSoot, true);
            L4Stronghold.Prop(t, "K:SM_Bld_Tent_02", new Vector3(44.4f, 0f, 61.4f), 180f, 0.7f, Canvas, false);
            L4Stronghold.Prop(t, "K:SM_Bld_Tent_03", new Vector3(48.8f, 0f, 61f), 180f, 0.65f, Canvas, false);
            L2Fire.Fire(t, "TentFire", new Vector3(48.8f, 0.2f, 61f), 1.2f, 3f, 10f, true, new Vector3(2.6f, 0f, 2.6f));
            n += 4;

            // Bunks in the open where the lean-tos ran out, their sleepers still in them.
            foreach (var (x, z, yaw, sleeper) in new[] { (44f, 48.6f, 90f, true), (44f, 52.4f, 90f, false), (48.2f, 48.6f, 90f, true) })
            {
                var bunk = L3Build.Child(t, "Bunk", new Vector3(x, 0f, z), new Vector3(0f, yaw, 0f)).transform;
                foreach (var (lx, lz) in new[] { (-0.95f, -0.42f), (0.95f, -0.42f), (-0.95f, 0.42f), (0.95f, 0.42f) })
                {
                    L4Stronghold.Beam(bunk, new Vector3(lx, 0f, lz), new Vector3(lx, 0.62f, lz), 0.09f, Charred);
                }

                for (int k = 0; k < 4; k++)
                {
                    Plank(bunk, new Vector3(2.05f, 0.05f, 0.22f), new Vector3(0f, 0.48f, -0.33f + k * 0.22f), 0f, rng.Next(100000), k == 2);
                }

                if (sleeper)
                {
                    Skeleton(bunk, "Supine", bunk.TransformPoint(new Vector3(0f, 0.52f, 0f)), yaw + 90f);
                }

                var col = L3Build.Child(bunk, "Collider");
                L3Build.StandingBox(col, new Vector3(2.1f, 0.65f, 0.95f));
                col.layer = L3Build.Layer(L3Build.Cover);
                n++;
            }

            // The fire they sat round, still smouldering; their kit stacked against the rampart.
            L4Stronghold.Prop(t, "K:SM_Prop_CampFire_01", new Vector3(46.4f, 0f, 56.2f), 20f, 1.1f, KSoot, false);
            L2Fire.Smoulder(t, "CampSmoulder", new Vector3(46.4f, 0.1f, 56.2f), new Vector2(1.4f, 1.4f));
            L4Stronghold.Stores(t, new Vector3(49.6f, 0f, 43.1f), 0f, 933);
            Skeleton(t, "Curled", new Vector3(47.6f, 0f, 57.6f), 210f);
            Skeleton(t, "Prone", new Vector3(50.4f, 0f, 47.2f), 30f);
            n += 4;

            // The breach in the forge yard's wall: fallen blocks at its jambs.
            for (int i = 0; i < 9; i++)
            {
                float side = i % 2 == 0 ? 51.4f : 56.6f;
                L4Kit.Fallen(t, new Vector3(54.2f + (float)rng.NextDouble() * 1.8f, 0f, side + ((float)rng.NextDouble() - 0.5f) * 1.6f), rng);
            }

            return n + 9;
        }

        // ---------------------------------------------------------------- The muster rampart (out of reach)

        private static int Rampart(Transform g)
        {
            var t = L3Build.Child(g, "MusterRampart").transform;
            L4Stronghold.Watchtower(t, new Vector3(39.4f, T1, 38.4f), 180f, 7f, false, 941);
            L4Level.PutPrefab("C07", t, new Vector3(60f, T1, 35.8f), 180f);
            L4Stronghold.Brazier(t, new Vector3(45.6f, T1, 38f));
            L4Stronghold.Prop(t, "K:SM_Bld_Leanto_01", new Vector3(53.4f, T1, 40.5f), 180f, 1f, KSoot, false);
            L4Stronghold.Stores(t, new Vector3(69.6f, T1, 40.4f), 180f, 943, false);
            L4Stronghold.Flag(t, new Vector3(74.6f, T1, 35.2f), 180f, 1.2f);
            Skeleton(t, "Supine", new Vector3(57.2f, T1, 37.6f), 100f);
            return 7;
        }
    }
}
