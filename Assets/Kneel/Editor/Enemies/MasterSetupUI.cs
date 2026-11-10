using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.EditorTools
{
    // The Master's boss bar, built as a prefab in the main and pause menus' style (owner, 2026-11-10: "premium, much
    // like the main menu and the pause menu"): their font (Cormorant Garamond SemiBold, wide tracking), their palette
    // (bone white text, ember rules that fade at both ends, a soft dark pool behind), their ember seam and its image
    // sparks. Rerun after changing it; MasterBoss spawns the prefab.
    public static partial class MasterSetup
    {
        public const string BossBarPath = Folder + "UI/BossHealthBar.prefab";
        private const string BarFillSpritePath = Folder + "UI/T_BossBarFill.png";
        private const string MenuArt = "Assets/Kneel/UI/Art/";

        private static readonly Color MenuEmber = Hex(0xC8641E);
        private static readonly Color MenuBright = Hex(0xF2E6D0);

        [MenuItem("Kneel/Enemies/The Master/Build Boss Bar")]
        public static void BuildBossBar()
        {
            EnsureFolder(Folder + "UI");
            Sprite radial = AssetDatabase.LoadAssetAtPath<Sprite>(MenuArt + "RadialSoft.png");
            Sprite emberStrip = AssetDatabase.LoadAssetAtPath<Sprite>(MenuArt + "EmberStrip.png");
            Material additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/Kneel/UI/Materials/M_UIAdditive.mat");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Kneel/UI/Fonts/CormorantGaramond-SemiBold SDF.asset");
            Sprite fillSprite = FillSprite();

            var root = new GameObject("BossHealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.layer = LayerMask.NameToLayer("UI");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            const float width = 1100f;
            const float height = 9f;

            // Top centre: the name, then the bar 44 px under it.
            RectTransform panel = UiRect("Panel", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(width + 200f, 120f));

            // A soft dark pool behind, as behind the pause menu's list, so it reads over a torch-lit patch.
            UiImage("Backdrop", panel, radial, new Color(0f, 0f, 0f, 0.62f), Vector2.zero, new Vector2(width + 420f, 190f));

            // The name between two ember rules, each ending in a diamond next to the words.
            var title = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(panel, false);
            title.font = font;
            title.text = "THE MASTER";
            title.fontSize = 34f;
            title.characterSpacing = 16f;
            title.color = MenuBright;
            title.alignment = TextAlignmentOptions.Center;
            title.raycastTarget = false;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Place(title.rectTransform, new Vector2(0f, 22f), new Vector2(560f, 50f));
            foreach (float side in new[] { -1f, 1f })
            {
                UiImage(side < 0f ? "RuleLeft" : "RuleRight", panel, radial, Alpha(MenuEmber, 0.65f), new Vector2(side * 330f, 20f), new Vector2(200f, 2f));
                Diamond(side < 0f ? "DiamondLeft" : "DiamondRight", panel, new Vector2(side * 212f, 20f), 7f, MenuEmber);
            }

            // The bar.
            RectTransform bar = UiRect("Bar", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(width, height));
            UiImage("Halo", bar, radial, Hex(0xFF7A2A, 0.07f), Vector2.zero, new Vector2(width + 160f, 46f));
            foreach (float y in new[] { 8f, -8f })
                UiImage(y > 0f ? "HairlineTop" : "HairlineBottom", bar, radial, Alpha(MenuEmber, 0.5f), new Vector2(0f, y), new Vector2(width + 120f, 1.5f));

            Image back = UiImage("Back", bar, null, Hex(0x140808, 0.9f), Vector2.zero, new Vector2(width, height));
            back.raycastTarget = false;
            Image trail = Filled("Trail", bar, fillSprite, Alpha(MenuBright, 0.42f), width, height);
            Image fill = Filled("Fill", bar, fillSprite, Hex(0xB8461A), width, height);
            Image seam = Filled("Seam", bar, emberStrip, Hex(0xFFB868, 0.7f), width, height * 0.6f);
            seam.material = additive;
            Image flash = Filled("Flash", bar, fillSprite, new Color(1f, 0.93f, 0.8f, 0f), width, height);

            // End caps: the menus' diamond, with a faint ember halo.
            foreach (float side in new[] { -1f, 1f })
            {
                UiImage(side < 0f ? "CapHaloLeft" : "CapHaloRight", bar, radial, Hex(0xFF7A2A, 0.25f), new Vector2(side * (width * 0.5f + 12f), 0f), new Vector2(34f, 22f)).material = additive;
                Diamond(side < 0f ? "CapLeft" : "CapRight", bar, new Vector2(side * (width * 0.5f + 12f), 0f), 9f, MenuEmber);
            }

            // The leading edge: a hot glow where the fill ends, shedding the odd spark.
            RectTransform edge = UiRect("Edge", bar, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1f, 1f));
            Image edgeGlow = UiImage("Glow", edge, radial, Hex(0xFFB868, 0.85f), Vector2.zero, new Vector2(46f, 30f));
            edgeGlow.material = additive;
            Image edgeCore = UiImage("Core", edge, radial, new Color(1f, 0.95f, 0.85f, 0.9f), Vector2.zero, new Vector2(10f, 14f));
            edgeCore.material = additive;
            RectTransform sparksHost = UiRect("Sparks", edge, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var sparks = sparksHost.gameObject.AddComponent<Kneel.UI.UISparks>();
            var sparksSo = new SerializedObject(sparks);
            sparksSo.FindProperty("sprite").objectReferenceValue = radial;
            sparksSo.FindProperty("material").objectReferenceValue = additive;
            sparksSo.FindProperty("rate").floatValue = 6f;
            sparksSo.FindProperty("size").vector2Value = new Vector2(2f, 3.5f);
            sparksSo.FindProperty("lifetime").vector2Value = new Vector2(0.5f, 1.1f);
            sparksSo.FindProperty("rise").vector2Value = new Vector2(14f, 32f);
            sparksSo.FindProperty("drift").floatValue = 12f;
            sparksSo.ApplyModifiedPropertiesWithoutUndo();

            var view = root.AddComponent<BossHealthBar>();
            var so = new SerializedObject(view);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("seam").objectReferenceValue = seam;
            so.FindProperty("trail").objectReferenceValue = trail;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("edge").objectReferenceValue = edge;
            so.FindProperty("edgeSparks").objectReferenceValue = sparks;
            so.FindProperty("width").floatValue = width;
            SerializedProperty flicker = so.FindProperty("flicker");
            Image[] flickering = { seam, edgeGlow };
            flicker.arraySize = flickering.Length;
            for (int i = 0; i < flickering.Length; i++)
                flicker.GetArrayElementAtIndex(i).objectReferenceValue = flickering[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            PrefabUtility.SaveAsPrefabAsset(root, BossBarPath);
            Object.DestroyImmediate(root);
            Debug.Log("Built " + BossBarPath);
        }

        // The fill's texture: a vertical profile, lit along its upper third and darker at the foot, like a seam of
        // embers seen edge-on. Tinted by the Image's colour.
        private static Sprite FillSprite()
        {
            if (File.Exists(BarFillSpritePath) == false)
            {
                var texture = new Texture2D(4, 32, TextureFormat.RGBA32, false);
                for (int y = 0; y < 32; y++)
                {
                    float v = y / 31f;
                    float lit = Mathf.Exp(-Mathf.Pow((v - 0.68f) / 0.16f, 2f));
                    float shade = Mathf.Lerp(0.55f, 0.85f, v) + 0.45f * lit;
                    for (int x = 0; x < 4; x++)
                        texture.SetPixel(x, y, new Color(Mathf.Min(shade, 1f), Mathf.Min(shade, 1f), Mathf.Min(shade, 1f), 1f));
                }

                File.WriteAllBytes(BarFillSpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(BarFillSpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(BarFillSpritePath);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(BarFillSpritePath);
        }

        private static RectTransform UiRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static Image UiImage(string name, Transform parent, Sprite sprite, Color color, Vector2 position, Vector2 size)
        {
            RectTransform rt = UiRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Image Filled(string name, RectTransform bar, Sprite sprite, Color color, float width, float height)
        {
            Image image = UiImage(name, bar, sprite, color, Vector2.zero, new Vector2(width, height));
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            return image;
        }

        // A small solid diamond (a square turned 45 degrees).
        private static void Diamond(string name, Transform parent, Vector2 position, float size, Color color)
        {
            Image image = UiImage(name, parent, null, color, position, new Vector2(size, size));
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
