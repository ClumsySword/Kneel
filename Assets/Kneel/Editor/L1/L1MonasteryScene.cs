using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Puts L1's monastery (L1Monastery) into the open L1 scene, Patrick's source scene or the playable copy:
    //  - Ground: L1_Ground (a 1 m grid from z -26) runs on south to z -64 under and round the monastery (mirrored
    //    heights and texture, so GroundMinZ and the painted layout keep their meaning), dips to the floor near its
    //    walls and is cut away under them. The mesh is shared by both scenes; the untouched original is kept as
    //    L1_Ground_PreMonastery.asset and every run starts from it, so reruns don't build up.
    //  - The battlefield dressing inside the walls and in front of the main door is removed; what stands on the
    //    lowered ground follows it down.
    //  - The invisible boundary is opened at the door and closed round the façade.
    //  - The monastery prefab goes under the level root, at the origin.
    // Safe to rerun (each step checks what's already done).
    public static class L1MonasteryScene
    {
        private const string GroundPath = L1Monastery.L1Folder + "/Meshes/L1_Ground.asset";
        private const string BackupPath = L1Monastery.L1Folder + "/Meshes/L1_Ground_PreMonastery.asset";
        private const int Columns = 115;
        private const float MinX = -52f, OldMinZ = -26f, NewMinZ = -96f;
        private const float Blend = 6.5f;     // metres over which the ground climbs back from the floor

        // In front of the main door: kept clear of dressing and boundary.
        private static readonly Rect DoorApproach = new Rect(-4.6f, -10.6f, 9.2f, 5f);

        // The walk out of the main door onto Start: the boundary is opened only here.
        private static readonly Rect DoorPath = new Rect(-2.2f, -10.6f, 4.4f, 6f);

        [MenuItem("Kneel/L1/Monastery/Place In Open Scene")]
        public static void PlaceMenu()
        {
            Debug.Log("[L1 Monastery] " + Place());
        }

        public static string Place()
        {
            var root = L1LevelTools.Root;
            if (root == null)
            {
                return "No L1_BrokenLine root in the open scene.";
            }

            var log = new StringBuilder();
            // What this scene's dressing stands on: the original ground the first time the monastery goes into it (the
            // mesh is shared, so another scene may have reshaped it already), else the ground as it is now.
            bool first = root.transform.Find("Monastery") == null;
            var before = first ? AssetDatabase.LoadAssetAtPath<Mesh>(BackupPath) : null;
            var oldHeight = Heights(before ?? AssetDatabase.LoadAssetAtPath<Mesh>(GroundPath), out int oldRows, out float oldMinZ);
            log.Append(ReshapeGround()).Append(' ');
            var newHeight = Heights(AssetDatabase.LoadAssetAtPath<Mesh>(GroundPath), out int newRows, out float newMinZ);

            // Refresh the ground's collider (same mesh asset, new data).
            var ground = root.transform.Find("Terrain/L1_Ground");
            var mc = ground.GetComponent<MeshCollider>();
            mc.sharedMesh = null;
            mc.sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;

            log.Append(ClearDressing(root.transform)).Append(' ');
            log.Append(Regrade(root.transform, p => Sample(oldHeight, oldRows, oldMinZ, p), p => Sample(newHeight, newRows, newMinZ, p))).Append(' ');
            log.Append(Boundary(root.transform)).Append(' ');

            var existing = root.transform.Find("Monastery");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Monastery.PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.name = "Monastery";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            log.Append("Placed the monastery.");

            EditorSceneManager.MarkSceneDirty(root.scene);
            return log.ToString();
        }

        // ---------------------------------------------------------------- Ground

        [MenuItem("Kneel/L1/Monastery/Reshape Ground Only")]
        public static void ReshapeMenu()
        {
            Debug.Log("[L1 Monastery] " + ReshapeGround());
        }

        public static string ReshapeGround()
        {
            var ground = AssetDatabase.LoadAssetAtPath<Mesh>(GroundPath);
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(BackupPath);
            if (source == null)
            {
                source = Object.Instantiate(ground);
                AssetDatabase.CreateAsset(source, BackupPath);
            }

            var sv = source.vertices;
            var sn = source.normals;
            var suv = source.uv;
            int oldRows = sv.Length / Columns;
            if (sv.Length != Columns * oldRows || Mathf.Abs(sv[0].z - OldMinZ) > 0.01f || Mathf.Abs(sv[1].x - (MinX + 1f)) > 0.01f)
            {
                return "L1_Ground isn't the expected 1 m grid from (-52, -26); left alone.";
            }

            int extra = Mathf.RoundToInt(OldMinZ - NewMinZ);
            int rows = oldRows + extra;
            var verts = new Vector3[Columns * rows];
            var uvs = new Vector2[verts.Length];
            var changed = new bool[verts.Length];
            for (int j = 0; j < rows; j++)
            {
                float z = NewMinZ + j;
                // South of the old edge the ground mirrors what lies north of it (heights and texture).
                int sj = z >= OldMinZ ? Mathf.RoundToInt(z - OldMinZ) : Mathf.RoundToInt(OldMinZ - z);
                sj = Mathf.Clamp(sj, 0, oldRows - 1);
                for (int i = 0; i < Columns; i++)
                {
                    int s = sj * Columns + i;
                    float x = MinX + i;
                    float h = sv[s].y;
                    float d = DistanceToBlocks(new Vector2(x, z));
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.3f) / Blend));
                    float lowered = Mathf.Lerp(-0.03f, h, t);
                    int k = j * Columns + i;
                    verts[k] = new Vector3(x, lowered, z);
                    uvs[k] = suv[s];
                    changed[k] = z < OldMinZ || Mathf.Abs(lowered - h) > 1e-4f;
                }
            }

            // Triangles, leaving out those under the monastery (well inside its outer walls).
            var tris = new List<int>((Columns - 1) * (rows - 1) * 6);
            int cut = 0;
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < Columns - 1; i++)
                {
                    int a = j * Columns + i;
                    var centre = new Vector2(MinX + i + 0.5f, NewMinZ + j + 0.5f);
                    if (InsideBlocks(centre, 0.6f))
                    {
                        cut++;
                        continue;
                    }

                    tris.AddRange(new[] { a, a + Columns, a + 1, a + 1, a + Columns, a + Columns + 1 });
                }
            }

            var mesh = new Mesh { name = "L1_Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Untouched ground keeps its original normals exactly.
            var normals = mesh.normals;
            for (int j = extra; j < rows; j++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    int k = j * Columns + i;
                    if (!changed[k] && !NearChanged(changed, k, rows))
                    {
                        normals[k] = sn[(j - extra) * Columns + i];
                    }
                }
            }

            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            ground.Clear();
            EditorUtility.CopySerialized(mesh, ground);
            ground.name = "L1_Ground";
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(ground);
            AssetDatabase.SaveAssets();
            return $"Ground: {rows} rows to z {NewMinZ}, {cut} cells cut away under the monastery.";
        }

        private static bool NearChanged(bool[] changed, int k, int rows)
        {
            int i = k % Columns, j = k / Columns;
            for (int dj = -1; dj <= 1; dj++)
            {
                for (int di = -1; di <= 1; di++)
                {
                    int ii = i + di, jj = j + dj;
                    if (ii >= 0 && ii < Columns && jj >= 0 && jj < rows && changed[jj * Columns + ii])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static float DistanceToBlocks(Vector2 p)
        {
            float best = Mathf.Max(0f, (p - L1Layout.MonasteryHallCentre).magnitude - L1Layout.MonasteryHallRadius);
            foreach (var r in L1Layout.MonasteryBlocks)
            {
                float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
                float dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        // Inside one of the outer-wall blocks, at least 'inset' from its edge.
        public static bool InsideBlocks(Vector2 p, float inset)
        {
            if ((p - L1Layout.MonasteryHallCentre).magnitude < L1Layout.MonasteryHallRadius - inset)
            {
                return true;
            }

            foreach (var r in L1Layout.MonasteryBlocks)
            {
                if (p.x > r.xMin + inset && p.x < r.xMax - inset && p.y > r.yMin + inset && p.y < r.yMax - inset)
                {
                    return true;
                }
            }

            return false;
        }

        private static float[] Heights(Mesh mesh, out int rows, out float minZ)
        {
            var v = mesh.vertices;
            rows = v.Length / Columns;
            minZ = v[0].z;
            var h = new float[v.Length];
            for (int k = 0; k < v.Length; k++)
            {
                h[k] = v[k].y;
            }

            return h;
        }

        private static float Sample(float[] h, int rows, float minZ, Vector2 p)
        {
            float fx = Mathf.Clamp(p.x - MinX, 0f, Columns - 1.001f), fz = p.y - minZ;
            if (fz < 0f || fz > rows - 1.001f)
            {
                return float.NaN;
            }

            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = Mathf.Lerp(h[j * Columns + i], h[j * Columns + i + 1], tx);
            float b = Mathf.Lerp(h[(j + 1) * Columns + i], h[(j + 1) * Columns + i + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        // ---------------------------------------------------------------- Dressing

        private static readonly string[] DressingGroups =
        {
            "SetDressing/DeadTrees", "SetDressing/Corpses", "SetDressing/Debris", "SetDressing/Standards", "SetDressing/Campfires",
            "SetDressing/Torches", "SetDressing/Gates", "SetDressing/Monsters", "SetDressing/Fallen", "SetDressing/DroppedArms",
            "SetDressing/GroundCover", "SetDressing/Grass", "SetDressing/PathTrees", "Boundary/Wreckage_Band",
            "Boundary/Wreckage_Outer", "Boundary/Silhouettes", "Boundary/NaturalFringe", "Landmarks/Lore", "Landmarks/Wreckage",
        };

        // Removes battlefield dressing standing inside the monastery, on its walls or in front of the main door.
        private static string ClearDressing(Transform root)
        {
            int removed = 0;
            var names = new List<string>();
            foreach (var path in DressingGroups)
            {
                var group = root.Find(path);
                if (group == null)
                {
                    continue;
                }

                var doomed = new List<GameObject>();
                foreach (Transform child in group)
                {
                    if (Intrudes(child))
                    {
                        doomed.Add(child.gameObject);
                    }
                }

                foreach (var go in doomed)
                {
                    if (!path.EndsWith("Grass") && !path.EndsWith("GroundCover") && names.Count < 40)
                    {
                        names.Add(go.name);
                    }

                    Object.DestroyImmediate(go);
                    removed++;
                }
            }

            return $"Removed {removed} dressing pieces ({string.Join(", ", names)}).";
        }

        private static bool Intrudes(Transform t)
        {
            var p = new Vector2(t.position.x, t.position.z);
            if (DistanceToBlocks(p) < 0.6f || DoorApproach.Contains(p))
            {
                return true;
            }

            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (rect.Overlaps(DoorApproach))
                {
                    return true;
                }

                foreach (var block in L1Layout.MonasteryBlocks)
                {
                    if (rect.Overlaps(block))
                    {
                        return true;
                    }
                }

                var nearest = new Vector2(Mathf.Clamp(L1Layout.MonasteryHallCentre.x, rect.xMin, rect.xMax), Mathf.Clamp(L1Layout.MonasteryHallCentre.y, rect.yMin, rect.yMax));
                if ((nearest - L1Layout.MonasteryHallCentre).magnitude < L1Layout.MonasteryHallRadius)
                {
                    return true;
                }
            }

            return false;
        }

        // Moves every piece of dressing standing where the ground was lowered down with it.
        private static string Regrade(Transform root, System.Func<Vector2, float> before, System.Func<Vector2, float> after)
        {
            int moved = 0;
            foreach (var path in DressingGroups)
            {
                var group = root.Find(path);
                if (group == null)
                {
                    continue;
                }

                foreach (Transform child in group)
                {
                    var p = new Vector2(child.position.x, child.position.z);
                    if (DistanceToBlocks(p) > Blend + 1f)
                    {
                        continue;
                    }

                    float b = before(p), a = after(p);
                    if (float.IsNaN(b) || float.IsNaN(a) || Mathf.Abs(a - b) < 0.005f)
                    {
                        continue;
                    }

                    child.position += Vector3.up * (a - b);
                    moved++;
                }
            }

            return $"Lowered {moved} pieces with the ground.";
        }

        // ---------------------------------------------------------------- Boundary

        // Takes out the invisible boundary boxes in the way of the main door and the monastery, and closes the gaps
        // either side of the façade with boxes of the same kind (Ignore Raycast, 5 m).
        private static string Boundary(Transform root)
        {
            var colliders = root.Find("Boundary/BoundaryColliders");
            int removed = 0;
            foreach (var box in colliders.GetComponents<BoxCollider>())
            {
                var b = box.bounds;
                var rect = Rect.MinMaxRect(b.min.x + 0.05f, b.min.z + 0.05f, b.max.x - 0.05f, b.max.z - 0.05f);
                bool hit = rect.Overlaps(DoorPath);
                foreach (var block in L1Layout.MonasteryBlocks)
                {
                    hit |= rect.Overlaps(block);
                }

                if (hit)
                {
                    Object.DestroyImmediate(box);
                    removed++;
                }
            }

            var old = root.Find("Boundary/MonasteryBoundary");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var go = new GameObject("MonasteryBoundary");
            go.transform.SetParent(root.Find("Boundary"), false);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            // From the façade's corners out to the boxes still ringing Start.
            float fx = L1Monastery.CorridorX + L1Monastery.Wall + 0.05f;
            foreach (var (x0, x1) in new[] { (-10f, -fx), (fx, 10f) })
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3((x0 + x1) * 0.5f, 2.5f, -8.25f);
                box.size = new Vector3(x1 - x0, 5f, 4.5f);
            }

            return $"Boundary: removed {removed} boxes, closed both sides of the façade.";
        }
    }
}
