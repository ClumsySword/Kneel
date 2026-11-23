using System.Collections.Generic;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Gothic pieces for L1's monastery, in the faceted low-poly kit the other levels use: coursed walls (L4Kit
    // blocks) with pointed lancet openings, voussoir rings, clustered piers, plank door leaves, the octagonal altar
    // and old candles. Everything is built in a local frame and baked by L1Monastery afterwards.
    public static class L1MonasteryKit
    {
        public const float Course = L4Kit.Course;
        private const float Sqrt3 = 1.7320508f;

        // An equilateral pointed opening in a wall: centred at U along the wall, W half-width, a sill (0 for a
        // door), springing at Spring. Its arch is a ring of voussoirs RingDepth deep.
        public struct Opening
        {
            public float U, W, Sill, Spring, RingDepth;
            public bool Pane;
            public bool Arch;

            public Opening(float u, float w, float sill, float spring, bool pane, float ringDepth = 0.34f)
            {
                U = u;
                W = w;
                Sill = sill;
                Spring = spring;
                Pane = pane;
                RingDepth = ringDepth;
                Arch = true;
            }

            // A plain gap through a cut-down wall (no arch over it).
            public static Opening Gap(float u, float w)
            {
                return new Opening(u, w, 0f, 50f, false, 0f) { Arch = false };
            }

            public float Apex => Spring + W * Sqrt3;

            public float OuterApex => Spring + Mathf.Sqrt(Sq(2f * W + RingDepth) - W * W);

            // Half-width of the clear opening at height y.
            public float Inner(float y)
            {
                if (y < Sill || y > Apex)
                {
                    return 0f;
                }

                return y <= Spring ? W : Mathf.Max(0f, -W + Mathf.Sqrt(Mathf.Max(0f, 4f * W * W - Sq(y - Spring))));
            }

            // Half-width the coursed wall keeps clear at height y (to the outside of the ring).
            public float Outer(float y)
            {
                if (y < Sill || y > OuterApex)
                {
                    return 0f;
                }

                return y <= Spring ? W : Mathf.Max(0f, -W + Mathf.Sqrt(Mathf.Max(0f, Sq(2f * W + RingDepth) - Sq(y - Spring))));
            }
        }

        private static float Sq(float x) => x * x;

        // ---------------------------------------------------------------- Walls

        // A coursed wall in g's local frame: it runs along +x from x 0 to 'length', its inner face on z 0 and its
        // outer face at z -thickness, ground at y 0. top(x) is its height at x (a stepped cutaway where it drops);
        // the last course under the top is ragged by 'loss'. Openings are cut through it with voussoir rings.
        public static void Wall(Transform g, float length, float thickness, System.Func<float, float> top, Opening[] openings, float loss, int seed, float widthScale = 2f)
        {
            var rng = new System.Random(seed);
            openings = openings ?? new Opening[0];
            float maxTop = 0f;
            for (float x = 0f; x <= length; x += 0.1f)
            {
                maxTop = Mathf.Max(maxTop, top(x));
            }

            int courses = Mathf.CeilToInt(maxTop / Course - 0.01f);
            const float step = 0.05f;
            int samples = Mathf.CeilToInt(length / step);
            for (int c = 0; c < courses; c++)
            {
                float y0 = c * Course, y1 = y0 + Course;
                // 0: nothing, 1: a middle course, 2: the wall's top course here.
                var state = new int[samples];
                for (int i = 0; i < samples; i++)
                {
                    float x = (i + 0.5f) * step;
                    float t = top(x);
                    if (t < y0 + Course * 0.5f)
                    {
                        continue;
                    }

                    bool blocked = false;
                    foreach (var o in openings)
                    {
                        float half = Mathf.Max(o.Outer(y0 + 0.01f), Mathf.Max(o.Outer((y0 + y1) * 0.5f), o.Outer(y1 - 0.01f)));
                        if (half > 0f && Mathf.Abs(x - o.U) < half)
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                    {
                        state[i] = t < y1 + Course * 0.5f ? 2 : 1;
                    }
                }

                int start = 0;
                while (start < samples)
                {
                    int s = state[start];
                    int end = start;
                    while (end < samples && state[end] == s)
                    {
                        end++;
                    }

                    if (s != 0)
                    {
                        float x0 = start * step, x1 = Mathf.Min(length, end * step);
                        if (x1 - x0 > 0.06f)
                        {
                            L4Kit.Courses(g, new Vector3(x0, y0, -thickness), Vector3.right, Vector3.back, x1 - x0, Course, rng, s == 2 ? loss : 0f, thickness, widthScale);
                        }
                    }

                    start = end;
                }
            }

            foreach (var o in openings)
            {
                if (!o.Arch)
                {
                    continue;
                }

                Ring(g, o, thickness, rng);
                if (o.Sill > 0f)
                {
                    // The sill: one long stone, splayed a little proud of the inner face.
                    var sill = L3Build.MeshChild(g, "Sill", L2PolyKit.Tread(new Vector3(o.W * 2f + 0.3f, 0.16f, thickness + 0.12f), rng.Next(100000)), new Vector3(o.U, o.Sill - 0.08f, -thickness * 0.5f + 0.06f), L4Kit.Stone);
                    sill.name = "Sill";
                }
            }
        }

        // A free-standing pointed arch (an arcade's, between two pillars): its voussoirs only, from z zBack to zFront.
        public static void ArchRing(L3Mesh m, Opening o, float zBack, float zFront, System.Random rng)
        {
            Voussoirs(m, o, zBack, zFront, rng);
        }

        private static void Voussoirs(L3Mesh m, Opening o, float zBack, float zFront, System.Random rng)
        {
            float r0 = 2f * o.W, r1 = r0 + o.RingDepth;
            foreach (float side in new[] { 1f, -1f })
            {
                var centre = new Vector2(o.U - side * o.W, o.Spring);
                float end = 60f * Mathf.Deg2Rad;
                int count = Mathf.Max(3, Mathf.RoundToInt(r0 * end / 0.42f));
                for (int i = 0; i < count; i++)
                {
                    float a0 = end * i / count + 0.006f, a1 = end * (i + 1) / count - 0.006f;
                    bool key = i == count - 1;
                    float depth = key ? r1 + 0.08f : r1 - (float)rng.NextDouble() * 0.06f;
                    Wedge(m, centre, side, r0, depth, a0, a1, zBack - (key ? 0.03f : 0f), zFront + (key ? 0.03f : 0f), rng.NextDouble() < 0.35 ? L2PolyKit.TopLight : L2PolyKit.Top);
                }
            }
        }

        // The voussoirs of a pointed arch (two arcs meeting at the apex) and a dark backing strip behind the joints.
        private static void Ring(Transform g, Opening o, float thickness, System.Random rng)
        {
            var m = new L3Mesh();
            float r0 = 2f * o.W, r1 = r0 + o.RingDepth;
            foreach (float side in new[] { 1f, -1f })
            {
                // The right half's arc is centred on the left springing point, and the reverse.
                var centre = new Vector2(o.U - side * o.W, o.Spring);
                float end = 60f * Mathf.Deg2Rad;
                int count = Mathf.Max(3, Mathf.RoundToInt(r0 * end / 0.42f));
                for (int i = 0; i < count; i++)
                {
                    float a0 = end * i / count + 0.006f, a1 = end * (i + 1) / count - 0.006f;
                    bool key = i == count - 1;
                    float depth = key ? r1 + 0.08f : r1 - (float)rng.NextDouble() * 0.06f;
                    Wedge(m, centre, side, r0, depth, a0, a1, -thickness - (key ? 0.04f : 0.01f), key ? 0.04f : 0.01f, rng.NextDouble() < 0.35 ? L2PolyKit.TopLight : L2PolyKit.Top);
                }
            }

            // Backing: the joints and the stair-steps of the courses behind the ring open into a dark core.
            for (float y = o.Spring; y < o.OuterApex; y += 0.15f)
            {
                float inner = o.Inner(y), outer = o.Outer(y) + 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float from = inner + 0.02f;
                    float centreX = o.U + side * (from + outer) * 0.5f;
                    L4Shapes.Box(m, 0, new Vector3(centreX, y + 0.075f, -thickness * 0.5f), Quaternion.identity, new Vector3(outer - from, 0.15f, thickness - 0.1f), L4Kit.Cell(L2PolyKit.Joint));
                }
            }

            L3Build.MeshChild(g, "Ring", m.ToMesh("Ring"), Vector3.zero, L4Kit.Stone);
        }

        // One wedge stone of a pointed arc: radii r0..r1 round 'centre' (u, y), angles a0..a1 measured from the
        // horizontal towards the apex, mirrored for the left half; z from zBack to zFront.
        private static void Wedge(L3Mesh m, Vector2 centre, float side, float r0, float r1, float a0, float a1, float zBack, float zFront, int face)
        {
            Vector3 P(float r, float a, float z) => new Vector3(centre.x + side * Mathf.Cos(a) * r, centre.y + Mathf.Sin(a) * r, z);
            float am = (a0 + a1) * 0.5f;
            var radial = new Vector3(side * Mathf.Cos(am), Mathf.Sin(am), 0f);
            var cell = L4Kit.Cell(face);
            L4Shapes.Quad(m, 0, P(r0, a0, zFront), P(r0, a1, zFront), P(r1, a1, zFront), P(r1, a0, zFront), Vector3.forward, cell);
            L4Shapes.Quad(m, 0, P(r0, a0, zBack), P(r0, a1, zBack), P(r1, a1, zBack), P(r1, a0, zBack), Vector3.back, cell);
            L4Shapes.Quad(m, 0, P(r0, a0, zBack), P(r0, a1, zBack), P(r0, a1, zFront), P(r0, a0, zFront), -radial, L4Kit.Cell(L2PolyKit.Side));
            L4Shapes.Quad(m, 0, P(r1, a0, zBack), P(r1, a1, zBack), P(r1, a1, zFront), P(r1, a0, zFront), radial, L4Kit.Cell(L2PolyKit.Top));
            var t0 = new Vector3(side * Mathf.Sin(a0), -Mathf.Cos(a0), 0f);
            var t1 = new Vector3(-side * Mathf.Sin(a1), Mathf.Cos(a1), 0f);
            L4Shapes.Quad(m, 0, P(r0, a0, zBack), P(r1, a0, zBack), P(r1, a0, zFront), P(r0, a0, zFront), t0, L4Kit.Cell(L2PolyKit.End));
            L4Shapes.Quad(m, 0, P(r0, a1, zBack), P(r1, a1, zBack), P(r1, a1, zFront), P(r0, a1, zFront), t1, L4Kit.Cell(L2PolyKit.End));
        }

        // The lancet's outline from the sill round the arch (local u, y), for panes.
        public static List<Vector2> Outline(Opening o, float inset)
        {
            var points = new List<Vector2>();
            float w = o.W - inset;
            points.Add(new Vector2(o.U - w, o.Sill + inset));
            points.Add(new Vector2(o.U + w, o.Sill + inset));
            const int steps = 6;
            for (int k = 0; k <= steps; k++)
            {
                float y = o.Spring + (o.Apex - inset - o.Spring) * k / steps;
                points.Add(new Vector2(o.U + Mathf.Max(0f, o.Inner(y) - inset), y));
            }

            for (int k = steps - 1; k >= 0; k--)
            {
                float y = o.Spring + (o.Apex - inset - o.Spring) * k / steps;
                points.Add(new Vector2(o.U - Mathf.Max(0f, o.Inner(y) - inset), y));
            }

            return points;
        }

        // A glazed lancet set into the opening at depth z: a pale pane facing +z and -z, and iron cames.
        public static void Pane(L3Mesh glass, L3Mesh iron, Opening o, float z)
        {
            var outline = Outline(o, 0.02f);
            var c = new Vector2(o.U, (o.Sill + o.Apex) * 0.5f);
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                var pa = new Vector3(a.x, a.y, z);
                var pb = new Vector3(b.x, b.y, z);
                var pc = new Vector3(c.x, c.y, z);
                L4Shapes.Tri(glass, 0, pc, pa, pb, Vector3.forward, Vector2.one * 0.5f);
                L4Shapes.Tri(glass, 0, pc, pa, pb, Vector3.back, Vector2.one * 0.5f);
            }

            // Few, broad cames (thin bars against the bright pane were lines a pixel or two wide that crawled as the
            // camera moved).
            var cell = L4Kit.Cell(L2PolyKit.Side);
            L4Shapes.Shaft(iron, 0, new Vector3(o.U, o.Sill, z), new Vector3(o.U, o.Apex - 0.05f, z), 0.055f, 0.055f, 4, cell);
            for (float y = o.Sill + 1.2f; y < o.Spring - 0.3f; y += 1.2f)
            {
                L4Shapes.Shaft(iron, 0, new Vector3(o.U - o.W, y, z), new Vector3(o.U + o.W, y, z), 0.045f, 0.045f, 4, cell);
            }
        }

        // ---------------------------------------------------------------- Piers

        // A clustered pier: a stepped base, a core with four attached shafts, and a moulded capital at 'height'
        // (left off when the pier is cut down, cutHeight > 0, which leaves a broken top and its fallen drum).
        public static void Pier(Transform g, Vector3 at, float height, int seed, float cutHeight = 0f, float scale = 1f)
        {
            var rng = new System.Random(seed);
            var m = new L3Mesh();
            var top = L4Kit.Cell(L2PolyKit.Top);
            var side = L4Kit.Cell(L2PolyKit.Side);
            var light = L4Kit.Cell(L2PolyKit.TopLight);
            // Radii scale with the pier (the round hall's are 1.3x); heights stay as given.
            float s = scale;
            float baseH = 0.55f * s;
            var octagon = Matrix4x4.TRS(at, Quaternion.identity, Vector3.one);
            L4Shapes.Lathe(m, 0, octagon, new[] { new Vector2(0.62f * s, 0f), new Vector2(0.62f * s, 0.22f * s), new Vector2(0.52f * s, 0.3f * s), new Vector2(0.52f * s, 0.48f * s), new Vector2(0.45f * s, baseH), new Vector2(0f, baseH) }, 8, side, -1, Mathf.PI / 8f);
            float shaftTop = cutHeight > 0f ? cutHeight : height - 0.6f * s;
            L4Shapes.Shaft(m, 0, at + Vector3.up * baseH, at + Vector3.up * shaftTop, 0.33f * s, 0.33f * s, 8, top);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.36f * s;
                float t = cutHeight > 0f ? cutHeight - 0.15f - (float)rng.NextDouble() * 0.3f : shaftTop;
                L4Shapes.Shaft(m, 0, at + o + Vector3.up * baseH, at + o + Vector3.up * t, 0.12f * s, 0.12f * s, 6, light);
            }

            if (cutHeight > 0f)
            {
                // The broken top: a tilted cap of rough stone.
                L4Shapes.Shaft(m, 0, at + Vector3.up * (cutHeight - 0.02f), at + new Vector3(0.05f, cutHeight + 0.1f, -0.04f), 0.34f * s, 0.18f * s, 7, L4Kit.Cell(L2PolyKit.TopDark));
            }
            else
            {
                // Capital: two flaring octagonal courses and an abacus.
                L4Shapes.Lathe(m, 0, Matrix4x4.TRS(at + Vector3.up * shaftTop, Quaternion.identity, Vector3.one), new[] { new Vector2(0f, 0f), new Vector2(0.42f * s, 0f), new Vector2(0.55f * s, 0.3f * s), new Vector2(0.66f * s, 0.42f * s), new Vector2(0.66f * s, 0.6f * s), new Vector2(0f, 0.6f * s) }, 8, light, -1, Mathf.PI / 8f);
            }

            L3Build.MeshChild(g, "Pier", m.ToMesh("Pier"), Vector3.zero, L4Kit.Stone);
        }

        // A flat pilaster against a wall: courses 'width' wide standing 'proud' off the inner face (z 0 up to +proud),
        // centred at x, from the ground to 'height'.
        public static void Pilaster(Transform g, float x, float width, float proud, float height, float loss, int seed)
        {
            var rng = new System.Random(seed);
            L4Kit.Courses(g, new Vector3(x - width * 0.5f, 0f, proud), Vector3.right, Vector3.forward, width, height, rng, loss, proud + 0.05f);
        }

        // ---------------------------------------------------------------- Doors

        // One leaf of a pointed door in its own frame: hinge at x 0, the leaf running along +x to 'width', its face
        // towards +z, ground at y 0. The planks follow the arch of the opening (o, of which this leaf is the
        // 'right' half when right is true), with iron straps, studs and a ring.
        public static void Leaf(L3Mesh wood, L3Mesh iron, Opening o, bool right, int seed, float flatTop = 0f)
        {
            var rng = new System.Random(seed);
            const float plankWidth = 0.21f, thickness = 0.09f;
            int planks = Mathf.Max(2, Mathf.RoundToInt(o.W / plankWidth));
            float pw = o.W / planks;
            for (int i = 0; i < planks; i++)
            {
                // Distance of this plank's centre from the opening's centre line.
                float fromHinge = (i + 0.5f) * pw;
                float fromCentre = o.W - fromHinge;
                float h = (flatTop > 0f ? flatTop : HeightAt(o, Mathf.Abs(fromCentre) + pw * 0.5f) - 0.03f) - (float)rng.NextDouble() * 0.02f;
                var board = L3Build.Board(new Vector3(h, thickness * 0.5f, pw - 0.012f), rng.Next(100000));
                // Board: length along x, thickness y, width z. Stand it up: x -> y (up), y -> z (face), z -> x (across).
                var frame = Matrix4x4.TRS(new Vector3(fromHinge, h * 0.5f, 0f), Quaternion.LookRotation(Vector3.right, Vector3.forward), Vector3.one);
                wood.Append(board, frame, 0);
            }

            var cell = L4Kit.Cell(L2PolyKit.Iron);
            foreach (float y in new[] { 0.45f, 1.5f, (flatTop > 0f ? flatTop : o.Spring) - 0.3f })
            {
                float reach = o.W * 0.92f;
                L4Shapes.Box(iron, 0, new Vector3(reach * 0.5f, y, thickness * 0.5f + 0.012f), Quaternion.identity, new Vector3(reach, 0.07f, 0.02f), cell);
                for (float x = 0.12f; x < reach; x += 0.24f)
                {
                    L4Shapes.Box(iron, 0, new Vector3(x, y, thickness * 0.5f + 0.03f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.035f, 0.035f, 0.025f), cell);
                }
            }

            // The ring pull near the meeting edge.
            var ringAt = new Vector3(o.W - 0.18f, 1.05f, thickness * 0.5f + 0.05f);
            for (int k = 0; k < 10; k++)
            {
                float a0 = k * 36f * Mathf.Deg2Rad, a1 = (k + 1) * 36f * Mathf.Deg2Rad;
                L4Shapes.Shaft(iron, 0, ringAt + new Vector3(Mathf.Sin(a0), -Mathf.Cos(a0), 0f) * 0.09f, ringAt + new Vector3(Mathf.Sin(a1), -Mathf.Cos(a1), 0f) * 0.09f, 0.013f, 0.013f, 4, cell);
            }
        }

        // Height of a pointed opening's intrados at distance d from its centre line.
        private static float HeightAt(Opening o, float d)
        {
            d = Mathf.Clamp(d, 0f, o.W);
            return o.Spring + Mathf.Sqrt(Mathf.Max(0f, 4f * o.W * o.W - Sq(d + o.W)));
        }

        // ---------------------------------------------------------------- Altar, relic, candles

        // The altar: a two-step octagonal base, a pedestal and the shallow basin the relic lay in. Origin on the
        // floor at its centre.
        public static void Altar(L3Mesh m)
        {
            var frame = Matrix4x4.identity;
            float ph = Mathf.PI / 8f;
            var side = L4Kit.Cell(L2PolyKit.Side);
            var light = L4Kit.Cell(L2PolyKit.TopLight);
            var top = L4Kit.Cell(L2PolyKit.Top);
            L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(1.95f, -0.05f), new Vector2(1.95f, 0.22f), new Vector2(1.88f, 0.26f), new Vector2(1.5f, 0.26f), new Vector2(0f, 0.26f) }, 8, side, -1, ph);
            L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(1.45f, 0.2f), new Vector2(1.45f, 0.5f), new Vector2(1.38f, 0.54f), new Vector2(0f, 0.54f) }, 8, light, -1, ph);
            L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(0.7f, 0.5f), new Vector2(0.62f, 0.62f), new Vector2(0.5f, 0.66f), new Vector2(0.48f, 1.02f), new Vector2(0.62f, 1.08f), new Vector2(0f, 1.08f) }, 8, top, -1, ph);
            // The basin: out over the rim and down into a shallow bowl.
            L4Shapes.Lathe(m, 0, frame, new[] { new Vector2(0.5f, 1.04f), new Vector2(0.92f, 1.2f), new Vector2(1.02f, 1.36f), new Vector2(1.02f, 1.42f), new Vector2(0.88f, 1.42f), new Vector2(0.84f, 1.3f), new Vector2(0.55f, 1.24f), new Vector2(0f, 1.22f) }, 8, light, -1, ph);
        }

        // The broken relic: a dark faceted stone split in two, its halves fallen apart in the basin, and shards.
        public static void Relic(L3Mesh m, int seed)
        {
            var rng = new System.Random(seed);
            var cell = Vector2.one * 0.5f;
            var basin = new Vector3(0f, 1.24f, 0f);
            Half(m, basin + new Vector3(-0.17f, 0.11f, 0.05f), Quaternion.Euler(8f, 20f, 64f), 0.15f, 0.25f, cell);
            Half(m, basin + new Vector3(0.18f, 0.08f, -0.06f), Quaternion.Euler(-12f, 150f, 74f), 0.15f, 0.22f, cell);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.15f + (float)rng.NextDouble() * 0.55f;
                var p = basin + new Vector3(Mathf.Cos(a) * r, 0.03f + (r > 0.55f ? 0.0f : 0.0f), Mathf.Sin(a) * r);
                if (r > 0.6f)
                {
                    p.y = 1.42f;   // on the rim
                }

                var size = 0.03f + (float)rng.NextDouble() * 0.05f;
                var rot = Quaternion.Euler(rng.Next(360), rng.Next(360), rng.Next(360));
                L4Shapes.Shaft(m, 0, p, p + rot * Vector3.up * size * 1.6f, size, 0.005f, 3, cell);
            }
        }

        // Half a split, elongated octahedral stone: a pointed crown over the flat break face.
        private static void Half(L3Mesh m, Vector3 at, Quaternion rot, float radius, float length, Vector2 cell)
        {
            const int sides = 6;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                ring[i] = new Vector3(Mathf.Cos(a) * radius * (i % 2 == 0 ? 1f : 0.82f), 0f, Mathf.Sin(a) * radius);
            }

            Vector3 tip = new Vector3(0.02f, length, -0.01f);
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = ring[i], b = ring[(i + 1) % sides];
                Vector3 mid = (a + b) * 0.5f;
                L4Shapes.Tri(m, 0, at + rot * a, at + rot * b, at + rot * tip, rot * (mid + Vector3.up * 0.3f), cell);
                L4Shapes.Tri(m, 0, at + rot * a, at + rot * b, at, rot * Vector3.down, cell);
            }
        }

        // A pricket stand: an iron shaft on three feet with an empty dished tray.
        public static void Pricket(L3Mesh iron, Vector3 at, float height)
        {
            var cell = L4Kit.Cell(L2PolyKit.Iron);
            for (int k = 0; k < 3; k++)
            {
                float a = (k * 120f + 30f) * Mathf.Deg2Rad;
                var foot = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.28f;
                L4Shapes.Shaft(iron, 0, foot, at + Vector3.up * 0.25f, 0.02f, 0.025f, 4, cell);
            }

            L4Shapes.Shaft(iron, 0, at + Vector3.up * 0.2f, at + Vector3.up * height, 0.03f, 0.022f, 6, cell);
            L4Shapes.Lathe(iron, 0, Matrix4x4.TRS(at + Vector3.up * height, Quaternion.identity, Vector3.one), new[] { new Vector2(0f, -0.02f), new Vector2(0.3f, 0f), new Vector2(0.32f, 0.05f), new Vector2(0.28f, 0.05f), new Vector2(0f, 0.02f) }, 10, cell);
        }

        // A low drift of dust and grit against a wall foot: a few flattened faceted lumps.
        public static void Drift(L3Mesh m, Vector3 at, Vector3 along, float length, int seed)
        {
            var rng = new System.Random(seed);
            int lumps = Mathf.Max(2, Mathf.RoundToInt(length / 0.35f));
            for (int i = 0; i < lumps; i++)
            {
                var p = at + along * (length * (i + (float)rng.NextDouble() * 0.5f) / lumps);
                var radii = new Vector3(0.25f + (float)rng.NextDouble() * 0.25f, 0.025f + (float)rng.NextDouble() * 0.04f, 0.2f + (float)rng.NextDouble() * 0.2f);
                m.Ellipsoid(0, p, Quaternion.Euler(0f, rng.Next(360), 0f), radii, 6, 3, L4Kit.Cell(rng.NextDouble() < 0.5 ? L2PolyKit.TopLight : L2PolyKit.Top));
            }
        }
    }
}
