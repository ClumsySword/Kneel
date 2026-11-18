using System.Collections.Generic;
using Kneel.Core;
using Kneel.Markers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.EditorTools.Transitions
{
    // Building blocks for authoring the seams into the level scenes. Everything new goes under one root per scene,
    // "_Transition" (exit, arrival, preload, glimpse groups), so it can be found, diffed or removed in one place;
    // the few in-place edits (a wall segment switched off, a door mesh hidden) are listed by each seam's builder.
    public static class SeamKit
    {
        public const string RootName = "_Transition";

        public static Transform Root()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject g in scene.GetRootGameObjects())
            {
                if (g.name == RootName)
                {
                    return g.transform;
                }
            }

            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "Transition root");
            return go.transform;
        }

        // A named child, emptied first when `clear` (only ever our own groups).
        public static Transform Group(Transform parent, string name, bool clear)
        {
            Transform t = parent.Find(name);
            if (t != null && clear)
            {
                Object.DestroyImmediate(t.gameObject);
                t = null;
            }

            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(parent, false);
            }

            return t;
        }

        public static Transform Point(Transform parent, string name, Vector3 position, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return t;
        }

        // A camera pose framing `focus` from `distance` metres at pitch/yaw (PlayerCamera's own construction).
        public static Transform Orbit(Transform parent, string name, Vector3 focus, float pitch, float yaw, float distance)
        {
            Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(focus - r * Vector3.forward * distance, r);
            return t;
        }

        public static Transform Look(Transform parent, string name, Vector3 position, Vector3 target)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            return t;
        }

        // The gameplay camera's pose for a player standing at `feet` (the intro's last key, so the release is seamless).
        public static Transform Gameplay(Transform parent, string name, Vector3 feet)
        {
            PlayerCamera cam = Object.FindAnyObjectByType<PlayerCamera>();
            PlayerCameraSettings s = cam.Settings;
            return Orbit(parent, name, feet + Vector3.up * s.targetHeight, s.pitch, s.yaw, s.startDistance);
        }

        public static CameraKey Key(Transform pose, float time, float fov = 0f) => new CameraKey { pose = pose, time = time, fieldOfView = fov };

        public static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size, bool trigger, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = trigger;
            return box;
        }

        public static GameObject Place(string assetPath, Transform parent, Vector3 position, Vector3 euler, float scale = 1f, bool colliders = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning("SeamKit: no asset at " + assetPath);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            go.transform.localScale *= scale;
            if (colliders == false)
            {
                foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                {
                    c.enabled = false;
                }
            }

            return go;
        }

        public static void SetMaterial(GameObject go, Material material)
        {
            if (go == null || material == null)
            {
                return;
            }

            foreach (Renderer r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = material;
                }

                r.sharedMaterials = mats;
            }
        }

        public static void SetLayer(GameObject go, string layer)
        {
            int l = LayerMask.NameToLayer(layer);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = l;
            }
        }

        // A ribbon along a polyline (y: its height where it doesn't follow the ground, or there is none to find), lying
        // 3 cm up, `width` wide, its ends tapering over 3 m; u runs across it, v along it at 6 m to 1. Visual only: no
        // collider, casts no shadow. Saved as Meshes/Transitions/Road_<name>. SeamRoads.Ribbon gives it a material.
        public static GameObject Road(Transform parent, string name, Vector3[] points, Material material, float width = 3.4f, bool followGround = true)
        {
            float total = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                total += Vector3.Distance(Flat(points[i - 1]), Flat(points[i]));
            }

            const float step = 0.5f;
            const int across = 8;
            int along = Mathf.Max(2, Mathf.CeilToInt(total / step) + 1);
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 origin = Along(points, 0f, out _);
            origin = new Vector3(origin.x, 0f, origin.z);
            for (int j = 0; j < along; j++)
            {
                float d = Mathf.Min(j * step, total);
                Vector3 c = Along(points, d, out Vector3 dir);
                Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                float taper = Mathf.Clamp01(Mathf.Min(d, total - d) / 3f);
                float half = width * 0.5f * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, taper));
                for (int i = 0; i <= across; i++)
                {
                    float u = i / (float)across;
                    Vector3 p = c + right * Mathf.Lerp(-half, half, u);
                    Vector3 onGround = followGround ? GroundOnly(new Vector3(p.x, c.y, p.z)) : new Vector3(p.x, c.y - 0.03f, p.z);
                    vertices.Add(onGround + Vector3.up * 0.03f - origin);
                    uvs.Add(new Vector2(u, d / 6f));
                }
            }

            for (int j = 0; j < along - 1; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int a0 = j * (across + 1) + i, b0 = a0 + across + 1;
                    triangles.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1 });
                }
            }

            var mesh = new Mesh { name = "Road_" + name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string folder = SeamBuilders.MeshFolder;
            System.IO.Directory.CreateDirectory(folder);
            string path = folder + "/Road_" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Road_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 Along(Vector3[] points, float distance, out Vector3 dir)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = Flat(points[i - 1]), b = Flat(points[i]);
                float len = Vector3.Distance(a, b);
                if (distance <= len || i == points.Length - 1)
                {
                    dir = (b - a).normalized;
                    // y carries the fallback height where there is no ground to find.
                    return Vector3.Lerp(points[i - 1], points[i], Mathf.Clamp01(distance / Mathf.Max(len, 0.001f)));
                }

                distance -= len;
            }

            dir = Vector3.forward;
            return Flat(points[0]);
        }

        // The walkable ground (or, where a level has none, its visible ground mesh) under a point.
        public static Vector3 GroundOnly(Vector3 p)
        {
            var from = new Vector3(p.x, 60f, p.z);
            foreach (RaycastHit hit in SortedHits(from))
            {
                int layer = hit.collider.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Ground") || (layer == 0 && hit.collider.isTrigger == false && hit.collider is MeshCollider))
                {
                    return hit.point;
                }
            }

            return new Vector3(p.x, p.y, p.z);
        }

        private static RaycastHit[] SortedHits(Vector3 from)
        {
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            return hits;
        }

        // Snaps a point onto whatever walkable ground is under it.
        public static Vector3 Ground(Vector3 p, float above = 6f)
        {
            int mask = LayerMask.GetMask("Ground", "Default", "Obstacles");
            return Physics.Raycast(p + Vector3.up * above, Vector3.down, out RaycastHit hit, above + 10f, mask, QueryTriggerInteraction.Ignore) ? hit.point : p;
        }

        // The ExitMarker + LevelExit on `trigger` (an existing marker's box, or a new one).
        public static LevelExit Exit(GameObject trigger, string destination)
        {
            // (GetComponent's "missing" result is a fake null in the Editor, so no ?? here.)
            var marker = trigger.GetComponent<ExitMarker>();
            if (marker == null)
            {
                marker = trigger.AddComponent<ExitMarker>();
            }

            marker.destination = destination;
            var exit = trigger.GetComponent<LevelExit>();
            if (exit == null)
            {
                exit = trigger.AddComponent<LevelExit>();
            }
            EditorUtility.SetDirty(trigger);
            return exit;
        }

        public static PlayerSpawnMarker Spawn(Transform parent, Vector3 position, float yaw)
        {
            PlayerSpawnMarker existing = Object.FindAnyObjectByType<PlayerSpawnMarker>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("PlayerSpawn");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go.AddComponent<PlayerSpawnMarker>();
        }

        public static GameObject Find(string path)
        {
            GameObject go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogWarning("SeamKit: not found: " + path);
            }

            return go;
        }

        // The scene object nearest `near` whose name starts with `name`, under `under` (null = whole scene).
        public static GameObject Nearest(string name, Vector3 near, float within, Transform under = null)
        {
            GameObject best = null;
            float bestD = within * within;
            IEnumerable<Transform> all = under != null ? under.GetComponentsInChildren<Transform>(true) : Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in all)
            {
                if (t.name.StartsWith(name) == false)
                {
                    continue;
                }

                float d = (t.position - near).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = t.gameObject;
                }
            }

            return best;
        }

        public static void Save()
        {
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Open(string sceneName)
        {
            string path = LevelTransitionSetup.ScenePath(sceneName);
            if (SceneManager.GetActiveScene().path != path)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
    }
}
