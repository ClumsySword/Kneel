using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Read-back and checks for the L3 level scene (section 15.8), run in edit mode against the open scene, and
    // the captures used to judge it. Nothing here changes the scene except Frame, which moves the player and
    // the camera for a Game-view capture (do not save afterwards; Unframe puts them back).
    public static class L3LevelChecks
    {
        public const string ShotsFolder = "Temp/L3Captures";
        private const float PlayerRadius = 0.2f;

        private static int Obstacles => 1 << L3Build.Layer(L3Build.Boundary);

        private static int Ground => 1 << L3Build.Layer(L3Build.Walkable);

        // ---------------------------------------------------------------- Read-back

        public static string Readback(char stage)
        {
            Physics.SyncTransforms();
            var root = L3Level.Root;
            var sb = new StringBuilder();
            if (root == null)
            {
                return "No L3_Root in the open scene.";
            }

            sb.AppendLine("root at " + root.transform.position + " rot " + root.transform.eulerAngles + " scale " + root.transform.localScale);

            // Floors: collider bounds against the rectangle and height in the plan.
            int floors = 0, floorErrors = 0;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                floors++;
                Transform t = null;
                foreach (var candidate in root.transform.Find("Floors").GetComponentsInChildren<Transform>())
                {
                    if (candidate.name.StartsWith(f.Id + " "))
                    {
                        t = candidate;
                    }
                }

                if (t == null)
                {
                    sb.AppendLine("MISSING floor " + f.Id);
                    floorErrors++;
                    continue;
                }

                Bounds b = t.GetComponent<Collider>().bounds;
                float top = Mathf.Max(f.HSouth, f.HNorth), bottom = Mathf.Min(f.HSouth, f.HNorth) - 0.5f;
                bool ok = Mathf.Abs(b.min.x - f.X0) < 0.02f && Mathf.Abs(b.max.x - f.X1) < 0.02f && Mathf.Abs(b.min.z - f.Z0) < 0.02f && Mathf.Abs(b.max.z - f.Z1) < 0.02f
                    && Mathf.Abs(b.max.y - top) < 0.02f && Mathf.Abs(b.min.y - bottom) < 0.02f && t.gameObject.layer == L3Build.Layer(L3Build.Walkable);
                if (!ok)
                {
                    floorErrors++;
                    sb.AppendLine("floor " + f.Id + " bounds " + b.min + " to " + b.max + ", layer " + LayerMask.LayerToName(t.gameObject.layer));
                }
            }

            sb.AppendLine("floors: " + floors + " read back, " + floorErrors + " off");

            // Placed prefabs: position, source prefab and layer of the root.
            int places = 0, placeErrors = 0;
            foreach (var place in L3Plan.Places)
            {
                if (place.Stage > stage)
                {
                    continue;
                }

                places++;
                var parent = root.transform.Find(place.Group + "/" + place.Area);
                var t = parent != null ? parent.Find(place.Name) : null;
                if (t == null)
                {
                    sb.AppendLine("MISSING " + place.Name);
                    placeErrors++;
                    continue;
                }

                var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                var expected = L3Assets.Load(place.Id);
                if ((t.position - place.Position).magnitude > 0.01f || source != expected || Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y, place.Yaw)) > 0.1f)
                {
                    placeErrors++;
                    sb.AppendLine(place.Name + " at " + t.position + " yaw " + t.eulerAngles.y + " source " + (source != null ? source.name : "none"));
                }
            }

            sb.AppendLine("placed prefabs: " + places + " read back, " + placeErrors + " off");

            // Boundary pieces: the scene against a fresh computation, and every one a prefab instance.
            var planned = L3Level.BoundaryPieces(stage);
            int pieces = 0, loose = 0;
            var byKit = new SortedDictionary<string, int>();
            foreach (Transform area in root.transform.Find("Boundaries"))
            {
                foreach (Transform piece in area)
                {
                    pieces++;
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(piece.gameObject);
                    if (source == null)
                    {
                        loose++;
                        continue;
                    }

                    byKit.TryGetValue(source.name, out int n);
                    byKit[source.name] = n + 1;
                }
            }

            var kits = new List<string>();
            foreach (var pair in byKit)
            {
                kits.Add(pair.Key + " x" + pair.Value);
            }

            sb.AppendLine("boundary pieces: " + pieces + " in scene, " + planned.Count + " planned, " + loose + " not prefab instances (" + string.Join(", ", kits) + ")");

            // Anything under the root that is neither scene geometry nor a prefab instance.
            var strays = new List<string>();
            foreach (var group in new[] { "Boundaries", "Props", "Hazards", "Markers", "Spawns" })
            {
                foreach (Transform area in root.transform.Find(group))
                {
                    foreach (Transform item in area)
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject) == null && item.name != "Trench")
                        {
                            strays.Add(group + "/" + area.name + "/" + item.name);
                        }
                    }
                }
            }

            sb.AppendLine("loose objects outside Floors: " + (strays.Count == 0 ? "none" : string.Join(", ", strays)));

            int missing = 0, pink = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader")
                    {
                        pink++;
                    }
                }
            }

            sb.AppendLine("missing scripts " + missing + ", broken materials " + pink);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Edges: leaks and openings

        // Walks every floor edge with a capsule the player's size held just outside it. An edge sample is fine
        // if something solid stands there or the ground carries on at the same height; otherwise the player
        // could step off the floor there, and the stretch is listed.
        public static string Edges(char stage)
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            int samples = 0, open = 0;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                for (int e = 0; e < f.Poly.Length; e++)
                {
                    Vector2 a = f.Poly[e], b = f.Poly[(e + 1) % f.Poly.Length];
                    Vector2 ab = b - a;
                    float length = ab.magnitude;
                    Vector2 dir = ab / length, outward = new Vector2(dir.y, -dir.x);
                    float runStart = -1f, runEnd = -1f;
                    string runKind = null;
                    for (float s = 0.15f; s < length; s += 0.15f)
                    {
                        Vector2 onEdge = a + dir * s;
                        Vector2 p = onEdge + outward * (PlayerRadius + 0.12f);
                        float h = f.HeightAt(onEdge.y);
                        samples++;
                        string kind = null;
                        bool blocked = Physics.CheckCapsule(new Vector3(p.x, h + 0.45f, p.y), new Vector3(p.x, h + 1.6f, p.y), PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore);
                        // A bank or a rail that stands on the floor itself, just inside the edge, seals it as well.
                        foreach (float inside in new[] { 0.3f, 1.1f })
                        {
                            Vector2 q = onEdge - outward * inside;
                            blocked |= Physics.CheckCapsule(new Vector3(q.x, h + 0.45f, q.y), new Vector3(q.x, h + 0.9f, q.y), PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore);
                        }

                        if (!blocked)
                        {
                            bool ground = Physics.Raycast(new Vector3(p.x, h + 1f, p.y), Vector3.down, out var hit, 30f, Ground | Obstacles, QueryTriggerInteraction.Ignore);
                            if (!ground)
                            {
                                kind = "no ground";
                            }
                            else if (hit.point.y < h - 0.35f)
                            {
                                kind = "drop of " + (h - hit.point.y).ToString("F1") + " m onto " + hit.collider.name;
                            }
                        }

                        if (kind != runKind || s + 0.15f >= length)
                        {
                            if (runKind != null)
                            {
                                Vector2 from = a + dir * runStart, to = a + dir * runEnd;
                                sb.AppendLine("  " + f.Id + " edge " + e + ": open from (" + from.x.ToString("F1") + ", " + from.y.ToString("F1") + ") to (" + to.x.ToString("F1") + ", " + to.y.ToString("F1") + "), " + runKind);
                                open++;
                            }

                            runKind = kind;
                            runStart = s;
                        }

                        runEnd = s;
                    }
                }
            }

            return "edge samples " + samples + ", open stretches " + open + "\n" + sb;
        }

        // Containment, now that boundaries wander off the floor edges: everywhere a body the player's size can
        // walk to from the spawn is flooded on a 0.2 m grid (stepping up 0.3 m, dropping any height), and any
        // step that would put it over nothing at all is a leak. Unseen pads and caps count as what they are:
        // ground and wall. Jumping is not simulated; the caps over low walls are what answer it.
        public static string Flood(char stage)
        {
            Physics.SyncTransforms();
            int unseen = 1 << L3Build.Layer(L3Build.Marker);
            int solid = Obstacles | unseen, ground = Ground | Obstacles | unseen;
            const float cell = 0.2f;
            int nx = Mathf.RoundToInt(L3Plan.GroundWidth / cell), nz = Mathf.RoundToInt(L3Plan.GroundLength / cell);
            var height = new float[nx * nz];
            for (int i = 0; i < height.Length; i++)
            {
                height[i] = float.NaN;
            }

            Vector3 At(int ix, int iz, float y)
            {
                return new Vector3(L3Plan.GroundMinX + (ix + 0.5f) * cell, y, L3Plan.GroundMinZ + (iz + 0.5f) * cell);
            }

            int sx = Mathf.FloorToInt((64f - L3Plan.GroundMinX) / cell), sz = Mathf.FloorToInt((2f - L3Plan.GroundMinZ) / cell);
            if (!Physics.Raycast(At(sx, sz, 2f), Vector3.down, out var first, 10f, ground, QueryTriggerInteraction.Ignore))
            {
                return "no ground at the spawn";
            }

            var queue = new Queue<int>();
            var cameFrom = new int[nx * nz];
            var leakFrom = new List<int>();
            height[sz * nx + sx] = first.point.y;
            queue.Enqueue(sz * nx + sx);
            int reached = 0, offFloor = 0;
            float farthest = 0f;
            Vector3 farthestAt = Vector3.zero;
            var leaks = new List<Vector3>();
            int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int ix = index % nx, iz = index / nx;
                float h = height[index];
                reached++;

                var here = At(ix, iz, h);
                var plan = new Vector2(here.x, here.z);
                float beyond = float.MaxValue;
                foreach (var f in L3Plan.Floors)
                {
                    if (!f.Virtual && f.Stage <= stage)
                    {
                        beyond = Mathf.Min(beyond, f.SignedDistance(plan));
                    }
                }

                if (beyond > 0.05f && h > -1f)
                {
                    offFloor++;
                    if (beyond > farthest && !L3Plan.KeepClearContains(plan, stage) && !L3Plan.OnBlock(plan, stage))
                    {
                        farthest = beyond;
                        farthestAt = here;
                    }
                }

                for (int k = 0; k < 4; k++)
                {
                    int jx = ix + dx[k], jz = iz + dz[k];
                    if (jx < 0 || jz < 0 || jx >= nx || jz >= nz)
                    {
                        continue;
                    }

                    int next = jz * nx + jx;
                    if (!float.IsNaN(height[next]))
                    {
                        continue;
                    }

                    Vector3 there = At(jx, jz, h);
                    // Nothing in the way at body height on this level (a wall, a cap, a hedge)?
                    if (Physics.CheckCapsule(there + Vector3.up * 0.5f, there + Vector3.up * 1.7f, PlayerRadius, solid, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (!Physics.Raycast(there + Vector3.up * 0.45f, Vector3.down, out var hit, 40f, ground, QueryTriggerInteraction.Ignore))
                    {
                        if (leaks.Count < 400)
                        {
                            leaks.Add(there);
                            leakFrom.Add(index);
                        }

                        height[next] = float.NegativeInfinity;
                        continue;
                    }

                    if (hit.point.y > h + 0.32f)
                    {
                        continue;
                    }

                    // Standing room where it lands.
                    Vector3 feet = new Vector3(there.x, hit.point.y, there.z);
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.7f, PlayerRadius, solid, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    height[next] = hit.point.y;
                    cameFrom[next] = index;
                    queue.Enqueue(next);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("flood: " + (reached * cell * cell).ToString("F0") + " m2 reachable from the spawn; " + (offFloor * cell * cell).ToString("F0") + " m2 of it beyond a floor edge, at most " + farthest.ToString("F2")
                + " m beyond (at " + farthestAt.x.ToString("F1") + ", " + farthestAt.z.ToString("F1") + "); steps into the void: " + leaks.Count);
            // Group the leaks so a gap shows as one line.
            var shown = new List<Vector3>();
            foreach (var leak in leaks)
            {
                bool near = false;
                foreach (var other in shown)
                {
                    near |= (other - leak).magnitude < 3f;
                }

                if (!near && shown.Count < 25)
                {
                    shown.Add(leak);
                    sb.AppendLine("  LEAK near (" + leak.x.ToString("F1") + ", " + leak.y.ToString("F1") + ", " + leak.z.ToString("F1") + ")");
                }
            }

            if (leaks.Count > 0)
            {
                // The way the flood got to the first leak: the last place on that route that is on a floor is
                // where it left the level.
                int at = leakFrom[0], start = sz * nx + sx, guard = 0;
                var route = new List<string>();
                bool wasOff = true;
                while (at != start && guard++ < 200000)
                {
                    var q = At(at % nx, at / nx, height[at]);
                    bool on = false;
                    foreach (var f in L3Plan.Floors)
                    {
                        on |= !f.Virtual && f.Stage <= stage && f.Contains(new Vector2(q.x, q.z));
                    }

                    if (on && wasOff && route.Count < 6)
                    {
                        route.Add("(" + q.x.ToString("F1") + ", " + q.y.ToString("F1") + ", " + q.z.ToString("F1") + ")");
                    }

                    wasOff = !on;
                    at = cameFrom[at];
                }

                sb.AppendLine("  route to the first leak left a floor at: " + string.Join(" <- ", route));
            }

            return sb.ToString();
        }

        // Every listed opening must be clear: a capsule carried along its middle line meets nothing solid.
        public static string Openings(char stage)
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            int count = 0, bad = 0;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Openings == null || f.Stage > stage)
                {
                    continue;
                }

                foreach (var o in f.Openings)
                {
                    if (f.Kit(o.Side) == null)
                    {
                        continue;
                    }

                    count++;
                    bool alongX = o.Side == 'S' || o.Side == 'N';
                    float line = o.Side == 'S' ? f.Z0 : o.Side == 'N' ? f.Z1 : o.Side == 'W' ? f.X0 : f.X1;
                    float clearFrom = float.MaxValue, clearTo = float.MinValue;
                    for (float t = o.A + 0.25f; t <= o.B - 0.25f + 0.001f; t += 0.25f)
                    {
                        bool blocked = false;
                        for (float across = -0.75f; across <= 0.76f; across += 0.5f)
                        {
                            Vector3 p = alongX ? new Vector3(t, 0f, line + across) : new Vector3(line + across, 0f, t);
                            blocked |= Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.6f, PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore);
                        }

                        if (!blocked)
                        {
                            clearFrom = Mathf.Min(clearFrom, t - 0.25f);
                            clearTo = Mathf.Max(clearTo, t + 0.25f);
                        }
                    }

                    float clear = clearTo > clearFrom ? clearTo - clearFrom : 0f;
                    bool ok = clear >= (o.B - o.A) - 0.6f;
                    if (!ok)
                    {
                        bad++;
                    }

                    sb.AppendLine("  " + f.Id + " " + o.Side + " " + o.A + "-" + o.B + ": clear " + clear.ToString("F1") + " m" + (ok ? "" : "  <-- BLOCKED"));
                }
            }

            return "openings " + count + ", blocked " + bad + "\n" + sb;
        }

        // The clear width of each 8 m lane between its walls, measured by rays east and west (or north and
        // south for the dyke lane) from the lane's middle.
        public static string LaneWidths(char stage)
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            foreach (var f in L3Plan.Floors)
            {
                if (!f.Lane || f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                float min = float.MaxValue, max = 0f;
                int measured = 0;
                if (f.Diagonal)
                {
                    Vector2 d = f.Dir, n = new Vector2(d.y, -d.x);
                    for (float t = -f.Length * 0.5f + 3f; t <= f.Length * 0.5f - 3f; t += 1f)
                    {
                        Vector2 c = f.Centre + d * t;
                        Measure(new Vector3(c.x, 0.5f, c.y), new Vector3(n.x, 0f, n.y), ref min, ref max, ref measured);
                    }
                }
                else
                {
                    bool eastWest = (f.X1 - f.X0) > (f.Z1 - f.Z0) * 1.5f;
                    if (Mathf.Min(f.X1 - f.X0, f.Z1 - f.Z0) < 3.5f)
                    {
                        continue;
                    }

                    if (eastWest)
                    {
                        continue;
                    }

                    for (float z = f.Z0 + 0.5f; z <= f.Z1 - 0.5f; z += 1f)
                    {
                        Measure(new Vector3((f.X0 + f.X1) * 0.5f, f.HeightAt(z) + 0.5f, z), Vector3.right, ref min, ref max, ref measured);
                    }
                }

                if (measured > 0)
                {
                    sb.AppendLine("  " + f.Id + " " + f.Label + ": " + min.ToString("F2") + " to " + max.ToString("F2") + " m between walls (" + measured + " samples)");
                }
            }

            return sb.ToString();
        }

        private static void Measure(Vector3 origin, Vector3 across, ref float min, ref float max, ref int measured)
        {
            if (Physics.Raycast(origin, across, out var right, 12f, Obstacles, QueryTriggerInteraction.Ignore) && Physics.Raycast(origin, -across, out var left, 12f, Obstacles, QueryTriggerInteraction.Ignore))
            {
                float width = right.distance + left.distance;
                min = Mathf.Min(min, width);
                max = Mathf.Max(max, width);
                measured++;
            }
        }

        // Nothing standing on a floor's south edge may rise more than 1.2 m above the floor.
        public static string SouthEdges(char stage)
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            int checkedEdges = 0, tall = 0;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Diagonal || f.Stage > stage)
                {
                    continue;
                }

                checkedEdges++;
                float h = f.HSouth;
                var centre = new Vector3((f.X0 + f.X1) * 0.5f, h + 1.25f + 3f, f.Z0 - 0.75f);
                var half = new Vector3((f.X1 - f.X0) * 0.5f - 0.05f, 3f, 0.7f);
                foreach (var hit in Physics.OverlapBox(centre, half, Quaternion.identity, Obstacles, QueryTriggerInteraction.Ignore))
                {
                    // A floor that carries on south of this edge is not an edge.
                    if (OnFloor(new Vector2(Mathf.Clamp(hit.bounds.center.x, f.X0, f.X1), f.Z0 - 0.5f), stage, f))
                    {
                        continue;
                    }

                    tall++;
                    sb.AppendLine("  " + f.Id + " south edge: " + hit.transform.root.name + "/" + hit.name + " top at " + hit.bounds.max.y.ToString("F2") + " (floor " + h + ")");
                }
            }

            return "south edges " + checkedEdges + ", pieces above 1.2 m: " + tall + "\n" + sb;
        }

        private static bool OnFloor(Vector2 p, char stage, L3Plan.Floor except)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (f != except && !f.Virtual && f.Stage <= stage && f.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- Stage C and D geometry

        // Stage C: how much walkable ground beside the Wallow's mud is not mud, and whether the farmhouse can be
        // entered anywhere but its door.
        public static string StageC()
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            var mud = L3Level.Root.transform.Find("Hazards/G/Mud_Wallow").GetComponent<Kneel.Hazards.SurfaceVolume>();
            Bounds area = mud.GetComponent<BoxCollider>().bounds;
            sb.AppendLine("mud volume x " + area.min.x.ToString("F1") + "-" + area.max.x.ToString("F1") + ", z " + area.min.z.ToString("F1") + "-" + area.max.z.ToString("F1"));
            float dry = 0f, dryFrom = -1f, dryTo = -1f;
            for (float z = 236f; z <= 256f; z += 0.05f)
            {
                var p = new Vector3(158f, 0f, z);
                bool floor = Physics.Raycast(p + Vector3.up, Vector3.down, 1.3f, Ground, QueryTriggerInteraction.Ignore);
                bool wall = Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.6f, PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore);
                if (floor && !wall && !area.Contains(new Vector3(158f, area.center.y, z)))
                {
                    dry += 0.05f;
                    dryFrom = dryFrom < 0f ? z : dryFrom;
                    dryTo = z;
                }
            }

            sb.AppendLine("walkable ground at x 158 that is not mud: " + dry.ToString("F1") + " m" + (dry > 0.05f ? " (z " + dryFrom.ToString("F1") + " to " + dryTo.ToString("F1") + ")" : ""));

            // Flood from the middle of the house, kept to the house and 0.6 m round it: wherever the flood gets
            // outside the walls is a way in.
            const float cell = 0.2f;
            float x0 = 190f - 0.6f, x1 = 201f + 0.6f, z0 = 266f - 0.6f, z1 = 278f + 0.6f;
            int nx = Mathf.RoundToInt((x1 - x0) / cell), nz = Mathf.RoundToInt((z1 - z0) / cell);
            var seen = new bool[nx, nz];
            var queue = new Queue<Vector2Int>();
            var start = new Vector2Int(Mathf.RoundToInt((195.5f - x0) / cell), Mathf.RoundToInt((272f - z0) / cell));
            queue.Enqueue(start);
            seen[start.x, start.y] = true;
            float outMinZ = float.MaxValue, outMaxZ = float.MinValue, outMinX = float.MaxValue, outMaxX = float.MinValue;
            int outside = 0, inside = 0;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                float x = x0 + (c.x + 0.5f) * cell, z = z0 + (c.y + 0.5f) * cell;
                if (x < 190f || x > 201f || z < 266f || z > 278f)
                {
                    outside++;
                    outMinX = Mathf.Min(outMinX, x);
                    outMaxX = Mathf.Max(outMaxX, x);
                    outMinZ = Mathf.Min(outMinZ, z);
                    outMaxZ = Mathf.Max(outMaxZ, z);
                }
                else
                {
                    inside++;
                }

                foreach (var step in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + step;
                    if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= nz || seen[n.x, n.y])
                    {
                        continue;
                    }

                    seen[n.x, n.y] = true;
                    var p = new Vector3(x0 + (n.x + 0.5f) * cell, 0f, z0 + (n.y + 0.5f) * cell);
                    if (!Physics.CheckCapsule(p + Vector3.up * 0.6f, p + Vector3.up * 1.6f, PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore))
                    {
                        queue.Enqueue(n);
                    }
                }
            }

            sb.AppendLine("farmhouse: interior flood " + (inside * cell * cell).ToString("F0") + " m2; it leaves the walls " + (outside == 0 ? "nowhere" :
                "only between x " + outMinX.ToString("F1") + "-" + outMaxX.ToString("F1") + ", z " + outMinZ.ToString("F1") + "-" + outMaxZ.ToString("F1")));
            return sb.ToString();
        }

        // Stage D: the mill stair. Its tread line from the yard to the landing, what is solid on and beside it,
        // and the unseen walls that keep the player off the rest of the mound.
        public static string StageD()
        {
            Physics.SyncTransforms();
            var sb = new StringBuilder();
            var stair = L3Level.Root.transform.Find("Props/E5/MillStair").position;
            float run = L3Assets.StairSteps * L3Assets.StairGoing;
            foreach (float along in new[] { -0.6f, 0.2f, 1.35f, 2.7f, 4.05f, 5.2f, run + 0.5f, run + 1.7f })
            {
                bool hit = Physics.Raycast(new Vector3(stair.x, 9f, stair.z + along), Vector3.down, out var h, 12f, Ground, QueryTriggerInteraction.Ignore);
                bool clear = !Physics.CheckCapsule(new Vector3(stair.x, h.point.y + 0.45f, stair.z + along), new Vector3(stair.x, h.point.y + 1.75f, stair.z + along), PlayerRadius, Obstacles, QueryTriggerInteraction.Ignore);
                sb.AppendLine("stair, " + along.ToString("F2") + " m from its foot (z " + (stair.z + along).ToString("F1") + "): " + (hit ? "ground at Y " + h.point.y.ToString("F2") : "NO GROUND") + (clear ? ", clear" : ", BLOCKED"));
            }

            string Solid(float x0, float x1, float z0, float z1, float y, int mask)
            {
                var names = new List<string>();
                foreach (var hit in Physics.OverlapBox(new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f), new Vector3((x1 - x0) * 0.5f - 0.02f, 0.8f, (z1 - z0) * 0.5f - 0.02f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                {
                    names.Add(hit.transform.parent != null && hit.transform.parent.name != "E5" ? hit.transform.parent.name + "/" + hit.name : hit.name);
                }

                return names.Count == 0 ? "nothing solid" : string.Join(", ", names);
            }

            float half = L3Assets.StairWidth * 0.5f;
            sb.AppendLine("landing, x " + (stair.x - half) + "-" + (stair.x + half) + ", z " + (stair.z + run) + "-" + (stair.z + run + L3Assets.StairLanding) + ", at head height: " + Solid(stair.x - half + 0.25f, stair.x + half - 0.25f, stair.z + run + 0.25f, stair.z + run + L3Assets.StairLanding - 0.25f, 4.2f, Obstacles));
            sb.AppendLine("west of the landing on the mound top: " + Solid(stair.x - half - 1.4f, stair.x - half - 0.7f, stair.z + run + 0.1f, stair.z + run + 1.9f, 4.2f, ~0));
            sb.AppendLine("east of the landing on the mound top: " + Solid(stair.x + half + 0.7f, stair.x + half + 1.4f, stair.z + run + 0.1f, stair.z + run + 1.9f, 4.2f, ~0));
            sb.AppendLine("yard west of the stair, x 158-" + (stair.x - half - 0.6f) + ", z 326-340: " + Solid(158f, stair.x - half - 0.6f, 326f, 340f, 1f, Obstacles));
            sb.AppendLine("yard east of the stair, x " + (stair.x + half + 0.6f) + "-190, z 326-340: " + Solid(stair.x + half + 0.6f, 190f, 326f, 340f, 1f, Obstacles));
            return sb.ToString();
        }

        // Stage E: counts against 15.5 and 15.6, the three surfaces, and the paths that do not need the gate.
        public static string StageE()
        {
            Physics.SyncTransforms();
            var root = L3Level.Root;
            var sb = new StringBuilder();
            int hounds = 0, brutes = 0, footmen = 0, off = 0, withEnemy = 0;
            var markers = root.transform.Find("Spawns").GetComponentsInChildren<Kneel.Markers.SpawnMarker>(true);
            foreach (var m in markers)
            {
                hounds += m.enemyType == Kneel.Markers.EnemyType.Hound ? 1 : 0;
                brutes += m.enemyType == Kneel.Markers.EnemyType.Brute ? 1 : 0;
                footmen += m.enemyType == Kneel.Markers.EnemyType.Footman ? 1 : 0;
                withEnemy += m.transform.childCount >= 2 ? 1 : 0;
                bool found = false;
                foreach (var s in L3Plan.Spawns)
                {
                    found |= s.Type == m.enemyType && s.Encounter == m.encounterId && Mathf.Abs(s.Position.x - m.transform.position.x) < 0.01f && Mathf.Abs(s.Position.y - m.transform.position.z) < 0.01f
                        && Mathf.Abs(Mathf.DeltaAngle(m.transform.eulerAngles.y, 180f)) < 0.1f;
                }

                off += found ? 0 : 1;
            }

            sb.AppendLine("spawn markers: " + markers.Length + " (" + hounds + " hounds, " + brutes + " brutes, " + footmen + " footmen), " + withEnemy + " with the enemy as a child, " + off + " not matching 15.5");
            sb.AppendLine("markers: " + root.GetComponentsInChildren<Kneel.Markers.PlayerSpawnMarker>(true).Length + " player spawn, " + root.GetComponentsInChildren<Kneel.Markers.ExitMarker>(true).Length + " exit, "
                + root.GetComponentsInChildren<Kneel.Markers.VistaMarker>(true).Length + " vista, " + root.GetComponentsInChildren<Kneel.Markers.ArenaMarker>(true).Length + " arena volumes, "
                + root.GetComponentsInChildren<Kneel.Markers.SecretTell>(true).Length + " secret tells, " + root.GetComponentsInChildren<Kneel.Markers.PickupMarker>(true).Length + " pickups, "
                + root.GetComponentsInChildren<Kneel.Markers.FadeOnEnterMarker>(true).Length + " fade-on-enter");

            foreach (var surface in root.GetComponents<Unity.AI.Navigation.NavMeshSurface>())
            {
                var settings = NavMesh.GetSettingsByID(surface.agentTypeID);
                var so = new SerializedObject(surface);
                var links = so.FindProperty("m_GenerateLinks");
                sb.AppendLine("surface " + NavMesh.GetSettingsNameFromID(surface.agentTypeID) + ": radius " + settings.agentRadius + ", height " + settings.agentHeight + ", layers " + string.Join("+", LayerNames(surface.layerMask))
                    + ", collects " + surface.collectObjects + ", generated links " + (links != null ? links.boolValue.ToString() : "n/a") + ", data " + (surface.navMeshData != null ? AssetDatabase.GetAssetPath(surface.navMeshData) : "none"));
            }

            foreach (var agent in L3Level.Agents)
            {
                float a = PathLength(agent.name, new Vector3(64f, 0.2f, 2f), new Vector3(140f, 3f, 209.5f), out string whyA);
                float b = PathLength(agent.name, new Vector3(126f, 0f, 204f), new Vector3(186f, 0f, 398f), out string whyB);
                float c = PathLength(agent.name, new Vector3(176.3f, 0f, 299.5f), new Vector3(174f, 0f, 314f), out string whyC);
                sb.AppendLine(agent.name + ": spawn to Shrine A " + a.ToString("F1") + " m " + whyA + "; F16 to the exit " + b.ToString("F1") + " m " + whyB + "; Shrine B to E5's south opening " + c.ToString("F1") + " m " + whyC);
            }

            foreach (var (name, x, z) in new[] { ("Mud", 158f, 247f), ("Mud", 146f, 300f), ("CropRow", 101f, 211f), ("CropRow", 111f, 211f) })
            {
                var filter = new NavMeshQueryFilter { agentTypeID = AgentId("Thrall"), areaMask = NavMesh.AllAreas };
                bool ok = NavMesh.SamplePosition(new Vector3(x, 0f, z), out var hit, 1f, filter);
                int area = NavMesh.GetAreaFromName(name);
                sb.AppendLine("area at (" + x + ", " + z + "): " + (ok && (hit.mask & (1 << area)) != 0 ? name : "NOT " + name + " (mask " + (ok ? hit.mask.ToString() : "off mesh") + ")"));
            }

            return sb.ToString();
        }

        private static List<string> LayerNames(LayerMask mask)
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) != 0)
                {
                    names.Add(LayerMask.LayerToName(i));
                }
            }

            return names;
        }

        // ---------------------------------------------------------------- NavMesh

        public static int AgentId(string name)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == name)
                {
                    return id;
                }
            }

            return -1;
        }

        // Path length between two points for one agent type, or -1 when there is no complete path.
        public static float PathLength(string agent, Vector3 from, Vector3 to, out string detail)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = AgentId(agent), areaMask = NavMesh.AllAreas };
            detail = "";
            if (!NavMesh.SamplePosition(from, out var a, 1.5f, filter) || !NavMesh.SamplePosition(to, out var b, 1.5f, filter))
            {
                detail = "an end is off the NavMesh";
                return -1f;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, filter, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                detail = "status " + path.status;
                return -1f;
            }

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += (path.corners[i] - path.corners[i - 1]).magnitude;
            }

            return length;
        }

        // ---------------------------------------------------------------- Captures

        // Straight-down picture of a plan rectangle, without fog and lifted to stand in for the grade (camera
        // renders skip post-processing). For reading the layout, not for judging the look.
        public static string TopDown(string name, float x0, float x1, float z0, float z1, float pixelsPerMetre = 8f)
        {
            int width = Mathf.RoundToInt((x1 - x0) * pixelsPerMetre), height = Mathf.RoundToInt((z1 - z0) * pixelsPerMetre);
            var go = new GameObject("_TopDownCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = (z1 - z0) * 0.5f;
            cam.aspect = (x1 - x0) / (z1 - z0);
            cam.transform.SetPositionAndRotation(new Vector3((x0 + x1) * 0.5f, 120f, (z0 + z1) * 0.5f), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderSettings.fog = fog;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);

            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                pixels[i] = new Color(Mathf.Pow(Mathf.Clamp01(c.r * 2.2f), 0.8f), Mathf.Pow(Mathf.Clamp01(c.g * 2.2f), 0.8f), Mathf.Pow(Mathf.Clamp01(c.b * 2.2f), 0.8f));
            }

            tex.SetPixels(pixels);
            tex.Apply();
            Directory.CreateDirectory(ShotsFolder);
            string path = ShotsFolder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        private static Vector3 playerHome, cameraHome;
        private static Quaternion cameraHomeRotation;
        private static bool framed;

        // Puts the player at a plan point and the camera where PlayerCamera would hold it (full zoom-out), so
        // the Game view shows what the player would see there. Edit mode only; do not save the scene.
        public static string Frame(float x, float z, float distance = 8.5f)
        {
            var player = GameObject.Find("Player");
            var cam = Camera.main;
            if (player == null || cam == null)
            {
                return "No player or camera.";
            }

            if (!framed)
            {
                playerHome = player.transform.position;
                cameraHome = cam.transform.position;
                cameraHomeRotation = cam.transform.rotation;
                framed = true;
            }

            Physics.SyncTransforms();
            float y = Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out var hit, 60f, Ground | Obstacles, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
            player.transform.position = new Vector3(x, y, z);
            var rotation = Quaternion.Euler(65f, 0f, 0f);
            // PlayerCamera looks at a point a little above the feet.
            cam.transform.SetPositionAndRotation(new Vector3(x, y + 1f, z) - rotation * Vector3.forward * distance, rotation);
            return "framed (" + x + ", " + y.ToString("F2") + ", " + z + ")";
        }

        public static string Unframe()
        {
            if (!framed)
            {
                return "not framed";
            }

            GameObject.Find("Player").transform.position = playerHome;
            Camera.main.transform.SetPositionAndRotation(cameraHome, cameraHomeRotation);
            framed = false;
            return "restored";
        }
    }
}
