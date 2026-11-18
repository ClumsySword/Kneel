using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools.Transitions
{
    // The seams' roads, made of each level's own ground (owner, 2026-11-18: "It should be natural. Blend in with the
    // existing ground"). Where a level's ground is a painted layout (L1's URP Lit ground, L2's Kneel/Ground terrain), the
    // road is painted into that layout in the colour of the level's own paths, its width wandering and its edges
    // feathered, so the shader's own detail runs on across it. Each paint starts from the layout as it was before any
    // road (a copy kept in LayoutSources~, which Unity doesn't import), so reruns don't build up; a level's roads are
    // all painted in one pass. Where the ground is a plain plane (L1's far ground), the road is a ribbon in that
    // ground's own material and detail, a shade of the path colour, feathered by an alpha mask (PathMask).
    public static class SeamRoads
    {
        public const string SourceFolder = "Assets/Kneel/Editor/Transitions/LayoutSources~";

        public struct Road
        {
            public Vector3[] points;
            public float width;
            // A point on one of the level's own paths: the road takes the layout's colour there.
            public Vector3 colourFrom;
        }

        // Paints every road on the active level's ground layout(s). Returns a summary.
        public static string Paint(Road[] roads, int seed)
        {
            Physics.SyncTransforms();
            int ground = LayerMask.GetMask("Ground");
            var layouts = new Dictionary<string, Texture2D>();
            var weights = new Dictionary<string, float[]>();
            var targets = new Dictionary<string, Color[]>();
            var log = new List<string>();

            Texture2D Layout(string path)
            {
                if (layouts.TryGetValue(path, out Texture2D t))
                {
                    return t;
                }

                Directory.CreateDirectory(SourceFolder);
                string source = SourceFolder + "/" + Path.GetFileName(path);
                if (File.Exists(source) == false)
                {
                    File.Copy(path, source);
                }

                t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(source));
                layouts[path] = t;
                weights[path] = new float[t.width * t.height];
                targets[path] = new Color[t.width * t.height];
                return t;
            }

            bool Hit(Vector3 p, out RaycastHit hit, out string path)
            {
                path = null;
                if (Physics.Raycast(new Vector3(p.x, 80f, p.z), Vector3.down, out hit, 200f, ground, QueryTriggerInteraction.Ignore) && hit.collider is MeshCollider)
                {
                    var r = hit.collider.GetComponent<Renderer>();
                    Texture tex = r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseMap") ? r.sharedMaterial.GetTexture("_BaseMap") : null;
                    path = tex != null ? AssetDatabase.GetAssetPath(tex) : null;
                    return path != null && path.EndsWith(".png");
                }

                return false;
            }

            for (int n = 0; n < roads.Length; n++)
            {
                Road road = roads[n];
                // The path colour: the layout's mean in a 1.5 m disc at the reference point.
                Color colour = Color.clear;
                int samples = 0;
                for (float dx = -0.75f; dx <= 0.75f; dx += 0.25f)
                {
                    for (float dz = -0.75f; dz <= 0.75f; dz += 0.25f)
                    {
                        if (Hit(road.colourFrom + new Vector3(dx, 0f, dz), out RaycastHit h, out string p))
                        {
                            colour += Layout(p).GetPixelBilinear(h.textureCoord.x, h.textureCoord.y);
                            samples++;
                        }
                    }
                }

                if (samples == 0)
                {
                    log.Add($"road {n}: no layout under its colour point");
                    continue;
                }

                colour /= samples;
                float total = 0f;
                for (int i = 1; i < road.points.Length; i++)
                {
                    total += Vector3.Distance(Flat(road.points[i - 1]), Flat(road.points[i]));
                }

                int painted = 0;
                float reach = road.width * 0.5f + 1.2f;
                const float step = 0.07f;
                for (float d = 0f; d <= total; d += step)
                {
                    Vector3 c = Along(road.points, d, out Vector3 dir);
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
                    // The road's width wanders, more slowly than its edges fray; it tapers in over its last 4 m.
                    float half = road.width * 0.5f * (0.85f + 0.3f * Noise(d * 0.08f, n * 7.3f + seed));
                    half *= Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(d, total - d) / 4f)));
                    for (float s = -reach; s <= reach; s += step)
                    {
                        float edge = half + (Noise(d * 0.9f, s * 0.9f + seed) - 0.5f) * 0.7f;
                        float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(s) - (edge - 0.9f)) / 1.3f));
                        if (w <= 0.001f || Hit(c + right * s, out RaycastHit h, out string p) == false)
                        {
                            continue;
                        }

                        Texture2D t = Layout(p);
                        int px = Mathf.Clamp(Mathf.FloorToInt(h.textureCoord.x * t.width), 0, t.width - 1);
                        int py = Mathf.Clamp(Mathf.FloorToInt(h.textureCoord.y * t.height), 0, t.height - 1);
                        int k = py * t.width + px;
                        // Trodden unevenly: the path colour, a little darker or paler in patches.
                        float tone = 0.92f + 0.16f * Noise(d * 0.5f + 31f, s * 0.5f + seed);
                        if (w * 0.9f > weights[p][k])
                        {
                            weights[p][k] = w * 0.9f;
                            targets[p][k] = colour * tone;
                            painted++;
                        }
                    }
                }

                log.Add($"road {n}: {total:0} m, colour #{ColorUtility.ToHtmlStringRGB(colour)}, {painted} texels");
            }

            foreach (var kv in layouts)
            {
                Texture2D t = kv.Value;
                Color[] px = t.GetPixels();
                float[] w = weights[kv.Key];
                Color[] target = targets[kv.Key];
                for (int i = 0; i < px.Length; i++)
                {
                    if (w[i] > 0f)
                    {
                        Color c = Color.Lerp(px[i], target[i], w[i]);
                        c.a = px[i].a;
                        px[i] = c;
                    }
                }

                t.SetPixels(px);
                t.Apply();
                File.WriteAllBytes(kv.Key, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(kv.Key, ImportAssetOptions.ForceUpdate);
                log.Add("painted " + kv.Key);
            }

            return string.Join("; ", log);
        }

        // Puts a level's layout(s) back as they were before any road.
        public static void Restore(string layoutPath)
        {
            string source = SourceFolder + "/" + Path.GetFileName(layoutPath);
            if (File.Exists(source))
            {
                File.Copy(source, layoutPath, true);
                AssetDatabase.ImportAsset(layoutPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // A ribbon road on a plain ground plane, in a transparent copy of that ground's own material: its colour
        // `tint`, its detail map tiled as on the plane (`detailTileMetres`), feathered by PathMask's alpha.
        public static GameObject Ribbon(Transform parent, string name, Vector3[] points, Material ground, Color tint, Vector2 detailTileMetres, float width)
        {
            string path = SeamCopy.MaterialFolder + "/Road_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(ground);
                AssetDatabase.CreateAsset(m, path);
            }

            m.CopyPropertiesFromMaterial(ground);
            m.SetTexture("_BaseMap", PathMask());
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_DetailAlbedoMap"))
            {
                // The ribbon's UVs: u across its width, v along it at 6 m to 1.
                m.SetTextureScale("_DetailAlbedoMap", new Vector2(width / detailTileMetres.x, 6f / detailTileMetres.y));
            }

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return SeamKit.Road(parent, name, points, m, width, false);
        }

        // White, its alpha a band across u: solid in the middle, feathered and ragged at both edges; tileable along v.
        public static Texture2D PathMask()
        {
            string path = SeamCopy.MaterialFolder + "/T_PathMask.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int w = 128, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), v = y / (float)h;
                    float edge = Mathf.Min(u, 1f - u) + (TileNoise(u * 4f + 5f, v * 12f, 12) - 0.5f) * 0.18f;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.02f) / 0.3f));
                    a *= 0.9f + 0.1f * TileNoise(u * 10f, v * 30f, 30);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            Directory.CreateDirectory(SeamCopy.MaterialFolder);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapModeU = TextureWrapMode.Clamp;
            importer.wrapModeV = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 Along(Vector3[] points, float distance, out Vector3 dir)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = Flat(points[i - 1]), b = Flat(points[i]);
                float len = Vector3.Distance(a, b);
                if (distance <= len || i == points.Length - 1)
                {
                    dir = (b - a).normalized;
                    return a + dir * Mathf.Min(distance, len);
                }

                distance -= len;
            }

            dir = Vector3.forward;
            return Flat(points[0]);
        }

        private static float Noise(float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx), Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx), fy);
        }

        private static float TileNoise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Y(int k) => ((k % period) + period) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, Y(y0)), Hash(x0 + 1, Y(y0)), fx), Mathf.Lerp(Hash(x0, Y(y0 + 1)), Hash(x0 + 1, Y(y0 + 1)), fx), fy);
        }

        private static float Hash(int i, int k)
        {
            uint n = (uint)(i * 374761393 + k * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return ((n ^ (n >> 16)) & 0xffff) / 65535f;
        }
    }
}
