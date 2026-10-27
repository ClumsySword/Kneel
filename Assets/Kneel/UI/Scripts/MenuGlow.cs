using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.UI
{
    // The highlight under the selected entry: a thin seam of embers spanning the label's words, a faint halo,
    // and the odd spark. It slides to the newly selected entry and fades in and out rather than snapping, and
    // follows the words rather than the row, so it sits under a left-aligned options label too.
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuGlow : MonoBehaviour
    {
        [SerializeField] private float slideTime = 0.15f;
        [SerializeField] private float fadeTime = 0.15f;

        [Header("Ember")]
        [SerializeField] private Image[] flicker = new Image[0];
        [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.25f;
        [SerializeField] private float flickerSpeed = 2.2f;
        [SerializeField] private ParticleSystem sparks;

        // Image-based sparks instead, on overlay canvases (the pause menu), where particles don't render.
        [SerializeField] private UISparks uiSparks;

        // Spark emitter width = the label's text width × this.
        [SerializeField] private float sparkWidth = 0.9f;

        // Parts centred on the words, text width + 2 × padding wide.
        [SerializeField] private RectTransform[] spans = new RectTransform[0];
        [SerializeField] private float[] spanPadding = new float[0];

        private RectTransform rect;
        private CanvasGroup group;
        private CanvasGroup[] parentGroups;
        private EmberFade sparksFade;
        private float[] flickerBase;
        private Vector3 targetPosition;
        private float targetAlpha;
        private Vector2 textTarget;
        private Vector2 textCurrent;
        private float seed;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            parentGroups = transform.parent.GetComponentsInParent<CanvasGroup>(true);
            sparksFade = sparks != null ? sparks.GetComponent<EmberFade>() : null;
            targetPosition = rect.localPosition;
            seed = Random.value * 100f;

            flickerBase = new float[flicker.Length];
            for (int i = 0; i < flicker.Length; i++)
            {
                flickerBase[i] = flicker[i].color.a;
            }
        }

        public void MoveTo(RectTransform item, bool instant)
        {
            Vector3 world = item.TransformPoint(item.rect.center);
            Vector3 local = rect.parent.InverseTransformPoint(world);
            targetPosition = new Vector3(local.x, local.y, rect.localPosition.z);
            targetAlpha = 1f;
            textTarget = TextEdges(item, local.x);
            if (instant || group.alpha <= 0.01f)
            {
                rect.localPosition = targetPosition;
                textCurrent = textTarget;
            }
        }

        public void Hide()
        {
            targetAlpha = 0f;
        }

        // The selected label's text edges, as x offsets from the highlight's centre.
        private Vector2 TextEdges(RectTransform item, float centreX)
        {
            var selectable = item.GetComponent<MenuSelectable>();
            TMP_Text label = selectable != null ? selectable.Label : null;
            if (label == null || string.IsNullOrEmpty(label.text))
            {
                return new Vector2(-item.rect.width * 0.5f, item.rect.width * 0.5f);
            }

            label.ForceMeshUpdate();
            Bounds bounds = label.textBounds;
            Vector3 left = rect.parent.InverseTransformPoint(label.transform.TransformPoint(new Vector3(bounds.min.x, bounds.center.y, 0f)));
            Vector3 right = rect.parent.InverseTransformPoint(label.transform.TransformPoint(new Vector3(bounds.max.x, bounds.center.y, 0f)));
            return new Vector2(left.x - centreX, right.x - centreX);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Exponential approach: about 95% of the way in slideTime / fadeTime.
            float slide = 1f - Mathf.Exp(-3f * dt / Mathf.Max(slideTime, 0.001f));
            rect.localPosition = Vector3.Lerp(rect.localPosition, targetPosition, slide);
            textCurrent = Vector2.Lerp(textCurrent, textTarget, slide);
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, dt / Mathf.Max(fadeTime, 0.001f));

            // Two octaves of noise per image (a slow breathing plus a quicker flutter), each image out of phase.
            float t = Time.unscaledTime * flickerSpeed + seed;
            for (int i = 0; i < flicker.Length; i++)
            {
                float n = 0.65f * Mathf.PerlinNoise(t, 0.3f + i * 3.7f) + 0.35f * Mathf.PerlinNoise(t * 3.1f, 1.7f + i * 2.3f);
                Color c = flicker[i].color;
                c.a = Mathf.Clamp01(flickerBase[i] * (1f - flickerAmount + flickerAmount * 2f * n));
                flicker[i].color = c;
            }

            float textCentre = (textCurrent.x + textCurrent.y) * 0.5f;
            float textWidth = textCurrent.y - textCurrent.x;
            for (int i = 0; i < spans.Length; i++)
            {
                float padding = i < spanPadding.Length ? spanPadding[i] : 0f;
                spans[i].anchoredPosition = new Vector2(textCentre, spans[i].anchoredPosition.y);
                spans[i].sizeDelta = new Vector2(textWidth + padding * 2f, spans[i].sizeDelta.y);
            }

            if (uiSparks != null)
            {
                uiSparks.Width = textWidth * sparkWidth;
            }

            if (sparks == null)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.scale = new Vector3(textWidth * sparkWidth, shape.scale.y, shape.scale.z);

            // Particles ignore CanvasGroups: follow this highlight's alpha and every group above it (a dialog
            // dimming the list, a panel fading).
            if (sparksFade != null)
            {
                float alpha = group.alpha;
                foreach (CanvasGroup parent in parentGroups)
                {
                    alpha *= parent.alpha;
                    if (parent.ignoreParentGroups)
                    {
                        break;
                    }
                }

                sparksFade.Alpha = alpha;
            }
        }
    }
}
