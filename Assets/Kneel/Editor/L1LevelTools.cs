using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Editor-only helpers for L1 "The Broken Line". Nothing here runs in a build.
    public static class L1LevelTools
    {
        public const string ScenePath = "Assets/Kneel/Scenes/Levels/L1_BrokenLine.unity";
        public const string RootName = "L1_BrokenLine";
        private const string NavMeshAssetPath = "Assets/Kneel/Scenes/Levels/L1_BrokenLine/NavMesh-L1_BrokenLine.asset";
        private const int MaxLightsPerObject = 4; // PC_RPAsset additional lights per object

        public static GameObject Root => GameObject.Find(RootName);

        // ---------------------------------------------------------------- Layers

        [MenuItem("Kneel/L1/Apply Layers")]
        public static void ApplyLayers()
        {
            var root = RequireRoot();
            int ground = LayerMask.NameToLayer("Ground");
            int obstacles = LayerMask.NameToLayer("Obstacles");
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");

            foreach (var t in root.transform.Find("Terrain").GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = ground;
            }

            foreach (var group in new[] { "Boundary", "Landmarks", "SetDressing" })
            {
                foreach (var t in root.transform.Find(group).GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = obstacles;
                }
            }

            // Invisible walls must block movement but never catch the mouse-aim ray.
            root.transform.Find("Boundary/BoundaryColliders").gameObject.layer = ignoreRaycast;

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[L1] Layers applied: Terrain=Ground, dressing=Obstacles, BoundaryColliders=Ignore Raycast.");
        }

        // ---------------------------------------------------------------- Ground snapping

        [MenuItem("Kneel/L1/Snap Dressing to Ground")]
        public static void SnapDressingToGroundMenu()
        {
            Debug.Log("[L1] " + SnapDressingToGround());
        }

        // Drops loose dressing onto the ground mesh and tilts flat pieces to its slope.
        public static string SnapDressingToGround()
        {
            var root = RequireRoot();
            Physics.SyncTransforms();
            int groundMask = 1 << LayerMask.NameToLayer("Ground");
            int moved = 0;

            var targets = new List<Transform>();
            foreach (var path in new[] { "SetDressing/Corpses", "SetDressing/Debris", "SetDressing/Monsters", "SetDressing/Campfires", "SetDressing/Torches" })
            {
                var group = root.transform.Find(path);
                if (group == null)
                {
                    continue;
                }

                foreach (Transform child in group)
                {
                    targets.Add(child);
                }
            }

            foreach (var t in targets)
            {
                Vector3 p = t.position;
                if (!Physics.Raycast(new Vector3(p.x, 50f, p.z), Vector3.down, out var hit, 100f, groundMask))
                {
                    continue;
                }

                // Keep the piece's lift above the ground (weapons lie a few cm up, planted swords sit higher).
                // Dressing was authored on a flat y = 0 plane, so a piece below the ground still carries its
                // authored lift in p.y. Measuring against the current ground keeps repeat runs stable.
                float lift = p.y - hit.point.y;
                if (lift < 0f)
                {
                    lift = Mathf.Max(0f, p.y);
                }

                float target = hit.point.y + lift;
                if (Mathf.Abs(target - p.y) > 0.005f)
                {
                    t.position = new Vector3(p.x, target, p.z);
                    moved++;
                }
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            return $"Snapped {moved} dressing objects to the ground.";
        }

        // ---------------------------------------------------------------- Optimisation

        [MenuItem("Kneel/L1/Optimize Dressing")]
        public static void OptimizeMenu()
        {
            Debug.Log("[L1] " + Optimize());
        }

        // Strips colliders from dressing the player can never reach (the invisible boundary walls
        // already contain them), and gives small pieces a culling LOD so they drop out at distance.
        public static string Optimize()
        {
            var root = RequireRoot();
            int stripped = 0, lods = 0;

            foreach (var path in new[] { "Boundary/Wreckage_Band", "Boundary/Wreckage_Outer", "Boundary/Silhouettes", "SetDressing/DeadTrees" })
            {
                var group = root.transform.Find(path);
                if (group == null)
                {
                    continue;
                }

                foreach (var collider in group.GetComponentsInChildren<Collider>(true))
                {
                    var b = collider.bounds;
                    float nearest = float.MaxValue;
                    foreach (var corner in new[] { b.center, new Vector3(b.min.x, 0f, b.min.z), new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.min.z), new Vector3(b.max.x, 0f, b.max.z) })
                    {
                        nearest = Mathf.Min(nearest, L1Layout.SignedDistance(corner));
                    }

                    if (nearest > 1.5f)
                    {
                        Object.DestroyImmediate(collider);
                        stripped++;
                    }
                }
            }

            foreach (var path in new[] { "SetDressing/Corpses", "SetDressing/Debris", "SetDressing/Monsters", "SetDressing/Standards" })
            {
                var group = root.transform.Find(path);
                if (group == null)
                {
                    continue;
                }

                foreach (Transform piece in group)
                {
                    var renderers = piece.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0 || piece.GetComponentInChildren<Animation>() != null)
                    {
                        continue;
                    }

                    var lod = piece.GetComponent<LODGroup>();
                    if (lod == null)
                    {
                        lod = piece.gameObject.AddComponent<LODGroup>();
                    }

                    lod.SetLODs(new[] { new LOD(0.012f, renderers) });
                    lod.RecalculateBounds();
                    lods++;
                }
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            return $"Optimized: {stripped} unreachable colliders removed, {lods} culling LOD groups.";
        }

        // ---------------------------------------------------------------- NavMesh + validation

        [MenuItem("Kneel/L1/Rebake NavMesh + Validate")]
        public static void RebakeAndValidateMenu()
        {
            Debug.Log("[L1] " + RebakeNavMesh());
            Debug.Log("[L1] " + Validate());
        }

        public static string RebakeNavMesh()
        {
            var root = RequireRoot();
            var surface = root.GetComponent<NavMeshSurface>();
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

        [MenuItem("Kneel/L1/Validate Only")]
        public static void ValidateMenu()
        {
            Debug.Log("[L1] " + Validate());
        }

        public static string Validate()
        {
            var root = RequireRoot();
            Physics.SyncTransforms();
            var report = new StringBuilder();
            var gameplay = root.transform.Find("_Gameplay");

            // 1. Every marker reachable on the NavMesh from PlayerStart.
            NavMesh.SamplePosition(gameplay.Find("PlayerStart").position, out var startHit, 2f, NavMesh.AllAreas);
            int reachable = 0;
            var unreachable = new List<string>();
            foreach (var t in gameplay.GetComponentsInChildren<Transform>())
            {
                if (t == gameplay || t.name == "ArenaTrigger" || t.GetComponent<Light>() != null || t.GetComponentInParent<CharacterController>() != null)
                {
                    continue;
                }

                var path = new NavMeshPath();
                bool onMesh = NavMesh.SamplePosition(t.position, out var hit, 2f, NavMesh.AllAreas);
                if (onMesh && NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    reachable++;
                }
                else
                {
                    unreachable.Add(t.name);
                }
            }

            report.Append($"Paths {reachable}/{reachable + unreachable.Count}");
            if (unreachable.Count > 0)
            {
                report.Append(" (UNREACHABLE: " + string.Join(", ", unreachable) + ")");
            }

            report.Append(". ");

            // 2. Arena floors: NavMesh coverage, no props, no fog, no warm lights.
            var renderers = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is ParticleSystemRenderer) && !r.name.StartsWith("L1_Ground"))
                {
                    renderers.Add(r);
                }
            }

            foreach (var arena in L1Layout.Arenas)
            {
                int on = 0, total = 0, props = 0, lights = 0;
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
                        if (NavMesh.SamplePosition(new Vector3(arena.Center.x + x, 0f, arena.Center.y + z), out _, 0.25f, NavMesh.AllAreas))
                        {
                            on++;
                        }
                    }
                }

                foreach (var rend in renderers)
                {
                    var c = rend.bounds.center;
                    if ((new Vector2(c.x, c.z) - arena.Center).magnitude < arena.Radius - 2.5f)
                    {
                        props++;
                    }
                }

                foreach (var light in root.GetComponentsInChildren<Light>())
                {
                    if (light.type != LightType.Point || light.name.StartsWith("ArenaRim"))
                    {
                        continue;
                    }

                    var lp = light.transform.position;
                    if ((new Vector2(lp.x, lp.z) - arena.Center).magnitude < arena.Radius - 1f)
                    {
                        lights++;
                    }
                }

                report.Append($"{arena.Name}: nav {100 * on / Mathf.Max(1, total)}%, props {props}, warmLights {lights}; ");
            }

            // 3. Ground fog never drifts onto an arena floor.
            float worstFog = 0f;
            var fogRoot = root.transform.Find("_Lighting/GroundFog");
            if (fogRoot != null)
            {
                foreach (var ps in fogRoot.GetComponentsInChildren<ParticleSystem>())
                {
                    worstFog = Mathf.Max(worstFog, FogIntrusion(ps));
                }
            }

            report.Append($"Fog intrusion {worstFog:F2} m. ");

            // 4. Readability: gameplay camera sight-lines to the player and the enemy spawns at every zoom level and full edge-pan.
            int clear = 0, tests = 0;
            var blockers = new Dictionary<string, int>();
            foreach (var arena in L1Layout.Arenas)
            {
                var encounter = gameplay.Find("Encounter_" + arena.Name);
                var spots = new List<Vector3> { encounter.position };
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f * Mathf.Deg2Rad;
                    spots.Add(encounter.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * arena.Radius * (k % 2 == 0 ? 0.55f : 0.85f));
                }

                foreach (float distance in new[] { L1Layout.CameraMinDistance, L1Layout.CameraDistance, L1Layout.CameraMaxDistance })
                {
                    foreach (var pan in new[] { Vector3.zero, new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f) })
                    {
                        Vector3 edge = pan * L1Layout.CameraMaxEdgeOffset * (distance / L1Layout.CameraMaxDistance);
                        foreach (var spot in spots)
                        {
                            var cam = L1Layout.CameraPose(spot, distance, edge).position;
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
                                    blockers[arena.Name + ":" + blocker] = blockers.TryGetValue(arena.Name + ":" + blocker, out var n) ? n + 1 : 1;
                                }
                            }
                        }

                        foreach (Transform spawn in encounter)
                        {
                            if (!spawn.name.StartsWith("EnemySpawn"))
                            {
                                continue;
                            }

                            var cam = L1Layout.CameraPose(encounter.position, distance, edge).position;
                            tests++;
                            if (FirstOccluder(renderers, cam, spawn.position + Vector3.up * 1.5f) == null)
                            {
                                clear++;
                            }
                        }
                    }
                }
            }

            report.Append($"Readability {clear}/{tests} sight-lines clear");
            if (blockers.Count > 0)
            {
                report.Append(" (blockers: ");
                foreach (var kv in blockers)
                {
                    report.Append(kv.Key + " x" + kv.Value + ", ");
                }

                report.Append(")");
            }

            report.Append(". ");

            // 5. No walkable point touched by more than the URP per-object light limit.
            var pointLights = new List<Light>();
            foreach (var light in root.GetComponentsInChildren<Light>())
            {
                if (light.type == LightType.Point && light.enabled && light.gameObject.activeInHierarchy)
                {
                    pointLights.Add(light);
                }
            }

            int worstLights = 0;
            Vector3 worstAt = Vector3.zero;
            for (float x = -30f; x <= 40f; x += 1f)
            {
                for (float z = -8f; z <= 376f; z += 1f)
                {
                    var p = new Vector2(x, z);
                    if (L1Layout.SignedDistance(p) > 0.5f)
                    {
                        continue;
                    }

                    int count = 0;
                    foreach (var light in pointLights)
                    {
                        // A 1 m object at p is lit if the light's range reaches its bounds.
                        if (Vector3.Distance(light.transform.position, new Vector3(x, 0.9f, z)) < light.range + 0.9f)
                        {
                            count++;
                        }
                    }

                    if (count > worstLights)
                    {
                        worstLights = count;
                        worstAt = new Vector3(x, 0f, z);
                    }
                }
            }

            report.Append($"Max point lights on one spot: {worstLights} (limit {MaxLightsPerObject}) at {worstAt}.");
            return report.ToString();
        }

        private static string FirstOccluder(List<Renderer> renderers, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            var ray = new Ray(from, dir / dist);

            foreach (var r in renderers)
            {
                if (!r.bounds.IntersectRay(ray, out float d) || d > dist - 0.3f)
                {
                    continue;
                }

                // Bounds are conservative; confirm against the actual mesh.
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

        private static float FogIntrusion(ParticleSystem ps)
        {
            var particles = new ParticleSystem.Particle[ps.main.maxParticles];
            float worst = 0f;
            for (int step = 0; step < 6; step++)
            {
                ps.Simulate(5f, true, step == 0, true);
                int n = ps.GetParticles(particles);
                for (int i = 0; i < n; i++)
                {
                    var p = new Vector2(particles[i].position.x, particles[i].position.z);
                    float radius = particles[i].GetCurrentSize(ps) * 0.35f;
                    foreach (var arena in L1Layout.Arenas)
                    {
                        worst = Mathf.Max(worst, arena.Radius - ((p - arena.Center).magnitude - radius));
                    }
                }
            }

            return worst;
        }

        private static GameObject RequireRoot()
        {
            var root = Root;
            if (root == null)
            {
                throw new System.InvalidOperationException("Open " + ScenePath + " first.");
            }

            return root;
        }
    }
}
