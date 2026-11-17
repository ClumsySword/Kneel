using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The Master's hall (L5, edited in place: L5Level's Build Scene is never rerun over it).
    public static partial class MasterSetup
    {
        private const string HallFixturesPath = "L5_Root/Art/Hall/Fixtures";

        // The hall chandeliers come out of the hall's baked fixtures mesh into their own bodies, so each can be moved on
        // its own (MasterSetup.HangChandeliers hangs them high and evenly apart). The baked mesh keeps the candelabras.
        // Run once; it skips chandeliers already split.
        [MenuItem("Kneel/Enemies/The Master/Split Hall Chandeliers")]
        public static void SplitHallChandeliers()
        {
            GameObject fixtures = GameObject.Find(HallFixturesPath);
            Transform baked = fixtures.transform.Find("Baked");
            var filter = baked.GetComponent<MeshFilter>();
            var renderer = baked.GetComponent<MeshRenderer>();
            Mesh mesh = filter.sharedMesh;
            List<Transform> chandeliers = fixtures.transform.Cast<Transform>().Where(t => t.name == "Chandelier").ToList();
            if (chandeliers.All(c => c.Find("Body") != null))
            {
                Debug.Log("The hall chandeliers are already split.");
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            Matrix4x4 toWorld = baked.localToWorldMatrix;
            int subs = mesh.subMeshCount;

            // Each triangle goes to the chandelier whose hoop, spokes, candles or chains it belongs to: above 17 m and
            // within 2.9 m of its hanging line. Everything else stays baked.
            var keep = new List<int>[subs];
            var parts = new List<int>[chandeliers.Count][];
            for (int s = 0; s < subs; s++)
                keep[s] = new List<int>();
            for (int c = 0; c < chandeliers.Count; c++)
            {
                parts[c] = new List<int>[subs];
                for (int s = 0; s < subs; s++)
                    parts[c][s] = new List<int>();
            }

            for (int s = 0; s < subs; s++)
            {
                int[] triangles = mesh.GetTriangles(s);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 centroid = toWorld.MultiplyPoint3x4((vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f);
                    int owner = -1;
                    for (int c = 0; c < chandeliers.Count && owner < 0; c++)
                    {
                        Vector3 at = chandeliers[c].position;
                        Vector2 off = new Vector2(centroid.x - at.x, centroid.z - at.z);
                        if (centroid.y > 17f && off.magnitude < 2.9f)
                            owner = c;
                    }

                    List<int> into = owner < 0 ? keep[s] : parts[owner][s];
                    into.Add(triangles[i]);
                    into.Add(triangles[i + 1]);
                    into.Add(triangles[i + 2]);
                }
            }

            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh)).Replace('\\', '/');
            Material[] materials = renderer.sharedMaterials;

            for (int c = 0; c < chandeliers.Count; c++)
            {
                Transform chandelier = chandeliers[c];
                Matrix4x4 toLocal = chandelier.worldToLocalMatrix * toWorld;
                string path = folder + "/L5_Hall_Chandelier_" + Mathf.RoundToInt(chandelier.position.z) + ".asset";
                Mesh body = Build("L5_Hall_Chandelier_" + Mathf.RoundToInt(chandelier.position.z), parts[c], vertices, normals, uvs, toLocal);
                body = SaveMesh(body, path);

                var go = new GameObject("Body");
                go.transform.SetParent(chandelier, false);
                go.layer = baked.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = body;
                var bodyRenderer = go.AddComponent<MeshRenderer>();
                bodyRenderer.sharedMaterials = materials;
                bodyRenderer.renderingLayerMask = renderer.renderingLayerMask;
                bodyRenderer.shadowCastingMode = renderer.shadowCastingMode;
                bodyRenderer.receiveShadows = renderer.receiveShadows;
            }

            Mesh rest = Build(mesh.name, keep, vertices, normals, uvs, Matrix4x4.identity);
            mesh.Clear();
            EditorUtility.CopySerialized(rest, mesh);
            Object.DestroyImmediate(rest);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(fixtures.scene);
            EditorSceneManager.SaveScene(fixtures.scene);
            Debug.Log("Split " + chandeliers.Count + " hall chandeliers out of " + mesh.name + ".");
        }

        private static Mesh Build(string name, List<int>[] subs, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Matrix4x4 transform)
        {
            var remap = new Dictionary<int, int>();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>[subs.Length];
            for (int s = 0; s < subs.Length; s++)
            {
                triangles[s] = new List<int>(subs[s].Count);
                foreach (int index in subs[s])
                {
                    if (remap.TryGetValue(index, out int mapped) == false)
                    {
                        mapped = v.Count;
                        remap[index] = mapped;
                        v.Add(transform.MultiplyPoint3x4(vertices[index]));
                        n.Add(normals.Length > index ? transform.MultiplyVector(normals[index]).normalized : Vector3.up);
                        uv.Add(uvs.Length > index ? uvs[index] : Vector2.zero);
                    }

                    triangles[s].Add(mapped);
                }
            }

            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++)
                mesh.SetTriangles(triangles[s], s, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
