using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kneel.EditorTools.Transitions
{
    // Look-dev captures for the seams, through the URP post stack (a camera render without it skips the grade and
    // reads far darker than the Game view). Also a dump of what stands around a point, to author against.
    public static class TransitionShots
    {
        public static string Folder = "Temp/TransitionShots";

        public static Camera MakeCamera()
        {
            Camera main = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            var go = new GameObject("__TransitionShot") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
            }

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var mainData = main != null ? main.GetComponent<UniversalAdditionalCameraData>() : null;
            if (mainData != null)
            {
                data.volumeLayerMask = mainData.volumeLayerMask;
                data.renderShadows = mainData.renderShadows;
                data.SetRenderer(0);
            }

            return cam;
        }

        // The gameplay camera's pose for a player standing at `feet` (PlayerCamera's own formula).
        public static void GameplayPose(Vector3 feet, PlayerCameraSettings s, float distance, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(s.pitch, s.yaw, 0f);
            Vector3 focus = feet + Vector3.up * s.targetHeight;
            position = focus - rotation * Vector3.forward * distance;
        }

        public static string Shot(string name, Vector3 position, Quaternion rotation, float fov, int width = 1280, int height = 720)
        {
            Directory.CreateDirectory(Folder);
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude))
            {
                if (ps.main.loop && (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null))
                {
                    ps.Simulate(8f, true, true, true);
                }
            }

            Camera cam = MakeCamera();
            cam.transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            string path = Path.Combine(Folder, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            return path;
        }

        // The loading screen's placeholder art for a level (0-based index): a 2560 x 1440 capture through the post stack,
        // graded darker and desaturated so it reads as a stand-in, written to the slot's file (the owner overwrites it
        // with the concept art under the same name) and labelled "Placeholder".
        public static string Placeholder(int level, Vector3 position, Quaternion rotation, float fov, float darken = 0.72f, float desaturate = 0.55f)
        {
            const int width = 2560;
            const int height = 1440;
            string temp = Shot("placeholder_raw_L" + (level + 1), position, rotation, fov, width, height);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(File.ReadAllBytes(temp));
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                float luma = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                c = Color.Lerp(c, new Color(luma, luma, luma), desaturate) * darken;
                // A soft vignette, so the line low on the screen always sits on dark.
                int x = i % width;
                int y = i / width;
                float u = (x / (float)width - 0.5f) * 2f;
                float v = (y / (float)height - 0.5f) * 2f;
                float vignette = Mathf.Clamp01(1f - 0.35f * (u * u * 0.6f + v * v));
                float bottom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, y / (float)height));
                c *= vignette * Mathf.Lerp(0.55f, 1f, bottom);
                c.a = 1f;
                px[i] = c;
            }

            tex.SetPixels(px);
            tex.Apply();
            string path = LevelTransitionSetup.ArtPathFor(level);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            AssetDatabase.SetLabels(asset, new[] { "Placeholder", "LoadingArt" });

            var catalog = AssetDatabase.LoadAssetAtPath<Kneel.Core.LevelTransitionCatalog>(LevelTransitionSetup.CatalogPath);
            string scene = Kneel.Core.LevelCatalog.Levels[level].Scene;
            foreach (var seam in catalog.seams)
            {
                if (seam.fromScene == scene)
                {
                    seam.art = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    seam.artIsPlaceholder = true;
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return path;
        }

        // Renderers whose bounds come within `radius` of a point (top-level prefab instances or named groups only).
        public static string Around(Vector3 point, float radius, int max = 120)
        {
            var sb = new StringBuilder();
            var seen = new System.Collections.Generic.HashSet<Transform>();
            var list = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.bounds.SqrDistance(point) < radius * radius)
                .Select(r => Owner(r.transform))
                .Where(t => seen.Add(t))
                .OrderBy(t => (t.position - point).sqrMagnitude)
                .Take(max);
            foreach (Transform t in list)
            {
                Bounds b = BoundsOf(t);
                sb.AppendLine($"{TransitionSurvey.PathOf(t)}  at {TransitionSurvey.V(t.position)} yaw {t.eulerAngles.y:0}  size {TransitionSurvey.V(b.size)} top {b.max.y:0.#} layer {LayerMask.LayerToName(t.gameObject.layer)}");
            }

            return sb.ToString();
        }

        private static Transform Owner(Transform t)
        {
            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
            return root != null ? root.transform : t;
        }

        public static Bounds BoundsOf(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0)
            {
                return new Bounds(t.position, Vector3.zero);
            }

            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs)
            {
                b.Encapsulate(r.bounds);
            }

            return b;
        }
    }
}
