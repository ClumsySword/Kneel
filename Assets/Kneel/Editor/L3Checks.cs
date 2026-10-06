using System.Collections.Generic;
using System.Text;
using Kneel.Hazards;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Reads every manifest prefab back and compares it with its row: collider footprint and height (within
    // 5 cm), pivot at ground level in the centre, layer, cover height, missing scripts and broken materials.
    // Read-only.
    public static class L3Checks
    {
        private const float Tolerance = 0.05f;

        public class Result
        {
            public L3Assets.Item Item;
            public Vector3 ColliderSize;
            public Bounds Visual;
            public List<string> Problems = new List<string>();
            public List<string> Remarks = new List<string>();
        }

        [MenuItem("Kneel/L3/Check/Asset Prefabs")]
        public static void RunMenu()
        {
            Debug.Log(Report(Run()));
        }

        public static string Report(List<Result> results)
        {
            var sb = new StringBuilder();
            int failed = 0;
            foreach (var r in results)
            {
                if (r.Problems.Count > 0)
                {
                    failed++;
                }

                sb.Append(r.Item.Id).Append(' ').Append(r.Item.Name).Append(": ").Append(r.Problems.Count == 0 ? "OK" : "FAIL");
                sb.Append("  collider ").Append(Format(r.ColliderSize)).Append("  mesh ").Append(Format(r.Visual.size)).Append(" top ").Append(r.Visual.max.y.ToString("F2"));
                foreach (var p in r.Problems)
                {
                    sb.Append("\n    ! ").Append(p);
                }

                foreach (var p in r.Remarks)
                {
                    sb.Append("\n    - ").Append(p);
                }

                sb.Append('\n');
            }

            sb.Insert(0, "L3 asset check: " + results.Count + " rows, " + failed + " failing.\n");
            return sb.ToString();
        }

        // x by z by height, the way the design note writes footprints.
        public static string Format(Vector3 size)
        {
            return size.x.ToString("0.##") + " x " + size.z.ToString("0.##") + " x " + size.y.ToString("0.##");
        }

        public static List<Result> Run()
        {
            var results = new List<Result>();
            foreach (var item in L3Assets.Items)
            {
                var result = new Result { Item = item };
                results.Add(result);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.Path);
                if (prefab == null)
                {
                    result.Problems.Add("prefab missing at " + item.Path);
                    continue;
                }

                Check(prefab, item, result);
            }

            return results;
        }

        private static void Check(GameObject prefab, L3Assets.Item item, Result result)
        {
            Transform root = prefab.transform;
            result.Visual = L3Build.LocalBounds(prefab, root);

            // Missing scripts and broken materials, everywhere in the prefab.
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                {
                    result.Problems.Add("missing script on " + t.name);
                }
            }

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                    {
                        result.Problems.Add("broken material on " + r.name);
                    }
                }
            }

            if (item.ExistingPath != null)
            {
                result.Remarks.Add("existing project prefab, used as it is; not measured against a footprint");
                return;
            }

            if (prefab.layer != L3Build.Layer(item.Layer))
            {
                result.Problems.Add("root layer is " + LayerMask.LayerToName(prefab.layer) + ", expected " + item.Layer);
            }

            // Our own colliders: nested project prefabs (the L1 shrine, the L2 pickup) keep theirs.
            var solid = new List<Collider>();
            var triggers = new List<Collider>();
            foreach (var c in prefab.GetComponentsInChildren<Collider>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(c);
                string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : "";
                if (sourcePath.Length > 0 && !sourcePath.StartsWith(L3Build.PrefabsPath))
                {
                    continue;
                }

                (c.isTrigger ? triggers : solid).Add(c);
            }

            switch (item.Collider)
            {
                case L3Assets.Col.None:
                    if (solid.Count + triggers.Count > 0)
                    {
                        result.Problems.Add("has " + (solid.Count + triggers.Count) + " collider(s); the row says none");
                    }

                    break;
                case L3Assets.Col.Box:
                case L3Assets.Col.Cylinder:
                case L3Assets.Col.Capsule:
                    if (solid.Count != 1)
                    {
                        result.Problems.Add("expected one solid collider, found " + solid.Count);
                        break;
                    }

                    Bounds b = BoundsOf(solid[0], root);
                    result.ColliderSize = b.size;
                    Compare(result, "collider", b, item.Size);
                    if (item.Collider == L3Assets.Col.Capsule && !(solid[0] is CapsuleCollider))
                    {
                        result.Problems.Add("expected a capsule collider");
                    }

                    if (item.Collider == L3Assets.Col.Cylinder && !(solid[0] is MeshCollider))
                    {
                        result.Problems.Add("expected a cylinder (convex mesh) collider");
                    }

                    break;
                default:
                    Custom(prefab, item, result, solid, triggers);
                    break;
            }

            // Cover blocks a hound but never hides the player.
            if (item.Table.StartsWith("14.3") && item.Collider != L3Assets.Col.None)
            {
                if (item.Size.y < 1.2f - 0.001f || item.Size.y > 1.8f + 0.001f)
                {
                    result.Problems.Add("cover height " + item.Size.y + " is outside 1.2 to 1.8");
                }

                if (result.Visual.max.y > 1.8f + Tolerance)
                {
                    result.Problems.Add("mesh top " + result.Visual.max.y.ToString("F2") + " is above the 1.8 m cover limit");
                }
            }

            if (item.Size.y > 0f && result.Visual.size.sqrMagnitude > 0f && result.Visual.max.y > item.Size.y + Tolerance && !item.Table.StartsWith("14.6"))
            {
                result.Remarks.Add("mesh top " + result.Visual.max.y.ToString("F2") + " is above the row's height " + item.Size.y);
            }
        }

        private static void Compare(Result result, string what, Bounds actual, Vector3 size, Vector3? baseCentre = null)
        {
            Vector3 centre = baseCentre ?? Vector3.zero;
            if (Mathf.Abs(actual.size.x - size.x) > Tolerance || Mathf.Abs(actual.size.y - size.y) > Tolerance || Mathf.Abs(actual.size.z - size.z) > Tolerance)
            {
                result.Problems.Add(what + " is " + Format(actual.size) + ", expected " + Format(size));
            }

            if (Mathf.Abs(actual.center.x - centre.x) > Tolerance || Mathf.Abs(actual.center.z - centre.z) > Tolerance || Mathf.Abs(actual.min.y - centre.y) > Tolerance)
            {
                result.Problems.Add(what + " is centred on (" + actual.center.x.ToString("F2") + ", " + actual.center.z.ToString("F2") + ") with its base at " + actual.min.y.ToString("F2")
                    + ", expected (" + centre.x + ", " + centre.z + ") and " + centre.y);
            }
        }

        private static Collider Named(GameObject prefab, string path)
        {
            var t = prefab.transform.Find(path);
            return t != null ? t.GetComponent<Collider>() : null;
        }

        private static void Expect(Result result, GameObject prefab, string path, Vector3 size, Vector3 baseCentre, bool trigger, string layer)
        {
            var c = path.Length == 0 ? prefab.GetComponent<Collider>() : Named(prefab, path);
            string what = path.Length == 0 ? "root collider" : path;
            if (c == null)
            {
                result.Problems.Add(what + " has no collider");
                return;
            }

            Compare(result, what, BoundsOf(c, prefab.transform), size, baseCentre);
            if (c.isTrigger != trigger)
            {
                result.Problems.Add(what + (trigger ? " should be a trigger" : " should be solid"));
            }

            if (c.gameObject.layer != L3Build.Layer(layer))
            {
                result.Problems.Add(what + " is on " + LayerMask.LayerToName(c.gameObject.layer) + ", expected " + layer);
            }
        }

        private static void Custom(GameObject prefab, L3Assets.Item item, Result result, List<Collider> solid, List<Collider> triggers)
        {
            Transform root = prefab.transform;
            switch (item.Id)
            {
                case "H01":
                    Expect(result, prefab, "", new Vector3(2f, 2f, 2f), Vector3.zero, true, L3Build.Hazard);
                    result.ColliderSize = new Vector3(2f, 2f, 2f);
                    var obstacle = prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                    if (obstacle == null || !obstacle.carving || obstacle.enabled)
                    {
                        result.Problems.Add("needs a carving NavMeshObstacle, saved disabled");
                    }

                    break;
                case "H02":
                    var row = prefab.GetComponent<CropRow>();
                    if (row == null || row.Cells.Count != 12)
                    {
                        result.Problems.Add("expected a CropRow with 12 cells");
                        break;
                    }

                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 p = root.InverseTransformPoint(row.Cells[i].transform.position);
                        if (Mathf.Abs(p.x) > Tolerance || Mathf.Abs(p.z - (1f + 2f * i)) > Tolerance)
                        {
                            result.Problems.Add("cell " + i + " is at z " + p.z.ToString("F2") + ", expected " + (1f + 2f * i));
                        }
                    }

                    result.ColliderSize = new Vector3(2f, 2f, 24f);
                    if (prefab.GetComponentInChildren<Unity.AI.Navigation.NavMeshModifierVolume>(true) == null)
                    {
                        result.Problems.Add("no NavMeshModifierVolume");
                    }

                    break;
                case "H03":
                    Expect(result, prefab, "Hit", new Vector3(0.3f, 1.5f, 0.3f), Vector3.zero, true, L3Build.Hittable);
                    result.ColliderSize = new Vector3(0.3f, 1.5f, 0.3f);
                    break;
                case "H04":
                    Expect(result, prefab, "", new Vector3(4f, 2f, 4f), Vector3.zero, true, L3Build.Hazard);
                    result.ColliderSize = new Vector3(4f, 2f, 4f);
                    break;
                case "H06":
                    Expect(result, prefab, "", new Vector3(6f, 2.5f, 6f), Vector3.zero, true, L3Build.Hazard);
                    result.ColliderSize = new Vector3(6f, 2.5f, 6f);
                    break;
                case "S01":
                    Expect(result, prefab, "Deck", new Vector3(6f, 0.5f, 10f), new Vector3(0f, -0.5f, 0f), false, L3Build.Walkable);
                    Expect(result, prefab, "Parapet_West", new Vector3(0.5f, 1f, 10f), new Vector3(-2.75f, 0f, 0f), false, L3Build.Boundary);
                    Expect(result, prefab, "Parapet_East", new Vector3(0.5f, 1f, 10f), new Vector3(2.75f, 0f, 0f), false, L3Build.Boundary);
                    result.ColliderSize = new Vector3(6f, 1f, 10f);
                    break;
                case "S02":
                    Expect(result, prefab, "Deck", new Vector3(4f, 0.3f, 10f), new Vector3(0f, -0.3f, 0f), false, L3Build.Walkable);
                    result.ColliderSize = new Vector3(4f, 1f, 10f);
                    var rails = root.Find("Rails");
                    if (rails == null || rails.childCount != 10)
                    {
                        result.Problems.Add("expected five L3_Rail per side");
                    }

                    break;
                case "S03":
                    Bounds walls = Union(solid.FindAll(c => c.gameObject.layer == L3Build.Layer(L3Build.Boundary)), root);
                    result.ColliderSize = walls.size;
                    Compare(result, "walls", walls, new Vector3(11f, 3.2f, 12f));
                    foreach (var c in solid)
                    {
                        Bounds wb = BoundsOf(c, root);
                        if (c.gameObject.layer == L3Build.Layer(L3Build.Boundary) && wb.min.x < -4.4f && wb.max.z > -1.95f && wb.min.z < 1.95f)
                        {
                            result.Problems.Add(c.name + " blocks the 4 m door gap in the west wall");
                        }
                    }

                    Expect(result, prefab, "Floor", new Vector3(10f, 0.5f, 10f), new Vector3(-0.5f, -0.5f, 0f), false, L3Build.Walkable);
                    if (prefab.GetComponentsInChildren<Markers.FadeOnEnterMarker>(true).Length != 2)
                    {
                        result.Problems.Add("roof and south wall should each carry a FadeOnEnterMarker");
                    }

                    break;
                case "M01":
                    if (prefab.GetComponent<Markers.ShrineMarker>() == null)
                    {
                        result.Problems.Add("no ShrineMarker");
                    }

                    result.Remarks.Add("the nested L1 shrine measures " + Format(result.Visual.size) + " (the row's fallback pillar is 1 x 1 x 1.5)");
                    break;
                case "M02":
                    Expect(result, prefab, "Blocker", new Vector3(4f, 2f, 0.3f), Vector3.zero, false, L3Build.Unlayered);
                    result.ColliderSize = new Vector3(4f, 2f, 0.3f);
                    var carve = prefab.GetComponent<UnityEngine.AI.NavMeshObstacle>();
                    if (carve == null || !carve.carving || carve.enabled)
                    {
                        result.Problems.Add("needs a carving NavMeshObstacle, saved disabled");
                    }

                    break;
                case "M08":
                    Expect(result, prefab, "", new Vector3(8f, 4f, 6f), Vector3.zero, true, L3Build.Marker);
                    result.ColliderSize = new Vector3(8f, 4f, 6f);
                    break;
                case "M09":
                    Expect(result, prefab, "", new Vector3(8f, 6f, 8f), Vector3.zero, true, L3Build.Marker);
                    result.ColliderSize = new Vector3(8f, 6f, 8f);
                    break;
                case "M10b":
                    Expect(result, prefab, "", new Vector3(4f, 3f, 4f), Vector3.zero, true, L3Build.Marker);
                    result.ColliderSize = new Vector3(4f, 3f, 4f);
                    break;
                case "E02":
                    var capsule = prefab.GetComponent<CapsuleCollider>();
                    if (capsule == null || capsule.direction != 2 || Mathf.Abs(capsule.radius - 0.4f) > 0.001f || Mathf.Abs(capsule.height - 1.4f) > 0.001f)
                    {
                        result.Problems.Add("expected a capsule lying along Z, 1.4 long, radius 0.4");
                    }
                    else
                    {
                        result.ColliderSize = BoundsOf(capsule, root).size;
                        Compare(result, "capsule", BoundsOf(capsule, root), new Vector3(0.8f, 0.8f, 1.4f));
                    }

                    if (prefab.GetComponents<MonoBehaviour>().Length > 0)
                    {
                        result.Problems.Add("a stand-in should have a collider and nothing else");
                    }

                    break;
            }
        }

        private static Bounds Union(List<Collider> colliders, Transform root)
        {
            var b = new Bounds();
            for (int i = 0; i < colliders.Count; i++)
            {
                if (i == 0)
                {
                    b = BoundsOf(colliders[i], root);
                }
                else
                {
                    b.Encapsulate(BoundsOf(colliders[i], root));
                }
            }

            return b;
        }

        // A collider's box in the prefab root's space, worked out from its own numbers (prefab assets have no
        // physics bounds to read).
        public static Bounds BoundsOf(Collider collider, Transform root)
        {
            Vector3 centre, size;
            switch (collider)
            {
                case BoxCollider box:
                    centre = box.center;
                    size = box.size;
                    break;
                case CapsuleCollider capsule:
                    centre = capsule.center;
                    float d = capsule.radius * 2f;
                    size = new Vector3(d, d, d);
                    size[capsule.direction] = Mathf.Max(capsule.height, d);
                    break;
                case SphereCollider sphere:
                    centre = sphere.center;
                    size = Vector3.one * sphere.radius * 2f;
                    break;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    centre = mesh.sharedMesh.bounds.center;
                    size = mesh.sharedMesh.bounds.size;
                    break;
                default:
                    return new Bounds();
            }

            Matrix4x4 m = root.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            var bounds = new Bounds(m.MultiplyPoint3x4(centre), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = centre + Vector3.Scale(size * 0.5f, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bounds.Encapsulate(m.MultiplyPoint3x4(corner));
            }

            return bounds;
        }
    }
}
