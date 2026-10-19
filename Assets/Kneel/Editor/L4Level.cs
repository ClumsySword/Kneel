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
    // Builds the L4 level scene from L4Plan (the report's blockout spec, 15.1-15.5), every stage up to the one
    // asked for (15.5: A metrics, B floors, C walls and structures, D E1-E3 and Shrine A, E E4 and the shortcut,
    // F E5, E6 and the threshold, G secrets and vignettes). The scene is generated from nothing each time: Build
    // replaces it. Floors, their edges and the ravine are scene geometry; everything else is a section 14 prefab.
    public static class L4Level
    {
        public const string RootName = "L4_Root";
        public const string ThresholdName = "L4L5_Threshold";
        public const string ScenePath = L4Build.ScenesPath + "/L4_BurningGate.unity";
        public const string SceneFolder = L4Build.ScenesPath + "/L4_BurningGate";
        private const string MeshFolder = L4Build.MeshesPath + "/Level";
        private const string MaterialFolder = L4Build.MaterialsPath + "/Level";
        private const float Thickness = 0.5f;

        public static readonly (string name, float radius, float height)[] Agents =
        {
            ("Thrall", 0.5f, 2f), ("Husk", 0.6f, 2.2f), ("Brute", 1f, 2.6f), ("Hound", 0.4f, 0.9f),
        };

        [MenuItem("Kneel/L4/Build/Level (replaces the scene)")]
        public static void BuildMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild L4_BurningGate", "This replaces the level scene with a freshly generated one. Hand edits to the scene are lost.", "Rebuild", "Cancel"))
            {
                Debug.Log(Build('G'));
            }
        }

        public static GameObject Root => GameObject.Find(RootName);

        private static readonly Dictionary<string, GameObject> placed = new Dictionary<string, GameObject>();

        // ---------------------------------------------------------------- Build

        public static string Build(char stage)
        {
            if (EditorApplication.isPlaying)
            {
                return "Stop play mode first.";
            }

            L2Build.EnsureFolder(L4Build.ScenesPath);
            L2Build.EnsureFolder(SceneFolder);
            L2Build.EnsureFolder(MeshFolder);
            L2Build.EnsureFolder(MaterialFolder);
            placed.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            var threshold = L3Build.Child(root, ThresholdName).transform;
            foreach (var name in new[] { "Floors", "Walls", "Stairs", "Structures", "Props", "Hazards", "Markers", "Spawns", "Ravine", "NavMesh" })
            {
                L3Build.Child(root, name);
                L3Build.Child(threshold, name);
            }

            var log = new List<string>();
            log.Add(Metrics(root, stage));
            if (stage >= 'B')
            {
                log.Add(Floors(root));
                log.Add(Stairs(root));
                Ravine(root);
            }

            if (stage >= 'C')
            {
                log.Add(Edges(root));
            }

            log.Add(Places(root, stage));
            log.Add(Links());
            log.Add(Enemies(root, stage));
            log.Add(Volumes(root, stage));
            RoofHiders(root);
            log.Add(NavRules(root, stage));
            if (stage >= 'B')
            {
                log.Add(L4Surroundings.Build(root));
                log.Add(L4Lava.Build(root));
                log.Add(L4Farmland.Build(root));
            }

            if (stage >= 'G')
            {
                log.Add(L4Citadel.Build(root));
                log.Add(L4Atmosphere.Build(root));
                log.Add(L4Dressing.Build(root));
                log.Add(L4Stronghold.Build(root));
            }

            var reset = L3Build.Child(root, "LevelReset").AddComponent<LevelReset>();
            reset.name = "LevelReset";

            PlayerAndCamera();
            Kneel.Lighting.EditorTools.L4LightingPass.Apply(root);
            Physics.SyncTransforms();
            if (stage >= 'B')
            {
                log.Add(BakeNavMesh(root.gameObject, stage));
                log.Add(Check(stage));
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            return "L4_BurningGate built through stage " + stage + ": " + string.Join("; ", log);
        }

        private static Transform Group(Transform root, string group, string area, bool threshold = false)
        {
            var top = threshold ? root.Find(ThresholdName) : root;
            var parent = top.Find(group);
            var child = parent.Find(area);
            return child != null ? child : L3Build.Child(parent, area).transform;
        }

        private static GameObject Prefab(string id)
        {
            var item = L4Assets.Find(id);
            if (item == null)
            {
                throw new System.ArgumentException("No L4 manifest row " + id);
            }

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(item.Path);
            if (go == null)
            {
                throw new System.ArgumentException("Missing prefab for " + id + " at " + item.Path);
            }

            return go;
        }

        // For the refinement builders: one manifest prefab, placed.
        public static GameObject PutPrefab(string id, Transform parent, Vector3 position, float yaw) => Put(id, parent, position, yaw);

        private static GameObject Put(string id, Transform parent, Vector3 position, float yaw, Vector3? scale = null)
        {
            var go = L3Build.Instance(Prefab(id), parent, position, yaw);
            if (scale.HasValue)
            {
                go.transform.localScale = scale.Value;
            }

            return go;
        }

        // ---------------------------------------------------------------- Stage A: the metrics map

        // Section 12's measures outside the level (x -90): a 2 m grid, a breach edge with its buffer, a pour, the
        // charge lane (6 R between two curtain faces) and a stair, each on its own flagged pad.
        private static string Metrics(Transform root, char stage)
        {
            var g = L3Build.Child(root, "Metrics", new Vector3(-90f, 0f, 0f)).transform;
            var pad = L3Build.MeshChild(g, "Pad", L4LookDev.FlatQuad(), new Vector3(0f, 0f, 20f), L4Build.Mat("L4_Ground"));
            pad.transform.localScale = new Vector3(40f, 1f, 60f);
            var col = pad.AddComponent<BoxCollider>();
            col.size = new Vector3(1f, 0.02f, 1f);
            col.center = new Vector3(0f, -0.01f, 0f);
            L3Build.SetLayer(pad, L3Build.Walkable);
            for (int i = 0; i < 6; i++)
            {
                Put("W06", g, new Vector3(16f, 0f, 2f + i * 2f), 90f);
            }

            Put("H01", g, new Vector3(-8f, 0f, 6f), 0f);
            Put("W10", g, new Vector3(-8f, 5f, 9f), 0f);
            foreach (float x in new[] { -7f, 7f })
            {
                Put("W01a", g, new Vector3(x, 0f, 24f), x < 0f ? -90f : 90f);
                Put("W01a", g, new Vector3(x, 0f, 30f), x < 0f ? -90f : 90f);
            }

            Put("W09a", g, new Vector3(0f, 0f, 44f), 0f);
            return "metrics map at x -90";
        }

        // ---------------------------------------------------------------- Stage B: floors, stairs, the ravine

        private static string Floors(Transform root)
        {
            int count = 0;
            foreach (var f in L4Plan.Floors)
            {
                var parent = Group(root, "Floors", f.Area, f.Threshold);
                var centre = new Vector3((f.X0 + f.X1) * 0.5f, f.Y, (f.Z0 + f.Z1) * 0.5f);
                var go = L3Build.Child(parent, f.Id + " " + f.Label, centre);
                go.layer = L3Build.Layer(L3Build.Walkable);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                if (!f.Hidden)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = Slab(f);
                    go.AddComponent<MeshRenderer>().sharedMaterial = FloorMaterial(f);
                }

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(f.X1 - f.X0, Thickness, f.Z1 - f.Z0);
                box.center = new Vector3(0f, -Thickness * 0.5f, 0f);
                count++;
            }

            return count + " floors";
        }

        // A slab under the floor's rectangle, its top at the floor's height, UV0 spanning the rectangle (the ground
        // shader's layout and mask); detail is world-space. East and west faces are left out: the ground shader
        // builds its tangent frame from world +X and returns NaN on a face pointing due east or west, and walls
        // stand against every floor edge.
        private static Mesh Slab(L4Plan.Floor f)
        {
            float hx = (f.X1 - f.X0) * 0.5f, hz = (f.Z1 - f.Z0) * 0.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    uvs.Add(new Vector2((v.x + hx) / (2f * hx), (v.z + hz) / (2f * hz)));
                }

                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            float y0 = -Thickness;
            Quad(new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz));
            Quad(new Vector3(-hx, y0, -hz), new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, y0, -hz));
            Quad(new Vector3(hx, y0, hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz), new Vector3(-hx, y0, hz));
            Quad(new Vector3(-hx, y0, -hz), new Vector3(hx, y0, -hz), new Vector3(hx, y0, hz), new Vector3(-hx, y0, hz));
            var mesh = new Mesh { name = "L4_Floor_" + f.Id };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        internal static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            string name = mesh.name;
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // The floor's own copy of L4_Ground: earth at the wall feet and in worn patches, pitch at its murder holes.
        private static Material FloorMaterial(L4Plan.Floor f)
        {
            string path = MaterialFolder + "/L4_Ground_" + f.Id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(L4Build.Mat("L4_Ground"));
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.CopyPropertiesFromMaterial(L4Build.Mat("L4_Ground"));
            }

            var min = new Vector2(f.X0, f.Z0);
            var size = new Vector2(f.X1 - f.X0, f.Z1 - f.Z0);
            var feet = new[]
            {
                new Rect(f.X0, f.Z0, size.x, 0.8f), new Rect(f.X0, f.Z1 - 0.8f, size.x, 0.8f),
                new Rect(f.X0, f.Z0, 0.8f, size.y), new Rect(f.X1 - 0.8f, f.Z0, 0.8f, size.y),
            };
            var scuffs = new List<Vector2>();
            foreach (var s in f.Splashes)
            {
                scuffs.Add(s.Centre);
            }

            int seed = Mathf.Abs(f.Id.GetHashCode()) % 997;
            var mask = L4Paving.Mask("L4_Ground_" + f.Id + "_Mask", min, size, feet, scuffs.ToArray(), seed);
            mat.SetTexture("_PaveMask", mask);
            mat.SetTexture("_BaseMap", L4Paving.LayoutFromMask("L4_Ground_" + f.Id + "_Layout", mask, min, size, f.Splashes.ToArray()));
            if (f.Splashes.Count > 0)
            {
                mat.SetFloat("_PuddleAmount", 1f);
                mat.SetFloat("_PuddleDepth", 1.5f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string Stairs(Transform root)
        {
            foreach (var s in L4Plan.Stairs)
            {
                var area = L4Plan.At(new Vector2(s.Centre.x, s.Centre.z));
                Put("W09a", Group(root, "Stairs", area != null ? area.Area : "Misc"), s.Centre, s.Yaw).name = s.Name;
            }

            return L4Plan.Stairs.Count + " stairs";
        }

        // The dry ravine round the castle: its bed 12 m down, dark, and one kill volume over all of it.
        private static void Ravine(Transform root)
        {
            // The ground itself is L4Surroundings (no colliders), so a fall passes through the rock: the kill volume
            // tops out at -4.5, under the crypt floor (-3.45) and soon after a fall begins. The breach, the one
            // fall the level invites, has its own box from just under the terrace.
            var g = root.Find("Ravine");
            var kills = new[]
            {
                ("RavineKill", new Vector3(60f, -24f, 150f), new Vector3(340f, 19.5f, 520f)),
                ("BreachKill", new Vector3(101f, -12f, 148f), new Vector3(16f, 10.5f, 16f)),
            };
            foreach (var (name, at, size) in kills)
            {
                var kill = Put("H09", g, at, 0f);
                kill.name = name;
                foreach (var box in kill.GetComponentsInChildren<BoxCollider>())
                {
                    box.size = size;
                    box.center = new Vector3(0f, size.y * 0.5f, 0f);
                }
            }
        }

        // ---------------------------------------------------------------- Stage C: edges

        private enum Treat
        {
            None, Wall, Cut, Low, Face, FaceParapet, FaceMerlon, Merlon, FaceHoarding, FaceBreach,
        }

        // Each floor side is sampled every 0.5 m. A sample looks just past the edge: a floor at the same height
        // means open, a lower one means this side is raised over it (a face below, a parapet on top), nothing
        // means the castle's boundary. The floor's listed spans override. Equal samples join into runs.
        private static string Edges(Transform root)
        {
            int pieces = 0;
            foreach (var f in L4Plan.Floors)
            {
                foreach (char side in new[] { 'S', 'W', 'E', 'N' })
                {
                    bool alongX = side == 'S' || side == 'N';
                    float a = alongX ? f.X0 : f.Z0, b = alongX ? f.X1 : f.Z1;
                    int samples = Mathf.Max(1, Mathf.RoundToInt((b - a) / 0.5f));
                    float step = (b - a) / samples;
                    Treat run = Treat.None;
                    bool runBoundary = false;
                    float runStart = a;
                    for (int i = 0; i <= samples; i++)
                    {
                        Treat t = Treat.None;
                        bool boundary = false;
                        if (i < samples)
                        {
                            float s = a + (i + 0.5f) * step;
                            t = Classify(f, side, s, out boundary);
                        }

                        if (i == samples || t != run || boundary != runBoundary)
                        {
                            float runEnd = a + i * step;
                            if (run != Treat.None && runEnd - runStart > 0.05f)
                            {
                                pieces += Run(root, f, side, run, runBoundary, runStart, runEnd);
                            }

                            run = t;
                            runBoundary = boundary;
                            runStart = runEnd;
                        }
                    }
                }
            }

            return pieces + " edge pieces";
        }

        private static Vector2 Outward(char side)
        {
            return side == 'S' ? Vector2.down : side == 'N' ? Vector2.up : side == 'W' ? Vector2.left : Vector2.right;
        }

        private static Treat Classify(L4Plan.Floor f, char side, float s, out bool boundary)
        {
            boundary = false;
            foreach (var span in f.Spans)
            {
                if (span.Side == side && s > span.A && s < span.B)
                {
                    switch (span.Kind)
                    {
                        case L4Plan.Edit.Skip:
                            return Treat.None;
                        case L4Plan.Edit.NoTop:
                            return f.Raised ? Treat.Face : Treat.None;
                        case L4Plan.Edit.Close:
                            return Treat.Merlon;
                        case L4Plan.Edit.Hoarding:
                            return Treat.FaceHoarding;
                        case L4Plan.Edit.Breach:
                            boundary = true;
                            return Treat.FaceBreach;
                        case L4Plan.Edit.Cut:
                            boundary = true;
                            return Treat.Cut;
                    }
                }
            }

            bool alongX = side == 'S' || side == 'N';
            float edge = side == 'S' ? f.Z0 : side == 'N' ? f.Z1 : side == 'W' ? f.X0 : f.X1;
            var o = Outward(side);
            var probe = alongX ? new Vector2(s, edge) + o * 0.25f : new Vector2(edge, s) + o * 0.25f;
            var other = L4Plan.At(probe, f);
            if (other != null)
            {
                if (Mathf.Abs(other.Y - f.Y) < 0.1f || other.Y > f.Y)
                {
                    return Treat.None;
                }

                return (f.Tower || f.Mass) && !f.Outpost ? Treat.Face : Treat.FaceParapet;
            }

            boundary = true;
            if (f.Raised)
            {
                return (f.Tower || f.Mass) && !f.Outpost ? Treat.Face : Treat.FaceMerlon;
            }

            if (f.Ravine)
            {
                return Treat.Low;
            }

            return side == 'S' && f.CutSouth ? Treat.Cut : Treat.Wall;
        }

        // Builds one run of edge kit from a to b along the side.
        private static int Run(Transform root, L4Plan.Floor f, char side, Treat t, bool boundary, float a, float b)
        {
            var parent = Group(root, "Walls", f.Area, f.Threshold);
            bool alongX = side == 'S' || side == 'N';
            float edge = side == 'S' ? f.Z0 : side == 'N' ? f.Z1 : side == 'W' ? f.X0 : f.X1;
            var o = Outward(side);
            // Yaw that turns a kit piece's -Z (its yard face) to face 'dir'.
            float FaceYaw(Vector2 dir) => dir == Vector2.up ? 180f : dir == Vector2.down ? 0f : dir == Vector2.left ? 90f : -90f;
            Vector3 At(float s, float offset, float y) => alongX ? new Vector3(s, y, edge + o.y * offset) : new Vector3(edge + o.x * offset, y, s);
            int n = 0;
            switch (t)
            {
                case Treat.Wall:
                    n += Curtain(parent, a, b, s => At(s, 1f, 0f), FaceYaw(-o), alongX);
                    break;
                case Treat.Cut:
                    n += Modules(parent, "W04", a, b, s => At(s, 1f, 0f), FaceYaw(-o));
                    break;
                case Treat.Low:
                    n += Modules(parent, "W03a", a, b, s => At(s, -0.25f, f.Y), FaceYaw(-o));
                    break;
                case Treat.Face:
                case Treat.FaceParapet:
                case Treat.FaceMerlon:
                case Treat.FaceHoarding:
                case Treat.FaceBreach:
                    n += Curtain(parent, a, b, s => At(s, -1f, -0.05f), FaceYaw(o), alongX);
                    if (t == Treat.FaceParapet)
                    {
                        n += Modules(parent, "W03a", a, b, s => At(s, -0.25f, f.Y), FaceYaw(o), "W03b");
                    }
                    else if (t == Treat.FaceMerlon)
                    {
                        n += Merlons(parent, a, b, s => At(s, -0.3f, f.Y), FaceYaw(o));
                    }
                    else if (t == Treat.FaceHoarding)
                    {
                        n += Modules(parent, "W11", a, b, s => At(s, 0f, f.Y), FaceYaw(o));
                    }
                    else if (t == Treat.FaceBreach)
                    {
                        n += Modules(parent, "W06", a, b, s => At(s, 0f, f.Y), FaceYaw(-o));
                    }

                    break;
                case Treat.Merlon:
                    n += Merlons(parent, a, b, s => At(s, -0.3f, f.Y), FaceYaw(o));
                    break;
            }

            if (boundary)
            {
                Foundation(parent, a, b, alongX, t == Treat.Low ? At((a + b) * 0.5f, 1f, 0f) : t == Treat.Wall || t == Treat.Cut ? At((a + b) * 0.5f, 1f, 0f) : At((a + b) * 0.5f, -1f, 0f));
            }

            return n;
        }

        // Curtain faces (W01a/b/c: 6, 4 and 2 m) along a..b, the last piece stretched to close the run.
        private static int Curtain(Transform parent, float a, float b, System.Func<float, Vector3> at, float yaw, bool alongX)
        {
            float length = b - a;
            var parts = new List<float>();
            float rem = length;
            while (rem > 6.5f)
            {
                parts.Add(6f);
                rem -= 6f;
            }

            parts.Add(rem);
            float s = a;
            int n = 0;
            foreach (float l in parts)
            {
                string id = l > 4.5f ? "W01a" : l > 2.5f ? "W01b" : "W01c";
                float native = id == "W01a" ? 6f : id == "W01b" ? 4f : 2f;
                Put(id, parent, at(s + l * 0.5f), yaw, new Vector3(l / native, 1f, 1f));
                s += l;
                n++;
            }

            return n;
        }

        // 2 m kit modules along a..b, evenly stretched to fill it; 'alternate' mixes in a variant one in three.
        private static int Modules(Transform parent, string id, float a, float b, System.Func<float, Vector3> at, float yaw, string alternate = null)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / 2f));
            float l = (b - a) / count;
            for (int i = 0; i < count; i++)
            {
                float s = a + l * (i + 0.5f);
                string use = alternate != null && Mathf.Abs(Mathf.RoundToInt(s * 7.3f)) % 3 == 0 ? alternate : id;
                Put(use, parent, at(s), yaw, new Vector3(l / 2f, 1f, 1f));
            }

            return count;
        }

        // The outer parapet: mostly whole merlons, some chipped, the odd stub or gone.
        private static int Merlons(Transform parent, float a, float b, System.Func<float, Vector3> at, float yaw)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((b - a) / 2f));
            float l = (b - a) / count;
            for (int i = 0; i < count; i++)
            {
                float s = a + l * (i + 0.5f);
                int h = Mathf.Abs(Mathf.RoundToInt(s * 13.7f + at(s).x * 3.1f)) % 10;
                string id = h < 6 ? "W02a" : h < 8 ? "W02b" : h < 9 ? "W02c" : "W02d";
                Put(id, parent, at(s), yaw, new Vector3(l / 2f, 1f, 1f));
            }

            return count;
        }

        // The castle's footing from the ravine bed up to the ground line, under a boundary run.
        private static void Foundation(Transform parent, float a, float b, bool alongX, Vector3 mid)
        {
            float length = b - a;
            var size = alongX ? new Vector3(length, -L4Plan.RavineY, 2f) : new Vector3(2f, -L4Plan.RavineY, length);
            var go = L3Build.Fit(parent, "K:SM_Bld_Castle_Wall_01", alongX ? 0 : 90, size, new Vector2(mid.x, mid.z), L4Plan.RavineY, L4Build.Mat("L4_Knights_StoneDark"));
            go.name = "Foundation";
        }

        // ---------------------------------------------------------------- Placements

        private static string Places(Transform root, char stage)
        {
            int count = 0;
            foreach (var p in L4Plan.Places)
            {
                if (p.Stage > stage)
                {
                    continue;
                }

                var go = Put(p.Id, Group(root, p.Group, p.Area, p.Threshold), p.Position, p.Yaw);
                go.name = p.Name;
                placed[p.Name] = go;
                count++;
            }

            return count + " placed";
        }

        private static T Get<T>(string name) where T : Component
        {
            return placed.TryGetValue(name, out var go) ? go.GetComponentInChildren<T>(true) : null;
        }

        // The links the hazards need (each set on the instance, so the prefab stays generic).
        private static string Links()
        {
            int n = 0;
            void Light(string light, string slick)
            {
                var l = Get<HangingLight>(light);
                var s = Get<OilSlick>(slick);
                if (l != null && s != null)
                {
                    L3Build.Set(l, "slick", s);
                    n++;
                }
            }

            void Partners(params string[] names)
            {
                var all = new List<KneelerAmbush>();
                foreach (var name in names)
                {
                    var k = Get<KneelerAmbush>(name);
                    if (k != null)
                    {
                        all.Add(k);
                    }
                }

                foreach (var k in all)
                {
                    L3Build.SetArray(k, "partners", all.FindAll(o => o != k).ToArray());
                    n++;
                }
            }

            Light("Cresset_Lists_1", "Slick_Lists_1");
            Light("Cresset_Lists_2", "Slick_Lists_2");
            Light("Corona", "Slick_Nave_Crossing");
            Light("Tripod_W", "Slick_Forecourt_W");
            Light("Tripod_E", "Slick_Forecourt_E");
            Partners("Kneeler_Twin_1", "Kneeler_Twin_2");
            Partners("Kneeler_Twin_3", "Kneeler_Twin_4");

            var rings = new List<MurderHole>();
            for (int i = 0; i < 3; i++)
            {
                var ring = Get<MurderHole>("PourRing_" + i);
                if (ring != null)
                {
                    L3Build.Set(ring, "order", i);
                    L3Build.Set(ring, "ringsInLoop", 3);
                    rings.Add(ring);
                }
            }

            var tender = Get<PourTender>("PourTrough");
            if (tender != null && rings.Count > 0)
            {
                L3Build.SetArray(tender, "holes", rings.ToArray());
                n++;
            }

            var demoGate = Get<DemoTrigger>("Demo_OuterPortcullis");
            if (demoGate != null)
            {
                L3Build.Set(demoGate, "portcullis", Get<PortcullisCycle>("OuterPortcullis"));
                n++;
            }

            var demoScaffold = Get<DemoTrigger>("Demo_Scaffold");
            if (demoScaffold != null)
            {
                L3Build.Set(demoScaffold, "scaffold", Get<ScaffoldCollapse>("Scaffold_Demo"));
                n++;
            }

            var winch = Get<Winch>("Winch");
            if (winch != null)
            {
                L3Build.Set(winch, "gate", Get<PortcullisCycle>("LaneGate"));
                n++;
            }

            var loft = Get<ScaffoldCollapse>("ChoirLoft");
            if (loft != null)
            {
                L3Build.Set(loft, "igniteOnCollapse", Get<OilSlick>("Slick_Nave_N"));
                n++;
            }

            return n + " links";
        }

        // ---------------------------------------------------------------- Enemies, vistas, arenas

        private static EnemyType TypeOf(string id)
        {
            switch (id)
            {
                case "N01": return EnemyType.Footman;
                case "N02": return EnemyType.IronHusk;
                case "N03": return EnemyType.CursedTwin;
                case "N04": return EnemyType.Crossbowman;
                case "N05": return EnemyType.Fool;
                case "N06a": return EnemyType.Hound;
                case "N06b": return EnemyType.Brute;
                default: return EnemyType.Steward;
            }
        }

        private static string Enemies(Transform root, char stage)
        {
            int count = 0;
            var index = new Dictionary<string, int>();
            foreach (var e in L4Plan.Enemies)
            {
                if (e.Stage > stage)
                {
                    continue;
                }

                index.TryGetValue(e.Encounter, out int k);
                index[e.Encounter] = k + 1;
                var parent = Group(root, "Spawns", e.Encounter, e.Threshold);
                var go = Put(e.Id, parent, e.Position, e.Yaw);
                go.name = "Spawn_" + e.Encounter + "_" + TypeOf(e.Id) + "_" + (k + 1);
                var marker = go.GetComponentInChildren<SpawnMarker>(true);
                if (marker == null)
                {
                    // The project's own enemies carry no marker: shown for scale with their behaviour off, under one.
                    foreach (var behaviour in go.GetComponentsInChildren<Behaviour>(true))
                    {
                        if (!(behaviour is Animator))
                        {
                            behaviour.enabled = false;
                        }
                    }

                    var holder = Put("M12", parent, e.Position, e.Yaw);
                    holder.name = go.name;
                    go.transform.SetParent(holder.transform, true);
                    go.name = e.Id;
                    marker = holder.GetComponentInChildren<SpawnMarker>(true);
                }

                if (marker != null)
                {
                    marker.enemyType = TypeOf(e.Id);
                    marker.encounterId = e.Encounter;
                    marker.initialState = e.State;
                    EditorUtility.SetDirty(marker);
                }

                // The courtyard Husk carries the tribunal key on his belt.
                if (e.Encounter == "E3" && e.Id == "N02")
                {
                    var key = Put("M04", go.transform, new Vector3(0.28f, 1.02f, 0.12f), 0f);
                    key.name = "TribunalKey";
                }

                count++;
            }

            return count + " enemies";
        }

        [MenuItem("Kneel/L4/Show Marker Volumes")]
        private static void ShowMarkersMenu() => ToggleMarkers(true);

        [MenuItem("Kneel/L4/Hide Marker Volumes")]
        private static void HideMarkersMenu() => ToggleMarkers(false);

        private static void ToggleMarkers(bool show)
        {
            var root = Root;
            if (root == null)
            {
                return;
            }

            foreach (var marker in root.GetComponentsInChildren<Kneel.Markers.MarkerVisual>(true))
            {
                ShowMarkerVolumes(marker.gameObject, show);
            }

            ShowMarkerVolumes(root.transform.Find("Markers").gameObject, show);
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static void ShowMarkerVolumes(GameObject go, bool show)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.Contains("Marker"))
                {
                    r.enabled = show;
                }
            }
        }

        private static string Volumes(Transform root, char stage)
        {
            int count = 0;
            foreach (var v in L4Plan.Volumes)
            {
                if (v.Stage > stage)
                {
                    continue;
                }

                var go = Put(v.Id, Group(root, "Markers", "Volumes", v.Threshold), v.Centre, 0f);
                go.name = v.Name;
                // The box is on the nested marker prefab, a child of the instance.
                foreach (var box in go.GetComponentsInChildren<BoxCollider>())
                {
                    box.size = v.Size;
                    box.center = new Vector3(0f, v.Size.y * 0.5f, 0f);
                }

                // The magenta boxes stay off in the scene (they are hidden in play anyway); Kneel > L4 > Show Marker
                // Volumes brings them back for layout work.
                ShowMarkerVolumes(go, false);

                var vista = go.GetComponentInChildren<VistaMarker>();
                if (vista != null)
                {
                    L3Build.Set(vista, "id", v.Name.Replace("Vista_", ""));
                    L3Build.Set(vista, "pitch", v.Pitch);
                    L3Build.Set(vista, "fieldOfView", v.Fov);
                    L3Build.Set(vista, "pullBack", v.PullBack);
                    L3Build.Set(vista, "sidewaysShift", v.Shift);
                    L3Build.Set(vista, "panTarget", v.PanTarget);
                    L3Build.Set(vista, "panDuration", v.PanDuration);
                    L3Build.Set(vista, "note", v.Note ?? "");
                }

                var arena = go.GetComponentInChildren<ArenaMarker>();
                if (arena != null)
                {
                    L3Build.Set(arena, "arenaId", v.Name.Replace("Arena_", ""));
                }

                count++;
            }

            return count + " volumes";
        }

        // ---------------------------------------------------------------- Player, camera, NavMesh

        private static void PlayerAndCamera()
        {
            Physics.SyncTransforms();
            // At the plan's PlayerSpawn (since the refinement, on the farm track south of the causeway).
            var mark = L4Plan.Places.Find(p => p.Name == "PlayerSpawn");
            var spawn = mark != null ? mark.Position : new Vector3(60f, 0f, 2f);
            if (Physics.Raycast(spawn + Vector3.up * 20f, Vector3.down, out var hit, 40f, 1 << L3Build.Layer(L3Build.Walkable)))
            {
                spawn = hit.point;
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab"));
            player.name = "Player";
            player.transform.SetPositionAndRotation(spawn + Vector3.up * 0.05f, Quaternion.identity);

            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 500f;
            camera.AddComponent<AudioListener>();
            var playerCamera = camera.AddComponent<PlayerCamera>();
            L3Build.Set(playerCamera, "settings", AssetDatabase.LoadAssetAtPath<PlayerCameraSettings>("Assets/Player/Settings/PlayerCameraSettings.asset"));
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 8.7f, -3.6f), Quaternion.Euler(65f, 0f, 0f));
            camera.AddComponent<Kneel.OcclusionFader>();
        }

        // The roof hiders (the two towers, and the guardroom's own) list what goes to shadows only while the player
        // is inside: every renderer of the structure that starts above its roof line (the roof flags and merlons,
        // baked together), and on the NE tower what lies on the roof (D2's scout and pickup), which would otherwise
        // hang in the air over the player once the roof has gone.
        private static void RoofHiders(Transform root)
        {
            foreach (var hider in root.GetComponentsInChildren<RoofHiderMarker>(true))
            {
                string structure = hider.name == "RoofHider_NE" ? "TowerNE" : hider.name == "RoofHider_NW" ? "TowerNW" : hider.name == "RoofHider_Chapel" ? "Chapel" : "Guardroom";
                if (!placed.TryGetValue(structure, out var go))
                {
                    continue;
                }

                // The chapel's ruined roof (refinement) goes while the player fights under it.
                if (structure == "Chapel")
                {
                    var roof = go.transform.Find("Roof");
                    L3Build.SetArray(hider, "roofs", roof != null ? System.Array.ConvertAll(roof.GetComponentsInChildren<Renderer>(true), r => (Object)r) : new Object[0]);
                    continue;
                }

                // Wall heights: towers 5 m, the guardroom 4.5 m (L4Structures).
                float roofLine = go.transform.position.y + (structure == "Guardroom" ? 4.5f : 5f) - 0.4f;
                var roofs = new List<Object>();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.bounds.min.y >= roofLine && !(r is ParticleSystemRenderer))
                    {
                        roofs.Add(r);
                    }
                }

                foreach (var extra in structure == "TowerNE" ? new[] { "DeadScout", "Consumable_D2" } : new string[0])
                {
                    if (placed.TryGetValue(extra, out var item))
                    {
                        roofs.AddRange(item.GetComponentsInChildren<Renderer>(true));
                    }
                }

                L3Build.SetArray(hider, "roofs", roofs.ToArray());
            }
        }

        // Section 15.4's per-agent rules: Not Walkable volumes for one agent, and one-way links.
        private static string NavRules(Transform root, char stage)
        {
            var g = root.Find("NavMesh");
            int volumes = 0, links = 0;
            foreach (var rule in L4Plan.Barred)
            {
                if (rule.Stage > stage)
                {
                    continue;
                }

                var go = L3Build.Child(g, rule.Name, rule.Centre);
                // On a baked layer: the surfaces only collect modifier volumes inside their layer mask.
                go.layer = L3Build.Layer(L3Build.Walkable);
                var volume = go.AddComponent<NavMeshModifierVolume>();
                volume.size = rule.Size;
                volume.center = Vector3.zero;
                volume.area = NavMesh.GetAreaFromName("Not Walkable");
                var so = new SerializedObject(volume);
                var agents = so.FindProperty("m_AffectedAgents");
                agents.arraySize = 1;
                agents.GetArrayElementAtIndex(0).intValue = AgentId(rule.Agent);
                so.ApplyModifiedPropertiesWithoutUndo();
                volumes++;
            }

            foreach (var l in L4Plan.NavLinks)
            {
                if (l.Stage > stage)
                {
                    continue;
                }

                var go = L3Build.Child(g, l.Name, l.Start);
                var link = go.AddComponent<NavMeshLink>();
                link.agentTypeID = AgentId(l.Agent);
                link.startPoint = Vector3.zero;
                link.endPoint = l.End - l.Start;
                link.width = l.Width;
                link.bidirectional = false;
                links++;
            }

            return volumes + " agent no-walk volumes, " + links + " one-way links";
        }

        private static int AgentId(string name)
        {
            foreach (var (n, radius, height) in Agents)
            {
                if (n == name)
                {
                    return L3Level.AgentType(n, radius, height);
                }
            }

            return L3Level.AgentType(name, 0.5f, 2f);
        }

        public static string BakeNavMesh(GameObject root, char stage = 'G')
        {
            foreach (var old in root.GetComponents<NavMeshSurface>())
            {
                Object.DestroyImmediate(old);
            }

            // Gates are baked open: their blockers would otherwise be in the NavMesh for good, and a gate that
            // opens in play could never be walked through. While shut, their NavMeshObstacles carve it.
            var lifted = new List<Collider>();
            foreach (var gate in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(gate is OneTimeGate) && !(gate is PortcullisCycle))
                {
                    continue;
                }

                var blocker = new SerializedObject(gate).FindProperty("blocker");
                if (blocker != null && blocker.objectReferenceValue is Collider c && c.enabled)
                {
                    c.enabled = false;
                    lifted.Add(c);
                }
            }

            var parts = new List<string>();
            foreach (var (name, radius, height) in Agents)
            {
                var surface = root.AddComponent<NavMeshSurface>();
                surface.agentTypeID = L3Level.AgentType(name, radius, height);
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

            // The Perch agent: one surface per perch, collecting only inside its box.
            int perches = 0;
            foreach (var perch in L4Plan.Perches)
            {
                if (perch.Stage > stage)
                {
                    continue;
                }

                var surface = root.AddComponent<NavMeshSurface>();
                surface.agentTypeID = AgentId(perch.Agent);
                surface.collectObjects = CollectObjects.Volume;
                surface.center = perch.Centre;
                surface.size = perch.Size;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = (1 << L3Build.Layer(L3Build.Walkable)) | (1 << L3Build.Layer(L3Build.Boundary));
                surface.BuildNavMesh();
                // One full build in four (2026-10-19) left the gallows perch empty while the same bake run again
                // was fine; check that the perch has a mesh and bake it once more if not.
                var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
                if (!NavMesh.SamplePosition(perch.Centre, out _, perch.Size.magnitude * 0.5f, filter))
                {
                    Physics.SyncTransforms();
                    surface.BuildNavMesh();
                    if (!NavMesh.SamplePosition(perch.Centre, out _, perch.Size.magnitude * 0.5f, filter))
                    {
                        parts.Add(perch.Name + " EMPTY");
                    }
                }

                string path = SceneFolder + "/NavMesh-" + perch.Name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(surface.navMeshData, path);
                perches++;
            }

            if (perches > 0)
            {
                parts.Add("Perch (" + perches + " perches)");
            }

            foreach (var c in lifted)
            {
                c.enabled = true;
            }

            return "NavMesh baked for " + string.Join(", ", parts) + " (" + lifted.Count + " gates baked open)";
        }

        // ---------------------------------------------------------------- Checks

        // Where the Thrall NavMesh may lie although no floor is there: the causeway, the gallows deck, the grand
        // stair and its landing, the crypt, the tower roofs, the hoarding and the metrics map.
        private static readonly Rect[] Allowed =
        {
            new Rect(55f, -10f, 10f, 50f), new Rect(69.5f, 137f, 11f, 10f), new Rect(44f, 254f, 20f, 12f), new Rect(82f, 190f, 10f, 13f),
            new Rect(82f, 153f, 12f, 16f), new Rect(46.5f, 153f, 12f, 12f), new Rect(-120f, -20f, 60f, 90f), new Rect(39.5f, 105.5f, 21f, 3f),
        };

        // Section 15.4's rules, as paths: what each agent must and must not be able to reach.
        private static string RuleChecks(char stage)
        {
            var fails = new List<string>();
            int n = 0;
            void Expect(string what, string agent, Vector3 from, Vector3 to, bool reachable)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = AgentId(agent), areaMask = NavMesh.AllAreas };
                bool got = false;
                if (NavMesh.SamplePosition(from, out var a, 1.5f, filter) && NavMesh.SamplePosition(to, out var b, 1.5f, filter))
                {
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(a.position, b.position, filter, path);
                    got = path.status == NavMeshPathStatus.PathComplete;
                }

                n++;
                if (got != reachable)
                {
                    fails.Add(what);
                }
            }

            if (stage >= 'D')
            {
                Expect("Husk courtyard to guardroom", "Husk", new Vector3(66f, 0f, 140f), new Vector3(70f, L4Plan.T1, 118f), false);
                Expect("Husk courtyard to rampart landing", "Husk", new Vector3(66f, 0f, 140f), new Vector3(79f, L4Plan.T1, 126f), false);
                Expect("Thrall courtyard to guardroom", "Thrall", new Vector3(66f, 0f, 140f), new Vector3(70f, L4Plan.T1, 118f), true);
                Expect("Perch on the gallows deck", "Perch", new Vector3(75f, 2f, 144.5f), new Vector3(71f, 2f, 139f), true);
                Expect("Perch off the gallows", "Perch", new Vector3(75f, 2f, 144.5f), new Vector3(66f, 0f, 140f), false);
            }

            if (stage >= 'F')
            {
                Expect("Hound out of the west kennel", "Hound", new Vector3(40.6f, 0f, 250f), new Vector3(50f, 0f, 246f), true);
                Expect("Hound back into the west kennel", "Hound", new Vector3(50f, 0f, 246f), new Vector3(40.6f, 0f, 250f), false);
                Expect("Hound out of the east kennel", "Hound", new Vector3(67.4f, 0f, 250f), new Vector3(58f, 0f, 246f), true);
                Expect("Hound back into the east kennel", "Hound", new Vector3(58f, 0f, 246f), new Vector3(67.4f, 0f, 250f), false);
                Expect("Thrall into the west kennel", "Thrall", new Vector3(50f, 0f, 246f), new Vector3(40.8f, 0f, 250f), true);
                Expect("Hound up the grand stair", "Hound", new Vector3(54f, 0f, 247f), new Vector3(54f, L4Plan.T1, 265f), false);
                Expect("Thrall up the grand stair", "Thrall", new Vector3(54f, 0f, 247f), new Vector3(54f, L4Plan.T1, 265f), true);
                Expect("Perch on the E6a walk", "Perch", new Vector3(44f, L4Plan.T1, 228f), new Vector3(44f, L4Plan.T1, 238f), true);
                Expect("Perch off the E6a walk", "Perch", new Vector3(44f, L4Plan.T1, 228f), new Vector3(54f, 0f, 216f), false);
            }

            return "agent rules " + (n - fails.Count) + "/" + n + (fails.Count > 0 ? " FAILED: " + string.Join(", ", fails) : "");
        }

        // Reachability from the spawn (Thrall) to every floor's centre, and NavMesh that has leaked outside the
        // level's floors (a hole in the boundary).
        public static string Check(char stage = 'G')
        {
            var lines = new List<string>();
            int agent = L3Level.AgentType("Thrall", 0.5f, 2f);
            var filter = new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas };
            NavMesh.SamplePosition(new Vector3(60f, 0f, 2f), out var start, 4f, filter);
            var unreachable = new List<string>();
            foreach (var f in L4Plan.Floors)
            {
                if (f.Mass)
                {
                    continue;
                }

                var c = new Vector3((f.X0 + f.X1) * 0.5f, f.Y, (f.Z0 + f.Z1) * 0.5f);
                if (!NavMesh.SamplePosition(c, out var end, 3f, filter))
                {
                    unreachable.Add(f.Id + " (no NavMesh)");
                    continue;
                }

                var path = new NavMeshPath();
                NavMesh.CalculatePath(start.position, end.position, filter, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    unreachable.Add(f.Id);
                }
            }

            var tri = NavMesh.CalculateTriangulation();
            int leaks = 0;
            var leakAt = new List<string>();
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                var v = tri.vertices[i];
                var p = new Vector2(v.x, v.z);
                bool ok = false;
                foreach (var f in L4Plan.Floors)
                {
                    if (f.Contains(p, 0.8f) && Mathf.Abs(v.y - f.Y) < 2.5f)
                    {
                        ok = true;
                        break;
                    }
                }

                foreach (var r in Allowed)
                {
                    ok |= r.Contains(p);
                }

                foreach (var s in L4Plan.Stairs)
                {
                    bool alongZ = Mathf.Abs(Mathf.DeltaAngle(s.Yaw, 0f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(s.Yaw, 180f)) < 1f;
                    var half = alongZ ? new Vector2(2.6f, 4.6f) : new Vector2(4.6f, 2.6f);
                    ok |= Mathf.Abs(p.x - s.Centre.x) < half.x && Mathf.Abs(p.y - s.Centre.z) < half.y;
                }

                // Only NavMesh the player could reach counts: wall tops and roofs carry islands nobody stands on.
                if (!ok)
                {
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(start.position, v, filter, path);
                    ok = path.status != NavMeshPathStatus.PathComplete;
                }

                if (!ok)
                {
                    leaks++;
                    if (leakAt.Count < 12)
                    {
                        leakAt.Add(v.ToString("F0"));
                    }
                }
            }

            lines.Add(RuleChecks(stage));
            lines.Add("unreachable from the spawn: " + (unreachable.Count == 0 ? "none" : string.Join(", ", unreachable)));
            lines.Add("reachable NavMesh vertices outside the floors: " + leaks + (leaks > 0 ? " at " + string.Join(" ", leakAt) : ""));
            string report = string.Join(" | ", lines);
            System.IO.File.WriteAllText("Temp/L4LevelCheck.txt", report);
            return report;
        }
    }
}
