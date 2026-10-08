using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Single source of truth for the shape of L2 "The Hollow Village".
    // Camera yaw is 0, so world north (+Z) is screen-up and the camera side of anything is its south.
    // Walkable space is a union of capsules (a circle is a capsule with A == B). Each capsule carries a height
    // at both ends, and the terrain takes the height of the nearest walkable piece, so pieces at different
    // heights that don't touch meet in a cliff (the retaining walls and the churchyard's edge above E4).
    public static class L2Layout
    {
        public struct Walk
        {
            public string Name;
            public Vector2 A;
            public Vector2 B;
            public float HalfWidth;
            public float HA;
            public float HB;

            // Height the ground climbs back to outside this piece (a sunken road has banks up to 0); NaN keeps its own height.
            public float Outer;

            public Walk(string name, Vector2 a, Vector2 b, float halfWidth, float ha, float hb, float outer)
            {
                Name = name;
                A = a;
                B = b;
                HalfWidth = halfWidth;
                HA = ha;
                HB = hb;
                Outer = outer;
            }
        }

        public struct Node
        {
            public string Name;
            public Vector2 P;
            public float H;
            public float HalfWidth;

            // No walkable piece joins this node to the next one (a gap in the route; none at present).
            public bool Break;

            public Node(string name, float x, float z, float h, float halfWidth, bool breakAfter = false)
            {
                Name = name;
                P = new Vector2(x, z);
                H = h;
                HalfWidth = halfWidth;
                Break = breakAfter;
            }
        }

        public struct Area
        {
            public string Name;
            public Vector2 Center;
            public float Radius;
            public float H;
            public bool IsArena;

            public Area(string name, float x, float z, float radius, float h, bool isArena = false)
            {
                Name = name;
                Center = new Vector2(x, z);
                Radius = radius;
                H = h;
                IsArena = isArena;
            }
        }

        // A solid route blocker. Near and Far are probes on either side, used to prove it blocks.
        public struct Blocker
        {
            public string Name;
            public Vector2 Center;
            public Vector2 Size;
            public float Yaw;
            public bool Fire;
            public Vector2 Near;
            public Vector2 Far;

            public Blocker(string name, float x, float z, float width, float depth, float yaw, bool fire, Vector2 near, Vector2 far)
            {
                Name = name;
                Center = new Vector2(x, z);
                Size = new Vector2(width, depth);
                Yaw = yaw;
                Fire = fire;
                Near = near;
                Far = far;
            }
        }

        // A passable damage-over-time fire bed. The trigger and the visual share these bounds exactly.
        public struct FireZone
        {
            public string Name;
            public Vector2 Center;
            public Vector2 Size;
            public float Yaw;

            public FireZone(string name, float x, float z, float width, float depth, float yaw = 0f)
            {
                Name = name;
                Center = new Vector2(x, z);
                Size = new Vector2(width, depth);
                Yaw = yaw;
            }
        }

        // A building plot: Kind decides what stands there (see L2Greybox / L2Dressing).
        // Tall: roofless shells and half-collapsed houses (far side of the path). Low: wall stubs, lone chimneys
        // (camera side). Burning: a burning house (a fire behind an encounter). BurningLow: a collapsed, burning
        // front you can see over but not cross. Church: the landmark. Backdrop: intact distant silhouettes.
        public enum LotKind { Tall, Low, Burning, BurningLow, Church, Backdrop }

        public struct Lot
        {
            public string Name;
            public Vector2 Center;
            public Vector2 Size;
            public float Yaw;
            public LotKind Kind;

            public Lot(string name, float x, float z, float width, float depth, float yaw, LotKind kind)
            {
                Name = name;
                Center = new Vector2(x, z);
                Size = new Vector2(width, depth);
                Yaw = yaw;
                Kind = kind;
            }
        }

        // ---------------------------------------------------------------- Critical path

        // Right (east) through Village 1, up (north) the road, left (west) up to the church and round it,
        // down the churchyard stair into E4, through the E4 gate up to the crossroads, right through Village 3,
        // through the E5 gate and up through E5, out past the first fields to the exit.
        public static readonly Node[] Route =
        {
            new Node("Start", -96f, -57f, 0f, 2.0f),
            new Node(null, -88f, -60f, 0f, 2.0f),
            new Node(null, -80f, -56f, 0f, 2.0f),
            new Node(null, -72f, -62f, 0f, 1.9f),
            new Node(null, -63f, -56f, 0f, 2.0f),
            new Node(null, -54f, -61f, 0f, 2.0f),
            new Node(null, -45f, -57f, 0f, 2.2f),
            new Node("E1", -32f, -57f, 0f, 2.5f),
            new Node(null, -24f, -54f, 0f, 2.0f),
            new Node(null, -17f, -51f, 0f, 2.0f),
            new Node(null, -9f, -48f, 0f, 2.2f),
            new Node("WellSquare", 0f, -44f, 0f, 2.5f),
            new Node(null, 9f, -43f, 0f, 2.4f),
            new Node(null, 15f, -45f, 0f, 2.4f),
            new Node(null, 21f, -40f, 0f, 2.6f),
            new Node("E2", 27f, -35f, 0f, 2.5f),
            new Node(null, 33f, -31f, 0f, 2.5f),
            new Node(null, 41f, -34f, -1.5f, 2.5f),
            new Node(null, 49f, -30f, -1.5f, 2.5f),
            new Node(null, 54f, -23f, -1.5f, 2.5f),
            new Node(null, 52f, -17f, 0f, 2.5f),
            new Node("Bridge", 48f, -12f, 0f, 2.0f),
            new Node(null, 45f, -7f, 0f, 2.5f),
            new Node(null, 40f, -3f, 0f, 3f),
            new Node("E3_Bottom", 35f, -3f, 0f, 4.6f),
            new Node("E3_Top", 21f, -5f, 4f, 4.6f),
            new Node(null, 17f, -15f, 4f, 2.2f),
            new Node(null, 8f, -22f, 4f, 2.5f),
            new Node(null, -4f, -25f, 4f, 2.5f),
            new Node(null, -14f, -21f, 4f, 2.5f),
            new Node(null, -26f, -24f, 4f, 2.5f),
            new Node(null, -31f, -14f, 4f, 2.3f),
            new Node(null, -31f, -2f, 4f, 2.3f),
            new Node("StairTop", -26f, 2.6f, 4f, 1.8f),
            new Node("StairFoot", -25.6f, 9.2f, 1f, 1.8f),
            new Node("E4", -24f, 17f, 1f, 2.5f),
            new Node(null, -26f, 25f, 1f, 2f),
            new Node(null, -27f, 32f, 1f, 2f),
            new Node(null, -28f, 38f, 1f, 2f),
            new Node(null, -18f, 37f, 1f, 2.2f),
            new Node(null, -10f, 39f, 1f, 2.4f),
            new Node("Crossroads", -4f, 42f, 1f, 2.5f),
            new Node(null, 3f, 42f, 1f, 2.2f),
            new Node(null, 11f, 40f, 1f, 2.3f),
            new Node(null, 21f, 40f, 1f, 2.5f),
            new Node(null, 31f, 39f, 1f, 2.5f),
            new Node(null, 39f, 44f, 1f, 2.3f),
            new Node(null, 45f, 50f, 1f, 2.3f),
            new Node(null, 42f, 58f, 1f, 2.3f),
            new Node(null, 38f, 68f, 1f, 2.3f),
            new Node(null, 28f, 72f, 1f, 2.5f),
            new Node(null, 17f, 71f, 1f, 2.5f),
            new Node("V3_Plaza", 6f, 74f, 1f, 2.5f),
            new Node(null, 6f, 80f, 1.2f, 2.5f),
            new Node(null, 8f, 86f, 2.5f, 2.5f),
            new Node("E5", 10f, 95f, 2.5f, 2.5f),
            new Node(null, 12f, 104f, 2.5f, 2.5f),
            new Node(null, 5f, 113f, 2.5f, 2.5f),
            new Node(null, 9f, 123f, 2.5f, 2.5f),
            new Node(null, 3f, 133f, 2.5f, 2.5f),
            new Node("Exit", 8f, 143f, 2.5f, 2.5f),
        };

        // Side streets, pockets and dead ends (each a polyline of nodes).
        public static readonly Dictionary<string, Node[]> Branches = new Dictionary<string, Node[]>
        {
            // Well square exit b: the heal yard behind a collapsed lean-to.
            ["HealYard"] = new[] { new Node(null, 7f, -50f, 0f, 1.8f), new Node(null, 11f, -55f, 0f, 1.8f) },
            // Well square exit a: stairs up to the graveyard, blocked by burning debris at the foot.
            ["GraveyardStair"] = new[] { new Node(null, 2f, -35f, 0f, 2f), new Node(null, 3f, -31f, 1.8f, 2f), new Node(null, 4f, -26.5f, 4f, 2f) },
            // The top of the church slope: the archer's perch and the church street both open off it.
            ["SlopeTop"] = new[] { new Node(null, 21f, -4.5f, 4f, 2.2f), new Node(null, 16.5f, 0f, 4f, 2.2f) },
            // The direct street from the church front down to E4, blocked by fire near the top.
            ["FireStreet"] = new[] { new Node(null, 17f, 1f, 4f, 2f), new Node(null, 12f, 8f, 4f, 2f), new Node(null, 2f, 13f, 1f, 2.2f), new Node(null, -16f, 16f, 1f, 2.2f) },
            // Crossroads west arm (a road to elsewhere, blocked by debris) and north arm (blocked by fire).
            ["CrossroadsWest"] = new[] { new Node(null, -9f, 46f, 1f, 2.2f), new Node(null, -19f, 50f, 1f, 2.2f) },
            ["CrossroadsNorth"] = new[] { new Node(null, -3f, 48f, 1f, 2.2f), new Node(null, -4f, 58f, 1f, 2.2f) },
            // The dead-end alley inside the burning block; its only way in is the fire gap off the north street.
            ["WeaponAlley"] = new[] { new Node(null, 25f, 71f, 1f, 1.5f), new Node(null, 25f, 55f, 1f, 1.5f) },
            // Rubble ramps up to the E5 archer perches.
            ["PerchRampW"] = new[] { new Node(null, 2f, 92f, 2.5f, 1.6f), new Node(null, -1f, 97f, 4f, 1.6f) },
            ["PerchRampE"] = new[] { new Node(null, 18f, 92f, 2.5f, 1.6f), new Node(null, 21f, 97f, 4f, 1.6f) },
        };

        public static readonly Area[] Areas =
        {
            new Area("Start", -96f, -57f, 4.5f, 0f),
            new Area("E1", -32f, -57f, 7f, 0f, true),
            new Area("WellSquare", 0f, -44f, 9f, 0f),
            new Area("HealYard", 13f, -57f, 3.2f, 0f),
            new Area("E2", 27f, -35f, 6.5f, 0f, true),
            new Area("V2_Doorframe", 39f, 1.5f, 2.5f, 0f),
            new Area("E3_Perch", 15f, 1f, 2f, 4f),
            new Area("E4", -24f, 17f, 8.5f, 1f, true),
            new Area("Crossroads", -4f, 42f, 6f, 1f),
            new Area("V3_Plaza", 6f, 74f, 5f, 1f),
            new Area("E5", 10f, 95f, 9f, 2.5f, true),
            new Area("E5_PerchW", -1f, 97f, 2f, 4f),
            new Area("E5_PerchE", 21f, 97f, 2f, 4f),
            new Area("Exit", 8f, 143f, 4f, 2.5f),
        };

        // E3 is fought on the slope; its arena is a height-less circle over the slope capsule.
        public static readonly Area E3Arena = new Area("E3", 27f, -4f, 4.8f, 2f, true);

        public static IEnumerable<Area> Arenas
        {
            get
            {
                foreach (var a in Areas)
                {
                    if (a.IsArena)
                    {
                        yield return a;
                    }
                }

                yield return E3Arena;
            }
        }

        // ---------------------------------------------------------------- Fire and blockers

        public static readonly Blocker[] Blockers =
        {
            new Blocker("FB_GraveyardStair", 2.6f, -33f, 5.2f, 2.2f, 14f, true, new Vector2(1.5f, -37f), new Vector2(3.6f, -29f)),
            new Blocker("FB_ChurchStreet", 14.2f, 4.6f, 5.2f, 2.4f, -35f, true, new Vector2(16.5f, -1f), new Vector2(9f, 9.8f)),
            new Blocker("DB_CrossroadsWest", -16f, 48.8f, 5.4f, 2.2f, -22f, false, new Vector2(-13.4f, 47.9f), new Vector2(-18.6f, 50.2f)),
            new Blocker("FB_CrossroadsNorth", -3.4f, 50.2f, 5.4f, 2.2f, 5f, true, new Vector2(-3f, 46.5f), new Vector2(-4f, 55f)),
        };

        // Burning debris just outside the far (north) rim of a fight, outside the walkable space: purely the
        // backdrop enemies stand against. The camera frames ~9 m up-screen, so the glow has to sit on the rim.
        public static readonly FireZone[] BackFires =
        {
            new FireZone("BF_E2", 24.5f, -27.4f, 4.5f, 1.4f, -15f),
            new FireZone("BF_E4", -19.5f, 25.6f, 4.2f, 1.4f, 20f),
            new FireZone("BF_E5_W", 3.2f, 104.2f, 4f, 1.4f, 10f),
            new FireZone("BF_E5_E", 18.2f, 104f, 4f, 1.4f, -12f),
        };

        public static readonly FireZone[] FireZones =
        {
            new FireZone("DZ_E1_BurningHouse", -32f, -50.6f, 8f, 1.4f),
            new FireZone("DZ_WeaponGap", 25f, 66f, 3.2f, 2.6f),
            new FireZone("DZ_V3_SouthStreet", 24f, 42f, 10f, 1.1f),
            new FireZone("DZ_E5_Stall", 17.6f, 89.8f, 2.4f, 2f, 20f),
        };

        // ---------------------------------------------------------------- Landmarks and lots

        // The church stands on the plateau (+4): nave east-west, front door east, tower at the south-east corner.
        public static readonly Rect ChurchNave = new Rect(-13.4f, -11.2f, 26.4f, 6.4f);
        public static readonly Vector2 ChurchTower = new Vector2(10.5f, -13.5f);
        public const float PlateauHeight = 4f;

        // Gatehouses across the route: an arched gate, round towers either side, the wall running off both ways
        // to the houses. Each is shut; its lever stands beside the arch on the near side, and pulling it is the
        // way on. Both stand across streets running north, so the camera sees them face-on: the E4 gate on the
        // street out of that fight, the E5 gate on the street from the Village 3 plaza up to E5.
        public struct Gate
        {
            public string Name;
            public Vector2 Center;
            public float Yaw;          // the heading through the gate (the way on)
            public float WallLength;   // each side of the arch

            public Gate(string name, Vector2 center, Vector2 heading, float wallLength)
            {
                Name = name;
                Center = center;
                Yaw = Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
                WallLength = wallLength;
            }

            // The passage direction (the way on) and the wall direction, in the ground plane.
            public Vector2 Forward => new Vector2(Mathf.Sin(Yaw * Mathf.Deg2Rad), Mathf.Cos(Yaw * Mathf.Deg2Rad));
            public Vector2 Along => new Vector2(Mathf.Cos(Yaw * Mathf.Deg2Rad), -Mathf.Sin(Yaw * Mathf.Deg2Rad));

            // Beside the arch on the near side, where the player arrives.
            public Vector2 Lever => Center - Forward * 1.5f + Along * 1.5f;

            // Where the player stands in front of the gate (the gameplay marker).
            public Vector2 Front => Center - Forward * 1.6f;
        }

        public static readonly Gate[] Gates =
        {
            new Gate("Gate_E4", new Vector2(-26.5f, 28.5f), new Vector2(-1f, 7f), 5.6f),
            new Gate("Gate_E5", new Vector2(6f, 78.8f), new Vector2(0f, 1f), 7f),   // on the flat, before the street climbs to E5
        };

        public const float GateArchWidth = 2.1f;

        public static bool InGateWall(Vector2 p, float margin)
        {
            foreach (var g in Gates)
            {
                Vector2 d = p - g.Center;
                if (Mathf.Abs(Vector2.Dot(d, g.Along)) < g.WallLength + 1.2f + margin && Mathf.Abs(Vector2.Dot(d, g.Forward)) < 1.6f + margin)
                {
                    return true;
                }
            }

            return false;
        }

        // The weapon block: a burning house ring around the dead-end alley.
        public static readonly Rect BurningBlock = new Rect(16f, 46f, 18f, 18f);

        // Hand-placed lots; L2Greybox fills the rest of each street edge automatically.
        public static readonly Lot[] KeyLots =
        {
            new Lot("Burning_E1", -32f, -46f, 8f, 6f, 0f, LotKind.Burning),
            new Lot("Burning_E2", 24f, -19f, 8f, 6f, 10f, LotKind.Burning),
            new Lot("Burning_Church", 4f, 3f, 8f, 6f, 0f, LotKind.Burning),
            new Lot("Burning_E4", -16f, 30f, 8f, 5.5f, 0f, LotKind.Burning),
            new Lot("Burning_E5_W", -3f, 106f, 8f, 6f, 5f, LotKind.Burning),
            new Lot("Burning_E5_E", 23f, 106f, 8f, 6f, -5f, LotKind.Burning),
            new Lot("Church", -0.2f, -8f, 26.4f, 6.4f, 0f, LotKind.Church),
            // The weapon block: two burning wings either side of the dead-end alley, and a collapsed burning
            // south front the weapon is first seen over.
            new Lot("Block_W", 19.5f, 55f, 7f, 18f, 0f, LotKind.Burning),
            new Lot("Block_E", 30.5f, 55f, 7f, 18f, 0f, LotKind.Burning),
            new Lot("Block_S", 25f, 49.8f, 4f, 7f, 0f, LotKind.BurningLow),
            // The house with the scorched doorframe (Kael's recognition), its door facing the Village 2 entrance.
            new Lot("Doorframe", 40f, 8.6f, 5f, 6.4f, 0f, LotKind.Tall),
        };

        public static Lot DoorframeLot => KeyLots[KeyLots.Length - 1];

        // L1_CheckpointShrine faces its local -Z.
        public const float ShrineYawOffset = 180f;

        // Hard cover for the two archer fights, staggered 3-6 m apart with open ground between, all under 1.3 m.
        // E3: the player climbs the slope west toward the archer on the churchyard edge.
        // E5: archers stand on terraces on the west and east sides of the market.
        public static readonly (string key, Vector2 p, float yaw, float scale)[] CoverPieces =
        {
            ("K/Props/SM_Prop_Cart_01", new Vector2(30.5f, -5.8f), 80f, 1f),
            ("A/Buildings/SM_Bld_Wall_01", new Vector2(29f, -0.6f), 100f, 0.8f),
            ("K/Environments/SM_Env_RockPile_01", new Vector2(25f, -3f), 30f, 0.26f),
            ("A/Props/SM_Prop_Stall_Table_01", new Vector2(24f, -7.4f), 75f, 1f),
            ("A/Props/SM_Prop_Cart_01", new Vector2(19.5f, -6.8f), 70f, 1f),
            ("A/Buildings/SM_Bld_Wall_01", new Vector2(19.5f, -1.8f), 95f, 0.8f),
            ("A/Buildings/SM_Bld_Wall_01", new Vector2(5f, 92f), 90f, 0.8f),
            ("A/Buildings/SM_Bld_Wall_01", new Vector2(15f, 92f), 90f, 0.8f),
            ("A/Props/SM_Prop_Cart_01", new Vector2(4.5f, 98.5f), 10f, 1f),
            ("K/Environments/SM_Env_RockPile_03", new Vector2(15.5f, 99f), 60f, 0.24f),
            ("A/Props/SM_Prop_Stall_Table_01", new Vector2(10f, 101f), 0f, 1f),
        };

        // Gameplay marker positions (ground height is resolved at build time).
        public static readonly Dictionary<string, Vector2> Markers = new Dictionary<string, Vector2>
        {
            ["PlayerStart"] = new Vector2(-95f, -57f),
            ["Lore_Well"] = new Vector2(-6.5f, -39.5f),
            ["Well"] = new Vector2(-4f, -40.5f),
            ["Loot_Heal"] = new Vector2(14f, -58f),
            ["AshUnlock"] = new Vector2(48f, -12f),
            ["Lore_ScorchedDoorframe"] = new Vector2(39.8f, 3.2f),
            ["E3_ArcherPerch_1"] = new Vector2(15f, 1f),
            ["Checkpoint_Shrine"] = new Vector2(-10.3f, 46.4f),
            ["RespawnPoint"] = new Vector2(-6f, 43.5f),
            ["Gate_E4"] = Gates[0].Front,
            ["Gate_E5"] = Gates[1].Front,
            ["Loot_WeaponUpgrade"] = new Vector2(25f, 55.5f),
            ["E5_ArcherPerch_1"] = new Vector2(-1f, 97.5f),
            ["E5_ArcherPerch_2"] = new Vector2(21f, 97.5f),
            ["Exit_ToL3"] = new Vector2(8f, 143f),
        };

        // Enemy spawns per encounter: far side of each space (north), so they stand against the fires.
        public static readonly Dictionary<string, Vector2[]> Spawns = new Dictionary<string, Vector2[]>
        {
            ["E1"] = new[] { new Vector2(-32f, -53f) },
            ["E2"] = new[] { new Vector2(28f, -31.5f) },
            ["E3"] = new[] { new Vector2(22f, -1f) },
            ["E4"] = new[] { new Vector2(-27f, 21.5f), new Vector2(-20f, 21.5f) },
            ["E5"] = new[] { new Vector2(10f, 99f) },
        };

        // The stair from the churchyard down into E4: a straight flight between the StairTop and StairFoot
        // route nodes (the route's own ramp; the steps are dressing on it).
        public static readonly Vector2 StairTop = new Vector2(-26f, 2.6f);     // = the StairTop route node
        public static readonly Vector2 StairFoot = new Vector2(-25.6f, 9.2f);  // = the StairFoot route node
        public const float StairTopHeight = 4f, StairFootHeight = 1f;
        public const float StairHalfWidth = 1.8f;

        public static bool InStair(Vector2 p, float margin)
        {
            return DistanceToSegment(p, StairTop, StairFoot, out _) < StairHalfWidth + margin;
        }

        // A spot on the churchyard path above the stair, used to prove the stair works both ways.
        public static readonly Vector2 StairTopProbe = new Vector2(-31f, -4f);

        // The checkpoint shrine stands just behind its marker, facing the respawn point.
        public static Vector2 ShrinePosition
        {
            get
            {
                Vector2 face = (Markers["RespawnPoint"] - Markers["Checkpoint_Shrine"]).normalized;
                return Markers["Checkpoint_Shrine"] - face * 1.8f;
            }
        }

        // The level's end leaves the village for the first fields of the farmland (L3): no houses north of here,
        // fences and field walls along the road, ploughed fields either side.
        public const float FarmlandStart = 117f;

        public static bool InFarmland(Vector2 p)
        {
            return p.y > FarmlandStart;
        }

        // How far the village has given way to the country at p: 0 in the streets, 1 in the farmland. The band
        // runs from just past E5 to the first fields, its edge wandering so nothing ends on a straight line.
        public static float Rural(Vector2 p)
        {
            return IsInArena(p, 3f) ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(105f, 124f, p.y + RuralWander(p)));
        }

        public static float RuralWander(Vector2 p)
        {
            return (Mathf.PerlinNoise(p.x * 0.07f + 17f, p.y * 0.07f + 3f) - 0.5f) * 10f + (Mathf.PerlinNoise(p.x * 0.3f + 5f, p.y * 0.3f + 9f) - 0.5f) * 3f;
        }

        // ---------------------------------------------------------------- Ground extents

        public const float GroundMinX = -130f;
        public const float GroundMinZ = -100f;
        public const float GroundWidth = 230f;
        public const float GroundLength = 285f;
        public const float GroundCell = 0.5f;

        // ---------------------------------------------------------------- Camera (Assets/Kneel/Settings/L2/L2_PlayerCameraSettings.asset)

        public const float CameraPitch = 65f;
        public const float CameraYaw = 0f;
        public const float CameraDistance = 8.5f;
        public const float CameraMinDistance = 3.2f;
        public const float CameraMaxDistance = 8.5f;
        public const float CameraTargetHeight = 1f;
        public const float CameraMaxEdgeOffset = 10f;

        public static Vector2 CameraForward
        {
            get
            {
                Vector3 f = Quaternion.Euler(0f, CameraYaw, 0f) * Vector3.forward;
                return new Vector2(f.x, f.z).normalized;
            }
        }

        public static Pose CameraPose(Vector3 feet, float distance, Vector3 edgeOffset = default)
        {
            Quaternion rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            Vector3 look = feet + Vector3.up * CameraTargetHeight + edgeOffset;
            return new Pose(look - rotation * Vector3.forward * distance, rotation);
        }

        // ---------------------------------------------------------------- Walkable space

        private static List<Walk> walks;

        public static List<Walk> Walks
        {
            get
            {
                if (walks == null)
                {
                    walks = BuildWalks();
                }

                return walks;
            }
        }

        private static List<Walk> BuildWalks()
        {
            var list = new List<Walk>();
            AddPolyline(list, "Route", Route);
            foreach (var kv in Branches)
            {
                AddPolyline(list, kv.Key, kv.Value);
            }

            foreach (var a in Areas)
            {
                list.Add(new Walk(a.Name, a.Center, a.Center, a.Radius, a.H, a.H, float.NaN));
            }

            return list;
        }

        private static void AddPolyline(List<Walk> list, string name, Node[] nodes)
        {
            for (int i = 1; i < nodes.Length; i++)
            {
                var a = nodes[i - 1];
                var b = nodes[i];
                if (a.Break)
                {
                    continue;
                }

                // Sunken stretches climb back to street level (0) outside the road: those are the banks.
                float outer = Mathf.Min(a.H, b.H) < 0f ? 0f : float.NaN;
                list.Add(new Walk(name + "_" + i, a.P, b.P, Mathf.Min(a.HalfWidth, b.HalfWidth), a.H, b.H, outer));
            }
        }

        public static float SignedDistance(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (var w in Walks)
            {
                best = Mathf.Min(best, DistanceToSegment(p, w.A, w.B, out _) - w.HalfWidth);
            }

            return best;
        }

        public static float SignedDistance(Vector3 worldPosition)
        {
            return SignedDistance(new Vector2(worldPosition.x, worldPosition.z));
        }

        // Height of the walkable piece nearest to p, at p. Outside the path it eases toward that piece's outer height.
        // On the church stair the flight itself decides: one even slope across its whole width (where it overlaps
        // the churchyard path's end, the nearest-piece rule would leave a step).
        public static float Height(Vector2 p)
        {
            float stair = DistanceToSegment(p, StairTop, StairFoot, out float along);
            if (stair < StairHalfWidth)
            {
                return Mathf.Lerp(StairTopHeight, StairFootHeight, along);
            }

            float best = float.MaxValue;
            float height = 0f;
            float outer = 0f;
            foreach (var w in Walks)
            {
                float d = DistanceToSegment(p, w.A, w.B, out float t) - w.HalfWidth;
                if (d < best)
                {
                    best = d;
                    height = Mathf.Lerp(w.HA, w.HB, t);
                    outer = w.Outer;
                }
            }

            if (best > 0f && !float.IsNaN(outer))
            {
                height = Mathf.Lerp(height, outer, Mathf.SmoothStep(0f, 1f, best / 3.5f));
            }

            return height;
        }

        // ---------------------------------------------------------------- Terrain extras

        // The creek: it springs from a culvert at the foot of the churchyard plateau, runs east past the hill,
        // crosses under the road bridge at right angles and winds off east into the hills. Its water falls
        // gently downstream; the road crosses on a flush timber deck (L2Creek), so the bed is carved under it too.
        public static readonly Vector2[] Creek =
        {
            new Vector2(25.5f, -12.4f), new Vector2(31f, -11.6f), new Vector2(36.5f, -11.8f), new Vector2(41f, -14.4f),
            new Vector2(44.5f, -14.8f), new Vector2(48f, -12.3f), new Vector2(52f, -9.4f), new Vector2(58f, -9.2f),
            new Vector2(66f, -11.5f), new Vector2(78f, -13.5f), new Vector2(96f, -14f),
        };

        public const float CreekHalfWidth = 2.2f;   // bank top to bank top
        public const float WaterHalfWidth = 1.3f;

        // The bridge carries the road (Route[20] -> Route[22]) over the creek.
        public static readonly Vector2 BridgeCenter = new Vector2(48f, -12.2f);
        public const float BridgeLength = 8.6f;
        public const float BridgeWidth = 5.4f;

        public static Vector2 BridgeDirection => (Route[22].P - Route[20].P).normalized;

        public static bool OnBridge(Vector2 p, float margin)
        {
            Vector2 d = p - BridgeCenter;
            Vector2 along = BridgeDirection;
            Vector2 across = new Vector2(-along.y, along.x);
            return Mathf.Abs(Vector2.Dot(d, along)) < BridgeLength * 0.5f + margin && Mathf.Abs(Vector2.Dot(d, across)) < BridgeWidth * 0.5f + margin;
        }

        private static float creekLength = -1f;

        // Distance from p to the creek's centre line, and how far along the creek (0 at the spring, 1 at the end).
        public static float CreekDistance(Vector2 p, out float along)
        {
            if (creekLength < 0f)
            {
                creekLength = 0f;
                for (int i = 1; i < Creek.Length; i++)
                {
                    creekLength += (Creek[i] - Creek[i - 1]).magnitude;
                }
            }

            float best = float.MaxValue, travelled = 0f;
            along = 0f;
            for (int i = 1; i < Creek.Length; i++)
            {
                float length = (Creek[i] - Creek[i - 1]).magnitude;
                float d = DistanceToSegment(p, Creek[i - 1], Creek[i], out float t);
                if (d < best)
                {
                    best = d;
                    along = (travelled + t * length) / creekLength;
                }

                travelled += length;
            }

            return best;
        }

        public static bool InCreek(Vector2 p, float margin)
        {
            return CreekDistance(p, out _) < CreekHalfWidth + margin;
        }

        // The water surface: a little below the banks at the spring, falling past the bridge and on east.
        public static float WaterLevel(float along)
        {
            return along < 0.25f ? Mathf.Lerp(-0.5f, -1.25f, along / 0.25f) : Mathf.Lerp(-1.25f, -2.4f, (along - 0.25f) / 0.75f);
        }

        public static readonly Vector2 Hill = new Vector2(37f, -19.5f);
        public const float HillRadius = 6.5f;
        public const float HillHeight = 3.2f;

        // Ground mesh height: the walkable height field plus the creek, the hill and the far hills that hide the edges.
        public static float TerrainHeight(Vector2 p)
        {
            float h = Height(p);
            float sd = SignedDistance(p);

            // Cliffs belong next to the paths (retaining walls, the ledge). Further out the level blends between
            // neighbouring heights, so no step runs off across the countryside.
            if (sd > 3f)
            {
                float sum = 0f, weights = 0f;
                foreach (var w in Walks)
                {
                    float d = DistanceToSegment(p, w.A, w.B, out float t) - w.HalfWidth;
                    float weight = Mathf.Exp(-(d - sd) / 5f);
                    float wh = Mathf.Lerp(w.HA, w.HB, t);
                    if (!float.IsNaN(w.Outer))
                    {
                        wh = w.Outer;
                    }

                    sum += wh * weight;
                    weights += weight;
                }

                h = Mathf.Lerp(h, sum / weights, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 11f, sd)));
            }

            float hill = (p - Hill).magnitude;
            if (hill < HillRadius && sd > 0.5f)
            {
                float k = 1f - (hill / HillRadius) * (hill / HillRadius);
                h += HillHeight * k * Mathf.Clamp01((sd - 0.5f) / 2f);
            }

            if (sd > 16f)
            {
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 48f, sd));
                h += rise * (2.5f + 6f * Mathf.PerlinNoise(p.x * 0.025f + 40f, p.y * 0.025f + 11f));
            }

            // The creek bed (last, so the far hills never lift it above its water): flat under the water, banks
            // rising to the ground around. It never cuts a street, except under the bridge, where the deck carries
            // the road across.
            float creek = CreekDistance(p, out float flow);
            if (creek < CreekHalfWidth + 1.5f)
            {
                float bed = WaterLevel(flow) - 0.6f;
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WaterHalfWidth, CreekHalfWidth + 1.5f, creek));
                float keep = OnBridge(p, 0.6f) ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1.2f, sd));
                h = Mathf.Lerp(h, Mathf.Min(h, bed), k * keep);
            }

            return h;
        }

        public static bool IsInArena(Vector2 p, float padding)
        {
            foreach (var a in Arenas)
            {
                if ((p - a.Center).magnitude < a.Radius + padding)
                {
                    return true;
                }
            }

            return false;
        }

        // Tallest a piece may be at p without hiding the player: anything south of walkable ground (toward
        // the camera) must stay under the camera-to-chest line, and the camera-side rims of arenas stay low.
        public static float MaxPropHeight(Vector2 p)
        {
            Vector2 forward = CameraForward;
            foreach (var arena in Arenas)
            {
                Vector2 offset = p - arena.Center;
                if (offset.magnitude < arena.Radius + 7f && Vector2.Dot(offset, forward) < -arena.Radius * 0.2f)
                {
                    return 1.2f;
                }
            }

            float slope = Mathf.Tan(CameraPitch * Mathf.Deg2Rad);
            float here = Height(p);
            for (float d = 0.5f; d <= 14f; d += 0.5f)
            {
                Vector2 q = p + forward * d;
                if (SignedDistance(q) < 0f)
                {
                    // Relative to this spot's own ground: walkable ground higher than p lifts the cap.
                    return Height(q) - here + CameraTargetHeight + slope * d;
                }
            }

            return float.MaxValue;
        }

        // Height cap that still applies once tall scenery fades out of the camera's way (OcclusionFade): the
        // camera-side rims of the fights stay low, so nothing ever hides an enemy's telegraph.
        public static float ArenaCap(Vector2 p)
        {
            Vector2 forward = CameraForward;
            foreach (var arena in Arenas)
            {
                Vector2 offset = p - arena.Center;
                if (offset.magnitude < arena.Radius + 7f && Vector2.Dot(offset, forward) < -arena.Radius * 0.2f)
                {
                    return 1.2f;
                }
            }

            return float.MaxValue;
        }

        // Critical path length along the route polyline (the drop counts as its horizontal distance).
        public static float RouteLength()
        {
            float length = 0f;
            for (int i = 1; i < Route.Length; i++)
            {
                length += (Route[i].P - Route[i - 1].P).magnitude;
            }

            return length;
        }

        public static Vector3 OnRoute(string nodeName)
        {
            foreach (var n in Route)
            {
                if (n.Name == nodeName)
                {
                    return new Vector3(n.P.x, n.H, n.P.y);
                }
            }

            throw new System.ArgumentException("No route node " + nodeName);
        }

        public static Vector2 OnRouteXZ(string nodeName)
        {
            var p = OnRoute(nodeName);
            return new Vector2(p.x, p.z);
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }
    }
}
