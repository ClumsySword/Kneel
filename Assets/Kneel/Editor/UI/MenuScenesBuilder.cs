using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kneel.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Kneel.EditorTools
{
    // Builds the Splash and MainMenu scenes and the in-game pause menu prefab from scratch, plus everything they
    // use: the generated glow, ember and key-hint sprites, the TMP font assets, the additive materials, the audio
    // mixer and the menu's post volume. It then puts Splash and MainMenu first in the Build Profiles scene list.
    // Safe to rerun: the scenes and prefab are regenerated every time, the assets only when missing.
    public static class MenuScenesBuilder
    {
        public const string Root = "Assets/Kneel/UI";
        public const string SplashPath = Root + "/Scenes/Splash.unity";
        public const string MainMenuPath = Root + "/Scenes/MainMenu.unity";
        public const string MixerPath = "Assets/Kneel/Audio/Resources/KneelMixer.mixer";
        public const string PausePrefabPath = Root + "/Resources/PauseMenu.prefab";

        private const string ArtDir = Root + "/Art";
        private const string FontDir = Root + "/Fonts";
        private const string MaterialDir = Root + "/Materials";
        private const string SettingsDir = Root + "/Settings";
        private const string LogoPath = ArtDir + "/KneelLogo.png";
        private const string Logo4Path = ArtDir + "/KneelLogo4.png";
        private const string RadialPath = ArtDir + "/RadialSoft.png";
        private const string EmberStripPath = ArtDir + "/EmberStrip.png";
        private const string MenuMusicDir = "Assets/Kneel/Audio/Music/MainMenu";
        private const string ThemePath = MenuMusicDir + "/kneel_main_theme.mp3";
        private const string FirePath = MenuMusicDir + "/fire_sound_effect.mp3";
        private const string KeyCapPath = ArtDir + "/KeyCap.png";
        private const string PadButtonPath = ArtDir + "/PadButton.png";

        private static readonly string[] GameScenes =
        {
            "Assets/Kneel/Scenes/L1_BrokenLine_Playable_L.unity",
            "Assets/Kneel/Scenes/Levels/L2_HollowVillage.unity",
            "Assets/Kneel/Scenes/Levels/L3_FallowFields.unity",
            "Assets/Kneel/Scenes/Levels/L4_BurningGate.unity",
            "Assets/Kneel/Scenes/Levels/L5_ThroneRoom.unity",
            "Assets/Kneel/Scenes/Arena_S.unity",
        };

        private const string Charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~©×‹›–—·…’";

        // Palette: the logo's stone grey and ember orange.
        private static readonly Color Ember = Hex(0xC8641E);
        private static readonly Color Unselected = Hex(0x8C8478, 0.7f);
        private static readonly Color Bright = Hex(0xF2E6D0);
        private static readonly Color FooterColor = Hex(0x8C8478, 0.45f);
        private static readonly Color HeaderColor = Hex(0xB0764A, 0.85f);

        private static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        // While building the pause menu: highlight sparks as UI images (its overlay canvas can't show particles).
        private static bool imageSparks;

        private class MenuAssets
        {
            public Sprite logo;
            public Sprite logo4;
            public Sprite radial;
            public Sprite emberStrip;
            public Sprite keyCap;
            public Sprite padButton;
            private TMP_FontAsset bodyFont;
            private TMP_FontAsset titleFont;

            // Reloaded whenever the cached asset has been unloaded. The first build after a script recompile
            // sometimes lost it partway, and labels given the dead reference fell back to TMP's default sans font.
            public TMP_FontAsset body => bodyFont != null ? bodyFont : bodyFont = EnsureFont("CormorantGaramond-Medium");

            public TMP_FontAsset title => titleFont != null ? titleFont : titleFont = EnsureFont("CormorantGaramond-SemiBold");
            public Material logoMaterial;
            public Material menuLogoMaterial;
            public Material particleMaterial;
            public AudioMixer mixer;
            public AudioClip theme;
            public AudioClip fire;
            public VolumeProfile volume;
            public VolumeProfile menuVolume;
        }

        [MenuItem("Tools/Kneel/Build Menu Scenes")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            MenuAssets a = EnsureAssets();
            BuildSplash(a);
            BuildMainMenu(a);
            BuildPauseMenu(a);
            UpdateBuildScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MainMenuPath);
            Debug.Log("Built " + SplashPath + ", " + MainMenuPath + " and " + PausePrefabPath + ".");
        }

        // ---------------------------------------------------------------- assets

        private static MenuAssets EnsureAssets()
        {
            EnsureFolder(ArtDir);
            EnsureFolder(FontDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(SettingsDir);
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Path.GetDirectoryName(MixerPath).Replace('\\', '/'));

            var a = new MenuAssets
            {
                logo = ImportSprite(LogoPath),
                logo4 = ImportSprite(Logo4Path, true),
                radial = EnsureGeneratedSprite(RadialPath, 256, 256, (x, y) =>
                {
                    float r = Mathf.Clamp01(new Vector2(x, y).magnitude);
                    float k = 1f - r * r;
                    return k * k;
                }),
                emberStrip = EnsureGeneratedSprite(EmberStripPath, 1024, 32, EmberStrip),
                keyCap = EnsureSlicedSprite(KeyCapPath, (x, y) => RoundedFrame(x, y, 10f, 2.5f, 0.1f), 14f),
                padButton = EnsureGeneratedSprite(PadButtonPath, 64, 64, (x, y) => RoundedFrame(x, y, 30f, 2.5f, 0.1f)),
            };

            Shader additive = Shader.Find("Kneel/UI/Additive");
            a.logoMaterial = EnsureMaterial(MaterialDir + "/M_UIAdditive.mat", additive, null);

            // The title screen's logo clips its near-black (compression noise, 1-3/255) so no box shows on a black
            // background. The project renders in Linear space, so this is linear: 0.002 is about 7/255 in sRGB. (0.02
            // was ~39/255 and stripped the blue out of the orange cracks, visibly oversaturating them.)
            a.menuLogoMaterial = EnsureMaterial(MaterialDir + "/M_UIAdditiveLogo.mat", additive, null);
            a.menuLogoMaterial.SetFloat("_BlackPoint", 0.002f);
            a.particleMaterial = EnsureMaterial(MaterialDir + "/M_EmberParticle.mat", additive, a.radial.texture);
            a.mixer = EnsureMixer();

            // Long music streams from disk; the short fire loop sits in memory.
            a.theme = ImportAudio(ThemePath, AudioClipLoadType.Streaming);
            a.fire = ImportAudio(FirePath, AudioClipLoadType.CompressedInMemory);
            // Splash: warm-tinted bloom on the helmet's embers. Main menu: a lighter, neutral bloom, so the logo keeps
            // its own colours (the warm tint pushed it visibly more saturated than the art).
            a.volume = EnsureVolumeProfile("MenuVolumeProfile.asset", 0.75f, 0.6f, Hex(0xFFD2A8));
            a.menuVolume = EnsureVolumeProfile("MainMenuVolumeProfile.asset", 0.9f, 0.3f, Color.white);
            return a;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Generated gradients stay uncompressed (compression bands them); a large photo-like logo is compressed at
        // high quality instead, at full size.
        private static Sprite ImportSprite(string path, bool largeArt = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            TextureImporterCompression compression = largeArt ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            int maxSize = largeArt ? 4096 : 2048;
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.textureCompression != compression || importer.maxTextureSize != maxSize)
            {
                importer.maxTextureSize = maxSize;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = compression;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Vorbis at the given load type. Null (and the scene builds silent) when the file isn't there.
        private static AudioClip ImportAudio(string path, AudioClipLoadType loadType)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"No audio at {path}; that part of the menu stays silent.");
                return null;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool stream = loadType == AudioClipLoadType.Streaming;
            if (settings.loadType != loadType || settings.compressionFormat != AudioCompressionFormat.Vorbis || importer.loadInBackground != stream)
            {
                settings.loadType = loadType;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = stream;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // White texture whose alpha is f(x, y), x and y in -1..1.
        private static Sprite EnsureGeneratedSprite(string path, int width, int height, Func<float, float, float> alpha)
        {
            if (!File.Exists(path))
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float u = (x + 0.5f) / width * 2f - 1f;
                        float v = (y + 0.5f) / height * 2f - 1f;
                        pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
                    }
                }

                tex.SetPixels32(pixels);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            return ImportSprite(path);
        }

        // A glowing seam of coals: bright and broken along its length (layered value noise), tapered at the ends,
        // soft across. u, v in -1..1.
        private static float EmberStrip(float u, float v)
        {
            float Hash(int i)
            {
                float h = Mathf.Sin(i * 12.9898f) * 43758.5453f;
                return h - Mathf.Floor(h);
            }

            float Noise(float x)
            {
                int i = Mathf.FloorToInt(x);
                float f = x - i;
                return Mathf.Lerp(Hash(i), Hash(i + 1), f * f * (3f - 2f * f));
            }

            float x = (u + 1f) * 0.5f;
            float n = 0.5f * Noise(x * 9f) + 0.3f * Noise(x * 23f + 7f) + 0.2f * Noise(x * 57f + 13f);
            float taper = Mathf.Pow(Mathf.Clamp01(1f - u * u), 1.5f);
            float across = Mathf.Exp(-v * v / 0.12f);
            return taper * across * (0.25f + 0.75f * n);
        }

        private static TMP_FontAsset EnsureFont(string fontName)
        {
            string path = FontDir + "/" + fontName + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
            {
                return existing;
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + fontName + ".ttf");
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            asset.name = fontName + " SDF";
            if (!asset.TryAddCharacters(Charset, out string missing))
            {
                Debug.LogWarning($"{fontName}: no glyphs for '{missing}'.");
            }

            // Static: the atlas is baked now and never grows at runtime, so the asset doesn't churn in git.
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTexture.name = fontName + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = fontName + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        private static Material EnsureMaterial(string path, Shader shader, Texture texture)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_Tint", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // Unity has no public API for building a mixer, so this goes through the editor's internal
        // AudioMixerController: Master with Music and SFX children, their volumes exposed as MusicVolume / SFXVolume
        // (SettingsRuntime's names). Master's own fader stays at 0 dB: master volume is AudioListener.volume.
        private static AudioMixer EnsureMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer != null)
            {
                return mixer;
            }

            Assembly editor = typeof(Editor).Assembly;
            Type controllerType = editor.GetType("UnityEditor.Audio.AudioMixerController");
            Type groupType = editor.GetType("UnityEditor.Audio.AudioMixerGroupController");
            Type pathType = editor.GetType("UnityEditor.Audio.AudioGroupParameterPath");
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            object controller = controllerType.GetMethod("CreateMixerControllerAtPath", all).Invoke(null, new object[] { MixerPath });
            object master = controllerType.GetProperty("masterGroup", all).GetValue(controller);
            foreach (string groupName in new[] { "Music", "SFX" })
            {
                object group = controllerType.GetMethod("CreateNewGroup", all).Invoke(controller, new object[] { groupName, false });
                controllerType.GetMethod("AddChildToParent", all).Invoke(controller, new[] { group, master });

                object guid = groupType.GetMethod("GetGUIDForVolume", all).Invoke(group, null);
                object parameterPath = Activator.CreateInstance(pathType, all, null, new[] { group, guid }, null);
                controllerType.GetMethod("AddExposedParameter", all).Invoke(controller, new[] { parameterPath });

                // Rename the new exposed parameter from its default name.
                PropertyInfo exposedProp = controllerType.GetProperty("exposedParameters", all);
                var exposed = (Array)exposedProp.GetValue(controller);
                for (int i = 0; i < exposed.Length; i++)
                {
                    object p = exposed.GetValue(i);
                    FieldInfo guidField = p.GetType().GetField("guid", all);
                    if (guidField.GetValue(p).Equals(guid))
                    {
                        p.GetType().GetField("name", all).SetValue(p, groupName == "Music" ? "MusicVolume" : "SFXVolume");
                        exposed.SetValue(p, i);
                    }
                }

                exposedProp.SetValue(controller, exposed);
            }

            // One mixer-window view showing every group (a new controller has none, and the window then lists nothing).
            Type viewType = editor.GetType("UnityEditor.Audio.MixerGroupView");
            var groups = (System.Collections.IList)controllerType.GetMethod("GetAllAudioGroupsSlow", all).Invoke(controller, null);
            Type guidType = groupType.GetProperty("groupID", all).PropertyType;
            Array guids = Array.CreateInstance(guidType, groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                guids.SetValue(groupType.GetProperty("groupID", all).GetValue(groups[i]), i);
            }

            object view = Activator.CreateInstance(viewType);
            viewType.GetField("name", all).SetValue(view, "View");
            viewType.GetField("guids", all).SetValue(view, guids);
            Array views = Array.CreateInstance(viewType, 1);
            views.SetValue(view, 0);
            controllerType.GetProperty("views", all).SetValue(controller, views);

            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        // Post for the menu scenes: no tonemapping (keeps the UI's colours exact) and a soft bloom. Values are
        // (re)applied on every build, so tuning here takes effect on the next run.
        private static VolumeProfile EnsureVolumeProfile(string fileName, float threshold, float intensity, Color tint)
        {
            string path = SettingsDir + "/" + fileName;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = AddToProfile<Tonemapping>(profile);
            }

            tonemapping.mode.Override(TonemappingMode.None);

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = AddToProfile<Bloom>(profile);
            }

            bloom.threshold.Override(threshold);
            bloom.intensity.Override(intensity);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(tint);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T AddToProfile<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ---------------------------------------------------------------- splash

        private static void BuildSplash(MenuAssets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = MakeCamera();
            MakeVolume(a.volume);

            Canvas root = MakeRootCanvas("Canvas", cam);
            Stretch(Img("Background", root.transform, null, Color.black));

            Canvas logoLayer = MakeLayer("LogoLayer", root.transform, 10, false);
            var logoGroup = logoLayer.gameObject.AddComponent<CanvasGroup>();
            RectTransform area = Rect("LogoArea", logoLayer.transform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);
            RectTransform logo = MakeLogo(area, a.logo, a.logoMaterial);
            ParticleSystem embers = MakeEmbers(a, logo, false);

            var splash = new GameObject("Splash").AddComponent<SplashSequence>();
            Wire(splash, ("logoGroup", logoGroup), ("logo", logo), ("embers", embers));

            // Fire crackle under the logo, on the SFX group (Options' SFX slider); SplashSequence fades it.
            if (a.fire != null)
            {
                var fire = new GameObject("FireSound").AddComponent<AudioSource>();
                fire.clip = a.fire;
                fire.loop = true;
                fire.playOnAwake = false;
                fire.spatialBlend = 0f;
                fire.outputAudioMixerGroup = a.mixer.FindMatchingGroups("SFX").FirstOrDefault(g => g.name == "SFX");
                Wire(splash, ("sound", fire));
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SplashPath);
        }

        // ---------------------------------------------------------------- main menu

        private static void BuildMainMenu(MenuAssets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = MakeCamera();
            MakeVolume(a.menuVolume);
            MakeEventSystem();

            Canvas root = MakeRootCanvas("Canvas", cam);
            Stretch(Img("Background", root.transform, null, Color.black));

            // Logo: upper ~55% of the screen, sitting a little low so it hangs close over the menu.
            Canvas logoLayer = MakeLayer("LogoLayer", root.transform, 10, false);
            var logoGroup = logoLayer.gameObject.AddComponent<CanvasGroup>();
            RectTransform area = Rect("LogoArea", logoLayer.transform, new Vector2(0.1f, 0.37f), new Vector2(0.9f, 0.92f), Vector2.zero, Vector2.zero);
            RectTransform logo = MakeLogo(area, a.logo4, a.menuLogoMaterial);
            logo.localScale = Vector3.one * 1.15f;
            ParticleSystem embers = MakeEmbers(a, logo, true);
            MakeRingEmbers(logo, embers);

            Canvas front = MakeLayer("Front", root.transform, 20, true);

            // Menu list: lower third (six entries at 50 apart clear the footer).
            RectTransform list = Rect("MenuList", front.transform, new Vector2(0.5f, 0.335f), new Vector2(0.5f, 0.335f), Vector2.zero, new Vector2(700f, 0f), new Vector2(0.5f, 1f));
            var listGroup = list.gameObject.AddComponent<CanvasGroup>();
            MenuGlow listGlow = MakeGlow(a, list, 66f);
            var listSelection = list.gameObject.AddComponent<SelectionGroup>();
            Wire(listSelection, ("glow", listGlow));
            MenuItemView itemTemplate = MakeItem<MenuItemView>(a, list, "ItemTemplate", "ITEM", new Vector2(560f, 50f), 34f);

            // Footer.
            RectTransform footer = Stretch(Rect("Footer", front.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var footerGroup = footer.gameObject.AddComponent<CanvasGroup>();
            footerGroup.interactable = false;
            footerGroup.blocksRaycasts = false;
            TMP_Text copyright = Text("Copyright", footer, a.body, 17f, FooterColor, TextAlignmentOptions.Bottom, "©");
            Place(copyright.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(800f, 30f), new Vector2(0.5f, 0f));
            copyright.characterSpacing = 6f;
            TMP_Text version = Text("Version", footer, a.body, 17f, FooterColor, TextAlignmentOptions.BottomRight, "VER.");
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 26f), new Vector2(400f, 30f), new Vector2(1f, 0f));
            version.characterSpacing = 4f;

            OptionsPanel options = MakeOptions(a, front.transform);
            ControlsPanel controls = MakeControls(a, front.transform);
            LevelsPanel levels = MakeLevels(a, front.transform);
            ConfirmDialog dialog = MakeDialog(a, front.transform);

            var menu = new GameObject("MainMenu");
            MenuAudio audio = MakeAudio(menu, a.mixer);
            Wire(audio, ("music", a.theme));
            var controller = menu.AddComponent<MainMenuController>();
            Wire(controller,
                ("logoGroup", logoGroup), ("listGroup", listGroup), ("footerGroup", footerGroup),
                ("listContainer", list), ("itemTemplate", itemTemplate), ("listSelection", listSelection),
                ("dialog", dialog), ("options", options), ("controls", controls), ("levels", levels), ("menuAudio", audio),
                ("particleFades", new Object[] { embers.GetComponent<EmberFade>() }),
                ("copyrightLabel", copyright), ("versionLabel", version));

            CheckFonts(a);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), MainMenuPath);
        }

        private static OptionsPanel MakeOptions(MenuAssets a, Transform parent)
        {
            RectTransform panel = Stretch(Rect("Options", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var group = panel.gameObject.AddComponent<CanvasGroup>();

            TMP_Text title = Text("Title", panel, a.title, 52f, Bright, TextAlignmentOptions.Center, "OPTIONS");
            title.characterSpacing = 14f;
            Place(title.rectTransform, new Vector2(0.5f, 0.88f), Vector2.zero, new Vector2(900f, 70f));
            RectTransform line = Rect("TitleLine", panel, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), new Vector2(0f, -46f), new Vector2(900f, 3f));
            Img(line, a.radial, Ember.WithAlpha(0.7f));

            RectTransform rows = Rect("Rows", panel, new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(1040f, 0f), new Vector2(0.5f, 1f));
            MenuGlow glow = MakeGlow(a, rows, 58f);
            var selection = rows.gameObject.AddComponent<SelectionGroup>();
            Wire(selection, ("glow", glow));

            TMP_Text header = Text("HeaderTemplate", rows, a.title, 22f, HeaderColor, TextAlignmentOptions.Left, "HEADER");
            header.characterSpacing = 16f;
            Place(header.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1040f, 40f));

            OptionRow row = MakeOptionRow(a, rows);
            MenuItemView back = MakeItem<MenuItemView>(a, rows, "Back", "BACK", new Vector2(400f, 50f), 32f);

            var options = panel.gameObject.AddComponent<OptionsPanel>();
            Wire(options, ("group", group), ("rowContainer", rows), ("rowTemplate", row), ("headerTemplate", header),
                ("backButton", back), ("selection", selection));
            return options;
        }

        private static OptionRow MakeOptionRow(MenuAssets a, RectTransform parent)
        {
            OptionRow row = MakeItem<OptionRow>(a, parent, "RowTemplate", "LABEL", new Vector2(1040f, 46f), 28f);
            row.Label.alignment = TextAlignmentOptions.Left;
            row.Label.characterSpacing = 6f;
            Place(row.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260f, 0f), new Vector2(520f, 46f));

            // Slider: thin track, filled part in the label's colour, value on the right.
            RectTransform slider = Rect("Slider", row.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 0f), new Vector2(460f, 46f));
            RectTransform track = Rect("Track", slider, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), new Vector2(340f, 30f));
            Img(track, null, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            track.gameObject.AddComponent<OptionRowPointer>();
            RectTransform rail = Rect("Rail", track, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 2f));
            Img(rail, null, Hex(0x8C8478, 0.3f));
            RectTransform fill = Rect("Fill", track, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 3f), new Vector2(0f, 0.5f));
            Image fillImage = Img(fill, null, Unselected);
            TMP_Text sliderValue = Text("Value", slider, a.body, 28f, Unselected, TextAlignmentOptions.Right, "100");
            Place(sliderValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 0f), new Vector2(100f, 46f));

            // Choice: ‹ VALUE ›.
            RectTransform choice = Rect("Choice", row.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, 0f), new Vector2(460f, 46f));
            TMP_Text value = Text("Value", choice, a.body, 28f, Unselected, TextAlignmentOptions.Center, "VALUE");
            value.characterSpacing = 4f;
            Place(value.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 46f));
            TMP_Text left = MakeArrow(a, choice, row, "‹", -1, -205f);
            TMP_Text right = MakeArrow(a, choice, row, "›", 1, 205f);

            Wire(track.GetComponent<OptionRowPointer>(), ("row", row), ("step", 0));
            Wire(row, ("valueLabel", value), ("sliderValueLabel", sliderValue), ("sliderRoot", slider.gameObject),
                ("sliderTrack", track), ("sliderFill", fill), ("choiceRoot", choice.gameObject),
                ("tinted", new Object[] { fillImage, sliderValue, value, left, right }));
            return row;
        }

        private static TMP_Text MakeArrow(MenuAssets a, RectTransform parent, OptionRow row, string glyph, int step, float x)
        {
            TMP_Text arrow = Text(step < 0 ? "ArrowLeft" : "ArrowRight", parent, a.body, 42f, Unselected, TextAlignmentOptions.Center, glyph);
            Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 2f), new Vector2(44f, 46f));
            arrow.raycastTarget = true;
            var pointer = arrow.gameObject.AddComponent<OptionRowPointer>();
            Wire(pointer, ("row", row), ("step", step));
            return arrow;
        }

        // A panel's title with the ember rule under it, placed from the top edge (so tall pages fit at 21:9).
        private static void MakePanelTitle(MenuAssets a, RectTransform panel, string text, float y)
        {
            TMP_Text title = Text("Title", panel, a.title, 52f, Bright, TextAlignmentOptions.Center, text);
            title.characterSpacing = 14f;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, 70f));
            RectTransform line = Rect("TitleLine", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 46f), new Vector2(900f, 3f));
            Img(line, a.radial, Ember.WithAlpha(0.7f));
        }

        // drop: how much lower the page sits (the pause menu's header takes the top edge).
        private static ControlsPanel MakeControls(MenuAssets a, Transform parent, float drop = 0f)
        {
            RectTransform panel = Stretch(Rect("Controls", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            MakePanelTitle(a, panel, "CONTROLS", -100f - drop);

            TMP_Text hint = Text("Hint", panel, a.body, 21f, FooterColor, TextAlignmentOptions.Center,
                "Select a control, then press the new key or mouse button. Esc cancels.");
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -172f - drop), new Vector2(1300f, 34f));

            // The list scrolls inside a masked window whose top and bottom edges fade; the list keeps that much
            // padding at its ends (edgePadding), so its first and last lines rest clear of the fade.
            const float fade = 24f;
            RectTransform viewport = Rect("Viewport", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f + fade - drop), new Vector2(1200f, 640f), new Vector2(0.5f, 1f));
            viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, (int)fade);
            RectTransform rows = Rect("Rows", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1040f, 0f), new Vector2(0.5f, 1f));
            MenuGlow glow = MakeGlow(a, rows, 50f);
            var selection = rows.gameObject.AddComponent<SelectionGroup>();
            Wire(selection, ("glow", glow));

            TMP_Text header = Text("HeaderTemplate", rows, a.title, 22f, HeaderColor, TextAlignmentOptions.Left, "HEADER");
            header.characterSpacing = 16f;
            Place(header.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1040f, 40f));

            KeybindRow row = MakeItem<KeybindRow>(a, rows, "RowTemplate", "LABEL", new Vector2(1040f, 38f), 26f);
            row.Label.alignment = TextAlignmentOptions.Left;
            row.Label.characterSpacing = 6f;
            Place(row.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260f, 0f), new Vector2(520f, 38f));
            TMP_Text value = Text("Value", row.transform, a.body, 26f, Unselected, TextAlignmentOptions.Center, "KEY");
            value.characterSpacing = 6f;
            Place(value.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(250f, 0f), new Vector2(400f, 38f));
            Wire(row, ("valueLabel", value), ("tinted", new Object[] { value }));

            MenuItemView reset = MakeItem<MenuItemView>(a, rows, "Reset", "RESET TO DEFAULTS", new Vector2(560f, 38f), 28f);
            MenuItemView back = MakeItem<MenuItemView>(a, rows, "Back", "BACK", new Vector2(400f, 38f), 30f);

            var controls = panel.gameObject.AddComponent<ControlsPanel>();
            Wire(controls, ("group", group), ("selection", selection), ("rowContainer", rows), ("rowTemplate", row),
                ("headerTemplate", header), ("resetButton", reset), ("backButton", back), ("rowHeight", 38f), ("headerHeight", 46f),
                ("viewport", viewport), ("edgePadding", fade));
            return controls;
        }

        private static LevelsPanel MakeLevels(MenuAssets a, Transform parent)
        {
            RectTransform panel = Stretch(Rect("Levels", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            MakePanelTitle(a, panel, "LEVELS", -100f);

            RectTransform list = Rect("List", panel, new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), Vector2.zero, new Vector2(800f, 0f), new Vector2(0.5f, 1f));
            MenuGlow glow = MakeGlow(a, list, 66f);
            var selection = list.gameObject.AddComponent<SelectionGroup>();
            Wire(selection, ("glow", glow));
            MenuItemView item = MakeItem<MenuItemView>(a, list, "ItemTemplate", "LEVEL", new Vector2(760f, 50f), 34f);
            MenuItemView back = MakeItem<MenuItemView>(a, list, "Back", "BACK", new Vector2(400f, 50f), 32f);

            var levels = panel.gameObject.AddComponent<LevelsPanel>();
            Wire(levels, ("group", group), ("selection", selection), ("listContainer", list), ("itemTemplate", item), ("backButton", back));
            return levels;
        }

        private static ConfirmDialog MakeDialog(MenuAssets a, Transform parent)
        {
            RectTransform root = Stretch(Rect("ConfirmDialog", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var group = root.gameObject.AddComponent<CanvasGroup>();

            // Full-screen dim also swallows clicks meant for the menu behind.
            Stretch(Img("Dim", root, null, new Color(0f, 0f, 0f, 0.55f))).GetComponent<Image>().raycastTarget = true;
            RectTransform band = Rect("Band", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3000f, 440f));
            Img(band, a.radial, new Color(0f, 0f, 0f, 0.92f));
            foreach (float y in new[] { 125f, -125f })
            {
                RectTransform line = Rect(y > 0f ? "LineTop" : "LineBottom", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(1200f, 3f));
                Img(line, a.radial, Ember.WithAlpha(0.6f));
            }

            TMP_Text message = Text("Message", root, a.body, 36f, Hex(0xE6DAC4), TextAlignmentOptions.Center, "Message");
            message.lineSpacing = 8f;
            Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(1400f, 110f));

            RectTransform buttons = Rect("Buttons", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(700f, 56f));
            MenuGlow glow = MakeGlow(a, buttons, 62f, 21);
            var selection = buttons.gameObject.AddComponent<SelectionGroup>();
            Wire(selection, ("glow", glow), ("horizontal", true));
            MenuItemView confirm = MakeItem<MenuItemView>(a, buttons, "Confirm", "CONFIRM", new Vector2(280f, 50f), 32f);
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(-170f, 0f);
            MenuItemView cancel = MakeItem<MenuItemView>(a, buttons, "Cancel", "CANCEL", new Vector2(280f, 50f), 32f);
            ((RectTransform)cancel.transform).anchoredPosition = new Vector2(170f, 0f);
            foreach (MenuItemView button in new[] { confirm, cancel })
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            }

            var dialog = root.gameObject.AddComponent<ConfirmDialog>();
            Wire(dialog, ("group", group), ("message", message), ("confirmButton", confirm), ("cancelButton", cancel), ("selection", selection));
            return dialog;
        }

        private static MenuAudio MakeAudio(GameObject host, AudioMixer mixer)
        {
            AudioMixerGroup Group(string name) => mixer != null ? mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name) : null;

            AudioSource MakeSource(string name, AudioMixerGroup output)
            {
                var go = new GameObject(name);
                go.transform.SetParent(host.transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.outputAudioMixerGroup = output;
                return source;
            }

            var audio = host.AddComponent<MenuAudio>();
            Wire(audio, ("musicSource", MakeSource("Music", Group("Music"))), ("sfxSource", MakeSource("SFX", Group("SFX"))));
            return audio;
        }

        // ---------------------------------------------------------------- pause menu

        // The in-game pause menu, saved as a prefab under Resources (PauseMenu spawns it into every gameplay
        // scene). It draws on an overlay canvas, so the level's camera and post-processing never touch it; its
        // highlight sparks are therefore UI images rather than particles.
        private static void BuildPauseMenu(MenuAssets a)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(Path.GetDirectoryName(PausePrefabPath).Replace('\\', '/'));
            imageSparks = true;

            var root = new GameObject("PauseMenu");
            GameObject events = MakeEventSystem();
            events.transform.SetParent(root.transform, false);
            events.SetActive(false);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var rootGroup = canvasGo.AddComponent<CanvasGroup>();

            // Darkens the game; also swallows clicks.
            Image dim = Img("Dim", canvasGo.transform, null, new Color(0f, 0f, 0f, 0.7f));
            Stretch(dim);
            dim.raycastTarget = true;

            // A soft, darker pool behind the centred list and pages, so they stay legible over a lit patch of level.
            RectTransform pool = Rect("Backdrop", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 900f));
            Img(pool, a.radial, new Color(0f, 0f, 0f, 0.6f));

            // Header: where you are (left) and playtime (right), each over an ember rule.
            RectTransform header = Stretch(Rect("Header", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            TMP_Text location = Text("Location", header, a.title, 34f, Bright, TextAlignmentOptions.Left, "LOCATION");
            location.characterSpacing = 10f;
            Place(location.rectTransform, new Vector2(0f, 1f), new Vector2(72f, -58f), new Vector2(900f, 50f), new Vector2(0f, 0.5f));
            RectTransform leftRule = Rect("LocationRule", header, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -88f), new Vector2(560f, 2f), new Vector2(0f, 0.5f));
            Img(leftRule, a.radial, Ember.WithAlpha(0.6f));
            TMP_Text playtime = Text("Playtime", header, a.body, 26f, Bright, TextAlignmentOptions.Right, "PLAYTIME");
            playtime.characterSpacing = 4f;
            Place(playtime.rectTransform, new Vector2(1f, 1f), new Vector2(-72f, -58f), new Vector2(600f, 50f), new Vector2(1f, 0.5f));
            RectTransform rightRule = Rect("PlaytimeRule", header, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -88f), new Vector2(560f, 2f), new Vector2(1f, 0.5f));
            Img(rightRule, a.radial, Ember.WithAlpha(0.6f));

            // Centred list (PauseMenu centres it on its entries).
            RectTransform list = Rect("MenuList", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 0f), new Vector2(0.5f, 1f));
            var listGroup = list.gameObject.AddComponent<CanvasGroup>();
            MenuGlow listGlow = MakeGlow(a, list, 66f);
            var listSelection = list.gameObject.AddComponent<SelectionGroup>();
            Wire(listSelection, ("glow", listGlow));
            MenuItemView itemTemplate = MakeItem<MenuItemView>(a, list, "ItemTemplate", "ITEM", new Vector2(640f, 50f), 34f);

            OptionsPanel options = MakeOptions(a, canvasGo.transform);
            ControlsPanel controls = MakeControls(a, canvasGo.transform, 30f);
            ConfirmDialog dialog = MakeDialog(a, canvasGo.transform);

            // After the dialog, so the hints stay readable over its dim.
            KeyHintBar hints = MakeKeyHints(a, canvasGo.transform);

            MakeAudio(root, a.mixer);
            var pause = root.AddComponent<PauseMenu>();
            Wire(pause,
                ("canvas", canvas), ("rootGroup", rootGroup), ("dim", dim), ("listGroup", listGroup), ("listContainer", list),
                ("itemTemplate", itemTemplate), ("listSelection", listSelection), ("dialog", dialog), ("options", options),
                ("controls", controls), ("locationLabel", location), ("playtimeLabel", playtime), ("hints", hints),
                ("ownEventSystem", events.GetComponent<EventSystem>()));

            CheckFonts(a);
            PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
            imageSparks = false;
        }

        // Bottom-right row of key hints; KeyHintBar fills it per screen.
        private static KeyHintBar MakeKeyHints(MenuAssets a, Transform parent)
        {
            RectTransform bar = Rect("KeyHints", parent, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-72f, 54f), new Vector2(0f, 40f), new Vector2(1f, 0.5f));
            RectTransform hint = Rect("HintTemplate", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 40f), new Vector2(1f, 0.5f));

            RectTransform icon = Rect("Icon", hint, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(34f, 34f), new Vector2(1f, 0.5f));
            Image iconImage = Img(icon, a.keyCap, Hex(0xE6DAC4, 0.75f));
            iconImage.type = Image.Type.Sliced;
            iconImage.pixelsPerUnitMultiplier = 1.4f;
            TMP_Text glyph = Text("Glyph", icon, a.title, 17f, Hex(0xF2E6D0, 0.95f), TextAlignmentOptions.CenterGeoAligned, "ESC");
            glyph.characterSpacing = 3f;
            Stretch(glyph.rectTransform);

            TMP_Text label = Text("Label", hint, a.body, 22f, Hex(0xC8BCA8, 0.85f), TextAlignmentOptions.Left, "LABEL");
            label.characterSpacing = 6f;
            Place(label.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, 40f), new Vector2(1f, 0.5f));

            var view = hint.gameObject.AddComponent<KeyHint>();
            Wire(view, ("icon", iconImage), ("glyph", glyph), ("label", label));
            var row = bar.gameObject.AddComponent<KeyHintBar>();
            Wire(row, ("template", view), ("keySprite", a.keyCap), ("buttonSprite", a.padButton));
            return row;
        }

        // A rounded outline with a faint fill (keycaps; a circle when the radius is half the size). u, v in -1..1
        // over 64 px.
        private static float RoundedFrame(float u, float v, float radius, float stroke, float fill)
        {
            const float half = 32f;
            float box = half - 2f - radius;
            float qx = Mathf.Abs(u) * half - box;
            float qy = Mathf.Abs(v) * half - box;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            float inside = Mathf.Clamp01(0.5f - d);
            float ring = inside * Mathf.Clamp01(d + stroke + 0.5f);
            return Mathf.Max(ring, inside * fill);
        }

        // A generated sprite drawn 9-sliced, so a keycap can stretch to fit "ENTER".
        private static Sprite EnsureSlicedSprite(string path, Func<float, float, float> alpha, float border)
        {
            EnsureGeneratedSprite(path, 64, 64, alpha);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var borders = new Vector4(border, border, border, border);
            if (importer.spriteBorder != borders)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = borders;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static UISparks MakeImageSparks(MenuAssets a, RectTransform parent, Vector2 anchor)
        {
            RectTransform host = Rect("Sparks", parent, anchor, anchor, Vector2.zero, Vector2.zero);
            var sparks = host.gameObject.AddComponent<UISparks>();
            Wire(sparks, ("sprite", a.radial), ("material", a.logoMaterial));
            return sparks;
        }

        // ---------------------------------------------------------------- shared pieces

        private static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            return cam;
        }

        private static void MakeVolume(VolumeProfile profile)
        {
            var go = new GameObject("Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static GameObject MakeEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null)
            {
                module.AssignDefaultActions();
            }

            // A click on empty space keeps the current selection (gamepad/keyboard focus is never lost).
            module.deselectOnBackgroundClick = false;
            return go;
        }

        private static Canvas MakeRootCanvas(string name, Camera cam)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        // A nested canvas with its own sorting order, so particle systems can sit between UI layers.
        private static Canvas MakeLayer(string name, Transform parent, int order, bool raycast)
        {
            RectTransform rect = Stretch(Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
            if (raycast)
            {
                rect.gameObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        // The logo fitted inside its area at its own aspect, drawn additively so its black background vanishes.
        private static RectTransform MakeLogo(RectTransform area, Sprite sprite, Material material)
        {
            RectTransform logo = Stretch(Rect("Logo", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero));
            var fitter = logo.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            Image image = Img(logo, sprite, Color.white);
            image.material = material;
            return logo;
        }

        // The title logo is circled, so its embers come off the painted ring (centre and radius measured on the
        // 2752×1700 art) and only drift a little way up. LogoEmberRing sizes the ring at runtime.
        private static void MakeRingEmbers(RectTransform logo, ParticleSystem embers)
        {
            var host = (RectTransform)embers.transform;
            host.anchorMin = host.anchorMax = new Vector2(0.503f, 0.516f);
            host.anchoredPosition = Vector2.zero;

            ParticleSystem.MainModule main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            ParticleSystem.EmissionModule emission = embers.emission;
            emission.rateOverTime = 20f;
            ParticleSystem.VelocityOverLifetimeModule velocity = embers.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(12f, 40f);
            ParticleSystem.ShapeModule shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 200f;
            shape.radiusThickness = 0.04f;
            shape.arc = 360f;
            shape.scale = Vector3.one;

            var ring = logo.gameObject.AddComponent<LogoEmberRing>();
            Wire(ring, ("logo", logo), ("embers", embers), ("radius", 0.2035f));
        }

        // The selection highlight: a thin seam of embers under the selected label's words (a little longer than
        // them), a faint halo, and the odd spark. MenuGlow centres these on the words at runtime.
        // sparkOrder sorts the sparks against the UI canvases (Front is 20).
        private static MenuGlow MakeGlow(MenuAssets a, RectTransform parent, float height, int sparkOrder = 19)
        {
            RectTransform rect = Rect("Glow", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(800f, height));
            rect.gameObject.AddComponent<CanvasGroup>();

            const float lineY = 0.16f;
            RectTransform halo = Rect("Halo", rect, new Vector2(0.5f, lineY), new Vector2(0.5f, lineY), Vector2.zero, new Vector2(300f, 8f));
            Image haloImage = Img(halo, a.radial, Hex(0xFF7A2A, 0.15f));
            RectTransform line = Rect("Line", rect, new Vector2(0.5f, lineY), new Vector2(0.5f, lineY), Vector2.zero, new Vector2(300f, 2f));
            Image lineImage = Img(line, a.emberStrip, Hex(0xFFB868, 0.75f));
            var glow = rect.gameObject.AddComponent<MenuGlow>();
            if (imageSparks)
            {
                UISparks images = MakeImageSparks(a, rect, new Vector2(0.5f, lineY));
                Wire(glow, ("flicker", new Object[] { lineImage, haloImage }), ("uiSparks", images),
                    ("spans", new Object[] { line, halo }), ("spanPadding", new[] { 14f, 34f }));
                return glow;
            }

            ParticleSystem sparks = MakeSparks(a, rect, new Vector2(0.5f, lineY), sparkOrder, 5f, new Vector2(2f, 3.5f), new Vector2(0.6f, 1.3f), new Vector2(20f, 45f), 300f);
            Wire(glow, ("flicker", new Object[] { lineImage, haloImage }), ("sparks", sparks),
                ("spans", new Object[] { line, halo, sparks.transform }), ("spanPadding", new[] { 14f, 34f, 0f }));
            return glow;
        }

        private static void SetArray(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // Small sparks lifting off a highlight. Sizes, speeds and the emitter width are in reference pixels.
        private static ParticleSystem MakeSparks(MenuAssets a, RectTransform parent, Vector2 anchor, int order, float rate, Vector2 size, Vector2 lifetime, Vector2 rise, float width)
        {
            RectTransform host = Rect("Sparks", parent, anchor, anchor, Vector2.zero, Vector2.zero);
            ParticleSystem ps = host.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 3f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex(0xFF8A3A), Hex(0xFFC27A));
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 3f, 1f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-10f, 10f);
            velocity.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 14f;
            noise.frequency = 0.02f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Hex(0xFF9A5A), 0.45f), new GradientColorKey(Hex(0xB0301A), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = a.particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = order;
            renderer.maxParticleSize = 1f;
            host.gameObject.AddComponent<EmberFade>();
            return ps;
        }

        private static T MakeItem<T>(MenuAssets a, RectTransform parent, string name, string label, Vector2 size, float fontSize) where T : MenuSelectable
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, size);

            // Invisible hit area: the whole line reacts to the mouse, not just the glyphs.
            Img(rect, null, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            TMP_Text text = Text("Label", rect, a.body, fontSize, Unselected, TextAlignmentOptions.Center, label);
            text.characterSpacing = 8f;
            Stretch(text.rectTransform);
            var item = rect.gameObject.AddComponent<T>();
            Wire(item, ("label", text));
            return item;
        }

        private static ParticleSystem MakeEmbers(MenuAssets a, RectTransform logo, bool prewarm)
        {
            // Sits at the ash pile under the helmet; sizes and speeds are in reference pixels (hierarchy scaling).
            RectTransform host = Rect("Embers", logo, new Vector2(0.52f, 0.16f), new Vector2(0.52f, 0.16f), Vector2.zero, Vector2.zero);
            ParticleSystem ps = host.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = prewarm;
            main.playOnAwake = prewarm;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex(0xFF7A2A), Hex(0xFFC070));
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 22f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(340f, 30f, 1f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-18f, 18f);
            velocity.y = new ParticleSystem.MinMaxCurve(45f, 120f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 40f;
            noise.frequency = 0.006f;
            noise.scrollSpeed = 0.25f;
            noise.damping = true;

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Hex(0xFF9A5A), 0.5f), new GradientColorKey(Hex(0xB0301A), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = a.particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 15;
            renderer.maxParticleSize = 1f;
            host.gameObject.AddComponent<EmberFade>();
            return ps;
        }

        // Every label must use the Cormorant assets; a stale font reference falls back to TMP's sans default.
        private static void CheckFonts(MenuAssets a)
        {
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                if (text.font != a.body && text.font != a.title)
                {
                    Debug.LogError($"Menu label '{text.name}' lost its font ({(text.font != null ? text.font.name : "none")}); rerun Tools/Kneel/Build Menu Scenes.", text);
                }
            }
        }

        private static void UpdateBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SplashPath, true),
                new EditorBuildSettingsScene(MainMenuPath, true),
            };
            foreach (string path in GameScenes)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            // Anything else already listed keeps its place after those.
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scenes.All(s => s.path != scene.path))
                {
                    scenes.Add(scene);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- helpers

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Stretch(Image image)
        {
            return Stretch(image.rectTransform);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            return Img(Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), sprite, color);
        }

        private static Image Img(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment, string text)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 50f));
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.text = text;
            return tmp;
        }

        // Sets serialized (usually private) fields by name.
        private static void Wire(Object target, params (string field, object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach ((string field, object value) in fields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogError($"{target.GetType().Name} has no serialized field '{field}'.", target);
                    continue;
                }

                switch (value)
                {
                    case Object[] objects:
                        p.arraySize = objects.Length;
                        for (int i = 0; i < objects.Length; i++)
                        {
                            p.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                        }

                        break;
                    case Object obj:
                        p.objectReferenceValue = obj;
                        break;
                    case bool b:
                        p.boolValue = b;
                        break;
                    case int n:
                        p.intValue = n;
                        break;
                    case float f:
                        p.floatValue = f;
                        break;
                    case string s:
                        p.stringValue = s;
                        break;
                    case Color c:
                        p.colorValue = c;
                        break;
                    case float[] floats:
                        p.arraySize = floats.Length;
                        for (int i = 0; i < floats.Length; i++)
                        {
                            p.GetArrayElementAtIndex(i).floatValue = floats[i];
                        }

                        break;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(int rgb, float alpha = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
        }

        private static Color WithAlpha(this Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }
    }
}
