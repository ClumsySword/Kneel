using System.Collections.Generic;
using System.Linq;
using Kneel.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Kneel.EditorTools
{
    // Generates the Intro scene (UI/Scenes/Intro.unity) that New Game plays before L1: the prologue film on a full-screen
    // RawImage of the overlay canvas (IntroSequence gives it a render texture; letterboxed by an AspectRatioFitter; the
    // camera's far-plane mode drew nothing under URP here), its sound on a plain 2D source (master volume only, so the music and
    // effects sliders don't cut the film's mix), a black cover, the card ("YOU HAVE AWAKENED") and the skip hint, all driven by
    // IntroSequence. It also puts the scene in the build, straight after MainMenu. Rerun it after changing it; don't
    // edit the scene by hand.
    public static class IntroSceneBuilder
    {
        private const string ScenePath = "Assets/Kneel/UI/Scenes/Intro.unity";
        // WebM (VP8/Vorbis): the one format Unity imports and plays on every platform. An H.264 .mp4 does not import
        // in the Linux Editor at all ("format 0 not supported"), and the film silently went missing.
        private const string FilmPath = "Assets/Kneel/UI/Video/kneel_intro.webm";
        private const string BodyFont = "Assets/Kneel/UI/Fonts/CormorantGaramond-Medium SDF.asset";

        // The menu's palette (MenuScenesBuilder).
        private static readonly Color Bright = new Color32(0xF2, 0xE6, 0xD0, 0xFF);
        private static readonly Color Muted = new Color32(0x8C, 0x84, 0x78, 0xFF);

        // The card between the film and the wake-up, in the owner's words.
        private const string CardText = "YOU HAVE AWAKENED";

        [MenuItem("Kneel/UI/Build Intro Scene")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "Intro: leave Play mode first.";
            }

            var film = AssetDatabase.LoadAssetAtPath<VideoClip>(FilmPath);
            if (film == null)
            {
                return "Intro: no film at " + FilmPath;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;

            // The camera only clears to black; it draws nothing of the world.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            cameraObject.AddComponent<AudioListener>();

            var filmObject = new GameObject("Film");
            var audio = filmObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var video = filmObject.AddComponent<VideoPlayer>();
            video.source = VideoSource.VideoClip;
            video.clip = film;
            video.playOnAwake = false;
            video.waitForFirstFrame = true;
            video.isLooping = false;
            video.skipOnDrop = true;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.aspectRatio = VideoAspectRatio.FitInside;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            video.controlledAudioTrackCount = film.audioTrackCount;
            for (ushort track = 0; track < film.audioTrackCount; track++)
            {
                video.EnableAudioTrack(track, true);
                video.SetTargetAudioSource(track, audio);
            }

            // Overlay: the film, the black cover over it, then the card and the hint on top.
            var canvasObject = new GameObject("Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = new GameObject("Screen", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            screen.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)screen.transform);
            var image = screen.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            var fitter = screen.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = film.width / (float)film.height;

            CanvasGroup black = Group(canvasObject.transform, "Black");
            var blackImage = black.gameObject.AddComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;
            Stretch(black.transform as RectTransform);

            CanvasGroup card = Group(canvasObject.transform, "TitleCard");
            Stretch(card.transform as RectTransform);
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFont);
            Text(card.transform, "Line", CardText, bodyFont, 60f, 24f, new Color(Bright.r, Bright.g, Bright.b, 0.9f), Vector2.zero, new Vector2(1400f, 120f));

            CanvasGroup hint = Group(canvasObject.transform, "SkipHint");
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(1f, 0f);
            hintRect.anchoredPosition = new Vector2(-64f, 48f);
            hintRect.sizeDelta = new Vector2(600f, 60f);
            TextMeshProUGUI hintText = Text(hint.transform, "Text", "Press again to skip", bodyFont, 28f, 6f, Muted, Vector2.zero, new Vector2(600f, 60f));
            hintText.alignment = TextAlignmentOptions.Right;

            var intro = new GameObject("Intro").AddComponent<IntroSequence>();
            var so = new SerializedObject(intro);
            so.FindProperty("video").objectReferenceValue = video;
            so.FindProperty("videoAudio").objectReferenceValue = audio;
            so.FindProperty("screen").objectReferenceValue = image;
            so.FindProperty("black").objectReferenceValue = black;
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("skipHint").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();

            black.alpha = 1f;
            card.alpha = 0f;
            hint.alpha = 0f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild();
            return $"Intro scene built ({film.length:F1} s film, {film.width}x{film.height}, {film.audioTrackCount} audio track(s)), in the build after MainMenu.";
        }

        private static CanvasGroup Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var group = go.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, TMP_FontAsset font, float size, float spacing, Color colour, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = size;
            tmp.characterSpacing = spacing;
            tmp.color = colour;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = box;
            rect.anchoredPosition = position;
            return tmp;
        }

        // Splash, MainMenu, Intro, then the levels.
        private static void AddToBuild()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            int menu = scenes.FindIndex(s => s.path.EndsWith("/MainMenu.unity"));
            scenes.Insert(menu >= 0 ? menu + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
