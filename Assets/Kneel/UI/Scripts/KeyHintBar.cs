using System.Collections.Generic;
using Kneel.Core;
using UnityEngine;

namespace Kneel.UI
{
    // The footer's row of key hints, right-aligned. Each hint names a keyboard key and a gamepad button; the row
    // shows whichever matches the device used last, and switches the moment the player picks up the other one.
    // Single-letter gamepad buttons (A, B) get a round icon, everything else a keycap that fits its text.
    public class KeyHintBar : MonoBehaviour
    {
        public readonly struct Hint
        {
            public Hint(string key, string button, string label)
            {
                Key = key;
                Button = button;
                Label = label;
            }

            public string Key { get; }

            public string Button { get; }

            public string Label { get; }
        }

        [SerializeField] private KeyHint template;
        [SerializeField] private Sprite keySprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private float spacing = 44f;
        [SerializeField] private float iconPadding = 12f;
        [SerializeField] private float gap = 12f;

        private readonly List<KeyHint> views = new List<KeyHint>();
        private Hint[] hints = new Hint[0];

        private void Awake()
        {
            template.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            LastInputDevice.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LastInputDevice.Changed -= Refresh;
        }

        public void Show(params Hint[] newHints)
        {
            hints = newHints;
            Refresh();
        }

        private void Refresh()
        {
            while (views.Count < hints.Length)
            {
                KeyHint view = Instantiate(template, transform);
                view.name = "Hint";
                views.Add(view);
            }

            bool gamepad = LastInputDevice.IsGamepad;
            float x = 0f;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                KeyHint view = views[i];
                bool shown = i < hints.Length;
                view.gameObject.SetActive(shown);
                if (!shown)
                {
                    continue;
                }

                string glyph = gamepad ? hints[i].Button : hints[i].Key;
                bool round = gamepad && glyph.Length == 1;
                float width = view.Set(glyph, hints[i].Label, round ? buttonSprite : keySprite, round, iconPadding, gap);
                ((RectTransform)view.transform).anchoredPosition = new Vector2(-x, 0f);
                x += width + spacing;
            }
        }
    }
}
