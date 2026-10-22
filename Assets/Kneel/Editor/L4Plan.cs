using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Section 15 of L4_AssetReport.md (the blockout spec) as data: floors, their edges, stairs, everything placed.
    // Plan (x, y) is Unity (X, Z). Nothing here builds anything; L4Level reads it.
    //
    // The route is a zigzag spine (owner's review of the first blockout, 2026-10-19: "more interesting than a
    // straight line"). Every arena sits to one side of the one before it, joined by a sideways leg, and the route
    // still only climbs up-screen through the arenas:
    // - S, BA and E1 run up the middle (x 54-70) to the rubble gap at x 60-64, 76 m further south since the
    //   outer ward went in (Gap).
    // - O1, the mustering yard, swings west (x 36-64); the stable row is the leg back east under the muster
    //   rampart, into O2, the forge yard (x 56-82); the Lists gate runs north out of its west end into E2.
    //   D7, the barracks yard, opens off the forge yard's west side.
    // - E2 the Lists swing west (x 36-64), mirrored from the spec, so their stair is at the far west end and the
    //   prison lane (D3) leaves the north-east corner, going east.
    // - WW, the curtain walk, is the long leg back east over the Lists (x 36-80) to the guardroom.
    // - A, E3, E4 and the two towers sit 6 m east of the spec (guardroom at 66, courtyard 52-80, terrace 80-93).
    // - NW: down the NW tower's stair into the west lane's stub, east along the inner ward under the north wall,
    //   up into the chapel (E5, moved out east to x 66-90), out its west door into a side yard and the lane, north.
    // - CL, E6 and the threshold swing back west (6 m west, 2 m north of the spec).
    //
    // Where the spec's numbers and the built prefabs disagree, the layout follows the prefabs: the towers'
    // doors, the terrace and landing, merlons closing every T+1 join the route must not take, the courtyard
    // starting at the foot of the T+1 block. The report's "Phase 2" section lists them.
    public static class L4Plan
    {
        public const float T0 = 0f, T1 = 5f, TM1 = -3f;
        public const float RavineY = -12f;

        // The outer ward (owner, 2026-10-21: the level is "a bit too small... add 2 more arena... moving the
        // gate/bridge further away"): everything from E1's rubble gap south (the gatehouse, the apron, the causeway
        // and the farm track) stands this far south of where it was, and the ward fills the gap.
        public const float Gap = 76f;

        public enum Edit
        {
            Skip,       // nothing: a structure stands here, or the edge is open by design
            NoTop,      // a raised edge keeps its face but carries no parapet (a stair arrives, the drop)
            Close,      // merlons on a same-height join the route must not take
            Hoarding,   // timber hoarding bays instead of the parapet
            Breach,     // the breach edge instead of merlons
            Cut,        // a T0 boundary as the camera-side cut wall
        }

        public class Span
        {
            public char Side;
            public float A, B;
            public Edit Kind;
        }

        public class Floor
        {
            public string Id, Area, Label;
            public float X0, X1, Z0, Z1, Y;

            // Raised mass whose top kit a structure provides (the towers): faces only.
            public bool Tower;

            // Masonry that fills a space the zigzag leaves between walls: solid to its top if raised (faces only),
            // or an enclosed yard at T0. Never on the route.
            public bool Mass;

            // A T0 floor on the ravine: its boundary is the low parapet, with a cliff below.
            public bool Ravine;

            // A T0 floor whose open south (camera) side is the cut wall.
            public bool CutSouth;

            // Under L4L5_Threshold rather than the level's own groups.
            public bool Threshold;

            // A raised mass made a walkable outpost (refinement, owner 2026-10-19: "make it feel like an outpost
            // with guardrails around... navigate up here via stairs"): its edges get parapets and merlons like any
            // raised floor, not bare faces.
            public bool Outpost;

            // Not on the crag (the causeway, the timber bridge): the surroundings leave the ground under it alone.
            public bool Bridge;

            // Collider only; something else draws it (the timber bridge).
            public bool Hidden;

            public List<Span> Spans = new List<Span>();
            public List<L4Paving.Splash> Splashes = new List<L4Paving.Splash>();

            public bool Raised => Y > 0.1f;

            public bool Contains(Vector2 p, float grow = 0f)
            {
                return p.x > X0 - grow && p.x < X1 + grow && p.y > Z0 - grow && p.y < Z1 + grow;
            }

            public Floor Do(char side, float a, float b, Edit kind)
            {
                Spans.Add(new Span { Side = side, A = a, B = b, Kind = kind });
                return this;
            }

            public Floor Splash(float x, float z, float radius, int seed)
            {
                Splashes.Add(new L4Paving.Splash(new Vector2(x, z), radius, seed));
                return this;
            }
        }

        private static List<Floor> floors;

        public static List<Floor> Floors => floors ?? (floors = BuildFloors());

        public static Floor Find(string id)
        {
            return Floors.Find(f => f.Id == id);
        }

        // The floor whose footprint holds p (strictly), other than 'except'; where floors overlap in plan (the
        // rampart landing over the courtyard) the highest.
        public static Floor At(Vector2 p, Floor except = null)
        {
            Floor best = null;
            foreach (var f in Floors)
            {
                if (f != except && f.Contains(p) && (best == null || f.Y > best.Y))
                {
                    best = f;
                }
            }

            return best;
        }

        private static List<Floor> BuildFloors()
        {
            var list = new List<Floor>();
            Floor F(string id, string area, string label, float x0, float x1, float z0, float z1, float y)
            {
                var f = new Floor { Id = id, Area = area, Label = label, X0 = x0, X1 = x1, Z0 = z0, Z1 = z1, Y = y };
                list.Add(f);
                return f;
            }

            // ---- S, BA, E1 up the middle. The causeway's deck is a floor like the yards (owner, 2026-10-19: the
            // bridge should use the same cobblestones as the rest); the S12 spans carry its parapets and arches.
            // The farm track the player arrives by out of L3's fields (owner, 2026-10-19: "a smooth transition from lvl 3
            // Farmland... no hard lines"): open land, fenced (L4Farmland), the ground itself drawn by the surroundings.
            var track = F("F00", "S", "Farm track", 56f, 64f, -120f, -86f, T0).Do('W', -120f, -86f, Edit.Skip).Do('E', -120f, -86f, Edit.Skip).Do('S', 56f, 64f, Edit.Skip);
            track.Bridge = track.Hidden = true;
            F("F01", "S", "Causeway", 56f, 64f, -86f, -36f, T0).Do('W', -86f, -36f, Edit.Skip).Do('E', -86f, -36f, Edit.Skip).Do('S', 56f, 64f, Edit.Skip).Bridge = true;
            F("F02", "BA", "Barbican apron", 44f, 76f, -36f, -19f, T0).Do('S', 56f, 64f, Edit.Skip).Do('N', 44f, 76f, Edit.Skip).Ravine = true;
            F("F03", "E1", "Gatehouse passage", 54f, 70f, -19f, 8f, T0).Do('W', -19f, 8f, Edit.Skip).Do('E', -19f, 8f, Edit.Skip).Do('N', 54f, 70f, Edit.Skip)
                .Splash(58f, 0f, 2.6f, 11).Splash(62f, 1f, 2.4f, 23).Splash(66f, 0f, 2.7f, 37).Splash(62f, -3f, 2.5f, 41);
            F("F03g", "E1", "Rubble gap", 60f, 64f, 8f, 10f, T0).Do('W', 8f, 10f, Edit.Skip).Do('E', 8f, 10f, Edit.Skip);
            // D4, a side path off the apron's west end: a ledge over the ravine where the refugees left their dead.
            F("F02w", "D4", "Ravine lookout", 34f, 44f, -30f, -22f, T0).Ravine = true;

            // ---- The outer ward (refinement, 2026-10-21): two arenas, the legs between them and a side yard.
            // O1, the mustering yard, west of the rubble gap. Its south (camera) side fades; the gatehouse's own
            // rubble wall closes x 54-60.
            F("F30", "O1", "Mustering yard", 36f, 64f, 10f, 34f, T0).Do('S', 54f, 60f, Edit.Skip).CutSouth = true;
            // The stable row: east out of the yard under the muster rampart, then north into the forge yard.
            F("F31", "SR", "Stable row", 64f, 82f, 26f, 34f, T0).CutSouth = true;
            F("F31n", "SR", "Stable row (bend)", 76f, 82f, 34f, 42f, T0);
            // O2, the forge yard, east; its west wall has the barracks gate in it.
            F("F32", "O2", "Forge yard", 56f, 82f, 42f, 64f, T0);
            // The Lists gate: north out of the forge yard's west end into the Lists (their old opening from E1).
            F("F33", "LG", "Lists gate", 54f, 64f, 64f, 86f, T0);
            // D7, the barracks yard, off the forge yard through a breach in its west wall (the forge yard's wall
            // and the bailey's south wall are its own east and north walls).
            F("F34", "D7", "Barracks yard", 36f, 54f, 42f, 64f, T0).Do('E', 42f, 64f, Edit.Skip).Do('N', 36f, 54f, Edit.Skip);
            F("F34g", "D7", "Barracks breach", 54f, 56f, 52f, 56f, T0).Do('N', 54f, 56f, Edit.Skip).Do('S', 54f, 56f, Edit.Skip);
            // The muster rampart between the mustering yard and the forge yard: solid, out of reach, manned once.
            var rampart = F("M07", "O1", "Muster rampart", 36f, 76f, 34f, 42f, T1);
            rampart.Mass = rampart.Outpost = true;

            // ---- E2 swings west; D3 leaves its north-east corner going east
            F("F04", "E2", "The Lists", 36f, 64f, 86f, 108f, T0).Do('S', 54f, 64f, Edit.Skip).Do('S', 36f, 54f, Edit.Cut).Do('E', 89f, 93f, Edit.Skip).CutSouth = true;
            // Behind the Lists' cut wall, an enclosed outer bailey (its east wall the Lists gate's west wall); east of
            // them a bastion up to the curtain walk.
            F("M01", "E2", "Outer bailey", 36f, 52f, 66f, 86f, T0).Do('E', 66f, 86f, Edit.Skip).Mass = true;
            // The bastion over the Lists is a lookout post, reached by a stair cut into it from the Lists' east side
            // (x 64-71.8, y 89-93): south and north of the stair well, and the landing at its head.
            F("M02a", "E2", "Bastion lookout (south)", 64f, 80f, 86f, 89f, T1).Outpost = true;
            F("M02b", "E2", "Bastion lookout (north)", 64f, 80f, 93f, 98f, T1).Outpost = true;
            F("M02c", "E2", "Bastion lookout (stair head)", 71.8f, 80f, 89f, 93f, T1).Do('W', 89f, 93f, Edit.Skip).Outpost = true;
            F("F05", "D3", "Prison lane", 64f, 78f, 102f, 106f, T0).Do('N', 64f, 78f, Edit.Skip);
            F("F05c", "D3", "Cells", 65.5f, 78f, 98f, 102f, T0).Do('W', 98f, 102f, Edit.Skip);

            // ---- WW, the long leg east, and A (T+1)
            F("F06", "WW", "Inner curtain walk", 36f, 80f, 108f, 112f, T1).Do('S', 40f, 60f, Edit.Hoarding).Do('S', 36f, 40f, Edit.NoTop);
            F("F07", "A", "Guardroom level", 52f, 80f, 112f, 124f, T1).Do('N', 62f, 66f, Edit.NoTop).Do('N', 78f, 80f, Edit.Close);
            F("F08", "SC", "West wall-walk (shortcut)", 48f, 52f, 112f, 154f, T1);

            // ---- E3, E4 and the north walk, 6 m east of the spec
            F("F09", "E3", "Courtyard", 52f, 80f, 124f, 154f, T0)
                .Splash(56f, 151f, 3.1f, 17).Splash(64f, 151f, 2.9f, 141).Splash(72f, 151f, 3.2f, 263);
            F("F11", "E4", "East terrace", 80f, 93f, 124f, 154f, T1)
                .Do('W', 138f, 142f, Edit.NoTop).Do('E', 142f, 154f, Edit.Breach).Do('N', 80f, 83f, Edit.Close);
            F("F11L", "E3", "Rampart landing", 78f, 80f, 124f, 128f, T1).Do('S', 78f, 80f, Edit.Close).Do('W', 124f, 128f, Edit.NoTop);
            F("F12", "NW", "North wall-walk", 57.5f, 83f, 154f, 158f, T1).Do('S', 80f, 83f, Edit.Skip);
            F("F13", "NE", "NE tower", 83f, 93f, 154f, 164f, T1).Tower = true;
            F("F14", "NW", "NW tower", 47.5f, 57.5f, 154f, 164f, T1).Tower = true;
            // Solid behind the NE tower, under D2's stair turret, between the ward's end and the chapel.
            var ne = F("M05", "D2", "Bastion (NE)", 83f, 93f, 164f, 168f, T1);
            ne.Mass = ne.Outpost = true;

            // ---- IW east under the north wall, E5 out east, out west through a side yard into WL
            F("F15", "IW", "Inner ward", 57.5f, 83f, 158f, 168f, T0).Do('N', 65.4f, 83f, Edit.Skip);
            F("F15b", "IW", "Ward neck", 53.4f, 57.5f, 164f, 168f, T0);
            F("F16", "WL", "West lane", 47.4f, 53.4f, 164f, 196.6f, T0).Do('E', 172.5f, 176.5f, Edit.Skip);
            F("F16b", "WL", "Chapel side yard", 53.4f, 65.4f, 181f, 191f, T0).Do('E', 181f, 191f, Edit.Skip);
            // D5, west off the lane by the NW tower stair's foot: a passage to the barricaded postern's guardroom.
            F("F16p", "D5", "Postern passage", 42f, 47.4f, 172f, 175f, T0).CutSouth = true;
            F("F16q", "D5", "Postern guardroom", 35f, 42f, 169f, 178f, T0).CutSouth = true;
            // Either side of the side yard, between the lane and the chapel: the guard post, up a stair cut into it
            // from the lane (x 53.4-61.2, y 172.5-176.5, beside the lane gate), and the signal post north of the yard,
            // reached over a timber bridge across it (x 57.5-60.5).
            F("M03a", "WL", "Guard post (south)", 53.4f, 65.4f, 168f, 172.5f, T1).Do('E', 168f, 172.5f, Edit.Skip).Outpost = true;
            F("M03b", "WL", "Guard post (north)", 53.4f, 65.4f, 176.5f, 181f, T1).Do('E', 176.5f, 181f, Edit.Skip).Do('N', 57.5f, 60.5f, Edit.NoTop).Outpost = true;
            F("M03c", "WL", "Guard post (stair head)", 61.2f, 65.4f, 172.5f, 176.5f, T1).Do('E', 172.5f, 176.5f, Edit.Skip).Do('W', 172.5f, 176.5f, Edit.Skip).Outpost = true;
            var bridge = F("F16t", "WL", "Timber bridge", 57.5f, 60.5f, 181f, 191f, T1).Do('W', 181f, 191f, Edit.Skip).Do('E', 181f, 191f, Edit.Skip);
            bridge.Bridge = bridge.Hidden = true;
            F("M04", "WL", "Signal post", 53.4f, 65.4f, 191f, 196.6f, T1).Do('E', 191f, 196.6f, Edit.Skip).Do('S', 57.5f, 60.5f, Edit.NoTop).Outpost = true;
            // The chapel floor, cut round the crypt stair (x 86-88, y 191-196).
            F("F17", "E5", "Chapel", 65.4f, 90.6f, 168f, 191f, T0).Do('N', 86f, 88f, Edit.Skip).Do('W', 168f, 191f, Edit.Skip).Do('E', 168f, 191f, Edit.Skip).Do('S', 65.4f, 90.6f, Edit.Skip);
            F("F17b", "E5", "Chapel north (west)", 65.4f, 86f, 191f, 196f, T0).Do('W', 191f, 196f, Edit.Skip).Do('N', 65.4f, 86f, Edit.Skip).Do('E', 191f, 196f, Edit.Skip);
            F("F17c", "E5", "Chapel north (east)", 88f, 90.6f, 191f, 196f, T0).Do('E', 191f, 196f, Edit.Skip).Do('N', 88f, 90.6f, Edit.Skip).Do('W', 191f, 196f, Edit.Skip);
            F("F19", "CL", "Cloister walk", 44f, 92f, 196.6f, 206f, T0).Do('S', 65.4f, 90.6f, Edit.Skip);
            // D6, north off the cloister beside E6a: the infirmary garth. Its west wall is the yard's east curtain.
            F("F19n", "D6", "Infirmary garth", 70f, 78f, 206f, 216f, T0).Do('W', 206f, 216f, Edit.Skip);

            // ---- E6 swings back west
            F("F20", "E6a", "Barbican yard", 40f, 68f, 206f, 226f, T0).Do('N', 48f, 60f, Edit.Skip);
            F("F21w", "E6a", "West wall-walk", 40f, 48f, 226f, 240f, T1).Do('E', 226f, 240f, Edit.Skip);
            F("F21e", "E6a", "East wall-walk", 60f, 68f, 226f, 240f, T1).Do('W', 226f, 240f, Edit.Skip);
            F("F22", "E6b", "Gate passage", 50f, 58f, 226f, 240f, T0).Do('W', 226f, 240f, Edit.Skip).Do('E', 226f, 240f, Edit.Skip)
                .Splash(54f, 231f, 2.8f, 307).Splash(54f, 235f, 2.9f, 311);
            F("F23", "E6c", "Forecourt", 42f, 66f, 240f, 255f, T0)
                .Do('S', 48f, 50f, Edit.Skip).Do('S', 58f, 60f, Edit.Skip).Do('N', 45f, 63f, Edit.Skip).Do('W', 248f, 252f, Edit.Skip).Do('E', 248f, 252f, Edit.Skip);
            // The kennels' bays, open to the forecourt (their walls are the kennels' own).
            F("F23kw", "E6c", "West kennel", 39f, 42f, 248f, 252f, T0).Do('W', 248f, 252f, Edit.Skip).Do('S', 39f, 42f, Edit.Skip).Do('N', 39f, 42f, Edit.Skip);
            F("F23ke", "E6c", "East kennel", 66f, 69f, 248f, 252f, T0).Do('E', 248f, 252f, Edit.Skip).Do('S', 66f, 69f, Edit.Skip).Do('N', 66f, 69f, Edit.Skip);
            // The court's outer sides meet the citadel's bastion (M06) at the same height: merlons close them.
            var west = F("F25b", "B", "Stair flank (west)", 42f, 45f, 255f, 263f, T1).Do('E', 255f, 263f, Edit.Skip).Do('W', 255f, 263f, Edit.Close);
            var east = F("F25c", "B", "Stair flank (east)", 63f, 66f, 255f, 263f, T1).Do('W', 255f, 263f, Edit.Skip).Do('E', 255f, 263f, Edit.Close);
            // From 262.8 so the grand stair's ramp (to 262.9) meets it.
            var court = F("F25", "B", "Kneeling Court", 42f, 66f, 262.8f, 268f, T1).Do('S', 45f, 63f, Edit.Skip).Do('N', 43.4f, 64.6f, Edit.Skip)
                .Do('N', 42f, 43.4f, Edit.Close).Do('N', 64.6f, 66f, Edit.Close).Do('W', 262.8f, 268f, Edit.Close).Do('E', 262.8f, 268f, Edit.Close);
            // The citadel's rock (refinement, 2026-10-19): a solid bastion at T+1 behind and beside the court that
            // carries the throne hall, the great keep and their towers (L4Citadel). Never on the route.
            var rock = F("M06", "B", "Citadel bastion", 26f, 82f, 268f, 352f, T1);
            var rockW = F("M06w", "B", "Citadel bastion (west)", 26f, 42f, 255f, 268f, T1);
            var rockE = F("M06e", "B", "Citadel bastion (east)", 66f, 82f, 255f, 268f, T1);
            rock.Mass = rockW.Mass = rockE.Mass = true;
            rock.Threshold = rockW.Threshold = rockE.Threshold = true;
            west.Threshold = east.Threshold = court.Threshold = true;
            return list;
        }

        // ---------------------------------------------------------------- Stairs (W09a): foot to top

        public class Stair
        {
            public string Name;
            public Vector3 Centre;
            public float Yaw;   // the direction it climbs
        }

        public static readonly List<Stair> Stairs = new List<Stair>
        {
            new Stair { Name = "Stair_ListsWest", Centre = new Vector3(38f, 0f, 104.1f), Yaw = 0f },
            new Stair { Name = "Stair_ShrineA", Centre = new Vector3(64f, 0f, 127.9f), Yaw = 180f },
            new Stair { Name = "Stair_Rampart", Centre = new Vector3(74.1f, 0f, 126f), Yaw = 90f },
            new Stair { Name = "Stair_NWTower", Centre = new Vector3(50f, 0f, 167.9f), Yaw = 180f },
            // Up into the outposts (refinement): each cut into its bastion, its well's sides the bastion's own faces.
            new Stair { Name = "Stair_Lookout", Centre = new Vector3(67.9f, 0f, 91f), Yaw = 90f },
            new Stair { Name = "Stair_GuardPost", Centre = new Vector3(57.3f, 0f, 174.5f), Yaw = 90f },
        };

        // ---------------------------------------------------------------- Placed prefabs

        public class Place
        {
            public char Stage;
            public string Group, Area, Id, Name;
            public Vector3 Position;
            public float Yaw;
            public bool Threshold;
        }

        private static Place P(char stage, string group, string area, string id, string name, float x, float z, float y = 0f, float yaw = 0f)
        {
            return new Place { Stage = stage, Group = group, Area = area, Id = id, Name = name, Position = new Vector3(x, y, z), Yaw = yaw };
        }

        private static Place B(char stage, string group, string id, string name, float x, float z, float y = 0f, float yaw = 0f)
        {
            var p = P(stage, group, "B", id, name, x, z, y, yaw);
            p.Threshold = true;
            return p;
        }

        public static readonly List<Place> Places = new List<Place>
        {
            // ---- Stage C: structures
            P('C', "Structures", "S", "S12", "Causeway_0", 60f, -81f),
            P('C', "Structures", "S", "S12", "Causeway_1", 60f, -71f),
            P('C', "Structures", "S", "S12", "Causeway_2", 60f, -61f),
            P('C', "Structures", "S", "S12", "Causeway_3", 60f, -51f),
            P('C', "Structures", "S", "S12", "Causeway_4", 60f, -41f),
            P('C', "Structures", "E1", "S01", "OuterGatehouse", 62f, -19f),
            P('C', "Structures", "D3", "S10", "PrisonCells", 64f, 104f, 0f, 180f),
            P('C', "Structures", "A", "S02", "Guardroom", 66f, 118f, T1),
            P('C', "Structures", "E3", "S11", "Gallows", 75f, 142f),
            P('C', "Structures", "NE", "S03", "TowerNE", 88f, 159f, T1),
            P('C', "Structures", "NW", "S04", "TowerNW", 52.5f, 159f, T1),
            P('C', "Structures", "E5", "S05", "Chapel", 78f, 182f),
            P('C', "Structures", "D1", "S06", "Crypt", 87f, 191f, -0.15f),
            P('C', "Structures", "CL", "S14", "Cloister", 59.5f, 197.2f, 0f, 180f),
            P('C', "Structures", "E6b", "S07", "InnerGate", 54f, 233f),
            P('C', "Structures", "B", "S08", "GrandStair", 54f, 255f),
            B('C', "Structures", "S09", "ThroneDoors", 54f, 268f, T1),

            // ---- Stage D: E1-E3 and Shrine A
            P('D', "Markers", "S", "M10a", "PlayerSpawn", 60f, -114f),
            P('D', "Hazards", "BA", "H04b", "OuterPortcullis", 60f, -19f),
            P('D', "Markers", "BA", "M13", "Demo_OuterPortcullis", 60f, -29f),
            P('D', "Props", "E1", "F04", "GateCaptain", 56f, -14f, 0f, 30f),
            P('D', "Markers", "E1", "M03", "HeavyWeapon", 56.6f, -13.7f, 0.35f, 30f),
            P('D', "Props", "E1", "C15a", "Barricade_Out", 62f, -15.5f),

            // ---- O1, the mustering yard: the garrison knelt here in its ranks when the master came, and died
            // kneeling. Racks along the west wall, the quartermaster's cart, mantlets the archers drilled behind; a
            // tripod over a spilt oil jar by the racks; three of the kneeling ranks are an ambush.
            P('D', "Props", "O1", "C21", "Rack_Muster_1", 37.6f, 18f, 0f, 90f),
            P('D', "Props", "O1", "C21", "Rack_Muster_2", 37.6f, 23f, 0f, 90f),
            P('D', "Props", "O1", "C21", "Rack_Muster_3", 37.6f, 28f, 0f, 90f),
            P('D', "Props", "O1", "C03", "SupplyCart_Muster", 44.5f, 13.2f, 0f, 70f),
            P('D', "Props", "O1", "C01", "Mantlet_Muster_1", 57.5f, 15.5f, 0f, -15f),
            P('D', "Props", "O1", "C01", "Mantlet_Muster_2", 57.5f, 31.2f, 0f, 190f),
            P('D', "Props", "O1", "C20", "OilJars_Muster", 39f, 31.5f, 0f, 120f),
            P('D', "Hazards", "O1", "H02", "Slick_Muster", 42.5f, 29.5f),
            P('D', "Hazards", "O1", "H03c", "Tripod_Muster", 42.5f, 27.3f),
            P('D', "Hazards", "O1", "H08a", "Kneeler_Muster_1", 49f, 21f, 0f, 0f),
            P('D', "Hazards", "O1", "H08b", "Kneeler_Muster_Twin_1", 53f, 24f, 0f, 0f),
            P('D', "Hazards", "O1", "H08b", "Kneeler_Muster_Twin_2", 45f, 24f, 0f, 0f),

            // ---- O2, the forge yard: the smithy that kept arming the dead, its forge against the north wall still
            // burning; the portcullis the smiths were mending, the iron stock, the quench trough, the coal.
            P('D', "Props", "O2", "C14", "FallenPortcullis_Forge", 64f, 49f, 0f, 75f),
            P('D', "Props", "O2", "C13", "Wagon_Forge", 80f, 49f, 0f, 90f),
            P('D', "Props", "O2", "C21", "Rack_Forge_1", 80.4f, 55f, 0f, -90f),
            P('D', "Props", "O2", "C21", "Rack_Forge_2", 80.4f, 59.5f, 0f, -90f),
            P('D', "Props", "O2", "C08", "Cauldron_Forge", 59.5f, 58.5f),
            P('D', "Props", "O2", "C20", "OilJars_Forge", 58f, 61.6f, 0f, 30f),
            P('D', "Hazards", "O2", "H02", "Slick_Forge", 61f, 55f),
            P('D', "Hazards", "O2", "H03c", "Tripod_Forge", 63.2f, 55f),
            P('D', "Props", "O2", "C19", "Chest_Forge", 80.5f, 62.6f, 0f, -90f),

            // ---- D7, the barracks yard (side path): where the garrison slept, under lean-tos against the walls.
            P('D', "Markers", "D7", "M05b", "Lore_D7", 38.6f, 61.2f),
            P('D', "Markers", "D7", "M05a", "Consumable_D7", 40.4f, 45.2f),
            P('D', "Hazards", "D7", "H08a", "Kneeler_Barracks", 41.8f, 46.6f, 0f, 200f),
            P('D', "Props", "D7", "F02", "Kneeler_Sentry", 39.6f, 60.4f, 0f, 150f),
            P('D', "Props", "E2", "C01", "Mantlet", 50f, 90f),
            P('D', "Props", "E2", "C02", "DeadHorse", 60f, 96f, 0f, -80f),
            P('D', "Props", "E2", "C03", "SupplyCart", 43.5f, 92f, 0f, -15f),
            P('D', "Hazards", "E2", "H02", "Slick_Lists_1", 53f, 99f),
            P('D', "Hazards", "E2", "H02", "Slick_Lists_2", 43f, 104f),
            P('D', "Hazards", "E2", "H03", "Cresset_Lists_1", 53f, 96.6f),
            P('D', "Hazards", "E2", "H03", "Cresset_Lists_2", 43f, 101.6f),
            P('D', "Hazards", "E2", "H08a", "Kneeler_Footman", 56f, 98f, 0f, 180f),
            P('D', "Hazards", "E2", "H08b", "Kneeler_Twin_1", 44f, 98.5f, 0f, 160f),
            P('D', "Hazards", "E2", "H08b", "Kneeler_Twin_2", 41f, 95f, 0f, 200f),
            P('D', "Markers", "A", "M01", "Shrine_A", 66f, 121f, T1, 180f),
            P('D', "Markers", "E3", "M06b", "KeyGrille", 70f, 126f, 0f, 90f),
            P('D', "Hazards", "E3", "H01", "PourRing_0", 56f, 151f),
            P('D', "Hazards", "E3", "H01", "PourRing_1", 64f, 151f),
            P('D', "Hazards", "E3", "H01", "PourRing_2", 72f, 151f),
            P('D', "Props", "E3", "W10", "Machicolation_0", 56f, 154f, T1),
            P('D', "Props", "E3", "W10", "Machicolation_1", 64f, 154f, T1),
            P('D', "Props", "E3", "W10", "Machicolation_2", 72f, 154f, T1),
            P('D', "Hazards", "E3", "H10", "PourTrough", 64f, 156.2f, T1),
            P('D', "Props", "E3", "C04a", "Tumbril_Turned", 64f, 136f, 0f, 90f),
            P('D', "Props", "E3", "C04b", "Tumbril_Traitor", 69f, 135f, 0f, 70f),
            P('D', "Props", "E3", "C05", "Well", 56f, 141f),
            P('D', "Props", "E3", "C06", "Coffins", 76.5f, 131.5f, 0f, -90f),
            P('D', "Hazards", "E3", "H06b", "Scaffold_Demo", 78.9f, 149f, 0f, 90f),
            P('D', "Markers", "E3", "M13", "Demo_Scaffold", 72f, 126f, 0f, 90f),
            P('D', "Hazards", "E3", "H06a", "Scaffold_Landing", 78.9f, 131f, 0f, 90f),
            P('D', "Props", "E3", "C23", "ExecutionBlock", 58f, 147f),

            // ---- Stage E: E4, the shortcut, the drop
            P('E', "Props", "E4", "C07", "Ballista", 83.6f, 146f, T1, 270f),
            P('E', "Props", "E4", "C08", "Cauldron", 83.2f, 135f, T1),

            // ---- Stage F: E5, E6, Shrine B, the threshold
            P('F', "Hazards", "E5", "H02", "Slick_Nave_S", 78f, 176f),
            P('F', "Hazards", "E5", "H02", "Slick_Nave_Crossing", 76f, 183f),
            P('F', "Hazards", "E5", "H02", "Slick_Nave_N", 80f, 189f),
            P('F', "Hazards", "E5", "H03b", "Corona", 75.5f, 183f),
            P('F', "Hazards", "E5", "H07", "ChoirLoft", 78f, 191.5f),
            P('F', "Hazards", "E5", "H08b", "Kneeler_Twin_3", 75f, 178f, 0f, 180f),
            P('F', "Hazards", "E5", "H08b", "Kneeler_Twin_4", 81f, 180f, 0f, 180f),
            P('F', "Hazards", "E5", "H08a", "Kneeler_Footman_2", 78f, 187f, 0f, 180f),
            P('F', "Props", "E5", "C08", "HallCauldron", 78f, 172.5f),
            P('F', "Props", "E5", "C19", "Chest_1", 70f, 178f, 0f, 90f),
            P('F', "Props", "E5", "C19", "Chest_2", 86.5f, 174f, 0f, -80f),
            P('F', "Hazards", "WL", "H04c", "LaneGate", 50.4f, 178f),
            P('F', "Hazards", "WL", "H05", "Winch", 55.5f, 186f, 0f, 90f),
            P('F', "Props", "E6a", "C11", "ToppledStatue", 54f, 208.5f),
            P('F', "Props", "E6a", "C13", "Wagon", 43.5f, 216f, 0f, 90f),
            P('F', "Props", "E6a", "C01", "Mantlet_Yard", 65f, 218f, 0f, -20f),
            P('F', "Props", "E6c", "C22a", "Kennel_W", 40.5f, 250f, 0f, -90f),
            P('F', "Props", "E6c", "C22b", "Kennel_E", 67.5f, 250f, 0f, 90f),
            P('F', "Hazards", "E6c", "H02", "Slick_Forecourt_W", 50f, 249f),
            P('F', "Hazards", "E6c", "H02", "Slick_Forecourt_E", 58f, 249f),
            P('F', "Hazards", "E6c", "H03c", "Tripod_W", 50f, 246.8f),
            P('F', "Hazards", "E6c", "H03c", "Tripod_E", 58f, 246.8f),
            P('F', "Props", "E6c", "C12", "BrokenStatue_W", 47f, 244f, 0f, 20f),
            P('F', "Props", "E6c", "C12", "BrokenStatue_E", 61f, 244f, 0f, -25f),
            B('F', "Markers", "M01", "Shrine_B", 54f, 263.6f, T1, 180f),
            B('F', "Markers", "M10b", "Exit", 54f, 267.2f, T1),

            // ---- Stage G: secrets, vignettes, dressing
            P('G', "Markers", "D3", "M02", "Handprint_D3", 64.6f, 104f, 0f, -90f),
            P('G', "Markers", "D1", "M02", "Handprint_D1", 88.4f, 190.2f, 0f, 180f),
            P('G', "Markers", "D3", "M05a", "Consumable_D3", 75f, 100.5f),
            P('G', "Markers", "D3", "M05b", "Lore_D3", 71f, 100f),
            P('G', "Markers", "D1", "M05b", "Lore_D1", 84.5f, 200.5f, -3.15f),
            P('G', "Markers", "D1", "M05c", "Cordial_D1", 89.5f, 201f, -3.15f),
            // The side paths (owner's review, 2026-10-19): three dead ends, each with a lore fragment or healing.
            P('G', "Props", "D4", "C17", "RefugeeCart_3", 40.8f, -23.5f, 0f, 95f),
            P('G', "Props", "D4", "C06", "Coffins_Lookout", 37f, -28.3f, 0f, 0f),
            P('G', "Markers", "D4", "M05b", "Lore_D4", 36.2f, -24.2f),
            P('G', "Props", "D5", "C15b", "Barricade_Postern", 36.2f, 173.5f, 0f, 90f),
            P('G', "Props", "D5", "F01a", "Kneeler_Postern", 38.6f, 175.6f, 0f, -90f),
            P('G', "Markers", "D5", "M05a", "Consumable_D5", 38.4f, 170.6f),
            P('G', "Props", "D6", "C18", "Pew_Garth_1", 71.2f, 209.5f, 0f, 90f),
            P('G', "Props", "D6", "C18", "Pew_Garth_2", 71.2f, 214f, 0f, 90f),
            P('G', "Props", "D6", "F01b", "Kneeler_Garth", 75.5f, 214.6f, 0f, 10f),
            P('G', "Props", "D6", "C19", "Chest_Garth", 77.2f, 209f, 0f, -90f),
            P('G', "Props", "D6", "C20", "Remedies_Garth", 77f, 213.6f, 0f, -90f),
            P('G', "Markers", "D6", "M05a", "Consumable_D6", 76.4f, 210.8f),
            P('G', "Markers", "D6", "M05b", "Lore_D6", 73.6f, 215.2f),
            P('G', "Props", "S", "C17", "RefugeeCart_1", 57.8f, -62f, 0f, 175f),
            P('G', "Props", "S", "C17", "RefugeeCart_2", 62.2f, -49f, 0f, 190f),
            P('G', "Props", "BA", "C16", "Ram", 65f, -22f, 0f, 10f),
            P('G', "Props", "BA", "F01a", "Kneeler_Gate_1", 52f, -23.5f, 0f, 180f),
            P('G', "Props", "BA", "F01a", "Kneeler_Gate_2", 54.5f, -24.2f, 0f, 172f),
            P('G', "Props", "BA", "F01b", "Kneeler_Gate_3", 57f, -23.2f, 0f, 186f),
            P('G', "Props", "BA", "F01a", "Kneeler_Gate_4", 69.5f, -23.8f, 0f, 178f),
            P('G', "Props", "BA", "F01b", "Kneeler_Gate_5", 72f, -24.4f, 0f, 190f),
            // The mustering yard's ranks: the dead still kneeling in their lines, facing the rampart where the
            // master's herald stood (the ambush's three kneel among them).
            P('G', "Props", "O1", "F01a", "Rank_1", 41f, 21f, 0f, 4f),
            P('G', "Props", "O1", "F01b", "Rank_2", 45f, 21f, 0f, -6f),
            P('G', "Props", "O1", "F01a", "Rank_3", 53f, 21f, 0f, 8f),
            P('G', "Props", "O1", "F02", "Rank_4", 57f, 21f, 0f, -3f),
            P('G', "Props", "O1", "F01b", "Rank_5", 41f, 24f, 0f, 2f),
            P('G', "Props", "O1", "F02", "Rank_6", 49f, 24f, 0f, 0f),
            P('G', "Props", "O1", "F01a", "Rank_7", 57f, 24f, 0f, 10f),
            P('G', "Props", "O1", "F01b", "Rank_8", 45f, 18f, 0f, -12f),
            P('G', "Props", "O1", "F01a", "Rank_9", 53f, 18f, 0f, 5f),
            P('G', "Props", "WW", "F05", "Cook", 49f, 107.2f, T1, 160f),
            P('G', "Props", "WW", "C20", "OilJars", 46.5f, 107f, T1, 180f),
            P('G', "Props", "E3", "F01a", "Kneeler_Block_1", 59.5f, 148f, 0f, 120f),
            P('G', "Props", "E3", "F01b", "Kneeler_Block_2", 56.5f, 146f, 0f, 250f),
            P('G', "Props", "D2", "F07", "DeadScout", 86.4f, 162f, T1 + 5f, 200f),
            P('G', "Markers", "D2", "M05a", "Consumable_D2", 84.8f, 161.4f, T1 + 5f),
            // On the east bank, beyond the ravine (L4Surroundings sets its height on the ground).
            P('G', "Props", "Siege", "C24", "SiegeCampRuin", 150f, 172f, RavineY, -100f),
            B('G', "Props", "F01a", "Court_Kneeler_1", 46f, 264.5f, T1, 0f),
            B('G', "Props", "F01b", "Court_Kneeler_2", 48.5f, 265f, T1, 6f),
            B('G', "Props", "F01a", "Court_Kneeler_3", 59.5f, 265f, T1, -4f),
            B('G', "Props", "F01b", "Court_Kneeler_4", 62f, 264.5f, T1, 3f),
            B('G', "Props", "F01a", "Court_Kneeler_5", 44.5f, 263.6f, T1, 10f),
            B('G', "Props", "F01b", "Court_Kneeler_6", 63.5f, 263.6f, T1, -8f),
        };

        // ---------------------------------------------------------------- Enemies (section 6)

        public class Enemy
        {
            public char Stage;
            public string Encounter, Id, State;
            public Vector3 Position;
            public float Yaw;
            public bool Threshold;
        }

        private static Enemy N(char stage, string encounter, string id, float x, float z, float y, float yaw, string state)
        {
            return new Enemy { Stage = stage, Encounter = encounter, Id = id, Position = new Vector3(x, y, z), Yaw = yaw, State = state };
        }

        public static readonly List<Enemy> Enemies = new List<Enemy>
        {
            N('D', "E1", "N02", 62f, 4f, 0f, 180f, "Still, facing the breach. Wakes when the heavy weapon is taken"),
            N('D', "O1", "N01", 49f, 30f, 0f, 180f, "Standing before the ranks, where the herald stood"),
            N('D', "O1", "N01", 59f, 27.5f, 0f, 200f, "Standing by the stable row"),
            N('D', "O1", "N01", 39.5f, 25.5f, 0f, 120f, "At the racks"),
            N('D', "O2", "N02", 71f, 57.5f, 0f, 180f, "At the anvil, hammering; turns when the player passes the wagon"),
            N('D', "O2", "N01", 62f, 46f, 0f, 150f, "By the portcullis"),
            N('D', "O2", "N01", 75f, 51f, 0f, 200f, "Standing"),
            N('D', "O2", "N01", 59f, 62f, 0f, 160f, "Holding the Lists gate"),
            N('D', "E3", "N02", 66f, 140f, 0f, 180f, "Patrols the yard; the tribunal key on his belt"),
            N('D', "E3", "N04", 75f, 144.5f, 2f, 200f, "On the gallows deck, covering the yard"),
            N('E', "E4", "N05", 88f, 150f, T1, 200f, "Two-thirds along the terrace"),
            N('E', "E4", "N02", 85.5f, 151.5f, T1, 180f, "In front of the NE tower door"),
            N('F', "E6a", "N02", 54f, 222f, 0f, 180f, "Holds the gate mouth"),
            N('F', "E6a", "N01", 46f, 214f, 0f, 160f, "Standing"),
            N('F', "E6a", "N01", 62f, 216f, 0f, 200f, "Standing"),
            N('F', "E6a", "N04", 44f, 228f, T1, 170f, "On the wall-walk west of the gate"),
            N('F', "E6b", "N06b", 54f, 233f, 0f, 180f, "Fills the passage"),
            N('F', "E6c", "N06a", 41.2f, 250f, 0f, 90f, "In the west kennel"),
            N('F', "E6c", "N03", 51f, 253.5f, 0f, 180f, "At the stair foot"),
            N('F', "E6c", "N03", 57f, 253.5f, 0f, 180f, "At the stair foot"),
            new Enemy { Stage = 'F', Encounter = "B", Id = "N07", Position = new Vector3(54f, T1, 265.4f), Yaw = 160f, State = "Feeding the braziers", Threshold = true },
        };

        // ---------------------------------------------------------------- Vistas and arenas (section 7)

        public class Volume
        {
            public char Stage;
            public string Id, Name;
            public Vector3 Centre, Size;
            public bool Threshold;

            // Vistas (M07), section 7's starting values: degrees below the horizon, vertical FOV (0 keeps the
            // gameplay camera's), metres further back, metres sideways, and a pan's target and length.
            public float Pitch, Fov, PullBack, Shift, PanDuration;
            public Vector3 PanTarget;
            public string Note;
        }

        private static Volume Vista(char stage, string name, float x, float z, float y, float sx, float sz, float pitch, float fov, float pullBack, string note)
        {
            var v = V(stage, "M07", name, x, z, y, sx, sz);
            v.Pitch = pitch;
            v.Fov = fov;
            v.PullBack = pullBack;
            v.Note = note;
            return v;
        }

        private static Volume V(char stage, string id, string name, float x, float z, float y, float sx, float sz, float sy = 4f)
        {
            return new Volume { Stage = stage, Id = id, Name = name, Centre = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz) };
        }

        public static readonly List<Volume> Volumes = new List<Volume>
        {
            Vista('D', "Vista_V1", 60f, -72f, 0f, 8f, 4f, 10f, 44f, 12f, "Gatehouse low in frame, the spire in the upper third, the causeway parapets converging on the breach."),
            Vista('D', "Vista_V1b", 62f, 12f, 0f, 4f, 3f, 30f, 0f, 4f, "Through the rubble gap: the kneeling ranks of the mustering yard, the muster rampart behind them, the Lists' towers beyond."),
            Vista('D', "Vista_V2", 66f, 120.5f, T1, 6f, 3f, 34f, 0f, 0f, "From the loggia rail: the whole courtyard (Husk, crossbow, the pours), the belfry beyond, the spire in the upper third."),
            Vista('E', "Vista_V3", 88f, 138f, T1, 8f, 4f, 20f, 0f, 0f, "Framing north: the terrace, the breach, the Fool, the NE tower, the spire. The Fool waits for the player to leave this volume."),
            Vista('E', "Vista_V3b", 89f, 161.5f, T1 + 5f, 5f, 4f, 10f, 0f, -2f, "Secret (D2), on the NE tower roof: the throne hall's roofs and the spire; across the ravine, the foreign siege camp."),
            new Volume { Stage = 'E', Id = "M07", Name = "Vista_VZ", Centre = new Vector3(50f, T1, 152f), Size = new Vector3(4f, 4f, 3f), PanTarget = new Vector3(66f, T1, 121f), PanDuration = 2f,
                Note = "Not entered: fired when the postern is unbarred. A 2 s pan south along the west wall-walk to Shrine A's light, then back." },
            Vista('F', "Vista_V4", 58f, 202.5f, 0f, 10f, 5f, 14f, 0f, -4f, "From the cloister: the gate, the brazier-lit doors through it, the spire. E6a's enemies all in frame."),
            new Volume { Stage = 'F', Id = "M07", Name = "Vista_V5", Centre = new Vector3(54f, T1, 263.5f), Size = new Vector3(16f, 4f, 2f), Threshold = true, Pitch = 12f, Fov = 50f, PullBack = 6f,
                Note = "Shrine B: the doors filling the upper third, the spire above them." },
            V('D', "M08", "Arena_E1", 62f, -5.5f, 0f, 16f, 27f, 8f),
            V('D', "M08", "Arena_O1", 50f, 22f, 0f, 28f, 24f, 8f),
            V('D', "M08", "Arena_O2", 69f, 53f, 0f, 26f, 22f, 8f),
            V('D', "M08", "Arena_E2", 50f, 97f, 0f, 28f, 22f, 8f),
            V('D', "M08", "Arena_E3", 66f, 139f, 0f, 28f, 30f, 8f),
            V('E', "M08", "Arena_E4", 86.5f, 139f, T1, 13f, 30f, 6f),
            V('F', "M08", "Arena_E5", 78f, 182f, 0f, 24f, 28f, 8f),
            V('F', "M08", "Arena_E6a", 54f, 216f, 0f, 28f, 20f, 8f),
            V('F', "M08", "Arena_E6b", 54f, 233f, 0f, 8f, 14f, 8f),
            V('F', "M08", "Arena_E6c", 54f, 247.5f, 0f, 24f, 15f, 8f),
            V('E', "M11", "RoofHider_NE", 88f, 159f, T1, 9f, 9f, 5f),
            V('E', "M11", "RoofHider_NW", 52.5f, 159f, T1, 9f, 9f, 5f),
        };

        // ---------------------------------------------------------------- NavMesh rules (15.4)

        public class NavRule
        {
            public char Stage;
            public string Name, Agent;
            public Vector3 Centre, Size;
        }

        public class NavLink
        {
            public char Stage;
            public string Name, Agent;
            public Vector3 Start, End;
            public float Width;
        }

        // The Perch agent (crossbowmen) is baked only inside these boxes, so its mesh is its leash.
        public static readonly List<NavRule> Perches = new List<NavRule>
        {
            new NavRule { Stage = 'D', Name = "Perch_Gallows", Agent = "Perch", Centre = new Vector3(75f, 2.5f, 142f), Size = new Vector3(10f, 1.6f, 8f) },
            new NavRule { Stage = 'F', Name = "Perch_E6aWalk", Agent = "Perch", Centre = new Vector3(44f, 5.5f, 233f), Size = new Vector3(8f, 2f, 14f) },
        };

        // Not Walkable for one agent only: E3's Husk stops at the courtyard stairs' feet; the hound keeps off the
        // grand stair; the kennel sills, so a hound's way out is the one-way link and it never goes back in.
        public static readonly List<NavRule> Barred = new List<NavRule>
        {
            new NavRule { Stage = 'D', Name = "Husk_ShrineStair", Agent = "Husk", Centre = new Vector3(64f, 2.5f, 128.05f), Size = new Vector3(4.2f, 6f, 7.5f) },
            new NavRule { Stage = 'D', Name = "Husk_RampartStair", Agent = "Husk", Centre = new Vector3(74.25f, 2.5f, 126f), Size = new Vector3(7.5f, 6f, 4.2f) },
            new NavRule { Stage = 'F', Name = "Hound_GrandStair", Agent = "Hound", Centre = new Vector3(54f, 3f, 258.9f), Size = new Vector3(16.4f, 8f, 7.8f) },
            new NavRule { Stage = 'F', Name = "Hound_KennelSill_W", Agent = "Hound", Centre = new Vector3(42f, 0.5f, 250f), Size = new Vector3(0.8f, 3f, 3.6f) },
            new NavRule { Stage = 'F', Name = "Hound_KennelSill_E", Agent = "Hound", Centre = new Vector3(66f, 0.5f, 250f), Size = new Vector3(0.8f, 3f, 3.6f) },
        };

        public static readonly List<NavLink> NavLinks = new List<NavLink>
        {
            new NavLink { Stage = 'F', Name = "KennelOut_W", Agent = "Hound", Start = new Vector3(40.8f, 0f, 250f), End = new Vector3(43.4f, 0f, 250f), Width = 2f },
            new NavLink { Stage = 'F', Name = "KennelOut_E", Agent = "Hound", Start = new Vector3(67.2f, 0f, 250f), End = new Vector3(64.6f, 0f, 250f), Width = 2f },
        };
    }
}
