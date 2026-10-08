using System.Collections.Generic;
using Kneel.Hazards;
using Kneel.Markers;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Kneel.EditorTools
{
    // Builds the L3 level scene from L3Plan (section 15 of the note), every stage up to the one asked for. The
    // scene is generated from nothing each time: Build replaces it. Floors, ramps, the ditch, the two solid
    // blocks and the backdrop are scene geometry; everything else is an instance of a section 14 prefab.
    public static class L3Level
    {
        public const string RootName = "L3_Root";
        public const string ScenePath = L3Build.ScenesPath + "/L3_FallowFields.unity";
        public const string SceneFolder = L3Build.ScenesPath + "/L3_FallowFields";
        public const string IgniteTriggerPath = L3Build.PrefabsPath + "/Hazards/L3_IgniteTrigger.prefab";
        private const string MeshFolder = "Level";
        private const float Thickness = 0.5f;

        public static readonly (string name, float radius, float height)[] Agents =
        {
            ("Thrall", 0.5f, 2f), ("Brute", 1f, 2.6f), ("Hound", 0.4f, 0.9f),
        };

        [MenuItem("Kneel/L3/Build/Level (replaces the scene)")]
        public static void BuildMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild L3_FallowFields", "This replaces the level scene with a freshly generated one. Hand edits to the scene are lost.", "Rebuild", "Cancel"))
            {
                Debug.Log(Build('E'));
            }
        }

        public static GameObject Root => GameObject.Find(RootName);

        // ---------------------------------------------------------------- Build

        public static string Build(char stage)
        {
            if (EditorApplication.isPlaying)
            {
                return "Stop play mode first.";
            }

            L2Build.EnsureFolder(L3Build.ScenesPath);
            L2Build.EnsureFolder(SceneFolder);
            EnsureExtras();
            L3Ground.Paint(stage);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            foreach (var name in new[] { "Floors", "Boundaries", "Props", "Hazards", "Markers", "Spawns", "Dressing" })
            {
                L3Build.Child(root, name);
            }

            var log = new List<string>();
            log.Add(Floors(root, stage));
            Backdrop(root, stage);
            Blocks(root, stage);
            if (stage >= 'B')
            {
                Ditch(root);
            }

            log.Add(Boundaries(root, stage));
            log.Add(Places(root, stage));
            log.Add(L3Dressing.Build(root, stage, lastPieces));
            log.Add(L3Fields.Build(root, stage, lastPieces));
            if (stage >= 'E')
            {
                log.Add(SpawnMarkers(root));
                log.Add(Triggers(root));
            }

            PlayerAndCamera();
            Kneel.Lighting.EditorTools.L3LightingPass.Apply(root);
            Physics.SyncTransforms();
            log.Add(BakeNavMesh(root.gameObject));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return "L3_FallowFields built through stage " + stage + ": " + string.Join("; ", log);
        }

        private static Transform Group(Transform root, string group, string area)
        {
            var parent = root.Find(group);
            var child = parent.Find(area);
            return child != null ? child : L3Build.Child(parent, area).transform;
        }

        // The little trigger that lights the demonstration row (15.4). Not a section 14 row: the note asks for
        // the behaviour, and a prefab keeps it out of loose scene objects.
        private static void EnsureExtras()
        {
            var root = new GameObject("L3_IgniteTrigger");
            root.layer = L3Build.Layer(L3Build.Marker);
            var size = new Vector3(8f, 3f, 2f);
            L3Build.StandingBox(root, size, true);
            var visual = L3Build.Block(root.transform, "Visual", size, Vector3.zero, L3Build.Mat("L3_GB_Marker"));
            visual.layer = root.layer;
            visual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visual.AddComponent<MarkerVisual>();
            var trigger = root.AddComponent<RowIgniteTrigger>();
            L3Build.Set(trigger, "playerMask", (LayerMask)LayerMask.GetMask("Player"));
            L2Build.EnsureFolder(L3Build.PrefabsPath + "/Hazards");
            PrefabUtility.SaveAsPrefabAsset(root, IgniteTriggerPath);
            Object.DestroyImmediate(root);

            L3Build.Flat("L3_Ditch_Bed", "#14110E", 0.5f);
            L3Build.Flat("L3_Trench", "#0E0C0A", 0.1f);
        }

        // ---------------------------------------------------------------- Floors

        private static string Floors(Transform root, char stage)
        {
            int count = 0;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                FloorObject(Group(root, "Floors", f.Area), f);
                count++;
            }

            return count + " floor pieces";
        }

        private static GameObject FloorObject(Transform parent, L3Plan.Floor f)
        {
            var centre = new Vector2((f.X0 + f.X1) * 0.5f, (f.Z0 + f.Z1) * 0.5f);
            var go = L3Build.Child(parent, f.Id + " " + f.Label, new Vector3(centre.x, 0f, centre.y));
            go.layer = L3Build.Layer(L3Build.Walkable);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

            var tops = new Vector3[f.Poly.Length];
            for (int i = 0; i < tops.Length; i++)
            {
                tops[i] = new Vector3(f.Poly[i].x - centre.x, f.HeightAt(f.Poly[i].y), f.Poly[i].y - centre.y);
            }

            var mesh = Slab(tops, Thickness, new Vector3(centre.x, 0f, centre.y), true, true);
            mesh = SaveMesh(mesh, "L3_Floor_" + f.Id);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = L3Ground.Material;
            if (f.Diagonal || f.Ramp)
            {
                // The collider keeps all its faces.
                var solid = SaveMesh(Slab(tops, Thickness, new Vector3(centre.x, 0f, centre.y), true, false), "L3_FloorCollider_" + f.Id);
                go.AddComponent<MeshCollider>().sharedMesh = solid;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(f.X1 - f.X0, Thickness, f.Z1 - f.Z0);
                box.center = new Vector3(0f, f.HSouth - Thickness * 0.5f, 0f);
            }

            return go;
        }

        // A slab under a top polygon (counter-clockwise seen from above), with the layout UVs of the ground.
        // drawn: the mesh is for a renderer with the ground material. The ground shader builds its tangent
        // frame from world +X, which has no answer on a face that points due east or west (it returns NaN,
        // and bloom spreads one such pixel into a white patch), so those side faces are left out. They are
        // never needed: banks, walls or the backdrop stand against every floor edge.
        private static Mesh Slab(Vector3[] tops, float thickness, Vector3 origin, bool bottom, bool drawn)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    uvs.Add(L3Ground.Uv(origin.x + v.x, origin.z + v.z));
                }

                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            Vector3 Down(Vector3 v)
            {
                return v + Vector3.down * thickness;
            }

            Quad(tops[0], tops[3], tops[2], tops[1]);
            for (int i = 0; i < tops.Length; i++)
            {
                Vector3 a = tops[i], b = tops[(i + 1) % tops.Length];
                var along = new Vector2(b.x - a.x, b.z - a.z).normalized;
                if (drawn && Mathf.Abs(along.y) > 0.985f)
                {
                    continue;
                }

                Quad(a, b, Down(b), Down(a));
            }

            if (bottom)
            {
                Quad(Down(tops[0]), Down(tops[1]), Down(tops[2]), Down(tops[3]));
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            L2Build.EnsureFolder(L3Build.MeshesPath + "/" + MeshFolder);
            string path = L3Build.MeshesPath + "/" + MeshFolder + "/" + name + ".asset";
            mesh.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------------------------------------------------------------- Backdrop

        // The land the level sits in: a sheet with no collider, just under every floor, that rises to the top
        // of the banks round the raised ground. It turns the crest and the knoll into hills and the sunken lane
        // into a cutting, and keeps the void out of frame. Not walkable, not baked, safe to delete.
        public static float BackdropHeight(Vector2 p, char stage)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (f.Stage <= stage && f.Contains(p, 0.001f))
                {
                    // Level floors: just under the slab, so the land meets a fenced lane's edge without a step.
                    return f.Virtual ? -0.05f : f.Raised ? f.HeightAt(p.y) - 0.3f : -0.04f;
                }
            }

            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage <= stage && b.Contains(p, 0.001f))
                {
                    return b.Top - 0.3f;
                }
            }

            float sum = 0f, weights = 0f, nearest = float.MaxValue;
            foreach (var f in L3Plan.Floors)
            {
                if (f.Stage > stage)
                {
                    continue;
                }

                Vector2 q = f.Nearest(p);
                nearest = Mathf.Min(nearest, (p - q).magnitude);
                float e = f.Virtual ? 0f : f.HeightAt(q.y);
                if (f.Id == "F01" && q.y <= L3Plan.SunkenEnd)
                {
                    e = L3Plan.SunkenTop;
                }

                float w = 1f / Mathf.Pow((p - q).magnitude + 0.5f, 4f);
                sum += e * w;
                weights += w;
            }

            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage > stage)
                {
                    continue;
                }

                var q = new Vector2(Mathf.Clamp(p.x, b.X0, b.X1), Mathf.Clamp(p.y, b.Z0, b.Z1));
                nearest = Mathf.Min(nearest, (p - q).magnitude);
                float w = 1f / Mathf.Pow((p - q).magnitude + 0.5f, 4f);
                sum += b.Top * w;
                weights += w;
            }

            // Open country is never level: it rolls a little, more with distance from anything built.
            float roll = (L3Ground.Fbm(p.x * 0.045f, p.y * 0.045f) - 0.5f) * 1.8f + (L3Ground.Fbm(p.x * 0.16f + 5f, p.y * 0.16f) - 0.5f) * 0.4f;
            return sum / weights - 0.05f + roll * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.6f, 12f, nearest));
        }

        private static void Backdrop(Transform root, char stage)
        {
            const float cell = 2f;
            int nx = Mathf.RoundToInt(L3Plan.GroundWidth / cell), nz = Mathf.RoundToInt(L3Plan.GroundLength / cell);
            var verts = new Vector3[(nx + 1) * (nz + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    float x = L3Plan.GroundMinX + i * cell, z = L3Plan.GroundMinZ + j * cell;
                    verts[j * (nx + 1) + i] = new Vector3(x, BackdropHeight(new Vector2(x, z), stage), z);
                    uvs[j * (nx + 1) + i] = L3Ground.Uv(x, z);
                }
            }

            var tris = new List<int>();
            var ditch = L3Plan.Ditch;
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    float x = L3Plan.GroundMinX + i * cell, z = L3Plan.GroundMinZ + j * cell;
                    // Nothing in the ditch: it has its own bed and sides.
                    if (stage >= ditch.Stage && x >= ditch.X0 - 0.01f && x + cell <= ditch.X1 + 0.01f && z >= ditch.Z0 - 0.01f && z + cell <= ditch.Z1 + 0.01f)
                    {
                        continue;
                    }

                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, d, a, d, b });
                }
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, "L3_Backdrop");

            var go = L3Build.Child(root.Find("Floors"), "_Backdrop");
            go.layer = L3Build.Layer(L3Build.Unlayered);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = L3Ground.Material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ---------------------------------------------------------------- Solid blocks and the ditch

        private static void NotWalkable(GameObject go)
        {
            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = go.AddComponent<NavMeshModifier>();
            }

            modifier.overrideArea = true;
            modifier.area = 1;
            modifier.applyToChildren = true;
        }

        private static void Blocks(Transform root, char stage)
        {
            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage > stage)
                {
                    continue;
                }

                var centre = new Vector3((b.X0 + b.X1) * 0.5f, 0f, (b.Z0 + b.Z1) * 0.5f);
                var go = L3Build.Child(Group(root, "Floors", b.Area), "Block " + b.Id, centre);
                go.layer = L3Build.Layer(L3Build.Boundary);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(b.X1 - b.X0, b.Top, b.Z1 - b.Z0);
                box.center = new Vector3(0f, b.Top * 0.5f, 0f);
                NotWalkable(go);

                // Its top is ground like any other; its sides are faced with bank pieces (see Boundaries).
                float hx = (b.X1 - b.X0) * 0.5f, hz = (b.Z1 - b.Z0) * 0.5f, top = b.Top - 0.02f;
                var tops = new[] { new Vector3(-hx, top, -hz), new Vector3(hx, top, -hz), new Vector3(hx, top, hz), new Vector3(-hx, top, hz) };
                var mesh = SaveMesh(Slab(tops, 0.2f, centre, false, true), "L3_Block_" + b.Id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = L3Ground.Material;
            }
        }

        private static void Ditch(Transform root)
        {
            var d = L3Plan.Ditch;
            float length = d.X1 - d.X0, width = d.Z1 - d.Z0, cx = (d.X0 + d.X1) * 0.5f, cz = (d.Z0 + d.Z1) * 0.5f;
            var ditch = L3Build.Child(Group(root, "Floors", "BR"), "Ditch").transform;
            NotWalkable(ditch.gameObject);

            var bed = L3Build.Block(ditch, "Bed", new Vector3(length, Thickness, width), new Vector3(cx, -3f - Thickness, cz), L3Build.Mat("L3_Ditch_Bed"));
            bed.layer = L3Build.Layer(L3Build.Walkable);
            bed.AddComponent<BoxCollider>();

            // Standing water over the bed: dark, and low enough that the fall still reads as a fall. A sheet
            // made for the project's water shader; its shore is wherever the banks rise through it.
            var water = L3Build.Child(ditch, "Water");
            water.AddComponent<MeshFilter>().sharedMesh = SaveMesh(L3Ditch.Water(), "L3_Ditch_Water");
            var waterRenderer = water.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = L3Ditch.WaterMaterial();
            waterRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // The vertical sides of the cut are colliders only, a hair inside the rim so they never share a face
            // with the verge slabs. What is seen is the banks (L3Ditch).
            const float t = 0.3f, inset = 0.01f;
            Side(ditch, "Side_South", new Vector3(length, 3f, t), new Vector3(cx, -3f, d.Z0 - t * 0.5f + inset));
            Side(ditch, "Side_North", new Vector3(length, 3f, t), new Vector3(cx, -3f, d.Z1 + t * 0.5f - inset));
            Side(ditch, "Side_West", new Vector3(t, 3f, width + 2f * t), new Vector3(d.X0 - t * 0.5f + inset, -3f, cz));
            Side(ditch, "Side_East", new Vector3(t, 3f, width + 2f * t), new Vector3(d.X1 + t * 0.5f - inset, -3f, cz));

            L3Ditch.Build(ditch);

            var kill = L3Build.Instance(L3Assets.Load("H06"), Group(root, "Hazards", "BR"), new Vector3(cx, -3f, cz));
            kill.name = "DitchKill";
            L3Assets.SizeVolume(kill, new Vector3(length, 2.5f, width));
            Record(kill);
        }

        private static void Side(Transform parent, string name, Vector3 size, Vector3 baseCentre)
        {
            var side = L3Build.Child(parent, name, baseCentre);
            side.layer = L3Build.Layer(L3Build.Boundary);
            var box = side.AddComponent<BoxCollider>();
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
        }

        // ---------------------------------------------------------------- Boundaries (15.3)

        public class Piece
        {
            public string Kit, Area, Why;
            public Vector2 P;
            public float BaseY, Yaw, Lean;
            public bool South;

            // A low piece on the outside of the level: the player must not be able to jump onto it.
            public bool Capped;

            // Stands on a raised edge or a ramp, where the floor beside it is not level with its base.
            public bool Raised;
        }

        // Ground under a run of boundary pieces that wander outward off the floor's edge.
        public class Pad
        {
            public string Area;
            public Vector2 Centre, Size;
            public float Yaw;
        }

        public static readonly List<Pad> Pads = new List<Pad>();

        // A steady number in 0..1 from a name, so every rebuild places things the same way.
        public static float Hash(string key, int i = 0)
        {
            uint h = 2166136261u;
            foreach (char c in key)
            {
                h = (h ^ c) * 16777619u;
            }

            h = (h ^ (uint)i) * 16777619u;
            h ^= h >> 13;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        private static float Depth(string kit)
        {
            return kit == "K05" ? 2f : Fence(kit) ? 0.2f : kit == "K07" ? 0.5f : 1f;
        }

        // K06 and its weathered variants: one fence, three looks, the same collider.
        public static bool Fence(string kit)
        {
            return kit == "K06" || kit == "K09" || kit == "K10";
        }

        // Cuts [a, b] out of a list of intervals.
        private static void Cut(List<Vector2> intervals, float a, float b)
        {
            for (int i = intervals.Count - 1; i >= 0; i--)
            {
                Vector2 v = intervals[i];
                if (b <= v.x + 0.01f || a >= v.y - 0.01f)
                {
                    continue;
                }

                intervals.RemoveAt(i);
                if (a > v.x + 0.01f)
                {
                    intervals.Add(new Vector2(v.x, a));
                }

                if (b < v.y - 0.01f)
                {
                    intervals.Add(new Vector2(b, v.y));
                }
            }
        }

        private struct Contact
        {
            public float A, B, Height;
        }

        // Where other floors meet one side of f: the range along the edge and the other floor's height there.
        private static List<Contact> Contacts(L3Plan.Floor f, char side, char stage)
        {
            var result = new List<Contact>();
            bool alongX = side == 'S' || side == 'N';
            float line = side == 'S' ? f.Z0 : side == 'N' ? f.Z1 : side == 'W' ? f.X0 : f.X1;
            float lo = alongX ? f.X0 : f.Z0, hi = alongX ? f.X1 : f.Z1;
            foreach (var g in L3Plan.Floors)
            {
                if (g == f || g.Virtual || g.Stage > stage)
                {
                    continue;
                }

                float a, b, height;
                if (g.Diagonal)
                {
                    // A diagonal lane meets its neighbours along the two lines it was cut at.
                    if (g.ClipX == alongX)
                    {
                        continue;
                    }

                    bool atStart = (side == 'E' || side == 'N') && Mathf.Abs(g.ClipA - line) < 0.01f;
                    bool atEnd = (side == 'W' || side == 'S') && Mathf.Abs(g.ClipB - line) < 0.01f;
                    if (!atStart && !atEnd)
                    {
                        continue;
                    }

                    Vector2 p = atStart ? g.Poly[0] : g.Poly[1], q = atStart ? g.Poly[3] : g.Poly[2];
                    a = alongX ? Mathf.Min(p.x, q.x) : Mathf.Min(p.y, q.y);
                    b = alongX ? Mathf.Max(p.x, q.x) : Mathf.Max(p.y, q.y);
                    height = 0f;
                }
                else
                {
                    float other = side == 'S' ? g.Z1 : side == 'N' ? g.Z0 : side == 'W' ? g.X1 : g.X0;
                    if (Mathf.Abs(other - line) > 0.01f)
                    {
                        continue;
                    }

                    a = alongX ? g.X0 : g.Z0;
                    b = alongX ? g.X1 : g.Z1;
                    float mid = (Mathf.Max(a, lo) + Mathf.Min(b, hi)) * 0.5f;
                    height = alongX ? g.HeightAt(line) : g.HeightAt(mid);
                }

                a = Mathf.Max(a, lo);
                b = Mathf.Min(b, hi);
                if (b - a > 0.01f)
                {
                    result.Add(new Contact { A = a, B = b, Height = height });
                }
            }

            return result;
        }

        private static bool OnAnyGround(Vector2 p, char stage)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (f.Stage <= stage && f.Contains(p))
                {
                    return true;
                }
            }

            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage <= stage && b.Contains(p))
                {
                    return true;
                }
            }

            return false;
        }

        // Pieces along a line from a to b, pushed out by half their depth. A hedge is not a ruled line and a
        // drystone wall is not a row of identical blocks: each run wanders outward along its length (never
        // inward, so no floor is lost, and back to the line at both ends, so gates and corners stay true),
        // and each piece turns with the run, leans a little and sits at its own height.
        private static void Run(List<Piece> pieces, string kit, string area, string why, Vector2 a, Vector2 b, Vector2 outward, System.Func<Vector2, float> baseY, bool south = false, float offset = -1f, bool raised = false)
        {
            float length = (b - a).magnitude;
            if (length < 0.05f)
            {
                return;
            }

            Vector2 dir = (b - a) / length;
            bool hedge = kit == "K01" || kit == "K02", stone = kit == "K03" || kit == "K04", fence = kit == "K06";
            bool edge = outward.sqrMagnitude > 0.5f;
            float pieceLength = kit == "K07" ? 1f : 2f;
            // Hedges are set closer than their length, so a turned piece still meets its neighbours.
            float module = hedge ? 1.7f : pieceLength;
            int n = Mathf.Max(1, Mathf.CeilToInt((length - 0.05f) / module));
            if (offset < 0f)
            {
                offset = Depth(kit) * 0.5f;
            }

            string key = why + a + b;
            float amplitude = !edge || raised ? 0f : hedge ? 0.75f : stone ? 0.3f : fence ? 0.28f : 0f;
            float phase = Hash(key) * 97f;
            float Wander(float along)
            {
                if (amplitude <= 0f)
                {
                    return 0f;
                }

                float taper = Mathf.SmoothStep(0f, 1f, along / 3f) * Mathf.SmoothStep(0f, 1f, (length - along) / 3f);
                float wave = Mathf.PerlinNoise(phase, along * 0.11f) * 0.7f + Mathf.PerlinNoise(phase + 31f, along * 0.31f) * 0.3f;
                return amplitude * taper * Mathf.Clamp01(wave * 1.5f - 0.2f);
            }

            if (edge && !raised && kit != "K05")
            {
                // The run stands partly off the floor: give it ground of its own to stand on.
                float width = Depth(kit) + amplitude + 1.3f;
                Pads.Add(new Pad
                {
                    Area = area, Centre = (a + b) * 0.5f + outward * (width * 0.5f - 0.15f), Size = new Vector2(length, width),
                    Yaw = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg,
                });
            }

            Vector2 across = edge ? outward : new Vector2(dir.y, -dir.x);
            for (int i = 0; i < n; i++)
            {
                float along = n == 1 ? length * 0.5f : pieceLength * 0.5f + i * (length - pieceLength) / (n - 1);
                Vector2 onEdge = a + dir * along;
                float wander = Wander(along), slope = (Wander(along + 0.4f) - Wander(along - 0.4f)) / 0.8f;
                Vector2 tangent = (dir + across * slope).normalized;
                float r1 = Hash(key, i * 4 + 1), r2 = Hash(key, i * 4 + 2), r3 = Hash(key, i * 4 + 3), r4 = Hash(key, i * 4 + 4);

                float yaw = Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg;
                float sink = 0f, lean = 0f, sideways = 0f;
                string placed = kit;
                // The piece at each end of a run is a gatepost or a corner: it stands true, so openings keep
                // their full width.
                if (edge && (i == 0 || i == n - 1))
                {
                    r1 = 0.5f;
                    r4 = 0.5f;
                }

                if (hedge)
                {
                    yaw += (r1 - 0.5f) * 14f;
                    sideways = edge && !raised ? r2 * 0.14f : 0f;
                    sink = kit == "K01" ? r3 * 0.35f : r3 * 0.08f;
                    lean = (r4 - 0.5f) * 8f;
                }
                else if (stone)
                {
                    yaw += (r1 - 0.5f) * 7f;
                    sideways = raised ? 0.09f : edge ? r2 * 0.06f : (r2 - 0.5f) * 0.14f;
                    sink = kit == "K04" ? r3 * 0.3f : r3 * 0.05f;
                    lean = (r4 - 0.5f) * 5f;
                }
                else if (kit == "K05")
                {
                    // Square on plan: any quarter turn fits, and no two faces in a row match.
                    yaw += 90f * Mathf.FloorToInt(r1 * 4f);
                }
                else if (fence)
                {
                    // A field fence: no two panels quite in line, each settled and leaning its own way.
                    yaw += (r1 - 0.5f) * (edge ? 7f : 3f);
                    sink = edge ? r3 * 0.07f : 0f;
                    lean = edge ? (r4 - 0.5f) * 11f : 0f;
                    // One panel in three has seen better days. Gateposts stay whole.
                    float worn = Hash(key, i * 4 + 7);
                    if (edge && i > 0 && i < n - 1)
                    {
                        placed = worn < 0.2f ? "K09" : worn < 0.34f ? "K10" : "K06";
                    }
                }

                // Every other piece is turned about, so a run does not repeat the same face.
                if (kit != "K05" && i % 2 == 1)
                {
                    yaw += 180f;
                    lean = -lean;
                }

                pieces.Add(new Piece
                {
                    Kit = placed, Area = area, Why = why, P = onEdge + across * (offset + wander + sideways), BaseY = baseY(onEdge) - sink, South = south, Yaw = yaw, Lean = lean,
                    Capped = edge && (kit == "K02" || kit == "K03" || fence), Raised = raised,
                });
            }
        }

        public static List<Piece> BoundaryPieces(char stage)
        {
            var pieces = new List<Piece>();
            Pads.Clear();
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Stage > stage)
                {
                    continue;
                }

                if (f.Diagonal)
                {
                    for (int e = 0; e < 4; e += 2)
                    {
                        Vector2 a = f.Poly[e], b = f.Poly[e + 1];
                        Vector2 ab = (b - a).normalized;
                        Run(pieces, f.West, f.Area, f.Id + " side", a, b, new Vector2(ab.y, -ab.x), _ => 0f);
                    }

                    continue;
                }

                foreach (char side in "SWEN")
                {
                    Edge(pieces, f, side, stage);
                }
            }

            // The solid blocks are faced with bank pieces inside their own outline.
            foreach (var b in L3Plan.Blocks)
            {
                if (b.Stage > stage)
                {
                    continue;
                }

                int nx = Mathf.RoundToInt((b.X1 - b.X0) / 2f), nz = Mathf.RoundToInt((b.Z1 - b.Z0) / 2f);
                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        if (i == 0 || j == 0 || i == nx - 1 || j == nz - 1)
                        {
                            pieces.Add(new Piece { Kit = "K05", Area = b.Area, Why = b.Id + " face", P = new Vector2(b.X0 + 1f + 2f * i, b.Z0 + 1f + 2f * j), BaseY = b.Top - 3f, Yaw = 90f * ((i + j) % 4) });
                        }
                    }
                }
            }

            foreach (var run in L3Plan.KitRuns)
            {
                if (run.Stage <= stage)
                {
                    Run(pieces, run.Kit, run.Area, run.Name, run.From, run.To, Vector2.zero, _ => 0f, false, 0f);
                }
            }

            if (stage >= 'A')
            {
                // The orchard gap: thin hedge that looks solid and is not.
                pieces.Add(new Piece { Kit = "K08", Area = "D1", Why = "E1 north opening", P = new Vector2(52f, 120.5f) });
            }

            if (stage >= 'C')
            {
                // The dyke lane's south side is open to the ditch; whitewashed kerb stones mark the edge.
                for (float x = 100f; x <= 178.01f; x += 2f)
                {
                    // Set by hand along a ditch, long ago: none quite square to the next, a few sunk or missing.
                    if (Hash("kerb", (int)x) > 0.1f)
                    {
                        pieces.Add(new Piece
                        {
                            Kit = "K07", Area = "SG", Why = "Dyke kerb", P = new Vector2(x + (Hash("kerb", (int)x + 500) - 0.5f) * 0.5f, 238.5f + (Hash("kerb", (int)x + 900) - 0.5f) * 0.22f),
                            Yaw = (Hash("kerb", (int)x + 1300) - 0.5f) * 30f, BaseY = -Hash("kerb", (int)x + 1700) * 0.1f, Lean = (Hash("kerb", (int)x + 2100) - 0.5f) * 14f,
                        });
                    }
                }
            }

            if (stage >= 'D')
            {
                // The causeway narrows between kerb stones (section 5): a loose line of them at the foot of each
                // wall, no two alike.
                foreach (float side in new[] { 184.4f, 187.6f })
                {
                    int k = 0;
                    for (float z = 365.2f; z < 399f; z += 1.2f + Hash("causeway" + side, k) * 1.3f, k++)
                    {
                        if (Hash("causeway" + side, k + 300) < 0.12f)
                        {
                            continue;
                        }

                        pieces.Add(new Piece
                        {
                            Kit = "K07", Area = "CW", Why = "Causeway kerb", P = new Vector2(side + (Hash("causeway" + side, k + 600) - 0.5f) * 0.16f, z),
                            Yaw = 90f + (Hash("causeway" + side, k + 900) - 0.5f) * 30f, BaseY = -Hash("causeway" + side, k + 1200) * 0.1f, Lean = (Hash("causeway" + side, k + 1500) - 0.5f) * 14f,
                        });
                    }
                }

                // The bank the cellar is cut into (the front stands in it).
                pieces.Add(new Piece { Kit = "K05", Area = "B", Why = "Cellar bank", P = new Vector2(182f, 297f), BaseY = 0f });
                pieces.Add(new Piece { Kit = "K05", Area = "B", Why = "Cellar bank", P = new Vector2(182f, 299f), BaseY = 0f, Yaw = 180f });
            }

            Filter(pieces, stage);
            CornerFills(pieces, stage);
            return pieces;
        }

        private static void Edge(List<Piece> pieces, L3Plan.Floor f, char side, char stage)
        {
            bool alongX = side == 'S' || side == 'N';
            float line = side == 'S' ? f.Z0 : side == 'N' ? f.Z1 : side == 'W' ? f.X0 : f.X1;
            float lo = alongX ? f.X0 : f.Z0, hi = alongX ? f.X1 : f.Z1;
            Vector2 outward = side == 'S' ? Vector2.down : side == 'N' ? Vector2.up : side == 'W' ? Vector2.left : Vector2.right;
            Vector2 At(float t)
            {
                return alongX ? new Vector2(t, line) : new Vector2(line, t);
            }

            float Height(Vector2 p)
            {
                return alongX ? (side == 'S' ? f.HSouth : f.HNorth) : f.HeightAt(p.y);
            }

            var contacts = Contacts(f, side, stage);
            string kit = f.Kit(side);

            // ---- The piece that stands on the edge.
            var kitRuns = new List<Vector2> { new Vector2(lo, hi) };
            if (f.Openings != null)
            {
                foreach (var o in f.Openings)
                {
                    if (o.Side == side)
                    {
                        Cut(kitRuns, o.A, o.B);
                    }
                }
            }
            else
            {
                foreach (var c in contacts)
                {
                    // Open where a floor at the same height touches; where a higher floor touches, its bank
                    // is the boundary.
                    float mine = Height(At((c.A + c.B) * 0.5f));
                    if (c.Height > mine - 0.3f)
                    {
                        Cut(kitRuns, c.A, c.B);
                    }
                }
            }

            foreach (var o in f.NoKit)
            {
                if (o.Side == side)
                {
                    Cut(kitRuns, o.A, o.B);
                }
            }

            // ---- The bank below a raised edge.
            var bankRuns = new List<Vector2>();
            if (f.Raised)
            {
                bankRuns.Add(new Vector2(lo, hi));
                foreach (var c in contacts)
                {
                    float mine = Height(At((c.A + c.B) * 0.5f));
                    if (c.Height > mine - 0.3f)
                    {
                        Cut(bankRuns, c.A, c.B);
                    }
                }
            }

            bool sunken = f.Id == "F01" && !alongX;
            if (sunken)
            {
                // The sunken lane: banks to Y = 5 from y 0 to y 30, in two courses, and no wall on top.
                Cut(kitRuns, lo, L3Plan.SunkenEnd);
                Cut(bankRuns, lo, L3Plan.SunkenEnd);
                float from = OnAnyGround(At(lo) + outward * 0.5f + Vector2.down * 0.5f, stage) ? lo : lo - 2f;
                Run(pieces, "K05", f.Area, f.Id + " " + side + " sunken bank", At(from), At(L3Plan.SunkenEnd), outward, p => L3Plan.SunkenTop - 3f + Hash("sunken" + side, Mathf.RoundToInt(p.y)) * 0.6f);
                Run(pieces, "K05", f.Area, f.Id + " " + side + " sunken bank foot", At(from), At(L3Plan.SunkenEnd), outward, _ => L3Plan.SunkenTop - 6f);
            }

            foreach (var run in bankRuns)
            {
                Vector2 r = Extend(run, 2f);
                Run(pieces, "K05", f.Area, f.Id + " " + side + " bank", At(r.x), At(r.y), outward, p => Height(Clamp(p)) - 3f);
            }

            if (kit != null)
            {
                foreach (var run in kitRuns)
                {
                    Vector2 r = Extend(run, 1f);
                    Run(pieces, kit, f.Area, f.Id + " " + side, At(r.x), At(r.y), outward, p => Height(Clamp(p)), side == 'S', -1f, f.Raised);
                }
            }

            Vector2 Clamp(Vector2 p)
            {
                return new Vector2(Mathf.Clamp(p.x, f.X0, f.X1), Mathf.Clamp(p.y, f.Z0, f.Z1));
            }

            // West and east runs reach past a free corner, so the corner square is not left open.
            Vector2 Extend(Vector2 run, float depth)
            {
                if (alongX)
                {
                    return run;
                }

                if (Mathf.Abs(run.x - lo) < 0.01f && !OnAnyGround(At(lo) + outward * 0.5f + Vector2.down * 0.5f, stage))
                {
                    run.x -= depth;
                }

                if (Mathf.Abs(run.y - hi) < 0.01f && !OnAnyGround(At(hi) + outward * 0.5f + Vector2.up * 0.5f, stage))
                {
                    run.y += depth;
                }

                return run;
            }
        }

        private static void Filter(List<Piece> pieces, char stage)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var piece = pieces[i];
                bool drop = false;
                if (piece.Kit == "K05" && piece.BaseY + 3f < 0.25f)
                {
                    // Nothing to hold up at the foot of a ramp.
                    drop = true;
                }

                foreach (var keep in L3Plan.KeepClear)
                {
                    if (keep.Stage <= stage && keep.Contains(piece.P, -0.05f))
                    {
                        drop = true;
                    }
                }

                if (!piece.Why.EndsWith("face") && piece.Why != "Cellar bank")
                {
                    foreach (var block in L3Plan.Blocks)
                    {
                        if (block.Stage > stage || !block.Contains(piece.P, -0.01f))
                        {
                            continue;
                        }

                        if (piece.Kit == "K05" || block.Top - piece.BaseY >= 1.5f)
                        {
                            drop = true;
                        }
                        else
                        {
                            piece.BaseY = block.Top;
                        }
                    }
                }

                if (drop)
                {
                    pieces.RemoveAt(i);
                }
            }

            // Two runs can ask for the same piece at a corner: keep the taller.
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    // The same piece twice: same place and lying the same way. A wall end that merely comes close
                    // to the end of the wall round the corner is not a duplicate, and dropping it opens the corner.
                    bool parallel = pieces[i].Kit == "K05" || Mathf.Abs(Mathf.DeltaAngle(pieces[i].Yaw * 2f, pieces[j].Yaw * 2f)) < 50f;
                    bool same = pieces[i].Kit == pieces[j].Kit || (Fence(pieces[i].Kit) && Fence(pieces[j].Kit));
                    if (same && parallel && (pieces[i].P - pieces[j].P).magnitude < 0.45f && Mathf.Abs(pieces[i].BaseY - pieces[j].BaseY) < 0.7f)
                    {
                        pieces[j].BaseY = Mathf.Max(pieces[i].BaseY, pieces[j].BaseY);
                        pieces.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        // Unseen colliders that keep the level closed: ground under boundary runs that stand off the floor's
        // edge, and caps over low outer walls. On Ignore Raycast, so neither the mouse aim nor the NavMesh bake
        // sees them.
        private static Transform Containment(Transform root, string area)
        {
            var parent = Group(root, "Floors", area);
            var group = parent.Find("_Containment");
            return group != null ? group : L3Build.Child(parent, "_Containment").transform;
        }

        // A hedged field does not have square corners: a piece set across each one rounds it off.
        private static void CornerFills(List<Piece> pieces, char stage)
        {
            foreach (var f in L3Plan.Floors)
            {
                if (f.Virtual || f.Diagonal || f.Raised || f.Stage > stage)
                {
                    continue;
                }

                foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    string kitX = sx < 0 ? f.West : f.East, kitZ = sz < 0 ? f.South : f.North;
                    bool hedgeX = kitX == "K01" || kitX == "K02", hedgeZ = kitZ == "K01" || kitZ == "K02";
                    if (!hedgeX || !hedgeZ)
                    {
                        continue;
                    }

                    var corner = new Vector2(sx < 0 ? f.X0 : f.X1, sz < 0 ? f.Z0 : f.Z1);
                    // Only where both hedges really run into the corner (no gate or neighbouring floor there).
                    bool alongX = false, alongZ = false;
                    string runZ = f.Id + " " + (sz < 0 ? "S" : "N"), runX = f.Id + " " + (sx < 0 ? "W" : "E");
                    foreach (var piece in pieces)
                    {
                        alongZ |= piece.Why == runZ && (piece.P - (corner + new Vector2(-sx * 1.2f, sz * 0.5f))).magnitude < 1.6f;
                        alongX |= piece.Why == runX && (piece.P - (corner + new Vector2(sx * 0.5f, -sz * 1.2f))).magnitude < 1.6f;
                    }

                    if (!alongX || !alongZ)
                    {
                        continue;
                    }

                    string kit = sz < 0 ? "K02" : "K01";
                    string key = f.Id + " corner " + sx + sz;
                    pieces.Add(new Piece
                    {
                        Kit = kit, Area = f.Area, Why = key, P = corner + new Vector2(-sx * 0.95f, -sz * 0.95f), Yaw = (sx * sz > 0 ? -45f : 45f) + (Hash(key) - 0.5f) * 16f,
                        BaseY = -Hash(key, 1) * (kit == "K01" ? 0.4f : 0.08f), South = sz < 0,
                    });
                }
            }
        }

        private static List<Piece> lastPieces;

        private static string Boundaries(Transform root, char stage)
        {
            var pieces = BoundaryPieces(stage);
            lastPieces = pieces;
            var counts = new SortedDictionary<string, int>();
            foreach (var piece in pieces)
            {
                var prefab = L3Assets.Load(piece.Kit);
                var parent = Group(root, "Boundaries", piece.Area);
                var go = L3Build.Instance(prefab, parent, new Vector3(piece.P.x, piece.BaseY, piece.P.y), piece.Yaw);
                go.transform.localRotation = Quaternion.Euler(0f, piece.Yaw, 0f) * Quaternion.Euler(piece.Lean, 0f, 0f);
                go.name = prefab.name + " (" + piece.Why + ")";
                if (piece.Capped)
                {
                    // The player's jump (0.9 m, plus the controller's step) reaches the top of a 1 m wall. An
                    // unseen box over each low outer piece keeps the level closed without making walls taller.
                    var cap = L3Build.Child(Containment(root, piece.Area), "Cap", new Vector3(piece.P.x, piece.BaseY + 0.8f, piece.P.y), new Vector3(0f, piece.Yaw, 0f));
                    cap.layer = L3Build.Layer(L3Build.Marker);
                    var box = cap.AddComponent<BoxCollider>();
                    // A fence is a hand's breadth deep: its cap is given some body.
                    box.size = new Vector3(2.05f, 3f, Mathf.Max(Depth(piece.Kit), 0.5f));
                    box.center = new Vector3(0f, 1.5f, 0f);
                    // And a lip above head height, reaching 0.35 m out over the floor: a jump beside the wall is
                    // cut short, so the player cannot come to rest perched on the wall's edge under the cap.
                    // Walking is untouched (the player is 1.95 m tall). On ramps the floor is not level with the
                    // piece's base, so the lip sits higher there.
                    float lipFrom = piece.Raised ? 2f : 1.65f;
                    var lip = cap.AddComponent<BoxCollider>();
                    lip.size = new Vector3(2.05f, 3f - lipFrom, Depth(piece.Kit) + 0.7f);
                    lip.center = new Vector3(0f, (3f + lipFrom) * 0.5f, 0f);
                }
                if (go.layer == L3Build.Layer(L3Build.Boundary))
                {
                    NotWalkable(go);
                }

                counts.TryGetValue(piece.Kit, out int n);
                counts[piece.Kit] = n + 1;
            }

            foreach (var pad in Pads)
            {
                var go = L3Build.Child(Containment(root, pad.Area), "Pad", new Vector3(pad.Centre.x, 0f, pad.Centre.y), new Vector3(0f, pad.Yaw, 0f));
                go.layer = L3Build.Layer(L3Build.Marker);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(pad.Size.x, Thickness, pad.Size.y);
                box.center = new Vector3(0f, -Thickness * 0.5f, 0f);
            }

            var parts = new List<string>();
            foreach (var pair in counts)
            {
                parts.Add(pair.Key + " x" + pair.Value);
            }

            return "boundaries " + string.Join(", ", parts) + ", " + Pads.Count + " pads";
        }

        // ---------------------------------------------------------------- Props, hazards and structures (15.4)

        // Direct changes to a prefab instance are only saved with the scene once they are recorded.
        private static void Record(GameObject instance)
        {
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c != null && !(c is Transform))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                    }
                }
            }
        }

        private static string Places(Transform root, char stage)
        {
            var made = new Dictionary<string, GameObject>();
            int count = 0;
            foreach (var place in L3Plan.Places)
            {
                if (place.Stage > stage)
                {
                    continue;
                }

                var prefab = L3Assets.Load(place.Id);
                var go = L3Build.Instance(prefab, Group(root, place.Group, place.Area), place.Position, place.Yaw);
                go.name = place.Name;
                made[place.Name] = go;
                if (go.layer == L3Build.Layer(L3Build.Cover))
                {
                    NotWalkable(go);
                }

                count++;
            }

            // ---- Stage A: the demonstration row lights when the player crosses the crest's north end.
            var ignite = L3Build.Instance(AssetDatabase.LoadAssetAtPath<GameObject>(IgniteTriggerPath), Group(root, "Hazards", "E1a"), new Vector3(64f, L3Plan.Tier, 45f));
            ignite.name = "IgniteTrigger_Descent";
            L3Build.Set(ignite.GetComponent<RowIgniteTrigger>(), "row", made["CropRow_Descent"].GetComponent<CropRow>());

            if (stage >= 'B')
            {
                L3Build.Set(made["BrandStake_West"].GetComponent<BrandStake>(), "row", made["CropRow_West"].GetComponent<CropRow>());
                L3Build.Set(made["BrandStake_East"].GetComponent<BrandStake>(), "row", made["CropRow_East"].GetComponent<CropRow>());
                // The worked example: a stake that fell before the player came, and its row already ash.
                made["CropRow_Ash"].GetComponent<CropRow>().SetStartInAsh(true);
                Record(made["CropRow_Ash"]);
                var fallen = made["BrandStake_Fallen"].GetComponent<BrandStake>();
                L3Build.Set(fallen, "row", made["CropRow_Ash"].GetComponent<CropRow>());
                fallen.SetStartFallen(true);
                Record(made["BrandStake_Fallen"]);
            }

            if (stage >= 'C')
            {
                made["Mud_Wallow"].GetComponent<SurfaceVolume>().Configure(new Vector2(20f, 14f), true, true, false, false);
                PaintedGround(made["Mud_Wallow"]);
                L3Mud.Build(Group(root, "Floors", "G"), L3Ground.Mud[0]);
            }

            if (stage >= 'D')
            {
                made["Mud_Grave"].GetComponent<SurfaceVolume>().Configure(new Vector2(20f, 16f), false, false, false, false);
                PaintedGround(made["Mud_Grave"]);
                // Clear of the trench, the lime mound and the grave cart.
                L3Mud.Build(Group(root, "Floors", "D3"), L3Ground.Mud[1], 138f, 154f, 304f, 307f, 138f, 142f, 293f, 297f, 138f, 140f, 298.5f, 301.5f);

                // The mill stair makes the top of the mound reachable, and the mound is not a floor: nothing
                // closes its far sides. Unseen walls along its south edge either side of the landing, and caps
                // over the landing's cheek walls, keep the player on the stair.
                var stair = made["MillStair"].transform.position;
                var mound = L3Plan.Blocks.Find(b => b.Id == "MillMound");
                float half = (L3Assets.StairWidth + L3Assets.StairCheek) * 0.5f, landingZ = stair.z + L3Assets.StairSteps * L3Assets.StairGoing;
                void Closed(string name, Vector3 baseCentre, Vector3 size)
                {
                    var go = L3Build.Child(Containment(root, "E5"), name, baseCentre);
                    go.layer = L3Build.Layer(L3Build.Marker);
                    var box = go.AddComponent<BoxCollider>();
                    box.size = size;
                    box.center = new Vector3(0f, size.y * 0.5f, 0f);
                }

                float westEnd = stair.x - half + L3Assets.StairCheek * 0.5f, eastEnd = stair.x + half - L3Assets.StairCheek * 0.5f;
                Closed("Mound_South_West", new Vector3((mound.X0 + westEnd) * 0.5f, mound.Top, landingZ + 0.3f), new Vector3(westEnd - mound.X0, 4.5f, 0.6f));
                Closed("Mound_South_East", new Vector3((eastEnd + mound.X1) * 0.5f, mound.Top, landingZ + 0.3f), new Vector3(mound.X1 - eastEnd, 4.5f, 0.6f));
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = new Vector3(stair.x + side * half, mound.Top, landingZ + L3Assets.StairLanding * 0.5f);
                    Closed("Landing_Cap", at + Vector3.up * 0.8f, new Vector3(L3Assets.StairCheek, 3.7f, L3Assets.StairLanding + 0.4f));
                    Closed("Landing_Lip", at + Vector3.up * 2.45f, new Vector3(L3Assets.StairCheek + 0.7f, 2.05f, L3Assets.StairLanding + 0.4f));
                }

                // The open trench (15.4): a dark, solid block 0.3 m high that nothing walks on.
                var trench = L3Build.Block(Group(root, "Props", "D3"), "Trench", new Vector3(16f, 0.3f, 3f), new Vector3(146f, 0f, 305.5f), L3Build.Mat("L3_Trench"));
                trench.layer = L3Build.Layer(L3Build.Boundary);
                trench.AddComponent<BoxCollider>();
                NotWalkable(trench);
            }

            return count + " props, hazards and structures";
        }

        // In the level the mud is part of the painted ground (L3Ground) with clods and water laid on it
        // (L3Mud), so the volume's own rectangular plane and damp bands are not drawn. Its trigger, its NavMesh
        // area and its size are untouched.
        private static void PaintedGround(GameObject mud)
        {
            foreach (var r in mud.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = false;
            }

            Record(mud);
        }

        // ---------------------------------------------------------------- Stage E: spawns, triggers, tells, pickups

        private static float GroundY(float x, float z)
        {
            int mask = 1 << L3Build.Layer(L3Build.Walkable);
            return Physics.Raycast(new Vector3(x, 20f, z), Vector3.down, out var hit, 40f, mask, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
        }

        private static string SpawnMarkers(Transform root)
        {
            Physics.SyncTransforms();
            int hounds = 0, brutes = 0, footmen = 0;
            var index = new Dictionary<string, int>();
            foreach (var s in L3Plan.Spawns)
            {
                index.TryGetValue(s.Encounter, out int n);
                index[s.Encounter] = n + 1;
                var marker = L3Build.Instance(L3Assets.Load("M12"), Group(root, "Spawns", s.Encounter), new Vector3(s.Position.x, GroundY(s.Position.x, s.Position.y), s.Position.y), 180f);
                marker.name = "Spawn_" + s.Encounter + "_" + s.Type + "_" + (n + 1);
                var data = marker.GetComponent<SpawnMarker>();
                L3Build.Set(data, "enemyType", s.Type);
                L3Build.Set(data, "encounterId", s.Encounter);
                L3Build.Set(data, "initialState", s.State);
                L3Build.Set(data, "aggroRadius", s.Aggro);
                L3Build.Set(data, "leashNote", s.Leash);

                string id = s.Type == EnemyType.Footman ? "E01" : s.Type == EnemyType.Hound ? "E02" : "E03";
                var enemy = L3Build.Instance(L3Assets.Load(id), marker.transform, Vector3.zero);
                if (s.Type == EnemyType.Footman)
                {
                    // Shown for scale, not run: there is no spawner or encounter logic yet, and a live footman
                    // would hunt the player from the first frame.
                    foreach (var behaviour in enemy.GetComponents<Behaviour>())
                    {
                        if (!(behaviour is Animator))
                        {
                            behaviour.enabled = false;
                        }
                    }

                    Record(enemy);
                    footmen++;
                }
                else if (s.Type == EnemyType.Hound)
                {
                    hounds++;
                }
                else
                {
                    brutes++;
                }
            }

            return "spawn markers: " + hounds + " hounds, " + brutes + " brutes, " + footmen + " footmen";
        }

        private static GameObject Volume(Transform parent, string id, string name, float x0, float x1, float z0, float z1, float y, float height)
        {
            var go = L3Build.Instance(L3Assets.Load(id), parent, new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f));
            go.name = name;
            L3Assets.SizeVolume(go, new Vector3(x1 - x0, height, z1 - z0));
            Record(go);
            return go;
        }

        private static string Triggers(Transform root)
        {
            Physics.SyncTransforms();
            var spawn = L3Build.Instance(L3Assets.Load("M10a"), Group(root, "Markers", "S"), new Vector3(64f, GroundY(64f, 2f), 2f));
            spawn.name = "PlayerSpawn";
            Volume(Group(root, "Markers", "CW"), "M10b", "ExitTrigger", 184f, 188f, 396f, 400f, 0f, 3f);

            void Vista(string area, string id, float x0, float x1, float z0, float z1, float y, float pitch, float fov, float pullBack, float shift, string note)
            {
                var v = Volume(Group(root, "Markers", area), "M08", "Vista_" + id, x0, x1, z0, z1, y, 4f).GetComponent<VistaMarker>();
                L3Build.Set(v, "id", id);
                L3Build.Set(v, "pitch", pitch);
                L3Build.Set(v, "fieldOfView", fov);
                L3Build.Set(v, "pullBack", pullBack);
                L3Build.Set(v, "sidewaysShift", shift);
                L3Build.Set(v, "note", note);
            }

            Vista("A0", "V1", 60f, 68f, 37f, 43f, L3Plan.Tier, 14f, 40f, 12f, 0f, "Stronghold in the upper third, right of centre; windmill below it, oak light below that. Burning strips as converging lines.");
            Vista("A", "V2", 132f, 138f, 211f, 217f, L3Plan.Tier, 22f, 0f, 0f, -10f, "Fire field below-left, barred sluice gate at the knoll's foot, windmill in the upper third. Shift is toward -X.");
            Vista("BR", "V3", 103f, 109f, 232f, 238f, 0f, 16f, 0f, 0f, 0f, "Windmill in the upper third, right of centre; lime mound beneath it; the dyke lane as a leading line.");
            Vista("CW", "V4", 182f, 190f, 360f, 368f, 0f, 12f, 0f, 0f, 0f, "Stronghold centred, filling the upper third. Kerb stones converge on its gate.");

            // VG has no volume of its own: it is the pan the sluice gate asks for when it opens.
            var vg = Volume(Group(root, "Markers", "SG"), "M08", "Vista_VG", 138f, 142f, 239.5f, 240.5f, 0f, 3f).GetComponent<VistaMarker>();
            L3Build.Set(vg, "id", "VG");
            L3Build.Set(vg, "panTarget", new Vector3(140f, L3Plan.Tier, 212f));
            L3Build.Set(vg, "panDuration", 2f);
            L3Build.Set(vg, "note", "Not entered: fired by SluiceGate.Opened. A 2 s pan south to the footway, the knoll and Shrine A's light, then back.");

            foreach (var (arena, floor) in new[] { ("E1", "F07"), ("E3", "F17"), ("E4", "F23"), ("E5", "F30"), ("D3", "F28") })
            {
                var f = L3Plan.Find(floor);
                var volume = Volume(Group(root, "Markers", arena), "M09", "ArenaCam_" + arena, f.X0, f.X1, f.Z0, f.Z1, 0f, 6f);
                L3Build.Set(volume.GetComponent<ArenaMarker>(), "arenaId", arena);
            }

            void Tell(string secret, float x, float z, float yaw)
            {
                var tell = L3Build.Instance(L3Assets.Load("M03"), Group(root, "Markers", secret), new Vector3(x, 0f, z), yaw);
                tell.name = "SecretTell_" + secret;
                L3Build.Set(tell.GetComponent<SecretTell>(), "secretId", secret);
                // Each of the three stands on its own post, at the spot the note gives.
                tell.transform.Find("Post").gameObject.SetActive(true);
                Record(tell);
            }

            Tell("D1", 53.5f, 119.5f, 180f);
            Tell("D2", 189.5f, 269.5f, 270f);
            Tell("D3", 167.5f, 302.5f, 180f);

            void Pickup(string id, string secret, string name, float x, float z)
            {
                var pickup = L3Build.Instance(L3Assets.Load(id), Group(root, "Markers", secret), new Vector3(x, GroundY(x, z), z));
                pickup.name = "Pickup_" + secret + "_" + name;
                L3Build.Set(pickup.GetComponent<PickupMarker>(), "id", "L3_" + secret + "_" + name);
            }

            Pickup("M04", "D1", "Consumable", 51f, 138f);
            Pickup("M07", "D1", "Apple", 53f, 138f);
            Pickup("M04", "D2", "Consumable", 195.5f, 273f);
            Pickup("M05", "D2", "Writ", 194f, 275f);
            Pickup("M06", "D3", "HealCapacity", 141f, 300f);
            Pickup("M05", "D3", "Grave", 146f, 303f);
            return "triggers: spawn, exit, 5 vistas, 5 arena volumes, 3 tells, 6 pickups";
        }

        // ---------------------------------------------------------------- Player, camera, NavMesh

        // The same set-up as L1 and L2: the project's player prefab and its PlayerCamera, untouched, with the
        // shared camera settings asset.
        private static void PlayerAndCamera()
        {
            Physics.SyncTransforms();
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"));
            player.name = "Player";
            player.transform.SetPositionAndRotation(new Vector3(64f, GroundY(64f, 2f) + 0.05f, 2f), Quaternion.identity);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 400f;
            camera.AddComponent<AudioListener>();
            var playerCamera = camera.AddComponent<PlayerCamera>();
            L3Build.Set(playerCamera, "settings", AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/Player/Settings/PlayerCameraSettings.asset"));
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 8.7f, -3.6f), Quaternion.Euler(65f, 0f, 0f));
            camera.AddComponent<Kneel.OcclusionFader>();
        }

        // Thrall, Brute and Hound agent types (15.7), added to the project's NavMesh settings if missing.
        public static int AgentType(string name, float radius, float height)
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == name)
                {
                    return id;
                }
            }

            int created = NavMesh.CreateSettings().agentTypeID;
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var settings = so.FindProperty("m_Settings");
            var names = so.FindProperty("m_SettingNames");
            for (int i = 0; i < settings.arraySize; i++)
            {
                var entry = settings.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("agentTypeID").intValue != created)
                {
                    continue;
                }

                entry.FindPropertyRelative("agentRadius").floatValue = radius;
                entry.FindPropertyRelative("agentHeight").floatValue = height;
                entry.FindPropertyRelative("agentClimb").floatValue = 0.4f;
                entry.FindPropertyRelative("agentSlope").floatValue = 45f;
                if (names.arraySize <= i)
                {
                    names.arraySize = i + 1;
                }

                names.GetArrayElementAtIndex(i).stringValue = name;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return created;
        }

        public static string BakeNavMesh(GameObject root)
        {
            foreach (var old in root.GetComponents<NavMeshSurface>())
            {
                Object.DestroyImmediate(old);
            }

            var parts = new List<string>();
            foreach (var (name, radius, height) in Agents)
            {
                var surface = root.AddComponent<NavMeshSurface>();
                surface.agentTypeID = AgentType(name, radius, height);
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = (1 << L3Build.Layer(L3Build.Walkable)) | (1 << L3Build.Layer(L3Build.Boundary));
                var so = new SerializedObject(surface);
                var links = so.FindProperty("m_GenerateLinks");
                if (links != null)
                {
                    links.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                surface.BuildNavMesh();
                string path = SceneFolder + "/NavMesh-" + name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
                parts.Add(name);
            }

            return "NavMesh baked for " + string.Join(", ", parts);
        }
    }
}
