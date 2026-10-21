using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The start of L4: L3's farmland, burning down to the stronghold. The owner asked for "a smooth transition from
    // lvl 3 Farmland... no hard lines", then, on a first try that coloured the land's facets in field tones and
    // set L3's heavy crop-row wheat about: "The start of the level looks bad. Please find images of lvl 3 and then
    // make it look like the farmlands but add a nice transition." So this builds the start the way L3 builds
    // itself:
    // - the ground is L3's: a patch of land painted onto L3's ground material (the Kneel/Ground shader over tiled
    //   earth) from L3's own palette, with L3's strips of dead straw and ash (L3Ground.Burnt) and a pale, winding
    //   road ribbon down the track and on to the causeway;
    // - the wheat is L3's: plants of a dozen triangles in drilled rows that bend with the strips (L3Fields.Plant,
    //   Stub), swaying in the wind, all of it burnt black (owner, 2026-10-20) on L3's charred crop materials;
    // - the track is walled with L3's low stone walls like L3's own opening lane, then fenced with its rails;
    //   L3's well, mill, wagons, orchard and dead oaks stand in the fields (the farmhouse by the track was taken
    //   out at the owner's request);
    // - the transition: toward the ravine the fire has taken the farm. The strips go black (the ash and the
    //   stubble take over), soot drifts thicker, a rick burns, the dead lie in the stubble, and the painted land
    //   fades into the colour of the burnt land at the patch's edges so its border never shows.
    // The track itself is floor F00 (L4Plan); the patch replaces the surroundings' faceted ground inside Rect
    // (L4Surroundings skips its faces there). Rebuilding replaces only this group.
    public static class L4Farmland
    {
        public const string GroupName = "Farmland";
        private const string L3 = "Assets/Kneel/Prefabs/L3/";
        private const float Track0 = -44f, Track1 = -10.6f, RoadX = 60f;

        // The painted patch, on the surroundings' 2 m grid (x -10 to 130, z -100 to -8).
        public static readonly Rect Rect = Rect.MinMaxRect(-10f, -100f, 130f, -8f);
        private const float Grid = 2f;
        private const int TexWidth = 1024, TexHeight = 704;
        private const string TexturesPath = "Assets/Kneel/Textures/L4";

        [MenuItem("Kneel/L4/Build/Farmland (replaces that group)")]
        public static void BuildMenu()
        {
            var root = L4Level.Root;
            if (root == null)
            {
                Debug.LogWarning("Open L4_BurningGate first.");
                return;
            }

            Debug.Log(Build(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        private static float Ground(float x, float z) => L4Surroundings.Height(new Vector2(x, z));

        public static string Build(Transform root)
        {
            var old = root.Find(GroupName);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var g = L3Build.Child(root, GroupName).transform;
            var mat = Paint();
            GroundPatch(g, mat);
            int plants = Fields(g);
            int pieces = Bounds(g);
            int props = Farm(g);
            return GroupName + ": painted ground, " + plants + " wheat and stubble plants, " + pieces + " walls and rails, " + props + " farm pieces";
        }

        // ---------------------------------------------------------------- The transition

        // How far the fire has taken the farm at p: 0 out in the fields, rising toward the ravine and the bank in a
        // ragged front, 1 where only ash is left. Shared by the paint and the crops, so wheat stands exactly where
        // the ground is not black.
        public static float Scorch(Vector2 p)
        {
            float front = -32f + (L3Ground.Fbm(p.x * 0.035f + 7f, p.y * 0.02f + 3f) - 0.5f) * 20f;
            float s = Mathf.InverseLerp(front, front + 22f, p.y);
            // Tongues of fire that ran down the fields along the strips.
            float tongue = L3Ground.Step(0.62f, 0.78f, L3Ground.Fbm(p.x * 0.05f + 31f, p.y * 0.012f + 9f));
            return Mathf.Clamp01(Mathf.Max(s, tongue * Mathf.InverseLerp(-80f, -30f, p.y) * 0.85f));
        }

        private static float Burn(Vector2 p) => Mathf.Max(L3Ground.Burnt(p.x, p.y), Scorch(p));

        // ---------------------------------------------------------------- The road

        // The road down the track and on to the causeway, wandering a little inside the walls.
        private static readonly Vector2[] MainRoad =
        {
            new Vector2(59.2f, -104f), new Vector2(60.6f, -86f), new Vector2(59.4f, -68f), new Vector2(60.8f, -52f), new Vector2(59.6f, -40f),
            new Vector2(60.5f, -30f), new Vector2(59.7f, -20f), new Vector2(60f, -8f),
        };

        private static float Polyline(Vector2 p, Vector2[] line, out float t)
        {
            float best = float.MaxValue;
            t = 0f;
            float run = 0f, total = 0f;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                total += Vector2.Distance(line[i], line[i + 1]);
            }

            for (int i = 0; i + 1 < line.Length; i++)
            {
                var a = line[i];
                var b = line[i + 1];
                var ab = b - a;
                float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = (a + ab * k - p).magnitude;
                if (d < best)
                {
                    best = d;
                    t = (run + ab.magnitude * k) / total;
                }

                run += ab.magnitude;
            }

            return best;
        }

        // Signed distance to the road's edge (negative on it) and how strongly it is worn there.
        private static float RoadDistance(Vector2 p, out float weight)
        {
            weight = 1f;
            return Polyline(p, MainRoad, out _) - 2.2f;
        }

        // ---------------------------------------------------------------- The paint

        private static Material Paint()
        {
            // L3's palette (L3Ground.Paint), paler than it ends up: the shader multiplies it by the tiled earth.
            Color burnt = L1Build.Hex("#5C4F42"), straw = L1Build.Hex("#80735A"), ash = L1Build.Hex("#38302A"), regrowth = L1Build.Hex("#5C6645");
            Color chalk = L1Build.Hex("#DDD2BA"), verge = L1Build.Hex("#7C6E57"), cinder = L1Build.Hex("#241D19");
            var pixels = new Color[TexWidth * TexHeight];
            for (int y = 0; y < TexHeight; y++)
            {
                float z = Rect.yMin + (y + 0.5f) / TexHeight * Rect.height;
                for (int x = 0; x < TexWidth; x++)
                {
                    float wx = Rect.xMin + (x + 0.5f) / TexWidth * Rect.width;
                    var p = new Vector2(wx, z);
                    float fine = (L3Ground.Fbm(wx * 0.45f, z * 0.45f) - 0.5f) * 1.3f;

                    float scorch = Scorch(p);
                    float strip = L3Ground.Burnt(wx, z);
                    float black = Mathf.Max(strip, scorch);
                    Color c = Color.Lerp(straw, ash, black * 0.85f);
                    c = Color.Lerp(c, burnt, 0.45f);
                    float green = L3Ground.Step(0.56f, 0.74f, L3Ground.Fbm(wx * 0.07f + 41f, z * 0.07f + 5f));
                    c = Color.Lerp(c, regrowth, green * 0.5f * (1f - black));

                    // The verge inside the track's walls.
                    float lane = Mathf.Abs(wx - RoadX) - 4.4f;
                    if (z < Track1 + 1f && z > Track0 - 2f)
                    {
                        float wVerge = 1f - L3Ground.Step(-0.8f, 1.6f, lane + fine);
                        float tuft = L3Ground.Fbm(wx * 0.33f + 71f, z * 0.33f + 13f);
                        c = Color.Lerp(c, Color.Lerp(verge * 0.85f, Color.Lerp(verge, regrowth, 0.35f), L3Ground.Step(0.4f, 0.7f, tuft)), wVerge * (1f - scorch * 0.6f));
                    }

                    // The road: one ribbon, soft at its edges, worn palest down the middle, sooted near the fire.
                    float roadDistance = RoadDistance(p, out float roadWeight);
                    float wRoad = (1f - L3Ground.Step(-1.1f, 0.7f, roadDistance + fine * 0.55f)) * roadWeight;
                    if (wRoad > 0f)
                    {
                        Color dust = Color.Lerp(chalk * 0.82f, chalk, L3Ground.Fbm(wx * 0.3f + 3f, z * 0.3f + 11f));
                        dust = Color.Lerp(dust * 0.9f, dust, L3Ground.Step(-0.2f, -1.6f, roadDistance));
                        dust = Color.Lerp(dust, ash * 1.3f, scorch * 0.45f);
                        c = Color.Lerp(c, dust, wRoad);
                    }

                    // Soot drifts over everything, thicker toward the fire; nearest it the ground is cinders.
                    float soot = L3Ground.Step(0.56f, 0.72f, L3Ground.Fbm(wx * 0.11f + 31f, z * 0.11f + 17f));
                    c = Color.Lerp(c, ash, Mathf.Clamp01(soot * (0.4f - 0.3f * wRoad) + scorch * 0.35f));
                    float cinders = L3Ground.Step(0.75f, 1f, scorch) * L3Ground.Step(0.35f, 0.65f, L3Ground.Fbm(wx * 0.2f + 5f, z * 0.2f + 8f));
                    c = Color.Lerp(c, cinder, cinders * 0.7f * (1f - wRoad));

                    // The patch's border: into the surroundings' own painted land (the same function paints it),
                    // over the last 24 m, so the farm's edge does not show.
                    float border = Mathf.Min(Mathf.Min(wx - Rect.xMin, Rect.xMax - wx), Mathf.Min(z - Rect.yMin, Rect.yMax - z));
                    c = Color.Lerp(L4Surroundings.LandPaint(p), c, L3Ground.Step(0.5f, 24f, border + fine * 4f));

                    float grain = Mathf.PerlinNoise(wx * 1.4f, z * 1.4f);
                    c *= 0.9f + 0.2f * grain;
                    pixels[y * TexWidth + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 0.04f);
                }
            }

            L2Build.EnsureFolder(TexturesPath);
            string path = TexturesPath + "/L4_FarmGround_Layout.png";
            var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            // L3's ground material, copied, with this layout.
            string matPath = L4Build.MaterialsPath + "/L4_FarmGround.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                AssetDatabase.CopyAsset(L3Build.MaterialsPath + "/L3_Ground.mat", matPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }
            else
            {
                mat.CopyPropertiesFromMaterial(L3Ground.Material);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            mat.SetTexture("_PaveMask", null);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- The ground

        // The patch on the surroundings' own 2 m grid and heights (so its border meets their facets edge for edge),
        // smooth-shaded like L3's ground, UV0 over Rect for the layout. No collider: the track is floor F00.
        private static void GroundPatch(Transform g, Material mat)
        {
            int nx = Mathf.RoundToInt(Rect.width / Grid), nz = Mathf.RoundToInt(Rect.height / Grid);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    float x = Rect.xMin + i * Grid, z = Rect.yMin + j * Grid;
                    verts.Add(new Vector3(x, Ground(x, z), z));
                    uvs.Add(new Vector2((float)i / nx, (float)j / nz));
                }
            }

            var tris = new List<int>();
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, d, a, d, b });
                }
            }

            var mesh = new Mesh { name = "L4_FarmGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // Normals from the same height samples the surroundings' land uses, so the shading runs on across the edge.
            var normals = new List<Vector3>();
            foreach (var v in verts)
            {
                normals.Add(L4Surroundings.Normal(v.x, v.z));
            }

            mesh.SetNormals(normals);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = L3Build.MeshChild(g, "Ground", L4Level.SaveMesh(mesh), Vector3.zero, mat);
            go.layer = L3Build.Layer(L3Build.Unlayered);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ---------------------------------------------------------------- The wheat

        // Rows 0.55 m apart that lean with L3's strips, a plant every half metre: dead wheat where the ground is
        // straw, stubble where it is black, thinning out away from the track where only the vistas look. Never on
        // the road or the track, under a wall, a building or a tree. One mesh per 32 m band, L3's materials.
        private static int Fields(Transform g)
        {
            var parent = L3Build.Child(g, "Fields").transform;
            var keep = new List<(Vector2 at, float r)>
            {
                (new Vector2(42.5f, -27f), 2.5f), (new Vector2(-6f, -78f), 8f), (new Vector2(50.8f, -31f), 3f),
                (new Vector2(76f, -27f), 3.5f), (new Vector2(71f, -16f), 3f), (new Vector2(92f, -47f), 13f),
            };
            var bands = new Dictionary<int, L3Mesh>();
            L3Mesh Band(float z)
            {
                int index = Mathf.FloorToInt((z - Rect.yMin) / 32f);
                if (!bands.TryGetValue(index, out var mesh))
                {
                    mesh = new L3Mesh();
                    while (mesh.Tris.Count < 3)
                    {
                        mesh.Tris.Add(new List<int>());
                    }

                    bands[index] = mesh;
                }

                return mesh;
            }

            int seed = 4100, plants = 0;
            float Rand() => L3Level.Hash("l4field", seed++);
            const float rowPitch = 0.55f, plantPitch = 0.5f;
            for (float u = Rect.xMin + 2f; u < Rect.xMax - 2f; u += rowPitch)
            {
                for (float z = Rect.yMin + 2f; z < Rect.yMax - 0.5f; z += plantPitch)
                {
                    float lean = (L3Ground.Fbm(u * 0.03f, z * 0.014f) - 0.5f) * 30f + Mathf.Sin(z * 0.043f + u * 0.02f) * 3f;
                    float r1 = Rand(), r2 = Rand(), r3 = Rand();
                    var p = new Vector2(u - lean + (r1 - 0.5f) * 0.16f, z + (r2 - 0.5f) * 0.3f);
                    if (!Rect.Contains(p) || Mathf.Abs(p.x - RoadX) < 5f || RoadDistance(p, out _) < 0.6f)
                    {
                        continue;
                    }

                    // Within reach of the track, thinning out toward the patch's border.
                    float off = Mathf.Abs(p.x - RoadX);
                    float border = Mathf.Min(Mathf.Min(p.x - Rect.xMin, Rect.xMax - p.x), Mathf.Min(p.y - Rect.yMin, Rect.yMax - p.y));
                    if (r3 > Mathf.InverseLerp(80f, 44f, off) * L3Ground.Step(1f, 12f, border))
                    {
                        continue;
                    }

                    bool clear = true;
                    foreach (var (at, r) in keep)
                    {
                        clear &= (at - p).sqrMagnitude > r * r;
                    }

                    float y = Ground(p.x, p.y);
                    if (!clear || y < -0.9f)
                    {
                        continue;
                    }

                    float burn = Burn(p);
                    float gaps = L3Ground.Fbm(p.x * 0.09f + 3f, p.y * 0.09f + 77f);
                    if (burn < 0.34f)
                    {
                        if (gaps < 0.36f || Rand() < burn * 1.6f)
                        {
                            continue;
                        }

                        L3Fields.Plant(Band(p.y), y > 0.5f ? L3Fields.Still : L3Fields.Wind, new Vector3(p.x, y + 0.03f, p.y), Mathf.Lerp(0.62f, 0.95f, Rand()) * Mathf.Lerp(1f, 0.8f, burn * 2.5f), ref seed);
                        plants++;
                    }
                    else if (burn > 0.55f && Rand() < 0.45f && gaps > 0.3f)
                    {
                        L3Fields.Stub(Band(p.y), new Vector3(p.x, y + 0.03f, p.y), ref seed);
                        plants++;
                    }
                }
            }

            // Burnt black (owner, 2026-10-20: "I would like the wheats here to be black indicating they have been
            // burned"): L3's charred crop materials, the same palette layout, on the swaying and the still plants.
            L3Fields.Materials();
            var materials = new[] { L3Crops.CharredStandingMaterial, L3Crops.StubbleMaterial, L3Crops.StubbleMaterial };
            foreach (var pair in bands)
            {
                var mesh = pair.Value.ToMesh("L4_FarmFields_" + pair.Key.ToString("00"));
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                var go = L3Build.MeshChild(parent, "Fields " + pair.Key.ToString("00"), L4Level.SaveMesh(mesh), Vector3.zero, materials);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.layer = L3Build.Layer(L3Build.Unlayered);
            }

            return plants;
        }

        // ---------------------------------------------------------------- Walls and rails

        private static GameObject Put(Transform parent, string path, float x, float z, float yaw, float scale = 1f, float sink = 0f, bool onGround = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L3 + path + ".prefab");
            if (prefab == null)
            {
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, (onGround ? Ground(x, z) : 0f) - sink, z), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // The track's boundary (it is the level's edge here): low stone walls like L3's opening lane over its
        // southern half, giving way to rails as it nears the burnt land; a hay wagon across its far end. A few
        // rails, broken where the fire has been, and bramble along the field edges.
        private static int Bounds(Transform g)
        {
            var parent = L3Build.Child(g, "Bounds").transform;
            int n = 0;
            foreach (float x in new[] { 55.5f, 64.5f })
            {
                for (float z = Track0 + 1f; z < Track1; z += 2f)
                {
                    bool wall = z < -26f;
                    if (Put(parent, wall ? "Kit/L3_StoneWall_Low" : "Kit/L3_Rail", x, z, 90f, 1f, 0f, false) != null)
                    {
                        n++;
                    }
                }
            }

            for (float x = 57f; x < 64f; x += 2f)
            {
                if (Put(parent, "Kit/L3_StoneWall_Low", x, Track0 - 0.5f, 0f, 1f, 0f, false) != null)
                {
                    n++;
                }
            }

            Put(parent, "Props/L3_HayWagon", 60f, Track0 + 1.6f, 84f, 1f, 0f, false);

            var rng = new System.Random(812);
            foreach (var (start, dir, length) in new[]
            {
                (new Vector2(66f, -24f), new Vector2(0.21f, -0.98f), 52f),
                (new Vector2(54f, -26f), new Vector2(-0.21f, -0.98f), 48f),
                (new Vector2(68f, -58f), new Vector2(0.98f, 0.21f), 36f),
            })
            {
                for (float s = 0f; s < length; s += 2f)
                {
                    var p = start + dir * s;
                    float scorch = Scorch(p);
                    if (rng.NextDouble() < scorch * 0.8f)
                    {
                        continue;
                    }

                    string kind = scorch < 0.2f && rng.NextDouble() < 0.8 ? "Kit/L3_Rail" : rng.NextDouble() < 0.5 ? "Kit/L3_Rail_Broken" : "Kit/L3_Rail_Leaning";
                    if (Put(parent, kind, p.x, p.y, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f) != null)
                    {
                        n++;
                    }
                }
            }

            foreach (var (x, z, yaw) in new[] { (44f, -60f, 12f), (46f, -64f, 15f), (78f, -70f, -10f), (80f, -66f, -14f) })
            {
                if (Put(parent, "Kit/L3_Hedge_Low", x, z, yaw) != null)
                {
                    n++;
                }
            }

            return n;
        }

        // ---------------------------------------------------------------- The farm

        private static int Farm(Transform g)
        {
            var parent = L3Build.Child(g, "Farm").transform;
            int n = 0;
            void Add(GameObject go)
            {
                if (go != null)
                {
                    n++;
                }
            }

            Add(Put(parent, "Props/L3_Well", 42.5f, -27f, 20f));
            Add(Put(parent, "Structures/L3_Windmill", -6f, -78f, 160f, 1f, 0.3f));
            Add(Put(parent, "Props/L3_GrainCart", 50.8f, -31f, 170f));
            Add(Put(parent, "Props/L3_HayWagon", 76f, -27f, 30f));
            var rng = new System.Random(816);
            for (int i = 0; i < 9; i++)
            {
                float x = 84f + (i % 3) * 6.5f + (float)rng.NextDouble() * 2f, z = -40f - (i / 3) * 7f + (float)rng.NextDouble() * 2f;
                Add(Put(parent, "Structures/L3_OrchardTree", x, z, rng.Next(360), 0.9f + (float)rng.NextDouble() * 0.3f));
            }

            foreach (var (x, z) in new[] { (51.5f, -16f), (68.5f, -38f), (47f, -52f), (26f, -58f), (88f, -20f) })
            {
                Add(Put(parent, "Structures/L3_BoundaryOak", x, z, rng.Next(360)));
            }

            // Toward the bank the farm burns: a rick alight, the fields smouldering, and the first of the dead.
            L2Fire.Fire(parent, "BurningRick", new Vector3(71f, Ground(71f, -16f) + 0.2f, -16f), 1.4f, 3.5f, 11f, true, new Vector3(2.4f, 0f, 2.4f));
            L2Fire.Smoulder(parent, "FieldSmoulder_W", new Vector3(44f, Ground(44f, -15f) + 0.1f, -15f), new Vector2(6f, 4f));
            L2Fire.Smoulder(parent, "FieldSmoulder_E", new Vector3(82f, Ground(82f, -13f) + 0.1f, -13f), new Vector2(7f, 4f), 20f);
            foreach (var (x, z, yaw, pose) in new[] { (66.6f, -13.8f, 40f, "Prone"), (52.4f, -12.6f, 200f, "Supine"), (48.5f, -19.5f, 120f, "Curled") })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(L4Assets.Skeletons + pose + ".prefab");
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.SetPositionAndRotation(new Vector3(x, Ground(x, z), z), Quaternion.Euler(0f, yaw, 0f));
                    n++;
                }
            }

            return n;
        }
    }
}
