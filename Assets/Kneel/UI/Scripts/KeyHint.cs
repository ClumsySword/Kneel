using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.UI
{
    // One footer hint: a key or button icon with its glyph, then what it does. Lays itself out from the right
    // edge (icon, gap, label) and reports its width so KeyHintBar can line hints up.
    public class KeyHint : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text glyph;
        [SerializeField] private TMP_Text label;

        public float Set(string glyphText, string labelText, Sprite sprite, bool round, float iconPadding, float gap)
        {
            glyph.text = glyphText;
            label.text = labelText;
            icon.sprite = sprite;
            icon.type = round ? Image.Type.Simple : Image.Type.Sliced;

            float height = icon.rectTransform.sizeDelta.y;
            float iconWidth = round ? height : Mathf.Max(height, glyph.GetPreferredValues(glyphText).x + iconPadding * 2f);
            float labelWidth = label.GetPreferredValues(labelText).x;

            label.rectTransform.sizeDelta = new Vector2(labelWidth, label.rectTransform.sizeDelta.y);
            label.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.sizeDelta = new Vector2(iconWidth, height);
            icon.rectTransform.anchoredPosition = new Vector2(-(labelWidth + gap), 0f);
            return iconWidth + gap + labelWidth;
        }
    }
}
