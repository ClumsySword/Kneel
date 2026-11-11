using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kneel.EditorTools
{
    // The hall made whole (owner, 2026-11-11): "The upper arch of the backwall and wall right when you walk also read as
    // unfinished. Please refine these to make the hall feel grand, complete and cohesive. Pull in the Knights stone a bit
    // more so we get the full texture, but randomize it a bit so we get a random pattern ... Make sure the Pallete stone
    // do not have gaps ... There is some blue light being cast different places ... make the wall in the beginning (just
    // before the staircase) fit into the arch." Applied to the scene in place; each pass is safe to rerun.
    // - Warm dark: no light in the castle is blue. The ambient's sky term and the character fill were a cool blue-grey,
    //   and with no skybox the reflections came from Unity's default sky cubemap, so every polished edge (the throne's
    //   stone and gilt) caught a blue sky. The ambient and fill are warmed and reflections come from a dark warm cubemap.
    // - The Knights walls: every Castle_Wall_01 tile is rebaked as one continuous tiling per wall plane, at 0.8 of the
    //   old size (more stones to the metre), in running bond, each tile mirrored or turned at random so its big corner
    //   stone lands anywhere. The tiling is clipped exactly to the wall, so tiles meet with no seam (the old tiles only
    //   butted, and the dark core showed between them).
    // - The vaults' end walls: their flat gables (one palette cell, no joints) go; the wall below carries on up into the
    //   arch in the same tiling, clipped to the vault, under a two-order archivolt and over a string course at the
    //   spring line. The stair hall's south wall stood 1.5 m in front of its gable and stopped flat at the spring: the
    //   gable now rises from the wall's own face. A blind rose window crowns each of the great end walls.
    // - The carpet: the lozenge chain is gone; a clean crimson field between flat gold and oxblood borders (L5Level's
    //   CarpetMaterial). New runners: the hall's, from the grand doors up the dais to the throne; a ring round the stair
    //   hall's fire bowl; and from the nave's runner to the foot of each flight.
    public static partial class L5Polish
    {
        private const string PolishMeshes = L5Level.MeshesPath + "/Polish";
        private const string TileMeshName = "SM_Bld_Castle_Wall_01";
        private const string KnightsWallName = "KnightsWall";

        // The new tile's face, against the old (5 x 5 m): smaller, so the wall shows more of its stones.
        private const float TileScale = 0.8f;

        [MenuItem("Kneel/L5/Polish/Walls, Gables, Light and Carpet")]
        public static void HallFinishMenu() => Debug.Log(HallFinish());

        public static string HallFinish()
        {
            OpenScene();
            EnsureFolder(PolishMeshes);
            var log = new List<string>();
            log.Add(WarmTheDark());
            log.Add(StripFlatGables());
            log.Add(RetileWalls());
            log.Add(GableDressing());
            log.Add(Threshold());
            log.Add(Carpets());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return "L5 hall: " + string.Join("; ", log);
        }

        // ---------------------------------------------------------------- Warm dark

        public static readonly Color AmbientSky = new Color32(0x30, 0x2A, 0x26, 0xFF);
        public static readonly Color CharacterFillColour = new Color32(0xD6, 0xC6, 0xB2, 0xFF);

        private static string WarmTheDark()
        {
            RenderSettings.ambientSkyColor = AmbientSky;
            Light fill = GameObject.Find("L5_Root/_Lighting/CharacterFill")?.GetComponent<Light>();
            if (fill != null)
            {
                fill.color = CharacterFillColour;
                EditorUtility.SetDirty(fill);
            }

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = WarmCubemap();
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
            return "ambient and fill warmed, reflections from a dark warm cubemap";
        }

        // What a polished surface in the hall reflects: the vault's dark stone overhead, a little firelight round the
        // horizon (the torches on every wall), the floor below.
        private static Cubemap WarmCubemap()
        {
            const string path = L5Level.MaterialsPath + "/Textures/L5_HallReflection.cubemap";
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cube == null)
            {
                cube = new Cubemap(16, TextureFormat.RGBAHalf, false);
                AssetDatabase.CreateAsset(cube, path);
            }

            Color above = new Color(0.035f, 0.024f, 0.018f), horizon = new Color(0.11f, 0.06f, 0.03f), below = new Color(0.03f, 0.02f, 0.015f);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                var px = new Color[16 * 16];
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        Vector3 d = CubeDirection(face, (x + 0.5f) / 16f * 2f - 1f, (y + 0.5f) / 16f * 2f - 1f);
                        float up = d.y;
                        px[y * 16 + x] = up >= 0f ? Color.Lerp(horizon, above, Mathf.SmoothStep(0f, 1f, up)) : Color.Lerp(horizon, below, Mathf.SmoothStep(0f, 1f, -up * 2f));
                    }
                }

                cube.SetPixels(px, face);
            }

            cube.Apply(false);
            EditorUtility.SetDirty(cube);
            return cube;
        }

        private static Vector3 CubeDirection(CubemapFace face, float s, float t)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -t, -s).normalized;
                case CubemapFace.NegativeX: return new Vector3(-1f, -t, s).normalized;
                case CubemapFace.PositiveY: return new Vector3(s, 1f, t).normalized;
                case CubemapFace.NegativeY: return new Vector3(s, -1f, -t).normalized;
                case CubemapFace.PositiveZ: return new Vector3(s, -t, 1f).normalized;
                default: return new Vector3(-s, -t, -1f).normalized;
            }
        }

        // ---------------------------------------------------------------- The vaults

        // A pointed barrel vault as L5Level builds it: across x0..x1, springing at `spring`, its crown `rise` above.
        private struct Vault
        {
            public string name;
            public float x0, x1, spring, rise, y0, y1;

            public float Crown => spring + Rise;
            private float A => (x1 - x0) * 0.5f;
            private float Rise => Mathf.Max(rise, A * 1.1f);

            // The underside's outline in (x, height), from the west springing over the crown to the east, and each
            // point's inward normal.
            public List<Vector2> Profile(out List<Vector2> inward)
            {
                float a = A, xc = x0 + a, r = Rise;
                float radius = (a * a + r * r) / (2f * a);
                float leftCentre = x0 + radius;
                float end = Mathf.Atan2(r, xc - leftCentre);
                const int perSide = 14;
                var profile = new List<Vector2>();
                inward = new List<Vector2>();
                for (int i = 0; i <= perSide; i++)
                {
                    float ang = Mathf.Lerp(Mathf.PI, end, i / (float)perSide);
                    var p = new Vector2(leftCentre + radius * Mathf.Cos(ang), spring + radius * Mathf.Sin(ang));
                    profile.Add(p);
                    inward.Add((new Vector2(leftCentre, spring) - p).normalized);
                }

                for (int i = perSide - 1; i >= 0; i--)
                {
                    profile.Add(new Vector2(2f * xc - profile[i].x, profile[i].y));
                    inward.Add(new Vector2(-inward[i].x, inward[i].y));
                }

                return profile;
            }
        }

        private static readonly Vault HallVault = new Vault { name = "HallVault", x0 = 39.6f, x1 = 68.4f, spring = 24.5f, rise = 16f, y0 = 408f, y1 = 472.5f };
        private static readonly Vault StairVault = new Vault { name = "StairVault", x0 = 34.5f, x1 = 73.5f, spring = 20f, rise = 22f, y0 = 358.5f, y1 = 406.5f };
        private static readonly Vault NaveVault = new Vault { name = "NaveVault", x0 = 46.5f, x1 = 61.5f, spring = 11f, rise = 8.6f, y0 = 268f, y1 = 358.5f };

        // One finished end wall: the face it shows the room (z), which way the room lies (+1 north of it, -1 south),
        // the vault it closes, how high the wall below already reaches, and its rose window (radius 0: none).
        private struct EndWall
        {
            public string name;
            public Vault vault;
            public float face, room, wallTop, rose, roseHeight;
            public bool fill;
            // An open arch (the nave's mouth): no string course across it, and a soffit through the wall to `through`.
            public bool open;
            public float through;
        }

        private static readonly EndWall[] EndWalls =
        {
            // Behind the throne: the wall the whole fight faces.
            new EndWall { name = "ThroneEnd", vault = HallVault, face = 472.5f, room = -1f, wallTop = 25f, rose = 5f, roseHeight = 32.2f, fill = true },
            // Over the grand doors, from inside the hall; its back is the stair hall's north wall (one fill for both).
            new EndWall { name = "DoorEnd", vault = HallVault, face = 408f, room = 1f, wallTop = 25f, rose = 4.2f, roseHeight = 31.6f, fill = false },
            new EndWall { name = "LandingEnd", vault = StairVault, face = 406.5f, room = -1f, wallTop = 25f, rose = 6f, roseHeight = 30.5f, fill = true },
            // Where the nave opens into the stair hall: the wall over its mouth now rises from its own face into the arch.
            new EndWall { name = "StairSouthEnd", vault = StairVault, face = 360f, room = 1f, wallTop = 20f, rose = 6f, roseHeight = 30.5f, fill = true },
            new EndWall { name = "NaveSouthEnd", vault = NaveVault, face = 269.5f, room = 1f, wallTop = 11f, rose = 1.9f, roseHeight = 15.4f, fill = true },
            // The nave's mouth into the stair hall (owner, 2026-11-11: "the wall covering the exit just before the
            // stairs need to be an arch as well. There should not be a big square wall blocking the view"): the wall
            // over it is cut to the nave vault's own arch, framed on both faces, a soffit through the wall between.
            new EndWall { name = "NaveNorthEnd", vault = NaveVault, face = 358.5f, room = -1f, wallTop = 20f, rose = 0f, fill = false, open = true, through = 360f },
            new EndWall { name = "NaveMouth", vault = NaveVault, face = 360f, room = 1f, wallTop = 20f, rose = 0f, fill = false, open = true, through = 358.5f },
        };

        // The vaults' flat gables: every triangle fanned from the middle of a springing line at either end.
        private static string StripFlatGables()
        {
            int removed = 0;
            foreach (Vault vault in new[] { HallVault, StairVault, NaveVault })
            {
                var filter = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).FirstOrDefault(f => f.name == vault.name);
                if (filter == null || filter.sharedMesh == null)
                    continue;
                Mesh mesh = filter.sharedMesh;
                Vector3[] v = mesh.vertices;
                float xc = (vault.x0 + vault.x1) * 0.5f;
                bool Mid(Vector3 p) => Mathf.Abs(p.x - xc) < 0.01f && Mathf.Abs(p.y - vault.spring) < 0.01f && (Mathf.Abs(p.z - vault.y0) < 0.01f || Mathf.Abs(p.z - vault.y1) < 0.01f);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] tris = mesh.GetTriangles(s);
                    var keep = new List<int>(tris.Length);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        if (Mid(v[tris[i]]) || Mid(v[tris[i + 1]]) || Mid(v[tris[i + 2]]))
                        {
                            removed++;
                            continue;
                        }

                        keep.Add(tris[i]);
                        keep.Add(tris[i + 1]);
                        keep.Add(tris[i + 2]);
                    }

                    mesh.SetTriangles(keep, s);
                }

                EditorUtility.SetDirty(mesh);
            }

            return removed + " flat gable triangles removed";
        }

        // ---------------------------------------------------------------- The Knights walls

        // A wall plane: the tiles' frame (u along the wall, v up, t through it), the slab's thickness, and the regions
        // of it the wall covers (convex, counter-clockwise in (u, v), not overlapping).
        private class WallPlane
        {
            public bool alongX;
            public float t0, t1;
            public readonly List<List<Vector2>> regions = new List<List<Vector2>>();
            public ShadowCastingMode shadows = ShadowCastingMode.On;
            public uint renderingLayers = 1;
            public int layer;
            public StaticEditorFlags flags;

            public Vector3 U => alongX ? Vector3.right : Vector3.forward;
            public Vector3 N => alongX ? Vector3.forward : Vector3.right;
            public Vector3 Origin => N * ((t0 + t1) * 0.5f);
        }

        private static string RetileWalls()
        {
            string log0 = "";
            // The original tiles are kept, switched off, as the record of where the walls are (so this reruns).
            Transform art = GameObject.Find("L5_Root/Art").transform;
            Transform walls = art.Find("Walls");
            Transform source = walls.Find(KnightsWallName + "Source");
            if (source == null)
            {
                source = new GameObject(KnightsWallName + "Source").transform;
                source.SetParent(walls, false);
                foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.sharedMesh.name == TileMeshName).ToList())
                    filter.transform.SetParent(source, true);
            }

            source.gameObject.SetActive(true);
            var tiles = source.GetComponentsInChildren<MeshFilter>(true).ToList();
            Transform holder = walls.Find(KnightsWallName + "s");
            if (tiles.Count == 0)
                return "walls: no Knights tiles found";

            Mesh tile = tiles[0].sharedMesh;
            Material stone = tiles[0].GetComponent<MeshRenderer>().sharedMaterial;
            var planes = new Dictionary<string, WallPlane>();
            int overlaps = 0;
            foreach (MeshFilter filter in tiles)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                Bounds b = renderer.bounds;
                bool alongX = b.size.x >= b.size.z;
                float t0 = alongX ? b.min.z : b.min.x, t1 = alongX ? b.max.z : b.max.x;
                string key = (alongX ? "x" : "z") + Mathf.RoundToInt(t0 * 20f) + "_" + Mathf.RoundToInt(t1 * 20f);
                if (planes.TryGetValue(key, out WallPlane plane) == false)
                {
                    plane = new WallPlane { alongX = alongX, t0 = t0, t1 = t1, shadows = renderer.shadowCastingMode, renderingLayers = renderer.renderingLayerMask, layer = filter.gameObject.layer, flags = GameObjectUtility.GetStaticEditorFlags(filter.gameObject) };
                    planes[key] = plane;
                }

                float a0 = alongX ? b.min.x : b.min.z, a1 = alongX ? b.max.x : b.max.z;
                var rect = Box2(a0, b.min.y, a1, b.max.y);
                foreach (List<Vector2> other in plane.regions)
                {
                    if (Overlap(other, rect))
                        overlaps++;
                }

                plane.regions.Add(rect);
            }

            // The end walls carry on up into their vaults, in the plane of the wall below.
            foreach (EndWall end in EndWalls)
            {
                if (end.fill == false)
                    continue;
                float t0 = end.room < 0f ? end.face : end.face - 1.5f, t1 = t0 + 1.5f;
                WallPlane plane = planes.Values.FirstOrDefault(p => p.alongX && Mathf.Abs((end.room < 0f ? p.t0 : p.t1) - end.face) < 0.06f);
                if (plane == null)
                {
                    plane = new WallPlane { alongX = true, t0 = t0, t1 = t1, flags = StaticEditorFlags.BatchingStatic };
                    planes["gable_" + end.name] = plane;
                }

                List<Vector2> profile = end.vault.Profile(out _);
                List<Vector2> arch = ClipHalfPlane(profile, new Vector2(0f, end.wallTop), Vector2.up);
                plane.regions.Add(Ccw(arch));
            }

            // The nave's mouth: its arch is cut out of the wall over it (to the floor; below 11 m it was open already).
            List<Vector2> mouth = NaveVault.Profile(out _);
            mouth.Add(new Vector2(NaveVault.x1, -1f));
            mouth.Add(new Vector2(NaveVault.x0, -1f));
            mouth = Ccw(mouth);
            int cut = 0;
            foreach (WallPlane plane in planes.Values.Where(p => p.alongX && Mathf.Abs(p.t0 - 358.5f) < 0.06f && Mathf.Abs(p.t1 - 360f) < 0.06f))
            {
                var kept = new List<List<Vector2>>();
                foreach (List<Vector2> region in plane.regions)
                {
                    if (Overlap(region, mouth) == false)
                    {
                        kept.Add(region);
                        continue;
                    }

                    kept.AddRange(Subtract(region, mouth));
                    cut++;
                }

                plane.regions.Clear();
                plane.regions.AddRange(kept);
            }

            log0 = cut + " wall pieces cut to the nave's mouth, " + StripMouthCore();

            if (holder != null)
                Object.DestroyImmediate(holder.gameObject);
            holder = new GameObject(KnightsWallName + "s").transform;
            holder.SetParent(walls, false);

            int meshes = 0, vertices = 0, made = 0;
            int seed = 6100;
            foreach (KeyValuePair<string, WallPlane> pair in planes.OrderBy(p => p.Key))
            {
                WallPlane plane = pair.Value;
                var rng = new System.Random(seed++);
                foreach ((string name, Mesh mesh) in TilePlane(plane, tile, rng, pair.Key))
                {
                    mesh.name = "L5_" + KnightsWallName + "_" + name;
                    Mesh saved = SaveMeshAsset(mesh, PolishMeshes + "/" + mesh.name + ".asset");
                    var go = new GameObject(KnightsWallName);
                    go.transform.SetParent(holder, false);
                    go.layer = plane.layer;
                    GameObjectUtility.SetStaticEditorFlags(go, plane.flags);
                    go.AddComponent<MeshFilter>().sharedMesh = saved;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = stone;
                    r.shadowCastingMode = plane.shadows;
                    r.renderingLayerMask = plane.renderingLayers;
                    meshes++;
                    vertices += saved.vertexCount;
                }

                made++;
            }

            source.gameObject.SetActive(false);
            return "walls: " + log0 + "; " + tiles.Count + " tiles in " + made + " planes rebaked as " + meshes + " meshes (" + vertices / 1000 + "k vertices), " + overlaps + " overlapping tiles found";
        }

        // Tiles a plane's regions in running bond and bakes them, clipped to the regions, a mesh per 8 m along it.
        private static IEnumerable<(string, Mesh)> TilePlane(WallPlane plane, Mesh tile, System.Random rng, string key)
        {
            // The tile's face, not its bounds: the face is 5.0 x 5.0 m (x -2.49..2.51, y 0..5), but stones buried in the
            // wall stick out 0.2 m past its sides, and laying tiles by the bounds left a 0.4 m slot between faces
            // with those stones showing in it (the "vertical gaps").
            Bounds nb = tile.bounds;
            var face = new Bounds(new Vector3(0.011f, 2.5f, nb.center.z), new Vector3(5f, 5f, nb.size.z));
            float tw = face.size.x * TileScale, th = face.size.y * TileScale;
            float depth = plane.t1 - plane.t0;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (List<Vector2> region in plane.regions)
            {
                foreach (Vector2 p in region)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }

            Vector3[] tv = tile.vertices;
            Vector3[] tn = tile.normals;
            Vector2[] tuv = tile.uv;
            // The tile's two faces stand at z +-1.0; the 438 triangles between them (of 1534) are never seen.
            float hidden = nb.extents.z * 0.85f;
            var kept = new List<int>();
            int[] all = tile.triangles;
            for (int i = 0; i < all.Length; i += 3)
            {
                if (Mathf.Max(Mathf.Abs(tv[all[i]].z - nb.center.z), Mathf.Abs(tv[all[i + 1]].z - nb.center.z), Mathf.Abs(tv[all[i + 2]].z - nb.center.z)) < hidden)
                    continue;
                kept.AddRange(new[] { all[i], all[i + 1], all[i + 2] });
            }

            int[] tt = kept.ToArray();
            Vector3 U = plane.U, V = Vector3.up, N = plane.N, O = plane.Origin;
            bool rightHanded = Vector3.Dot(Vector3.Cross(U, V), N) > 0f;
            // 8 m pieces: each stays under 65k vertices (16-bit indices) and culls on its own.
            const float chunk = 8f;
            var builders = new Dictionary<int, MeshBuilder>();

            float vPhase = (float)rng.NextDouble() * th;
            for (float v = min.y - vPhase; v < max.y; v += th)
            {
                float uPhase = (float)rng.NextDouble() * tw;
                for (float u = min.x - uPhase; u < max.x; u += tw)
                {
                    var cell = Box2(u, v, u + tw, v + th);
                    var touching = plane.regions.Where(r => Overlap(r, cell)).ToList();
                    if (touching.Count == 0)
                        continue;

                    float mx = rng.NextDouble() < 0.5 ? -1f : 1f, fy = rng.NextDouble() < 0.5 ? -1f : 1f;
                    float sx = mx * tw / face.size.x, sy = fy * th / face.size.y, sz = depth / nb.size.z;
                    bool flip = (mx * fy < 0f) != (rightHanded == false);
                    Vector3 centre = O + U * (u + tw * 0.5f) + V * (v + th * 0.5f);
                    int bucket = Mathf.FloorToInt((u + tw * 0.5f - min.x) / chunk);
                    if (builders.TryGetValue(bucket, out MeshBuilder builder) == false)
                        builders[bucket] = builder = new MeshBuilder();

                    var world = new TileVertex[tv.Length];
                    for (int i = 0; i < tv.Length; i++)
                    {
                        Vector3 l = tv[i] - face.center;
                        Vector3 p = centre + U * (l.x * sx) + V * (l.y * sy) + N * (l.z * sz);
                        Vector3 n = (U * (tn[i].x / sx) + V * (tn[i].y / sy) + N * (tn[i].z / sz)).normalized;
                        world[i] = new TileVertex { p = p, n = n, uv = tuv.Length > i ? tuv[i] : Vector2.zero, f = new Vector2(Vector3.Dot(p - O, U), Vector3.Dot(p - O, V)) };
                    }

                    foreach (List<Vector2> region in touching)
                    {
                        if (cell.All(c => Inside(region, c)))
                        {
                            builder.Indexed(world, tt, flip);
                            continue;
                        }

                        for (int i = 0; i < tt.Length; i += 3)
                        {
                            TileVertex a = world[tt[i]], b = world[flip ? tt[i + 2] : tt[i + 1]], c = world[flip ? tt[i + 1] : tt[i + 2]];
                            List<TileVertex> poly = ClipToRegion(new List<TileVertex> { a, b, c }, region);
                            for (int k = 1; k + 1 < poly.Count; k++)
                                builder.Triangle(poly[0], poly[k], poly[k + 1]);
                        }
                    }
                }
            }

            foreach (KeyValuePair<int, MeshBuilder> pair in builders.OrderBy(p => p.Key))
            {
                if (pair.Value.Count == 0)
                    continue;
                pair.Value.Weld();
                yield return (key + "_" + pair.Key, pair.Value.ToMesh(1, true));
            }
        }

        private struct TileVertex
        {
            public Vector3 p, n;
            public Vector2 uv, f;

            public static TileVertex Lerp(TileVertex a, TileVertex b, float t) => new TileVertex
            {
                p = Vector3.Lerp(a.p, b.p, t),
                n = Vector3.Lerp(a.n, b.n, t).normalized,
                uv = Vector2.Lerp(a.uv, b.uv, t),
                f = Vector2.Lerp(a.f, b.f, t),
            };
        }

        private class MeshBuilder
        {
            private readonly List<Vector3> p = new List<Vector3>();
            private readonly List<Vector3> n = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly Dictionary<int, List<int>> subs = new Dictionary<int, List<int>>();

            public int Count => p.Count;

            private List<int> Sub(int s)
            {
                if (subs.TryGetValue(s, out List<int> list) == false)
                    subs[s] = list = new List<int>();
                return list;
            }

            // A whole tile: its own vertices once each (only those its triangles use), its triangles indexed.
            public void Indexed(TileVertex[] vertices, int[] triangles, bool flip)
            {
                var map = new Dictionary<int, int>();
                List<int> sub = Sub(0);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    foreach (int k in flip ? new[] { triangles[i], triangles[i + 2], triangles[i + 1] } : new[] { triangles[i], triangles[i + 1], triangles[i + 2] })
                    {
                        if (map.TryGetValue(k, out int at) == false)
                        {
                            at = p.Count;
                            map[k] = at;
                            p.Add(vertices[k].p);
                            n.Add(vertices[k].n);
                            uv.Add(vertices[k].uv);
                        }

                        sub.Add(at);
                    }
                }
            }

            public void Triangle(TileVertex a, TileVertex b, TileVertex c)
            {
                int k = p.Count;
                foreach (TileVertex v in new[] { a, b, c })
                {
                    p.Add(v.p);
                    n.Add(v.n);
                    uv.Add(v.uv);
                }

                Sub(0).AddRange(new[] { k, k + 1, k + 2 });
            }

            // A flat quad turned to face `facing`, a UV at each corner.
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
            {
                bool flipped = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f;
                int k = p.Count;
                p.AddRange(new[] { a, b, c, d });
                n.AddRange(Enumerable.Repeat(facing.normalized, 4));
                uv.AddRange(new[] { ua, ub, uc, ud });
                Sub(sub).AddRange(flipped ? new[] { k, k + 2, k + 1, k, k + 3, k + 2 } : new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv0, Vector3 facing) => Quad(sub, a, b, c, d, uv0, uv0, uv0, uv0, facing);

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 facing)
            {
                bool flipped = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f;
                int k = p.Count;
                p.AddRange(new[] { a, b, c });
                n.AddRange(Enumerable.Repeat(facing.normalized, 3));
                uv.AddRange(new[] { ua, ub, uc });
                Sub(sub).AddRange(flipped ? new[] { k, k + 2, k + 1 } : new[] { k, k + 1, k + 2 });
            }

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 uv0, Vector3 facing)
            {
                bool flipped = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f;
                int k = p.Count;
                p.AddRange(new[] { a, b, c });
                n.AddRange(Enumerable.Repeat(facing.normalized, 3));
                uv.AddRange(new[] { uv0, uv0, uv0 });
                Sub(sub).AddRange(flipped ? new[] { k, k + 2, k + 1 } : new[] { k, k + 1, k + 2 });
            }

            // Merges vertices that are the same in position, normal and UV (a clipped tile writes each triangle's own).
            public void Weld()
            {
                var key = new Dictionary<(int, int, int, int, int, int, int, int), int>();
                var remap = new int[p.Count];
                var np = new List<Vector3>();
                var nn = new List<Vector3>();
                var nuv = new List<Vector2>();
                for (int i = 0; i < p.Count; i++)
                {
                    var k = (Mathf.RoundToInt(p[i].x * 1000f), Mathf.RoundToInt(p[i].y * 1000f), Mathf.RoundToInt(p[i].z * 1000f),
                        Mathf.RoundToInt(n[i].x * 100f), Mathf.RoundToInt(n[i].y * 100f), Mathf.RoundToInt(n[i].z * 100f),
                        Mathf.RoundToInt(uv[i].x * 4096f), Mathf.RoundToInt(uv[i].y * 4096f));
                    if (key.TryGetValue(k, out int at) == false)
                    {
                        at = np.Count;
                        key[k] = at;
                        np.Add(p[i]);
                        nn.Add(n[i]);
                        nuv.Add(uv[i]);
                    }

                    remap[i] = at;
                }

                foreach (List<int> list in subs.Values)
                {
                    for (int i = 0; i < list.Count; i++)
                        list[i] = remap[list[i]];
                }

                p.Clear(); p.AddRange(np);
                n.Clear(); n.AddRange(nn);
                uv.Clear(); uv.AddRange(nuv);
            }

            // `compact`: 20 bytes a vertex instead of 32 (normals in 8 bits, UVs in half floats), 16-bit indices where
            // they fit; the walls' meshes are stored as text, and this nearly halves them.
            public Mesh ToMesh(int subMeshes = 1, bool compact = false)
            {
                var mesh = new Mesh { indexFormat = compact && p.Count < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32 };
                if (compact)
                {
                    mesh.SetVertexBufferParams(p.Count,
                        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2));
                    var data = new CompactVertex[p.Count];
                    for (int i = 0; i < data.Length; i++)
                    {
                        Vector3 nn = n[i].normalized;
                        data[i] = new CompactVertex
                        {
                            position = p[i],
                            nx = (sbyte)Mathf.RoundToInt(nn.x * 127f),
                            ny = (sbyte)Mathf.RoundToInt(nn.y * 127f),
                            nz = (sbyte)Mathf.RoundToInt(nn.z * 127f),
                            u = Mathf.FloatToHalf(uv[i].x),
                            v = Mathf.FloatToHalf(uv[i].y),
                        };
                    }

                    mesh.SetVertexBufferData(data, 0, 0, data.Length);
                }
                else
                {
                    mesh.SetVertices(p);
                    mesh.SetNormals(n);
                    mesh.SetUVs(0, uv);
                }

                mesh.subMeshCount = subMeshes;
                for (int s = 0; s < subMeshes; s++)
                    mesh.SetTriangles(Sub(s), s);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct CompactVertex
        {
            public Vector3 position;
            public sbyte nx, ny, nz, nw;
            public ushort u, v;
        }

        private static List<Vector2> Box2(float u0, float v0, float u1, float v1) =>
            new List<Vector2> { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };

        private static List<Vector2> Ccw(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }

            if (area < 0f)
                polygon.Reverse();
            return polygon;
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static bool Inside(List<Vector2> region, Vector2 p)
        {
            for (int i = 0; i < region.Count; i++)
            {
                if (Side(region[i], region[(i + 1) % region.Count], p) < -1e-4f)
                    return false;
            }

            return true;
        }

        // Whether two convex polygons share any area (separating axes).
        private static bool Overlap(List<Vector2> a, List<Vector2> b)
        {
            foreach (List<Vector2> poly in new[] { a, b })
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector2 e = poly[(i + 1) % poly.Count] - poly[i];
                    var axis = new Vector2(-e.y, e.x);
                    float amin = a.Min(p => Vector2.Dot(p, axis)), amax = a.Max(p => Vector2.Dot(p, axis));
                    float bmin = b.Min(p => Vector2.Dot(p, axis)), bmax = b.Max(p => Vector2.Dot(p, axis));
                    if (amax <= bmin + 1e-3f || bmax <= amin + 1e-3f)
                        return false;
                }
            }

            return true;
        }

        // A convex region less a convex hole: the parts of the region outside each of the hole's edges in turn.
        private static List<List<Vector2>> Subtract(List<Vector2> region, List<Vector2> hole)
        {
            var pieces = new List<List<Vector2>>();
            List<Vector2> remaining = region;
            for (int i = 0; i < hole.Count && remaining.Count >= 3; i++)
            {
                Vector2 a = hole[i], e = hole[(i + 1) % hole.Count] - a;
                var outward = new Vector2(e.y, -e.x);
                List<Vector2> outside = ClipHalfPlane(remaining, a, outward);
                if (outside.Count >= 3 && Area(outside) > 1e-3f)
                    pieces.Add(Ccw(outside));
                remaining = ClipHalfPlane(remaining, a, -outward);
            }

            return pieces;
        }

        private static float Area(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }

            return Mathf.Abs(area) * 0.5f;
        }

        // The wall's solid core over the nave's mouth (L5Level's Clad 46.5-61.5, 358.5-360, 11-20) goes with it.
        private static string StripMouthCore()
        {
            var filter = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).FirstOrDefault(f => f.name == "WallCores");
            if (filter == null)
                return "no wall cores";
            Mesh mesh = filter.sharedMesh;
            Vector3[] v = mesh.vertices;
            bool In(Vector3 p) => p.x > 46.4f && p.x < 61.6f && p.y > 10.9f && p.y < 20.1f && p.z > 358.4f && p.z < 360.1f;
            int removed = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] tris = mesh.GetTriangles(s);
                var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (In(v[tris[i]]) && In(v[tris[i + 1]]) && In(v[tris[i + 2]]))
                    {
                        removed++;
                        continue;
                    }

                    keep.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                }

                mesh.SetTriangles(keep, s);
            }

            EditorUtility.SetDirty(mesh);
            return removed + " core triangles removed";
        }

        // ---------------------------------------------------------------- The grand doors' threshold

        // The gallery's floor and the hall's meet in a straight line under the grand doors (owner: "fix the hard edge of
        // the floor from outside the throneroom at top of stairs and from the inside"). A threshold of big stone slabs,
        // flush with both floors, spans the doorway and laps over the line on either side.
        private static string Threshold()
        {
            Transform art = GameObject.Find("L5_Root/Art").transform;
            Transform old = art.Find("Threshold");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var b = new MeshBuilder();
            const float top = L5Greybox.Hall + 0.006f, bottom = L5Greybox.Hall - 0.12f, joint = 0.03f;
            float[] xs = { 48.7f, 51.4f, 54.1f, 56.6f, 59.3f };
            float[] zs = { 405.5f, 407.2f, 408.9f };
            int[] tones = { L2PolyKit.Side, L2PolyKit.TopDark, L2PolyKit.End };
            int k = 0;
            for (int i = 0; i + 1 < xs.Length; i++)
            {
                for (int j = 0; j + 1 < zs.Length; j++)
                {
                    float x0 = xs[i] + joint * 0.5f, x1 = xs[i + 1] - joint * 0.5f, z0 = zs[j] + joint * 0.5f, z1 = zs[j + 1] - joint * 0.5f;
                    Vector2 uv = Cell(tones[(k++ * 7 + j) % tones.Length]);
                    Vector2 dark = Cell(L2PolyKit.TopDark);
                    b.Quad(0, new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), uv, Vector3.up);
                    b.Quad(0, new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, top, z0), new Vector3(x0, top, z0), dark, Vector3.back);
                    b.Quad(0, new Vector3(x0, bottom, z1), new Vector3(x1, bottom, z1), new Vector3(x1, top, z1), new Vector3(x0, top, z1), dark, Vector3.forward);
                    b.Quad(0, new Vector3(x0, bottom, z0), new Vector3(x0, bottom, z1), new Vector3(x0, top, z1), new Vector3(x0, top, z0), dark, Vector3.left);
                    b.Quad(0, new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), dark, Vector3.right);
                }
            }

            // The joints between the slabs: a dark bed just under their tops.
            float jy = top - 0.03f;
            b.Quad(0, new Vector3(xs[0], jy, zs[0]), new Vector3(xs[0], jy, zs[zs.Length - 1]), new Vector3(xs[xs.Length - 1], jy, zs[zs.Length - 1]), new Vector3(xs[xs.Length - 1], jy, zs[0]), Cell(L2PolyKit.Joint), Vector3.up);
            Mesh mesh = SaveMeshAsset(b.ToMesh(1), PolishMeshes + "/L5_GrandDoorsThreshold.asset");
            var go = new GameObject("Threshold");
            go.transform.SetParent(art, false);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = L4Kit.Stone;
            return "a threshold under the grand doors";
        }

        private static List<Vector2> ClipHalfPlane(List<Vector2> polygon, Vector2 point, Vector2 normal)
        {
            var output = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 cur = polygon[i], prev = polygon[(i + polygon.Count - 1) % polygon.Count];
                float dc = Vector2.Dot(cur - point, normal), dp = Vector2.Dot(prev - point, normal);
                if (dc >= 0f)
                {
                    if (dp < 0f)
                        output.Add(Vector2.Lerp(prev, cur, dp / (dp - dc)));
                    output.Add(cur);
                }
                else if (dp >= 0f)
                {
                    output.Add(Vector2.Lerp(prev, cur, dp / (dp - dc)));
                }
            }

            return output;
        }

        // Sutherland-Hodgman against a convex counter-clockwise region, in the plane's (u, v).
        private static List<TileVertex> ClipToRegion(List<TileVertex> polygon, List<Vector2> region)
        {
            List<TileVertex> output = polygon;
            for (int e = 0; e < region.Count && output.Count > 0; e++)
            {
                Vector2 a = region[e], b = region[(e + 1) % region.Count];
                var input = output;
                output = new List<TileVertex>();
                for (int i = 0; i < input.Count; i++)
                {
                    TileVertex cur = input[i], prev = input[(i + input.Count - 1) % input.Count];
                    float dc = Side(a, b, cur.f), dp = Side(a, b, prev.f);
                    if (dc >= 0f)
                    {
                        if (dp < 0f)
                            output.Add(TileVertex.Lerp(prev, cur, dp / (dp - dc)));
                        output.Add(cur);
                    }
                    else if (dp >= 0f)
                    {
                        output.Add(TileVertex.Lerp(prev, cur, dp / (dp - dc)));
                    }
                }
            }

            return output;
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string path)
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

        // ---------------------------------------------------------------- Archivolts, string courses, roses

        private static Material RoseGlass
        {
            get
            {
                string path = L5Level.MaterialsPath + "/L5_RoseGlass.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }

                // Old crimson glass with nothing lit behind it: dark, a little gloss to catch the hall's fire.
                // The last chandelier hangs 4.5 m in front of the throne end's rose: kept dark so it doesn't burn white.
                material.SetColor("_BaseColor", new Color(0.075f, 0.016f, 0.02f));
                material.SetFloat("_Smoothness", 0.45f);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
                return material;
            }
        }

        private static string GableDressing()
        {
            Transform art = GameObject.Find("L5_Root/Art").transform;
            Transform old = art.Find("EndWalls");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var holder = new GameObject("EndWalls").transform;
            holder.SetParent(art, false);

            int roses = 0;
            foreach (EndWall end in EndWalls)
            {
                var b = new MeshBuilder();
                List<Vector2> profile = end.vault.Profile(out List<Vector2> inward);
                Archivolt(b, profile, inward, end.face, end.room);
                if (end.open)
                    Soffit(b, profile, inward, end.face, end.through);
                else
                    StringCourse(b, end.vault.x0, end.vault.x1, end.vault.spring, end.face, end.room);
                if (end.rose > 0f)
                {
                    Rose(b, new Vector3((end.vault.x0 + end.vault.x1) * 0.5f, end.roseHeight, end.face), end.room, end.rose);
                    roses++;
                }

                Mesh mesh = SaveMeshAsset(b.ToMesh(2), PolishMeshes + "/L5_EndWall_" + end.name + ".asset");
                var go = new GameObject(end.name);
                go.transform.SetParent(holder, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { L4Kit.Stone, RoseGlass };
            }

            return EndWalls.Length + " end walls dressed (archivolts, string courses, " + roses + " rose windows)";
        }

        private static Vector2 Cell(int cell) => L4Kit.Cell(cell);

        // Two orders of voussoirs round the vault's end, standing out of the end wall into the room: the outer order
        // broad and shallow, the inner narrow and deep, so the arch steps in to the gable like the grand doors' portal.
        private static void Archivolt(MeshBuilder b, List<Vector2> profile, List<Vector2> inward, float face, float room)
        {
            var orders = new[] { (width: 1.1f, depth: 0.55f, cell: L2PolyKit.Side, cheek: L2PolyKit.End), (width: 0.55f, depth: 1.0f, cell: L2PolyKit.TopDark, cheek: L2PolyKit.Side) };
            Vector3 At(Vector2 p, float z) => new Vector3(p.x, p.y, z);
            foreach (var order in orders)
            {
                float z0 = face, z1 = face + room * order.width;
                for (int i = 0; i + 1 < profile.Count; i++)
                {
                    Vector2 a = profile[i], c = profile[i + 1];
                    Vector2 ai = a + inward[i] * order.depth, ci = c + inward[i + 1] * order.depth;
                    var n = new Vector3((inward[i].x + inward[i + 1].x) * 0.5f, (inward[i].y + inward[i + 1].y) * 0.5f, 0f);
                    // Alternate voussoirs a shade apart, as the ribs' blocks are.
                    Vector2 uv = Cell(i % 2 == 0 ? order.cell : L2PolyKit.Top);
                    b.Quad(0, At(ai, z0), At(ai, z1), At(ci, z1), At(ci, z0), uv, n);
                    b.Quad(0, At(a, z1), At(ai, z1), At(ci, z1), At(c, z1), Cell(order.cheek), new Vector3(0f, 0f, room));
                }

                // The ends of the arch, standing on the string course.
                foreach (int i in new[] { 0, profile.Count - 1 })
                {
                    Vector2 p = profile[i], pi = p + inward[i] * order.depth;
                    b.Quad(0, At(p, z0), At(pi, z0), At(pi, z1), At(p, z1), Cell(L2PolyKit.TopDark), Vector3.down);
                }
            }
        }

        // The underside of an arch cut through a wall, from one face to the other.
        private static void Soffit(MeshBuilder b, List<Vector2> profile, List<Vector2> inward, float z0, float z1)
        {
            for (int i = 0; i + 1 < profile.Count; i++)
            {
                Vector2 a = profile[i], c = profile[i + 1];
                var n = new Vector3((inward[i].x + inward[i + 1].x) * 0.5f, (inward[i].y + inward[i + 1].y) * 0.5f, 0f);
                b.Quad(0, new Vector3(a.x, a.y, z0), new Vector3(a.x, a.y, z1), new Vector3(c.x, c.y, z1), new Vector3(c.x, c.y, z0), Cell(i % 2 == 0 ? L2PolyKit.Side : L2PolyKit.End), n);
            }
        }

        // The moulding along the springing line, across the end wall (as L5Level's cornices run along the long walls).
        private static void StringCourse(MeshBuilder b, float x0, float x1, float h, float face, float room)
        {
            var inward = new Vector3(0f, 0f, room);
            var profile = new[] { (-0.15f, -0.45f), (0.45f, -0.45f), (0.45f, 0.15f), (-0.15f, 0.75f) };
            var faces = new[] { (Vector3.down, L2PolyKit.TopDark), (inward, L2PolyKit.Side), ((inward + Vector3.up).normalized, L2PolyKit.Top) };
            Vector3 At(float x, float o, float dy) => new Vector3(x, h + dy, face) + inward * o;
            for (int k = 0; k < 3; k++)
            {
                var (o0, d0) = profile[k];
                var (o1, d1) = profile[k + 1];
                b.Quad(0, At(x0, o0, d0), At(x1, o0, d0), At(x1, o1, d1), At(x0, o1, d1), Cell(faces[k].Item2), faces[k].Item1);
            }
        }

        // A blind rose: a deep moulded ring, crimson glass set back in it, twelve spokes from a hub, and a ring of twelve
        // small roundels between the spokes. Low poly: 32-sided rings, flat faces, palette stone.
        private static void Rose(MeshBuilder b, Vector3 centre, float room, float radius)
        {
            var toRoom = new Vector3(0f, 0f, room);
            float glassAt = 0.12f, frameOut = 0.65f, traceryOut = 0.42f;
            Vector3 P(float r, float angle, float out_) => centre + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f) + toRoom * out_;

            // The glass.
            const int sides = 32;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                b.Tri(1, P(0f, 0f, glassAt), P(radius, a0, glassAt), P(radius, a1, glassAt), Vector2.zero, toRoom);
            }

            // The frame: an outer order and an inner order stepping in to the glass.
            Ring(b, centre, toRoom, radius - 0.02f, radius + 0.7f, 0f, frameOut, sides, L2PolyKit.Side, L2PolyKit.TopDark);
            Ring(b, centre, toRoom, radius - 0.3f, radius, 0f, frameOut - 0.18f, sides, L2PolyKit.TopDark, L2PolyKit.End);

            // The hub and spokes.
            float hub = radius * 0.22f;
            Ring(b, centre, toRoom, hub * 0.55f, hub, 0f, traceryOut + 0.08f, 16, L2PolyKit.Side, L2PolyKit.TopDark);
            Disc(b, centre + toRoom * (traceryOut + 0.02f), toRoom, hub * 0.6f, 16, L2PolyKit.Top);
            for (int s = 0; s < 12; s++)
            {
                float a = s * Mathf.PI / 6f;
                Spoke(b, centre, toRoom, a, hub - 0.05f, radius - 0.25f, Mathf.Max(0.09f, radius * 0.022f), traceryOut);
            }

            // Roundels between the spokes, near the rim.
            float at = radius * 0.7f, small = radius * 0.155f;
            for (int s = 0; s < 12; s++)
            {
                float a = (s + 0.5f) * Mathf.PI / 6f;
                Vector3 c = centre + new Vector3(Mathf.Cos(a) * at, Mathf.Sin(a) * at, 0f);
                Ring(b, c, toRoom, small - Mathf.Max(0.07f, radius * 0.018f), small, 0f, traceryOut - 0.06f, 12, L2PolyKit.Top, L2PolyKit.TopDark);
            }
        }

        // An annulus standing out of the wall from `z0` to `z1` (along `toRoom`): its front face and both walls.
        private static void Ring(MeshBuilder b, Vector3 centre, Vector3 toRoom, float r0, float r1, float z0, float z1, int sides, int front, int sidesCell)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                Vector3 mid = (d0 + d1).normalized;
                Vector2 uv = Cell(i % 2 == 0 ? front : L2PolyKit.Side);
                b.Quad(0, centre + d0 * r0 + toRoom * z1, centre + d0 * r1 + toRoom * z1, centre + d1 * r1 + toRoom * z1, centre + d1 * r0 + toRoom * z1, uv, toRoom);
                b.Quad(0, centre + d0 * r1 + toRoom * z0, centre + d1 * r1 + toRoom * z0, centre + d1 * r1 + toRoom * z1, centre + d0 * r1 + toRoom * z1, Cell(sidesCell), mid);
                b.Quad(0, centre + d0 * r0 + toRoom * z0, centre + d1 * r0 + toRoom * z0, centre + d1 * r0 + toRoom * z1, centre + d0 * r0 + toRoom * z1, Cell(sidesCell), -mid);
            }
        }

        private static void Disc(MeshBuilder b, Vector3 centre, Vector3 facing, float radius, int sides, int cell)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                b.Tri(0, centre, centre + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius, centre + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius, Cell(cell), facing);
            }
        }

        // A bar of tracery from radius r0 to r1 along `angle`, `width` across, standing `out_` from the wall.
        private static void Spoke(MeshBuilder b, Vector3 centre, Vector3 toRoom, float angle, float r0, float r1, float width, float out_)
        {
            var along = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var across = new Vector3(-along.y, along.x, 0f) * (width * 0.5f);
            Vector3 a = centre + along * r0, c = centre + along * r1;
            Vector3 front = toRoom * out_;
            b.Quad(0, a - across + front, c - across + front, c + across + front, a + across + front, Cell(L2PolyKit.Top), toRoom);
            b.Quad(0, a + across, c + across, c + across + front, a + across + front, Cell(L2PolyKit.TopDark), across.normalized);
            b.Quad(0, a - across, c - across, c - across + front, a - across + front, Cell(L2PolyKit.TopDark), -across.normalized);
        }

        // ---------------------------------------------------------------- Carpets (L5PolishCarpet.cs)

        private const float CarpetLift = 0.014f;
    }
}
