using System.Collections.Generic;
using Kneel.Markers;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The farmer's house (S03): 11 x 12 m outside, walls 1 m thick, one 9 x 10 m room, a 4 m door gap in the
    // west wall. It is cut from the pack's own timber-framed houses, at their own scale:
    //   - each pack house is sliced at the eaves into walls and roof;
    //   - its four walls become panels, laid along our walls as an outer skin and an inner skin (so the room
    //     has real framed walls and windows on the inside too);
    //   - four of its roofs make a double-pile roof (two ridges running north-south), flattened to keep the
    //     ridge at 4 m.
    // The roof and the south (camera-side) wall are separate objects that fade when they hide the player.
    public static class L3Farmhouse
    {
        private const string MeshFolder = L3Build.MeshesPath + "/Farmhouse";
        private const string KnightsBuildings = "Assets/SyntyStudios/PolygonKnights/Prefabs/Buildings/";

        // Measured on the pack houses (SM_Bld_House_Room_xx): the roof's lowest edge, and how far the wall
        // (with its stone footing) reaches from the middle.
        private const float Eaves = 2.3f, HalfWidth = 1.96f, HalfLength = 2.94f, Ridge = 5.09f;
        private const float RoofTop = 4f;

        private const int Skin = 0, Wood = 1;

        private enum Side
        {
            East,
            West,
            North,
            South,
        }

        public static void Build(GameObject root)
        {
            L2Build.EnsureFolder(MeshFolder);
            Transform t = root.transform;
            var houses = new Dictionary<string, L3Mesh>();
            L3Mesh House(string name)
            {
                if (!houses.TryGetValue(name, out var data))
                {
                    var filter = AssetDatabase.LoadAssetAtPath<GameObject>(KnightsBuildings + name + ".prefab").GetComponentInChildren<MeshFilter>();
                    data = L3Mesh.From(filter.sharedMesh);
                    houses[name] = data;
                }

                return data;
            }

            // A wall panel in its own frame: x along the wall, y up, z outward (0 at the outermost face).
            L3Mesh Panel(string house, Side side)
            {
                L3Mesh lower = House(house).Clip(1, Eaves, true);
                var panel = new L3Mesh();
                switch (side)
                {
                    case Side.East:
                        panel.Append(lower.Clip(0, 1.2f, false), Rows(new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(1, 0, 0, -HalfWidth)));
                        break;
                    case Side.West:
                        panel.Append(lower.Clip(0, -1.2f, true), Rows(new Vector4(0, 0, 1, 0), new Vector4(0, 1, 0, 0), new Vector4(-1, 0, 0, -HalfWidth)));
                        break;
                    case Side.North:
                        panel.Append(lower.Clip(2, 2.2f, false), Rows(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, -HalfLength)));
                        break;
                    default:
                        panel.Append(lower.Clip(2, -2.2f, true), Rows(new Vector4(1, 0, 0, 0), new Vector4(0, 1, 0, 0), new Vector4(0, 0, -1, -HalfLength)));
                        break;
                }

                return panel;
            }

            // Lays a panel along a wall: centred on 'centre' (a point on the wall face at ground level), running
            // along 'along', its finished face toward 'outward', stretched to 'length'.
            void Lay(L3Mesh target, L3Mesh panel, Vector3 centre, Vector3 along, Vector3 outward, float length)
            {
                Bounds b = panel.Bounds();
                float scale = length / b.size.x;
                var m = new Matrix4x4();
                m.SetColumn(0, along * scale);
                m.SetColumn(1, Vector3.up);
                m.SetColumn(2, outward);
                Vector3 origin = centre - along * (b.center.x * scale);
                m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));
                target.Append(panel, m, Skin);
            }

            // Boards closing the top of a wall or the end of one (the walls are two skins with a gap between).
            void Board(L3Mesh target, Vector3 centre, Vector3 lengthAxis, Vector3 faceAxis, float length, float width, int seed)
            {
                var plank = L3Build.Kit(L2PolyKit.Plank(new Vector3(length, 0.1f, width), seed));
                var m = new Matrix4x4();
                m.SetColumn(0, lengthAxis);
                m.SetColumn(1, faceAxis);
                m.SetColumn(2, Vector3.Cross(lengthAxis, faceAxis));
                m.SetColumn(3, new Vector4(centre.x, centre.y, centre.z, 1f));
                target.Append(plank, m, Wood);
            }

            Vector3 east = Vector3.right, west = Vector3.left, north = Vector3.forward, south = Vector3.back;

            // ---- North, east and west walls: one mesh.
            var walls = new L3Mesh();
            walls.Tris.Add(new List<int>());
            // East: 12 m outside, 10 m inside.
            Lay(walls, Panel("SM_Bld_House_Room_01", Side.East), new Vector3(5.5f, 0f, -3f), north, east, 6f);
            Lay(walls, Panel("SM_Bld_House_Room_03", Side.East), new Vector3(5.5f, 0f, 3f), north, east, 6f);
            Lay(walls, Panel("SM_Bld_House_Room_06", Side.West), new Vector3(4.5f, 0f, -2.5f), north, west, 5f);
            Lay(walls, Panel("SM_Bld_House_Room_05", Side.East), new Vector3(4.5f, 0f, 2.5f), north, west, 5f);
            // North: 11 m outside, 9 m inside.
            Lay(walls, Panel("SM_Bld_House_Room_03", Side.West), new Vector3(-2.75f, 0f, 6f), east, north, 5.5f);
            Lay(walls, Panel("SM_Bld_House_Room_01", Side.West), new Vector3(2.75f, 0f, 6f), east, north, 5.5f);
            Lay(walls, Panel("SM_Bld_House_Room_06", Side.East), new Vector3(-2.25f, 0f, 5f), east, south, 4.5f);
            Lay(walls, Panel("SM_Bld_House_Room_05", Side.West), new Vector3(2.25f, 0f, 5f), east, south, 4.5f);
            // West: two 4 m lengths either side of the door gap (3 m inside).
            Lay(walls, Panel("SM_Bld_House_Room_06", Side.North), new Vector3(-5.5f, 0f, -4f), north, west, 4f);
            Lay(walls, Panel("SM_Bld_House_Room_03", Side.North), new Vector3(-5.5f, 0f, 4f), north, west, 4f);
            Lay(walls, Panel("SM_Bld_House_Room_06", Side.South), new Vector3(-4.5f, 0f, -3.5f), north, east, 3f);
            Lay(walls, Panel("SM_Bld_House_Room_05", Side.South), new Vector3(-4.5f, 0f, 3.5f), north, east, 3f);
            // Wall tops, and the two door jambs.
            Board(walls, new Vector3(0f, Eaves, 5.5f), east, Vector3.up, 11f, 1.04f, 1);
            Board(walls, new Vector3(5f, Eaves, 0f), north, Vector3.up, 10f, 1.04f, 2);
            Board(walls, new Vector3(-5f, Eaves, -3.5f), north, Vector3.up, 3f, 1.04f, 3);
            Board(walls, new Vector3(-5f, Eaves, 3.5f), north, Vector3.up, 3f, 1.04f, 4);
            Board(walls, new Vector3(-5f, Eaves * 0.5f, -2.02f), Vector3.up, north, Eaves, 1.04f, 5);
            Board(walls, new Vector3(-5f, Eaves * 0.5f, 2.02f), Vector3.up, south, Eaves, 1.04f, 6);

            // ---- The south wall, on its own so it can fade.
            var southWall = new L3Mesh();
            southWall.Tris.Add(new List<int>());
            Lay(southWall, Panel("SM_Bld_House_Room_01", Side.East), new Vector3(-2.75f, 0f, -6f), east, south, 5.5f);
            Lay(southWall, Panel("SM_Bld_House_Room_05", Side.West), new Vector3(2.75f, 0f, -6f), east, south, 5.5f);
            Lay(southWall, Panel("SM_Bld_House_Room_06", Side.West), new Vector3(-2.25f, 0f, -5f), east, north, 4.5f);
            Lay(southWall, Panel("SM_Bld_House_Room_06", Side.East), new Vector3(2.25f, 0f, -5f), east, north, 4.5f);
            Board(southWall, new Vector3(0f, Eaves, -5.5f), east, Vector3.up, 11f, 1.04f, 7);

            // ---- The roof: four pack roofs, two ridges, pressed down to a 4 m ridge.
            var roof = new L3Mesh();
            float squash = (RoofTop - Eaves) / (Ridge - Eaves);
            string[] roofHouses = { "SM_Bld_House_Room_01", "SM_Bld_House_Room_03", "SM_Bld_House_Room_05", "SM_Bld_House_Room_01" };
            int unit = 0;
            foreach (float x in new[] { -2.9f, 2.9f })
            {
                foreach (float z in new[] { -3.15f, 3.15f })
                {
                    L3Mesh upper = House(roofHouses[unit++]).Clip(1, Eaves, false);
                    Bounds b = upper.Bounds();
                    // Keep the roof itself and the gable that shows at the outer end. The pack house's own wall
                    // tops, and the gable at the end that butts against the next roof, would hang inside the room
                    // (and show as ghosts when the roof fades).
                    float inner = z < 0f ? 1f : -1f;
                    upper = upper.Where((centre, normal) =>
                    {
                        bool upright = Mathf.Abs(normal.y) < 0.25f;
                        bool innerGable = upright && Mathf.Abs(normal.z) > 0.7f && centre.z * inner > 1.9f;
                        bool wallTop = upright && Mathf.Abs(normal.x) > 0.7f && Mathf.Abs(centre.x) < HalfWidth + 0.05f && centre.y < Eaves + 0.5f;
                        return !innerGable && !wallTop;
                    });
                    var scale = new Vector3(5.8f / b.size.x, squash, 6.3f / b.size.z);
                    roof.Append(upper, Matrix4x4.TRS(new Vector3(x, Eaves * (1f - squash), z), Quaternion.identity, scale), Skin);
                }
            }

            // The pack roof is loose rows of tiles with gaps between them. A dark boarding under each slope
            // closes the gaps, so the lit room never shows through the roof.
            Vector2 dark = new Vector2((L2PolyKit.Side + 0.5f) / 8f, 0.5f);
            foreach (float ridgeX in new[] { -2.9f, 2.9f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 ridgeSouth = new Vector3(ridgeX, RoofTop - 0.3f, -6.2f), ridgeNorth = new Vector3(ridgeX, RoofTop - 0.3f, 6.2f);
                    Vector3 eavesSouth = new Vector3(ridgeX + side * 2.8f, Eaves - 0.04f, -6.2f), eavesNorth = new Vector3(ridgeX + side * 2.8f, Eaves - 0.04f, 6.2f);
                    if (side > 0f)
                    {
                        roof.Tri(Wood, ridgeSouth, ridgeNorth, eavesNorth, dark);
                        roof.Tri(Wood, ridgeSouth, eavesNorth, eavesSouth, dark);
                    }
                    else
                    {
                        roof.Tri(Wood, ridgeSouth, eavesNorth, ridgeNorth, dark);
                        roof.Tri(Wood, ridgeSouth, eavesSouth, eavesNorth, dark);
                    }
                }
            }

            // ---- The floor: boards running east-west over the 9 x 10 room and the 1 m threshold.
            var floor = new L3Mesh();
            const int rows = 24;
            float pitch = 10f / rows;
            for (int r = 0; r < rows; r++)
            {
                float z = -5f + pitch * (r + 0.5f);
                // Staggered joints.
                float joint = r % 2 == 0 ? -0.5f : 0.7f;
                float westEnd = -5.5f, eastEnd = 4.5f;
                AppendBoard(floor, westEnd, joint, z, pitch, r * 2);
                AppendBoard(floor, joint, eastEnd, z, pitch, r * 2 + 1);
            }

            // ---- Assemble.
            Material skin = L3Build.Mat("L3_Knights_Dawn"), wood = L2PolyKit.Wood;
            const float colliderHeight = 3.2f;

            GameObject Solid(string name, Vector3 size, Vector3 baseCentre, Transform parent)
            {
                var wall = L3Build.Child(parent, name, baseCentre);
                wall.layer = L3Build.Layer(L3Build.Boundary);
                L3Build.StandingBox(wall, size);
                return wall;
            }

            var floorObject = L3Build.Child(t, "Floor", new Vector3(-0.5f, 0f, 0f));
            floorObject.layer = L3Build.Layer(L3Build.Walkable);
            var floorBox = floorObject.AddComponent<BoxCollider>();
            floorBox.size = new Vector3(10f, 0.5f, 10f);
            floorBox.center = new Vector3(0f, -0.25f, 0f);
            var boards = MeshObject(t, "Boards", Save(floor, "L3_Farmhouse_Floor"), wood);
            boards.transform.SetParent(floorObject.transform, true);
            boards.layer = floorObject.layer;

            var wallsObject = L3Build.Child(t, "Walls");
            Solid("Wall_North", new Vector3(11f, colliderHeight, 1f), new Vector3(0f, 0f, 5.5f), wallsObject.transform);
            Solid("Wall_East", new Vector3(1f, colliderHeight, 10f), new Vector3(5f, 0f, 0f), wallsObject.transform);
            Solid("Wall_West_South", new Vector3(1f, colliderHeight, 3f), new Vector3(-5f, 0f, -3.5f), wallsObject.transform);
            Solid("Wall_West_North", new Vector3(1f, colliderHeight, 3f), new Vector3(-5f, 0f, 3.5f), wallsObject.transform);
            MeshObject(wallsObject.transform, "WallsMesh", Save(walls, "L3_Farmhouse_Walls"), skin, wood);

            var south_ = Solid("SouthWall", new Vector3(11f, colliderHeight, 1f), new Vector3(0f, 0f, -5.5f), t);
            var southMesh = MeshObject(t, "SouthWallMesh", Save(southWall, "L3_Farmhouse_SouthWall"), skin, wood);
            southMesh.transform.SetParent(south_.transform, true);
            L3Build.Set(south_.AddComponent<FadeOnEnterMarker>(), "buildingId", "D2");
            L3Build.AddFade(south_);

            var roofObject = L3Build.Child(t, "Roof");
            MeshObject(roofObject.transform, "RoofMesh", Save(roof, "L3_Farmhouse_Roof"), skin, L2PolyKit.CharredWood);
            L3Build.Set(roofObject.AddComponent<FadeOnEnterMarker>(), "buildingId", "D2");
            L3Build.AddFade(roofObject);
            // Four slopes overlap from above: the roof has to fade further than a wall to read as gone.
            L3Build.Set(roofObject.GetComponent<Kneel.OcclusionFade>(), "fadedAlpha", 0.12f);

            Dress(t);
        }

        private static void AppendBoard(L3Mesh floor, float from, float to, float z, float width, int seed)
        {
            var plank = L3Build.Kit(L2PolyKit.Plank(new Vector3(to - from - 0.015f, 0.08f, width - 0.015f), 900 + seed));
            floor.Append(plank, Matrix4x4.Translate(new Vector3((from + to) * 0.5f, -0.03f, z)), 0);
        }

        // The chimney and its smoke, the banked fire, and the room: a table laid for five, the farmer in his
        // chair facing the door, stores along the walls.
        private static void Dress(Transform t)
        {
            // The chimney stands in the north wall near the east corner, its hearth opening into the room.
            const float hearthX = 3.2f;
            var chimney = L3Build.Seat(t, "K:SM_Bld_House_Chimney_02", Vector3.zero, new Vector3(1.25f, 1.02f, 1.25f), new Vector2(hearthX, 5.4f));
            chimney.name = "Chimney";
            var smoke = L1Wayfinding.Particles("ChimneySmoke", t, L1Build.FxMat("L1_FX_Smoke"), 90, 5f, new Vector2(12f, 16f), new Vector2(1.4f, 2f),
                new Vector2(0.9f, 1.5f), new Color(0.2f, 0.19f, 0.18f, 0.55f), new Color(0.3f, 0.29f, 0.28f, 0f));
            smoke.transform.localPosition = new Vector3(hearthX, 4.7f, 5.4f);
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var smokeSize = smoke.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 3.2f));
            // The wind blows up-screen.
            var drift = smoke.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            drift.z = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            smoke.GetComponent<ParticleSystemRenderer>().maxParticleSize = 1.2f;

            var room = L3Build.Child(t, "Room").transform;

            // The hearth: a stone kerb against the north wall, the fire banked low.
            var hearth = L3Build.Child(room, "Hearth", new Vector3(hearthX, 0f, 4.45f));
            hearth.layer = L3Build.Layer(L3Build.Boundary);
            L3Build.StandingBox(hearth, new Vector3(2f, 0.5f, 1.1f));
            L3Build.Fit(hearth.transform, "K:SM_Bld_Rockwall_Straight_01", 90, new Vector3(2f, 0.5f, 0.35f), new Vector2(0f, -0.38f));
            L3Build.Fit(hearth.transform, "K:SM_Bld_Rockwall_Straight_01", 0, new Vector3(0.35f, 0.5f, 0.75f), new Vector2(-0.82f, 0.18f));
            L3Build.Fit(hearth.transform, "K:SM_Bld_Rockwall_Straight_01", 0, new Vector3(0.35f, 0.5f, 0.75f), new Vector2(0.82f, 0.18f));
            L3Build.Seat(hearth.transform, "A:SM_Prop_Loghalf_01", new Vector3(0f, 12f, 0f), 0.5f, new Vector2(0.05f, 0.15f), 0.05f, L3Build.Mat("L3_Knights_Charred"));
            L2Fire.Smoulder(hearth.transform, "BankedFire", new Vector3(0f, 0.12f, 0.15f), new Vector2(1.1f, 0.6f), 0f, false);
            var glow = L3Build.Child(hearth.transform, "HearthLight", new Vector3(0f, 0.7f, -0.2f)).AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = L1Build.Hex("#FF9A4A");
            glow.intensity = 3.2f;
            glow.range = 7f;
            glow.shadows = LightShadows.None;
            L3Build.Seat(room, "A:SM_Prop_Logpile_01", new Vector3(0f, 90f, 0f), 0.6f, new Vector2(1.55f, 4.3f));

            // The table, laid for five, along the north side of the room.
            const float tableX = -1.5f, tableZ = 3f, top = 1.09f;
            var table = L3Build.Child(room, "Table", new Vector3(tableX, 0f, tableZ));
            table.layer = L3Build.Layer(L3Build.Boundary);
            L3Build.StandingBox(table, new Vector3(2.5f, 1.05f, 1.05f));
            L3Build.Seat(table.transform, "A:SM_Prop_Stall_Table_01", Vector3.zero, 1f, Vector2.zero);
            float[] placeX = { -0.8f, 0f, 0.8f, -0.8f, 0.9f };
            float[] placeZ = { 0.3f, 0.3f, 0.3f, -0.3f, -0.05f };
            for (int i = 0; i < 5; i++)
            {
                L3Build.Seat(table.transform, "A:SM_Prop_Pot_02", new Vector3(0f, i * 70f, 0f), 0.6f, new Vector2(placeX[i], placeZ[i]), top);
            }

            L3Build.Seat(table.transform, "A:SM_Prop_Cheese_01", new Vector3(0f, 30f, 0f), 0.7f, new Vector2(0.1f, -0.3f), top);
            L3Build.Seat(table.transform, "A:SM_Prop_Pot_03", new Vector3(0f, 110f, 0f), 0.7f, new Vector2(-0.35f, -0.05f), top);

            // Four stools pushed in; nobody sat down.
            foreach (var (x, z) in new[] { (-0.8f, 0.95f), (0f, 0.95f), (0.8f, 0.95f), (-0.8f, -0.95f) })
            {
                L3Build.Seat(room, "A:SM_Prop_Crate_02", new Vector3(0f, x * 30f, 0f), new Vector3(0.6f, 0.62f, 0.6f), new Vector2(tableX + x, tableZ + z));
            }

            // The farmer, in his chair at the east end, facing the door.
            L3Build.Seat(room, "A:SM_Prop_Chest_01", new Vector3(0f, 90f, 0f), new Vector3(0.8f, 0.7f, 0.8f), new Vector2(tableX + 2.05f, tableZ));
            var farmer = AssetDatabase.LoadAssetAtPath<GameObject>(L2Build.PrefabsPath + "/Corpses/L2_Corpse_Peasant_Slumped.prefab");
            if (farmer != null)
            {
                var body = L3Build.Instance(farmer, room, new Vector3(tableX + 1.85f, 0.42f, tableZ), 270f);
                body.name = "Farmer";
            }

            // Stores he would not burn.
            L3Build.Seat(room, "A:SM_Prop_Barrel_01", Vector3.zero, 1f, new Vector2(3.9f, -4.3f));
            L3Build.Seat(room, "A:SM_Prop_Barrel_01", new Vector3(0f, 40f, 0f), 0.92f, new Vector2(3.0f, -4.4f));
            L3Build.Seat(room, "A:SM_Prop_Sack_04", new Vector3(0f, 8f, 0f), 1f, new Vector2(1.4f, -4.45f));
            L3Build.Seat(room, "A:SM_Prop_Sack_01", new Vector3(0f, 60f, 0f), 1.2f, new Vector2(2.2f, -3.9f));
            L3Build.Seat(room, "A:SM_Prop_Crate_01", new Vector3(0f, 12f, 0f), 1f, new Vector2(3.95f, -3.3f));
            L3Build.Seat(room, "A:SM_Prop_Basket_02", new Vector3(0f, 25f, 0f), 1f, new Vector2(3.95f, -3.3f), 0.77f);
            L3Build.Seat(room, "A:SM_Prop_Chest_01", new Vector3(0f, 180f, 0f), 1f, new Vector2(-2.6f, -4.45f));
            L3Build.Seat(room, "A:SM_Prop_Pumpkin_01", new Vector3(0f, 50f, 0f), 1f, new Vector2(-3.7f, -4.3f));
            L3Build.Seat(room, "A:SM_Prop_Pumpkin_02", new Vector3(0f, 10f, 0f), 1f, new Vector2(-3.3f, -3.9f));
        }

        private static Matrix4x4 Rows(Vector4 r0, Vector4 r1, Vector4 r2)
        {
            var m = new Matrix4x4();
            m.SetRow(0, r0);
            m.SetRow(1, r1);
            m.SetRow(2, r2);
            m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
            return m;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = L3Build.Child(parent, name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static Mesh Save(L3Mesh data, string name)
        {
            string path = MeshFolder + "/" + name + ".asset";
            var mesh = data.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
