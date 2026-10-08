using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Editor-only helpers for L2 "The Hollow Village": layers, NavMesh bake, validation and review captures.
    public static class L2LevelTools
    {
        private const string NavMeshAssetPath = L2Build.SceneFolder + "/NavMesh-L2_HollowVillage.asset";
        private const string ShotsFolder = "Screenshots/L2_HollowVillage";

        // ---------------------------------------------------------------- Layers

        [MenuItem("Kneel/L2/Apply Layers")]
        public static void ApplyLayersMenu()
        {
            ApplyLayers();
            Debug.Log("[L2] Layers applied.");
        }

        public static void ApplyLayers()
        {
            var root = L2Build.RequireRoot().transform;
            int ground = LayerMask.NameToLayer("Ground");
            int obstacles = LayerMask.NameToLayer("Obstacles");
            int ignore = LayerMask.NameToLayer("Ignore Raycast");

            SetLayer(root.Find("Terrain"), ground);
            foreach (var group in new[] { "Boundary", "Landmarks", "SetDressing" })
            {
                SetLayer(root.Find(group), obstacles);
            }

            // Invisible walls and triggers must never catch the mouse-aim ray.
            SetLayer(root.Find("Boundary/BoundaryColliders"), ignore);

            // Occlusion-fade probes keep their own layer (sight-line tests only; nothing collides with them).
            int probe = LayerMask.NameToLayer(Kneel.OcclusionFade.ProbeLayer);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "FadeProbe")
                {
                    t.gameObject.layer = probe;
                }
            }
            var gameplay = root.Find("_Gameplay");
            if (gameplay != null)
            {
                foreach (var c in gameplay.GetComponentsInChildren<Collider>(true))
                {
                    c.gameObject.layer = ignore;
                }
            }

            L2Build.MarkDirty();
        }

        private static void SetLayer(Transform t, int layer)
        {
            if (t == null)
            {
                return;
            }

            foreach (var c in t.GetComponentsInChildren<Transform>(true))
            {
                c.gameObject.layer = layer;
            }
        }

        // ---------------------------------------------------------------- NavMesh

        [MenuItem("Kneel/L2/Rebake NavMesh + Validate")]
        public static void RebakeAndValidateMenu()
        {
            Debug.Log("[L2] " + RebakeNavMesh());
            Debug.Log("[L2] " + Validate());
        }

        [MenuItem("Kneel/L2/Validate Only")]
        public static void ValidateMenu()
        {
            Debug.Log("[L2] " + Validate());
        }

        public static string RebakeNavMesh()
        {
            var root = L2Build.RequireRoot();
            Physics.SyncTransforms();
            var surface = root.GetComponent<NavMeshSurface>();
            surface.layerMask = ~(1 << LayerMask.NameToLayer(Kneel.OcclusionFade.ProbeLayer));   // fade probes are not geometry
            surface.BuildNavMesh();

            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            }

            surface.RemoveData();
            surface.AddData();
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene);

            var triangulation = NavMesh.CalculateTriangulation();
            return $"NavMesh baked: {triangulation.indices.Length / 3} triangles.";
        }

        // ---------------------------------------------------------------- NavMesh audit

        [MenuItem("Kneel/L2/NavMesh Audit")]
        public static void NavAuditMenu()
        {
            Debug.Log("[L2] " + NavAudit());
        }

        // Every walkable spot (0.5 m grid, away from the street edges) must sit on the NavMesh at the right height,
        // except where something stands on purpose (blockers, the closed gate, archer cover, square stalls, the
        // well). Holes are reported with the colliders that cause them. Then the route is walked: every 3 m of it
        // must be reachable from the start.
        public static string NavAudit()
        {
            var root = L2Build.RequireRoot().transform;
            Physics.SyncTransforms();
            int checkedSpots = 0, holes = 0;
            var causes = new Dictionary<string, int>();
            var holeSpots = new List<Vector2>();
            var deliberate = new List<Collider>();
            foreach (var path in new[] { "SetDressing/Blockers", "Landmarks/Gates", "SetDressing/Cover", "SetDressing/Squares", "Landmarks/SetPieces" })
            {
                var t = root.Find(path);
                if (t != null)
                {
                    deliberate.AddRange(t.GetComponentsInChildren<Collider>());
                }
            }

            int probe = LayerMask.NameToLayer(Kneel.OcclusionFade.ProbeLayer);
            int mask = ~((1 << probe) | (1 << LayerMask.NameToLayer("Ignore Raycast")) | (1 << LayerMask.NameToLayer("Ground")));
            for (float x = -104f; x < 70f; x += 0.5f)
            {
                for (float z = -72f; z < 150f; z += 0.5f)
                {
                    var p = new Vector2(x, z);
                    if (L2Layout.SignedDistance(p) > -0.6f)
                    {
                        continue;
                    }

                    float h = L2Layout.Height(p);
                    var world = new Vector3(p.x, h, p.y);
                    bool nearDeliberate = false;
                    foreach (var c in deliberate)
                    {
                        if (c.enabled && c.bounds.SqrDistance(world + Vector3.up * 0.5f) < 0.9f * 0.9f)
                        {
                            nearDeliberate = true;
                            break;
                        }
                    }

                    if (nearDeliberate)
                    {
                        continue;
                    }

                    checkedSpots++;
                    if (NavMesh.SamplePosition(world, out var hit, 0.6f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - h) < 0.45f
                        && new Vector2(hit.position.x - p.x, hit.position.z - p.y).magnitude < 0.3f)
                    {
                        continue;
                    }

                    holes++;
                    holeSpots.Add(p);
                    var near = Physics.OverlapSphere(world + Vector3.up * 0.6f, 0.9f, mask, QueryTriggerInteraction.Ignore);
                    if (near.Length == 0)
                    {
                        Count(causes, $"(no collider) near ({p.x:F0},{p.y:F0})");
                    }

                    foreach (var c in near)
                    {
                        var t = c.transform;
                        string name = t.parent != null ? t.parent.name + "/" + t.name : t.name;
                        Count(causes, name);
                    }
                }
            }

            // The route, every 3 m, reachable from the start.
            var start = Onto(L2Layout.Route[0].P);
            int routeSpots = 0, unreachable = 0;
            var lost = new StringBuilder();
            var navPath = new NavMeshPath();
            for (int i = 1; i < L2Layout.Route.Length; i++)
            {
                var a = L2Layout.Route[i - 1];
                var b = L2Layout.Route[i];
                if (a.Break)
                {
                    continue;
                }

                float length = (b.P - a.P).magnitude;
                for (float s = 0f; s < length; s += 3f)
                {
                    var p = Vector2.Lerp(a.P, b.P, s / length);
                    routeSpots++;
                    if (!NavMesh.CalculatePath(start, Onto(p), NavMesh.AllAreas, navPath) || navPath.status != NavMeshPathStatus.PathComplete)
                    {
                        unreachable++;
                        if (unreachable <= 6)
                        {
                            lost.Append($"({p.x:F0},{p.y:F0}) ");
                        }
                    }
                }
            }

            var sb = new StringBuilder($"NavMesh audit: {checkedSpots - holes}/{checkedSpots} walkable spots on the NavMesh ({holes} holes)");
            if (holes > 0)
            {
                sb.Append(". Causes: ");
                foreach (var kv in causes.OrderByDescending(kv => kv.Value).Take(15))
                {
                    sb.Append(kv.Key).Append(" x").Append(kv.Value).Append(", ");
                }

                sb.Append(" first holes at: ");
                foreach (var h in holeSpots.Take(10))
                {
                    sb.Append($"({h.x:F1},{h.y:F1}) ");
                }
            }

            // Containment: the countryside past the walls has NavMesh too (islands nobody can reach); a leak is
            // NavMesh outside the streets that IS reachable from the start (a gap in the boundary).
            var tri = NavMesh.CalculateTriangulation();
            int leaks = 0;
            var leakAt = new StringBuilder();
            var seen = new HashSet<Vector2Int>();
            foreach (var v in tri.vertices)
            {
                float outside = L2Layout.SignedDistance(new Vector2(v.x, v.z));
                if (outside > 0.9f && outside < 10f && seen.Add(new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.z)))
                    && NavMesh.CalculatePath(start, v, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete)
                {
                    leaks++;
                    if (leaks <= 6)
                    {
                        leakAt.Append($"({v.x:F0},{v.z:F0}) ");
                    }
                }
            }

            sb.Append($". Containment: {(leaks == 0 ? "no reachable NavMesh outside the streets" : $"{leaks} reachable NavMesh spots outside the streets at {leakAt}")}");
            sb.Append($". Route: {routeSpots - unreachable}/{routeSpots} points reachable from the start");
            if (unreachable > 0)
            {
                sb.Append(" (lost at ").Append(lost).Append(')');
            }

            return sb.Append('.').ToString();
        }

        // ---------------------------------------------------------------- Validation

        public static string Validate()
        {
            var root = L2Build.RequireRoot().transform;
            Physics.SyncTransforms();
            var report = new StringBuilder();
            var gameplay = root.Find("_Gameplay");
            NavMesh.SamplePosition(gameplay.Find("PlayerStart").position, out var startHit, 2f, NavMesh.AllAreas);
            var start = startHit.position;

            // 1. Every marker reachable from PlayerStart.
            int reachable = 0;
            var unreachable = new List<string>();
            foreach (var t in gameplay.GetComponentsInChildren<Transform>())
            {
                if (t == gameplay || t.name == "ArenaTrigger" || t.name == "Hazards" || t.parent.name == "Hazards")
                {
                    continue;
                }

                if (PathLength(start, t.position, out _) >= 0f)
                {
                    reachable++;
                }
                else
                {
                    unreachable.Add(t.name);
                }
            }

            report.Append($"Markers reachable {reachable}/{reachable + unreachable.Count}");
            report.Append(unreachable.Count > 0 ? " (UNREACHABLE: " + string.Join(", ", unreachable) + "). " : ". ");

            // 2. Blocked routes stay blocked: no path through, or only the long way round.
            foreach (var b in L2Layout.Blockers)
            {
                report.Append(b.Name).Append(' ').Append(BlockedVerdict(b.Near, b.Far)).Append("; ");
            }

            // 3. The church stair works both ways: down into E4 and back up to the churchyard.
            var e4 = Onto(L2Layout.Areas[7].Center);
            var churchyard = Onto(L2Layout.StairTopProbe);
            bool down = PathLength(churchyard, e4, out _) >= 0f;
            bool up = PathLength(e4, churchyard, out _) >= 0f;
            report.Append($"Church stair: down {(down ? "ok" : "BROKEN")}, up {(up ? "ok" : "BROKEN")}. ");

            // 4. Each gate (closed) seals its street: every spot across it at chest height is inside the gate
            //    leaf's or the gatehouse's colliders. (The baked NavMesh runs through; the gate carves it at runtime.)
            Physics.SyncTransforms();
            foreach (var g in L2Layout.Gates)
            {
                var gateRoot = root.Find("Landmarks/Gates/" + g.Name);
                int closedSpots = 0, across = 0;
                for (float t = -3.5f; t <= 3.5f; t += 0.25f)
                {
                    var q = g.Center + g.Along * t;
                    if (L2Layout.SignedDistance(q) > 0.2f)
                    {
                        continue;
                    }

                    across++;
                    var hits = Physics.OverlapSphere(new Vector3(q.x, L2Layout.Height(q) + 1f, q.y), 0.3f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                    {
                        if (gateRoot != null && h.transform.IsChildOf(gateRoot))
                        {
                            closedSpots++;
                            break;
                        }
                    }
                }

                var gateObstacle = gateRoot != null ? gateRoot.Find("Gate")?.GetComponent<NavMeshObstacle>() : null;
                report.Append($"{g.Name}: seals {closedSpots}/{across} spots across the street{(gateObstacle != null && gateObstacle.carving ? ", carves the NavMesh while shut" : ", NO NAVMESH OBSTACLE")}. ");
            }

            // 5. The weapon alley: reachable, and only through the fire gap.
            var street = Onto(new Vector2(22f, 71f));
            var weapon = Onto(L2Layout.Markers["Loot_WeaponUpgrade"]);
            float alley = PathLength(street, weapon, out var alleyPath);
            L2Layout.FireZone gap = default;
            foreach (var z in L2Layout.FireZones)
            {
                if (z.Name == "DZ_WeaponGap")
                {
                    gap = z;
                }
            }

            bool throughFire = alley >= 0f && PathCrosses(alleyPath, gap);
            report.Append($"Weapon alley: {(alley < 0f ? "UNREACHABLE" : throughFire ? "reached through the fire gap (ok)" : "REACHED AROUND THE FIRE")}. ");

            // 6. Arenas: NavMesh coverage and blocking props.
            var renderers = SolidRenderers(root);
            var cover = root.Find("SetDressing/Cover");
            foreach (var arena in L2Layout.Arenas)
            {
                int on = 0, total = 0, props = 0, coverPieces = 0;
                float r = arena.Radius - 0.8f;
                for (float x = -r; x <= r; x += 0.5f)
                {
                    for (float z = -r; z <= r; z += 0.5f)
                    {
                        if (x * x + z * z > r * r)
                        {
                            continue;
                        }

                        total++;
                        var p = new Vector2(arena.Center.x + x, arena.Center.y + z);
                        if (NavMesh.SamplePosition(new Vector3(p.x, L2Layout.Height(p), p.y), out _, 0.4f, NavMesh.AllAreas))
                        {
                            on++;
                        }
                    }
                }

                foreach (var rend in renderers)
                {
                    var c = rend.bounds.center;
                    if ((new Vector2(c.x, c.z) - arena.Center).magnitude < arena.Radius - 2.5f && Blocks(rend))
                    {
                        // Archer cover is placed in the fight on purpose; everything else is clutter.
                        if (cover != null && rend.transform.IsChildOf(cover))
                        {
                            coverPieces++;
                        }
                        else
                        {
                            props++;
                        }
                    }
                }

                report.Append($"{arena.Name}: nav {100 * on / Mathf.Max(1, total)}%, props {props}{(coverPieces > 0 ? $", cover renderers {coverPieces}" : "")}; ");
            }

            // 7. Readability: sight-lines from the gameplay camera to the player (arenas at every zoom and edge-pan,
            //    and the route every 3 m at default and max zoom).
            report.Append(Readability(root, renderers));

            // 8. Point/spot lights per walkable spot.
            report.Append(LightCount(root));
            return report.ToString();
        }

        private static string BlockedVerdict(Vector2 near, Vector2 far)
        {
            var a = Onto(near);
            var b = Onto(far);
            float straight = (b - a).magnitude;
            float length = PathLength(a, b, out _);
            if (length < 0f)
            {
                return "blocked (no path)";
            }

            return length > straight * 3f + 15f ? $"blocked (only {length:F0} m round vs {straight:F0} m across)" : $"LEAKS ({length:F0} m vs {straight:F0} m)";
        }

        private static Vector3 Onto(Vector2 p)
        {
            var guess = new Vector3(p.x, L2Layout.Height(p), p.y);
            return NavMesh.SamplePosition(guess, out var hit, 2.5f, NavMesh.AllAreas) ? hit.position : guess;
        }

        // Complete path length, or -1.
        private static float PathLength(Vector3 from, Vector3 to, out Vector3[] corners)
        {
            corners = null;
            if (!NavMesh.SamplePosition(to, out var hit, 2f, NavMesh.AllAreas))
            {
                return -1f;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return -1f;
            }

            corners = path.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += (corners[i] - corners[i - 1]).magnitude;
            }

            return length;
        }

        private static bool PathCrosses(Vector3[] corners, L2Layout.FireZone zone)
        {
            var inverse = Quaternion.Euler(0f, -zone.Yaw, 0f);
            for (int i = 1; i < corners.Length; i++)
            {
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    var p = Vector3.Lerp(corners[i - 1], corners[i], t);
                    var local = inverse * new Vector3(p.x - zone.Center.x, 0f, p.z - zone.Center.y);
                    if (Mathf.Abs(local.x) <= zone.Size.x * 0.5f && Mathf.Abs(local.z) <= zone.Size.y * 0.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Solid geometry only: particles, the ground and lighting cards never occlude.
        public static List<Renderer> SolidRenderers(Transform root)
        {
            var list = new List<Renderer>();
            var lighting = root.Find("_Lighting");
            var terrain = root.Find("Terrain");
            var fire = root.Find("SetDressing/FireBeds");
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || (lighting != null && r.transform.IsChildOf(lighting)) || (terrain != null && r.transform.IsChildOf(terrain)) || (fire != null && r.transform.IsChildOf(fire)))
                {
                    continue;
                }

                list.Add(r);
            }

            return list;
        }

        private static bool Blocks(Renderer rend)
        {
            float floor = L2Layout.Height(new Vector2(rend.bounds.center.x, rend.bounds.center.z));
            if (rend.bounds.max.y - rend.bounds.min.y > 0.9f && rend.bounds.max.y > floor + 0.9f)
            {
                return true;
            }

            foreach (var col in rend.GetComponentsInParent<Collider>())
            {
                if (col.enabled && !col.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Readability(Transform root, List<Renderer> renderers)
        {
            int clear = 0, tests = 0;
            var blockers = new Dictionary<string, int>();
            var yaw = Quaternion.Euler(0f, L2Layout.CameraYaw, 0f);
            var pans = new[] { Vector3.zero, yaw * Vector3.forward, yaw * Vector3.right, yaw * Vector3.left };

            foreach (var arena in L2Layout.Arenas)
            {
                var spots = new List<Vector3> { Feet(arena.Center) };
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f * Mathf.Deg2Rad;
                    spots.Add(Feet(arena.Center + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * arena.Radius * (k % 2 == 0 ? 0.55f : 0.85f)));
                }

                foreach (float distance in new[] { L2Layout.CameraMinDistance, L2Layout.CameraDistance })
                {
                    foreach (var pan in pans)
                    {
                        Vector3 edge = pan * L2Layout.CameraMaxEdgeOffset * (distance / L2Layout.CameraMaxDistance);
                        foreach (var spot in spots)
                        {
                            var cam = L2Layout.CameraPose(spot, distance, edge).position;
                            foreach (float h in new[] { 1.2f, 1.8f })
                            {
                                tests++;
                                string blocker = FirstOccluder(renderers, cam, spot + Vector3.up * h);
                                if (blocker == null)
                                {
                                    clear++;
                                }
                                else
                                {
                                    Count(blockers, arena.Name + ":" + blocker);
                                }
                            }
                        }
                    }
                }
            }

            // The route and every branch, every 3 m.
            var polylines = new List<L2Layout.Node[]> { L2Layout.Route };
            polylines.AddRange(L2Layout.Branches.Values);
            foreach (var nodes in polylines)
            {
                for (int i = 1; i < nodes.Length; i++)
                {
                    if (nodes[i - 1].Break)
                    {
                        continue;
                    }

                    float length = (nodes[i].P - nodes[i - 1].P).magnitude;
                    for (float s = 0f; s < length; s += 3f)
                    {
                        var p = Vector2.Lerp(nodes[i - 1].P, nodes[i].P, s / length);
                        var spot = Feet(p);
                        foreach (float distance in new[] { L2Layout.CameraDistance })
                        {
                            tests++;
                            string blocker = FirstOccluder(renderers, L2Layout.CameraPose(spot, distance).position, spot + Vector3.up * 1.2f, true);
                            if (blocker == null)
                            {
                                clear++;
                            }
                            else
                            {
                                Count(blockers, $"Path({p.x:F0},{p.y:F0}):{blocker}");
                            }
                        }
                    }
                }
            }

            var sb = new StringBuilder($"Readability {clear}/{tests} sight-lines clear");
            if (blockers.Count > 0)
            {
                sb.Append(" (blockers: ");
                foreach (var kv in blockers)
                {
                    sb.Append(kv.Key).Append(" x").Append(kv.Value).Append(", ");
                }

                sb.Append(')');
            }

            return sb.Append(". ").ToString();
        }

        private static void Count(Dictionary<string, int> counts, string key)
        {
            counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        private static Vector3 Feet(Vector2 p)
        {
            return L2Build.Ground(p, L2Layout.Height(p));
        }

        // On the route, scenery that fades out of the camera's way (OcclusionFade) doesn't count; in the fights it
        // does, because it fades for the player only and must never hide an enemy's telegraph.
        private static string FirstOccluder(List<Renderer> renderers, Vector3 from, Vector3 to, bool ignoreFading = false)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            var ray = new Ray(from, dir / dist);
            foreach (var r in renderers)
            {
                if (!r.bounds.IntersectRay(ray, out float d) || d > dist - 0.3f || (ignoreFading && r.GetComponentInParent<Kneel.OcclusionFade>() != null))
                {
                    continue;
                }

                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var probe = r.gameObject.AddComponent<MeshCollider>();
                probe.sharedMesh = filter.sharedMesh;
                Physics.SyncTransforms();
                bool hit = probe.Raycast(ray, out _, dist - 0.3f);
                Object.DestroyImmediate(probe);
                if (hit)
                {
                    return r.transform.parent != null ? r.transform.parent.name + "/" + r.name : r.name;
                }
            }

            return null;
        }

        private static string LightCount(Transform root)
        {
            var lights = new List<Light>();
            foreach (var light in root.GetComponentsInChildren<Light>())
            {
                if ((light.type == LightType.Point || light.type == LightType.Spot) && light.enabled && (light.renderingLayerMask & 1) != 0)
                {
                    lights.Add(light);
                }
            }

            int worst = 0;
            Vector2 worstAt = Vector2.zero;
            for (float x = L2Layout.GroundMinX; x < L2Layout.GroundMinX + L2Layout.GroundWidth; x += 1f)
            {
                for (float z = L2Layout.GroundMinZ; z < L2Layout.GroundMinZ + L2Layout.GroundLength; z += 1f)
                {
                    var p = new Vector2(x, z);
                    if (L2Layout.SignedDistance(p) > 0.5f)
                    {
                        continue;
                    }

                    var at = new Vector3(x, L2Layout.Height(p) + 0.9f, z);
                    int n = 0;
                    foreach (var l in lights)
                    {
                        if ((l.transform.position - at).magnitude < l.range + 0.9f)
                        {
                            n++;
                        }
                    }

                    if (n > worst)
                    {
                        worst = n;
                        worstAt = p;
                    }
                }
            }

            int realtime = 0;
            foreach (var l in root.GetComponentsInChildren<Light>())
            {
                if (l.enabled && l.lightmapBakeType != LightmapBakeType.Baked)
                {
                    realtime++;
                }
            }

            return $"Lights: {realtime} realtime; max point/spot on one spot {worst} at ({worstAt.x:F0},{worstAt.y:F0}).";
        }

        // ---------------------------------------------------------------- Layout report

        [MenuItem("Kneel/L2/Report Layout")]
        public static void ReportMenu()
        {
            Debug.Log("[L2] " + LayoutReport());
        }

        public static string LayoutReport()
        {
            float longest = 0f;
            string at = "";
            var route = L2Layout.Route;
            for (int i = 1; i < route.Length; i++)
            {
                float d = (route[i].P - route[i - 1].P).magnitude;
                if (d > longest)
                {
                    longest = d;
                    at = $"({route[i - 1].P.x:F0},{route[i - 1].P.y:F0})";
                }
            }

            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (var w in L2Layout.Walks)
            {
                min = Vector2.Min(min, Vector2.Min(w.A, w.B) - Vector2.one * w.HalfWidth);
                max = Vector2.Max(max, Vector2.Max(w.A, w.B) + Vector2.one * w.HalfWidth);
            }

            float length = L2Layout.RouteLength();
            return $"Route {length:F0} m (~{length / 1.44f / 60f:F1} min at walk speed), longest leg {longest:F1} m at {at}, " +
                   $"walkable footprint {max.x - min.x:F0} x {max.y - min.y:F0} m, {L2Layout.Walks.Count} walk pieces.";
        }

        // ---------------------------------------------------------------- Captures

        // Renders the Main Camera exactly where PlayerCamera would put it (post-processing on), with the player at the focus.
        public static string Capture(string name, Vector2 focus, float distance, Vector3 edge = default, int width = 1280, int height = 720)
        {
            var cam = Camera.main;
            var feet = Feet(focus);
            var pose = L2Layout.CameraPose(feet, distance, edge);
            var player = GameObject.Find("Player");
            Vector3 home = player != null ? player.transform.position : Vector3.zero;
            if (player != null)
            {
                player.transform.position = feet;
            }

            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            string path = Render(cam, name, width, height);
            if (player != null)
            {
                player.transform.position = home;
            }

            return path;
        }

        // Straight-down orthographic overview of a world rectangle.
        public static string CaptureTopDown(string name, Rect area, int width = 1600)
        {
            var go = new GameObject("_TopDownCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = area.height * 0.5f;
            cam.aspect = area.width / area.height;
            cam.transform.SetPositionAndRotation(new Vector3(area.center.x, 150f, area.center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
            string path = Render(cam, name, width, Mathf.RoundToInt(width * area.height / area.width));
            Object.DestroyImmediate(go);
            return path;
        }

        private static string Render(Camera cam, string name, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);

            Directory.CreateDirectory(ShotsFolder);
            string path = ShotsFolder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }
    }
}
