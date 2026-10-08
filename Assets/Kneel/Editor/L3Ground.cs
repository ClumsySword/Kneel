using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The ground of L3, painted the way L1 and L2 paint theirs: one layout map over the whole footprint (large
    // colour in RGB, wetness in alpha) under the project's Kneel/Ground shader, which lays tiled earth over it in
    // world space. Nothing in the paint follows a ruler. The road is one unbroken, winding ribbon of pale
    // trodden earth from the spawn to the exit: it does not stop at a gate and start again in the field beyond,
    // it bends round whatever stands in a lane, and side tracks leave it and fade out. A field's floor spills
    // past its hedges in an uneven edge; mud, scorch, ash and lime are stains with soft, wandering borders.
    // Every floor slab and the backdrop share the material, so the paint runs across their joints.
    public static class L3Ground
    {
        public const string TexturesPath = L3Build.MaterialsPath + "/Textures";
        public const string LayoutPath = TexturesPath + "/L3_Ground_Layout.png";
        private const int Width = 1024, Height = 2048;

        public static Vector2 Uv(float x, float z)
        {
            return new Vector2((x - L3Plan.GroundMinX) / L3Plan.GroundWidth, (z - L3Plan.GroundMinZ) / L3Plan.GroundLength);
        }

        public static Material Material => AssetDatabase.LoadAssetAtPath<Material>(L3Build.MaterialsPath + "/L3_Ground.mat");

        // ---------------------------------------------------------------- Stains

        public class Stain
        {
            public char Stage;
            public string Name;
            public float X0, X1, Z0, Z1;

            // How much of it lies under water: the level the pool noise must pass (lower floods more).
            public float PoolLine;
        }

        // Where the ground is mud (the two mud volumes of 15.4). The Wallow is half pond; the grave is dug
        // earth with water only in its lowest places.
        public static readonly Stain[] Mud =
        {
            new Stain { Stage = 'C', Name = "Wallow", X0 = 148f, X1 = 168f, Z0 = 240f, Z1 = 254f, PoolLine = 0.5f },
            new Stain { Stage = 'D', Name = "Grave", X0 = 136f, X1 = 156f, Z0 = 292f, Z1 = 308f, PoolLine = 0.585f },
        };

        // Standing water in a mud stain: 1 in a pool, 0 on the mud between, a soft shore between the two. Pools
        // are a few metres across, lobed, and keep off the fading border of the stain. The paint turns this
        // into wetness, and the ground shader into water that fills the hollows of the earth; L3Mud heaps
        // clods along it.
        public static float Pool(Vector2 p, Stain stain)
        {
            float weight = StainWeight(p, stain.X0, stain.X1, stain.Z0, stain.Z1);
            if (weight < 0.45f)
            {
                return 0f;
            }

            float broad = Fbm(p.x * 0.2f + 71f, p.y * 0.2f + 5f), fine = Fbm(p.x * 0.75f + 3f, p.y * 0.75f + 29f);
            return Step(stain.PoolLine, stain.PoolLine + 0.07f, broad + (fine - 0.5f) * 0.14f) * Step(0.45f, 0.95f, weight);
        }

        public static float RectDistance(Vector2 p, float x0, float x1, float z0, float z1)
        {
            float dx = Mathf.Max(x0 - p.x, p.x - x1), dz = Mathf.Max(z0 - p.y, p.y - z1);
            if (dx <= 0f && dz <= 0f)
            {
                return Mathf.Max(dx, dz);
            }

            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dz, 0f)).magnitude;
        }

        // 0 below a, 1 above b, smooth between. (Mathf.SmoothStep takes its arguments the other way round.)
        public static float Step(float a, float b, float x)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        }

        public static float Fbm(float x, float z)
        {
            return Mathf.PerlinNoise(x + 61f, z + 47f) * 0.55f + Mathf.PerlinNoise(x * 2.1f + 7f, z * 2.1f + 3f) * 0.3f + Mathf.PerlinNoise(x * 4.3f + 13f, z * 4.3f + 19f) * 0.15f;
        }

        // 1 inside a stain, 0 outside, with a border that wanders by about a metre.
        public static float StainWeight(Vector2 p, float x0, float x1, float z0, float z1, float feather = 2.4f)
        {
            float sd = RectDistance(p, x0, x1, z0, z1);
            if (sd > feather + 2f)
            {
                return 0f;
            }

            float wander = (Fbm(p.x * 0.13f, p.y * 0.13f) - 0.5f) * 2.6f + (Fbm(p.x * 0.5f + 9f, p.y * 0.5f) - 0.5f) * 0.9f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-feather * 0.65f, feather * 0.35f, sd + wander));
        }

        // How burnt the open country is at a point (0 standing crop, 1 ash): strips running north as the army
        // fired them, leaning, swelling and breaking like anything drawn by fire and wind. The paint and the
        // crops (L3Fields) share it, so wheat stands exactly where the ground is not black.
        public static float Burnt(float wx, float z)
        {
            float lean = (Fbm(wx * 0.03f, z * 0.014f) - 0.5f) * 30f + Mathf.Sin(z * 0.043f + wx * 0.02f) * 3f;
            float strip = Mathf.Sin((wx + lean) * (Mathf.PI * 2f / 13f));
            float broken = Step(0.3f, 0.6f, Fbm(wx * 0.045f + 19f, z * 0.03f + 3f));
            return Step(-0.25f, 0.35f, strip) * Mathf.Lerp(0.35f, 1f, broken);
        }

        // ---------------------------------------------------------------- The road

        private class Road
        {
            public float Half, Weight;
            public bool FadeIn, FadeOut, Lime;

            // x, z, and the stage that point belongs to (as a number: 'A' = 0).
            public float[] Points;
        }

        private static float S(char stage)
        {
            return stage - 'A';
        }

        // Waypoints are hand-set so the road leaves room for whatever stands in a lane (see L3Plan.Pois), goes
        // round the shrine, the well and the brute's ring, and sends a path to the foot of the mill stair.
        private static readonly Road[] Roads =
        {
            // Spawn to the oak knoll, then down its north ramp to the dyke.
            new Road
            {
                Half = 2.3f, Weight = 1f,
                Points = new[]
                {
                    64f, -12f, 0, 64f, 4f, 0, 62.9f, 13f, 0, 64.2f, 24f, 0, 64.3f, 34f, 0, 63.8f, 46f, 0, 64.4f, 56f, 0, 65.4f, 63.5f, 0, 64f, 72f, 0, 65.8f, 80f, 0,
                    64.2f, 87f, 0, 62.7f, 90.5f, 0, 64f, 96.5f, 0, 64.3f, 103f, 0, 67.5f, 109.5f, 0, 75.5f, 112.2f, 0, 85.4f, 119.1f, 0, 96f, 124.1f, 0, 103.5f, 126.5f, 0,
                    108.5f, 134f, 0, 115.8f, 142.4f, 0, 122.1f, 152.4f, 0, 127.2f, 163.4f, 0, 134.8f, 173f, 0, 140f, 184.5f, 0, 140.2f, 195f, 0, 139.6f, 204f, 0,
                    137.4f, 208.5f, 0, 136.8f, 212.5f, 0, 138.4f, 216.3f, 0, 140f, 218.6f, 0, 140f, 224f, 1, 140f, 232f, 1, 140f, 239f, 2, 140.4f, 244f, 2,
                },
            },
            // From the foot of the knoll's drop, through the fire field, over the bridge, along the dyke, through
            // the farmstead and the mill yard, to the causeway.
            new Road
            {
                Half = 2.3f, Weight = 1f, FadeIn = true,
                Points = new[]
                {
                    131.8f, 206f, 1, 126.5f, 204.4f, 1, 121.5f, 200.8f, 1, 115.5f, 199.5f, 1, 109.6f, 200.2f, 1, 106.5f, 204f, 1, 105.7f, 215f, 1, 106f, 226f, 1, 106f, 233f, 1,
                    106f, 239f, 2, 107.2f, 243f, 2, 113f, 244.3f, 2, 121.5f, 243.2f, 2, 128f, 244.2f, 2, 133.5f, 243.4f, 2, 140f, 244.1f, 2, 147f, 244.4f, 2, 158f, 243.8f, 2,
                    167f, 244.3f, 2, 172f, 245.3f, 2, 173.6f, 249f, 2, 173f, 252.5f, 2, 174f, 258f, 2, 174.4f, 265f, 2, 173f, 271.5f, 2, 173.4f, 278.5f, 2, 174f, 285f, 2,
                    175.2f, 289.5f, 3, 174f, 294f, 3, 172.9f, 298f, 3, 173f, 301.5f, 3, 173.4f, 308f, 3, 174f, 314f, 3, 174.8f, 319.5f, 3, 181f, 326f, 3, 186.9f, 330.5f, 3,
                    187.3f, 336.5f, 3, 186.2f, 342f, 3, 185.3f, 351.5f, 3, 186.3f, 358.5f, 3, 186f, 366f, 3, 186f, 384f, 3, 186f, 400f, 3, 186f, 412f, 3,
                },
            },
            // Across the mill yard to the foot of the stair.
            new Road { Half = 1.5f, Weight = 0.85f, Points = new[] { 174.7f, 319f, 3, 175.2f, 324f, 3, 174.3f, 329.5f, 3, 174f, 334.9f, 3 } },
            // To the farmer's door.
            new Road { Half = 1.5f, Weight = 0.85f, Points = new[] { 173.2f, 270.5f, 2, 178f, 271f, 2, 184f, 271.8f, 2, 190f, 272f, 2 } },
            // The cart track to the grave: the same trodden earth, white with the lime the carts carried.
            new Road { Half = 1.5f, Weight = 0.85f, FadeOut = true, Lime = true, Points = new[] { 173f, 299.6f, 3, 167f, 300.2f, 3, 160f, 299.7f, 3, 153f, 300.2f, 3, 146.5f, 299.5f, 3 } },
            // The dyke lane west of the bridge leads nowhere, and looks it.
            new Road { Half = 1.6f, Weight = 0.7f, FadeOut = true, Points = new[] { 107.2f, 243.4f, 2, 103.8f, 244f, 2, 100.6f, 244.2f, 2 } },
        };

        private struct Sample
        {
            public Vector2 P;
            public float Half, Weight;
            public bool Lime;
        }

        private const float Bucket = 5f;
        private static List<Sample>[] buckets;
        private static int bucketsX, bucketsZ;
        private static char roadsFor = '\0';

        // Lays the road for the stages built so far as a smooth curve through its waypoints.
        public static void PrepareRoads(char stage)
        {
            bucketsX = Mathf.CeilToInt(L3Plan.GroundWidth / Bucket);
            bucketsZ = Mathf.CeilToInt(L3Plan.GroundLength / Bucket);
            buckets = new List<Sample>[bucketsX * bucketsZ];
            roadsFor = stage;
            foreach (var road in Roads)
            {
                var points = new List<Vector2>();
                for (int i = 0; i + 2 < road.Points.Length; i += 3)
                {
                    if (road.Points[i + 2] <= S(stage))
                    {
                        points.Add(new Vector2(road.Points[i], road.Points[i + 1]));
                    }
                }

                if (points.Count < 2)
                {
                    continue;
                }

                // Catmull-Rom through the waypoints, walked in short steps.
                var line = new List<Vector2>();
                for (int i = 0; i + 1 < points.Count; i++)
                {
                    Vector2 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Count - 1)];
                    int steps = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / 0.4f));
                    for (int k = 0; k < steps; k++)
                    {
                        float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                        line.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                    }
                }

                line.Add(points[points.Count - 1]);
                float total = 0f;
                var along = new float[line.Count];
                for (int i = 1; i < line.Count; i++)
                {
                    total += (line[i] - line[i - 1]).magnitude;
                    along[i] = total;
                }

                for (int i = 0; i < line.Count; i++)
                {
                    // It breathes: a little wider here, narrower there. Side tracks thin to nothing at their ends.
                    float half = road.Half * Mathf.Lerp(0.82f, 1.18f, Mathf.PerlinNoise(along[i] * 0.07f, road.Half * 3.1f + road.Points[0]));
                    if (road.FadeIn)
                    {
                        half *= Step(0f, 6f, along[i]);
                    }

                    if (road.FadeOut)
                    {
                        half *= Step(0f, 6f, total - along[i]);
                    }

                    int bx = Mathf.FloorToInt((line[i].x - L3Plan.GroundMinX) / Bucket), bz = Mathf.FloorToInt((line[i].y - L3Plan.GroundMinZ) / Bucket);
                    if (bx < 0 || bz < 0 || bx >= bucketsX || bz >= bucketsZ)
                    {
                        continue;
                    }

                    int index = bz * bucketsX + bx;
                    if (buckets[index] == null)
                    {
                        buckets[index] = new List<Sample>();
                    }

                    buckets[index].Add(new Sample { P = line[i], Half = half, Weight = road.Weight, Lime = road.Lime });
                }
            }
        }

        // Distance from p to the road's edge (negative on the road). 99 when no road is near.
        public static float RoadDistance(Vector2 p)
        {
            return RoadDistance(p, out _, out _);
        }

        public static float RoadDistance(Vector2 p, out float weight, out bool lime)
        {
            weight = 0f;
            lime = false;
            if (buckets == null)
            {
                return 99f;
            }

            int cx = Mathf.FloorToInt((p.x - L3Plan.GroundMinX) / Bucket), cz = Mathf.FloorToInt((p.y - L3Plan.GroundMinZ) / Bucket);
            float best = 99f;
            for (int bz = cz - 1; bz <= cz + 1; bz++)
            {
                for (int bx = cx - 1; bx <= cx + 1; bx++)
                {
                    if (bx < 0 || bz < 0 || bx >= bucketsX || bz >= bucketsZ || buckets[bz * bucketsX + bx] == null)
                    {
                        continue;
                    }

                    foreach (var s in buckets[bz * bucketsX + bx])
                    {
                        float d = (s.P - p).magnitude - s.Half;
                        if (d < best)
                        {
                            best = d;
                            weight = s.Weight;
                            lime = s.Lime;
                        }
                    }
                }
            }

            return best;
        }

        // ---------------------------------------------------------------- Paint

        // Paints the floors of every stage up to 'stage'.
        public static string Paint(char stage)
        {
            L2Build.EnsureFolder(TexturesPath);
            PrepareRoads(stage);
            var pixels = new Color[Width * Height];

            // Much paler than they end up: the ground shader multiplies them by a tiled earth overlay that sits
            // well under mid-grey (L1 lifts its map for the same reason).
            Color burnt = L1Build.Hex("#5C4F42"), straw = L1Build.Hex("#80735A"), ash = L1Build.Hex("#38302A"), regrowth = L1Build.Hex("#5C6645");
            Color chalk = L1Build.Hex("#DDD2BA"), stubble = L1Build.Hex("#8C7560"), verge = L1Build.Hex("#7C6E57");
            Color mud = L1Build.Hex("#735C47"), mudDeep = L1Build.Hex("#574434"), lime = L1Build.Hex("#FAF7EB"), gouge = L1Build.Hex("#29211A");

            var floors = new List<L3Plan.Floor>();
            foreach (var f in L3Plan.Floors)
            {
                if (f.Stage <= stage)
                {
                    floors.Add(f);
                }
            }

            Vector2 ring = new Vector2(114.5f, 131f);
            var furrowFrom = new Vector2(77f, 112.6f);
            var furrowTo = new Vector2(111.2f, 133.2f);

            for (int y = 0; y < Height; y++)
            {
                float z = L3Plan.GroundMinZ + (y + 0.5f) / Height * L3Plan.GroundLength;
                for (int x = 0; x < Width; x++)
                {
                    float wx = L3Plan.GroundMinX + (x + 0.5f) / Width * L3Plan.GroundWidth;
                    var p = new Vector2(wx, z);
                    float slow = (Fbm(wx * 0.12f, z * 0.12f) - 0.5f) * 3.2f;
                    float fine = (Fbm(wx * 0.45f, z * 0.45f) - 0.5f) * 1.3f;

                    float laneDistance = 99f, arenaDistance = 99f;
                    foreach (var f in floors)
                    {
                        if (wx < f.X0 - 6f || wx > f.X1 + 6f || z < f.Z0 - 6f || z > f.Z1 + 6f)
                        {
                            continue;
                        }

                        bool isLane = f.Lane || f.Id == "LaneFromL2";
                        if (f.Virtual && !isLane)
                        {
                            continue;
                        }

                        float sd = f.SignedDistance(p);
                        if (isLane)
                        {
                            laneDistance = Mathf.Min(laneDistance, sd);
                        }
                        else
                        {
                            arenaDistance = Mathf.Min(arenaDistance, sd);
                        }
                    }

                    // Outside: strips of ash and dead straw.
                    float burntStrip = Burnt(wx, z);
                    Color c = Color.Lerp(straw, ash, burntStrip * 0.85f);
                    c = Color.Lerp(c, burnt, 0.45f);
                    float green = Step(0.56f, 0.74f, Fbm(wx * 0.07f + 41f, z * 0.07f + 5f));
                    c = Color.Lerp(c, regrowth, green * 0.5f * (1f - burntStrip));
                    float alpha = 0.04f;

                    // A field's floor: trodden stubble, darker where it is worn, with an edge that wanders well
                    // past the hedge line.
                    float wArena = 1f - Step(-1f, 2.2f, arenaDistance + slow * 0.55f + fine);
                    if (wArena > 0f)
                    {
                        float worn = Fbm(wx * 0.16f + 9f, z * 0.16f + 23f);
                        Color floor = Color.Lerp(stubble * 0.82f, Color.Lerp(stubble, straw, 0.5f), Step(0.35f, 0.7f, worn));
                        c = Color.Lerp(c, floor, wArena);
                        alpha = Mathf.Lerp(alpha, 0.07f, wArena);
                    }

                    // A lane's verge: the ground between the road and the walls.
                    float wVerge = 1f - Step(-0.8f, 1.6f, laneDistance + fine);
                    if (wVerge > 0f)
                    {
                        float tuft = Fbm(wx * 0.33f + 71f, z * 0.33f + 13f);
                        Color side = Color.Lerp(verge * 0.85f, Color.Lerp(verge, regrowth, 0.35f), Step(0.4f, 0.7f, tuft));
                        c = Color.Lerp(c, side, wVerge);
                    }

                    // The road: one ribbon, soft at its edges, worn palest down the middle.
                    float roadDistance = RoadDistance(p, out float roadWeight, out bool limed);
                    float wRoad = (1f - Step(-1.1f, 0.7f, roadDistance + fine * 0.55f)) * roadWeight;
                    if (wRoad > 0f)
                    {
                        Color dust = Color.Lerp(chalk * 0.82f, chalk, Fbm(wx * 0.3f + 3f, z * 0.3f + 11f));
                        dust = Color.Lerp(dust * 0.9f, dust, Step(-0.2f, -1.6f, roadDistance));
                        if (limed)
                        {
                            dust = Color.Lerp(dust, lime, 0.55f * Step(0.3f, 0.6f, Fbm(wx * 0.5f + 5f, z * 0.5f)));
                        }

                        c = Color.Lerp(c, dust, wRoad);
                        alpha = Mathf.Lerp(alpha, 0.05f, wRoad);
                    }

                    // Soot and ash drift over everything, thinner where feet keep the ground clean.
                    float soot = Step(0.56f, 0.72f, Fbm(wx * 0.11f + 31f, z * 0.11f + 17f));
                    c = Color.Lerp(c, ash, soot * (0.55f - 0.4f * wRoad));

                    if (wx > 70f && wx < 120f && z > 106f && z < 140f)
                    {
                        // The ring the ploughman's flail has gouged, and the one furrow that leads to it.
                        float fromRing = Mathf.Abs((p - ring).magnitude - 4f + fine * 0.1f);
                        c = Color.Lerp(c, gouge, (1f - Step(0.25f, 0.7f, fromRing)) * 0.9f);
                        Vector2 ab = furrowTo - furrowFrom;
                        float t = Mathf.Clamp01(Vector2.Dot(p - furrowFrom, ab) / ab.sqrMagnitude);
                        float fromFurrow = (furrowFrom + ab * t - p).magnitude + fine * 0.05f;
                        c = Color.Lerp(c, gouge, (1f - Step(0.2f, 0.6f, fromFurrow)) * 0.9f);
                    }

                    if (stage >= 'B')
                    {
                        // The row that burned before the player came: ash blown off it to the north-east.
                        float ashRow = StainWeight(new Vector2(wx - (z - 214f) * 0.04f, z), 87.4f, 90.6f, 201f, 228f, 1.6f);
                        c = Color.Lerp(c, ash, ashRow * 0.8f);
                    }

                    foreach (var stain in Mud)
                    {
                        if (stain.Stage > stage)
                        {
                            continue;
                        }

                        // Mud: churned, darker toward the middle, damp all through, and under water where
                        // its pools lie. The 2 m band of damp ground at the ends of the lane is the same
                        // stain thinning out. Wetness is chosen for what the ground shader does with it:
                        // about 0.14 floods the hollows of the earth, about 0.115 leaves it damp with water
                        // only in its deepest dents.
                        float w = StainWeight(p, stain.X0, stain.X1, stain.Z0, stain.Z1);
                        if (w <= 0f)
                        {
                            continue;
                        }

                        float churn = Fbm(wx * 0.38f + 27f, z * 0.38f + 51f);
                        Color wetGround = Color.Lerp(mudDeep, mud, Step(0.3f, 0.75f, churn)) * 0.8f;
                        c = Color.Lerp(c, wetGround, w * 0.92f);
                        float pool = Pool(p, stain);
                        // The earth is darker and smoother where water has stood on it.
                        c = Color.Lerp(c, mudDeep * 0.9f, pool * 0.6f * w);
                        alpha = Mathf.Lerp(alpha, Mathf.Lerp(Mathf.Lerp(0.118f, 0.108f, Step(0.35f, 0.7f, churn)), 0.142f, pool), w);
                    }

                    if (stage >= L3Plan.Ditch.Stage)
                    {
                        // The ditch: its banks carry this paint. Damp, dark earth, darkest at the water.
                        var ditch = L3Plan.Ditch;
                        float into = -RectDistance(p, ditch.X0, ditch.X1, ditch.Z0, ditch.Z1);
                        if (into > -0.6f)
                        {
                            float bank = Step(-0.5f, 0.5f, into + (Fbm(wx * 0.4f + 15f, z * 0.4f + 2f) - 0.5f) * 0.6f);
                            Color earth = Color.Lerp(mudDeep, mudDeep * 0.62f, Step(0.4f, 2f, into));
                            c = Color.Lerp(c, earth, bank * 0.9f);
                            alpha = Mathf.Lerp(alpha, 0.04f, bank);
                        }
                    }

                    if (stage >= 'D')
                    {
                        // Lime spilled round the mound.
                        float spill = 1f - Step(2.2f, 4.8f, (p - new Vector2(140f, 295f)).magnitude + slow * 0.7f);
                        c = Color.Lerp(c, lime, spill * 0.6f);
                    }

                    float grain = Mathf.PerlinNoise(wx * 1.4f, z * 1.4f);
                    c *= 0.9f + 0.2f * grain;
                    c = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), alpha);
                    pixels[y * Width + x] = c;
                }
            }

            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(LayoutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(LayoutPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LayoutPath);
            if (importer.wrapMode != TextureWrapMode.Clamp || importer.maxTextureSize != 2048)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            string matPath = L3Build.MaterialsPath + "/L3_Ground.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(L2Build.MaterialsPath + "/L2_Ground.mat", matPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(LayoutPath));
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            // No paving and no moss in the fields. Water stands exactly where the paint says the ground is wet
            // (only the mud is): asking the shader for full cover turns off its own scattering of puddles, so
            // the painted pools are what fills.
            mat.SetTexture("_PaveMask", null);
            mat.SetFloat("_MossAmount", 0f);
            mat.SetFloat("_PuddleAmount", 1f);
            // Past the top of the slider on purpose: at 1 the water only ever half fills the earth, and a pool
            // came out as a scatter of blots. At this depth a painted pool is one sheet with a few lumps
            // standing out of it, and the damp mud between pools holds water in its deepest dents only.
            mat.SetFloat("_PuddleDepth", 1.5f);
            mat.SetFloat("_PuddleScale", 2.2f);
            mat.SetColor("_PuddleSky", L1Build.Hex("#34373A"));
            EditorUtility.SetDirty(mat);
            Decals(mat);
            AssetDatabase.SaveAssets();
            return "ground painted for stage " + stage;
        }

        // The hazard prefabs' ground decals were flat colours, which the grade turns into black rectangles.
        // They use the ground shader with a one-colour layout instead. In the level the mud's own plane is
        // switched off altogether (the paint above takes its place); these are what the review scene shows.
        private static void Decals(Material ground)
        {
            Wet(ground, "L3_GB_Mud", "L3_Mud_Layout", new Color(0.32f, 0.255f, 0.2f, 0.3f), 0.5f, 0.7f);
            Wet(ground, "L3_GB_Damp", "L3_Damp_Layout", new Color(0.5f, 0.42f, 0.34f, 0.15f), 0.4f, 0.5f);
        }

        private static void Wet(Material ground, string material, string texture, Color colour, float puddles, float depth)
        {
            string path = TexturesPath + "/" + texture + ".png";
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = colour;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.alphaIsTransparency || importer.mipmapEnabled)
            {
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var mat = L3Build.Mat(material);
            mat.shader = ground.shader;
            mat.CopyPropertiesFromMaterial(ground);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetFloat("_PuddleAmount", puddles);
            mat.SetFloat("_PuddleDepth", depth);
            EditorUtility.SetDirty(mat);
        }
    }
}
