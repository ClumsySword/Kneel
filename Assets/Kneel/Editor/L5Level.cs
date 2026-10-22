using System.Collections.Generic;
using System.IO;
using Kneel.Lighting.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools
{
    // Dresses the L5 greybox (L5Greybox: the brief's plan, its colliders and metrics) in the game's art, so the throne
    // room reads as the end of the same road as L4: L4's graded castle stone and coursed masonry, its flagstone ground,
    // the master's crimson, gold brazier fire and blood-red hellfire, bone-white skeletons, ash and embers, under L4's
    // endgame grade (L4LightingPass). Build regenerates the whole scene: the greybox first, then this pass hides its
    // boxes (their colliders stay, so collision and Check Metrics are the greybox's) and lays the art over them.
    // Enemies are stand-ins, as in L4; their AI comes later.
    public static class L5Level
    {
        public const string MaterialsPath = "Assets/Kneel/Materials/L5";
        public const string MeshesPath = "Assets/Kneel/Meshes/L5";
        private const string TexturesPath = MaterialsPath + "/Textures";
        private const string Skeletons = "Assets/Kneel/Prefabs/L2/Skeletons/L2_Skeleton_";
        private const string L4Prefabs = "Assets/Kneel/Prefabs/L4/";
        private const float Hall = L5Greybox.Hall, Landing = L5Greybox.Landing;

        private static Material Stone => L4Build.Mat("L4_Knights_Stone");
        private static Material Soot => L4Build.Mat("L4_Knights_Soot");
        private static Material Iron => L4Build.Mat("L4_Iron");
        private static Material Bronze => L4Build.Mat("L4_Knights_Bronze");
        private static Material Basalt => L4Build.Mat("L4_Basalt");
        private static Material Charred => L2PolyKit.CharredWood;
        private static Material Blocks => L4Kit.Stone;
        private static Material Carpet => Flat("L5_Carpet", "#4A151A", 0.04f);
        private static Material Tapestry => Flat("L5_TapestryBurnt", "#2E0F11", 0.05f);
        private static Material Marble => Flat("L5_KingMarble", "#77726B", 0.25f);
        private static Material Wax => Flat("L5_Wax", "#A89D86", 0.25f);
        private static Material Parchment => Flat("L5_Parchment", "#7A6C55", 0.1f);
        private static Material Robe => Flat("L5_MasterRobe", "#141213", 0.18f);

        private static System.Random rng;
        private const float AmbientLift = 1.25f;

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
            HideGreybox(root);

            var art = Group(root, "Art");
            log.Add(Floors(Group(art, "Floors")));
            log.Add(Walls(Group(art, "Walls"), root));
            Nave(Group(art, "Nave"), root);
            Chapel(Group(art, "Chapel"));
            Alcove(Group(art, "Alcove"));
            Stair(Group(art, "Stair"), root);
            Portal(Group(art, "OuterDoors"), L5Greybox.P(54f, 268.6f, 0f), 801);
            Portal(Group(art, "GrandDoors"), L5Greybox.P(54f, 407.1f, Hall), 811);
            ThroneHall(Group(art, "Hall"));
            Dais(Group(art, "Dais"));
            Phases(root);
            Outside(Group(art, "Outside"));
            Capital(Group(art, "Capital"));
            Figures(Group(art, "Figures"), root);
            Atmosphere(Group(art, "Atmosphere"));

            foreach (Transform area in art)
            {
                L4Build.BakeLooseMeshes(area.gameObject, "L5_" + area.name, MeshesPath + "/Kit");
            }

            foreach (var phase in new[] { "Phase1", "Phase2" })
            {
                var art2 = root.Find(phase + "/Art");
                if (art2 != null)
                {
                    L4Build.BakeLooseMeshes(art2.gameObject, "L5_" + phase, MeshesPath + "/Kit");
                }
            }

            log.Add(L4LightingPass.Apply(root));
            // L4's grade over L4's yards, but L5 is indoors under tall walls: lift the ambient a little so the walls
            // and floors keep their shape in the shade, as the yards do in L4's low sun.
            RenderSettings.ambientSkyColor *= AmbientLift;
            RenderSettings.ambientEquatorColor *= AmbientLift;
            RenderSettings.ambientGroundColor *= AmbientLift;
            foreach (var figure in new[] { "Master (stand-in)", "RoyalGuard (stand-in)" })
            {
                var go = root.Find("Art/Figures/" + figure);
                if (go != null)
                {
                    L1LightingShots.SetCharacterLayer(go.gameObject);
                }
            }
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

            // The ground round the whole building, trodden earth and ash, far enough out that no frame ends in a void.
            var outside = Rect.MinMaxRect(0f, 236f, 108f, 482f);
            L3Build.MeshChild(g, "Outside", Slab("L5_Floor_Outside", outside.width, outside.height), L5Greybox.P(outside.center.x, outside.center.y, -0.06f), FloorMaterial("Outside", outside, true, 0f));
            return FloorRects.Length + 1 + " floors";
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
        // earth shows. In the nave and the hall, ash lies grey over the flags and drifts against the walls; in the
        // hall, soot round the braziers; at the back of the nave, the scullery's drag marks toward the doors.
        private static Material FloorMaterial(string id, Rect r, bool ash, float paving = 1f)
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
            var ashTone = new Color(0.5f, 0.485f, 0.46f);
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
                    pave *= paving;
                    var c = Color.Lerp(earth, stone, pave);
                    if (ash)
                    {
                        float drift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.6f, 0.2f, edge + ragged * 0.6f));
                        float lay = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.75f, Mathf.PerlinNoise(p.x * 0.3f + seed, p.y * 0.3f)));
                        c = Color.Lerp(c, ashTone, Mathf.Max(drift * 0.7f, lay * 0.4f));
                    }

                    if (id == "Nave")
                    {
                        // Drag marks: furrows in the ash from the files toward the doors, ending a body-length short.
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

                        // Scorch under the fallen roof at the north end.
                        float north = Mathf.InverseLerp(452f, 466f, p.y) * Mathf.PerlinNoise(p.x * 0.4f, p.y * 0.4f + 9f);
                        c = Color.Lerp(c, soot, north * 0.6f);
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
        // scale instead of stretching. Hall walls run down to the ground outside, round the podium.
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
                bool hall = t.parent.name == "ThroneHall";
                if (hall)
                {
                    h0 = 0f;
                }

                // The grand doors' portal and towers stand where the door wall meets the opening.
                if (t.name == "DoorWall_W")
                {
                    x1 = 44f;
                }
                else if (t.name == "DoorWall_E")
                {
                    x0 = 64f;
                }

                pieces += Clad(g, x0, x1, y0, y1, h0, h1);
            }

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
            // Pilasters up both walls, between the lancets (none across D1's or D2's door).
            foreach (float y in NavePilasters)
            {
                if (y < 306f || y > 315f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, 9f, 1.1f), new Vector2(46.8f, y), 0f, Stone);
                }

                if (y < 333f || y > 339f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, 9f, 1.1f), new Vector2(61.2f, y), 0f, Stone);
                }
            }

            // Burned lancets in the west wall, firelight from the city behind them striping the floor; burnt
            // tapestries opposite them on the east wall.
            var lights = Group(g, "LancetLight");
            for (float y = 279f; y < 356f; y += 9f)
            {
                if (y > 305f && y < 316f)
                {
                    continue;
                }

                L4Citadel.Window(g, new Vector3(46.52f, 5f, y), -90f, 1.4f, 4.6f);
                PointLight(lights, "Lancet", new Vector3(47.8f, 4.2f, y), "#FF6A2A", 4f, 9f);
                if (y < 333f || y > 339f)
                {
                    var banner = L3Build.Part(g, rng.NextDouble() < 0.5 ? "K:SM_Prop_Banner_02" : "K:SM_Prop_Banner_03", new Vector3(61.4f, 8.2f, y), new Vector3(0f, 90f, ((float)rng.NextDouble() - 0.5f) * 4f), Vector3.one * 1.6f, Tapestry);
                    banner.name = "Tapestry";
                }
            }

            // The roof burned off long ago: its trusses remain only as shadows across the floor.
            for (float y = 272f; y < 358f; y += 4.5f)
            {
                ShadowBeam(g, new Vector3(54f, 9.4f, y), 18.5f);
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

            // The front ranks were the court: their cups and books lie where they dropped them.
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

            // Braziers down the walk, pools of firelight from the doors to the stair (each a small obstacle).
            foreach (float y in new[] { 290f, 308f, 326f, 344f })
            {
                foreach (float x in new[] { 50.1f, 57.9f })
                {
                    var at = new Vector3(x, 0f, y + (x < 54f ? 0f : 9f));
                    StandingBrazier(g, at, 1f, false);
                    Obstacle(root, at, new Vector3(0.6f, 1.9f, 0.6f));
                }
            }

            foreach (float x in new[] { 48.2f, 59.8f })
            {
                StandingBrazier(g, new Vector3(x, 0f, 358.9f), 1.25f, false);
                Obstacle(root, new Vector3(x, 0f, 358.9f), new Vector3(0.7f, 2.4f, 0.7f));
            }

            // Guttered candles among the front files, a few still burning, and the chapel's light spilling from its door.
            Candles(g, new Vector3(50f, 0f, 356.4f), 5, 1);
            Candles(g, new Vector3(58.2f, 0f, 356.1f), 5, 1);
            Candles(g, new Vector3(48.2f, 0f, 309.9f), 3, 1);
            LayCarpet(g, Rect.MinMaxRect(51.5f, 270f, 56.5f, 358.4f), 0f, 0.18f, 911);
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

            // The prayer stool, worn hollow at the knees, the confessor's book on it.
            var stool = Group(g, "PrayerStool");
            stool.position = new Vector3(36.4f, 0f, 310.5f);
            L3Build.MeshChild(stool, "Top", L2PolyKit.Plank(new Vector3(0.5f, 0.06f, 1.1f), 9211), new Vector3(0f, 0.42f, 0f), Charred);
            foreach (float s in new[] { -1f, 1f })
            {
                L3Build.MeshChild(stool, "Leg", L2PolyKit.Plank(new Vector3(0.45f, 0.4f, 0.06f), 9212 + (int)s), new Vector3(0f, 0.2f, s * 0.48f), Charred);
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

            // The cupbearer: sitting, not kneeling, against the wall; empty cups round him.
            Prefab(Skeletons + "Slumped", g, new Vector3(69.7f, 0f, 336.4f), -90f);
            for (int i = 0; i < 4; i++)
            {
                var cup = L3Build.Seat(g, "P:SM_Icon_Cup_0" + (1 + i % 3), new Vector3(i == 1 ? 90f : 0f, rng.Next(360), 0f), 0.22f, new Vector2(68.2f + (float)rng.NextDouble() * 1.4f, 334.2f + i * 1.1f), 0f, Bronze);
                cup.name = "Cup";
            }

            Prefab(L4Prefabs + "Markers/L4_Pickup_Consumable", g, new Vector3(66f, 0f, 335f), 0f);
            Candles(g, new Vector3(64.2f, 0f, 338.2f), 2, 1);
        }

        // ---------------------------------------------------------------- The imperial stair

        private static void Stair(Transform g, Transform root)
        {
            Steps(g, "Flight_Centre", 46.5f, 61.5f, 360f, 0f, 3001);
            Steps(g, "Flight_W", 34.5f, 46.5f, 384f, Landing, 3002);
            Steps(g, "Flight_E", 61.5f, 73.5f, 384f, Landing, 3003);

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

            // The old King's statue, taken off at the knee and set back down, kneeling to the doors. The greybox box
            // stood inside the pier; this one gets its own collider on the landing.
            var statue = Group(g, "KingStatue");
            statue.position = new Vector3(54f, Landing, 382f);
            L3Build.Seat(statue, "K:SM_Prop_Statue_Base_01", new Vector3(0f, 180f, 0f), 1.9f, Vector2.zero, 0f, Stone);
            var king = L3Build.Seat(statue, "K:SM_Prop_Statue_01", new Vector3(0f, 180f, 0f), 1.35f, Vector2.zero, 0.2f, Marble);
            king.transform.localPosition += Vector3.down * 1.6f;
            Fallen(statue, new Vector3(0f, 0f, -2.4f), 5, 1.6f);
            var col = new GameObject("StatueCollider");
            col.transform.SetParent(root, false);
            col.transform.position = statue.position;
            col.layer = L3Build.Layer("Obstacles");
            L3Build.StandingBox(col, new Vector3(3.6f, 4.5f, 3.6f));
            var greybox = root.Find("ImperialStair/KingStatue_Kneeling");
            if (greybox != null)
            {
                Object.DestroyImmediate(greybox.gameObject);
            }

            // Earlier wardens along the plinth, each a little further gone.
            Prefab(L4Prefabs + "Figures/L4_Kneeler_Slumped_Knight", g, new Vector3(50.6f, Landing, 381.2f), 15f);
            Prefab(L4Prefabs + "Figures/L4_Kneeler_Slumped_Soldier", g, new Vector3(57.4f, Landing, 381.6f), -20f);
            Prefab(Skeletons + "Remains", g, new Vector3(49.2f, Landing, 383.2f), 40f);

            // Banners down the stair hall walls; great braziers either side of the flight's head.
            foreach (float y in new[] { 366f, 378f, 390f })
            {
                L4Citadel.HangBanner(g, new Vector3(34.6f, 13f, y), -90f, 2.4f, (int)y);
                L4Citadel.HangBanner(g, new Vector3(73.4f, 13f, y), 90f, 2.4f, (int)y + 1);
            }

            foreach (float x in new[] { 43.5f, 64.5f })
            {
                StandingBrazier(g, new Vector3(x, Landing, 362.4f), 1.3f, false);
                var at = new Vector3(x + (x < 54f ? 3f : -3f), Landing, 381.5f);
                StandingBrazier(g, at, 1.3f, false);
                Obstacle(root, at, new Vector3(0.7f, 2.4f, 0.7f));
            }

            LayCarpet(g, Rect.MinMaxRect(50f, 372.4f, 58f, 380.5f), Landing, 0.3f, 931);
            LayCarpet(g, Rect.MinMaxRect(51f, 396.4f, 57f, 406.4f), Hall, 0.2f, 932);
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

        // A portal like L4's throne doors (S09): three recessed round-arched orders between two square towers with the
        // master's banners and cressets, great braziers on plinths before it. Here the doors stand open, swung back
        // inside against the wall. 'at' is the door line's centre on its floor; the orders step out toward -Z.
        private static void Portal(Transform g, Vector3 at, int seed)
        {
            var r = new System.Random(seed);
            g.position = at;
            const float wall = 13f;
            for (int o = 0; o < 3; o++)
            {
                float span = 8.4f + o * 1.2f;
                var ring = L3Build.Child(g, "Order", new Vector3(0f, 0f, o == 0 ? 0f : -0.3f - o * 0.6f)).transform;
                L4Kit.GateArch(ring, span, 0.6f, 6.6f, span * 0.5f, wall, o == 0 ? 1.2f : 0.6f, seed + o);
            }

            L4Kit.MerlonRun(g, new Vector3(-6f, wall, -1.8f), Vector3.right, Vector3.back, 6, seed + 5, 0.3f, 0f, null, null, false);
            foreach (float s in new[] { -1f, 1f })
            {
                // The leaves stand open, back against the inside of the wall.
                L3Build.Fit(g, "K:SM_Bld_Castle_Door_01", 90, new Vector3(0.45f, 6.6f, 4.2f), new Vector2(s * 4.15f, 3.2f), 0f, Soot);

                const float scale = 1.6f, stage = 3.04f * scale;
                string[] stages = { "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_04" };
                var tower = L3Build.Child(g, s < 0 ? "TowerWest" : "TowerEast", new Vector3(s * 8.3f, 0f, 0f)).transform;
                for (int i = 0; i < 3; i++)
                {
                    L3Build.Part(tower, stages[i], new Vector3(0f, i * stage, 0f), new Vector3(0f, s < 0 ? 0f : 90f, 0f), Vector3.one * scale, Stone);
                }

                L3Build.Part(tower, "K:SM_Bld_Castle_Tower_Top_01", new Vector3(0f, 3f * stage, 0f), Vector3.zero, Vector3.one * scale, Stone);
                L3Build.Part(tower, "K:SM_Prop_Banner_01", new Vector3(0f, wall + 0.6f, -2.32f), new Vector3(0f, 0f, s * 1.5f), Vector3.one * 2f, L4Build.Mat("L4_RoyalCloth"));
                var cresset = L3Build.Child(tower, "Cresset", new Vector3(0f, 3f * stage, 0f)).transform;
                L3Build.Seat(cresset, "K:SM_Prop_Brazier_01", Vector3.zero, 0.9f, Vector2.zero, 0f, Iron);
                L2Fire.Fire(cresset, "Flames", new Vector3(0f, 1.75f, 0f), 0.5f, 2.2f, 10f, true);

                var plinth = L3Build.Child(g, "Brazier", new Vector3(s * 5.3f, 0f, -2.6f)).transform;
                L4Kit.Courses(plinth, new Vector3(-0.6f, 0f, -0.6f), Vector3.right, Vector3.back, 1.2f, 0.9f, r, 0f, 1.2f, 2f);
                L3Build.Seat(plinth, "K:SM_Prop_Brazier_01", Vector3.zero, 1.5f, Vector2.zero, 0.9f, Iron);
                L2Fire.Fire(plinth, "Flames", new Vector3(0f, 0.9f + 1.89f * 1.5f - 0.1f, 0f), 0.85f, 3.6f, 13f, true);
                var col = L3Build.Child(plinth, "Collider");
                L3Build.StandingBox(col, new Vector3(1.2f, 3.6f, 1.2f));
                col.layer = L3Build.Layer("Obstacles");
                var towerCol = L3Build.Child(tower, "Collider");
                L3Build.StandingBox(towerCol, new Vector3(2.78f * scale, 3f * stage, 2.78f * scale));
                towerCol.layer = L3Build.Layer("Obstacles");
            }
        }

        // ---------------------------------------------------------------- The throne hall

        private static void ThroneHall(Transform g)
        {
            // Burning lancets high in both long walls, between the pillar rows.
            var lights = Group(g, "LancetLight");
            foreach (float y in new[] { 420f, 432f, 444f, 456f })
            {
                L4Citadel.Window(g, new Vector3(33.02f, Hall + 7.5f, y), -90f, 1.8f, 6f);
                L4Citadel.Window(g, new Vector3(74.98f, Hall + 7.5f, y), 90f, 1.8f, 6f);
                PointLight(lights, "Lancet", new Vector3(34.6f, Hall + 5f, y), "#FF6A2A", 2.6f, 9f);
                PointLight(lights, "Lancet", new Vector3(73.4f, Hall + 5f, y), "#FF6A2A", 2.6f, 9f);
            }

            // Pillars: coursed piers on plinths. Down the processional they stand whole, 12 m, under the last of the
            // roof; in the arena they are stumps, broken when the roof came in, their blocks lying at their feet.
            var r = new System.Random(4001);
            foreach (float x in L5Greybox.PillarLines)
            {
                foreach (float y in L5Greybox.PillarRows)
                {
                    bool arena = y > L5Greybox.ArenaFloor.yMin;
                    Pier(Group(g, arena ? "Stump" : "Pillar"), new Vector3(x, Hall, y), arena ? 4f : 12f, arena, r);
                }
            }

            // The sagging beams: a burnt roof timber fallen from the wall top onto each middle stump, still smouldering
            // along the crack where it will give.
            foreach (float s in new[] { -1f, 1f })
            {
                float wallX = 54f + s * 21.2f, stumpX = 54f + s * 15f;
                var top = new Vector3(wallX, Hall + 12.2f, 450f);
                var foot = new Vector3(stumpX, Hall + 4.1f, 450f);
                Beam(g, "SaggingBeam", top, foot, 0.85f);
                L2Fire.Smoulder(g, "Crack", Vector3.Lerp(top, foot, 0.35f), new Vector2(1.2f, 0.6f), 0f, true);
            }

            // What is left of the roof: charred rafter ends along both wall tops over the arena, burning in places.
            for (float y = 434f; y < 466f; y += 3f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    float wallX = 54f + s * 21f;
                    float reach = 1.2f + (float)rng.NextDouble() * 1.6f;
                    Beam(g, "Rafter", new Vector3(wallX + s * 0.6f, Hall + 12.1f, y), new Vector3(wallX - s * reach, Hall + 11.6f - (float)rng.NextDouble() * 0.6f, y + ((float)rng.NextDouble() - 0.5f) * 0.6f), 0.4f);
                }
            }

            foreach (var (x, y) in new[] { (32.2f, 440f), (75.8f, 446f), (32.2f, 458f), (75.8f, 463f), (32.2f, 427f) })
            {
                L2Fire.Fire(g, "RoofFire", new Vector3(x, Hall + 12.2f, y), 0.9f, 2.6f, 10f, true, new Vector3(1.2f, 0f, 2.4f), L2Fire.Shape.Box);
            }

            // The roof over the processional still stands as burnt trusses: shadows across the floor.
            for (float y = 410f; y < 431f; y += 4f)
            {
                ShadowBeam(g, new Vector3(54f, Hall + 12.4f, y), 45f);
            }

            // Banners of the master's crimson on the walls behind the stumps.
            foreach (float y in new[] { 438f, 450f, 462f })
            {
                L4Citadel.HangBanner(g, new Vector3(33.1f, Hall + 10.5f, y), -90f, 2.2f, (int)y);
                L4Citadel.HangBanner(g, new Vector3(74.9f, Hall + 10.5f, y), 90f, 2.2f, (int)y + 1);
            }

            // Those who came before the player: remains and dropped blades along the arena's edges, never in the ring.
            var remains = new[] { (36f, 436f, "Remains"), (42.5f, 447f, "Prone"), (35.5f, 455f, "Slumped"), (71.5f, 441f, "Supine"), (65.6f, 459f, "Remains"), (72.5f, 452f, "Curled"), (44f, 464.5f, "Prone"), (64.5f, 434.5f, "Remains") };
            foreach (var (x, y, kind) in remains)
            {
                Prefab(Skeletons + kind, g, new Vector3(x, Hall, y), rng.Next(360));
                var blade = L3Build.Seat(g, "K:SM_Wep_Broadsword_01", new Vector3(90f, rng.Next(360), 0f), 1f, new Vector2(x + 0.8f, y + 0.5f), Hall, Iron);
                blade.name = "Blade";
            }

            // The processional carpet to the dais, burnt to scraps toward the throne.
            LayCarpet(g, Rect.MinMaxRect(51f, 408.4f, 57f, 466.3f), Hall, 0.3f, 951, 440f);
        }

        // A square pier of coursed blocks on a plinth; whole ones get a capital, broken ones a ragged top over a rubble
        // core and their fallen blocks round the foot.
        private static void Pier(Transform g, Vector3 c, float height, bool broken, System.Random r)
        {
            const float h = 1.5f;
            float loss = broken ? 1f : 0f;
            L3Build.MeshChild(g, "Plinth", L2PolyKit.Tread(new Vector3(3.5f, 0.45f, 3.5f), r.Next(100000)), c + Vector3.up * 0.22f, Blocks);
            var b = c + Vector3.up * 0.45f;
            float hh = height - 0.45f;
            float Face() => broken ? hh * (0.4f + (float)r.NextDouble() * 0.6f) : hh;
            L4Kit.Courses(g, b + new Vector3(-h, 0f, -h), Vector3.right, Vector3.back, 3f, Face(), r, loss, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(h, 0f, h), Vector3.left, Vector3.forward, 3f, Face(), r, loss, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(-h, 0f, h), Vector3.back, Vector3.left, 3f, Face(), r, loss, 0.6f, 1.5f);
            L4Kit.Courses(g, b + new Vector3(h, 0f, -h), Vector3.forward, Vector3.right, 3f, Face(), r, loss, 0.6f, 1.5f);
            if (broken)
            {
                float core = hh * 0.55f;
                L3Build.MeshChild(g, "Core", L2PolyKit.Tread(new Vector3(2f, core, 2f), r.Next(100000)), b + Vector3.up * core * 0.5f, Blocks);
                for (int i = 0; i < 3; i++)
                {
                    var chunk = L3Build.MeshChild(g, "Chunk", L2PolyKit.Tread(new Vector3(1f + (float)r.NextDouble() * 0.8f, 0.45f, 0.6f), r.Next(100000)), b + new Vector3(((float)r.NextDouble() - 0.5f) * 1.6f, core + 0.2f + i * 0.3f, ((float)r.NextDouble() - 0.5f) * 1.6f), Blocks);
                    chunk.transform.rotation = Quaternion.Euler(((float)r.NextDouble() - 0.5f) * 40f, r.Next(360), ((float)r.NextDouble() - 0.5f) * 40f);
                }

                Fallen(g, c + new Vector3(0f, 0f, 0f), 8, 2.2f);
            }
            else
            {
                L3Build.MeshChild(g, "Capital", L2PolyKit.Tread(new Vector3(3.5f, 0.5f, 3.5f), r.Next(100000)), c + Vector3.up * (height + 0.25f), Blocks);
            }
        }

        // ---------------------------------------------------------------- The dais and the throne

        private static void Dais(Transform g)
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

            // The throne: a high-backed seat of black stone on its own step, scorched; a scrap of crimson on the seat.
            var throne = Group(g, "Throne");
            throne.position = L5Greybox.Throne;
            Block(throne, new Vector3(3.2f, 0.4f, 2.6f), new Vector3(0f, 0f, 0f), Basalt, r);
            Block(throne, new Vector3(1.9f, 0.55f, 1.5f), new Vector3(0f, 0.4f, -0.1f), Basalt, r);
            Block(throne, new Vector3(2.1f, 3.8f, 0.5f), new Vector3(0f, 0.4f, 0.8f), Basalt, r);
            foreach (float s in new[] { -1f, 1f })
            {
                Block(throne, new Vector3(0.38f, 0.75f, 1.5f), new Vector3(s * 1.14f, 0.4f, -0.1f), Basalt, r);
                var spire = Block(throne, new Vector3(0.5f, 1.4f, 0.5f), new Vector3(s * 0.55f, 3.95f, 0.8f), Basalt, r);
                spire.transform.localRotation = Quaternion.Euler(0f, 0f, s * 24f);
            }

            Block(throne, new Vector3(1.4f, 0.1f, 1.1f), new Vector3(0f, 0.95f, -0.15f), L4Citadel.BannerRed, r);

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

            // Hellfire either side of the throne: the master's own fire, blood-red.
            foreach (float s in new[] { -1f, 1f })
            {
                var b = Group(g, "Hellfire");
                b.position = new Vector3(54f + s * 4.6f, y + 0.6f, 471f);
                L3Build.Seat(b, "K:SM_Prop_Brazier_01", Vector3.zero, 1.35f, Vector2.zero, 0f, Iron);
                L4Citadel.Hellfire(b, "Flames", new Vector3(0f, 1.89f * 1.35f - 0.05f, 0f), 0.8f, 3.4f, 11f);
                Candles(g, new Vector3(54f + s * 2.6f, y + 0.6f, 469f), 6, 1);
            }

            // Two great banners on the apse wall either side of the breach.
            L4Citadel.HangBanner(g, new Vector3(42.5f, Hall + 11.5f, 472.4f), 0f, 3f, 0);
            L4Citadel.HangBanner(g, new Vector3(65.5f, Hall + 11.5f, 472.4f), 0f, 3f, 1);

            // The roof that fell before the player came: rubble heaped either side of the dais, timbers through it.
            foreach (float s in new[] { -1f, 1f })
            {
                float cx = 54f + s * 16.5f;
                var heap = Group(g, "Rubble");
                for (int i = 0; i < 70; i++)
                {
                    float u = (float)r.NextDouble() - 0.5f, v = (float)r.NextDouble() - 0.5f;
                    float rise = 1.6f * (1f - Mathf.Max(Mathf.Abs(u) * 1.6f, Mathf.Abs(v) * 1.6f));
                    var block = L4Kit.Fallen(heap, new Vector3(cx + u * 9f, y + Mathf.Max(0f, rise) * (float)r.NextDouble(), 469.5f + v * 6f), r);
                    block.transform.localScale = Vector3.one * (1.2f + (float)r.NextDouble() * 1.2f);
                }

                Beam(heap, "Timber", new Vector3(cx - s * 3.5f, y + 0.2f, 467.6f), new Vector3(cx + s * 2.5f, y + 2.6f, 471.8f), 0.6f);
                Beam(heap, "Timber", new Vector3(cx + s * 3.8f, y + 0.1f, 468.4f), new Vector3(cx - s * 1.5f, y + 1.9f, 470.8f), 0.55f);
                L2Fire.Smoulder(heap, "Smoulder", new Vector3(cx, y + 0.9f, 469.6f), new Vector2(3f, 2f), 0f, true);
            }

            // The breach's ragged sill.
            L4Kit.Courses(g, new Vector3(48f, Hall, 472.5f), Vector3.right, Vector3.back, 12f, 0.9f, r, 0.7f, 1.5f, 1.5f);
        }

        // ---------------------------------------------------------------- Phase art

        private static void Phases(Transform root)
        {
            var p1 = Group(root.Find("Phase1"), "Art");
            foreach (var b in L5Greybox.Braziers)
            {
                StandingBrazier(p1, new Vector3(b.x, Hall, b.y), 1f, true);
            }

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
                var half = alongX ? new Vector3(2.8f, 0f, 0f) : new Vector3(0f, 0f, 2.8f);
                Beam(p2, "FallenBeam", c - half + Vector3.up * 0.55f, c + half + Vector3.up * 0.75f, 1.1f);
                Fallen(p2, c, 6, 1.8f);
                L2Fire.Smoulder(p2, "Smoulder", c + Vector3.up * 0.9f, new Vector2(1.6f, 3.6f), alongX ? 90f : 0f, true);
            }
        }

        // A brazier on the floor (P1's can be toppled); 'tall' baskets stand on an iron stand of their own.
        private static void StandingBrazier(Transform g, Vector3 at, float scale, bool hazard)
        {
            var b = Group(g, hazard ? "Brazier_Hazard" : "Brazier");
            b.position = at;
            L3Build.Seat(b, "K:SM_Prop_Brazier_01", Vector3.zero, scale, Vector2.zero, 0f, Iron);
            L2Fire.Fire(b, "Flames", new Vector3(0f, 1.89f * scale - 0.08f, 0f), 0.6f * scale, 2.8f * scale, 9f * scale, true);
        }

        // ---------------------------------------------------------------- Outside

        private static void Outside(Transform g)
        {
            // Buttresses down the outer faces of the nave and the hall.
            for (float y = 274.5f; y < 358f; y += 9f)
            {
                L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(1.6f, 9.6f, 1.8f), new Vector2(44.2f, y), 0f, Stone);
                L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(1.6f, 9.6f, 1.8f), new Vector2(63.8f, y), 0f, Stone);
            }

            foreach (float y in L5Greybox.PillarRows)
            {
                L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(2.2f, 18.6f, 2.4f), new Vector2(30.4f, y), 0f, Stone);
                L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(2.2f, 18.6f, 2.4f), new Vector2(77.6f, y), 0f, Stone);
            }

            // Rubble and the dead along the outer walls, and fires in the ruins round the building.
            for (int i = 0; i < 60; i++)
            {
                bool west = i % 2 == 0;
                float y = 262f + (float)rng.NextDouble() * 205f;
                float wall = y < 360f ? (west ? 45f : 63f) : y < 406.5f ? (west ? 33f : 75f) : (west ? 31.5f : 76.5f);
                float x = wall + (west ? -1f : 1f) * (0.6f + (float)rng.NextDouble() * 6f);
                L4Kit.Fallen(g, new Vector3(x, 0f, y), rng);
            }

            foreach (var (x, y) in new[] { (36f, 290f), (72f, 318f), (24f, 380f), (86f, 392f), (20f, 446f), (88f, 430f) })
            {
                L2Fire.Fire(g, "RuinFire", new Vector3(x, 0.1f, y), 1.2f, 3f, 12f, true, new Vector3(3f, 0f, 2f), L2Fire.Shape.Box);
            }
        }

        // ---------------------------------------------------------------- The burning capital, through the breach

        private static void Capital(Transform g)
        {
            const float ground = -48f;
            var plain = Rect.MinMaxRect(-160f, 476f, 270f, 900f);
            L3Build.MeshChild(g, "CityGround", Slab("L5_Floor_City", plain.width, plain.height), new Vector3(plain.center.x, ground, plain.center.y), FloorMaterial("City", Rect.MinMaxRect(0f, 0f, 160f, 160f), true, 0.25f));
            // The rock the citadel stands on, falling away under the apse.
            var r = new System.Random(6001);
            for (int i = 0; i < 9; i++)
            {
                float x = 20f + i * 9f;
                L3Build.Seat(g, "K:SM_Bld_Castle_Tower_Base_01", new Vector3(0f, r.Next(4) * 90f, 0f), new Vector3(4.2f, 7f, 4.2f), new Vector2(x, 480f + (float)r.NextDouble() * 4f), ground, Soot);
            }

            // Towers, halls and walls of the city in the plain, burning; smoke leaning north.
            string[] keys = { "K:SM_Bld_Castle_Tower_01", "K:SM_Bld_Castle_Tower_02", "K:SM_Bld_Church_Tower_01", "K:SM_Bld_Church_Room_01", "K:SM_Bld_Castle_Wall_01", "K:SM_Bld_Church_Room_02" };
            for (int i = 0; i < 46; i++)
            {
                float x = -100f + (float)r.NextDouble() * 310f;
                float y = 520f + (float)r.NextDouble() * 320f;
                float scale = 2f + (float)r.NextDouble() * 2.2f;
                L3Build.Seat(g, keys[r.Next(keys.Length)], new Vector3(0f, r.Next(360), 0f), scale, new Vector2(x, y), ground, i % 3 == 0 ? Soot : Stone);
                if (i % 2 == 0)
                {
                    L2Fire.Fire(g, "CityFire", new Vector3(x, ground + 3f, y), 2.4f, 6f, 40f, false, new Vector3(6f, 0f, 6f), L2Fire.Shape.Box, 0.8f);
                }

                if (i % 6 == 0)
                {
                    L4Atmosphere.Column(g, new Vector3(x, ground + 4f, y), 2.4f);
                }
            }
        }

        // ---------------------------------------------------------------- Figures (stand-ins and set dressing)

        private static void Figures(Transform g, Transform root)
        {
            Prefab(L4Prefabs + "Markers/L4_Shrine", g, L5Greybox.ShrineC, 0f).name = "Shrine_C_LastShrine";
            Prefab(L4Prefabs + "Markers/L4_Shrine", g, L5Greybox.P(54f, 263.6f, 0f), 0f).name = "Shrine_B";

            // The Royal Guard (stand-in: L4's black-iron knight, kneeling upright being the cue; AI comes later).
            var guard = Prefab(L4Prefabs + "Enemies/L4_StandIn_IronHusk", g, L5Greybox.P(54f, 377.6f, Landing), 0f);
            guard.name = "RoyalGuard (stand-in)";
            foreach (var marker in guard.GetComponentsInChildren<Kneel.Markers.SpawnMarker>(true))
            {
                Object.DestroyImmediate(marker);
            }

            // The Master (stand-in): a gaunt figure half again a man's height, robed in black, before the throne.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Kneel/Meshes/L4/Figures/L4_Stand_Knight_02.asset");
            var master = L3Build.MeshChild(g, "Master (stand-in)", mesh, L5Greybox.Throne + new Vector3(0f, 0f, -1.9f), Robe);
            master.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            master.transform.localScale = new Vector3(1.25f, 1.45f, 1.25f);
            L4Citadel.Hellfire(master.transform, "PalmEmber", new Vector3(-0.5f, 1.35f, 0.35f), 0.12f, 1.2f, 3f);
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

        private static void Atmosphere(Transform g)
        {
            L4Atmosphere.Ash(g, new Vector3(54f, 10f, 314f), new Vector3(18f, 2f, 92f));
            L4Atmosphere.Ash(g, new Vector3(54f, 16f, 383f), new Vector3(40f, 2f, 46f));
            L4Atmosphere.Ash(g, new Vector3(54f, Hall + 13f, 440f), new Vector3(42f, 2f, 66f));
            L4Atmosphere.Embers(g, new Vector3(54f, Hall + 6f, 452f), new Vector3(40f, 8f, 36f));
            foreach (var (x, y) in new[] { (24f, 452f), (86f, 440f), (30f, 330f), (80f, 300f), (54f, 492f) })
            {
                L4Atmosphere.Column(g, new Vector3(x, 0f, y), 1.6f);
            }
        }

        // ---------------------------------------------------------------- Pieces

        // A carpet runner laid down an axis, frayed along its edges and burnt through: across a burn the cloth is eaten
        // back from both edges until it parts, each torn end tapering to a charred rim, and past 'burntFrom' only scraps
        // are left. Built as a strip of trapezoids between 0.25 m stations, so its edges run smooth.
        private static void LayCarpet(Transform g, Rect r, float floor, float burn, int seed, float burntFrom = float.MaxValue)
        {
            var verts = new List<Vector3>();
            var cloth = new List<int>();
            var rim = new List<int>();
            const float step = 0.25f, charredRim = 0.1f, lift = 0.012f;
            int stations = Mathf.Max(2, Mathf.RoundToInt(r.height / step)) + 1;
            float ch = r.height / (stations - 1);
            var left = new float[stations];
            var right = new float[stations];
            var charred = new bool[stations];
            for (int j = 0; j < stations; j++)
            {
                float y = r.yMin + j * ch;
                float tear = Mathf.PerlinNoise(y * 0.16f + seed, 0.37f) * 0.8f + Mathf.PerlinNoise(y * 0.6f, seed * 0.1f) * 0.2f;
                float threshold = 1f - burn;
                if (y > burntFrom)
                {
                    threshold -= Mathf.InverseLerp(burntFrom, burntFrom + 16f, y) * 0.38f;
                }

                float eat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - 0.16f, threshold + 0.02f, tear));
                float fray = 0.03f + Mathf.Abs(Mathf.PerlinNoise(y * 1.1f + seed, 7f) - 0.5f) * 0.22f;
                float bias = Mathf.PerlinNoise(y * 0.5f, seed + 3f);
                left[j] = r.xMin + fray + eat * r.width * 0.62f * bias;
                right[j] = r.xMax - fray - eat * r.width * 0.62f * (1f - bias);
                charred[j] = eat > 0.06f;
            }

            void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int k = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                verts.Add(d);
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }

            for (int j = 0; j < stations - 1; j++)
            {
                float y0 = r.yMin + j * ch, y1 = y0 + ch;
                if (right[j] - left[j] < 0.3f || right[j + 1] - left[j + 1] < 0.3f)
                {
                    continue;
                }

                float rl0 = charred[j] ? charredRim : 0f, rl1 = charred[j + 1] ? charredRim : 0f;
                if (rl0 > 0f || rl1 > 0f)
                {
                    Quad(rim, new Vector3(left[j], lift, y0), new Vector3(left[j + 1], lift, y1), new Vector3(left[j + 1] + rl1, lift, y1), new Vector3(left[j] + rl0, lift, y0));
                    Quad(rim, new Vector3(right[j] - rl0, lift, y0), new Vector3(right[j + 1] - rl1, lift, y1), new Vector3(right[j + 1], lift, y1), new Vector3(right[j], lift, y0));
                }

                Quad(cloth, new Vector3(left[j] + rl0, lift, y0), new Vector3(left[j + 1] + rl1, lift, y1), new Vector3(right[j + 1] - rl1, lift, y1), new Vector3(right[j] - rl0, lift, y0));
            }

            var mesh = new Mesh { name = "L5_Carpet_" + seed, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(cloth, 0);
            mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = L3Build.MeshChild(g, "Carpet", SaveMesh(mesh, MeshesPath + "/Floors"), new Vector3(0f, floor, 0f), Carpet, Charred);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // A charred timber from a to b, 'thickness' metres across.
        private static void Beam(Transform g, string name, Vector3 a, Vector3 b, float thickness)
        {
            var dir = b - a;
            var beam = L3Build.Part(g, "K:SM_Prop_Beam_01", a, Vector3.zero, new Vector3(thickness / 0.26f, dir.magnitude / 2.49f, thickness / 0.26f), Charred);
            beam.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            beam.name = name;
        }

        // A roof timber across the room that exists only as its shadow on the floor.
        private static void ShadowBeam(Transform g, Vector3 centre, float length)
        {
            var beam = L3Build.Part(g, "K:SM_Prop_Beam_01", centre + Vector3.left * length * 0.5f, new Vector3(0f, 0f, -90f), new Vector3(2.4f, length / 2.49f, 2.4f), Charred);
            beam.name = "TrussShadow";
            foreach (var r in beam.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

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
                L3Build.MeshChild(g, "Candle", L2PolyKit.Tread(new Vector3(0.07f, h, 0.07f), rng.Next(100000)), p + Vector3.up * h * 0.5f, Wax);
                if (i < lit)
                {
                    L2Fire.Candle(g, "Flame", p + Vector3.up * (h + 0.04f), 0.9f, 4.5f);
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

        private static void PointLight(Transform g, string name, Vector3 at, string hex, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(g, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = L1Build.Hex(hex);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
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
        // Temp/L5/art_*.png.
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
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.volumeLayerMask = 1;
            var shots = new List<(string name, Vector3 pos, Vector3 euler, float fov)>();
            void Play(string name, float x, float y, float h)
            {
                var at = new Vector3(x, h + 1f, y);
                shots.Add((name, at + Quaternion.Euler(65f, 0f, 0f) * Vector3.back * 8.5f, new Vector3(65f, 0f, 0f), 60f));
            }

            Play("nave_start", 54f, 276f, 0f);
            Play("nave_mid", 54f, 312f, 0f);
            Play("chapel", 38f, 310.5f, 0f);
            Play("alcove", 64f, 336f, 0f);
            Play("stair_foot", 54f, 362f, 0f);
            Play("landing", 54f, 376f, Landing);
            Play("terrace", 54f, 400f, Hall);
            Play("processional", 54f, 420f, Hall);
            var arena = GameObject.Find("CM_Arena_FixedFrame");
            if (arena != null)
            {
                shots.Add(("arena_frame", arena.transform.position, arena.transform.eulerAngles, L5Greybox.ArenaFov));
            }

            shots.Add(("vista_nave", new Vector3(54f, 7f, 270f), new Vector3(14f, 0f, 0f), 45f));
            shots.Add(("reveal", new Vector3(54f, Hall + 1.6f, 409.5f), new Vector3(-2f, 0f, 0f), 50f));
            shots.Add(("throne", new Vector3(54f, Hall + 9f, 452f), new Vector3(32f, 0f, 0f), 50f));
            var player = GameObject.Find("Player");
            var saved = player != null ? player.transform.position : Vector3.zero;
            foreach (var (name, pos, euler, fov) in shots)
            {
                if (player != null && name != "arena_frame" && euler.x > 60f)
                {
                    player.transform.position = pos + Quaternion.Euler(euler) * Vector3.forward * 8.5f - Vector3.up;
                }

                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
                cam.fieldOfView = fov;
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
