using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Houses that are actually burning (flames from the model itself, not a glowing bed beside it), and the
    // intact, lived-in houses the raid passed by. Burning houses are Synty shells cut along a burn line
    // (L2Ruins.Clip) with the fire placed where the house is failing: the fallen roof end, the upper floor,
    // the collapsed wall. Each carries its own flickering light, so the burning houses are the lamps that
    // lead the player through the village and break up the moonlight.
    public static class L2Houses
    {
        public const string Folder = L2Build.PrefabsPath + "/Houses";

        public static readonly string[] Burning = { "Houses/L2_BurningHouse_Roof", "Houses/L2_BurningHouse_Tall", "Houses/L2_BurningHouse_Collapsed" };

        // Intact village houses (the night palette, not the burnt one): these are why the village reads as lived in.
        public static readonly string[] Intact =
        {
            "K/Buildings/SM_Bld_House_Room_01", "K/Buildings/SM_Bld_House_Room_02", "K/Buildings/SM_Bld_House_Room_03",
            "K/Buildings/SM_Bld_House_Room_05", "K/Buildings/SM_Bld_House_Room_06", "K/Buildings/SM_Bld_House_Room_07",
            "K/Buildings/SM_Bld_House_RoomTop_01", "K/Buildings/SM_Bld_House_RoomTop_03", "K/Buildings/SM_Bld_House_RoomTop_05",
            "K/Buildings/SM_Bld_House_RoomTall_01", "K/Buildings/SM_Bld_House_RoomTall_03", "K/Buildings/SM_Bld_House_TopRoomSmall_02",
        };

        public const float HouseLight = 55f;

        [MenuItem("Kneel/L2/Build/Burning Houses")]
        public static void BuildMenu()
        {
            Debug.Log("[L2] " + BuildAll());
        }

        public static string BuildAll()
        {
            L2Build.EnsureFolder(Folder);
            string a = RoofFire();
            string b = TallFire();
            string c = CollapsedFire();
            AssetDatabase.SaveAssets();
            return "burning houses: " + a + ", " + b + ", " + c;
        }

        private static GameObject Shell(Transform parent, string mesh, string source, float height, Vector2 tilt, float jag, int seed)
        {
            var go = new GameObject("Shell");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = L2Ruins.Baked(mesh, source, height, tilt, jag, seed);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { L2Build.Mat("L2_Knights_Burnt"), L2Build.Mat("L2_Knights_Charred") };
            return go;
        }

        private static GameObject Timber(Transform parent, Vector3 p, Vector3 euler, float scale)
        {
            var go = L2Build.Spawn("K/Props/SM_Prop_Beam_01", parent, p, euler, scale);
            L1Build.StripColliders(go);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Knights_Charred");
            }

            return go;
        }

        // A: the front half of the roof has fallen into the house; fire roars up out of the gap, runs along the
        // broken roof edge, and glows out of the door. The back half still has its roof.
        private static string RoofFire()
        {
            var root = new GameObject("L2_BurningHouse_Roof");
            Shell(root.transform, "Burning_Roof_Room01", "SM_Bld_House_Room_01", 3.7f, new Vector2(0f, -0.36f), 0.55f, 31);
            Timber(root.transform, new Vector3(-0.6f, 1.4f, 1.6f), new Vector3(38f, 10f, 0f), 1.6f);
            Timber(root.transform, new Vector3(0.9f, 1.1f, 2.1f), new Vector3(52f, -25f, 0f), 1.5f);
            Timber(root.transform, new Vector3(0.2f, 0.3f, 2.6f), new Vector3(84f, 70f, 0f), 1.5f);
            Timber(root.transform, new Vector3(1.9f, 0.1f, 3.9f), new Vector3(90f, 30f, 0f), 1.2f);   // fallen out of the gable

            // Still burning where the roof came down; the rest has burnt down to embers and smoke.
            L2Fire.Fire(root.transform, "Fire_Gap", new Vector3(0.2f, 0.7f, 1.6f), 1.35f, HouseLight, 17f, true, new Vector3(2.4f, 0f, 1.8f), L2Fire.Shape.Box, 0.8f);
            L2Fire.Fire(root.transform, "Fire_RoofEdge", new Vector3(-0.8f, 3.5f, 0.3f), 0.7f, 0f, 0f, false);
            L2Fire.Smoulder(root.transform, "Smoulder_Door", new Vector3(0.6f, 0.05f, 3.6f), new Vector2(1.6f, 1f));
            L2Fire.GroundEmbers(root.transform, new Vector3(1.4f, 0f, 4.2f), new Vector2(2.2f, 1f));
            return Save(root);
        }

        // B: a narrow two-storey house whose upper floor is ablaze: the roof burnt through, fire standing out of
        // the top, and a window on the upper floor spilling flame.
        private static string TallFire()
        {
            var root = new GameObject("L2_BurningHouse_Tall");
            Shell(root.transform, "Burning_Tall_RoomTall02", "SM_Bld_House_RoomTall_02", 4.7f, new Vector2(0.28f, 0f), 0.5f, 33);
            Timber(root.transform, new Vector3(-0.5f, 4.2f, 0.2f), new Vector3(20f, 90f, 0f), 1.2f);
            Timber(root.transform, new Vector3(0.6f, 4.0f, -0.4f), new Vector3(-30f, 90f, 10f), 1.1f);
            Timber(root.transform, new Vector3(-2.3f, 0.12f, 1.1f), new Vector3(90f, 20f, 0f), 1.3f);
            Timber(root.transform, new Vector3(2.4f, 0.4f, -0.9f), new Vector3(70f, -40f, 0f), 1.2f);

            L2Fire.Fire(root.transform, "Fire_Top", new Vector3(0.3f, 3.9f, 0.2f), 1.2f, HouseLight, 16f, true, new Vector3(1.6f, 0f, 1.4f), L2Fire.Shape.Box, 0.8f);
            L2Fire.Fire(root.transform, "Fire_Window", new Vector3(0.1f, 3.0f, -1.75f), 0.5f, 0f, 0f, false);
            L2Fire.Smoulder(root.transform, "Smoulder_Beam", new Vector3(-2.3f, 0.1f, 1.1f), new Vector2(0.8f, 1.8f));
            return Save(root);
        }

        // C: the long west wall has come down; the roof slid off with it into a burning heap spilling into the
        // yard. What still stands burns along its top.
        private static string CollapsedFire()
        {
            var root = new GameObject("L2_BurningHouse_Collapsed");
            Shell(root.transform, "Burning_Collapsed_RoomTop03", "SM_Bld_House_RoomTop_03", 2.5f, new Vector2(0.48f, 0f), 0.6f, 35);
            var rng = new System.Random(35);
            for (int i = 0; i < 11; i++)
            {
                var p = new Vector3(-2.2f - (float)rng.NextDouble() * 2.2f, 0.15f + (float)rng.NextDouble() * 0.7f, ((float)rng.NextDouble() - 0.5f) * 5.6f);
                Timber(root.transform, p, new Vector3(55f + (float)rng.NextDouble() * 35f, (float)rng.NextDouble() * 180f, ((float)rng.NextDouble() - 0.5f) * 40f), 1.3f + (float)rng.NextDouble() * 0.6f);
            }

            var wheel = L2Build.Spawn("K/Props/SM_Prop_CartWheel_01", root.transform, new Vector3(-3.6f, 0.35f, 2.2f), new Vector3(0f, 30f, 70f));
            L1Build.StripColliders(wheel);
            var barrel = L2Build.Spawn("A/Props/SM_Prop_Barrel_01", root.transform, new Vector3(-4.1f, 0.35f, -1.6f), new Vector3(90f, 40f, 0f));
            L1Build.StripColliders(barrel);
            foreach (var r in barrel.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = L2Build.Mat("L2_Knights_Charred");
            }

            L2Fire.Fire(root.transform, "Fire_Heap", new Vector3(-2.8f, 0.4f, -0.6f), 1.1f, HouseLight, 16f, true, new Vector3(1.8f, 0f, 2.6f), L2Fire.Shape.Box, 0.8f);
            L2Fire.Fire(root.transform, "Fire_WallTop", new Vector3(2f, 2.9f, 1.8f), 0.45f, 0f, 0f, false);
            L2Fire.Smoulder(root.transform, "Smoulder_Heap", new Vector3(-3.2f, 0.1f, 1.8f), new Vector2(2f, 2.2f));
            L2Fire.Smoulder(root.transform, "Smoulder_Inside", new Vector3(0.3f, 0.1f, -1f), new Vector2(1.8f, 2.2f), 0f, false);
            return Save(root);
        }

        private static string Save(GameObject root)
        {
            L1Build.StripColliders(root);
            L1Build.SetStatic(root, L1Build.EnvironmentStatic);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is ParticleSystemRenderer))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }

            string name = root.name;
            L1Build.SavePrefab(root, Folder + "/" + name + ".prefab");
            return name;
        }
    }
}
