using Kneel.Lighting.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kneel.EditorTools
{
    // Look-dev for L4: one corner of the E3 courtyard built from the PolygonKnights castle kit under the L4 grade,
    // to settle the stone, the wall rhythm and the ruin before the kit is made into prefabs. It lives in the
    // asset review scene under LookDev and is rebuilt from nothing each time.
    public static class L4LookDev
    {
        // The corner in its own frame: courtyard floor x 0..28, z 0..24; the north curtain's face at z 24 with
        // the north wall-walk on top (T+1, +5 m); the NE tower at x 28..38, z 22..32; the east wall's face at
        // x 28 with the terrace's inner parapet on top.
        private const float Tier = 5f;
        private const float NorthFace = 24f;
        private const float EastFace = 28f;

        public static readonly Vector3 PlayerSpot = new Vector3(17f, 0f, 20.5f);

        [MenuItem("Kneel/L4/Look-dev/Build Courtyard Corner")]
        public static void BuildMenu()
        {
            Debug.Log(Build());
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
            {
                return "Leave Play mode first.";
            }

            var root = OpenReviewScene();
            var group = L2Build.Group(root.transform, "LookDev", true);
            L4Kit.BuildStonePalette();
            var stone = L4Build.Mat("L4_Knights_Stone");
            var soot = L4Build.Mat("L4_Knights_Soot");

            Floor(group);
            NorthCurtain(group, stone, soot);
            Tower(group, stone, soot);
            EastWall(group, stone);
            Props(group, stone, soot);
            CameraAndPlayer(root.transform);

            string lighting = L4LightingPass.Apply(root.transform);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            return "L4 look-dev corner built. " + lighting;
        }

        private static GameObject OpenReviewScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != L4Build.ReviewScenePath)
            {
                if (System.IO.File.Exists(L4Build.ReviewScenePath))
                {
                    scene = EditorSceneManager.OpenScene(L4Build.ReviewScenePath, OpenSceneMode.Single);
                }
                else
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    L2Build.EnsureFolder(L4Build.ScenesPath);
                    EditorSceneManager.SaveScene(scene, L4Build.ReviewScenePath);
                }
            }

            var root = GameObject.Find(L4Build.ReviewRootName);
            if (root == null)
            {
                root = new GameObject(L4Build.ReviewRootName);
            }

            return root;
        }

        // ---------------------------------------------------------------- Pieces

        private static void Floor(Transform group)
        {
            var floor = L2Build.Group(group, "Floor", true);
            var yardMat = YardMaterial();
            var quad = L3Build.MeshChild(floor, "Yard", FlatQuad(), new Vector3(14f, 0f, 14f), yardMat);
            quad.transform.localScale = new Vector3(56f, 1f, 56f);
            L3Build.SetLayer(quad, L3Build.Walkable);

            // The wall-walk and the terrace: decks at T+1 behind their walls.
            var walk = L3Build.MeshChild(floor, "NorthWalk", FlatQuad(), new Vector3(14f, Tier, NorthFace + 2.2f), L4Build.Mat("L4_Ground"));
            walk.transform.localScale = new Vector3(28f, 1f, 4.4f);
            var terrace = L3Build.MeshChild(floor, "Terrace", FlatQuad(), new Vector3(EastFace + 6f, Tier, 11f), L4Build.Mat("L4_Ground"));
            terrace.transform.localScale = new Vector3(12f, 1f, 22f);
        }

        // Four 6 m faces (the pack wall at x1.11) with the merlon run on the far edge of the walk, a low parapet
        // on its yard edge, and three machicolations corbelled out over the pour rings.
        private static void NorthCurtain(Transform group, Material stone, Material soot)
        {
            var g = L2Build.Group(group, "NorthCurtain", true);
            // Runs close on the tower with a 4 m cut, so no piece is squashed below its own scale by much.
            float[] lengths = { 6f, 6f, 6f, 6f, 4f };
            float start = 0f;
            foreach (float length in lengths)
            {
                float x = start + length * 0.5f;
                L3Build.Fit(g, "K:SM_Bld_Castle_Wall_01", 0, new Vector3(length, Tier, 2.2f), new Vector2(x, NorthFace + 1.1f), 0f, stone);
                start += length;
            }

            // The outer parapet (W02): merlons on a 2 m rhythm, many broken in the siege, their stones on the
            // walk. The yard-side parapet (W03) is 0.9 m and ragged.
            L4Kit.MerlonRun(g, new Vector3(0f, Tier, NorthFace + 4.4f), Vector3.right, Vector3.forward, 14, 31, 0.5f, 0f);
            L4Kit.ParapetRun(g, new Vector3(0f, Tier, NorthFace), Vector3.right, Vector3.back, 28f, 32, 0.35f);

            // Cracks spreading from the machicolation corbels and the wall foot.
            Stain(g, "Crack", new Vector3(10f, 4.2f, NorthFace - 0.03f), 0f, new Vector2(2.4f, 3.2f), StainMaterial("L4_Crack", 2));
            Stain(g, "Crack", new Vector3(19.5f, 3.6f, NorthFace - 0.03f), 0f, new Vector2(1.8f, 2.6f), StainMaterial("L4_Crack", 2));

            foreach (float x in new[] { 6f, 14f, 22f })
            {
                L3Build.Seat(g, "K:SM_Bld_Castle_Wood_Battlement_04", new Vector3(0f, 180f, 0f), 1f, new Vector2(x, NorthFace - 0.4f), Tier - 1.6f, soot);
            }
        }

        // The NE tower: two storeys of curtain faces round a 10 m square, merlons on top.
        private static void Tower(Transform group, Material stone, Material soot)
        {
            var g = L2Build.Group(group, "TowerNE", true);
            var c = new Vector2(EastFace + 5f, NorthFace + 3f);
            for (int side = 0; side < 4; side++)
            {
                int yaw = side * 90;
                Vector2 dir = side == 0 ? Vector2.down : side == 1 ? Vector2.left : side == 2 ? Vector2.up : Vector2.right;
                Vector2 at = c + dir * 3.9f;
                Vector3 size = side % 2 == 0 ? new Vector3(10f, Tier, 2.2f) : new Vector3(2.2f, Tier, 10f);
                int yawFit = side % 2 == 0 ? 0 : 90;
                L3Build.Fit(g, "K:SM_Bld_Castle_Wall_01", yawFit, size, at, 0f, stone);
                Vector3 upper = side % 2 == 0 ? new Vector3(10f, 4f, 2.2f) : new Vector3(2.2f, 4f, 10f);
                L3Build.Fit(g, "K:SM_Bld_Castle_Wall_01", yawFit, upper, at, Tier, stone);
            }

            // Merlons round the top, the worst of them knocked down by the neighbours' siege engines.
            const float top = 9f;
            float x0 = c.x - 5f, x1 = c.x + 5f, z0 = c.y - 5f, z1 = c.y + 5f;
            L4Kit.MerlonRun(g, new Vector3(x0, top, z0), Vector3.right, Vector3.back, 5, 41, 0.6f, 0f);
            L4Kit.MerlonRun(g, new Vector3(x1, top, z0), Vector3.forward, Vector3.right, 5, 42, 0.6f, 0f);
            L4Kit.MerlonRun(g, new Vector3(x1, top, z1), Vector3.left, Vector3.forward, 5, 43, 0.6f, 0f);
            L4Kit.MerlonRun(g, new Vector3(x0, top, z1), Vector3.back, Vector3.left, 5, 44, 0.6f, 0f);

            L3Build.Seat(g, "K:SM_Bld_Castle_Door_01", Vector3.zero, 0.9f, new Vector2(c.x, c.y - 5.05f), 0f, soot);
        }

        private static void EastWall(Transform group, Material stone)
        {
            var g = L2Build.Group(group, "EastWall", true);
            for (int i = 0; i < 4; i++)
            {
                float z = 3f + 6f * i;
                if (z > NorthFace - 2f)
                {
                    break;
                }

                L3Build.Fit(g, "K:SM_Bld_Castle_Wall_01", 90, new Vector3(2.2f, Tier, 6f), new Vector2(EastFace + 1.1f, z), 0f, stone);
            }

            L4Kit.ParapetRun(g, new Vector3(EastFace, Tier, 0f), Vector3.forward, Vector3.left, 22f, 51, 0.4f);
            Stain(g, "Crack", new Vector3(EastFace - 0.03f, 3.8f, 8f), 90f, new Vector2(2.2f, 3.0f), StainMaterial("L4_Crack", 2));
        }

        private static void Props(Transform group, Material stone, Material soot)
        {
            var g = L2Build.Group(group, "Props", true);
            // Rubble along the wall feet.
            // Rubble along the wall feet: dressed blocks fallen from the parapets, tilted and half sunk, with
            // small dark stones between them. Clustered, not evenly strewn.
            var rubble = new[] { "A:SM_Env_Rock_07", "A:SM_Env_Rock_08", "A:SM_Env_Rock_014", "A:SM_Env_Rock_015", "A:SM_Env_Rock_016" };
            var rng = new System.Random(4);
            var clusters = new[] { new Vector2(3.5f, NorthFace - 0.7f), new Vector2(10.5f, NorthFace - 0.6f), new Vector2(18f, NorthFace - 0.8f), new Vector2(25.5f, NorthFace - 0.9f), new Vector2(EastFace - 0.8f, 6f), new Vector2(EastFace - 0.7f, 15f) };
            int n = 0;
            foreach (var centre in clusters)
            {
                int blocks = 2 + rng.Next(3);
                for (int i = 0; i < blocks; i++)
                {
                    var at = centre + new Vector2(((float)rng.NextDouble() - 0.5f) * 2.4f, ((float)rng.NextDouble() - 0.5f) * 1.0f);
                    var size = new Vector3(0.5f + (float)rng.NextDouble() * 0.5f, 0.3f + (float)rng.NextDouble() * 0.15f, 0.35f + (float)rng.NextDouble() * 0.2f);
                    var block = L3Build.MeshChild(g, "FallenBlock_" + n++, L2PolyKit.Tread(size, 700 + n), new Vector3(at.x, size.y * 0.3f, at.y), CourseStone);
                    block.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 30f, rng.Next(360), ((float)rng.NextDouble() - 0.5f) * 40f);
                }

                for (int i = 0; i < 5; i++)
                {
                    var at = centre + new Vector2(((float)rng.NextDouble() - 0.5f) * 3.2f, ((float)rng.NextDouble() - 0.5f) * 1.4f);
                    float s = 0.35f + (float)rng.NextDouble() * 0.45f;
                    L3Build.Seat(g, rubble[rng.Next(rubble.Length)], new Vector3(0f, rng.Next(360), 0f), s, at, -0.04f, L4Build.Mat("L4_Adventure_Soot"));
                }
            }

            // Pitch run down the curtain under each machicolation, and soot along the wall feet.
            foreach (float x in new[] { 6f, 14f, 22f })
            {
                Stain(g, "PitchRun", new Vector3(x, Tier - 0.9f, NorthFace - 0.03f), 0f, new Vector2(2.6f, 4.1f), StainMaterial("L4_PitchRun", 0));
            }

            for (int i = 0; i < 5; i++)
            {
                Stain(g, "SootFoot", new Vector3(3f + 6f * i - (i == 4 ? 1f : 0f), 0f, NorthFace - 0.04f), 0f, new Vector2(i == 4 ? 4.2f : 6.2f, 1.8f), StainMaterial("L4_SootFoot", 1));
            }

            for (int i = 0; i < 4; i++)
            {
                Stain(g, "SootFoot", new Vector3(EastFace - 0.04f, 0f, 3f + 6f * i - (i == 3 ? 1.9f : 0f)), 90f, new Vector2(i == 3 ? 4.2f : 6.2f, 1.8f), StainMaterial("L4_SootFoot", 1));
            }

            L3Build.Seat(g, "K:SM_Env_RockPile_03", new Vector3(0f, 30f, 0f), new Vector3(0.45f, 0.3f, 0.45f), new Vector2(EastFace - 1.6f, NorthFace - 1.6f), -0.2f, stone);
            L3Build.Seat(g, "K:SM_Prop_Cart_01", new Vector3(0f, 74f, 0f), 1f, new Vector2(12f, 15f), 0f, stone);
            L3Build.Seat(g, "K:SM_Prop_Brazier_01", Vector3.zero, 1.1f, new Vector2(20f, 18f), 0f, stone);
            L3Build.Seat(g, "K:SM_Prop_Banner_02", new Vector3(0f, 180f, 0f), 1f, new Vector2(11f, NorthFace - 0.1f), 2.2f, stone);
        }

        private static void CameraAndPlayer(Transform root)
        {
            var cams = L2Build.Group(root, "_Camera", false);
            var camGo = GameObject.FindWithTag("MainCamera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            camGo.transform.SetParent(cams, false);
            var cam = camGo.GetComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            // The gameplay framing: pitch 65, 8.5 m from the player.
            camGo.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            camGo.transform.position = PlayerSpot + Vector3.up * 1f - camGo.transform.forward * 8.5f;

            var player = GameObject.Find("Player");
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kneel/Player/Player.prefab");
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.name = "Player";
            }

            player.transform.position = PlayerSpot;
        }

        // ---------------------------------------------------------------- Helpers

        // A vertical decal quad on a wall face: bottom centre at 'at' (top centre for runs, which hang), facing
        // -Z turned by yaw (so 0 faces the yard from a north wall, 90 from an east wall).
        internal static void Stain(Transform parent, string name, Vector3 at, float yaw, Vector2 size, Material material)
        {
            bool hangs = material.name == "L4_PitchRun" || material.name == "L4_Crack";
            var go = L3Build.MeshChild(parent, name, FlatQuad(), Vector3.zero, material);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            go.transform.localPosition = at + Vector3.up * (hangs ? -size.y * 0.5f : size.y * 0.5f);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Kind 0, runs: streaks of pitch from a top band, uneven lengths, thicker at the top. 1, foot: smoke soot
        // rising from the ground in a ragged band. 2, crack: a branching fracture, dark and thin, with a faint
        // shadow beside it.
        internal static Material StainMaterial(string name, int kind)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(L4Build.MaterialsPath + "/" + name + ".mat");
            if (existing != null && Built.Contains(name))
            {
                return existing;
            }

            const int w = 128, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            bool run = kind == 0;
            var rng = new System.Random(11 + kind);
            var crack = kind == 2 ? CrackField(w, h, rng) : null;
            var lengths = new float[w];
            float l = 0.5f;
            for (int x = 0; x < w; x++)
            {
                if (x % 6 == 0)
                {
                    l = 0.25f + (float)rng.NextDouble() * 0.75f;
                }

                lengths[x] = l;
            }

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float a;
                    if (crack != null)
                    {
                        a = crack[y * w + x];
                    }
                    else if (run)
                    {
                        float fromTop = 1f - v;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.2f));
                        float streak = fromTop < lengths[x] * edge ? 1f - fromTop / Mathf.Max(lengths[x], 0.01f) * 0.6f : 0f;
                        float band = Mathf.Clamp01(1f - fromTop / 0.12f) * edge;
                        a = Mathf.Max(streak * edge * (0.55f + Mathf.PerlinNoise(x * 0.3f, y * 0.05f) * 0.45f), band) * 0.9f;
                    }
                    else
                    {
                        float top = 0.35f + Mathf.PerlinNoise(u * 6f, 3.1f) * 0.45f;
                        a = Mathf.Clamp01(1f - v / top) * (0.5f + Mathf.PerlinNoise(u * 14f, v * 5f) * 0.4f);
                        a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.08f));
                    }

                    tex.SetPixel(x, y, new Color(0.02f, 0.018f, 0.016f, a));
                }
            }

            tex.Apply();
            string folder = L4Build.MaterialsPath + "/Textures";
            L2Build.EnsureFolder(folder);
            string path = folder + "/" + name + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var mat = L4Build.LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", run ? 0.45f : 0.02f);
            L4Build.MakeTransparent(mat, -20);
            EditorUtility.SetDirty(mat);
            Built.Add(name);
            return mat;
        }

        // A crack from the top centre wandering down and splitting: a 1-2 px dark line with a softer shadow.
        private static float[] CrackField(int w, int h, System.Random rng)
        {
            var field = new float[w * h];
            void Branch(float x, float y, float angle, float length, float width, int depth)
            {
                float travelled = 0f;
                while (travelled < length && y > 0 && x > 0 && x < w - 1)
                {
                    angle += ((float)rng.NextDouble() - 0.5f) * 0.9f;
                    angle = Mathf.Clamp(angle, -2.6f, -0.55f);
                    float step = 1.5f;
                    x += Mathf.Cos(angle) * step;
                    y += Mathf.Sin(angle) * step;
                    travelled += step;
                    float ww = width * (1f - travelled / length * 0.7f);
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int px = Mathf.RoundToInt(x) + dx, py = Mathf.RoundToInt(y) + dy;
                            if (px < 0 || py < 0 || px >= w || py >= h)
                            {
                                continue;
                            }

                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            float v = d <= ww ? 0.95f : Mathf.Clamp01(1f - (d - ww) / 2.5f) * 0.3f;
                            field[py * w + px] = Mathf.Max(field[py * w + px], v);
                        }
                    }

                    if (depth < 3 && rng.NextDouble() < 0.035)
                    {
                        Branch(x, y, angle + (rng.NextDouble() < 0.5 ? -0.8f : 0.8f), (length - travelled) * 0.6f, ww * 0.7f, depth + 1);
                    }
                }
            }

            Branch(w * 0.5f, h - 2f, -Mathf.PI * 0.5f, h * 0.95f, 1.4f, 0);
            return field;
        }

        private static readonly System.Collections.Generic.HashSet<string> Built = new System.Collections.Generic.HashSet<string>();

        // The look-dev yard's own copy of L4_Ground, with a wear mask: earth at the wall feet and round the rings.
        private static Material YardMaterial()
        {
            L4Paving.Apply();
            string path = L4Build.MaterialsPath + "/L4_Ground_LookDev.mat";
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

            var feet = new[] { new Rect(-14f, NorthFace, 42f, 3f), new Rect(EastFace, -14f, 3f, 42f) };
            var rings = new[] { new Vector2(6f, NorthFace - 3f), new Vector2(14f, NorthFace - 3f), new Vector2(22f, NorthFace - 3f) };
            var mask = L4Paving.Mask("L4_Ground_LookDev_Mask", new Vector2(-14f, -14f), new Vector2(56f, 56f), feet, rings, 3);
            mat.SetTexture("_PaveMask", mask);
            // The murder holes' pitch is painted into the ground (colour and a wet sheen), never a decal quad:
            // transparent quads lying on this floor drew a straight-edged veil across it.
            var splashes = new[]
            {
                new L4Paving.Splash(rings[0], 3.1f, 17),
                new L4Paving.Splash(rings[1], 2.9f, 141),
                new L4Paving.Splash(rings[2], 3.2f, 263),
            };
            mat.SetTexture("_BaseMap", L4Paving.LayoutFromMask("L4_Ground_LookDev_Layout", mask, new Vector2(-14f, -14f), new Vector2(56f, 56f), splashes));
            mat.SetFloat("_PuddleAmount", 1f);
            mat.SetFloat("_PuddleDepth", 1.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CourseStone => L4Kit.Stone;

        internal static Mesh FlatQuad()
        {
            string path = L4Build.MeshesPath + "/L4_FlatQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            mesh = new Mesh { name = "L4_FlatQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            L2Build.EnsureFolder(L4Build.MeshesPath);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
