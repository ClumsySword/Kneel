using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.EditorTools
{
    // The sanctuary: a round, flooded hall (the cutscene's wide shot, 05-Art/Prologue Cutscene Prompts.md): a
    // 40-sided outer wall 38 m across, a ring of twenty clustered pillars 11 m to their capitals carrying pointed
    // arches (owner, 2026-11-23: bigger, "very grand"), and the octagonal altar in the middle under a cold beam from
    // above. Under the gameplay camera (on +x, looking down towards -x):
    //  - Up-screen (x < 0) the wall and the pillars stand full height; on the camera side they are cut down (the
    //    wall to 0.9 m, the pillars to broken stumps), since only those could ever stand between the camera and Kael.
    //    For any point inside the ring, a pillar at the same z on the up-screen half is always further up-screen.
    //  - Behind the up-screen pillars, low stone screens close the ambulatory: walking behind a pillar would put it
    //    between him and the camera.
    //  - Where the wall faces along the level (near the door and the far end) it steps down before x reaches the
    //    camera's side, as the corridor's cross walls do.
    public static partial class L1Monastery
    {
        public static readonly Vector2 HallCentre = new Vector2(0f, -52.5f);
        public const float HallApothem = 19f;      // centre to the wall's inner face (the door segment's is z -33.5)
        public const int HallSides = 40;
        public const float HallHigh = 11f;
        public const float ArcadeRadius = 16.2f;
        public const int PillarCount = 20;
        public const float PillarHigh = 11f;        // to the top of the capital, where the arches spring
        public const float PillarScale = 1.5f;

        public static Vector3 HallPoint(float angle, float radius, float y = 0f)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector3(HallCentre.x + Mathf.Cos(a) * radius, y, HallCentre.y + Mathf.Sin(a) * radius);
        }

        // Segment k of the outer wall is centred on angle 90 + k * 360 / sides (k 0 is the inner door, towards +z).
        public static float SegmentAngle(int k) => 90f + k * 360f / HallSides;

        // Pillar k stands either side of the door axis, so the way in runs between two of them.
        public static float PillarAngle(int k) => 90f + 180f / PillarCount + k * 360f / PillarCount;

        // Full height up-screen, cut down towards the camera side; the wall steps down as the cross walls do.
        private static float HallWallHeight(float x) => StepHeight(x, HallHigh, -7f, -3.2f);

        public static bool PillarIsTall(int k) => HallPoint(PillarAngle(k), ArcadeRadius).x < -4f;

        private static float SegmentLength => 2f * (HallApothem + Wall) * Mathf.Tan(Mathf.PI / HallSides);

        // The lancets: every other segment well up-screen.
        private static bool SegmentHasWindow(int k)
        {
            return k % 2 == 0 && HallPoint(SegmentAngle(k), HallApothem).x < -9f;
        }

        private static readonly L1MonasteryKit.Opening HallWindow = new L1MonasteryKit.Opening(0f, 0.7f, 1.3f, 5.8f, true);

        // ---------------------------------------------------------------- Structure

        private static void Hall(Transform root)
        {
            var rng = new System.Random(110);
            float length = SegmentLength;
            for (int k = 0; k < HallSides; k++)
            {
                float angle = SegmentAngle(k);
                var mid = HallPoint(angle, HallApothem);
                var inward = (new Vector3(HallCentre.x, 0f, HallCentre.y) - mid).normalized;
                var along = Vector3.Cross(Vector3.up, inward);
                var frame = WallFrame(root, "Wall_" + k, mid - along * (length * 0.5f), inward);
                float h = HallWallHeight(mid.x);
                var openings = new List<L1MonasteryKit.Opening>();
                if (k == 0)
                {
                    openings.Add(L1MonasteryKit.Opening.Gap(length * 0.5f, DoorHalf));
                }
                else if (SegmentHasWindow(k))
                {
                    var o = HallWindow;
                    o.U = length * 0.5f;
                    openings.Add(o);
                }

                bool low = h < 3f;
                L1MonasteryKit.Wall(frame, length, Wall, _ => h, openings.ToArray(), low ? 0.1f : 0.15f, 1000 + k, low ? 3f : 2f);
            }

            // The arcade: clustered pillars, tall up-screen with pointed arches between, broken stumps elsewhere.
            var tall = new bool[PillarCount];
            for (int k = 0; k < PillarCount; k++)
            {
                tall[k] = PillarIsTall(k);
                var at = HallPoint(PillarAngle(k), ArcadeRadius);
                if (tall[k])
                {
                    L1MonasteryKit.Pier(root, at, PillarHigh, rng.Next(100000), 0f, PillarScale);
                }
                else
                {
                    float cut = 0.8f + (float)rng.NextDouble() * 0.9f;
                    L1MonasteryKit.Pier(root, at, PillarHigh, rng.Next(100000), cut, PillarScale);
                    // Its fallen drums, lying out towards the camera side, off the way through.
                    var outward = (at - new Vector3(HallCentre.x, 0f, HallCentre.y)).normalized;
                    for (int d = 0; d < 2; d++)
                    {
                        var p = at + outward * (1.3f + d * 0.9f) + Vector3.Cross(Vector3.up, outward) * (((float)rng.NextDouble() - 0.5f) * 1.2f);
                        var drum = new L3Mesh();
                        var axis = Quaternion.Euler(0f, rng.Next(360), 0f) * Vector3.right;
                        L4Shapes.Shaft(drum, 0, p - axis * 0.3f + Vector3.up * 0.42f, p + axis * 0.3f + Vector3.up * 0.42f, 0.43f, 0.43f, 8, L4Kit.Cell(L2PolyKit.Top));
                        L3Build.MeshChild(root, "Drum", drum.ToMesh("Drum"), Vector3.zero, L4Kit.Stone);
                    }
                }
            }

            for (int k = 0; k < PillarCount; k++)
            {
                int n = (k + 1) % PillarCount;
                if (!tall[k] || !tall[n])
                {
                    continue;
                }

                var a = HallPoint(PillarAngle(k), ArcadeRadius);
                var b = HallPoint(PillarAngle(n), ArcadeRadius);
                var mid = (a + b) * 0.5f;
                var along = (b - a).normalized;
                var outward = (mid - new Vector3(HallCentre.x, 0f, HallCentre.y)).normalized;
                float span = (b - a).magnitude;
                var o = new L1MonasteryKit.Opening(0f, span * 0.5f - 0.62f * PillarScale, 0f, PillarHigh, false, 0.42f);
                var arch = new L3Mesh();
                L1MonasteryKit.ArchRing(arch, o, -0.35f * PillarScale, 0.35f * PillarScale, rng);
                var archGo = L3Build.MeshChild(root, "Arch", arch.ToMesh("Arch"), Vector3.zero, L4Kit.Stone);
                archGo.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(-outward, Vector3.up));
                // A low screen between the pillars closes the ambulatory behind them.
                var screen = WallFrame(root, "Screen_" + k, a + along * (0.55f * PillarScale) + outward * 0.3f, -outward);
                L1MonasteryKit.Wall(screen, span - 1.1f * PillarScale, 0.6f, _ => Low, null, 0.25f, 1200 + k, 2f);
            }

            int end = 0;
            foreach (var (from, to) in AmbulatoryEnds())
            {
                var dir = (to - from).normalized;
                var sideways = Vector3.Cross(Vector3.up, dir);
                var screen = WallFrame(root, "AmbulatoryEnd_" + end, from - sideways * 0.3f, -sideways);
                L1MonasteryKit.Wall(screen, (to - from).magnitude, 0.6f, _ => Low, null, 0.25f, 1300 + end++, 2f);
            }
        }

        // The closed ambulatory's two ends: from the first and last tall pillars out to the wall, so he can't walk
        // in behind them from the open (camera-side) ambulatory.
        private static IEnumerable<(Vector3 from, Vector3 to)> AmbulatoryEnds()
        {
            for (int k = 0; k < PillarCount; k++)
            {
                int prev = (k + PillarCount - 1) % PillarCount, next = (k + 1) % PillarCount;
                if (PillarIsTall(k) && (!PillarIsTall(prev) || !PillarIsTall(next)))
                {
                    yield return (HallPoint(PillarAngle(k), ArcadeRadius + 0.55f * PillarScale), HallPoint(PillarAngle(k), HallApothem + 0.2f));
                }
            }
        }

        // ---------------------------------------------------------------- Floor, water, windows, dressing

        // The water: a 1 m grid over the round floor and into the door's threshold.
        private static void HallSheet(List<Vector3> verts, List<Vector2> uv0, List<Vector2> uv1, List<int> tris)
        {
            float r = HallApothem + 0.4f;
            int n = Mathf.CeilToInt(2f * r);
            var index = new Dictionary<(int, int), int>();
            int Vertex(int i, int j)
            {
                if (index.TryGetValue((i, j), out int v))
                {
                    return v;
                }

                var p = new Vector3(HallCentre.x - r + i, WaterY, HallCentre.y - r + j);
                verts.Add(p);
                uv0.Add(new Vector2(p.x, p.z));
                uv1.Add(Vector2.zero);
                index[(i, j)] = verts.Count - 1;
                return verts.Count - 1;
            }

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    var c = new Vector2(-r + i + 0.5f, -r + j + 0.5f);
                    bool door = Mathf.Abs(c.x) < DoorHalf + 0.7f && c.y > 0f;
                    if (c.magnitude > r && !(door && c.y < HallApothem + 1.2f))
                    {
                        continue;
                    }

                    int a = Vertex(i, j), b = Vertex(i + 1, j), d = Vertex(i, j + 1), e = Vertex(i + 1, j + 1);
                    tris.AddRange(new[] { a, d, b, b, d, e });
                }
            }
        }

        private static void HallPanes(L3Mesh glass, L3Mesh iron)
        {
            for (int k = 0; k < HallSides; k++)
            {
                if (!SegmentHasWindow(k))
                {
                    continue;
                }

                float angle = SegmentAngle(k);
                var mid = HallPoint(angle, HallApothem);
                var inward = (new Vector3(HallCentre.x, 0f, HallCentre.y) - mid).normalized;
                AddPane(glass, iron, mid, inward, HallWindow, Wall * 0.55f);
            }
        }

        private static void HallDressing(Transform root, System.Random rng)
        {
            // Fallen stones under the cut-down camera-side wall.
            for (int k = 0; k < HallSides; k++)
            {
                var mid = HallPoint(SegmentAngle(k), HallApothem - 0.6f);
                if (mid.x < 4f || rng.NextDouble() < 0.6)
                {
                    continue;
                }

                for (int i = 0; i < 2 + rng.Next(2); i++)
                {
                    L4Kit.Fallen(root, mid + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.8f, 0f, ((float)rng.NextDouble() - 0.5f) * 1.2f), rng);
                }
            }
        }

        // ---------------------------------------------------------------- Light

        // Cold light: a beam from above onto the altar (the brightest thing in the hall, as in the cutscene), the
        // lancets' light falling in over the screens, and a soft fill. None of these cast shadows: a dozen shadowed
        // spots overfilled URP's shadow atlas and their shadows snapped on and off.
        private static void HallLights(Transform root, Color cold, Color fill)
        {
            var altar = new Vector3(HallCentre.x, 0f, HallCentre.y);
            Spot(root, "Oculus", altar + Vector3.up * 30f + Vector3.left * 2.5f, altar + Vector3.up * 0.8f, cold, 2000f, 46f, 28f, false);
            Shaft(root, "OculusShaft", altar + new Vector3(-1.5f, 18f, 0f), altar + Vector3.up * 0.5f, 5.2f, Mat("ShaftOculus"));
            for (int k = 0; k < HallSides; k++)
            {
                if (!SegmentHasWindow(k))
                {
                    continue;
                }

                float angle = SegmentAngle(k);
                var window = HallPoint(angle, HallApothem + 0.6f, 3.4f);
                var pool = HallPoint(angle, ArcadeRadius - 3.2f);
                Spot(root, "Window", window + (window - pool).normalized * 6f, pool, cold, 70f, 24f, 22f, false);
                Shaft(root, "Shaft", HallPoint(angle, HallApothem - 0.2f, 4.6f), pool + Vector3.up * 0.1f, 1.3f, Mat("ShaftCold"));
            }

            foreach (var angle in new[] { 150f, 210f, 90f, 270f })
            {
                Point(root, "FillHall", HallPoint(angle, 10f, 10f), fill, 9f, 22f);
            }
        }

        // ---------------------------------------------------------------- Colliders

        private static void HallColliders(Transform solids, Transform caps, Transform floors)
        {
            float length = SegmentLength;
            for (int k = 0; k < HallSides; k++)
            {
                float angle = SegmentAngle(k);
                var mid = HallPoint(angle, HallApothem + Wall * 0.5f);
                var inward = (new Vector3(HallCentre.x, 0f, HallCentre.y) - mid).normalized;
                var rot = Quaternion.LookRotation(inward, Vector3.up);
                float h = HallWallHeight(HallPoint(angle, HallApothem).x);
                if (k == 0)
                {
                    // Either side of the inner door.
                    float jamb = (length * 0.5f - DoorHalf);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var at = mid + rot * Vector3.right * side * (DoorHalf + jamb * 0.5f);
                        RotatedBox(solids, "DoorJamb", at, rot, new Vector3(jamb, h, Wall), "Obstacles");
                        RotatedBox(caps, "Cap", at + Vector3.up * 1f, rot, new Vector3(jamb, 5f, Wall), "Ignore Raycast");
                    }

                    continue;
                }

                RotatedBox(solids, "HallWall", mid, rot, new Vector3(length, h, Wall), "Obstacles");
                if (h < 3f)
                {
                    RotatedBox(caps, "Cap", mid + Vector3.up * 1f, rot, new Vector3(length, 5f, Wall), "Ignore Raycast");
                }
            }

            var rng = new System.Random(110);
            for (int k = 0; k < PillarCount; k++)
            {
                var at = HallPoint(PillarAngle(k), ArcadeRadius);
                bool tall = PillarIsTall(k);
                float h = tall ? PillarHigh : 1.8f;
                // Round, like the pillar (its base's radius).
                var pillar = new GameObject("Pillar");
                pillar.transform.SetParent(solids, false);
                pillar.transform.position = at;
                pillar.layer = L3Build.Layer("Obstacles");
                var capsule = pillar.AddComponent<CapsuleCollider>();
                capsule.radius = 0.62f * PillarScale;
                capsule.height = Mathf.Max(h, 2f * capsule.radius);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                int n = (k + 1) % PillarCount;
                if (tall && PillarIsTall(n))
                {
                    var a = at;
                    var b = HallPoint(PillarAngle(n), ArcadeRadius);
                    var outward = (((a + b) * 0.5f) - new Vector3(HallCentre.x, 0f, HallCentre.y)).normalized;
                    var screenRot = Quaternion.LookRotation(-outward, Vector3.up);
                    var centre = (a + b) * 0.5f + outward * 0.6f;
                    RotatedBox(solids, "Screen", centre, screenRot, new Vector3((b - a).magnitude, Low, 0.6f), "Obstacles");
                    RotatedBox(caps, "Cap", centre + Vector3.up * 1f, screenRot, new Vector3((b - a).magnitude, 5f, 0.6f), "Ignore Raycast");
                }
            }

            foreach (var (from, to) in AmbulatoryEnds())
            {
                var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, (to - from).normalized), Vector3.up);
                var endSize = new Vector3((to - from).magnitude + 0.4f, Low, 0.6f);
                RotatedBox(solids, "AmbulatoryEnd", (from + to) * 0.5f, rot, endSize, "Obstacles");
                RotatedBox(caps, "Cap", (from + to) * 0.5f + Vector3.up * 1f, rot, new Vector3(endSize.x, 5f, 0.6f), "Ignore Raycast");
            }

            // The round floor (a square under it; its corners lie outside the wall).
            float size = 2f * (HallApothem + Wall);
            FloorBox(floors, "Hall", HallCentre.x - size * 0.5f, HallCentre.x + size * 0.5f, HallCentre.y - size * 0.5f, HallCentre.y + size * 0.5f);
        }

        // A box collider standing on 'at' (its base), turned by 'rot'.
        private static void RotatedBox(Transform parent, string name, Vector3 at, Quaternion rot, Vector3 size, string layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, rot);
            go.layer = L3Build.Layer(layer);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
            box.size = size;
        }
    }
}
