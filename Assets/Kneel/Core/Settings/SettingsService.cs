using System;
using UnityEngine;

namespace Kneel.Core
{
    // The player's options. Volumes and brightness are 0..1 (brightness 0.5 = unchanged).
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float brightness = 0.5f;

        public int resolutionWidth;
        public int resolutionHeight;
        public FullScreenMode windowMode = FullScreenMode.FullScreenWindow;
        public int qualityLevel;
        public bool vSync;
    }

    // Loads the options from PlayerPrefs, applies them, and saves them back. It bootstraps itself before the
    // first scene loads (the splash, or any level started straight from the Editor), so every scene starts with
    // the player's settings.
    public static class SettingsService
    {
        private const string Prefix = "kneel.settings.";
        private const string MasterKey = Prefix + "masterVolume";
        private const string MusicKey = Prefix + "musicVolume";
        private const string SfxKey = Prefix + "sfxVolume";
        private const string BrightnessKey = Prefix + "brightness";
        private const string WidthKey = Prefix + "resolutionWidth";
        private const string HeightKey = Prefix + "resolutionHeight";
        private const string WindowModeKey = Prefix + "windowMode";
        private const string QualityKey = Prefix + "qualityLevel";
        private const string VSyncKey = Prefix + "vSync";

        public static GameSettings Current { get; private set; }

        // Raised after any Apply, so the audio mixer and brightness volume can follow.
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Load();

            // Display and quality are only forced once the player has chosen them, so a first launch keeps
            // whatever the player/launcher configured.
            if (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(WindowModeKey))
            {
                ApplyDisplay();
            }

            if (PlayerPrefs.HasKey(QualityKey) || PlayerPrefs.HasKey(VSyncKey))
            {
                ApplyGraphics();
            }

            SettingsRuntime.Create();
        }

        public static void Load()
        {
            Resolution screen = Screen.currentResolution;
            var defaults = new GameSettings
            {
                resolutionWidth = screen.width,
                resolutionHeight = screen.height,
                windowMode = Screen.fullScreenMode,
                qualityLevel = QualitySettings.GetQualityLevel(),
                vSync = QualitySettings.vSyncCount > 0,
            };

            Current = new GameSettings
            {
                masterVolume = PlayerPrefs.GetFloat(MasterKey, defaults.masterVolume),
                musicVolume = PlayerPrefs.GetFloat(MusicKey, defaults.musicVolume),
                sfxVolume = PlayerPrefs.GetFloat(SfxKey, defaults.sfxVolume),
                brightness = PlayerPrefs.GetFloat(BrightnessKey, defaults.brightness),
                resolutionWidth = PlayerPrefs.GetInt(WidthKey, defaults.resolutionWidth),
                resolutionHeight = PlayerPrefs.GetInt(HeightKey, defaults.resolutionHeight),
                windowMode = (FullScreenMode)PlayerPrefs.GetInt(WindowModeKey, (int)defaults.windowMode),
                qualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, defaults.qualityLevel), 0, QualitySettings.names.Length - 1),
                vSync = PlayerPrefs.GetInt(VSyncKey, defaults.vSync ? 1 : 0) != 0,
            };
        }

        public static void Save()
        {
            GameSettings s = Current;
            PlayerPrefs.SetFloat(MasterKey, s.masterVolume);
            PlayerPrefs.SetFloat(MusicKey, s.musicVolume);
            PlayerPrefs.SetFloat(SfxKey, s.sfxVolume);
            PlayerPrefs.SetFloat(BrightnessKey, s.brightness);
            PlayerPrefs.SetInt(WidthKey, s.resolutionWidth);
            PlayerPrefs.SetInt(HeightKey, s.resolutionHeight);
            PlayerPrefs.SetInt(WindowModeKey, (int)s.windowMode);
            PlayerPrefs.SetInt(QualityKey, s.qualityLevel);
            PlayerPrefs.SetInt(VSyncKey, s.vSync ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Volumes and brightness are applied by SettingsRuntime when it hears this.
        public static void ApplyAudioAndBrightness()
        {
            Changed?.Invoke();
        }

        public static void ApplyDisplay()
        {
            GameSettings s = Current;
            if (Application.isEditor || s.resolutionWidth <= 0 || s.resolutionHeight <= 0)
            {
                return;
            }

            Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, s.windowMode, BestRefreshRate(s.resolutionWidth, s.resolutionHeight));
        }

        public static void ApplyGraphics()
        {
            GameSettings s = Current;
            if (QualitySettings.GetQualityLevel() != s.qualityLevel)
            {
                QualitySettings.SetQualityLevel(s.qualityLevel, true);
            }

            // After the quality level: switching levels resets vSyncCount to the level's own value.
            QualitySettings.vSyncCount = s.vSync ? 1 : 0;
            Changed?.Invoke();
        }

        private static RefreshRate BestRefreshRate(int width, int height)
        {
            RefreshRate best = Screen.currentResolution.refreshRateRatio;
            double bestHz = 0;
            foreach (Resolution r in Screen.resolutions)
            {
                if (r.width == width && r.height == height && r.refreshRateRatio.value > bestHz)
                {
                    best = r.refreshRateRatio;
                    bestHz = r.refreshRateRatio.value;
                }
            }

            return best;
        }
    }
}
