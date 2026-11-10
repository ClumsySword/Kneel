using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kneel.EditorTools
{
    // The throne hall's lava cracks (owner, 2026-11-10, with a reference of glowing branching cracks on black): the
    // crack textures (red: the crack, a hot core with a soft glow round it and hot spots where cracks meet; green: the
    // order it spreads in, 0 at its seed to 1 at its tips), their Kneel/LavaCrack materials, and the hall's panels and the
    // Colossus wired to them. Safe to rerun.
    public static partial class MasterSetup
    {
        private const string CrackFolder = Folder + "Hall/Cracks/";
        public const string RadialCracks = CrackFolder + "T_LavaCracks_Radial.png";
        public const string PanelCracks = CrackFolder + "T_LavaCracks_Panel.png";
        public const string RadialCrackMaterial = CrackFolder + "M_LavaCracks_Radial.mat";
        public const string PanelCrackMaterial = CrackFolder + "M_LavaCracks_Panel.mat";
        private const string RubblePrefab = "Assets/Kneel/Prefabs/L4/Kit/L4_RubbleLow.prefab";

        [MenuItem("Kneel/Enemies/The Master/Build Lava Cracks")]
        public static void BuildLavaCracksMenu() => Debug.Log(BuildLavaCracks());

        public static string BuildLavaCracks()
        {
            EnsureFolder(CrackFolder);
            // The Hammerfall's: 7 cracks out from the impact, forking, over a 16 m square (64 px a metre).
            WriteCracks(RadialCracks, 1024, 7, true, 11);
            // A panel's: three seeds scattered over it, their cracks meeting (a 5-6 m panel on 512 px).
            WriteCracks(PanelCracks, 512, 3, false, 23);
            Material radial = CrackMaterial(RadialCrackMaterial, RadialCracks);
            Material panel = CrackMaterial(PanelCrackMaterial, PanelCracks);
            return "lava cracks: " + RadialCracks + ", " + PanelCracks + "; " + radial.name + ", " + panel.name;
        }

        private static Material CrackMaterial(string path, string texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Kneel/LavaCrack"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Kneel/LavaCrack");
            material.SetTexture("_CrackTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            material.SetColor("_Color", new Color(1f, 0.36f, 0.07f, 1f));
            material.SetColor("_HotColor", new Color(1f, 0.82f, 0.4f, 1f));
            material.SetFloat("_Edge", 0.04f);
            material.SetFloat("_Reveal", 1f);
            material.SetFloat("_Intensity", 1f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        // --- The crack painter ---

        private class CrackCanvas
        {
            public readonly int size;
            public readonly float[] glow;
            public readonly float[] order;
            public float longest;

            public CrackCanvas(int size)
            {
                this.size = size;
                glow = new float[size * size];
                order = Enumerable.Repeat(float.MaxValue, size * size).ToArray();
            }

            // A soft disc: the core (radius w) bright, a glow falling off to 4 w; the spread order where it shows.
            public void Stamp(float x, float y, float w, float travelled, float boost = 1f)
            {
                float reach = w * 4f;
                int x0 = Mathf.Max(0, (int)(x - reach)), x1 = Mathf.Min(size - 1, (int)(x + reach) + 1);
                int y0 = Mathf.Max(0, (int)(y - reach)), y1 = Mathf.Min(size - 1, (int)(y + reach) + 1);
                for (int py = y0; py <= y1; py++)
                {
                    for (int px = x0; px <= x1; px++)
                    {
                        float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                        if (d > reach)
                            continue;
                        float v = d <= w ? 1f - 0.3f * d / w : 0.42f * Mathf.Exp(-(d - w) / (w * 1.1f));
                        v = Mathf.Min(1f, v * boost);
                        int i = py * size + px;
                        if (v > glow[i])
                            glow[i] = v;
                        if (v > 0.1f && travelled < order[i])
                            order[i] = travelled;
                    }
                }

                longest = Mathf.Max(longest, travelled);
            }
        }

        // One crack from (x, y) heading `angle`: kinked straight runs 6-20 px long, each within 0.45 rad of its heading,
        // tapering; it forks now and then (a hot spot where it does).
        private static void PaintCrack(CrackCanvas canvas, System.Random random, float x, float y, float angle, float width, float length, float travelled, int depth)
        {
            float heading = angle;
            float done = 0f;
            while (done < length && width > 0.7f)
            {
                float run = 6f + (float)random.NextDouble() * 14f;
                float dir = heading + ((float)random.NextDouble() * 2f - 1f) * 0.45f;
                for (float s = 0f; s < run && done < length; s += 1.2f)
                {
                    x += Mathf.Cos(dir) * 1.2f;
                    y += Mathf.Sin(dir) * 1.2f;
                    done += 1.2f;
                    if (x < 2 || y < 2 || x > canvas.size - 3 || y > canvas.size - 3)
                        return;
                    float w = width * Mathf.Lerp(1f, 0.35f, done / length);
                    canvas.Stamp(x, y, w, travelled + done);
                }

                // The heading drifts a little, as a crack does.
                heading += ((float)random.NextDouble() * 2f - 1f) * 0.12f;

                if (depth < 2 && random.NextDouble() < (depth == 0 ? 0.16 : 0.1))
                {
                    float side = random.NextDouble() < 0.5 ? -1f : 1f;
                    float childAngle = heading + side * (0.45f + (float)random.NextDouble() * 0.6f);
                    float childLength = (length - done) * (0.35f + (float)random.NextDouble() * 0.3f) + 20f;
                    canvas.Stamp(x, y, width * 1.45f, travelled + done, 1.15f);
                    PaintCrack(canvas, random, x, y, childAngle, width * 0.62f, childLength, travelled + done, depth + 1);
                }
            }
        }

        private static void WriteCracks(string path, int size, int seeds, bool radial, int seed)
        {
            var random = new System.Random(seed);
            var canvas = new CrackCanvas(size);
            float c = size * 0.5f;
            if (radial)
            {
                // From the impact: a hot heart, then the cracks out to the edge.
                canvas.Stamp(c, c, size * 0.009f, 0f, 1.3f);
                for (int i = 0; i < seeds; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.6f - 0.3f) * Mathf.PI * 2f / seeds;
                    PaintCrack(canvas, random, c, c, angle, size * 0.0048f, size * (0.4f + (float)random.NextDouble() * 0.06f), 0f, 0);
                }
            }
            else
            {
                // Seeds spread over the panel, each sending cracks out in several directions.
                for (int i = 0; i < seeds; i++)
                {
                    float x = size * (0.25f + 0.5f * (float)random.NextDouble());
                    float y = size * (0.2f + 0.6f * i / Mathf.Max(1, seeds - 1));
                    canvas.Stamp(x, y, size * 0.011f, 0f, 1.3f);
                    int arms = 3 + random.Next(2);
                    float start = (float)random.NextDouble() * Mathf.PI * 2f;
                    for (int a = 0; a < arms; a++)
                        PaintCrack(canvas, random, x, y, start + a * Mathf.PI * 2f / arms + ((float)random.NextDouble() - 0.5f) * 0.6f, size * 0.0075f, size * (0.32f + (float)random.NextDouble() * 0.2f), 0f, 0);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float g = canvas.order[i] == float.MaxValue ? 1f : canvas.order[i] / Mathf.Max(canvas.longest, 1f);
                pixels[i] = new Color32((byte)(Mathf.Clamp01(canvas.glow[i]) * 255f), (byte)(Mathf.Clamp01(g) * 255f), 0, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }

        // --- Wiring ---

        // The hall's panels: each crack decal on the panel cracks, driven by HallFloor; the burst's materials. Saves L5.
        [MenuItem("Kneel/Enemies/The Master/Wire Lava Cracks in L5")]
        public static void WireLavaCracksMenu() => Debug.Log(WireLavaCracks());

        public static string WireLavaCracks()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != L5Scene)
                return "open L5_ThroneRoom first (active: " + scene.path + ")";

            var floor = Object.FindAnyObjectByType<HallFloor>(FindObjectsInactive.Include);
            if (floor == null)
                return "no HallFloor: Build Hall Systems first";

            Material panelMaterial = AssetDatabase.LoadAssetAtPath<Material>(PanelCrackMaterial);
            Texture2D panelMap = AssetDatabase.LoadAssetAtPath<Texture2D>(PanelCracks);
            int wired = 0;
            foreach (HallFloor.Panel panel in floor.panels)
            {
                if (panel.crack == null)
                    continue;
                var renderer = panel.crack.GetComponent<Renderer>();
                renderer.sharedMaterial = panelMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var cracks = panel.crack.GetComponent<LavaCracks>() ?? panel.crack.AddComponent<LavaCracks>();
                cracks.map = panelMap;
                wired++;
            }

            floor.emberMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Ember.mat");
            floor.smokeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/Levels/L1/Materials/FX/L1_FX_Smoke.mat");
            (floor.debrisMesh, floor.debrisMaterial) = Debris();
            EditorUtility.SetDirty(floor);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            return "lava cracks on " + wired + " panels; saved " + scene.path;
        }

        // The stones a burst throws up: L4's low rubble.
        public static (Mesh, Material) Debris()
        {
            var rubble = AssetDatabase.LoadAssetAtPath<GameObject>(RubblePrefab);
            MeshFilter filter = rubble != null ? rubble.GetComponentInChildren<MeshFilter>() : null;
            MeshRenderer renderer = rubble != null ? rubble.GetComponentInChildren<MeshRenderer>() : null;
            return (filter != null ? filter.sharedMesh : null, renderer != null ? renderer.sharedMaterial : null);
        }
    }
}
