using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kneel.Core
{
    // The loading screen between two levels: concept art of the level just left, full-bleed at 16:9 (letterboxed
    // on other aspects, never stretched), its one line low on the screen in Cormorant Garamond, and, only if the
    // wait runs long, one small ember. Nothing else: no name, no percentage, no tips. It sits just under
    // SceneLoader's black, which fades it in and out. Built at runtime; unscaled time throughout.
    public class LoadingScreen : MonoBehaviour
    {
        // The art's frame is a 2560 x 1440 reference; the line sits in its bottom band, inside the 90% safe area.
        public const float ReferenceWidth = 2560f;
        public const float ReferenceHeight = 1440f;
        public const float SafeMargin = 0.05f;
        public const float LineBaseline = 0.085f;

        private Canvas canvas;
        private RectTransform frame;
        private RawImage art;
        private TextMeshProUGUI line;
        private Image ember;
        private Image emberCore;
        private float shownAt;
        private float push;
        private float pushTime;
        private float emberAlpha;
        private bool emberWanted;

        public bool Visible => canvas != null && canvas.enabled;

        public static LoadingScreen Create(Transform parent)
        {
            var go = new GameObject("LoadingScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<LoadingScreen>();
            screen.Build();
            return screen;
        }

        private void Build()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Kneel.UI.SceneLoader.LoadingScreenSortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvas.enabled = false;

            // Letterbox bars are plain black.
            var back = new GameObject("Black", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(transform, false);
            Stretch((RectTransform)back.transform);
            back.GetComponent<Image>().color = Color.black;
            back.GetComponent<Image>().raycastTarget = false;

            // The 16:9 frame: the art fills it, the line and the ember are placed inside it.
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(AspectRatioFitter), typeof(RectMask2D));
            frameGo.transform.SetParent(transform, false);
            frame = (RectTransform)frameGo.transform;
            Stretch(frame);
            var fitter = frameGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = ReferenceWidth / ReferenceHeight;

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
            artGo.transform.SetParent(frame, false);
            Stretch((RectTransform)artGo.transform);
            art = artGo.GetComponent<RawImage>();
            art.raycastTarget = false;

            var lineGo = new GameObject("Line", typeof(RectTransform));
            lineGo.transform.SetParent(frame, false);
            line = lineGo.AddComponent<TextMeshProUGUI>();
            line.raycastTarget = false;
            line.alignment = TextAlignmentOptions.Bottom;
            line.textWrappingMode = TextWrappingModes.Normal;
            var lineRect = line.rectTransform;
            lineRect.anchorMin = new Vector2(0.2f, LineBaseline);
            lineRect.anchorMax = new Vector2(0.8f, LineBaseline + 0.12f);
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = Vector2.zero;
            lineRect.pivot = new Vector2(0.5f, 0f);

            // The ember: a soft spark round a hot point, low in the frame's safe corner.
            var emberGo = new GameObject("Ember", typeof(RectTransform), typeof(Image));
            emberGo.transform.SetParent(frame, false);
            ember = emberGo.GetComponent<Image>();
            ember.raycastTarget = false;
            var emberRect = (RectTransform)emberGo.transform;
            emberRect.anchorMin = emberRect.anchorMax = new Vector2(1f - SafeMargin - 0.01f, SafeMargin + 0.02f);
            emberRect.sizeDelta = new Vector2(22f, 22f);
            var coreGo = new GameObject("Core", typeof(RectTransform), typeof(Image));
            coreGo.transform.SetParent(emberGo.transform, false);
            emberCore = coreGo.GetComponent<Image>();
            emberCore.raycastTarget = false;
            ((RectTransform)coreGo.transform).sizeDelta = new Vector2(6f, 6f);
            SetEmber(0f);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void Show(LevelTransitionCatalog catalog, LevelTransitionCatalog.Seam seam, float artPush, float pushOver)
        {
            art.texture = seam != null ? seam.art : null;
            art.color = art.texture != null ? Color.white : Color.black;
            line.text = seam != null ? seam.line : string.Empty;
            if (catalog != null)
            {
                if (catalog.font != null)
                {
                    line.font = catalog.font;
                }

                line.color = catalog.lineColor;
                line.fontSize = catalog.lineSize;
                ember.sprite = catalog.ember;
                emberCore.sprite = catalog.ember;
            }

            line.characterSpacing = 2f;
            push = artPush;
            pushTime = Mathf.Max(pushOver, 0.1f);
            shownAt = Time.realtimeSinceStartup;
            emberWanted = false;
            emberAlpha = 0f;
            SetEmber(0f);
            art.rectTransform.localScale = Vector3.one;
            canvas.enabled = true;
        }

        public void Hide()
        {
            canvas.enabled = false;
            art.texture = null;
        }

        // The "still loading" cue, only when the wait runs past what the screen would have taken anyway.
        public void WantEmber(bool want) => emberWanted = want;

        private void Update()
        {
            if (Visible == false)
            {
                return;
            }

            float t = Time.realtimeSinceStartup - shownAt;

            // A very slow push in, eased so it never visibly starts or stops.
            float s = 1f + push * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (pushTime * 1.6f)));
            art.rectTransform.localScale = new Vector3(s, s, 1f);

            emberAlpha = Mathf.MoveTowards(emberAlpha, emberWanted ? 1f : 0f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) / 0.6f);
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(7.3f, t * 3.1f);
            SetEmber(emberAlpha * flicker);
        }

        private void SetEmber(float alpha)
        {
            ember.color = new Color(1f, 0.42f, 0.12f, 0.55f * alpha);
            emberCore.color = new Color(1f, 0.86f, 0.55f, alpha);
            ember.enabled = emberCore.enabled = alpha > 0.001f;
        }
    }
}
