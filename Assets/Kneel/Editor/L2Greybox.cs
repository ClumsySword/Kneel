using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools
{
    // Builds the L2 "The Hollow Village" scene skeleton from L2Layout: terrain, boundary, lots, blockers,
    // fire beds, the shortcut gate, gameplay markers, player and camera, NavMesh surface and links.
    // Every step clears and rebuilds only its own group, so running it again never duplicates anything.
    // Dressing (L2Dressing) replaces the lot boxes; the lot boxes stay as the source of truth for plots.
    public static class L2Greybox
    {
        private const string BlockoutPath = L2Build.MaterialsPath + "/Blockout";
        private const float ChunkCells = 64;
        private const float BoundaryStep = 1.6f;

        [MenuItem("Kneel/L2/Greybox/Build Scene")]
        public static void BuildSceneMenu()
        {
            Debug.Log("[L2] " + BuildScene());
        }

        public static string BuildScene()
        {
            EnsureFolders();
            OpenOrCreateScene();
            var root = EnsureRoot();
            var log = new List<string>();

            log.Add(BuildTerrain(root.transform));
            log.Add(L2Creek.BuildDeck(root.transform));
            Physics.SyncTransforms();
            log.Add(BuildBoundary(root.transform));
            log.Add(BuildLots(root.transform));
            log.Add(BuildBlockers(root.transform));
            log.Add(BuildFire(root.transform));
            log.Add(BuildGate(root.transform));
            Physics.SyncTransforms();
            log.Add(BuildMarkers(root.transform));
            log.Add(EnsurePlayerAndCamera());
            log.Add(EnsureBaseLighting(root.transform));
            L2LevelTools.ApplyLayers();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            return "Greybox built. " + string.Join(" | ", log);
        }

        // ---------------------------------------------------------------- Scene and hierarchy

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { L2Build.SceneFolder, L2Build.MaterialsPath, BlockoutPath, L2Build.PrefabsPath, L2Build.MeshesPath, L2Build.SettingsPath })
            {
                L2Build.EnsureFolder(folder);
            }
        }

        private static void OpenOrCreateScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == L2Build.ScenePath)
            {
                return;
            }

            if (active.isDirty)
            {
                throw new System.InvalidOperationException("Save or discard the changes in " + active.path + " first.");
            }

            if (File.Exists(L2Build.ScenePath))
            {
                EditorSceneManager.OpenScene(L2Build.ScenePath, OpenSceneMode.Single);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, L2Build.ScenePath);
        }

        private static GameObject EnsureRoot()
        {
            var root = L2Build.Root;
            if (root == null)
            {
                root = new GameObject(L2Build.RootName);
            }

            if (root.GetComponent<NavMeshSurface>() == null)
            {
                var surface = root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            }

            // Same NavMesh policy as L1: lighting, markers and the far backdrop are ignored, dressing is never walkable.
            foreach (var (name, ignore) in new[] { ("_Lighting", true), ("_Gameplay", true), ("Terrain", false), ("Boundary", false), ("Landmarks", false), ("SetDressing", false), ("Background", true) })
            {
                var group = L2Build.Group(root.transform, name, false);
                if (name == "Terrain")
                {
                    continue;
                }

                var modifier = group.GetComponent<NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = group.gameObject.AddComponent<NavMeshModifier>();
                }

                modifier.applyToChildren = true;
                modifier.ignoreFromBuild = ignore;
                modifier.overrideArea = !ignore;
                modifier.area = 1;
            }

            return root;
        }

        // ---------------------------------------------------------------- Materials

        public static Material Blockout(string name)
        {
            string path = BlockoutPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            string l1 = L1Build.MaterialsPath + "/Blockout/" + name + ".mat";
            if (File.Exists(l1))
            {
                AssetDatabase.CopyAsset(l1, path);
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            // Greybox fire: a flat emissive orange, obviously not final.
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", L1Build.Hex("#C8501E"));
            mat.SetColor("_EmissionColor", L1Build.Hex("#FF6A20") * 1.6f);
            mat.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- Terrain

        public static string BuildTerrain(Transform root)
        {
            var terrain = L2Build.Group(root, "Terrain", true);
            float cell = L2Layout.GroundCell;
            int nx = Mathf.CeilToInt(L2Layout.GroundWidth / cell);
            int nz = Mathf.CeilToInt(L2Layout.GroundLength / cell);
            var heights = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            {
                for (int j = 0; j <= nz; j++)
                {
                    heights[i, j] = L2Layout.TerrainHeight(new Vector2(L2Layout.GroundMinX + i * cell, L2Layout.GroundMinZ + j * cell));
                }
            }

            L2Build.EnsureFolder(L2Build.MeshesPath + "/Terrain");
            var material = L2Build.Mat("L2_Ground") ?? Blockout("BLK_Ground");
            int chunks = 0;
            int step = (int)ChunkCells;
            for (int ci = 0; ci < nx; ci += step)
            {
                for (int cj = 0; cj < nz; cj += step)
                {
                    int cx = Mathf.Min(step, nx - ci);
                    int cz = Mathf.Min(step, nz - cj);
                    var mesh = ChunkMesh(heights, ci, cj, cx, cz, cell);
                    string name = $"L2_Ground_{ci / step:00}_{cj / step:00}";
                    mesh = SaveMesh(mesh, L2Build.MeshesPath + "/Terrain/" + name + ".asset");

                    var go = new GameObject(name);
                    go.transform.SetParent(terrain, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                    L1Build.SetStatic(go, L1Build.EnvironmentStatic);
                    chunks++;
                }
            }

            L1Build.SetLayer(terrain.gameObject, "Ground");
            AssetDatabase.SaveAssets();
            return $"terrain {nx}x{nz} cells in {chunks} chunks";
        }

        private static Mesh ChunkMesh(float[,] heights, int i0, int j0, int cx, int cz, float cell)
        {
            var verts = new Vector3[(cx + 1) * (cz + 1)];
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i <= cx; i++)
            {
                for (int j = 0; j <= cz; j++)
                {
                    float x = L2Layout.GroundMinX + (i0 + i) * cell;
                    float z = L2Layout.GroundMinZ + (j0 + j) * cell;
                    verts[i * (cz + 1) + j] = new Vector3(x, heights[i0 + i, j0 + j], z);
                    uvs[i * (cz + 1) + j] = new Vector2((x - L2Layout.GroundMinX) / L2Layout.GroundWidth, (z - L2Layout.GroundMinZ) / L2Layout.GroundLength);
                }
            }

            var tris = new int[cx * cz * 6];
            int k = 0;
            for (int i = 0; i < cx; i++)
            {
                for (int j = 0; j < cz; j++)
                {
                    int v00 = i * (cz + 1) + j, v01 = v00 + 1, v10 = v00 + cz + 1, v11 = v10 + 1;
                    tris[k++] = v00; tris[k++] = v01; tris[k++] = v11;
                    tris[k++] = v00; tris[k++] = v11; tris[k++] = v10;
                }
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            mesh.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetTangents(mesh.tangents);
            existing.SetUVs(0, mesh.uv);
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Boundary

        // Invisible walls just outside every walkable edge, except where the path is meant to drop away.
        public static string BuildBoundary(Transform root)
        {
            var boundary = L2Build.Group(root, "Boundary", false);
            var group = L2Build.Group(boundary, "BoundaryColliders", true);
            var placed = new List<Vector2>();
            int count = 0;

            foreach (var w in L2Layout.Walks)
            {
                foreach (var (edge, normal, height) in Outline(w))
                {
                    Vector2 q = edge + normal * 0.45f;
                    if (L2Layout.SignedDistance(q) < 0.3f || Near(placed, q, 1.1f))
                    {
                        continue;
                    }

                    // At a concave join of two street pieces a full-width box would reach into the next piece's
                    // walkable space: narrow it until its inner corners stay outside every street.
                    // If narrowing isn't enough, the full box steps back from the street instead (no gap opens).
                    Vector2 tangent = new Vector2(-normal.y, normal.x);
                    float width = 1.9f;
                    bool Intrudes(Vector2 c, float w) => L2Layout.SignedDistance(c - normal * 0.25f + tangent * w * 0.5f) < 0.05f
                                                         || L2Layout.SignedDistance(c - normal * 0.25f - tangent * w * 0.5f) < 0.05f;
                    while (width > 1.2f && Intrudes(q, width))
                    {
                        width -= 0.35f;
                    }

                    if (Intrudes(q, width))
                    {
                        width = 1.9f;
                        for (int k = 0; k < 8 && Intrudes(q, width); k++)
                        {
                            q += normal * 0.2f;
                        }

                        // Still cutting into the next street (a cap set at an angle to it): leave the slot to the
                        // seal pass below, whose small posts never reach in.
                        if (Intrudes(q, width))
                        {
                            continue;
                        }
                    }

                    placed.Add(q);
                    var go = new GameObject("Wall_" + count++);
                    go.transform.SetParent(group, false);
                    go.transform.position = new Vector3(q.x, height + 1.25f, q.y);
                    go.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(normal.x, normal.y) * Mathf.Rad2Deg, 0f);
                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(width, 4.5f, 0.5f);
                }
            }

            // Seal: sweep the band just outside every street and fill any spot no wall covers (concave corners
            // where two streets meet leave slots between their rim walls).
            Physics.SyncTransforms();
            int fillers = 0;
            for (float x = -112f; x < 76f; x += 0.4f)
            {
                for (float z = -78f; z < 156f; z += 0.4f)
                {
                    var p = new Vector2(x, z);
                    float sd = L2Layout.SignedDistance(p);
                    if (sd < 0.3f || sd > 0.75f)
                    {
                        continue;
                    }

                    float h = L2Layout.Height(p);
                    bool covered = false;
                    foreach (var c in Physics.OverlapSphere(new Vector3(p.x, h + 1.25f, p.y), 0.45f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (c.transform.parent == group)
                        {
                            covered = true;
                            break;
                        }
                    }

                    if (covered)
                    {
                        continue;
                    }

                    var go = new GameObject("Seal_" + fillers++);
                    go.transform.SetParent(group, false);
                    go.transform.position = new Vector3(p.x, h + 1.25f, p.y);
                    go.AddComponent<BoxCollider>().size = new Vector3(0.7f, 4.5f, 0.7f);
                    Physics.SyncTransforms();
                }
            }

            count += fillers;

            return $"boundary {count} walls";
        }

        // Points along a walkable piece's rim, with the outward normal and the path height there.
        internal static IEnumerable<(Vector2, Vector2, float)> Outline(L2Layout.Walk w)
        {
            float r = w.HalfWidth;
            Vector2 ab = w.B - w.A;
            float length = ab.magnitude;
            if (length < 0.01f)
            {
                int n = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r / BoundaryStep));
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var normal = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    yield return (w.A + normal * r, normal, w.HA);
                }

                yield break;
            }

            Vector2 dir = ab / length;
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (float s = 0f; s <= length; s += BoundaryStep)
            {
                float t = s / length;
                float h = Mathf.Lerp(w.HA, w.HB, t);
                yield return (w.A + dir * s + side * r, side, h);
                yield return (w.A + dir * s - side * r, -side, h);
            }

            int caps = Mathf.Max(4, Mathf.CeilToInt(Mathf.PI * r / BoundaryStep));
            for (int i = 0; i <= caps; i++)
            {
                float a = i * Mathf.PI / caps;
                Vector2 outB = dir * Mathf.Sin(a) + side * Mathf.Cos(a);
                yield return (w.B + outB * r, outB, w.HB);
                yield return (w.A - outB * r, -outB, w.HA);
            }
        }

        private static bool Near(List<Vector2> points, Vector2 q, float radius)
        {
            foreach (var p in points)
            {
                if ((p - q).sqrMagnitude < radius * radius)
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- Lots

        // Plots along every street edge. Far-side plots may stand tall; plots on the camera side (south of a path)
        // are capped by the camera line and become low ruins. Key lots (burning houses, church, weapon block) first.
        public static string BuildLots(Transform root)
        {
            var dressing = L2Build.Group(root, "SetDressing", false);
            var group = L2Build.Group(dressing, "Lots", true);
            var landmarks = L2Build.Group(root, "Landmarks", false);
            var church = L2Build.Group(landmarks, "Church", true);
            var lots = new List<L2Layout.Lot>(L2Layout.KeyLots);
            var rng = new System.Random(2);
            int auto = 0;

            foreach (var w in L2Layout.Walks)
            {
                foreach (var (edge, normal, _) in Outline(w))
                {
                    if (rng.NextDouble() < 0.2)
                    {
                        continue; // the odd gap between houses; the overlap check does the rest
                    }

                    // Village streets are lined almost wall to wall, fronts close to the path.
                    float width = 4.6f + (float)rng.NextDouble() * 2.4f;
                    float depth = 4.8f + (float)rng.NextDouble() * 1.8f;
                    Vector2 c = edge + normal * (1.0f + depth * 0.5f);
                    float yaw = Mathf.Atan2(-normal.x, -normal.y) * Mathf.Rad2Deg;
                    var lot = new L2Layout.Lot("Lot_" + auto, c.x, c.y, width, depth, yaw, L2Layout.LotKind.Tall);
                    if (!Fits(lot, lots))
                    {
                        continue;
                    }

                    // Tall houses fade out of the camera's way, so only the camera-side rims of fights stay low.
                    lot.Kind = MinArenaCap(lot) >= 5.4f ? L2Layout.LotKind.Tall : L2Layout.LotKind.Low;
                    lots.Add(lot);
                    auto++;
                }
            }

            int tall = 0, low = 0;
            foreach (var lot in lots)
            {
                if (lot.Kind == L2Layout.LotKind.Church)
                {
                    BuildChurchGreybox(church, lot);
                    continue;
                }

                float height = lot.Kind == L2Layout.LotKind.Tall ? 4.6f : lot.Kind == L2Layout.LotKind.Low || lot.Kind == L2Layout.LotKind.BurningLow ? 1.0f : 4.2f;
                string mat = lot.Kind == L2Layout.LotKind.Tall ? "BLK_Wall" : lot.Kind == L2Layout.LotKind.Low ? "BLK_WallLow" : "BLK_Fire";
                float ground = LotGround(lot);
                var box = L2Build.Box(lot.Name + "_" + lot.Kind, group, new Vector3(lot.Center.x, ground + height * 0.5f - 0.3f, lot.Center.y),
                    new Vector3(lot.Size.x, height + 0.6f, lot.Size.y), lot.Yaw, Blockout(mat));
                L1Build.SetStatic(box, L1Build.EnvironmentStatic);
                if (lot.Kind == L2Layout.LotKind.Tall)
                {
                    tall++;
                }
                else if (lot.Kind == L2Layout.LotKind.Low)
                {
                    low++;
                }
            }

            return $"lots {lots.Count} ({tall} tall, {low} low, {L2Layout.KeyLots.Length} key)";
        }

        // Lot positions are saved on the lot boxes' names and transforms, which L2Dressing reads back.
        public static IEnumerable<(L2Layout.Lot lot, Transform box)> ReadLots(Transform root)
        {
            var group = root.Find("SetDressing/Lots");
            if (group == null)
            {
                yield break;
            }

            foreach (Transform t in group)
            {
                int split = t.name.LastIndexOf('_');
                var kind = (L2Layout.LotKind)System.Enum.Parse(typeof(L2Layout.LotKind), t.name.Substring(split + 1));
                yield return (new L2Layout.Lot(t.name.Substring(0, split), t.position.x, t.position.z, t.localScale.x, t.localScale.z, t.eulerAngles.y, kind), t);
            }
        }

        private static bool Fits(L2Layout.Lot lot, List<L2Layout.Lot> existing)
        {
            foreach (var corner in Corners(lot, 0f))
            {
                // Houses keep back from a fight's rim, so no lot box ever stands in the arena's sight-lines.
                if (L2Layout.SignedDistance(corner) < 0.75f || Reserved(corner) || L2Layout.IsInArena(corner, 2.5f))
                {
                    return false;
                }
            }

            if (L2Layout.SignedDistance(lot.Center) < 2.3f || Reserved(lot.Center))
            {
                return false;
            }

            foreach (var other in existing)
            {
                if ((other.Center - lot.Center).magnitude < 3.2f || Overlaps(lot, other))
                {
                    return false;
                }
            }

            return true;
        }

        // Areas that are never auto-filled with houses: the churchyard plateau (graves, not houses), the hill,
        // the creek, the gates, the shrine's nook, the weapon block and the fields at the level's end.
        private static bool Reserved(Vector2 p)
        {
            if (p.x > -38f && p.x < 20f && p.y > -30f && p.y < 9f)
            {
                return true;
            }

            if ((p - L2Layout.Hill).magnitude < L2Layout.HillRadius + 1f || L2Layout.InCreek(p, 1.5f))
            {
                return true;
            }

            if (L2Layout.InGateWall(p, 1.4f) || L2Layout.InFarmland(p) || (p - L2Layout.ShrinePosition).magnitude < 4.5f)
            {
                return true;
            }

            var block = L2Layout.BurningBlock;
            return p.x > block.xMin - 1f && p.x < block.xMax + 1f && p.y > block.yMin - 1f && p.y < block.yMax + 1f;
        }

        private static bool Overlaps(L2Layout.Lot a, L2Layout.Lot b)
        {
            foreach (var c in Corners(a, 0.3f))
            {
                if (Contains(b, c))
                {
                    return true;
                }
            }

            foreach (var c in Corners(b, 0.3f))
            {
                if (Contains(a, c))
                {
                    return true;
                }
            }

            return Contains(b, a.Center) || Contains(a, b.Center);
        }

        internal static bool Contains(L2Layout.Lot lot, Vector2 p)
        {
            Vector3 local = Quaternion.Euler(0f, -lot.Yaw, 0f) * new Vector3(p.x - lot.Center.x, 0f, p.y - lot.Center.y);
            return Mathf.Abs(local.x) < lot.Size.x * 0.5f && Mathf.Abs(local.z) < lot.Size.y * 0.5f;
        }

        public static IEnumerable<Vector2> Corners(L2Layout.Lot lot, float inset)
        {
            var rotation = Quaternion.Euler(0f, lot.Yaw, 0f);
            float hx = lot.Size.x * 0.5f - inset, hz = lot.Size.y * 0.5f - inset;
            foreach (var (sx, sz) in new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) })
            {
                Vector3 v = rotation * new Vector3(sx * hx, 0f, sz * hz);
                yield return lot.Center + new Vector2(v.x, v.z);
            }
        }

        // Smallest camera height cap over a lot (corners, edge midpoints and centre).
        public static float MinCap(L2Layout.Lot lot)
        {
            float cap = L2Layout.MaxPropHeight(lot.Center);
            foreach (var c in Corners(lot, 0f))
            {
                cap = Mathf.Min(cap, L2Layout.MaxPropHeight(c));
                cap = Mathf.Min(cap, L2Layout.MaxPropHeight((c + lot.Center) * 0.5f));
            }

            return cap;
        }

        public static float MinArenaCap(L2Layout.Lot lot)
        {
            float cap = L2Layout.ArenaCap(lot.Center);
            foreach (var c in Corners(lot, 0f))
            {
                cap = Mathf.Min(cap, L2Layout.ArenaCap(c));
                cap = Mathf.Min(cap, L2Layout.ArenaCap((c + lot.Center) * 0.5f));
            }

            return cap;
        }

        public static float LotGround(L2Layout.Lot lot)
        {
            float ground = L2Layout.TerrainHeight(lot.Center);
            foreach (var c in Corners(lot, 0f))
            {
                ground = Mathf.Min(ground, L2Layout.TerrainHeight(c));
            }

            return ground;
        }

        private static void BuildChurchGreybox(Transform parent, L2Layout.Lot lot)
        {
            float h = L2Layout.PlateauHeight;
            var nave = L2Build.Box("Church_Nave", parent, new Vector3(lot.Center.x, h + 3.2f, lot.Center.y), new Vector3(lot.Size.x, 6.4f, lot.Size.y), 0f, Blockout("BLK_Landmark"));
            var tower = L2Build.Box("Church_Tower", parent, new Vector3(L2Layout.ChurchTower.x, h + 6f, L2Layout.ChurchTower.y), new Vector3(3.2f, 12f, 3.2f), 0f, Blockout("BLK_Landmark"));
            L1Build.SetStatic(nave, L1Build.EnvironmentStatic);
            L1Build.SetStatic(tower, L1Build.EnvironmentStatic);
        }

        // ---------------------------------------------------------------- Blockers and fire

        public static string BuildBlockers(Transform root)
        {
            var group = L2Build.Group(L2Build.Group(root, "SetDressing", false), "Blockers", true);
            foreach (var b in L2Layout.Blockers)
            {
                float ground = L2Layout.Height(b.Center);
                var box = L2Build.Box(b.Name, group, new Vector3(b.Center.x, ground + 1.1f, b.Center.y), new Vector3(b.Size.x, 2.4f, b.Size.y), b.Yaw,
                    Blockout(b.Fire ? "BLK_Fire" : "BLK_Key"));
                L1Build.SetStatic(box, L1Build.EnvironmentStatic);
            }

            return $"blockers {L2Layout.Blockers.Length}";
        }

        // Flat burning beds (visual, no collider) and their damage triggers, with identical bounds.
        public static string BuildFire(Transform root)
        {
            var beds = L2Build.Group(L2Build.Group(root, "SetDressing", false), "FireBeds", true);
            var hazards = L2Build.Group(L2Build.Group(root, "_Gameplay", false), "Hazards", true);
            foreach (var z in L2Layout.FireZones)
            {
                float ground = L2Layout.Height(z.Center);
                L2Build.Box(z.Name + "_Bed", beds, new Vector3(z.Center.x, ground + 0.04f, z.Center.y), new Vector3(z.Size.x, 0.08f, z.Size.y), z.Yaw, Blockout("BLK_Fire"), false);

                var trigger = new GameObject(z.Name);
                trigger.transform.SetParent(hazards, false);
                trigger.transform.position = new Vector3(z.Center.x, ground + 1f, z.Center.y);
                trigger.transform.rotation = Quaternion.Euler(0f, z.Yaw, 0f);
                var box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(z.Size.x, 2f, z.Size.y);
                AttachFireHazard(trigger);
            }

            return $"fire beds {L2Layout.FireZones.Length}";
        }

        // FireHazard is a runtime component; it is attached once it exists (Phase 6).
        public static void AttachFireHazard(GameObject trigger)
        {
            var type = System.Type.GetType("Kneel.FireHazard, Assembly-CSharp");
            if (type != null && trigger.GetComponent(type) == null)
            {
                trigger.AddComponent(type);
            }
        }

        // ---------------------------------------------------------------- Village 3 gate

        public static string BuildGate(Transform root)
        {
            var landmarks = L2Build.Group(root, "Landmarks", false);
            var retired = landmarks.Find("ShortcutGate");
            if (retired != null)
            {
                Object.DestroyImmediate(retired.gameObject);
            }

            var gates = L2Build.Group(landmarks, "Gates", true);
            foreach (var g in L2Layout.Gates)
            {
                var group = new GameObject(g.Name).transform;
                group.SetParent(gates, false);
                // The leaf fills the gatehouse arch; the gatehouse's own walls (L2Dressing) close the rest of the street.
                float ground = L2Layout.Height(g.Center);
                var gate = L2Build.Box("Gate", group, new Vector3(g.Center.x, ground + 1.3f, g.Center.y), new Vector3(L2Layout.GateArchWidth + 0.3f, 2.6f, 0.4f), g.Yaw, Blockout("BLK_Key"));
                // The gate is on the critical path: the baked NavMesh runs through it, and a carving obstacle closes
                // it at runtime until the lever opens it.
                var modifier = gate.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
                var obstacle = gate.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                obstacle.enabled = false;   // saved off (it would carve in the editor too); ShortcutGate switches it on at runtime

                L2Build.Box("Lever", group, new Vector3(g.Lever.x, L2Layout.Height(g.Lever) + 0.6f, g.Lever.y), new Vector3(0.3f, 1.2f, 0.3f), g.Yaw, Blockout("BLK_Key"));
                L1Build.SetStatic(gate, StaticEditorFlags.ContributeGI);
            }

            return $"{L2Layout.Gates.Length} gates + levers (on the route)";
        }

        // ---------------------------------------------------------------- Markers

        private static readonly Dictionary<string, string> Icons = new Dictionary<string, string>
        {
            ["PlayerStart"] = "sv_label_3", ["Exit"] = "sv_label_2", ["E"] = "sv_label_6", ["Lore"] = "sv_label_5", ["Loot"] = "sv_label_4",
            ["Checkpoint"] = "sv_label_1", ["Ash"] = "sv_label_7", ["Gate"] = "sv_label_0",
        };

        public static string BuildMarkers(Transform root)
        {
            var gameplay = L2Build.Group(root, "_Gameplay", false);
            // Rebuild the markers only; hazards (a sibling group) are owned by BuildFire.
            var keep = gameplay.Find("Hazards");
            var doomed = new List<GameObject>();
            foreach (Transform t in gameplay)
            {
                if (t != keep)
                {
                    doomed.Add(t.gameObject);
                }
            }

            foreach (var go in doomed)
            {
                Object.DestroyImmediate(go);
            }

            int count = 0;
            foreach (var kv in L2Layout.Markers)
            {
                if (kv.Key == "Well" || kv.Key == "RespawnPoint")
                {
                    continue;
                }

                var m = Marker(kv.Key, gameplay, kv.Value);
                count++;
                if (kv.Key == "Checkpoint_Shrine")
                {
                    Marker("RespawnPoint", m.transform, L2Layout.Markers["RespawnPoint"]);
                }
                else if (kv.Key == "Exit_ToL3")
                {
                    var box = m.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.center = Vector3.up * 1.5f;
                    box.size = new Vector3(6f, 3f, 3f);
                    m.layer = LayerMask.NameToLayer("Ignore Raycast");
                }
            }

            foreach (var arena in L2Layout.Arenas)
            {
                var e = Marker(arena.Name, gameplay, arena.Center);
                int i = 1;
                foreach (var s in L2Layout.Spawns[arena.Name])
                {
                    Marker("SpawnPoint_" + i++.ToString("00"), e.transform, s);
                }

                var trigger = new GameObject("ArenaTrigger");
                trigger.transform.SetParent(e.transform, false);
                trigger.layer = LayerMask.NameToLayer("Ignore Raycast");
                var box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = Vector3.up * 2f;
                box.size = new Vector3(arena.Radius * 2f, 4f, arena.Radius * 2f);
                count++;
            }

            return $"markers {count}";
        }

        private static GameObject Marker(string name, Transform parent, Vector2 p)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = L2Build.Ground(p);
            string icon = null;
            foreach (var kv in Icons)
            {
                if (name.StartsWith(kv.Key))
                {
                    icon = kv.Value;
                    break;
                }
            }

            if (name.StartsWith("SpawnPoint") || name.StartsWith("RespawnPoint"))
            {
                icon = "sv_icon_dot" + (name.StartsWith("Spawn") ? "6" : "3") + "_pix16_gizmo";
            }

            if (icon != null)
            {
                var content = EditorGUIUtility.IconContent(icon);
                if (content != null && content.image != null)
                {
                    EditorGUIUtility.SetIconForObject(go, (Texture2D)content.image);
                }
            }

            return go;
        }

        // ---------------------------------------------------------------- Player and camera

        public static string EnsurePlayerAndCamera()
        {
            var settings = CameraSettings();
            var start = L2Build.Ground(L2Layout.Markers["PlayerStart"]);
            var scene = EditorSceneManager.GetActiveScene();
            GameObject player = null, camera = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Player")
                {
                    player = go;
                }
                else if (go.name == "Main Camera")
                {
                    camera = go;
                }
            }

            if (player == null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"));
                player.name = "Player";
            }

            player.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 90f, 0f));
            var movement = player.GetComponent("PlayerMovement");
            if (movement != null)
            {
                var so = new SerializedObject(movement);
                so.FindProperty("aimLayerMask").intValue = LayerMask.GetMask("Ground", "Obstacles");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (camera == null)
            {
                camera = new GameObject("Main Camera");
                camera.tag = "MainCamera";
                var cam = camera.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 600f;
                camera.AddComponent<AudioListener>();
                camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var type = System.Type.GetType("PlayerCamera, Assembly-CSharp");
                camera.AddComponent(type);
            }

            var playerCamera = camera.GetComponent("PlayerCamera");
            var cso = new SerializedObject(playerCamera);
            cso.FindProperty("settings").objectReferenceValue = settings;
            cso.ApplyModifiedPropertiesWithoutUndo();
            // Scenery between the camera and the player fades (Diablo-style), so houses can stand on the camera side.
            if (camera.GetComponent<Kneel.OcclusionFader>() == null)
            {
                camera.AddComponent<Kneel.OcclusionFader>();
            }

            var pose = L2Layout.CameraPose(start, L2Layout.CameraDistance);
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
            return "player + camera (yaw 0, occlusion fader)";
        }

        public static ScriptableObject CameraSettings()
        {
            string path = L2Build.SettingsPath + "/L2_PlayerCameraSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (settings == null)
            {
                AssetDatabase.CopyAsset("Assets/Kneel/Settings/L1/L1_PlayerCameraSettings.asset", path);
                settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            var so = new SerializedObject(settings);
            so.FindProperty("yaw").floatValue = L2Layout.CameraYaw;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return settings;
        }

        // ---------------------------------------------------------------- Base lighting (the lighting pass replaces it)

        private static string EnsureBaseLighting(Transform root)
        {
            var lighting = L2Build.Group(root, "_Lighting", false);
            if (lighting.Find("Moon") != null)
            {
                return "lighting kept";
            }

            var moon = new GameObject("Moon");
            moon.transform.SetParent(lighting, false);
            moon.transform.rotation = Quaternion.Euler(52f, 135f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = L1Build.Hex("#A9BCDC");
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = L1Build.Hex("#56627A");
            RenderSettings.ambientEquatorColor = L1Build.Hex("#3C4250");
            RenderSettings.ambientGroundColor = L1Build.Hex("#24211E");
            return "greybox moon";
        }
    }
}
