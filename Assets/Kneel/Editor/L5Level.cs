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
        private const float Hall = L5Greybox.Hall;

        // Heights of the shell. The follow camera rides 8.7 m over the player, so every chandelier and every
        // vault springs above that; the fixed arena camera sees through the one-sided vaults.
        private const float NaveTop = 11f, NaveRise = 8.6f;
        private const float StairTop = 20f, StairRise = 22f;
        // The aisle ceilings sit above the arcade arches' crowns (Hall + 14.5), or the arches showed open air over them.
        private const float HallTop = Hall + 16f, Capital = Hall + 10f, ArcadeTop = Hall + 15.5f, AisleCeiling = Hall + 15f, HallRise = 16f;

        private static Material Stone => L4Build.Mat("L4_Knights_Stone");
        private static Material Soot => L4Build.Mat("L4_Knights_Soot");
        private static Material Iron => L4Build.Mat("L4_Iron");
        private static Material Bronze => L4Build.Mat("L4_Knights_Bronze");
        private static Material Basalt => L4Build.Mat("L4_Basalt");
        private static Material Blocks => L4Kit.Stone;
        private static Material Carpet => carpet != null ? carpet : (carpet = CarpetMaterial());
        private static Material carpet;
        private static Material Tapestry => Flat("L5_TapestryBurnt", "#2E0F11", 0.05f);
        private static Material Marble => Flat("L5_KingMarble", "#77726B", 0.25f);
        // The fixtures' iron and wax take only their own gentle glow light (FixtureGlow): warm, never past the bloom
        // threshold, so the thin parts can't shimmer.
        private static Material Wax => Glow(Flat("L5_Wax", "#7A705E", 0.2f), "#3A2010");
        private static Material FixtureIron => Glow(Flat("L5_FixtureIron", "#6A5440", 0.2f), "#1A0D06");

        // The master's colour, a shade darker than L4's banner red (owner, 2026-10-23: carpets and banners "a slightly
        // darker red").
        private static Material BannerDark => Flat("L5_BannerRed", "#3D1216", 0.1f);
        private static Material Bone => Flat("L5_Bone", "#857B6A", 0.15f);
        private static Material WindowDark => Flat("L5_WindowDark", "#060505", 0.3f);
        private static Material StatueStone => Flat("L5_StatueStone", "#4E4A45", 0.22f);

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
            carpet = null;
            sootMesh = null;
            sootMaterial = null;
            L2Build.EnsureFolder(TexturesPath);
            L2Build.EnsureFolder(MeshesPath + "/Floors");
            L2Build.EnsureFolder(MeshesPath + "/Shell");
            HideGreybox(root);

            var art = Group(root, "Art");
            wallCores = new ShellMesh();
            log.Add(Floors(Group(art, "Floors")));
            log.Add(Walls(Group(art, "Walls"), root));
            Nave(Group(art, "Nave"), root);
            Chapel(Group(art, "Chapel"), root);
            Alcove(Group(art, "Alcove"), root);
            Stair(Group(art, "Stair"), root);
            // The outer doors are shut behind the player: a collider across them, and the portal fades when the follow
            // camera, as the player walks back to them, ends up behind it (as the hall's pillars do).
            var outerDoors = Group(art, "OuterDoors");
            Portal(outerDoors, L5Greybox.P(54f, 268.6f, 0f), NaveTop, 801, false, new[] { 1f }, true);
            Obstacle(root, L5Greybox.P(54f, 268.6f, 0f), new Vector3(8.4f, 7f, 0.6f));
            foreach (var (x0, x1) in new[] { (45f, 48.6f), (59.4f, 63f) })
            {
                wallCores.Box(0, new Vector3((x0 + x1) * 0.5f, NaveTop * 0.5f, 268.15f), Quaternion.identity, new Vector3(x1 - x0 - 0.08f, NaveTop - 0.04f, 2.62f), L4Kit.Cell(L2PolyKit.Joint));
            }

            // The torches' soot plumes stay out of the fade (a faded multiply material makes no sense).
            var outerSoot = Group(art, "OuterDoorSoot");
            foreach (var t in outerDoors.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Soot")
                {
                    t.SetParent(outerSoot, true);
                }
            }

            L4Build.BakeLooseMeshes(outerDoors.gameObject, "L5_OuterDoors", MeshesPath + "/Kit");
            L4Build.AddFade(outerDoors.gameObject);
            Portal(Group(art, "GrandDoors"), L5Greybox.P(54f, 407.1f, Hall), 13f, 811, true, new[] { -1f, 1f });
            ThroneHall(Group(art, "Hall"));
            Dais(Group(art, "Dais"), root);
            Phases(root);
            Figures(Group(art, "Figures"), root);
            Atmosphere(Group(art, "Atmosphere"));
            Wear(art);
            L3Build.MeshChild(art.Find("Walls"), "WallCores", wallCores.Save("L5_WallCores", 1), Vector3.zero, Blocks);
            wallCores = null;

            foreach (Transform area in art)
            {
                GatherFixtures(area);
            }

            L4Build.BakeBoundaries.Add(FixturesGroup);
            try
            {
                foreach (Transform area in art)
                {
                    L4Build.BakeLooseMeshes(area.gameObject, "L5_" + area.name, MeshesPath + "/Kit");
                }
            }
            finally
            {
                L4Build.BakeBoundaries.Remove(FixturesGroup);
            }

            log.Add(FixtureLayer(art));

            foreach (var phase in new[] { "Phase1", "Phase2" })
            {
                var phaseArt = root.Find(phase + "/Art");
                if (phaseArt != null)
                {
                    L4Build.BakeLooseMeshes(phaseArt.gameObject, "L5_" + phase, MeshesPath + "/Kit");
                }
            }

            // Banners, trophy shields and the throne's cushion in L5's darker red (L4's material stays L4's).
            var l4Red = L4Citadel.BannerRed;
            var darkRed = BannerDark;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == l4Red)
                    {
                        mats[i] = darkRed;
                        changed = true;
                    }
                }

                if (changed)
                {
                    r.sharedMaterials = mats;
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
            ("Nave", 45f, 63f, 268f, 358.5f, 0f, false),
            ("D1", 30f, 45f, 301.5f, 319.5f, 0f, false),
            ("D2", 63f, 72f, 331.5f, 340.5f, 0f, false),
            ("StairHall", 33f, 75f, 358.5f, 406.5f, 0f, false),
            ("Landing", 47f, 61f, 394f, 402f, Hall, false),
            ("Gallery", 33f, 75f, 402f, 406.5f, Hall, false),
            ("Hall", 31.5f, 76.5f, 406.5f, 474f, Hall, true),
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

                    // Neglect: a few old dark stains where something bled or burned, and hairline cracks in the flags.
                    float blot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.8f, Mathf.PerlinNoise(p.x * 0.21f + seed * 0.37f, p.y * 0.21f + 11f)));
                    c = Color.Lerp(c, new Color(0.17f, 0.09f, 0.08f), 0.5f * blot * (0.6f + 0.4f * Mathf.PerlinNoise(p.x * 1.4f, p.y * 1.4f + seed)));
                    float crack = Mathf.Abs(Mathf.PerlinNoise(p.x * 0.55f + 70f, p.y * 0.55f + seed) - 0.5f);
                    c = Color.Lerp(c, soot, 0.45f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.012f, 0.004f, crack)));

                    if (id == "Nave")
                    {
                        // A dark inlaid border either side of the runner.
                        float d = Mathf.Min(Mathf.Abs(p.x - 51.05f), Mathf.Abs(p.x - 56.95f));
                        c = Color.Lerp(c, soot * 1.2f, 0.75f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f, 0.12f, d)));
                    }

                    if (id == "StairHall")
                    {
                        // The inlaid rose round the centrepiece: an eight-pointed star of pale stone laid into the flags
                        // like the marble of a palace floor. (Its two dark rings went, owner 2026-10-23.)
                        var q = p - new Vector2(L5Greybox.Centrepiece.x, L5Greybox.Centrepiece.z);
                        float rad = q.magnitude, phi = Mathf.Atan2(q.y, q.x);
                        float star = 3.3f + 3f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(8f * phi), 3f);
                        if (rad > 3.3f && rad < star)
                        {
                            c = Color.Lerp(c, new Color(0.6f, 0.58f, 0.54f), 0.65f);
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
                // The niche's cheeks under the landing are coursed by Stair(); clad here, they rose through the landing.
                if (t.IsChildOf(g) || !(t.name.Contains("Wall") || t.name.StartsWith("Apse_")) || t.name.StartsWith("OuterDoorWall") || t.name.StartsWith("NicheWall") || t.GetComponent<MeshRenderer>() == null)
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

        private static ShellMesh wallCores;

        private static int Clad(Transform g, float x0, float x1, float y0, float y1, float h0, float h1)
        {
            wallCores?.Box(0, new Vector3((x0 + x1) * 0.5f, (h0 + h1) * 0.5f - 0.02f, (y0 + y1) * 0.5f), Quaternion.identity, new Vector3(x1 - x0 - 0.08f, h1 - h0 - 0.04f, y1 - y0 - 0.08f), L4Kit.Cell(L2PolyKit.Joint));
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

        // The nave as the castle's hall of honour: a stone knight kneels on a plinth in every bay, sword planted before
        // him, turned toward the stair; iron candelabras flank the runner; shields of the master's crimson on crossed
        // swords hang over the torches on the pilasters.
        private static void Nave(Transform g, Transform root)
        {
            foreach (float y in NavePilasters)
            {
                if (y < 306f || y > 315f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, NaveTop, 1.1f), new Vector2(46.8f, y), 0f, Stone);
                    Sconce(g, new Vector3(47.1f, 0f, y), Vector3.right, 3.5f);
                    Trophy(g, new Vector3(47.1f, 0f, y), Vector3.right, 6.4f);
                }

                if (y < 333f || y > 339f)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Pillar_01", 90, new Vector3(0.6f, NaveTop, 1.1f), new Vector2(61.2f, y), 0f, Stone);
                    Sconce(g, new Vector3(60.9f, 0f, y), Vector3.left, 3.5f);
                    Trophy(g, new Vector3(60.9f, 0f, y), Vector3.left, 6.4f);
                }
            }

            for (float y = 279f; y < 356f; y += 9f)
            {
                if (y < 305f || y > 316f)
                {
                    BlindLancet(g, new Vector3(46.52f, 7.4f, y), -90f, 1.3f, 3.8f);
                }

                if (y < 333f || y > 339f)
                {
                    var banner = L3Build.Part(g, rng.NextDouble() < 0.5 ? "K:SM_Prop_Banner_02" : "K:SM_Prop_Banner_03", new Vector3(61.4f, 9.6f, y), new Vector3(0f, 90f, ((float)rng.NextDouble() - 0.5f) * 4f), Vector3.one * 1.4f, L4Citadel.BannerRed);
                    banner.name = "Banner";
                }
            }

            Vault(g, "NaveVault", 46.5f, 61.5f, 268f, 358.5f, NaveTop, NaveRise, NavePilasters, true, true, 7101);
            foreach (float y in new[] { 281f, 293f, 305f, 317f, 329f, 341f, 353f })
            {
                Chandelier(g, new Vector3(54f, 9.8f, y), 1.4f, 10, VaultHeight(46.5f, 61.5f, NaveTop, NaveRise, 54f), 0f, false);
            }

            var knight = Bake("L5_Kneeling_Knight_02_Black", KneelPose).mesh;
            for (float y = 279f; y < 356f; y += 9f)
            {
                KneelingStatue(g, root, new Vector3(48.5f, 0f, y), 60f, knight, StatueStone, 1.3f);
                KneelingStatue(g, root, new Vector3(59.5f, 0f, y), -60f, knight, StatueStone, 1.3f);
            }

            foreach (float y in new[] { 284f, 302f, 320f, 338f, 356f })
            {
                Candelabra(g, root, new Vector3(50.3f, 0f, y));
            }

            foreach (float y in new[] { 293f, 311f, 329f, 347f })
            {
                Candelabra(g, root, new Vector3(57.7f, 0f, y));
            }

            Runner(g, Rect.MinMaxRect(51.5f, 270f, 56.5f, 375.6f), 0f, false, 911);
        }

        // A knight in stone kneeling on a plinth, his sword planted point-down before him, candles at its foot.
        private static void KneelingStatue(Transform g, Transform root, Vector3 at, float yaw, Mesh mesh, Material mat, float scale, bool plinth = true)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var forward = rot * Vector3.forward;
            float top = plinth ? 0.55f : 0f;
            if (plinth)
            {
                var block = L3Build.MeshChild(g, "Plinth", L2PolyKit.Tread(new Vector3(1.8f, 0.55f, 1.8f), rng.Next(100000)), at + Vector3.up * 0.275f, Blocks);
                block.transform.rotation = rot;
                Obstacle(root, at, new Vector3(2f, 2.7f, 2f));
            }

            var statue = L3Build.MeshChild(g, "KneelingKnight", mesh, at + Vector3.up * (top - mesh.bounds.min.y * scale), mat);
            statue.transform.rotation = rot;
            statue.transform.localScale = Vector3.one * scale;
            var sword = L3Build.Seat(g, "K:SM_Wep_Broadsword_01", new Vector3(180f, yaw, 0f), 1.15f, new Vector2((at + forward * 0.62f * scale).x, (at + forward * 0.62f * scale).z), at.y + top, mat);
            sword.name = "PlantedSword";
            if (plinth)
            {
                Candles(g, at + forward * 1.3f, 3, 1);
            }
        }

        // An iron floor candelabra: base, shaft, three crossed arms with a candle at each end and one at the crown,
        // one light for the lot.
        private static void Candelabra(Transform g, Transform root, Vector3 at)
        {
            var c = Group(g, "Candelabra");
            c.position = at;
            int seed = rng.Next(100000);
            // The foot stands outside the fixture (FixtureLayer): lit from 2.4 m above it reads as iron, not a black hole.
            Part(g, L2PolyKit.Tread(new Vector3(0.55f, 0.12f, 0.55f), seed), at + new Vector3(0f, 0.06f, 0f), Quaternion.identity);
            Part(c, L2PolyKit.Tread(new Vector3(0.09f, 2.1f, 0.09f), seed + 1), new Vector3(0f, 1.17f, 0f), Quaternion.identity);
            Part(c, L2PolyKit.Tread(new Vector3(0.2f, 0.08f, 0.2f), seed + 2), new Vector3(0f, 2.22f, 0f), Quaternion.identity);
            for (int k = 0; k < 3; k++)
            {
                Part(c, L2PolyKit.Tread(new Vector3(0.95f, 0.05f, 0.05f), seed + 3 + k), new Vector3(0f, 2.24f, 0f), Quaternion.Euler(0f, k * 60f, 0f));
            }

            for (int k = 0; k <= 6; k++)
            {
                var p = k == 6 ? new Vector3(0f, 2.3f, 0f) : new Vector3(Mathf.Cos(k * Mathf.PI / 3f) * 0.46f, 2.27f, Mathf.Sin(k * Mathf.PI / 3f) * 0.46f);
                float h = k == 6 ? 0.3f : 0.22f;
                L3Build.MeshChild(c, "Candle", L2PolyKit.Tread(new Vector3(0.06f, h, 0.06f), seed + 10 + k), p + Vector3.up * h * 0.5f, Wax);
                L2Fire.Candle(c, "Flame", p + Vector3.up * (h + 0.04f), 0.01f, 0.1f);
            }

            StripLights(c);
            var light = new GameObject("Light");
            light.transform.SetParent(c, false);
            light.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            var l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ChandelierColour;
            // A pool of candlelight round its foot, about 4 m across (owner, 2026-10-23: "there is no light source coming
            // out of the chandeliers and candelabras"), and a soft steady halo round the crown.
            l.intensity = 24f;
            l.range = 7f;
            l.shadows = LightShadows.None;
            FixtureGlow(c, new Vector3(0f, 4.8f, 0f), 2.5f);
            Halo(c, new Vector3(0f, 2.45f, 0f), 2.2f);
            Obstacle(root, at, new Vector3(0.55f, 2.4f, 0.55f));
        }

        // A shield in the master's crimson over two crossed swords, hung on a face above its torch.
        private static void Trophy(Transform g, Vector3 wall, Vector3 outward, float height)
        {
            var t = Group(g, "Trophy");
            t.SetPositionAndRotation(wall + Vector3.up * height + outward * 0.14f, Quaternion.LookRotation(outward));
            foreach (float s in new[] { -1f, 1f })
            {
                var arm = Group(t, "Sword");
                arm.localPosition = new Vector3(0f, 0f, -0.06f);
                arm.localRotation = Quaternion.Euler(0f, 0f, s * 38f);
                L3Build.Seat(arm, "K:SM_Wep_Broadsword_01", Vector3.zero, 1.1f, Vector2.zero, -0.95f, Iron);
            }

            L3Build.Seat(t, "K:SM_Wep_Shield_01", new Vector3(0f, 180f, 0f), 1.5f, Vector2.zero, -0.55f, L4Citadel.BannerRed);
        }

        // Removes every light (and what depends on it) under a piece, before it gets its one light of its own.
        private static void StripLights(Transform t)
        {
            foreach (var l in t.GetComponentsInChildren<Light>(true))
            {
                foreach (var dependant in l.GetComponents<MonoBehaviour>())
                {
                    Object.DestroyImmediate(dependant);
                }

                Object.DestroyImmediate(l);
            }
        }

        // A side room's doorway in a nave wall (owner, 2026-10-23: "make the holes to the secret rooms more beautiful.
        // I want arch and everything. They should still be small"): a round arch of voussoirs 2.4 m wide, its crown at
        // 3.6 m, on coursed piers that fill the greybox's 3 m gap, the haunches coursed up to the wall top so no slot
        // is left over it. The piers get colliders. 'wallX' is the wall's centre line, 'y' the doorway's centre.
        private static void SideDoor(Transform g, Transform root, float wallX, float y, int seed)
        {
            var door = Group(g, "Doorway");
            door.position = new Vector3(wallX, 0f, y);
            door.rotation = Quaternion.Euler(0f, 90f, 0f);
            L4Kit.GateArch(door, 2.4f, 0.3f, 2.4f, 1.2f, NaveTop, 1.5f, seed);
            foreach (float s in new[] { -1f, 1f })
            {
                Obstacle(root, new Vector3(wallX, 0f, y + s * 1.35f), new Vector3(1.5f, 2.4f, 0.3f));
            }
        }

        // ---------------------------------------------------------------- D1, the Master's private chapel

        private static void Chapel(Transform g, Transform root)
        {
            SideDoor(g, root, 45.75f, 310.5f, 9301);
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

        private static void Alcove(Transform g, Transform root)
        {
            // The arched doorway, the tapestry hung in it from the springing (the dark of the alcove shows over it), and
            // the scorched handprint on the jamb beside it (the game's one tell).
            SideDoor(g, root, 62.25f, 336f, 9302);
            L3Build.Fit(g, "K:SM_Prop_Banner_01", 90, new Vector3(0.12f, 2.3f, 2.3f), new Vector2(61.9f, 336f), 0.1f, Tapestry);
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

        // The double horseshoe stair (owner's reference, 2026-10-23: "the same shape... but much more grand"): two
        // curved flights of 36 steps rise 9 m from the floor either side of the hall and sweep in to meet on a landing
        // before the doors. Each flight is massive coursed stone with a gold-edged crimson runner and wrought-iron scroll
        // balustrades both sides, torch-topped newels at its foot. The landing bridges an arched niche where the old King
        // stands in marble; a gallery runs along the back wall; a great brazier on an inlaid rose fills the well.
        private static void Stair(Transform g, Transform root)
        {
            var rails = new ShellMesh();
            foreach (bool west in new[] { true, false })
            {
                CurvedFlight(g, root, rails, west);
            }

            var r = new System.Random(3010);
            // The landing's front: the niche's arch, coursed fascia either side, coursed sides; the gallery's face.
            var arch = Group(g, "NicheArch");
            arch.position = new Vector3(54f, 0f, 394f);
            L4Kit.GateArch(arch, 7f, 0.6f, 4.2f, 3.2f, Hall, 1.2f, 3201);
            L4Kit.Courses(g, new Vector3(47f, 0f, 393.4f), Vector3.right, Vector3.back, 2.9f, Hall, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(58.1f, 0f, 393.4f), Vector3.right, Vector3.back, 2.9f, Hall, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(47f, 0f, 402f), Vector3.back, Vector3.left, 8f, Hall, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(61f, 0f, 394f), Vector3.forward, Vector3.right, 8f, Hall, r, 0f, 0.6f, 2f);
            Clad(g, 34.5f, 47f, 402f, 403.2f, 0f, Hall);
            Clad(g, 61f, 73.5f, 402f, 403.2f, 0f, Hall);

            // Solid cores for the landing block (its cheeks either side of the niche, the mass over the niche's ceiling)
            // and the gallery under its floor, so no joint between their faces opens onto a hollow.
            foreach (var (x0, x1, y0, y1, h0) in new[] { (47f, 50.5f, 394f, 402f, 0f), (57.5f, 61f, 394f, 402f, 0f), (50.5f, 57.5f, 394f, 402f, Hall - 1.4f), (34.5f, 73.5f, 402f, 406.5f, 0f) })
            {
                float coreFoot = h0 + 0.04f, coreTop = Hall - 0.04f;
                wallCores?.Box(0, new Vector3((x0 + x1) * 0.5f, (coreFoot + coreTop) * 0.5f, (y0 + y1) * 0.5f), Quaternion.identity, new Vector3(x1 - x0 - 0.08f, coreTop - coreFoot, y1 - y0 - 0.08f), L4Kit.Cell(L2PolyKit.Joint));
            }

            // The niche: coursed walls, its low ceiling, the old King in marble on his plinth, candles, two torches.
            L4Kit.Courses(g, new Vector3(50.5f, 0f, 402f), Vector3.right, Vector3.back, 7f, Hall - 1.4f, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(50.5f, 0f, 394f), Vector3.forward, Vector3.right, 8f, Hall - 1.4f, r, 0f, 0.6f, 2f);
            L4Kit.Courses(g, new Vector3(57.5f, 0f, 402f), Vector3.back, Vector3.left, 8f, Hall - 1.4f, r, 0f, 0.6f, 2f);
            Ceiling(g, "NicheCeiling", 50.5f, 57.5f, 394f, 402f, Hall - 1.4f, 7302);
            var king = Group(g, "KingStatue");
            king.position = new Vector3(54f, 0f, 399.2f);
            L3Build.Seat(king, "K:SM_Prop_Statue_Base_01", new Vector3(0f, 180f, 0f), 1.6f, Vector2.zero, 0f, Stone);
            L3Build.Seat(king, "K:SM_Prop_Statue_01", new Vector3(0f, 180f, 0f), 1.25f, Vector2.zero, 1.6f, Marble);
            Candles(g, new Vector3(52.2f, 0f, 397.2f), 6, 1);
            Candles(g, new Vector3(55.8f, 0f, 397.2f), 6, 1);
            Sconce(g, new Vector3(50.5f, 0f, 397.5f), Vector3.right, 3.2f);
            Sconce(g, new Vector3(57.5f, 0f, 397.5f), Vector3.left, 3.2f);

            // The landing's and the gallery's rails.
            IronRail(rails, Straight(new Vector3(49.6f, Hall, 394.12f), new Vector3(58.4f, Hall, 394.12f)));
            IronRail(rails, Straight(new Vector3(47.12f, Hall, 398.8f), new Vector3(47.12f, Hall, 402f)));
            IronRail(rails, Straight(new Vector3(60.88f, Hall, 398.8f), new Vector3(60.88f, Hall, 402f)));
            IronRail(rails, Straight(new Vector3(34.6f, Hall, 402.12f), new Vector3(47f, Hall, 402.12f)));
            IronRail(rails, Straight(new Vector3(61f, Hall, 402.12f), new Vector3(73.4f, Hall, 402.12f)));
            L3Build.MeshChild(g, "IronBalustrades", rails.Save("L5_IronBalustrades", 1), Vector3.zero, Iron);

            Centrepiece(g);

            // Runners: across the landing from flight to flight, and on to the doors.
            Runner(g, Rect.MinMaxRect(48.6f, 395.4f, 59.4f, 397.0f), Hall, false, 3115);
            Runner(g, Rect.MinMaxRect(51f, 397.0f, 57f, 406.5f), Hall, false, 3116);

            // The gallery's back wall: blind arched doors either side of the grand doors, torches between.
            foreach (float x in new[] { 40.5f, 67.5f })
            {
                var blind = Group(g, "BlindDoor");
                blind.position = new Vector3(x, Hall, 406.4f);
                L4Kit.GateArch(blind, 3.2f, 0.45f, 3.4f, 1.6f, 5.8f, 0.5f, 3300 + (int)x);
                L3Build.Fit(blind, "K:SM_Bld_Castle_Door_01", 0, new Vector3(3.2f, 3.4f, 0.3f), new Vector2(0f, 0.05f), 0f, Soot);
            }

            foreach (float x in new[] { 36.5f, 44.5f, 63.5f, 71.5f })
            {
                Sconce(g, new Vector3(x, Hall, 406.5f), Vector3.back, 3.5f);
            }

            // Torches down the side walls, climbing with the flights beside them; banners above.
            foreach (float y in new[] { 368f, 376f, 384f, 392f, 399f })
            {
                float h = FlightHeightBeside(y) + 3.2f;
                Sconce(g, new Vector3(34.5f, 0f, y), Vector3.right, h);
                Sconce(g, new Vector3(73.5f, 0f, y), Vector3.left, h);
            }

            foreach (float y in new[] { 372f, 384f, 396f })
            {
                L4Citadel.HangBanner(g, new Vector3(34.6f, 17f, y), -90f, 2.8f, (int)y);
                L4Citadel.HangBanner(g, new Vector3(73.4f, 17f, y), 90f, 2.8f, (int)y + 1);
            }

            // The great chandelier over the well, one over the landing, the vault over all.
            float crown = VaultHeight(34.5f, 73.5f, StairTop, StairRise, 54f);
            Chandelier(g, new Vector3(54f, 18.6f, 380f), 3.4f, 22, crown, 0f, true);
            Chandelier(g, new Vector3(39.5f, 19.2f, 384f), 1.9f, 12, crown, 4.5f, false);
            Chandelier(g, new Vector3(68.5f, 19.2f, 384f), 1.9f, 12, crown, 4.5f, false);
            Chandelier(g, new Vector3(54f, Hall + 9.6f, 400.5f), 1.8f, 12, crown, Hall, false);
            Vault(g, "StairVault", 34.5f, 73.5f, 358.5f, 406.5f, StairTop, StairRise, new[] { 366f, 378f, 390f, 402f }, true, true, 7301);
        }

        // The height of a flight's outer edge where it passes the side wall at plan y (both flights are mirror images).
        private static float FlightHeightBeside(float y)
        {
            float s = Mathf.Clamp((y - L5Greybox.StairCentre.y) / L5Greybox.StairOuter, -1f, 1f);
            float a = Mathf.Clamp(180f - Mathf.Asin(s) * Mathf.Rad2Deg, L5Greybox.StairTopFrom, L5Greybox.StairFrom);
            return Hall * (L5Greybox.StairFrom - a) / (L5Greybox.StairFrom - L5Greybox.StairTopFrom) * (L5Greybox.StairSteps - 1) / L5Greybox.StairSteps;
        }

        // One flight: its steps (three courses of worn blocks across, each from the floor up), its runner over every
        // tread and riser, its rails, and a torch-topped newel either side of its foot.
        private static void CurvedFlight(Transform g, Transform root, ShellMesh rails, bool west)
        {
            var flight = Group(g, west ? "Flight_W" : "Flight_E");
            var r = new System.Random(west ? 3001 : 3002);
            var runner = new ShellMesh();
            var stone = new ShellMesh();
            var inner = new List<Vector3>();
            var outer = new List<Vector3>();
            float down = west ? 1f : -1f;
            for (int i = 0; i <= L5Greybox.StairSteps; i++)
            {
                float a = L5Greybox.StairAngle(i, west);
                float nose = Hall * (i + 0.5f) / L5Greybox.StairSteps;
                inner.Add(L5Greybox.StairPoint(L5Greybox.StairInner + 0.12f, a, Mathf.Min(nose, Hall)));
                outer.Add(L5Greybox.StairPoint(L5Greybox.StairOuter - 0.12f, a, Mathf.Min(nose, Hall)));
                if (i == L5Greybox.StairSteps)
                {
                    break;
                }

                float a1 = L5Greybox.StairAngle(i + 1, west), mid = (a + a1) * 0.5f, top = L5Greybox.StairTop(i);
                bool last = i == L5Greybox.StairSteps - 1;

                // The top tread runs onto the landing's corner: it sits just under the landing slab and its runner just
                // over the landing's runner, so neither z-fights.
                SectorStep(stone, a, a1, i == 0 ? 0f : L5Greybox.StairTop(i - 1), last ? top - 0.012f : top, last, west, r);

                // The runner: tread (in two parts along the arc) and riser, gold-edged.
                float lift = top + (last ? 0.024f : 0.015f), below = i == 0 ? 0f : L5Greybox.StairTop(i - 1);
                var facing = (L5Greybox.StairPoint(15f, a + down, 0f) - L5Greybox.StairPoint(15f, a, 0f)).normalized;
                foreach (var (r0, r1, sub) in new[] { (14.2f, 15.8f, 0), (14.05f, 14.2f, 1), (15.8f, 15.95f, 1) })
                {
                    // The weave's v is the carpet's own length from the foot: along each tread, then up each riser.
                    float u0 = (r0 - 14.2f) / 1.6f, u1 = (r1 - 14.2f) / 1.6f;
                    float CarpetV(float deg, float h) => (Mathf.Abs(deg - L5Greybox.StairAngle(0, west)) * Mathf.Deg2Rad * 15f + h) / 1.6f;
                    for (int k = 0; k < 2; k++)
                    {
                        float b0 = Mathf.Lerp(a, a1, k * 0.5f), b1 = Mathf.Lerp(a, a1, (k + 1) * 0.5f);
                        runner.Quad(sub, L5Greybox.StairPoint(r0, b0, lift), L5Greybox.StairPoint(r1, b0, lift), L5Greybox.StairPoint(r1, b1, lift), L5Greybox.StairPoint(r0, b1, lift),
                            new Vector2(u0, CarpetV(b0, top)), new Vector2(u1, CarpetV(b0, top)), new Vector2(u1, CarpetV(b1, top)), new Vector2(u0, CarpetV(b1, top)), Vector3.up);
                    }

                    runner.Quad(sub, L5Greybox.StairPoint(r0, a, below) + facing * 0.012f, L5Greybox.StairPoint(r1, a, below) + facing * 0.012f, L5Greybox.StairPoint(r1, a, lift) + facing * 0.012f, L5Greybox.StairPoint(r0, a, lift) + facing * 0.012f,
                        new Vector2(u0, CarpetV(a, below)), new Vector2(u1, CarpetV(a, below)), new Vector2(u1, CarpetV(a, top)), new Vector2(u0, CarpetV(a, top)), facing);
                }
            }

            L3Build.MeshChild(flight, "Steps", stone.Save("L5_FlightSteps_" + (west ? "W" : "E"), 1), Vector3.zero, Blocks);
            var carpet = L3Build.MeshChild(flight, "Runner", runner.Save("L5_FlightRunner_" + (west ? "W" : "E"), 2), Vector3.zero, Carpet, Bronze);
            carpet.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            IronRail(rails, inner);
            IronRail(rails, outer);

            float foot = L5Greybox.StairAngle(0, west) + (west ? 2.5f : -2.5f);
            StandingTorch(g, root, L5Greybox.StairPoint(L5Greybox.StairInner + 0.3f, foot, 0f));
            StandingTorch(g, root, L5Greybox.StairPoint(L5Greybox.StairOuter - 0.3f, foot, 0f));
        }

        // One step of a curved flight as solid stone between angles a and a1: its tread laid in slabs (three across,
        // two along, each a palette tone, over a dark joint bed so the joints read), its riser, and its curved cheeks
        // down to the floor inside and out, so the flight is one closed mass with no slits between steps.
        private static void SectorStep(ShellMesh m, float a, float a1, float below, float top, bool last, bool west, System.Random r)
        {
            float r0 = L5Greybox.StairInner, r1 = L5Greybox.StairOuter;
            Vector3 P(float radius, float deg, float h) => L5Greybox.StairPoint(radius, deg, h);
            int[] tones = { L2PolyKit.Top, L2PolyKit.TopLight, L2PolyKit.Top, L2PolyKit.TopDark };
            m.Quad(0, P(r0, a, top - 0.006f), P(r1, a, top - 0.006f), P(r1, a1, top - 0.006f), P(r0, a1, top - 0.006f), L4Kit.Cell(L2PolyKit.Joint), Vector3.up);
            const float joint = 0.025f;
            float da = (a1 - a) * 0.5f, jointDeg = joint / ((r0 + r1) * 0.5f) * Mathf.Rad2Deg * Mathf.Sign(da);
            for (int k = 0; k < 3; k++)
            {
                float ra = r0 + k * 2f + joint, rb = r0 + (k + 1) * 2f - joint;
                for (int h = 0; h < 2; h++)
                {
                    float b0 = a + h * da + jointDeg, b1 = a + (h + 1) * da - jointDeg;
                    m.Quad(0, P(ra, b0, top), P(rb, b0, top), P(rb, b1, top), P(ra, b1, top), L4Kit.Cell(tones[r.Next(tones.Length)]), Vector3.up);
                }
            }

            var down = (P(15f, a + (west ? 1f : -1f), 0f) - P(15f, a, 0f)).normalized;
            m.Quad(0, P(r0, a, 0f), P(r1, a, 0f), P(r1, a, top), P(r0, a, top), L4Kit.Cell(L2PolyKit.Side), down);
            if (last)
            {
                m.Quad(0, P(r0, a1, 0f), P(r1, a1, 0f), P(r1, a1, top), P(r0, a1, top), L4Kit.Cell(L2PolyKit.Side), -down);
            }

            for (int h = 0; h < 2; h++)
            {
                float b0 = a + h * da, b1 = a + (h + 1) * da, mid = (b0 + b1) * 0.5f;
                var outward = (P(1f, mid, 0f) - P(0f, mid, 0f)).normalized;
                int cell = (h + (int)(top * 4f)) % 2 == 0 ? L2PolyKit.Side : L2PolyKit.End;
                m.Quad(0, P(r1, b0, 0f), P(r1, b1, 0f), P(r1, b1, top), P(r1, b0, top), L4Kit.Cell(cell), outward);
                m.Quad(0, P(r0, b0, 0f), P(r0, b1, 0f), P(r0, b1, top), P(r0, b0, top), L4Kit.Cell(cell), -outward);
            }
        }

        // The well's centrepiece: a round plinth in two tiers, a great stone fire bowl on a pedestal burning on it (the
        // stair hall's heart of light), candles round its foot.
        private static void Centrepiece(Transform g)
        {
            var c = Group(g, "Centrepiece");
            c.position = L5Greybox.Centrepiece;
            var r = new System.Random(3401);
            foreach (var (radius, h0, n) in new[] { (2.9f, 0f, 16), (2.1f, 0.45f, 12) })
            {
                float seg = 2f * Mathf.PI * radius / n;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) / n * Mathf.PI * 2f;
                    var block = L3Build.MeshChild(c, "Block", L2PolyKit.Tread(new Vector3(seg + 0.05f, 0.45f, 0.9f), r.Next(100000)), new Vector3(Mathf.Cos(a) * radius, h0 + 0.225f, Mathf.Sin(a) * radius), Blocks);
                    block.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                }

                foreach (float yaw in new[] { 0f, 45f })
                {
                    var fill = L3Build.MeshChild(c, "Fill", L2PolyKit.Tread(new Vector3(radius * 1.5f, 0.44f, radius * 1.5f), r.Next(100000)), new Vector3(0f, h0 + 0.22f, 0f), Blocks);
                    fill.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }

            foreach (float yaw in new[] { 0f, 45f })
            {
                var pedestal = L3Build.MeshChild(c, "Pedestal", L2PolyKit.Tread(new Vector3(1.5f, 1.3f, 1.5f), r.Next(100000)), new Vector3(0f, 0.9f + 0.65f, 0f), Blocks);
                pedestal.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var floor = L3Build.MeshChild(c, "BowlFloor", L2PolyKit.Tread(new Vector3(2.5f, 0.25f, 2.5f), r.Next(100000)), new Vector3(0f, 2.3f, 0f), Blocks);
                floor.transform.localRotation = Quaternion.Euler(0f, yaw + 22.5f, 0f);
            }

            for (int i = 0; i < 12; i++)
            {
                float a = (i + 0.5f) / 12f * Mathf.PI * 2f;
                var lip = L3Build.MeshChild(c, "Bowl", L2PolyKit.Tread(new Vector3(0.82f, 0.6f, 0.36f), r.Next(100000)), new Vector3(Mathf.Cos(a) * 1.5f, 2.6f, Mathf.Sin(a) * 1.5f), Blocks);
                lip.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f) * Quaternion.Euler(-14f, 0f, 0f);
            }

            var fire = L2Fire.Fire(c, "Flames", new Vector3(0f, 2.55f, 0f), 1.4f, 60f, 28f, true, new Vector3(2f, 0f, 2f), L2Fire.Shape.Box, 1.3f);
            foreach (var l in fire.GetComponentsInChildren<Light>(true))
            {
                l.color = TorchColour;
            }

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Candles(g, c.position + new Vector3(Mathf.Cos(a) * 3.6f, 0f, Mathf.Sin(a) * 3.6f), 4, 1);
            }
        }

        // Points every 1.2 m along a straight run, for a rail.
        private static List<Vector3> Straight(Vector3 a, Vector3 b)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 1.2f));
            var points = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                points.Add(Vector3.Lerp(a, b, i / (float)n));
            }

            return points;
        }

        // A wrought-iron balustrade along points at walking height: a post at each point, top and bottom rails, and in
        // each panel two scroll rings and a bar between them.
        private static void IronRail(ShellMesh m, List<Vector3> points)
        {
            const float top = 1.02f;
            for (int k = 0; k + 1 < points.Count; k++)
            {
                var a = points[k];
                var b = points[k + 1];
                Bar(m, a + Vector3.up * top, b + Vector3.up * top, 0.075f);
                Bar(m, a + Vector3.up * 0.1f, b + Vector3.up * 0.1f, 0.045f);
                Bar(m, a, a + Vector3.up * (top + 0.06f), 0.06f);
                var along = b - a;
                along.y = 0f;
                along.Normalize();
                foreach (float s in new[] { 0.28f, 0.72f })
                {
                    var c = Vector3.Lerp(a, b, s) + Vector3.up * 0.56f;
                    const int segments = 10;
                    for (int i = 0; i < segments; i++)
                    {
                        float t0 = i / (float)segments * Mathf.PI * 2f, t1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                        Bar(m, c + (along * Mathf.Cos(t0) + Vector3.up * Mathf.Sin(t0)) * 0.17f, c + (along * Mathf.Cos(t1) + Vector3.up * Mathf.Sin(t1)) * 0.17f, 0.025f);
                    }
                }

                var middle = Vector3.Lerp(a, b, 0.5f);
                Bar(m, middle + Vector3.up * 0.1f, middle + Vector3.up * top, 0.03f);
            }

            var last = points[points.Count - 1];
            Bar(m, last, last + Vector3.up * (top + 0.06f), 0.06f);
        }

        private static void Bar(ShellMesh m, Vector3 p, Vector3 q, float thickness)
        {
            var d = q - p;
            if (d.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var rot = Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up);
            m.Box(0, (p + q) * 0.5f, rot, new Vector3(thickness, thickness, d.magnitude + thickness * 0.5f), L4Kit.Cell(L2PolyKit.Iron));
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
                float a0 = alongY ? r.xMin : r.yMin, v0 = (s0 - (alongY ? r.yMin : r.xMin)) / width, v1 = v0 + step / width;
                Vector2 UV(float across, float v) => new Vector2((across - a0) / width, v);
                m.Quad(0, P(s0, lo[j] + t0), P(s1, lo[j + 1] + t1), P(s1, hi[j + 1] - t1), P(s0, hi[j] - t0), UV(lo[j] + t0, v0), UV(lo[j + 1] + t1, v1), UV(hi[j + 1] - t1, v1), UV(hi[j] - t0, v0), Vector3.up);
            }

            var go = L3Build.MeshChild(g, "Runner", m.Save("L5_Runner_" + seed, 3), new Vector3(0f, floor, 0f), Carpet, Bronze, L2PolyKit.CharredWood);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- The doors

        // A portal in the manner of L4's throne doors: three recessed round-arched orders, the doors standing open,
        // swung back inside against the wall. Inside the castle it has no towers: torches either side of it on each
        // face, and (for the grand doors) great braziers on plinths before it. 'at' is the door line's centre on its
        // floor; the orders step out toward -Z.
        // 'closed' (the outer doors, owner 2026-10-23: "make sure the door is closed so that player is confined within
        // the level"): the leaves shut across the opening and a planked tympanum fills the arch over them.
        private static void Portal(Transform g, Vector3 at, float wall, int seed, bool braziers, float[] faces, bool closed = false)
        {
            var r = new System.Random(seed);
            g.position = at;
            for (int o = 0; o < 3; o++)
            {
                float span = 8.4f + o * 1.2f;
                var ring = L3Build.Child(g, "Order", new Vector3(0f, 0f, o == 0 ? 0f : -0.3f - o * 0.6f)).transform;
                L4Kit.GateArch(ring, span, 0.6f, 6.6f, span * 0.5f, wall, o == 0 ? 1.2f : 0.6f, seed + o);
            }

            if (closed)
            {
                // Planks from the floor to the arch behind the leaves (the pack's leaves are round-headed, so the
                // planks fill round them and make the tympanum over them), and a board behind the planks' joints.
                const float radius = 4.2f, plank = 0.42f;
                for (int i = 0; i < 20; i++)
                {
                    float x = -radius + (i + 0.5f) * plank;
                    float h = 6.6f + Mathf.Sqrt(radius * radius - x * x) + 0.1f;
                    L3Build.MeshChild(g, "Planks", L2PolyKit.Plank(new Vector3(plank - 0.02f, h, 0.14f), seed + 40 + i), new Vector3(x, h * 0.5f, -0.32f), L2PolyKit.CharredWood);
                }

                L3Build.MeshChild(g, "Board", L2PolyKit.Plank(new Vector3(8.4f, 10.8f, 0.08f), seed + 70), new Vector3(0f, 5.4f, -0.46f), L2PolyKit.CharredWood);
            }

            foreach (float s in new[] { -1f, 1f })
            {
                if (closed)
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Door_01", s < 0f ? 0 : 180, new Vector3(4.2f, 6.6f, 0.45f), new Vector2(s * 2.1f, 0f), 0f, Soot);
                }
                else
                {
                    L3Build.Fit(g, "K:SM_Bld_Castle_Door_01", 90, new Vector3(0.45f, 6.6f, 4.2f), new Vector2(s * 4.15f, 3.2f), 0f, Soot);
                }

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

                // The wall over each pillar, from its capital to the arcade top, between the two arches it carries
                // (the bays stop at the pillar's faces; this was open air up to the vault).
                foreach (float y in L5Greybox.PillarRows)
                {
                    var over = Group(arcades, "OverPillar");
                    over.position = new Vector3(x, Capital, y);
                    over.rotation = Quaternion.Euler(0f, 90f, 0f);
                    L4Kit.Courses(over, new Vector3(-1.55f, 0f, -0.6f), Vector3.right, Vector3.back, 3.1f, ArcadeTop - Capital, r, 0f, 1.2f);
                    L4Build.BakeLooseMeshes(over.gameObject, "L5_OverPillar_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(y), MeshesPath + "/Kit");
                    L4Build.AddFade(over.gameObject);
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

            // Iron candelabras down the processional and either side of the dais front (outside the duel ring).
            foreach (var (x, y) in new[] { (49.6f, 414f), (58.4f, 414f), (49.6f, 426f), (58.4f, 426f), (44.2f, 465.6f), (63.8f, 465.6f) })
            {
                Candelabra(g, g.parent.parent, new Vector3(x, Hall, y));
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

            // The old King in marble, kneeling at the foot of his own dais, his crown fallen before him.
            KneelingStatue(g, root, new Vector3(54f, y + 0.2f, 466.75f), 0f, Bake("L5_Kneeling_Knight_02_Black", KneelPose).mesh, Marble, 1.15f, false);
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

        // The PolygonKnights black knight baked in a pose, and the height of its hips in that pose.
        private static (Mesh mesh, float hipsY) Bake(string name, L1Bakers.PoseFn pose)
        {
            L2Build.EnsureFolder(MeshesPath + "/Figures");
            var mesh = L1Bakers.BakeStanding("Assets/SyntyStudios/PolygonKnights/Prefabs/Characters/Character_Knight_02_Black.prefab", "Character_Knight_02", pose, null, out var rig);
            float hips = 0f;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Hips")
                {
                    hips = t.position.y;
                }
            }

            Object.DestroyImmediate(rig);
            mesh.name = name;
            return (SaveMesh(mesh, MeshesPath + "/Figures"), hips);
        }

        private static (Mesh mesh, float hipsY) SeatedMaster() => Bake("L5_Seated_Knight_02_Black", SeatedPose);

        // Kneeling on the right knee, the left foot planted, back straight, both hands forward on a sword's pommel.
        private static void KneelPose(Transform r, float s)
        {
            var l = new Vector3(s, 0f, 0f);
            Aim(r, "UpperLeg_L", "LowerLeg_L", l * 0.12f + new Vector3(0f, -0.2f, 1f));
            Aim(r, "LowerLeg_L", "Ankle_L", new Vector3(0f, -1f, -0.05f));
            Aim(r, "UpperLeg_R", "LowerLeg_R", -l * 0.1f + new Vector3(0f, -1f, -0.12f));
            Aim(r, "LowerLeg_R", "Ankle_R", -l * 0.05f + new Vector3(0f, -0.12f, -1f));
            Aim(r, "Shoulder_L", "Elbow_L", l * 0.12f + new Vector3(0f, -0.75f, 0.7f));
            Aim(r, "Elbow_L", "Hand_L", -l * 0.55f + new Vector3(0f, -0.35f, 0.65f));
            Aim(r, "Shoulder_R", "Elbow_R", -l * 0.12f + new Vector3(0f, -0.75f, 0.7f));
            Aim(r, "Elbow_R", "Hand_R", l * 0.55f + new Vector3(0f, -0.35f, 0.65f));
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
            var guard = Prefab(L4Prefabs + "Enemies/L4_StandIn_IronHusk", g, L5Greybox.P(54f, 373.5f, 0f), 0f);
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
            L4Atmosphere.Ash(g, new Vector3(54f, 16f, 383f), new Vector3(36f, 2f, 44f));
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

            // The fires breathe slowly and only a little: L2Fire's flicker, made for single campfires, read as strobing
            // across a hundred torches.
            foreach (var flicker in root.GetComponentsInChildren<Kneel.Lighting.LightFlicker>(true))
            {
                var so = new SerializedObject(flicker);
                so.FindProperty("amplitude").floatValue = 0.06f;
                so.FindProperty("speed").floatValue = 0.8f;
                so.FindProperty("rangeBreath").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // The Fixtures layer (FixtureLayer) takes only the fixtures' gentle glow lights; every other light, the
            // candles' own included, lights the Default layer only, so nothing burns the thin iron and wax white.
            uint fixtures = FixturesMask();
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type != LightType.Directional)
                {
                    l.shadows = LightShadows.None;
                    uint mask = l.name == FixtureGlowName ? fixtures : 1u;
                    l.renderingLayerMask = (int)mask;
                    var data = l.GetComponent<UniversalAdditionalLightData>();
                    if (data == null)
                    {
                        data = l.gameObject.AddComponent<UniversalAdditionalLightData>();
                    }

                    data.renderingLayers = mask;
                }
            }

            int lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            return "interior lighting: " + lights + " lights, no sun";
        }

        private const float Exposure = 1.45f;

        // ---------------------------------------------------------------- Fixtures

        // Candelabras, chandeliers and floor candles (owner, 2026-10-23: "the lights still flicker whenever the player is
        // moving... specifically the candlelights, the chandeliers, and the candelabras"). Their lights hang a hand's
        // width from thin iron and wax, which burned near-white, and those 1-2 px bright lines shimmered with every step
        // of the camera. Each area's fixtures bake into one mesh of their own on the Fixtures rendering layer, which no
        // fire light reaches (Lighting sets every light to Default); their iron and wax glow faintly instead, steadily.
        private const string FixturesGroup = "Fixtures";
        private const string FixturesRenderingLayer = "Fixtures";
        private static readonly string[] FixtureNames = { "Chandelier", "Candelabra", "Candles" };

        // The candles' light on their own fixture (owner, 2026-10-23: "add the light back to the chandeliers and the
        // candelabras, but make sure it does not flicker"): a steady light that touches only the Fixtures layer, set
        // 'distance' metres off the nearest part so near and far parts light alike, about 3 lux on the nearest: warm
        // iron and wax, never past the bloom threshold. No flicker script.
        private const string FixtureGlowName = "FixtureGlow";

        private static void FixtureGlow(Transform fixture, Vector3 local, float distance)
        {
            var go = new GameObject(FixtureGlowName);
            go.transform.SetParent(fixture, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ChandelierColour;
            l.intensity = 3f * distance * distance;
            l.range = distance * 3f;
            l.shadows = LightShadows.None;
        }

        // A soft warm halo round a flame cluster, as the torches have: long-lived overlapping glow sprites, so its
        // brightness holds steady.
        // The halo fades out within a few metres of the camera, so a chandelier hung near the follow camera's height
        // never fills the screen with glow as the player walks under it.
        private static void Halo(Transform parent, Vector3 local, float size, float strength = 1f)
        {
            var halo = L1Wayfinding.Particles("Halo", parent, HaloMaterial(), 16, 6f, new Vector2(2.4f, 2.6f), Vector2.zero,
                new Vector2(size * 0.95f, size * 1.05f), new Color(1f, 0.62f, 0.3f, 0.13f * strength), new Color(1f, 0.55f, 0.25f, 0.11f * strength));
            halo.transform.localPosition = local;
        }

        private static Material HaloMaterial()
        {
            string path = MaterialsPath + "/L5_FX_Halo.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(L1Build.FxMat("L1_FX_Glow"));
                AssetDatabase.CreateAsset(mat, path);
            }

            const float near = 3f, far = 7f;
            mat.SetFloat("_CameraFadingEnabled", 1f);
            mat.SetFloat("_CameraNearFadeDistance", near);
            mat.SetFloat("_CameraFarFadeDistance", far);
            mat.SetVector("_CameraFadeParams", new Vector4(near, 1f / (far - near), 0f, 0f));
            mat.EnableKeyword("_FADING_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void GatherFixtures(Transform area)
        {
            var found = new List<Transform>();
            foreach (var t in area.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(FixtureNames, t.name) >= 0 && found.TrueForAll(f => !t.IsChildOf(f)))
                {
                    found.Add(t);
                }
            }

            if (found.Count == 0)
            {
                return;
            }

            var group = Group(area, FixturesGroup);
            foreach (var f in found)
            {
                f.SetParent(group, true);
            }
        }

        private static string FixtureLayer(Transform art)
        {
            uint mask = FixturesMask();
            var iron = Iron;
            var fixtureIron = FixtureIron;
            int count = 0;
            foreach (Transform area in art)
            {
                var group = area.Find(FixturesGroup);
                if (group == null)
                {
                    continue;
                }

                foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.renderingLayerMask = mask;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == iron)
                        {
                            mats[i] = fixtureIron;
                        }
                    }

                    r.sharedMaterials = mats;
                    count++;
                }

                // Steady candle flames: L2's candle lives half a second, six at most, so each 2-4 px flame shrank and
                // regrew every few frames. More, overlapping, calmer particles that rise half as fast hold one steady
                // flame at the wick (longer-lived ones drifted up into a stack of blobs).
                foreach (var ps in group.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (ps.name != "Flame")
                    {
                        continue;
                    }

                    var main = ps.main;
                    main.startLifetime = 0.7f;
                    main.maxParticles = 12;
                    var emission = ps.emission;
                    emission.rateOverTime = 14f;
                    var noise = ps.noise;
                    noise.strengthMultiplier *= 0.25f;
                    var velocity = ps.velocityOverLifetime;
                    velocity.speedModifierMultiplier *= 0.5f;
                    var size = ps.sizeOverLifetime;
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f)));
                }
            }

            return count + " fixture meshes on the Fixtures layer";
        }

        // The Fixtures rendering layer: slot 2 (unused, "Light Layer 2") is named on first use.
        private static uint FixturesMask()
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("m_RenderingLayers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == FixturesRenderingLayer)
                {
                    return 1u << i;
                }
            }

            var slot = layers.GetArrayElementAtIndex(2);
            if (!string.IsNullOrEmpty(slot.stringValue) && !slot.stringValue.StartsWith("Light Layer"))
            {
                throw new System.InvalidOperationException("Rendering layer 2 is taken by '" + slot.stringValue + "'.");
            }

            slot.stringValue = FixturesRenderingLayer;
            tags.ApplyModifiedPropertiesWithoutUndo();
            return 1u << 2;
        }

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

            // Years of the torch's smoke on the wall over it.
            var soot = L3Build.MeshChild(s, "Soot", SootMesh(), new Vector3(0f, 0.2f, 0.12f), SootMaterial());
            soot.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------------------------------------------------------- Wear (owner, 2026-10-23: "rough the place up a
        // bit (subtle). Right now it looks too clean. This place was inhabited by the evil guys.")

        private static Mesh sootMesh;
        private static Material sootMaterial;

        // A plume of soot on a wall: a quad 1.5 m wide and 2.6 m tall facing +z, darkening what is behind it
        // (multiply blend), narrow over the flame and spreading as it rises.
        private static Mesh SootMesh()
        {
            if (sootMesh == null)
            {
                // White vertex colours: the particle shader multiplies by them (without, its alpha read as zero).
                var m = new Mesh { name = "L5_SootPlume" };
                m.SetVertices(new[] { new Vector3(-0.75f, 0f, 0f), new Vector3(0.75f, 0f, 0f), new Vector3(0.75f, 2.6f, 0f), new Vector3(-0.75f, 2.6f, 0f) });
                m.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                m.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
                m.SetNormals(new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
                m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                m.RecalculateBounds();
                sootMesh = SaveMesh(m, MeshesPath + "/Shell");
            }

            return sootMesh;
        }

        private static Material SootMaterial()
        {
            if (sootMaterial != null)
            {
                return sootMaterial;
            }

            const int w = 64, h = 128;
            var px = new Color[w * h];
            var soot = L1Build.Hex("#1A1310");
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, t = (y + 0.5f) / h;
                    float width = 0.12f + 0.34f * t;
                    float across = Mathf.Exp(-2f * Mathf.Pow((u - 0.5f + (Mathf.PerlinNoise(t * 3f, 4f) - 0.5f) * 0.12f * t) / width, 2f));
                    float up = Mathf.SmoothStep(0f, 1f, t / 0.1f) * Mathf.Pow(1f - t, 1.1f);
                    float streak = 0.7f + 0.3f * Mathf.PerlinNoise(u * 9f, t * 2.5f);
                    var c = soot;
                    c.a = Mathf.Clamp01(1.45f * across * up * streak);
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            tex.Apply();
            string texPath = TexturesPath + "/L5_SootPlume.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            string path = MaterialsPath + "/L5_Soot.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            // Multiply: the colour fades to white where the alpha does, and the wall is multiplied by it.
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 3f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
            mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return sootMaterial = mat;
        }

        // A spill of rubble: a broken block or two and a scatter of chips, within 'spread' metres of 'at'.
        private static void Rubble(Transform g, Vector3 at, int count, float spread, System.Random r)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (float)r.NextDouble() * Mathf.PI * 2f, d = spread * Mathf.Sqrt((float)r.NextDouble());
                float s = i < 2 ? 0.28f + (float)r.NextDouble() * 0.2f : 0.06f + (float)r.NextDouble() * 0.14f;
                var size = new Vector3(s * (0.8f + (float)r.NextDouble() * 0.6f), s * 0.6f, s);
                var chip = L3Build.MeshChild(g, "Rubble", L2PolyKit.Tread(size, r.Next(100000)), at + new Vector3(Mathf.Cos(a) * d, size.y * 0.35f, Mathf.Sin(a) * d), Blocks);
                chip.transform.rotation = Quaternion.Euler(((float)r.NextDouble() - 0.5f) * 30f, r.Next(360), ((float)r.NextDouble() - 0.5f) * 30f);
            }
        }

        // Shackles on a wall: a ring bolted at 'height', a chain sagging to a cuff and back up to a second ring.
        private static void Shackles(Transform g, Vector3 wall, Vector3 outward, float height, int seed)
        {
            var side = Vector3.Cross(Vector3.up, outward).normalized;
            var a = wall + outward * 0.12f + Vector3.up * height - side * 0.45f;
            var b = a + side * 0.9f;
            var low = (a + b) * 0.5f + Vector3.down * 0.85f + outward * 0.06f;
            Chain(g, a, low, seed);
            Chain(g, low, b, seed + 50);
            foreach (var p in new[] { a, b })
            {
                L3Build.MeshChild(g, "Ring", L2PolyKit.Tread(new Vector3(0.14f, 0.14f, 0.05f), seed + 90), p, Iron).transform.rotation = Quaternion.LookRotation(outward);
            }

            L3Build.MeshChild(g, "Cuff", L2PolyKit.Tread(new Vector3(0.16f, 0.07f, 0.16f), seed + 91), low + Vector3.down * 0.06f, Iron);
        }

        // The signs of who lived here, kept to the walls' feet and the corners, off every walking line: rubble, a few
        // bones, dropped arms, tipped cups, shackles; stores in the stair hall's dark pockets.
        private static void Wear(Transform art)
        {
            var r = new System.Random(6101);
            var nave = art.Find("Nave");
            var stair = art.Find("Stair");
            var hall = art.Find("Hall");
            var chapel = art.Find("Chapel");

            foreach (var (x, y) in new[] { (47.1f, 277.2f), (47.1f, 323.8f), (47.0f, 350.7f), (60.9f, 288.3f), (60.9f, 330.6f), (60.9f, 352.2f) })
            {
                Rubble(nave, new Vector3(x, 0f, y), 7, 0.6f, r);
            }

            Shackles(nave, new Vector3(46.5f, 0f, 281f), Vector3.right, 2.9f, 6201);
            Shackles(nave, new Vector3(46.5f, 0f, 326f), Vector3.right, 2.9f, 6202);
            Shackles(nave, new Vector3(61.5f, 0f, 295f), Vector3.left, 2.9f, 6203);
            Shackles(nave, new Vector3(61.5f, 0f, 349f), Vector3.left, 2.9f, 6204);
            Prefab(Skeletons + "Remains", nave, new Vector3(47.6f, 0f, 299.6f), 30f);
            Prefab(Skeletons + "Remains", nave, new Vector3(60.3f, 0f, 342.2f), 200f);
            L3Build.Fit(nave, "P:SM_Icon_Skull_01", 90, new Vector3(0.2f, 0.22f, 0.25f), new Vector2(47.3f, 343.2f), 0f, Bone);
            L3Build.Seat(nave, "P:SM_Icon_Cup_01", new Vector3(90f, 40f, 0f), 0.22f, new Vector2(49.5f, 290.5f), 0f, Bronze);
            L3Build.Seat(nave, "P:SM_Icon_Cup_02", new Vector3(90f, 210f, 0f), 0.22f, new Vector2(58.6f, 316.4f), 0f, Bronze);

            // The stair hall's pockets outside the flights' tops: stores, and the floor at the corners.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = 54f + s * 17.8f;
                L3Build.Seat(stair, "A:SM_Prop_Barrel_01", new Vector3(0f, r.Next(360), 0f), 1f, new Vector2(x, 399.6f), 0f);
                L3Build.Seat(stair, "A:SM_Prop_Sack_0" + (s < 0 ? 1 : 2), new Vector3(0f, r.Next(360), 0f), 1f, new Vector2(x + s * -1.1f, 400.6f), 0f);
                Rubble(stair, new Vector3(x - s * 0.8f, 0f, 398.2f), 5, 0.5f, r);
                Rubble(stair, new Vector3(54f + s * 18.6f, 0f, 361.2f), 6, 0.6f, r);
            }

            // The throne hall: rubble at pillar feet, arms dropped in the aisles, bones, cups before the dais.
            foreach (var (x, y) in new[] { (37.0f, 431.5f), (71.0f, 444.0f), (37.0f, 456.5f), (71.0f, 419.5f) })
            {
                Rubble(hall, new Vector3(x, Hall, y), 6, 0.55f, r);
            }

            L3Build.Part(hall, "K:SM_Wep_Halberd_01", new Vector3(33.45f, Hall, 438.6f), new Vector3(0f, 90f, -12f), Vector3.one, Iron);
            L3Build.Part(hall, "K:SM_Wep_Shield_02", new Vector3(34.6f, Hall + 0.06f, 437.3f), new Vector3(-88f, 35f, 0f), Vector3.one * 1.3f, Iron);
            Prefab(Skeletons + "Remains", hall, new Vector3(74.0f, Hall, 450.5f), 120f);
            L3Build.Fit(hall, "P:SM_Icon_Skull_01", 180, new Vector3(0.2f, 0.22f, 0.25f), new Vector2(73.9f, 431.0f), Hall, Bone);
            Shackles(hall, new Vector3(33f, Hall, 450f), Vector3.right, 2.9f, 6205);
            Shackles(hall, new Vector3(75f, Hall, 426f), Vector3.left, 2.9f, 6206);
            L3Build.Seat(hall, "P:SM_Icon_Cup_03", new Vector3(90f, 120f, 0f), 0.22f, new Vector2(47.0f, 464.0f), Hall, Bronze);
            L3Build.Seat(hall, "P:SM_Icon_Cup_01", new Vector3(90f, 300f, 0f), 0.22f, new Vector2(61.5f, 463.2f), Hall, Bronze);

            Rubble(chapel, new Vector3(32.2f, 0f, 317.2f), 5, 0.5f, r);
            L3Build.Seat(chapel, "P:SM_Icon_Cup_02", new Vector3(90f, 75f, 0f), 0.22f, new Vector2(35.2f, 304.0f), 0f, Bronze);
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
            StripLights(c);

            float drop = at.y - floor;
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(c, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.75f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ChandelierColour;
            // A pool on the floor under it (2.4 lux straight below, fading out before the next chandelier), so the
            // light visibly comes from it rather than filling the room evenly (owner, 2026-10-23).
            light.intensity = 2.4f * drop * drop;
            light.range = 1.35f * drop + 5f;
            FixtureGlow(c, new Vector3(0f, -4f, 0f), 3.6f);
            Halo(c, new Vector3(0f, 0.2f, 0f), Mathf.Min(radius * 2.4f, 5.5f), radius > 2.5f ? 0.55f : 0.8f);
            // No shadows: seven shadowed point lights (six slices each) overflowed URP's shadow atlas, which then
            // swapped which lights got shadows as the camera moved, and the light flickered.
            light.shadows = LightShadows.None;
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
            Cornice(m, new Vector3(x0, 0f, y0), new Vector3(x0, 0f, y1), Vector3.right, spring);
            Cornice(m, new Vector3(x1, 0f, y0), new Vector3(x1, 0f, y1), Vector3.left, spring);
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

        // A moulded cornice along the line where a vault or ceiling meets its wall (plan points a to b at height h,
        // projecting 'inward'): an underside, a face, and a slope that runs up behind the vault's or ceiling's edge.
        // The cladding stands proud of the wall line, so without it a slit of open air showed along every springing.
        private static void Cornice(ShellMesh m, Vector3 a, Vector3 b, Vector3 inward, float h)
        {
            var along = (b - a).normalized;
            Vector3 At(Vector3 p, float o, float dy) => new Vector3(p.x, h + dy, p.z) + inward * o;
            var profile = new[] { (-0.15f, -0.45f), (0.45f, -0.45f), (0.45f, 0.15f), (-0.15f, 0.75f) };
            var faces = new[] { (Vector3.down, L2PolyKit.TopDark), (inward, L2PolyKit.Side), ((inward + Vector3.up).normalized, L2PolyKit.Top) };
            for (int k = 0; k < 3; k++)
            {
                var (o0, d0) = profile[k];
                var (o1, d1) = profile[k + 1];
                m.Quad(0, At(a, o0, d0), At(b, o0, d0), At(b, o1, d1), At(a, o1, d1), L4Kit.Cell(faces[k].Item2), faces[k].Item1);
            }

            foreach (var (p, facing) in new[] { (a, -along), (b, along) })
            {
                m.Tri(0, At(p, profile[0].Item1, profile[0].Item2), At(p, profile[1].Item1, profile[1].Item2), At(p, profile[2].Item1, profile[2].Item2), L4Kit.Cell(L2PolyKit.Side), facing);
                m.Tri(0, At(p, profile[0].Item1, profile[0].Item2), At(p, profile[2].Item1, profile[2].Item2), At(p, profile[3].Item1, profile[3].Item2), L4Kit.Cell(L2PolyKit.Side), facing);
            }
        }

        // A flat ceiling at height h over a rectangle, faces down, in palette blocks, a cornice round its edge.
        private static void Ceiling(Transform g, string name, float x0, float x1, float y0, float y1, float h, int seed)
        {
            var m = new ShellMesh();
            Cornice(m, new Vector3(x0, 0f, y0), new Vector3(x0, 0f, y1), Vector3.right, h);
            Cornice(m, new Vector3(x1, 0f, y0), new Vector3(x1, 0f, y1), Vector3.left, h);
            Cornice(m, new Vector3(x0, 0f, y0), new Vector3(x1, 0f, y0), Vector3.forward, h);
            Cornice(m, new Vector3(x0, 0f, y1), new Vector3(x1, 0f, y1), Vector3.back, h);
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

            // A quad with its own UV at each corner (the carpet's weave runs along the runner).
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
            {
                int k = uvs.Count;
                Quad(sub, a, b, c, d, Vector2.zero, facing);
                uvs[k] = ua;
                uvs[k + 1] = ub;
                uvs[k + 2] = uc;
                uvs[k + 3] = ud;
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

            public void Box(int sub, Vector3 centre, Quaternion rot, Vector3 size, Vector2 uv)
            {
                var h = size * 0.5f;
                Vector3 V(float x, float y, float z) => centre + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(sub, V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1), uv, rot * Vector3.forward);
                Quad(sub, V(1, -1, -1), V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1), uv, rot * Vector3.back);
                Quad(sub, V(1, -1, 1), V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), uv, rot * Vector3.right);
                Quad(sub, V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1), uv, rot * Vector3.left);
                Quad(sub, V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1), V(-1, 1, -1), uv, rot * Vector3.up);
                Quad(sub, V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1), uv, rot * Vector3.down);
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
        private static void Candles(Transform parent, Vector3 at, int count, int lit)
        {
            var g = Group(parent, "Candles");
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

        // A faint, steady self-glow: the fixtures' iron and wax read as firelit without any light touching them.
        private static Material Glow(Material mat, string hex)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", L1Build.Hex(hex));
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // The runners' weave (owner, 2026-10-23: "add some nice texture to the red carpet"): a crimson field with a
        // chain of dark lozenges down the middle, each with a gold heart, a dark border band inside the bronze trims
        // carrying small gold diamonds, thin gold rules, and a fine weave grain. u runs across the runner, v along it,
        // one lozenge per runner width, so a 1.6 m stair runner and the 5 m nave runner carry the same proportions. The
        // texture holds four lozenges (tiled at a quarter), so its wear doesn't repeat every runner width: the nap
        // trodden flat down the middle, the borders grimed, a few old dark stains (owner, 2026-10-23: "rough the place
        // up a bit... too clean"; and "a slightly darker red").
        private static Material CarpetMaterial()
        {
            const int w = 256, h = 1024, lozenges = 4;
            var px = new Color[w * h];
            var field = L1Build.Hex("#47131A");
            var deep = L1Build.Hex("#200709");
            var band = L1Build.Hex("#2E0B0F");
            var gold = L1Build.Hex("#7E5E2E");
            var rose = L1Build.Hex("#5B1B21");
            var stainColour = L1Build.Hex("#1C0B09");
            var stains = new List<Vector3>();
            var sr = new System.Random(9177);
            for (int i = 0; i < 9; i++)
            {
                stains.Add(new Vector3(0.15f + (float)sr.NextDouble() * 0.7f, (float)sr.NextDouble(), 0.03f + (float)sr.NextDouble() * 0.05f));
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, whole = (y + 0.5f) / h, v = Mathf.Repeat(whole * lozenges, 1f);
                    float edge = Mathf.Min(u, 1f - u);
                    Color c = field;
                    if (edge < 0.1f)
                    {
                        // The border band, with a gold diamond every eighth of a repeat.
                        c = band;
                        float bv = Mathf.Repeat(v * 8f, 1f) - 0.5f, bu = (edge - 0.05f) / 0.035f;
                        if (Mathf.Abs(bu) + Mathf.Abs(bv) * 1.6f < 0.55f)
                        {
                            c = gold;
                        }
                    }
                    else if (Mathf.Abs(edge - 0.115f) < 0.007f)
                    {
                        c = gold;
                    }
                    else
                    {
                        // The lozenge chain: dark outline, rose fill, gold heart; small dark crosses where two meet.
                        float lz = Mathf.Abs(u - 0.5f) / 0.3f + Mathf.Abs(v - 0.5f) / 0.46f;
                        if (lz < 1f)
                        {
                            c = lz > 0.86f ? deep : lz < 0.3f ? (lz < 0.1f ? deep : gold) : rose;
                        }
                        else
                        {
                            float cv = Mathf.Min(v, 1f - v), cu = Mathf.Abs(u - 0.5f);
                            if ((cu < 0.012f && cv < 0.07f) || (cv < 0.01f && cu < 0.05f))
                            {
                                c = deep;
                            }
                        }
                    }

                    // The weave: fine rows across, a little noise in every thread.
                    float grain = 0.93f + 0.07f * Mathf.PerlinNoise(x * 0.9f, y * 0.13f) + ((y & 1) == 0 ? 0.025f : -0.025f);
                    c *= grain;

                    // Wear: the nap trodden flat and dulled down the middle, grime toward the borders, old stains.
                    float tread = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) / 0.22f) * (0.6f + 0.4f * Mathf.PerlinNoise(u * 6f, whole * 40f));
                    c = Color.Lerp(c, new Color(c.grayscale, c.grayscale * 0.8f, c.grayscale * 0.75f) * 1.15f, 0.22f * tread);
                    c *= 1f - 0.18f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.02f, edge)) * Mathf.PerlinNoise(u * 20f, whole * 90f);
                    foreach (var s in stains)
                    {
                        float dv = Mathf.Abs(whole - s.y);
                        dv = Mathf.Min(dv, 1f - dv) * lozenges;
                        float d = new Vector2(u - s.x, dv).magnitude / s.z + (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.8f;
                        c = Color.Lerp(c, stainColour, 0.55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.1f, 0.6f, d)));
                    }

                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            tex.Apply();
            string texPath = TexturesPath + "/L5_Carpet_Weave.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();

            var mat = Flat("L5_Carpet", "#FFFFFF", 0.04f);
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetTextureScale("_BaseMap", new Vector2(1f, 1f / lozenges));
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
            Play("stair_well", 54f, 368f, 0f);
            Play("stair_flight", 39.1f, 384f, 4.75f);
            Play("terrace", 54f, 400f, Hall);
            Play("processional", 54f, 420f, Hall);
            var arena = GameObject.Find("CM_Arena_FixedFrame");
            if (arena != null)
            {
                shots.Add(("arena_frame", arena.transform.position, arena.transform.eulerAngles, L5Greybox.ArenaFov, new Vector3(54f, Hall, 446f)));
            }

            shots.Add(("vista_nave", new Vector3(54f, 7f, 272f), new Vector3(12f, 0f, 0f), 45f, new Vector3(54f, 0f, 280f)));
            shots.Add(("nave_knights", new Vector3(52f, 2.2f, 296f), new Vector3(4f, -28f, 0f), 60f, new Vector3(54f, 0f, 300f)));
            // Inspection angles for holes and seams: from the gallery down over the flights, beside a flight's outer
            // cheek, into the niche, and the throne hall's north-west corner.
            shots.Add(("inspect_gallery", new Vector3(54f, Hall + 2.2f, 404.8f), new Vector3(24f, 180f, 0f), 75f, new Vector3(54f, Hall, 403f)));
            shots.Add(("inspect_cheek", new Vector3(37.2f, 1.8f, 366f), new Vector3(-4f, 20f, 0f), 70f, new Vector3(36f, 0f, 368f)));
            shots.Add(("inspect_niche", new Vector3(54f, 1.8f, 389f), new Vector3(2f, 0f, 0f), 65f, new Vector3(54f, 0f, 391f)));
            shots.Add(("inspect_corner", new Vector3(44f, Hall + 2f, 430f), new Vector3(8f, -35f, 0f), 70f, new Vector3(44f, Hall, 432f)));
            shots.Add(("stair_low", new Vector3(54f, 2.4f, 346f), new Vector3(-9f, 0f, 0f), 72f, new Vector3(54f, 0f, 356f)));
            shots.Add(("stair_plan", new Vector3(54f, 34f, 350f), new Vector3(44f, 0f, 0f), 62f, new Vector3(54f, 0f, 366f)));
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
