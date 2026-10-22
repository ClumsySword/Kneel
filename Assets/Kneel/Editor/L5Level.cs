using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Kneel.Lighting.EditorTools;

namespace Kneel.EditorTools
{
    // Dresses the L5 greybox (L5Greybox: the brief's plan, its colliders and metrics) as the inside of the castle
    // (owner, 2026-10-22, with concept art: "This should be INSIDE the castle... Think Diablo 4 like final boss. The
    // lighting in this level should purely be cast by torches along walls and pillars and chandeliers... Make it feel
    // AAA and premium"). L4's castle stone, coursed masonry and flagstones, under vaults; no sun and no sky, only iron
    // torch sconces on every pillar and wall, ring chandeliers on chains, braziers and candles, in the dark beyond.
    //
    // The top-down cameras look down through the roof: every vault and ceiling is one-sided, its faces turned to the
    // room, so it is there from below (the reveal, the cinematics) and gone from above. Build regenerates the whole
    // scene: the greybox first, then this pass hides its boxes (their colliders stay, so collision and Check Metrics
    // are the greybox's) and lays the art over them. Enemies are stand-ins, as in L4; their AI comes later.
    public static class L5Level
    {
        public const string MaterialsPath = "Assets/Kneel/Materials/L5";
        public const string MeshesPath = "Assets/Kneel/Meshes/L5";
        private const string TexturesPath = MaterialsPath + "/Textures";
        private const string VolumesPath = "Assets/Kneel/Lighting/L5/Volumes";
        private const string Skeletons = "Assets/Kneel/Prefabs/L2/Skeletons/L2_Skeleton_";
        private const string L4Prefabs = "Assets/Kneel/Prefabs/L4/";
        private const float Hall = L5Greybox.Hall, Landing = L5Greybox.Landing;

        // Heights of the shell. The follow camera rides 8.7 m over the player, so every chandelier and every
        // vault springs above that; the fixed arena camera sees through the one-sided vaults.
        private const float NaveTop = 11f, NaveRise = 8.6f;
        private const float StairTop = 16f, StairRise = 22f;
        private const float HallTop = Hall + 16f, Capital = Hall + 10f, ArcadeTop = Hall + 15.5f, AisleCeiling = Hall + 13f, HallRise = 16f;

        private static Material Stone => L4Build.Mat("L4_Knights_Stone");
        private static Material Soot => L4Build.Mat("L4_Knights_Soot");
        private static Material Iron => L4Build.Mat("L4_Iron");
        private static Material Bronze => L4Build.Mat("L4_Knights_Bronze");
        private static Material Basalt => L4Build.Mat("L4_Basalt");
        private static Material Blocks => L4Kit.Stone;
        private static Material Carpet => Flat("L5_Carpet", "#4A151A", 0.04f);
        private static Material Tapestry => Flat("L5_TapestryBurnt", "#2E0F11", 0.05f);
        private static Material Marble => Flat("L5_KingMarble", "#77726B", 0.25f);
        private static Material Wax => Flat("L5_Wax", "#A89D86", 0.25f);
        private static Material Parchment => Flat("L5_Parchment", "#7A6C55", 0.1f);
        private static Material WindowDark => Flat("L5_WindowDark", "#060505", 0.3f);

        private static readonly Color TorchColour = new Color(1f, 0.54f, 0.24f);
        private static readonly Color ChandelierColour = new Color(1f, 0.7f, 0.43f);

        private static System.Random rng;

        // Vaults and ceilings are on their own layer: the cinematic cameras draw it, the gameplay cameras (the follow
        // camera, the fixed arena frame) leave it out, so a roof never stands between them and the fight.
        public const string CeilingLayer = "Ceiling";

        [MenuItem("Kneel/L5/Build Level (replaces the scene)")]
        public static void BuildMenu()
        {
            if (EditorUtility.DisplayDialog("Rebuild L5_ThroneRoom", "This replaces the level scene with a freshly generated one. Hand edits to the scene are lost.", "Rebuild", "Cancel"))
            {
                Debug.Log("[L5] " + Build());
            }
        }

        [MenuItem("Kneel/L5/Look-dev Shots")]
        public static void ShotsMenu()
        {
            Debug.Log("[L5] " + Shots());
        }

        public static string Build()
        {
            var log = new List<string> { L5Greybox.Build() };
            var root = GameObject.Find(L5Greybox.RootName).transform;
            rng = new System.Random(5);
            L2Build.EnsureFolder(TexturesPath);
            L2Build.EnsureFolder(MeshesPath + "/Floors");
            L2Build.EnsureFolder(MeshesPath + "/Shell");
            HideGreybox(root);

            var art = Group(root, "Art");
            log.Add(Floors(Group(art, "Floors")));
            log.Add(Walls(Group(art, "Walls"), root));
            Nave(Group(art, "Nave"), root);
            Chapel(Group(art, "Chapel"));
            Alcove(Group(art, "Alcove"));
            Stair(Group(art, "Stair"), root);
            Portal(Group(art, "OuterDoors"), L5Greybox.P(54f, 268.6f, 0f), NaveTop, 801, false, new[] { 1f });
            Portal(Group(art, "GrandDoors"), L5Greybox.P(54f, 407.1f, Hall), 13f, 811, true, new[] { -1f, 1f });
            ThroneHall(Group(art, "Hall"));
            Dais(Group(art, "Dais"), root);
            Phases(root);
            Figures(Group(art, "Figures"), root);
            Atmosphere(Group(art, "Atmosphere"));

            foreach (Transform area in art)
            {
                L4Build.BakeLooseMeshes(area.gameObject, "L5_" + area.name, MeshesPath + "/Kit");
            }

            foreach (var phase in new[] { "Phase1", "Phase2" })
            {
                var phaseArt = root.Find(phase + "/Art");
                if (phaseArt != null)
                {
                    L4Build.BakeLooseMeshes(phaseArt.gameObject, "L5_" + phase, MeshesPath + "/Kit");
                }
            }

            log.Add(Lighting(root));
            L5Greybox.ShowPhase(1);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            AssetDatabase.SaveAssets();
            return string.Join(" | ", log);
        }

        // The greybox stays as the level's collision and metrics: its boxes stop rendering, their colliders stay.
        private static void HideGreybox(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = false;
            }

            var sun = root.Find("Sun (dusk)");
            if (sun != null)
            {
                Object.DestroyImmediate(sun.gameObject);
            }
        }

        // ---------------------------------------------------------------- Floors

        private static readonly (string id, float x0, float x1, float y0, float y1, float h, bool ash)[] FloorRects =
        {
            ("Apron", 45f, 63f, 256f, 268f, 0f, false),
            ("Nave", 46.5f, 61.5f, 268f, 358.5f, 0f, true),
            ("D1", 31.5f, 46.5f, 303f, 318f, 0f, false),
            ("D2", 61.5f, 70.5f, 333f, 339f, 0f, false),
            ("Foot", 46.5f, 61.5f, 358.5f, 360f, 0f, true),
            ("MasonryW", 34.5f, 46.5f, 360f, 372f, Landing, false),
            ("MasonryE", 61.5f, 73.5f, 360f, 372f, Landing, false),
            ("Landing", 34.5f, 73.5f, 372f, 384f, Landing, false),
            ("Pier", 46.5f, 61.5f, 384f, 396f, Hall, false),
            ("Terrace", 34.5f, 73.5f, 396f, 406.5f, Hall, false),
            ("Threshold", 49.5f, 58.5f, 406.5f, 408f, Hall, false),
            ("Hall", 33f, 75f, 408f, 466.5f, Hall, true),
        };

        private static string Floors(Transform g)
        {
            foreach (var f in FloorRects)
            {
                var rect = Rect.MinMaxRect(f.x0, f.y0, f.x1, f.y1);
                var go = L3Build.MeshChild(g, f.id, Slab("L5_Floor_" + f.id, rect.width, rect.height), L5Greybox.P(rect.center.x, rect.center.y, f.h), FloorMaterial(f.id, rect, f.ash));
                go.layer = L3Build.Layer("Ground");
            }

            return FloorRects.Length + " floors";
        }

        // A slab under a rectangle, its top at y 0, UV0 spanning the rectangle (the ground shader's layout); no east or
        // west faces (the ground shader returns NaN on faces due east or west, L4Level.Slab).
        private static Mesh Slab(string name, float width, float depth)
        {
            float hx = width * 0.5f, hz = depth * 0.5f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    uvs.Add(new Vector2((v.x + hx) / width, (v.z + hz) / depth));
                }

                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            const float y0 = -0.5f;
            Quad(new Vector3(-hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(hx, 0f, -hz));
            Quad(new Vector3(-hx, y0, -hz), new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, y0, -hz));
            Quad(new Vector3(hx, y0, hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz), new Vector3(-hx, y0, hz));
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, MeshesPath + "/Floors");
        }

        // The floor's own copy of L4_Ground: L4's dressed flags everywhere but the wall feet and worn patches, where
        // earth shows. In the nave, ash and dust drift against the walls and the scullery's drag marks run toward the
        // doors; in the hall, soot round the braziers and a cleaner processional path between its kerbs.
        private static Material FloorMaterial(string id, Rect r, bool ash)
        {
            string path = MaterialsPath + "/L5_Ground_" + id + ".mat";
            var source = L4Build.Mat("L4_Ground");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.CopyPropertiesFromMaterial(source);
            }

            int w = Mathf.Clamp(Mathf.RoundToInt(r.width * 12f), 64, 1024), h = Mathf.Clamp(Mathf.RoundToInt(r.height * 12f), 64, 1024);
            var mask = new Color[w * h];
            var layout = new Color[w * h];
            var stone = new Color(0.43f, 0.415f, 0.39f);
            var earth = new Color(0.27f, 0.245f, 0.22f);
            var ashTone = new Color(0.46f, 0.445f, 0.42f);
            var soot = new Color(0.15f, 0.14f, 0.13f);
            int seed = Mathf.Abs(id.GetHashCode()) % 997;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(r.xMin + (x + 0.5f) / w * r.width, r.yMin + (y + 0.5f) / h * r.height);
                    float ragged = (Mathf.PerlinNoise(p.x * 0.7f + seed, p.y * 0.7f) - 0.5f) * 1.2f + (Mathf.PerlinNoise(p.x * 2.3f, p.y * 2.3f + seed) - 0.5f) * 0.5f;
                    float edge = Mathf.Min(Mathf.Min(p.x - r.xMin, r.xMax - p.x), Mathf.Min(p.y - r.yMin, r.yMax - p.y));
                    float pave = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1.6f, edge + ragged));
                    float worn = Mathf.PerlinNoise(p.x * 0.14f + 40f + seed, p.y * 0.14f + 7f);
                    pave *= 1f - 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.8f, worn));
                    var c = Color.Lerp(earth, stone, pave);
                    if (ash)
                    {
                        float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.6f, 0.2f, edge + ragged * 0.6f));
                        float lay = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.78f, Mathf.PerlinNoise(p.x * 0.3f + seed, p.y * 0.3f)));
                        c = Color.Lerp(c, ashTone, Mathf.Max(drift * 0.55f, lay * 0.3f));
                    }

                    if (id == "Nave")
                    {
                        // Drag marks: furrows in the dust from the files toward the doors, ending a body-length short.
                        foreach (float fx in new[] { 48.4f, 50.1f, 58.2f, 59.6f })
                        {
                            float along = Mathf.InverseLerp(285f, 272.5f, p.y);
                            float wobble = Mathf.Sin(p.y * 0.9f + fx) * 0.12f;
                            if (along > 0f && along < 1f && Mathf.Abs(p.x - fx - wobble) < 0.22f)
                            {
                                c = Color.Lerp(c, soot, 0.55f * (1f - along * 0.6f));
                            }
                        }
                    }

                    if (id == "Hall")
                    {
                        foreach (var b in L5Greybox.Braziers)
                        {
                            float d = (p - b).magnitude + ragged * 0.6f;
                            c = Color.Lerp(c, soot, 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.2f, 1.2f, d)));
                        }

                        // The processional path between its kerbs: swept, the flags a shade cooler and darker.
                        if (p.x > 51.2f && p.x < 56.8f)
                        {
                            c = Color.Lerp(c, stone * 0.82f, 0.7f);
                        }
                    }

                    mask[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(pave));
                    c.a = 0f;
                    layout[y * w + x] = c;
                }
            }

            mat.SetTexture("_PaveMask", SaveTexture("L5_Ground_" + id + "_Mask", mask, w, h, true));
            mat.SetTexture("_BaseMap", SaveTexture("L5_Ground_" + id + "_Layout", layout, w, h, false));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Texture2D SaveTexture(string name, Color[] pixels, int w, int h, bool linear)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = TexturesPath + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = !linear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- Walls

        // Every greybox wall is clad in the pack's castle wall, laid in rows about 4.5 m high so the stone keeps L4's
        // scale, and raised to meet its vault: the nave's walls to 11 m, the stair hall's to 16 m, the throne hall's to
        // 16 m above its floor, from the ground. The apse is closed (no breach: this is the inside of the castle).
        private static string Walls(Transform g, Transform root)
        {
            int pieces = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.IsChildOf(g) || !(t.name.Contains("Wall") || t.name.StartsWith("Apse_")) || t.name.StartsWith("OuterDoorWall") || t.GetComponent<MeshRenderer>() == null)
                {
                    continue;
                }

                var size = t.lossyScale;
                var c = t.position;
                float x0 = c.x - size.x * 0.5f, x1 = c.x + size.x * 0.5f, y0 = c.z - size.z * 0.5f, y1 = c.z + size.z * 0.5f;
                float h0 = c.y - size.y * 0.5f, h1 = c.y + size.y * 0.5f;
                string area = t.parent.name;
                if (area == "Nave" && t.name.StartsWith("Wall_"))
                {
                    h1 = NaveTop;
                }
                else if (area == "ImperialStair")
                {
                    h1 = StairTop;
                }
                else if (area == "ThroneHall")
                {
                    h0 = 0f;
                    h1 = HallTop;
                }

                // The grand doors' portal fills the door wall between x 48 and 60.
                if (t.name == "DoorWall_W")
                {
                    x1 = 48f;
                }
                else if (t.name == "DoorWall_E")
                {
                    x0 = 60f;
                }

                pieces += Clad(g, x0, x1, y0, y1, h0, h1);
            }

            // Above the grand doors' portal; the closed apse; above the nave's mouth into the stair hall; either side of
            // the outer doors' portal.
            pieces += Clad(g, 48f, 60f, 406.5f, 408f, Hall + 13f, HallTop);
            pieces += Clad(g, 48f, 60f, 472.5f, 474f, 0f, HallTop);
            pieces += Clad(g, 46.5f, 61.5f, 358.5f, 360f, NaveTop, StairTop);
            pieces += Clad(g, 45f, 48f, 268f, 269.5f, 0f, NaveTop);
            pieces += Clad(g, 60f, 63f, 268f, 269.5f, 0f, NaveTop);
            Obstacle(root, new Vector3(54f, Hall, 473.25f), new Vector3(12f, 12f, 1.5f));
            return pieces + " wall pieces";
        }

        private static int Clad(Transform g, float x0, float x1, float y0, float y1, float h0, float h1)
        {
            bool alongZ = (y1 - y0) > (x1 - x0);
            float length = alongZ ? y1 - y0 : x1 - x0;
            float thickness = alongZ ? x1 - x0 : y1 - y0;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 5.4f));
            int rows = Mathf.Max(1, Mathf.RoundToInt((h1 - h0) / 4.6f));
            float piece = length / n, row = (h1 - h0) / rows;
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < rows; k++)
                {
                    var centre = alongZ ? new Vector2((x0 + x1) * 0.5f, y0 + piece * (i + 0.5f)) : new Vector2(x0 + piece * (i + 0.5f), (y0 + y1) * 0.5f);
                    var size = alongZ ? new Vector3(thickness, row, piece) : new Vector3(piece, row, thickness);
                    L3Build.Fit(g, "K:SM_Bld_Castle_Wall_01", alongZ ? 90 : 0, size, centre, h0 + row * k, Stone);
                }
            }

            return n * rows;
        }

        // ---------------------------------------------------------------- The Long Nave

        private static readonly float[] NavePilasters = { 274.5f, 283.5f, 292.5f, 301.5f, 310.5f, 319.5f, 328.5f, 337.5f, 346.5f, 355.5f };

        private static void Nave(Transform g, Transform root)
        {
            // Pilasters up both walls carrying the vault's ribs, a torch on each; blind lancets between them on the west,
            // burnt tapestries on the east (none across D1's or D2's door).
            foreach (float y in NavePilasters)
            {
                if (y < 306f || y > 315f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, NaveTop, 1.1f), new Vector2(46.8f, y), 0f, Stone);
                    Sconce(g, new Vector3(47.1f, 0f, y), Vector3.right, 3.5f);
                }

                if (y < 333f || y > 339f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, NaveTop, 1.1f), new Vector2(61.2f, y), 0f, Stone);
                    Sconce(g, new Vector3(60.9f, 0f, y), Vector3.left, 3.5f);
                }
            }

            for (float y = 279f; y < 356f; y += 9f)
            {
                if (y < 305f || y > 316f)
                {
                    BlindLancet(g, new Vector3(46.52f, 6.4f, y), -90f, 1.3f, 4.2f);
                }

                if (y < 333f || y > 339f)
                {
                    var banner = L3Build.Part(g, rng.NextDouble() < 0.5 ? "K:SM_Prop_Banner_02" : "K:SM_Prop_Banner_03", new Vector3(61.4f, 8.6f, y), new Vector3(0f, 90f, ((float)rng.NextDouble() - 0.5f) * 4f), Vector3.one * 1.6f, Tapestry);
                    banner.name = "Tapestry";
                }
            }

            Vault(g, "NaveVault", 46.5f, 61.5f, 268f, 358.5f, NaveTop, NaveRise, NavePilasters, true, true, 7101);
            foreach (float y in new[] { 281f, 293f, 305f, 317f, 329f, 341f, 353f })
            {
                Chandelier(g, new Vector3(54f, 9.8f, y), 1.4f, 10, VaultHeight(46.5f, 61.5f, NaveTop, NaveRise, 54f), 0f, false);
            }

            // The kneeling household: two files a side, slumped toward the stair. The greybox kneelers are their
            // colliders; each figure keeps its collider in step with its jitter, and kneelers a pilaster stands on go.
            var files = root.Find("Nave/Kneelers");
            var figures = Group(g, "Courtiers");
            foreach (Transform k in files)
            {
                var at = k.position;
                bool outer = Mathf.Abs(at.x - 54f) > 6f;
                bool underPilaster = outer && System.Array.Exists(NavePilasters, y => Mathf.Abs(y - at.z) < 0.85f);
                if (underPilaster)
                {
                    Object.DestroyImmediate(k.gameObject.GetComponent<Collider>());
                    continue;
                }

                var jitter = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.3f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.3f);
                k.position = at + jitter;
                double roll = rng.NextDouble();
                string kind = roll < 0.62 ? "Kneeling" : roll < 0.84 ? "Slumped" : roll < 0.95 ? "Prone" : "Remains";
                float yaw = (float)(rng.NextDouble() - 0.5) * 26f + (kind == "Slumped" ? 0f : 180f);
                Prefab(Skeletons + kind, figures, new Vector3(at.x + jitter.x, 0f, at.z + jitter.z), yaw);
            }

            // The front ranks were the court: their books lie where they dropped them.
            for (int i = 0; i < 14; i++)
            {
                float x = rng.NextDouble() < 0.5 ? 46.9f + (float)rng.NextDouble() * 2.8f : 58.3f + (float)rng.NextDouble() * 2.8f;
                float y = 336f + (float)rng.NextDouble() * 20f;
                var book = L3Build.Seat(g, "A:SM_Prop_Book_0" + (1 + rng.Next(3)), new Vector3(0f, rng.Next(360), 0f), 1f, new Vector2(x, y), 0f, L4Build.Mat("L4_Adventure_Soot"));
                book.name = "Book";
            }

            // The herald at the head of the files with his roll.
            Prefab(Skeletons + "Prone", g, new Vector3(54.6f, 0f, 354.6f), 170f);
            var scroll = L3Build.MeshChild(g, "Roll", L2PolyKit.Tread(new Vector3(0.75f, 0.06f, 0.42f), 9101), new Vector3(53.2f, 0.03f, 354.1f), Parchment);
            scroll.transform.rotation = Quaternion.Euler(0f, 24f, 0f);

            Candles(g, new Vector3(50f, 0f, 356.4f), 5, 1);
            Candles(g, new Vector3(58.2f, 0f, 356.1f), 5, 1);
            Candles(g, new Vector3(48.2f, 0f, 309.9f), 3, 1);
            Runner(g, Rect.MinMaxRect(51.5f, 270f, 56.5f, 358.4f), 0f, true, 911);
        }

        // ---------------------------------------------------------------- D1, the Master's private chapel

        private static void Chapel(Transform g)
        {
            var altar = L3Build.MeshChild(g, "Altar", L2PolyKit.Tread(new Vector3(2.4f, 1.1f, 3f), 9201), new Vector3(33.2f, 0.55f, 310.5f), Blocks);
            altar.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            // The socket where the old stone sat, cracked and empty.
            foreach (float s in new[] { -1f, 1f })
            {
                var shard = L3Build.MeshChild(g, "Socket", L2PolyKit.Tread(new Vector3(0.5f, 0.35f, 0.9f), 9202 + (int)s), new Vector3(33.2f, 1.25f, 310.5f + s * 0.32f), Basalt);
                shard.transform.rotation = Quaternion.Euler(s * 9f, 90f, s * 6f);
            }

            Candles(g, new Vector3(33.4f, 1.1f, 312f), 4, 2);
            Candles(g, new Vector3(33.4f, 1.1f, 309f), 4, 1);
            L4Citadel.HangBanner(g, new Vector3(31.6f, 5.4f, 310.5f), -90f, 1.3f, 2);
            Sconce(g, new Vector3(37f, 0f, 317.95f), Vector3.back, 3.2f);
            Sconce(g, new Vector3(37f, 0f, 303.05f), Vector3.forward, 3.2f);
            Ceiling(g, "ChapelCeiling", 31.5f, 45f, 303f, 318f, 6f, 7201);

            // The prayer stool, worn hollow at the knees, the confessor's book on it.
            var stool = Group(g, "PrayerStool");
            stool.position = new Vector3(36.4f, 0f, 310.5f);
            L3Build.MeshChild(stool, "Top", L2PolyKit.Plank(new Vector3(0.5f, 0.06f, 1.1f), 9211), new Vector3(0f, 0.42f, 0f), L2PolyKit.CharredWood);
            foreach (float s in new[] { -1f, 1f })
            {
                L3Build.MeshChild(stool, "Leg", L2PolyKit.Plank(new Vector3(0.45f, 0.4f, 0.06f), 9212 + (int)s), new Vector3(0f, 0.2f, s * 0.48f), L2PolyKit.CharredWood);
            }

            Prefab(L4Prefabs + "Markers/L4_Pickup_Lore", g, new Vector3(36.4f, 0.45f, 310.5f), 90f);
            Prefab(Skeletons + "Slumped", g, new Vector3(43.6f, 0f, 316.9f), 200f);
            Fallen(g, new Vector3(41.5f, 0f, 304f), 3);
        }

        // ---------------------------------------------------------------- D2, the hidden alcove

        private static void Alcove(Transform g)
        {
            // The tapestry over the doorway, and the scorched handprint on the jamb beside it (the game's one tell).
            L3Build.Fit(g, "K:SM_Prop_Banner_01", 90, new Vector3(0.15f, 4.3f, 3.3f), new Vector2(61.58f, 336f), 0.25f, Tapestry);
            Prefab(L4Prefabs + "Markers/L4_SecretTell", g, new Vector3(61.42f, 0f, 338.3f), 90f);

            // The cupbearer: sitting, not kneeling, against the wall; empty cups round him; one guttering candle.
            Prefab(Skeletons + "Slumped", g, new Vector3(69.7f, 0f, 336.4f), -90f);
            for (int i = 0; i < 4; i++)
            {
                var cup = L3Build.Seat(g, "P:SM_Icon_Cup_0" + (1 + i % 3), new Vector3(i == 1 ? 90f : 0f, rng.Next(360), 0f), 0.22f, new Vector2(68.2f + (float)rng.NextDouble() * 1.4f, 334.2f + i * 1.1f), 0f, Bronze);
                cup.name = "Cup";
            }

            Prefab(L4Prefabs + "Markers/L4_Pickup_Consumable", g, new Vector3(66f, 0f, 335f), 0f);
            Candles(g, new Vector3(64.2f, 0f, 338.2f), 2, 1);
            Ceiling(g, "AlcoveCeiling", 63f, 70.5f, 333f, 339f, 6f, 7202);
        }

        // ---------------------------------------------------------------- The imperial stair

        private static float StairFloor(float y) => y < 384f ? Landing : y < 396f ? Landing + (y - 384f) * 0.25f : Hall;

        private static void Stair(Transform g, Transform root)
        {
            // The imperial stair: one broad flight up the middle to the half-landing, where the old King kneels in
            // stone; two flights from there, one each side of him, up to the doors' terrace. A crimson runner with a
            // gold edge climbs every step of the way.
            Steps(g, "Flight_Centre", 46.5f, 61.5f, 360f, 0f, 3001);
            Steps(g, "Flight_W", 34.5f, 46.5f, 384f, Landing, 3002);
            Steps(g, "Flight_E", 61.5f, 73.5f, 384f, Landing, 3003);
            StairRunner(g, 50.5f, 57.5f, 360f, 0f, 3101);
            StairRunner(g, 37.5f, 43.5f, 384f, Landing, 3102);
            StairRunner(g, 64.5f, 70.5f, 384f, Landing, 3103);
            Runner(g, Rect.MinMaxRect(50.5f, 372f, 57.5f, 375.5f), Landing, false, 3111);
            Runner(g, Rect.MinMaxRect(37.5f, 375.5f, 70.5f, 379.2f), Landing, false, 3112);
            Runner(g, Rect.MinMaxRect(37.5f, 379.2f, 43.5f, 384f), Landing, false, 3113);
            Runner(g, Rect.MinMaxRect(64.5f, 379.2f, 70.5f, 384f), Landing, false, 3114);
            Runner(g, Rect.MinMaxRect(37.5f, 396f, 70.5f, 399.6f), Hall, false, 3115);
            Runner(g, Rect.MinMaxRect(50.5f, 399.6f, 57.5f, 406.5f), Hall, false, 3116);

            // Cheeks: the masonry beside the central flight and the pier between the side flights, in courses.
            var r = new System.Random(3010);
            L4Kit.Courses(g, new Vector3(46.5f, 0f, 360f), Vector3.forward, Vector3.right, 12f, Landing, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(61.5f, 0f, 372f), Vector3.back, Vector3.left, 12f, Landing, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(46.5f, Landing, 384f), Vector3.right, Vector3.back, 15f, Hall - Landing, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(46.5f, Landing, 396f), Vector3.back, Vector3.left, 12f, Hall - Landing, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(61.5f, Landing, 384f), Vector3.forward, Vector3.right, 12f, Hall - Landing, r, 0f, 0.6f, 2f);

            Balustrade(g, new Vector3(46.35f, Landing, 360.2f), new Vector3(46.35f, Landing, 371.8f), 3020);
            Balustrade(g, new Vector3(61.65f, Landing, 360.2f), new Vector3(61.65f, Landing, 371.8f), 3021);
            Balustrade(g, new Vector3(46.7f, Hall, 384.15f), new Vector3(61.3f, Hall, 384.15f), 3022);

            // Newel posts topped with torches at the foot and head of every flight.
            foreach (var (x, y) in new[] { (46.95f, 360.45f), (61.05f, 360.45f), (34.95f, 384.45f), (46.05f, 384.45f), (61.95f, 384.45f), (73.05f, 384.45f), (34.95f, 395.6f), (73.05f, 395.6f) })
            {
                float floor = y < 361f ? 0f : y < 390f ? Landing : Hall;
                StandingTorch(g, root, new Vector3(x, floor, y));
            }

            // The old King's statue, taken off at the knee and set back down, kneeling to the doors. The greybox box
            // stood inside the pier; this one gets its own collider on the landing.
            var statue = Group(g, "KingStatue");
            statue.position = new Vector3(54f, Landing, 382f);
            L3Build.Seat(statue, "K:SM_Prop_Statue_Base_01", new Vector3(0f, 180f, 0f), 1.9f, Vector2.zero, 0f, Stone);
            var king = L3Build.Seat(statue, "K:SM_Prop_Statue_01", new Vector3(0f, 180f, 0f), 1.35f, Vector2.zero, 0.2f, Marble);
            king.transform.localPosition += Vector3.down * 1.6f;
            Fallen(statue, new Vector3(0f, 0f, -2.4f), 5, 1.6f);
            Candles(g, new Vector3(52.4f, Landing, 380f), 5, 1);
            Candles(g, new Vector3(55.8f, Landing, 380.1f), 5, 1);
            Obstacle(root, statue.position, new Vector3(3.6f, 4.5f, 3.6f));
            var greybox = root.Find("ImperialStair/KingStatue_Kneeling");
            if (greybox != null)
            {
                Object.DestroyImmediate(greybox.gameObject);
            }

            // Earlier wardens along the plinth, each a little further gone.
            Prefab(L4Prefabs + "Figures/L4_Kneeler_Slumped_Knight", g, new Vector3(50.6f, Landing, 381.2f), 15f);
            Prefab(L4Prefabs + "Figures/L4_Kneeler_Slumped_Soldier", g, new Vector3(57.4f, Landing, 381.6f), -20f);
            Prefab(Skeletons + "Remains", g, new Vector3(49.2f, Landing, 383.2f), 40f);

            // Torches and banners down the stair hall's walls, a chandelier over the landing and one over the terrace,
            // and the vault over it all.
            foreach (float y in new[] { 364f, 376f, 389f, 401f })
            {
                Sconce(g, new Vector3(34.5f, StairFloor(y), y), Vector3.right, 3.5f);
                Sconce(g, new Vector3(73.5f, StairFloor(y), y), Vector3.left, 3.5f);
            }

            foreach (float y in new[] { 370f, 383f, 395f })
            {
                L4Citadel.HangBanner(g, new Vector3(34.6f, 14f, y), -90f, 2.6f, (int)y);
                L4Citadel.HangBanner(g, new Vector3(73.4f, 14f, y), 90f, 2.6f, (int)y + 1);
            }

            Sconce(g, new Vector3(49.2f, Landing, 383.95f), Vector3.back, 2.2f);
            Sconce(g, new Vector3(58.8f, Landing, 383.95f), Vector3.back, 2.2f);
            float crown = VaultHeight(34.5f, 73.5f, StairTop, StairRise, 54f);
            Chandelier(g, new Vector3(54f, Landing + 10f, 377.5f), 2.6f, 16, crown, Landing, true);
            Chandelier(g, new Vector3(54f, Hall + 9.6f, 401f), 1.8f, 12, crown, Hall, false);
            Vault(g, "StairVault", 34.5f, 73.5f, 358.5f, 406.5f, StairTop, StairRise, new[] { 366f, 378f, 390f, 402f }, true, true, 7301);
        }

        // A flight of 12 steps 1 m deep and 0.25 m high, each a row of worn blocks from the floor below up.
        private static void Steps(Transform g, string name, float x0, float x1, float y0, float baseH, int seed)
        {
            var flight = Group(g, name);
            var r = new System.Random(seed);
            for (int i = 0; i < 12; i++)
            {
                float top = 0.25f * (i + 1);
                float x = x0;
                while (x < x1 - 0.05f)
                {
                    float w = Mathf.Min(1.6f + (float)r.NextDouble() * 1.4f, x1 - x);
                    L3Build.MeshChild(flight, "Step", L2PolyKit.Tread(new Vector3(w - 0.02f, top, 1f), r.Next(100000)), new Vector3(x + w * 0.5f, baseH + top * 0.5f, y0 + i + 0.5f), Blocks);
                    x += w;
                }
            }
        }

        // The runner up a flight: over each tread and down each riser, a gold edge either side.
        private static void StairRunner(Transform g, float x0, float x1, float y0, float baseH, int seed)
        {
            var m = new ShellMesh();
            const float lift = 0.015f, trim = 0.14f;
            for (int i = 0; i < 12; i++)
            {
                float top = baseH + 0.25f * (i + 1) + lift;
                float y = y0 + i;
                foreach (var (a, b, sub) in new[] { (x0, x1, 0), (x0 - trim, x0, 1), (x1, x1 + trim, 1) })
                {
                    m.Quad(sub, new Vector3(a, top, y), new Vector3(a, top, y + 1f), new Vector3(b, top, y + 1f), new Vector3(b, top, y), Vector2.zero, Vector3.up);
                    m.Quad(sub, new Vector3(a, top - 0.25f, y - lift), new Vector3(a, top, y - lift), new Vector3(b, top, y - lift), new Vector3(b, top - 0.25f, y - lift), Vector2.zero, Vector3.back);
                }
            }

            var go = L3Build.MeshChild(g, "StairRunner", m.Save("L5_StairRunner_" + seed, 2), Vector3.zero, Carpet, Bronze);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // A flat runner over a floor rectangle, gold-edged along its long sides; 'burnt' tears it through in places.
        private static void Runner(Transform g, Rect r, float floor, bool burnt, int seed)
        {
            var m = new ShellMesh();
            const float lift = 0.012f, trim = 0.14f;
            bool alongY = r.height >= r.width;
            int stations = Mathf.Max(2, Mathf.RoundToInt((alongY ? r.height : r.width) / 0.25f)) + 1;
            float step = (alongY ? r.height : r.width) / (stations - 1);
            float width = alongY ? r.width : r.height;
            var lo = new float[stations];
            var hi = new float[stations];
            for (int j = 0; j < stations; j++)
            {
                float s = (alongY ? r.yMin : r.xMin) + j * step;
                float eat = 0f;
                if (burnt)
                {
                    float tear = Mathf.PerlinNoise(s * 0.16f + seed, 0.37f) * 0.8f + Mathf.PerlinNoise(s * 0.6f, seed * 0.1f) * 0.2f;
                    eat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 0.84f, tear));
                }

                float fray = 0.02f + Mathf.Abs(Mathf.PerlinNoise(s * 1.1f + seed, 7f) - 0.5f) * (burnt ? 0.18f : 0.04f);
                float bias = Mathf.PerlinNoise(s * 0.5f, seed + 3f);
                float a0 = alongY ? r.xMin : r.yMin, a1 = alongY ? r.xMax : r.yMax;
                lo[j] = a0 + fray + eat * width * 0.62f * bias;
                hi[j] = a1 - fray - eat * width * 0.62f * (1f - bias);
            }

            Vector3 P(float along, float across) => alongY ? new Vector3(across, lift, along) : new Vector3(along, lift, across);
            for (int j = 0; j < stations - 1; j++)
            {
                if (hi[j] - lo[j] < 0.4f || hi[j + 1] - lo[j + 1] < 0.4f)
                {
                    continue;
                }

                float s0 = (alongY ? r.yMin : r.xMin) + j * step, s1 = s0 + step;
                bool torn = burnt && (hi[j] - lo[j] < width - 0.5f || hi[j + 1] - lo[j + 1] < width - 0.5f);
                float t0 = torn ? 0.1f : trim, t1 = torn ? 0.1f : trim;
                int edge = torn ? 2 : 1;
                m.Quad(edge, P(s0, lo[j]), P(s1, lo[j + 1]), P(s1, lo[j + 1] + t1), P(s0, lo[j] + t0), Vector2.zero, Vector3.up);
                m.Quad(edge, P(s0, hi[j] - t0), P(s1, hi[j + 1] - t1), P(s1, hi[j + 1]), P(s0, hi[j]), Vector2.zero, Vector3.up);
                m.Quad(0, P(s0, lo[j] + t0), P(s1, lo[j + 1] + t1), P(s1, hi[j + 1] - t1), P(s0, hi[j] - t0), Vector2.zero, Vector3.up);
            }

            var go = L3Build.MeshChild(g, "Runner", m.Save("L5_Runner_" + seed, 3), new Vector3(0f, floor, 0f), Carpet, Bronze, L2PolyKit.CharredWood);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // Balusters every 0.45 m under a stone rail, from a to b (on the same height).
        private static void Balustrade(Transform g, Vector3 a, Vector3 b, int seed)
        {
            var bal = Group(g, "Balustrade");
            var r = new System.Random(seed);
            var dir = (b - a).normalized;
            float length = (b - a).magnitude;
            var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
            for (float s = 0f; s <= length; s += 0.45f)
            {
                L3Build.MeshChild(bal, "Baluster", L2PolyKit.Tread(new Vector3(0.22f, 0.75f, 0.22f), r.Next(100000)), a + dir * s + Vector3.up * 0.375f, Blocks);
            }

            for (float s = 0f; s < length - 0.05f; s += 2.4f)
            {
                float piece = Mathf.Min(2.4f, length - s);
                var rail = L3Build.MeshChild(bal, "Rail", L2PolyKit.Tread(new Vector3(piece, 0.16f, 0.36f), r.Next(100000)), a + dir * (s + piece * 0.5f) + Vector3.up * 0.83f, Blocks);
                rail.transform.rotation = rot * Quaternion.Euler(0f, 90f, 0f);
            }
        }

        // ---------------------------------------------------------------- The doors

        // A portal in the manner of L4's throne doors: three recessed round-arched orders, the doors standing open,
        // swung back inside against the wall. Inside the castle it has no towers: torches either side of it on each
        // face, and (for the grand doors) great braziers on plinths before it. 'at' is the door line's centre on its
        // floor; the orders step out toward -Z.
        private static void Portal(Transform g, Vector3 at, float wall, int seed, bool braziers, float[] faces)
        {
            var r = new System.Random(seed);
            g.position = at;
            for (int o = 0; o < 3; o++)
            {
                float span = 8.4f + o * 1.2f;
                var ring = L3Build.Child(g, "Order", new Vector3(0f, 0f, o == 0 ? 0f : -0.3f - o * 0.6f)).transform;
                L4Kit.GateArch(ring, span, 0.6f, 6.6f, span * 0.5f, wall, o == 0 ? 1.2f : 0.6f, seed + o);
            }

            foreach (float s in new[] { -1f, 1f })
            {
                L3Build.Fit(g, "K:SM_Bld_Castle_Door_01", 90, new Vector3(0.45f, 6.6f, 4.2f), new Vector2(s * 4.15f, 3.2f), 0f, Soot);
                foreach (float f in faces)
                {
                    Sconce(g, at + new Vector3(s * 7.4f, 0f, f < 0 ? -0.6f : 0.9f), new Vector3(0f, 0f, f), 3.8f);
                }

                if (braziers)
                {
                    var plinth = L3Build.Child(g, "Brazier", new Vector3(s * 5.3f, 0f, -2.6f)).transform;
                    L4Kit.Courses(plinth, new Vector3(-0.6f, 0f, -0.6f), Vector3.right, Vector3.back, 1.2f, 0.9f, r, 0f, 1.2f, 2f);
                    L3Build.Seat(plinth, "K:SM_Prop_Brazier_01", Vector3.zero, 1.5f, Vector2.zero, 0.9f, Iron);
                    L2Fire.Fire(plinth, "Flames", new Vector3(0f, 0.9f + 1.89f * 1.5f - 0.1f, 0f), 0.85f, 14f, 15f, false);
                    var col = L3Build.Child(plinth, "Collider");
                    L3Build.StandingBox(col, new Vector3(1.2f, 3.6f, 1.2f));
                    col.layer = L3Build.Layer("Obstacles");
                }
            }
        }

        // ---------------------------------------------------------------- The throne hall

        private static void ThroneHall(Transform g)
        {
            // The pillars: massive coursed piers on stepped bases, stepped capitals, the whole length of the hall, a torch
            // on the face toward the processional and one toward the aisle. They fade when they hide the player.
            var r = new System.Random(4001);
            var pillars = Group(g, "Pillars");
            var bases = Group(g, "PillarBases");
            foreach (float x in L5Greybox.PillarLines)
            {
                float toward = x < 54f ? 1f : -1f;
                foreach (float y in L5Greybox.PillarRows)
                {
                    var pillar = Group(pillars, "Pillar");
                    Pier(pillar, bases, new Vector3(x, Hall, y), r);
                    L4Build.BakeLooseMeshes(pillar.gameObject, "L5_Pillar_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(y), MeshesPath + "/Kit");
                    L4Build.AddFade(pillar.gameObject);
                    Sconce(g, new Vector3(x + toward * 1.5f, Hall, y), new Vector3(toward, 0f, 0f), 3.6f);
                    Sconce(g, new Vector3(x - toward * 1.5f, Hall, y), new Vector3(-toward, 0f, 0f), 3.6f);
                }
            }

            // Arcades from pillar to pillar, carrying the high walls the vault springs from.
            var arcades = Group(g, "Arcades");
            foreach (float x in L5Greybox.PillarLines)
            {
                var stops = new List<float> { 408f };
                foreach (float y in L5Greybox.PillarRows)
                {
                    stops.Add(y - 1.5f);
                    stops.Add(y + 1.5f);
                }

                stops.Add(472.5f);
                for (int i = 0; i + 1 < stops.Count; i += 2)
                {
                    float span = stops[i + 1] - stops[i];
                    var bay = Group(arcades, "Bay");
                    bay.position = new Vector3(x, Hall, (stops[i] + stops[i + 1]) * 0.5f);
                    bay.rotation = Quaternion.Euler(0f, 90f, 0f);
                    L4Kit.GateArch(bay, span, 0.05f, Capital - Hall, span * 0.5f, ArcadeTop - Hall, 1.2f, 4100 + i + (x < 54f ? 0 : 50));
                    L4Build.BakeLooseMeshes(bay.gameObject, "L5_Arcade_" + Mathf.RoundToInt(x) + "_" + i, MeshesPath + "/Kit");
                    L4Build.AddFade(bay.gameObject);
                }
            }


            // Torches and blind lancets down the long walls between the pillar rows; torches on the end walls.
            foreach (float y in new[] { 420f, 432f, 444f, 456f })
            {
                Sconce(g, new Vector3(33f, Hall, y), Vector3.right, 3.8f);
                Sconce(g, new Vector3(75f, Hall, y), Vector3.left, 3.8f);
                BlindLancet(g, new Vector3(33.02f, Hall + 9f, y), -90f, 1.6f, 5f);
                BlindLancet(g, new Vector3(74.98f, Hall + 9f, y), 90f, 1.6f, 5f);
            }

            foreach (float x in new[] { 42f, 66f })
            {
                Sconce(g, new Vector3(x, Hall, 408f), Vector3.forward, 3.8f);
            }

            // Banners of the master's crimson on the walls behind the pillars.
            foreach (float y in new[] { 426f, 438f, 450f, 462f })
            {
                L4Citadel.HangBanner(g, new Vector3(33.1f, Hall + 11.5f, y), -90f, 2.4f, (int)y);
                L4Citadel.HangBanner(g, new Vector3(74.9f, Hall + 11.5f, y), 90f, 2.4f, (int)y + 1);
            }

            // The roof: the high vault over the processional, the aisles' lower ceilings, three chandeliers.
            Vault(g, "HallVault", 39.6f, 68.4f, 408f, 472.5f, ArcadeTop, HallRise, L5Greybox.PillarRows, true, true, 7401);
            Ceiling(g, "AisleCeiling_W", 33f, 38.4f, 408f, 472.5f, AisleCeiling, 7402);
            Ceiling(g, "AisleCeiling_E", 69.6f, 75f, 408f, 472.5f, AisleCeiling, 7403);
            float crown = VaultHeight(39.6f, 68.4f, ArcadeTop, HallRise, 54f);
            foreach (float y in new[] { 422f, 436f, 450f, 463f })
            {
                Chandelier(g, new Vector3(54f, Hall + 12f, y), 2.3f, 14, crown, Hall, y > 430f);
            }

            // Kerbs along the processional path, from the doors to the dais.
            for (float y = 408.4f; y < 466.2f; y += 1.25f)
            {
                foreach (float x in new[] { 51.05f, 56.95f })
                {
                    L3Build.MeshChild(g, "Kerb", L2PolyKit.Tread(new Vector3(0.28f, 0.1f, 1.2f), r.Next(100000)), new Vector3(x, Hall + 0.05f, y + 0.6f), Blocks);
                }
            }

            // Those who came before the player: remains and dropped blades along the arena's edges, never in the ring.
            var remains = new[] { (36f, 436f, "Remains"), (42.5f, 447f, "Prone"), (35.5f, 455f, "Slumped"), (71.5f, 441f, "Supine"), (65.6f, 459f, "Remains"), (72.5f, 452f, "Curled"), (44f, 464.5f, "Prone"), (64.5f, 434.5f, "Remains") };
            foreach (var (x, y, kind) in remains)
            {
                Prefab(Skeletons + kind, g, new Vector3(x, Hall, y), rng.Next(360));
                var blade = L3Build.Seat(g, "K:SM_Wep_Broadsword_01", new Vector3(90f, rng.Next(360), 0f), 1f, new Vector2(x + 0.8f, y + 0.5f), Hall, Iron);
                blade.name = "Blade";
            }
        }

        // A square pier of coursed blocks between a stepped base and a stepped capital. The base goes to its own group
        // (it does not fade, and stands out no further than the greybox column it dresses, plus a lip).
        private static void Pier(Transform g, Transform bases, Vector3 c, System.Random r)
        {
            const float h = 1.5f;
            L3Build.MeshChild(bases, "Base", L2PolyKit.Tread(new Vector3(3.5f, 0.25f, 3.5f), r.Next(100000)), c + Vector3.up * 0.125f, Blocks);
            L3Build.MeshChild(bases, "Base", L2PolyKit.Tread(new Vector3(3.2f, 0.25f, 3.2f), r.Next(100000)), c + Vector3.up * 0.375f, Blocks);
            var b = c + Vector3.up * 0.5f;
            float shaft = Capital - Hall - 1.25f;
            L4Kit.Courses(g, b + new Vector3(-h, 0f, -h), Vector3.right, Vector3.back, 3f, shaft, r, 0f, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(h, 0f, h), Vector3.left, Vector3.forward, 3f, shaft, r, 0f, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(-h, 0f, h), Vector3.back, Vector3.left, 3f, shaft, r, 0f, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(h, 0f, -h), Vector3.forward, Vector3.right, 3f, shaft, r, 0f, 0.6f, 1.5f);
            float top = 0.5f + shaft;
            L3Build.MeshChild(g, "Capital", L2PolyKit.Tread(new Vector3(3.3f, 0.35f, 3.3f), r.Next(100000)), c + Vector3.up * (top + 0.175f), Blocks);
            L3Build.MeshChild(g, "Capital", L2PolyKit.Tread(new Vector3(3.7f, 0.4f, 3.7f), r.Next(100000)), c + Vector3.up * (top + 0.55f), Blocks);
        }

        // ---------------------------------------------------------------- The dais and the throne

        private static void Dais(Transform g, Transform root)
        {
            var r = new System.Random(5001);
            float y = Hall;
            // Three steps across the front, the top laid in flags.
            foreach (var (x0, x1, front, top) in new[] { (45f, 63f, 466.5f, 0.2f), (45.5f, 62.5f, 467f, 0.4f), (46f, 62f, 467.5f, 0.6f) })
            {
                float x = x0;
                while (x < x1 - 0.05f)
                {
                    float w = Mathf.Min(1.2f + (float)r.NextDouble() * 0.9f, x1 - x);
                    L3Build.MeshChild(g, "Step", L2PolyKit.Tread(new Vector3(w - 0.02f, top, 0.5f), r.Next(100000)), new Vector3(x + w * 0.5f, y + top * 0.5f, front + 0.25f), Blocks);
                    x += w;
                }
            }

            var deck = Group(g, "Deck");
            deck.position = new Vector3(54f, y + 0.6f, 470.25f);
            L4Kit.Flags(deck, new Vector2(16f, 4.5f), 0.6f, 5002);

            // The niche behind the throne: an arch of three orders on the apse wall, the master's great banner in it.
            var niche = Group(g, "Niche");
            niche.position = new Vector3(54f, y + 0.6f, 472.2f);
            for (int o = 0; o < 2; o++)
            {
                var ring = L3Build.Child(niche, "Order", new Vector3(0f, 0f, -0.3f - o * 0.5f)).transform;
                L4Kit.GateArch(ring, 6.4f + o * 1.2f, 0.6f, 7f, 3.2f + o * 0.6f, 12.5f, o == 0 ? 0.6f : 0.5f, 5100 + o);
            }

            L4Citadel.HangBanner(niche, new Vector3(0f, 9.4f, 0.05f), 0f, 3.1f, 0);
            Sconce(g, new Vector3(48.8f, y + 0.6f, 472.5f), Vector3.back, 3.4f);
            Sconce(g, new Vector3(59.2f, y + 0.6f, 472.5f), Vector3.back, 3.4f);

            Throne(g, root, r);

            // The old King, kneeling at the foot of his own dais, his crown fallen before him.
            Prefab(Skeletons + "Kneeling", g, new Vector3(54f, y + 0.2f, 466.75f), 180f);
            var crown = Group(g, "Crown");
            crown.position = new Vector3(54.2f, y + 0.02f, 466.0f);
            crown.rotation = Quaternion.Euler(14f, 30f, 0f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                var spike = L3Build.MeshChild(crown, "Point", L2PolyKit.Tread(new Vector3(0.09f, 0.16f, 0.05f), 5100 + i), new Vector3(Mathf.Cos(a) * 0.16f, 0.08f, Mathf.Sin(a) * 0.16f), Bronze);
                spike.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
            }

            // Hellfire either side of the throne: the master's own fire, blood-red. Candles along the dais.
            foreach (float s in new[] { -1f, 1f })
            {
                var b = Group(g, "Hellfire");
                b.position = new Vector3(54f + s * 4.6f, y + 0.6f, 470.6f);
                L3Build.Seat(b, "K:SM_Prop_Brazier_01", Vector3.zero, 1.35f, Vector2.zero, 0f, Iron);
                L4Citadel.Hellfire(b, "Flames", new Vector3(0f, 1.89f * 1.35f - 0.05f, 0f), 0.8f, 13f, 13f);
                Candles(g, new Vector3(54f + s * 2.7f, y + 0.6f, 468.6f), 7, 1);
                Candles(g, new Vector3(54f + s * 6.6f, y + 0.6f, 468.4f), 6, 1);
            }

            // Where the roof-fall rubble stood in the greybox: stepped plinths either side of the dais, each carrying a
            // black statue of the master's champion, sword lowered, torches at their feet.
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = 54f + s * 16.5f;
                var plinth = Group(g, "ChampionPlinth");
                plinth.position = new Vector3(cx, y, 469.5f);
                for (int k = 0; k < 3; k++)
                {
                    var size = new Vector3(9f - k * 1.2f, 0.6f, 6f - k * 0.8f);
                    L3Build.MeshChild(plinth, "Tier", L2PolyKit.Tread(size, r.Next(100000)), new Vector3(0f, 0.3f + k * 0.6f, 0f), Blocks);
                }

                var champion = L3Build.Seat(plinth, "K:SM_Prop_Statue_01", new Vector3(0f, 180f, 0f), 1.7f, Vector2.zero, 1.8f, Basalt);
                champion.name = "Champion";
                StandingTorch(g, root, new Vector3(cx - s * 4.9f, y, 466.1f));
                Candles(g, plinth.position + new Vector3(-2.6f, 1.8f, -1.6f), 4, 0);
                Candles(g, plinth.position + new Vector3(2.6f, 1.8f, -1.6f), 4, 1);
            }
        }

        // The throne: a tall seat of black stone with a pointed back between two spired posts, armrests, a crimson
        // cushion; the Master (stand-in) seated in it. Measured from the seated figure so his feet rest on its step.
        private static void Throne(Transform g, Transform root, System.Random r)
        {
            var (mesh, hipsY) = SeatedMaster();
            const float scale = 1.45f;
            const float step = 0.42f;
            float seatTop = step + (hipsY - mesh.bounds.min.y) * scale - 0.06f;
            var t = Group(g, "Throne");
            t.position = L5Greybox.Throne;
            Block(t, new Vector3(3.6f, step, 3f), new Vector3(0f, 0f, 0.1f), Basalt, r);
            Block(t, new Vector3(2.1f, seatTop - step, 1.5f), new Vector3(0f, step, 0.3f), Basalt, r);
            Block(t, new Vector3(2.5f, 5.2f, 0.55f), new Vector3(0f, step, 1.2f), Basalt, r);
            foreach (float s in new[] { -1f, 1f })
            {
                Block(t, new Vector3(0.42f, 0.7f, 1.6f), new Vector3(s * 1.25f, seatTop - 0.1f, 0.3f), Basalt, r);
                var lean = Block(t, new Vector3(0.6f, 1.8f, 0.5f), new Vector3(s * 0.6f, step + 5.0f, 1.2f), Basalt, r);
                lean.transform.localRotation = Quaternion.Euler(0f, 0f, s * 22f);
                Block(t, new Vector3(0.55f, 6.4f, 0.6f), new Vector3(s * 1.55f, step, 1.2f), Basalt, r);
                L3Build.Seat(t, "K:SM_Bld_Castle_Roof_Spire_01", Vector3.zero, 0.24f, new Vector2(s * 1.55f, 1.2f), step + 6.4f, Basalt);
            }

            Block(t, new Vector3(1.6f, 0.1f, 1.2f), new Vector3(0f, seatTop, 0.25f), L4Citadel.BannerRed, r);
            var master = L3Build.MeshChild(t, "Master (stand-in)", mesh, new Vector3(0f, seatTop - hipsY * scale + 0.06f, 0.1f), AssetDatabase.LoadAssetAtPath<Material>("Assets/SyntyStudios/PolygonKnights/Materials/PolyKnights_Character_Mat_Black.mat"));
            master.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            master.transform.localScale = Vector3.one * scale;
            L4Citadel.Hellfire(master.transform, "PalmEmber", new Vector3(-0.45f, 1.05f, 0.42f), 0.1f, 1.2f, 3f);
            L1LightingShots.SetCharacterLayer(master);
            Obstacle(root, L5Greybox.Throne + new Vector3(0f, 0f, 0.4f), new Vector3(3.6f, 6f, 3f));
        }

        // The PolygonKnights black knight, baked seated: thighs forward, shins down, forearms on the armrests.
        private static (Mesh mesh, float hipsY) SeatedMaster()
        {
            string path = MeshesPath + "/Figures/L5_Seated_Knight_02_Black.asset";
            L2Build.EnsureFolder(MeshesPath + "/Figures");
            var mesh = L1Bakers.BakeStanding("Assets/SyntyStudios/PolygonKnights/Prefabs/Characters/Character_Knight_02_Black.prefab", "Character_Knight_02", SeatedPose, null, out var rig);
            float hips = 0f;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Hips")
                {
                    hips = t.position.y;
                }
            }

            Object.DestroyImmediate(rig);
            mesh.name = "L5_Seated_Knight_02_Black";
            return (SaveMesh(mesh, MeshesPath + "/Figures"), hips);
        }

        private static void SeatedPose(Transform r, float s)
        {
            var l = new Vector3(s, 0f, 0f);
            Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.16f + new Vector3(0f, -0.06f, 1f));
            Aim(r, "UpperLeg_R", "LowerLeg_R", -l * 0.16f + new Vector3(0f, -0.06f, 1f));
            Aim(r, "LowerLeg_L", "Ankle_L", l * 0.05f + new Vector3(0f, -1f, 0.1f));
            Aim(r, "LowerLeg_R", "Ankle_R", -l * 0.05f + new Vector3(0f, -1f, 0.1f));
            Aim(r, "Shoulder_L", "Elbow_L", l * 0.3f + new Vector3(0f, -1f, 0.35f));
            Aim(r, "Elbow_L", "Hand_L", l * 0.05f + new Vector3(0f, -0.15f, 1f));
            Aim(r, "Shoulder_R", "Elbow_R", -l * 0.3f + new Vector3(0f, -1f, 0.35f));
            Aim(r, "Elbow_R", "Hand_R", -l * 0.05f + new Vector3(0f, -0.15f, 1f));
        }

        private static void Aim(Transform r, string bone, string child, Vector3 dir)
        {
            Transform b = null, c = null;
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == bone)
                {
                    b = t;
                }
                else if (t.name == child)
                {
                    c = t;
                }
            }

            if (b != null && c != null)
            {
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }
        }

        // ---------------------------------------------------------------- Phase art

        private static void Phases(Transform root)
        {
            var p1 = Group(root.Find("Phase1"), "Art");
            foreach (var b in L5Greybox.Braziers)
            {
                var brazier = Group(p1, "Brazier_Hazard");
                brazier.position = new Vector3(b.x, Hall, b.y);
                L3Build.Seat(brazier, "K:SM_Prop_Brazier_01", Vector3.zero, 1f, Vector2.zero, 0f, Iron);
                L2Fire.Fire(brazier, "Flames", new Vector3(0f, 1.81f, 0f), 0.6f, 11f, 11f, false);
            }

            // Phase 2: the braziers over in burning pools; stone fallen from the vault where the greybox puts debris.
            var p2 = Group(root.Find("Phase2"), "Art");
            foreach (var b in L5Greybox.Braziers)
            {
                var at = new Vector3(b.x, Hall, b.y);
                var basket = L3Build.Seat(p2, "K:SM_Prop_Brazier_01", new Vector3(90f, rng.Next(360), 0f), 1f, new Vector2(b.x + 0.6f, b.y - 0.3f), Hall, Iron);
                basket.name = "Toppled";
                L2Fire.Fire(p2, "FirePool", at + Vector3.up * 0.05f, 1f, 3f, 9f, true, new Vector3(2.6f, 0f, 2.6f), L2Fire.Shape.Box, 1.2f);
                L2Fire.GroundEmbers(p2, at + Vector3.up * 0.03f, new Vector2(3f, 3f));
            }

            foreach (var (x0, x1, y0, y1) in new[] { (46.5f, 49.5f, 437f, 443f), (58.5f, 61.5f, 437f, 443f), (51f, 57f, 457.5f, 460.5f) })
            {
                bool alongX = x1 - x0 > y1 - y0;
                var c = new Vector3((x0 + x1) * 0.5f, Hall, (y0 + y1) * 0.5f);
                var rib = L3Build.MeshChild(p2, "FallenRib", L2PolyKit.Tread(new Vector3(1.1f, 1f, 5.4f), rng.Next(100000)), c + Vector3.up * 0.5f, Blocks);
                rib.transform.rotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 8f, alongX ? 90f : 0f, ((float)rng.NextDouble() - 0.5f) * 10f);
                Fallen(p2, c, 9, 1.9f);
            }
        }

        // ---------------------------------------------------------------- Figures (stand-ins and markers)

        private static void Figures(Transform g, Transform root)
        {
            Prefab(L4Prefabs + "Markers/L4_Shrine", g, L5Greybox.ShrineC, 0f).name = "Shrine_C_LastShrine";
            Prefab(L4Prefabs + "Markers/L4_Shrine", g, L5Greybox.P(54f, 263.6f, 0f), 0f).name = "Shrine_B";

            // The Royal Guard (stand-in: L4's black-iron knight; AI comes later).
            var guard = Prefab(L4Prefabs + "Enemies/L4_StandIn_IronHusk", g, L5Greybox.P(54f, 377.6f, Landing), 0f);
            guard.name = "RoyalGuard (stand-in)";
            foreach (var marker in guard.GetComponentsInChildren<Kneel.Markers.SpawnMarker>(true))
            {
                Object.DestroyImmediate(marker);
            }

            L1LightingShots.SetCharacterLayer(guard);
            foreach (var pin in new[] { "Markers/Master_OnThrone", "Markers/RoyalGuard_UprightKneeler", "Markers/StandIn_Player", "Markers/StandIn_Master" })
            {
                var t = root.Find(pin);
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                }
            }
        }

        // ---------------------------------------------------------------- Atmosphere

        // Dust hanging in the torchlight down the nave and the stair, embers rising round the throne end.
        private static void Atmosphere(Transform g)
        {
            L4Atmosphere.Ash(g, new Vector3(54f, 7f, 314f), new Vector3(14f, 2f, 90f));
            L4Atmosphere.Ash(g, new Vector3(54f, 12f, 383f), new Vector3(36f, 2f, 44f));
            L4Atmosphere.Ash(g, new Vector3(54f, Hall + 10f, 440f), new Vector3(34f, 2f, 62f));
            L4Atmosphere.Embers(g, new Vector3(54f, Hall + 1.5f, 458f), new Vector3(30f, 6f, 24f));
        }

        // ---------------------------------------------------------------- Lighting

        // Inside the castle there is no sun and no sky. The only light is fire: the torches, the chandeliers, the
        // braziers and the candles. The ambient is a breath above black, so stone between two torches still has its
        // shape; a dark haze swallows the far end of every room; past the walls, nothing. The grade is L4's (ACES, a cool
        // split tone, heavy vignette and grain) with the bloom up so every flame glows, and the saturation kept so fire
        // stays fire. A shadowless fill on the Characters layer keeps the fighters readable, as in L4.
        private static string Lighting(Transform root)
        {
            var lighting = Group(root, "_Lighting");
            RenderSettings.sun = null;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = L1Build.Hex("#2B2E36");
            RenderSettings.ambientEquatorColor = L1Build.Hex("#302A25");
            RenderSettings.ambientGroundColor = L1Build.Hex("#15120F");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = L1Build.Hex("#0B0908");
            RenderSettings.fogDensity = 0.008f;
            DynamicGI.UpdateEnvironment();

            var fillGo = new GameObject("CharacterFill");
            fillGo.transform.SetParent(lighting, false);
            fillGo.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = L1Build.Hex("#C9CCD4");
            fill.intensity = 0.5f;
            fill.shadows = LightShadows.None;
            uint characters = (uint)RenderingLayerMask.GetMask(L1LightingPass.CharactersLayer);
            fill.renderingLayerMask = (int)characters;
            fillGo.AddComponent<UniversalAdditionalLightData>().renderingLayers = characters;

            L2Build.EnsureFolder(VolumesPath);
            string path = VolumesPath + "/L5_Global.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (var component in new List<VolumeComponent>(profile.components))
                {
                    profile.Remove(component.GetType());
                    Object.DestroyImmediate(component, true);
                }
            }

            T Get<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }

            Get<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var adjust = Get<ColorAdjustments>();
            adjust.postExposure.Override(Exposure);
            adjust.contrast.Override(24f);
            adjust.saturation.Override(-12f);
            var split = Get<SplitToning>();
            split.shadows.Override(L1Build.Hex("#22303A"));
            split.highlights.Override(L1Build.Hex("#C9874E"));
            split.balance.Override(-20f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.45f);
            var bloom = Get<Bloom>();
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.25f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(L1Build.Hex("#FFB27A"));
            var grain = Get<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.32f);
            grain.response.Override(0.8f);
            Get<ChromaticAberration>().intensity.Override(0.08f);
            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("GlobalVolume");
            volumeGo.transform.SetParent(lighting, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var player = L1LightingShots.PlayerRoot();
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask &= ~(1 << L3Build.Layer(CeilingLayer));
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null)
                {
                    data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                data.renderPostProcessing = true;
                data.volumeLayerMask = 1;
                data.volumeTrigger = player != null ? player : cam.transform;
                EditorUtility.SetDirty(cam);
            }

            if (player != null)
            {
                L1LightingShots.SetCharacterLayer(player.gameObject);
            }

            int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            return "interior lighting: " + lights + " lights, no sun";
        }

        private const float Exposure = 1.45f;

        // ---------------------------------------------------------------- Torches and chandeliers

        // An iron torch sconce on a wall or pillar face: a backplate, an arm with a brace, a cage basket of bars round a
        // bowl, fire in it and the fire's light. 'wall' is the face's point on the floor, 'outward' the way it faces.
        private static void Sconce(Transform g, Vector3 wall, Vector3 outward, float height, float intensity = 13.5f, float range = 12.5f)
        {
            var s = Group(g, "Sconce");
            s.SetPositionAndRotation(wall + Vector3.up * height, Quaternion.LookRotation(outward));
            int seed = rng.Next(100000);
            Part(s, L2PolyKit.Tread(new Vector3(0.3f, 0.55f, 0.08f), seed), new Vector3(0f, -0.1f, 0.04f), Quaternion.identity);
            Part(s, L2PolyKit.Tread(new Vector3(0.08f, 0.08f, 0.62f), seed + 1), new Vector3(0f, 0f, 0.33f), Quaternion.identity);
            Part(s, L2PolyKit.Tread(new Vector3(0.06f, 0.06f, 0.55f), seed + 2), new Vector3(0f, -0.2f, 0.22f), Quaternion.Euler(-40f, 0f, 0f));
            Basket(s, new Vector3(0f, 0.02f, 0.62f), seed + 3);
            var fire = L2Fire.Fire(s, "Flames", new Vector3(0f, 0.3f, 0.62f), 0.42f, intensity, range, false);
            foreach (var l in fire.GetComponentsInChildren<Light>(true))
            {
                l.color = TorchColour;
            }
        }

        // A torch on a stone newel post (with its own collider), for the foot and head of each flight.
        private static void StandingTorch(Transform g, Transform root, Vector3 at)
        {
            var t = Group(g, "StandingTorch");
            t.position = at;
            int seed = rng.Next(100000);
            L3Build.MeshChild(t, "Post", L2PolyKit.Tread(new Vector3(0.5f, 1.3f, 0.5f), seed), new Vector3(0f, 0.65f, 0f), Blocks);
            L3Build.MeshChild(t, "Cap", L2PolyKit.Tread(new Vector3(0.62f, 0.14f, 0.62f), seed + 1), new Vector3(0f, 1.37f, 0f), Blocks);
            Basket(t, new Vector3(0f, 1.44f, 0f), seed + 2);
            var fire = L2Fire.Fire(t, "Flames", new Vector3(0f, 1.72f, 0f), 0.42f, 12f, 11f, false);
            foreach (var l in fire.GetComponentsInChildren<Light>(true))
            {
                l.color = TorchColour;
            }

            Obstacle(root, at, new Vector3(0.5f, 1.4f, 0.5f));
        }

        // A cage of six iron bars flaring out round a bowl, the torch's basket.
        private static void Basket(Transform parent, Vector3 at, int seed)
        {
            Part(parent, L2PolyKit.Tread(new Vector3(0.26f, 0.07f, 0.26f), seed), at, Quaternion.identity);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var horizontal = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var offset = horizontal * 0.17f + Vector3.up * 0.18f;
                var tilt = Quaternion.AngleAxis(14f, Vector3.Cross(Vector3.up, horizontal));
                Part(parent, L2PolyKit.Tread(new Vector3(0.035f, 0.36f, 0.035f), seed + 1 + i), at + offset, tilt);
            }

            Part(parent, L2PolyKit.Tread(new Vector3(0.42f, 0.03f, 0.04f), seed + 9), at + new Vector3(0f, 0.34f, 0.19f), Quaternion.identity);
            Part(parent, L2PolyKit.Tread(new Vector3(0.42f, 0.03f, 0.04f), seed + 10), at + new Vector3(0f, 0.34f, -0.19f), Quaternion.identity);
        }

        private static void Part(Transform parent, Mesh mesh, Vector3 local, Quaternion rotation)
        {
            var go = L3Build.MeshChild(parent, "Iron", mesh, local, Iron);
            go.transform.localRotation = rotation;
        }

        // A ring chandelier: an iron hoop on spokes round a hub, a smaller hoop under it, candles all round, hung on
        // three chains to a ring and one chain up to the vault at 'ceiling'. One light for the whole thing.
        private static void Chandelier(Transform g, Vector3 at, float radius, int candles, float ceiling, float floor, bool shadows)
        {
            var c = Group(g, "Chandelier");
            c.position = at;
            int seed = rng.Next(100000);
            void Hoop(float rad, float y, int n)
            {
                float seg = 2f * Mathf.PI * rad / n;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) / n * Mathf.PI * 2f;
                    var go = L3Build.MeshChild(c, "Hoop", L2PolyKit.Tread(new Vector3(seg + 0.02f, 0.09f, 0.08f), seed + i), new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad), Iron);
                    go.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                }
            }

            Hoop(radius, 0f, Mathf.Max(12, candles + 4));
            Hoop(radius * 0.55f, -0.35f, 10);
            L3Build.MeshChild(c, "Hub", L2PolyKit.Tread(new Vector3(0.28f, 0.6f, 0.28f), seed + 50), new Vector3(0f, -0.1f, 0f), Iron);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f;
                var spoke = L3Build.MeshChild(c, "Spoke", L2PolyKit.Tread(new Vector3(radius, 0.06f, 0.06f), seed + 60 + i), new Vector3(Mathf.Cos(a) * radius * 0.5f, 0f, Mathf.Sin(a) * radius * 0.5f), Iron);
                spoke.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }

            for (int i = 0; i < candles; i++)
            {
                float a = i / (float)candles * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius);
                L3Build.MeshChild(c, "Candle", L2PolyKit.Tread(new Vector3(0.07f, 0.24f, 0.07f), seed + 100 + i), p + Vector3.up * 0.12f, Wax);
                L2Fire.Candle(c, "Flame", p + Vector3.up * 0.28f, 0.01f, 0.1f);
            }

            // Chains: three from the hoop up to a ring, one from the ring to the vault.
            var ring = new Vector3(0f, 2.4f, 0f);
            for (int k = 0; k < 3; k++)
            {
                float a = k / 3f * Mathf.PI * 2f;
                Chain(c, new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius), ring, seed + 200 + k * 40);
            }

            Chain(c, ring, new Vector3(0f, ceiling - at.y, 0f), seed + 400);

            // The candles' own lights go: one light for the whole chandelier, hung under the hub (inside it, the hub's
            // shadow swallowed it), as bright as its height over the floor needs.
            foreach (var l in c.GetComponentsInChildren<Light>(true))
            {
                // The flicker requires its light, so it goes first.
                foreach (var dependant in l.GetComponents<MonoBehaviour>())
                {
                    Object.DestroyImmediate(dependant);
                }

                Object.DestroyImmediate(l);
            }

            float drop = at.y - floor;
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(c, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ChandelierColour;
            light.intensity = 1.05f * drop * drop;
            light.range = 1.8f * drop + 10f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.85f;
        }

        private static void Chain(Transform parent, Vector3 a, Vector3 b, int seed)
        {
            var dir = b - a;
            float length = dir.magnitude;
            var rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            int links = Mathf.Max(1, Mathf.RoundToInt(length / 0.16f));
            for (int i = 0; i < links; i++)
            {
                var link = L3Build.MeshChild(parent, "Link", L2PolyKit.Tread(new Vector3(0.06f, 0.17f, 0.025f), seed + i), a + dir * ((i + 0.5f) / links), Iron);
                link.transform.localRotation = rot * Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f);
            }
        }

        // ---------------------------------------------------------------- Vaults and ceilings (one-sided)

        // Height of a pointed vault's underside over x, for hanging chandeliers from its crown.
        private static float VaultHeight(float x0, float x1, float spring, float rise, float x)
        {
            float a = (x1 - x0) * 0.5f;
            rise = Mathf.Max(rise, a * 1.1f);
            return spring + rise;
        }

        // A pointed barrel vault over x0..x1, running y0..y1, springing at 'spring', its faces turned down into the room
        // so it is invisible from above. Laid in blocks of the stone palette, staggered course by course; transverse
        // ribs at 'ribs'; optional end gables over the springing line.
        private static void Vault(Transform g, string name, float x0, float x1, float y0, float y1, float spring, float rise, float[] ribs, bool southGable, bool northGable, int seed)
        {
            float a = (x1 - x0) * 0.5f, xc = x0 + a;
            rise = Mathf.Max(rise, a * 1.1f);
            float radius = (a * a + rise * rise) / (2f * a);
            float leftCentre = x0 + radius;
            float end = Mathf.Atan2(rise, xc - leftCentre);
            const int perSide = 14;
            var profile = new List<Vector2>();
            var inward = new List<Vector2>();
            for (int i = 0; i <= perSide; i++)
            {
                float ang = Mathf.Lerp(Mathf.PI, end, i / (float)perSide);
                var p = new Vector2(leftCentre + radius * Mathf.Cos(ang), radius * Mathf.Sin(ang));
                profile.Add(p);
                inward.Add((new Vector2(leftCentre, 0f) - p).normalized);
            }

            for (int i = perSide - 1; i >= 0; i--)
            {
                profile.Add(new Vector2(2f * xc - profile[i].x, profile[i].y));
                inward.Add(new Vector2(-inward[i].x, inward[i].y));
            }

            Vector3 At(Vector2 p, float y) => new Vector3(p.x, spring + p.y, y);
            var m = new ShellMesh();
            var r = new System.Random(seed);
            int[] cells = { L2PolyKit.Side, L2PolyKit.TopDark, L2PolyKit.Top, L2PolyKit.Side, L2PolyKit.End };
            const float stationLength = 0.8f;
            int stations = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / stationLength));
            float sl = (y1 - y0) / stations;
            for (int i = 0; i + 1 < profile.Count; i++)
            {
                var n = new Vector3((inward[i].x + inward[i + 1].x) * 0.5f, (inward[i].y + inward[i + 1].y) * 0.5f, 0f);
                for (int s = 0; s < stations; s++)
                {
                    float ya = y0 + s * sl, yb = ya + sl;
                    int block = (s + (i % 2) * 1) / 2;
                    var uv = L4Kit.Cell(cells[(block * 7 + i * 3 + seed) % cells.Length]);
                    m.Quad(0, At(profile[i], ya), At(profile[i], yb), At(profile[i + 1], yb), At(profile[i + 1], ya), uv, n);
                }
            }

            // Ribs: a band under the vault at each rib line, its underside and both cheeks.
            const float ribWidth = 0.7f, ribDepth = 0.45f;
            var ribUv = L4Kit.Cell(L2PolyKit.TopDark);
            foreach (float ry in ribs)
            {
                float ya = ry - ribWidth * 0.5f, yb = ry + ribWidth * 0.5f;
                for (int i = 0; i + 1 < profile.Count; i++)
                {
                    var pa = profile[i] + inward[i] * ribDepth;
                    var pb = profile[i + 1] + inward[i + 1] * ribDepth;
                    var n = new Vector3(inward[i].x, inward[i].y, 0f);
                    m.Quad(0, At(pa, ya), At(pa, yb), At(pb, yb), At(pb, ya), ribUv, n);
                    m.Quad(0, At(profile[i], ya), At(pa, ya), At(pb, ya), At(profile[i + 1], ya), ribUv, Vector3.back);
                    m.Quad(0, At(profile[i], yb), At(pa, yb), At(pb, yb), At(profile[i + 1], yb), ribUv, Vector3.forward);
                }
            }

            // Gables: the pointed end walls above the springing line.
            var gableUv = L4Kit.Cell(L2PolyKit.Side);
            foreach (var (y, facing, on) in new[] { (y0, Vector3.forward, southGable), (y1, Vector3.back, northGable) })
            {
                if (!on)
                {
                    continue;
                }

                var mid = new Vector3(xc, spring, y);
                for (int i = 0; i + 1 < profile.Count; i++)
                {
                    m.Tri(0, mid, At(profile[i], y), At(profile[i + 1], y), gableUv, facing);
                }
            }

            var go = L3Build.MeshChild(g, name, m.Save(name, 1), Vector3.zero, Blocks);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.layer = L3Build.Layer(CeilingLayer);
        }

        // A flat ceiling at height h over a rectangle, faces down, in palette blocks.
        private static void Ceiling(Transform g, string name, float x0, float x1, float y0, float y1, float h, int seed)
        {
            var m = new ShellMesh();
            int[] cells = { L2PolyKit.Side, L2PolyKit.TopDark, L2PolyKit.End };
            int i = 0;
            for (float x = x0; x < x1 - 0.01f; x += 1.2f)
            {
                float xb = Mathf.Min(x + 1.2f, x1);
                int j = 0;
                for (float y = y0; y < y1 - 0.01f; y += 0.9f)
                {
                    float yb = Mathf.Min(y + 0.9f, y1);
                    m.Quad(0, new Vector3(x, h, y), new Vector3(x, h, yb), new Vector3(xb, h, yb), new Vector3(xb, h, y), L4Kit.Cell(cells[(i * 5 + j * 3 + seed) % cells.Length]), Vector3.down);
                    j++;
                }

                i++;
            }

            var go = L3Build.MeshChild(g, name, m.Save(name, 1), Vector3.zero, Blocks);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.layer = L3Build.Layer(CeilingLayer);
        }

        // A dark blind lancet set into a wall: the pack's window frame round a black pane (no light comes in).
        private static void BlindLancet(Transform g, Vector3 at, float yaw, float width, float height)
        {
            var m = new ShellMesh();
            float hw = width * 0.5f, hh = height * 0.5f;
            m.Quad(0, new Vector3(-hw, -hh, 0f), new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f), new Vector3(hw, -hh, 0f), Vector2.zero, Vector3.back);
            var pane = L3Build.MeshChild(g, "BlindPane", m.Save("L5_BlindPane_" + Mathf.RoundToInt(width * 10) + "_" + Mathf.RoundToInt(height * 10), 1), at, WindowDark);
            pane.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var frame = L3Build.Part(g, "K:SM_Bld_Church_Window_01", at - Vector3.up * height * 0.5f, new Vector3(0f, yaw + 180f, 0f), new Vector3(width / 0.8f * 1.25f, height / 1.5f * 1.1f, 1.6f), Stone);
            frame.name = "WindowFrame";
        }

        // Generated geometry: quads and triangles in sub-meshes, each face turned to the given normal, flat UVs.
        private class ShellMesh
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly Dictionary<int, List<int>> subs = new Dictionary<int, List<int>>();

            private List<int> Sub(int s)
            {
                if (!subs.TryGetValue(s, out var list))
                {
                    subs[s] = list = new List<int>();
                }

                return list;
            }

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 facing)
            {
                var n = Vector3.Cross(b - a, c - a);
                bool flip = Vector3.Dot(n, facing) < 0f;
                int k = verts.Count;
                foreach (var v in new[] { a, b, c, d })
                {
                    verts.Add(v);
                    normals.Add(facing.normalized);
                    uvs.Add(uv);
                }

                Sub(sub).AddRange(flip ? new[] { k, k + 2, k + 1, k, k + 3, k + 2 } : new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 uv, Vector3 facing)
            {
                var n = Vector3.Cross(b - a, c - a);
                bool flip = Vector3.Dot(n, facing) < 0f;
                int k = verts.Count;
                foreach (var v in new[] { a, b, c })
                {
                    verts.Add(v);
                    normals.Add(facing.normalized);
                    uvs.Add(uv);
                }

                Sub(sub).AddRange(flip ? new[] { k, k + 2, k + 1 } : new[] { k, k + 1, k + 2 });
            }

            public Mesh Save(string name, int subMeshes)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = subMeshes;
                for (int s = 0; s < subMeshes; s++)
                {
                    mesh.SetTriangles(Sub(s), s);
                }

                mesh.RecalculateBounds();
                return SaveMesh(mesh, MeshesPath + "/Shell");
            }
        }

        // ---------------------------------------------------------------- Small pieces

        // Fallen blocks round a point (within 'spread' metres).
        private static void Fallen(Transform g, Vector3 at, int count, float spread = 1.5f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = spread * (0.5f + (float)rng.NextDouble() * 0.6f);
                L4Kit.Fallen(g, at + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), rng);
            }
        }

        // A cluster of guttered candle stubs; 'lit' of them burn and carry a small light.
        private static void Candles(Transform g, Vector3 at, int count, int lit)
        {
            for (int i = 0; i < count; i++)
            {
                var p = at + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.9f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.9f);
                float h = 0.08f + (float)rng.NextDouble() * 0.22f;
                var stub = L3Build.MeshChild(g, "Candle", L2PolyKit.Tread(new Vector3(0.07f, h, 0.07f), rng.Next(100000)), Vector3.zero, Wax);
                stub.transform.position = p + Vector3.up * h * 0.5f;
                if (i < lit)
                {
                    var flame = L2Fire.Candle(g, "Flame", Vector3.zero, 2.4f, 5f);
                    flame.position = p + Vector3.up * (h + 0.04f);
                }
            }
        }

        // A collider for a piece of dressing the player must walk round (on Obstacles, so the NavMesh sees it too).
        private static void Obstacle(Transform root, Vector3 baseCentre, Vector3 size)
        {
            var go = new GameObject("DressingCollider");
            go.transform.SetParent(root, false);
            go.transform.position = baseCentre;
            go.layer = L3Build.Layer("Obstacles");
            L3Build.StandingBox(go, size);
        }

        private static GameObject Block(Transform g, Vector3 size, Vector3 baseCentre, Material mat, System.Random r)
        {
            return L3Build.MeshChild(g, "Block", L2PolyKit.Tread(size, r.Next(100000)), baseCentre + Vector3.up * size.y * 0.5f, mat);
        }

        private static GameObject Prefab(string path, Transform parent, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (asset == null)
            {
                throw new System.ArgumentException("Missing prefab " + path);
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // Saves (or overwrites in place) a generated mesh under 'folder'.
        private static Mesh SaveMesh(Mesh mesh, string folder)
        {
            L2Build.EnsureFolder(folder);
            string path = folder + "/" + mesh.name + ".asset";
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

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Material Flat(string name, string hex, float smoothness)
        {
            L2Build.EnsureFolder(MaterialsPath);
            string path = MaterialsPath + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", L1Build.Hex(hex));
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- Look-dev shots

        // Game-view-equivalent captures (post on, particles simulated) of the places that set the level's look, into
        // Temp/L5/art_*.png. The low shots look along the halls the way the concept art does.
        public static string Shots()
        {
            Directory.CreateDirectory("Temp/L5");
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                ps.Simulate(10f, true, true, true);
            }

            var go = new GameObject("ShotCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1;
            var shots = new List<(string name, Vector3 pos, Vector3 euler, float fov, Vector3 player)>();
            void Play(string name, float x, float y, float h)
            {
                var at = new Vector3(x, h + 1f, y);
                shots.Add((name, at + Quaternion.Euler(65f, 0f, 0f) * Vector3.back * 8.5f, new Vector3(65f, 0f, 0f), 60f, new Vector3(x, h, y)));
            }

            Play("nave_mid", 54f, 312f, 0f);
            Play("chapel", 38f, 310.5f, 0f);
            Play("stair_foot", 54f, 362f, 0f);
            Play("landing", 54f, 376f, Landing);
            Play("terrace", 54f, 400f, Hall);
            Play("processional", 54f, 420f, Hall);
            var arena = GameObject.Find("CM_Arena_FixedFrame");
            if (arena != null)
            {
                shots.Add(("arena_frame", arena.transform.position, arena.transform.eulerAngles, L5Greybox.ArenaFov, new Vector3(54f, Hall, 446f)));
            }

            shots.Add(("vista_nave", new Vector3(54f, 7f, 272f), new Vector3(12f, 0f, 0f), 45f, new Vector3(54f, 0f, 280f)));
            shots.Add(("stair_low", new Vector3(54f, 2.2f, 344f), new Vector3(-6f, 0f, 0f), 58f, new Vector3(54f, 0f, 352f)));
            shots.Add(("concept", new Vector3(54f, Hall + 2.4f, 410.5f), new Vector3(3f, 0f, 0f), 55f, new Vector3(54f, Hall, 417f)));
            shots.Add(("throne", new Vector3(54f, Hall + 6f, 452f), new Vector3(16f, 0f, 0f), 52f, new Vector3(54f, Hall, 462f)));
            var player = GameObject.Find("Player");
            var saved = player != null ? player.transform.position : Vector3.zero;
            foreach (var (name, pos, euler, fov, stand) in shots)
            {
                if (player != null)
                {
                    player.transform.position = stand;
                }

                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
                cam.fieldOfView = fov;
                bool cinematic = euler.x < 30f;
                cam.cullingMask = cinematic ? ~0 : ~(1 << L3Build.Layer(CeilingLayer));
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes("Temp/L5/art_" + name + ".png", tex.EncodeToPNG());
                RenderTexture.active = null;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }

            if (player != null)
            {
                player.transform.position = saved;
            }

            Object.DestroyImmediate(go);
            return shots.Count + " shots in Temp/L5/art_*.png";
        }
    }
}
