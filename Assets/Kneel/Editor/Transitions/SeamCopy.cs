using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kneel.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kneel.EditorTools.Transitions
{
    // Real pieces of one level copied into its neighbour, so the glimpse across a seam is the other level's own
    // geometry: visual-only clones (no colliders, no scripts), mapped through the seam, optionally clipped to a box,
    // and optionally turned into silhouettes. Plus an edit-mode preview of a seam's camera keys.
    public static class SeamCopy
    {
        public const string MaterialFolder = "Assets/Kneel/Materials/Transitions";

        private static readonly HashSet<Type> Visual = new HashSet<Type>
        {
            typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(Light),
        };

        // Copies every transform in `source` whose path starts with one of `paths` (and, when `within` has a size,
        // whose renderer bounds touch it), placing each at map * its pose, under `parent` in the active scene.
        public static int Copy(string sourceScene, string[] paths, Bounds within, Matrix4x4 map, Transform parent, Material silhouette = null, bool clip = false, Func<Transform, bool> skip = null)
        {
            Scene target = SceneManager.GetActiveScene();
            // Copying from the open level itself (extending its own ground) needs no second scene.
            bool self = target.name == sourceScene;
            Scene src = self ? target : EditorSceneManager.OpenScene(LevelTransitionSetup.ScenePath(sourceScene), OpenSceneMode.Additive);
            int copied = 0;
            try
            {
                var picked = new List<Transform>();
                foreach (GameObject root in src.GetRootGameObjects())
                {
                    if (self && root.name == SeamKit.RootName)
                    {
                        continue;
                    }

                    foreach (Transform t in root.GetComponentsInChildren<Transform>(false))
                    {
                        string p = TransitionSurvey.PathOf(t);
                        bool match = false;
                        foreach (string prefix in paths)
                        {
                            if (p == prefix || p.StartsWith(prefix + "/"))
                            {
                                match = true;
                            }
                        }

                        if (match == false || (skip != null && skip(t)))
                        {
                            continue;
                        }

                        var r = t.GetComponent<Renderer>();
                        var l = t.GetComponent<Light>();
                        if (r == null && l == null)
                        {
                            continue;
                        }

                        // What the level itself doesn't draw (L5's blockout floor under its nave floor) isn't copied:
                        // a copy would draw it, on top of what replaced it.
                        if ((r != null && r.enabled == false && l == null) || (l != null && r == null && l.enabled == false))
                        {
                            continue;
                        }

                        if (within.size != Vector3.zero && r != null && within.Intersects(r.bounds) == false)
                        {
                            continue;
                        }

                        if (within.size != Vector3.zero && l != null && within.Contains(t.position) == false)
                        {
                            continue;
                        }

                        picked.Add(t);
                    }
                }

                foreach (Transform t in picked)
                {
                    GameObject copy = CloneVisual(t, target);
                    if (map.determinant < 0f)
                    {
                        // A mirror across a z plane (the only kind used): reflect the rotation, flip the local z scale
                        // (Unity reverses the winding for negative scale, so faces stay outward).
                        Quaternion q = t.rotation;
                        copy.transform.SetPositionAndRotation(map.MultiplyPoint3x4(t.position), new Quaternion(-q.x, -q.y, q.z, q.w));
                        Vector3 ls = t.lossyScale;
                        copy.transform.localScale = new Vector3(ls.x, ls.y, -ls.z);
                    }
                    else
                    {
                        copy.transform.SetPositionAndRotation(map.MultiplyPoint3x4(t.position), map.rotation * t.rotation);
                        copy.transform.localScale = Vector3.Scale(t.lossyScale, map.lossyScale);
                    }
                    copy.transform.SetParent(parent, true);
                    if (clip && within.size != Vector3.zero)
                    {
                        Clip(copy, within, map);
                    }

                    if (silhouette != null)
                    {
                        Silhouette(copy, silhouette);
                    }

                    copied++;
                }
            }
            finally
            {
                if (self == false)
                {
                    EditorSceneManager.CloseScene(src, true);
                    SceneManager.SetActiveScene(target);
                }
            }

            return copied;
        }

        // One object's own renderer, mesh, particles or light, without its children (those are copied on their own).
        private static GameObject CloneVisual(Transform t, Scene into)
        {
            var go = new GameObject(t.name);
            SceneManager.MoveGameObjectToScene(go, into);
            go.layer = t.gameObject.layer;
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf != null && mr != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = mr.shadowCastingMode;
                r.renderingLayerMask = mr.renderingLayerMask;
            }

            var ps = t.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(ps);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
                var pr = t.GetComponent<ParticleSystemRenderer>();
                if (pr != null)
                {
                    UnityEditorInternal.ComponentUtility.CopyComponent(pr);
                    if (go.GetComponent<ParticleSystemRenderer>() != null)
                    {
                        UnityEditorInternal.ComponentUtility.PasteComponentValues(go.GetComponent<ParticleSystemRenderer>());
                    }
                }
            }

            var light = t.GetComponent<Light>();
            if (light != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(light);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
            }

            return go;
        }

        // Keeps only the triangles whose centre lies inside `box` (in the source level's space), as a new mesh asset.
        private static void Clip(GameObject copy, Bounds box, Matrix4x4 map)
        {
            var mf = copy.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                return;
            }

            Mesh m = mf.sharedMesh;
            Matrix4x4 toSource = map.inverse * copy.transform.localToWorldMatrix;
            Vector3[] v = m.vertices;
            if (v.Length == 0)
            {
                return;
            }

            var mesh = new Mesh();
            var keeps = new int[m.subMeshCount][];
            mesh.name = m.name + "_Clip";
            bool any = false;
            bool allIn = true;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                int[] tris = m.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 c = toSource.MultiplyPoint3x4((v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) / 3f);
                    if (box.Contains(c))
                    {
                        keep.Add(tris[i]);
                        keep.Add(tris[i + 1]);
                        keep.Add(tris[i + 2]);
                    }
                    else
                    {
                        allIn = false;
                    }
                }

                any |= keep.Count > 0;
                keeps[s] = keep.ToArray();
            }

            if (allIn)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                return;
            }

            if (any == false)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(copy);
                return;
            }

            Compact(mesh, false, m, keeps);
            Directory.CreateDirectory(SeamBuilders.MeshFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{SeamBuilders.MeshFolder}/{SceneManager.GetActiveScene().name}_{m.name}_Clip.asset");
            AssetDatabase.CreateAsset(mesh, path);
            mf.sharedMesh = mesh;
        }

        // Fills `target` with `source`'s triangles (or the given ones, per submesh) and only the vertices they use (a
        // clipped copy kept its source's whole vertex buffer otherwise). positionsOnly: drops every other attribute and
        // welds vertices within a millimetre. Reads the source asset itself, which the Editor allows even where its
        // import isn't readable (a copy made with Instantiate isn't).
        public static void Compact(Mesh target, bool positionsOnly, Mesh source = null, int[][] triangles = null)
        {
            Mesh mesh = source != null ? source : target;
            Vector3[] v = mesh.vertices;
            Vector3[] n = positionsOnly ? new Vector3[0] : mesh.normals;
            Vector4[] t = positionsOnly ? new Vector4[0] : mesh.tangents;
            Color[] c = positionsOnly ? new Color[0] : mesh.colors;
            var uvs = new List<Vector2>[4];
            for (int k = 0; k < 4; k++)
            {
                uvs[k] = new List<Vector2>();
                if (positionsOnly == false)
                {
                    mesh.GetUVs(k, uvs[k]);
                }
            }

            int subs = mesh.subMeshCount;
            var tris = new int[subs][];
            for (int s = 0; s < subs; s++)
            {
                tris[s] = triangles != null ? triangles[s] : mesh.GetTriangles(s);
            }

            var map = new Dictionary<int, int>();
            var weld = new Dictionary<Vector3Int, int>();
            var nv = new List<Vector3>();
            var nn = new List<Vector3>();
            var nt = new List<Vector4>();
            var nc = new List<Color>();
            var nuv = new List<Vector2>[4];
            for (int k = 0; k < 4; k++)
            {
                nuv[k] = new List<Vector2>();
            }

            int Map(int i)
            {
                if (map.TryGetValue(i, out int r))
                {
                    return r;
                }

                Vector3Int key = default;
                if (positionsOnly)
                {
                    key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
                    if (weld.TryGetValue(key, out r))
                    {
                        map[i] = r;
                        return r;
                    }
                }

                r = nv.Count;
                nv.Add(v[i]);
                if (n.Length == v.Length) nn.Add(n[i]);
                if (t.Length == v.Length) nt.Add(t[i]);
                if (c.Length == v.Length) nc.Add(c[i]);
                for (int k = 0; k < 4; k++)
                {
                    if (uvs[k].Count == v.Length) nuv[k].Add(uvs[k][i]);
                }

                if (positionsOnly)
                {
                    weld[key] = r;
                }

                map[i] = r;
                return r;
            }

            var newTris = new int[subs][];
            for (int s = 0; s < subs; s++)
            {
                newTris[s] = new int[tris[s].Length];
                for (int i = 0; i < tris[s].Length; i++)
                {
                    newTris[s][i] = Map(tris[s][i]);
                }
            }

            target.Clear();
            target.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            target.SetVertices(nv);
            if (nn.Count == nv.Count) target.SetNormals(nn);
            if (nt.Count == nv.Count) target.SetTangents(nt);
            if (nc.Count == nv.Count) target.SetColors(nc);
            for (int k = 0; k < 4; k++)
            {
                if (nuv[k].Count == nv.Count && nv.Count > 0) target.SetUVs(k, nuv[k]);
            }

            target.subMeshCount = subs;
            for (int s = 0; s < subs; s++)
            {
                target.SetTriangles(newTris[s], s);
            }

            target.RecalculateBounds();
        }

        private static void Silhouette(GameObject go, Material material)
        {
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = material;
                }

                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }

            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                UnityEngine.Object.DestroyImmediate(ps.GetComponent<ParticleSystemRenderer>());
                UnityEngine.Object.DestroyImmediate(ps);
            }

            foreach (Light l in go.GetComponentsInChildren<Light>(true))
            {
                UnityEngine.Object.DestroyImmediate(l);
            }
        }

        // Merges every silhouette part under `group` into as few meshes as fit 32-bit index buffers of ~200k vertices
        // (they share one material and never move), saved as assets; the parts are removed.
        public static int Combine(Transform group, string tag)
        {
            var parts = group.GetComponentsInChildren<MeshFilter>(true);
            if (parts.Length == 0)
            {
                return 0;
            }

            Material material = parts[0].GetComponent<MeshRenderer>().sharedMaterial;
            var batch = new List<CombineInstance>();
            int vertices = 0;
            int made = 0;
            Matrix4x4 toGroup = group.worldToLocalMatrix;
            void Flush()
            {
                if (batch.Count == 0)
                {
                    return;
                }

                var mesh = new Mesh { name = $"{tag}_{made}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(batch.ToArray(), true, true);
                // Kneel/Silhouette reads positions only: the rest goes, and coincident corners are welded.
                Compact(mesh, material != null && material.shader != null && material.shader.name == "Kneel/Silhouette");
                Directory.CreateDirectory(SeamBuilders.MeshFolder);
                string path = $"{SeamBuilders.MeshFolder}/{SceneManager.GetActiveScene().name}_{tag}_{made}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject($"Combined_{made}");
                go.transform.SetParent(group, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                made++;
                batch.Clear();
                vertices = 0;
            }

            foreach (MeshFilter mf in parts)
            {
                Mesh m = mf.sharedMesh;
                if (m == null)
                {
                    continue;
                }

                for (int s = 0; s < m.subMeshCount; s++)
                {
                    batch.Add(new CombineInstance { mesh = m, subMeshIndex = s, transform = toGroup * mf.transform.localToWorldMatrix });
                }

                vertices += m.vertexCount;
                if (vertices > 200000)
                {
                    Flush();
                }
            }

            Flush();
            foreach (Transform child in new List<Transform>(group.Cast<Transform>()))
            {
                if (child.name.StartsWith("Combined_") == false)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            AssetDatabase.SaveAssets();
            return made;
        }

        // A far ridge for silhouettes to stand on: a band `width` wide and `depth` deep at `center`, its crest wandering
        // between minHeight and maxHeight (value noise), facing `yaw`. Saved as a mesh asset.
        public static GameObject Ridge(Transform parent, string name, Vector3 center, float yaw, float width, float depth, float minHeight, float maxHeight, int seed, Material material)
        {
            const int steps = 96;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(seed);
            var knots = new float[steps / 8 + 3];
            for (int i = 0; i < knots.Length; i++)
            {
                knots[i] = (float)rng.NextDouble();
            }

            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps;
                float k = u * (knots.Length - 3);
                int ki = Mathf.FloorToInt(k);
                float h = Mathf.Lerp(minHeight, maxHeight, Mathf.SmoothStep(knots[ki], knots[ki + 1], k - ki) * 0.8f + 0.2f * Mathf.PerlinNoise(seed * 0.1f, u * 23f));
                float x = (u - 0.5f) * width;
                verts.Add(new Vector3(x, -6f, 0f));
                verts.Add(new Vector3(x, h, depth * 0.35f));
                verts.Add(new Vector3(x, -6f, depth));
                if (i > 0)
                {
                    int a = (i - 1) * 3;
                    int b = i * 3;
                    tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a + 1, a + 2, b + 1, b + 1, a + 2, b + 2 });
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Directory.CreateDirectory(SeamBuilders.MeshFolder);
            string path = $"{SeamBuilders.MeshFolder}/{SceneManager.GetActiveScene().name}_{name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Material SilhouetteMaterial(string name, Color color, float fog, Color haze, float hazeTop, float hazeBottom)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Kneel/Silhouette"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_Color", color);
            mat.SetFloat("_FogAmount", fog);
            mat.SetColor("_HazeColor", haze);
            mat.SetFloat("_HazeTop", hazeTop);
            mat.SetFloat("_HazeBottom", hazeBottom);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Maps a pose in one level onto a pose in the other: what stands at `fromOrigin` (facing fromYaw) lands at
        // `toOrigin` (facing toYaw), scaled by `scale` about it.
        public static Matrix4x4 Map(Vector3 fromOrigin, float fromYaw, Vector3 toOrigin, float toYaw, float scale = 1f)
        {
            return Matrix4x4.TRS(toOrigin, Quaternion.Euler(0f, toYaw - fromYaw, 0f), Vector3.one * scale) * Matrix4x4.Translate(-fromOrigin);
        }

        // --- Preview ---

        // Renders the open level's outro (exit) or intro (arrival) at the given seconds, with Kael moved to where
        // the walk would have him, through the post stack. Nothing is saved; Kael goes back.
        public static string Preview(bool exit, float[] times, string tag)
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
            Vector3 home = player.transform.position;
            Quaternion homeRot = player.transform.rotation;
            CameraKey[] keys;
            var path = new List<Vector3>();
            if (exit)
            {
                var e = UnityEngine.Object.FindAnyObjectByType<LevelExit>();
                keys = e.cameraKeys;
                path.Add(e.GetComponent<Collider>().bounds.center);
                if (e.doorStand != null)
                {
                    path.Add(e.doorStand.position);
                }

                foreach (Transform p in e.walkPath)
                {
                    path.Add(p.position);
                }
            }
            else
            {
                var a = UnityEngine.Object.FindAnyObjectByType<LevelArrival>();
                keys = a.cameraKeys;
                path.Add(a.start.position);
                foreach (Transform p in a.walkPath)
                {
                    path.Add(p.position);
                }

                if (a.door != null)
                {
                    a.door.SetOpen(true);
                }
            }

            var sb = new System.Text.StringBuilder();
            string scene = SceneManager.GetActiveScene().name;
            foreach (float t in times)
            {
                Vector3 at = AlongPath(path, t * 1.73f);
                Vector3 next = AlongPath(path, t * 1.73f + 0.5f);
                Vector3 dir = next - at;
                dir.y = 0f;
                player.transform.SetPositionAndRotation(SeamKit.Ground(at, 2f), dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : homeRot);
                var exitDoor = exit ? UnityEngine.Object.FindAnyObjectByType<LevelExit>().door : null;
                if (exitDoor != null)
                {
                    exitDoor.SetOpen(t > 1.6f);
                }
                if (TransitionCamera.Evaluate(keys, t, out Vector3 pos, out Quaternion rot, out float fov))
                {
                    sb.AppendLine(TransitionShots.Shot($"preview_{scene}_{tag}_{t:0.0}", pos, rot, fov > 0f ? fov : 60f, 960, 540));
                }
            }

            player.transform.SetPositionAndRotation(home, homeRot);
            if (exit && UnityEngine.Object.FindAnyObjectByType<LevelExit>().door != null)
            {
                UnityEngine.Object.FindAnyObjectByType<LevelExit>().door.SetOpen(false);
            }

            var arrival = UnityEngine.Object.FindAnyObjectByType<LevelArrival>();
            if (exit == false && arrival != null && arrival.door != null)
            {
                arrival.door.SetOpen(false);
            }

            return sb.ToString();
        }

        private static Vector3 AlongPath(List<Vector3> path, float distance)
        {
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= seg)
                {
                    return Vector3.Lerp(path[i - 1], path[i], seg > 0f ? distance / seg : 1f);
                }

                distance -= seg;
            }

            return path[path.Count - 1];
        }
    }
}
