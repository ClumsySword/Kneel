using System.Collections.Generic;
using System.IO;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Kneel.Lighting.EditorTools
{
    // Review captures for L2 in Play mode, from the real Game view (post-processing, particles and fire flicker
    // all running): the knight is placed at each beat, the real PlayerCamera frames him (edge-look off), and each
    // frame is saved plus stitched into a contact sheet. Output: Screenshots/L2_HollowVillage/Beats.
    public static class L2LightingShots
    {
        private const string Folder = "Screenshots/L2_HollowVillage/Beats";

        public struct Beat
        {
            public string Name;
            public Vector2 Focus;
            public float FacingYaw;

            public Beat(string name, float x, float z, float facingYaw = 0f)
            {
                Name = name;
                Focus = new Vector2(x, z);
                FacingYaw = facingYaw;
            }
        }

        public static readonly Beat[] Beats =
        {
            new Beat("01_Start", -93f, -57.5f, 90f),
            new Beat("02_E1", -32f, -59f),
            new Beat("03_WellSquare", -1f, -45f, -20f),
            new Beat("04_E2", 26f, -38f, 30f),
            new Beat("05_RoadVista", 47f, -15f, -30f),
            new Beat("06_E3Slope", 31f, -5f, -80f),
            new Beat("07_ChurchFront", 16f, -17f, -60f),
            new Beat("08_FireDetour", -12f, -22f, -90f),
            new Beat("09_ChurchStair", -26.5f, 3.5f, 0f),
            new Beat("10_E4", -24f, 12f, 0f),
            new Beat("11_Crossroads", -4f, 39f, 0f),
            new Beat("12_WeaponThroughFire", 25f, 40.5f, 0f),
            new Beat("13_WeaponGap", 22.5f, 71f, 150f),
            new Beat("14_E5", 10f, 89f, 0f),
            new Beat("15_Exit", 5.5f, 138f, 0f),
            new Beat("16_V2Doorframe", 38f, 0.5f, 20f),
            new Beat("17_GateE4", -26f, 25.5f, -8f),
            new Beat("18_Bridge", 47f, -15.5f, 0f),
            new Beat("19_E1SouthRim", -32f, -62f, 0f),
            new Beat("20_CreekWest", 37f, -15f, 90f),
            new Beat("21_GateE5", 17.5f, 71f, -75f),
            new Beat("22_Fields", 6.5f, 128f, 0f),
            new Beat("23_Shrine", -9f, 45f, -60f),
        };

        private static int index;
        private static float settleUntil;
        private static bool captured;
        private static readonly List<string> Files = new List<string>();

        [MenuItem("Kneel/L2/Lighting/Capture Beat Shots (Play)")]
        public static void CaptureMenu()
        {
            Run();
        }

        public static void Run()
        {
            Directory.CreateDirectory(Folder);
            SessionState.SetBool("L2Shots.Pending", true);
            Resume();
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }
        }

        public static bool Done => !SessionState.GetBool("L2Shots.Pending", false);

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool("L2Shots.Pending", false))
            {
                return;
            }

            index = -1;
            captured = false;
            Files.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || Time.unscaledTime < 1f)
            {
                return;
            }

            if (index < 0 || (captured && Time.unscaledTime > settleUntil + 0.4f))
            {
                index++;
                if (index >= Beats.Length)
                {
                    Finish();
                    return;
                }

                Frame(Beats[index]);
                settleUntil = Time.unscaledTime + 1.6f;
                captured = false;
                return;
            }

            if (!captured && Time.unscaledTime > settleUntil)
            {
                string path = Folder + "/" + Beats[index].Name + ".png";
                ScreenCapture.CaptureScreenshot(path);
                Files.Add(path);
                captured = true;
            }
        }

        // Stands the knight at the beat, facing the way the path goes on, and lets the real camera frame him.
        private static void Frame(Beat beat)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            var player = GameObject.Find("Player");
            var cc = player.GetComponent<CharacterController>();
            var feet = L2Build.Ground(beat.Focus);
            cc.enabled = false;
            player.transform.SetPositionAndRotation(feet + Vector3.up * 0.05f, Quaternion.Euler(0f, beat.FacingYaw, 0f));
            cc.enabled = true;

            var cameraScript = Camera.main.GetComponent("PlayerCamera");
            var type = cameraScript.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            type.GetField("edgeLookEnabled", flags)?.SetValue(cameraScript, false);
            type.GetField("edgeOffset", flags)?.SetValue(cameraScript, Vector3.zero);
            type.GetField("focusPoint", flags)?.SetValue(cameraScript, feet);
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("L2Shots.Pending", false);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += Stitch;
        }

        // Contact sheet, 4 across, once every capture has been written.
        private static void Stitch()
        {
            var tiles = new List<Texture2D>();
            foreach (var beat in Beats)
            {
                string path = Folder + "/" + beat.Name + ".png";
                if (!File.Exists(path))
                {
                    continue;
                }

                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(path));
                tiles.Add(t);
            }

            if (tiles.Count == 0)
            {
                return;
            }

            const int w = 480, h = 270, cols = 4;
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < tiles.Count; i++)
            {
                var scaled = Scale(tiles[i], w, h);
                sheet.SetPixels((i % cols) * w, (rows - 1 - i / cols) * h, w, h, scaled);
                Object.DestroyImmediate(tiles[i]);
            }

            sheet.Apply();
            File.WriteAllBytes(Folder + "/_Sheet.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("[L2] Beat shots captured: " + Folder);
        }

        // ---------------------------------------------------------------- Silhouette test

        [MenuItem("Kneel/L2/Lighting/Silhouette Test")]
        public static void SilhouetteMenu()
        {
            Debug.Log("[L2] " + Silhouettes());
        }

        // Stands a near-black soldier at each spawn point of each fight, frames it from where the player would be
        // (the near half of the fight), renders with and without the figure, and compares the figure's mean
        // luminance with the ground right around it. Fire behind the fight should make the figure the darker one.
        public static string Silhouettes()
        {
            var figurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabPath("K/Characters/Character_Soldier_01_Black"));
            var player = L1LightingShots.PlayerRoot();
            bool playerActive = player != null && player.gameObject.activeSelf;
            if (player != null)
            {
                player.gameObject.SetActive(false);
            }

            Directory.CreateDirectory(Folder + "/Silhouettes");
            var report = new System.Text.StringBuilder("Silhouettes (Weber contrast figure vs surroundings; figure darker = backlit): ");
            foreach (var arena in L2Layout.Arenas)
            {
                var figures = new List<GameObject>();
                foreach (var s in L2Layout.Spawns[arena.Name])
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(figurePrefab);
                    f.transform.SetPositionAndRotation(L2Build.Ground(s), Quaternion.Euler(0f, 180f, 0f));
                    L1LightingShots.SetCharacterLayer(f);
                    foreach (var r in f.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }

                    figures.Add(f);
                }

                // The player squares up 3.5 m south of the (first) enemy, the camera centred on the player.
                var stand = L2Layout.Spawns[arena.Name][0] - new Vector2(0f, 3.5f);
                SetFigures(figures, false);
                var without = RenderAt(stand);
                SetFigures(figures, true);
                var with = RenderAt(stand);
                float c = Contrast(with, without, out float figureL, out float groundL);
                report.Append($"{arena.Name} {c:F2} ({(figureL < groundL ? "dark on light" : "light on dark")}); ");
                File.WriteAllBytes(Folder + "/Silhouettes/" + arena.Name + ".png", with.EncodeToPNG());
                Object.DestroyImmediate(with);
                Object.DestroyImmediate(without);
                foreach (var f in figures)
                {
                    Object.DestroyImmediate(f);
                }
            }

            if (player != null)
            {
                player.gameObject.SetActive(playerActive);
            }

            return report.ToString();
        }

        private static void SetFigures(List<GameObject> figures, bool on)
        {
            foreach (var f in figures)
            {
                f.SetActive(on);
            }
        }

        private static Texture2D RenderAt(Vector2 focus)
        {
            const int width = 960, height = 540;
            var cam = Camera.main;
            var pose = L2Layout.CameraPose(L2Build.Ground(focus), L2Layout.CameraDistance);
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        private static float Contrast(Texture2D with, Texture2D without, out float figureL, out float groundL)
        {
            var a = with.GetPixels();
            var b = without.GetPixels();
            int width = with.width, height = with.height;
            var mask = new bool[a.Length];
            double sumFigure = 0, sumGround = 0;
            int nFigure = 0, nGround = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 0.06f)
                {
                    mask[i] = true;
                    sumFigure += Luminance(a[i]);
                    nFigure++;
                }
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }

                int x = i % width, y = i / width;
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * Mathf.PI / 4f;
                    int r = 3 + (i * 7 + k) % 10;
                    int nx = x + Mathf.RoundToInt(Mathf.Cos(angle) * r), ny = y + Mathf.RoundToInt(Mathf.Sin(angle) * r);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || mask[ny * width + nx])
                    {
                        continue;
                    }

                    sumGround += Luminance(b[ny * width + nx]);
                    nGround++;
                }
            }

            figureL = nFigure > 0 ? (float)(sumFigure / nFigure) : 0f;
            groundL = nGround > 0 ? (float)(sumGround / nGround) : 0f;
            return Mathf.Abs(figureL - groundL) / Mathf.Max(groundL, 0.02f);
        }

        private static Color[] Scale(Texture2D source, int width, int height)
        {
            var result = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[y * width + x] = source.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            return result;
        }
    }
}
