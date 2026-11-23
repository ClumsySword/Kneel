using System.Collections.Generic;
using System.IO;
using Kneel.Core;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools.Transitions
{
    // The four seams, authored into the five level scenes (Kneel > Transitions > Build <seam>). Each builder only
    // rebuilds its own groups under "_Transition"; the in-place edits it makes are listed in its comment and are
    // idempotent (a wall segment switched off, a door's single mesh hidden behind two leaves).
    public static class SeamBuilders
    {
        public const string L1 = "L1_BrokenLine_Playable_L";
        public const string L2 = "L2_HollowVillage";
        public const string L3 = "L3_FallowFields";
        public const string L4 = "L4_BurningGate";
        public const string L5 = "L5_ThroneRoom";

        public const string MeshFolder = "Assets/Kneel/Meshes/Transitions";

        // ---------------------------------------------------------------- L1 -> L2

        // The road between L1 and L2 (owner, 2026-11-18: "He runs off into buildings ... When he enters L2 he runs out
        // from a house and a tree"). One dirt road: in L1 it runs north from the gap at the battlefield's end, in L2 it
        // comes in from the west into the start square, whose west side the burning tall house used to close. The
        // seam's frame: L2's road point Seam1L2 (heading east) stands at L1's Seam1L1 (heading north), so the copy of
        // L2's village west end lies astride L1's road (SeamGlimpses.L1Glimpse).
        public static readonly Vector3 Seam1L1 = new Vector3(4.5f, -0.4f, 380f);
        public static readonly Vector3 Seam1L2 = new Vector3(-128f, 0f, -58f);
        public const float Seam1Road = -58f;

        // L3's lane floor runs on 8 m south of its mouth, falling 0.3 m to the field (SeamGlimpses.L3Answer).
        public const float MouthDrop = 0.3f;
        private static readonly Vector3 TreeNear = new Vector3(4.11f, 0.83f, 385.31f);

        // The roads, painted into each level's own ground layout in the colour of its own paths (SeamRoads.Paint): L1's
        // from the battlefield path through the gap; L2's from the west into the start square (the colour of the
        // square's trodden mud) and from the end of the village street up the rise to the crest (the street's).
        public static readonly SeamRoads.Road[] RoadsL1 =
        {
            new SeamRoads.Road { points = new[] { new Vector3(3.8f, 0f, 360f), new Vector3(4.3f, 0f, 376f), new Vector3(4.5f, 0f, 391f) }, width = 3.6f, colourFrom = new Vector3(3.5f, 0f, 368f) },
        };

        public static readonly SeamRoads.Road[] RoadsL2 =
        {
            new SeamRoads.Road { points = new[] { new Vector3(-142f, 0f, -58.9f), new Vector3(-126f, 0f, -58.4f), new Vector3(-104f, 0f, Seam1Road), new Vector3(-96f, 0f, -57.3f) }, width = 3.6f, colourFrom = new Vector3(-94f, 0f, -57f) },
            new SeamRoads.Road { points = new[] { new Vector3(7.6f, 0f, 136f), new Vector3(8.4f, 0f, 150f), new Vector3(8.7f, 0f, 168f), new Vector3(8.8f, 0f, 188f) }, width = 3.4f, colourFrom = new Vector3(6f, 0f, 138f) },
        };

        // L1's far ground (a plain plane past the level's end) takes the road on to the copied village: a ribbon of its
        // own material, in the path's colour (L1's layout path, #6B5D4F, over the plane's #4A4440 ground).
        public static readonly Color FarRoadL1 = new Color(0.42f, 0.365f, 0.31f, 1f);

        // L1's end: the battlefield path runs out at the broken village fence (L1ExitDressing). The game takes over at
        // the end of the path and walks Kael on north up the road, past the leaning road sign, toward the Hollow
        // Village's burning west end. In place: the road sign moves from 13 m back to the gap's east side; the
        // overturned cart and two bushes that closed the gap move to its verges; the two unseen boundary boxes across
        // it stay, and open only when the game takes over; the exit's red fire glow and haze (L1LightingPass.Exit) are
        // switched off (owner: "remove red dusk light"), and the one background village house that stood on the road
        // (SM_Bld_Village_05) is switched off, the copied village taking its place.
        [MenuItem("Kneel/Transitions/Build Seam 1 (L1 exit)")]
        public static void L1Exit()
        {
            SeamKit.Open(L1);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Exit", true);

            BoxCollider trigger = SeamKit.Box(g, "ExitTrigger", new Vector3(4f, 1.5f, 372.2f), new Vector3(9f, 3f, 2.2f), true);
            LevelExit exit = SeamKit.Exit(trigger.gameObject, L2);
            exit.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", SeamKit.Ground(new Vector3(4.3f, 0f, 376.5f))),
                SeamKit.Point(g, "Walk_1", SeamKit.Ground(new Vector3(4.5f, 0f, 382f))),
                SeamKit.Point(g, "Walk_2", SeamKit.Ground(new Vector3(4.5f, 0f, 389f))),
            };
            exit.outroTime = 3.4f;

            // The camera leaves the side-on gameplay frame (looking west) and swings round behind him to look up the
            // road, low, so the village and its fires rise into the frame either side of it.
            Vector3 end = new Vector3(4.2f, 0f, 374f);
            exit.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Orbit(g, "Cam_0", end + Vector3.up, 62f, 270f, 8.5f), 0f),
                SeamKit.Key(SeamKit.Look(g, "Cam_1", new Vector3(9.5f, 6.2f, 367.5f), new Vector3(4.2f, 1.0f, 381f)), 1.7f, 54f),
                SeamKit.Key(SeamKit.Look(g, "Cam_2", new Vector3(3.3f, 3.5f, 369.6f), new Vector3(4.6f, 1.9f, 400f)), 3.4f, 46f),
            };

            // The gap: the boundary boxes across it open at the threshold.
            var open = new List<Collider>();
            GameObject boundary = SeamKit.Find("L1_BrokenLine/Boundary/BoundaryColliders");
            if (boundary != null)
            {
                foreach (BoxCollider b in boundary.GetComponents<BoxCollider>())
                {
                    Vector3 c = boundary.transform.TransformPoint(b.center);
                    if (c.z > 378f && c.z < 382f && Mathf.Abs(c.x - 4f) < 2f)
                    {
                        open.Add(b);
                    }
                }
            }

            exit.openWhenTaken = open.ToArray();

            // In place: the cart and the bushes out of the gap, the sign to its side.
            Move("L1_BrokenLine/Boundary/Wreckage_Band/L1_OverturnedCart", new Vector3(3.67f, 0f, 380.64f), new Vector3(-1.4f, 0f, 382.6f), 28f);
            Move("L1_BrokenLine/Boundary/NaturalFringe/SM_Env_Bush_04", new Vector3(2.33f, -0.05f, 378.02f), new Vector3(1.1f, -0.05f, 379.6f), null);
            Move("L1_BrokenLine/Boundary/NaturalFringe/SM_Env_Bush_04", new Vector3(5.48f, -0.05f, 378.49f), new Vector3(8.2f, -0.05f, 379.2f), null);
            Move("L1_BrokenLine/Boundary/NaturalFringe/SM_Env_Bush_04", new Vector3(4.95f, 0.22f, 382.71f), new Vector3(8.6f, 0.1f, 383.4f), null);
            Move("L1_BrokenLine/SetDressing/ExitRoad/SM_Prop_RoadSign_01", new Vector3(8.4f, 0f, 359.5f), new Vector3(7.4f, 0f, 377.2f), 300f);

            // In place: the backdrop's two houses at the gap (the bell house and its neighbour) stood on the road's east
            // half and the twisted tree in its middle; they move out to its verges.
            Move("L1_BrokenLine/Background/HollowVillage/SM_Bld_Village_01", new Vector3(7.5f, 0f, 381.6f), new Vector3(10.6f, 0f, 382.6f), null);
            Move("L1_BrokenLine/Background/HollowVillage/SM_Bld_Village_07", new Vector3(7.1f, 0f, 385.3f), new Vector3(9.7f, -0.4f, 389.8f), null);
            Move("L1_BrokenLine/Boundary/NaturalFringe/SM_Env_Tree_Twisted_01", TreeNear, new Vector3(-0.9f, 0.6f, 386.4f), null);

            // In place: no red glow at the exit, and no background house standing on the road.
            int off = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                bool red = t.parent != null && t.parent.name == "ExitGlow" && (t.name.StartsWith("VillageFire_") || t.name.StartsWith("FireGlow_") || t.name.StartsWith("RedHaze_"));
                bool onRoad = t.name == "SM_Bld_Village_05" && t.parent != null && t.parent.name == "HollowVillage" && Mathf.Abs(t.position.x + 3f) < 1f;
                if ((red || onRoad) && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    EditorUtility.SetDirty(t.gameObject);
                    off++;
                }
            }

            // The road itself, from the battlefield's last stretch through the gap to where the copied village's road
            // takes over (the far plane, 0.4 m below the level's ground, past z 390).
            Transform road = SeamKit.Group(g, "Road", true);
            string painted = SeamRoads.Paint(RoadsL1, 11);
            GameObject farGround = SeamKit.Find("L1_BrokenLine/Terrain/L1_Ground_Far");
            if (farGround != null)
            {
                // The plane's detail tiles 300 x 400 over its 700 x 900 m.
                SeamRoads.Ribbon(road, "L1Far", new[] { new Vector3(4.5f, -0.33f, 384f), new Vector3(4.5f, -0.33f, 396f), new Vector3(4.4f, -0.33f, 407f) },
                    farGround.GetComponent<Renderer>().sharedMaterial, FarRoadL1, new Vector2(700f / 300f, 900f / 400f), 3.6f);
            }

            Debug.Log("Seam 1 (L1 road): " + painted);

            // Preload: on leaving the last fight (E5's arena, z 300), about 60 m and 35 s of walking before the exit.
            Transform pre = SeamKit.Group(root, "Preload", true);
            SeamKit.Box(pre, "PreloadTrigger", new Vector3(-4.5f, 1.5f, 312f), new Vector3(16f, 4f, 2f), true, -42f).gameObject.AddComponent<PreloadTrigger>();

            SeamKit.Spawn(root, new Vector3(0f, 0.05f, 0f), 0f);
            SeamKit.Save();
            Debug.Log($"Seam 1 (L1 exit): {off} red glow / road-blocking pieces switched off");
        }

        // ---------------------------------------------------------------- L2 arrival

        // L2's start square at the west end of Village 1 was closed on its west side by the tall burning house (Lot 57),
        // so he came in from inside it. In place: that house and its lot move 9 m north-west, to the north verge of the
        // road it now flanks, and the beams, pot and slumped skeleton that lay across the square's west edge move to
        // its sides. He walks in from the west along the road (SeamGlimpses.L2Answer lays it), the burning house on his
        // left; the camera follows him in from behind, looking east into the burning square, and settles into the
        // gameplay frame at the spawn.
        [MenuItem("Kneel/Transitions/Build Seam 1 (L2 arrival)")]
        public static void L2Arrival()
        {
            SeamKit.Open(L2);
            Transform root = SeamKit.Root();

            Move("L2_HollowVillage/SetDressing/Buildings/Lot_57_L2_BurningHouse_Tall", new Vector3(-104.6f, 0f, -57f), new Vector3(-110.5f, 0f, -50.2f), null);
            Move("L2_HollowVillage/SetDressing/Lots/Lot_57_Tall", new Vector3(-104.6f, 2f, -57f), new Vector3(-110.5f, 2f, -50.2f), null);
            Move("L2_HollowVillage/SetDressing/Skeletons/L2_Skeleton_Slumped", new Vector3(-101.2f, -0.02f, -57.61f), new Vector3(-102.9f, -0.02f, -55.3f), 120f);
            Move("L2_HollowVillage/SetDressing/LivedIn/SM_Prop_Beam_01", new Vector3(-100.5f, 0.1f, -57.47f), new Vector3(-103.6f, 0.1f, -61.0f), 75f);
            Move("L2_HollowVillage/SetDressing/Clutter/SM_Prop_Beam_01", new Vector3(-99.25f, 0.12f, -57.91f), new Vector3(-99.0f, 0.12f, -55.0f), 110f);
            Move("L2_HollowVillage/SetDressing/LivedIn/SM_Prop_Pot_03", new Vector3(-100.8f, 0.1f, -56.47f), new Vector3(-101.6f, 0.1f, -55.6f), null);

            Transform g = SeamKit.Group(root, "Arrival", true);
            var arrival = g.gameObject.AddComponent<LevelArrival>();
            arrival.fromScene = L1;
            Vector3 spawn = new Vector3(-96f, 0f, -57f);
            arrival.start = SeamKit.Point(g, "Start", SeamKit.Ground(new Vector3(-107.5f, 0f, Seam1Road)), 90f);
            arrival.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", SeamKit.Ground(new Vector3(-102.5f, 0f, Seam1Road))),
                SeamKit.Point(g, "Walk_1", SeamKit.Ground(new Vector3(-99f, 0f, -57.5f))),
                SeamKit.Point(g, "Walk_2", SeamKit.Ground(spawn)),
            };

            // Low behind him on the road, looking east down it into the burning square; up and round to his right as
            // he enters it; then the gameplay frame.
            arrival.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Look(g, "Cam_0", new Vector3(-113.2f, 1.9f, -59.4f), new Vector3(-99f, 1.6f, -57.2f)), 0f, 44f),
                SeamKit.Key(SeamKit.Orbit(g, "Cam_1", new Vector3(-100.5f, 1.1f, -57.6f), 30f, 72f, 8.5f), 1.9f, 50f),
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_2", spawn), 3.4f),
            };

            // The start ring's unseen west walls (Wall_646-650) stand across the road: they open for the walk in and
            // close behind him at the spawn, like a door with no leaves (the road back to L1 stays shut in play).
            var ring = new List<Collider>();
            for (int i = 646; i <= 650; i++)
            {
                GameObject w = SeamKit.Find("L2_HollowVillage/Boundary/BoundaryColliders/Wall_" + i);
                if (w != null && w.TryGetComponent(out Collider c))
                {
                    ring.Add(c);
                }
            }

            LevelDoor ringDoor = g.gameObject.AddComponent<LevelDoor>();
            ringDoor.blockers = ring.ToArray();
            ringDoor.closeTime = 0.1f;
            arrival.door = ringDoor;
            arrival.closeAt = 5.5f;

            SeamKit.Spawn(root, spawn, 90f);
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- L2 -> L3

        // L2's end: past E5 the street leaves the village for the first fields (L2Farmland), and the ground rises north
        // to a crest (y 6.6 at z 185). The game takes over at the exit and walks him on up a dirt road that climbs the
        // rise toward the crest, where L3's sunken lane begins and, far beyond, the stronghold stands on the horizon.
        // In place: the three unseen boundary boxes across the exit (Wall_791-793) open when the game takes over (they
        // stopped him dead before).
        [MenuItem("Kneel/Transitions/Build Seam 2 (L2 exit)")]
        public static void L2Exit()
        {
            SeamKit.Open(L2);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Exit", true);
            Vector3 end = SeamKit.Ground(new Vector3(8f, 2.5f, 143f));
            BoxCollider trigger = SeamKit.Box(g, "ExitTrigger", end + new Vector3(0f, 1.5f, 0f), new Vector3(9f, 3f, 2f), true);
            LevelExit exit = SeamKit.Exit(trigger.gameObject, L3);
            exit.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", SeamKit.Ground(new Vector3(8.4f, 2.5f, 149f))),
                SeamKit.Point(g, "Walk_1", SeamKit.Ground(new Vector3(8.6f, 2.5f, 156f))),
                SeamKit.Point(g, "Walk_2", SeamKit.Ground(new Vector3(8.8f, 2.5f, 165f))),
            };
            exit.outroTime = 3.4f;
            exit.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_0", end), 0f),
                SeamKit.Key(SeamKit.Look(g, "Cam_1", end + new Vector3(4.5f, 6.5f, -7f), end + new Vector3(0.5f, 0.6f, 10f)), 1.7f, 54f),
                SeamKit.Key(SeamKit.Look(g, "Cam_2", end + new Vector3(-0.6f, 2.6f, -2.5f), end + new Vector3(1.0f, 5.2f, 60f)), 3.4f, 46f),
            };

            var open = new List<Collider>();
            foreach (string wall in new[] { "Wall_791", "Wall_792", "Wall_793" })
            {
                GameObject w = SeamKit.Find("L2_HollowVillage/Boundary/BoundaryColliders/" + wall);
                if (w != null && w.TryGetComponent(out Collider c))
                {
                    open.Add(c);
                }
            }

            exit.openWhenTaken = open.ToArray();

            // The road: from the end of the village street up the rise to the crest, the field's furrows either side.
            SeamKit.Group(g, "Road", true);
            Debug.Log("Seam 2 (L2 roads): " + SeamRoads.Paint(RoadsL2, 23));

            // Preload: leaving E5 north (z 113), about 30 m before the exit.
            Transform pre = SeamKit.Group(root, "Preload", true);
            SeamKit.Box(pre, "PreloadTrigger", SeamKit.Ground(new Vector3(5f, 2.5f, 113f)) + Vector3.up * 1.5f, new Vector3(14f, 4f, 2f), true, -20f).gameObject.AddComponent<PreloadTrigger>();
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- L3 arrival

        // L3's sunken lane was closed at its south end by four low wall segments. In place: the middle two are switched
        // off, so the lane's mouth is open toward L2 (an unseen box keeps it shut in play), and he walks in through it.
        // The camera comes in behind him, low in the mouth, looking north up the lane into the dawn (L3 has no ground or
        // sky worth seeing to the south), and rises into the gameplay frame.
        [MenuItem("Kneel/Transitions/Build Seam 2 (L3 arrival)")]
        public static void L3Arrival()
        {
            SeamKit.Open(L3);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Arrival", true);
            foreach (float x in new[] { 63f, 65f })
            {
                GameObject wall = SeamKit.Nearest("L3_StoneWall_Low", new Vector3(x, 0f, -0.59f), 0.6f);
                if (wall != null)
                {
                    wall.SetActive(false);
                    EditorUtility.SetDirty(wall);
                }
            }

            BoxCollider mouthBlocker = SeamKit.Box(g, "MouthBlocker", new Vector3(64f, 1.5f, -1.2f), new Vector3(4.4f, 3f, 0.6f), false);

            // The lane's floor stops at the old wall line: an unseen floor under the mouth, where he starts, sloped like
            // the lane floor's own run on south (SeamGlimpses.L3Answer: from y -0.3 at z -8 up to the floor's edge, y 0).
            float pitch = Mathf.Atan2(MouthDrop, 8f) * Mathf.Rad2Deg;
            BoxCollider mouthFloor = SeamKit.Box(g, "MouthFloor", new Vector3(64f, -MouthDrop * 0.5f - 0.25f, -4f), new Vector3(4.4f, 0.5f, 8.2f), false);
            mouthFloor.transform.rotation = Quaternion.Euler(-pitch, 0f, 0f);
            mouthFloor.gameObject.layer = LayerMask.NameToLayer("Ground");

            var arrival = g.gameObject.AddComponent<LevelArrival>();
            arrival.fromScene = L2;
            arrival.start = SeamKit.Point(g, "Start", new Vector3(64f, -MouthDrop * 4.6f / 8f + 0.02f, -4.6f), 0f);
            arrival.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", SeamKit.Ground(new Vector3(64f, 0.2f, 0.4f))),
                SeamKit.Point(g, "Walk_1", SeamKit.Ground(new Vector3(64f, 0.2f, 3.6f))),
            };
            // The mouth's unseen box opens for the walk in and closes behind him (a door with no leaves).
            // With it go the lane's own containment caps over the two wall segments that were switched off.
            var mouthShut = new List<Collider> { mouthBlocker };
            GameObject containment = SeamKit.Find("L3_Root/Floors/S/_Containment");
            if (containment != null)
            {
                foreach (Collider c in containment.GetComponentsInChildren<Collider>(true))
                {
                    Vector3 at = c.bounds.center;
                    if (Mathf.Abs(at.z + 0.59f) < 0.6f && (Mathf.Abs(at.x - 63f) < 0.6f || Mathf.Abs(at.x - 65f) < 0.6f))
                    {
                        mouthShut.Add(c);
                    }
                }
            }

            // A field gate in the mouth (owner: "a fence gate that is shut behind the player"): two leaves of farm
            // fence on hinges at the walls' cut ends, in L3's dawn palette, open as he comes in, shut behind him.
            Transform gate = SeamKit.Group(root, "MouthGate", true);
            LevelDoor mouth = gate.gameObject.AddComponent<LevelDoor>();
            Material dawn = FindMaterial("L3_Adventure_Dawn");
            mouth.leaves = new[]
            {
                new LevelDoor.Leaf { hinge = Hinge(gate, "Hinge_W", new Vector3(62.05f, 0f, -0.59f), 1f, dawn), openAngle = -100f },
                new LevelDoor.Leaf { hinge = Hinge(gate, "Hinge_E", new Vector3(65.95f, 0f, -0.59f), -1f, dawn), openAngle = 100f },
            };
            mouth.blockers = mouthShut.ToArray();
            mouth.openTime = 1.6f;
            mouth.closeTime = 1.4f;
            arrival.door = mouth;
            arrival.closeAt = 3.6f;
            arrival.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Look(g, "Cam_0", new Vector3(64.6f, 1.7f, -8.6f), new Vector3(64f, 1.5f, 6f)), 0f, 46f),
                SeamKit.Key(SeamKit.Orbit(g, "Cam_1", new Vector3(64f, 1.1f, 2.4f), 34f, 12f, 8f), 1.7f, 50f),
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_2", new Vector3(64f, 0.2f, 3.6f)), 3.0f),
            };
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- L3 -> L4

        // L3's end: the causeway (F32) and its existing exit trigger. In place: the trigger gets LevelExit and a real
        // destination ("L4" was not a scene name); the causeway's two north wall segments are switched off and an
        // unseen floor stub and an unseen box that opens at the threshold go in their place, so he walks on north,
        // straight through the open field gate of L4's farm track (glimpse) and up it.
        [MenuItem("Kneel/Transitions/Build Seam 3 (L3 exit)")]
        public static void L3Exit()
        {
            SeamKit.Open(L3);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Exit", true);
            GameObject trigger = SeamKit.Find("L3_Root/Markers/CW/ExitTrigger");
            LevelExit exit = SeamKit.Exit(trigger, L4);
            foreach (float x in new[] { 185f, 187f })
            {
                GameObject wall = SeamKit.Nearest("L3_StoneWall_Low", new Vector3(x, 0f, 400.58f), 0.6f);
                if (wall != null)
                {
                    wall.SetActive(false);
                    EditorUtility.SetDirty(wall);
                }
            }

            BoxCollider floor = SeamKit.Box(g, "FloorStub", new Vector3(186f, -0.25f, 408f), new Vector3(6f, 0.5f, 16f), false);
            floor.gameObject.layer = LayerMask.NameToLayer("Ground");
            BoxCollider shut = SeamKit.Box(g, "EndBlocker", new Vector3(186f, 1.5f, 400.9f), new Vector3(4.4f, 3f, 0.6f), false);
            exit.openWhenTaken = new Collider[] { shut };
            exit.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", new Vector3(186f, 0f, 401.5f)),
                SeamKit.Point(g, "Walk_1", new Vector3(186f, 0f, 406f)),
                SeamKit.Point(g, "Walk_2", new Vector3(186f, 0f, 414f)),
            };
            exit.outroTime = 3.4f;
            Vector3 end = new Vector3(186f, 0f, 397f);
            exit.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_0", end), 0f),
                SeamKit.Key(SeamKit.Orbit(g, "Cam_1", end + new Vector3(0f, 1.4f, 4f), 36f, 0f, 10.5f), 1.6f, 52f),
                SeamKit.Key(SeamKit.Orbit(g, "Cam_2", end + new Vector3(0f, 2.2f, 9f), 13f, 0f, 12.5f), 3.4f, 44f),
            };

            // Preload: at the start of the exit lane (F31), after the last fight, about 55 m before the exit.
            Transform pre = SeamKit.Group(root, "Preload", true);
            SeamKit.Box(pre, "PreloadTrigger", new Vector3(186f, 1.5f, 343f), new Vector3(10f, 4f, 2f), true).gameObject.AddComponent<PreloadTrigger>();
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- L4 arrival

        // L4 starts on the walled farm track, which was closed at its south end by a low wall with the hay wagon across
        // it. In place: the wall's middle two segments make way for a field gate (two leaves of farm fence on hinges, in
        // L3's dawn palette like the track's other fences), and the hay wagon is drawn up along the track's west wall.
        // He comes off L3's causeway (glimpse) through the open gate, which swings shut behind him; an unseen box closes
        // the gateway with it. The camera follows him through from behind, looking north up the track to the stronghold.
        [MenuItem("Kneel/Transitions/Build Seam 3 (L4 arrival)")]
        public static void L4Arrival()
        {
            SeamKit.Open(L4);
            Transform root = SeamKit.Root();
            foreach (float x in new[] { 59f, 61f })
            {
                GameObject wall = SeamKit.Nearest("L3_StoneWall_Low", new Vector3(x, 0f, -120.5f), 0.6f);
                if (wall != null)
                {
                    wall.SetActive(false);
                    EditorUtility.SetDirty(wall);
                }
            }

            Move("L4_Root/Farmland/Bounds/L3_HayWagon", new Vector3(60f, 0f, -118.4f), new Vector3(57.35f, 0f, -115.6f), 176f);

            Transform g = SeamKit.Group(root, "Arrival", true);

            // The gate: two leaves of farm fence, each on a hinge at the wall's cut end, shut across the gateway.
            Transform gate = SeamKit.Group(root, "FieldGate", true);
            LevelDoor door = gate.gameObject.AddComponent<LevelDoor>();
            Material dawn = FindMaterial("L3_Adventure_Dawn");
            Transform hingeW = Hinge(gate, "Hinge_W", new Vector3(58.05f, 0f, -120.5f), 1f, dawn);
            Transform hingeE = Hinge(gate, "Hinge_E", new Vector3(61.95f, 0f, -120.5f), -1f, dawn);
            BoxCollider gateBlock = SeamKit.Box(gate, "Blocker", new Vector3(60f, 0.9f, -120.5f), new Vector3(4f, 1.8f, 0.5f), false);
            door.leaves = new[]
            {
                new LevelDoor.Leaf { hinge = hingeW, openAngle = -100f },
                new LevelDoor.Leaf { hinge = hingeE, openAngle = 100f },
            };
            door.blockers = new Collider[] { gateBlock };
            door.openTime = 1.6f;
            door.closeTime = 1.4f;

            // Outside the gate: the farm ground has no collider, so an unseen floor carries the walk in. It runs on under
            // the gateway to z -119.5, past the farm track's south edge (-120): the wall segments switched off above
            // took their colliders with them, and a floor stopping at the wall line (-120.5) left a 0.5 m strip with
            // nothing under it, wider than Kael's capsule, that he could drop through mid-walk.
            BoxCollider outside = SeamKit.Box(g, "OutsideFloor", new Vector3(60f, -0.33f, -124.5f), new Vector3(5f, 0.5f, 10f), false);
            outside.gameObject.layer = LayerMask.NameToLayer("Ground");

            var arrival = g.gameObject.AddComponent<LevelArrival>();
            arrival.fromScene = L3;
            Vector3 spawn = new Vector3(60f, 0f, -114f);
            arrival.start = SeamKit.Point(g, "Start", new Vector3(60f, -0.08f, -125.2f), 0f);
            arrival.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", new Vector3(60f, 0f, -120.5f)),
                SeamKit.Point(g, "Walk_1", new Vector3(60.1f, 0f, -117f)),
                SeamKit.Point(g, "Walk_2", SeamKit.Ground(spawn)),
            };
            arrival.door = door;
            arrival.closeAt = 3.3f;
            arrival.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Look(g, "Cam_0", new Vector3(60.7f, 1.8f, -130.5f), new Vector3(60f, 1.6f, -115f)), 0f, 46f),
                SeamKit.Key(SeamKit.Orbit(g, "Cam_1", new Vector3(60f, 1.0f, -117.2f), 34f, 14f, 8.5f), 1.8f, 50f),
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_2", spawn), 3.2f),
            };
            SeamKit.Save();
        }

        // A gate leaf: a hinge pivot at `at`, and a farm fence segment 1.9 m long reaching `side` (+1 east, -1 west).
        private static Transform Hinge(Transform parent, string name, Vector3 at, float side, Material material)
        {
            var hinge = new GameObject(name).transform;
            hinge.SetParent(parent, false);
            hinge.position = at;
            GameObject leaf = SeamKit.Place("Assets/SyntyStudios/PolygonAdventure/Prefabs/Buildings/SM_Bld_Fence_01.prefab", hinge, at, Vector3.zero);
            if (leaf == null)
            {
                return hinge;
            }

            // Lay the segment's long axis along x, scale it to the leaf's length, and set its near end on the hinge.
            Bounds b = TransitionShots.BoundsOf(leaf.transform);
            if (b.size.z > b.size.x)
            {
                leaf.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                b = TransitionShots.BoundsOf(leaf.transform);
            }

            Vector3 ls = leaf.transform.localScale;
            leaf.transform.localScale = new Vector3(ls.x * 1.9f / Mathf.Max(b.size.x, 0.01f), ls.y, ls.z);
            b = TransitionShots.BoundsOf(leaf.transform);
            leaf.transform.position += new Vector3(side > 0f ? at.x - b.min.x : at.x - b.max.x, at.y - b.min.y + 0.02f, at.z - b.center.z);
            SeamKit.SetMaterial(leaf, material);
            SeamKit.SetLayer(leaf, "Obstacles");
            return hinge;
        }

        internal static Material FindMaterial(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- L4 -> L5

        // L4's end: Shrine B in the Kneeling Court and the throne-hall doors. The door beat: he stops before the doors,
        // they swing in, and he walks through. The portal's Synty door is a frame with two leaf children
        // (SM_Bld_Castle_Door_L/R) pivoted on their hinges, so LevelDoor turns those. In place: the portal's bake
        // (L4Build) also held a copy of the door half a metre behind it, which would have stayed shut; it gets a copy
        // without it. The exit marker gets LevelExit and a real destination.
        [MenuItem("Kneel/Transitions/Build Seam 4 (L4 exit)")]
        public static void L4Exit()
        {
            SeamKit.Open(L4);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Exit", true);
            GameObject trigger = SeamKit.Find("L4_Root/L4L5_Threshold/Markers/B/Exit/L3_ExitTrigger");
            LevelExit exit = SeamKit.Exit(trigger, L5);

            GameObject single = SeamKit.Find("L4_Root/L4L5_Threshold/Structures/B/ThroneDoors/Portal/Doors/SM_Bld_Castle_Door_01");
            GameObject portalCollider = SeamKit.Find("L4_Root/L4L5_Threshold/Structures/B/ThroneDoors/Portal/Collider");
            Transform doorGroup = SeamKit.Group(root, "ThroneDoorLeaves", true);
            LevelDoor door = doorGroup.gameObject.AddComponent<LevelDoor>();
            Transform leafW = single != null ? single.transform.Find("SM_Bld_Castle_Door_R") : null;
            Transform leafE = single != null ? single.transform.Find("SM_Bld_Castle_Door_L") : null;
            door.leaves = new[]
            {
                new LevelDoor.Leaf { hinge = leafW, openAngle = -105f },
                new LevelDoor.Leaf { hinge = leafE, openAngle = 105f },
            };
            door.blockers = portalCollider != null ? new[] { portalCollider.GetComponent<Collider>() } : new Collider[0];
            door.openTime = 2.0f;
            LeavesMovable(leafW != null ? leafW.gameObject : null, leafE != null ? leafE.gameObject : null);
            if (single != null)
            {
                // (An earlier pass hid the frame; it stays.)
                single.GetComponent<MeshRenderer>().enabled = true;
                EditorUtility.SetDirty(single);
                GameObject baked = SeamKit.Find("L4_Root/L4L5_Threshold/Structures/B/ThroneDoors/Baked");
                if (baked != null)
                {
                    StripDoorFromBake(baked.GetComponent<MeshFilter>(), single, "L4_ThroneDoors_NoLeaves");
                }
            }

            // In place: the throne hall's shell (a 4x Synty church room) stands 5 m behind the doors with its own closed
            // door in its south wall. A copy of its mesh without that wall below 12 m (hidden from the court behind the
            // portal, which is 15 m tall) opens the way to the L5 nave glimpse inside it.
            GameObject church = SeamKit.Find("L4_Root/L4L5_Threshold/Citadel/ThroneHall/SM_Bld_Church_Room_01");
            if (church != null)
            {
                CutBox(church.GetComponent<MeshFilter>(), new[] { new Bounds(new Vector3(54f, 8f, 277.5f), new Vector3(17f, 7f, 8f)), new Bounds(new Vector3(54f, 5f, 292f), new Vector3(28f, 0.8f, 40f)) }, "L4_ThroneHallShell_Open",
                    "Assets/SyntyStudios/PolygonKnights/Models/SM_Bld_Church_Room_01.fbx");
            }

            exit.door = door;
            exit.doorStand = SeamKit.Point(g, "DoorStand", new Vector3(54f, 5f, 265.9f), 0f);

            // The court's floor ends at the doors: an unseen floor carries the walk through them.
            BoxCollider floor = SeamKit.Box(g, "FloorStub", new Vector3(54f, 4.75f, 274f), new Vector3(7f, 0.5f, 11f), false);
            floor.gameObject.layer = LayerMask.NameToLayer("Ground");
            exit.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", new Vector3(54f, 5f, 270.5f)),
                SeamKit.Point(g, "Walk_1", new Vector3(54f, 5f, 276f)),
            };
            exit.outroTime = 4.0f;
            Vector3 court = new Vector3(54f, 5f, 265.5f);
            exit.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_0", court), 0f),
                // Off the axis to the east, so Shrine B's statue doesn't stand between the camera and the doors.
                SeamKit.Key(SeamKit.Look(g, "Cam_1", new Vector3(58.6f, 10.2f, 257.8f), new Vector3(54f, 6.6f, 267.6f)), 1.8f, 50f),
                SeamKit.Key(SeamKit.Look(g, "Cam_2", new Vector3(55.5f, 10.8f, 261.6f), new Vector3(54f, 5.4f, 278f)), 4.0f, 46f),
            };

            // Preload: where the west lane opens on the cloister and vista V4 frames the doors (y 202), after which only
            // the E6 gauntlet stands between him and Shrine B. Measured against L5's load (see the report).
            Transform pre = SeamKit.Group(root, "Preload", true);
            SeamKit.Box(pre, "PreloadTrigger", new Vector3(54f, 1.5f, 236f), new Vector3(30f, 6f, 2f), true).gameObject.AddComponent<PreloadTrigger>();
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- L5 arrival

        // L5 continues L4's coordinates: Shrine B stands 4 m outside the outer doors. He arrives where the L4 doors let
        // him through, between Shrine B and the outer doors, and walks in up the nave to the level's start (y 279).
        // The doors stand open as he comes through and swing shut behind him (they are shut in play, as built).
        [MenuItem("Kneel/Transitions/Build Seam 4 (L5 arrival)")]
        public static void L5Arrival()
        {
            SeamKit.Open(L5);
            Transform root = SeamKit.Root();
            Transform g = SeamKit.Group(root, "Arrival", true);
            var arrival = g.gameObject.AddComponent<LevelArrival>();
            arrival.fromScene = L4;
            Vector3 spawn = new Vector3(54f, 0.05f, 279f);
            arrival.start = SeamKit.Point(g, "Start", new Vector3(54f, 0.02f, 267.2f), 0f);
            arrival.walkPath = new[]
            {
                SeamKit.Point(g, "Walk_0", new Vector3(54f, 0.02f, 270f)),
                SeamKit.Point(g, "Walk_1", new Vector3(54f, 0.02f, 272.6f)),
            };

            // The doors: the L5 gate's two leaves, hinged on their outer edges.
            GameObject leafW = SeamKit.Find("L5_Root/Art/OuterDoors/Gate/Leaf_W");
            GameObject leafE = SeamKit.Find("L5_Root/Art/OuterDoors/Gate/Leaf_E");
            Transform doorGroup = SeamKit.Group(root, "OuterDoor", true);
            LevelDoor door = doorGroup.gameObject.AddComponent<LevelDoor>();
            door.leaves = new[]
            {
                new LevelDoor.Leaf { hinge = leafW.transform, openAngle = -96f },
                new LevelDoor.Leaf { hinge = leafE.transform, openAngle = 96f },
            };
            // Blockers: every collider standing in the doorway at body height that isn't part of a leaf (the leaves'
            // own fade probes swing with them): the dressing collider that shuts it.
            var blockers = new List<Collider>();
            foreach (Collider c in Physics.OverlapCapsule(new Vector3(54f, 0.6f, 268.6f), new Vector3(54f, 1.8f, 268.6f), 0.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(leafW.transform) == false && c.transform.IsChildOf(leafE.transform) == false)
                {
                    blockers.Add(c);
                }
            }

            door.blockers = blockers.ToArray();
            LeavesMovable(leafW, leafE);
            door.closeTime = 2.2f;
            arrival.door = door;
            arrival.closeAt = 2.7f;

            // V1 (the L5 note's CM_V1 at the outer doors): low, just inside the open doors, down the whole nave; he steps
            // in through them beneath it, and the camera rises to the top of the doorway (inside: from outside, the
            // door's soot plumes black out the view) to look down on him crossing the threshold, then settles into the
            // gameplay frame. Shrine B stays behind him.
            arrival.cameraKeys = new[]
            {
                SeamKit.Key(SeamKit.Look(g, "Cam_0", new Vector3(54f, 3.4f, 270.8f), new Vector3(54f, 1.9f, 300f)), 0f, 45f),
                SeamKit.Key(SeamKit.Look(g, "Cam_1", new Vector3(54f, 9.6f, 269.3f), new Vector3(54f, 0.4f, 274.4f)), 1.6f, 50f),
                SeamKit.Key(SeamKit.Gameplay(g, "Cam_2", new Vector3(54f, 0.02f, 272.6f)), 2.7f),
            };
            SeamKit.Spawn(root, spawn, 0f);
            SeamKit.Save();
        }

        // ---------------------------------------------------------------- helpers

        private static void Move(string path, Vector3 near, Vector3 to, float? yaw)
        {
            GameObject parent = GameObject.Find(path.Substring(0, path.LastIndexOf('/')));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            GameObject go = parent != null ? SeamKit.Nearest(name, near, 0.5f, parent.transform) : null;
            if (go == null)
            {
                // Already moved by an earlier run?
                go = parent != null ? SeamKit.Nearest(name, to, 0.5f, parent.transform) : null;
                if (go == null)
                {
                    Debug.LogWarning($"SeamBuilders: {path} not found near {near}");
                    return;
                }
            }

            Undo.RecordObject(go.transform, "Move for seam");
            go.transform.position = to;
            if (yaw.HasValue)
            {
                Vector3 e = go.transform.eulerAngles;
                go.transform.eulerAngles = new Vector3(e.x, yaw.Value, e.z);
            }

            EditorUtility.SetDirty(go);
        }

        // In place: door leaves that were built static (batched, occluders) can't swing in play; they keep only their
        // occludee flag.
        private static void LeavesMovable(params GameObject[] leaves)
        {
            foreach (GameObject leaf in leaves)
            {
                if (leaf == null)
                {
                    continue;
                }

                foreach (Transform t in leaf.GetComponentsInChildren<Transform>(true))
                {
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags & ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic));
                    EditorUtility.SetDirty(t.gameObject);
                }
            }
        }

        // A copy of `mf`'s mesh without the triangles lying wholly inside `box` (world), assigned to it. Reruns start
        // from the original mesh in `sourceModel`.
        private static void CutBox(MeshFilter mf, Bounds[] boxes, string assetName, string sourceModel)
        {
            Mesh source = mf.sharedMesh;
            if (source != null && source.name == assetName)
            {
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(sourceModel))
                {
                    if (o is Mesh m && assetName.Length > 0 && mf.sharedMesh.name == assetName && m.name == Path.GetFileNameWithoutExtension(sourceModel))
                    {
                        source = m;
                    }
                }
            }

            if (source == null || source.name == assetName)
            {
                Debug.LogWarning("SeamBuilders: no source mesh for " + assetName);
                return;
            }

            // A triangle goes when its centre lies in the box and none of it rises above the box (the wall's big
            // triangles run wider than the doorway; anything above the box would show over the portal).
            Vector3[] v = source.vertices;
            var world = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                world[i] = mf.transform.TransformPoint(v[i]);
            }

            Mesh cut = new Mesh();
            var keeps = new int[source.subMeshCount][];
            cut.name = assetName;
            int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = world[tris[i]], b = world[tris[i + 1]], c = world[tris[i + 2]];
                    bool inside = false;
                    foreach (Bounds box in boxes)
                    {
                        inside |= box.Contains((a + b + c) / 3f) && Mathf.Max(a.y, Mathf.Max(b.y, c.y)) <= box.max.y + 0.01f;
                    }

                    if (inside)
                    {
                        removed++;
                        continue;
                    }

                    keep.Add(tris[i]);
                    keep.Add(tris[i + 1]);
                    keep.Add(tris[i + 2]);
                }

                keeps[s] = keep.ToArray();
            }

            SeamCopy.Compact(cut, false, source, keeps);
            Directory.CreateDirectory(MeshFolder);
            string path = $"{MeshFolder}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cut, path);
            Undo.RecordObject(mf, "Cut for seam");
            mf.sharedMesh = cut;
            EditorUtility.SetDirty(mf);
            Debug.Log($"SeamBuilders: cut {removed} triangles from {source.name} ({assetName})");
        }

        // Removes from a baked mesh every triangle lying wholly inside the door's slab (the door's copy in the bake),
        // saving the result as a new asset. Reruns start again from L4Build's bake.
        private static void StripDoorFromBake(MeshFilter bake, GameObject door, string assetName)
        {
            string path = $"{MeshFolder}/{assetName}.asset";
            // Always from the bake as L4Build made it (a rerun replaces an earlier copy).
            Mesh source = bake.sharedMesh != null && bake.sharedMesh.name == assetName ? AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Kneel/Meshes/L4/Kit/L4_ThroneDoors.asset") : bake.sharedMesh;
            if (source == null)
            {
                return;
            }

            // The door's slab: the door's box in x and y (a little inside its edges, so the jambs stay), and in depth where
            // the bake actually has it: z 268.8-269.7, about half a metre behind the door object as it stands now (it
            // was moved after the bake). The leaves lie wholly inside it; the portal's walls, jambs and arch soffit all
            // reach out of it, so they stay.
            Bounds door3 = door.GetComponent<MeshRenderer>().bounds;
            // A triangle goes when its centre lies in the doorway (the door's box, a little inside its sides, from
            // its sill to just over its head, z 268-270): the bake's door copy fills that, the jambs and soffit don't.
            var slab = new Bounds(new Vector3(door3.center.x, door3.center.y + 0.1f, 269f), new Vector3(door3.size.x - 0.4f, door3.size.y + 0.3f, 2f));
            Vector3[] bv = source.vertices;
            var world = new Vector3[bv.Length];
            for (int i = 0; i < bv.Length; i++)
            {
                world[i] = bake.transform.TransformPoint(bv[i]);
            }

            Mesh stripped = new Mesh();
            var keeps = new int[source.subMeshCount][];
            stripped.name = assetName;
            int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (slab.Contains((world[tris[i]] + world[tris[i + 1]] + world[tris[i + 2]]) / 3f))
                    {
                        removed++;
                        continue;
                    }

                    keep.Add(tris[i]);
                    keep.Add(tris[i + 1]);
                    keep.Add(tris[i + 2]);
                }

                keeps[s] = keep.ToArray();
            }

            SeamCopy.Compact(stripped, false, source, keeps);
            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(stripped, path);
            Undo.RecordObject(bake, "Bake without door");
            bake.sharedMesh = stripped;
            EditorUtility.SetDirty(bake);
            Debug.Log($"SeamBuilders: removed {removed} door triangles from {source.name}");
        }

        private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x * 50f), Mathf.FloorToInt(p.y * 50f), Mathf.FloorToInt(p.z * 50f));
    }
}
