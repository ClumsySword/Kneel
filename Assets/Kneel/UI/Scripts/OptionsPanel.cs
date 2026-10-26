using System;
using System.Collections.Generic;
using Kneel.Core;
using TMPro;
using UnityEngine;

namespace Kneel.UI
{
    // The Options screen: audio volumes and display settings. Every change applies at once and is saved when
    // the panel closes.
    public class OptionsPanel : MenuPanel
    {
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private OptionRow rowTemplate;
        [SerializeField] private TMP_Text headerTemplate;
        [SerializeField] private MenuItemView backButton;
        [SerializeField] private float rowHeight = 50f;
        [SerializeField] private float headerHeight = 64f;

        private static readonly FullScreenMode[] WindowModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        private static readonly string[] WindowModeNames = { "FULLSCREEN", "EXCLUSIVE FULLSCREEN", "WINDOWED" };

        private readonly List<MenuSelectable> navigable = new List<MenuSelectable>();
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private float cursor;

        protected override MenuSelectable FirstSelected => navigable[0];

        protected override void Awake()
        {
            base.Awake();
            rowTemplate.gameObject.SetActive(false);
            headerTemplate.gameObject.SetActive(false);
            backButton.Confirmed += _ => Back();
            Build();
        }

        private void Build()
        {
            GameSettings s = SettingsService.Current;

            AddHeader("AUDIO");
            AddSlider("MASTER VOLUME", s.masterVolume, v => { s.masterVolume = v; SettingsService.ApplyAudioAndBrightness(); });
            AddSlider("MUSIC VOLUME", s.musicVolume, v => { s.musicVolume = v; SettingsService.ApplyAudioAndBrightness(); });
            AddSlider("SFX VOLUME", s.sfxVolume, v => { s.sfxVolume = v; SettingsService.ApplyAudioAndBrightness(); });

            AddHeader("DISPLAY");
            AddChoice("RESOLUTION", ResolutionNames(s, out int resolutionIndex), resolutionIndex, i =>
            {
                s.resolutionWidth = resolutions[i].x;
                s.resolutionHeight = resolutions[i].y;
                SettingsService.ApplyDisplay();
            });
            AddChoice("WINDOW MODE", WindowModeNames, Mathf.Max(0, Array.IndexOf(WindowModes, s.windowMode)), i =>
            {
                s.windowMode = WindowModes[i];
                SettingsService.ApplyDisplay();
            });
            AddChoice("QUALITY", QualityNames(), s.qualityLevel, i =>
            {
                s.qualityLevel = i;
                SettingsService.ApplyGraphics();
            });
            AddChoice("VSYNC", new[] { "OFF", "ON" }, s.vSync ? 1 : 0, i =>
            {
                s.vSync = i == 1;
                SettingsService.ApplyGraphics();
            });
            AddSlider("BRIGHTNESS", s.brightness, v => { s.brightness = v; SettingsService.ApplyAudioAndBrightness(); });

            cursor -= headerHeight * 0.5f;
            Place((RectTransform)backButton.transform, rowHeight);
            navigable.Add(backButton);
            selection.SetItems(navigable);
        }

        protected override void OnClosing()
        {
            SettingsService.Save();
        }

        private void AddHeader(string text)
        {
            if (cursor < 0f)
            {
                cursor -= headerHeight * 0.35f;
            }

            TMP_Text header = Instantiate(headerTemplate, rowContainer);
            header.name = "Header_" + text;
            header.text = text;
            header.gameObject.SetActive(true);
            Place(header.rectTransform, headerHeight);
        }

        private void AddSlider(string label, float value, Action<float> changed)
        {
            OptionRow row = NewRow(label);
            row.SetupSlider(label, value, v => Mathf.RoundToInt(v * 100f).ToString(), changed);
        }

        private void AddChoice(string label, string[] options, int index, Action<int> changed)
        {
            OptionRow row = NewRow(label);
            row.SetupChoice(label, options, index, changed);
        }

        private OptionRow NewRow(string label)
        {
            OptionRow row = Instantiate(rowTemplate, rowContainer);
            row.name = "Row_" + label;
            row.gameObject.SetActive(true);
            Place((RectTransform)row.transform, rowHeight);
            navigable.Add(row);
            return row;
        }

        // Stacks rows top-down from the container's top edge.
        private void Place(RectTransform rect, float height)
        {
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, cursor - height * 0.5f);
            cursor -= height;
        }

        private string[] ResolutionNames(GameSettings s, out int current)
        {
            resolutions.Clear();
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(size))
                {
                    resolutions.Add(size);
                }
            }

            var saved = new Vector2Int(s.resolutionWidth, s.resolutionHeight);
            if (!resolutions.Contains(saved) && saved.x > 0)
            {
                resolutions.Add(saved);
            }

            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            current = Mathf.Max(0, resolutions.IndexOf(saved));

            var names = new string[resolutions.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = resolutions[i].x + " × " + resolutions[i].y;
            }

            return names;
        }

        private static string[] QualityNames()
        {
            string[] names = QualitySettings.names;
            var upper = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                upper[i] = names[i].ToUpperInvariant();
            }

            return upper;
        }
    }
}
