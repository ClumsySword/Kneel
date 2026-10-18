using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Section 15 of the L3 note as data: floors, openings, solid blocks and everything that is placed. Plan
    // (x, y) is Unity (X, Z). Nothing here builds anything; L3Level reads it.
    public static class L3Plan
    {
        // The painted ground and the backdrop cover this rectangle (a power-of-two aspect for the layout map).
        public const float GroundMinX = 16f, GroundMinZ = -8f, GroundWidth = 208f, GroundLength = 416f;

        public const float Tier = 3f;

        public class Opening
        {
            public char Side;
            public float A, B;
        }

        public class Floor
        {
            public string Id, Area, Label;
            public char Stage;
            public bool Lane;
            public float X0, X1, Z0, Z1, HSouth, HNorth;

            // Kit piece on each edge (null for none).
            public string South, West, East, North;

            // Listed openings: when set, the edge is closed everywhere else, even where another floor touches.
            // When null, the edge is open wherever another floor touches it.
            public List<Opening> Openings;

            // Stretches of edge that get no kit piece on top (the bank below a raised edge stays).
            public List<Opening> NoKit = new List<Opening>();

            // No geometry: only shapes the backdrop and the ground paint.
            public bool Virtual;

            public bool Diagonal;
            public Vector2 Centre;
            public float Length, Width, Yaw;
            public bool ClipX;
            public float ClipA, ClipB;

            public Vector2[] Poly;

            public bool Ramp => Mathf.Abs(HSouth - HNorth) > 0.01f;

            public bool Raised => HSouth > 0.01f || HNorth > 0.01f;

            public float HeightAt(float z)
            {
                return Mathf.Lerp(HSouth, HNorth, Mathf.InverseLerp(Z0, Z1, z));
            }

            public string Kit(char side)
            {
                return side == 'S' ? South : side == 'W' ? West : side == 'E' ? East : North;
            }

            public Vector2 Dir => new Vector2(Mathf.Sin(Yaw * Mathf.Deg2Rad), Mathf.Cos(Yaw * Mathf.Deg2Rad));

            public bool Contains(Vector2 p, float grow = 0f)
            {
                return SignedDistance(p) <= grow;
            }

            // Negative inside.
            public float SignedDistance(Vector2 p)
            {
                if (!Diagonal)
                {
                    float dx = Mathf.Max(X0 - p.x, p.x - X1), dz = Mathf.Max(Z0 - p.y, p.y - Z1);
                    if (dx <= 0f && dz <= 0f)
                    {
                        return Mathf.Max(dx, dz);
                    }

                    return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
                }

                bool inside = true;
                float best = float.MaxValue;
                for (int i = 0; i < Poly.Length; i++)
                {
                    Vector2 a = Poly[i], b = Poly[(i + 1) % Poly.Length];
                    Vector2 ab = b - a, ap = p - a;
                    // Counter-clockwise polygon: inside is to the left of every edge.
                    if (ab.x * ap.y - ab.y * ap.x < 0f)
                    {
                        inside = false;
                    }

                    float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, (a + ab * t - p).magnitude);
                }

                return inside ? -best : best;
            }

            // The nearest point of the footprint to p (p itself when inside).
            public Vector2 Nearest(Vector2 p)
            {
                if (!Diagonal)
                {
                    return new Vector2(Mathf.Clamp(p.x, X0, X1), Mathf.Clamp(p.y, Z0, Z1));
                }

                if (SignedDistance(p) <= 0f)
                {
                    return p;
                }

                Vector2 nearest = Poly[0];
                float best = float.MaxValue;
                for (int i = 0; i < Poly.Length; i++)
                {
                    Vector2 a = Poly[i], b = Poly[(i + 1) % Poly.Length];
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    Vector2 q = a + ab * t;
                    if ((q - p).sqrMagnitude < best)
                    {
                        best = (q - p).sqrMagnitude;
                        nearest = q;
                    }
                }

                return nearest;
            }
        }

        // A solid mass of tier +1 ground that nothing walks on.
        public class Block
        {
            public string Id, Area;
            public char Stage;
            public float X0, X1, Z0, Z1, Top;

            public bool Contains(Vector2 p, float grow = 0f)
            {
                return p.x >= X0 - grow && p.x <= X1 + grow && p.y >= Z0 - grow && p.y <= Z1 + grow;
            }
        }

        public class Rect
        {
            public string Name;
            public char Stage;
            public float X0, X1, Z0, Z1;

            public bool Contains(Vector2 p, float grow = 0f)
            {
                return p.x >= X0 - grow && p.x <= X1 + grow && p.y >= Z0 - grow && p.y <= Z1 + grow;
            }
        }

        private static List<Floor> floors;

        public static List<Floor> Floors => floors ?? (floors = BuildFloors());

        public static Floor Find(string id)
        {
            return Floors.Find(f => f.Id == id);
        }

        public static readonly List<Block> Blocks = new List<Block>
        {
            new Block { Id = "KnollBank", Area = "A", Stage = 'A', X0 = 132f, X1 = 136f, Z0 = 190f, Z1 = 204f, Top = Tier },
            new Block { Id = "MillMound", Area = "E5", Stage = 'D', X0 = 160f, X1 = 182f, Z0 = 340f, Z1 = 356f, Top = Tier },
        };

        // The trench (15.2). Bottom at Y = -3.
        public static readonly Rect Ditch = new Rect { Name = "Ditch", Stage = 'B', X0 = 88f, X1 = 182f, Z0 = 232f, Z1 = 238f };

        // Footprints that kit pieces must stay out of: structures that are their own boundary, and crop rows.
        public static readonly List<Rect> KeepClear = new List<Rect>
        {
            new Rect { Name = "DescentRow", Stage = 'A', X0 = 71f, X1 = 73f, Z0 = 46f, Z1 = 70f },
            new Rect { Name = "AshRow", Stage = 'B', X0 = 88f, X1 = 90f, Z0 = 201f, Z1 = 226f },
            new Rect { Name = "StoneBridge", Stage = 'B', X0 = 103f, X1 = 109f, Z0 = 230f, Z1 = 240f },
            new Rect { Name = "SluiceFootway", Stage = 'B', X0 = 138f, X1 = 142f, Z0 = 230f, Z1 = 240f },
            new Rect { Name = "Farmhouse", Stage = 'C', X0 = 190f, X1 = 201f, Z0 = 266f, Z1 = 278f },
            new Rect { Name = "CellarFront", Stage = 'D', X0 = 180f, X1 = 181f, Z0 = 296f, Z1 = 300f },
        };

        // ---------------------------------------------------------------- Floors (15.2) and their edges (15.3)

        private static List<Floor> BuildFloors()
        {
            var list = new List<Floor>();
            // Owner's review of the blockout: this is farmland, so a lane on the level runs between field
            // fences (K06 and its weathered variants), not walls. Drystone stays where it holds something up:
            // on the ramps, the crest and the knoll, round the two yards, and along the causeway.
            const string fence = "K06", stone = "K03";

            Floor Add(string id, char stage, string area, string label, float x0, float x1, float z0, float z1, float hSouth, float hNorth, bool isLane,
                string south, string west, string east, string north)
            {
                var f = new Floor
                {
                    Id = id, Stage = stage, Area = area, Label = label, X0 = x0, X1 = x1, Z0 = z0, Z1 = z1, HSouth = hSouth, HNorth = hNorth, Lane = isLane,
                    South = south, West = west, East = east, North = north,
                };
                f.Poly = new[] { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) };
                list.Add(f);
                return f;
            }

            Floor Lane(string id, char stage, string area, string label, float x0, float x1, float z0, float z1, float hSouth = 0f, float hNorth = 0f)
            {
                string kit = hSouth > 0.01f || hNorth > 0.01f ? stone : fence;
                return Add(id, stage, area, label, x0, x1, z0, z1, hSouth, hNorth, true, kit, kit, kit, kit);
            }

            Floor Diagonal(string id, char stage, string area, string label, Vector2 centre, float length, float width, float yaw, bool clipX, float a, float b)
            {
                var f = new Floor
                {
                    Id = id, Stage = stage, Area = area, Label = label, Lane = true, Diagonal = true, Centre = centre, Length = length, Width = width, Yaw = yaw,
                    ClipX = clipX, ClipA = a, ClipB = b, West = fence, East = fence,
                };
                // The lane's two long sides, cut where they meet the floors at each end, so there is neither an
                // overlap nor a wedge of missing floor at the joint.
                Vector2 d = f.Dir, n = new Vector2(d.y, -d.x);
                Vector2 Cut(float side, float at)
                {
                    Vector2 origin = centre + n * (side * width * 0.5f);
                    float t = clipX ? (at - origin.x) / d.x : (at - origin.y) / d.y;
                    return origin + d * t;
                }

                // n points to the right of travel; counter-clockwise order.
                f.Poly = new[] { Cut(1f, a), Cut(1f, b), Cut(-1f, b), Cut(-1f, a) };
                f.X0 = Mathf.Min(f.Poly[0].x, f.Poly[3].x);
                f.X1 = Mathf.Max(f.Poly[1].x, f.Poly[2].x);
                f.Z0 = Mathf.Min(f.Poly[0].y, f.Poly[1].y);
                f.Z1 = Mathf.Max(f.Poly[2].y, f.Poly[3].y);
                list.Add(f);
                return f;
            }

            List<Opening> Open(params (char side, float a, float b)[] gaps)
            {
                var result = new List<Opening>();
                foreach (var g in gaps)
                {
                    result.Add(new Opening { Side = g.side, A = g.a, B = g.b });
                }

                return result;
            }

            // ---- Stage A
            Lane("F01", 'A', "S", "Sunken lane (ramp)", 60f, 68f, 0f, 34f, 0f, Tier);
            Lane("F02", 'A', "A0", "Crest", 58f, 70f, 34f, 46f, Tier, Tier);
            Lane("F03", 'A', "E1a", "Descent (ramp)", 60f, 68f, 46f, 58f, Tier, 0f);
            Lane("F04", 'A', "E1a", "Lane", 60f, 68f, 58f, 71f);
            Add("F05", 'A', "E1a", "E1a Kennel Wagon", 56f, 72f, 71f, 85f, 0f, 0f, false, fence, fence, fence, fence);
            Lane("F06", 'A', "E1", "Lane", 60f, 68f, 85f, 96f);
            Add("F07", 'A', "E1", "E1 Stubble Field", 48f, 76f, 96f, 120f, 0f, 0f, false, "K02", "K01", "K01", "K01").Openings =
                Open(('S', 60f, 68f), ('E', 108f, 116f), ('N', 51f, 53f));
            Add("F08", 'A', "D1", "D1 path", 50f, 54f, 120f, 126f, 0f, 0f, false, "K02", "K01", "K01", "K01");
            Add("F09", 'A', "D1", "D1 orchard", 44f, 60f, 126f, 142f, 0f, 0f, false, "K02", "K01", "K01", "K01");
            Diagonal("F10", 'A', "E2", "Lane, diagonal", new Vector2(86f, 118f), 24f, 8f, 59f, true, 76f, 96f);
            Add("F11", 'A', "E2", "E2 The Fallow", 96f, 128f, 110f, 142f, 0f, 0f, false, fence, fence, fence, "K01").Openings =
                Open(('W', 120f, 128f), ('N', 112f, 120f));
            Diagonal("F12", 'A', "A", "Lane, diagonal", new Vector2(128f, 163f), 49f, 8f, 30f, false, 142f, 184f);
            Lane("F13", 'A', "A", "Knoll south ramp", 136f, 144f, 184f, 204f, 0f, Tier);
            // The knoll's west edge from y 204 to 208 is the one-way drop: no kerb there.
            Lane("F14", 'A', "A", "Oak knoll", 132f, 148f, 204f, 218f, Tier, Tier).NoKit.Add(new Opening { Side = 'W', A = 204f, B = 208f });

            // ---- Stage B
            // The north ramp ends on the footway, which carries its own rails.
            Lane("F15", 'B', "SG", "Knoll north ramp", 138f, 142f, 218f, 230f, Tier, 0f).North = null;
            Lane("F16", 'B', "E3", "Landing hollow", 120f, 132f, 198f, 210f);
            // E3's north side is the rail on the verge, not a run on its own edge.
            Add("F17", 'B', "E3", "E3 Fire Field", 92f, 120f, 198f, 230f, 0f, 0f, false, "K02", fence, "K01", null).Openings =
                Open(('E', 198f, 202f), ('N', 92f, 120f));
            // The verges are cut where the bridge and footway decks lie on them, so the two never share a face.
            Add("F18a", 'B', "E3", "South verge", 92f, 103f, 230f, 232f, 0f, 0f, true, null, fence, null, null);
            Add("F18b", 'B', "E3", "South verge", 109f, 120f, 230f, 232f, 0f, 0f, true, null, null, "K03", null);
            Add("F19a", 'B', "SG", "North verge", 100f, 103f, 238f, 240f, 0f, 0f, true, null, "K03", null, "K03");
            Add("F19b", 'B', "SG", "North verge", 109f, 138f, 238f, 240f, 0f, 0f, true, null, null, null, "K03");
            Add("F19c", 'B', "SG", "North verge", 142f, 178f, 238f, 240f, 0f, 0f, true, null, null, "K03", "K03");

            // ---- Stage C
            Add("F20", 'C', "SG", "Dyke lane", 100f, 178f, 240f, 248f, 0f, 0f, true, null, fence, fence, fence);
            Add("F21", 'C', "G", "Wallow flat", 146f, 170f, 248f, 254f, 0f, 0f, false, null, "K01", "K01", "K01");
            Lane("F22", 'C', "E4", "Farm lane", 170f, 178f, 248f, 258f);
            Add("F23", 'C', "E4", "E4 Farmstead yard", 160f, 188f, 258f, 284f, 0f, 0f, false, "K03", "K04", "K04", "K04").Openings =
                Open(('S', 170f, 178f), ('N', 170f, 178f), ('E', 270f, 274f));
            Add("F24", 'C', "D2", "House path", 188f, 190f, 270f, 274f, 0f, 0f, false, "K03", null, null, "K04").Openings =
                Open(('W', 270f, 274f), ('E', 270f, 274f));

            // ---- Stage D
            Lane("F25", 'D', "B", "Lane", 170f, 178f, 284f, 294f);
            Lane("F26", 'D', "B", "Shrine B forecourt", 168f, 180f, 294f, 302f).Openings =
                Open(('S', 170f, 178f), ('N', 170f, 178f), ('W', 298f, 302f));
            Add("F27", 'D', "D3", "Grave track", 156f, 168f, 298f, 302f, 0f, 0f, false, "K02", null, null, "K01");
            Add("F28", 'D', "D3", "D3 mass grave", 136f, 156f, 292f, 308f, 0f, 0f, false, "K02", "K01", "K01", "K01").Openings =
                Open(('E', 298f, 302f));
            Lane("F29", 'D', "E5", "Lane", 170f, 178f, 302f, 314f);
            Add("F30", 'D', "E5", "E5 Mill Yard", 158f, 190f, 314f, 340f, 0f, 0f, false, "K03", "K01", "K01", "K01").Openings =
                Open(('S', 170f, 178f), ('N', 182f, 190f));
            Lane("F31", 'D', "CW", "Exit lane", 182f, 190f, 340f, 364f);
            Add("F32", 'D', "CW", "Causeway", 184f, 188f, 364f, 400f, 0f, 0f, true, stone, stone, stone, stone);

            // ---- Shapes for the backdrop only: where the ground must stay flat although no floor is there.
            list.Add(Pad("LaneFromL2", 'A', 60f, 68f, -8f, 0f));
            list.Add(Pad("DescentRow", 'A', 70f, 74f, 46f, 70f));
            list.Add(Pad("Farmhouse", 'C', 190f, 201f, 266f, 278f));
            return list;
        }

        private static Floor Pad(string id, char stage, float x0, float x1, float z0, float z1)
        {
            return new Floor
            {
                Id = id, Stage = stage, Virtual = true, X0 = x0, X1 = x1, Z0 = z0, Z1 = z1,
                Poly = new[] { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) },
            };
        }

        // The sunken lane's banks stand to Y = 5 from y 0 to y 30 (15.3).
        public const float SunkenTop = 5f, SunkenEnd = 30f;

        // ---------------------------------------------------------------- Placed prefabs (15.4)

        public class Place
        {
            public char Stage;
            public string Group, Area, Id, Name;
            public Vector3 Position;
            public float Yaw;
            public string Note;
        }

        private static Place P(char stage, string group, string area, string id, string name, float x, float z, float y = 0f, float yaw = 0f, string note = null)
        {
            return new Place { Stage = stage, Group = group, Area = area, Id = id, Name = name, Position = new Vector3(x, y, z), Yaw = yaw, Note = note };
        }

        public static readonly List<Place> Places = new List<Place>
        {
            // Stage A
            P('A', "Props", "A0", "C14", "RequisitionCart", 68f, 42f, Tier),
            P('A', "Hazards", "E1a", "H02", "CropRow_Descent", 72f, 46f),
            P('A', "Hazards", "E1a", "H07", "Gibbet_Descent_1", 58.5f, 62f),
            P('A', "Hazards", "E1", "H07", "Gibbet_Descent_2", 69.5f, 90f),
            P('A', "Props", "E1a", "C02", "KennelWagon", 70.5f, 78f),
            P('A', "Props", "E1a", "C15", "Carcass_Ox", 64f, 81f),
            P('A', "Props", "E1", "C01", "HayWagon", 57.5f, 101f),
            P('A', "Props", "E1", "C04", "HedgeStub", 62f, 109f),
            P('A', "Props", "E1", "C03", "Cairn", 70f, 107f),
            P('A', "Props", "E1", "C15", "Carcass", 62.5f, 117f),
            P('A', "Props", "D1", "S06", "OrchardTree_1", 48f, 130f),
            P('A', "Props", "D1", "S06", "OrchardTree_2", 56f, 130f, 0f, 70f),
            P('A', "Props", "D1", "S06", "OrchardTree_3", 48f, 136f, 0f, 150f),
            P('A', "Props", "D1", "S06", "OrchardTree_4", 56f, 136f, 0f, 230f),
            P('A', "Props", "D1", "S06", "OrchardTree_Bearing", 52f, 140f, 0f, 300f),
            P('A', "Props", "E2", "C05", "PloughTeam", 125f, 141f),
            P('A', "Hazards", "E2", "H07", "Gibbet_Fallow", 101.5f, 139f),
            P('A', "Markers", "A", "M01", "Shrine_A", 140f, 212f, Tier),
            P('A', "Props", "A", "S05", "BoundaryOak", 144f, 215f, Tier),

            // Stage B
            P('B', "Hazards", "E3", "H02", "CropRow_West", 101f, 202f),
            P('B', "Hazards", "E3", "H02", "CropRow_East", 111f, 202f),
            P('B', "Hazards", "E3", "H03", "BrandStake_West", 101f, 201f),
            P('B', "Hazards", "E3", "H03", "BrandStake_East", 111f, 201f),
            P('B', "Hazards", "E3", "H02", "CropRow_Ash", 89f, 202f),
            P('B', "Hazards", "E3", "H03", "BrandStake_Fallen", 89f, 201f),
            P('B', "Props", "E3", "C06", "StoneRoller", 106f, 213f),
            P('B', "Props", "E3", "C07", "SeedDrill", 96f, 219f),
            P('B', "Props", "BR", "S01", "StoneBridge", 106f, 235f),
            P('B', "Props", "SG", "S02", "SluiceFootway", 140f, 235f),
            P('B', "Markers", "SG", "M02", "SluiceGate", 140f, 240f),

            // Stage C
            P('C', "Hazards", "G", "H04", "Mud_Wallow", 158f, 247f),
            P('C', "Hazards", "G", "H08", "Gibbet_Wallow_Crowed", 158.5f, 251f),
            P('C', "Hazards", "G", "H07", "Gibbet_Wallow_West", 149.5f, 253f),
            P('C', "Hazards", "G", "H07", "Gibbet_Wallow_East", 166.5f, 253f),
            P('C', "Hazards", "E4", "H08", "Gibbet_Yard_West", 165.5f, 261f),
            P('C', "Hazards", "E4", "H08", "Gibbet_Yard_East", 182.5f, 261f),
            P('C', "Props", "E4", "C01", "HayWagon", 180.5f, 265f),
            P('C', "Props", "E4", "C08", "Well", 176f, 273f),
            P('C', "Props", "D2", "S03", "Farmhouse", 195.5f, 272f),

            // Stage D
            P('D', "Markers", "B", "M01", "Shrine_B", 178f, 298f),
            P('D', "Props", "B", "S07", "CellarFront", 180.5f, 298f, 0f, 90f),
            P('D', "Hazards", "D3", "H04", "Mud_Grave", 146f, 300f),
            P('D', "Props", "D3", "C12", "LimeMound", 140f, 295f),
            P('D', "Props", "D3", "C13", "GraveCart", 139f, 300f),
            // Owner's review: no sail hazard; the mill has a stair to its door instead (foot at y 334.6,
            // landing on the mound from y 340 to 342).
            P('D', "Props", "E5", "S08", "MillStair", 174f, 334.6f),
            P('D', "Props", "E5", "C15", "Carcass_Hound", 172.5f, 333f),
            P('D', "Props", "E5", "C10", "GrainCart", 179.5f, 321f),
            P('D', "Props", "E5", "C09", "Millstones", 164f, 325f),
            P('D', "Props", "E5", "C11", "Trough", 184f, 329f),
            P('D', "Props", "E5", "S04", "Windmill", 174f, 346f, Tier),

            // ---- Points of interest along the lanes (owner's review of the blockout): what the people who
            // were told to leave left on the road. Each stands on a verge, so the lane keeps a clear way past,
            // and the road (L3Ground) bends round it.
            P('A', "Props", "S", "C03", "Poi_RockFall", 66.4f, 13f, 1.06f),
            P('A', "Props", "E1a", "C14", "Poi_Cart_Descent", 61.7f, 63.5f, 0f, 20f),
            P('A', "Props", "E1", "C13", "Poi_Cart_Lane", 66.5f, 90.5f, 0f, -12f),
            P('A', "Props", "E2", "C04", "Poi_HedgeStub_Furrow", 87.5f, 115.5f, 0f, -31f),
            P('A', "Props", "A", "C15", "Poi_Carcass_Firebreak", 123.3f, 151.9f, 0f, 40f),
            P('A', "Props", "A", "C13", "Poi_Cart_Firebreak", 130.3f, 161.65f, 0f, 30f),
            P('A', "Props", "A", "C03", "Poi_BoundaryStone", 131.6f, 174.8f),
            P('C', "Props", "SG", "C14", "Poi_Cart_Dyke", 121.5f, 246.6f, 0f, 84f),
            P('C', "Props", "SG", "C03", "Poi_Cairn_Dyke", 133.5f, 246.7f),
            P('C', "Props", "E4", "C10", "Poi_Cart_FarmLane", 176.4f, 252.5f, 0f, 90f),
            P('D', "Props", "B", "C13", "Poi_Cart_CellarLane", 171.7f, 289.5f, 0f, 8f),
            P('D', "Props", "E5", "C03", "Poi_Cairn_MillLane", 176.6f, 308f),
            P('D', "Props", "CW", "C14", "Poi_Cart_ExitLane", 188.1f, 351.5f, 0f, -15f),
            P('D', "Props", "CW", "C03", "Poi_Cairn_ExitLane", 183.4f, 358.5f),
        };

        // Kit pieces that 15.4 places by hand (the barn ruin in E4), as runs: kit, area, from, to (the run's
        // centre line), stage.
        public class KitRun
        {
            public char Stage;
            public string Kit, Area, Name;
            public Vector2 From, To;
        }

        public static readonly List<KitRun> KitRuns = new List<KitRun>
        {
            new KitRun { Stage = 'C', Kit = "K04", Area = "E4", Name = "Barn_North", From = new Vector2(162f, 279.5f), To = new Vector2(171f, 279.5f) },
            new KitRun { Stage = 'C', Kit = "K04", Area = "E4", Name = "Barn_East_N", From = new Vector2(170.5f, 274f), To = new Vector2(170.5f, 279f) },
            new KitRun { Stage = 'C', Kit = "K04", Area = "E4", Name = "Barn_East_S", From = new Vector2(170.5f, 266f), To = new Vector2(170.5f, 270f) },
            new KitRun { Stage = 'C', Kit = "K03", Area = "E4", Name = "Barn_South_W", From = new Vector2(162f, 266.5f), To = new Vector2(165f, 266.5f) },
            new KitRun { Stage = 'C', Kit = "K03", Area = "E4", Name = "Barn_South_E", From = new Vector2(168f, 266.5f), To = new Vector2(170f, 266.5f) },
            // E3's north side: the rail on the south verge at y 231, clear of the bridge.
            new KitRun { Stage = 'B', Kit = "K06", Area = "E3", Name = "Rail_W", From = new Vector2(92f, 231f), To = new Vector2(103f, 231f) },
            new KitRun { Stage = 'B', Kit = "K06", Area = "E3", Name = "Rail_E", From = new Vector2(109f, 231f), To = new Vector2(120f, 231f) },
        };

        // ---------------------------------------------------------------- Spawn markers (15.5)

        public class Spawn
        {
            public string Encounter, State, Leash;
            public Kneel.Markers.EnemyType Type;
            public Vector2 Position;
            public float Aggro;
        }

        private static Spawn S(string encounter, Kneel.Markers.EnemyType type, float x, float z, string state, string leash = "", float aggro = 0f)
        {
            return new Spawn { Encounter = encounter, Type = type, Position = new Vector2(x, z), State = state, Leash = leash, Aggro = aggro };
        }

        public static readonly List<Spawn> Spawns = new List<Spawn>
        {
            S("E1a", Kneel.Markers.EnemyType.Hound, 63f, 80f, "Feeding", "", 14f),
            S("E1", Kneel.Markers.EnemyType.Hound, 60.5f, 117f, "Feeding"),
            S("E1", Kneel.Markers.EnemyType.Hound, 64.5f, 117f, "Feeding"),
            S("E1", Kneel.Markers.EnemyType.Hound, 71.5f, 117f, "Asleep", "Wakes 3 s after the other two aggro"),
            S("E2", Kneel.Markers.EnemyType.Brute, 114.5f, 131f, "Circling", "Aggro when the player steps onto F11. Leash: F11"),
            S("E3", Kneel.Markers.EnemyType.Hound, 100.5f, 209f, "Bedded in the west row", "Wakes when the player passes y 202 or its row ignites"),
            S("E3", Kneel.Markers.EnemyType.Hound, 101.5f, 221f, "Bedded in the west row", "Wakes when the player passes y 202 or its row ignites"),
            S("E3", Kneel.Markers.EnemyType.Hound, 110.5f, 211f, "Bedded in the east row", "Wakes when the player passes y 202 or its row ignites"),
            S("E3", Kneel.Markers.EnemyType.Hound, 111.5f, 219f, "Bedded in the east row", "Wakes when the player passes y 202 or its row ignites"),
            S("E3", Kneel.Markers.EnemyType.Hound, 106.5f, 227f, "Standing", "In the open, at the bridge foot"),
            S("G", Kneel.Markers.EnemyType.Footman, 158.5f, 251f, "On the gibbet", "Woken by that gibbet. Leash: the mud volume"),
            S("E4", Kneel.Markers.EnemyType.Brute, 176.5f, 277f, "At the well", "Leash: F23"),
            S("E4", Kneel.Markers.EnemyType.Footman, 166.5f, 273f, "Standing in the barn"),
            S("E4", Kneel.Markers.EnemyType.Footman, 165.5f, 261f, "On the gibbet", "Woken by that gibbet"),
            S("E4", Kneel.Markers.EnemyType.Footman, 182.5f, 261f, "On the gibbet", "Woken by that gibbet"),
            S("E5", Kneel.Markers.EnemyType.Brute, 174.5f, 331f, "At the foot of the mill stair", "Leash: F30"),
            S("E5", Kneel.Markers.EnemyType.Hound, 170.5f, 337.5f, "Asleep", "Wakes when the player passes y 316"),
            S("E5", Kneel.Markers.EnemyType.Hound, 162.5f, 331f, "Asleep", "Wakes when the player passes y 316"),
            S("E5", Kneel.Markers.EnemyType.Hound, 186.5f, 331f, "Asleep", "Wakes when the player passes y 316"),
            S("D3", Kneel.Markers.EnemyType.Brute, 147f, 300f, "Digging", "Leash: the mud volume. Never leaves it"),
        };

        public static bool Upto(char stage, char current)
        {
            return stage <= current;
        }

        public static bool KeepClearContains(Vector2 p, char stage)
        {
            foreach (var keep in KeepClear)
            {
                if (keep.Stage <= stage && keep.Contains(p, 0.3f))
                {
                    return true;
                }
            }

            return Ditch.Stage <= stage && Ditch.Contains(p, 0.3f);
        }

        public static bool OnBlock(Vector2 p, char stage)
        {
            foreach (var block in Blocks)
            {
                if (block.Stage <= stage && block.Contains(p, 0.3f))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
