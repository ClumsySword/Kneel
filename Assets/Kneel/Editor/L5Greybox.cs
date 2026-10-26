using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Builds the L5 "The Throne Room" greybox, the brief's metrics test map: the long nave, the imperial stair, the
    // grand doors and the long throne hall, with the shrine, the royal guard, the boss and every phase's floor changes
    // as flat markers, the player and camera, and the fixed arena camera. Plan coordinates continue L4's (x east,
    // y north = Unity Z, axis x = 54; L4's doors at y 268); heights are L5's own, the nave floor at 0.
    // R is the shipped roll (PlayerCombatSettings.dodgeDistance, 3 m). Check() measures what the vault note quotes.
    public static class L5Greybox
    {
        public const float R = 3f;
        public const string ScenePath = L4Build.ScenesPath + "/L5_ThroneRoom.unity";
        public const string MaterialsPath = "Assets/Kneel/Materials/L5/Greybox";
        public const string RootName = "L5_Root";
        private const string OutFolder = "Temp/L5";

        // Heights: nave 0; the stair's landing, its gallery and the throne hall +9 (raised from 6 so the horseshoe stair
        // is a real climb, owner 2026-10-23: "epic and grand").
        public const float Hall = 9f;

        // The horseshoe stair: two flights on one circle round StairCentre, from the floor either side of the hall up to
        // the landing before the doors. The west flight runs from StairFrom down to StairTo degrees (counter-clockwise
        // from east, plan y north); the east flight mirrors it about x 54. The player climbs away from the camera,
        // curving in to meet the other flight at the top, never down-screen. The top tread is a wide wedge from
        // StairTopFrom, the ray through the landing's corner, so no lower step runs under the landing's edge (a 0.5 m
        // ledge there stopped the climb one step short).
        public static readonly Vector2 StairCentre = new Vector2(54f, 382f);
        public const float StairInner = 12f, StairOuter = 18f, StairFrom = 235f, StairTopFrom = 120.5f, StairTo = 110f;
        public const int StairSteps = 36;
        public static readonly Vector3 Centrepiece = new Vector3(54f, 0f, 380f);

        public static float StairAngle(int i, bool west)
        {
            float a = i >= StairSteps ? StairTo : Mathf.Lerp(StairFrom, StairTopFrom, i / (float)(StairSteps - 1));
            return west ? a : 180f - a;
        }

        public static float StairTop(int i) => Hall * (i + 1) / StairSteps;

        public static Vector3 StairPoint(float radius, float degrees, float h)
        {
            float a = degrees * Mathf.Deg2Rad;
            return P(StairCentre.x + radius * Mathf.Cos(a), StairCentre.y + radius * Mathf.Sin(a), h);
        }

        // The arena (section 5 of the note): the hall's north part, framed by one fixed camera.
        public static readonly Rect ArenaFloor = Rect.MinMaxRect(33f, 432f, 75f, 466.5f);
        public static readonly Rect DuelRing = Rect.MinMaxRect(46.5f, 433.5f, 61.5f, 466.5f);
        public static readonly float[] PillarRows = { 414f, 426f, 438f, 450f, 462f };
        public static readonly float[] PillarLines = { 39f, 69f };
        public static readonly Vector2[] Braziers = { new Vector2(39f, 444f), new Vector2(69f, 444f), new Vector2(39f, 456f), new Vector2(69f, 456f) };

        // Gameplay points, at floor height. The player starts 10 m inside the shut outer doors, so the follow camera
        // (6.4 m behind) starts inside the nave too (owner, 2026-10-23: "player should spawn within the hall not outside").
        public static readonly Vector3 PlayerStart = P(54f, 279f, 0f);
        public static readonly Vector3 StairFoot = P(54f, 360f, 0f);
        public static readonly Vector3 ShrineC = P(54f, 400.5f, Hall);
        public static readonly Vector3 AggroLine = P(54f, 432.5f, Hall);
        public static readonly Vector3 ChapelLore = P(36f, 310.5f, 0f);
        public static readonly Vector3 AlcoveReward = P(68.5f, 336f, 0f);
        public static readonly Vector3 Throne = P(54f, 471f, Hall + 0.6f);

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("Kneel/L5/Greybox/Build Scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Debug.Log("[L5] " + Build());
        }

        [MenuItem("Kneel/L5/Greybox/Check Metrics")]
        public static void CheckMenu()
        {
            Debug.Log("[L5] " + Check());
        }

        [MenuItem("Kneel/L5/Greybox/Show Phase 1")]
        public static void ShowPhase1() => ShowPhase(1);

        [MenuItem("Kneel/L5/Greybox/Show Phase 2")]
        public static void ShowPhase2() => ShowPhase(2);

        // Plan point (x, y) at height h, in Unity space.
        public static Vector3 P(float x, float y, float h) => new Vector3(x, h, y);

        // ---------------------------------------------------------------- Build

        public static string Build()
        {
            L2Build.EnsureFolder(MaterialsPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;

            Nave(Group(root, "Nave"));
            Stair(Group(root, "ImperialStair"));
            ThroneHall(Group(root, "ThroneHall"));
            Phase1(Group(root, "Phase1"));
            var p2 = Group(root, "Phase2");
            Phase2(p2);
            Points(Group(root, "Markers"));
            PlayerAndCameras(root);
            Sunlight(root);

            var surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = (1 << L3Build.Layer("Ground")) | (1 << L3Build.Layer("Obstacles"));
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            ShowPhase(1);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return "Built " + ScenePath + " (" + root.GetComponentsInChildren<Transform>(true).Length + " objects)";
        }

        // The Long Nave: one 90 m corridor, 5 R wide, the chapel (D1) off the west wall and the hidden alcove (D2) off
        // the east wall. Its statues, candelabras and their colliders are L5Level's.
        private static void Nave(Transform g)
        {
            Box(g, "Apron", 45f, 63f, 256f, 268f, -0.5f, 0f, "Floor", "Ground");
            Box(g, "Floor", 45f, 63f, 268f, 360f, -0.5f, 0f, "Floor", "Ground");
            Box(g, "OuterDoorWall_W", 45f, 51f, 268f, 269.5f, 0f, 3f, "Wall", "Obstacles");
            Box(g, "OuterDoorWall_E", 57f, 63f, 268f, 269.5f, 0f, 3f, "Wall", "Obstacles");

            // Side walls with the two doorways: D1 (west, open, lit) and D2 (east, behind a tapestry).
            Box(g, "Wall_W_S", 45f, 46.5f, 269.5f, 309f, 0f, 9f, "Wall", "Obstacles");
            Box(g, "Wall_W_N", 45f, 46.5f, 312f, 358.5f, 0f, 9f, "Wall", "Obstacles");
            Box(g, "Wall_E_S", 61.5f, 63f, 269.5f, 334.5f, 0f, 9f, "Wall", "Obstacles");
            Box(g, "Wall_E_N", 61.5f, 63f, 337.5f, 358.5f, 0f, 9f, "Wall", "Obstacles");

            // Burned lancets on the west wall every 9 m: their light stripes the walk.
            for (float y = 279f; y < 356f; y += 9f)
            {
                if (y > 305f && y < 316f)
                {
                    continue;
                }

                Box(g, "Lancet", 46.4f, 46.55f, y - 0.75f, y + 0.75f, 2.5f, 7.5f, "Ember", null);
            }

            // D1, the Master's private chapel.
            Box(g, "D1_Floor", 31.5f, 45f, 303f, 318f, -0.5f, 0f, "Floor", "Ground");
            Box(g, "D1_Wall_W", 30f, 31.5f, 301.5f, 319.5f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D1_Wall_N", 31.5f, 46.5f, 318f, 319.5f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D1_Wall_S", 31.5f, 46.5f, 301.5f, 303f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D1_Altar", 31.5f, 34.5f, 309f, 312f, 0f, 1.1f, "Stone", "Obstacles");
            Box(g, "D1_PrayerStool", 35.5f, 36.5f, 310f, 311f, 0f, 0.45f, "Wood", "Obstacles");

            // D2, the cupbearer's alcove, behind the tapestry; the scorched handprint on the corridor-side jamb.
            Box(g, "D2_Floor", 63f, 70.5f, 333f, 339f, -0.5f, 0f, "Floor", "Ground");
            Box(g, "D2_Wall_E", 70.5f, 72f, 331.5f, 340.5f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D2_Wall_N", 61.5f, 70.5f, 339f, 340.5f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D2_Wall_S", 61.5f, 70.5f, 331.5f, 333f, 0f, 6f, "Wall", "Obstacles");
            Box(g, "D2_Tapestry", 61.6f, 61.7f, 334.5f, 337.5f, 0.3f, 4.5f, "Cloth", null);
            Box(g, "D2_Handprint", 61.4f, 61.5f, 337.6f, 338.1f, 1.1f, 1.6f, "Ember", null);
        }

        // The imperial stair, a double horseshoe (owner's reference, 2026-10-23): two curved flights rise from the floor
        // either side of the stair hall and meet on a landing before the doors, the landing bridging an arched niche; an
        // upper gallery runs along the back wall. A round plinth with a great brazier stands in the well between them.
        // Balustrade colliders are 2.2 m tall, so nobody vaults a rail into the 9 m drop.
        private static void Stair(Transform g)
        {
            Box(g, "Floor", 33f, 75f, 358.5f, 406.5f, -0.5f, 0f, "Floor", "Ground");
            Box(g, "Wall_S_W", 33f, 46.5f, 358.5f, 360f, 0f, 9f, "Wall", "Obstacles");
            Box(g, "Wall_S_E", 61.5f, 75f, 358.5f, 360f, 0f, 9f, "Wall", "Obstacles");
            Box(g, "Wall_W", 33f, 34.5f, 360f, 406.5f, 0f, 15f, "Wall", "Obstacles");
            Box(g, "Wall_E", 73.5f, 75f, 360f, 406.5f, 0f, 15f, "Wall", "Obstacles");

            foreach (bool west in new[] { true, false })
            {
                var flight = Group(g, west ? "Flight_W" : "Flight_E");
                for (int i = 0; i < StairSteps; i++)
                {
                    float a0 = StairAngle(i, west), a1 = StairAngle(i + 1, west);

                    // Boxes of at most 4 degrees, so the wide top tread's box doesn't overhang its wedge.
                    int parts = Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 4f);
                    for (int k = 0; k < parts; k++)
                    {
                        float b0 = Mathf.Lerp(a0, a1, k / (float)parts), b1 = Mathf.Lerp(a0, a1, (k + 1) / (float)parts), mid = (b0 + b1) * 0.5f;
                        float depth = StairOuter * Mathf.Abs(b1 - b0) * Mathf.Deg2Rad * 1.08f;
                        RotBox(flight, "Step", StairPoint((StairInner + StairOuter) * 0.5f, mid, 0f), -mid, StairOuter - StairInner + 0.1f, depth, 0f, StairTop(i), "Stone", "Ground");
                    }

                    foreach (float r in new[] { StairInner + 0.12f, StairOuter - 0.12f })
                    {
                        Rail(flight, StairPoint(r, a0, 0f), StairPoint(r, a1, 0f), StairTop(i));
                    }
                }
            }

            // The landing over the niche, the niche's cheeks, the gallery, and their rails.
            Box(g, "Landing", 47f, 61f, 394f, 402f, Hall - 1.4f, Hall, "Stone", "Ground");
            Box(g, "NicheWall_W", 47f, 50.5f, 394f, 402f, 0f, Hall - 1.4f, "Stone", "Obstacles");
            Box(g, "NicheWall_E", 57.5f, 61f, 394f, 402f, 0f, Hall - 1.4f, "Stone", "Obstacles");
            Box(g, "Gallery", 34.5f, 73.5f, 402f, 406.5f, 0f, Hall, "Stone", "Ground");
            Rail(g, P(49.6f, 394.12f, 0f), P(58.4f, 394.12f, 0f), Hall);
            Rail(g, P(47.12f, 398.8f, 0f), P(47.12f, 402f, 0f), Hall);
            Rail(g, P(60.88f, 398.8f, 0f), P(60.88f, 402f, 0f), Hall);
            Rail(g, P(34.5f, 402.12f, 0f), P(47f, 402.12f, 0f), Hall);
            Rail(g, P(61f, 402.12f, 0f), P(73.5f, 402.12f, 0f), Hall);

            // The centrepiece: a round plinth and the great brazier on it.
            var plinth = new GameObject("Centrepiece_Plinth");
            plinth.transform.SetParent(g, false);
            plinth.transform.position = Centrepiece;
            plinth.layer = L3Build.Layer("Obstacles");
            L3Build.StandingCylinder(plinth, 6.6f, 0.9f);
            var brazier = new GameObject("Centrepiece_Brazier");
            brazier.transform.SetParent(g, false);
            brazier.transform.position = Centrepiece + Vector3.up * 0.9f;
            brazier.layer = L3Build.Layer("Obstacles");
            L3Build.StandingCylinder(brazier, 1.6f, 4.6f);
        }

        // A box turned 'yaw' degrees about Y, its base centred on 'baseCentre', sx along its local x, sz along local z.
        private static GameObject RotBox(Transform parent, string name, Vector3 baseCentre, float yaw, float sx, float sz, float h0, float h1, string material, string layer)
        {
            var go = Box(parent, name, -0.5f, 0.5f, -0.5f, 0.5f, h0, h1, material, layer);
            go.transform.position = new Vector3(baseCentre.x, (h0 + h1) * 0.5f, baseCentre.z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(sx, h1 - h0, sz);
            return go;
        }

        // A balustrade's collider from a to b (plan points), standing on 'floor', 2.2 m tall.
        private static void Rail(Transform parent, Vector3 a, Vector3 b, float floor)
        {
            var dir = b - a;
            dir.y = 0f;
            var mid = (a + b) * 0.5f;
            RotBox(parent, "Balustrade", mid, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0.25f, dir.magnitude + 0.05f, floor, floor + 2.2f, "Stone", "Obstacles");
        }

        // The throne hall: 14 R wide and 22 R long. The south 8 R is the processional between the doors and the arena;
        // the north 12 R is the arena, then the dais and the throne under the fallen roof, the apse breach behind it.
        private static void ThroneHall(Transform g)
        {
            Box(g, "Floor", 33f, 75f, 406.5f, 472.5f, 0f, Hall, "Floor", "Ground");
            Box(g, "DoorWall_W", 31.5f, 49.5f, 406.5f, 408f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "DoorWall_E", 58.5f, 76.5f, 406.5f, 408f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "DoorLintel", 49.5f, 58.5f, 406.5f, 408f, Hall + 9f, Hall + 12f, "Wall", "Obstacles");
            Box(g, "GrandDoor_W_Open", 49.5f, 50.1f, 408f, 412.5f, Hall, Hall + 9f, "Wood", "Obstacles");
            Box(g, "GrandDoor_E_Open", 57.9f, 58.5f, 408f, 412.5f, Hall, Hall + 9f, "Wood", "Obstacles");
            Box(g, "Wall_W", 31.5f, 33f, 408f, 472.5f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "Wall_E", 75f, 76.5f, 408f, 472.5f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "Apse_W", 31.5f, 48f, 472.5f, 474f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "Apse_E", 60f, 76.5f, 472.5f, 474f, Hall, Hall + 12f, "Wall", "Obstacles");
            Box(g, "ApseBreach_Sill", 48f, 60f, 472.5f, 474f, Hall, Hall + 0.9f, "Stone", "Obstacles");

            // Pillars: full height (12 m) down the processional, where the follow camera is close; broken to 4 m stumps
            // in the arena, under the fallen roof, because in the fixed frame a tall pillar on the side edge leans in
            // over the floor it stands beside. The middle pair still carries a roof beam that rests on the stump.
            var pillars = Group(g, "Pillars");
            foreach (float x in PillarLines)
            {
                foreach (float y in PillarRows)
                {
                    bool arena = y > ArenaFloor.yMin;
                    bool sagging = y == 450f;
                    string name = sagging ? "Pillar_Stump_SaggingBeam" : arena ? "Pillar_Stump" : "Pillar";
                    Box(pillars, name, x - 1.5f, x + 1.5f, y - 1.5f, y + 1.5f, Hall, Hall + (arena ? 4f : 12f), sagging ? "PillarCracked" : "Stone", "Obstacles");
                    if (sagging)
                    {
                        float wall = x < 54f ? 33f : 75f;
                        Box(pillars, "SaggingBeam", Mathf.Min(wall, x), Mathf.Max(wall, x), y - 0.5f, y + 0.5f, Hall + 4f, Hall + 4.8f, "Debris", null);
                    }
                }
            }

            // The dais: three 0.2 m steps; the throne; the old roof-fall heaped either side of it.
            Box(g, "Dais_1", 45f, 63f, 466.5f, 472.5f, Hall, Hall + 0.2f, "Stone", "Ground");
            Box(g, "Dais_2", 45.5f, 62.5f, 467f, 472.5f, Hall, Hall + 0.4f, "Stone", "Ground");
            Box(g, "Dais_3", 46f, 62f, 467.5f, 472.5f, Hall, Hall + 0.6f, "Stone", "Ground");
            Box(g, "Throne", 52.5f, 55.5f, 470f, 472.5f, Hall + 0.6f, Hall + 4.1f, "Throne", "Obstacles");
            Box(g, "Rubble_W", 33f, 42f, 466.5f, 472.5f, Hall, Hall + 1.8f, "Rubble", "Obstacles");
            Box(g, "Rubble_E", 66f, 75f, 466.5f, 472.5f, Hall, Hall + 1.8f, "Rubble", "Obstacles");
        }

        // Phase 1, The Audience: four standing braziers in the pillar lines, between pillars.
        private static void Phase1(Transform g)
        {
            foreach (var b in Braziers)
            {
                Box(g, "Brazier", b.x - 0.5f, b.x + 0.5f, b.y - 0.5f, b.y + 0.5f, Hall, Hall + 1.8f, "Iron", "Obstacles");
                Box(g, "Brazier_Coals", b.x - 0.45f, b.x + 0.45f, b.y - 0.45f, b.y + 0.45f, Hall + 1.8f, Hall + 2f, "Ember", null);
            }

            // The spill a toppled brazier leaves (example, west-south brazier knocked east): 1 R square, 8 s.
            Zone(g, "Zone_SpillStrip_Example", 40.5f, 43.5f, 442.5f, 445.5f, "ZoneSpill");
        }

        // Phase 2, The Burning, after Transition 1: three roof beams down (debris, cover), the four hearths flooded
        // (fire pools between the pillars), the sagging-beam stubs (only if his charge hits pillar 450), the smoke bands.
        private static void Phase2(Transform g)
        {
            foreach (var b in Braziers)
            {
                Zone(g, "Zone_FirePool", b.x - 1.5f, b.x + 1.5f, b.y - 1.5f, b.y + 1.5f, "ZoneFire");
                Box(g, "Brazier_Toppled", b.x - 0.9f, b.x + 0.9f, b.y - 0.5f, b.y + 0.5f, Hall, Hall + 0.6f, "Iron", null);
            }

            Box(g, "Debris_B1", 46.5f, 49.5f, 437f, 443f, Hall, Hall + 1.4f, "Debris", "Obstacles");
            Box(g, "Debris_B2", 58.5f, 61.5f, 437f, 443f, Hall, Hall + 1.4f, "Debris", "Obstacles");
            Box(g, "Debris_B3", 51f, 57f, 457.5f, 460.5f, Hall, Hall + 1.4f, "Debris", "Obstacles");
            Zone(g, "Zone_PillarBeam_W", 40.5f, 43.5f, 448.5f, 451.5f, "ZoneBeam");
            Zone(g, "Zone_PillarBeam_E", 64.5f, 67.5f, 448.5f, 451.5f, "ZoneBeam");
            Zone(g, "Zone_SmokeBand_W", 33f, 40.5f, 432f, 466.5f, "ZoneSmoke");
            Zone(g, "Zone_SmokeBand_E", 67.5f, 75f, 432f, 466.5f, "ZoneSmoke");
            // An example roof-fall shadow (P2, 1.5 s tell, shatters on impact, nothing left behind).
            Zone(g, "Zone_RoofFall_Example", 51f, 54f, 448.5f, 454.5f, "ZoneBeam");
        }

        private static void Points(Transform g)
        {
            Zone(g, "Zone_DuelRing", DuelRing.xMin, DuelRing.xMax, DuelRing.yMin, DuelRing.yMax, "ZoneRing", Hall + 0.005f);
            Zone(g, "Zone_FireLine_ArenaSeal", 33f, 75f, 431.5f, 432.5f, "ZoneSeal");
            Zone(g, "Door_Outer", 51f, 57f, 268f, 269.5f, "Wood", 0f);
            Zone(g, "Door_Grand", 49.5f, 58.5f, 406.5f, 408f, "Wood", Hall);
            Pin(g, "ShrineB_LevelStart", P(54f, 263.6f, 0f), "Shrine", 1.2f);
            Pin(g, "ShrineC_LastShrine", ShrineC, "Shrine", 1.2f);
            Pin(g, "RoyalGuard_UprightKneeler", P(54f, 373.5f, 0f), "Enemy", 2.1f);
            Pin(g, "Master_OnThrone", Throne, "Boss", 2.8f);
            Pin(g, "Herald", P(54f, 355f, 0f), "Bone", 0.4f);
            // Stand-ins at fighting distance, to judge scale in the fixed frame.
            Pin(g, "StandIn_Player", P(54f, 446f, Hall), "Shrine", 1.95f);
            Pin(g, "StandIn_Master", P(54f, 452f, Hall), "Enemy", 2.8f);
            Pin(g, "D1_Lore_MastersBook", ChapelLore, "Lore", 0.6f);
            Pin(g, "D2_Reward_Consumable", AlcoveReward, "Lore", 0.6f);
        }

        private static void PlayerAndCameras(Transform root)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Player/Player.prefab"));
            player.name = "Player";
            player.transform.SetPositionAndRotation(PlayerStart + Vector3.up * 0.05f, Quaternion.identity);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 500f;
            camera.AddComponent<AudioListener>();
            var playerCamera = camera.AddComponent<PlayerCamera>();
            L3Build.Set(playerCamera, "settings", AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/Kneel/Player/Settings/PlayerCameraSettings.asset"));
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 8.7f, -3.6f), Quaternion.Euler(65f, 0f, 0f));
            camera.AddComponent<Kneel.OcclusionFader>();

            // The fixed arena frame (CM_Arena in the note), solved to show the whole arena; disabled, for captures.
            var arena = new GameObject("CM_Arena_FixedFrame");
            arena.transform.SetParent(root, false);
            var arenaCam = arena.AddComponent<Camera>();
            arenaCam.enabled = false;
            var frame = SolveArenaFrame(ArenaPitch, ArenaFov);
            arenaCam.fieldOfView = ArenaFov;
            arenaCam.farClipPlane = 500f;
            arena.transform.SetPositionAndRotation(frame.position, frame.rotation);
        }

        private static void Sunlight(Transform root)
        {
            var sun = new GameObject("Sun (dusk)");
            sun.transform.SetParent(root, false);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.color = new Color(1f, 0.82f, 0.66f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.3f, 0.3f);
        }

        public static void ShowPhase(int phase)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                return;
            }

            root.transform.Find("Phase1").gameObject.SetActive(phase == 1);
            root.transform.Find("Phase2").gameObject.SetActive(phase >= 2);
        }

        // ---------------------------------------------------------------- The fixed arena camera

        public const float ArenaPitch = 55f, ArenaFov = 40f;
        private const float Aspect = 16f / 9f;

        // What the fixed frame must hold: the whole arena floor from the sealing fire line to the apse, its full width
        // wall to wall, and standing height at both ends (the Master on the dais, the player at the fire line).
        private static IEnumerable<Vector3> FramePoints()
        {
            foreach (float x in new[] { 33f, 75f })
            {
                foreach (float y in new[] { 431.5f, 474f })
                {
                    yield return P(x, y, Hall);
                    yield return P(x, y, Hall + 2.8f);
                }
            }

            yield return P(54f, 472.5f, Hall + 4.1f);
        }

        // The nearest camera (straight up the axis, fixed pitch and FOV, 16:9) that holds every frame point with a 3%
        // margin: a search over where on the axis it looks, then the distance.
        public static Pose SolveArenaFrame(float pitch, float fov)
        {
            var go = new GameObject("FrameSolver") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.aspect = Aspect;
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var best = new Pose(Vector3.zero, rotation);
            float bestDistance = float.MaxValue;
            for (float y = 440f; y <= 470f; y += 0.25f)
            {
                var focus = P(54f, y, Hall);
                float lo = 5f, hi = 200f;
                if (!Fits(cam, focus, rotation, hi))
                {
                    continue;
                }

                for (int i = 0; i < 30; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Fits(cam, focus, rotation, mid)) hi = mid; else lo = mid;
                }

                if (hi < bestDistance)
                {
                    bestDistance = hi;
                    best = new Pose(focus - rotation * Vector3.forward * hi, rotation);
                }
            }

            Object.DestroyImmediate(go);
            return best;
        }

        private static bool Fits(Camera cam, Vector3 focus, Quaternion rotation, float distance)
        {
            cam.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            const float m = 0.03f;
            foreach (var p in FramePoints())
            {
                var v = cam.WorldToViewportPoint(p);
                if (v.z <= 0f || v.x < m || v.x > 1f - m || v.y < m || v.y > 1f - m)
                {
                    return false;
                }
            }

            return true;
        }

        // Screen height in pixels at 1080p of a figure of the given height standing at a plan point.
        private static float PixelHeight(Camera cam, Vector3 feet, float height)
        {
            var a = cam.WorldToViewportPoint(feet);
            var b = cam.WorldToViewportPoint(feet + Vector3.up * height);
            return (b.y - a.y) * 1080f;
        }

        // ---------------------------------------------------------------- Check

        // Measures the note's numbers in the built scene and writes them to Temp/L5/metrics.txt: gaps between solids and
        // hazards in the arena, the fixed frame, runback and approach path lengths on a fresh NavMesh, captures.
        public static string Check()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                return "Open " + ScenePath + " first.";
            }

            Directory.CreateDirectory(OutFolder);
            var log = new List<string> { "L5 greybox metrics, R = " + R + " m, sprint " + Sprint.ToString("0.00") + " m/s" };

            // Footprints inside the arena (plan rects), by kind.
            ShowPhase(2);
            Physics.SyncTransforms();
            var solids = new List<(string name, Rect r)>();
            var hazards = new List<(string name, Rect r)>();
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                var rend = t.GetComponent<Renderer>();
                if (rend == null || t.name.StartsWith("Brazier") || t.name.StartsWith("Zone_RoofFall"))
                {
                    continue;
                }

                var b = rend.bounds;
                var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (!rect.Overlaps(Rect.MinMaxRect(33f, 431.5f, 75f, 472.5f)) || b.max.y < Hall - 0.1f)
                {
                    continue;
                }

                if (t.name.StartsWith("Zone_FirePool") || t.name.StartsWith("Zone_PillarBeam") || t.name.StartsWith("Zone_Smoke"))
                {
                    hazards.Add((t.name, rect));
                }
                else if (t.GetComponent<Collider>() != null && t.gameObject.layer == L3Build.Layer("Obstacles") && b.size.y > 1f)
                {
                    solids.Add((t.name, rect));
                }
            }

            // Spill strip and P1 braziers.
            ShowPhase(1);
            foreach (var t in root.transform.Find("Phase1").GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("Zone_Spill"))
                {
                    var b = t.GetComponent<Renderer>().bounds;
                    hazards.Add((t.name, Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z)));
                }
            }

            float minSolidGap = float.MaxValue;
            string minSolidPair = "";
            for (int i = 0; i < solids.Count; i++)
            {
                for (int j = i + 1; j < solids.Count; j++)
                {
                    float d = Gap(solids[i].r, solids[j].r);
                    if (d > 0.01f && d < minSolidGap)
                    {
                        minSolidGap = d;
                        minSolidPair = solids[i].name + " / " + solids[j].name;
                    }
                }
            }

            log.Add("Arena solids: " + solids.Count + ". Smallest gap between two solids that do not touch: " + minSolidGap.ToString("0.00") + " m (" + minSolidPair + ")");

            float ringToHazard = hazards.Min(h => Gap(h.r, DuelRing));
            log.Add("Hazard footprints: " + hazards.Count + ". Smallest gap from the duel ring to a hazard: " + ringToHazard.ToString("0.00") + " m");
            foreach (var h in hazards.GroupBy(h => h.name.Split('_')[1]))
            {
                float toSolid = h.Min(z => solids.Where(s => !s.name.StartsWith("Wall") || !z.name.Contains("Smoke")).Min(s => Gap(z.r, s.r) < 0.01f ? 999f : Gap(z.r, s.r)));
                float toRing = h.Min(z => Gap(z.r, DuelRing));
                log.Add("  " + h.Key + ": to the ring " + toRing.ToString("0.00") + " m, to the nearest solid it does not touch " + toSolid.ToString("0.00") + " m");
            }

            // NavMesh paths (a fresh in-memory bake).
            var surface = root.GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
            log.Add(PathLine("Runback: Shrine C to the arena's aggro line", ShrineC, AggroLine));
            log.Add(PathLine("Approach: level start to the stair foot", PlayerStart, StairFoot));
            log.Add(PathLine("Approach: level start to Shrine C", PlayerStart, ShrineC));
            log.Add(PathLine("D1 detour: nave axis to the Master's book", P(54f, 310.5f, 0f), ChapelLore));
            log.Add(PathLine("D2 detour: nave axis to the alcove reward", P(54f, 336f, 0f), AlcoveReward));
            log.Add(PathLine("Aggro line to the Master's dais foot", AggroLine, P(54f, 465f, Hall)));

            // The fixed frame, for a few lenses.
            var probe = new GameObject("FrameProbe") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            probe.aspect = Aspect;
            foreach (float fov in new[] { 40f, 45f, 50f, 55f })
            {
                var pose = SolveArenaFrame(ArenaPitch, fov);
                probe.fieldOfView = fov;
                probe.transform.SetPositionAndRotation(pose.position, pose.rotation);
                float h = pose.position.y - Hall;
                log.Add("Arena frame, pitch " + ArenaPitch + ", FOV " + fov + ": camera " + h.ToString("0.0") + " m above the floor at plan y " + pose.position.z.ToString("0.0") +
                        ", player 1.95 m = " + PixelHeight(probe, P(54f, 450f, Hall), 1.95f).ToString("0") + " px at 1080p mid-arena, " + PixelHeight(probe, P(54f, 434f, Hall), 1.95f).ToString("0") + " px at the fire line; Master 2.8 m = " +
                        PixelHeight(probe, P(54f, 465f, Hall), 2.8f).ToString("0") + " px at the dais foot");
            }

            var gameplay = new GameObject("GameplayProbe") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            gameplay.aspect = Aspect;
            gameplay.fieldOfView = 60f;
            var playerFeet = P(54f, 300f, 0f);
            gameplay.transform.SetPositionAndRotation(playerFeet + Vector3.up * 1f + Quaternion.Euler(65f, 0f, 0f) * Vector3.back * 8.5f, Quaternion.Euler(65f, 0f, 0f));
            log.Add("Gameplay camera (PlayerCamera, 65 deg, 8.5 m, FOV 60): player = " + PixelHeight(gameplay, playerFeet, 1.95f).ToString("0") + " px at 1080p");
            Object.DestroyImmediate(probe.gameObject);
            Object.DestroyImmediate(gameplay.gameObject);

            // Captures from the fixed frame, one per phase, and two approach views.
            var arenaCam = root.transform.Find("CM_Arena_FixedFrame").GetComponent<Camera>();
            ShowPhase(1);
            Capture(arenaCam, "arena_phase1.png");
            ShowPhase(2);
            Capture(arenaCam, "arena_phase2.png");
            ShowPhase(1);
            CaptureFrom(P(54f, 266f, 0f) + new Vector3(0f, 7f, 0f), Quaternion.Euler(14f, 0f, 0f), 45f, "vista_nave.png");
            CaptureFrom(P(54f, 409f, Hall) + new Vector3(0f, 1.6f, 0f), Quaternion.Euler(-2f, 0f, 0f), 50f, "reveal_hall.png");
            CaptureFrom(P(54f, 369f, 0f) + new Vector3(0f, 85f, 0f), Quaternion.Euler(90f, 0f, 0f), 60f, "overview_top.png", 112f);
            CaptureFrom(P(54f, 383f, 0f) + new Vector3(0f, 40f, 0f), Quaternion.Euler(60f, 0f, 0f) * Quaternion.identity, 50f, "stair.png");
            CaptureFrom(P(54f, 440f, Hall) + new Vector3(0f, 70f, 0f), Quaternion.Euler(90f, 0f, 0f), 60f, "overview_hall_top.png", 60f);

            string text = string.Join("\n", log);
            File.WriteAllText(Path.Combine(OutFolder, "metrics.txt"), text);
            return text;
        }

        // ---------------------------------------------------------------- ASCII maps

        [MenuItem("Kneel/L5/Greybox/Write ASCII Maps")]
        public static void AsciiMenu()
        {
            Debug.Log("[L5] " + Ascii());
        }

        // The note's maps, drawn from the built scene and written to Temp/L5/ascii.txt: every renderer's plan footprint,
        // by name, onto a grid. A cell takes the highest-priority symbol covering at least half of it (pieces of one kind
        // add up, so a flight of steps fills its cells); pieces smaller than a cell in both directions (people, braziers,
        // kneelers) mark the cell holding their centre, and thin lines (the seal, the tapestry) mark every cell they touch.
        // The duel ring is drawn by its side edges only.
        public static string Ascii()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                return "Open " + ScenePath + " first.";
            }

            Directory.CreateDirectory(OutFolder);
            var sb = new System.Text.StringBuilder();
            ShowPhase(1);
            sb.AppendLine("OVERVIEW, phase 1. 1 column = 3 m (1 R), 1 row = 6 m (2 R).");
            sb.AppendLine(Grid(root, 30f, 78f, 3f, 259.5f, 475.5f, 6f));
            sb.AppendLine("ARENA, phase 1. 1 column = 1.5 m (0.5 R), 1 row = 3 m (1 R).");
            sb.AppendLine(Grid(root, 31.5f, 76.5f, 1.5f, 424.5f, 475.5f, 3f));
            ShowPhase(2);
            sb.AppendLine("ARENA, phase 2. 1 column = 1.5 m (0.5 R), 1 row = 3 m (1 R).");
            sb.AppendLine(Grid(root, 31.5f, 76.5f, 1.5f, 424.5f, 475.5f, 3f));
            ShowPhase(1);
            File.WriteAllText(Path.Combine(OutFolder, "ascii.txt"), sb.ToString());
            return sb.ToString();
        }

        private static string Grid(GameObject root, float x0, float x1, float cw, float y0, float y1, float ch)
        {
            int cols = Mathf.RoundToInt((x1 - x0) / cw), rows = Mathf.RoundToInt((y1 - y0) / ch);
            var area = new Dictionary<(int, int, char), float>();
            var best = new (char symbol, int priority)[cols, rows];
            var ring = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    best[c, r] = (' ', -1);
                }
            }

            foreach (var rend in root.GetComponentsInChildren<Renderer>())
            {
                var (symbol, priority, kind) = Symbol(rend.name);
                if (symbol == '\0')
                {
                    continue;
                }

                var b = rend.bounds;
                var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (kind == 'p' || (rect.width <= cw + 0.01f && rect.height <= ch + 0.01f && kind != 'l'))
                {
                    int c = Mathf.FloorToInt((rect.center.x - x0) / cw), r = Mathf.FloorToInt((rect.center.y - y0) / ch);
                    Mark(best, cols, rows, c, r, symbol, priority);
                    continue;
                }

                for (int c = Mathf.Max(0, Mathf.FloorToInt((rect.xMin - x0) / cw)); c < cols && x0 + c * cw < rect.xMax; c++)
                {
                    for (int r = Mathf.Max(0, Mathf.FloorToInt((rect.yMin - y0) / ch)); r < rows && y0 + r * ch < rect.yMax; r++)
                    {
                        var cell = Rect.MinMaxRect(x0 + c * cw, y0 + r * ch, x0 + (c + 1) * cw, y0 + (r + 1) * ch);
                        float overlap = Mathf.Max(0f, Mathf.Min(cell.xMax, rect.xMax) - Mathf.Max(cell.xMin, rect.xMin)) * Mathf.Max(0f, Mathf.Min(cell.yMax, rect.yMax) - Mathf.Max(cell.yMin, rect.yMin));
                        if (overlap <= 0.0001f)
                        {
                            continue;
                        }

                        if (kind == 'l')
                        {
                            Mark(best, cols, rows, c, r, symbol, priority);
                            continue;
                        }

                        area.TryGetValue((c, r, symbol), out float sum);
                        sum += overlap;
                        area[(c, r, symbol)] = sum;
                        if (sum >= cw * ch * 0.5f - 0.001f)
                        {
                            if (kind == 'e') ring[c, r] = true;
                            else Mark(best, cols, rows, c, r, symbol, priority);
                        }
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            for (int r = rows - 1; r >= 0; r--)
            {
                float y = y0 + r * ch;
                sb.Append((rows - 1 - r) % 4 == 0 ? y.ToString("0.#").PadLeft(5) + " |" : "      |");
                for (int c = 0; c < cols; c++)
                {
                    char s = best[c, r].symbol;
                    bool ringEdge = ring[c, r] && (c == 0 || !ring[c - 1, r] || c == cols - 1 || !ring[c + 1, r]);
                    if (ringEdge && best[c, r].priority <= 1)
                    {
                        s = ':';
                    }

                    sb.Append(s);
                }

                sb.AppendLine();
            }

            sb.Append("      +").AppendLine(new string('-', cols));
            var axis = new string(' ', cols + 12).ToCharArray();
            for (int c = 0; c < cols; c++)
            {
                float x = x0 + c * cw;
                if (Mathf.Approximately(Mathf.Repeat(x, 12f), 0f))
                {
                    x.ToString("0").CopyTo(0, axis, 7 + c, 2);
                }
            }

            sb.AppendLine(new string(axis).TrimEnd() + "   x (m); rows labelled by their south edge, y (m)");
            return sb.ToString();
        }

        private static void Mark((char symbol, int priority)[,] best, int cols, int rows, int c, int r, char symbol, int priority)
        {
            if (c >= 0 && c < cols && r >= 0 && r < rows && priority > best[c, r].priority)
            {
                best[c, r] = (symbol, priority);
            }
        }

        // Symbol, priority and kind (a area, p point, l line, e ring edge) for a greybox piece, by name.
        private static (char, int, char) Symbol(string name)
        {
            if (name.StartsWith("StandIn") || name.StartsWith("GrandDoor") || name == "DoorLintel" || name == "SaggingBeam" || name.StartsWith("Brazier_") || name.StartsWith("D1_PrayerStool") || name.StartsWith("Balustrade")) return ('\0', 0, 'a');
            if (name.StartsWith("ShrineB")) return ('B', 20, 'p');
            if (name.StartsWith("ShrineC")) return ('C', 20, 'p');
            if (name.StartsWith("RoyalGuard")) return ('G', 20, 'p');
            if (name.StartsWith("Master")) return ('M', 20, 'p');
            if (name == "Herald") return ('h', 20, 'p');
            if (name.StartsWith("D1_Lore") || name.StartsWith("D2_Reward")) return ('+', 20, 'p');
            if (name == "D2_Handprint") return ('?', 19, 'p');
            if (name == "D2_Tapestry") return ('%', 18, 'l');
            if (name == "Lancet") return ('L', 18, 'p');
            if (name.StartsWith("Door_")) return ('X', 17, 'l');
            if (name == "Throne") return ('T', 16, 'p');
            if (name.StartsWith("KingStatue")) return ('S', 16, 'p');
            if (name == "D1_Altar") return ('a', 16, 'p');
            if (name == "Brazier") return ('b', 16, 'p');
            if (name.StartsWith("ApseBreach")) return ('~', 15, 'a');
            if (name.Contains("Wall") || name.StartsWith("Apse_")) return ('#', 14, 'a');
            if (name.StartsWith("Pillar_Stump_Sagging")) return ('Y', 12, 'a');
            if (name.StartsWith("Pillar_Stump")) return ('i', 12, 'a');
            if (name.StartsWith("Pillar")) return ('I', 12, 'a');
            if (name.StartsWith("Debris")) return ('H', 11, 'a');
            if (name.StartsWith("Zone_FireLine")) return ('-', 10, 'l');
            if (name.StartsWith("Zone_FirePool")) return ('*', 10, 'a');
            if (name.StartsWith("Zone_SpillStrip")) return ('o', 10, 'a');
            if (name.StartsWith("Zone_PillarBeam")) return (',', 10, 'a');
            if (name.StartsWith("Zone_RoofFall")) return ('v', 10, 'a');
            if (name.StartsWith("Rubble")) return ('r', 9, 'a');
            if (name == "Kneeler") return ('k', 8, 'p');
            if (name.StartsWith("Dais")) return ('=', 6, 'a');
            if (name == "Step") return ('^', 5, 'a');
            if (name == "HalfLanding" || name == "DoorTerrace" || name == "Pier" || name.StartsWith("Masonry")) return ('=', 4, 'a');
            if (name.StartsWith("Zone_SmokeBand")) return ('s', 2, 'a');
            if (name.StartsWith("Zone_DuelRing")) return (':', 1, 'e');
            if (name.Contains("Floor") || name == "Apron") return ('.', 0, 'a');
            return ('\0', 0, 'a');
        }

        private static float Sprint
        {
            get
            {
                var s = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/Kneel/Player/Settings/PlayerMovementSettings.asset");
                return s.sprintSpeed * s.sprintSpeedMultiplier;
            }
        }

        private static string PathLine(string label, Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            if (!NavMesh.SamplePosition(from, out var a, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 2f, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return label + ": NO PATH";
            }

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return label + ": " + length.ToString("0.0") + " m, " + (length / Sprint).ToString("0.0") + " s at a sprint";
        }

        private static float Gap(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static void CaptureFrom(Vector3 position, Quaternion rotation, float fov, string file, float orthoSize = 0f)
        {
            var go = new GameObject("CaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.farClipPlane = 600f;
            if (orthoSize > 0f)
            {
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
            }

            go.transform.SetPositionAndRotation(position, rotation);
            Capture(cam, file);
            Object.DestroyImmediate(go);
        }

        private static void Capture(Camera cam, string file)
        {
            var rt = new RenderTexture(1600, 900, 24);
            var previous = RenderTexture.active;
            bool wasEnabled = cam.enabled;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutFolder, file), tex.EncodeToPNG());
            cam.targetTexture = null;
            cam.enabled = wasEnabled;
            RenderTexture.active = previous;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // ---------------------------------------------------------------- Geometry helpers

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // An axis-aligned box from plan rect (x0..x1, y0..y1) and heights h0..h1. A null layer means no collider.
        private static GameObject Box(Transform parent, string name, float x0, float x1, float y0, float y1, float h0, float h1, string material, string layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (h0 + h1) * 0.5f, (y0 + y1) * 0.5f);
            go.transform.localScale = new Vector3(x1 - x0, h1 - h0, y1 - y0);
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            if (layer == null)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            else
            {
                go.layer = L3Build.Layer(layer);
            }

            return go;
        }

        // A flight of 0.25 m steps (under the player's 0.3 m step offset), each a solid block down to the floor below.
        private static void Steps(Transform parent, string name, float x0, float x1, float y0, float y1, float h0, float h1)
        {
            var g = Group(parent, name);
            int count = Mathf.RoundToInt((h1 - h0) / 0.25f);
            float tread = (y1 - y0) / count;
            for (int i = 0; i < count; i++)
            {
                Box(g, "Step", x0, x1, y0 + i * tread, y0 + (i + 1) * tread, 0f, h0 + (i + 1) * 0.25f, "Stone", "Ground");
            }
        }

        // A floor marker: a 2 cm opaque slab, no collider.
        private static void Zone(Transform parent, string name, float x0, float x1, float y0, float y1, string material, float floor = Hall)
        {
            Box(parent, name, x0, x1, y0, y1, floor + 0.01f, floor + 0.03f, material, null);
        }

        // A standing marker for a person or a pickup: a column of the given height.
        private static void Pin(Transform parent, string name, Vector3 at, string material, float height)
        {
            float half = height > 1.5f ? 0.45f : 0.3f;
            Box(parent, name, at.x - half, at.x + half, at.z - half, at.z + half, at.y, at.y + height, material, null);
        }

        private static Material Mat(string key)
        {
            if (Materials.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            string path = MaterialsPath + "/L5_Grey_" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            var colour = Colour(key);
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", 0.1f);
            bool glows = key == "Ember" || key == "ZoneFire" || key == "Shrine";
            if (glows)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", colour * 1.5f);
            }

            EditorUtility.SetDirty(mat);
            Materials[key] = mat;
            return mat;
        }

        private static Color Colour(string key)
        {
            switch (key)
            {
                case "Floor": return new Color(0.46f, 0.45f, 0.43f);
                case "Wall": return new Color(0.27f, 0.26f, 0.26f);
                case "Stone": return new Color(0.55f, 0.53f, 0.5f);
                case "Marble": return new Color(0.8f, 0.78f, 0.74f);
                case "Bone": return new Color(0.82f, 0.79f, 0.7f);
                case "Wood": return new Color(0.33f, 0.22f, 0.15f);
                case "Cloth": return new Color(0.32f, 0.1f, 0.12f);
                case "Iron": return new Color(0.16f, 0.16f, 0.17f);
                case "Rubble": return new Color(0.38f, 0.35f, 0.33f);
                case "Debris": return new Color(0.2f, 0.13f, 0.1f);
                case "Throne": return new Color(0.12f, 0.1f, 0.1f);
                case "PillarCracked": return new Color(0.5f, 0.42f, 0.38f);
                case "Ember": return new Color(1f, 0.42f, 0.12f);
                case "Shrine": return new Color(0.95f, 0.85f, 0.5f);
                case "Enemy": return new Color(0.5f, 0.12f, 0.12f);
                case "Boss": return new Color(0.08f, 0.08f, 0.09f);
                case "Lore": return new Color(0.3f, 0.6f, 0.9f);
                case "ZoneRing": return new Color(0.62f, 0.68f, 0.72f);
                case "ZoneSeal": return new Color(0.9f, 0.55f, 0.2f);
                case "ZoneFire": return new Color(1f, 0.35f, 0.05f);
                case "ZoneSpill": return new Color(0.95f, 0.6f, 0.15f);
                case "ZoneBeam": return new Color(0.75f, 0.1f, 0.1f);
                case "ZoneSmoke": return new Color(0.36f, 0.33f, 0.4f);
                default: return Color.magenta;
            }
        }
    }
}
