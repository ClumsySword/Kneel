using System.Collections.Generic;
using System.IO;
using System.Text;
using Kneel.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Kneel.Lighting.EditorTools
{
    // Review captures for the L1 lighting pass. Renders the real Main Camera (post-processing on) at the
    // gameplay framing for each zone, and measures how well a standing figure reads in greyscale.
    public static class L1LightingShots
    {
        private const string Folder = "Screenshots/L1_BrokenLine/Lighting";
        private const int Width = 1280;
        private const int Height = 720;

        public struct Zone
        {
            public string Name;
            public Vector3 Focus;
            public float Distance;

            public Zone(string name, float x, float z, float distance = L1Layout.CameraDistance)
            {
                Name = name;
                Focus = new Vector3(x, 0f, z);
                Distance = distance;
            }
        }

        public static readonly Zone[] Zones =
        {
            new Zone("01_Start", 0f, 0f),
            new Zone("02_Banner", 5.5f, 14f),
            new Zone("03_E1", -3f, 40f),
            new Zone("04_Corridor", -3f, 60f),
            new Zone("05_E2", 5f, 85f),
            new Zone("06_Ram", 0f, 128f, 20f),
            new Zone("07_E3", -3f, 180f),
            new Zone("08_E4", 4f, 232f),
            new Zone("09_E5", 0f, 290f),
            new Zone("10_ShrineApproach", -2f, 318f, 20f),
            new Zone("11_Shrine", 2f, 336f),
            new Zone("12_Exit", 4f, 370f, 18f),
            new Zone("13_ExitLookNorth", 4f, 377f, 22f),
        };

        [MenuItem("Kneel/L1/Lighting/Capture Zone Shots")]
        public static void CaptureMenu()
        {
            Debug.Log("[L1] " + CaptureAll("After"));
        }

        public static string CaptureAll(string prefix)
        {
            var sb = new StringBuilder();
            foreach (var zone in Zones)
            {
                Capture(prefix + "_" + zone.Name, zone.Focus, zone.Distance, null);
                sb.Append(zone.Name).Append(' ');
            }

            return "Captured " + sb;
        }

        // Places the Main Camera exactly where PlayerCamera would put it and renders one frame with post-processing.
        public static Texture2D Render(Vector3 focus, float distance)
        {
            var cam = Camera.main;
            var ground = GroundAt(focus);
            var pose = L1Layout.CameraPose(ground, distance);
            cam.transform.SetPositionAndRotation(pose.position, pose.rotation);

            // Stand the player at the focus, as in play (local Volumes follow the player).
            var player = PlayerRoot();
            Vector3 playerHome = player != null ? player.position : Vector3.zero;
            if (player != null)
            {
                player.position = ground;
            }

            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            if (player != null)
            {
                player.position = playerHome;
            }

            return tex;
        }

        // The Player prefab instance at the scene root (its model child is also called "Player").
        public static Transform PlayerRoot()
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Player")
                {
                    return go.transform;
                }
            }

            return null;
        }

        public static string Capture(string name, Vector3 focus, float distance, string subfolder)
        {
            var tex = Render(focus, distance);
            string path = Save(tex, name, subfolder);
            Object.DestroyImmediate(tex);
            return path;
        }

        private static string Save(Texture2D tex, string name, string subfolder)
        {
            string dir = subfolder == null ? Folder : Folder + "/" + subfolder;
            Directory.CreateDirectory(dir);
            string path = dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }

        // ---------------------------------------------------------------- Greyscale readability

        [MenuItem("Kneel/L1/Lighting/Readability Test")]
        public static void ReadabilityMenu()
        {
            Debug.Log("[L1] " + Readability("After"));
        }

        // Stands a figure at 3 spots in each arena, renders with and without it (the difference is its mask),
        // then compares its mean greyscale luminance with a ring of ground around it. Also saves greyscale shots.
        public static string Readability(string prefix)
        {
            var figurePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(L1Build.PrefabPath("K/Characters/Character_Soldier_01_Black"));
            // Keep the player out of the measurement (Render stands it at the focus).
            var player = PlayerRoot();
            bool playerActive = player != null && player.gameObject.activeSelf;
            if (player != null)
            {
                player.gameObject.SetActive(false);
            }

            var report = new StringBuilder("Greyscale contrast (figure vs ground, Weber |dL|/L):\n");
            float worst = float.MaxValue;
            foreach (var arena in L1Layout.Arenas)
            {
                var spots = new[]
                {
                    new Vector3(arena.Center.x, 0f, arena.Center.y),
                    new Vector3(arena.Center.x - arena.Radius * 0.6f, 0f, arena.Center.y + arena.Radius * 0.3f),
                    new Vector3(arena.Center.x + arena.Radius * 0.3f, 0f, arena.Center.y - arena.Radius * 0.6f),
                };

                var figures = new List<GameObject>();
                foreach (var spot in spots)
                {
                    var figure = (GameObject)PrefabUtility.InstantiatePrefab(figurePrefab);
                    figure.transform.position = GroundAt(spot);
                    figure.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    SetCharacterLayer(figure);
                    // The figure's own long dusk shadow would count as "figure" pixels; measure the body only.
                    foreach (var r in figure.GetComponentsInChildren<Renderer>())
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }

                    figures.Add(figure);
                }

                var focus = new Vector3(arena.Center.x, 0f, arena.Center.y);
                foreach (var f in figures)
                {
                    f.SetActive(false);
                }

                var without = Render(focus, L1Layout.CameraDistance);
                foreach (var f in figures)
                {
                    f.SetActive(true);
                }

                var with = Render(focus, L1Layout.CameraDistance);
                float contrast = Contrast(with, without, out float figureL, out float groundL);
                worst = Mathf.Min(worst, contrast);
                report.Append($"  {arena.Name}: figure L {figureL:F3}, ground L {groundL:F3}, contrast {contrast:F2}\n");

                Save(Greyscale(with), prefix + "_Grey_" + arena.Name, "Greyscale");
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

            report.Append($"  Worst: {worst:F2}");
            return report.ToString();
        }

        // Puts every renderer of a character on the Characters rendering layer (plus Default for normal lights).
        public static void SetCharacterLayer(GameObject go)
        {
            uint mask = 1u | (uint)RenderingLayerMask.GetMask(L1LightingPass.CharactersLayer);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask = mask;
            }
        }

        private static Vector3 GroundAt(Vector3 p)
        {
            int ground = 1 << LayerMask.NameToLayer("Ground");
            return Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f, ground) ? hit.point : p;
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }

        // Mean luminance of pixels the figure changed, against a ring of unchanged pixels 3-12 px around them.
        private static float Contrast(Texture2D with, Texture2D without, out float figureL, out float groundL)
        {
            var a = with.GetPixels();
            var b = without.GetPixels();
            var mask = new bool[a.Length];
            double sumFigure = 0;
            int nFigure = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 0.06f)
                {
                    mask[i] = true;
                    sumFigure += Luminance(a[i]);
                    nFigure++;
                }
            }

            double sumGround = 0;
            int nGround = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!mask[i])
                {
                    continue;
                }

                int x = i % Width, y = i / Width;
                for (int k = 0; k < 8; k++)
                {
                    float angle = k * Mathf.PI / 4f;
                    int r = 3 + (i * 7 + k) % 10;
                    int nx = x + Mathf.RoundToInt(Mathf.Cos(angle) * r), ny = y + Mathf.RoundToInt(Mathf.Sin(angle) * r);
                    if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                    {
                        continue;
                    }

                    int j = ny * Width + nx;
                    if (!mask[j])
                    {
                        sumGround += Luminance(b[j]);
                        nGround++;
                    }
                }
            }

            figureL = nFigure > 0 ? (float)(sumFigure / nFigure) : 0f;
            groundL = nGround > 0 ? (float)(sumGround / nGround) : 0f;
            return Mathf.Abs(figureL - groundL) / Mathf.Max(groundL, 0.02f);
        }

        private static Texture2D Greyscale(Texture2D source)
        {
            var px = source.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float l = Luminance(px[i]);
                px[i] = new Color(l, l, l, 1f);
            }

            var tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
